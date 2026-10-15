using Avalonia.Controls;
using Avalonia.Interactivity;
using NokyLabs.Models;
using NokyLabs.ViewModels;

namespace NokyLabs.Views;

/// <summary>
/// Janela "Instalar app": abre por cima do launcher e devolve as escolhas
/// (ou null, se a pessoa cancelar ou fechar no X).
/// </summary>
public partial class AppInstallDialog : Window
{
    public AppInstallDialog()
    {
        InitializeComponent();
    }

    private void OnInstall(object? sender, RoutedEventArgs e)
    {
        AppInstallChoices? choices = (DataContext as AppInstallDialogViewModel)?.ToChoices();
        Close(choices);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
