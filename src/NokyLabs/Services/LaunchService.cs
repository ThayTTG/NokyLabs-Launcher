using System.ComponentModel;
using System.Diagnostics;

namespace NokyLabs.Services;

/// <summary>Abre apps e pastas do jeito certo em cada sistema.</summary>
public static class LaunchService
{
    /// <summary>Inicia o executável de um app instalado.</summary>
    public static void OpenApp(string appFolder, string executable, string appName)
    {
        var exePath = Path.Combine(appFolder, executable);
        if (!File.Exists(exePath))
            throw new LauncherException($"Não encontrei o executável do {appName}. Tente reinstalar.");

        var info = new ProcessStartInfo(exePath)
        {
            WorkingDirectory = appFolder,   // o app enxerga os próprios arquivos (configs, DLLs...)
            UseShellExecute = false
        };

        try
        {
            Process.Start(info);
        }
        catch (Win32Exception ex)
        {
            // No Linux isso costuma ser falta de permissão de execução; no Windows, antivírus ou arquivo bloqueado.
            throw new LauncherException($"O sistema não deixou abrir o {appName}: {ex.Message}");
        }
    }

    /// <summary>Abre uma pasta no Explorador de Arquivos (Windows) ou no gerenciador de arquivos (Linux).</summary>
    public static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);

        var opener =
            OperatingSystem.IsWindows() ? "explorer.exe" :
            OperatingSystem.IsMacOS() ? "open" :
            "xdg-open";

        var info = new ProcessStartInfo(opener) { UseShellExecute = false };
        info.ArgumentList.Add(folder);

        try
        {
            Process.Start(info);
        }
        catch (Win32Exception)
        {
            throw new LauncherException("Não consegui abrir o gerenciador de arquivos deste sistema.");
        }
    }

    /// <summary>Abre um endereço da internet no navegador padrão.</summary>
    public static void OpenUrl(string url)
    {
        // Aceita também "mailto:" (ex.: pedir acesso a um app restrito por e-mail)
        if (!HttpHelper.IsWebUrl(url) && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            throw new LauncherException("Esse endereço não parece um link válido.");

        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return;
            }

            var info = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
            info.ArgumentList.Add(url);
            Process.Start(info);
        }
        catch (Win32Exception)
        {
            throw new LauncherException("Não consegui abrir o navegador deste sistema.");
        }
    }
}
