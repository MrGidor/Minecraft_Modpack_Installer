using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Modpack_Installer.Gui.ViewModels;
using System.Linq;

namespace Modpack_Installer.Gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, OnDrop, handledEventsToo: true);
    }

    // IDataObject members are marked obsolete in newer Avalonia 11.x releases but still work
#pragma warning disable CS0618
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) && CanAcceptDrop()
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!CanAcceptDrop())
            return;

        string? path = e.Data.GetFiles()?.FirstOrDefault()?.TryGetLocalPath();
        if (path != null && DataContext is MainWindowViewModel vm)
            vm.SetSource(path);
        e.Handled = true;
    }
#pragma warning restore CS0618

    private bool CanAcceptDrop() => DataContext is MainWindowViewModel { IsNotDownloading: true };
}
