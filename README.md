# Codex Monitor

> A lightweight desktop & CLI monitor for OpenAI Codex / ChatGPT Plus & Pro quota tracking.  
> 极轻量的 OpenAI Codex / ChatGPT Plus & Pro 桌面与终端用量监控工具。

<p align="center">
  <img src="assets/widget.png" alt="Codex Monitor Widget" width="120" style="margin-right: 20px;">
  <img src="assets/menu.png" alt="Context Menu" width="160">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Language-Go%20%7C%20C%23-blue?style=flat-square" alt="Language">
  <img src="https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-brightgreen?style=flat-square" alt="Platform">
  <a href="https://linux.do/"><img src="https://img.shields.io/badge/Community-LINUX%20DO-orange?style=flat-square" alt="LINUX DO"></a>
  <img src="https://img.shields.io/badge/License-MIT-purple?style=flat-square" alt="License">
</p>

---

[English](#english) | [中文](#中文)

---

<a name="中文"></a>
## 中文

### 简介
**Codex Monitor** 是一款专为 OpenAI Codex / ChatGPT 订阅用户打造的极轻量用量监控工具。直接读取本地 `~/.codex/auth.json` 登录凭据（兼容 `cc-switch` 等订阅管理工具），实时展示剩余用量百分比、7天重置倒计时与账号订阅到期日。

- ⚡ **智能刷新**：默认每 60 秒后台自动同步一次，支持单击或右键菜单即刻手动刷新。
- 🔄 **自动续期**：检测到 Token 过期时自动调用官方 OAuth 接口刷新并持久化写回凭据文件。

### 双端形态与核心特性

> 💡 **平台形态说明**：  
> - **Windows** 采用 **桌面悬浮标** 形态（支持在桌面上自由拖拽、贴边吸附与全屏流光尾迹）；  
> - **Linux** 采用 **系统状态栏托盘** 形态（常驻于屏幕顶部/底部任务栏，不占桌面空间，无悬浮拖拽），同时提供终端命令行仪表盘。

#### 🪟 Windows 桌面悬浮标 (`src/`)
- 基于 C# (.NET 4.8 / WPF) 构建，GPU DirectX 硬件加速渲染，体积仅 ~40 KB。
- 支持**桌面自由拖拽**，并在**悬浮圆环 (72x72)** 与 **贴边胶囊 (42x96)** 间平滑形变。
- **三大流光尾迹**：等离子闪电、东方水墨、七彩极光。
- **三大渐变配色**：蓝靛紫、青碧翠、白金钛。
- 支持右键菜单一键切换开机自启。

#### 🐧 Linux 系统状态栏与终端 (`linux/`)
- 基于 Go 语言构建的单一独立二进制程序，直接运行。
- **原生状态栏常驻（无桌面悬浮拖拽）**：常驻于屏幕顶部/底部状态栏（时钟旁），动态光栅化渲染发光圆弧与用量百分比，支持右键暗黑详情菜单与一键开机自启。
- **终端命令行模式**：支持 `--cli` 单次输出彩色仪表盘与 `--watch` 实时守护监控。

---

### 下载与使用

#### 📦 下载预编译版本 (GitHub Releases)
前往 [**Releases 页面**](https://github.com/Scott143-dot/CodexMonitor/releases) 根据您的操作系统与 CPU 架构下载：

| 资产文件名 (File Asset) | 适用操作系统 (OS) | CPU 架构 (Architecture) | 平台形态 | 运行方式 |
| :--- | :--- | :--- | :--- | :--- |
| **`CodexMonitor-windows-x64.exe`** | **Windows 10 / 11 / Server** | **x86_64 / x64** | 桌面可拖拽悬浮标 | 双击直接运行 |
| **`codex-monitor-linux-x86_64.tar.gz`** | **Linux (Ubuntu/Debian/Arch/Fedora)** | **x86_64 / AMD64** | 系统状态栏托盘 / 终端 | 解压后运行 `./codex-monitor -d` |

- **Linux 常用运行命令**：
  ```bash
  # 启动状态栏托盘 (后台常驻，关闭终端不退出)
  ./codex-monitor -d

  # 终端命令行模式
  ./codex-monitor --cli

  # 终端实时守护监控
  ./codex-monitor --watch
  ```

---

### 源码编译

#### 🪟 Windows
双击运行 `build.bat`，调用系统内置 `csc.exe` 编译生成 `CodexMonitor-windows-x64.exe`。

#### 🐧 Linux
```bash
cd linux
go build -ldflags="-s -w" -o codex-monitor main.go
chmod +x codex-monitor
```

---

### 📂 项目架构

```text
CodexMonitor/
├── .github/workflows/
│   └── release.yml          # GitHub Actions 自动化跨平台编译与 Release 流水线
├── assets/                  # 真实运行展示图片
├── src/                     # Windows C# WPF 源码 (桌面可拖拽悬浮标)
│   ├── Program.cs           # 单实例互斥锁与入口
│   ├── ApiService.cs        # 凭据自适应解析与 OpenAI 官方请求
│   ├── ConfigManager.cs     # 本地配置持久化
│   ├── MainWindow.cs        # 悬浮标核心组件与形态变换
│   └── TrailOverlay.cs      # 全屏流光尾迹渲染系统
├── linux/                   # Linux Go 源码 (状态栏托盘与终端一体化)
│   ├── go.mod               # Go 模块配置
│   ├── go.sum               # Go 依赖校验
│   └── main.go              # 状态栏托盘与终端一体化源码
├── build.bat                # Windows 本地编译脚本
├── .gitignore               # Git 忽略配置
├── LICENSE                  # MIT 开源协议
└── README.md                # 中英文双语开发文档
```

---

### 💬 社区与讨论
- 欢迎加入 **[LINUX DO — 中文开发者社区](https://linux.do/)** 交流与讨论！

---
---

<a name="english"></a>
## English

### Introduction
**Codex Monitor** is a lightweight desktop and CLI quota monitor for OpenAI Codex and ChatGPT Plus/Pro users. It reads local `~/.codex/auth.json` credentials (fully compatible with `cc-switch`) to display remaining quota percentages, 7-day reset countdowns, and subscription details in real time.

- ⚡ **Smart Refresh**: Automatically refreshes every 60 seconds in the background, with instant manual refresh on click or via menu.
- 🔄 **OAuth Auto-Renewal**: Automatically renews expired tokens and persists them back to the local credentials file.

### Dual Form Factors & Features

> 💡 **Platform Design Note**:  
> - **Windows** runs as a **Floating Desktop Widget** (supports free dragging, edge snapping, and visual trails).  
> - **Linux** runs as a **Native System Tray Widget** (sits quietly in the top/bottom status bar without desktop floating windows), alongside a CLI terminal dashboard.

#### 🪟 Windows Desktop Widget (`src/`)
- Built with C# (.NET 4.8 / WPF), GPU DirectX hardware acceleration, single binary ~40 KB.
- Supports **free desktop dragging** with smooth morphing between **Floating Ring (72x72)** and **Docked Capsule (42x96)**.
- **3 Visual Trail Effects**: Plasma Lightning, Traditional Chinese Ink Wash, and Prismatic Aurora.
- **3 Gradient Themes**: Blue-Indigo-Violet, Cyan-Emerald-Forest, Platinum-Gold-Titanium.
- Auto-start toggle via context menu.

#### 🐧 Linux System Tray & CLI (`linux/`)
- Built with Go as a standalone single executable binary.
- **Native Status Bar Tray (No floating windows)**: Integrates seamlessly into the top/bottom panel (next to the clock), dynamically rasterizing glowing progress arcs and percentages, with a dark context menu and auto-start toggle.
- **CLI Terminal Mode**: Supports `--cli` single output dashboard and `--watch` real-time monitoring.

---

### Download & Usage

#### 📦 Download Precompiled Binaries (GitHub Releases)
Visit the [**Releases Page**](https://github.com/Scott143-dot/CodexMonitor/releases) to download the latest builds for your OS & Architecture:

| Asset File | Operating System (OS) | Architecture | Form Factor | Usage |
| :--- | :--- | :--- | :--- | :--- |
| **`CodexMonitor-windows-x64.exe`** | **Windows 10 / 11 / Server** | **x86_64 / x64** | Draggable Floating Widget | Double click to run |
| **`codex-monitor-linux-x86_64.tar.gz`** | **Linux (Ubuntu/Debian/Arch/Fedora)** | **x86_64 / AMD64** | System Tray / CLI | Unpack and run `./codex-monitor -d` |

- **Linux Common Commands**:
  ```bash
  # Launch System Tray Widget in daemon mode
  ./codex-monitor -d

  # CLI Terminal Dashboard
  ./codex-monitor --cli

  # Real-time monitoring
  ./codex-monitor --watch
  ```

---

### Build from Source

#### 🪟 Windows
Run `build.bat` to compile with the built-in Windows `csc.exe` compiler.

#### 🐧 Linux
```bash
cd linux
go build -ldflags="-s -w" -o codex-monitor main.go
chmod +x codex-monitor
```

---

### 💬 Community
- Join the discussion on **[LINUX DO — Chinese Developer Community](https://linux.do/)**!

---

## License
MIT License © 2026
