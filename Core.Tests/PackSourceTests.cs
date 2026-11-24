namespace Modpack_Installer.Core.Tests;

public class PackSourceTests
{
    [Fact]
    public void ParseInput_AcceptsBareSlug()
    {
        var target = ModrinthPackSource.ParseInput("fabulously-optimized");

        Assert.Equal("fabulously-optimized", target.Slug);
        Assert.Null(target.Version);
    }

    [Theory]
    [InlineData("https://modrinth.com/modpack/fabulously-optimized")]
    [InlineData("https://www.modrinth.com/modpack/fabulously-optimized/versions")]
    [InlineData("https://modrinth.com/project/fabulously-optimized")]
    public void ParseInput_AcceptsProjectLinks(string link)
    {
        Assert.Equal("fabulously-optimized", ModrinthPackSource.ParseInput(link).Slug);
    }

    [Fact]
    public void ParseInput_ReadsVersionFromLink()
    {
        var target = ModrinthPackSource.ParseInput("https://modrinth.com/modpack/fabulously-optimized/version/5.0.0");

        Assert.Equal("fabulously-optimized", target.Slug);
        Assert.Equal("5.0.0", target.Version);
    }

    [Theory]
    [InlineData("https://example.com/modpack/foo")]
    [InlineData("https://modrinth.com/")]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("")]
    public void ParseInput_RejectsInvalidInput(string input)
    {
        Assert.Throws<ArgumentException>(() => ModrinthPackSource.ParseInput(input));
    }

    [Fact]
    public void Detect_ClassifiesInput()
    {
        using var temp = new TempDir();
        string existing = TestZip.Create(temp.Combine("pack.zip"), ("a.jar", "x"));

        Assert.Equal(PackSourceKind.LocalFile, PackSources.Detect(existing));
        Assert.Equal(PackSourceKind.LocalFile, PackSources.Detect($"\"{existing}\""));
        Assert.Equal(PackSourceKind.LocalFile, PackSources.Detect("C:\\missing\\pack.mrpack"));
        Assert.Equal(PackSourceKind.Url, PackSources.Detect("https://example.com/pack.zip"));
        Assert.Equal(PackSourceKind.Modrinth, PackSources.Detect("https://modrinth.com/modpack/foo"));
        Assert.Equal(PackSourceKind.Modrinth, PackSources.Detect("fabulously-optimized"));
        Assert.Equal(PackSourceKind.None, PackSources.Detect("  "));
    }

    [Fact]
    public void UrlPackSource_RejectsEmptyAndNonHttpUrls()
    {
        Assert.Throws<ArgumentException>(() => new UrlPackSource(""));
        Assert.Throws<ArgumentException>(() => new UrlPackSource("ftp://example.com/a.zip"));
        Assert.Throws<ArgumentException>(() => new UrlPackSource("not a url"));
    }
}
