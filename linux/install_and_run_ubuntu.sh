#!/usr/bin/env bash
# ==========================================================
# Codex Monitor for Linux / Ubuntu (零依赖极速一键运行脚本)
# ==========================================================

# 1. 自动定位 Python 解释器
PYTHON_CMD="python3"
if ! command -v python3 &> /dev/null; then
    if command -v python &> /dev/null; then
        PYTHON_CMD="python"
    else
        echo "❌ 未检测到 Python 环境，请先安装 Python 3"
        exit 1
    fi
fi

# 2. 获取脚本所在目录
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_PY="$SCRIPT_DIR/codex_monitor.py"

# 3. 赋予执行权限并直接运行
chmod +x "$TARGET_PY" 2>/dev/null || true
$PYTHON_CMD "$TARGET_PY" "$@"
