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
- **Windows 原生悬浮球** (`src/`)：
  - 基于 Windows 原生 `.NET 4.8`，GPU DirectX 硬件加速。
  - 支持**悬浮圆环 (72x72)** 与 **贴边胶囊 (42x96)** 平滑形变。
  - 3 种拖拽流光尾迹（等离子闪电、东方水墨、七彩极光）与 3 种渐变配色（蓝靛紫、青碧翠、白金钛）。
  - 右键菜单一键开启/关闭开机自启。
- **Linux 桌面与终端全能支持** (`linux/`)：
  - **桌面图形悬浮球** (`linux/codex-monitor-gui`)：基于 Python 官方标准库 Tkinter，零外部依赖，桌面置顶悬浮、自由拖拽与悬停卡片。
  - **终端独立执行程序源码** (`linux/main.go`)：纯 Go 静态编译，无外部运行时依赖。

---

### 下载与使用

#### 📦 直接下载预编译版本 (GitHub Releases)
前往 [**Releases 页面**](https://github.com/Scott143-dot/CodexMonitor/releases) 下载最新编译好的程序：
- **Windows 用户**：下载 `CodexMonitor.exe` 直接双击运行。
- **Linux 用户**：下载 `codex-monitor-linux-amd64.tar.gz` 解压后直接运行 `./codex-monitor-linux-amd64`。

---

### 本地源码编译

#### 🪟 Windows 本地编译
双击运行 `build.bat`，脚本将自动调用系统内置的 `csc.exe` 瞬间完成编译并生成 `CodexMonitor.exe`。

#### 🐧 Linux 本地编译
```bash
cd linux
CGO_ENABLED=0 go build -ldflags="-s -w" -o codex-monitor-linux-amd64 main.go
chmod +x codex-monitor-linux-amd64 codex-monitor-gui
```

---

### 📂 项目工程架构 (纯源码)

```text
CodexMonitor/
├── .github/workflows/
│   └── release.yml          # GitHub Actions 自动化多平台编译与 Release 流水线
├── src/                     # Windows 原生 C# WPF 源码
│   ├── Program.cs           # 互斥锁与入口
│   ├── ApiService.cs        # 5 重自适应凭据探针与 OpenAI 官方请求
│   ├── ConfigManager.cs     # 本地持久化配置
│   ├── MainWindow.cs        # 桌面悬浮标核心组件
│   └── TrailOverlay.cs      # 全屏穿透 Segment 物理尾迹系统
├── linux/                   # Linux 纯源码
│   ├── codex-monitor-gui    # Linux 桌面原生图形悬浮球
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

### Features
- **Windows Desktop Widget** (`src/`):
  - Standalone executable (~40 KB), built on native `.NET 4.8` with zero dependencies.
  - GPU DirectX hardware acceleration with smooth morphing between **Floating Ring (72x72)** and **Docked Capsule (42x96)**.
  - 3 visual trail effects (Lightning, Ink Wash, Aurora) and 3 gradient color themes.
  - Auto-start toggle via context menu.
- **Linux Desktop & CLI Suite** (`linux/`):
  - **Desktop GUI Widget** (`linux/codex-monitor-gui`): Built with Python standard library Tkinter (zero external dependencies), draggable floating widget with hover tooltip card.
  - **CLI Standalone Binary Source** (`linux/main.go`): Pure Go source ready for static compilation.

---

### Download & Usage

#### 📦 Download Precompiled Binaries (GitHub Releases)
Visit the [**Releases Page**](https://github.com/Scott143-dot/CodexMonitor/releases) to download the latest builds:
- **Windows**: Download `CodexMonitor.exe` and double-click to run.
- **Linux**: Download `codex-monitor-linux-amd64.tar.gz`, unpack and run `./codex-monitor-linux-amd64`.

---

### Build from Source

#### 🪟 Windows
Run `build.bat` to compile with the built-in Windows `csc.exe` compiler.

#### 🐧 Linux
```bash
cd linux
CGO_ENABLED=0 go build -ldflags="-s -w" -o codex-monitor-linux-amd64 main.go
chmod +x codex-monitor-linux-amd64 codex-monitor-gui
```

---

## License
MIT License © 2026
