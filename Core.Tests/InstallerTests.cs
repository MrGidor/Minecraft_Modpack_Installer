namespace Modpack_Installer.Core.Tests;

public class InstallerTests
{
    private static InstallOptions Options(TempDir temp, ModsAction mods = ModsAction.Backup,
        ConfigPolicy policy = ConfigPolicy.BackupThenOverwrite,
        Func<string, CancellationToken, Task<bool>>? confirm = null) => new()
    {
        MinecraftDir = temp.Combine("mc"),
        ModsAction = mods,
        ConfigPolicy = policy,
        ConfirmDelete = confirm
    };

    private static Task<InstallResult> Install(TempDir temp, string archive, InstallOptions options) =>
        new ModpackInstaller().InstallArchiveAsync(archive, options);

    private static void Seed(TempDir temp, string relative, string content)
    {
        string path = temp.Combine("mc", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public async Task FlatJars_GoToModsFolder_AndOldModsAreBackedUp()
    {
        using var temp = new TempDir();
        Seed(temp, "mods/old.jar", "old");
        string zip = TestZip.Create(temp.Combine("p.zip"), ("a.jar", "new"));

        var result = await Install(temp, zip, Options(temp));

        Assert.Equal(PackFormat.FlatJars, result.Format);
        Assert.Equal("new", File.ReadAllText(temp.Combine("mc", "mods", "a.jar")));
        Assert.False(File.Exists(temp.Combine("mc", "mods", "old.jar")));
        Assert.NotNull(result.ModsBackupPath);
        Assert.Contains("mods-backup-", result.ModsBackupPath);
        Assert.Equal("old", File.ReadAllText(Path.Combine(result.ModsBackupPath!, "old.jar")));
    }

    [Fact]
    public async Task Delete_WithoutConfirmCallback_RefusesAndKeepsMods()
    {
        using var temp = new TempDir();
        Seed(temp, "mods/old.jar", "old");
        string zip = TestZip.Create(temp.Combine("p.zip"), ("a.jar", "new"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Install(temp, zip, Options(temp, ModsAction.Delete)));

        Assert.True(File.Exists(temp.Combine("mc", "mods", "old.jar")));
    }

    [Fact]
    public async Task Delete_WhenNotConfirmed_IsCancelledAndKeepsMods()
    {
        using var temp = new TempDir();
        Seed(temp, "mods/old.jar", "old");
        string zip = TestZip.Create(temp.Combine("p.zip"), ("a.jar", "new"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Install(temp, zip, Options(temp, ModsAction.Delete, confirm: (_, _) => Task.FromResult(false))));

        Assert.True(File.Exists(temp.Combine("mc", "mods", "old.jar")));
    }

    [Fact]
    public async Task Delete_WhenConfirmed_ReplacesMods()
    {
        using var temp = new TempDir();
        Seed(temp, "mods/old.jar", "old");
        string zip = TestZip.Create(temp.Combine("p.zip"), ("a.jar", "new"));

        var result = await Install(temp, zip, Options(temp, ModsAction.Delete, confirm: (_, _) => Task.FromResult(true)));

        Assert.Null(result.ModsBackupPath);
        Assert.False(File.Exists(temp.Combine("mc", "mods", "old.jar")));
        Assert.True(File.Exists(temp.Combine("mc", "mods", "a.jar")));
    }

    [Fact]
    public async Task PathTraversalEntry_IsRejectedBeforeAnythingIsWritten()
    {
        using var temp = new TempDir();
        Seed(temp, "mods/old.jar", "old");
        string zip = TestZip.Create(temp.Combine("p.zip"), ("mods/a.jar", "new"), ("../evil.txt", "x"));

        await Assert.ThrowsAsync<UnsafePathException>(() => Install(temp, zip, Options(temp)));

        Assert.False(File.Exists(temp.Combine("evil.txt")));
        Assert.False(File.Exists(temp.Combine("mc", "evil.txt")));
        Assert.True(File.Exists(temp.Combine("mc", "mods", "old.jar")));
    }

    [Fact]
    public async Task FullLayout_BacksUpThenOverwritesConfig_ByDefault()
    {
        using var temp = new TempDir();
        Seed(temp, "config/a.toml", "mine");
        string zip = TestZip.Create(temp.Combine("p.zip"), ("config/a.toml", "pack"), ("mods/m.jar", "jar"));

        var result = await Install(temp, zip, Options(temp));

        Assert.Equal(PackFormat.FullLayout, result.Format);
        Assert.Equal("pack", File.ReadAllText(temp.Combine("mc", "config", "a.toml")));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(result.ConfigBackupPath!, "config", "a.toml")));
        Assert.True(File.Exists(temp.Combine("mc", "mods", "m.jar")));
    }

    [Fact]
    public async Task FullLayout_KeepExisting_LeavesConfigAlone()
    {
        using var temp = new TempDir();
        Seed(temp, "config/a.toml", "mine");
        string zip = TestZip.Create(temp.Combine("p.zip"), ("config/a.toml", "pack"), ("config/b.toml", "new"));

        var result = await Install(temp, zip, Options(temp, policy: ConfigPolicy.KeepExisting));

        Assert.Equal("mine", File.ReadAllText(temp.Combine("mc", "config", "a.toml")));
        Assert.Equal("new", File.ReadAllText(temp.Combine("mc", "config", "b.toml")));
        Assert.Equal(1, result.FilesSkipped);
    }

    [Fact]
    public async Task FullLayout_Overwrite_ReplacesConfigWithoutBackup()
    {
        using var temp = new TempDir();
        Seed(temp, "config/a.toml", "mine");
        string zip = TestZip.Create(temp.Combine("p.zip"), ("config/a.toml", "pack"));

        var result = await Install(temp, zip, Options(temp, policy: ConfigPolicy.Overwrite));

        Assert.Equal("pack", File.ReadAllText(temp.Combine("mc", "config", "a.toml")));
        Assert.Null(result.ConfigBackupPath);
    }

    [Theory]
    [InlineData(ConfigPolicy.Overwrite)]
    [InlineData(ConfigPolicy.KeepExisting)]
    [InlineData(ConfigPolicy.BackupThenOverwrite)]
    public async Task OptionsAndServers_AreNeverOverwrittenByDefault(ConfigPolicy policy)
    {
        using var temp = new TempDir();
        Seed(temp, "options.txt", "mine");
        Seed(temp, "servers.dat", "mine");
        string zip = TestZip.Create(temp.Combine("p.zip"),
            ("options.txt", "pack"), ("servers.dat", "pack"), ("config/a.toml", "pack"));

        await Install(temp, zip, Options(temp, policy: policy));

        Assert.Equal("mine", File.ReadAllText(temp.Combine("mc", "options.txt")));
        Assert.Equal("mine", File.ReadAllText(temp.Combine("mc", "servers.dat")));
    }

    [Fact]
    public async Task OptionsTxt_CanBeOverwrittenWhenProtectionIsCleared()
    {
        using var temp = new TempDir();
        Seed(temp, "options.txt", "mine");
        string zip = TestZip.Create(temp.Combine("p.zip"), ("options.txt", "pack"), ("config/a.toml", "pack"));
        var options = new InstallOptions
        {
            MinecraftDir = temp.Combine("mc"),
            ConfigPolicy = ConfigPolicy.Overwrite,
            ProtectedFiles = new HashSet<string>()
        };

        await Install(temp, zip, options);

        Assert.Equal("pack", File.ReadAllText(temp.Combine("mc", "options.txt")));
    }

    [Fact]
    public async Task FullLayout_IgnoresLauncherFilesAndStrayRootFiles()
    {
        using var temp = new TempDir();
        Seed(temp, "launcher_accounts.json", "secret");
        string zip = TestZip.Create(temp.Combine("p.zip"),
            ("config/a.toml", "pack"), ("launcher_accounts.json", "evil"), ("readme.txt", "hi"));

        var result = await Install(temp, zip, Options(temp));

        Assert.Equal("secret", File.ReadAllText(temp.Combine("mc", "launcher_accounts.json")));
        Assert.False(File.Exists(temp.Combine("mc", "readme.txt")));
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task WrapperFolder_IsStripped()
    {
        using var temp = new TempDir();
        string zip = TestZip.Create(temp.Combine("p.zip"), ("Pack/mods/a.jar", "jar"), ("Pack/config/a.toml", "c"));

        await Install(temp, zip, Options(temp));

        Assert.True(File.Exists(temp.Combine("mc", "mods", "a.jar")));
        Assert.True(File.Exists(temp.Combine("mc", "config", "a.toml")));
    }

    [Fact]
    public async Task Mrpack_AppliesOverrides_ClientOverridesWin_ServerOverridesIgnored()
    {
        using var temp = new TempDir();
        string index = """{ "formatVersion": 1, "game": "minecraft", "name": "P", "files": [], "dependencies": { "minecraft": "1.20.1" } }""";
        string zip = TestZip.Create(temp.Combine("p.mrpack"),
            ("modrinth.index.json", index),
            ("overrides/config/a.toml", "base"),
            ("overrides/mods/local.jar", "jar"),
            ("client-overrides/config/a.toml", "client"),
            ("server-overrides/config/s.toml", "server"));

        var result = await Install(temp, zip, Options(temp));

        Assert.Equal(PackFormat.Mrpack, result.Format);
        Assert.Equal("P", result.PackName);
        Assert.Equal("client", File.ReadAllText(temp.Combine("mc", "config", "a.toml")));
        Assert.True(File.Exists(temp.Combine("mc", "mods", "local.jar")));
        Assert.False(File.Exists(temp.Combine("mc", "config", "s.toml")));
        Assert.Contains(result.Warnings, w => w.Contains("1.20.1"));
    }

    [Fact]
    public async Task Mrpack_SkipsClientUnsupportedFiles_WithoutNetwork()
    {
        using var temp = new TempDir();
        string index = """
            { "formatVersion": 1, "game": "minecraft", "name": "P",
              "files": [ { "path": "mods/server.jar", "hashes": { "sha1": "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d" },
                           "env": { "client": "unsupported", "server": "required" },
                           "downloads": ["https://cdn.modrinth.com/server.jar"] } ] }
            """;
        string zip = TestZip.Create(temp.Combine("p.mrpack"), ("modrinth.index.json", index));

        var result = await Install(temp, zip, Options(temp));

        Assert.Equal(0, result.FilesInstalled);
        Assert.Equal(1, result.FilesSkipped);
        Assert.False(File.Exists(temp.Combine("mc", "mods", "server.jar")));
    }

    [Fact]
    public async Task Mrpack_WithTraversalOverride_IsRejected()
    {
        using var temp = new TempDir();
        string index = """{ "formatVersion": 1, "game": "minecraft", "files": [] }""";
        string zip = TestZip.Create(temp.Combine("p.mrpack"),
            ("modrinth.index.json", index), ("overrides/../../evil.txt", "x"));

        await Assert.ThrowsAsync<UnsafePathException>(() => Install(temp, zip, Options(temp)));

        Assert.False(File.Exists(temp.Combine("evil.txt")));
    }

    [Fact]
    public async Task Mrpack_WithDisallowedHost_IsRejectedBeforeInstalling()
    {
        using var temp = new TempDir();
        Seed(temp, "mods/old.jar", "old");
        string index = """
            { "formatVersion": 1, "game": "minecraft",
              "files": [ { "path": "mods/a.jar", "hashes": { "sha1": "aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d" },
                           "downloads": ["https://evil.example.com/a.jar"] } ] }
            """;
        string zip = TestZip.Create(temp.Combine("p.mrpack"), ("modrinth.index.json", index));

        await Assert.ThrowsAsync<InvalidModpackException>(() => Install(temp, zip, Options(temp)));

        Assert.True(File.Exists(temp.Combine("mc", "mods", "old.jar")));
    }

    [Fact]
    public async Task UnrecognizedArchive_IsRejected()
    {
        using var temp = new TempDir();
        string zip = TestZip.Create(temp.Combine("p.zip"), ("readme.txt", "hi"));

        await Assert.ThrowsAsync<InvalidModpackException>(() => Install(temp, zip, Options(temp)));
    }
}
