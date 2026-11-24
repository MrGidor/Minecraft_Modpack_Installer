using System.Runtime.InteropServices;

namespace Modpack_Installer.Core;

public static class MinecraftPaths
{
    public static string GetMinecraftDir()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string? appData = Environment.GetEnvironmentVariable("APPDATA");
            if (string.IsNullOrEmpty(appData))
                throw new InvalidOperationException("APPDATA not set on Windows.");
            return Path.Combine(appData, ".minecraft");
        }

        string? home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home))
            throw new InvalidOperationException("HOME not set.");

        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? Path.Combine(home, "Library", "Application Support", "minecraft")
            : Path.Combine(home, ".minecraft");
    }
}
