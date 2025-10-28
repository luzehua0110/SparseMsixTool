using MsixTool.Config;
using MsixTool.Utils;

namespace MsixTool.Services
{
    public class CertificateService
    {
        public async Task CreateCertificateAsync(string? subjectName, string? manifestPath, string? password)
        {
            Console.WriteLine("开始创建证书...");

            string publisher;
            string? outputPath = null; // 默认为 null，让证书保存到 data 目录
            string certPassword = password ?? "1234567";

            // 优先使用 --name 参数
            if (!string.IsNullOrEmpty(subjectName))
            {
                // 确保主体名称格式正确
                if (!subjectName.StartsWith("CN=") && !subjectName.StartsWith("O=") && !subjectName.StartsWith("OU="))
                {
                    publisher = $"CN={subjectName}";
                }
                else
                {
                    publisher = subjectName;
                }
                // 使用默认的 data 目录
                outputPath = null;
            }
            // 其次使用 --manifest 参数
            else if (!string.IsNullOrEmpty(manifestPath))
            {
                Console.WriteLine($"从清单文件读取 Publisher: {manifestPath}");
                
                if (!File.Exists(manifestPath))
                {
                    throw new FileNotFoundException($"清单文件不存在: {manifestPath}");
                }

                // 从清单文件解析 Publisher
                publisher = ManifestHelper.GetPublisherFromManifest(manifestPath);
                Console.WriteLine($"✓ 从清单文件解析到 Publisher: {publisher}");
                
                // 使用默认的 data 目录，不再使用清单文件所在目录
                outputPath = null;
            }
            else
            {
                // 不应该到达这里，因为 CommandBuilder 已经做了验证
                throw new ArgumentException("必须指定 --name 或 --manifest 参数");
            }

            var certPath = await CreateCertificateForPublisherAsync(publisher, outputPath, certPassword);
            Console.WriteLine($"✓ 证书创建完成: {certPath}");
            Console.WriteLine($"✓ 证书密码: {certPassword}");

            // 保存到配置
            ConfigManager.SaveCertificateConfig(publisher, certPath, certPassword);
            Console.WriteLine($"✓ 证书配置已保存到配置文件");
        }        public async Task<string> CreateCertificateForPublisherAsync(string publisher, string? outputDirectory, string password = "1234567", bool installCertificate = true)
        {
            // 如果没有指定输出目录，使用程序运行目录下的data目录
            string actualOutputDir;
            if (string.IsNullOrEmpty(outputDirectory))
            {
                var appDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
                actualOutputDir = Path.Combine(appDir, "data");
            }
            else
            {
                actualOutputDir = outputDirectory;
            }
            
            // 确保目录存在
            if (!Directory.Exists(actualOutputDir))
            {
                Directory.CreateDirectory(actualOutputDir);
                Console.WriteLine($"创建证书目录: {actualOutputDir}");
            }
            
            var sanitizedPublisher = SanitizeFileName(publisher);
            var certFileName = $"{sanitizedPublisher}.pfx";
            var certPath = Path.Combine(actualOutputDir, certFileName);

            // 如果证书已存在，自动覆盖
            if (File.Exists(certPath))
            {
                Console.WriteLine($"证书文件已存在，将自动覆盖: {certPath}");
                File.Delete(certPath);
            }

            try
            {
                // 尝试使用 Windows SDK 的 makecert 和 pvk2pfx
                await CreateCertificateWithWindowsSDK(publisher, certPath, password);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"使用 Windows SDK 创建证书失败: {ex.Message}");
                Console.WriteLine("尝试使用 PowerShell 创建自签名证书...");
                
                // 备用方案：使用 PowerShell 创建自签名证书
                await CreateCertificateWithPowerShell(publisher, certPath, password);
            }

            if (!File.Exists(certPath))
            {
                throw new InvalidOperationException("证书创建失败");
            }

            Console.WriteLine($"证书创建成功: {certPath}");
            Console.WriteLine($"证书主体: {publisher}");
            Console.WriteLine($"证书密码: {password}");

            // 根据参数决定是否立即安装证书
            if (installCertificate)
            {
                // 自动安装证书到受信任的根证书颁发机构
                try
                {
                    await InstallCertificateToTrustedRootAsync(certPath, password);
                    Console.WriteLine("✓ 证书已自动安装到受信任的根证书颁发机构");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 证书安装到受信任根失败: {ex.Message}");
                    Console.WriteLine("您可能需要手动运行以下命令安装证书:");
                    Console.WriteLine($"  Import-PfxCertificate -FilePath \"{certPath}\" -CertStoreLocation Cert:\\LocalMachine\\Root -Password (ConvertTo-SecureString \"{password}\" -AsPlainText -Force)");
                }
            }

            return certPath;
        }        private async Task CreateCertificateWithWindowsSDK(string publisher, string certPath, string password)
        {
            var makeCertPath = WindowsSdkHelper.GetMakeCertPath();
            var tempDir = Path.GetTempPath();
            var certName = Path.GetFileNameWithoutExtension(certPath);
            var cerFile = Path.Combine(tempDir, $"{certName}.cer");
            var pvkFile = Path.Combine(tempDir, $"{certName}.pvk");

            try
            {
                // 创建证书和私钥
                var makeCertArgs = $"-r -pe -n \"{publisher}\" -ss my -sr currentuser -a sha256 -cy end -sky signature -sv \"{pvkFile}\" \"{cerFile}\"";
                
                Console.WriteLine("正在创建证书和私钥...");
                var (success1, output1, error1) = await WindowsSdkHelper.ExecuteCommandAsync(makeCertPath, makeCertArgs);
                
                if (!success1)
                {
                    throw new InvalidOperationException($"makecert 失败: {error1}");
                }

                // 查找 pvk2pfx 工具
                var pvk2pfxPath = WindowsSdkHelper.GetPvk2PfxPath();
                
                // 转换为 PFX 格式
                var pvk2pfxArgs = $"-pvk \"{pvkFile}\" -spc \"{cerFile}\" -pfx \"{certPath}\" -po {password}";
                
                Console.WriteLine("正在转换为 PFX 格式...");
                var (success2, output2, error2) = await WindowsSdkHelper.ExecuteCommandAsync(pvk2pfxPath, pvk2pfxArgs);
                
                if (!success2)
                {
                    throw new InvalidOperationException($"pvk2pfx 失败: {error2}");
                }
            }
            finally
            {
                // 清理临时文件
                try { if (File.Exists(cerFile)) File.Delete(cerFile); } catch { }
                try { if (File.Exists(pvkFile)) File.Delete(pvkFile); } catch { }
            }
        }

        private async Task CreateCertificateWithPowerShell(string publisher, string certPath, string password)
        {
            // 使用单引号避免转义问题
            var escapedCertPath = certPath.Replace("'", "''");
            var escapedPassword = password.Replace("'", "''");
            var escapedPublisher = publisher.Replace("'", "''");
            
            var command = $@"
$cert = New-SelfSignedCertificate -Subject '{escapedPublisher}' -Type CodeSigningCert -CertStoreLocation Cert:\CurrentUser\My
$pwd = ConvertTo-SecureString -String '{escapedPassword}' -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath '{escapedCertPath}' -Password $pwd
Remove-Item -Path ""Cert:\CurrentUser\My\$($cert.Thumbprint)"" -Force
";

            var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{command}\"");

            if (!success)
            {
                throw new InvalidOperationException($"PowerShell 证书创建失败: {error}");
            }
        }

        public async Task InstallCertificateToTrustedRootAsync(string certPath, string password)
        {
            // 使用 PowerShell 安装证书到受信任的根证书颁发机构
            var escapedCertPath = certPath.Replace("'", "''");
            var escapedPassword = password.Replace("'", "''");

            var command = $@"
$pwd = ConvertTo-SecureString -String '{escapedPassword}' -Force -AsPlainText
$cert = Import-PfxCertificate -FilePath '{escapedCertPath}' -CertStoreLocation Cert:\LocalMachine\Root -Password $pwd
if ($cert) {{ 
    Write-Host 'Certificate installed successfully' 
}} else {{ 
    throw 'Failed to install certificate' 
}}
";

            var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{command}\"");

            if (!success)
            {
                throw new InvalidOperationException($"证书安装失败: {error}");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = fileName;

            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }

            // 移除 CN= 前缀等常见的证书主体前缀
            sanitized = sanitized.Replace("CN=", "").Replace("O=", "").Replace("OU=", "");

            return sanitized;
        }

        /// <summary>
        /// 智能获取或创建证书：优先使用本地证书，其次尝试下载，最后自动创建
        /// </summary>
        public async Task<string> GetOrCreateCertificateAsync(string publisher, string? outputDirectory = null, string? password = null)
        {
            string certPassword = password ?? "1234567";

            Console.WriteLine($"正在为 Publisher 查找或创建证书: {publisher}");

            // 如果没有指定输出目录，使用程序运行目录下的data目录
            string actualOutputDir;
            if (string.IsNullOrEmpty(outputDirectory))
            {
                var appDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
                actualOutputDir = Path.Combine(appDir, "data");
            }
            else
            {
                actualOutputDir = outputDirectory;
            }
            
            // 确保目录存在
            if (!Directory.Exists(actualOutputDir))
            {
                Directory.CreateDirectory(actualOutputDir);
            }

            // 1. 先查找本地配置中是否有匹配的证书
            var localCert = FindLocalCertificate(publisher, actualOutputDir);
            if (localCert != null)
            {
                Console.WriteLine($"✓ 找到本地证书: {localCert}");
                return localCert;
            }

            // 2. 尝试从公开网站下载证书
            Console.WriteLine("本地未找到证书，尝试从公开网站下载...");
            var downloadedCert = await TryDownloadCertificateAsync(publisher, actualOutputDir);
            if (downloadedCert != null)
            {
                Console.WriteLine($"✓ 成功下载证书: {downloadedCert}");
                // 安装下载的证书到受信任根
                try
                {
                    await InstallCertificateToTrustedRootAsync(downloadedCert, certPassword);
                    Console.WriteLine("✓ 证书已安装到受信任的根证书颁发机构");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 证书安装到受信任根失败: {ex.Message}");
                }
                return downloadedCert;
            }

            // 3. 自动创建本地证书
            Console.WriteLine("无法下载证书，自动创建本地自签名证书...");
            var createdCert = await CreateCertificateForPublisherAsync(publisher, actualOutputDir, certPassword);
            Console.WriteLine($"✓ 已自动创建并安装证书: {createdCert}");
            
            // 保存到配置
            ConfigManager.SaveCertificateConfig(publisher, createdCert, certPassword);
            Console.WriteLine($"✓ 证书配置已保存到配置文件");
            
            return createdCert;
        }

        /// <summary>
        /// 在本地配置或常见位置查找证书
        /// </summary>
        /// <summary>
        /// 在本地配置或常见位置查找证书
        /// </summary>
        private string? FindLocalCertificate(string publisher, string? outputDirectory = null)
        {
            // 从配置文件查找
            var certConfig = ConfigManager.GetCertificateConfig(publisher);
            if (certConfig != null && !string.IsNullOrEmpty(certConfig.CertificatePath) && File.Exists(certConfig.CertificatePath))
            {
                return certConfig.CertificatePath;
            }

            var sanitizedName = SanitizeFileName(publisher);
            var searchDirectories = new List<string>();

            // 添加输出目录（清单所在目录）
            if (!string.IsNullOrEmpty(outputDirectory) && Directory.Exists(outputDirectory))
            {
                searchDirectories.Add(outputDirectory);
            }

            // 添加当前工作目录
            searchDirectories.Add(Environment.CurrentDirectory);

            // 在这些目录中查找证书
            foreach (var dir in searchDirectories)
            {
                var possiblePaths = new[]
                {
                    Path.Combine(dir, $"{sanitizedName}.pfx"),
                    Path.Combine(dir, $"{publisher}.pfx"),
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 尝试从公开网站下载证书
        /// </summary>
        private async Task<string?> TryDownloadCertificateAsync(string publisher, string outputDirectory)
        {
            try
            {
                // 这里可以实现从公开证书库下载的逻辑
                // 目前 Windows 应用签名证书通常需要从以下途径获取：
                // 1. Microsoft Store 认证 (https://partner.microsoft.com)
                // 2. 商业 CA: DigiCert (https://www.digicert.com/), Sectigo (https://sectigo.com/)
                // 3. GlobalSign (https://www.globalsign.com/)
                
                // 注意：真实的代码签名证书需要企业身份验证，不能直接下载
                // 这里仅作为示例框架，实际部署时应该移除或指向内部证书服务
                
                var sanitizedName = SanitizeFileName(publisher);
                var possibleUrls = new string[]
                {
                    // 示例：企业内部证书服务器
                    // $"https://your-company-cert-server.com/api/certificates/{sanitizedName}.pfx",
                    
                    // 可以添加您自己的证书分发服务URL
                };

                if (possibleUrls.Length == 0)
                {
                    // 没有配置证书下载源，直接返回
                    return null;
                }

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    
                    foreach (var url in possibleUrls)
                    {
                        try
                        {
                            Console.WriteLine($"  尝试下载: {url}");
                            var response = await httpClient.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                var certData = await response.Content.ReadAsByteArrayAsync();
                                var certPath = Path.Combine(outputDirectory, $"{sanitizedName}.pfx");
                                await File.WriteAllBytesAsync(certPath, certData);
                                return certPath;
                            }
                        }
                        catch
                        {
                            // 忽略单个URL的下载失败，继续尝试下一个
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  下载证书失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 查找证书（支持模糊匹配 publisher 名称）
        /// </summary>
        public async Task FindCertificatesAsync(string? namePattern)
        {
            Console.WriteLine("查找证书...");

            if (string.IsNullOrEmpty(namePattern))
            {
                // 列出所有配置的证书
                var config = ConfigManager.GetConfig();
                if (config.Certificates.Count == 0)
                {
                    Console.WriteLine("配置中没有保存的证书");
                    return;
                }

                Console.WriteLine($"找到 {config.Certificates.Count} 个配置的证书:\n");
                foreach (var cert in config.Certificates)
                {
                    var exists = File.Exists(cert.Value.CertificatePath) ? "✓" : "✗";
                    Console.WriteLine($"{exists} {cert.Key}");
                    Console.WriteLine($"  路径: {cert.Value.CertificatePath}");
                    Console.WriteLine($"  密码: {cert.Value.Password}");
                    Console.WriteLine();
                }
                return;
            }

            // 模糊匹配查找
            var pattern = namePattern.ToLowerInvariant();
            var config2 = ConfigManager.GetConfig();
            var matches = config2.Certificates
                .Where(c => c.Key.ToLowerInvariant().Contains(pattern) || 
                           c.Value.Subject.ToLowerInvariant().Contains(pattern))
                .ToList();

            if (matches.Count == 0)
            {
                Console.WriteLine($"未找到匹配 '{namePattern}' 的证书");
                
                // 也搜索系统证书存储
                Console.WriteLine("\n正在搜索系统证书存储...");
                await SearchSystemCertificatesAsync(pattern);
                return;
            }

            Console.WriteLine($"找到 {matches.Count} 个匹配的证书:\n");
            foreach (var cert in matches)
            {
                var exists = File.Exists(cert.Value.CertificatePath) ? "✓" : "✗";
                Console.WriteLine($"{exists} {cert.Key}");
                Console.WriteLine($"  路径: {cert.Value.CertificatePath}");
                Console.WriteLine($"  密码: {cert.Value.Password}");
                Console.WriteLine();
            }

            // 同时搜索系统证书存储
            Console.WriteLine("正在搜索系统证书存储...");
            await SearchSystemCertificatesAsync(pattern);
        }

        /// <summary>
        /// 搜索系统证书存储
        /// </summary>
        private async Task SearchSystemCertificatesAsync(string pattern)
        {
            var command = $@"
Get-ChildItem -Path Cert:\CurrentUser\My, Cert:\LocalMachine\Root, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
    Where-Object {{ $_.Subject -like '*{pattern}*' -or $_.FriendlyName -like '*{pattern}*' }} |
    Select-Object Subject, FriendlyName, Thumbprint, NotAfter |
    Format-Table -AutoSize
";

            var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{command}\"");

            if (success && !string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine("系统证书存储中的匹配项:");
                Console.WriteLine(output);
            }
            else
            {
                Console.WriteLine("系统证书存储中未找到匹配项");
            }
        }

        /// <summary>
        /// 删除证书
        /// </summary>
        public async Task DeleteCertificateAsync(string? certPath, bool removeFromSystem)
        {
            if (string.IsNullOrEmpty(certPath))
            {
                throw new ArgumentException("必须指定证书路径 (--path)");
            }

            if (!File.Exists(certPath))
            {
                throw new FileNotFoundException($"证书文件不存在: {certPath}");
            }

            Console.WriteLine($"准备删除证书: {certPath}");

            // 从配置中查找并移除
            var config = ConfigManager.GetConfig();
            var certToRemove = config.Certificates.FirstOrDefault(c => 
                Path.GetFullPath(c.Value.CertificatePath).Equals(Path.GetFullPath(certPath), StringComparison.OrdinalIgnoreCase));

            if (certToRemove.Value != null)
            {
                config.Certificates.Remove(certToRemove.Key);
                ConfigManager.SaveConfig();
                Console.WriteLine($"✓ 已从配置中移除: {certToRemove.Key}");
            }

            // 从系统证书存储中移除
            if (removeFromSystem)
            {
                try
                {
                    await RemoveCertificateFromSystemAsync(certPath);
                    Console.WriteLine("✓ 已从系统证书存储中移除");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ 从系统证书存储移除失败: {ex.Message}");
                }
            }

            // 删除证书文件
            try
            {
                File.Delete(certPath);
                Console.WriteLine($"✓ 证书文件已删除: {certPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ 删除证书文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从系统证书存储中移除证书
        /// </summary>
        private async Task RemoveCertificateFromSystemAsync(string certPath)
        {
            // 先获取证书的 Thumbprint
            var getThumbprintCommand = $@"
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2('{certPath.Replace("'", "''")}')
$cert.Thumbprint
";

            var (success1, thumbprint, error1) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{getThumbprintCommand}\"");

            if (!success1 || string.IsNullOrWhiteSpace(thumbprint))
            {
                throw new InvalidOperationException($"无法读取证书指纹: {error1}");
            }

            thumbprint = thumbprint.Trim();
            Console.WriteLine($"证书指纹: {thumbprint}");

            // 从所有可能的存储位置移除
            var removeCommand = $@"
$thumbprint = '{thumbprint}'
$stores = @('Cert:\CurrentUser\My', 'Cert:\LocalMachine\Root', 'Cert:\LocalMachine\My')
$removed = 0
foreach ($store in $stores) {{
    $cert = Get-ChildItem -Path $store -ErrorAction SilentlyContinue | Where-Object {{ $_.Thumbprint -eq $thumbprint }}
    if ($cert) {{
        Remove-Item -Path ""$store\$thumbprint"" -Force -ErrorAction SilentlyContinue
        $removed++
    }}
}}
Write-Host ""已从 $removed 个证书存储中移除""
";

            var (success2, output2, error2) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{removeCommand}\"");

            if (!success2)
            {
                throw new InvalidOperationException($"移除证书失败: {error2}");
            }

            if (!string.IsNullOrWhiteSpace(output2))
            {
                Console.WriteLine(output2);
            }
        }
    }
}