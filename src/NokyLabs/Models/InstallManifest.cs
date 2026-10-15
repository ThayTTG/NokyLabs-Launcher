namespace NokyLabs.Models;

/// <summary>
/// Arquivo "nokylabs-app.json" que o launcher grava dentro da pasta de cada app instalado.
/// É assim que ele descobre depois o que está instalado, em qual versão e quais atalhos criou.
/// </summary>
public sealed class InstallManifest
{
    public const string FileName = "nokylabs-app.json";

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Executable { get; set; } = string.Empty;
    public DateTime InstalledAt { get; set; }

    /// <summary>Atalhos criados para este app (removidos junto ao desinstalar).</summary>
    public List<string> Shortcuts { get; set; } = new();
}

/// <summary>
/// Arquivo "install.json" com o registro da instalação do próprio NokyLabs.
/// Fica em %APPDATA%\NokyLabs (Linux: ~/.config/NokyLabs).
/// </summary>
public sealed class InstallInfo
{
    public string InstallFolder { get; set; } = string.Empty;
    public string ExePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime InstalledAt { get; set; }

    /// <summary>Atalhos do launcher criados pelo assistente.</summary>
    public List<string> Shortcuts { get; set; } = new();

    /// <summary>Aparece em Configurações › Aplicativos instalados do Windows.</summary>
    public bool RegisteredInSystem { get; set; }
}

/// <summary>Escolhas feitas na janela "Instalar app".</summary>
public sealed record AppInstallChoices(
    bool DesktopShortcut,
    bool MenuShortcut,
    bool OpenAfterInstall,
    bool RememberChoices = false);
