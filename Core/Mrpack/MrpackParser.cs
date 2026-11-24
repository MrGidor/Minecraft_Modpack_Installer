using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Modpack_Installer.Core;

public static class MrpackParser
{
    public const string IndexFileName = "modrinth.index.json";

    private const long MaxIndexBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static MrpackIndex ReadFromArchive(ZipArchive zip)
    {
        var entry = zip.GetEntry(IndexFileName)
            ?? throw new InvalidModpackException($"{IndexFileName} was not found in the archive.");
        if (entry.Length > MaxIndexBytes)
            throw new InvalidModpackException($"{IndexFileName} is unreasonably large.");

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    public static MrpackIndex Parse(string json)
    {
        MrpackIndex? index;
        try
        {
            index = JsonSerializer.Deserialize<MrpackIndex>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidModpackException($"{IndexFileName} is not valid JSON: {ex.Message}", ex);
        }

        if (index is null)
            throw new InvalidModpackException($"{IndexFileName} is empty.");
        if (index.FormatVersion != 1)
            throw new InvalidModpackException($"Unsupported .mrpack formatVersion {index.FormatVersion} (only 1 is supported).");
        if (!string.Equals(index.Game, "minecraft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidModpackException($"Unsupported game '{index.Game}' (only minecraft is supported).");
        if (index.Files is null)
            throw new InvalidModpackException($"{IndexFileName} has no 'files' array.");

        foreach (var file in index.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Path))
                throw new InvalidModpackException("A file entry has no 'path'.");
            if (file.Downloads is null || file.Downloads.Count == 0)
                throw new InvalidModpackException($"'{file.Path}' has no download URLs.");
            if (!HashVerifier.HasSupportedHash(file.Hashes))
                throw new InvalidModpackException($"'{file.Path}' has no sha1 or sha512 hash.");
        }

        return index;
    }
}
