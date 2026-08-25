# Codex Monitor

> A lightweight desktop monitor for OpenAI Codex / ChatGPT Plus & Pro quota tracking.  
> 极轻量 (~40 KB) 的 OpenAI Codex / ChatGPT Plus & Pro 桌面用量监控悬浮标。

[English](#english) | [中文](#中文)

---

<a name="中文"></a>
## 中文

### 简介
**Codex Monitor** 是一个用 C# WPF (.NET 4.8) 编写的桌面悬浮监控工具。它直接读取本地 `~/.codex/auth.json` 登录凭据，实时获取剩余用量额度、重置倒计时与订阅到期时间。

### 特性
- **轻量独立**：单文件仅 ~40 KB，基于 Windows 内置 `.NET 4.8`，免安装即开即用。
- **悬浮与磁吸形态**：
  - **悬浮圆环** (72x72 px)：显示剩余用量与倒计时。
  - **贴边胶囊** (42x96 px)：自动吸附屏幕边缘。
- **三种拖拽尾迹特效**：
  - 等离子闪电 (裂空电弧)
  - 东方水墨 (交织水晕)
  - 七彩极光 (流光天幕)
- **三种渐变配色**：蓝靛紫 / 青碧翠 / 白金钛。
- **开机自启**：右键菜单一键开启/关闭（写入当前用户注册表）。
- **多平台支持**：同时提供 Linux / Docker 终端监控脚本（位于 `linux/` 目录）。

### 使用说明

#### Windows
直接运行 `CodexMonitor.exe`：
- **左键单击**：即刻刷新用量。
- **左键拖拽**：移动位置，移至屏幕边缘自动吸附。
- **右键菜单**：切换配色主题、尾迹特效、开关开机自启。

#### Linux / Docker 终端
在终端中执行：
```bash
# 纯 Shell 脚本 (需系统自带 curl)
bash linux/codex-monitor.sh

# Python 3 终端版
python3 linux/codex_monitor.py

# Python 3 实时监控模式 (每 60 秒刷新)
python3 linux/codex_monitor.py --watch
```

### 源码编译 (Windows)
双击根目录下的 `build.bat` 即可调用系统自带的 `csc.exe` 完成快速编译。

---

<a name="english"></a>
## English

### Introduction
**Codex Monitor** is a lightweight desktop floating monitor written in C# WPF (.NET 4.8). It reads local `~/.codex/auth.json` credentials to provide real-time remaining quota percentages, reset countdowns, and subscription expiration details.

### Features
- **Ultra-Lightweight**: Single standalone executable (~40 KB), zero external dependencies on Windows 10/11.
- **Dual Form Factor**:
  - **Floating Ring** (72x72 px): Displays percentage and countdown.
  - **Edge-Docked Capsule** (42x96 px): Automatically snaps to screen edges.
- **3 Visual Trail Effects**:
  - Plasma Lightning
  - Traditional Ink Wash
  - Prismatic Aurora
- **3 Gradient Color Themes**: Blue-Indigo-Violet, Cyan-Emerald-Forest, Platinum-Gold-Titanium.
- **Auto-Start**: Toggle via context menu (writes to current user registry `HKCU`).
- **Cross-Platform**: Includes zero-dependency Linux / Docker CLI scripts in `linux/`.

### Usage

#### Windows
Run `CodexMonitor.exe`:
- **Left Click**: Refresh quota immediately.
- **Left Drag**: Move widget around; release near edges to dock.
- **Right Click**: Open menu to change themes, VFX, or toggle auto-start.

#### Linux / Docker CLI
Run in terminal:
```bash
# Pure Shell script (requires curl)
bash linux/codex-monitor.sh

# Python 3 CLI
python3 linux/codex_monitor.py

# Python 3 Daemon watch mode (refreshes every 60s)
python3 linux/codex_monitor.py --watch
```

### Build from Source (Windows)
Run `build.bat` to compile with the built-in Windows `csc.exe` compiler.

---

## License
MIT License © 2026
