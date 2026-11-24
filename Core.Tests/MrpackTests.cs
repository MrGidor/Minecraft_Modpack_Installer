namespace Modpack_Installer.Core.Tests;

public class MrpackTests
{
    private const string Sha1 = "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d";

    private static string Index(string files, string extra = "") => $$"""
        {
          "formatVersion": 1,
          "game": "minecraft",
          "versionId": "1.2.3",
          "name": "Test Pack",
          "files": [{{files}}],
          "dependencies": { "minecraft": "1.20.1", "fabric-loader": "0.15.7" }{{extra}}
        }
        """;

    private static string File_(string path, string url = "https://cdn.modrinth.com/data/x/versions/y/a.jar", string env = "")
        => $$"""
        { "path": "{{path}}", "hashes": { "sha1": "{{Sha1}}", "sha512": "{{new string('a', 128)}}" },
          "downloads": ["{{url}}"], "fileSize": 5 {{env}} }
        """;

    [Fact]
    public void Parse_ReadsIndex()
    {
        var index = MrpackParser.Parse(Index(File_("mods/a.jar")));

        Assert.Equal("Test Pack", index.Name);
        Assert.Equal("1.2.3", index.VersionId);
        Assert.Equal("1.20.1", index.Dependencies!["minecraft"]);
        var file = Assert.Single(index.Files!);
        Assert.Equal("mods/a.jar", file.Path);
        Assert.Equal(Sha1, file.Hashes!["sha1"]);
        Assert.Single(file.Downloads!);
        Assert.Equal(5, file.FileSize);
    }

    [Fact]
    public void Parse_ReadsEnv()
    {
        var index = MrpackParser.Parse(Index(File_("mods/a.jar", env: ", \"env\": { \"client\": \"unsupported\", \"server\": \"required\" }")));

        Assert.True(index.Files![0].IsClientUnsupported);
    }

    [Fact]
    public void Parse_RejectsInvalidJson()
    {
        Assert.Throws<InvalidModpackException>(() => MrpackParser.Parse("{ not json"));
    }

    [Fact]
    public void Parse_RejectsUnsupportedFormatVersion()
    {
        string json = Index(File_("mods/a.jar")).Replace("\"formatVersion\": 1", "\"formatVersion\": 2");

        Assert.Throws<InvalidModpackException>(() => MrpackParser.Parse(json));
    }

    [Fact]
    public void Parse_RejectsOtherGames()
    {
        string json = Index(File_("mods/a.jar")).Replace("\"minecraft\",", "\"hytale\",");

        Assert.Throws<InvalidModpackException>(() => MrpackParser.Parse(json));
    }

    [Fact]
    public void Parse_RejectsMissingFilesArray()
    {
        Assert.Throws<InvalidModpackException>(() =>
            MrpackParser.Parse("""{ "formatVersion": 1, "game": "minecraft" }"""));
    }

    [Fact]
    public void Parse_RejectsFileWithoutHashes()
    {
        string file = """{ "path": "mods/a.jar", "hashes": {}, "downloads": ["https://cdn.modrinth.com/a.jar"] }""";

        Assert.Throws<InvalidModpackException>(() => MrpackParser.Parse(Index(file)));
    }

    [Fact]
    public void Parse_RejectsFileWithoutDownloads()
    {
        string file = $$"""{ "path": "mods/a.jar", "hashes": { "sha1": "{{Sha1}}" }, "downloads": [] }""";

        Assert.Throws<InvalidModpackException>(() => MrpackParser.Parse(Index(file)));
    }

    [Fact]
    public void Plan_SkipsClientUnsupportedFiles()
    {
        using var temp = new TempDir();
        string files = File_("mods/client.jar") + "," +
                       File_("mods/server-only.jar", env: ", \"env\": { \"client\": \"unsupported\" }");
        var index = MrpackParser.Parse(Index(files));

        var plan = MrpackPlanner.Plan(index, temp.Path);

        var item = Assert.Single(plan);
        Assert.Equal("mods/client.jar", item.RelativePath);
        Assert.Equal(temp.Combine("mods", "client.jar"), item.FullPath);
    }

    [Theory]
    [InlineData("../evil.jar")]
    [InlineData("mods/../../evil.jar")]
    [InlineData("/etc/cron.d/evil")]
    [InlineData("C:\\evil.dll")]
    public void Plan_RejectsUnsafePaths(string path)
    {
        using var temp = new TempDir();
        var index = MrpackParser.Parse(Index(File_(path.Replace("\\", "\\\\"))));

        Assert.Throws<UnsafePathException>(() => MrpackPlanner.Plan(index, temp.Path));
    }

    [Theory]
    [InlineData("https://evil.example.com/a.jar")]
    [InlineData("http://cdn.modrinth.com/a.jar")]
    [InlineData("https://cdn.modrinth.com.evil.com/a.jar")]
    [InlineData("https://sub.github.com/a.jar")]
    [InlineData("https://cdn.modrinth.com@evil.com/a.jar")]
    public void Plan_RejectsDisallowedHosts(string url)
    {
        using var temp = new TempDir();
        var index = MrpackParser.Parse(Index(File_("mods/a.jar", url)));

        Assert.Throws<InvalidModpackException>(() => MrpackPlanner.Plan(index, temp.Path));
    }

    [Theory]
    [InlineData("https://cdn.modrinth.com/data/a.jar")]
    [InlineData("https://github.com/o/r/releases/download/v1/a.jar")]
    [InlineData("https://raw.githubusercontent.com/o/r/main/a.jar")]
    [InlineData("https://gitlab.com/o/r/-/raw/main/a.jar")]
    public void Plan_AcceptsAllowedHosts(string url)
    {
        using var temp = new TempDir();
        var index = MrpackParser.Parse(Index(File_("mods/a.jar", url)));

        Assert.Single(MrpackPlanner.Plan(index, temp.Path));
    }

    [Fact]
    public void Plan_DropsDisallowedMirrorButKeepsAllowedOne()
    {
        using var temp = new TempDir();
        string file = $$"""
            { "path": "mods/a.jar", "hashes": { "sha1": "{{Sha1}}" },
              "downloads": ["https://evil.example.com/a.jar", "https://cdn.modrinth.com/a.jar"] }
            """;
        var index = MrpackParser.Parse(Index(file));
        var warnings = new List<string>();

        var plan = MrpackPlanner.Plan(index, temp.Path, warnings);

        Assert.Equal(new Uri("https://cdn.modrinth.com/a.jar"), Assert.Single(Assert.Single(plan).Urls));
        Assert.Single(warnings);
    }
}
