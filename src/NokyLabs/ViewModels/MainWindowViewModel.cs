using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NokyLabs.Models;
using NokyLabs.Services;

namespace NokyLabs.ViewModels;

/// <summary>Um aviso para mostrar no canto da tela (quem exibe é o App.axaml.cs).</summary>
public sealed record NotificationRequest(string Title, string Message);

public partial class MainWindowViewModel : ObservableObject
{
    private readonly List<AppCardViewModel> _allApps = new();
    private bool _loading = true;   // evita salvar o settings.json enquanto ele ainda está sendo lido

    public MainWindowViewModel()
    {
        // 1) Configurações salvas
        var settings = SettingsService.Load();
        SourceFolder = string.IsNullOrWhiteSpace(settings.SourceFolder) ? AppPaths.DefaultSourceFolder : settings.SourceFolder;
        CatalogUrl = settings.CatalogUrl;
        CheckUpdatesOnStartup = settings.CheckUpdatesOnStartup;
        PeriodicCheck = settings.PeriodicCheck;
        MinimizeToTray = settings.MinimizeToTray;
        ShowNotifications = settings.ShowNotifications;
        AutoUpdateApps = settings.AutoUpdateApps;
        AutoUpdateLauncher = settings.AutoUpdateLauncher;
        AskBeforeInstall = settings.AskBeforeInstall;
        AppDesktopShortcut = settings.AppDesktopShortcut;
        AppMenuShortcut = settings.AppMenuShortcut;
        OpenAfterInstall = settings.OpenAfterInstall;

        // "Iniciar com o sistema" é lido do próprio sistema, não do settings.json
        StartWithSystem = StartupService.IsEnabled();
        _loading = false;

        // 2) Catálogo local (embutido ou cache) — rápido e funciona sem internet
        ApplyCatalog(CatalogService.LoadLocal(CatalogUrl));
    }

    /// <summary>
    /// Chamado uma única vez quando o launcher começa a rodar (com ou sem janela visível):
    /// liga a verificação periódica e faz a verificação de abertura.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        _periodicTimer = new DispatcherTimer { Interval = TimerTick };
        _periodicTimer.Tick += async (_, _) => await PeriodicTickAsync();
        _periodicTimer.Start();

        // "Rodando": confere a cada 2 segundos quais apps instalados estão abertos
        _runningTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _runningTimer.Tick += (_, _) => RefreshRunningApps();
        _runningTimer.Start();
        RefreshRunningApps();

        if (CheckUpdatesOnStartup && HasCatalogUrl)
            await RefreshCatalogAsync(fromUser: false);
    }

    // ---------- Verificação periódica ----------
    // O intervalo de propósito não aparece na tela: para quem usa, é só "de tempos em tempos".

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan TimerTick = TimeSpan.FromMinutes(10);   // olha o relógio a cada 10 min (sobrevive a suspensão do PC)
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(30);

    private bool _initialized;
    private bool _isRefreshing;
    private DispatcherTimer? _periodicTimer;
    private DispatcherTimer? _runningTimer;

    private void RefreshRunningApps()
    {
        foreach (var app in _allApps.Where(a => a.IsInstalled))
            app.RefreshRunning();
    }
    private DateTime _lastOnlineCheck = DateTime.Now;

    private async Task PeriodicTickAsync()
    {
        try
        {
            if (!PeriodicCheck || !HasCatalogUrl || _isRefreshing) return;
            if (DateTime.Now - _lastOnlineCheck < CheckInterval) return;

            await RefreshCatalogAsync(fromUser: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] Verificação periódica falhou: {ex.Message}");
        }
    }

    /// <summary>Usado pela notificação e pelo menu da bandeja.</summary>
    public void ShowUpdatesPage()
    {
        IsLibraryPage = false;
        IsSettingsPage = false;
        IsUpdatesPage = true;
    }

    // ---------- Listas mostradas na tela ----------

    public ObservableCollection<AppCardViewModel> VisibleApps { get; } = new();
    public ObservableCollection<AppCardViewModel> UpdatableApps { get; } = new();

    // ---------- Navegação ----------

    [ObservableProperty] private bool _isLibraryPage = true;
    [ObservableProperty] private bool _isUpdatesPage;
    [ObservableProperty] private bool _isSettingsPage;

    // ---------- Filtros da Biblioteca ----------

    [ObservableProperty] private bool _isFilterAll = true;
    [ObservableProperty] private bool _isFilterInstalled;
    [ObservableProperty] private bool _isFilterAvailable;
    [ObservableProperty] private string _searchText = string.Empty;

    // Atualiza a cada mudança (ligar E desligar), porque os RadioButtons
    // trocam de estado um de cada vez e só o estado final importa.
    partial void OnIsFilterAllChanged(bool value) => RefreshLists();
    partial void OnIsFilterInstalledChanged(bool value) => RefreshLists();
    partial void OnIsFilterAvailableChanged(bool value) => RefreshLists();
    partial void OnSearchTextChanged(string value) => RefreshLists();

    // ---------- Configurações (salvas automaticamente) ----------

    [ObservableProperty] private string _sourceFolder = string.Empty;
    [ObservableProperty] private string _catalogUrl = string.Empty;
    [ObservableProperty] private bool _checkUpdatesOnStartup = true;
    [ObservableProperty] private bool _periodicCheck = true;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _showNotifications = true;
    [ObservableProperty] private bool _autoUpdateApps;
    [ObservableProperty] private bool _autoUpdateLauncher;
    [ObservableProperty] private bool _startWithSystem;
    [ObservableProperty] private bool _askBeforeInstall = true;
    [ObservableProperty] private bool _appDesktopShortcut;
    [ObservableProperty] private bool _appMenuShortcut = true;
    [ObservableProperty] private bool _openAfterInstall;

    partial void OnSourceFolderChanged(string value)
    {
        SaveSettings();
        RescanInstalled();
    }

    partial void OnCatalogUrlChanged(string value)
    {
        SaveSettings();
        OnPropertyChanged(nameof(HasCatalogUrl));
    }

    partial void OnCheckUpdatesOnStartupChanged(bool value) => SaveSettings();
    partial void OnPeriodicCheckChanged(bool value) => SaveSettings();
    partial void OnMinimizeToTrayChanged(bool value) => SaveSettings();
    partial void OnShowNotificationsChanged(bool value) => SaveSettings();
    partial void OnAutoUpdateAppsChanged(bool value) => SaveSettings();
    partial void OnAutoUpdateLauncherChanged(bool value) => SaveSettings();
    partial void OnAskBeforeInstallChanged(bool value) => SaveSettings();
    partial void OnAppDesktopShortcutChanged(bool value) => SaveSettings();
    partial void OnAppMenuShortcutChanged(bool value) => SaveSettings();
    partial void OnOpenAfterInstallChanged(bool value) => SaveSettings();

    // ---------- Instalação de apps (janela de opções) ----------

    public string AppMenuShortcutText => $"Adicionar ao {ShortcutService.MenuLabel}";

    /// <summary>
    /// Mostra a janela "Instalar app" e devolve as escolhas (null = cancelou).
    /// Quem preenche é o MainWindow (a janela precisa de um "dono" para abrir como diálogo).
    /// </summary>
    public Func<AppInstallRequest, Task<AppInstallChoices?>>? ShowInstallDialog { get; set; }

    private async Task<AppInstallChoices?> GetInstallChoicesAsync(AppCardViewModel card)
    {
        var defaults = new AppInstallChoices(AppDesktopShortcut, AppMenuShortcut, OpenAfterInstall);
        if (!AskBeforeInstall || ShowInstallDialog is null) return defaults;

        var choices = await ShowInstallDialog(new AppInstallRequest(card, defaults, SourceFolder));

        // "Lembrar e não perguntar de novo": vira o padrão (dá para mudar em Configurações)
        if (choices is { RememberChoices: true })
        {
            AppDesktopShortcut = choices.DesktopShortcut;
            AppMenuShortcut = choices.MenuShortcut;
            OpenAfterInstall = choices.OpenAfterInstall;
            AskBeforeInstall = false;
        }

        return choices;
    }

    // ---------- Instalação do próprio NokyLabs ----------

    /// <summary>Abrir o assistente / o desinstalador (quem atende é o App.axaml.cs).</summary>
    public event EventHandler? SetupRequested;
    public event EventHandler? UninstallRequested;

    public string InstallModeText
    {
        get
        {
            if (SetupService.IsSimulation) return "Modo de desenvolvimento (Visual Studio): o assistente roda em demonstração.";
            if (AppPaths.IsPortable) return $"Modo portátil · tudo fica em {AppPaths.ExeFolder}";
            var info = SetupService.LoadInstallInfo();
            return info is null ? "Rodando sem instalação registrada." : $"Instalado em {info.InstallFolder}";
        }
    }

    /// <summary>Desinstalar só faz sentido no modo instalado (ou em demonstração).</summary>
    public bool CanUninstallLauncher => SetupService.IsSimulation || (!AppPaths.IsPortable && SetupService.LoadInstallInfo() is not null);

    [RelayCommand]
    private void OpenSetup() => SetupRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void UninstallLauncher() => UninstallRequested?.Invoke(this, EventArgs.Empty);

    // ---------- Iniciar com o sistema ----------

    private bool _syncingStartup;

    public bool IsStartupSupported => StartupService.IsSupported;
    public string StartupHint => StartupService.SystemHint;

    partial void OnStartWithSystemChanged(bool value)
    {
        if (_loading || _syncingStartup) return;

        try
        {
            StartupService.SetEnabled(value);
            StatusMessage = value
                ? "O NokyLabs vai abrir junto com o sistema, já minimizado."
                : "O NokyLabs não vai mais abrir junto com o sistema.";
        }
        catch (LauncherException ex)
        {
            StatusMessage = ex.Message;
            // Volta o botão para o estado real (depois do clique terminar de ser processado)
            Dispatcher.UIThread.Post(SyncStartupState);
        }
    }

    /// <summary>
    /// Relê do sistema. Chamado quando a janela volta ao foco, porque a pessoa pode ter
    /// ligado/desligado pelo Gerenciador de Tarefas enquanto o launcher estava aberto.
    /// </summary>
    public void SyncStartupState()
    {
        _syncingStartup = true;
        StartWithSystem = StartupService.IsEnabled();
        _syncingStartup = false;
    }

    private void SaveSettings()
    {
        if (_loading) return;

        SettingsService.Save(new LauncherSettings
        {
            // Guarda vazio quando é a pasta padrão, assim ela acompanha o usuário em outro PC
            SourceFolder = SourceFolder == AppPaths.DefaultSourceFolder ? string.Empty : SourceFolder,
            CatalogUrl = CatalogUrl.Trim(),
            CheckUpdatesOnStartup = CheckUpdatesOnStartup,
            PeriodicCheck = PeriodicCheck,
            MinimizeToTray = MinimizeToTray,
            ShowNotifications = ShowNotifications,
            AutoUpdateApps = AutoUpdateApps,
            AutoUpdateLauncher = AutoUpdateLauncher,
            AskBeforeInstall = AskBeforeInstall,
            AppDesktopShortcut = AppDesktopShortcut,
            AppMenuShortcut = AppMenuShortcut,
            OpenAfterInstall = OpenAfterInstall
        });
    }

    public bool HasCatalogUrl => !string.IsNullOrWhiteSpace(CatalogUrl);

    public string AppVersion => $"v{SelfUpdateService.CurrentVersion}";

    public string LauncherModeText => SelfUpdateService.IsSingleFile
        ? "Versão publicada · atualização automática ativa"
        : "Modo de desenvolvimento · atualização automática desativada";
    public string ConfigFolder => AppPaths.ConfigFolder;
    public string SystemDescription =>
        $"{RuntimeInformation.OSDescription} · {RuntimeInformation.OSArchitecture}";

    // ---------- Catálogo ----------

    [ObservableProperty] private string _catalogSourceText = string.Empty;

    private void ApplyCatalog(CatalogResult result)
    {
        _launcherInfo = result.Catalog.Launcher;
        RaiseLauncherStateChanged();

        _allApps.Clear();
        foreach (var app in result.Catalog.Apps)
            _allApps.Add(new AppCardViewModel(app, () => SourceFolder, msg => StatusMessage = msg, RefreshLists,
                                              GetInstallChoicesAsync));

        CatalogSourceText = result.Source switch
        {
            CatalogSource.Online => $"Online · atualizado às {DateTime.Now:HH\\:mm}",
            CatalogSource.Cache => "Cópia salva da última verificação online",
            _ => "Embutido no launcher"
        };

        RefreshLists();
    }

    /// <summary>Busca o catálogo e, se deu certo, roda as atualizações automáticas e os avisos.</summary>
    private async Task RefreshCatalogAsync(bool fromUser)
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            if (await FetchCatalogAsync(fromUser))
                await RunAutomationsAsync();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task<bool> FetchCatalogAsync(bool fromUser)
    {
        if (_allApps.Any(a => a.IsBusy))
        {
            StatusMessage = "Espere as instalações em andamento terminarem para recarregar o catálogo.";
            return false;
        }

        IsChecking = true;
        StatusMessage = "Procurando atualizações…";

        try
        {
            if (HasCatalogUrl)
            {
                _lastOnlineCheck = DateTime.Now;
                ApplyCatalog(await CatalogService.FetchOnlineAsync(CatalogUrl.Trim()));
            }
            else
            {
                // Sem link: relê o embutido e a pasta de origem
                ApplyCatalog(CatalogService.LoadLocal(CatalogUrl));
            }

            StatusMessage = TotalUpdateCount switch
            {
                0 when fromUser && !HasCatalogUrl => "Tudo em dia (usando o catálogo embutido — configure um link para buscar online).",
                0 => "Tudo em dia! Nenhuma atualização pendente.",
                1 => "Encontrei 1 atualização disponível.",
                _ => $"Encontrei {TotalUpdateCount} atualizações disponíveis."
            };
            return true;
        }
        catch (LauncherException ex)
        {
            StatusMessage = ex.Message + " Usando o catálogo que já estava carregado.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Não consegui atualizar o catálogo: {ex.Message}";
        }
        finally
        {
            IsChecking = false;
        }

        // Falhou (ex.: sem internet): tenta de novo daqui a pouco, em vez de esperar o intervalo inteiro
        _lastOnlineCheck = DateTime.Now - CheckInterval + RetryAfterFailure;
        return false;
    }

    // ---------- Atualizações automáticas e avisos ----------

    /// <summary>Disparado quando há algo para avisar no canto da tela.</summary>
    public event EventHandler<NotificationRequest>? NotificationRequested;

    /// <summary>Guarda o que já foi avisado nesta sessão, para não repetir o mesmo aviso.</summary>
    private readonly HashSet<string> _notified = new();

    private async Task RunAutomationsAsync()
    {
        // 1) Apps
        var autoUpdated = new List<string>();
        if (AutoUpdateApps)
        {
            var pending = RealApps.Where(a => a.HasUpdate && !a.IsDownloadPending && !a.IsBusy).ToList();
            foreach (var app in pending)
            {
                await app.UpdateAsync();
                if (!app.HasUpdate) autoUpdated.Add(app.Name);
            }
        }

        // 2) Launcher (a versão nova passa a valer na próxima abertura)
        if (AutoUpdateLauncher && SelfUpdateService.IsSingleFile && CanUpdateLauncher())
            await UpdateLauncherAsync();

        // 3) Avisos
        if (autoUpdated.Count == 1)
            Notify($"{autoUpdated[0]} foi atualizado", "A versão nova já está instalada e pronta para usar.");
        else if (autoUpdated.Count > 1)
            Notify($"{autoUpdated.Count} apps foram atualizados", $"{string.Join(", ", autoUpdated)} já estão nas versões novas.");

        if (LauncherUpdateReady && _notified.Add($"launcher-pronto@{LauncherLatestVersion}"))
            Notify($"NokyLabs v{LauncherLatestVersion} baixado", "A versão nova passa a valer na próxima vez que o launcher abrir.");

        // Novidades que ainda esperam por você (só as que já dá para instalar)
        var waiting = new List<string>();
        foreach (var app in RealApps)
        {
            if (app.HasUpdate && !app.IsDownloadPending && _notified.Add($"{app.Id}@{app.LatestVersion}"))
                waiting.Add(app.Name);
        }

        if (LauncherUpdateAvailable && !LauncherUpdateReady && LauncherDownload is { IsPlaceholder: false } &&
            _notified.Add($"launcher@{LauncherLatestVersion}"))
        {
            waiting.Add("o próprio NokyLabs");
        }

        if (waiting.Count == 1)
            Notify("Atualização disponível", $"{FirstUpper(waiting[0])} tem uma versão nova. Clique para ver.");
        else if (waiting.Count > 1)
            Notify($"{waiting.Count} atualizações disponíveis", $"Novas versões de {string.Join(", ", waiting)}. Clique para ver.");
    }

    private void Notify(string title, string message)
    {
        if (ShowNotifications)
            NotificationRequested?.Invoke(this, new NotificationRequest(title, message));
    }

    private static string FirstUpper(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];

    private void RescanInstalled()
    {
        foreach (var app in _allApps) app.Rescan();
        RefreshLists();
    }


    // ---------- Atualização do próprio launcher ----------

    private LauncherInfo? _launcherInfo;

    /// <summary>Pedido para fechar e abrir a nova versão (quem atende é o App.axaml.cs).</summary>
    public event EventHandler? RestartRequested;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UpdateLauncherCommand))]
    private bool _isLauncherUpdating;

    [ObservableProperty] private double _launcherProgress;
    [ObservableProperty] private bool _isLauncherProgressIndeterminate;

    /// <summary>A versão nova já foi baixada e trocada; falta só reiniciar.</summary>
    [ObservableProperty] private bool _launcherUpdateReady;

    partial void OnIsLauncherUpdatingChanged(bool value) => OnPropertyChanged(nameof(LauncherActionText));
    partial void OnLauncherUpdateReadyChanged(bool value) => RaiseLauncherStateChanged();

    private CatalogDownload? LauncherDownload =>
        _launcherInfo is not null && _launcherInfo.Downloads.TryGetValue(PlatformInfo.Key, out var dl) ? dl : null;

    public string LauncherLatestVersion => _launcherInfo?.Version ?? SelfUpdateService.CurrentVersion;

    public bool LauncherUpdateAvailable =>
        _launcherInfo is not null &&
        LauncherDownload is not null &&
        VersionHelper.IsNewer(_launcherInfo.Version, SelfUpdateService.CurrentVersion);

    public bool ShowLauncherBanner => LauncherUpdateAvailable || LauncherUpdateReady;
    public bool ShowLauncherUpdateButton => !LauncherUpdateReady;

    public string LauncherBannerTitle => LauncherUpdateReady
        ? $"NokyLabs v{LauncherLatestVersion} pronto!"
        : $"Nova versão do NokyLabs: v{LauncherLatestVersion}";

    public string LauncherBannerText => LauncherUpdateReady
        ? "Reinicie agora, ou ela entra sozinha na próxima vez que o launcher abrir."
        : string.IsNullOrWhiteSpace(_launcherInfo?.Notes)
            ? $"Você está na v{SelfUpdateService.CurrentVersion}."
            : _launcherInfo!.Notes;

    public string LauncherActionText =>
        IsLauncherUpdating ? "Baixando…"
        : LauncherDownload is null || LauncherDownload.IsPlaceholder ? "Download em breve"
        : "Atualizar launcher";

    private void RaiseLauncherStateChanged()
    {
        foreach (var name in new[]
                 {
                     nameof(LauncherLatestVersion), nameof(LauncherUpdateAvailable), nameof(ShowLauncherBanner),
                     nameof(ShowLauncherUpdateButton), nameof(LauncherBannerTitle), nameof(LauncherBannerText),
                     nameof(LauncherActionText), nameof(TotalUpdateCount), nameof(HasAnyUpdate), nameof(HasNoUpdates)
                 })
        {
            OnPropertyChanged(name);
        }

        UpdateLauncherCommand.NotifyCanExecuteChanged();
    }

    private bool CanUpdateLauncher() =>
        !IsLauncherUpdating && !LauncherUpdateReady &&
        LauncherDownload is { IsPlaceholder: false };

    [RelayCommand(CanExecute = nameof(CanUpdateLauncher))]
    private async Task UpdateLauncherAsync()
    {
        var download = LauncherDownload;
        if (download is null) return;

        IsLauncherUpdating = true;
        LauncherProgress = 0;
        StatusMessage = $"Baixando o NokyLabs v{LauncherLatestVersion}…";

        var progress = new Progress<double>(value =>
        {
            IsLauncherProgressIndeterminate = value < 0;
            if (value >= 0) LauncherProgress = value;
        });

        try
        {
            await SelfUpdateService.UpdateAsync(download, progress);
            LauncherUpdateReady = true;
            StatusMessage = "Atualização do launcher pronta. É só reiniciar.";
        }
        catch (LauncherException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Não consegui atualizar o launcher: {ex.Message}";
        }
        finally
        {
            IsLauncherUpdating = false;
            IsLauncherProgressIndeterminate = false;
        }
    }

    [RelayCommand]
    private void RestartLauncher()
    {
        if (_allApps.Any(a => a.IsBusy))
        {
            StatusMessage = "Espere as instalações em andamento terminarem antes de reiniciar.";
            return;
        }

        RestartRequested?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Rodapé ----------

    [ObservableProperty] private string _statusMessage = "Pronto. Tudo carregado.";
    [ObservableProperty] private bool _isChecking;

    // ---------- Números e textos de resumo ----------

    private IEnumerable<AppCardViewModel> RealApps => _allApps.Where(a => !a.IsComingSoon);

    public int InstalledCount => RealApps.Count(a => a.IsInstalled);
    public int UpdateCount => RealApps.Count(a => a.HasUpdate);
    public int AvailableCount => RealApps.Count(a => !a.IsInstalled && !a.IsUnsupported);
    public bool HasUpdates => UpdateCount > 0;

    /// <summary>Apps com atualização + o próprio launcher (para o selo da barra lateral).</summary>
    public int TotalUpdateCount => UpdateCount + (LauncherUpdateAvailable && !LauncherUpdateReady ? 1 : 0);
    public bool HasAnyUpdate => TotalUpdateCount > 0;
    public bool HasNoUpdates => !HasAnyUpdate && !LauncherUpdateReady;
    public bool IsLibraryEmpty => VisibleApps.Count == 0;

    public string LibrarySubtitle =>
        $"{RealApps.Count()} apps no catálogo · {InstalledCount} instalados · {PlatformInfo.DisplayName}";

    public string UpdatesSubtitle => UpdateCount switch
    {
        0 => "Está tudo em dia por aqui.",
        1 => "1 app tem uma versão nova esperando por você.",
        _ => $"{UpdateCount} apps têm versões novas esperando por você."
    };

    /// <summary>Reconstrói as listas visíveis conforme filtro e busca.</summary>
    private void RefreshLists()
    {
        var query = SearchText.Trim();

        IEnumerable<AppCardViewModel> filtered = _allApps;

        if (IsFilterInstalled)
            filtered = filtered.Where(a => a.IsInstalled);
        else if (IsFilterAvailable)
            filtered = filtered.Where(a => !a.IsInstalled);

        if (query.Length > 0)
            filtered = filtered.Where(a =>
                a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

        var visible = filtered.ToList();
        if (!visible.SequenceEqual(VisibleApps))
        {
            VisibleApps.Clear();
            foreach (var app in visible) VisibleApps.Add(app);
        }

        var updatable = RealApps.Where(a => a.HasUpdate || (a.IsBusy && UpdatableApps.Contains(a))).ToList();
        if (!updatable.SequenceEqual(UpdatableApps))
        {
            UpdatableApps.Clear();
            foreach (var app in updatable) UpdatableApps.Add(app);
        }

        foreach (var name in new[]
                 {
                     nameof(InstalledCount), nameof(UpdateCount), nameof(AvailableCount),
                     nameof(HasUpdates), nameof(HasNoUpdates), nameof(IsLibraryEmpty),
                     nameof(TotalUpdateCount), nameof(HasAnyUpdate),
                     nameof(LibrarySubtitle), nameof(UpdatesSubtitle)
                 })
        {
            OnPropertyChanged(name);
        }
    }

    // ---------- Comandos ----------

    [RelayCommand]
    private Task CheckUpdatesAsync() => RefreshCatalogAsync(fromUser: true);

    [RelayCommand]
    private async Task UpdateAllAsync()
    {
        var pending = UpdatableApps.Where(a => a.HasUpdate && !a.IsDownloadPending).ToList();
        if (pending.Count == 0)
        {
            StatusMessage = "Nenhuma atualização pronta para baixar ainda.";
            return;
        }

        // Uma de cada vez: mais gentil com a internet e mais fácil de entender se algo falhar
        foreach (var app in pending)
            await app.UpdateAsync();

        if (UpdateCount == 0) StatusMessage = "Todos os apps foram atualizados.";
    }

    [RelayCommand]
    private void ResetSourceFolder()
    {
        SourceFolder = AppPaths.DefaultSourceFolder;
        StatusMessage = "Pasta de origem voltou para o padrão.";
    }

    [RelayCommand]
    private void OpenSourceFolder()
    {
        try
        {
            LaunchService.OpenFolder(SourceFolder);
        }
        catch (LauncherException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenConfigFolder()
    {
        try
        {
            LaunchService.OpenFolder(AppPaths.ConfigFolder);
        }
        catch (LauncherException ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
