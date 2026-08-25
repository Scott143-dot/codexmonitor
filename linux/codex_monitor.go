package main

import (
	"crypto/tls"
	"encoding/base64"
	"encoding/json"
	"flag"
	"fmt"
	"io/ioutil"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"strings"
	"time"
)

type TokenAuth struct {
	Tokens struct {
		AccessToken string `json:"access_token"`
		IDToken     string `json:"id_token"`
		AccountID   string `json:"account_id"`
	} `json:"tokens"`
	AccessToken string `json:"access_token"`
	IDToken     string `json:"id_token"`
	AccountID   string `json:"account_id"`
}

type UsageResponse struct {
	Email     string `json:"email"`
	PlanType  string `json:"plan_type"`
	RateLimit struct {
		PrimaryWindow struct {
			UsedPercent       float64 `json:"used_percent"`
			ResetAfterSeconds int     `json:"reset_after_seconds"`
		} `json:"primary_window"`
	} `json:"rate_limit"`
	Error struct {
		Message string `json:"message"`
		Code    string `json:"code"`
	} `json:"error"`
}

func findAuthPath() string {
	home, _ := os.UserHomeDir()
	candidates := []string{
		filepath.Join(home, ".codex", "auth.json"),
		filepath.Join(os.Getenv("HOME"), ".codex", "auth.json"),
		filepath.Join(os.Getenv("USERPROFILE"), ".codex", "auth.json"),
		"/root/.codex/auth.json",
	}
	for _, c := range candidates {
		if c != "" {
			if _, err := os.Stat(c); err == nil {
				return c
			}
		}
	}
	return ""
}

func queryUsage() {
	authPath := findAuthPath()
	if authPath == "" {
		fmt.Println("❌ 未检测到 ~/.codex/auth.json 凭据文件")
		return
	}

	bytes, err := ioutil.ReadFile(authPath)
	if err != nil {
		fmt.Printf("❌ 读取 auth.json 失败: %v\n", err)
		return
	}

	var auth TokenAuth
	_ = json.Unmarshal(bytes, &auth)

	accessToken := auth.Tokens.AccessToken
	if accessToken == "" {
		accessToken = auth.AccessToken
	}
	idToken := auth.Tokens.IDToken
	if idToken == "" {
		idToken = auth.IDToken
	}
	accountID := auth.Tokens.AccountID
	if accountID == "" {
		accountID = auth.AccountID
	}

	if accessToken == "" {
		fmt.Println("❌ auth.json 中未找到 access_token")
		return
	}

	email := "ChatGPT 用户"
	planType := "ChatGPT Plus"
	expiry := "--"

	parseTok := idToken
	if parseTok == "" {
		parseTok = accessToken
	}
	parts := strings.Split(parseTok, ".")
	if len(parts) >= 2 {
		b64 := parts[1]
		if rem := len(b64) % 4; rem > 0 {
			b64 += strings.Repeat("=", 4-rem)
		}
		if rawPayload, err := base64.URLEncoding.DecodeString(b64); err == nil {
			var pMap map[string]interface{}
			if err := json.Unmarshal(rawPayload, &pMap); err == nil {
				if e, ok := pMap["email"].(string); ok && e != "" {
					email = e
				}
				if authObj, ok := pMap["https://api.openai.com/auth"].(map[string]interface{}); ok {
					if pt, ok := authObj["chatgpt_plan_type"].(string); ok && pt != "" {
						if strings.Contains(strings.ToLower(pt), "pro") {
							planType = "ChatGPT Pro"
						} else if strings.Contains(strings.ToLower(pt), "team") {
							planType = "ChatGPT Team"
						} else {
							planType = "ChatGPT Plus"
						}
					}
					if until, ok := authObj["chatgpt_subscription_active_until"].(string); ok && len(until) >= 10 {
						expiry = until[:10]
					}
				}
			}
		}
	}

	// 智能代理自愈与 HTTP Transport 配置
	transport := &http.Transport{
		TLSClientConfig: &tls.Config{InsecureSkipVerify: true},
		Proxy:           http.ProxyFromEnvironment,
	}

	proxyEnv := os.Getenv("https_proxy")
	if proxyEnv == "" {
		proxyEnv = os.Getenv("HTTPS_PROXY")
	}
	if proxyEnv == "" {
		proxyEnv = os.Getenv("http_proxy")
	}
	if proxyEnv == "" {
		proxyEnv = os.Getenv("all_proxy")
	}
	if proxyEnv != "" {
		if !strings.HasPrefix(proxyEnv, "http://") && !strings.HasPrefix(proxyEnv, "https://") && !strings.HasPrefix(proxyEnv, "socks5://") {
			proxyEnv = "http://" + proxyEnv
		}
		if pURL, err := url.Parse(proxyEnv); err == nil {
			transport.Proxy = http.ProxyURL(pURL)
		}
	}

	client := &http.Client{
		Timeout:   10 * time.Second,
		Transport: transport,
	}

	req, _ := http.NewRequest("GET", "https://chatgpt.com/backend-api/wham/usage", nil)
	req.Header.Set("Authorization", "Bearer "+accessToken)
	req.Header.Set("User-Agent", "Mozilla/5.0 (X11; Linux x86_64)")
	req.Header.Set("Accept", "application/json")
	req.Header.Set("Origin", "https://chatgpt.com")
	req.Header.Set("Referer", "https://chatgpt.com/")
	if accountID != "" {
		req.Header.Set("chatgpt-account-id", accountID)
	}

	resp, err := client.Do(req)

	remainingPctStr := "--"
	resetCd := "--"
	resetDt := "正在连接网络 (请检查代理设置)"
	pctVal := 0.0

	if err == nil {
		defer resp.Body.Close()
		body, _ := ioutil.ReadAll(resp.Body)
		var uResp UsageResponse
		if err := json.Unmarshal(body, &uResp); err == nil {
			if uResp.Error.Code == "token_expired" {
				resetDt = "Token 已过期 (请重新登录更新 auth.json)"
			} else if resp.StatusCode == 200 {
				if uResp.Email != "" {
					email = uResp.Email
				}
				pctVal = 100.0 - uResp.RateLimit.PrimaryWindow.UsedPercent
				if pctVal < 0 {
					pctVal = 0
				}
				if pctVal > 100 {
					pctVal = 100
				}
				remainingPctStr = fmt.Sprintf("%d", int(pctVal))

				sec := uResp.RateLimit.PrimaryWindow.ResetAfterSeconds
				days := sec / 86400
				hours := (sec % 86400) / 3600
				mins := (sec % 3600) / 60

				if days > 0 {
					resetCd = fmt.Sprintf("%dd", days)
					resetDt = fmt.Sprintf("%d天 %d小时后", days, hours)
				} else if hours > 0 {
					resetCd = fmt.Sprintf("%dh", hours)
					resetDt = fmt.Sprintf("%d小时 %d分钟后", hours, mins)
				} else {
					resetCd = fmt.Sprintf("%dm", mins)
					resetDt = fmt.Sprintf("%d分钟后", mins)
				}
			}
		}
	}

	// 渲染进度条 (兼顾 ANSI 终端与标准 ASCII)
	totalBlocks := 20
	filled := 0
	if remainingPctStr != "--" {
		filled = int((pctVal / 100.0) * float64(totalBlocks))
	}
	empty := totalBlocks - filled

	barFill := strings.Repeat("#", filled)
	barEmpty := strings.Repeat("-", empty)

	fmt.Println("==================================================")
	fmt.Println("  ⚡ Codex Monitor (Linux Standalone ELF Binary)")
	fmt.Println("==================================================")
	fmt.Printf("  邮箱: %s\n", email)
	fmt.Printf("  计划: %s\n", planType)
	fmt.Printf("  到期: %s\n", expiry)
	fmt.Printf("  重置: %s\n", resetDt)
	fmt.Println("--------------------------------------------------")
	fmt.Printf("  额度: %3s%% [%s%s] (%s)\n", remainingPctStr, barFill, barEmpty, resetCd)
	fmt.Println("==================================================")
}

func main() {
	watchFlag := flag.Bool("watch", false, "实时守护监控模式 (每 60 秒自动刷新)")
	flag.BoolVar(watchFlag, "w", false, "实时守护监控模式 (缩写)")
	flag.Parse()

	if *watchFlag {
		fmt.Print("\033[2J\033[H")
		for {
			fmt.Print("\033[H")
			queryUsage()
			fmt.Println("\n(实时监控中... 每 60 秒刷新一次，按 Ctrl+C 退出)")
			time.Sleep(60 * time.Second)
		}
	} else {
		queryUsage()
	}
}
