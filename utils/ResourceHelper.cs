using System.Xml.Linq;

namespace SparseMsixTool.Utils
{
    public static class ResourceHelper
    {
        /// <summary>
        /// 从清单文件中解析引用的资源文件（图标等）
        /// </summary>
        public static List<string> GetReferencedResources(string manifestPath)
        {
            var resources = new List<string>();
            
            try
            {
                var doc = XDocument.Load(manifestPath);
                var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
                var uapNs = XNamespace.Get("http://schemas.microsoft.com/appx/manifest/uap/windows10");

                // 查找 Logo
                var properties = doc.Root?.Element(ns + "Properties");
                if (properties != null)
                {
                    var logo = properties.Element(ns + "Logo")?.Value;
                    if (!string.IsNullOrEmpty(logo))
                    {
                        resources.Add(logo);
                    }
                }

                // 查找 VisualElements 中的图标和启动屏幕
                var applications = doc.Root?.Element(ns + "Applications");
                if (applications != null)
                {
                    foreach (var app in applications.Elements(ns + "Application"))
                    {
                        var visualElements = app.Element(uapNs + "VisualElements");
                        if (visualElements != null)
                        {
                            var square150Logo = visualElements.Attribute("Square150x150Logo")?.Value;
                            var square44Logo = visualElements.Attribute("Square44x44Logo")?.Value;
                            var square71Logo = visualElements.Attribute("Square71x71Logo")?.Value;
                            var square310Logo = visualElements.Attribute("Square310x310Logo")?.Value;
                            var wideLogo = visualElements.Attribute("Wide310x150Logo")?.Value;

                            if (!string.IsNullOrEmpty(square150Logo)) resources.Add(square150Logo);
                            if (!string.IsNullOrEmpty(square44Logo)) resources.Add(square44Logo);
                            if (!string.IsNullOrEmpty(square71Logo)) resources.Add(square71Logo);
                            if (!string.IsNullOrEmpty(square310Logo)) resources.Add(square310Logo);
                            if (!string.IsNullOrEmpty(wideLogo)) resources.Add(wideLogo);

                            // 查找 SplashScreen
                            var splashScreen = visualElements.Element(uapNs + "SplashScreen");
                            if (splashScreen != null)
                            {
                                var splashImage = splashScreen.Attribute("Image")?.Value;
                                if (!string.IsNullOrEmpty(splashImage))
                                {
                                    resources.Add(splashImage);
                                }
                            }
                        }
                    }
                }

                // 将所有资源去重
                resources = resources.Distinct().ToList();

                // 自动包含 Assets 目录下的所有文件
                var manifestDir = Path.GetDirectoryName(manifestPath);
                if (!string.IsNullOrEmpty(manifestDir))
                {
                    var assetsDir = Path.Combine(manifestDir, "Assets");
                    if (Directory.Exists(assetsDir))
                    {
                        var assetFiles = Directory.GetFiles(assetsDir, "*.*", SearchOption.AllDirectories);
                        foreach (var file in assetFiles)
                        {
                            var relativePath = Path.GetRelativePath(manifestDir, file);
                            if (!resources.Contains(relativePath))
                            {
                                resources.Add(relativePath);
                            }
                        }
                    }

                    // Include resources.pri if it exists in the input directory
                    var resourcesPriPath = Path.Combine(manifestDir, "resources.pri");
                    if (File.Exists(resourcesPriPath))
                    {
                        if (!resources.Contains("resources.pri"))
                        {
                            resources.Add("resources.pri");
                            Console.WriteLine("✓ 检测到 resources.pri 文件，将其包含到稀疏包中");
                        }
                    }
                }

                return resources;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"警告: 解析清单资源文件时出错: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// 将资源文件复制到目标目录
        /// </summary>
        public static void CopyResourcesToDestination(string manifestDir, string destDir, List<string> resources, bool showMessage = true)
        {
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            int copiedCount = 0;
            foreach (var resource in resources)
            {
                try
                {
                    var sourcePath = Path.Combine(manifestDir, resource);
                    var destPath = Path.Combine(destDir, resource);

                    if (File.Exists(sourcePath))
                    {
                        var destFileDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destFileDir) && !Directory.Exists(destFileDir))
                        {
                            Directory.CreateDirectory(destFileDir);
                        }

                        File.Copy(sourcePath, destPath, overwrite: true);
                        copiedCount++;
                    }
                    else
                    {
                        Console.WriteLine($"警告: 资源文件不存在: {resource}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"警告: 复制资源文件失败 {resource}: {ex.Message}");
                }
            }

            if (showMessage && copiedCount > 0)
            {
                Console.WriteLine($"✓ 已复制 {copiedCount} 个资源文件");
            }
        }
    }
}
