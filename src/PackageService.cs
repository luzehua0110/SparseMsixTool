using SparseMsixTool.Config;
using SparseMsixTool.Utils;

namespace SparseMsixTool.Services
{
    public class PackageService
    {
        public async Task<string> PackSparsePackageAsync(string? sourcePath, string? packageName)
        {
            Console.WriteLine("开始打包稀疏包...");

            // 确定源路径
            var actualSourcePath = DetermineSourcePath(sourcePath, packageName);
            Console.WriteLine($"使用源路径: {actualSourcePath}");

            // 验证清单文件
            var manifestPath = ManifestHelper.FindManifestPath(actualSourcePath);
            ManifestHelper.ValidateManifest(manifestPath);

            var (pkgName, publisher, version) = ManifestHelper.GetPackageInfo(manifestPath);
            
            // 确定输出路径和文件名
            var outputDir = Path.GetDirectoryName(actualSourcePath) ?? actualSourcePath;
            var msixFileName = $"{pkgName}_{version}.msix";
            var msixPath = Path.Combine(outputDir, msixFileName);

            // 如果存在同名文件，重命名旧文件
            if (File.Exists(msixPath))
            {
                RenameExistingFile(msixPath);
            }

            try
            {
                // 使用 makeappx 打包（稀疏包跳过验证，允许exe不在包中）
                await CreateMsixPackageAsync(actualSourcePath, msixPath, skipValidation: true);
                
                Console.WriteLine($"✓ 稀疏包打包完成: {msixPath}");
                Console.WriteLine($"提示: 使用 'sparsemsix sign --src \"{msixPath}\" --cert <证书路径>' 命令对包进行签名 (旧别名: msixtool)");
                return msixPath;
            }
            catch (Exception ex)
            {
                // 清理失败的包文件
                if (File.Exists(msixPath))
                {
                    try { File.Delete(msixPath); } catch { }
                }
                throw new InvalidOperationException($"打包失败: {ex.Message}", ex);
            }
        }

        public async Task SignPackageAsync(string packagePath, string? certificatePath, string? certificatePassword)
        {
            Console.WriteLine("开始签名 MSIX 包...");
            
            if (!File.Exists(packagePath))
            {
                throw new FileNotFoundException($"未找到要签名的包文件: {packagePath}");
            }

            // 获取包的发布者信息
            var tempDir = Path.Combine(Path.GetTempPath(), $"msix_sign_{Guid.NewGuid()}");
            string publisher = "";
            string? actualCertPath = null;
            string? certPassword = null;
            
            try
            {
                Directory.CreateDirectory(tempDir);
                // 优先尝试直接从 MSIX 包中读取清单 (适用于稀疏包，避免 manifest 引用外部 EXE 导致 unpack 验证失败)
                bool directReadSucceeded = false;
                try
                {
                    var (pkgName, publisherFromZip, versionFromZip) = ManifestHelper.GetPackageInfoFromMsix(packagePath);
                    publisher = publisherFromZip;
                    directReadSucceeded = true;
                    Console.WriteLine($"✓ 直接读取清单成功: {pkgName} / {publisher} / {versionFromZip}");
                }
                catch (Exception directEx)
                {
                    Console.WriteLine($"⚠ 直接读取清单失败，尝试使用 makeappx 解包: {directEx.Message}");
                }

                if (!directReadSucceeded)
                {
                    // 解包包以读取清单 (对普通完整包有效)
                    var makeAppxPath = WindowsSdkHelper.GetMakeAppxPath();
                    var extractArgs = $"unpack /p \"{packagePath}\" /d \"{tempDir}\" /o";
                    Console.WriteLine($"执行解包命令: {Path.GetFileName(makeAppxPath)} {extractArgs}");
                    var (extractSuccess, extractOutput, extractError) = await WindowsSdkHelper.ExecuteCommandAsync(makeAppxPath, extractArgs);

                    if (!extractSuccess)
                    {
                        var errorMsg = string.IsNullOrEmpty(extractError) ? extractOutput : extractError;
                        // 如果包含典型的稀疏包验证错误 (文件不存在)，提示用户使用 packandinstall 或确认这是稀疏包
                        if (errorMsg.Contains("Manifest validation error") && errorMsg.Contains("doesn't exist in the package"))
                        {
                            throw new InvalidOperationException(
                                "检测到稀疏包签名失败：manifest 引用了不在包内的文件。\n" +
                                "解决方案:\n" +
                                "  1. 直接使用 packandinstall 命令完成打包+签名+安装\n" +
                                "  2. 或者确保需要签名的 MSIX 是完整包 (所有引用文件均在包内)\n" +
                                "  3. 若必须签名稀疏包，请使用工具生成的包而不是手动修改 manifest。\n" +
                                "原始错误:\n" + errorMsg);
                        }
                        throw new InvalidOperationException($"无法读取包信息: {errorMsg}");
                    }

                    var manifestPath = ManifestHelper.FindManifestPath(tempDir);
                    var (_, publisherFromManifest, _) = ManifestHelper.GetPackageInfo(manifestPath);
                    publisher = publisherFromManifest;
                }
                
                // 确定证书路径（如果需要创建，会在这里创建并立即安装）
                actualCertPath = await DetermineCertificatePathForSignAsync(certificatePath, certificatePassword, publisher, packagePath);
                
                // 获取证书密码
                var certConfig = ConfigManager.GetCertificateConfig(publisher);
                if (!string.IsNullOrEmpty(certificatePassword))
                {
                    certPassword = certificatePassword;
                }
                else
                {
                    certPassword = certConfig?.Password ?? "1234567";
                }
                
                // 验证证书
                Console.WriteLine("验证签名证书...");
                if (!CertificateValidator.ValidateCertificate(actualCertPath, certPassword))
                {
                    throw new InvalidOperationException("证书验证失败，无法继续签名");
                }

                // 验证证书是否匹配发布者
                CertificateValidator.VerifyCertificateMatchesPublisher(actualCertPath, publisher, certPassword);
                
                var signToolPath = WindowsSdkHelper.GetSignToolPath();
                var arguments = $"sign /a /f \"{actualCertPath}\" /p \"{certPassword}\" /fd SHA256 \"{packagePath}\"";

                Console.WriteLine($"执行签名命令...");
                
                var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync(signToolPath, arguments);

                if (!success)
                {
                    throw new InvalidOperationException($"signtool 签名失败:\n{error}\n{output}");
                }

                Console.WriteLine("✓ MSIX 包签名成功");
                if (!string.IsNullOrEmpty(output))
                {
                    Console.WriteLine($"输出: {output}");
                }
            }
            finally
            {
                // 清理临时目录
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }

        private string DetermineSourcePath(string? sourcePath, string? packageName)
        {
            if (!string.IsNullOrEmpty(sourcePath))
            {
                if (!Directory.Exists(sourcePath))
                {
                    throw new DirectoryNotFoundException($"指定的源路径不存在: {sourcePath}");
                }
                return sourcePath;
            }

            var currentDir = Environment.CurrentDirectory;

            // 如果指定了包名，先查找同名文件夹
            if (!string.IsNullOrEmpty(packageName))
            {
                var namedPath = Path.Combine(currentDir, packageName);
                if (Directory.Exists(namedPath))
                {
                    var manifestInNamed = Path.Combine(namedPath, "AppxManifest.xml");
                    var altManifestInNamed = Path.Combine(namedPath, "appmanifest.xml");
                    
                    if (File.Exists(manifestInNamed) || File.Exists(altManifestInNamed))
                    {
                        return namedPath;
                    }
                }
            }

            // 遍历当前目录查找包含清单文件的目录
            var manifestDirs = new List<string>();
            
            foreach (var dir in Directory.GetDirectories(currentDir))
            {
                var manifestPath = Path.Combine(dir, "AppxManifest.xml");
                var altManifestPath = Path.Combine(dir, "appmanifest.xml");
                
                if (File.Exists(manifestPath) || File.Exists(altManifestPath))
                {
                    manifestDirs.Add(dir);
                }
            }

            if (manifestDirs.Count == 0)
            {
                throw new InvalidOperationException("未找到包含 AppxManifest.xml 或 appmanifest.xml 的目录");
            }

            if (manifestDirs.Count == 1)
            {
                return manifestDirs[0];
            }

            // 多个候选目录，提示用户选择
            Console.WriteLine("找到多个包含清单文件的目录:");
            for (int i = 0; i < manifestDirs.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {Path.GetFileName(manifestDirs[i])}");
            }

            Console.Write("请选择目录 (输入序号): ");
            if (int.TryParse(Console.ReadLine(), out int choice) && choice > 0 && choice <= manifestDirs.Count)
            {
                return manifestDirs[choice - 1];
            }

            throw new InvalidOperationException("无效的选择");
        }

        private void RenameExistingFile(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var timestamp = BackupConstants.GenerateTimestamp();
                var directory = Path.GetDirectoryName(filePath)!;
                var nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                var extension = Path.GetExtension(filePath);
                
                var newName = $"{nameWithoutExt}_{timestamp}{extension}";
                var newPath = Path.Combine(directory, newName);
                
                File.Move(filePath, newPath);
                Console.WriteLine($"已重命名现有文件: {Path.GetFileName(filePath)} -> {newName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"警告: 重命名现有文件失败: {ex.Message}");
                // 继续执行，覆盖现有文件
            }
        }

        private async Task CreateMsixPackageAsync(string sourcePath, string outputPath, bool skipValidation = false)
        {
            var makeAppxPath = WindowsSdkHelper.GetMakeAppxPath();
            var nvFlag = skipValidation ? " /nv" : "";
            var arguments = $"pack /d \"{sourcePath}\" /p \"{outputPath}\"{nvFlag} /o";

            Console.WriteLine($"执行打包命令: {Path.GetFileName(makeAppxPath)} {arguments}");
            
            var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync(makeAppxPath, arguments);

            if (!success)
            {
                throw new InvalidOperationException($"makeappx 打包失败:\n{error}\n{output}");
            }

            Console.WriteLine("✓ MSIX 包创建成功");
            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine($"输出: {output}");
            }
        }

        private async Task<string> DetermineCertificatePathForSignAsync(string? certificatePath, string? certificatePassword, string publisher, string packagePath)
        {
            // 1. 使用命令行指定的证书路径
            if (!string.IsNullOrEmpty(certificatePath))
            {
                if (File.Exists(certificatePath))
                {
                    // 如果用户通过命令行提供了证书和密码，保存到配置中
                    if (!string.IsNullOrEmpty(certificatePassword))
                    {
                        ConfigManager.SaveCertificateConfig(publisher, certificatePath, certificatePassword);
                        Console.WriteLine($"✓ 证书配置已保存");
                    }
                    return certificatePath;
                }
                throw new FileNotFoundException($"指定的证书文件不存在: {certificatePath}");
            }

            // 2. 从配置中查找
            var certConfig = ConfigManager.GetCertificateConfig(publisher);
            if (certConfig != null && File.Exists(certConfig.CertificatePath))
            {
                Console.WriteLine($"使用配置中的证书: {certConfig.CertificatePath}");
                return certConfig.CertificatePath;
            }

            // 3. 交互选择
            Console.WriteLine("\n证书选择:");
            Console.WriteLine("1. 手动输入证书路径");
            Console.WriteLine("2. 自动生成证书");
            Console.Write("请选择 (1 或 2): ");

            var choice = Console.ReadLine();
            
            if (choice == "1")
            {
                Console.Write("请输入证书路径: ");
                var inputPath = Console.ReadLine();
                
                if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
                {
                    throw new FileNotFoundException("输入的证书路径无效");
                }

                Console.Write("请输入证书密码 (直接回车使用默认密码 1234567): ");
                var password = Console.ReadLine();
                if (string.IsNullOrEmpty(password))
                {
                    password = "1234567";
                }

                ConfigManager.SaveCertificateConfig(publisher, inputPath, password);
                Console.WriteLine($"✓ 证书配置已保存");
                return inputPath;
            }
            else if (choice == "2")
            {
                Console.Write("请输入证书密码 (直接回车使用默认密码 1234567): ");
                var password = Console.ReadLine();
                if (string.IsNullOrEmpty(password))
                {
                    password = "1234567";
                }

                var certService = new CertificateService();
                // 创建证书并立即安装（installCertificate=true）
                var generatedCertPath = await certService.CreateCertificateForPublisherAsync(publisher, null, password, installCertificate: true);
                
                // 保存证书配置到 app.config.json
                ConfigManager.SaveCertificateConfig(publisher, generatedCertPath, password);
                Console.WriteLine($"✓ 证书配置已保存到配置文件");
                
                return generatedCertPath;
            }
            else
            {
                throw new InvalidOperationException("无效的选择");
            }
        }
    }
}