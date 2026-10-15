using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NokyLabs.Models;
using NokyLabs.Services;

namespace NokyLabs.ViewModels;

/// <summary>
/// Representa um card de app na tela (Biblioteca e Atualizações).
/// </summary>
public partial class AppCardViewModel : ObservableObject
{
    private static readonly IBrush SuccessBrush = new ImmutableSolidColorBrush(Color.Parse("#34D399"));
    private static readonly IBrush SuccessSoftBrush = new ImmutableSolidColorBrush(Color.Parse("#34D399"), 0.14);
    private static readonly IBrush WarningBrush = new ImmutableSolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush WarningSoftBrush = new ImmutableSolidColorBrush(Color.Parse("#FBBF24"), 0.14);
    private static readonly IBrush PrimaryLightBrush = new ImmutableSolidColorBrush(Color.Parse("#C4B5FD"));
    private static readonly IBrush PrimarySoftBrush = new ImmutableSolidColorBrush(Color.Parse("#8B5CF6"), 0.16);
    private static readonly IBrush MutedBrush = new ImmutableSolidColorBrush(Color.Parse("#A99FC7"));
    private static readonly IBrush MutedSoftBrush = new ImmutableSolidColorBrush(Color.Parse("#A99FC7"), 0.12);
    private static readonly IBrush InfoBrush = new ImmutableSolidColorBrush(Color.Parse("#7DD3FC"));
    private static readonly IBrush InfoSoftBrush = new ImmutableSolidColorBrush(Color.Parse("#7DD3FC"), 0.14);

    private static readonly Geometry FallbackIcon = Geometry.Parse("M4,4 H20 V20 H4 Z");

    private readonly Func<string> _getSourceFolder;
    private readonly Action<string> _report;
    private readonly Action _onChanged;
    private readonly Func<AppCardViewModel, Task<AppInstallChoices?>> _askInstallChoices;

    /// <param name="askInstallChoices">Pergunta as opções de instalação (atalhos etc.); null = cancelou.</param>
    public AppCardViewModel(CatalogApp app, Func<string> getSourceFolder, Action<string> report, Action onChanged,
                            Func<AppCardViewModel, Task<AppInstallChoices?>> askInstallChoices)
    {
        App = app;
        _getSourceFolder = getSourceFolder;
        _report = report;
        _onChanged = onChanged;
        _askInstallChoices = askInstallChoices;

        Download = app.Downloads.TryGetValue(PlatformInfo.Key, out var dl) ? dl : null;

        var accent = TryParseColor(app.Accent) ?? Color.Parse("#A99FC7");
        AccentBrush = new ImmutableSolidColorBrush(accent);
        AccentSoftBrush = new ImmutableSolidColorBrush(accent, 0.16);
        IconGeometry = TryParseGeometry(app.Icon) ?? FallbackIcon;

        Rescan();
    }

    public CatalogApp App { get; }

    /// <summary>Download para o sistema atual (null = não existe versão para ele).</summary>
    public CatalogDownload? Download { get; }

    public string Id => App.Id;
    public string Name => App.Name;
    public string Description => App.Description;
    public string LatestVersion => App.Version;
    public IBrush AccentBrush { get; }
    public IBrush AccentSoftBrush { get; }
    public Geometry IconGeometry { get; }

    /// <summary>Manifesto do que está instalado (null = não instalado).</summary>
    [ObservableProperty]
    private InstallManifest? _installed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrimaryActionCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(AskUninstallCommand))]
    private bool _isBusy;

    /// <summary>True enquanto o card mostra "Remover este app?" com Cancelar/Desinstalar.</summary>
    [ObservableProperty]
    private bool _isConfirmingUninstall;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    /// <summary>O app está aberto neste momento (verificado de tempos em tempos pelo launcher).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunningAction), nameof(IsOpenAction), nameof(IsUpdateAction), nameof(ActionText), nameof(StatusText), nameof(StatusBrush), nameof(StatusSoftBrush), nameof(RunningTip))]
    [NotifyCanExecuteChangedFor(nameof(AskUninstallCommand))]
    private bool _isRunning;

    /// <summary>True enquanto o launcher está encerrando o app.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionText))]
    [NotifyCanExecuteChangedFor(nameof(PrimaryActionCommand))]
    private bool _isStopping;

    // ---------- Estado derivado ----------

    public string? InstalledVersion => Installed?.Version;

    public AppStatus Status =>
        App.ComingSoon ? AppStatus.ComingSoon
        : !string.IsNullOrWhiteSpace(App.Web) ? AppStatus.Web
        : Installed is not null
            ? (VersionHelper.IsNewer(LatestVersion, InstalledVersion) ? AppStatus.UpdateAvailable : AppStatus.Installed)
        : Download is null ? AppStatus.Unsupported
        : AppStatus.NotInstalled;

    public bool IsComingSoon => Status == AppStatus.ComingSoon;
    public bool IsUnsupported => Status == AppStatus.Unsupported;
    public bool IsWeb => Status == AppStatus.Web;
    public bool IsInstalled => Status is AppStatus.Installed or AppStatus.UpdateAvailable;
    public bool HasUpdate => Status == AppStatus.UpdateAvailable;

    /// <summary>Novidades da versão mais recente, prontas para a tela.</summary>
    public string NotesText => NotesHelper.Format(App.Notes);
    public string NotesTitle => $"Novidades da v{LatestVersion}";
    public bool HasNotes => !string.IsNullOrWhiteSpace(App.Notes) && !IsComingSoon && !IsWeb;

    /// <summary>O link deste sistema ainda é um placeholder.</summary>
    public bool IsDownloadPending => Download is null || Download.IsPlaceholder;

    public string StatusText => IsRunning && IsInstalled ? "Rodando" : Status switch
    {
        AppStatus.Web => "No navegador",
        AppStatus.Installed => "Instalado",
        AppStatus.UpdateAvailable => "Atualização",
        AppStatus.NotInstalled => "Disponível",
        AppStatus.Unsupported => "Indisponível",
        _ => "Em breve"
    };

    public IBrush StatusBrush => IsRunning && IsInstalled ? SuccessBrush : Status switch
    {
        AppStatus.Web => InfoBrush,
        AppStatus.Installed => SuccessBrush,
        AppStatus.UpdateAvailable => WarningBrush,
        AppStatus.NotInstalled => PrimaryLightBrush,
        _ => MutedBrush
    };

    public IBrush StatusSoftBrush => IsRunning && IsInstalled ? SuccessSoftBrush : Status switch
    {
        AppStatus.Web => InfoSoftBrush,
        AppStatus.Installed => SuccessSoftBrush,
        AppStatus.UpdateAvailable => WarningSoftBrush,
        AppStatus.NotInstalled => PrimarySoftBrush,
        _ => MutedSoftBrush
    };

    public string VersionText => Status switch
    {
        AppStatus.Web => "Roda direto no navegador, sem instalar",
        AppStatus.Installed when InstalledVersion == VersionHelper.Unknown => "Instalado · versão desconhecida",
        AppStatus.Installed => $"v{InstalledVersion} · em dia",
        AppStatus.UpdateAvailable => $"v{InstalledVersion}  →  v{LatestVersion}",
        AppStatus.NotInstalled when IsDownloadPending => $"v{LatestVersion} · download ainda não publicado",
        AppStatus.NotInstalled => $"v{LatestVersion} disponível",
        AppStatus.Unsupported => $"Ainda sem versão para {PlatformInfo.DisplayName}",
        _ => "Em desenvolvimento"
    };

    public string ActionText => IsBusy
        ? "Aguarde…"
        : IsStopping ? "Encerrando…"
        : IsRunningAction ? "Rodando"
        : Status switch
        {
            AppStatus.Web => string.IsNullOrWhiteSpace(App.WebAction) ? "Abrir no navegador" : App.WebAction!,
            AppStatus.Installed => "Abrir",
            AppStatus.UpdateAvailable when IsDownloadPending => "Atualização em breve",
            AppStatus.UpdateAvailable => "Atualizar",
            AppStatus.NotInstalled when IsDownloadPending => "Download em breve",
            AppStatus.NotInstalled => "Instalar",
            AppStatus.Unsupported => $"Indisponível no {PlatformInfo.DisplayName}",
            _ => "Em breve"
        };

    // Qual "cor" de botão usar (cada uma liga uma classe no XAML)
    /// <summary>Instalado e aberto: o botão mostra "Rodando" e, com o mouse por cima, "Encerrar".</summary>
    public bool IsRunningAction => IsRunning && IsInstalled;

    /// <summary>Dica ao passar o mouse sobre o botão de um app aberto.</summary>
    public string? RunningTip => IsRunningAction ? $"O {Name} está aberto. Clique para encerrar." : null;
    public bool IsOpenAction => !IsRunningAction && Status is AppStatus.Installed or AppStatus.Web;
    public bool IsUpdateAction => !IsRunningAction && Status == AppStatus.UpdateAvailable;
    public bool IsInstallAction => Status is AppStatus.NotInstalled or AppStatus.Unsupported or AppStatus.ComingSoon;

    partial void OnInstalledChanged(InstallManifest? value) => RaiseStateChanged();

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ActionText));

    private void RaiseStateChanged()
    {
        foreach (var name in new[]
                 {
                     nameof(InstalledVersion), nameof(Status), nameof(IsComingSoon), nameof(IsUnsupported), nameof(IsWeb),
                     nameof(IsRunningAction),
                     nameof(IsInstalled), nameof(HasUpdate), nameof(StatusText), nameof(StatusBrush),
                     nameof(StatusSoftBrush), nameof(VersionText), nameof(ActionText),
                     nameof(IsOpenAction), nameof(IsUpdateAction), nameof(IsInstallAction),
                     nameof(HasNotes), nameof(NotesTitle)
                 })
        {
            OnPropertyChanged(name);
        }

        PrimaryActionCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
        AskUninstallCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Relê a pasta de origem para saber se o app está instalado.</summary>
    public void Rescan()
    {
        if (IsBusy) return;
        Installed = App.ComingSoon || IsWebApp ? null : InstallService.Detect(_getSourceFolder(), App);
        RefreshRunning();
    }

    private bool IsWebApp => !string.IsNullOrWhiteSpace(App.Web);

    /// <summary>Confere se o app está aberto (chamado de tempos em tempos pela tela principal).</summary>
    public void RefreshRunning()
    {
        if (IsStopping) return;
        IsRunning = Installed is not null &&
                    ProcessService.IsRunning(AppPaths.AppFolder(_getSourceFolder(), Id), Installed.Executable);
    }

    // ---------- Ações ----------

    private bool CanRunAction() =>
        !IsBusy && !IsStopping && Status switch
        {
            AppStatus.Web => true,
            AppStatus.Installed => true,
            AppStatus.UpdateAvailable or AppStatus.NotInstalled => !IsDownloadPending,
            _ => false
        };

    [RelayCommand(CanExecute = nameof(CanRunAction))]
    private async Task PrimaryActionAsync()
    {
        // Rodando: o mesmo botão vira "Encerrar"
        if (IsRunningAction)
        {
            await StopAsync();
            return;
        }

        switch (Status)
        {
            case AppStatus.Web:
                OpenWeb();
                break;

            case AppStatus.Installed:
                Open();
                break;

            case AppStatus.UpdateAvailable:
                await UpdateAsync();
                break;

            case AppStatus.NotInstalled:
                await InstallAsync();
                break;
        }
    }

    private bool CanOpenFolder() => IsInstalled && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        try
        {
            LaunchService.OpenFolder(AppPaths.AppFolder(_getSourceFolder(), Id));
        }
        catch (LauncherException ex)
        {
            _report(ex.Message);
        }
    }

    // ---------- Desinstalar (em dois passos, para evitar clique acidental) ----------

    private bool CanAskUninstall() => IsInstalled && !IsBusy && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanAskUninstall))]
    private void AskUninstall() => IsConfirmingUninstall = true;

    [RelayCommand]
    private void CancelUninstall() => IsConfirmingUninstall = false;

    [RelayCommand]
    private async Task ConfirmUninstallAsync()
    {
        IsConfirmingUninstall = false;
        if (IsBusy || !IsInstalled) return;

        IsBusy = true;
        IsProgressIndeterminate = true;
        _report($"Desinstalando {Name}…");

        try
        {
            await InstallService.UninstallAsync(App, _getSourceFolder());
            IsBusy = false;
            Installed = InstallService.Detect(_getSourceFolder(), App);
            _report($"{Name} foi desinstalado.");
        }
        catch (LauncherException ex)
        {
            _report(ex.Message);
        }
        catch (Exception ex)
        {
            _report($"Não consegui desinstalar o {Name}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            _onChanged();
        }
    }

    private void Open()
    {
        if (Installed is null) return;

        try
        {
            LaunchService.OpenApp(AppPaths.AppFolder(_getSourceFolder(), Id), Installed.Executable, Name);
            _report($"{Name} aberto.");
            IsRunning = true;   // mostra "Rodando" na hora; a próxima verificação confirma
        }
        catch (LauncherException ex)
        {
            _report(ex.Message);
        }
    }

    private void OpenWeb()
    {
        try
        {
            LaunchService.OpenUrl(App.Web!);
            _report($"{Name} aberto no navegador.");
        }
        catch (LauncherException ex)
        {
            _report(ex.Message);
        }
    }

    /// <summary>Encerra o app: pede para fechar e, se não fechar em 5 s, força.</summary>
    private async Task StopAsync()
    {
        if (Installed is null || IsStopping) return;

        IsStopping = true;
        _report($"Encerrando o {Name}…");
        try
        {
            var stopped = await ProcessService.StopAsync(
                AppPaths.AppFolder(_getSourceFolder(), Id), Installed.Executable, TimeSpan.FromSeconds(5));
            _report(stopped ? $"{Name} foi encerrado." : $"Não consegui encerrar o {Name}. Tente fechá-lo pelo Gerenciador de Tarefas.");
        }
        catch (Exception ex)
        {
            _report($"Não consegui encerrar o {Name}: {ex.Message}");
        }
        finally
        {
            IsStopping = false;
            RefreshRunning();
            _onChanged();
        }
    }

    public Task InstallAsync() => RunInstallAsync(isUpdate: false);

    public Task UpdateAsync() => HasUpdate ? RunInstallAsync(isUpdate: true) : Task.CompletedTask;

    private async Task RunInstallAsync(bool isUpdate)
    {
        if (IsBusy) return;

        if (Download is null || Download.IsPlaceholder)
        {
            _report($"O download do {Name} ainda não foi publicado. Quando estiver no GitHub, é só trocar o link no catalogo.json.");
            return;
        }

        // Numa instalação nova, pergunta as opções (atalhos, abrir depois...). Atualizações não perguntam nada.
        AppInstallChoices? choices = null;
        if (!isUpdate)
        {
            choices = await _askInstallChoices(this);
            if (choices is null)
            {
                _report($"Instalação do {Name} cancelada.");
                return;
            }
        }

        IsBusy = true;
        Progress = 0;
        IsProgressIndeterminate = false;
        _report(isUpdate ? $"Atualizando {Name}…" : $"Instalando {Name}…");

        // Progress<T> devolve cada atualização para a thread da interface
        var progress = new Progress<double>(value =>
        {
            IsProgressIndeterminate = value < 0;
            if (value >= 0) Progress = value;
        });

        try
        {
            var manifest = await InstallService.InstallAsync(App, Download, _getSourceFolder(), progress);
            var shortcutWarning = string.Empty;

            if (choices is not null)
            {
                try
                {
                    manifest = InstallService.CreateShortcuts(App, _getSourceFolder(), manifest,
                                                              choices.DesktopShortcut, choices.MenuShortcut);
                }
                catch (Exception ex)
                {
                    shortcutWarning = $" (mas não consegui criar os atalhos: {ex.Message})";
                }
            }

            IsBusy = false;
            Installed = manifest;
            _report(isUpdate
                ? $"{Name} atualizado para a v{manifest.Version}."
                : $"{Name} foi instalado com sucesso{shortcutWarning}.");

            if (choices is { OpenAfterInstall: true }) Open();
        }
        catch (LauncherException ex)
        {
            _report(ex.Message);
        }
        catch (Exception ex)
        {
            _report($"Algo deu errado com o {Name}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            _onChanged();
        }
    }

    // ---------- Auxiliares ----------

    private static Color? TryParseColor(string? hex) =>
        !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex, out var color) ? color : null;

    private static Geometry? TryParseGeometry(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        try { return Geometry.Parse(data); }
        catch (Exception) { return null; }
    }
}
