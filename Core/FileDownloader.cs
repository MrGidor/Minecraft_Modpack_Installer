namespace Modpack_Installer.Core;

public sealed class FileDownloader
{
    private const string UserAgent =
        "MrGidor/Minecraft_Modpack_Installer/2.0 (https://github.com/MrGidor/Minecraft_Modpack_Installer)";

    private static readonly HttpClient SharedClient = CreateClient();

    private readonly HttpClient _http;

    public FileDownloader(HttpClient? http = null)
    {
        _http = http ?? SharedClient;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        return client;
    }

    public async Task<string> GetStringAsync(Uri uri, CancellationToken ct)
    {
        using var response = await _http.GetAsync(uri, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    public async Task DownloadAsync(Uri uri, string destinationPath, IProgress<double>? progress, CancellationToken ct)
    {
        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string partPath = destinationPath + ".part";
        try
        {
            using (var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;

                await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var target = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    done += read;
                    if (total is > 0)
                        progress?.Report((double)done / total.Value);
                }
            }

            File.Move(partPath, destinationPath, true);
        }
        catch
        {
            try { File.Delete(partPath); } catch (IOException) { }
            throw;
        }
    }
}
