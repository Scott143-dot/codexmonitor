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
	Email              string
	PlanType           string
	SubscriptionExpiry string
}

var (
	currentData  UsageData
	dataMutex    sync.RWMutex
	mStatus      *systray.MenuItem
	mEmail       *systray.MenuItem
	mPlan        *systray.MenuItem
	mExpiry      *systray.MenuItem
	mReset       *systray.MenuItem
	mRefresh     *systray.MenuItem
	mQuit        *systray.MenuItem
)

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

	client := &http.Client{Timeout: 10 * time.Second}
	resp, err := client.Do(req)
	if err != nil || resp.StatusCode != 200 {
		return ""
	}
	defer resp.Body.Close()

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

func fetchUsageData() UsageData {
	auth := fetchLocalAuth()
	data := UsageData{
		Percentage:         100.0,
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
			data.ResetDetail = "未检测到本地凭据"
			return data
		}
	}

	makeReq := func(token string) (*http.Response, error) {
		req, _ := http.NewRequest("GET", "https://chatgpt.com/backend-api/wham/usage", nil)
		req.Header.Set("Authorization", "Bearer "+token)
		req.Header.Set("User-Agent", "Mozilla/5.0 (X11; Linux x86_64)")
		req.Header.Set("Accept", "application/json")
		req.Header.Set("Origin", "https://chatgpt.com")
		req.Header.Set("Referer", "https://chatgpt.com/")
		if auth.AccountID != "" {
			req.Header.Set("chatgpt-account-id", auth.AccountID)
		}
		client := &http.Client{Timeout: 9 * time.Second}
		return client.Do(req)
	}

	resp, err := makeReq(auth.AccessToken)
	if err != nil || (resp != nil && resp.StatusCode == 401) {
		if newTok := autoRefreshToken(&auth); newTok != "" {
			resp, err = makeReq(newTok)
		}
	}

	if err != nil || resp == nil || resp.StatusCode != 200 {
		data.ResetDetail = "正在连接网络..."
		return data
	}
	defer resp.Body.Close()

	body, _ := io.ReadAll(resp.Body)
	var res map[string]interface{}
	if err := json.Unmarshal(body, &res); err != nil {
		return data
	}

	rateLimit, _ := res["rate_limit"].(map[string]interface{})
	primary, _ := rateLimit["primary_window"].(map[string]interface{})
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

	return data
}

func generateTrayIcon(pct float64) []byte {
	const size = 64
	img := image.NewRGBA(image.Rect(0, 0, size, size))

	// 透明背景
	draw.Draw(img, img.Bounds(), &image.Uniform{color.Transparent}, image.Point{}, draw.Src)

	cx, cy := 32.0, 32.0
	rOuter := 30.0
	rInner := 23.0
	cTrack := color.RGBA{28, 33, 46, 255}       // 暗灰底轨
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

func onReady() {
	systray.SetTitle("⚡ --")
	systray.SetTooltip("Codex Monitor (Linux Native Standalone)")

	mStatus = systray.AddMenuItem("⚡ 剩余额度: --", "")
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

	systray.AddSeparator()
	mRefresh = systray.AddMenuItem("🔄 立即刷新", "立即拉取最新用量")
	mQuit = systray.AddMenuItem("❌ 退出", "退出 Codex Monitor")

	updateData := func() {
		d := fetchUsageData()
		dataMutex.Lock()
		currentData = d
		dataMutex.Unlock()

		systray.SetTitle(fmt.Sprintf("⚡ %s (%s)", d.PercentageStr, d.ResetCountdown))
		systray.SetTooltip(fmt.Sprintf("Codex: %s (重置: %s)", d.PercentageStr, d.ResetCountdown))
		systray.SetIcon(generateTrayIcon(d.Percentage))

		mStatus.SetTitle(fmt.Sprintf("⚡ 剩余额度: %s (倒计时: %s)", d.PercentageStr, d.ResetCountdown))
		mEmail.SetTitle(fmt.Sprintf("📧 账号: %s", d.Email))
		mPlan.SetTitle(fmt.Sprintf("💎 类型: %s", d.PlanType))
		mExpiry.SetTitle(fmt.Sprintf("📅 到期: %s", d.SubscriptionExpiry))
		mReset.SetTitle(fmt.Sprintf("⏱️ 重置: %s", d.ResetDetail))
	}

	go func() {
		updateData()
		ticker := time.NewTicker(60 * time.Second)
		for {
			select {
			case <-ticker.C:
				updateData()
			case <-mRefresh.ClickedCh:
				go updateData()
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
		fmt.Println("  ⚡ Codex Monitor (Linux Standalone ELF Binary)")
		fmt.Println("==================================================")
		fmt.Printf("  📧 账号: %s\n", d.Email)
		fmt.Printf("  💎 计划: %s\n", d.PlanType)
		fmt.Printf("  📅 到期: %s\n", d.SubscriptionExpiry)
		fmt.Printf("  ⏱️ 重置: %s\n", d.ResetDetail)
		fmt.Println("--------------------------------------------------")
		barLen := 20
		filled := int((d.Percentage / 100.0) * float64(barLen))
		bar := strings.Repeat("#", filled) + strings.Repeat("-", barLen-filled)
		fmt.Printf("  ⚡ 额度: %4s [%s] (%s)\n", d.PercentageStr, bar, d.ResetCountdown)
		fmt.Println("==================================================")
		if watch {
			fmt.Printf("  🕒 刷新时间: %s (按 Ctrl+C 退出)\n", time.Now().Format("15:04:05"))
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
