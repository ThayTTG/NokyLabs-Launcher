using NokyLabs.Models;
using NokyLabs.Services;
using Xunit;

namespace NokyLabs.Tests;

/// <summary>
/// Garante que atualizar um app pelo launcher NÃO apaga o que é do usuário
/// (ex.: config_alunos.json do Avaliador, filtro-conteudo.json do YT-DLP GUI).
/// </summary>
public sealed class AtualizacaoTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "nokylabs-testes", Guid.NewGuid().ToString("N"));
    private string Antiga => Path.Combine(_raiz, "antiga");
    private string Nova => Path.Combine(_raiz, "nova");

    public AtualizacaoTests()
    {
        Directory.CreateDirectory(Antiga);
        Directory.CreateDirectory(Nova);
    }

    public void Dispose()
    {
        try { Directory.Delete(_raiz, recursive: true); } catch { }
    }

    private static void Escrever(string pasta, string relativo, string conteudo = "x")
    {
        var caminho = Path.Combine(pasta, relativo.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        File.WriteAllText(caminho, conteudo);
    }

    private string Ler(string relativo) =>
        File.ReadAllText(Path.Combine(Nova, relativo.Replace('/', Path.DirectorySeparatorChar)));

    private bool ExisteNaNova(string relativo) =>
        File.Exists(Path.Combine(Nova, relativo.Replace('/', Path.DirectorySeparatorChar)));

    [Fact]
    public void Instalacao_antiga_sem_lista_leva_dados_e_deixa_programas()
    {
        Escrever(Antiga, "App.exe");
        Escrever(Antiga, "biblioteca.dll");
        Escrever(Antiga, "config_alunos.json", "notas da turma");
        Escrever(Antiga, "logs/hoje.txt");
        Escrever(Antiga, InstallManifest.FileName);
        Escrever(Nova, "App.exe", "nova");

        var anterior = new InstallManifest { Executable = "App.exe" };   // sem PackageFiles (launcher ≤ 0.6)
        var levados = InstallService.CarryOverUserFiles(Antiga, Nova, anterior);

        Assert.Equal(2, levados);
        Assert.Equal("notas da turma", Ler("config_alunos.json"));
        Assert.True(ExisteNaNova("logs/hoje.txt"));
        Assert.False(ExisteNaNova("biblioteca.dll"));
        Assert.Equal("nova", Ler("App.exe"));
        Assert.False(ExisteNaNova(InstallManifest.FileName));
    }

    [Fact]
    public void Arquivo_que_veio_no_pacote_antigo_e_substituido_pelo_novo()
    {
        Escrever(Antiga, "App.exe");
        Escrever(Antiga, "padrao.json", "antigo");
        Escrever(Antiga, "meu.json", "do usuário");
        Escrever(Nova, "App.exe");
        Escrever(Nova, "padrao.json", "novo");

        var anterior = new InstallManifest
        {
            Executable = "App.exe",
            PackageFiles = new() { "App.exe", "padrao.json" }
        };
        InstallService.CarryOverUserFiles(Antiga, Nova, anterior);

        Assert.Equal("novo", Ler("padrao.json"));
        Assert.Equal("do usuário", Ler("meu.json"));
    }

    [Fact]
    public void Arquivo_do_usuario_vence_quando_o_pacote_novo_traz_um_igual()
    {
        Escrever(Antiga, "App.exe");
        Escrever(Antiga, "filtro-conteudo.json", "configurado pela professora");
        Escrever(Nova, "App.exe");
        Escrever(Nova, "filtro-conteudo.json", "padrão");

        var anterior = new InstallManifest { Executable = "App.exe", PackageFiles = new() { "App.exe" } };
        InstallService.CarryOverUserFiles(Antiga, Nova, anterior);

        Assert.Equal("configurado pela professora", Ler("filtro-conteudo.json"));
    }

    [Fact]
    public void Executavel_renomeado_nao_volta_para_a_pasta_nova()
    {
        Escrever(Antiga, "CubeBin.App.exe");
        Escrever(Antiga, "CubeBin.App.runtimeconfig.json");
        Escrever(Nova, "CubeBin.exe");

        InstallService.CarryOverUserFiles(Antiga, Nova, new InstallManifest { Executable = "CubeBin.App.exe" });

        Assert.False(ExisteNaNova("CubeBin.App.exe"));
        Assert.False(ExisteNaNova("CubeBin.App.runtimeconfig.json"));
    }

    [Fact]
    public void Lista_do_pacote_ignora_o_manifesto_e_usa_barras_normais()
    {
        Escrever(Nova, "App.exe");
        Escrever(Nova, "Assets/icone.png");
        Escrever(Nova, InstallManifest.FileName);

        var lista = InstallService.ListRelativeFiles(Nova);

        Assert.Contains("Assets/icone.png", lista);
        Assert.Contains("App.exe", lista);
        Assert.DoesNotContain(InstallManifest.FileName, lista);
    }

    [Theory]
    [InlineData("- Visual novo\n- Correção no download", "•  Visual novo\n•  Correção no download")]
    [InlineData("## Novidades\n* **Mais rápido**", "•  Mais rápido")]
    [InlineData("", "")]
    public void Notas_do_changelog_viram_texto_de_tela(string entrada, string esperado)
    {
        Assert.Equal(esperado, NotesHelper.Format(entrada));
    }
}
