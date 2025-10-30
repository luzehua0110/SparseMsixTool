using System.Text.Json;
using System.Text.Json.Serialization;

namespace MsixTool.Config
{
    // JSON 源生成器上下文 - 用于 Native AOT 兼容
    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(AppConfig))]
    [JsonSerializable(typeof(CertificateConfig))]
    [JsonSerializable(typeof(Dictionary<string, CertificateConfig>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }

    public class CertificateConfig
    {
        [JsonPropertyName("Subject")]
        public string Subject { get; set; } = string.Empty;
        
        [JsonPropertyName("CertificatePath")]
        public string CertificatePath { get; set; } = string.Empty;
        
        [JsonPropertyName("Password")]
        public string Password { get; set; } = "1234567";
    }

    public class AppConfig
    {
        [JsonPropertyName("Certificates")]
        public Dictionary<string, CertificateConfig> Certificates { get; set; } = new();
        
        [JsonPropertyName("DefaultCertificatePath")]
        public string DefaultCertificatePath { get; set; } = string.Empty;

        [JsonPropertyName("SparsePackagePaths")]
        public Dictionary<string, string> SparsePackagePaths { get; set; } = new();

        [JsonPropertyName("LastExternalAppPath")]
        public string LastExternalAppPath { get; set; } = string.Empty;
    }

    public static class ConfigManager
    {
        private static readonly string ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "app.config.json");
    // 使用非可空字段，初始化时保证赋值，避免序列化时的可空类型不匹配警告
    private static AppConfig _config = new AppConfig();

        public static AppConfig GetConfig()
        {
            if (_config.Certificates.Count == 0 && _config.SparsePackagePaths.Count == 0 && string.IsNullOrEmpty(_config.DefaultCertificatePath))
            {
                // 如果尚未真正加载（字段是默认初始化的空配置），尝试加载
                LoadConfig();
            }
            return _config;
        }

        private static void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var loaded = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppConfig);
                    _config = loaded ?? new AppConfig();
                    
                    // 验证配置中的证书路径
                    ValidateAndCleanupConfig();
                }
                else
                {
                    _config = new AppConfig();
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置文件失败: {ex.Message}");
                _config = new AppConfig();
            }
        }

        private static void ValidateAndCleanupConfig()
        {
            // _config 已保证非空

            var invalidCerts = new List<string>();

            // 检查所有证书路径是否有效
            foreach (var cert in _config.Certificates)
            {
                if (!File.Exists(cert.Value.CertificatePath))
                {
                    Console.WriteLine($"警告: 配置中的证书文件不存在: {cert.Value.CertificatePath}");
                    invalidCerts.Add(cert.Key);
                }
            }

            // 移除无效的证书配置
            foreach (var key in invalidCerts)
            {
                _config.Certificates.Remove(key);
                Console.WriteLine($"已从配置中移除无效证书: {key}");
            }

            // 如果有无效配置被移除，保存更新后的配置
            if (invalidCerts.Count > 0)
            {
                SaveConfig();
            }
        }

        public static void SaveConfig()
        {
            try
            {
                var configDir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir!);
                }

                var json = JsonSerializer.Serialize<AppConfig>(_config, AppJsonContext.Default.AppConfig);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置文件失败: {ex.Message}");
            }
        }

        public static void SaveCertificateConfig(string subject, string certificatePath, string password = "1234567")
        {
            var config = GetConfig();
            config.Certificates[subject] = new CertificateConfig
            {
                Subject = subject,
                CertificatePath = certificatePath,
                Password = password
            };
            SaveConfig();
            Console.WriteLine($"证书配置已保存: {subject} -> {certificatePath}");
        }

        public static CertificateConfig? GetCertificateConfig(string subject)
        {
            var config = GetConfig();
            return config.Certificates.TryGetValue(subject, out var cert) ? cert : null;
        }

        /// <summary>
        /// 保存稀疏包的外部程序路径配置
        /// </summary>
        public static void SaveSparsePackagePath(string publisher, string packageName, string externalPath)
        {
            var config = GetConfig();
            var key = $"{publisher}|{packageName}";
            config.SparsePackagePaths[key] = externalPath;
            SaveConfig();
            Console.WriteLine($"稀疏包路径已保存: {packageName} -> {externalPath}");
        }

        /// <summary>
        /// 获取稀疏包的外部程序路径配置
        /// </summary>
        public static string? GetSparsePackagePath(string publisher, string packageName)
        {
            var config = GetConfig();
            var key = $"{publisher}|{packageName}";
            return config.SparsePackagePaths.TryGetValue(key, out var path) ? path : null;
        }

        public static void SaveLastExternalAppPath(string path)
        {
            var config = GetConfig();
            config.LastExternalAppPath = path;
            SaveConfig();
        }

        public static string? GetLastExternalAppPath()
        {
            var config = GetConfig();
            return string.IsNullOrWhiteSpace(config.LastExternalAppPath) ? null : config.LastExternalAppPath;
        }
    }
}