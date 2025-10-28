using System.Diagnostics;
using Microsoft.Win32;

namespace MsixTool.Utils
{
    public static class VcRedistChecker
    {
        /// <summary>
        /// 检查是否已安装 VC++ Redistributable 2015-2022
        /// </summary>
        public static bool IsVcRedistInstalled()
        {
            try
            {
                // 检查注册表中的 VC++ Redistributable 安装信息
                var registryPaths = new[]
                {
                    @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
                    @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
                };

                foreach (var path in registryPaths)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(path);
                    if (key != null)
                    {
                        var installed = key.GetValue("Installed");
                        if (installed is int installedValue && installedValue == 1)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取 VC++ Redistributable 安装程序路径
        /// </summary>
        public static string? GetVcRedistInstallerPath()
        {
            var appDir = GetAppDirectory();
            var installerPath = Path.Combine(appDir, "tools", "vc_redist.x64.exe");
            
            if (File.Exists(installerPath))
            {
                return installerPath;
            }

            return null;
        }

        /// <summary>
        /// 提示用户安装 VC++ Redistributable
        /// </summary>
        public static void PromptInstallVcRedist()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  ⚠  检测到缺少必要的运行时依赖");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Windows SDK 工具需要安装 Microsoft Visual C++ Redistributable。");
            Console.WriteLine();

            var installerPath = GetVcRedistInstallerPath();
            if (installerPath != null)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("已为您准备好安装程序，是否立即安装？");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("  [Y] 是，立即安装 (推荐)");
                Console.WriteLine("  [N] 否，我稍后手动安装");
                Console.WriteLine();
                Console.Write("请选择 (Y/N): ");

                var response = Console.ReadLine()?.Trim().ToUpperInvariant();
                if (response == "Y" || response == "YES" || string.IsNullOrEmpty(response))
                {
                    InstallVcRedist(installerPath);
                }
                else
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"请手动运行安装程序: {installerPath}");
                    Console.ResetColor();
                    Console.WriteLine("安装完成后，再次运行本工具即可。");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("未找到 VC++ Redistributable 安装程序。");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("请手动下载并安装:");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  https://aka.ms/vs/17/release/vc_redist.x64.exe");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 安装 VC++ Redistributable
        /// </summary>
        private static void InstallVcRedist(string installerPath)
        {
            try
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("正在启动安装程序...");
                Console.ResetColor();
                Console.WriteLine("请在弹出的安装向导中完成安装。");
                Console.WriteLine();

                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/install /quiet /norestart",
                    UseShellExecute = true,
                    Verb = "runas" // 请求管理员权限
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    Console.WriteLine("等待安装完成...");
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine();
                        Console.WriteLine("✓ 安装完成！现在可以正常使用工具了。");
                        Console.ResetColor();
                        Console.WriteLine();
                    }
                    else if (process.ExitCode == 1638) // 已经安装了更新版本
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine();
                        Console.WriteLine("✓ 系统已安装相同或更新版本。");
                        Console.ResetColor();
                        Console.WriteLine();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine();
                        Console.WriteLine($"安装程序退出代码: {process.ExitCode}");
                        Console.WriteLine("如果安装未成功，请尝试手动运行安装程序。");
                        Console.ResetColor();
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine($"安装失败: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("请尝试手动运行安装程序:");
                Console.WriteLine($"  {installerPath}");
                Console.WriteLine();
            }
        }

        private static string GetAppDirectory()
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                return Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            }
            return AppContext.BaseDirectory;
        }
    }
}
