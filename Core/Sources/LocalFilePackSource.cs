namespace Modpack_Installer.Core;

public sealed class LocalFilePackSource : IPackSource
{
    private readonly string _path;

    public LocalFilePackSource(string path)
    {
        string cleaned = (path ?? "").Trim().Trim('"');
        if (cleaned.Length == 0)
            throw new ArgumentException("The modpack file path must not be empty.", nameof(path));

        _path = cleaned;
    }

    public string Description => $"local file {_path}";

    public Task<string> AcquireAsync(string workDir, FileDownloader downloader, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (!File.Exists(_path))
            throw new FileNotFoundException("Modpack file not found.", _path);

        progress?.Report(new InstallProgress($"Using local file {_path}", 100));
        return Task.FromResult(Path.GetFullPath(_path));
    }
}
