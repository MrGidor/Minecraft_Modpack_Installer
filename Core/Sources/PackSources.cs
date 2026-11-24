namespace Modpack_Installer.Core;

public enum PackSourceKind
{
    None,
    LocalFile,
    Url,
    Modrinth
}

public static class PackSources
{
    public static PackSourceKind Detect(string? input)
    {
        string text = Clean(input);
        if (text.Length == 0)
            return PackSourceKind.None;
        if (File.Exists(text))
            return PackSourceKind.LocalFile;

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return ModrinthPackSource.IsModrinthHost(uri) ? PackSourceKind.Modrinth : PackSourceKind.Url;

        if (text.Contains('/') || text.Contains('\\') ||
            text.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            text.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
            return PackSourceKind.LocalFile;

        return ModrinthPackSource.IsValidSlug(text) ? PackSourceKind.Modrinth : PackSourceKind.None;
    }

    public static IPackSource Create(string? input)
    {
        string text = Clean(input);
        return Detect(text) switch
        {
            PackSourceKind.LocalFile => new LocalFilePackSource(text),
            PackSourceKind.Url => new UrlPackSource(text),
            PackSourceKind.Modrinth => new ModrinthPackSource(text),
            _ => throw new ArgumentException("Enter a modpack URL, a Modrinth link or slug, or a local .zip/.mrpack file.", nameof(input))
        };
    }

    private static string Clean(string? input) => (input ?? "").Trim().Trim('"');
}
