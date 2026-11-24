namespace Modpack_Installer.Core;

public sealed class InstallResult
{
    public PackFormat Format { get; internal set; }

    public string? PackName { get; internal set; }

    public int FilesInstalled { get; internal set; }

    public int FilesSkipped { get; internal set; }

    public string? ModsBackupPath { get; internal set; }

    public string? ConfigBackupPath { get; internal set; }

    public List<string> Warnings { get; } = new();
}
