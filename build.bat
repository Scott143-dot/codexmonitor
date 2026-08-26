@echo off
setlocal
echo [BUILD] Building Windows CodexMonitor.exe ...

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if exist "%CSC%" goto compiler_ready
set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if exist "%CSC%" goto compiler_ready
echo [ERROR] .NET Framework compiler (csc.exe) was not found.
pause
exit /b 1

:compiler_ready

"%CSC%" /nologo /t:winexe /o+ /platform:anycpu /nowarn:0618 /lib:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Xaml.dll /r:WindowsBase.dll /r:PresentationCore.dll /r:PresentationFramework.dll /out:CodexMonitor.exe src\*.cs

if errorlevel 1 goto build_failed
echo [OK] Build succeeded: CodexMonitor.exe
goto build_done

:build_failed
echo [ERROR] Build failed. See the compiler output above.

:build_done
pause
endlocal
