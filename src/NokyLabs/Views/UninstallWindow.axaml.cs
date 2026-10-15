using Avalonia.Controls;
using NokyLabs.ViewModels;

namespace NokyLabs.Views;

public partial class UninstallWindow : Window
{
    public UninstallWindow()
    {
        InitializeComponent();

        // Enquanto remove, não deixa fechar no meio
        Closing += (_, e) =>
        {
            if (DataContext is UninstallViewModel { IsWorkingStep: true }) e.Cancel = true;
        };
    }
}
