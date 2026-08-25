#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Codex Monitor for Linux Desktop (GUI Floating Widget)
Linux 桌面原生悬浮球 (基于 Python 标准库 Tkinter，零外部依赖，即开即用)
"""

import os
import sys
import json
import time
import base64
import threading
import urllib.request
import urllib.error
import tkinter as tk
from tkinter import Menu

def find_auth_json():
    candidates = [
        os.path.expanduser("~/.codex/auth.json"),
        os.path.join(os.environ.get("HOME", ""), ".codex", "auth.json"),
        os.path.join(os.environ.get("USERPROFILE", ""), ".codex", "auth.json"),
        "/root/.codex/auth.json",
    ]
    for c in candidates:
        if os.path.isfile(c):
            return c
    return None

def fetch_local_auth():
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
        auth_info["email"] = email if email else "ChatGPT 用户"
        auth_info["plan_type"] = plan_type
        auth_info["subscription_expiry"] = sub_until
    except Exception:
        pass

    return auth_info

class ToolTip:
    def __init__(self, widget):
        self.widget = widget
        self.tip_window = None

    def show(self, text, x, y):
        if self.tip_window or not text:
            return
        self.tip_window = tw = tk.Toplevel(self.widget)
        tw.wm_overrideredirect(True)
        tw.wm_attributes("-topmost", True)
        tw.wm_geometry(f"+{x}+{y}")
        
        frame = tk.Frame(tw, bg="#12151E", bd=1, relief="solid", highlightbackground="#333A4A", highlightthickness=1)
        frame.pack()
        
        label = tk.Label(frame, text=text, justify="left", bg="#12151E", fg="#F1F5F9",
                         font=("Ubuntu", 9), padx=10, pady=8)
        label.pack()

    def hide(self):
        if self.tip_window:
            self.tip_window.destroy()
            self.tip_window = None

class CodexFloatingBall:
    def __init__(self):
        self.root = tk.Tk()
        self.root.title("Codex Monitor")
        self.root.geometry("72x72+300+300")
        self.root.overrideredirect(True)
        self.root.attributes("-topmost", True)
        
        # 尝试启用透明度 (X11 / Wayland)
        try:
            self.root.attributes("-alpha", 0.96)
        except Exception:
            pass

        self.theme = "slate" # slate (蓝靛紫), emerald (青碧翠), mono (白金钛)
        self.percentage = 100.0
        self.reset_countdown = "--"
        self.reset_detail = "正在同步用量..."
        self.email = "检测中..."
        self.plan_type = "--"
        self.subscription_expiry = "--"

        # 画布
        self.canvas = tk.Canvas(self.root, width=72, height=72, bg="#0E1016", highlightthickness=0)
        self.canvas.pack(fill="both", expand=True)

        self.tooltip = ToolTip(self.root)

        # 鼠标拖拽事件绑定
        self.drag_x = 0
        self.drag_y = 0
        self.canvas.bind("<Button-1>", self.on_press)
        self.canvas.bind("<B1-Motion>", self.on_drag)
        self.canvas.bind("<ButtonRelease-1>", self.on_release)
        self.canvas.bind("<Button-3>", self.show_context_menu)
        self.canvas.bind("<Enter>", self.on_enter)
        self.canvas.bind("<Leave>", self.on_leave)

        # 初始绘制与异步刷新
        self.draw_widget()
        self.refresh_async()

        # 60 秒定时自动同步
        self.schedule_sync()

    def schedule_sync(self):
        self.refresh_async()
        self.root.after(60000, self.schedule_sync)

    def on_press(self, event):
        self.drag_x = event.x
        self.drag_y = event.y
        self.tooltip.hide()

    def on_drag(self, event):
        x = self.root.winfo_x() + (event.x - self.drag_x)
        y = self.root.winfo_y() + (event.y - self.drag_y)
        self.root.geometry(f"+{x}+{y}")

    def on_release(self, event):
        # 如果单纯点击没有拖拽位移，则触发即刻刷新
        pass

    def on_enter(self, event):
        tip_text = f"账号: {self.email}\n类型: {self.plan_type}\n到期: {self.subscription_expiry}\n重置: {self.reset_detail}"
        x = self.root.winfo_rootx() - 20
        y = self.root.winfo_rooty() - 85
        self.tooltip.show(tip_text, x, y)

    def on_leave(self, event):
        self.tooltip.hide()

    def show_context_menu(self, event):
        menu = Menu(self.root, tearoff=0, bg="#12151E", fg="#F1F5F9", activebackground="#2563EB", activeforeground="#FFFFFF")
        menu.add_command(label="🔄 立即同步", command=self.refresh_async)
        
        theme_menu = Menu(menu, tearoff=0, bg="#12151E", fg="#F1F5F9", activebackground="#2563EB", activeforeground="#FFFFFF")
        theme_menu.add_command(label="🌊 蓝靛紫 (流光渐变)", command=lambda: self.set_theme("slate"))
        theme_menu.add_command(label="🍃 青碧翠 (流光渐变)", command=lambda: self.set_theme("emerald"))
        theme_menu.add_command(label="⚪ 白金钛 (流光渐变)", command=lambda: self.set_theme("mono"))
        menu.add_cascade(label="🎨 色彩主题", menu=theme_menu)
        
        menu.add_separator()
        menu.add_command(label="❌ 退出", command=self.root.destroy)
        menu.tk_popup(event.x_root, event.y_root)

    def set_theme(self, theme):
        self.theme = theme
        self.draw_widget()

    def draw_widget(self):
        self.canvas.delete("all")
        
        # 1. 深邃底板外边框
        self.canvas.create_oval(3, 3, 69, 69, fill="#12141C", outline="#252A38", width=1.5)
        
        # 2. 底槽灰色圆弧
        self.canvas.create_oval(7, 7, 65, 65, outline="#1E2330", width=4)

        # 3. 颜色主题色
        color_arc = "#38BDF8"
        if self.theme == "emerald":
            color_arc = "#34D399"
        elif self.theme == "mono":
            color_arc = "#E2E8F0"

        # 4. 用量圆弧进度条
        if self.percentage > 0:
            extent_deg = -(self.percentage / 100.0) * 359.9
            self.canvas.create_arc(7, 7, 65, 65, start=90, extent=extent_deg,
                                  outline=color_arc, width=4.5, style="arc")

        # 5. 核心百分比文字 (15pt Bold 居中)
        pct_str = f"{int(self.percentage)}%" if self.percentage > 0 else "--"
        self.canvas.create_text(36, 31, text=pct_str, fill="#FFFFFF", font=("Segoe UI", 13, "bold"))

        # 6. 倒计时文字 (8pt 居中)
        self.canvas.create_text(36, 48, text=self.reset_countdown, fill="#94A3B8", font=("Segoe UI", 8, "bold"))

    def refresh_async(self):
        threading.Thread(target=self._do_fetch, daemon=True).start()

    def _do_fetch(self):
        auth = fetch_local_auth()
        self.email = auth["email"]
        self.plan_type = auth["plan_type"]
        self.subscription_expiry = auth["subscription_expiry"]

        if not auth["access_token"]:
            self.percentage = 0.0
            self.reset_countdown = "--"
            self.reset_detail = "未检测到本地登录凭据"
            self.root.after(0, self.draw_widget)
            return

        # 智能代理适配
        proxy_url = os.environ.get("https_proxy") or os.environ.get("HTTPS_PROXY") or \
                    os.environ.get("http_proxy") or os.environ.get("HTTP_PROXY") or \
                    os.environ.get("all_proxy") or os.environ.get("ALL_PROXY")
        
        opener = urllib.request.build_opener()
        if proxy_url:
            if not proxy_url.startswith("http"):
                proxy_url = "http://" + proxy_url
            opener.add_handler(urllib.request.ProxyHandler({'http': proxy_url, 'https': proxy_url}))

        req = urllib.request.Request("https://chatgpt.com/backend-api/wham/usage")
        req.add_header("Authorization", "Bearer " + auth["access_token"])
        req.add_header("User-Agent", "Mozilla/5.0 (X11; Linux x86_64)")
        req.add_header("Accept", "application/json")
        req.add_header("Origin", "https://chatgpt.com")
        req.add_header("Referer", "https://chatgpt.com/")
        if auth["account_id"]:
            req.add_header("chatgpt-account-id", auth["account_id"])

        try:
            with opener.open(req, timeout=9) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                rate_limit = data.get("rate_limit", {})
                primary = rate_limit.get("primary_window", {}) if rate_limit else {}
                used_pct = float(primary.get("used_percent", 0.0))
                self.percentage = max(0.0, min(100.0, 100.0 - used_pct))
                sec = int(primary.get("reset_after_seconds", 0))

                days = sec // 86400
                hours = (sec % 86400) // 3600
                mins = (sec % 3600) // 60

                if days > 0:
                    self.reset_countdown = f"{days}d"
                    self.reset_detail = f"{days}天 {hours}小时后"
                elif hours > 0:
                    self.reset_countdown = f"{hours}h"
                    self.reset_detail = f"{hours}小时 {mins}分钟后"
                else:
                    self.reset_countdown = f"{mins}m"
                    self.reset_detail = f"{mins}分钟后"
        except Exception:
            self.reset_detail = "正在连接网络..."

        self.root.after(0, self.draw_widget)

    def run(self):
        self.root.mainloop()

if __name__ == "__main__":
    app = CodexFloatingBall()
    app.run()
