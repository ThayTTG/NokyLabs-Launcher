namespace NokyLabs.Services;

/// <summary>
/// Caminhos usados pelo launcher.
///
/// Dois modos:
/// • Instalado (padrão): configurações em %APPDATA%\NokyLabs (Linux: ~/.config/NokyLabs)
///   e apps em %LOCALAPPDATA%\NokyLabs\Apps (Linux: ~/.local/share/NokyLabs/Apps).
/// • Portátil: existe um arquivo "NokyLabs.portable" ao lado do executável.
///   Tudo fica junto dele (NokyLabs-Dados e NokyLabs-Apps), então dá para levar num pendrive
///   e funciona mesmo que a letra do drive mude de um PC para outro.
/// </summary>
public static class AppPaths
{
    public const string PortableMarkerName = "NokyLabs.portable";

    static AppPaths() => Refresh();

    /// <summary>Pasta onde está o executável que está rodando agora.</summary>
    public static string ExeFolder =>
        Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) is { Length: > 0 } dir
            ? dir
            : AppContext.BaseDirectory;

    public static bool IsPortable { get; private set; }

    /// <summary>Configurações do modo atual.</summary>
    public static string ConfigFolder { get; private set; } = string.Empty;

    /// <summary>Pasta de apps padrão do modo atual.</summary>
    public static string DefaultSourceFolder { get; private set; } = string.Empty;

    /// <summary>Relê se estamos em modo portátil (chamado de novo depois do assistente).</summary>
    public static void Refresh()
    {
        IsPortable = File.Exists(Path.Combine(ExeFolder, PortableMarkerName));
        ConfigFolder = ConfigFolderFor(IsPortable, ExeFolder);
        DefaultSourceFolder = DefaultSourceFolderFor(IsPortable, ExeFolder);
    }

    // ---------- Pastas por modo (usadas pelo assistente, que prepara um modo diferente do atual) ----------

    /// <summary>Windows: %APPDATA%\NokyLabs · Linux: ~/.config/NokyLabs</summary>
    public static string InstalledConfigFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NokyLabs");

    public static string ConfigFolderFor(bool portable, string exeFolder) =>
        portable ? Path.Combine(exeFolder, "NokyLabs-Dados") : InstalledConfigFolder;

    public static string DefaultSourceFolderFor(bool portable, string exeFolder) =>
        portable
            ? Path.Combine(exeFolder, "NokyLabs-Apps")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NokyLabs", "Apps");

    /// <summary>Onde o próprio NokyLabs é instalado. Windows: %LOCALAPPDATA%\Programs\NokyLabs (sem precisar de administrador).</summary>
    public static string DefaultInstallFolder =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "NokyLabs")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NokyLabs", "Launcher");

    public static string LauncherExeName => OperatingSystem.IsWindows() ? "NokyLabs.exe" : "NokyLabs";

    // ---------- Arquivos ----------

    public static string SettingsFile => Path.Combine(ConfigFolder, "settings.json");

    /// <summary>Última cópia do catálogo online que deu certo (para funcionar offline).</summary>
    public static string CatalogCacheFile => Path.Combine(ConfigFolder, "catalogo-cache.json");

    /// <summary>Registro da instalação do NokyLabs (só existe no modo instalado).</summary>
    public static string InstallInfoFile => Path.Combine(InstalledConfigFolder, "install.json");

    /// <summary>Pasta de um app específico dentro da pasta de origem.</summary>
    public static string AppFolder(string sourceFolder, string appId) => Path.Combine(sourceFolder, appId);

    /// <summary>Compara dois caminhos de arquivo (sem diferenciar maiúsculas no Windows).</summary>
    public static bool SamePath(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(
                Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
                comparison);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
