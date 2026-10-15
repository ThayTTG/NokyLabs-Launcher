using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NokyLabs.Models;
using NokyLabs.ViewModels;

namespace NokyLabs.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // A janela "Instalar app" precisa de um dono para abrir como diálogo: quem empresta é esta janela
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.ShowInstallDialog = ShowInstallDialogAsync;
        };
    }

    private async Task<AppInstallChoices?> ShowInstallDialogAsync(AppInstallRequest request)
    {
        // Se a janela estiver escondida (bandeja), usa as escolhas padrão em vez de abrir um diálogo sem dono
        if (!IsVisible) return request.Defaults;

        var dialog = new AppInstallDialog { DataContext = new AppInstallDialogViewModel(request) };
        return await dialog.ShowDialog<AppInstallChoices?>(this);
    }

    /// <summary>
    /// Abre o seletor de pastas do sistema (funciona igual no Windows e no Linux).
    /// Fica no code-behind porque precisa da janela para abrir o diálogo.
    /// </summary>
    private async void OnPickFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Escolha a pasta de origem dos apps",
            AllowMultiple = false
        });

        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (path is null) return;

        vm.SourceFolder = path;
        vm.StatusMessage = "Pasta de origem atualizada.";
    }
}
