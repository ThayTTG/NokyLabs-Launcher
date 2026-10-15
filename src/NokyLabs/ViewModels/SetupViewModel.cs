using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NokyLabs.Models;
using NokyLabs.Services;

namespace NokyLabs.ViewModels;

/// <summary>Como o assistente terminou.</summary>
public enum SetupOutcome
{
    Cancelled,      // fechou sem instalar
    OpenInstalled,  // abrir a cópia instalada (em outra pasta)
    OpenHere,       // continuar neste mesmo processo (portátil, simulação ou já é a cópia instalada)
    Quit            // terminou, mas a pessoa não quis abrir agora
}

public sealed record SetupResult(SetupOutcome Outcome, string? ExePath = null);

/// <summary>Uma etapa na trilha lateral do assistente.</summary>
public partial class SetupStepItem : ObservableObject
{
    public SetupStepItem(int number, string title)
    {
        Number = number;
        Title = title;
    }

    public int Number { get; }
    public string Title { get; }

    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isDone;
}

/// <summary>Um app que pode ser instalado junto com o launcher.</summary>
public partial class SetupAppItem : ObservableObject
{
    public SetupAppItem(CatalogApp app)
    {
        App = app;
        AccentBrush = IconHelper.AccentBrush(app.Accent);
        AccentSoftBrush = IconHelper.AccentBrush(app.Accent, 0.16);
        IconGeometry = IconHelper.ParseIcon(app.Icon);

        app.Downloads.TryGetValue(PlatformInfo.Key, out var download);
        IsAvailable = download is { IsPlaceholder: false };
        Note = download is null
            ? $"Ainda sem versão para {PlatformInfo.DisplayName}"
            : download.IsPlaceholder
                ? "Download ainda não publicado"
                : $"v{app.Version}";
    }

    public CatalogApp App { get; }
    public string Name => App.Name;
    public string Description => App.Description;
    public IBrush AccentBrush { get; }
    public IBrush AccentSoftBrush { get; }
    public Geometry IconGeometry { get; }
    public bool IsAvailable { get; }
    public string Note { get; }

    [ObservableProperty] private bool _isSelected;
}

/// <summary>
/// O assistente de instalação do NokyLabs.
/// Etapas: 0 Boas-vindas · 1 Local · 2 Integração · 3 Apps · 4 Preferências · 5 Resumo · 6 Instalando · 7 Concluído
/// </summary>
public partial class SetupViewModel : ObservableObject
{
    private const int StepWelcome = 0, StepLocation = 1, StepIntegration = 2, StepApps = 3,
                      StepPreferences = 4, StepSummary = 5, StepInstalling = 6, StepDone = 7;

    private string _resultExe = string.Empty;

    public SetupViewModel()
    {
        IsSimulation = SetupService.IsSimulation;

        // Pastas sugeridas
        _installFolder = AppPaths.DefaultInstallFolder;
        _appsFolder = AppPaths.DefaultSourceFolderFor(portable: false, AppPaths.ExeFolder);

        // Aproveita as preferências que já existiam (reinstalação ou atualização)
        var previous = SettingsService.Load(AppPaths.InstalledConfigFolder);
        if (!string.IsNullOrWhiteSpace(previous.SourceFolder)) _appsFolder = previous.SourceFolder;
        _catalogUrl = previous.CatalogUrl;
        _checkUpdatesOnStartup = previous.CheckUpdatesOnStartup;
        _periodicCheck = previous.PeriodicCheck;
        _showNotifications = previous.ShowNotifications;
        _autoUpdateApps = previous.AutoUpdateApps;
        _autoUpdateLauncher = previous.AutoUpdateLauncher;
        _minimizeToTray = previous.MinimizeToTray || !File.Exists(Path.Combine(AppPaths.InstalledConfigFolder, "settings.json"));
        _appDesktopShortcuts = previous.AppDesktopShortcut;
        _appMenuShortcuts = previous.AppMenuShortcut;
        _startWithSystem = StartupService.IsEnabled();

        var existing = SetupService.LoadInstallInfo();
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.InstallFolder))
            _installFolder = existing.InstallFolder;
        AlreadyInstalledText = existing is null
            ? string.Empty
            : $"O NokyLabs v{existing.Version} já está instalado em {existing.InstallFolder}. Continuar vai reinstalar ou atualizar essa cópia.";

        HasPreviousData = SetupService.HasPreviousData(_appsFolder);

        // Apps do catálogo (embutido ou a última cópia online)
        foreach (var app in CatalogService.LoadLocal(previous.CatalogUrl).Catalog.Apps.Where(a => !a.ComingSoon))
            Apps.Add(new SetupAppItem(app));

        Steps = new ObservableCollection<SetupStepItem>
        {
            new(1, "Boas-vindas"), new(2, "Local"), new(3, "Integração"), new(4, "Apps"),
            new(5, "Preferências"), new(6, "Instalação"), new(7, "Concluído")
        };
        UpdateStepState();
    }

    /// <summary>Disparado quando o assistente termina (ou é cancelado).</summary>
    public event EventHandler<SetupResult>? Finished;

    public bool IsSimulation { get; }
    public string SimulationText => "Modo de demonstração: rodando pelo Visual Studio, o assistente mostra tudo mas não altera nada no computador.";
    public string AlreadyInstalledText { get; }
    public bool IsAlreadyInstalled => AlreadyInstalledText.Length > 0;
    public string VersionText => $"v{SelfUpdateService.CurrentVersion}";
    public string PlatformName => PlatformInfo.DisplayName;
    public string MenuLabel => ShortcutService.MenuLabel;
    public bool CanRegisterInSystem => SystemRegistrationService.IsSupported;
    public bool CanStartWithSystem => StartupService.IsSupported;

    public ObservableCollection<SetupStepItem> Steps { get; }
    public ObservableCollection<SetupAppItem> Apps { get; } = new();
    public ObservableCollection<string> SummaryLines { get; } = new();
    public ObservableCollection<SetupProgress> LogLines { get; } = new();

    // ------------------------------------------------------------------ navegação

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcomeStep), nameof(IsLocationStep), nameof(IsIntegrationStep),
        nameof(IsAppsStep), nameof(IsPreferencesStep), nameof(IsSummaryStep), nameof(IsInstallingStep),
        nameof(IsDoneStep), nameof(ShowBack), nameof(ShowCancel), nameof(NextText), nameof(StepHeader))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(BackCommand), nameof(CancelCommand))]
    private int _step;

    public bool IsWelcomeStep => Step == StepWelcome;
    public bool IsLocationStep => Step == StepLocation;
    public bool IsIntegrationStep => Step == StepIntegration;
    public bool IsAppsStep => Step == StepApps;
    public bool IsPreferencesStep => Step == StepPreferences;
    public bool IsSummaryStep => Step == StepSummary;
    public bool IsInstallingStep => Step == StepInstalling;
    public bool IsDoneStep => Step == StepDone;

    public bool ShowBack => Step is > StepWelcome and < StepInstalling;
    public bool ShowCancel => Step < StepInstalling;

    public string NextText => Step switch
    {
        StepSummary when IsPortable => "Concluir configuração",
        StepSummary => "Instalar",
        StepDone => "Concluir",
        _ => "Próximo"
    };

    public string StepHeader => $"Etapa {Math.Min(Step, StepInstalling - 1) + 1} de 6";

    [ObservableProperty] private string _errorText = string.Empty;

    partial void OnStepChanged(int value)
    {
        ErrorText = string.Empty;
        UpdateStepState();
        if (value == StepSummary) BuildSummary();
    }

    private void UpdateStepState()
    {
        // Resumo e "Instalando" dividem a mesma bolinha ("Instalação")
        var railIndex = Step switch
        {
            <= StepPreferences => Step,
            StepSummary or StepInstalling => 5,
            _ => 6
        };

        for (var i = 0; i < Steps.Count; i++)
        {
            Steps[i].IsCurrent = i == railIndex;
            Steps[i].IsDone = i < railIndex || (Step == StepDone && i == 6);
        }
    }

    private bool CanGoNext() => !IsWorking;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        switch (Step)
        {
            case StepLocation when !ValidateLocation():
                return;

            case StepIntegration when IsPortable:
                Step = StepApps;   // no portátil a integração é só a bandeja; segue normalmente
                return;

            case StepSummary:
                await RunInstallAsync();
                return;

            case StepDone:
                Finish();
                return;

            default:
                Step++;
                return;
        }
    }

    private bool CanGoBack() => ShowBack && !IsWorking;

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back() => Step--;

    private bool CanCancel() => !IsWorking;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => Finished?.Invoke(this, new SetupResult(SetupOutcome.Cancelled));

    // ------------------------------------------------------------------ 0. modo

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPortable), nameof(NextText), nameof(LocationTitle), nameof(AppsFolderHint))]
    private bool _isInstallMode = true;

    public bool IsPortable => !IsInstallMode;

    /// <summary>Usado pelo RadioButton do modo portátil (o par do IsInstallMode).</summary>
    public bool IsPortableMode
    {
        get => !IsInstallMode;
        set => IsInstallMode = !value;
    }

    partial void OnIsInstallModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPortableMode));

        // Sugere a pasta de apps de cada modo (se a pessoa ainda não escolheu outra)
        var installDefault = AppPaths.DefaultSourceFolderFor(false, AppPaths.ExeFolder);
        var portableDefault = AppPaths.DefaultSourceFolderFor(true, AppPaths.ExeFolder);
        if (value && AppPaths.SamePath(AppsFolder, portableDefault)) AppsFolder = installDefault;
        if (!value && AppPaths.SamePath(AppsFolder, installDefault)) AppsFolder = portableDefault;
    }

    // ------------------------------------------------------------------ 1. local

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallFreeSpace))]
    private string _installFolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppsFreeSpace))]
    private string _appsFolder;

    public string InstallFreeSpace => SetupService.FreeSpaceText(InstallFolder);
    public string AppsFreeSpace => SetupService.FreeSpaceText(AppsFolder);
    public string PortableFolderText => $"O NokyLabs vai rodar daqui mesmo: {AppPaths.ExeFolder}";

    public string LocationTitle => IsInstallMode ? "Onde instalar" : "Onde guardar os apps";
    public string AppsFolderHint => IsInstallMode
        ? "Cada utilitário ganha uma subpasta aqui."
        : "No modo portátil, o ideal é deixar ao lado do launcher (funciona mesmo se a letra do pendrive mudar).";

    public bool HasPreviousData { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeepPreviousData))]
    private bool _cleanInstall;

    /// <summary>Par do CleanInstall, para os dois RadioButtons.</summary>
    public bool KeepPreviousData
    {
        get => !CleanInstall;
        set => CleanInstall = !value;
    }

    private bool ValidateLocation()
    {
        static bool LooksValid(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return false;
            var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = (Path.GetPathRoot(full) ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return full.Length > root.Length;   // não aceita a raiz do disco (C:\ ou /)
        }

        if (IsInstallMode && !LooksValid(InstallFolder))
        {
            ErrorText = "Escolha uma pasta completa para o NokyLabs (não pode ser a raiz do disco).";
            return false;
        }

        if (!LooksValid(AppsFolder))
        {
            ErrorText = "Escolha uma pasta completa para os apps (não pode ser a raiz do disco).";
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ 2. integração

    [ObservableProperty] private bool _desktopShortcut = true;
    [ObservableProperty] private bool _menuShortcut = true;
    [ObservableProperty] private bool _registerInSystem = true;
    [ObservableProperty] private bool _startWithSystem;
    [ObservableProperty] private bool _minimizeToTray;

    // ------------------------------------------------------------------ 3. apps

    [ObservableProperty] private bool _appDesktopShortcuts;
    [ObservableProperty] private bool _appMenuShortcuts = true;

    public bool HasAvailableApps => Apps.Any(a => a.IsAvailable);
    public bool HasNoAvailableApps => !HasAvailableApps;

    // ------------------------------------------------------------------ 4. preferências

    [ObservableProperty] private string _catalogUrl = string.Empty;
    [ObservableProperty] private bool _checkUpdatesOnStartup = true;
    [ObservableProperty] private bool _periodicCheck = true;
    [ObservableProperty] private bool _showNotifications = true;
    [ObservableProperty] private bool _autoUpdateApps;
    [ObservableProperty] private bool _autoUpdateLauncher;

    // ------------------------------------------------------------------ 5. resumo

    private void BuildSummary()
    {
        SummaryLines.Clear();

        if (IsInstallMode)
        {
            SummaryLines.Add($"Instalar o NokyLabs em {InstallFolder}");
            if (CleanInstall) SummaryLines.Add("Instalação limpa: apagar configurações, cache e apps da instalação anterior");
            if (DesktopShortcut) SummaryLines.Add("Criar atalho na Área de Trabalho");
            if (MenuShortcut) SummaryLines.Add($"Adicionar ao {MenuLabel}");
            if (RegisterInSystem && CanRegisterInSystem) SummaryLines.Add("Aparecer em Aplicativos instalados");
            SummaryLines.Add(StartWithSystem ? "Iniciar junto com o sistema" : "Não iniciar com o sistema");
        }
        else
        {
            SummaryLines.Add($"Usar em modo portátil a partir de {AppPaths.ExeFolder}");
            if (CleanInstall) SummaryLines.Add("Instalação limpa: apagar configurações, cache e apps da instalação anterior");
        }

        SummaryLines.Add($"Guardar os apps em {AppsFolder}");
        if (MinimizeToTray) SummaryLines.Add("Ficar na bandeja ao fechar");

        var chosen = Apps.Where(a => a.IsSelected && a.IsAvailable).Select(a => a.Name).ToList();
        SummaryLines.Add(chosen.Count == 0 ? "Nenhum app agora (dá para instalar depois)" : $"Instalar agora: {string.Join(", ", chosen)}");

        var auto = new List<string>();
        if (AutoUpdateApps) auto.Add("apps");
        if (AutoUpdateLauncher) auto.Add("launcher");
        SummaryLines.Add(auto.Count == 0 ? "Atualizações: você decide quando instalar" : $"Atualizações automáticas: {string.Join(" e ", auto)}");
    }

    // ------------------------------------------------------------------ 6. instalando

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(BackCommand), nameof(CancelCommand))]
    private bool _isWorking;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _currentAction = string.Empty;

    private async Task RunInstallAsync()
    {
        Step = StepInstalling;
        IsWorking = true;
        LogLines.Clear();

        var plan = new SetupPlan
        {
            Portable = IsPortable,
            InstallFolder = InstallFolder.Trim(),
            AppsFolder = AppsFolder.Trim(),
            CleanInstall = HasPreviousData && CleanInstall,
            DesktopShortcut = IsInstallMode && DesktopShortcut,
            MenuShortcut = IsInstallMode && MenuShortcut,
            RegisterInSystem = IsInstallMode && RegisterInSystem,
            StartWithSystem = IsInstallMode && StartWithSystem,
            InitialApps = Apps.Where(a => a.IsSelected && a.IsAvailable).Select(a => a.App).ToList(),
            AppDesktopShortcuts = AppDesktopShortcuts,
            AppMenuShortcuts = AppMenuShortcuts,
            Settings = new LauncherSettings
            {
                CatalogUrl = CatalogUrl.Trim(),
                CheckUpdatesOnStartup = CheckUpdatesOnStartup,
                PeriodicCheck = PeriodicCheck,
                MinimizeToTray = MinimizeToTray,
                ShowNotifications = ShowNotifications,
                AutoUpdateApps = AutoUpdateApps,
                AutoUpdateLauncher = AutoUpdateLauncher,
                AskBeforeInstall = true,
                AppDesktopShortcut = AppDesktopShortcuts,
                AppMenuShortcut = AppMenuShortcuts
            }
        };

        var progress = new Progress<SetupProgress>(p =>
        {
            if (p.Percent >= 0) Progress = p.Percent;
            if (p.Message.Length == 0) return;
            CurrentAction = p.Message;
            LogLines.Add(p);
        });

        try
        {
            _resultExe = await SetupService.InstallAsync(plan, progress);
            HasFailed = false;
            DoneTitle = IsSimulation ? "Demonstração concluída" : IsPortable ? "Modo portátil pronto!" : "Tudo pronto!";
            DoneText = IsSimulation
                ? "Nada foi alterado no computador. Na versão publicada, estes passos acontecem de verdade."
                : IsPortable
                    ? "O NokyLabs vai guardar tudo ao lado dele. Pode levar a pasta para onde quiser."
                    : "O NokyLabs foi instalado. Os atalhos e as preferências já estão no lugar.";
            Step = StepDone;
        }
        catch (Exception ex)
        {
            HasFailed = true;
            ErrorText = ex is LauncherException ? ex.Message : $"Algo deu errado: {ex.Message}";
            LogLines.Add(new SetupProgress(ErrorText, -1, true));
            Step = StepSummary;   // volta para o resumo, para a pessoa poder tentar de novo
            ErrorText = LogLines[^1].Message;
        }
        finally
        {
            IsWorking = false;
            OnPropertyChanged(nameof(HasWarnings));
        }
    }

    // ------------------------------------------------------------------ 7. concluído

    [ObservableProperty] private bool _hasFailed;
    [ObservableProperty] private string _doneTitle = string.Empty;
    [ObservableProperty] private string _doneText = string.Empty;
    [ObservableProperty] private bool _openAfterFinish = true;

    public bool HasWarnings => LogLines.Any(l => l.IsWarning);

    private void Finish()
    {
        if (!OpenAfterFinish)
        {
            Finished?.Invoke(this, new SetupResult(SetupOutcome.Quit));
            return;
        }

        // Simulação, portátil ou já é a cópia instalada: segue aqui mesmo
        if (IsSimulation || IsPortable || AppPaths.SamePath(_resultExe, Environment.ProcessPath))
        {
            Finished?.Invoke(this, new SetupResult(SetupOutcome.OpenHere));
            return;
        }

        Finished?.Invoke(this, new SetupResult(SetupOutcome.OpenInstalled, _resultExe));
    }
}
