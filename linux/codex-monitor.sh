#!/usr/bin/env bash
# ==========================================================
# Codex Monitor (Pure Shell / Curl Version - 零 Python 依赖)
# 适用环境：任何 Linux、Ubuntu、Docker、Alpine 容器
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
    echo -e "\033[1;31m❌ 未检测到 ~/.codex/auth.json 凭据，请先在机器上登录 Codex\033[0m"
    exit 1
fi

# 2. 提取 access_token 和 account_id (纯 grep/sed/awk，不依赖 python/jq)
ACCESS_TOKEN=$(grep -o '"access_token": *"[^"]*"' "$AUTH_PATH" | head -n1 | cut -d'"' -f4)
ACCOUNT_ID=$(grep -o '"account_id": *"[^"]*"' "$AUTH_PATH" | head -n1 | cut -d'"' -f4)
ID_TOKEN=$(grep -o '"id_token": *"[^"]*"' "$AUTH_PATH" | head -n1 | cut -d'"' -f4)

if [ -z "$ACCESS_TOKEN" ]; then
    echo -e "\033[1;31m❌ auth.json 中未找到有效的 access_token\033[0m"
    exit 1
fi

# 3. 提取邮箱与计划 (通过 base64 解析 JWT payload)
PARSE_TOK="${ID_TOKEN:-$ACCESS_TOKEN}"
PAYLOAD_B64=$(echo "$PARSE_TOK" | cut -d'.' -f2 | tr '_-' '/+')
# 补齐 base64 padding
REM=$((${#PAYLOAD_B64} % 4))
if [ $REM -eq 2 ]; then PAYLOAD_B64="${PAYLOAD_B64}=="; elif [ $REM -eq 3 ]; then PAYLOAD_B64="${PAYLOAD_B64}="; fi

JWT_JSON=$(echo "$PAYLOAD_B64" | base64 -d 2>/dev/null)
EMAIL=$(echo "$JWT_JSON" | grep -o '"email": *"[^"]*"' | head -n1 | cut -d'"' -f4)
[ -z "$EMAIL" ] && EMAIL="已登录用户"

PLAN_TYPE=$(echo "$JWT_JSON" | grep -o '"chatgpt_plan_type": *"[^"]*"' | head -n1 | cut -d'"' -f4)
[ -z "$PLAN_TYPE" ] && PLAN_TYPE="Plus"
PLAN_TYPE="ChatGPT $(echo "$PLAN_TYPE" | awk '{print toupper(substr($0,1,1))substr($0,2)}')"

EXPIRY=$(echo "$JWT_JSON" | grep -o '"chatgpt_subscription_active_until": *"[^"]*"' | head -n1 | cut -d'"' -f4 | cut -dT -f1)
[ -z "$EXPIRY" ] && EXPIRY="--"

# 4. 通过 curl 请求 OpenAI 官方 API
HEADER_ACC=""
if [ -n "$ACCOUNT_ID" ]; then
    HEADER_ACC="-H \"chatgpt-account-id: $ACCOUNT_ID\""
fi

RESP=$(curl -s --max-time 8 \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Origin: https://chatgpt.com" \
  -H "Referer: https://chatgpt.com/" \
  -H "User-Agent: Mozilla/5.0 (X11; Linux x86_64)" \
  -H "Accept: application/json" \
  $HEADER_ACC \
  "https://chatgpt.com/backend-api/wham/usage")

USED_PCT=$(echo "$RESP" | grep -o '"used_percent": *[0-9]*' | head -n1 | grep -o '[0-9]*')
RESET_SEC=$(echo "$RESP" | grep -o '"reset_after_seconds": *[0-9]*' | head -n1 | grep -o '[0-9]*')

if [ -n "$USED_PCT" ]; then
    REMAINING_PCT=$((100 - USED_PCT))
    [ $REMAINING_PCT -lt 0 ] && REMAINING_PCT=0
else
    REMAINING_PCT="--"
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
else
    RESET_CD="--"
    RESET_DT="正在连接网络更新用量..."
fi

# 5. 渲染彩色 ANSI 极客终端卡片
FILLED=0
EMPTY=24
if [ "$REMAINING_PCT" != "--" ]; then
    FILLED=$(( (REMAINING_PCT * 24) / 100 ))
    EMPTY=$(( 24 - FILLED ))
fi

BAR_FILL1=$(head -c $((FILLED / 2)) < /dev/zero | tr '\0' '█')
BAR_FILL2=$(head -c $((FILLED - FILLED / 2)) < /dev/zero | tr '\0' '█')
BAR_EMPTY=$(head -c $EMPTY < /dev/zero | tr '\0' '░')

echo -e "\033[1;36m┌────────────────────────────────────────────────────────┐\033[0m"
echo -e "\033[1;36m│\033[0m  \033[1;37m⚡ Codex Monitor (Linux 零依赖 Shell 版)\033[0m             \033[1;36m│\033[0m"
echo -e "\033[1;36m├────────────────────────────────────────────────────────┤\033[0m"
printf "\033[1;36m│\033[0m  📧 账号: \033[1;32m%-42s\033[0m \033[1;36m│\033[0m\n" "$EMAIL"
printf "\033[1;36m│\033[0m  💎 类型: \033[1;35m%-42s\033[0m \033[1;36m│\033[0m\n" "$PLAN_TYPE"
printf "\033[1;36m│\033[0m  📅 到期: \033[1;33m%-42s\033[0m \033[1;36m│\033[0m\n" "$EXPIRY"
printf "\033[1;36m│\033[0m  ⏱️  重置: \033[1;34m%-42s\033[0m \033[1;36m│\033[0m\n" "$RESET_DT"
echo -e "\033[1;36m├────────────────────────────────────────────────────────┤\033[0m"
printf "\033[1;36m│\033[0m  剩余额度: \033[1;37m%3s%%\033[0m [\033[38;2;56;189;248m%s\033[38;2;129;140;248m%s\033[90m%s\033[0m] (%-3s) \033[1;36m│\033[0m\n" "$REMAINING_PCT" "$BAR_FILL1" "$BAR_FILL2" "$BAR_EMPTY" "$RESET_CD"
echo -e "\033[1;36m└────────────────────────────────────────────────────────┘\033[0m"
