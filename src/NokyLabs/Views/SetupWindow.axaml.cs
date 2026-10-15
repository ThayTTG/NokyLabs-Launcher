using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NokyLabs.ViewModels;

namespace NokyLabs.Views;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();

        // Durante a instalação, a janela não pode ser fechada no meio do caminho
        Closing += (_, e) =>
        {
            if (DataContext is SetupViewModel { IsWorking: true }) e.Cancel = true;
        };
    }

    private async void OnPickInstallFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm && await PickFolderAsync("Onde instalar o NokyLabs") is { } path)
        {
            // Como os instaladores costumam fazer: escolher "D:\Programas" instala em "D:\Programas\NokyLabs"
            vm.InstallFolder = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))
                                   .Equals("NokyLabs", StringComparison.OrdinalIgnoreCase)
                ? path
                : Path.Combine(path, "NokyLabs");
        }
    }

    private async void OnPickAppsFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel vm && await PickFolderAsync("Onde guardar os apps") is { } path)
            vm.AppsFolder = path;
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
