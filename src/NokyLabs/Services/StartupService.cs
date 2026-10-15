using Microsoft.Win32;

namespace NokyLabs.Services;

/// <summary>
/// "Iniciar com o sistema", do jeito nativo de cada sistema.
///
/// Windows: um valor na chave HKCU\...\CurrentVersion\Run.
///   É exatamente essa chave que o Gerenciador de Tarefas mostra em "Aplicativos de inicialização".
///   Se você desativar o NokyLabs por lá, o Windows marca isso em "StartupApproved",
///   e o launcher respeita (a opção aparece desligada nas Configurações).
///   Não precisa de administrador nem de script.
///
/// Linux: um arquivo ~/.config/autostart/nokylabs.desktop, o padrão das sessões gráficas
///   (GNOME, KDE, XFCE, Cinnamon...). Ele também aparece nos "Aplicativos de inicialização" do sistema.
/// </summary>
public static class StartupService
{
    /// <summary>Argumento usado quando o sistema abre o launcher: começa minimizado.</summary>
    public const string MinimizedArg = "--minimized";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "NokyLabs";

    public static bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    /// <summary>Onde o sistema mostra essa opção, para explicar na tela.</summary>
    public static string SystemHint =>
        OperatingSystem.IsWindows()
            ? "Aparece em Gerenciador de Tarefas › Aplicativos de inicialização, onde também dá para ligar ou desligar."
            : "Usa o início automático da sessão (pasta autostart), que também aparece nos aplicativos de inicialização do sistema.";

    private static string LinuxDesktopFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // ~/.config
        "autostart", "nokylabs.desktop");

    /// <summary>Lê do próprio sistema se o launcher está configurado para iniciar junto.</summary>
    public static bool IsEnabled()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var run = Registry.CurrentUser.OpenSubKey(RunKey);
                if (run?.GetValue(ValueName) is not string) return false;

                // O Gerenciador de Tarefas grava aqui quando alguém desativa pela tela dele:
                // primeiro byte ímpar (0x03) = desativado; par (0x02) = ativado.
                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
                if (approved?.GetValue(ValueName) is byte[] { Length: > 0 } data && (data[0] & 1) == 1)
                    return false;

                return true;
            }

            if (OperatingSystem.IsLinux())
            {
                return File.Exists(LinuxDesktopFile) &&
                       !File.ReadAllText(LinuxDesktopFile).Contains("Hidden=true", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] Não consegui ler a inicialização: {ex.Message}");
        }

        return false;
    }

    /// <summary>Liga ou desliga o início junto com o sistema.</summary>
    /// <param name="exePath">Executável a registrar (o assistente usa o da pasta de instalação); vazio = o atual.</param>
    public static void SetEnabled(bool enabled, string? exePath = null)
    {
        var exe = exePath ?? Environment.ProcessPath
                  ?? throw new LauncherException("Não consegui descobrir onde o launcher está instalado.");

        try
        {
            if (OperatingSystem.IsWindows())
            {
                using (var run = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled)
                        run.SetValue(ValueName, $"\"{exe}\" {MinimizedArg}", RegistryValueKind.String);
                    else
                        run.DeleteValue(ValueName, throwOnMissingValue: false);
                }

                // Limpa a marcação do Gerenciador de Tarefas: ligar por aqui vale como "ativado"
                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
                approved?.DeleteValue(ValueName, throwOnMissingValue: false);
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                if (enabled)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LinuxDesktopFile)!);
                    File.WriteAllText(LinuxDesktopFile, ShortcutService.BuildDesktopEntry(
                        "NokyLabs Launcher", "Launcher dos utilitários da NokyLabs", exe, MinimizedArg,
                        iconPath: null, workingDirectory: Path.GetDirectoryName(exe), autostart: true));
                }
                else if (File.Exists(LinuxDesktopFile))
                {
                    File.Delete(LinuxDesktopFile);
                }
                return;
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            throw new LauncherException("O sistema não deixou alterar a inicialização automática.");
        }

        throw new LauncherException("Iniciar com o sistema ainda não funciona neste sistema.");
    }

    /// <summary>
    /// Se o launcher mudou de pasta, corrige o caminho registrado.
    /// Só mexe se estiver ATIVADO, para nunca desfazer um "desativar" feito no Gerenciador de Tarefas.
    /// </summary>
    public static void RefreshPathIfEnabled()
    {
        try
        {
            if (IsEnabled()) SetEnabled(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] Não consegui atualizar o caminho da inicialização: {ex.Message}");
        }
    }
}
