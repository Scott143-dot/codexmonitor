# Codex Monitor

> A lightweight desktop & CLI monitor for OpenAI Codex / ChatGPT Plus & Pro quota tracking.  
> 极轻量 (~40 KB) 的 OpenAI Codex / ChatGPT Plus & Pro 桌面与终端用量监控悬浮标。

[English](#english) | [中文](#中文)

---

<a name="中文"></a>
## 中文

### 简介
**Codex Monitor** 是一款专为 OpenAI Codex / ChatGPT 订阅用户打造的轻量监控工具。直接读取本地 `~/.codex/auth.json` 登录凭据（支持 `cc-switch` 订阅管理器），实时展示剩余用量百分比、7天重置倒计时与账号订阅到期日。

### 特性
- **Windows 原生悬浮球**：
  - 单文件仅 ~40 KB，基于 Windows 内置 `.NET 4.8`，免安装即开即用。
  - GPU DirectX 硬件加速渲染，支持**悬浮圆环 (72x72)** 与 **贴边胶囊 (42x96)** 平滑形变。
  - 支持 3 种拖拽流光尾迹（等离子闪电、东方水墨、七彩极光）与 3 种渐变配色（蓝靛紫、青碧翠、白金钛）。
  - 支持右键菜单一键开启/关闭开机自启。
- **Linux 桌面与终端全能支持**：
  - **桌面图形悬浮球** (`linux/codex_monitor_gui.py`)：基于 Python 官方标准库 Tkinter 打造，零外部依赖，支持桌面置顶悬浮、自由拖拽与悬停卡片。
  - **独立二进制执行程序** (`linux/codex-monitor`)：静态编译的 ELF 二进制文件，直接运行。
  - **终端彩色仪表盘** (`linux/codex-monitor.sh`)：零依赖 Shell 脚本，支持 Docker 容器与服务器环境。

---

### 使用指南

#### 🪟 Windows 桌面端
直接双击运行 **`CodexMonitor.exe`**：
- **左键单击**：即刻刷新用量。
- **左键拖拽**：在屏幕上移动，移至屏幕边缘自动吸附为胶囊形态。
- **右键菜单**：切换配色主题、尾迹特效、开关开机自启。

#### 🐧 Linux 桌面端 (Ubuntu / Debian / Fedora)
直接运行打包好的桌面 GUI 悬浮球执行程序（直接双击或终端运行，即刻弹出悬浮球）：
```bash
chmod +x linux/codex-monitor-gui
./linux/codex-monitor-gui
```
或执行桌面集成安装（将应用图标添加至系统应用抽屉并开机自启）：
```bash
bash linux/install_and_run_ubuntu.sh
```

#### 🖥️ Linux 服务器 / Docker 终端端
```bash
# 运行纯独立 ELF 终端执行程序
chmod +x linux/codex-monitor
./linux/codex-monitor

# 实时守护监控模式 (每 60 秒自动刷新)
./linux/codex-monitor --watch
```

---

### 源码构建

- **Windows**：双击 `build.bat`，脚本调用 Windows 内置 `csc.exe` 瞬间完成极速编译生成 `CodexMonitor.exe`。
- **Linux ELF 二进制**：在安装了 Go 的环境中执行 `CGO_ENABLED=0 GOOS=linux go build -ldflags="-s -w" -o linux/codex-monitor linux/codex_monitor.go`。

---
---

<a name="english"></a>
## English

### Introduction
**Codex Monitor** is a lightweight desktop and CLI quota monitor for OpenAI Codex and ChatGPT Plus/Pro users. It reads local `~/.codex/auth.json` credentials (fully compatible with `cc-switch`) to display remaining quota percentages, 7-day reset countdowns, and subscription details in real time.

### Features
- **Windows Desktop Widget**:
  - Standalone single executable (~40 KB), built on native `.NET 4.8` with zero dependencies.
  - GPU DirectX hardware acceleration with smooth morphing between **Floating Ring (72x72)** and **Docked Capsule (42x96)**.
  - 3 visual trail effects (Lightning, Ink Wash, Aurora) and 3 gradient color themes.
  - Auto-start on boot toggle via context menu.
- **Full Linux Desktop & CLI Suite**:
  - **Desktop GUI Widget** (`linux/codex_monitor_gui.py`): Built with Python standard library Tkinter (zero external dependencies), draggable floating widget with hover tooltip card.
  - **Standalone ELF Binary** (`linux/codex-monitor`): Precompiled static Linux binary, runs anywhere without runtime.
  - **CLI Terminal Dashboard** (`linux/codex-monitor.sh`): Pure Shell script for headless Docker & SSH environments.

---

### Usage

#### 🪟 Windows
Simply run **`CodexMonitor.exe`**:
- **Left Click**: Instant quota refresh.
- **Left Drag**: Move freely; snaps to screen edges.
- **Right Click**: Open menu to change themes, trails, or toggle auto-start.

#### 🐧 Linux Desktop (Ubuntu / Debian / Fedora)
Run the desktop installer script:
```bash
bash linux/install_and_run_ubuntu.sh
```
Or launch the GUI floating widget directly:
```bash
python3 linux/codex_monitor_gui.py
```

#### 🖥️ Linux Server / Docker
```bash
# Run standalone ELF executable
chmod +x linux/codex-monitor
./linux/codex-monitor

# Real-time daemon mode (refreshes every 60s)
./linux/codex-monitor --watch
```

---

### Build from Source

- **Windows**: Run `build.bat` to compile with the built-in Windows `csc.exe` compiler.
- **Linux Binary**: Run `CGO_ENABLED=0 GOOS=linux go build -ldflags="-s -w" -o linux/codex-monitor linux/codex_monitor.go`.

---

## License
MIT License © 2026
