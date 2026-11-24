namespace Modpack_Installer.Core;

public sealed record PlannedDownload(MrpackFile Entry, string RelativePath, string FullPath, IReadOnlyList<Uri> Urls);

public static class MrpackPlanner
{
    public static IReadOnlyList<PlannedDownload> Plan(MrpackIndex index, string minecraftDir, ICollection<string>? warnings = null)
    {
        var plan = new List<PlannedDownload>();

        foreach (var file in index.Files ?? new List<MrpackFile>())
        {
            if (file.IsClientUnsupported)
                continue;

            string relative = SafePath.Normalize(file.Path);
            string fullPath = SafePath.Resolve(minecraftDir, relative);

            var urls = new List<Uri>();
            foreach (string raw in file.Downloads ?? new List<string>())
            {
                if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) && DownloadHosts.IsAllowed(uri))
                    urls.Add(uri);
                else
                    warnings?.Add($"Ignored a download source for {relative} on a host that is not allowed: {raw}");
            }

            if (urls.Count == 0)
                throw new InvalidModpackException(
                    $"'{relative}' has no https download URL on an allowed host ({string.Join(", ", DownloadHosts.Allowed)}).");

            plan.Add(new PlannedDownload(file, relative, fullPath, urls));
        }

        return plan;
    }
}
