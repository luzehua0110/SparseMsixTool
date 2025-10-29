using MsixTool.Utils;
using Xunit;
using System.IO;

namespace MsixTool.Tests
{
    public class ManifestHelperTests
    {
        private const string SampleManifest = "Sample_AppxManifest.xml";

        [Fact]
        public void FindManifestPath_FindsUppercase()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ManifestTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var manifestPath = Path.Combine(dir, "AppxManifest.xml");
            File.WriteAllText(manifestPath, "<Package xmlns='http://schemas.microsoft.com/appx/manifest/foundation/windows10'><Identity Name='TestPkg' Publisher='CN=Test' Version='1.0.0.0'/></Package>");
            var found = ManifestHelper.FindManifestPath(dir);
            Assert.Equal(manifestPath, found);
            Directory.Delete(dir, true);
        }

        [Fact]
        public void GetPackageInfo_ParsesIdentity()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ManifestTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var manifestPath = Path.Combine(dir, "AppxManifest.xml");
            File.WriteAllText(manifestPath, "<Package xmlns='http://schemas.microsoft.com/appx/manifest/foundation/windows10'><Identity Name='MyApp' Publisher='CN=Publisher' Version='2.3.4.5'/></Package>");
            var (name, publisher, version) = ManifestHelper.GetPackageInfo(manifestPath);
            Assert.Equal("MyApp", name);
            Assert.Equal("CN=Publisher", publisher);
            Assert.Equal("2.3.4.5", version);
            Directory.Delete(dir, true);
        }
    }
}
