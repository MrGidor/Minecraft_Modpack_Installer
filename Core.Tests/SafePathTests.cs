namespace Modpack_Installer.Core.Tests;

public class SafePathTests
{
    [Theory]
    [InlineData("../evil.jar")]
    [InlineData("..\\evil.jar")]
    [InlineData("mods/../../evil.jar")]
    [InlineData("a/../b")]
    [InlineData("/etc/passwd")]
    [InlineData("\\windows\\system32\\x.dll")]
    [InlineData("C:\\Windows\\x.dll")]
    [InlineData("C:/Windows/x.dll")]
    [InlineData("mods/a.jar:stream")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("./")]
    public void Resolve_RejectsUnsafePaths(string path)
    {
        using var temp = new TempDir();
        Assert.Throws<UnsafePathException>(() => SafePath.Resolve(temp.Path, path));
    }

    [Theory]
    [InlineData("mods/a.jar", "mods/a.jar")]
    [InlineData("mods\\a.jar", "mods/a.jar")]
    [InlineData("./config/x.toml", "config/x.toml")]
    [InlineData("config//x.toml", "config/x.toml")]
    public void Normalize_ReturnsForwardSlashRelativePath(string input, string expected)
    {
        Assert.Equal(expected, SafePath.Normalize(input));
    }

    [Fact]
    public void Resolve_ReturnsPathInsideRoot()
    {
        using var temp = new TempDir();
        string resolved = SafePath.Resolve(temp.Path, "mods/a.jar");

        Assert.Equal(temp.Combine("mods", "a.jar"), resolved);
    }
}
