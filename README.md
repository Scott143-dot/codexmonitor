# Codex Monitor

> A lightweight desktop & CLI monitor for OpenAI Codex / ChatGPT Plus & Pro quota tracking.  
> 极轻量 (~40 KB) 的 OpenAI Codex / ChatGPT Plus & Pro 桌面与终端用量监控小工具。

<p align="center">
  <img src="assets/widget.png" alt="Codex Monitor Widget" width="120" style="margin-right: 20px;">
  <img src="assets/menu.png" alt="Context Menu" width="160">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20(All%20Distros)-blue?style=flat-square" alt="Platform">
  <img src="https://img.shields.io/badge/Dependencies-Zero%20Dependencies-success?style=flat-square" alt="Dependencies">
  <img src="https://img.shields.io/badge/License-MIT-purple?style=flat-square" alt="License">
</p>

---

[English](#english) | [中文](#中文)

---

<a name="中文"></a>
## 中文

### 简介
**Codex Monitor** 是一款专为 OpenAI Codex / ChatGPT 订阅用户打造的极轻量用量监控小工具。直接读取本地 `~/.codex/auth.json` 登录凭据（支持 `cc-switch` 订阅管理器），实时展示剩余用量百分比、7天重置倒计时与账号订阅到期日。

- ⚡ **智能刷新策略**：默认后台**每 60 秒（1 分钟）自动静默刷新**，同时支持左键点击或右键菜单**即刻手动刷新**。
- 🔄 **自动续期保障**：检测到 Token 过期时自动调用官方 OAuth 接口静默刷新并持久化写回凭据文件。

### 特性与双端形态 (全部 100% 原生，零 Python 依赖)
- **🪟 Windows 桌面端** (`src/`)：
  - 基于 Windows 原生 `.NET 4.8`，GPU DirectX 硬件加速，单文件仅 ~40 KB。
  - 支持**悬浮圆环 (72x72)** 与 **贴边胶囊 (42x96)** 平滑形变。
  - **三大全屏流光尾迹**：等离子闪电、东方水墨、七彩极光。
  - **三大渐变配色**：蓝靛紫 / 青碧翠 / 白金钛。
  - 右键菜单支持一键开启/关闭开机自启。
- **🐧 Linux 原生全能程序** (`linux/`)：
  - **100% 纯 Go 编写**，编译为单一独立二进制程序，**无需安装 Python，零依赖**！
  - **图形状态栏常驻**：直接运行即可常驻屏幕顶部状态栏（时钟旁），动态渲染发光环与用量百分比，右键呼出暗黑详情菜单。
  - **终端极客仪表盘**：支持 `--cli` 单次输出与 `--watch` 实时守护监控模式。

---

### 系统兼容性与零依赖说明

- **🪟 Windows**：Windows 10 / 11 / Server，**零依赖**（系统内置 .NET 4.8）。
- **🐧 Linux**：Ubuntu / Debian / Fedora / Arch Linux / CentOS 等全发行版，**零 Python 依赖、零 pip 安装**，解压即可直接运行。

---

### 下载与使用

#### 📦 直接下载预编译独立程序 (GitHub Releases)
前往 [**Releases 页面**](https://github.com/Scott143-dot/CodexMonitor/releases) 下载最新编译好的程序：
- **Windows 用户**：下载 `CodexMonitor.exe` 直接双击运行。
- **Linux 用户**：下载 `codex-monitor-linux-amd64.tar.gz` 解压后直接运行：
  ```bash
  # 1. 后台守护启动状态栏托盘 (关掉终端不退出)
  ./codex-monitor-linux-amd64 -d

  # 2. 纯终端命令行模式
  ./codex-monitor-linux-amd64 --cli

  # 3. 终端实时监控模式
  ./codex-monitor-linux-amd64 --watch
  ```

---

### 本地源码编译

#### 🪟 Windows
双击运行 `build.bat`，脚本将自动调用系统内置的 `csc.exe` 编译生成 `CodexMonitor.exe`。

#### 🐧 Linux
```bash
cd linux
go build -ldflags="-s -w" -o codex-monitor-linux-amd64 main.go
chmod +x codex-monitor-linux-amd64
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
├── linux/                   # Linux 纯 Go 源码 (100% 零 Python 依赖)
│   ├── go.mod               # Go 模块配置
│   ├── go.sum               # Go 依赖校验
│   └── main.go              # 纯 Go 状态栏托盘与终端一体化源码
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

- ⚡ **Smart Refresh Strategy**: Automatically refreshes in background **every 60 seconds**, with support for instant manual refresh via click or menu.
- 🔄 **OAuth Auto-Renewal**: Automatically renews expired tokens and persists them back to the local credentials file.

### Features & Platform Support (100% Native, Zero Python Dependencies)
- **Windows Desktop** (`src/`):
  - Standalone executable (~40 KB), built on native `.NET 4.8` with zero dependencies.
  - GPU DirectX hardware acceleration with smooth morphing between **Floating Ring (72x72)** and **Docked Capsule (42x96)**.
  - 3 visual trail effects: Plasma Lightning, Traditional Chinese Ink Wash, and Prismatic Aurora.
  - 3 gradient themes: Blue-Indigo-Violet, Cyan-Emerald-Forest, Platinum-Gold-Titanium.
  - Auto-start toggle via context menu.
- **Linux Native Binary** (`linux/`):
  - **100% Pure Go**, statically compiled single binary, **Zero Python / Pip dependencies**.
  - **System Tray Widget**: Sits quietly in the top/bottom status bar, dynamically rendering glowing progress ring and quota percentage with a dark dropdown details menu.
  - **CLI Dashboard**: Supports `--cli` single output and `--watch` real-time monitoring.

---

### Download & Usage

#### 📦 Download Precompiled Binaries (GitHub Releases)
Visit the [**Releases Page**](https://github.com/Scott143-dot/CodexMonitor/releases) to download the latest builds:
- **Windows**: Download `CodexMonitor.exe` and double-click to run.
- **Linux**: Download `codex-monitor-linux-amd64.tar.gz`, unpack and run:
  ```bash
  # 1. Run Top/Bottom System Tray Widget (Zero dependencies)
  ./codex-monitor-linux-amd64

  # 2. CLI Terminal Dashboard
  ./codex-monitor-linux-amd64 --cli

  # 3. Real-time watch mode
  ./codex-monitor-linux-amd64 --watch
  ```

---

### Build from Source

#### 🪟 Windows
Run `build.bat` to compile with the built-in Windows `csc.exe` compiler.

#### 🐧 Linux
```bash
cd linux
go build -ldflags="-s -w" -o codex-monitor-linux-amd64 main.go
chmod +x codex-monitor-linux-amd64
```

---

## License
MIT License © 2026
