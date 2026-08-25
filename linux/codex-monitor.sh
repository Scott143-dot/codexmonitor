#!/usr/bin/env bash
# ==========================================================
# Codex Monitor (Linux / Ubuntu / Docker Shell Client)
# ==========================================================

# 1. 寻找本地 auth.json
AUTH_PATH=""
for p in "$HOME/.codex/auth.json" "/root/.codex/auth.json" "${USERPROFILE}/.codex/auth.json"; do
    if [ -f "$p" ]; then
        AUTH_PATH="$p"
        break
    fi
done

if [ -z "$AUTH_PATH" ]; then
    echo "❌ 未检测到 ~/.codex/auth.json 凭据文件"
    exit 1
fi

# 2. 提取 token 与 account_id
ACCESS_TOKEN=$(grep -o '"access_token": *"[^"]*"' "$AUTH_PATH" | head -n1 | cut -d'"' -f4)
REFRESH_TOKEN=$(grep -o '"refresh_token": *"[^"]*"' "$AUTH_PATH" | head -n1 | cut -d'"' -f4)
ACCOUNT_ID=$(grep -o '"account_id": *"[^"]*"' "$AUTH_PATH" | head -n1 | cut -d'"' -f4)
ID_TOKEN=$(grep -o '"id_token": *"[^"]*"' "$AUTH_PATH" | head -n1 | cut -d'"' -f4)

# 3. 智能代理探测
CURL_PROXY_ARG=""
DETECTED_PROXY="${https_proxy:-${HTTPS_PROXY:-${http_proxy:-${HTTP_PROXY:-${all_proxy:-${ALL_PROXY:-${proxy_https:-${proxy_http}}}}}}}}"

if [ -n "$DETECTED_PROXY" ]; then
    case "$DETECTED_PROXY" in
        http://*|https://*|socks5://*) ;;
        *) DETECTED_PROXY="http://$DETECTED_PROXY" ;;
    esac
    CURL_PROXY_ARG="-x $DETECTED_PROXY"
fi

# 自动刷新 Token 函数
refresh_oauth_token() {
    if [ -z "$REFRESH_TOKEN" ]; then return 1; fi
    REF_BODY="{\"client_id\":\"app_EMoamEEZ73f0CkXaXp7hrann\",\"grant_type\":\"refresh_token\",\"refresh_token\":\"$REFRESH_TOKEN\"}"
    REF_RESP=$(curl -s --max-time 10 $CURL_PROXY_ARG -H "Content-Type: application/json" -d "$REF_BODY" "https://auth.openai.com/oauth/token" 2>/dev/null)
    NEW_TOK=$(echo "$REF_RESP" | grep -o '"access_token": *"[^"]*"' | head -n1 | cut -d'"' -f4)
    if [ -n "$NEW_TOK" ]; then
        ACCESS_TOKEN="$NEW_TOK"
        # 覆写回 auth.json
        sed -i "s/\"access_token\": *\"[^\"]*\"/\"access_token\": \"$NEW_TOK\"/g" "$AUTH_PATH" 2>/dev/null
        return 0
    fi
    return 1
}

# 4. 解析 JWT 提取邮箱与订阅信息
PARSE_TOK="${ID_TOKEN:-$ACCESS_TOKEN}"
PAYLOAD_B64=$(echo "$PARSE_TOK" | cut -d'.' -f2 | tr '_-' '/+')
REM=$((${#PAYLOAD_B64} % 4))
if [ $REM -eq 2 ]; then PAYLOAD_B64="${PAYLOAD_B64}=="; elif [ $REM -eq 3 ]; then PAYLOAD_B64="${PAYLOAD_B64}="; fi

JWT_JSON=$(echo "$PAYLOAD_B64" | base64 -d 2>/dev/null)
EMAIL=$(echo "$JWT_JSON" | grep -o '"email": *"[^"]*"' | head -n1 | cut -d'"' -f4)
[ -z "$EMAIL" ] && EMAIL="ChatGPT 用户"

PLAN_RAW=$(echo "$JWT_JSON" | grep -o '"chatgpt_plan_type": *"[^"]*"' | head -n1 | cut -d'"' -f4)
if [ -n "$PLAN_RAW" ]; then
    case "$PLAN_RAW" in
        *pro*|*Pro*) PLAN_TYPE="ChatGPT Pro" ;;
        *team*|*Team*) PLAN_TYPE="ChatGPT Team" ;;
        *) PLAN_TYPE="ChatGPT Plus" ;;
    esac
else
    PLAN_TYPE="ChatGPT Plus"
fi

EXPIRY=$(echo "$JWT_JSON" | grep -o '"chatgpt_subscription_active_until": *"[^"]*"' | head -n1 | cut -d'"' -f4 | cut -dT -f1)
[ -z "$EXPIRY" ] && EXPIRY="--"

HEADER_ACC=""
if [ -n "$ACCOUNT_ID" ]; then
    HEADER_ACC="-H \"chatgpt-account-id: $ACCOUNT_ID\""
fi

do_fetch() {
    curl -s --max-time 10 \
      $CURL_PROXY_ARG \
      -H "Authorization: Bearer $ACCESS_TOKEN" \
      -H "Origin: https://chatgpt.com" \
      -H "Referer: https://chatgpt.com/" \
      -H "User-Agent: Mozilla/5.0 (X11; Linux x86_64)" \
      -H "Accept: application/json" \
      $HEADER_ACC \
      "https://chatgpt.com/backend-api/wham/usage" 2>/dev/null
}

RESP=$(do_fetch)

# 如果 token 过期，自动刷新并重试！
if echo "$RESP" | grep -q "token_expired" || echo "$RESP" | grep -q "401"; then
    if refresh_oauth_token; then
        RESP=$(do_fetch)
    fi
fi

USED_PCT=$(echo "$RESP" | grep -o '"used_percent": *[0-9.]*' | head -n1 | grep -o '[0-9.]*' | cut -d'.' -f1)
RESET_SEC=$(echo "$RESP" | grep -o '"reset_after_seconds": *[0-9]*' | head -n1 | grep -o '[0-9]*')

REMAINING_PCT="--"
RESET_CD="--"

if echo "$RESP" | grep -q "token_expired"; then
    RESET_DT="Token 续期失败 (请在宿主机登录刷新)"
elif [ -n "$USED_PCT" ]; then
    REMAINING_PCT=$((100 - USED_PCT))
    [ $REMAINING_PCT -lt 0 ] && REMAINING_PCT=0
    [ $REMAINING_PCT -gt 100 ] && REMAINING_PCT=100
    RESET_DT="网络已同步"
else
    RESET_DT="正在连接网络 (请检查代理设置)"
fi

if [ -n "$RESET_SEC" ]; then
    DAYS=$((RESET_SEC / 86400))
    HOURS=$(((RESET_SEC % 86400) / 3600))
    MINS=$(((RESET_SEC % 3600) / 60))
    if [ $DAYS -gt 0 ]; then
        RESET_CD="${DAYS}d"
        RESET_DT="${DAYS}天 ${HOURS}小时后"
    elif [ $HOURS -gt 0 ]; then
        RESET_CD="${HOURS}h"
        RESET_DT="${HOURS}小时 ${MINS}分钟后"
    else
        RESET_CD="${MINS}m"
        RESET_DT="${MINS}分钟后"
    fi
fi

# 5. 渲染进度条 (兼顾 UTF-8 与 Docker ASCII 终端)
TOTAL=20
if [ "$REMAINING_PCT" != "--" ]; then
    FILLED=$(( (REMAINING_PCT * TOTAL) / 100 ))
    EMPTY=$(( TOTAL - FILLED ))
else
    FILLED=0
    EMPTY=$TOTAL
fi

BAR_FILL=$(head -c $FILLED < /dev/zero | tr '\0' '#')
BAR_EMPTY=$(head -c $EMPTY < /dev/zero | tr '\0' '-')

echo "=================================================="
echo "  ⚡ Codex Monitor (Linux Shell)"
echo "=================================================="
printf "  邮箱: %s\n" "$EMAIL"
printf "  计划: %s\n" "$PLAN_TYPE"
printf "  到期: %s\n" "$EXPIRY"
printf "  重置: %s\n" "$RESET_DT"
echo "--------------------------------------------------"
printf "  额度: %3s%% [%s%s] (%s)\n" "$REMAINING_PCT" "$BAR_FILL" "$BAR_EMPTY" "$RESET_CD"
echo "=================================================="
