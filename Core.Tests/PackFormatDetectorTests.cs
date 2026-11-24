namespace Modpack_Installer.Core.Tests;

public class PackFormatDetectorTests
{
    [Fact]
    public void Detects_Mrpack_ByIndexFile()
    {
        var info = PackFormatDetector.Detect(new[] { "modrinth.index.json", "overrides/config/a.toml" });

        Assert.Equal(PackFormat.Mrpack, info.Format);
    }

    [Fact]
    public void Detects_FullLayout()
    {
        var info = PackFormatDetector.Detect(new[] { "mods/a.jar", "config/x.toml", "resourcepacks/p.zip" });

        Assert.Equal(PackFormat.FullLayout, info.Format);
        Assert.Equal("", info.RootPrefix);
    }

    [Fact]
    public void Detects_FullLayout_WithBackslashes()
    {
        var info = PackFormatDetector.Detect(new[] { "mods\\a.jar" });

        Assert.Equal(PackFormat.FullLayout, info.Format);
    }

    [Fact]
    public void Detects_FlatJars()
    {
        var info = PackFormatDetector.Detect(new[] { "a.jar", "b.jar" });

        Assert.Equal(PackFormat.FlatJars, info.Format);
        Assert.Equal("", info.RootPrefix);
    }

    [Fact]
    public void Detects_FullLayout_InsideWrapperFolder()
    {
        var info = PackFormatDetector.Detect(new[] { "Pack/", "Pack/mods/a.jar", "Pack/config/x.toml" });

        Assert.Equal(PackFormat.FullLayout, info.Format);
        Assert.Equal("Pack/", info.RootPrefix);
    }

    [Fact]
    public void Detects_FlatJars_InsideWrapperFolder()
    {
        var info = PackFormatDetector.Detect(new[] { "Pack/a.jar", "Pack/b.jar" });

        Assert.Equal(PackFormat.FlatJars, info.Format);
        Assert.Equal("Pack/", info.RootPrefix);
    }

    [Fact]
    public void IgnoresMacOsJunk()
    {
        var info = PackFormatDetector.Detect(new[] { "__MACOSX/Pack/._a.jar", "Pack/a.jar", "Pack/.DS_Store" });

        Assert.Equal(PackFormat.FlatJars, info.Format);
        Assert.Equal("Pack/", info.RootPrefix);
    }

    [Theory]
    [InlineData("readme.txt")]
    [InlineData("Pack/readme.txt")]
    public void Unknown_WhenNoJarsAndNoKnownFolders(string name)
    {
        Assert.Equal(PackFormat.Unknown, PackFormatDetector.Detect(new[] { name }).Format);
    }

    [Fact]
    public void Unknown_WhenEmpty()
    {
        Assert.Equal(PackFormat.Unknown, PackFormatDetector.Detect(Array.Empty<string>()).Format);
    }
}
