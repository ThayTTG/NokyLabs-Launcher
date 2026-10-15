using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace NokyLabs.Services;

/// <summary>O que um atalho deve abrir.</summary>
/// <param name="Key">Identificador curto (vira parte do nome do arquivo no Linux).</param>
/// <param name="Name">Nome mostrado para a pessoa (vira o nome do atalho no Windows).</param>
public sealed record ShortcutSpec(
    string Key,
    string Name,
    string Description,
    string TargetPath,
    string WorkingDirectory,
    string? Arguments = null,
    string? IconPath = null);

/// <summary>
/// Cria e remove atalhos do jeito nativo de cada sistema.
///
/// Windows: arquivos .lnk de verdade, criados pela própria interface do Windows (IShellLink),
///   na Área de Trabalho e em Menu Iniciar › Programas › NokyLabs.
/// Linux: arquivos .desktop no menu de aplicativos (~/.local/share/applications)
///   e na Área de Trabalho.
/// </summary>
public static class ShortcutService
{
    private const string MenuFolderName = "NokyLabs";

    public static bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

    /// <summary>Como o menu se chama em cada sistema (para os textos da tela).</summary>
    public static string MenuLabel => OperatingSystem.IsWindows() ? "menu Iniciar" : "menu de aplicativos";

    // ------------------------------------------------------------------ criar

    /// <summary>Cria um atalho na Área de Trabalho. Retorna o caminho do arquivo criado.</summary>
    public static string? CreateDesktop(ShortcutSpec spec)
    {
        var desktop = DesktopFolder();

        if (OperatingSystem.IsWindows())
            return CreateWindowsLink(Path.Combine(desktop, SafeFileName(spec.Name) + ".lnk"), spec);

        if (OperatingSystem.IsLinux())
            return CreateLinuxEntry(Path.Combine(desktop, $"nokylabs-{spec.Key}.desktop"), spec, onDesktop: true);

        return null;
    }

    /// <summary>Cria um atalho no menu Iniciar (Windows) ou no menu de aplicativos (Linux).</summary>
    public static string? CreateMenu(ShortcutSpec spec)
    {
        if (OperatingSystem.IsWindows())
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            return CreateWindowsLink(Path.Combine(programs, MenuFolderName, SafeFileName(spec.Name) + ".lnk"), spec);
        }

        if (OperatingSystem.IsLinux())
        {
            var applications = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications");
            return CreateLinuxEntry(Path.Combine(applications, $"nokylabs-{spec.Key}.desktop"), spec, onDesktop: false);
        }

        return null;
    }

    // ------------------------------------------------------------------ remover

    /// <summary>Apaga atalhos criados antes. Só mexe em arquivos .lnk e .desktop.</summary>
    public static void Remove(IEnumerable<string>? paths)
    {
        if (paths is null) return;

        foreach (var path in paths.Distinct())
        {
            try
            {
                var ext = Path.GetExtension(path);
                var isShortcut = ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                 ext.Equals(".desktop", StringComparison.OrdinalIgnoreCase);
                if (isShortcut && File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[NokyLabs] Não apaguei o atalho {path}: {ex.Message}");
            }
        }

        // Se a pasta "NokyLabs" do menu Iniciar ficou vazia, some com ela também
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var menuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), MenuFolderName);
                if (Directory.Exists(menuFolder) && !Directory.EnumerateFileSystemEntries(menuFolder).Any())
                    Directory.Delete(menuFolder);
            }
            catch (Exception) { /* não é importante */ }
        }
    }

    // ------------------------------------------------------------------ Linux

    /// <summary>Monta um arquivo .desktop (usado pelos atalhos e pelo "iniciar com o sistema").</summary>
    public static string BuildDesktopEntry(string name, string comment, string exe, string? arguments,
                                           string? iconPath, string? workingDirectory, bool autostart = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Desktop Entry]");
        sb.AppendLine("Type=Application");
        sb.AppendLine($"Name={name}");
        sb.AppendLine($"Comment={comment}");
        sb.AppendLine($"Exec=\"{EscapeExec(exe)}\"{(string.IsNullOrWhiteSpace(arguments) ? "" : " " + arguments)}");
        if (!string.IsNullOrWhiteSpace(workingDirectory)) sb.AppendLine($"Path={workingDirectory}");
        if (!string.IsNullOrWhiteSpace(iconPath)) sb.AppendLine($"Icon={iconPath}");
        sb.AppendLine("Terminal=false");
        sb.AppendLine("Categories=Utility;");
        if (autostart) sb.AppendLine("X-GNOME-Autostart-enabled=true");
        return sb.ToString();
    }

    private static string CreateLinuxEntry(string file, ShortcutSpec spec, bool onDesktop)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, BuildDesktopEntry(spec.Name, spec.Description, spec.TargetPath, spec.Arguments,
                                                  spec.IconPath, spec.WorkingDirectory));

        // Na Área de Trabalho, o arquivo precisa poder ser "executado" para virar um ícone clicável
        if (onDesktop && !OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(file);
            File.SetUnixFileMode(file, mode | UnixFileMode.UserExecute);
        }

        return file;
    }

    /// <summary>Regras de aspas do padrão .desktop: dentro de "...", escapar \ " ` e $.</summary>
    private static string EscapeExec(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (c is '\\' or '"' or '`' or '$') sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ Windows

    [SupportedOSPlatform("windows")]
    private static string CreateWindowsLink(string file, ShortcutSpec spec)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(spec.TargetPath);
            link.SetWorkingDirectory(spec.WorkingDirectory);
            link.SetDescription(spec.Description);
            if (!string.IsNullOrWhiteSpace(spec.Arguments)) link.SetArguments(spec.Arguments);
            link.SetIconLocation(string.IsNullOrWhiteSpace(spec.IconPath) ? spec.TargetPath : spec.IconPath, 0);

            ((IPersistFile)link).Save(file, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }

        return file;
    }

    // Interfaces COM do Windows para criar atalhos (.lnk). São as mesmas que o Explorer usa.
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    // ------------------------------------------------------------------ auxiliares

    private static string DesktopFolder()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return string.IsNullOrWhiteSpace(desktop)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop")
            : desktop;
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return clean.Length == 0 ? "NokyLabs" : clean;
    }
}
