using System.Xml.Linq;
using SparseMsixTool.Utils;
using Xunit;

namespace SparseMsixTool.Tests;

public class ManifestHelperTests
{
    private const string ValidManifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""
         xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10"">
  <Identity Name=""TestApp"" 
            Publisher=""CN=TestPublisher"" 
            Version=""1.0.0.0"" 
            ProcessorArchitecture=""x64"" />
  <Properties>
    <DisplayName>Test Application</DisplayName>
    <PublisherDisplayName>Test Publisher</PublisherDisplayName>
  </Properties>
</Package>";

    [Fact]
    public void GetPackageInfo_ValidXml_ReturnsCorrectPackageName()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, ValidManifestXml);

            // Act
            var (name, publisher, version) = ManifestHelper.GetPackageInfo(tempFile);

            // Assert
            Assert.Equal("TestApp", name);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetPackageInfo_ValidXml_ReturnsCorrectPublisher()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, ValidManifestXml);

            // Act
            var (name, publisher, version) = ManifestHelper.GetPackageInfo(tempFile);

            // Assert
            Assert.Equal("CN=TestPublisher", publisher);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetPackageInfo_ValidXml_ReturnsCorrectVersion()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, ValidManifestXml);

            // Act
            var (name, publisher, version) = ManifestHelper.GetPackageInfo(tempFile);

            // Assert
            Assert.Equal("1.0.0.0", version);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetPackageInfo_FileNotFound_ThrowsException()
    {
        // Arrange
        var nonExistentFile = "C:\\NonExistent\\AppxManifest.xml";

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => 
            ManifestHelper.GetPackageInfo(nonExistentFile));
    }

    [Fact]
    public void GetPackageInfo_InvalidXml_ThrowsException()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "Invalid XML Content");

            // Act & Assert
            Assert.ThrowsAny<Exception>(() => 
                ManifestHelper.GetPackageInfo(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetPackageInfo_MissingIdentityElement_ThrowsException()
    {
        // Arrange
        var invalidManifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
  <Properties>
    <DisplayName>Test</DisplayName>
  </Properties>
</Package>";
        
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, invalidManifest);

            // Act & Assert
            Assert.ThrowsAny<Exception>(() => 
                ManifestHelper.GetPackageInfo(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void FindManifestPath_CurrentDirectory_ReturnsManifestPath()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var manifestPath = Path.Combine(tempDir, "AppxManifest.xml");
        
        try
        {
            File.WriteAllText(manifestPath, ValidManifestXml);

            // Act
            var result = ManifestHelper.FindManifestPath(tempDir);

            // Assert
            Assert.Equal(manifestPath, result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindManifestPath_NoManifestFound_ThrowsException()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => 
                ManifestHelper.FindManifestPath(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
