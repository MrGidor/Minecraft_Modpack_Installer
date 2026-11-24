using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Modpack_Installer.Core;

public sealed record ModrinthTarget(string Slug, string? Version);

public sealed class ModrinthPackSource : IPackSource
{
    private static readonly Uri ApiBase = new("https://api.modrinth.com/v2/");

    private static readonly Regex SlugPattern = new(
        @"^[\w!@$()`.+,""\-']{3,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ModrinthTarget _target;

    public ModrinthPackSource(string slugOrLink)
    {
        _target = ParseInput(slugOrLink);
    }

    public string Description => $"Modrinth modpack {_target.Slug}";

    public static bool IsValidSlug(string text) => SlugPattern.IsMatch(text);

    internal static bool IsModrinthHost(Uri uri) =>
        uri.Host.Equals("modrinth.com", StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("www.modrinth.com", StringComparison.OrdinalIgnoreCase);

    public static ModrinthTarget ParseInput(string? input)
    {
        string text = (input ?? "").Trim().Trim('"');
        if (text.Length == 0)
            throw new ArgumentException("Enter a Modrinth project slug or link.", nameof(input));

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            if (!IsModrinthHost(uri))
                throw new ArgumentException($"'{text}' is not a modrinth.com link.", nameof(input));

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
                throw new ArgumentException($"'{text}' does not point to a Modrinth project.", nameof(input));

            string? version = segments.Length >= 4 && segments[2] == "version"
                ? Uri.UnescapeDataString(segments[3])
                : null;
            return new ModrinthTarget(Uri.UnescapeDataString(segments[1]), version);
        }

        if (!IsValidSlug(text))
            throw new ArgumentException($"'{text}' is not a valid Modrinth slug.", nameof(input));

        return new ModrinthTarget(text, null);
    }

    public async Task<string> AcquireAsync(string workDir, FileDownloader downloader, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        progress?.Report(new InstallProgress($"Looking up '{_target.Slug}' on Modrinth...", 0));
        string slug = Uri.EscapeDataString(_target.Slug);

        var project = await GetJsonAsync<ModrinthProject>(downloader, $"project/{slug}", ct).ConfigureAwait(false);
        if (!string.Equals(project.ProjectType, "modpack", StringComparison.OrdinalIgnoreCase))
            throw new InvalidModpackException($"'{_target.Slug}' is a Modrinth {project.ProjectType ?? "project"}, not a modpack.");

        ModrinthVersion version;
        if (_target.Version is { } requested)
        {
            version = await GetJsonAsync<ModrinthVersion>(
                downloader, $"project/{slug}/version/{Uri.EscapeDataString(requested)}", ct).ConfigureAwait(false);
        }
        else
        {
            var versions = await GetJsonAsync<List<ModrinthVersion>>(downloader, $"project/{slug}/version", ct).ConfigureAwait(false);
            var candidates = versions.Where(v => v.Files is { Count: > 0 }).OrderByDescending(v => v.DatePublished).ToList();
            version = candidates.FirstOrDefault(v => v.VersionType == "release")
                ?? candidates.FirstOrDefault()
                ?? throw new InvalidModpackException($"'{_target.Slug}' has no downloadable versions.");
        }

        var files = version.Files ?? new List<ModrinthVersionFile>();
        var file = files.FirstOrDefault(f => f.Primary)
            ?? files.FirstOrDefault(f => f.Filename?.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase) == true)
            ?? files.FirstOrDefault()
            ?? throw new InvalidModpackException("The selected Modrinth version has no files.");

        if (!Uri.TryCreate(file.Url, UriKind.Absolute, out var fileUri) || !DownloadHosts.IsAllowed(fileUri))
            throw new InvalidModpackException($"Modrinth returned a download URL on a host that is not allowed: {file.Url}");

        string fileName = Path.GetFileName(file.Filename ?? "");
        if (fileName.Length == 0 || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            fileName = "modpack.mrpack";

        string target = Path.Combine(workDir, fileName);
        string label = $"{project.Title ?? _target.Slug} {version.VersionNumber}".Trim();
        var fraction = new InlineProgress<double>(f =>
            progress?.Report(new InstallProgress($"Downloading {label}...", 5 + f * 95)));
        progress?.Report(new InstallProgress($"Downloading {label}...", 5));

        await downloader.DownloadAsync(fileUri, target, fraction, ct).ConfigureAwait(false);
        if (file.Hashes is { Count: > 0 })
            await HashVerifier.VerifyAsync(target, file.Hashes, ct).ConfigureAwait(false);

        return target;
    }

    private static async Task<T> GetJsonAsync<T>(FileDownloader downloader, string relative, CancellationToken ct) where T : class
    {
        try
        {
            string json = await downloader.GetStringAsync(new Uri(ApiBase, relative), ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new InvalidDataException("Modrinth returned an empty response.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidModpackException("Modrinth could not find that modpack or version.", ex);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Unexpected response from Modrinth: {ex.Message}", ex);
        }
    }

    private sealed class ModrinthProject
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("project_type")]
        public string? ProjectType { get; set; }
    }

    private sealed class ModrinthVersion
    {
        [JsonPropertyName("version_number")]
        public string? VersionNumber { get; set; }

        [JsonPropertyName("version_type")]
        public string? VersionType { get; set; }

        [JsonPropertyName("date_published")]
        public DateTimeOffset DatePublished { get; set; }

        [JsonPropertyName("files")]
        public List<ModrinthVersionFile>? Files { get; set; }
    }

    private sealed class ModrinthVersionFile
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("filename")]
        public string? Filename { get; set; }

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }

        [JsonPropertyName("hashes")]
        public Dictionary<string, string>? Hashes { get; set; }
    }
}
