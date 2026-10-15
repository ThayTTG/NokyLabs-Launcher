using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using NokyLabs.Models;

namespace NokyLabs.Services;

/// <summary>
/// Detecta, baixa, instala e atualiza apps dentro da pasta de origem.
///
/// Estrutura na pasta de origem:
///   Apps/
///   ├─ hotswap/
///   │  ├─ HotSwap.exe
///   │  └─ nokylabs-app.json   ← manifesto gravado pelo launcher
///   └─ cubebin/ ...
/// </summary>
public static class InstallService
{
    // ---------------------------------------------------------------- detecção

    /// <summary>
    /// Descobre se o app está instalado e em qual versão.
    /// Retorna null se não estiver; "?" se a pasta existe com o executável mas sem manifesto
    /// (por exemplo, um app que você copiou para lá na mão).
    /// </summary>
    public static InstallManifest? Detect(string sourceFolder, CatalogApp app)
    {
        var folder = AppPaths.AppFolder(sourceFolder, app.Id);
        if (!Directory.Exists(folder)) return null;

        var manifestPath = Path.Combine(folder, InstallManifest.FileName);
        if (File.Exists(manifestPath))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<InstallManifest>(
                    File.ReadAllText(manifestPath), JsonDefaults.Options);
                if (manifest is not null && !string.IsNullOrWhiteSpace(manifest.Executable))
                    return manifest;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[NokyLabs] Manifesto inválido em {folder}: {ex.Message}");
            }
        }

        // Sem manifesto: procura o executável que o catálogo diz que existe
        if (app.Downloads.TryGetValue(PlatformInfo.Key, out var dl) && dl is not null &&
            !string.IsNullOrWhiteSpace(dl.Executable) &&
            File.Exists(Path.Combine(folder, dl.Executable)))
        {
            return new InstallManifest
            {
                Id = app.Id,
                Version = VersionHelper.Unknown,
                Executable = dl.Executable
            };
        }

        return null;
    }

    // ---------------------------------------------------------------- instalação

    /// <summary>
    /// Baixa e instala (ou atualiza) um app. O progresso vai de 0 a 100; -1 significa "tamanho desconhecido".
    /// Se algo der errado no meio, a versão antiga continua intacta.
    /// </summary>
    public static async Task<InstallManifest> InstallAsync(
        CatalogApp app,
        CatalogDownload download,
        string sourceFolder,
        IProgress<double> progress,
        CancellationToken ct = default)
    {
        if (download.IsPlaceholder)
            throw new LauncherException($"O download do {app.Name} ainda não foi publicado.");

        var executable = SafeRelativePath(download.Executable)
            ?? throw new LauncherException($"O catálogo não diz qual é o executável do {app.Name}.");

        Directory.CreateDirectory(sourceFolder);

        var target = AppPaths.AppFolder(sourceFolder, app.Id);
        var staging = Path.Combine(sourceFolder, $".{app.Id}.novo");
        var backup = Path.Combine(sourceFolder, $".{app.Id}.antigo");
        var tempDir = Path.Combine(Path.GetTempPath(), "NokyLabs", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);

            // 1) Baixar (0% → 85%)
            var fileName = GuessFileName(download.Url, executable);
            var packagePath = Path.Combine(tempDir, fileName);
            await DownloadAsync(download.Url, packagePath, progress, ct);

            // 2) Conferir o hash, se o catálogo informou um
            if (!string.IsNullOrWhiteSpace(download.Sha256))
                await VerifySha256Async(packagePath, download.Sha256, ct);
            progress.Report(88);

            // 3) Preparar a nova versão numa pasta separada
            DeleteDirectoryIfExists(staging);
            Directory.CreateDirectory(staging);

            if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(packagePath, staging);
                FlattenSingleFolder(staging);
            }
            else
            {
                // Executável solto (ex.: um único .exe ou um AppImage)
                File.Copy(packagePath, Path.Combine(staging, executable));
            }
            progress.Report(94);

            var exePath = Path.Combine(staging, executable);
            if (!File.Exists(exePath))
                throw new LauncherException($"O pacote do {app.Name} não contém o arquivo \"{executable}\".");

            EnsureExecutable(exePath);

            // Numa atualização, mantém a lista de atalhos que já existiam (eles continuam válidos)
            var previous = ReadManifest(AppPaths.AppFolder(sourceFolder, app.Id));
            var packageFiles = ListRelativeFiles(staging);

            var manifest = new InstallManifest
            {
                Id = app.Id,
                Name = app.Name,
                Version = app.Version,
                Executable = executable,
                InstalledAt = DateTime.Now,
                Shortcuts = previous?.Shortcuts ?? new List<string>(),
                PackageFiles = packageFiles
            };
            await File.WriteAllTextAsync(
                Path.Combine(staging, InstallManifest.FileName),
                JsonSerializer.Serialize(manifest, JsonDefaults.Options), ct);

            // 4) Trocar a versão antiga pela nova.
            //    Primeiro RENOMEIA a antiga: se o app estiver aberto, isso falha sem estragar nada.
            DeleteDirectoryIfExists(backup);
            if (Directory.Exists(target))
            {
                try
                {
                    Directory.Move(target, backup);
                }
                catch (IOException)
                {
                    throw new LauncherException($"Feche o {app.Name} antes de atualizar — ele parece estar aberto.");
                }
                catch (UnauthorizedAccessException)
                {
                    throw new LauncherException($"Feche o {app.Name} antes de atualizar — ele parece estar aberto.");
                }
            }

            try
            {
                // Configurações e dados que o app guardava na própria pasta vão junto para a versão nova.
                if (Directory.Exists(backup))
                    CarryOverUserFiles(backup, staging, previous);

                Directory.Move(staging, target);
            }
            catch
            {
                // Deu errado: devolve a versão antiga para o lugar
                if (Directory.Exists(backup) && !Directory.Exists(target))
                    Directory.Move(backup, target);
                throw;
            }

            TryDeleteDirectory(backup);
            progress.Report(100);
            return manifest;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient sinaliza timeout como cancelamento
            throw new LauncherException("O download demorou demais e foi interrompido.");
        }
        catch (HttpRequestException ex)
        {
            throw new LauncherException(ex.StatusCode == HttpStatusCode.NotFound
                ? $"O arquivo do {app.Name} não foi encontrado no servidor (404). Confira o link no catálogo."
                : $"Falha no download do {app.Name}: {ex.StatusCode?.ToString() ?? "sem conexão com a internet"}.");
        }
        catch (InvalidDataException)
        {
            throw new LauncherException($"O arquivo baixado do {app.Name} não é um .zip válido.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new LauncherException("Sem permissão para escrever na pasta de origem. Tente escolher outra pasta.");
        }
        finally
        {
            TryDeleteDirectory(tempDir);
            TryDeleteDirectory(staging);
        }
    }

    // ---------------------------------------------------------------- desinstalação

    /// <summary>
    /// Remove a pasta do app. Primeiro RENOMEIA a pasta: se o app estiver aberto,
    /// isso falha antes de apagar qualquer arquivo, e nada fica pela metade.
    /// </summary>
    public static async Task UninstallAsync(CatalogApp app, string sourceFolder)
    {
        var target = AppPaths.AppFolder(sourceFolder, app.Id);
        if (!Directory.Exists(target)) return;

        await RemoveAppFolderAsync(target, app.Name);
    }

    /// <summary>
    /// Apaga a pasta de um app e os atalhos dele (usado também pela instalação limpa e pelo desinstalador do NokyLabs).
    /// </summary>
    public static async Task RemoveAppFolderAsync(string appFolder, string appName)
    {
        var manifest = ReadManifest(appFolder);
        var parent = Path.GetDirectoryName(appFolder) ?? appFolder;
        var trash = Path.Combine(parent, $".{Path.GetFileName(appFolder)}.removendo");

        try
        {
            DeleteDirectoryIfExists(trash);   // sobra de alguma tentativa anterior
            Directory.Move(appFolder, trash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new LauncherException($"Feche o {appName} antes de desinstalar — ele parece estar aberto.");
        }

        ShortcutService.Remove(manifest?.Shortcuts);

        // Apagar pode demorar se o app tiver muitos arquivos: roda fora da thread da interface
        await Task.Run(() => TryDeleteDirectory(trash));
    }

    // ---------------------------------------------------------------- manifesto e atalhos

    public static InstallManifest? ReadManifest(string appFolder)
    {
        var file = Path.Combine(appFolder, InstallManifest.FileName);
        if (!File.Exists(file)) return null;
        try
        {
            return JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(file), JsonDefaults.Options);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void SaveManifest(string appFolder, InstallManifest manifest) =>
        File.WriteAllText(Path.Combine(appFolder, InstallManifest.FileName),
                          JsonSerializer.Serialize(manifest, JsonDefaults.Options));

    /// <summary>Subpastas da pasta de origem que foram instaladas pelo NokyLabs (têm manifesto).</summary>
    public static IEnumerable<string> FindManagedAppFolders(string sourceFolder)
    {
        if (!Directory.Exists(sourceFolder)) return Array.Empty<string>();
        return Directory.GetDirectories(sourceFolder)
            .Where(dir => File.Exists(Path.Combine(dir, InstallManifest.FileName)))
            .ToList();
    }

    /// <summary>
    /// Cria os atalhos de um app instalado e guarda no manifesto (para poder removê-los depois).
    /// Retorna o manifesto atualizado.
    /// </summary>
    public static InstallManifest CreateShortcuts(CatalogApp app, string sourceFolder, InstallManifest manifest,
                                                  bool desktop, bool menu)
    {
        if (!desktop && !menu) return manifest;

        var folder = AppPaths.AppFolder(sourceFolder, app.Id);
        var exe = Path.Combine(folder, manifest.Executable);
        var icon = Path.Combine(folder, "icon.png");   // usado só no Linux, se o app trouxer um

        var spec = new ShortcutSpec(
            Key: app.Id,
            Name: app.Name,
            Description: string.IsNullOrWhiteSpace(app.Description) ? app.Name : app.Description,
            TargetPath: exe,
            WorkingDirectory: folder,
            IconPath: OperatingSystem.IsWindows() || !File.Exists(icon) ? null : icon);

        var created = new List<string>(manifest.Shortcuts);
        if (desktop && ShortcutService.CreateDesktop(spec) is { } d) created.Add(d);
        if (menu && ShortcutService.CreateMenu(spec) is { } m) created.Add(m);

        manifest.Shortcuts = created.Distinct().ToList();
        SaveManifest(folder, manifest);
        return manifest;
    }

    // ---------------------------------------------------------------- etapas

    internal static async Task DownloadAsync(string url, string destination, IProgress<double> progress, CancellationToken ct)
    {
        progress.Report(0);

        // Caminho local: útil para testar antes de publicar os Releases
        if (!HttpHelper.IsWebUrl(url))
        {
            var localPath = HttpHelper.ToLocalPath(url);
            if (!File.Exists(localPath))
                throw new LauncherException($"Não encontrei o arquivo local \"{localPath}\".");
            File.Copy(localPath, destination, overwrite: true);
            progress.Report(85);
            return;
        }

        using var response = await HttpHelper.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        if (total is null or 0) progress.Report(-1);

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(destination);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        var lastReported = -1;

        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;

            if (total is > 0)
            {
                var percent = (int)(received * 85 / total.Value);
                if (percent != lastReported)
                {
                    lastReported = percent;
                    progress.Report(percent);
                }
            }
        }

        progress.Report(85);
    }

    // ---------------------------------------------------------------- arquivos do usuário

    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static string Normalize(string relative) => relative.Replace('\\', '/');

    /// <summary>Todos os arquivos de uma pasta, em caminhos relativos com "/" (menos o manifesto).</summary>
    internal static List<string> ListRelativeFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(f => Normalize(Path.GetRelativePath(folder, f)))
            .Where(r => !PathComparer.Equals(r, InstallManifest.FileName))
            .OrderBy(r => r, PathComparer)
            .ToList();

    /// <summary>Arquivos de programa nunca são levados de uma versão para a outra.</summary>
    private static bool IsProgramFile(string relative)
    {
        var name = Path.GetFileName(relative);
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".appimage", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copia da pasta antiga para a nova tudo o que for do usuário: arquivos que não vieram
    /// no pacote anterior (configurações, dados, logs). Exemplos reais: o config_alunos.json do
    /// Avaliador Acadêmico e o filtro-conteudo.json do YT-DLP GUI, que ficam ao lado do .exe.
    /// </summary>
    /// <returns>Quantos arquivos foram levados.</returns>
    internal static int CarryOverUserFiles(string oldFolder, string newFolder, InstallManifest? previous)
    {
        // Instalações feitas antes da v0.7 não têm a lista do pacote: aí vale só a regra
        // "arquivo de programa não passa; o resto passa se a versão nova não trouxer um igual".
        var knownPackage = previous?.PackageFiles is { Count: > 0 };
        var oldPackage = new HashSet<string>(previous?.PackageFiles ?? new List<string>(), PathComparer);
        var copied = 0;

        foreach (var relative in ListRelativeFiles(oldFolder))
        {
            if (oldPackage.Contains(relative)) continue;
            if (previous is not null && PathComparer.Equals(relative, Normalize(previous.Executable))) continue;
            if (IsProgramFile(relative)) continue;

            var destination = Path.Combine(newFolder, relative.Replace('/', Path.DirectorySeparatorChar));

            // Se a versão nova trouxe um arquivo com o mesmo nome: quando sabemos que o antigo era
            // do usuário (não veio no pacote anterior), o do usuário vence; sem essa certeza, fica o novo.
            if (File.Exists(destination) && !knownPackage) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(Path.Combine(oldFolder, relative.Replace('/', Path.DirectorySeparatorChar)), destination, overwrite: true);
            copied++;
        }

        if (copied > 0)
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] {copied} arquivo(s) do usuário levados para a versão nova.");
        return copied;
    }

    internal static async Task VerifySha256Async(string file, string expected, CancellationToken ct)
    {
        await using var stream = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
        if (!hash.Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new LauncherException("O arquivo baixado não bate com o SHA-256 do catálogo (pode ter vindo corrompido).");
    }

    /// <summary>Se o .zip tinha tudo dentro de uma única pasta, sobe o conteúdo um nível.</summary>
    private static void FlattenSingleFolder(string folder)
    {
        var dirs = Directory.GetDirectories(folder);
        var files = Directory.GetFiles(folder);
        if (dirs.Length != 1 || files.Length != 0) return;

        var inner = dirs[0];
        var temp = folder + "_tmp";
        DeleteDirectoryIfExists(temp);
        Directory.Move(inner, temp);
        Directory.Delete(folder);
        Directory.Move(temp, folder);
    }

    /// <summary>No Linux, o arquivo precisa da permissão de execução (o chmod +x).</summary>
    internal static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows()) return;

        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }

    internal static string GuessFileName(string url, string executable)
    {
        string? name = null;
        try
        {
            name = HttpHelper.IsWebUrl(url)
                ? Path.GetFileName(new Uri(url).AbsolutePath)
                : Path.GetFileName(HttpHelper.ToLocalPath(url));
        }
        catch (UriFormatException) { }

        return string.IsNullOrWhiteSpace(name) ? Path.GetFileName(executable) : name;
    }

    /// <summary>Garante que o caminho do executável fica DENTRO da pasta do app.</summary>
    internal static string? SafeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return null;
        var parts = path.Split('/', '\\');
        return parts.Any(p => p == "..") ? null : Path.Combine(parts);
    }

    internal static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    internal static void TryDeleteDirectory(string path)
    {
        try { DeleteDirectoryIfExists(path); }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"[NokyLabs] Não apaguei {path}: {ex.Message}"); }
    }
}
