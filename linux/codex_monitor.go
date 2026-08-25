package main

import (
	"bytes"
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
	AuthMode string `json:"auth_mode"`
	Tokens   struct {
		IDToken      string `json:"id_token"`
		AccessToken  string `json:"access_token"`
		RefreshToken string `json:"refresh_token"`
		AccountID    string `json:"account_id"`
	} `json:"tokens"`
	IDToken      string `json:"id_token"`
	AccessToken  string `json:"access_token"`
	RefreshToken string `json:"refresh_token"`
	AccountID    string `json:"account_id"`
	LastRefresh  string `json:"last_refresh,omitempty"`
}

type RefreshResponse struct {
	AccessToken  string `json:"access_token"`
	IDToken      string `json:"id_token"`
	RefreshToken string `json:"refresh_token"`
	ExpiresIn    int    `json:"expires_in"`
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

func getHTTPClient() *http.Client {
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

	return &http.Client{
		Timeout:   12 * time.Second,
		Transport: transport,
	}
}

// 自动 OAuth Refresh 续期并写回 auth.json
func autoRefreshToken(client *http.Client, authPath string, rawAuth *TokenAuth) bool {
	refreshToken := rawAuth.Tokens.RefreshToken
	if refreshToken == "" {
		refreshToken = rawAuth.RefreshToken
	}
	if refreshToken == "" {
		return false
	}

	clientID := "app_EMoamEEZ73f0CkXaXp7hrann"
	reqBody := map[string]string{
		"client_id":     clientID,
		"grant_type":    "refresh_token",
		"refresh_token": refreshToken,
	}
	b, _ := json.Marshal(reqBody)

	req, _ := http.NewRequest("POST", "https://auth.openai.com/oauth/token", bytes.NewReader(b))
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("User-Agent", "Mozilla/5.0 (X11; Linux x86_64)")

	resp, err := client.Do(req)
	if err != nil || resp.StatusCode != 200 {
		return false
	}
	defer resp.Body.Close()

	resBytes, _ := ioutil.ReadAll(resp.Body)
	var refResp RefreshResponse
	if err := json.Unmarshal(resBytes, &refResp); err != nil || refResp.AccessToken == "" {
		return false
	}

	// 更新内存与本地文件
	if rawAuth.Tokens.AccessToken != "" {
		rawAuth.Tokens.AccessToken = refResp.AccessToken
		if refResp.IDToken != "" {
			rawAuth.Tokens.IDToken = refResp.IDToken
		}
		if refResp.RefreshToken != "" {
			rawAuth.Tokens.RefreshToken = refResp.RefreshToken
		}
	} else {
		rawAuth.AccessToken = refResp.AccessToken
		if refResp.IDToken != "" {
			rawAuth.IDToken = refResp.IDToken
		}
		if refResp.RefreshToken != "" {
			rawAuth.RefreshToken = refResp.RefreshToken
		}
	}
	rawAuth.LastRefresh = time.Now().UTC().Format(time.RFC3339Nano)

	if updatedData, err := json.MarshalIndent(rawAuth, "", "  "); err == nil {
		_ = ioutil.WriteFile(authPath, updatedData, 0644)
	}

	return true
}

func queryUsage() {
	authPath := findAuthPath()
	if authPath == "" {
		fmt.Println("❌ 未检测到 ~/.codex/auth.json 凭据文件")
		return
	}

	bytesData, err := ioutil.ReadFile(authPath)
	if err != nil {
		fmt.Printf("❌ 读取 auth.json 失败: %v\n", err)
		return
	}

	var auth TokenAuth
	_ = json.Unmarshal(bytesData, &auth)

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

	client := getHTTPClient()

	if accessToken == "" {
		// 尝试用 refresh_token 恢复
		if autoRefreshToken(client, authPath, &auth) {
			accessToken = auth.Tokens.AccessToken
			if accessToken == "" {
				accessToken = auth.AccessToken
			}
		}
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

	remainingPctStr := "--"
	resetCd := "--"
	resetDt := "正在连接网络..."
	pctVal := 0.0

	doRequest := func(tok string) (int, []byte) {
		req, _ := http.NewRequest("GET", "https://chatgpt.com/backend-api/wham/usage", nil)
		req.Header.Set("Authorization", "Bearer "+tok)
		req.Header.Set("User-Agent", "Mozilla/5.0 (X11; Linux x86_64)")
		req.Header.Set("Accept", "application/json")
		req.Header.Set("Origin", "https://chatgpt.com")
		req.Header.Set("Referer", "https://chatgpt.com/")
		if accountID != "" {
			req.Header.Set("chatgpt-account-id", accountID)
		}
		resp, err := client.Do(req)
		if err != nil {
			return 0, nil
		}
		defer resp.Body.Close()
		b, _ := ioutil.ReadAll(resp.Body)
		return resp.StatusCode, b
	}

	status, body := doRequest(accessToken)

	// 如果 401 或 token 过期，自动用 refresh_token 刷新并重试！
	if status == 401 || (status == 200 && strings.Contains(string(body), "token_expired")) {
		if autoRefreshToken(client, authPath, &auth) {
			accessToken = auth.Tokens.AccessToken
			if accessToken == "" {
				accessToken = auth.AccessToken
			}
			status, body = doRequest(accessToken)
		}
	}

	if status == 200 {
		var uResp UsageResponse
		if err := json.Unmarshal(body, &uResp); err == nil && uResp.Error.Code == "" {
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
