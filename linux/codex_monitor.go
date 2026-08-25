package main

import (
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io/ioutil"
	"net/http"
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
}

func findAuthPath() string {
	home, _ := os.UserHomeDir()
	candidates := []string{
		filepath.Join(home, ".codex", "auth.json"),
		filepath.Join(os.Getenv("HOME"), ".codex", "auth.json"),
		"/root/.codex/auth.json",
	}
	for _, c := range candidates {
		if _, err := os.Stat(c); err == nil {
			return c
		}
	}
	return ""
}

func main() {
	authPath := findAuthPath()
	if authPath == "" {
		fmt.Println("\033[1;31m❌ 未检测到 ~/.codex/auth.json 凭据，请先在机器上登录 Codex\033[0m")
		return
	}

	bytes, err := ioutil.ReadFile(authPath)
	if err != nil {
		fmt.Printf("\033[1;31m❌ 读取 auth.json 失败: %v\033[0m\n", err)
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
		fmt.Println("\033[1;31m❌ auth.json 中未找到 access_token\033[0m")
		return
	}

	// 解析 JWT payload 提取邮箱与到期时间
	email := "已登录用户"
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
						planType = "ChatGPT " + strings.Title(pt)
					}
					if until, ok := authObj["chatgpt_subscription_active_until"].(string); ok && len(until) >= 10 {
						expiry = until[:10]
					}
				}
			}
		}
	}

	// 请求 OpenAI 官方用量
	req, _ := http.NewRequest("GET", "https://chatgpt.com/backend-api/wham/usage", nil)
	req.Header.Set("Authorization", "Bearer "+accessToken)
	req.Header.Set("User-Agent", "Mozilla/5.0 (X11; Linux x86_64)")
	req.Header.Set("Accept", "application/json")
	req.Header.Set("Origin", "https://chatgpt.com")
	req.Header.Set("Referer", "https://chatgpt.com/")
	if accountID != "" {
		req.Header.Set("chatgpt-account-id", accountID)
	}

	client := &http.Client{Timeout: 8 * time.Second}
	resp, err := client.Do(req)

	remainingPct := 100.0
	resetCd := "--"
	resetDt := "正在连接网络更新用量..."

	if err == nil && resp.StatusCode == 200 {
		defer resp.Body.Close()
		body, _ := ioutil.ReadAll(resp.Body)
		var uResp UsageResponse
		if err := json.Unmarshal(body, &uResp); err == nil {
			if uResp.Email != "" {
				email = uResp.Email
			}
			remainingPct = 100.0 - uResp.RateLimit.PrimaryWindow.UsedPercent
			if remainingPct < 0 {
				remainingPct = 0
			}

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

	// ANSI 彩色渐变进度条渲染
	filled := int((remainingPct / 100.0) * 24.0)
	empty := 24 - filled

	barFill1 := strings.Repeat("█", filled/2)
	barFill2 := strings.Repeat("█", filled-filled/2)
	barEmpty := strings.Repeat("░", empty)

	fmt.Println("\033[1;36m┌────────────────────────────────────────────────────────┐\033[0m")
	fmt.Println("\033[1;36m│\033[0m  \033[1;37m⚡ Codex Monitor (Linux 原生 Go 二进制版)\033[0m             \033[1;36m│\033[0m")
	fmt.Println("\033[1;36m├────────────────────────────────────────────────────────┤\033[0m")
	fmt.Printf("\033[1;36m│\033[0m  📧 账号: \033[1;32m%-42s\033[0m \033[1;36m│\033[0m\n", email)
	fmt.Printf("\033[1;36m│\033[0m  💎 类型: \033[1;35m%-42s\033[0m \033[1;36m│\033[0m\n", planType)
	fmt.Printf("\033[1;36m│\033[0m  📅 到期: \033[1;33m%-42s\033[0m \033[1;36m│\033[0m\n", expiry)
	fmt.Printf("\033[1;36m│\033[0m  ⏱️  重置: \033[1;34m%-42s\033[0m \033[1;36m│\033[0m\n", resetDt)
	fmt.Println("\033[1;36m├────────────────────────────────────────────────────────┤\033[0m")
	fmt.Printf("\033[1;36m│\033[0m  剩余额度: \033[1;37m%3d%%\033[0m [\033[38;2;56;189;248m%s\033[38;2;129;140;248m%s\033[90m%s\033[0m] (%-3s) \033[1;36m│\033[0m\n", int(remainingPct), barFill1, barFill2, barEmpty, resetCd)
	fmt.Println("\033[1;36m└────────────────────────────────────────────────────────┘\033[0m")
}
