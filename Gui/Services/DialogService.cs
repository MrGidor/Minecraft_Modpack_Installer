using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Threading.Tasks;

namespace Modpack_Installer.Gui.Services;

public sealed class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner)
    {
        _owner = owner;
    }

    public Task<string?> PickPackFileAsync() => Dispatcher.UIThread.InvokeAsync<string?>(async () =>
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a modpack",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Modpacks") { Patterns = new[] { "*.zip", "*.mrpack" } },
                FilePickerFileTypes.All
            }
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    });

    public Task ShowMessageAsync(string title, string message, bool isError) => Dispatcher.UIThread.InvokeAsync(async () =>
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            title, message, ButtonEnum.Ok, isError ? Icon.Error : Icon.Success);
        await box.ShowAsync();
    });

    public Task<bool> ConfirmAsync(string title, string message) => Dispatcher.UIThread.InvokeAsync<bool>(async () =>
    {
        var box = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.YesNo, Icon.Warning);
        return await box.ShowAsync() == ButtonResult.Yes;
    });
}
