using SparseMsixTool.Config;
using SparseMsixTool.Utils;
using Windows.Management.Deployment;

namespace SparseMsixTool.Services
{
    public class InstallService
    {
    public async Task InstallPackageAsync(string? sourcePath, string? packageName, string? destPath, bool autoUninstall = true)
        {
            Console.WriteLine("开始安装 MSIX 包...");

            var msixPath = DetermineMsixPath(sourcePath, packageName);
            Console.WriteLine($"安装包路径: {msixPath}");

            if (!File.Exists(msixPath))
            {
                throw new FileNotFoundException($"MSIX 包文件不存在: {msixPath}");
            }

            // 解析包信息
            string parsedPackageName = string.Empty;
            try
            {
                var (pkgName, publisher, version) = ManifestHelper.GetPackageInfoFromMsix(msixPath);
                parsedPackageName = pkgName;
                Console.WriteLine($"解析包信息 -> 名称: {pkgName}, 发布者: {publisher}, 版本: {version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ 解析 MSIX 包信息失败: {ex.Message}");
            }

            // 如果成功解析到包名则尝试卸载旧版本
            if (autoUninstall && !string.IsNullOrEmpty(parsedPackageName))
            {
                Console.WriteLine($"检查是否已安装同名包: {parsedPackageName}");
                try
                {
                    await UninstallExistingPackageAsync(parsedPackageName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ 自动卸载旧版本时出错(忽略继续): {ex.Message}");
                }
            }

            // 如果未指定 destPath，尝试从配置读取
            string? actualDestPath = destPath;
            if (string.IsNullOrEmpty(actualDestPath))
            {
                try
                {
                    var (pkgName, publisher, _) = ManifestHelper.GetPackageInfoFromMsix(msixPath);
                    actualDestPath = ConfigManager.GetSparsePackagePath(publisher, pkgName);
                    if (!string.IsNullOrEmpty(actualDestPath))
                    {
                        Console.WriteLine($"从配置读取到外部程序路径: {actualDestPath}");
                    }
                }
                catch
                {
                    // 读取失败则忽略，继续正常安装流程
                }
            }

            try
            {
                // 使用 PowerShell 安装包
                await InstallUsingPowerShellAsync(msixPath, actualDestPath);
                Console.WriteLine("✓ MSIX 包安装成功");
                
                // 如果指定了 destPath，保存到配置
                if (!string.IsNullOrEmpty(actualDestPath))
                {
                    try
                    {
                        var (pkgName, publisher, _) = ManifestHelper.GetPackageInfoFromMsix(msixPath);
                        ConfigManager.SaveSparsePackagePath(publisher, pkgName, actualDestPath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠ 保存配置失败: {ex.Message}");
                    }
                }

                // 刷新图标缓存，避免开始菜单残留旧图标
                try
                {
                    IconCacheHelper.ClearIconCache();
                }
                catch (Exception iconEx)
                {
                    Console.WriteLine($"⚠ 图标缓存刷新失败: {iconEx.Message}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"安装失败: {ex.Message}", ex);
            }
        }

        public async Task UninstallPackageAsync(string? packageName)
        {
            if (string.IsNullOrEmpty(packageName))
            {
                throw new ArgumentException("必须指定要卸载的包名称");
            }

            Console.WriteLine($"开始卸载包: {packageName}");

            try
            {
                // 使用 PowerShell 卸载包
                await UninstallUsingPowerShellAsync(packageName);
                Console.WriteLine("✓ 包卸载成功");

                // 验证包是否真正卸载（再次查找确认不存在）
                Console.WriteLine("验证卸载结果...");
                var findCommand = $"Get-AppxPackage | Where-Object {{$_.Name -like '*{packageName}*'}} | Select-Object -First 1 -ExpandProperty PackageFullName";
                var (findSuccess, findOutput, findError) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{findCommand}\"");

                if (findSuccess && !string.IsNullOrWhiteSpace(findOutput))
                {
                    Console.WriteLine($"⚠ 警告: 包似乎仍然存在: {findOutput.Trim()}");
                    Console.WriteLine("卸载可能未完全成功，请手动检查");
                }
                else
                {
                    Console.WriteLine("✓ 卸载验证通过：包已不存在");
                }

                // 刷新图标缓存（使用强制模式确保彻底刷新）
                try
                {
                    Console.WriteLine();
                    IconCacheHelper.ClearIconCache(aggressive: true);
                }
                catch (Exception iconEx)
                {
                    Console.WriteLine($"⚠ 图标缓存刷新失败: {iconEx.Message}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"卸载失败: {ex.Message}", ex);
            }
        }

        public async Task FindPackagesAsync(string packageNamePattern)
        {
            Console.WriteLine($"查找包含 '{packageNamePattern}' 的已安装包...");

            try
            {
                var command = $"Get-AppxPackage | Where-Object {{$_.Name -like '*{packageNamePattern}*'}} | Select-Object Name, Version, InstallLocation";
                var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{command}\"");

                if (!success)
                {
                    throw new InvalidOperationException($"查找包失败: {error}");
                }

                if (string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine("未找到匹配的包");
                    return;
                }

                Console.WriteLine("找到以下匹配的包:");
                Console.WriteLine(output);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"查找包失败: {ex.Message}", ex);
            }
        }

        private string DetermineMsixPath(string? sourcePath, string? packageName)
        {
            if (!string.IsNullOrEmpty(sourcePath))
            {
                if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).ToLower() == ".msix")
                {
                    return sourcePath;
                }
                
                if (Directory.Exists(sourcePath))
                {
                    var msixFiles = Directory.GetFiles(sourcePath, "*.msix");
                    if (msixFiles.Length == 1)
                    {
                        return msixFiles[0];
                    }
                    else if (msixFiles.Length > 1)
                    {
                        Console.WriteLine("找到多个 MSIX 文件:");
                        for (int i = 0; i < msixFiles.Length; i++)
                        {
                            Console.WriteLine($"{i + 1}. {Path.GetFileName(msixFiles[i])}");
                        }
                        Console.Write("请选择文件 (输入序号): ");
                        if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= msixFiles.Length)
                        {
                            return msixFiles[choice - 1];
                        }
                        throw new InvalidOperationException("无效的选择");
                    }
                }
            }

            // 在当前目录查找 MSIX 文件
            var currentDir = Environment.CurrentDirectory;
            var msixFilesInCurrent = Directory.GetFiles(currentDir, "*.msix");

            if (!string.IsNullOrEmpty(packageName))
            {
                var matchingFiles = msixFilesInCurrent.Where(f => 
                    Path.GetFileNameWithoutExtension(f).Contains(packageName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                
                if (matchingFiles.Length == 1)
                {
                    return matchingFiles[0];
                }
                else if (matchingFiles.Length > 1)
                {
                    return SelectFromMultipleFiles(matchingFiles);
                }
            }

            if (msixFilesInCurrent.Length == 1)
            {
                return msixFilesInCurrent[0];
            }
            else if (msixFilesInCurrent.Length > 1)
            {
                return SelectFromMultipleFiles(msixFilesInCurrent);
            }

            throw new FileNotFoundException("未找到 MSIX 包文件");
        }

        private string SelectFromMultipleFiles(string[] files)
        {
            Console.WriteLine("找到多个 MSIX 文件:");
            for (int i = 0; i < files.Length; i++)
            {
                Console.WriteLine($"{i + 1}. {Path.GetFileName(files[i])}");
            }
            Console.Write("请选择文件 (输入序号): ");
            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= files.Length)
            {
                return files[choice - 1];
            }
            throw new InvalidOperationException("无效的选择");
        }

        private async Task InstallUsingPowerShellAsync(string msixPath, string? destPath)
        {
            var command = $"Add-AppxPackage -Path '{msixPath}'";
            
            if (!string.IsNullOrEmpty(destPath))
            {
                // 使用 -ExternalLocation 参数指定外部可执行程序路径（稀疏包）
                command = $"Add-AppxPackage -Path '{msixPath}' -ExternalLocation '{destPath}'";
                Console.WriteLine($"使用外部程序路径: {destPath}");
            }

            var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{command}\"");

            if (!success)
            {
                throw new InvalidOperationException($"PowerShell 安装命令失败: {error}");
            }

            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine($"输出: {output}");
            }
        }

        private async Task UninstallUsingPowerShellAsync(string packageName)
        {
            // 首先查找完整的包名
            var findCommand = $"Get-AppxPackage | Where-Object {{$_.Name -like '*{packageName}*'}} | Select-Object -First 1 -ExpandProperty PackageFullName";
            var (findSuccess, findOutput, findError) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{findCommand}\"");

            if (!findSuccess || string.IsNullOrWhiteSpace(findOutput))
            {
                throw new InvalidOperationException($"未找到包含 '{packageName}' 的已安装包");
            }

            var fullPackageName = findOutput.Trim();
            Console.WriteLine($"找到包: {fullPackageName}");

            var removeCommand = $"Remove-AppxPackage -Package '{fullPackageName}'";
            var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{removeCommand}\"");

            if (!success)
            {
                throw new InvalidOperationException($"PowerShell 卸载命令失败: {error}");
            }

            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine($"输出: {output}");
            }
        }

        /// <summary>
        /// 注册稀疏包（使用清单文件和外部位置）
        /// </summary>
        public async Task RegisterSparsePackageAsync(string manifestPath, string externalLocation)
        {
            Console.WriteLine("开始注册稀疏包...");
            Console.WriteLine($"清单路径: {manifestPath}");
            Console.WriteLine($"外部程序位置: {externalLocation}");

            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"清单文件不存在: {manifestPath}");
            }

            if (!Directory.Exists(externalLocation))
            {
                throw new DirectoryNotFoundException($"外部程序目录不存在: {externalLocation}");
            }

            try
            {
                // 使用 Add-AppxPackage -Register 注册稀疏包
                var command = $"Add-AppxPackage -Register '{manifestPath}' -ExternalLocation '{externalLocation}'";
                var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{command}\"");

                if (!success)
                {
                    throw new InvalidOperationException($"稀疏包注册失败: {error}");
                }

                if (!string.IsNullOrEmpty(output))
                {
                    Console.WriteLine($"输出: {output}");
                }

                Console.WriteLine("✓ 稀疏包注册成功");
                try
                {
                    IconCacheHelper.ClearIconCache();
                }
                catch (Exception iconEx)
                {
                    Console.WriteLine($"⚠ 图标缓存刷新失败: {iconEx.Message}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"注册失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 安装稀疏包（使用 PackageManager API 和外部位置）
        /// </summary>
    public async Task InstallSparsePackageAsync(string msixPath, string externalLocation, bool autoUninstall = true)
        {
            Console.WriteLine("开始安装稀疏包...");
            Console.WriteLine($"包路径: {msixPath}");
            Console.WriteLine($"外部程序位置: {externalLocation}");

            if (!File.Exists(msixPath))
            {
                throw new FileNotFoundException($"MSIX 包文件不存在: {msixPath}");
            }

            if (!Directory.Exists(externalLocation))
            {
                throw new DirectoryNotFoundException($"外部程序目录不存在: {externalLocation}");
            }

            // 从 MSIX 获取包信息
            var (packageName, publisher, version) = ManifestHelper.GetPackageInfoFromMsix(msixPath);
            Console.WriteLine($"包名: {packageName}");
            Console.WriteLine($"发布者: {publisher}");
            Console.WriteLine($"版本: {version}");

            // 检查是否已安装同名包
            if (autoUninstall)
            {
                try
                {
                    await UninstallExistingPackageAsync(packageName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ 检查已安装包时出错: {ex.Message}");
                }
            }

            await Task.Run(() =>
            {
                try
                {
                    Uri locationUri = new Uri(externalLocation);
                    Uri packageUri = new Uri(msixPath);

                    PackageManager packageManager = new PackageManager();

                    // 声明使用外部位置
                    var options = new AddPackageOptions();
                    options.ExternalLocationUri = locationUri;

                    Console.WriteLine("正在部署稀疏包...");
                    var deploymentOperation = packageManager.AddPackageByUriAsync(packageUri, options);

                    // 等待操作完成
                    var opCompletedEvent = new System.Threading.ManualResetEvent(false);
                    deploymentOperation.Completed = (depProgress, status) => { opCompletedEvent.Set(); };
                    opCompletedEvent.WaitOne();

                    if (deploymentOperation.Status == Windows.Foundation.AsyncStatus.Error)
                    {
                        var errorCode = deploymentOperation.ErrorCode;
                        var errorText = deploymentOperation.GetResults().ErrorText;
                        throw new InvalidOperationException($"稀疏包安装失败: 错误代码 0x{errorCode.HResult:X8}, {errorText}");
                    }

                    if (deploymentOperation.Status != Windows.Foundation.AsyncStatus.Completed)
                    {
                        throw new InvalidOperationException($"稀疏包安装失败: 状态 {deploymentOperation.Status}");
                    }

                    Console.WriteLine("✓ 稀疏包安装成功");
                    
                    // 保存外部程序路径到配置
                    ConfigManager.SaveSparsePackagePath(publisher, packageName, externalLocation);
                    
                    try
                    {
                        IconCacheHelper.ClearIconCache();
                    }
                    catch (Exception iconEx)
                    {
                        Console.WriteLine($"⚠ 图标缓存刷新失败: {iconEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"安装失败: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 卸载已安装的包（如果存在）
        /// </summary>
        private async Task UninstallExistingPackageAsync(string packageName)
        {
            var findCommand = $"Get-AppxPackage | Where-Object {{$_.Name -eq '{packageName}'}} | Select-Object -First 1 -ExpandProperty PackageFullName";
            var (findSuccess, findOutput, findError) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{findCommand}\"");

            if (findSuccess && !string.IsNullOrWhiteSpace(findOutput))
            {
                var fullPackageName = findOutput.Trim();
                Console.WriteLine($"发现已安装的包: {fullPackageName}");
                Console.WriteLine("正在卸载旧版本...");

                var removeCommand = $"Remove-AppxPackage -Package '{fullPackageName}'";
                var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{removeCommand}\"");

                if (success)
                {
                    Console.WriteLine("✓ 旧版本已卸载");
                }
                else
                {
                    Console.WriteLine($"⚠ 卸载旧版本失败: {error}");
                }
            }
        }
    }
}
