@echo off
chcp 65001 >nul
echo 🚀 正在构建 Windows 原生 CodexMonitor.exe ...

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC%" (
    echo ❌ 错误: 未找到 .NET Framework 4.0/4.8 编译器 (csc.exe)
    pause
    exit /b 1
)

"%CSC%" /nologo /t:winexe /o+ /platform:anycpu /nowarn:0618 /lib:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll /r:WindowsBase.dll /r:PresentationCore.dll /r:PresentationFramework.dll /out:CodexMonitor.exe src\*.cs

if %ERRORLEVEL% equ 0 (
    echo ✅ 构建成功: CodexMonitor.exe
) else (
    echo ❌ 构建失败，请检查上方错误提示。
)
pause
