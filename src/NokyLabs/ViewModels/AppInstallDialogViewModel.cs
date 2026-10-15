using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NokyLabs.Models;
using NokyLabs.Services;

namespace NokyLabs.ViewModels;

/// <summary>Pedido para mostrar a janela "Instalar app".</summary>
public sealed record AppInstallRequest(AppCardViewModel App, AppInstallChoices Defaults, string Destination);

/// <summary>Janela de opções mostrada antes de instalar um app pelo launcher.</summary>
public partial class AppInstallDialogViewModel : ObservableObject
{
    public AppInstallDialogViewModel(AppInstallRequest request)
    {
        var app = request.App;
        Name = app.Name;
        Description = app.Description;
        VersionText = $"Versão {app.LatestVersion}";
        AccentBrush = app.AccentBrush;
        AccentSoftBrush = app.AccentSoftBrush;
        IconGeometry = app.IconGeometry;
        Destination = AppPaths.AppFolder(request.Destination, app.Id);
        FreeSpace = SetupService.FreeSpaceText(request.Destination);

        _desktopShortcut = request.Defaults.DesktopShortcut;
        _menuShortcut = request.Defaults.MenuShortcut;
        _openAfterInstall = request.Defaults.OpenAfterInstall;
    }

    public string Name { get; }
    public string Description { get; }
    public string VersionText { get; }
    public IBrush AccentBrush { get; }
    public IBrush AccentSoftBrush { get; }
    public Geometry IconGeometry { get; }
    public string Destination { get; }
    public string FreeSpace { get; }
    public string MenuText => $"Adicionar ao {ShortcutService.MenuLabel}";
    public bool CanCreateShortcuts => ShortcutService.IsSupported;

    [ObservableProperty] private bool _desktopShortcut;
    [ObservableProperty] private bool _menuShortcut;
    [ObservableProperty] private bool _openAfterInstall;
    [ObservableProperty] private bool _rememberChoices;

    public AppInstallChoices ToChoices() =>
        new(DesktopShortcut, MenuShortcut, OpenAfterInstall, RememberChoices);
}
