using System.ComponentModel;
using System.Diagnostics;

namespace NokyLabs.Services;

/// <summary>
/// Descobre se um app instalado está rodando e o encerra quando pedido.
///
/// Um processo só conta como "do app" se o executável dele estiver DENTRO da pasta do app.
/// Assim, outro programa que por acaso tenha o mesmo nome nunca é encerrado por engano.
/// </summary>
public static class ProcessService
{
    /// <summary>Processos que estão rodando a partir da pasta do app.</summary>
    public static List<Process> FindRunning(string appFolder, string executable)
    {
        var result = new List<Process>();
        var name = Path.GetFileNameWithoutExtension(executable);
        if (string.IsNullOrWhiteSpace(name)) return result;

        var folder = Path.GetFullPath(appFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        Process[] candidates;
        try
        {
            candidates = Process.GetProcessesByName(name);
        }
        catch (Exception)
        {
            return result;
        }

        foreach (var process in candidates)
        {
            var keep = false;
            try
            {
                var path = process.MainModule?.FileName;
                keep = path is not null && path.StartsWith(folder, comparison);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // Sem permissão para ver o caminho (ex.: processo de outro usuário): não é nosso.
            }

            if (keep) result.Add(process);
            else process.Dispose();
        }

        return result;
    }

    public static bool IsRunning(string appFolder, string executable)
    {
        var processes = FindRunning(appFolder, executable);
        var running = processes.Count > 0;
        foreach (var p in processes) p.Dispose();
        return running;
    }

    /// <summary>
    /// Encerra o app: primeiro pede para fechar (como clicar no X, dando chance de salvar),
    /// e só força o encerramento se ele não fechar em alguns segundos.
    /// Retorna true se não sobrou nenhum processo.
    /// </summary>
    public static async Task<bool> StopAsync(string appFolder, string executable, TimeSpan gracePeriod)
    {
        var processes = FindRunning(appFolder, executable);
        if (processes.Count == 0) return true;

        try
        {
            // 1) Pedir com educação
            foreach (var p in processes)
            {
                try { p.CloseMainWindow(); } catch (Exception) { /* sem janela (ex.: app de bandeja) */ }
            }

            var deadline = DateTime.UtcNow + gracePeriod;
            while (DateTime.UtcNow < deadline && processes.Any(IsAlive))
                await Task.Delay(250);

            // 2) Forçar quem não fechou
            foreach (var p in processes.Where(IsAlive))
            {
                try { p.Kill(entireProcessTree: true); } catch (Exception) { /* já terminou ou sem permissão */ }
            }

            var killDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
            while (DateTime.UtcNow < killDeadline && processes.Any(IsAlive))
                await Task.Delay(150);

            return !processes.Any(IsAlive);
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    private static bool IsAlive(Process p)
    {
        try { return !p.HasExited; }
        catch (Exception) { return false; }
    }
}
