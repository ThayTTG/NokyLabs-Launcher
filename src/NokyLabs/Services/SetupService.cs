using System.Diagnostics;
using System.Text.Json;
using Avalonia.Platform;
using NokyLabs.Models;

namespace NokyLabs.Services;

/// <summary>Tudo o que foi escolhido no assistente de instalação.</summary>
public sealed record SetupPlan
{
    public bool Portable { get; init; }
    public string InstallFolder { get; init; } = string.Empty;
    public string AppsFolder { get; init; } = string.Empty;
    public bool CleanInstall { get; init; }

    // Integração com o sistema (só no modo instalado)
    public bool DesktopShortcut { get; init; }
    public bool MenuShortcut { get; init; }
    public bool RegisterInSystem { get; init; }
    public bool StartWithSystem { get; init; }

    // Apps
    public IReadOnlyList<CatalogApp> InitialApps { get; init; } = Array.Empty<CatalogApp>();
    public bool AppDesktopShortcuts { get; init; }
    public bool AppMenuShortcuts { get; init; }

    // Preferências (vão para o settings.json)
    public LauncherSettings Settings { get; init; } = new();
}

/// <summary>Uma linha de progresso do assistente.</summary>
/// <param name="Percent">0 a 100; -1 = sem porcentagem.</param>
public sealed record SetupProgress(string Message, double Percent = -1, bool IsWarning = false);

/// <summary>
/// Instala, converte para portátil e desinstala o próprio NokyLabs.
///
/// Rodando pelo Visual Studio (não publicado como arquivo único), tudo é SIMULADO:
/// o assistente mostra o que faria, sem copiar arquivos nem mexer no sistema.
/// </summary>
public static class SetupService
{
    public const string SetupArg = "--setup";
    public const string UninstallArg = "--uninstall";

    /// <summary>True quando o assistente só demonstra (rodando pelo Visual Studio).</summary>
    public static bool IsSimulation => !SelfUpdateService.IsSingleFile;

    // ------------------------------------------------------------------ estado

    public static InstallInfo? LoadInstallInfo()
    {
        try
        {
            if (File.Exists(AppPaths.InstallInfoFile))
                return JsonSerializer.Deserialize<InstallInfo>(File.ReadAllText(AppPaths.InstallInfoFile), JsonDefaults.Options);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[NokyLabs] install.json ignorado: {ex.Message}");
        }
        return null;
    }

    private static void SaveInstallInfo(InstallInfo info)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.InstallInfoFile)!);
        File.WriteAllText(AppPaths.InstallInfoFile, JsonSerializer.Serialize(info, JsonDefaults.Options));
    }

    /// <summary>
    /// Decide se o assistente deve abrir:
    /// • com --setup, sempre;
    /// • pelo Visual Studio ou em modo portátil, nunca sozinho;
    /// • na primeira vez (sem install.json) ou quando esta não é a cópia instalada
    ///   (ex.: você baixou o NokyLabs.exe de novo e abriu da pasta Downloads).
    /// </summary>
    public static bool NeedsSetup(IEnumerable<string> args)
    {
        if (args.Contains(SetupArg)) return true;
        if (IsSimulation || AppPaths.IsPortable) return false;

        var info = LoadInstallInfo();
        return info is null || !AppPaths.SamePath(info.ExePath, Environment.ProcessPath);
    }

    /// <summary>Existe algo de uma instalação anterior (para oferecer a instalação limpa)?</summary>
    public static bool HasPreviousData(string appsFolder) =>
        File.Exists(Path.Combine(AppPaths.InstalledConfigFolder, "settings.json")) ||
        File.Exists(AppPaths.InstallInfoFile) ||
        InstallService.FindManagedAppFolders(appsFolder).Any();

    /// <summary>Espaço livre no disco de uma pasta, em texto (ex.: "120 GB livres").</summary>
    public static string FreeSpaceText(string folder)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(folder));
            if (string.IsNullOrEmpty(root)) return string.Empty;
            var free = new DriveInfo(root).AvailableFreeSpace;
            return free switch
            {
                >= 1L << 30 => $"{free / (double)(1L << 30):0.#} GB livres",
                >= 1L << 20 => $"{free / (double)(1L << 20):0} MB livres",
                _ => "Pouco espaço livre"
            };
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    // ------------------------------------------------------------------ instalar

    /// <summary>Executa o plano. Retorna o executável que deve ser aberto ao final.</summary>
    public static async Task<string> InstallAsync(SetupPlan plan, IProgress<SetupProgress> progress)
    {
        var simulate = IsSimulation;
        var currentExe = Environment.ProcessPath ?? throw new LauncherException("Não consegui descobrir onde o launcher está.");
        var exeFolder = AppPaths.ExeFolder;
        var version = SelfUpdateService.CurrentVersion;

        void Log(string message, double percent = -1, bool warning = false) =>
            progress.Report(new SetupProgress(simulate ? $"(simulação) {message}" : message, percent, warning));

        Log("Preparando a instalação…", 2);
        await Task.Delay(simulate ? 400 : 150);

        // 1) Instalação limpa
        if (plan.CleanInstall)
        {
            Log("Removendo configurações, cache e apps da instalação anterior…", 6);
            if (!simulate) await CleanPreviousDataAsync(Log);
            await Task.Delay(simulate ? 500 : 0);
        }

        // 2) O executável do launcher
        string launcherExe;
        if (plan.Portable)
        {
            launcherExe = currentExe;
            Log($"Ativando o modo portátil em {exeFolder}", 18);
            if (!simulate)
            {
                await File.WriteAllTextAsync(Path.Combine(exeFolder, AppPaths.PortableMarkerName),
                    "Este arquivo faz o NokyLabs rodar em modo portátil: configurações e apps ficam nesta pasta.\n");
            }
        }
        else
        {
            launcherExe = Path.Combine(plan.InstallFolder, AppPaths.LauncherExeName);
            Log($"Copiando o NokyLabs para {plan.InstallFolder}", 18);
            if (!simulate) CopyLauncher(currentExe, plan.InstallFolder, launcherExe);
        }
        await Task.Delay(simulate ? 500 : 0);

        // 3) Configurações
        var configFolder = AppPaths.ConfigFolderFor(plan.Portable, Path.GetDirectoryName(launcherExe)!);
        var defaultApps = AppPaths.DefaultSourceFolderFor(plan.Portable, Path.GetDirectoryName(launcherExe)!);
        var settings = plan.Settings;
        settings.SourceFolder = AppPaths.SamePath(plan.AppsFolder, defaultApps) ? string.Empty : plan.AppsFolder;
        Log("Salvando suas preferências", 28);
        if (!simulate)
        {
            SettingsService.Save(settings, configFolder);
            Directory.CreateDirectory(plan.AppsFolder);
        }
        await Task.Delay(simulate ? 400 : 0);

        // 4) Integração com o sistema (só no modo instalado)
        var info = new InstallInfo
        {
            InstallFolder = plan.InstallFolder,
            ExePath = launcherExe,
            Version = version,
            InstalledAt = DateTime.Now
        };

        if (!plan.Portable)
        {
            // Atalhos antigos do launcher saem antes de criar os novos
            if (!simulate) ShortcutService.Remove(LoadInstallInfo()?.Shortcuts);

            var spec = new ShortcutSpec(
                Key: "launcher",
                Name: "NokyLabs",
                Description: "Launcher dos utilitários da NokyLabs",
                TargetPath: launcherExe,
                WorkingDirectory: plan.InstallFolder,
                IconPath: OperatingSystem.IsWindows() ? null : Path.Combine(plan.InstallFolder, "nokylabs.png"));

            if (plan.DesktopShortcut)
            {
                Log("Criando atalho na Área de Trabalho", 34);
                if (!simulate) TryAdd(info.Shortcuts, () => ShortcutService.CreateDesktop(spec), Log);
            }

            if (plan.MenuShortcut)
            {
                Log($"Adicionando ao {ShortcutService.MenuLabel}", 38);
                if (!simulate) TryAdd(info.Shortcuts, () => ShortcutService.CreateMenu(spec), Log);
            }

            Log(plan.StartWithSystem ? "Configurando para iniciar com o sistema" : "Deixando o início automático desligado", 42);
            if (!simulate) TryRun(() => StartupService.SetEnabled(plan.StartWithSystem, launcherExe), Log);

            if (SystemRegistrationService.IsSupported)
            {
                if (plan.RegisterInSystem)
                {
                    Log("Registrando em Aplicativos instalados", 46);
                    if (!simulate)
                    {
                        TryRun(() => SystemRegistrationService.Register(launcherExe, plan.InstallFolder, version), Log);
                        info.RegisteredInSystem = SystemRegistrationService.IsRegistered();
                    }
                }
                else if (!simulate)
                {
                    SystemRegistrationService.Unregister();
                }
            }

            if (!simulate) SaveInstallInfo(info);
            await Task.Delay(simulate ? 600 : 0);
        }

        // 5) Apps iniciais (48% → 98%)
        var apps = plan.InitialApps;
        for (var i = 0; i < apps.Count; i++)
        {
            var app = apps[i];
            var start = 48 + 50.0 * i / apps.Count;
            var span = 50.0 / apps.Count;

            Log($"Instalando {app.Name}…", start);

            if (simulate)
            {
                await Task.Delay(700);
                continue;
            }

            if (!app.Downloads.TryGetValue(PlatformInfo.Key, out var download) || download is null)
            {
                Log($"{app.Name} não tem versão para {PlatformInfo.DisplayName} — pulado.", start, warning: true);
                continue;
            }

            try
            {
                var appProgress = new Progress<double>(p =>
                {
                    if (p >= 0) progress.Report(new SetupProgress(string.Empty, start + span * p / 100));
                });
                var manifest = await InstallService.InstallAsync(app, download, plan.AppsFolder, appProgress);
                InstallService.CreateShortcuts(app, plan.AppsFolder, manifest, plan.AppDesktopShortcuts, plan.AppMenuShortcuts);
                Log($"{app.Name} instalado.", start + span);
            }
            catch (Exception ex)
            {
                Log($"{app.Name}: {ex.Message}", start + span, warning: true);
            }
        }

        Log("Tudo pronto!", 100);
        return launcherExe;
    }

    private static void CopyLauncher(string currentExe, string installFolder, string targetExe)
    {
        Directory.CreateDirectory(installFolder);

        if (!AppPaths.SamePath(currentExe, targetExe))
        {
            try
            {
                File.Copy(currentExe, targetExe, overwrite: true);
            }
            catch (IOException)
            {
                throw new LauncherException("Feche o NokyLabs que já está instalado (inclusive o ícone da bandeja) e tente de novo.");
            }
            catch (UnauthorizedAccessException)
            {
                throw new LauncherException("Sem permissão para escrever nessa pasta. Escolha outra (ex.: dentro da sua pasta de usuário).");
            }
        }

        InstallService.EnsureExecutable(targetExe);

        // No Linux, os atalhos .desktop precisam de um arquivo de ícone de verdade
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                using var source = AssetLoader.Open(new Uri("avares://NokyLabs/Assets/nokylabs.png"));
                using var target = File.Create(Path.Combine(installFolder, "nokylabs.png"));
                source.CopyTo(target);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[NokyLabs] Ícone não copiado: {ex.Message}");
            }
        }
    }

    /// <summary>Instalação limpa: apaga apps gerenciados, atalhos, inicialização, registro e configurações.</summary>
    private static async Task CleanPreviousDataAsync(Action<string, double, bool> log)
    {
        var oldSettings = SettingsService.Load(AppPaths.InstalledConfigFolder);
        var oldApps = string.IsNullOrWhiteSpace(oldSettings.SourceFolder)
            ? AppPaths.DefaultSourceFolderFor(false, AppPaths.ExeFolder)
            : oldSettings.SourceFolder;

        foreach (var folder in InstallService.FindManagedAppFolders(oldApps))
        {
            try
            {
                await InstallService.RemoveAppFolderAsync(folder, Path.GetFileName(folder));
            }
            catch (Exception ex)
            {
                log($"Não removi {Path.GetFileName(folder)}: {ex.Message}", -1, true);
            }
        }

        ShortcutService.Remove(LoadInstallInfo()?.Shortcuts);
        TryRun(() => StartupService.SetEnabled(false), (m, p, w) => log(m, p, w));
        SystemRegistrationService.Unregister();

        foreach (var file in new[] { "settings.json", "catalogo-cache.json", "install.json" })
        {
            try { File.Delete(Path.Combine(AppPaths.InstalledConfigFolder, file)); }
            catch (Exception) { /* já não existia */ }
        }
    }

    // ------------------------------------------------------------------ desinstalar

    /// <summary>Remove o NokyLabs deste computador.</summary>
    /// <param name="removeApps">Apagar também os apps instalados pelo launcher.</param>
    /// <param name="removeData">Apagar configurações e cache.</param>
    public static async Task UninstallAsync(bool removeApps, bool removeData, IProgress<SetupProgress> progress)
    {
        var simulate = IsSimulation;
        void Log(string message, double percent = -1, bool warning = false) =>
            progress.Report(new SetupProgress(simulate ? $"(simulação) {message}" : message, percent, warning));

        var info = LoadInstallInfo();
        var settings = SettingsService.Load(AppPaths.InstalledConfigFolder);
        var appsFolder = string.IsNullOrWhiteSpace(settings.SourceFolder)
            ? AppPaths.DefaultSourceFolderFor(false, AppPaths.ExeFolder)
            : settings.SourceFolder;

        Log("Removendo atalhos do NokyLabs", 10);
        if (!simulate) ShortcutService.Remove(info?.Shortcuts);
        await Task.Delay(simulate ? 400 : 0);

        Log("Desligando o início automático", 20);
        if (!simulate) TryRun(() => StartupService.SetEnabled(false), Log);

        if (SystemRegistrationService.IsSupported)
        {
            Log("Removendo de Aplicativos instalados", 30);
            if (!simulate) SystemRegistrationService.Unregister();
        }
        await Task.Delay(simulate ? 400 : 0);

        if (removeApps)
        {
            var folders = InstallService.FindManagedAppFolders(appsFolder).ToList();
            for (var i = 0; i < folders.Count; i++)
            {
                var name = Path.GetFileName(folders[i]);
                Log($"Removendo o app {name}", 35 + 40.0 * i / Math.Max(1, folders.Count));
                if (simulate) { await Task.Delay(300); continue; }
                try
                {
                    await InstallService.RemoveAppFolderAsync(folders[i], name);
                }
                catch (Exception ex)
                {
                    Log(ex.Message, -1, warning: true);
                }
            }
        }

        Log(removeData ? "Apagando configurações e cache" : "Mantendo suas configurações para uma próxima instalação", 80);
        if (!simulate)
        {
            var files = removeData
                ? new[] { "settings.json", "catalogo-cache.json", "install.json" }
                : new[] { "install.json" };
            foreach (var file in files)
            {
                try { File.Delete(Path.Combine(AppPaths.InstalledConfigFolder, file)); }
                catch (Exception) { /* já não existia */ }
            }
            TryDeleteIfEmpty(AppPaths.InstalledConfigFolder);
        }
        await Task.Delay(simulate ? 400 : 0);

        Log("Removendo os arquivos do NokyLabs", 92);
        if (!simulate && info is not null) ScheduleLauncherRemoval(info);

        Log("O NokyLabs foi removido deste computador.", 100);
    }

    /// <summary>
    /// O executável que está rodando não pode se apagar. No Windows, um "cmd" escondido espera
    /// o launcher fechar e apaga os arquivos dele. No Linux, dá para apagar na hora.
    /// Só apaga o que o próprio assistente criou (o executável, o ícone e a pasta, se ficar vazia).
    /// </summary>
    private static void ScheduleLauncherRemoval(InstallInfo info)
    {
        var folder = info.InstallFolder;
        var exe = info.ExePath;
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(exe)) return;

        var icon = Path.Combine(folder, "nokylabs.png");

        if (OperatingSystem.IsWindows())
        {
            var command =
                "/c timeout /t 3 /nobreak >nul" +
                $" & del /f /q \"{exe}\"" +
                $" & del /f /q \"{exe}.old\"" +
                $" & del /f /q \"{icon}\"" +
                $" & rmdir \"{folder}\"";   // sem /s: só some se estiver vazia

            Process.Start(new ProcessStartInfo("cmd.exe", command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            return;
        }

        foreach (var file in new[] { exe, exe + ".old", icon })
        {
            try { File.Delete(file); } catch (Exception) { /* já não existia */ }
        }
        TryDeleteIfEmpty(folder);
    }

    // ------------------------------------------------------------------ auxiliares

    private static void TryAdd(List<string> list, Func<string?> create, Action<string, double, bool> log)
    {
        try
        {
            if (create() is { } path) list.Add(path);
        }
        catch (Exception ex)
        {
            log($"Atalho não criado: {ex.Message}", -1, true);
        }
    }

    private static void TryRun(Action action, Action<string, double, bool> log)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            log(ex.Message, -1, true);
        }
    }

    private static void TryDeleteIfEmpty(string folder)
    {
        try
        {
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                Directory.Delete(folder);
        }
        catch (Exception) { /* não é importante */ }
    }
}
