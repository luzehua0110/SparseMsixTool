using Xunit;

namespace SparseMsixTool.Tests;

public class BrandingConsistencyTests
{
    [Fact]
    public void NoLegacyAliasOutsideAllowedFiles()
    {
        // Allowed files: 更新日志.md and releases/archive/* (historical context)
        var repoRoot = GetRepoRoot();
        var disallowedHits = new List<string>();
        foreach (var file in Directory.GetFiles(repoRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(repoRoot, file).Replace("\\", "/");
            // Whitelist historical / generated / archive areas
            if (relative.EndsWith("更新日志.md") ||
                relative.StartsWith("releases/archive/") ||
                relative.StartsWith("bin/") ||
                relative.StartsWith("obj/") ||
                relative.StartsWith("publish/") ||
                relative.StartsWith(".github/") ||
                relative.StartsWith(".githooks/") ||
                relative.Contains("/archive/") ||
                relative.StartsWith("tests/MsixTool.Tests"))
                continue;
            if (Path.GetFileName(file).StartsWith(".") || relative.StartsWith(".git/"))
                continue; // skip hidden / git internals
            string content;
            try { content = File.ReadAllText(file); } catch { continue; }
            if (content.IndexOf("msixtool", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                disallowedHits.Add(relative);
            }
        }
        Assert.True(disallowedHits.Count == 0, "Legacy alias 'msixtool' found in: " + string.Join(", ", disallowedHits));
    }

    private static string GetRepoRoot()
    {
        // ascend until .git exists
        var dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Path.GetDirectoryName(dir) ?? string.Empty;
        }
        throw new DirectoryNotFoundException("Cannot locate repository root (.git folder).");
    }
}
