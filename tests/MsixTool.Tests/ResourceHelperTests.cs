using MsixTool.Utils;
using Xunit;
using System.IO;
using System.Linq;

namespace MsixTool.Tests
{
    public class ResourceHelperTests
    {
        [Fact]
        public void GetReferencedResources_IncludesAssets()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ResTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var assetsDir = Path.Combine(dir, "Assets");
            Directory.CreateDirectory(assetsDir);
            File.WriteAllText(Path.Combine(assetsDir, "logo.png"), "fake");
            File.WriteAllText(Path.Combine(assetsDir, "icon.ico"), "fake");
            var manifestPath = Path.Combine(dir, "AppxManifest.xml");
            File.WriteAllText(manifestPath, "<Package xmlns='http://schemas.microsoft.com/appx/manifest/foundation/windows10'><Identity Name='MyApp' Publisher='CN=Publisher' Version='1.0.0.0'/><Properties><Logo>Assets/logo.png</Logo></Properties><Applications><Application><VisualElements xmlns='http://schemas.microsoft.com/appx/manifest/uap/windows10' Square150x150Logo='Assets/logo.png' Square44x44Logo='Assets/icon.ico'/></Application></Applications></Package>");
            var resources = ResourceHelper.GetReferencedResources(manifestPath);
            Assert.Contains("Assets/logo.png", resources);
            Assert.Contains("Assets/icon.ico", resources);
            // Distinct
            Assert.Equal(resources.Count, resources.Distinct().Count());
            Directory.Delete(dir, true);
        }
    }
}
