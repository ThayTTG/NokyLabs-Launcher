using System.Text.Json;
using Avalonia.Platform;
using NokyLabs.Models;

namespace NokyLabs.Services;

/// <summary>De onde veio o catálogo que está na tela.</summary>
public enum CatalogSource
{
    Embedded,   // o catalogo.json que vai dentro do launcher
    Cache,      // a última cópia online que deu certo
    Online      // baixado agora há pouco
}

public sealed record CatalogResult(CatalogFile Catalog, CatalogSource Source);

/// <summary>
/// Carrega o catálogo. Ordem de preferência:
///   1. online (quando há um link configurado e a internet colabora)
///   2. cópia em cache da última vez que o online funcionou
///   3. catálogo embutido (Assets/catalogo.json), que sempre existe
/// </summary>
public static class CatalogService
{
    private static readonly Uri EmbeddedUri = new("avares://NokyLabs/Assets/catalogo.json");

    /// <summary>Carrega sem internet (rápido, usado ao abrir o launcher).</summary>
    public static CatalogResult LoadLocal(string catalogUrl)
    {
        // Só usa o cache se houver um catálogo online configurado;
        // sem link, o embutido manda (facilita testar mudanças no Assets/catalogo.json).
        if (!string.IsNullOrWhiteSpace(catalogUrl) && File.Exists(AppPaths.CatalogCacheFile))
        {
            try
            {
                return new CatalogResult(Parse(File.ReadAllText(AppPaths.CatalogCacheFile)), CatalogSource.Cache);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[NokyLabs] Cache do catálogo ignorado: {ex.Message}");
            }
        }

        return new CatalogResult(LoadEmbedded(), CatalogSource.Embedded);
    }

    /// <summary>Baixa o catálogo do link configurado e guarda uma cópia em cache.</summary>
    public static async Task<CatalogResult> FetchOnlineAsync(string catalogUrl, CancellationToken ct = default)
    {
        string text;
        try
        {
            text = HttpHelper.IsWebUrl(catalogUrl)
                ? await HttpHelper.Client.GetStringAsync(catalogUrl, ct)
                : await File.ReadAllTextAsync(HttpHelper.ToLocalPath(catalogUrl), ct);
        }
        catch (HttpRequestException ex)
        {
            throw new LauncherException($"Não consegui baixar o catálogo ({ex.StatusCode?.ToString() ?? "sem conexão"}).");
        }
        catch (TaskCanceledException)
        {
            throw new LauncherException("O servidor do catálogo demorou demais para responder.");
        }
        catch (IOException)
        {
            throw new LauncherException("Não encontrei o arquivo do catálogo nesse caminho.");
        }

        CatalogFile catalog;
        try
        {
            catalog = Parse(text);
        }
        catch (JsonException ex)
        {
            throw new LauncherException($"O catalogo.json tem um erro de formatação (linha {ex.LineNumber + 1}).");
        }

        try
        {
            Directory.CreateDirectory(AppPaths.ConfigFolder);
            await File.WriteAllTextAsync(AppPaths.CatalogCacheFile, text, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] Não foi possível salvar o cache do catálogo: {ex.Message}");
        }

        return new CatalogResult(catalog, CatalogSource.Online);
    }

    private static CatalogFile LoadEmbedded()
    {
        try
        {
            using var stream = AssetLoader.Open(EmbeddedUri);
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[NokyLabs] Catálogo embutido inválido: {ex.Message}");
            return new CatalogFile();
        }
    }

    private static CatalogFile Parse(string json)
    {
        var catalog = JsonSerializer.Deserialize<CatalogFile>(json, JsonDefaults.Options)
                      ?? throw new JsonException("Catálogo vazio.");

        // O id vira nome de pasta, então só aceita letras, números, "-" e "_"
        // (nada de "..", barras ou espaços que poderiam escapar da pasta de origem).
        catalog.Apps = catalog.Apps
            .Where(a => IsSafeId(a.Id))
            .ToList();

        return catalog;
    }

    private static bool IsSafeId(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Length <= 64 &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
}
