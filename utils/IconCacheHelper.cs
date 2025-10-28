using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MsixTool.Utils
{
    /// <summary>
    /// 提供清理 Windows 图标缓存的功能，防止卸载 / 安装后开始菜单残留旧图标。
    /// 采取分层策略：
    /// 1. 检测 Explorer.exe 是否运行（必要时延迟刷新）
    /// 2. 调用 ie4uinit.exe -show 触发刷新 (快速)
    /// 3. 调用 ie4uinit.exe -clear 进行更彻底清理 (需要 Explorer 重建图标)
    /// 4. 发送 SHChangeNotify 通知外壳刷新
    /// 所有操作记录到独立的诊断日志文件 icon_cache_diagnostics.log
    /// </summary>
    public static class IconCacheHelper
    {
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        // 常量参考：SHCNE_ASSOCCHANGED = 0x08000000; SHCNF_IDLIST = 0x0000
        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        /// <summary>
        /// 获取应用程序目录
        /// </summary>
        private static string GetAppDirectory()
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                return Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            }
            return AppContext.BaseDirectory;
        }

        private static readonly string LogFilePath = Path.Combine(
            GetAppDirectory(),
            "icon_cache_diagnostics.log"
        );

        /// <summary>
        /// 写入诊断日志
        /// </summary>
        private static void LogDiagnostic(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, logEntry, Encoding.UTF8);
            }
            catch
            {
                // 日志写入失败不影响主流程
            }
        }

        /// <summary>
        /// 检测 Explorer.exe 是否正在运行
        /// </summary>
        private static bool IsExplorerRunning()
        {
            try
            {
                var explorerProcesses = Process.GetProcessesByName("explorer");
                var isRunning = explorerProcesses.Length > 0;
                
                foreach (var proc in explorerProcesses)
                {
                    proc.Dispose();
                }
                
                return isRunning;
            }
            catch (Exception ex)
            {
                LogDiagnostic($"检测 Explorer 失败: {ex.Message}");
                return true; // 假定运行，避免等待
            }
        }

        /// <summary>
        /// 等待 Explorer 启动（最多等待指定秒数）
        /// </summary>
        private static bool WaitForExplorer(int maxWaitSeconds = 5)
        {
            LogDiagnostic($"开始等待 Explorer 启动 (最多 {maxWaitSeconds} 秒)...");
            var startTime = DateTime.Now;
            
            while ((DateTime.Now - startTime).TotalSeconds < maxWaitSeconds)
            {
                if (IsExplorerRunning())
                {
                    LogDiagnostic($"Explorer 已启动，等待时间: {(DateTime.Now - startTime).TotalSeconds:F2} 秒");
                    return true;
                }
                Thread.Sleep(500);
            }
            
            LogDiagnostic($"等待 Explorer 超时 ({maxWaitSeconds} 秒)");
            return false;
        }

        /// <summary>
        /// 轻量刷新：通知系统重新加载图标关联。
        /// </summary>
        private static void RefreshIconsBasic()
        {
            try
            {
                LogDiagnostic("执行 SHChangeNotify(SHCNE_ASSOCCHANGED)");
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                LogDiagnostic("SHChangeNotify 执行成功");
            }
            catch (Exception ex)
            {
                LogDiagnostic($"SHChangeNotify 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行 ie4uinit.exe 指令。
        /// </summary>
        private static void RunIe4uinit(string arguments)
        {
            var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var exePath = Path.Combine(systemDir, "ie4uinit.exe");
            
            if (!File.Exists(exePath))
            {
                LogDiagnostic($"ie4uinit.exe 不存在: {exePath}");
                return;
            }

            try
            {
                LogDiagnostic($"执行 ie4uinit.exe {arguments}");
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                var process = Process.Start(psi);
                if (process != null)
                {
                    var exited = process.WaitForExit(3000);
                    if (exited)
                    {
                        LogDiagnostic($"ie4uinit.exe {arguments} 完成，退出码: {process.ExitCode}");
                    }
                    else
                    {
                        LogDiagnostic($"ie4uinit.exe {arguments} 超时（3秒）");
                        process.Kill();
                    }
                }
            }
            catch (Exception ex)
            {
                LogDiagnostic($"运行 ie4uinit.exe 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 主调用：清理或刷新图标缓存。
        /// 自动检测 Explorer 状态并在必要时延迟刷新。
        /// </summary>
        /// <param name="aggressive">是否执行更彻底的 -clear 操作。</param>
        public static void ClearIconCache(bool aggressive = false)
        {
            LogDiagnostic("========== 开始图标缓存刷新操作 ==========");
            Console.WriteLine("刷新图标缓存...");
            
            // 检测 Explorer 是否运行
            if (!IsExplorerRunning())
            {
                LogDiagnostic("检测到 Explorer 未运行");
                Console.WriteLine("⚠ Explorer 未运行，等待其启动...");
                
                if (!WaitForExplorer(5))
                {
                    LogDiagnostic("Explorer 未在预期时间内启动，跳过图标刷新");
                    Console.WriteLine("⚠ Explorer 未启动，跳过图标刷新");
                    LogDiagnostic("========== 图标缓存刷新操作结束（跳过） ==========");
                    return;
                }
            }
            else
            {
                LogDiagnostic("Explorer 正在运行");
            }

            // 快速刷新
            RunIe4uinit("-show");
            RefreshIconsBasic();

            if (aggressive)
            {
                LogDiagnostic("执行深度清理模式");
                Console.WriteLine("执行深度图标缓存清理 (可能导致 Explorer 重建图标)...");
                RunIe4uinit("-clear");
                
                // 深度清理后再次刷新
                Thread.Sleep(500); // 给系统一点时间处理
                RefreshIconsBasic();
            }

            LogDiagnostic("========== 图标缓存刷新操作完成 ==========");
            Console.WriteLine("✓ 图标刷新操作完成");
        }
    }
}
