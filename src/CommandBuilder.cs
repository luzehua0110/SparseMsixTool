using System.CommandLine;
using SparseMsixTool.Services;
using SparseMsixTool.Utils;
using SparseMsixTool.Config;

namespace SparseMsixTool.Commands
{
    public static class CommandBuilder
    {
        private static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"错误: {message}");
            Console.ResetColor();
        }

        private static void PrintWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ {message}");
            Console.ResetColor();
        }

        public static Command CreatePackCommand()
        {
            var packCommand = new Command("pack", "打包稀疏包，将appmanifest.xml和其它必要资源打包成msix稀疏包（不包含签名）");
            
            var srcOption = new Option<string?>(
                aliases: new[] { "--src", "-s" },
                description: "源路径，稀疏包资源配置路径"
            );
            var nameOption = new Option<string?>(
                aliases: new[] { "--name", "-n" },
                description: "包名称"
            );

            packCommand.AddOption(srcOption);
            packCommand.AddOption(nameOption);
            
            packCommand.SetHandler(async (src, name) =>
            {
                try
                {
                    var packService = new PackageService();
                    await packService.PackSparsePackageAsync(src, name);
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, srcOption, nameOption);

            return packCommand;
        }

        public static Command CreateInstallCommand()
        {
            var installCommand = new Command("install", "安装 msix 包 (支持 --dest 省略回退此前使用的外部程序路径)");
            
            var srcOption = new Option<string?>(
                aliases: new[] { "--src", "-s" },
                description: "msix 包路径"
            );
            var destOption = new Option<string?>(
                aliases: new[] { "--dest", "-d" },
                description: "外部程序路径（稀疏包需要，可省略则自动使用最近一次成功安装的路径）"
            );
            var noAutoUninstallOption = new Option<bool>(
                aliases: new[] { "--no-auto-uninstall" },
                description: "不自动卸载已安装的同名包"
            );

            installCommand.AddOption(srcOption);
            installCommand.AddOption(destOption);
            installCommand.AddOption(noAutoUninstallOption);

            installCommand.SetHandler(async (src, dest, noAuto) =>
            {
                try
                {
                    // 回退逻辑: 如果未提供 dest, 尝试使用配置中的 LastExternalAppPath
                    var finalDest = dest;
                    if (string.IsNullOrWhiteSpace(finalDest))
                    {
                        finalDest = ConfigManager.GetLastExternalAppPath();
                        if (!string.IsNullOrWhiteSpace(finalDest))
                        {
                            Console.WriteLine($"使用历史外部程序路径: {finalDest}");
                        }
                    }

                    var installService = new InstallService();
                    await installService.InstallPackageAsync(src, null, finalDest, autoUninstall: !noAuto);

                    // 安装成功且提供了有效外部路径则保存
                    if (!string.IsNullOrWhiteSpace(finalDest))
                    {
                        ConfigManager.SaveLastExternalAppPath(finalDest);
                    }
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, srcOption, destOption, noAutoUninstallOption);

            return installCommand;
        }

        public static Command CreateUninstallCommand()
        {
            var uninstallCommand = new Command("uninstall", "卸载msix包");
            
            var nameArg = new Argument<string?>(
                name: "packageName",
                description: "包名称（支持模糊匹配）",
                getDefaultValue: () => null
            );

            uninstallCommand.AddArgument(nameArg);

            uninstallCommand.SetHandler(async (name) =>
            {
                try
                {
                    var installService = new InstallService();
                    await installService.UninstallPackageAsync(name);
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, nameArg);

            return uninstallCommand;
        }

        public static Command CreateMakeCertCommand()
        {
            var makeCertCommand = new Command("makecert", "创建自签名证书");
            
            var publisherOption = new Option<string?>(
                aliases: new[] { "--publisher", "-p" },
                description: "发布者DN，例如 'CN=MyCompany'"
            );
            var destOption = new Option<string?>(
                aliases: new[] { "--dest", "-d" },
                description: "保存证书的目录路径"
            );
            var passwordOption = new Option<string?>(
                aliases: new[] { "--password", "-pwd" },
                description: "证书密码（可选）"
            );

            makeCertCommand.AddOption(publisherOption);
            makeCertCommand.AddOption(destOption);
            makeCertCommand.AddOption(passwordOption);

            makeCertCommand.SetHandler(async (publisher, dest, password) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(publisher))
                    {
                        PrintError("必须提供 --publisher 参数");
                        Environment.Exit(1);
                        return;
                    }

                    var certService = new CertificateService();
                    await certService.CreateCertificateAsync(publisher, dest, password);
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, publisherOption, destOption, passwordOption);

            return makeCertCommand;
        }

        public static Command CreateSignCommand()
        {
            var signCommand = new Command("sign", "为msix包签名");
            
            var srcOption = new Option<string?>(
                aliases: new[] { "--src", "-s" },
                description: "msix包路径"
            );
            var certOption = new Option<string?>(
                aliases: new[] { "--cert", "-c" },
                description: "证书路径（.pfx文件），若提供则必须同时提供 --password"
            );
            var passwordOption = new Option<string?>(
                aliases: new[] { "--password", "-pwd" },
                description: "证书密码（提供 --cert 时必填）"
            );

            signCommand.AddOption(srcOption);
            signCommand.AddOption(certOption);
            signCommand.AddOption(passwordOption);

            signCommand.SetHandler(async (src, cert, password) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(src))
                    {
                        PrintError("必须提供 --src 参数 (MSIX 包路径)");
                        Console.WriteLine("用法: sparsemsix sign --src <包路径> [--cert <证书路径> --password <密码>]");
                        Environment.Exit(1);
                        return;
                    }

                    // 校验：提供 --cert 时必须同时提供 --password
                    if (!string.IsNullOrWhiteSpace(cert) && string.IsNullOrWhiteSpace(password))
                    {
                        PrintError("提供 --cert 参数时必须同时提供 --password 参数");
                        Console.WriteLine("用法: sparsemsix sign --src <包路径> --cert <证书路径> --password <密码>");
                        Environment.Exit(1);
                        return;
                    }

                    var packService = new PackageService();
                    await packService.SignPackageAsync(src!, cert, password);
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, srcOption, certOption, passwordOption);

            return signCommand;
        }

        public static Command CreatePackAndInstallCommand()
        {
            var packInstallCommand = new Command("packandinstall", "打包、签名并安装稀疏包（支持 --dest 省略使用最近路径）");
            
            var srcOption = new Option<string?>(
                aliases: new[] { "--src", "-s" },
                description: "源路径，稀疏包资源配置路径"
            );
            var nameOption = new Option<string?>(
                aliases: new[] { "--name", "-n" },
                description: "包名称（可选）"
            );
            var certOption = new Option<string?>(
                aliases: new[] { "--cert", "-c" },
                description: "证书路径（可选，不提供则使用上次使用的值或自动创建），若提供则必须同时提供 --password"
            );
            var passwordOption = new Option<string?>(
                aliases: new[] { "--password", "-pwd" },
                description: "证书密码（提供 --cert 时必填）"
            );
            var destOption = new Option<string?>(
                aliases: new[] { "--dest", "-d" },
                description: "外部程序路径（可省略，将自动使用最近一次成功安装的路径）"
            );
            var noAutoUninstallOption = new Option<bool>(
                aliases: new[] { "--no-auto-uninstall" },
                description: "不自动卸载已安装的同名包"
            );

            packInstallCommand.AddOption(srcOption);
            packInstallCommand.AddOption(nameOption);
            packInstallCommand.AddOption(certOption);
            packInstallCommand.AddOption(passwordOption);
            packInstallCommand.AddOption(destOption);
            packInstallCommand.AddOption(noAutoUninstallOption);

            packInstallCommand.SetHandler(async (src, name, cert, password, dest, noAuto) =>
            {
                try
                {
                    // 校验：提供 --cert 时必须同时提供 --password
                    if (!string.IsNullOrWhiteSpace(cert) && string.IsNullOrWhiteSpace(password))
                    {
                        PrintError("提供 --cert 参数时必须同时提供 --password 参数");
                        Console.WriteLine("用法: sparsemsix packandinstall --cert <证书路径> --password <密码> [其他选项]");
                        Environment.Exit(1);
                        return;
                    }

                    // 回退逻辑
                    var finalDest = dest;
                    if (string.IsNullOrWhiteSpace(finalDest))
                    {
                        finalDest = ConfigManager.GetLastExternalAppPath();
                        if (!string.IsNullOrWhiteSpace(finalDest))
                        {
                            Console.WriteLine($"使用历史外部程序路径: {finalDest}");
                        }
                    }
                    if (string.IsNullOrWhiteSpace(finalDest))
                    {
                        PrintError("未提供 --dest 且无历史路径可以回退，必须指定外部程序路径。");
                        Console.WriteLine("用法: sparsemsix packandinstall --dest <exe路径> [其他选项]");
                        Environment.Exit(1);
                        return;
                    }

                    // 获取清单路径（用于后续证书和资源处理）
                    var sourcePath = src ?? Environment.CurrentDirectory;
                    var manifestPath = ManifestHelper.FindManifestPath(sourcePath);
                    var manifestDir = Path.GetDirectoryName(manifestPath) ?? sourcePath;

                    // 处理稀疏包：创建临时打包目录，只包含清单、资源和占位符exe
                    Console.WriteLine($"\n正在准备稀疏包...");
                    Console.WriteLine($"外部程序目录: {finalDest}");
                    
                    // 创建临时打包目录
                    var tempPackDir = Path.Combine(Path.GetTempPath(), $"MsixSparse_{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempPackDir);
                    Console.WriteLine($"临时打包目录: {tempPackDir}");

                    try
                    {
                        // 复制清单文件
                        File.Copy(manifestPath, Path.Combine(tempPackDir, "AppxManifest.xml"), true);
                        
                        // 获取清单中引用的资源文件
                        var resources = ResourceHelper.GetReferencedResources(manifestPath);
                        Console.WriteLine($"检测到 {resources.Count} 个资源文件需要打包");
                        
                        // 复制资源到临时目录（打包到MSIX中）
                        ResourceHelper.CopyResourcesToDestination(manifestDir, tempPackDir, resources, showMessage: false);
                        
                        // 在临时目录中创建占位符exe（仅用于打包）
                        var exeName = Path.GetFileName(finalDest);
                        var placeholderExePath = Path.Combine(tempPackDir, exeName);
                        File.WriteAllBytes(placeholderExePath, new byte[] { 0x4D, 0x5A }); // MZ header
                        Console.WriteLine($"✓ 已创建占位符程序: {exeName}");

                        // 第1步: 打包成MSIX（使用临时目录）
                        Console.WriteLine();
                        Console.WriteLine("======== 第1步: 打包 ========");
                        var packService = new PackageService();
                        var msixPath = await packService.PackSparsePackageAsync(tempPackDir, name);
                        Console.WriteLine($"✓ MSIX 包已创建: {msixPath}");
                        
                        // 将 MSIX 移动到源目录的父目录
                        var targetDir = Directory.GetParent(manifestDir)?.FullName ?? manifestDir;
                        var finalMsixPath = Path.Combine(targetDir, Path.GetFileName(msixPath));
                        
                        if (File.Exists(finalMsixPath))
                        {
                            try
                            {
                                var timestamp = BackupConstants.GenerateTimestamp();
                                var nameWithoutExt = Path.GetFileNameWithoutExtension(finalMsixPath);
                                var extension = Path.GetExtension(finalMsixPath);
                                var backupName = $"{nameWithoutExt}_{timestamp}{extension}";
                                var backupPath = Path.Combine(targetDir, backupName);
                                File.Move(finalMsixPath, backupPath);
                                Console.WriteLine($"✓ 已备份现有文件: {Path.GetFileName(finalMsixPath)} -> {backupName}");
                            }
                            catch (Exception ex)
                            {
                                PrintWarning($"备份现有文件失败: {ex.Message}，将删除旧文件");
                                try { File.Delete(finalMsixPath); } catch { }
                            }
                        }
                        
                        File.Move(msixPath, finalMsixPath);
                        msixPath = finalMsixPath;
                        Console.WriteLine($"✓ MSIX 包已保存到: {msixPath}");
                        
                        // 第2步: 处理证书
                        Console.WriteLine();
                        Console.WriteLine("======== 第2步: 准备签名证书 ========");
                        string? actualCert = cert;
                        if (string.IsNullOrEmpty(actualCert))
                        {
                            Console.WriteLine("未提供证书参数，正在查找或创建证书...");
                            var publisher = ManifestHelper.GetPublisherFromManifest(manifestPath);
                            var certService = new CertificateService();
                            actualCert = await certService.GetOrCreateCertificateAsync(publisher, sourcePath);
                        }

                        // 第3步: 签名MSIX包
                        Console.WriteLine();
                        Console.WriteLine("======== 第3步: 签名 ========");
                        await packService.SignPackageAsync(msixPath, actualCert, password);

                        // 第4步: 复制资源到外部程序目录
                        Console.WriteLine();
                        Console.WriteLine("======== 第4步: 复制资源到外部程序目录 ========");
                        var destDir = Path.GetDirectoryName(finalDest);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            ResourceHelper.CopyResourcesToDestination(manifestDir, destDir, resources, showMessage: resources.Any());
                        }
                        
                        // 第5步: 安装稀疏包
                        Console.WriteLine();
                        Console.WriteLine("======== 第5步: 安装稀疏包 ========");
                        var installService = new InstallService();
                        await installService.InstallSparsePackageAsync(msixPath, finalDest, autoUninstall: !noAuto);

                        // 保存路径供下次回退
                        ConfigManager.SaveLastExternalAppPath(finalDest);

                        Console.WriteLine();
                        Console.WriteLine("========================================");
                        Console.WriteLine("✓ 所有步骤完成！稀疏包已成功安装");
                        Console.WriteLine($"  MSIX 包: {msixPath}");
                        Console.WriteLine($"  外部程序: {finalDest}");
                        Console.WriteLine("========================================");
                    }
                    finally
                    {
                        if (tempPackDir != null && Directory.Exists(tempPackDir))
                        {
                            try { Directory.Delete(tempPackDir, true); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, srcOption, nameOption, certOption, passwordOption, destOption, noAutoUninstallOption);

            return packInstallCommand;
        }

        public static Command CreateFindCommand()
        {
            var findCommand = new Command("find", "查找已安装的包");
            
            var packageNameArg = new Argument<string?>(
                name: "packageName",
                description: "包名称（支持模糊匹配）",
                getDefaultValue: () => null
            );

            findCommand.AddArgument(packageNameArg);

            findCommand.SetHandler(async (packageName) =>
            {
                try
                {
                    var installService = new InstallService();
                    await installService.FindPackagesAsync(packageName ?? "");
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, packageNameArg);

            return findCommand;
        }

        public static Command CreateDepsCommand()
        {
            var depsCommand = new Command("deps", "检查运行依赖和显示依赖信息");
            
            var checkOption = new Option<bool>(
                aliases: new[] { "--check", "-c" },
                description: "执行依赖检查"
            );
            checkOption.SetDefaultValue(false);

            var infoOption = new Option<bool>(
                aliases: new[] { "--info", "-i" },
                description: "显示依赖信息"
            );
            infoOption.SetDefaultValue(false);

            depsCommand.AddOption(checkOption);
            depsCommand.AddOption(infoOption);
            
            depsCommand.SetHandler(async (check, info) =>
            {
                try
                {
                    if (info || (!check && !info))
                    {
                        DependencyChecker.ShowDependencyInfo();
                    }

                    if (check || (!check && !info))
                    {
                        Console.WriteLine();
                        await DependencyChecker.CheckAllDependenciesAsync(autoInstall: false);
                    }
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, checkOption, infoOption);

            return depsCommand;
        }

        public static Command CreatePackAndSignCommand()
        {
            var packSignCommand = new Command("packandsign", "打包并签名稀疏包（不安装）");
            
            var srcOption = new Option<string?>(
                aliases: new[] { "--src", "-s" },
                description: "源路径，稀疏包资源配置路径"
            );
            var nameOption = new Option<string?>(
                aliases: new[] { "--name", "-n" },
                description: "包名称（可选）"
            );
            var certOption = new Option<string?>(
                aliases: new[] { "--cert", "-c" },
                description: "证书路径（可选，不提供则使用上次使用的值或自动创建），若提供则必须同时提供 --password"
            );
            var passwordOption = new Option<string?>(
                aliases: new[] { "--password", "-pwd" },
                description: "证书密码（提供 --cert 时必填）"
            );
            var destOption = new Option<string?>(
                aliases: new[] { "--dest", "-d" },
                description: "外部程序路径（可选，提供时会创建占位符）"
            );

            packSignCommand.AddOption(srcOption);
            packSignCommand.AddOption(nameOption);
            packSignCommand.AddOption(certOption);
            packSignCommand.AddOption(passwordOption);
            packSignCommand.AddOption(destOption);

            packSignCommand.SetHandler(async (src, name, cert, password, dest) =>
            {
                try
                {
                    // 校验：提供 --cert 时必须同时提供 --password
                    if (!string.IsNullOrWhiteSpace(cert) && string.IsNullOrWhiteSpace(password))
                    {
                        PrintError("提供 --cert 参数时必须同时提供 --password 参数");
                        Console.WriteLine("用法: sparsemsix packandsign --cert <证书路径> --password <密码> [其他选项]");
                        Environment.Exit(1);
                        return;
                    }

                    // 获取清单路径
                    var sourcePath = src ?? Environment.CurrentDirectory;
                    var manifestPath = ManifestHelper.FindManifestPath(sourcePath);
                    var manifestDir = Path.GetDirectoryName(manifestPath) ?? sourcePath;

                    string packDir;
                    string? tempPackDir = null;

                    // 如果提供了 dest，创建临时打包目录并复制资源+创建占位符
                    if (!string.IsNullOrEmpty(dest))
                    {
                        Console.WriteLine($"\n正在准备稀疏包...");
                        Console.WriteLine($"外部程序目录: {dest}");
                        
                        tempPackDir = Path.Combine(Path.GetTempPath(), $"MsixSparse_{Guid.NewGuid():N}");
                        Directory.CreateDirectory(tempPackDir);
                        Console.WriteLine($"临时打包目录: {tempPackDir}");
                        packDir = tempPackDir;
                    }
                    else
                    {
                        // 不提供 dest，直接使用源目录打包（假设已包含所有必要文件）
                        Console.WriteLine($"\n使用源目录直接打包: {manifestDir}");
                        packDir = manifestDir;
                    }

                    try
                    {
                        // 如果使用临时目录，需要复制资源和创建占位符
                        if (tempPackDir != null && !string.IsNullOrEmpty(dest))
                        {
                            // 复制清单文件
                            File.Copy(manifestPath, Path.Combine(tempPackDir, "AppxManifest.xml"), true);
                            
                            // 获取并复制资源
                            var resources = ResourceHelper.GetReferencedResources(manifestPath);
                            Console.WriteLine($"检测到 {resources.Count} 个资源文件需要打包");
                            ResourceHelper.CopyResourcesToDestination(manifestDir, tempPackDir, resources, showMessage: false);
                            
                            // 创建占位符exe
                            var exeName = Path.GetFileName(dest);
                            var placeholderExePath = Path.Combine(tempPackDir, exeName);
                            File.WriteAllBytes(placeholderExePath, new byte[] { 0x4D, 0x5A });
                            Console.WriteLine($"✓ 已创建占位符程序: {exeName}");
                        }

                        // 打包
                        Console.WriteLine();
                        Console.WriteLine("======== 第1步: 打包 ========");
                        var packService = new PackageService();
                        var msixPath = await packService.PackSparsePackageAsync(packDir, name);
                        Console.WriteLine($"✓ MSIX 包已创建: {msixPath}");
                        
                        // 目标目录与最终文件路径
                        var targetDir = Directory.GetParent(manifestDir)?.FullName ?? manifestDir;
                        var finalMsixPath = Path.Combine(targetDir, Path.GetFileName(msixPath));

                        // 仅在使用临时目录并且生成文件不在目标目录时执行移动与备份逻辑
                        bool producedInTemp = tempPackDir != null && !msixPath.Equals(finalMsixPath, StringComparison.OrdinalIgnoreCase);
                        if (producedInTemp)
                        {
                            if (File.Exists(finalMsixPath))
                            {
                                try
                                {
                                    var timestamp = BackupConstants.GenerateTimestamp();
                                    var nameWithoutExt = Path.GetFileNameWithoutExtension(finalMsixPath);
                                    var extension = Path.GetExtension(finalMsixPath);
                                    var backupName = $"{nameWithoutExt}_{timestamp}{extension}";
                                    var backupPath = Path.Combine(targetDir, backupName);
                                    File.Move(finalMsixPath, backupPath);
                                    Console.WriteLine($"✓ 已备份现有文件: {Path.GetFileName(finalMsixPath)} -> {backupName}");
                                }
                                catch (Exception ex)
                                {
                                    PrintWarning($"备份现有文件失败: {ex.Message}，将删除旧文件");
                                    try { File.Delete(finalMsixPath); } catch { }
                                }
                            }

                            try
                            {
                                File.Move(msixPath, finalMsixPath);
                                msixPath = finalMsixPath;
                                Console.WriteLine($"✓ MSIX 包已保存到: {msixPath}");
                            }
                            catch (Exception moveEx)
                            {
                                throw new InvalidOperationException($"移动生成的 MSIX 文件失败: {moveEx.Message}");
                            }
                        }
                        else
                        {
                            // 文件已在目标目录，无需移动
                            Console.WriteLine($"✓ MSIX 包已生成（无需移动）: {msixPath}");
                        }
                        
                        // 处理证书
                        Console.WriteLine();
                        Console.WriteLine("======== 第2步: 准备签名证书 ========");
                        string? actualCert = cert;
                        
                        if (string.IsNullOrEmpty(actualCert))
                        {
                            Console.WriteLine("未提供证书参数，正在查找或创建证书...");
                            var publisher = ManifestHelper.GetPublisherFromManifest(manifestPath);
                            var certService = new CertificateService();
                            actualCert = await certService.GetOrCreateCertificateAsync(publisher, sourcePath);
                        }

                        // 签名
                        Console.WriteLine();
                        Console.WriteLine("======== 第3步: 签名 ========");
                        await packService.SignPackageAsync(msixPath, actualCert, password);

                        Console.WriteLine();
                        Console.WriteLine("========================================");
                        Console.WriteLine("✓ 打包和签名完成！");
                        Console.WriteLine($"  MSIX 包: {msixPath}");
                        Console.WriteLine("========================================");
                    }
                    finally
                    {
                        if (Directory.Exists(tempPackDir))
                        {
                            try
                            {
                                Directory.Delete(tempPackDir, true);
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, srcOption, nameOption, certOption, passwordOption, destOption);

            return packSignCommand;
        }

        public static Command CreateFindCertCommand()
        {
            var findCertCommand = new Command("findcert", "查找证书");
            
            var nameArg = new Argument<string?>(
                name: "certName",
                description: "证书名称（支持模糊匹配，可选）",
                getDefaultValue: () => null
            );

            findCertCommand.AddArgument(nameArg);

            findCertCommand.SetHandler(async (name) =>
            {
                try
                {
                    var certService = new CertificateService();
                    await certService.FindCertificatesAsync(name);
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, nameArg);

            return findCertCommand;
        }

        public static Command CreateDelCertCommand()
        {
            var delCertCommand = new Command("delcert", "删除证书");

            var pathArg = new Argument<string?>(
                name: "certPath",
                description: "证书文件路径、证书主题名称或证书指纹（Thumbprint）"
            );

            var systemOption = new Option<bool>(
                aliases: new[] { "--system", "-s" },
                description: "同时从系统证书存储中移除"
            );
            systemOption.SetDefaultValue(true);

            delCertCommand.AddArgument(pathArg);
            delCertCommand.AddOption(systemOption);

            delCertCommand.SetHandler(async (path, system) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        PrintError("必须提供证书路径、名称或指纹参数");
                        Console.WriteLine("用法: sparsemsix delcert <证书路径|名称|指纹> [--system]");
                        Console.WriteLine("示例:");
                        Console.WriteLine("  sparsemsix delcert HelloKitty");
                        Console.WriteLine("  sparsemsix delcert CN=HelloKitty");
                        Console.WriteLine("  sparsemsix delcert C:\\certs\\HelloKitty.pfx");
                        Console.WriteLine("  sparsemsix delcert 5838D6C78689EEABBDB0E511DBFA8D273A3456AF");
                        Environment.Exit(1);
                        return;
                    }

                    var certService = new CertificateService();
                    await certService.DeleteCertificateAsync(path, system);
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    Environment.Exit(1);
                }
            }, pathArg, systemOption);

            return delCertCommand;
        }
    }
}
