using System;
using System.Threading;
using System.Windows;

namespace CodexMonitor
{
    public static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        public static void Main()
        {
            try
            {
                // 使用 Local 互斥体，彻底杜绝 Global 前缀在受限普通用户权限下的 UnauthorizedAccessException
                bool createdNew;
                _mutex = new Mutex(true, "Local\\CodexMonitor_SingleInstance_Mutex_WPF", out createdNew);
                if (!createdNew)
                {
                    // 已有实例在运行，直接平稳退出
                    return;
                }

                var app = new Application();
                app.DispatcherUnhandledException += (s, e) =>
                {
                    e.Handled = true; // 吞掉未捕获的渲染/调度异常，杜绝崩溃
                };

                AppDomain.CurrentDomain.UnhandledException += (s, e) => { };

                var cfg = ConfigManager.Load();
                var trail = new TrailOverlay();
                trail.Show();

                var mainWin = new MainWindow(cfg, trail);
                app.Run(mainWin);
            }
            catch (Exception ex)
            {
                MessageBox.Show("启动异常: " + ex.Message + "\n" + ex.StackTrace, "Codex Monitor 启动提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                if (_mutex != null)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    _mutex.Dispose();
                }
            }
        }
    }
}
