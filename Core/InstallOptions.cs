namespace Modpack_Installer.Core;

public enum ModsAction
{
    Backup,
    Delete
}

public enum ConfigPolicy
{
    Overwrite,
    KeepExisting,
    BackupThenOverwrite
}

public sealed class InstallOptions
{
    public static IReadOnlySet<string> DefaultProtectedFiles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "options.txt",
        "servers.dat"
    };

    public string? MinecraftDir { get; init; }

    public string? InstallerDir { get; init; }

    public ModsAction ModsAction { get; init; } = ModsAction.Backup;

    public ConfigPolicy ConfigPolicy { get; init; } = ConfigPolicy.BackupThenOverwrite;

    public IReadOnlySet<string> ProtectedFiles { get; init; } = DefaultProtectedFiles;

    public Func<string, CancellationToken, Task<bool>>? ConfirmDelete { get; init; }

    internal string ResolveMinecraftDir() => Path.GetFullPath(MinecraftDir ?? MinecraftPaths.GetMinecraftDir());

    internal string ResolveInstallerDir() =>
        Path.GetFullPath(InstallerDir ?? Path.Combine(ResolveMinecraftDir(), "modpack_installer"));
}
