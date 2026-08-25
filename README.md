# Codex Monitor

> A lightweight desktop & CLI monitor for OpenAI Codex / ChatGPT Plus & Pro quota tracking.  
> 极轻量 (~40 KB) 的 OpenAI Codex / ChatGPT Plus & Pro 桌面与终端用量监控小工具。

<p align="center">
  <img src="assets/widget.png" alt="Codex Monitor Widget" width="120" style="margin-right: 20px;">
  <img src="assets/menu.png" alt="Context Menu" width="160">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20(All%20Distros)-blue?style=flat-square" alt="Platform">
  <img src="https://img.shields.io/badge/Size-~40%20KB-success?style=flat-square" alt="Size">
  <img src="https://img.shields.io/badge/License-MIT-purple?style=flat-square" alt="License">
</p>

---

[English](#english) | [中文](#中文)

---

<a name="中文"></a>
## 中文

### 简介
**Codex Monitor** 是一款专为 OpenAI Codex / ChatGPT 订阅用户打造的极轻量用量监控小工具。直接读取本地 `~/.codex/auth.json` 登录凭据（支持 `cc-switch` 订阅管理器），实时展示剩余用量百分比、7天重置倒计时与账号订阅到期日。

### 特性与双端形态
- **🪟 Windows 桌面端** (`src/`)：
  - 基于 Windows 原生 `.NET 4.8`，GPU DirectX 硬件加速，单文件仅 ~40 KB。
  - 支持**悬浮圆环 (72x72)** 与 **贴边胶囊 (42x96)** 平滑形变。
  - **三大全屏流光尾迹**：等离子闪电、东方水墨、七彩极光。
  - **三大渐变配色**：蓝靛紫 / 青碧翠 / 白金钛。
  - 右键菜单支持一键开启/关闭开机自启。
- **🐧 Linux 全发行版通用支持** (`linux/`)：
  - **原生状态栏常驻小工具** (`linux/codex-monitor-tray`)：常驻在屏幕顶部状态栏（时钟旁），动态渲染发光环与用量百分比，右键呼出暗黑详情菜单。
  - **终端独立执行程序** (`linux/main.go`)：纯 Go 静态编译，专为无桌面 Docker 与 SSH 服务器打造。

---

### 系统兼容性与运行环境说明

#### 1. 🪟 Windows 系统
- **支持版本**：Windows 10 / 11 / Windows Server
- **依赖**：**零依赖**（系统内置 .NET Framework 4.8，双击即开）。

#### 2. 🐧 Linux 系统 (全部发行版通用)
全面兼容 **Ubuntu、Debian、Fedora、CentOS/RHEL、Arch Linux、Manjaro、openSUSE** 等主流发行版，以及 **GNOME、KDE Plasma、XFCE、Cinnamon、MATE、i3/Sway** 等全部桌面环境。

- **运行前依赖准备 (仅需一行)**：
  - **通用 Pip 安装**：
    ```bash
    pip3 install -r linux/requirements.txt
    ```
  - **Ubuntu / Debian**：
    ```bash
    sudo apt install -y python3-pystray python3-pil
    ```
  - **Fedora / RHEL**：
    ```bash
    sudo dnf install -y python3-pystray python3-pillow
    ```
  - **Arch Linux / Manjaro**：
    ```bash
    sudo pacman -S python-pystray python-pillow
    ```

---

### 下载与使用

#### 📦 直接下载预编译版本 (GitHub Releases)
前往 [**Releases 页面**](https://github.com/Scott143-dot/CodexMonitor/releases) 下载最新版本：
- **Windows 用户**：下载 `CodexMonitor.exe` 直接双击运行。
- **Linux 用户**：下载 `codex-monitor-linux-amd64.tar.gz` 解压后直接运行：
  - 桌面状态栏常驻：`./codex-monitor-tray`
  - 终端监控模式：`./codex-monitor-linux-amd64`

---

### 本地源码编译

#### 🪟 Windows
双击运行 `build.bat`，脚本将自动调用系统内置的 `csc.exe` 编译生成 `CodexMonitor.exe`。

#### 🐧 Linux
```bash
cd linux
CGO_ENABLED=0 go build -ldflags="-s -w" -o codex-monitor-linux-amd64 main.go
chmod +x codex-monitor-linux-amd64 codex-monitor-tray
```

---

### 📂 项目工程架构 (纯源码)

```text
CodexMonitor/
├── .github/workflows/
│   └── release.yml          # GitHub Actions 自动化多平台编译与 Release 流水线
├── assets/                  # 实机展示图片
├── src/                     # Windows 原生 C# WPF 源码
│   ├── Program.cs           # 互斥锁与入口
│   ├── ApiService.cs        # 5 重自适应凭据探针与 OpenAI 官方请求
│   ├── ConfigManager.cs     # 本地持久化配置
│   ├── MainWindow.cs        # 桌面悬浮标核心组件
│   └── TrailOverlay.cs      # 全屏穿透 Segment 物理尾迹系统
├── linux/                   # Linux 纯源码
│   ├── codex-monitor-tray   # Linux 原生状态栏常驻小工具 (System Tray)
│   ├── requirements.txt     # Linux 托盘 Python 依赖清单
│   └── main.go              # Linux 终端静态二进制 Go 源码
├── build.bat                # Windows 本地一键编译脚本
├── .gitignore               # Git 忽略配置
├── LICENSE                  # MIT 开源协议
└── README.md                # 中英文双语开发文档
```

---
---

<a name="english"></a>
## English

### Introduction
**Codex Monitor** is a lightweight desktop and CLI quota monitor for OpenAI Codex and ChatGPT Plus/Pro users. It reads local `~/.codex/auth.json` credentials (fully compatible with `cc-switch`) to display remaining quota percentages, 7-day reset countdowns, and subscription details in real time.

### Features & Platform Support
- **Windows Desktop** (`src/`):
  - Standalone executable (~40 KB), built on native `.NET 4.8` with zero dependencies.
  - GPU DirectX hardware acceleration with smooth morphing between **Floating Ring (72x72)** and **Docked Capsule (42x96)**.
  - 3 visual trail effects: Plasma Lightning, Traditional Chinese Ink Wash, and Prismatic Aurora.
  - 3 gradient themes: Blue-Indigo-Violet, Cyan-Emerald-Forest, Platinum-Gold-Titanium.
  - Auto-start toggle via context menu.
- **Linux All Distros Support** (`linux/`):
  - **Native System Tray Widget** (`linux/codex-monitor-tray`): Sits quietly in the top/bottom status bar, dynamically rendering glowing progress ring and quota percentage with a dark dropdown details menu.
  - **CLI Standalone Binary Source** (`linux/main.go`): Pure Go source ready for static compilation.

---

### Compatibility & Prerequisites

#### 1. 🪟 Windows
- **Supported**: Windows 10 / 11 / Windows Server
- **Dependencies**: **Zero Dependencies** (Built-in .NET Framework 4.8).

#### 2. 🐧 Linux (Universal Compatibility)
Fully compatible with **Ubuntu, Debian, Fedora, CentOS/RHEL, Arch Linux, Manjaro, openSUSE**, across all desktop environments (**GNOME, KDE Plasma, XFCE, Cinnamon, MATE, i3/Sway**).

- **Install Prerequisites (One-liner)**:
  - **Pip**:
    ```bash
    pip3 install -r linux/requirements.txt
    ```
  - **Ubuntu / Debian**:
    ```bash
    sudo apt install -y python3-pystray python3-pil
    ```
  - **Fedora / RHEL**:
    ```bash
    sudo dnf install -y python3-pystray python3-pillow
    ```
  - **Arch Linux / Manjaro**:
    ```bash
    sudo pacman -S python-pystray python-pillow
    ```

---

### Download & Usage

#### 📦 Download Precompiled Binaries (GitHub Releases)
Visit the [**Releases Page**](https://github.com/Scott143-dot/CodexMonitor/releases) to download the latest builds:
- **Windows**: Download `CodexMonitor.exe` and double-click to run.
- **Linux**: Download `codex-monitor-linux-amd64.tar.gz`, unpack and run:
  - System Tray Widget: `./codex-monitor-tray`
  - CLI Terminal Monitor: `./codex-monitor-linux-amd64`

---

### Build from Source

#### 🪟 Windows
Run `build.bat` to compile with the built-in Windows `csc.exe` compiler.

#### 🐧 Linux
```bash
cd linux
CGO_ENABLED=0 go build -ldflags="-s -w" -o codex-monitor-linux-amd64 main.go
chmod +x codex-monitor-linux-amd64 codex-monitor-tray
```

---

## License
MIT License © 2026
