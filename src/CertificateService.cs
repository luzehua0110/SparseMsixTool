using SparseMsixTool.Config;
using SparseMsixTool.Utils;

namespace SparseMsixTool.Services
{
    public class CertificateService
    {
        public async Task CreateCertificateAsync(string? subjectName, string? outputDirectory, string? password)
        {
            Console.WriteLine("开始创建证书...");

            string publisher;
            string certPassword = password ?? "1234567";

            // 确保主题名称格式正确
            if (!string.IsNullOrEmpty(subjectName))
            {
                if (!subjectName.StartsWith("CN=") && !subjectName.StartsWith("O=") && !subjectName.StartsWith("OU="))
                {
                    publisher = $"CN={subjectName}";
                }
                else
                {
                    publisher = subjectName;
                }
            }
            else
            {
                throw new ArgumentException("必须指定 --publisher 参数");
            }

            var certPath = await CreateCertificateForPublisherAsync(publisher, outputDirectory, certPassword);
            Console.WriteLine($"✓ 证书创建完成: {certPath}");
            Console.WriteLine($"✓ 证书密码: {certPassword}");

            // 保存到配置
            ConfigManager.SaveCertificateConfig(publisher, certPath, certPassword);
            Console.WriteLine($"✓ 证书配置已保存到配置文件");
        }public async Task<string> CreateCertificateForPublisherAsync(string publisher, string? outputDirectory, string password = "1234567", bool installCertificate = true)
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
                    Console.WriteLine();
                    Console.WriteLine($"⚠ 警告: 证书自动安装失败");
                    if (ex.Message.Contains("拒绝访问") || ex.Message.Contains("Access is denied"))
                    {
                        Console.WriteLine("原因: 需要管理员权限才能安装证书到受信任的根证书颁发机构");
                        Console.WriteLine();
                        Console.WriteLine("请选择以下任一方式手动安装证书:");
                        Console.WriteLine("  方式1（推荐）: 双击证书文件手动安装");
                        Console.WriteLine($"     - 证书路径: {certPath}");
                        Console.WriteLine("     - 密码: " + password);
                        Console.WriteLine("     - 存储位置: 本地计算机 -> 受信任的根证书颁发机构");
                        Console.WriteLine();
                        Console.WriteLine("  方式2: 以管理员权限运行 PowerShell 执行:");
                        Console.WriteLine($"     Import-PfxCertificate -FilePath \"{certPath}\" -CertStoreLocation Cert:\\LocalMachine\\Root -Password (ConvertTo-SecureString \"{password}\" -AsPlainText -Force)");
                    }
                    else
                    {
                        Console.WriteLine($"原因: {ex.Message}");
                        Console.WriteLine($"请手动安装证书: {certPath}");
                        Console.WriteLine($"证书密码: {password}");
                    }
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
$certs = Get-ChildItem -Path Cert:\CurrentUser\My, Cert:\LocalMachine\Root, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
    Where-Object {{ $_.Subject -like '*{pattern}*' -or $_.FriendlyName -like '*{pattern}*' }}

if ($certs) {{
    foreach ($cert in $certs) {{
        Write-Host ""━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━""
        Write-Host ""Subject      : $($cert.Subject)""
        Write-Host ""FriendlyName : $($cert.FriendlyName)""
        Write-Host ""Thumbprint   : $($cert.Thumbprint)""
        Write-Host ""NotAfter     : $($cert.NotAfter)""
        Write-Host ""Store        : $($cert.PSParentPath -replace '.*::','')""
    }}
    Write-Host ""━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━""
}} else {{
    Write-Host ""未找到匹配的系统证书""
}}
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
                throw new ArgumentException("必须指定证书路径或证书主题名称");
            }

            // 先尝试作为完整路径处理
            string? actualCertPath = certPath;
            string? certSubject = null;

            if (!File.Exists(certPath))
            {
                // 如果文件不存在，尝试从配置中查找该名称作为证书主题或 Thumbprint
                var configData = ConfigManager.GetConfig();
                
                // 尝试多种匹配方式：
                // 1. 完全匹配 key (如 "CN=HelloKitty|ABC123...")
                // 2. 完全匹配 Subject
                // 3. 部分匹配（如 "HelloKitty" 匹配 "CN=HelloKitty"）
                // 4. 通过 Thumbprint 匹配（key 中包含或 Value.Thumbprint 匹配）
                var certConfig = configData.Certificates.FirstOrDefault(c => 
                    c.Key.Equals(certPath, StringComparison.OrdinalIgnoreCase) ||
                    c.Value.Subject.Equals(certPath, StringComparison.OrdinalIgnoreCase) ||
                    c.Key.Contains(certPath, StringComparison.OrdinalIgnoreCase) ||
                    c.Value.Subject.Contains(certPath, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(c.Value.Thumbprint) && c.Value.Thumbprint.Equals(certPath, StringComparison.OrdinalIgnoreCase)));

                // 如果还没找到，尝试通过 Thumbprint 计算匹配
                if (certConfig.Value == null && certPath.Length >= 40) // Thumbprint 通常是 40 个十六进制字符
                {
                    certConfig = await FindCertificateByThumbprintAsync(configData, certPath);
                }

                if (certConfig.Value != null)
                {
                    actualCertPath = certConfig.Value.CertificatePath;
                    certSubject = certConfig.Key;
                    Console.WriteLine($"从配置中找到证书: {certSubject} -> {actualCertPath}");
                }
                else if (certPath.Length >= 40)
                {
                    // 如果是 Thumbprint 格式但未在配置中找到，仍然尝试从系统删除
                    Console.WriteLine($"未在配置中找到 Thumbprint: {certPath}");
                    Console.WriteLine("将仅尝试从系统证书存储中删除...");
                    actualCertPath = null; // 标记为只删除系统证书
                }
                else
                {
                    throw new FileNotFoundException($"证书文件不存在，且未在配置中找到名称或指纹: {certPath}");
                }
            }

            // 处理只有 Thumbprint 的情况（仅删除系统证书）
            if (actualCertPath == null)
            {
                if (removeFromSystem && certPath.Length >= 40)
                {
                    try
                    {
                        await RemoveCertificateFromSystemByThumbprintAsync(certPath);
                        Console.WriteLine("✓ 已从系统证书存储中移除");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠ 从系统证书存储移除失败: {ex.Message}");
                    }
                }
                return; // 没有文件路径，只处理系统证书
            }

            Console.WriteLine($"准备删除证书: {actualCertPath}");

            // 先获取密码（如果有），因为后面会从配置中删除
            // 无论是通过名称还是路径找到的，都需要从配置中查找密码
            string? password = null;
            var tempConfig = ConfigManager.GetConfig();
            var certEntry = tempConfig.Certificates.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Value.CertificatePath) &&
                Path.GetFullPath(c.Value.CertificatePath).Equals(Path.GetFullPath(actualCertPath), StringComparison.OrdinalIgnoreCase));
            
            if (certEntry.Value != null)
            {
                password = certEntry.Value.Password;
            }

            // 从配置中查找并移除（根据 Thumbprint 精确删除）
            var config = ConfigManager.GetConfig();
            var certsToRemove = config.Certificates.Where(c =>
                !string.IsNullOrEmpty(c.Value.CertificatePath) &&
                Path.GetFullPath(c.Value.CertificatePath).Equals(Path.GetFullPath(actualCertPath), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (certsToRemove.Count > 0)
            {
                // 如果提供了具体的 Thumbprint，只删除匹配的那个
                if (!string.IsNullOrEmpty(certPath) && certPath.Length >= 40)
                {
                    var thumbprintMatch = certsToRemove.FirstOrDefault(c => 
                        !string.IsNullOrEmpty(c.Value.Thumbprint) && 
                        c.Value.Thumbprint.Equals(certPath, StringComparison.OrdinalIgnoreCase));
                    if (thumbprintMatch.Value != null)
                    {
                        config.Certificates.Remove(thumbprintMatch.Key);
                        ConfigManager.SaveConfig();
                        Console.WriteLine($"✓ 已从配置中移除: {thumbprintMatch.Key}");
                    }
                }
                else
                {
                    // 否则删除所有匹配路径的证书配置
                    foreach (var cert in certsToRemove)
                    {
                        config.Certificates.Remove(cert.Key);
                    }
                    ConfigManager.SaveConfig();
                    Console.WriteLine($"✓ 已从配置中移除 {certsToRemove.Count} 个证书条目");
                }
            }

            // 从系统证书存储中移除
            if (removeFromSystem)
            {
                try
                {
                    // 尝试从文件读取 Thumbprint
                    await RemoveCertificateFromSystemAsync(actualCertPath, password);
                    Console.WriteLine("✓ 已从系统证书存储中移除");
                }
                catch (Exception ex)
                {
                    // 如果从文件读取失败（可能密码错误或文件被覆盖），尝试使用配置中的 Thumbprint
                    Console.WriteLine($"⚠ 使用文件读取失败: {ex.Message}");
                    
                    // 从配置 key 中提取 Thumbprint（格式：Subject|Thumbprint）
                    if (!string.IsNullOrEmpty(certSubject) && certSubject.Contains('|'))
                    {
                        var parts = certSubject.Split('|');
                        if (parts.Length == 2 && parts[1].Length >= 40)
                        {
                            Console.WriteLine($"尝试使用配置中的 Thumbprint: {parts[1]}");
                            try
                            {
                                await RemoveCertificateFromSystemByThumbprintAsync(parts[1]);
                                Console.WriteLine("✓ 已从系统证书存储中移除");
                            }
                            catch (Exception ex2)
                            {
                                Console.WriteLine($"⚠ 从系统证书存储移除失败: {ex2.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"⚠ 从系统证书存储移除失败: {ex.Message}");
                        }
                    }
                    else if (!string.IsNullOrEmpty(certPath) && certPath.Length >= 40)
                    {
                        // 如果 certPath 本身是 Thumbprint
                        Console.WriteLine($"尝试直接使用 Thumbprint: {certPath}");
                        try
                        {
                            await RemoveCertificateFromSystemByThumbprintAsync(certPath);
                            Console.WriteLine("✓ 已从系统证书存储中移除");
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine($"⚠ 从系统证书存储移除失败: {ex2.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠ 从系统证书存储移除失败: {ex.Message}");
                    }
                }
            }

            // 删除证书文件
            try
            {
                File.Delete(actualCertPath);
                Console.WriteLine($"✓ 证书文件已删除: {actualCertPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ 删除证书文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通过 Thumbprint 在配置中查找证书
        /// </summary>
        private async Task<KeyValuePair<string, CertificateConfig>> FindCertificateByThumbprintAsync(AppConfig config, string thumbprint)
        {
            foreach (var cert in config.Certificates)
            {
                if (!File.Exists(cert.Value.CertificatePath))
                    continue;

                try
                {
                    // 计算证书文件的 Thumbprint
                    string getThumbprintCommand;
                    if (!string.IsNullOrEmpty(cert.Value.Password))
                    {
                        getThumbprintCommand = $@"
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2('{cert.Value.CertificatePath.Replace("'", "''")}', '{cert.Value.Password.Replace("'", "''")}')
$cert.Thumbprint
";
                    }
                    else
                    {
                        getThumbprintCommand = $@"
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2('{cert.Value.CertificatePath.Replace("'", "''")}')
$cert.Thumbprint
";
                    }

                    var (success, certThumbprint, _) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-Command \"{getThumbprintCommand}\"");
                    
                    if (success && !string.IsNullOrWhiteSpace(certThumbprint))
                    {
                        certThumbprint = certThumbprint.Trim();
                        if (certThumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase))
                        {
                            return cert;
                        }
                    }
                }
                catch
                {
                    // 忽略无法读取的证书
                    continue;
                }
            }

            return default;
        }

        /// <summary>
        /// 仅通过 Thumbprint 从系统证书存储中移除证书（不需要证书文件）
        /// </summary>
        private async Task RemoveCertificateFromSystemByThumbprintAsync(string thumbprint)
        {
            thumbprint = thumbprint.Trim();
            Console.WriteLine($"证书指纹: {thumbprint}");

            // 从所有可能的存储位置移除
            var removeCommand = $@"
$thumbprint = '{thumbprint}'
$stores = @(
    @{{Path='Cert:\CurrentUser\My'; Name='CurrentUser\My'}},
    @{{Path='Cert:\LocalMachine\Root'; Name='LocalMachine\Root'}},
    @{{Path='Cert:\LocalMachine\My'; Name='LocalMachine\My'}}
)
$removed = 0
$failed = 0
$needsAdmin = @()

foreach ($store in $stores) {{
    $cert = Get-ChildItem -Path $store.Path -ErrorAction SilentlyContinue | Where-Object {{ $_.Thumbprint -eq $thumbprint }}
    if ($cert) {{
        try {{
            Remove-Item -Path ""$($store.Path)\$thumbprint"" -Force -ErrorAction Stop
            # Verify deletion
            $stillExists = Get-ChildItem -Path $store.Path -ErrorAction SilentlyContinue | Where-Object {{ $_.Thumbprint -eq $thumbprint }}      
            if ($stillExists) {{
                Write-Host ""[WARN] $($store.Name): Deletion command executed but certificate still exists (may need admin)"" -ForegroundColor Yellow
                if ($store.Name -like '*LocalMachine*') {{
                    $needsAdmin += $store.Name
                }}
                $failed++
            }} else {{
                Write-Host ""[OK] $($store.Name): Successfully deleted"" -ForegroundColor Green
                $removed++
            }}
        }} catch {{
            Write-Host ""[ERROR] $($store.Name): Deletion failed - $($_.Exception.Message)"" -ForegroundColor Red
            if ($store.Name -like '*LocalMachine*') {{
                $needsAdmin += $store.Name
            }}
            $failed++
        }}
    }}
}}

if ($needsAdmin.Count -gt 0) {{
    Write-Host """"
    Write-Host ""Note: The following store locations require administrator privileges:"" -ForegroundColor Yellow
    foreach ($loc in $needsAdmin) {{
        Write-Host ""  - $loc"" -ForegroundColor Yellow
    }}
    Write-Host ""Please run this command as administrator"" -ForegroundColor Yellow
}}

Write-Output ""Removed from $removed certificate store(s)""
if ($failed -gt 0) {{
    Write-Output ""Warning: $failed store location(s) failed""
}}
";

            // Use Base64 encoding to avoid quote escaping issues
            var scriptBytes = System.Text.Encoding.Unicode.GetBytes(removeCommand);
            var encodedCommand = Convert.ToBase64String(scriptBytes);
            var (success, output, error) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-NoProfile -EncodedCommand {encodedCommand}");            if (success && !string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine(output.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(error))
            {
                throw new InvalidOperationException($"移除证书失败: {error}");
            }
        }

        /// <summary>
        /// 从系统证书存储中移除证书
        /// </summary>
        private async Task RemoveCertificateFromSystemAsync(string certPath, string? password = null)
        {
            // 先获取证书的 Thumbprint
            string getThumbprintCommand;
            if (!string.IsNullOrEmpty(password))
            {
                getThumbprintCommand = $@"
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2('{certPath.Replace("'", "''")}', '{password.Replace("'", "''")}')
$cert.Thumbprint
";
            }
            else
            {
                getThumbprintCommand = $@"
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2('{certPath.Replace("'", "''")}')
$cert.Thumbprint
";
            }

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
$stores = @(
    @{{Path='Cert:\CurrentUser\My'; Name='CurrentUser\My'}},
    @{{Path='Cert:\LocalMachine\Root'; Name='LocalMachine\Root'}},
    @{{Path='Cert:\LocalMachine\My'; Name='LocalMachine\My'}}
)
$removed = 0
$failed = 0
$needsAdmin = @()

foreach ($store in $stores) {{
    $cert = Get-ChildItem -Path $store.Path -ErrorAction SilentlyContinue | Where-Object {{ $_.Thumbprint -eq $thumbprint }}
    if ($cert) {{
        try {{
            Remove-Item -Path ""$($store.Path)\$thumbprint"" -Force -ErrorAction Stop
            # Verify deletion
            $stillExists = Get-ChildItem -Path $store.Path -ErrorAction SilentlyContinue | Where-Object {{ $_.Thumbprint -eq $thumbprint }}      
            if ($stillExists) {{
                Write-Host ""[WARN] $($store.Name): Deletion command executed but certificate still exists (may need admin)"" -ForegroundColor Yellow
                if ($store.Name -like '*LocalMachine*') {{
                    $needsAdmin += $store.Name
                }}
                $failed++
            }} else {{
                Write-Host ""[OK] $($store.Name): Successfully deleted"" -ForegroundColor Green
                $removed++
            }}
        }} catch {{
            Write-Host ""[ERROR] $($store.Name): Deletion failed - $($_.Exception.Message)"" -ForegroundColor Red
            if ($store.Name -like '*LocalMachine*') {{
                $needsAdmin += $store.Name
            }}
            $failed++
        }}
    }}
}}

if ($needsAdmin.Count -gt 0) {{
    Write-Host """"
    Write-Host ""Note: The following store locations require administrator privileges:"" -ForegroundColor Yellow
    foreach ($loc in $needsAdmin) {{
        Write-Host ""  - $loc"" -ForegroundColor Yellow
    }}
    Write-Host ""Please run this command as administrator"" -ForegroundColor Yellow
}}

Write-Host ""Removed from $removed certificate store(s)""
if ($failed -gt 0) {{
    Write-Host ""Warning: $failed store location(s) failed""
}}
";

            // Use Base64 encoding to avoid quote escaping issues
            var scriptBytes2 = System.Text.Encoding.Unicode.GetBytes(removeCommand);
            var encodedCommand2 = Convert.ToBase64String(scriptBytes2);
            var (success2, output2, error2) = await WindowsSdkHelper.ExecuteCommandAsync("powershell.exe", $"-NoProfile -EncodedCommand {encodedCommand2}");            if (!success2)
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