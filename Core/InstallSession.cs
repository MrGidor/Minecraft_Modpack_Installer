namespace Modpack_Installer.Core;

internal sealed class InstallSession
{
    private readonly string _stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
    private readonly HashSet<string> _written = new(StringComparer.OrdinalIgnoreCase);
    private bool _modsPrepared;
    private string? _configBackupDir;

    public InstallSession(InstallOptions options, IProgress<InstallProgress>? progress)
    {
        Options = options;
        Progress = progress;
        McDir = options.ResolveMinecraftDir();
        ModsDir = Path.Combine(McDir, "mods");
        InstallerDir = options.ResolveInstallerDir();
    }

    public InstallOptions Options { get; }

    public IProgress<InstallProgress>? Progress { get; }

    public InstallResult Result { get; } = new();

    public string McDir { get; }

    public string ModsDir { get; }

    public string InstallerDir { get; }

    public void Report(string message, double? percent = null) =>
        Progress?.Report(new InstallProgress(message, percent));

    public async Task PrepareModsFolderAsync(CancellationToken ct)
    {
        if (_modsPrepared)
            return;
        _modsPrepared = true;

        if (Directory.Exists(ModsDir) && Directory.EnumerateFileSystemEntries(ModsDir).Any())
        {
            if (Options.ModsAction == ModsAction.Backup)
            {
                string backup = UniquePath(Path.Combine(McDir, $"mods-backup-{_stamp}"));
                Report($"Backing up current mods to {backup}...");
                Directory.Move(ModsDir, backup);
                Result.ModsBackupPath = backup;
            }
            else
            {
                if (Options.ConfirmDelete is null)
                    throw new InvalidOperationException("Deleting the existing mods folder requires a confirmation callback.");
                if (!await Options.ConfirmDelete(ModsDir, ct).ConfigureAwait(false))
                    throw new OperationCanceledException("Deleting the existing mods folder was not confirmed.");

                Report("Deleting current mods folder...");
                Directory.Delete(ModsDir, true);
            }
        }

        Directory.CreateDirectory(ModsDir);
    }

    public static bool IsUnderMods(string relativePath) =>
        relativePath.StartsWith("mods/", StringComparison.OrdinalIgnoreCase);

    public bool BeginWrite(string relativePath, string fullPath)
    {
        if (ProtectedLocations.IsBlocked(relativePath))
        {
            Result.FilesSkipped++;
            Result.Warnings.Add($"Skipped protected location: {relativePath}");
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        bool existing = File.Exists(fullPath);
        if (existing && !_written.Contains(relativePath) && !IsUnderMods(relativePath))
        {
            if (Options.ProtectedFiles.Contains(relativePath) || Options.ConfigPolicy == ConfigPolicy.KeepExisting)
            {
                Result.FilesSkipped++;
                return false;
            }

            if (Options.ConfigPolicy == ConfigPolicy.BackupThenOverwrite)
                BackupExisting(relativePath, fullPath);
        }

        _written.Add(relativePath);
        return true;
    }

    private void BackupExisting(string relativePath, string fullPath)
    {
        _configBackupDir ??= UniquePath(Path.Combine(InstallerDir, $"config-backup-{_stamp}"));
        string destination = Path.Combine(_configBackupDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(fullPath, destination, true);
        Result.ConfigBackupPath = _configBackupDir;
    }

    public static string UniquePath(string path)
    {
        string candidate = path;
        for (int n = 1; Directory.Exists(candidate) || File.Exists(candidate); n++)
            candidate = $"{path}-{n}";
        return candidate;
    }
}

internal static class ProtectedLocations
{
    private static readonly HashSet<string> BlockedTopLevelDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "versions", "libraries", "assets", "runtime", "modpack_installer"
    };

    public static bool IsBlocked(string relativePath)
    {
        int slash = relativePath.IndexOf('/');
        return slash < 0
            ? relativePath.StartsWith("launcher_", StringComparison.OrdinalIgnoreCase)
            : BlockedTopLevelDirs.Contains(relativePath[..slash]);
    }
}
