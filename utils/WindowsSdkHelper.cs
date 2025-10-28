using System.Diagnostics;
using System.Text;
using System.Reflection;

namespace MsixTool.Utils
{
    public static class WindowsSdkHelper
    {
        private static string? _makeAppxPath;
        private static string? _signToolPath;
        private static string? _makeCertPath;

        /// <summary>
        /// 获取应用程序根目录
        /// </summary>
        private static string GetAppDirectory()
        {
            // 获取可执行文件所在目录
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                return Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            }
            return AppContext.BaseDirectory;
        }

        /// <summary>
        /// 查找工具，优先使用系统安装的 Windows SDK
        /// </summary>
        private static string? FindTool(string toolName)
        {
            // 1. 优先查找系统安装的 Windows SDK（自动发现所有版本）
            var systemSdkPath = FindSystemWindowsSdk(toolName);
            if (!string.IsNullOrEmpty(systemSdkPath))
            {
                return systemSdkPath;
            }

            // 2. 在PATH环境变量中查找
            var pathInEnvironment = FindExecutableInPath(toolName);
            if (!string.IsNullOrEmpty(pathInEnvironment))
            {
                return pathInEnvironment;
            }

            // 3. 查找打包的本地工具 (tools/sdk目录) - 作为后备方案
            var appDir = GetAppDirectory();
            var bundledToolPath = Path.Combine(appDir, "tools", "sdk", toolName);
            if (File.Exists(bundledToolPath))
            {
                return bundledToolPath;
            }

            // 4. 在应用目录直接查找
            var appToolPath = Path.Combine(appDir, toolName);
            if (File.Exists(appToolPath))
            {
                return appToolPath;
            }

            return null;
        }

        /// <summary>
        /// 自动发现系统安装的 Windows SDK，支持所有版本
        /// </summary>
        private static string? FindSystemWindowsSdk(string toolName)
        {
            var kitsPaths = new[]
            {
                @"C:\Program Files (x86)\Windows Kits\10\bin",
                @"C:\Program Files\Windows Kits\10\bin"
            };

            foreach (var kitsPath in kitsPaths)
            {
                if (!Directory.Exists(kitsPath))
                    continue;

                try
                {
                    // 获取所有 SDK 版本目录，按版本号降序排序
                    var versionDirs = Directory.GetDirectories(kitsPath)
                        .Where(d => Path.GetFileName(d).StartsWith("10.0."))
                        .OrderByDescending(d => 
                        {
                            var versionStr = Path.GetFileName(d).Replace("10.0.", "");
                            if (Version.TryParse(versionStr, out var version))
                                return version;
                            return new Version(0, 0);
                        })
                        .ToList();

                    // 优先使用最新版本
                    foreach (var versionDir in versionDirs)
                    {
                        var toolPath = Path.Combine(versionDir, "x64", toolName);
                        if (File.Exists(toolPath))
                        {
                            return toolPath;
                        }
                    }
                }
                catch
                {
                    // 忽略错误，继续查找
                }
            }

            return null;
        }

        public static string GetMakeAppxPath()
        {
            if (_makeAppxPath != null) return _makeAppxPath;

            var path = FindTool("makeappx.exe");
            if (path != null)
            {
                _makeAppxPath = path;
                return path;
            }

            throw new FileNotFoundException(
                "未找到 makeappx.exe 工具。\n" +
                "请安装 Windows 10/11 SDK:\n" +
                "  下载地址: https://developer.microsoft.com/windows/downloads/windows-sdk/\n" +
                "或确保已将 SDK 工具目录添加到系统 PATH 环境变量。"
            );
        }

        public static string GetSignToolPath()
        {
            if (_signToolPath != null) return _signToolPath;

            var path = FindTool("signtool.exe");
            if (path != null)
            {
                _signToolPath = path;
                return path;
            }

            throw new FileNotFoundException(
                "未找到 signtool.exe 工具。\n" +
                "请安装 Windows 10/11 SDK:\n" +
                "  下载地址: https://developer.microsoft.com/windows/downloads/windows-sdk/\n" +
                "或确保已将 SDK 工具目录添加到系统 PATH 环境变量。"
            );
        }

        public static string GetMakeCertPath()
        {
            if (_makeCertPath != null) return _makeCertPath;

            var path = FindTool("makecert.exe");
            if (path != null)
            {
                _makeCertPath = path;
                return path;
            }

            throw new FileNotFoundException(
                "未找到 makecert.exe 工具。\n" +
                "请安装 Windows 10/11 SDK:\n" +
                "  下载地址: https://developer.microsoft.com/windows/downloads/windows-sdk/\n" +
                "或确保已将 SDK 工具目录添加到系统 PATH 环境变量。"
            );
        }

        public static string GetPvk2PfxPath()
        {
            var path = FindTool("pvk2pfx.exe");
            if (path != null)
            {
                return path;
            }

            throw new FileNotFoundException(
                "未找到 pvk2pfx.exe 工具。\n" +
                "请安装 Windows 10/11 SDK:\n" +
                "  下载地址: https://developer.microsoft.com/windows/downloads/windows-sdk/\n" +
                "或确保已将 SDK 工具目录添加到系统 PATH 环境变量。"
            );
        }

        private static string? FindExecutableInPath(string executable)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;

            var paths = pathEnv.Split(';');
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                
                var fullPath = Path.Combine(path.Trim(), executable);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        public static async Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(string executable, string arguments, string? workingDirectory = null)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
                };

                using var process = new Process { StartInfo = startInfo };
                
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        outputBuilder.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        errorBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                var output = outputBuilder.ToString();
                var error = errorBuilder.ToString();

                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                return (false, "", $"执行命令失败: {ex.Message}");
            }
        }
    }
}