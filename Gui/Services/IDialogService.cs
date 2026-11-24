using System.Threading.Tasks;

namespace Modpack_Installer.Gui.Services;

public interface IDialogService
{
    Task<string?> PickPackFileAsync();

    Task ShowMessageAsync(string title, string message, bool isError);

    Task<bool> ConfirmAsync(string title, string message);
}
