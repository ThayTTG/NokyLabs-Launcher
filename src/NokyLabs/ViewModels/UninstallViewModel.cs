using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NokyLabs.Services;

namespace NokyLabs.ViewModels;

/// <summary>
/// Desinstalador do NokyLabs. Abre pelo botão "Desinstalar" de Aplicativos instalados do Windows
/// (NokyLabs.exe --uninstall) ou pelas Configurações do launcher.
/// </summary>
public partial class UninstallViewModel : ObservableObject
{
    public UninstallViewModel()
    {
        var info = SetupService.LoadInstallInfo();
        InstallFolderText = info is null
            ? "Instalação não encontrada: só as configurações e os atalhos serão verificados."
            : $"Instalado em {info.InstallFolder}";

        var settings = SettingsService.Load(AppPaths.InstalledConfigFolder);
        var appsFolder = string.IsNullOrWhiteSpace(settings.SourceFolder)
            ? AppPaths.DefaultSourceFolderFor(false, AppPaths.ExeFolder)
            : settings.SourceFolder;
        var count = InstallService.FindManagedAppFolders(appsFolder).Count();
        AppsText = count switch
        {
            0 => "Nenhum app instalado pelo NokyLabs foi encontrado.",
            1 => $"1 app instalado em {appsFolder}",
            _ => $"{count} apps instalados em {appsFolder}"
        };
    }

    /// <summary>Disparado ao fechar: true se desinstalou.</summary>
    public event EventHandler<bool>? Finished;

    public bool IsSimulation => SetupService.IsSimulation;
    public string SimulationText => "Modo de demonstração: rodando pelo Visual Studio, nada é removido de verdade.";
    public string InstallFolderText { get; }
    public string AppsText { get; }

    [ObservableProperty] private bool _removeApps;
    [ObservableProperty] private bool _removeData = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmStep), nameof(IsWorkingStep), nameof(IsDoneStep))]
    private int _step;   // 0 confirmar · 1 removendo · 2 concluído

    public bool IsConfirmStep => Step == 0;
    public bool IsWorkingStep => Step == 1;
    public bool IsDoneStep => Step == 2;

    [ObservableProperty] private double _progress;
    public ObservableCollection<SetupProgress> LogLines { get; } = new();

    [RelayCommand]
    private void Cancel() => Finished?.Invoke(this, false);

    [RelayCommand]
    private async Task UninstallAsync()
    {
        Step = 1;
        var progress = new Progress<SetupProgress>(p =>
        {
            if (p.Percent >= 0) Progress = p.Percent;
            if (p.Message.Length > 0) LogLines.Add(p);
        });

        try
        {
            await SetupService.UninstallAsync(RemoveApps, RemoveData, progress);
        }
        catch (Exception ex)
        {
            LogLines.Add(new SetupProgress($"Algo deu errado: {ex.Message}", -1, true));
        }

        Step = 2;
    }

    [RelayCommand]
    private void Close() => Finished?.Invoke(this, true);
}
