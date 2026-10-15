using Microsoft.Win32;

namespace NokyLabs.Services;

/// <summary>
/// Faz o NokyLabs aparecer em Configurações › Aplicativos › Aplicativos instalados (e no antigo
/// "Programas e Recursos") do Windows, com nome, ícone, versão, editor, tamanho e botão Desinstalar.
///
/// É só uma chave em HKEY_CURRENT_USER\...\Uninstall\NokyLabs: não precisa de administrador.
/// O botão "Desinstalar" do Windows chama "NokyLabs.exe --uninstall", que abre o desinstalador do launcher.
/// </summary>
public static class SystemRegistrationService
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\NokyLabs";

    public static bool IsSupported => OperatingSystem.IsWindows();

    public static bool IsRegistered()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
            return key is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void Register(string exePath, string installFolder, string version)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
            key.SetValue("DisplayName", "NokyLabs Launcher");
            key.SetValue("DisplayVersion", version);
            key.SetValue("Publisher", "NokyLabs");
            key.SetValue("DisplayIcon", $"\"{exePath}\",0");
            key.SetValue("InstallLocation", installFolder);
            key.SetValue("UninstallString", $"\"{exePath}\" {SetupService.UninstallArg}");
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("EstimatedSize", EstimatedSizeKb(exePath), RegistryValueKind.DWord);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            throw new LauncherException("O Windows não deixou registrar o NokyLabs em Aplicativos instalados.");
        }
    }

    public static void Unregister()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] Não consegui remover o registro: {ex.Message}");
        }
    }

    /// <summary>Depois de uma autoatualização, mantém a versão e o tamanho certos na lista do Windows.</summary>
    public static void RefreshIfRegistered(string version)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);
            if (key is null) return;

            key.SetValue("DisplayVersion", version);
            if (Environment.ProcessPath is { } exe)
                key.SetValue("EstimatedSize", EstimatedSizeKb(exe), RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] Não consegui atualizar o registro: {ex.Message}");
        }
    }

    private static int EstimatedSizeKb(string exePath)
    {
        try
        {
            return (int)Math.Max(1, new FileInfo(exePath).Length / 1024);
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
