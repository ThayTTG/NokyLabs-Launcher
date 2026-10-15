using System.Text.Json;

namespace NokyLabs.Services;

/// <summary>Configuração única de JSON para todo o launcher.</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };
}

/// <summary>
/// Um HttpClient compartilhado (criar um por requisição esgota as conexões).
/// O GitHub exige um User-Agent, por isso ele é definido aqui.
/// </summary>
public static class HttpHelper
{
    public static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NokyLabs-Launcher/0.7");
        return client;
    }

    /// <summary>True se o texto for um link http/https (e não um caminho no disco).</summary>
    public static bool IsWebUrl(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Aceita "C:\pasta\arquivo.zip", "/home/x/arquivo.zip" ou "file:///...".</summary>
    public static string ToLocalPath(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.IsFile ? uri.LocalPath : text;
}

/// <summary>Informações sobre o sistema em que o launcher está rodando.</summary>
public static class PlatformInfo
{
    /// <summary>Chave usada no "downloads" do catálogo.</summary>
    public static string Key =>
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsMacOS() ? "macos" :
        "linux";

    public static string DisplayName =>
        OperatingSystem.IsWindows() ? "Windows" :
        OperatingSystem.IsMacOS() ? "macOS" :
        "Linux";
}

/// <summary>Compara versões como "1.2.0" e "v1.10".</summary>
public static class VersionHelper
{
    public const string Unknown = "?";

    /// <summary>True se <paramref name="latest"/> for mais nova que <paramref name="installed"/>.</summary>
    public static bool IsNewer(string latest, string? installed)
    {
        if (string.IsNullOrWhiteSpace(installed) || installed == Unknown) return false;

        if (Version.TryParse(Clean(latest), out var a) && Version.TryParse(Clean(installed), out var b))
            return a > b;

        // Se não der para entender como número, qualquer diferença conta como atualização
        return !string.Equals(Clean(latest), Clean(installed), StringComparison.OrdinalIgnoreCase);
    }

    private static string Clean(string v) => v.Trim().TrimStart('v', 'V');
}

/// <summary>Transforma as notas do CHANGELOG (Markdown simples) em texto para a tela.</summary>
public static class NotesHelper
{
    public static string Format(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return string.Empty;

        var lines = notes.Replace("\r\n", "\n").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .Select(l => l.StartsWith("- ") || l.StartsWith("* ") ? "•  " + l[2..].Trim() : l)
            .Select(l => l.Replace("**", "").Replace("`", ""));
        return string.Join("\n", lines);
    }
}

/// <summary>Erro "esperado", com uma mensagem amigável para mostrar na tela.</summary>
public sealed class LauncherException(string message) : Exception(message);
