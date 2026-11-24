namespace Modpack_Installer.Core;

public static class SafePath
{
    public static string Normalize(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new UnsafePathException(relativePath ?? "", "path is empty");
        if (relativePath.Contains('\0'))
            throw new UnsafePathException(relativePath, "path contains a null character");

        string normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(relativePath) || normalized.Contains(':'))
            throw new UnsafePathException(relativePath, "absolute paths are not allowed");

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Contains(".."))
            throw new UnsafePathException(relativePath, "'..' segments are not allowed");

        var clean = segments.Where(s => s != ".").ToArray();
        if (clean.Length == 0)
            throw new UnsafePathException(relativePath, "path is empty");

        return string.Join('/', clean);
    }

    public static string Resolve(string rootDirectory, string relativePath)
    {
        string normalized = Normalize(relativePath);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        string full = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));

        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, comparison))
            throw new UnsafePathException(relativePath, "path escapes the target directory");

        return full;
    }
}
