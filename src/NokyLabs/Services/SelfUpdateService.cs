using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using NokyLabs.Models;

namespace NokyLabs.Services;

/// <summary>
/// Atualiza o próprio launcher.
///
/// O truque: um programa aberto não pode ser sobrescrito, mas PODE ser renomeado
/// (no Windows e no Linux). Então:
///   1. baixa a versão nova numa pasta temporária;
///   2. renomeia o executável atual para "NokyLabs.exe.old";
///   3. coloca o novo no lugar do antigo;
///   4. reinicia — e, ao abrir, a versão nova apaga o ".old".
/// </summary>
public static class SelfUpdateService
{
    private const string OldSuffix = ".old";

    /// <summary>Versão deste launcher, lida do &lt;Version&gt; do .csproj (ex.: "0.3.0").</summary>
    public static string CurrentVersion { get; } = ReadVersion();

    /// <summary>
    /// True quando o launcher foi publicado como arquivo único (dotnet publish -p:PublishSingleFile=true).
    /// Rodando pelo Visual Studio são vários arquivos (.dll), e aí a autoatualização fica desligada.
    /// </summary>
#pragma warning disable IL3000 // Location é vazio em arquivo único: é exatamente isso que queremos detectar
    public static bool IsSingleFile => string.IsNullOrEmpty(typeof(SelfUpdateService).Assembly.Location);
#pragma warning restore IL3000

    private static string ReadVersion()
    {
        var v = typeof(SelfUpdateService).Assembly.GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
    }

    /// <summary>Apaga o executável antigo que sobrou de uma atualização (chamado ao abrir).</summary>
    public static void CleanupAfterUpdate()
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return;

        var old = exe + OldSuffix;
        if (!File.Exists(old)) return;

        // A versão antiga pode ainda estar terminando de fechar: tenta algumas vezes, sem pressa.
        _ = Task.Run(async () =>
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    File.Delete(old);
                    return;
                }
                catch (Exception)
                {
                    await Task.Delay(1000);
                }
            }
        });
    }

    /// <summary>Baixa e coloca a nova versão no lugar. Depois disso, é só chamar <see cref="Restart"/>.</summary>
    public static async Task UpdateAsync(CatalogDownload dl, IProgress<double> progress, CancellationToken ct = default)
    {
        if (dl.IsPlaceholder)
            throw new LauncherException("O download da nova versão do launcher ainda não foi publicado.");

        if (!IsSingleFile)
            throw new LauncherException(
                "A atualização automática só funciona na versão publicada (arquivo único). " +
                "Rodando pelo Visual Studio, é só compilar de novo.");

        var exePath = Environment.ProcessPath
            ?? throw new LauncherException("Não consegui descobrir onde o launcher está instalado.");

        var executableName = InstallService.SafeRelativePath(dl.Executable) ?? Path.GetFileName(exePath);
        var tempDir = Path.Combine(Path.GetTempPath(), "NokyLabs", "launcher-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);

            // 1) Baixar
            var fileName = InstallService.GuessFileName(dl.Url, executableName);
            var package = Path.Combine(tempDir, fileName);
            await InstallService.DownloadAsync(dl.Url, package, progress, ct);

            if (!string.IsNullOrWhiteSpace(dl.Sha256))
                await InstallService.VerifySha256Async(package, dl.Sha256, ct);
            progress.Report(90);

            // 2) Achar o executável novo
            string newExe;
            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var extractDir = Path.Combine(tempDir, "extraido");
                ZipFile.ExtractToDirectory(package, extractDir);
                newExe = Directory
                             .EnumerateFiles(extractDir, Path.GetFileName(executableName), SearchOption.AllDirectories)
                             .FirstOrDefault()
                         ?? throw new LauncherException($"O pacote da atualização não contém \"{Path.GetFileName(executableName)}\".");
            }
            else
            {
                newExe = package;
            }

            // 3) Renomear o atual e colocar o novo no lugar
            var oldPath = exePath + OldSuffix;
            try
            {
                if (File.Exists(oldPath)) File.Delete(oldPath);
                File.Move(exePath, oldPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new LauncherException(
                    "Sem permissão para trocar o arquivo do launcher. " +
                    "Se ele estiver em \"Arquivos de Programas\", mova-o para outra pasta (ex.: Documentos).");
            }

            try
            {
                File.Copy(newExe, exePath);
                InstallService.EnsureExecutable(exePath);
            }
            catch
            {
                // Desfaz: devolve o executável antigo para o lugar
                if (!File.Exists(exePath)) File.Move(oldPath, exePath);
                throw;
            }

            progress.Report(100);
        }
        catch (HttpRequestException ex)
        {
            throw new LauncherException(ex.StatusCode == HttpStatusCode.NotFound
                ? "O arquivo da nova versão não foi encontrado no servidor (404). Confira o link no catálogo."
                : $"Falha no download da nova versão: {ex.StatusCode?.ToString() ?? "sem conexão com a internet"}.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LauncherException("O download demorou demais e foi interrompido.");
        }
        catch (InvalidDataException)
        {
            throw new LauncherException("O arquivo baixado não é um .zip válido.");
        }
        finally
        {
            InstallService.TryDeleteDirectory(tempDir);
        }
    }

    /// <summary>Abre a nova versão. Quem chama deve encerrar o launcher atual logo em seguida.</summary>
    public static void Restart()
    {
        var exe = Environment.ProcessPath;
        if (exe is null) return;

        Process.Start(new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty
        });
    }
}

