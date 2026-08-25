@echo off
chcp 65001 >nul
title 编译 Codex Monitor (C# / WPF 极速轻量版)
setlocal

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "WPF_LIB=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

if not exist "%CSC%" (
    echo [ERROR] 未检测到系统内置的 .NET Framework 4.8 编译器！
    pause
    exit /b 1
)

echo ===================================================
echo   正在编译 Codex Monitor (C# / WPF DirectX 硬件加速)
echo ===================================================

"%CSC%" /nologo /t:winexe /o+ /platform:anycpu ^
    /lib:"%WPF_LIB%" ^
    /r:System.dll ^
    /r:System.Core.dll ^
    /r:System.Drawing.dll ^
    /r:System.Windows.Forms.dll ^
    /r:System.Web.Extensions.dll ^
    /r:System.Xaml.dll ^
    /r:WindowsBase.dll ^
    /r:PresentationCore.dll ^
    /r:PresentationFramework.dll ^
    /out:CodexMonitor.exe ^
    src\*.cs

if %errorlevel% equ 0 (
    echo.
    echo [OK] 编译成功！生成轻量独立可执行文件: CodexMonitor.exe
) else (
    echo.
    echo [ERROR] 编译失败，请检查语法与引用。
)

pause
