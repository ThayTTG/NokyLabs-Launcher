using System.Text.Json;
using Avalonia.Media;
using Avalonia.Styling;
using NokyLabs.Models;
using NokyLabs.Services;
using NokyLabs.UI;
using NokyLabs.ViewModels;
using Xunit;

namespace NokyLabs.Tests;

/// <summary>Kit NokyLabs no launcher: tema escolhido e cores dos apps legíveis nos dois temas.</summary>
public sealed class AparenciaTests
{
    [Fact]
    public void Configuracao_antiga_sem_tema_continua_no_escuro()
    {
        // settings.json de antes da v0.9 não tem "theme"
        var antigo = JsonSerializer.Deserialize<LauncherSettings>("""{ "minimizeToTray": true }""", JsonDefaults.Options)!;

        Assert.Equal(ThemeService.Dark, ThemeService.Normalize(antigo.Theme));
        Assert.Equal(ThemeVariant.Dark, ThemeService.VariantFor(antigo.Theme));
    }

    [Theory]
    [InlineData("escuro", "escuro")]
    [InlineData("claro", "claro")]
    [InlineData("Sistema", "sistema")]
    [InlineData(" CLARO ", "claro")]
    [InlineData("", "escuro")]
    [InlineData(null, "escuro")]
    [InlineData("roxo", "escuro")]
    public void Valor_do_tema_e_lido_com_tolerancia(string? salvo, string esperado) =>
        Assert.Equal(esperado, ThemeService.Normalize(salvo));

    [Fact]
    public void Cada_escolha_vira_o_tema_certo_do_Avalonia()
    {
        Assert.Equal(ThemeVariant.Dark, ThemeService.VariantFor("escuro"));
        Assert.Equal(ThemeVariant.Light, ThemeService.VariantFor("claro"));
        Assert.Equal(ThemeVariant.Default, ThemeService.VariantFor("sistema"));
    }

    [Fact]
    public void Tema_escolhido_e_salvo_e_lido_de_volta()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "nokylabs-tema-" + Guid.NewGuid().ToString("N"));
        try
        {
            SettingsService.Save(new LauncherSettings { Theme = ThemeService.Light }, pasta);
            Assert.Equal(ThemeService.Light, SettingsService.Load(pasta).Theme);
        }
        finally
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
    }

    /// <summary>As cores aprovadas em 08/11 (e o cinza do "Próximo utilitário").</summary>
    public static TheoryData<string> CoresDosApps => new()
    {
        "#A068FF", "#6EE7B7", "#C4362E", "#3B82F6", "#818CF8", "#22C55E", "#F97316", "#2DD4BF", "#A99FC7",
        // cores que o catálogo ainda usa até a troca de 08/11 chegar ao site
        "#F472B6",
    };

    [Theory]
    [MemberData(nameof(CoresDosApps))]
    public void Icone_de_cada_app_fica_legivel_nos_dois_temas(string hex)
    {
        var cor = Color.Parse(hex);

        var noEscuro = IconHelper.ReadableColor(cor, light: false);
        var noClaro = IconHelper.ReadableColor(cor, light: true);

        Assert.True(AccentPalette.Contrast(noEscuro, AccentPalette.DarkSurface) >= 4.5, $"{hex} no escuro");
        Assert.True(AccentPalette.Contrast(noClaro, AccentPalette.LightSurface) >= 4.5, $"{hex} no claro");
    }

    [Fact]
    public void No_escuro_as_cores_que_ja_sao_legiveis_nao_mudam()
    {
        // O roxo da marca e a menta do YT-DLP já se leem bem no escuro: ficam idênticos
        Assert.Equal(Color.Parse("#A068FF"), IconHelper.ReadableColor(Color.Parse("#A068FF"), light: false));
        Assert.Equal(Color.Parse("#6EE7B7"), IconHelper.ReadableColor(Color.Parse("#6EE7B7"), light: false));
    }

    [Theory]
    [InlineData(AppStatus.Installed, false, StatusTone.Success)]
    [InlineData(AppStatus.UpdateAvailable, true, StatusTone.Success)]      // aberto agora: verde, mesmo com atualização
    [InlineData(AppStatus.UpdateAvailable, false, StatusTone.Warning)]
    [InlineData(AppStatus.NotInstalled, false, StatusTone.Accent)]
    [InlineData(AppStatus.Web, false, StatusTone.Info)]
    [InlineData(AppStatus.Restricted, false, StatusTone.Info)]
    [InlineData(AppStatus.ComingSoon, false, StatusTone.Neutral)]
    [InlineData(AppStatus.Unsupported, false, StatusTone.Neutral)]
    public void Selo_de_situacao_tem_a_cor_certa(AppStatus status, bool aberto, StatusTone esperado) =>
        Assert.Equal(esperado, StatusTones.For(status, aberto));
}
