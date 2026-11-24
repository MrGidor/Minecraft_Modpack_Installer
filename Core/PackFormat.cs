using System.IO.Compression;

namespace Modpack_Installer.Core;

public enum PackFormat
{
    Unknown,
    Mrpack,
    FullLayout,
    FlatJars
}

public sealed record PackFormatInfo(PackFormat Format, string RootPrefix);

public static class PackFormatDetector
{
    public static IReadOnlySet<string> KnownTopLevelDirs { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "mods", "config", "resourcepacks", "shaderpacks", "saves", "scripts",
        "defaultconfigs", "kubejs", "datapacks", "schematics"
    };

    public static IReadOnlySet<string> KnownRootFiles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "options.txt", "servers.dat"
    };

    public static PackFormatInfo Detect(ZipArchive zip) => Detect(zip.Entries.Select(e => e.FullName));

    public static PackFormatInfo Detect(IEnumerable<string> entryNames)
    {
        var names = entryNames
            .Select(NormalizeName)
            .Where(n => n.Length > 0 && !IsJunk(n))
            .ToList();

        if (names.Contains(MrpackParser.IndexFileName))
            return new PackFormatInfo(PackFormat.Mrpack, "");
        if (names.Count == 0)
            return new PackFormatInfo(PackFormat.Unknown, "");

        string prefix = "";
        var topLevel = names.Select(TopSegment).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (topLevel.Count == 1 && names.All(n => n.Contains('/')) && !KnownTopLevelDirs.Contains(topLevel[0]))
        {
            prefix = topLevel[0] + "/";
            names = names.Select(n => n[prefix.Length..]).Where(n => n.Length > 0).ToList();
        }

        if (names.Any(IsKnownLayoutPath))
            return new PackFormatInfo(PackFormat.FullLayout, prefix);
        if (names.Any(n => n.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)))
            return new PackFormatInfo(PackFormat.FlatJars, prefix);

        return new PackFormatInfo(PackFormat.Unknown, "");
    }

    internal static string NormalizeName(string name) => name.Replace('\\', '/');

    internal static bool IsJunk(string normalizedName)
    {
        if (string.Equals(TopSegment(normalizedName), "__MACOSX", StringComparison.OrdinalIgnoreCase))
            return true;

        string file = normalizedName[(normalizedName.LastIndexOf('/') + 1)..];
        return file is ".DS_Store" or "Thumbs.db" or "desktop.ini";
    }

    internal static bool IsKnownLayoutPath(string relativePath)
    {
        int slash = relativePath.IndexOf('/');
        return slash > 0
            ? KnownTopLevelDirs.Contains(relativePath[..slash])
            : KnownRootFiles.Contains(relativePath);
    }

    private static string TopSegment(string name)
    {
        int slash = name.IndexOf('/');
        return slash < 0 ? name : name[..slash];
    }
}
