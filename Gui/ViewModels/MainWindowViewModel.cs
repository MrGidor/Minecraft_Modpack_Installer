using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Modpack_Installer.Core;
using Modpack_Installer.Gui.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Modpack_Installer.Gui.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly ConfigPolicy[] ConfigPolicies =
    {
        ConfigPolicy.BackupThenOverwrite,
        ConfigPolicy.Overwrite,
        ConfigPolicy.KeepExisting
    };

    private readonly ModpackInstaller _installer = new();
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _cts;

    public MainWindowViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    [ObservableProperty]
    private string statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceHint))]
    private string source = "https://mrgidor.github.io/downloads/DarkRL-Modpack.zip";

    [ObservableProperty]
    private bool backupMods = true;

    [ObservableProperty]
    private int configPolicyIndex = 0;

    [ObservableProperty]
    private double progress = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotDownloading))]
    private bool isDownloading = false;

    public bool IsNotDownloading => !IsDownloading;

    public string SourceHint => PackSources.Detect(Source) switch
    {
        PackSourceKind.LocalFile => "Local file",
        PackSourceKind.Url => "Download from URL",
        PackSourceKind.Modrinth => "Modrinth modpack",
        _ => "Enter a URL, a Modrinth link or slug, or choose a file"
    };

    public void SetSource(string path) => Source = path;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        string? path = await _dialogs.PickPackFileAsync();
        if (path != null)
            Source = path;
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        _cts = new CancellationTokenSource();
        IsDownloading = true;
        Progress = 0;
        StatusMessage = "Starting...";

        try
        {
            var options = new InstallOptions
            {
                ModsAction = BackupMods ? ModsAction.Backup : ModsAction.Delete,
                ConfigPolicy = ConfigPolicies[Math.Clamp(ConfigPolicyIndex, 0, ConfigPolicies.Length - 1)],
                ConfirmDelete = (folder, _) => _dialogs.ConfirmAsync(
                    "Delete mods folder?",
                    $"This will permanently delete:\n{folder}\n\nNo backup will be kept. Continue?")
            };
            var reporter = new System.Progress<InstallProgress>(p =>
            {
                StatusMessage = p.Message;
                if (p.Percent is { } percent)
                    Progress = percent;
            });

            var result = await _installer.InstallAsync(PackSources.Create(Source), options, reporter, _cts.Token);

            Progress = 100;
            StatusMessage = "Modpack installation complete.";
            await _dialogs.ShowMessageAsync("Minecraft Modpack Installer", BuildSummary(result), false);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Installation cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Installation failed.";
            await LogErrorAsync(ex);
            string message = ex.Message;
            if (ModpackInstaller.GetBackupNotice(ex) is { } notice)
                message += $"\n\n{notice}";
            await _dialogs.ShowMessageAsync("Installation failed", message, true);
        }
        finally
        {
            IsDownloading = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private static string BuildSummary(InstallResult result)
    {
        string text = $"Installed {result.FilesInstalled} files ({result.FilesSkipped} skipped).";
        if (result.ModsBackupPath != null)
            text += $"\n\nPrevious mods were backed up to:\n{result.ModsBackupPath}";
        if (result.ConfigBackupPath != null)
            text += $"\n\nReplaced config files were backed up to:\n{result.ConfigBackupPath}";
        foreach (string warning in result.Warnings)
            text += $"\n\n⚠ {warning}";
        return text;
    }

    private static async Task LogErrorAsync(Exception ex)
    {
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "modpack_installer_error.log");
        try
        {
            await File.AppendAllTextAsync(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {ex}\n");
        }
        catch
        {
            // logging must never mask the original error
        }
    }
}
