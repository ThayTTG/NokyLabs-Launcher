namespace NokyLabs.Models;

/// <summary>
/// As categorias do catálogo, iguais às do site da NokyLabs (Apps, Educação, Jogos).
/// </summary>
public static class AppCategories
{
    public const string Apps = "apps";
    public const string Educacao = "educacao";
    public const string Jogos = "jogos";

    /// <summary>Ordem em que as faixas aparecem na Biblioteca.</summary>
    public static readonly IReadOnlyList<string> Order = new[] { Apps, Educacao, Jogos };

    public static string Title(string id) => id switch
    {
        Educacao => "Educação",
        Jogos => "Jogos",
        _ => "Apps"
    };

    public static string Subtitle(string id) => id switch
    {
        Educacao => "Para a sala de aula",
        Jogos => "Para jogar e para quem joga",
        _ => "Ferramentas para o dia a dia no computador"
    };

    /// <summary>
    /// Categoria de um app. Usa o campo "category" do catálogo; num catálogo antigo, sem o campo,
    /// deduz pelo id dos apps conhecidos (e manda o resto para Apps).
    /// </summary>
    public static string Guess(CatalogApp app)
    {
        var c = (app.Category ?? string.Empty).Trim().ToLowerInvariant();
        if (c is Apps or Educacao or Jogos) return c;
        if (c is "educação") return Educacao;

        return app.Id switch
        {
            "avaliador" => Educacao,
            "blockable" or "valreport" => Jogos,
            _ => Apps
        };
    }
}
