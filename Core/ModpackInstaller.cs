using System.IO.Compression;

namespace Modpack_Installer.Core;

public sealed class ModpackInstaller
{
    private const int MaxParallelDownloads = 4;

    private readonly FileDownloader _downloader;

    public ModpackInstaller(FileDownloader? downloader = null)
    {
        _downloader = downloader ?? new FileDownloader();
    }

    public static string? GetBackupNotice(Exception exception) =>
        exception.Data["ModsBackupPath"] is string path
            ? $"Your previous mods were kept in: {path}"
            : null;

    public async Task<InstallResult> InstallAsync(
        IPackSource source,
        InstallOptions options,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        return await Task.Run(async () =>
        {
            string installerDir = options.ResolveInstallerDir();
            Directory.CreateDirectory(installerDir);

            string archive = await source
                .AcquireAsync(installerDir, _downloader, new ScaledProgress(progress, 0, 30), cancellationToken)
                .ConfigureAwait(false);
            return await InstallCoreAsync(archive, options, new ScaledProgress(progress, 30, 100), cancellationToken)
                .ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<InstallResult> InstallArchiveAsync(
        string archivePath,
        InstallOptions options,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => InstallCoreAsync(archivePath, options, progress, cancellationToken), cancellationToken);
    }

    private async Task<InstallResult> InstallCoreAsync(
        string archivePath,
        InstallOptions options,
        IProgress<InstallProgress>? progress,
        CancellationToken ct)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Modpack archive not found.", archivePath);

        var session = new InstallSession(options, progress);
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            var info = PackFormatDetector.Detect(zip);
            session.Result.Format = info.Format;
            session.Report($"Detected pack format: {info.Format}", 0);

            switch (info.Format)
            {
                case PackFormat.Mrpack:
                    await InstallMrpackAsync(zip, session, ct).ConfigureAwait(false);
                    break;
                case PackFormat.FullLayout:
                    await InstallFullLayoutAsync(zip, info.RootPrefix, session, ct).ConfigureAwait(false);
                    break;
                case PackFormat.FlatJars:
                    await InstallFlatJarsAsync(zip, info.RootPrefix, session, ct).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidModpackException(
                        "Unrecognized modpack: the archive has no modrinth.index.json, no Minecraft folders (mods, config, ...) and no .jar files.");
            }

            session.Report("Installation complete.", 100);
            return session.Result;
        }
        catch (Exception ex)
        {
            if (session.Result.ModsBackupPath is { } backup)
                ex.Data["ModsBackupPath"] = backup;
            throw;
        }
    }

    private static async Task InstallFlatJarsAsync(ZipArchive zip, string prefix, InstallSession session, CancellationToken ct)
    {
        var items = CollectItems(zip, prefix)
            .Select(i => new ZipItem(i.Entry, "mods/" + i.Relative))
            .ToList();
        await ExtractAsync(items, session, 5, 100, ct).ConfigureAwait(false);
    }

    private static async Task InstallFullLayoutAsync(ZipArchive zip, string prefix, InstallSession session, CancellationToken ct)
    {
        var all = CollectItems(zip, prefix);
        var items = all.Where(i => PackFormatDetector.IsKnownLayoutPath(i.Relative)).ToList();

        int ignored = all.Count - items.Count;
        if (ignored > 0)
        {
            session.Result.FilesSkipped += ignored;
            session.Result.Warnings.Add($"Ignored {ignored} entries outside the standard Minecraft folders.");
        }

        await ExtractAsync(items, session, 5, 100, ct).ConfigureAwait(false);
    }

    private async Task InstallMrpackAsync(ZipArchive zip, InstallSession session, CancellationToken ct)
    {
        session.Report("Reading modrinth.index.json...", 2);
        MrpackIndex index = MrpackParser.ReadFromArchive(zip);
        session.Result.PackName = index.Name;

        var plan = MrpackPlanner.Plan(index, session.McDir, session.Result.Warnings);
        session.Result.FilesSkipped += (index.Files?.Count ?? 0) - plan.Count;

        if (index.Dependencies is { Count: > 0 } dependencies)
        {
            string required = string.Join(", ", dependencies.Select(d => $"{d.Key} {d.Value}"));
            session.Result.Warnings.Add($"This pack requires {required}. This installer does not install Minecraft or the mod loader.");
        }

        var overrides = CollectItems(zip, "overrides/").Concat(CollectItems(zip, "client-overrides/")).ToList();
        foreach (var item in overrides)
            SafePath.Resolve(session.McDir, item.Relative);

        string staging = Path.Combine(session.InstallerDir, "staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var staged = new string[plan.Count];
            int completed = 0;

            await Parallel.ForEachAsync(
                Enumerable.Range(0, plan.Count),
                new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = ct },
                async (i, token) =>
                {
                    string target = Path.Combine(staging, i + ".tmp");
                    await DownloadVerifiedAsync(plan[i], target, token).ConfigureAwait(false);
                    staged[i] = target;

                    int done = Interlocked.Increment(ref completed);
                    session.Report($"Downloaded {Path.GetFileName(plan[i].FullPath)} ({done}/{plan.Count})", 5 + 80.0 * done / plan.Count);
                }).ConfigureAwait(false);

            session.Report("Installing files...", 85);
            if (plan.Any(p => InstallSession.IsUnderMods(p.RelativePath)) ||
                overrides.Any(o => InstallSession.IsUnderMods(o.Relative)))
                await session.PrepareModsFolderAsync(ct).ConfigureAwait(false);

            for (int i = 0; i < plan.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (!session.BeginWrite(plan[i].RelativePath, plan[i].FullPath))
                    continue;

                File.Move(staged[i], plan[i].FullPath, true);
                session.Result.FilesInstalled++;
            }

            await ExtractAsync(overrides, session, 90, 100, ct).ConfigureAwait(false);
        }
        finally
        {
            try { Directory.Delete(staging, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private async Task DownloadVerifiedAsync(PlannedDownload item, string target, CancellationToken ct)
    {
        Exception? last = null;
        foreach (var url in item.Urls)
        {
            try
            {
                await _downloader.DownloadAsync(url, target, null, ct).ConfigureAwait(false);
                await HashVerifier.VerifyAsync(target, item.Entry.Hashes!, ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is HttpRequestException or HashMismatchException)
            {
                last = ex;
            }
        }

        if (last is HttpRequestException)
            throw new HttpRequestException($"Could not download '{item.RelativePath}': {last.Message}", last);
        throw new InvalidDataException($"Could not download '{item.RelativePath}': {last?.Message}", last);
    }

    private static async Task ExtractAsync(List<ZipItem> items, InstallSession session, double startPercent, double endPercent, CancellationToken ct)
    {
        foreach (var item in items)
            SafePath.Resolve(session.McDir, item.Relative);

        if (items.Any(i => InstallSession.IsUnderMods(i.Relative)))
            await session.PrepareModsFolderAsync(ct).ConfigureAwait(false);

        for (int i = 0; i < items.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            string full = SafePath.Resolve(session.McDir, items[i].Relative);
            if (session.BeginWrite(items[i].Relative, full))
            {
                items[i].Entry.ExtractToFile(full, true);
                session.Result.FilesInstalled++;
            }

            if (i % 25 == 0 || i == items.Count - 1)
                session.Report($"Extracting files ({i + 1}/{items.Count})...",
                    startPercent + (endPercent - startPercent) * (i + 1) / items.Count);
        }
    }

    private static List<ZipItem> CollectItems(ZipArchive zip, string prefix)
    {
        var list = new List<ZipItem>();
        foreach (var entry in zip.Entries)
        {
            string name = PackFormatDetector.NormalizeName(entry.FullName);
            if (name.Length == 0 || name.EndsWith('/') || PackFormatDetector.IsJunk(name))
                continue;
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            string relative = name[prefix.Length..];
            if (relative.Length > 0)
                list.Add(new ZipItem(entry, SafePath.Normalize(relative)));
        }
        return list;
    }

    private sealed record ZipItem(ZipArchiveEntry Entry, string Relative);
}
