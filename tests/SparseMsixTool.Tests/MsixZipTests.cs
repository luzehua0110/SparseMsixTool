using System.IO.Compression;
using SparseMsixTool.Utils;
using Xunit;

namespace SparseMsixTool.Tests;

public class MsixZipTests
{
    [Fact]
    public void GetPackageInfoFromMsix_ValidZip_ReturnsInfo()
    {
        var msixPath = CreateMsixWithManifest("TestZipApp", "CN=ZipPublisher", "2.3.4.5");
        try
        {
            var (name, publisher, version) = ManifestHelper.GetPackageInfoFromMsix(msixPath);
            Assert.Equal("TestZipApp", name);
            Assert.Equal("CN=ZipPublisher", publisher);
            Assert.Equal("2.3.4.5", version);
        }
        finally { Cleanup(msixPath); }
    }

    [Fact]
    public void GetPackageInfoFromMsix_MissingManifest_Throws()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".msix");
        using (var fs = File.Create(tempFile))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            // no manifest entry
            var dummy = archive.CreateEntry("dummy.txt");
            using var writer = new StreamWriter(dummy.Open());
            writer.Write("placeholder");
        }

        try
        {
            Assert.Throws<InvalidOperationException>(() => ManifestHelper.GetPackageInfoFromMsix(tempFile));
        }
        finally { Cleanup(tempFile); }
    }

    private static string CreateMsixWithManifest(string name, string publisher, string version)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".msix");
        using (var fs = File.Create(tempFile))
        using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry("AppxManifest.xml");
            using var writer = new StreamWriter(manifestEntry.Open());
            writer.Write($"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Package xmlns=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10\">\n  <Identity Name=\"{name}\" Publisher=\"{publisher}\" Version=\"{version}\" ProcessorArchitecture=\"x64\" />\n</Package>");
        }
        return tempFile;
    }

    private static void Cleanup(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
