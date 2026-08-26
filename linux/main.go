package main

import (
	"bytes"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"image"
	"image/color"
	"image/draw"
	"image/png"
	"io"
	"math"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"strings"
	"sync"
	"syscall"
	"time"

	"github.com/getlantern/systray"
)

type AuthInfo struct {
	AccessToken        string `json:"access_token"`
	RefreshToken       string `json:"refresh_token"`
	AccountID          string `json:"account_id"`
	Email              string
	PlanType           string
	SubscriptionExpiry string
	AuthPath           string
}

type UsageData struct {
	Percentage         float64
	PercentageStr      string
	ResetCountdown     string
	ResetDetail        string
	RefreshTime        string
	Success            bool
	Email              string
	PlanType           string
	SubscriptionExpiry string
}

var (
	currentData UsageData
	dataMutex   sync.RWMutex
	mStatus     *systray.MenuItem
	mEmail      *systray.MenuItem
	mPlan       *systray.MenuItem
	mExpiry     *systray.MenuItem
	mReset      *systray.MenuItem
	mRefreshAt  *systray.MenuItem
	mAutostart  *systray.MenuItem
	mRefresh    *systray.MenuItem
	mQuit       *systray.MenuItem
)

// 使用 Go 默认 Transport；如果进程环境中设置了 HTTP_PROXY/HTTPS_PROXY/ALL_PROXY，
// 请求会自动沿用这些代理配置，并统一使用相同的请求超时。
var apiHTTPClient = &http.Client{Timeout: 15 * time.Second}

func findAuthJson() string {
	candidates := []string{
		os.Getenv("CODEX_AUTH_PATH"),
		filepath.Join(os.Getenv("CODEX_HOME"), "auth.json"),
		filepath.Join(os.Getenv("HOME"), ".codex", "auth.json"),
		filepath.Join(os.Getenv("HOME"), ".cc-switch", "current", "auth.json"),
		filepath.Join(os.Getenv("HOME"), ".cc-switch", "auth.json"),
		filepath.Join(os.Getenv("HOME"), ".config", "cc-switch", "auth.json"),
		"/config/.codex/auth.json",
		"/root/.codex/auth.json",
		"/root/.cc-switch/current/auth.json",
		"/root/.cc-switch/auth.json",
		"/root/.config/cc-switch/auth.json",
		filepath.Join(os.Getenv("USERPROFILE"), ".codex", "auth.json"),
	}

	for _, c := range candidates {
		if c != "" {
			if fi, err := os.Stat(c); err == nil && !fi.IsDir() {
				if realP, err := filepath.EvalSymlinks(c); err == nil {
					return realP
				}
				return c
			}
		}
	}
	return ""
}

func fetchLocalAuth() AuthInfo {
	info := AuthInfo{
		Email:              "未登录",
		PlanType:           "--",
		SubscriptionExpiry: "--",
	}

	p := findAuthJson()
	if p == "" {
		return info
	}
	info.AuthPath = p

	data, err := os.ReadFile(p)
	if err != nil {
		return info
	}

	var root map[string]interface{}
	if err := json.Unmarshal(data, &root); err != nil {
		return info
	}

	tokens, ok := root["tokens"].(map[string]interface{})
	if !ok {
		tokens = root
	}

	if acc, ok := tokens["access_token"].(string); ok {
		info.AccessToken = acc
	}
	if ref, ok := tokens["refresh_token"].(string); ok {
		info.RefreshToken = ref
	}
	if accId, ok := tokens["account_id"].(string); ok {
		info.AccountID = accId
	}

	idTok, _ := tokens["id_token"].(string)
	tok := idTok
	if tok == "" {
		tok = info.AccessToken
	}

	if strings.Contains(tok, ".") {
		parts := strings.Split(tok, ".")
		if len(parts) >= 2 {
			payloadSeg := parts[1]
			if rem := len(payloadSeg) % 4; rem != 0 {
				payloadSeg += strings.Repeat("=", 4-rem)
			}
			if decoded, err := base64.URLEncoding.DecodeString(payloadSeg); err == nil {
				var pMap map[string]interface{}
				if err := json.Unmarshal(decoded, &pMap); err == nil {
					if email, ok := pMap["email"].(string); ok && email != "" {
						info.Email = email
					} else if prof, ok := pMap["https://api.openai.com/profile"].(map[string]interface{}); ok {
						if em, ok := prof["email"].(string); ok {
							info.Email = em
						}
					}

					if authObj, ok := pMap["https://api.openai.com/auth"].(map[string]interface{}); ok {
						if pt, ok := authObj["chatgpt_plan_type"].(string); ok {
							ptLow := strings.ToLower(pt)
							if strings.Contains(ptLow, "pro") {
								info.PlanType = "ChatGPT Pro"
							} else if strings.Contains(ptLow, "plus") {
								info.PlanType = "ChatGPT Plus"
							} else if strings.Contains(ptLow, "team") {
								info.PlanType = "ChatGPT Team"
							} else {
								info.PlanType = "ChatGPT " + strings.Title(ptLow)
							}
						}
						if rawUntil, ok := authObj["chatgpt_subscription_active_until"].(string); ok && rawUntil != "" {
							if len(rawUntil) >= 16 {
								info.SubscriptionExpiry = strings.Replace(rawUntil[:16], "T", " ", 1)
							}
						}
					}
				}
			}
		}
	}

	if info.Email == "" || info.Email == "未登录" {
		info.Email = "ChatGPT 用户"
	}
	return info
}

func autoRefreshToken(info *AuthInfo) string {
	if info.RefreshToken == "" {
		return ""
	}

	reqBody, _ := json.Marshal(map[string]string{
		"client_id":     "app_EMoamEEZ73f0CkXaXp7hrann",
		"grant_type":    "refresh_token",
		"refresh_token": info.RefreshToken,
	})

	req, err := http.NewRequest("POST", "https://auth.openai.com/oauth/token", bytes.NewBuffer(reqBody))
	if err != nil {
		return ""
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("User-Agent", "Mozilla/5.0 (X11; Linux x86_64)")

	resp, err := apiHTTPClient.Do(req)
	if err != nil || resp == nil {
		return ""
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return ""
	}

	var res map[string]interface{}
	if err := json.NewDecoder(resp.Body).Decode(&res); err != nil {
		return ""
	}

	newAcc, _ := res["access_token"].(string)
	newRef, _ := res["refresh_token"].(string)

	if newAcc != "" && info.AuthPath != "" {
		if raw, err := os.ReadFile(info.AuthPath); err == nil {
			var fileData map[string]interface{}
			if err := json.Unmarshal(raw, &fileData); err == nil {
				if tokens, ok := fileData["tokens"].(map[string]interface{}); ok {
					tokens["access_token"] = newAcc
					if newRef != "" {
						tokens["refresh_token"] = newRef
					}
				} else {
					fileData["access_token"] = newAcc
					if newRef != "" {
						fileData["refresh_token"] = newRef
					}
				}
				if updated, err := json.MarshalIndent(fileData, "", "  "); err == nil {
					_ = os.WriteFile(info.AuthPath, updated, 0644)
				}
			}
		}
		info.AccessToken = newAcc
		return newAcc
	}
	return ""
}

func fetchUsageData() (data UsageData) {
	defer func() {
		// 记录本次请求结束时间，成功和失败都能在详情中看到最近刷新时间。
		data.RefreshTime = time.Now().Format("2006-01-02 15:04:05")
	}()

	auth := fetchLocalAuth()
	data = UsageData{
		Percentage:         0.0,
		PercentageStr:      "--",
		ResetCountdown:     "--",
		ResetDetail:        "正在同步用量...",
		Email:              auth.Email,
		PlanType:           auth.PlanType,
		SubscriptionExpiry: auth.SubscriptionExpiry,
	}

	if auth.AccessToken == "" {
		if newTok := autoRefreshToken(&auth); newTok != "" {
			auth.AccessToken = newTok
		} else {
			data.PercentageStr = "--"
			data.ResetDetail = "本地凭据缺失或已失效，请重新登录"
			return data
		}
	}

	makeReq := func(token string) (*http.Response, error) {
		req, err := http.NewRequest("GET", "https://chatgpt.com/backend-api/wham/usage", nil)
		if err != nil {
			return nil, err
		}
		req.Header.Set("Authorization", "Bearer "+token)
		req.Header.Set("User-Agent", "Mozilla/5.0 (X11; Linux x86_64)")
		req.Header.Set("Accept", "application/json")
		req.Header.Set("Origin", "https://chatgpt.com")
		req.Header.Set("Referer", "https://chatgpt.com/")
		if auth.AccountID != "" {
			req.Header.Set("chatgpt-account-id", auth.AccountID)
		}
		return apiHTTPClient.Do(req)
	}

	resp, err := makeReq(auth.AccessToken)
	if err == nil && resp != nil && resp.StatusCode == http.StatusUnauthorized {
		_ = resp.Body.Close()
		if newTok := autoRefreshToken(&auth); newTok != "" {
			resp, err = makeReq(newTok)
		} else {
			data.ResetDetail = "登录凭据已过期或刷新失败，请重新登录"
			return data
		}
	}

	if err != nil {
		data.ResetDetail = networkErrorDetail(err)
		return data
	}
	if resp == nil {
		data.ResetDetail = "网络连接失败，请检查系统代理"
		return data
	}
	if resp.StatusCode != http.StatusOK {
		data.ResetDetail = httpStatusDetail(resp.StatusCode)
		_ = resp.Body.Close()
		return data
	}
	defer resp.Body.Close()

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		data.ResetDetail = "读取额度接口失败，请检查网络代理"
		return data
	}
	var res map[string]interface{}
	if err := json.Unmarshal(body, &res); err != nil {
		data.ResetDetail = "额度接口返回格式异常"
		return data
	}

	rateLimit, _ := res["rate_limit"].(map[string]interface{})
	primary, _ := rateLimit["primary_window"].(map[string]interface{})
	if primary == nil {
		data.ResetDetail = "额度接口缺少 rate_limit 数据"
		return data
	}
	usedPct, _ := primary["used_percent"].(float64)

	pctVal := 100.0 - usedPct
	if pctVal < 0 {
		pctVal = 0
	}
	if pctVal > 100 {
		pctVal = 100
	}

	data.Percentage = pctVal
	data.PercentageStr = fmt.Sprintf("%d%%", int(pctVal))

	secFloat, _ := primary["reset_after_seconds"].(float64)
	sec := int(secFloat)
	days := sec / 86400
	hours := (sec % 86400) / 3600
	mins := (sec % 3600) / 60

	if days > 0 {
		data.ResetCountdown = fmt.Sprintf("%dd", days)
		data.ResetDetail = fmt.Sprintf("%d天 %d小时后", days, hours)
	} else if hours > 0 {
		data.ResetCountdown = fmt.Sprintf("%dh", hours)
		data.ResetDetail = fmt.Sprintf("%d小时 %d分钟后", hours, mins)
	} else {
		data.ResetCountdown = fmt.Sprintf("%dm", mins)
		data.ResetDetail = fmt.Sprintf("%d分钟后", mins)
	}

	data.Success = true
	return data
}

func networkErrorDetail(err error) string {
	if err == nil {
		return "网络连接失败，请检查系统代理"
	}
	msg := err.Error()
	lower := strings.ToLower(msg)
	if strings.Contains(lower, "proxy") {
		return "代理连接失败，请检查 HTTPS_PROXY/系统代理"
	}
	if strings.Contains(lower, "certificate") || strings.Contains(lower, "tls") {
		return "TLS 证书连接失败，请检查系统时间或代理证书"
	}
	if len(msg) > 96 {
		msg = msg[:96] + "..."
	}
	return "网络连接失败: " + msg
}

func httpStatusDetail(status int) string {
	switch status {
	case http.StatusUnauthorized:
		return "登录凭据已过期，请重新登录"
	case http.StatusForbidden:
		return "接口拒绝访问 (403)，请检查账号或代理"
	case http.StatusTooManyRequests:
		return "请求过于频繁，请稍后重试"
	case http.StatusBadGateway, http.StatusServiceUnavailable, http.StatusGatewayTimeout:
		return fmt.Sprintf("服务暂时不可用 (%d)，请稍后重试", status)
	default:
		return fmt.Sprintf("额度接口返回 HTTP %d", status)
	}
}

func generateTrayIcon(pct float64) []byte {
	const size = 64
	img := image.NewRGBA(image.Rect(0, 0, size, size))

	// 透明背景
	draw.Draw(img, img.Bounds(), &image.Uniform{color.Transparent}, image.Point{}, draw.Src)

	cx, cy := 32.0, 32.0
	rOuter := 30.0
	rInner := 23.0
	cTrack := color.RGBA{28, 33, 46, 255}      // 暗灰底轨
	cProgress := color.RGBA{56, 189, 248, 255} // 亮青蓝发光进度

	// 1. 顺时针进度角度上限 (从 12 点钟 0° 开始)
	limitAngle := (pct / 100.0) * 360.0

	for y := 0; y < size; y++ {
		for x := 0; x < size; x++ {
			dx := float64(x) - cx
			dy := float64(y) - cy
			dist := dx*dx + dy*dy

			// 处于圆环带内
			if dist <= rOuter*rOuter && dist >= rInner*rInner {
				// 计算相对于 12 点钟方向顺时针的夹角 [0, 360)
				angleRad := math.Atan2(dx, -dy)
				angleDeg := angleRad * (180.0 / math.Pi)
				if angleDeg < 0 {
					angleDeg += 360.0
				}

				if angleDeg <= limitAngle {
					img.Set(x, y, cProgress)
				} else {
					img.Set(x, y, cTrack)
				}
			}
		}
	}

	// 2. 在中央绘制高对比度的大号白色数字矩阵
	txt := fmt.Sprintf("%d", int(pct))
	drawDigits(img, txt, int(cx), int(cy))

	var buf bytes.Buffer
	_ = png.Encode(&buf, img)
	return buf.Bytes()
}

// 纯 Go 自带 5x7 大号点阵字体渲染
var font5x7 = map[rune][]string{
	'0': {"1111", "1001", "1001", "1001", "1001", "1001", "1111"},
	'1': {"0010", "0110", "0010", "0010", "0010", "0010", "0111"},
	'2': {"1111", "0001", "0001", "1111", "1000", "1000", "1111"},
	'3': {"1111", "0001", "0001", "1111", "0001", "0001", "1111"},
	'4': {"1001", "1001", "1001", "1111", "0001", "0001", "0001"},
	'5': {"1111", "1000", "1000", "1111", "0001", "0001", "1111"},
	'6': {"1111", "1000", "1000", "1111", "1001", "1001", "1111"},
	'7': {"1111", "0001", "0001", "0010", "0100", "0100", "0100"},
	'8': {"1111", "1001", "1001", "1111", "1001", "1001", "1111"},
	'9': {"1111", "1001", "1001", "1111", "0001", "0001", "1111"},
	'%': {"1001", "1010", "0100", "0100", "0010", "1001", "1001"},
}

func drawDigits(img *image.RGBA, s string, centerX, centerY int) {
	scale := 2 // 放大2倍
	charW := 4 * scale
	charH := 7 * scale
	spacing := 2 * scale
	totalW := len(s)*charW + (len(s)-1)*spacing
	startX := centerX - totalW/2
	startY := centerY - charH/2

	cWhite := color.RGBA{255, 255, 255, 255}

	curX := startX
	for _, ch := range s {
		if matrix, ok := font5x7[ch]; ok {
			for rowIdx, rowStr := range matrix {
				for colIdx, colChar := range rowStr {
					if colChar == '1' {
						for dy := 0; dy < scale; dy++ {
							for dx := 0; dx < scale; dx++ {
								img.Set(curX+colIdx*scale+dx, startY+rowIdx*scale+dy, cWhite)
							}
						}
					}
				}
			}
		}
		curX += charW + spacing
	}
}

func getAutostartDesktopPath() string {
	home := os.Getenv("HOME")
	if home == "" {
		home = "/root"
	}
	return filepath.Join(home, ".config", "autostart", "codex-monitor.desktop")
}

func isAutostartEnabled() bool {
	p := getAutostartDesktopPath()
	_, err := os.Stat(p)
	return err == nil
}

func toggleAutostart() bool {
	p := getAutostartDesktopPath()
	if isAutostartEnabled() {
		_ = os.Remove(p)
		return false
	}

	execPath, err := os.Executable()
	if err != nil {
		execPath = os.Args[0]
	}
	if absP, err := filepath.Abs(execPath); err == nil {
		execPath = absP
	}

	_ = os.MkdirAll(filepath.Dir(p), 0755)
	content := fmt.Sprintf(`[Desktop Entry]
Type=Application
Name=Codex Monitor
Exec=%s -d
Icon=utilities-system-monitor
Terminal=false
X-GNOME-Autostart-enabled=true
`, execPath)

	_ = os.WriteFile(p, []byte(content), 0644)
	return true
}

func updateAutostartMenuItem() {
	if mAutostart == nil {
		return
	}
	if isAutostartEnabled() {
		mAutostart.SetTitle("✅ 开机自启动 (已开启)")
	} else {
		mAutostart.SetTitle("⬜ 开机自启动 (未开启)")
	}
}

func onReady() {
	systray.SetTitle("--")
	systray.SetTooltip("Codex Monitor (Linux Native Standalone)")

	mStatus = systray.AddMenuItem("剩余额度: --", "")
	mStatus.Disable()
	systray.AddSeparator()

	mEmail = systray.AddMenuItem("📧 账号: --", "")
	mEmail.Disable()

	mPlan = systray.AddMenuItem("💎 类型: --", "")
	mPlan.Disable()

	mExpiry = systray.AddMenuItem("📅 到期: --", "")
	mExpiry.Disable()

	mReset = systray.AddMenuItem("⏱️ 重置: --", "")
	mReset.Disable()

	mRefreshAt = systray.AddMenuItem("🕒 最近刷新: --", "")
	mRefreshAt.Disable()

	systray.AddSeparator()
	mRefresh = systray.AddMenuItem("🔄 立即刷新", "立即拉取最新用量")
	mAutostart = systray.AddMenuItem("⬜ 开机自启动", "切换登录桌面时自动启动")
	updateAutostartMenuItem()

	systray.AddSeparator()
	mQuit = systray.AddMenuItem("❌ 退出", "退出 Codex Monitor")

	publishData := func(d UsageData) {
		refreshTime := d.RefreshTime
		if refreshTime == "" {
			refreshTime = "--"
		}

		systray.SetTitle(fmt.Sprintf("%s (%s)", d.PercentageStr, d.ResetCountdown))
		systray.SetTooltip(fmt.Sprintf("Codex: %s (重置: %s)", d.PercentageStr, d.ResetCountdown))
		systray.SetIcon(generateTrayIcon(d.Percentage))

		mStatus.SetTitle(fmt.Sprintf("剩余额度: %s (倒计时: %s)", d.PercentageStr, d.ResetCountdown))
		mEmail.SetTitle(fmt.Sprintf("📧 账号: %s", d.Email))
		mPlan.SetTitle(fmt.Sprintf("💎 类型: %s", d.PlanType))
		mExpiry.SetTitle(fmt.Sprintf("📅 到期: %s", d.SubscriptionExpiry))
		mReset.SetTitle(fmt.Sprintf("⏱️ 重置: %s", d.ResetDetail))
		mRefreshAt.SetTitle(fmt.Sprintf("🕒 最近刷新: %s", refreshTime))
	}

	var refreshLock sync.Mutex
	updateData := func() {
		if !refreshLock.TryLock() {
			return
		}
		defer refreshLock.Unlock()

		// 刷新期间不改动托盘和详情内容，保持上一次有效状态，
		// 避免用户看到额度瞬间跳回默认值或出现网络请求提示。
		dataMutex.RLock()
		previous := currentData
		dataMutex.RUnlock()

		fresh := fetchUsageData()

		// 请求失败时继续保留额度和倒计时，只把错误显示在重置详情中。
		// 这样网络瞬断不会让托盘图标和百分比闪成默认值。
		if !fresh.Success && previous.PercentageStr != "" && previous.PercentageStr != "--" {
			fresh.Percentage = previous.Percentage
			fresh.PercentageStr = previous.PercentageStr
			fresh.ResetCountdown = previous.ResetCountdown
		}

		dataMutex.Lock()
		currentData = fresh
		dataMutex.Unlock()
		publishData(fresh)
	}

	go func() {
		go updateData()
		ticker := time.NewTicker(60 * time.Second)
		for {
			select {
			case <-ticker.C:
				go updateData()
			case <-mRefresh.ClickedCh:
				go updateData()
			case <-mAutostart.ClickedCh:
				toggleAutostart()
				updateAutostartMenuItem()
			case <-mQuit.ClickedCh:
				systray.Quit()
				os.Exit(0)
			}
		}
	}()
}

func onExit() {
	// 清理
}

func runCliDashboard(watch bool) {
	printData := func() {
		d := fetchUsageData()
		fmt.Print("\033[2J\033[H") // 清屏
		fmt.Println("==================================================")
		fmt.Println("  Codex Monitor (Linux Standalone ELF Binary)")
		fmt.Println("==================================================")
		fmt.Printf("  📧 账号: %s\n", d.Email)
		fmt.Printf("  💎 计划: %s\n", d.PlanType)
		fmt.Printf("  📅 到期: %s\n", d.SubscriptionExpiry)
		fmt.Printf("  ⏱️ 重置: %s\n", d.ResetDetail)
		fmt.Printf("  🕒 最近刷新: %s\n", d.RefreshTime)
		fmt.Println("--------------------------------------------------")
		barLen := 20
		filled := int((d.Percentage / 100.0) * float64(barLen))
		bar := strings.Repeat("#", filled) + strings.Repeat("-", barLen-filled)
		fmt.Printf("  额度: %4s [%s] (%s)\n", d.PercentageStr, bar, d.ResetCountdown)
		fmt.Println("==================================================")
		if watch {
			fmt.Println("  按 Ctrl+C 退出")
		}
	}

	printData()
	if watch {
		c := make(chan os.Signal, 1)
		signal.Notify(c, os.Interrupt, syscall.SIGTERM)
		ticker := time.NewTicker(60 * time.Second)
		for {
			select {
			case <-ticker.C:
				printData()
			case <-c:
				return
			}
		}
	}
}

func main() {
	args := os.Args[1:]
	for _, arg := range args {
		if arg == "--cli" || arg == "-c" {
			runCliDashboard(false)
			return
		}
		if arg == "--watch" || arg == "-w" {
			runCliDashboard(true)
			return
		}
		if arg == "--daemon" || arg == "-d" {
			// 后台自派生守护模式：脱离当前终端
			cmd := os.Args[0]
			var cmdArgs []string
			for _, a := range args {
				if a != "--daemon" && a != "-d" {
					cmdArgs = append(cmdArgs, a)
				}
			}
			attr := &os.ProcAttr{
				Files: []*os.File{nil, nil, nil}, // 重定向标准输入输出，彻底断开终端挂载
			}
			p, err := os.StartProcess(cmd, append([]string{cmd}, cmdArgs...), attr)
			if err == nil {
				_ = p.Release()
				fmt.Println("🚀 Codex Monitor 已在后台持久化运行 (关掉终端亦不退出)！")
				return
			}
		}
		if arg == "--help" || arg == "-h" {
			fmt.Println("Codex Monitor for Linux (100% Pure Go Native Binary)")
			fmt.Println("用法:")
			fmt.Println("  ./codex-monitor          启动状态栏托盘")
			fmt.Println("  ./codex-monitor -d       后台守护启动 (关掉终端不退出)")
			fmt.Println("  ./codex-monitor --cli    单次输出终端彩色仪表盘")
			fmt.Println("  ./codex-monitor --watch  开启终端实时守护监控")
			return
		}
	}

	// 忽略 SIGHUP 终端挂断信号，防止关掉终端窗口时被杀死
	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, syscall.SIGHUP)

	// 默认启动状态栏托盘
	systray.Run(onReady, onExit)
}
