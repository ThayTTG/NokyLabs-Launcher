using System.Text.Json;
using NokyLabs.Models;
using NokyLabs.Services;
using Xunit;

namespace NokyLabs.Tests;

/// <summary>Apps de uso restrito: o catálogo marca com "requestAccess".</summary>
public sealed class CatalogoTests
{
    [Fact]
    public void App_com_requestAccess_e_restrito()
    {
        const string json = """
            { "apps": [
              { "id": "avaliador", "name": "Avaliador Acadêmico",
                "requestAccess": "https://exemplo.com/pedido", "requestAction": "Pedir acesso" },
              { "id": "hotswap", "name": "HotSwap" }
            ] }
            """;
        var cat = JsonSerializer.Deserialize<CatalogFile>(json, JsonDefaults.Options)!;

        Assert.True(cat.Apps[0].IsRestricted);
        Assert.Equal("Pedir acesso", cat.Apps[0].RequestAction);
        Assert.False(cat.Apps[1].IsRestricted);
    }
}
