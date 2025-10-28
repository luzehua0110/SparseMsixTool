using System.IO.Compression;

namespace MsixTool.Utils
{
    /// <summary>
    /// 从稀疏包中拷贝资源（图标、图片等）到外部程序路径
    /// </summary>
    public static class ResourceCopyHelper
    {
        private static readonly string[] AssetExtensions = new[] 
        { 
            ".png", ".jpg", ".jpeg", ".ico", ".bmp", ".gif", ".svg",
            ".scale-100.png", ".scale-125.png", ".scale-150.png", ".scale-200.png", ".scale-400.png"
        };

        /// <summary>
        /// 从 MSIX 包中拷贝资源文件到外部程序目录
        /// </summary>
        /// <param name="msixPath">MSIX 包路径</param>
        /// <param name="externalPath">外部程序目录</param>
        public static void CopyResourcesFromMsix(string msixPath, string externalPath)
        {
            if (!File.Exists(msixPath))
            {
                throw new FileNotFoundException($"MSIX 文件不存在: {msixPath}");
            }

            if (!Directory.Exists(externalPath))
            {
                throw new DirectoryNotFoundException($"外部程序目录不存在: {externalPath}");
            }

            Console.WriteLine("从稀疏包中拷贝资源文件...");

            try
            {
                using var fs = File.OpenRead(msixPath);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

                var copiedCount = 0;
                var assetsFolder = Path.Combine(externalPath, "Assets");

                foreach (var entry in archive.Entries)
                {
                    // 检查是否是资源文件
                    if (IsAssetFile(entry.FullName))
                    {
                        // 创建目标路径，保持目录结构
                        var relativePath = entry.FullName;
                        var targetPath = Path.Combine(externalPath, relativePath);
                        var targetDir = Path.GetDirectoryName(targetPath);

                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        // 解压到目标路径
                        entry.ExtractToFile(targetPath, overwrite: true);
                        copiedCount++;
                        
                        if (copiedCount <= 5) // 只显示前5个文件
                        {
                            Console.WriteLine($"  ✓ {relativePath}");
                        }
                    }
                }

                if (copiedCount > 5)
                {
                    Console.WriteLine($"  ... 及其他 {copiedCount - 5} 个文件");
                }

                Console.WriteLine($"✓ 共拷贝 {copiedCount} 个资源文件");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"拷贝资源文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 判断是否是资源文件
        /// </summary>
        private static bool IsAssetFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            // 检查扩展名
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (AssetExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // 检查是否在 Assets 文件夹中
            if (fileName.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
