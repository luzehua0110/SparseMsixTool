using System.Xml.Linq;

namespace SparseMsixTool.Utils
{
    public static class ManifestHelper
    {
        /// <summary>
        /// 从 MSIX 包 (zip 格式) 中直接读取清单并获取包信息，不进行完整解包。
        /// 适用于稀疏包（manifest 中引用的文件可能不在包内，使用 makeappx unpack 会失败）。
        /// </summary>
        /// <param name="msixPath">MSIX 包文件路径</param>
        /// <returns>(PackageName, Publisher, Version)</returns>
        public static (string PackageName, string Publisher, string Version) GetPackageInfoFromMsix(string msixPath)
        {
            if (string.IsNullOrEmpty(msixPath)) throw new ArgumentNullException(nameof(msixPath));
            if (!File.Exists(msixPath)) throw new FileNotFoundException($"MSIX 文件不存在: {msixPath}");

            // MSIX 实际是一个 ZIP 包，包含 AppxManifest.xml
            try
            {
                using var fs = File.OpenRead(msixPath);
                using var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: false);

                // 支持两种常见命名
                var entry = archive.GetEntry("AppxManifest.xml") ?? archive.GetEntry("appmanifest.xml");
                if (entry == null)
                {
                    throw new InvalidOperationException("MSIX 包中未找到 AppxManifest.xml 或 appmanifest.xml");
                }

                using var manifestStream = entry.Open();
                var doc = XDocument.Load(manifestStream);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                var identity = doc.Root?.Element(ns + "Identity");
                if (identity == null)
                {
                    throw new InvalidOperationException("清单文件中未找到 Identity 元素");
                }

                var name = identity.Attribute("Name")?.Value ?? string.Empty;
                var publisher = identity.Attribute("Publisher")?.Value ?? string.Empty;
                var version = identity.Attribute("Version")?.Value ?? string.Empty;

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(publisher) || string.IsNullOrEmpty(version))
                {
                    throw new InvalidOperationException("清单文件中存在空的 Name/Publisher/Version 属性");
                }

                return (name, publisher, version);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"直接从 MSIX 读取清单失败: {ex.Message}", ex);
            }
        }
        public static string FindManifestPath(string sourcePath)
        {
            var manifestPath = Path.Combine(sourcePath, "AppxManifest.xml");
            if (File.Exists(manifestPath))
            {
                return manifestPath;
            }

            manifestPath = Path.Combine(sourcePath, "appmanifest.xml");
            if (File.Exists(manifestPath))
            {
                return manifestPath;
            }

            throw new FileNotFoundException($"在路径 {sourcePath} 中未找到 AppxManifest.xml 或 appmanifest.xml 文件");
        }

        public static (string PackageName, string Publisher, string Version) GetPackageInfo(string manifestPath)
        {
            try
            {
                var doc = XDocument.Load(manifestPath);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                var identity = doc.Root?.Element(ns + "Identity");
                if (identity == null)
                {
                    throw new InvalidOperationException("清单文件中未找到 Identity 元素");
                }

                var name = identity.Attribute("Name")?.Value ?? "";
                var publisher = identity.Attribute("Publisher")?.Value ?? "";
                var version = identity.Attribute("Version")?.Value ?? "";

                if (string.IsNullOrEmpty(name))
                {
                    throw new InvalidOperationException("清单文件中 Name 属性为空");
                }

                return (name, publisher, version);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析清单文件失败: {ex.Message}", ex);
            }
        }

        public static string GetPublisherSubject(string manifestPath)
        {
            var (_, publisher, _) = GetPackageInfo(manifestPath);
            return publisher;
        }

        public static string GetPublisherFromManifest(string manifestPath)
        {
            // 别名方法，方便调用
            return GetPublisherSubject(manifestPath);
        }

        public static void ValidateManifest(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"清单文件不存在: {manifestPath}");
            }

            try
            {
                var doc = XDocument.Load(manifestPath);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

                var identity = doc.Root?.Element(ns + "Identity");
                if (identity == null)
                {
                    throw new InvalidOperationException("清单文件格式错误：缺少 Identity 元素");
                }

                var name = identity.Attribute("Name")?.Value;
                var publisher = identity.Attribute("Publisher")?.Value;
                var version = identity.Attribute("Version")?.Value;

                if (string.IsNullOrEmpty(name))
                    throw new InvalidOperationException("清单文件格式错误：Name 属性不能为空");
                
                if (string.IsNullOrEmpty(publisher))
                    throw new InvalidOperationException("清单文件格式错误：Publisher 属性不能为空");
                
                if (string.IsNullOrEmpty(version))
                    throw new InvalidOperationException("清单文件格式错误：Version 属性不能为空");

                // 验证版本号格式
                if (!IsValidVersion(version))
                    throw new InvalidOperationException($"清单文件格式错误：Version 格式无效 '{version}'，必须是四位数字格式 (例如: 1.0.0.0)");

                // 验证 Publisher 格式
                if (!publisher.StartsWith("CN=") && !publisher.StartsWith("O=") && !publisher.StartsWith("OU="))
                    throw new InvalidOperationException($"清单文件格式错误：Publisher 格式无效 '{publisher}'，应该以 CN=, O= 或 OU= 开头");

                Console.WriteLine($"清单文件验证成功:");
                Console.WriteLine($"  包名: {name}");
                Console.WriteLine($"  发布者: {publisher}");
                Console.WriteLine($"  版本: {version}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"清单文件验证失败: {ex.Message}", ex);
            }
        }

        private static bool IsValidVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return false;
            
            var parts = version.Split('.');
            if (parts.Length != 4) return false;

            foreach (var part in parts)
            {
                if (!ushort.TryParse(part, out _))
                    return false;
            }

            return true;
        }
    }
}