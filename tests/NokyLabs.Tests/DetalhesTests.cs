using System.Text.Json;
using NokyLabs.Models;
using NokyLabs.Services;
using NokyLabs.ViewModels;
using Xunit;

namespace NokyLabs.Tests;

/// <summary>Painel de detalhes: o sistema atual instala; os outros só baixam.</summary>
public sealed class DetalhesTests
{
    /// <summary>As linhas como apareceriam num computador com Windows.</summary>
    private static IReadOnlyList<PlatformRowViewModel> Linhas(string json, string sistema = "windows")
    {
        var app = JsonSerializer.Deserialize<CatalogApp>(json, JsonDefaults.Options)!;
        return PlatformRows.Build(app, sistema, isInstalled: false, _ => { });
    }

    private const string DoisSistemas = """
        { "id": "x", "name": "X", "version": "1.0.0", "downloads": {
            "windows": { "url": "https://exemplo.com/x-win.zip", "executable": "X.exe", "size": 2097152 },
            "linux":   { "url": "https://exemplo.com/x-linux.zip", "executable": "X", "size": 1048576 } } }
        """;

    [Fact]
    public void Sistema_atual_vem_primeiro_e_nao_tem_botao_de_download()
    {
        var linhas = Linhas(DoisSistemas);

        Assert.Equal("windows", linhas[0].Key);
        Assert.True(linhas[0].IsCurrent);
        Assert.False(linhas[0].CanDownload);
        Assert.Equal("Instalação disponível", linhas[0].StatusText);
    }

    [Fact]
    public void Outro_sistema_mostra_apenas_download()
    {
        var outro = Linhas(DoisSistemas).Single(l => !l.IsCurrent);

        Assert.Equal("linux", outro.Key);

        Assert.Equal("Apenas download", outro.StatusText);
        Assert.True(outro.CanDownload);
        Assert.StartsWith("Baixar · ", outro.DownloadText);
    }

    [Fact]
    public void App_restrito_nunca_oferece_download()
    {
        var linhas = Linhas("""
            { "id": "r", "name": "R", "version": "1.0.0", "requestAccess": "https://exemplo.com/pedido",
              "downloads": { "windows": { "url": "https://exemplo.com/r.zip", "executable": "R.exe" },
                             "linux": { "url": "https://exemplo.com/r-linux.zip", "executable": "R" } } }
            """);

        Assert.All(linhas, l => Assert.False(l.CanDownload));
        Assert.All(linhas, l => Assert.Equal("Uso restrito: peça acesso", l.StatusText));
    }

    [Fact]
    public void App_restrito_com_versao_nao_publicada_continua_uso_restrito()
    {
        var linhas = Linhas("""
            { "id": "avaliador", "name": "Avaliador", "version": "2.0.0", "requestAccess": "mailto:x@exemplo.com",
              "downloads": { "windows": { "url": "https://exemplo.com/a.zip", "executable": "A.exe", "published": false },
                             "linux": null } }
            """);

        Assert.Equal("Uso restrito: peça acesso", linhas.Single(l => l.Key == "windows").StatusText);
        Assert.Equal("Sem versão", linhas.Single(l => l.Key == "linux").StatusText);
    }

    [Fact]
    public void Sistema_sem_versao_aparece_como_sem_versao()
    {
        var linhas = Linhas("""
            { "id": "w", "name": "W", "version": "1.0.0",
              "downloads": { "windows": { "url": "https://exemplo.com/w.zip", "executable": "W.exe" }, "linux": null } }
            """);

        var linux = linhas.Single(l => l.Key == "linux");
        Assert.Equal("Sem versão", linux.StatusText);
        Assert.False(linux.CanDownload);
    }
}
