namespace NokyLabs.Models;

/// <summary>
/// Tudo o que fica salvo no settings.json.
/// Windows: %APPDATA%\NokyLabs\settings.json
/// Linux:   ~/.config/NokyLabs/settings.json
///
/// "Iniciar com o sistema" NÃO fica aqui: quem manda nisso é o próprio sistema
/// (Gerenciador de Tarefas no Windows, autostart no Linux), e o launcher só lê de lá.
/// </summary>
public sealed class LauncherSettings
{
    /// <summary>Vazio = usar a pasta padrão.</summary>
    public string SourceFolder { get; set; } = string.Empty;

    /// <summary>Catálogo oficial, publicado no site da NokyLabs.</summary>
    public const string DefaultCatalogUrl = "https://thayttg.github.io/nokylabs/catalogo.json";

    /// <summary>Vazio = usar só o catálogo embutido no launcher.</summary>
    public string CatalogUrl { get; set; } = DefaultCatalogUrl;

    /// <summary>Versão do formato deste arquivo (para migrar configurações antigas).</summary>
    public int SettingsVersion { get; set; } = CurrentSettingsVersion;

    public const int CurrentSettingsVersion = 2;

    public bool CheckUpdatesOnStartup { get; set; } = true;

    /// <summary>Procura novidades sozinho, de tempos em tempos, enquanto o launcher estiver aberto.</summary>
    public bool PeriodicCheck { get; set; } = true;

    public bool MinimizeToTray { get; set; }

    /// <summary>Mostra um aviso no canto da tela quando há novidades e o launcher não está em foco.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Instala sozinho as versões novas dos apps.</summary>
    public bool AutoUpdateApps { get; set; }

    /// <summary>Baixa sozinho a versão nova do launcher (vale na próxima abertura).</summary>
    public bool AutoUpdateLauncher { get; set; }

    // ---------- Instalação de apps ----------

    /// <summary>Mostra a janela de opções (atalhos etc.) antes de instalar cada app.</summary>
    public bool AskBeforeInstall { get; set; } = true;

    /// <summary>Escolhas padrão usadas quando a janela não é mostrada.</summary>
    public bool AppDesktopShortcut { get; set; }
    public bool AppMenuShortcut { get; set; } = true;
    public bool OpenAfterInstall { get; set; }
}
