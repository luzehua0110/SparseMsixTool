using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SparseMsixTool.Utils
{
    public static class DependencyChecker
    {
        private static readonly string[] RequiredSdkTools = new[]
        {
            "makeappx.exe",
            "signtool.exe",
            "makecert.exe",
            "pvk2pfx.exe"
        };

        /// <summary>
        /// 检查所有必需的依赖
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public static async Task<bool> CheckAllDependenciesAsync(bool autoInstall = true)
        {
            Console.WriteLine("正在检查运行依赖...");
            Console.WriteLine();

            bool allOk = true;

            // 1. 检查操作系统
            if (!CheckOperatingSystem())
            {
                allOk = false;
            }

            // 2. 检查 PowerShell
            if (!CheckPowerShell())
            {
                Console.WriteLine("✗ PowerShell 5.1+ 未找到");
                Console.WriteLine("  Windows 10+ 应该自带 PowerShell 5.1");
                allOk = false;
            }
            else
            {
                Console.WriteLine("✓ PowerShell 检查通过");
            }

            // 3. 检查 Windows SDK 工具
            var missingSdkTools = CheckWindowsSdkTools();
            if (missingSdkTools.Any())
            {
                Console.WriteLine($"✗ Windows SDK 工具缺失: {string.Join(", ", missingSdkTools)}");
                
                if (autoInstall)
                {
                    Console.WriteLine();
                    Console.WriteLine("正在尝试定位 Windows SDK...");
                    await TryInstallWindowsSdkAsync();
                }
                else
                {
                    Console.WriteLine("  请安装 Windows SDK 10.0.19041.0 或更高版本");
                    Console.WriteLine("  下载地址: https://developer.microsoft.com/windows/downloads/windows-sdk/");
                    allOk = false;
                }
            }
            else
            {
                Console.WriteLine("✓ Windows SDK 工具检查通过");
            }

            // 4. 检查管理员权限
            if (!CheckAdministratorPrivileges())
            {
                Console.WriteLine("⚠ 警告: 未以管理员身份运行");
                Console.WriteLine("  某些操作（如安装证书、部署包）需要管理员权限");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("✓ 管理员权限检查通过");
            }

            Console.WriteLine();
            return allOk;
        }

        /// <summary>
        /// 检查操作系统版本
        /// </summary>
        private static bool CheckOperatingSystem()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("✗ 此程序仅支持 Windows 操作系统");
                return false;
            }

            // 检查 Windows 10 或更高版本
            var version = Environment.OSVersion.Version;
            if (version.Major < 10)
            {
                Console.WriteLine($"✗ 操作系统版本过低: {version}");
                Console.WriteLine("  需要 Windows 10 (Build 19041+) 或 Windows 11");
                return false;
            }

            Console.WriteLine($"✓ 操作系统检查通过: Windows {version}");
            return true;
        }

        /// <summary>
        /// 检查 .NET Runtime
        /// </summary>
        /// <summary>
        /// 检查 PowerShell
        /// </summary>
        private static bool CheckPowerShell()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"$PSVersionTable.PSVersion.Major\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (int.TryParse(output, out int version) && version >= 5)
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// 检查 Windows SDK 工具
        /// </summary>
        private static List<string> CheckWindowsSdkTools()
        {
            var missing = new List<string>();

            // 检查 makeappx.exe
            try
            {
                var makeappxPath = WindowsSdkHelper.GetMakeAppxPath();
                if (string.IsNullOrEmpty(makeappxPath) || !File.Exists(makeappxPath))
                {
                    missing.Add("makeappx.exe");
                }
            }
            catch
            {
                missing.Add("makeappx.exe");
            }

            // 检查 signtool.exe
            try
            {
                var signtoolPath = WindowsSdkHelper.GetSignToolPath();
                if (string.IsNullOrEmpty(signtoolPath) || !File.Exists(signtoolPath))
                {
                    missing.Add("signtool.exe");
                }
            }
            catch
            {
                missing.Add("signtool.exe");
            }

            // 检查 makecert.exe
            try
            {
                var makecertPath = WindowsSdkHelper.GetMakeCertPath();
                if (string.IsNullOrEmpty(makecertPath) || !File.Exists(makecertPath))
                {
                    missing.Add("makecert.exe");
                }
            }
            catch
            {
                missing.Add("makecert.exe");
            }

            // pvk2pfx.exe 的检查比较特殊，它可能在不同的位置
            // 只要能找到makecert一般就能找到pvk2pfx，这里简化检查
            if (!missing.Contains("makecert.exe"))
            {
                // 如果找到了makecert，通常pvk2pfx也在同一目录
                try
                {
                    var makecertPath = WindowsSdkHelper.GetMakeCertPath();
                    var pvk2pfxPath = Path.Combine(Path.GetDirectoryName(makecertPath)!, "pvk2pfx.exe");
                    if (!File.Exists(pvk2pfxPath))
                    {
                        missing.Add("pvk2pfx.exe");
                    }
                }
                catch
                {
                    missing.Add("pvk2pfx.exe");
                }
            }
            else
            {
                missing.Add("pvk2pfx.exe");
            }

            return missing;
        }

        /// <summary>
        /// 尝试安装 Windows SDK
        /// </summary>
        private static async Task TryInstallWindowsSdkAsync()
        {
            Console.WriteLine("Windows SDK 需要手动安装");
            Console.WriteLine();
            Console.WriteLine("请访问以下地址下载并安装:");
            Console.WriteLine("  https://developer.microsoft.com/windows/downloads/windows-sdk/");
            Console.WriteLine();
            Console.WriteLine("安装时请确保选择以下组件:");
            Console.WriteLine("  ✓ Windows SDK Signing Tools for Desktop Apps");
            Console.WriteLine("  ✓ Windows SDK for UWP Managed Apps");
            Console.WriteLine();
            Console.Write("是否现在打开下载页面? (Y/N): ");
            
            var key = Console.ReadKey();
            Console.WriteLine();
            
            if (key.Key == ConsoleKey.Y)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://developer.microsoft.com/windows/downloads/windows-sdk/",
                        UseShellExecute = true
                    });
                    Console.WriteLine("已打开浏览器，请下载并安装 Windows SDK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"无法打开浏览器: {ex.Message}");
                }
            }
            
            Console.WriteLine();
            Console.WriteLine("安装完成后请重新运行此程序。");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 检查管理员权限
        /// </summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool CheckAdministratorPrivileges()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 显示依赖信息
        /// </summary>
        public static void ShowDependencyInfo()
        {
            Console.WriteLine("=== SparseMsixTool 运行依赖 (旧别名: MsixTool) ===");
            Console.WriteLine();
            Console.WriteLine("注意: 本程序使用 Native AOT 编译，无需安装 .NET Runtime");
            Console.WriteLine();
            Console.WriteLine("1. Windows SDK:");
            Console.WriteLine("   版本: 10.0.19041.0 或更高");
            Console.WriteLine("   下载: https://developer.microsoft.com/windows/downloads/windows-sdk/");
            Console.WriteLine("   所需组件:");
            Console.WriteLine("     - makeappx.exe (MSIX 打包工具)");
            Console.WriteLine("     - signtool.exe (数字签名工具)");
            Console.WriteLine("     - makecert.exe (证书创建工具)");
            Console.WriteLine("     - pvk2pfx.exe (证书转换工具)");
            Console.WriteLine();
            Console.WriteLine("2. 系统组件:");
            Console.WriteLine("   - Windows 10 (Build 19041+) 或 Windows 11");
            Console.WriteLine("   - PowerShell 5.1 或更高 (Windows 10+ 自带)");
            Console.WriteLine("   - 管理员权限 (用于证书安装和包部署)");
            Console.WriteLine();
        }
    }
}
