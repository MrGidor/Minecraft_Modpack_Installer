namespace Modpack_Installer.Core;

public interface IPackSource
{
    string Description { get; }

    Task<string> AcquireAsync(string workDir, FileDownloader downloader, IProgress<InstallProgress>? progress, CancellationToken ct);
}
