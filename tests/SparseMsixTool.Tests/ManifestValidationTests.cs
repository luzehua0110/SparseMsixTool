using SparseMsixTool.Utils;
using Xunit;

namespace SparseMsixTool.Tests;

public class ManifestValidationTests
{
        private const string ValidManifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
    <Identity Name=""TestApp"" Publisher=""CN=TestPublisher"" Version=""1.2.3.4"" ProcessorArchitecture=""x64"" />
</Package>";

    [Fact]
    public void ValidateManifest_Valid_Succeeds()
    {
        var path = WriteTemp(ValidManifestXml);
        try {
            ManifestHelper.ValidateManifest(path);
        } finally { Cleanup(path); }
    }

    [Fact]
    public void ValidateManifest_MissingIdentity_Throws()
    {
    var xml = @"<?xml version=""1.0""?><Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""></Package>";
        var path = WriteTemp(xml);
        try {
            Assert.Throws<InvalidOperationException>(() => ManifestHelper.ValidateManifest(path));
        } finally { Cleanup(path); }
    }

    [Fact]
    public void ValidateManifest_MissingPublisher_Throws()
    {
    var xml = @"<?xml version=""1.0""?><Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""><Identity Name=""TestApp"" Version=""1.0.0.0"" ProcessorArchitecture=""x64"" /></Package>";
        var path = WriteTemp(xml);
        try {
            Assert.Throws<InvalidOperationException>(() => ManifestHelper.ValidateManifest(path));
        } finally { Cleanup(path); }
    }

    [Fact]
    public void ValidateManifest_InvalidVersionFormat_Throws()
    {
    var xml = @"<?xml version=""1.0""?><Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""><Identity Name=""TestApp"" Publisher=""CN=TestPublisher"" Version=""1.0.0"" ProcessorArchitecture=""x64"" /></Package>";
        var path = WriteTemp(xml);
        try {
            Assert.Throws<InvalidOperationException>(() => ManifestHelper.ValidateManifest(path));
        } finally { Cleanup(path); }
    }

    [Fact]
    public void ValidateManifest_InvalidPublisherPrefix_Throws()
    {
    var xml = @"<?xml version=""1.0""?><Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""><Identity Name=""TestApp"" Publisher=""INVALID=TestPublisher"" Version=""1.0.0.0"" ProcessorArchitecture=""x64"" /></Package>";
        var path = WriteTemp(xml);
        try {
            Assert.Throws<InvalidOperationException>(() => ManifestHelper.ValidateManifest(path));
        } finally { Cleanup(path); }
    }

    private static string WriteTemp(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    private static void Cleanup(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
