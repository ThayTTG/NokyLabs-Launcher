using System.Text.Json;
using NokyLabs.Models;

namespace NokyLabs.Services;

public static class SettingsService
{
    /// <param name="configFolder">Pasta de configurações; vazio = a do modo atual.</param>
    public static LauncherSettings Load(string? configFolder = null)
    {
        var file = FileIn(configFolder);
        try
        {
            if (File.Exists(file))
            {
                var settings = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(file), JsonDefaults.Options);
                if (settings is not null) return Migrate(settings, File.ReadAllText(file));
            }
        }
        catch (Exception ex)
        {
            // Arquivo corrompido não pode impedir o launcher de abrir: volta para o padrão.
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] settings.json ignorado: {ex.Message}");
        }

        return new LauncherSettings();
    }

    /// <param name="configFolder">Pasta de configurações; vazio = a do modo atual.</param>
    public static void Save(LauncherSettings settings, string? configFolder = null)
    {
        var file = FileIn(configFolder);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);

            // Grava num arquivo temporário e depois troca: se o PC desligar no meio, o antigo continua inteiro.
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonDefaults.Options));
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] Não foi possível salvar settings.json: {ex.Message}");
        }
    }

    private static string FileIn(string? configFolder) =>
        string.IsNullOrWhiteSpace(configFolder)
            ? AppPaths.SettingsFile
            : Path.Combine(configFolder, "settings.json");

    /// <summary>
    /// Configurações salvas antes da v0.6 tinham o catálogo vazio por padrão:
    /// elas passam a usar o catálogo oficial do site (quem colocou outro link mantém o seu).
    /// </summary>
    private static LauncherSettings Migrate(LauncherSettings settings, string rawJson)
    {
        var hadVersion = rawJson.Contains("settingsVersion", StringComparison.OrdinalIgnoreCase);
        if (!hadVersion || settings.SettingsVersion < 2)
        {
            if (string.IsNullOrWhiteSpace(settings.CatalogUrl))
                settings.CatalogUrl = LauncherSettings.DefaultCatalogUrl;
            settings.SettingsVersion = LauncherSettings.CurrentSettingsVersion;
        }
        return settings;
    }
}
