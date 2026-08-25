#!/usr/bin/env bash
# ==========================================================
# Codex Monitor for Linux / Ubuntu 一键运行入口
# ==========================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# 自动判断当前是否有图形桌面环境 (DISPLAY / WAYLAND_DISPLAY)
if [ -n "$DISPLAY" ] || [ -n "$WAYLAND_DISPLAY" ]; then
    echo "🖥️ 检测到 Linux 桌面环境，正在启动桌面悬浮球..."
    if command -v python3 &> /dev/null; then
        python3 "$SCRIPT_DIR/codex_monitor_gui.py" &
    elif command -v python &> /dev/null; then
        python "$SCRIPT_DIR/codex_monitor_gui.py" &
    else
        echo "❌ 未检测到 Python，回退至纯 Shell 模式..."
        bash "$SCRIPT_DIR/codex-monitor.sh" "$@"
    fi
else
    echo "⚡ 检测到纯终端/服务器/Docker 环境，启动终端彩色仪表盘..."
    bash "$SCRIPT_DIR/codex-monitor.sh" "$@"
fi
