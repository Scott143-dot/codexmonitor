#!/usr/bin/env bash
# ==========================================================
# Codex Monitor for Linux / Ubuntu 一键安装与启动
# ==========================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -n "$DISPLAY" ] || [ -n "$WAYLAND_DISPLAY" ]; then
    echo "🖥️ 检测到 Linux 桌面环境，正在配置桌面快捷方式并启动悬浮球..."
    
    # 安装到用户应用目录
    INSTALL_DIR="$HOME/.local/share/codex-monitor"
    mkdir -p "$INSTALL_DIR" "$HOME/.local/share/applications"
    cp -r "$SCRIPT_DIR/"* "$INSTALL_DIR/" 2>/dev/null
    
    # 配置桌面启动项
    DESKTOP_FILE="$HOME/.local/share/applications/codex-monitor.desktop"
    cat <<EOF > "$DESKTOP_FILE"
[Desktop Entry]
Name=Codex Monitor
Comment=OpenAI Codex / ChatGPT Quota Floating Monitor
Exec=python3 $INSTALL_DIR/codex_monitor_gui.py
Icon=utilities-system-monitor
Terminal=false
Type=Application
Categories=Utility;Development;
StartupNotify=true
EOF
    chmod +x "$DESKTOP_FILE"
    update-desktop-database "$HOME/.local/share/applications" 2>/dev/null

    echo "✅ 桌面应用快捷方式已安装 (可在应用列表中搜索 Codex Monitor)"
    python3 "$INSTALL_DIR/codex_monitor_gui.py" &
else
    echo "⚡ 检测到终端环境，启动独立二进制监控..."
    if [ -f "$SCRIPT_DIR/codex-monitor" ]; then
        chmod +x "$SCRIPT_DIR/codex-monitor"
        "$SCRIPT_DIR/codex-monitor" "$@"
    else
        bash "$SCRIPT_DIR/codex-monitor.sh" "$@"
    fi
fi
