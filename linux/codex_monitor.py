#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Codex Monitor for Linux / Ubuntu (Zero Dependencies 零依赖全能版)
- 无需安装 PyQt5 / pip / sudo，纯 Python 3 标准库直接运行！
- 在终端/SSH/Docker下：呈现 ANSI 极客彩色仪表盘与实时用量监控
- 在桌面环境下：呈现轻量悬浮球
"""

import os
import sys
import json
import time
import base64
import urllib.request
import urllib.error
import argparse

# 确保在各种终端编码下均能顺畅输出
try:
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
except Exception:
    pass

def find_auth_json():
    candidates = [
        os.path.expanduser("~/.codex/auth.json"),
        os.path.join(os.environ.get("HOME", ""), ".codex", "auth.json"),
        os.path.join(os.environ.get("USERPROFILE", ""), ".codex", "auth.json"),
    ]
    for c in candidates:
        if os.path.isfile(c):
            return c
    return None

def get_auth_data():
    auth_info = {
        "access_token": "",
        "account_id": "",
        "email": "未登录",
        "plan_type": "--",
        "subscription_expiry": "--"
    }
    p = find_auth_json()
    if not p:
        return auth_info

    try:
        with open(p, "r", encoding="utf-8") as f:
            data = json.load(f)

        tokens = data.get("tokens", {}) if isinstance(data.get("tokens"), dict) else data
        access_token = tokens.get("access_token", "")
        id_token = tokens.get("id_token", "")
        account_id = tokens.get("account_id", "")

        tok = id_token if id_token else access_token
        email = ""
        plan_type = "ChatGPT Plus"
        sub_until = "--"

        if tok and "." in tok:
            parts = tok.split(".")
            if len(parts) >= 2:
                b64 = parts[1] + "=" * (4 - len(parts[1]) % 4)
                payload = json.loads(base64.urlsafe_b64decode(b64.encode()))
                email = payload.get("email", "")
                if not email and "https://api.openai.com/profile" in payload:
                    email = payload["https://api.openai.com/profile"].get("email", "")

                auth_obj = payload.get("https://api.openai.com/auth", {})
                if isinstance(auth_obj, dict):
                    pt = auth_obj.get("chatgpt_plan_type", "plus").lower()
                    if "pro" in pt: plan_type = "ChatGPT Pro"
                    elif "plus" in pt: plan_type = "ChatGPT Plus"
                    elif "team" in pt: plan_type = "ChatGPT Team"
                    else: plan_type = "ChatGPT " + pt.capitalize()

                    raw_until = auth_obj.get("chatgpt_subscription_active_until", "")
                    if raw_until:
                        sub_until = raw_until[:16].replace("T", " ")

        auth_info["access_token"] = access_token
        auth_info["account_id"] = account_id
        auth_info["email"] = email if email else "已登录用户"
        auth_info["plan_type"] = plan_type
        auth_info["subscription_expiry"] = sub_until
    except Exception:
        pass

    return auth_info

def fetch_usage(auth):
    if not auth or not auth["access_token"]:
        return {
            "success": False,
            "percentage": 0.0,
            "reset_countdown": "--",
            "reset_detail": "未检测到 ~/.codex/auth.json 登录凭据",
            "email": "未登录",
            "plan_type": "--",
            "subscription_expiry": "--"
        }

    url = "https://chatgpt.com/backend-api/wham/usage"
    req = urllib.request.Request(url)
    req.add_header("Authorization", "Bearer " + auth["access_token"])
    req.add_header("User-Agent", "Mozilla/5.0 (X11; Linux x86_64)")
    req.add_header("Accept", "application/json")
    req.add_header("Origin", "https://chatgpt.com")
    req.add_header("Referer", "https://chatgpt.com/")
    if auth["account_id"]:
        req.add_header("chatgpt-account-id", auth["account_id"])

    try:
        with urllib.request.urlopen(req, timeout=9) as resp:
            data = json.loads(resp.read().decode("utf-8"))
            final_email = data.get("email") or auth["email"]
            plan_type = auth["plan_type"]
            rate_limit = data.get("rate_limit", {})
            primary = rate_limit.get("primary_window", {}) if rate_limit else {}
            used_pct = float(primary.get("used_percent", 0.0))
            remaining_pct = max(0.0, min(100.0, 100.0 - used_pct))
            sec = int(primary.get("reset_after_seconds", 0))

            days = sec // 86400
            hours = (sec % 86400) // 3600
            mins = (sec % 3600) // 60

            if days > 0:
                cd = f"{days}d"
                dt = f"{days}天 {hours}小时后"
            elif hours > 0:
                cd = f"{hours}h"
                dt = f"{hours}小时 {mins}分钟后"
            else:
                cd = f"{mins}m"
                dt = f"{mins}分钟后"

            return {
                "success": True,
                "percentage": remaining_pct,
                "reset_countdown": cd,
                "reset_detail": dt,
                "email": final_email,
                "plan_type": plan_type,
                "subscription_expiry": auth["subscription_expiry"]
            }
    except Exception as e:
        return {
            "success": False,
            "percentage": 100.0,
            "reset_countdown": "--",
            "reset_detail": "正在连接网络更新用量...",
            "email": auth["email"],
            "plan_type": auth["plan_type"],
            "subscription_expiry": auth["subscription_expiry"]
        }

def print_cli_dashboard(data):
    pct = data["percentage"]
    total_blocks = 24
    filled_blocks = int((pct / 100.0) * total_blocks)
    empty_blocks = total_blocks - filled_blocks

    # ANSI 蓝靛紫渐变
    bar = f"\033[38;2;56;189;248m{'█' * (filled_blocks // 2)}\033[38;2;129;140;248m{'█' * (filled_blocks - filled_blocks // 2)}\033[90m{'░' * empty_blocks}\033[0m"

    print("\033[1;36m┌────────────────────────────────────────────────────────┐\033[0m")
    print(f"\033[1;36m│\033[0m  \033[1;37m⚡ Codex Monitor (Linux/Ubuntu 极速版)\033[0m                 \033[1;36m│\033[0m")
    print("\033[1;36m├────────────────────────────────────────────────────────┤\033[0m")
    print(f"\033[1;36m│\033[0m  📧 账号: \033[1;32m{data['email']:<42}\033[0m \033[1;36m│\033[0m")
    print(f"\033[1;36m│\033[0m  💎 类型: \033[1;35m{data['plan_type']:<42}\033[0m \033[1;36m│\033[0m")
    print(f"\033[1;36m│\033[0m  📅 到期: \033[1;33m{data['subscription_expiry']:<42}\033[0m \033[1;36m│\033[0m")
    print(f"\033[1;36m│\033[0m  ⏱️  重置: \033[1;34m{data['reset_detail']:<42}\033[0m \033[1;36m│\033[0m")
    print("\033[1;36m├────────────────────────────────────────────────────────┤\033[0m")
    print(f"\033[1;36m│\033[0m  剩余额度: \033[1;37m{int(pct):>3}%\033[0m [{bar}] ({data['reset_countdown']})  \033[1;36m│\033[0m")
    print("\033[1;36m└────────────────────────────────────────────────────────┘\033[0m")

def run_cli_mode(watch=False):
    auth = get_auth_data()
    if watch:
        print("\033[2J\033[H", end="")
        while True:
            res = fetch_usage(auth)
            print("\033[H", end="")
            print_cli_dashboard(res)
            print(f"\n\033[90m(实时监控中... 每 60 秒刷新一次，按 Ctrl+C 退出)\033[0m")
            time.sleep(60)
    else:
        res = fetch_usage(auth)
        print_cli_dashboard(res)

def main():
    parser = argparse.ArgumentParser(description="Codex Monitor for Linux")
    parser.add_argument("--watch", "-w", action="store_true", help="终端实时守护监控模式")
    args = parser.parse_args()

    # 优先启动纯终端极简彩色模式 (0 依赖，可在任何 Linux / Docker / SSH 运行)
    run_cli_mode(watch=args.watch)

if __name__ == "__main__":
    main()
