using System.Text.Json;
using NokyLabs.Models;
using NokyLabs.Services;
using Xunit;

namespace NokyLabs.Tests;

/// <summary>Categorias da Biblioteca: as mesmas do site (Apps, Educação, Jogos).</summary>
public sealed class CategoriasTests
{
    private static CatalogFile Ler(string json) => JsonSerializer.Deserialize<CatalogFile>(json, JsonDefaults.Options)!;

    [Fact]
    public void Usa_o_campo_category_do_catalogo()
    {
        var cat = Ler("""
            { "apps": [
              { "id": "x", "name": "X", "category": "jogos" },
              { "id": "y", "name": "Y", "category": "Educacao" }
            ] }
            """);

        Assert.Equal(AppCategories.Jogos, AppCategories.Guess(cat.Apps[0]));
        Assert.Equal(AppCategories.Educacao, AppCategories.Guess(cat.Apps[1]));
    }

    [Fact]
    public void Catalogo_antigo_sem_category_deduz_pelo_id()
    {
        var cat = Ler("""
            { "apps": [
              { "id": "avaliador", "name": "Avaliador" },
              { "id": "blockable", "name": "Blockable" },
              { "id": "hotswap", "name": "HotSwap" },
              { "id": "desconhecido", "name": "?", "category": "inventada" }
            ] }
            """);

        Assert.Equal(AppCategories.Educacao, AppCategories.Guess(cat.Apps[0]));
        Assert.Equal(AppCategories.Jogos, AppCategories.Guess(cat.Apps[1]));
        Assert.Equal(AppCategories.Apps, AppCategories.Guess(cat.Apps[2]));
        Assert.Equal(AppCategories.Apps, AppCategories.Guess(cat.Apps[3]));
    }

    [Fact]
    public void Catalogo_embutido_tem_categoria_em_todos_os_apps()
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "NokyLabs", "Assets", "catalogo.json");
        var cat = Ler(File.ReadAllText(caminho));

        Assert.All(cat.Apps, a => Assert.Contains(a.Category, AppCategories.Order));
    }
}
