# Codex Monitor

> A lightweight desktop & CLI monitor for OpenAI Codex / ChatGPT Plus & Pro quota tracking.  
> 极轻量 (~40 KB) 的 OpenAI Codex / ChatGPT Plus & Pro 桌面与终端用量监控小工具。

<p align="center">
  <img src="assets/preview.png" alt="Codex Monitor Preview" width="280">
</p>

[English](#english) | [中文](#中文)

---

<a name="中文"></a>
## 中文

### 简介
**Codex Monitor** 是一款专为 OpenAI Codex / ChatGPT 订阅用户打造的极轻量用量监控工具。直接读取本地 `~/.codex/auth.json` 登录凭据（支持 `cc-switch` 订阅管理器），实时展示剩余用量百分比、7天重置倒计时与账号订阅到期日。

<p align="center">
  <img src="assets/widget.png" alt="Widget" width="130" style="margin-right: 20px;">
  <img src="assets/menu.png" alt="Context Menu" width="180">
</p>

### 特性与双端形态
- **🪟 Windows 原生 GPU 加速悬浮球** (`src/`)：
  - 基于 Windows 原生 `.NET 4.8`，GPU DirectX 硬件加速，单文件仅 ~40 KB。
  - 支持**悬浮圆环 (72x72)** 与 **贴边胶囊 (42x96)** 平滑形变。
  - **三大全屏流光尾迹特效**：
    - ⚡ **等离子闪电**：裂空分形电弧与等离子火花微粒；
    - 🖌️ **东方水墨**：自然流体交织穿插墨丝与 38px 柔和浅灰烟雨水晕扩散；
    - 🌈 **七彩极光**：6 色带混沌螺旋缠绕与金色星尘；
  - **三大渐变配色**：蓝靛紫 / 青碧翠 / 白金钛。
  - 右键菜单一键开启/关闭开机自启。

<p align="center">
  <img src="assets/trail-ink.png" alt="Traditional Chinese Ink Wash Trail" width="520">
  <br>
  <em>🖌️ 东方水墨 (挥毫泼墨) 真实流体渲染效果</em>
</p>

- **🐧 Linux 原生顶部菜单栏常驻小工具** (`linux/`)：
  - **顶部菜单栏常驻** (`linux/codex-monitor-tray`)：遵从 Linux / GNOME / Ubuntu 标准状态栏设计，在顶部菜单栏常驻显示 **`⚡ 68% (5d)`**，点击弹出暗黑下拉详情菜单（邮箱、Plus/Pro、到期日、重置时间）。
  - **终端独立执行程序源码** (`linux/main.go`)：纯 Go 静态编译，专为无桌面 Docker 与 SSH 服务器打造。

---

### 下载与使用

#### 📦 直接下载预编译版本 (GitHub Releases)
前往 [**Releases 页面**](https://github.com/Scott143-dot/CodexMonitor/releases) 下载最新编译好的程序：
- **Windows 用户**：下载 `CodexMonitor.exe` 直接双击运行。
- **Linux 用户**：下载 `codex-monitor-linux-amd64.tar.gz` 解压后直接运行：
  - 桌面菜单栏常驻：`./codex-monitor-tray`
  - 纯终端监控模式：`./codex-monitor-linux-amd64`

---

### 本地源码编译

#### 🪟 Windows 本地编译
双击运行 `build.bat`，脚本将自动调用系统内置的 `csc.exe` 瞬间完成编译并生成 `CodexMonitor.exe`。

#### 🐧 Linux 本地编译
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
├── assets/                  # 真实实机运行截图与特效展示
├── src/                     # Windows 原生 C# WPF 源码
│   ├── Program.cs           # 互斥锁与入口
│   ├── ApiService.cs        # 5 重自适应凭据探针与 OpenAI 官方请求
│   ├── ConfigManager.cs     # 本地持久化配置
│   ├── MainWindow.cs        # 桌面悬浮标核心组件
│   └── TrailOverlay.cs      # 全屏穿透 Segment 物理尾迹系统
├── linux/                   # Linux 纯源码
│   ├── codex-monitor-tray   # Linux 原生顶部状态栏常驻小工具 (Top Bar Tray)
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

<p align="center">
  <img src="assets/preview.png" alt="Codex Monitor Preview" width="280">
</p>

### Features & Dual Form Factor
- **Windows Desktop Widget** (`src/`):
  - Standalone executable (~40 KB), built on native `.NET 4.8` with zero dependencies.
  - GPU DirectX hardware acceleration with smooth morphing between **Floating Ring (72x72)** and **Docked Capsule (42x96)**.
  - 3 visual trail effects: Plasma Lightning, Traditional Chinese Ink Wash, and Prismatic Aurora.
  - 3 gradient themes: Blue-Indigo-Violet, Cyan-Emerald-Forest, Platinum-Gold-Titanium.
  - Auto-start toggle via context menu.
- **Linux Native Top Bar & CLI Suite** (`linux/`):
  - **Top Bar AppIndicator** (`linux/codex-monitor-tray`): Seamlessly integrates into Ubuntu / GNOME top menu bar displaying **`⚡ 68% (5d)`** with a clean dark dropdown details menu.
  - **CLI Standalone Binary Source** (`linux/main.go`): Pure Go source ready for static compilation.

---

### Download & Usage

#### 📦 Download Precompiled Binaries (GitHub Releases)
Visit the [**Releases Page**](https://github.com/Scott143-dot/CodexMonitor/releases) to download the latest builds:
- **Windows**: Download `CodexMonitor.exe` and double-click to run.
- **Linux**: Download `codex-monitor-linux-amd64.tar.gz`, unpack and run:
  - Top Bar Tray Widget: `./codex-monitor-tray`
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
