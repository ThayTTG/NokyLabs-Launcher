using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using NokyLabs.Services;
using NokyLabs.ViewModels;
using NokyLabs.Views;

namespace NokyLabs;

public partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private MainWindow? _window;
    private MainWindowViewModel? _viewModel;
    private TrayIcon? _tray;
    private NotificationWindow? _notification;
    private bool _isQuitting;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktop = desktop;

            // Quem decide quando o app termina somos nós (janela escondida na bandeja não conta como "fechada")
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var args = desktop.Args ?? Array.Empty<string>();

            // Se acabamos de nos atualizar, apaga o executável antigo
            SelfUpdateService.CleanupAfterUpdate();

            if (args.Contains(SetupService.UninstallArg))
                ShowUninstall(fromLauncher: false);        // botão "Desinstalar" de Aplicativos instalados
            else if (SetupService.NeedsSetup(args))
                ShowSetup(fromLauncher: false);            // primeira execução ou --setup
            else
                StartLauncher(args);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ------------------------------------------------------------- launcher

    private void StartLauncher(IReadOnlyCollection<string> args)
    {
        // Se o launcher mudou de pasta, corrige o caminho da inicialização e a versão em "Aplicativos instalados"
        StartupService.RefreshPathIfEnabled();
        SystemRegistrationService.RefreshIfRegistered(SelfUpdateService.CurrentVersion);

        _viewModel = new MainWindowViewModel();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.RestartRequested += (_, _) => RestartIntoNewVersion();
        _viewModel.NotificationRequested += (_, request) => ShowNotification(request);
        _viewModel.SetupRequested += (_, _) => ShowSetup(fromLauncher: true);
        _viewModel.UninstallRequested += (_, _) => ShowUninstall(fromLauncher: true);

        _window = new MainWindow { DataContext = _viewModel };
        _window.Closing += OnWindowClosing;
        _window.Closed += (_, _) => Quit();                          // fechou de verdade → encerra
        _window.Activated += (_, _) => _viewModel.SyncStartupState();  // pode ter mudado no Gerenciador de Tarefas

        CreateTrayIcon();

        // Aberto pelo sistema ao ligar o computador?
        var startMinimized = args.Contains(StartupService.MinimizedArg);

        if (startMinimized && OperatingSystem.IsWindows() && _viewModel.MinimizeToTray)
        {
            // Começa só com o ícone na bandeja, sem janela nenhuma
            _ = _viewModel.InitializeAsync();
            return;
        }

        if (startMinimized)
            _window.WindowState = WindowState.Minimized;

        // Depois que a janela aparece, busca o catálogo online (sem travar a abertura)
        _window.Opened += async (_, _) => await _viewModel.InitializeAsync();
        if (_desktop is not null) _desktop.MainWindow = _window;
        _window.Show();
    }

    // ------------------------------------------------------------- assistente e desinstalador

    private void ShowSetup(bool fromLauncher)
    {
        var vm = new SetupViewModel();
        var window = new SetupWindow { DataContext = vm };
        var finished = false;

        vm.Finished += (_, result) =>
        {
            finished = true;
            window.Close();
            OnSetupFinished(result, fromLauncher);
        };

        // Fechou no X sem terminar
        window.Closed += (_, _) =>
        {
            if (!finished && !fromLauncher) Quit();
        };

        if (fromLauncher && _window is { IsVisible: true })
            _ = window.ShowDialog(_window);
        else
            window.Show();
    }

    private void OnSetupFinished(SetupResult result, bool fromLauncher)
    {
        switch (result.Outcome)
        {
            case SetupOutcome.Cancelled:
                if (!fromLauncher) Quit();
                break;

            case SetupOutcome.Quit:
                Quit();
                break;

            case SetupOutcome.OpenInstalled when result.ExePath is not null:
                // Abre a cópia instalada e encerra esta (que normalmente é o arquivo baixado)
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(result.ExePath)
                    {
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(result.ExePath) ?? string.Empty
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[NokyLabs] Não abri a cópia instalada: {ex.Message}");
                }
                Quit();
                break;

            default:
                // Continua aqui mesmo (portátil, demonstração ou já é a cópia instalada)
                AppPaths.Refresh();
                if (fromLauncher)
                {
                    // Configurações podem ter mudado de lugar (ex.: virou portátil): reinicia para reler tudo
                    if (!SetupService.IsSimulation)
                    {
                        SelfUpdateService.Restart();
                        Quit();
                    }
                }
                else
                {
                    StartLauncher(Array.Empty<string>());
                }
                break;
        }
    }

    private void ShowUninstall(bool fromLauncher)
    {
        var vm = new UninstallViewModel();
        var window = new UninstallWindow { DataContext = vm };
        var uninstalled = false;

        vm.Finished += (_, done) =>
        {
            uninstalled = done && !SetupService.IsSimulation;
            window.Close();
        };

        window.Closed += (_, _) =>
        {
            // Desinstalou de verdade, ou foi aberto pelo Windows: encerra o launcher
            if (uninstalled || !fromLauncher) Quit();
        };

        if (fromLauncher && _window is { IsVisible: true })
            _ = window.ShowDialog(_window);
        else
            window.Show();
    }

    // ------------------------------------------------------------- bandeja do sistema

    private void CreateTrayIcon()
    {
        var open = new NativeMenuItem("Abrir NokyLabs");
        open.Click += (_, _) => ShowMainWindow();

        var check = new NativeMenuItem("Verificar atualizações");
        check.Click += (_, _) =>
        {
            ShowMainWindow();
            _viewModel?.ShowUpdatesPage();
            _viewModel?.CheckUpdatesCommand.Execute(null);
        };

        var quit = new NativeMenuItem("Sair");
        quit.Click += (_, _) => Quit();

        var menu = new NativeMenu();
        menu.Items.Add(open);
        menu.Items.Add(check);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quit);

        // .ico é o formato nativo do Windows; no Linux o .png é mais garantido
        var iconFile = OperatingSystem.IsWindows() ? "nokylabs.ico" : "nokylabs.png";

        _tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri($"avares://NokyLabs/Assets/{iconFile}"))),
            ToolTipText = "NokyLabs Launcher",
            Menu = menu,
            IsVisible = _viewModel?.MinimizeToTray == true
        };
        _tray.Clicked += (_, _) => ShowMainWindow();

        TrayIcon.SetIcons(this, new TrayIcons { _tray });
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_tray is null || _viewModel is null) return;

        switch (e.PropertyName)
        {
            // O ícone só aparece na bandeja quando a opção está ligada
            case nameof(MainWindowViewModel.MinimizeToTray):
                _tray.IsVisible = _viewModel.MinimizeToTray;
                break;

            // Passar o mouse no ícone mostra se há novidades
            case nameof(MainWindowViewModel.TotalUpdateCount):
                _tray.ToolTipText = _viewModel.TotalUpdateCount switch
                {
                    0 => "NokyLabs Launcher",
                    1 => "NokyLabs Launcher · 1 atualização",
                    var n => $"NokyLabs Launcher · {n} atualizações"
                };
                break;
        }
    }

    // ------------------------------------------------------------- notificações

    private void ShowNotification(NotificationRequest request)
    {
        // Se a pessoa já está olhando para o launcher, a própria tela mostra as novidades
        if (_window is { IsVisible: true, IsActive: true } && _window.WindowState != WindowState.Minimized)
            return;

        // Só um aviso por vez
        _notification?.Close();

        var notification = new NotificationWindow(request.Title, request.Message);
        notification.Clicked += (_, _) =>
        {
            ShowMainWindow();
            _viewModel?.ShowUpdatesPage();
        };
        notification.Closed += (_, _) =>
        {
            if (ReferenceEquals(_notification, notification)) _notification = null;
        };

        _notification = notification;
        notification.ShowInCorner();
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isQuitting || _window is null || _viewModel is null || !_viewModel.MinimizeToTray) return;

        // Desligamento do computador ou do app: fecha de verdade
        if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown) return;

        if (OperatingSystem.IsWindows())
        {
            // No Windows a bandeja é garantida: some da barra de tarefas e fica só o ícone
            e.Cancel = true;
            _window.Hide();
        }
        else if (_window.WindowState != WindowState.Minimized)
        {
            // No Linux, nem todo ambiente (ex.: GNOME sem extensão) mostra a bandeja.
            // Então o primeiro "fechar" só minimiza; fechar de novo (já minimizado) encerra de verdade,
            // assim ninguém fica preso com um launcher invisível.
            e.Cancel = true;
            _window.WindowState = WindowState.Minimized;
        }
    }

    private void ShowMainWindow()
    {
        if (_window is null) return;

        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void Quit()
    {
        if (_isQuitting) return;
        _isQuitting = true;

        _notification?.Close();
        if (_tray is not null) _tray.IsVisible = false;
        _desktop?.Shutdown();
    }

    // ------------------------------------------------------------- autoatualização

    private void RestartIntoNewVersion()
    {
        try
        {
            SelfUpdateService.Restart();
        }
        catch (Exception ex)
        {
            if (_viewModel is not null)
                _viewModel.StatusMessage = $"Não consegui reiniciar sozinho ({ex.Message}). Feche e abra o launcher.";
            return;
        }

        Quit();
    }
}
