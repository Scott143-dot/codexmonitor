# ⚡ Codex Monitor | 极光用量监控悬浮标

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-blue?style=for-the-badge&logo=windows" alt="Platform">
  <img src="https://img.shields.io/badge/Size-~40%20KB-success?style=for-the-badge" alt="Size">
  <img src="https://img.shields.io/badge/Language-C%23%20%7C%20Shell%20%7C%20Python-orange?style=for-the-badge" alt="Language">
  <img src="https://img.shields.io/badge/License-MIT-purple?style=for-the-badge" alt="License">
</p>

---

[**English**](#english-version) | [**中文说明**](#中文说明)

---

## 中文说明

### 📖 项目简介
**Codex Monitor** 是一款专为 **OpenAI Codex / ChatGPT Plus & Pro** 用户打造的**极轻量 (~40 KB)、视网膜级硬件加速、极具视觉冲击力**的桌面用量监控悬浮标。

无需配置复杂的运行环境，程序通过 Windows 原生内置的 `.NET Framework 4.8` 运行时驱动，自动探测并解析本地 `~/.codex/auth.json` 登录凭据，实时获取剩余用量额度、重置倒计时与订阅到期时间，并在桌面上提供流畅的拖拽交互与大师级全屏流光尾迹特效。

---

### 🌟 核心特性与亮点

- **⚡ 极致轻量单文件 (~40 KB)**：
  - 基于 Windows 原生内置 `.NET 4.8`，单个 EXE 仅 **40 KB**，免安装、零依赖、双击瞬开。
- **🚀 GPU DirectX 硬件加速**：
  - 满血 144Hz/240Hz 硬件垂直同步（V-Sync），视网膜级 **亚像素抗锯齿 (Sub-pixel Anti-Aliasing)**，文字与圆弧如刀锋般锐利超清。
- **🔮 贝塞尔平滑插值形变 (CubicEase Morph)**：
  - **悬浮圆环形态 (72x72 px)** ↔ **贴边对称胶囊形态 (42x96 px)** 之间实现 160ms 极度丝滑的平滑过渡，0 抖动。
- **🎆 三大大师级全屏硬件加速尾迹特效**：
  - ⚡ **等离子闪电 (裂空电弧)**：中点位移分形电弧与等离子火花微粒，高能裂变；
  - 🖌️ **东方水墨 (挥毫泼墨)**：7 束自然流体交织穿插墨丝与 38px 柔和浅灰烟雨水晕扩散，行云流水；
  - 🌈 **七彩极光 (天幕流光)**：6 色带混沌螺旋缠绕与金色星尘，如丝绸天幕；
- **🎨 3 大跨相近流光渐变色彩主题**：
  - 🌊 **蓝靛紫 (Blue-Indigo-Violet)**：`#38BDF8` ➔ `#2563EB` ➔ `#4F46E5` ➔ `#A855F7`
  - 🍃 **青碧翠 (Cyan-Emerald-Forest)**：`#2DD4BF` ➔ `#34D399` ➔ `#10B981` ➔ `#047857`
  - ⚪ **白金钛 (Platinum-Gold-Titanium)**：`#FFFFFF` ➔ `#FDE68A` ➔ `#CBD5E1` ➔ `#64748B`
- **🛡️ 黄金双区迟滞防抖 (Dual-Zone Hysteresis)**：
  - 固定锚点最大包围盒与安全退出缓冲区，彻底根除交界处闪烁。
- **📄 高定暗黑悬停信息卡片**：
  - 鼠标悬停实时展示 **账号邮箱、ChatGPT Plus/Pro 订阅计划、到期时间与重置精确倒计时**。
- **🚀 开机自启动与智能右键菜单**：
  - 支持一键开机自启（写入当前用户注册表，无需管理员权限），鼠标移开 1.0 秒自动平滑关闭。
- **🔒 5 重自适应探针与动态多机隐私安全**：
  - 纯本地读取运行机器的 `~/.codex/auth.json`，发给任何人均只展示其自己的专属用量！

---

### 📂 项目工程架构

```text
CodexMonitor/
├── src/                     # Windows C# WPF 原生源码
│   ├── Program.cs           # 应用程序入口与单实例 Local Mutex 互斥锁
│   ├── ApiService.cs        # 本地 Codex 5重路径探针与 OpenAI 官方用量安全请求
│   ├── ConfigManager.cs     # 本地持久化配置读取与保存
│   ├── MainWindow.cs        # 核心悬浮标组件 (GPU 硬件加速、开机自启、17pt Bold 排版)
│   └── TrailOverlay.cs      # 全屏穿透 Segment 物理尾迹系统 (闪电/水墨/极光)
├── linux/                   # Linux / Ubuntu 零依赖套件
│   ├── codex-monitor.sh     # 纯 Shell + Curl 零依赖极速版 (Docker/终端直接运行)
│   ├── codex_monitor.py     # 纯 Python 3 标准库彩色终端版
│   ├── codex_monitor.go     # Go 独立 ELF 静态二进制源码
│   └── install_and_run_ubuntu.sh # 一键启动脚本
├── CodexMonitor.exe         # Windows 独立单文件绿色执行程序 (~40 KB)
├── build.bat                # Windows 一键编译脚本 (调用内置 csc.exe)
├── .gitignore               # Git 忽略配置
├── LICENSE                  # MIT 开源协议
└── README.md                # 中英文双语说明文档
```

---

### 🚀 快速使用

#### 🪟 Windows 用户：
直接双击运行 **`CodexMonitor.exe`** 即可。
- **左键单击**：即刻强制刷新用量；
- **左键拖拽**：在屏幕上自由拖拽，并欣赏绚丽流光尾迹；拖至屏幕边缘自动吸附；
- **右键菜单**：切换色彩主题、尾迹特效、开启/关闭开机自启。

#### 🐧 Linux / Ubuntu / Docker 用户：
在终端执行：
```bash
# 方案 A：纯 Shell 零依赖运行 (无需 Python，只要有 curl)
bash linux/codex-monitor.sh

# 方案 B：纯 Python 3 终端彩色仪表盘运行
python3 linux/codex_monitor.py
# 实时守护监控模式
python3 linux/codex_monitor.py --watch
```

#### 🛠️ Windows 一键源码编译：
直接双击运行 **`build.bat`**，脚本将自动调用 Windows 系统内置的 `csc.exe` 瞬间完成极速编译并生成 `CodexMonitor.exe`！

---
---

<a name="english-version"></a>
## English Version

### 📖 Introduction
**Codex Monitor** is an ultra-lightweight (**~40 KB**), DirectX GPU-accelerated desktop floating widget designed for **OpenAI Codex / ChatGPT Plus & Pro** users.

Powered natively by Windows built-in `.NET Framework 4.8`, it automatically probes and parses local `~/.codex/auth.json` credentials to provide real-time remaining quota percentages, reset countdowns, and subscription expiration details, paired with buttery smooth animations and full-screen visual effects.

---

### 🌟 Key Features

- **⚡ Ultra-Lightweight Single File (~40 KB)**:
  - Built natively with `.NET 4.8`. No Python, Go, or Node.js runtime required on Windows. Instant startup.
- **🚀 GPU DirectX Hardware Acceleration**:
  - Full 144Hz/240Hz hardware V-Sync synchronization with sub-pixel anti-aliasing for knife-sharp typography and curves.
- **🔮 Smooth CubicEase Morphing**:
  - Seamless 160ms zero-stutter transition between **Floating Ring (72x72 px)** and **Edge-Docked Capsule (42x96 px)** modes.
- **🎆 3 Master-Class Full-Screen Trail VFX**:
  - ⚡ **Plasma Lightning**: Midpoint displacement fractal arcs with flying spark particles;
  - 🖌️ **Chinese Ink Wash**: 7 braided fluid ink filaments with a 38px soft translucent misty bloom;
  - 🌈 **Prismatic Aurora**: 6 chaotic braided ribbon strands with drifting golden stardust;
- **🎨 3 Multi-Stop Harmonious Gradient Themes**:
  - 🌊 **Blue-Indigo-Violet**: `#38BDF8` ➔ `#2563EB` ➔ `#4F46E5` ➔ `#A855F7`
  - 🍃 **Cyan-Emerald-Forest**: `#2DD4BF` ➔ `#34D399` ➔ `#10B981` ➔ `#047857`
  - ⚪ **Platinum-Gold-Titanium**: `#FFFFFF` ➔ `#FDE68A` ➔ `#CBD5E1` ➔ `#64748B`
- **🛡️ Dual-Zone Hysteresis Anti-Flicker**:
  - Fixed-anchor bounding box with safe exit margins eliminates border flickering.
- **📄 Minimalist Dark Popup Card**:
  - Hover to reveal account email, subscription plan (Plus/Pro), expiration date, and exact reset time.
- **🚀 Auto-Start on Boot & Smart Menu**:
  - Toggle auto-start seamlessly via user registry (`HKCU`); context menu auto-closes 1.0s after mouse exit.
- **🔒 Multi-Path Probe & Multi-Machine Dynamic Auth**:
  - Reads locally from `~/.codex/auth.json` with multi-path fallback; safe to distribute to any machine without credential leaks.

---

### 🚀 Quick Start

#### 🪟 Windows:
Simply run **`CodexMonitor.exe`**.
- **Left Click**: Instant quota synchronization;
- **Left Drag**: Move freely across screens with dynamic trailing VFX; auto-docks when released near screen edges;
- **Right Click**: Open settings to switch themes, visual trails, or toggle auto-start.

#### 🐧 Linux / Ubuntu / Docker:
Run directly in terminal:
```bash
# Option A: Zero-dependency Pure Shell (Requires only curl)
bash linux/codex-monitor.sh

# Option B: Pure Python 3 ANSI Dashboard
python3 linux/codex_monitor.py
# Real-time daemon watch mode
python3 linux/codex_monitor.py --watch
```

#### 🛠️ Build from Source (Windows):
Run **`build.bat`** to compile the standalone binary using Windows built-in `csc.exe` in under 1 second.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE) © 2026.
