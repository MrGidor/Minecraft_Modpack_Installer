using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Modpack_Installer.Core;

public sealed class UrlPackSource : IPackSource
{
    private readonly Uri _uri;

    public UrlPackSource(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("The modpack URL must be an absolute http(s) URL.", nameof(url));

        _uri = uri;
    }

    public string Description => $"download {_uri}";

    public async Task<string> AcquireAsync(string workDir, FileDownloader downloader, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(workDir);

        string extension = _uri.AbsolutePath.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase) ? ".mrpack" : ".zip";
        string target = Path.Combine(workDir, "modpack" + extension);

        progress?.Report(new InstallProgress($"Downloading modpack from {_uri}...", 0));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && await TryCurlAsync(target, progress, ct).ConfigureAwait(false))
        {
            progress?.Report(new InstallProgress("Download complete.", 100));
            return target;
        }

        var fraction = new InlineProgress<double>(f =>
            progress?.Report(new InstallProgress("Downloading modpack...", f * 100)));
        await downloader.DownloadAsync(_uri, target, fraction, ct).ConfigureAwait(false);

        progress?.Report(new InstallProgress("Download complete.", 100));
        return target;
    }

    private async Task<bool> TryCurlAsync(string target, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "curl",
                RedirectStandardError = true,
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("-L");
            psi.ArgumentList.Add("-S");
            psi.ArgumentList.Add("-o");
            psi.ArgumentList.Add(target);
            psi.ArgumentList.Add(_uri.AbsoluteUri);

            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start curl process.");
            var stderrTask = proc.StandardError.ReadToEndAsync(ct);
            try
            {
                await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(true); } catch (InvalidOperationException) { }
                throw;
            }

            string stderr = await stderrTask.ConfigureAwait(false);
            if (proc.ExitCode == 0)
                return true;

            progress?.Report(new InstallProgress($"curl failed with exit code {proc.ExitCode} ({stderr.Trim()}). Falling back to HttpClient..."));
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress?.Report(new InstallProgress($"curl unavailable ({ex.Message}). Falling back to HttpClient..."));
            return false;
        }
    }
}
