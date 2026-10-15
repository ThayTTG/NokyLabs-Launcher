namespace NokyLabs.Models;

/// <summary>
/// O arquivo catalogo.json inteiro.
/// </summary>
public sealed class CatalogFile
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Versão mais recente do próprio launcher (opcional).</summary>
    public LauncherInfo? Launcher { get; set; }

    public List<CatalogApp> Apps { get; set; } = new();
}

/// <summary>
/// Informações para o launcher se atualizar sozinho.
/// O download deve ser o executável publicado como arquivo único (ou um .zip com ele dentro).
/// </summary>
public sealed class LauncherInfo
{
    public string Version { get; set; } = "0.0.0";

    /// <summary>Texto curto com as novidades, mostrado no aviso de atualização.</summary>
    public string Notes { get; set; } = string.Empty;

    public Dictionary<string, CatalogDownload?> Downloads { get; set; } = new();
}

/// <summary>
/// Um app dentro do catálogo.
/// </summary>
public sealed class CatalogApp
{
    /// <summary>Identificador único. Também é o nome da subpasta dentro da pasta de origem.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Versão mais recente publicada (ex.: "1.3.0").</summary>
    public string Version { get; set; } = "0.0.0";

    /// <summary>Novidades da versão mais recente (vêm do CHANGELOG do app, pelo robô do catálogo).</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Cor de destaque do card, em hexadecimal (ex.: "#818CF8").</summary>
    public string Accent { get; set; } = "#A99FC7";

    /// <summary>Ícone em "path data" (o mesmo formato do Data de um Path no XAML), numa grade 24x24.</summary>
    public string Icon { get; set; } = string.Empty;

    public bool ComingSoon { get; set; }

    /// <summary>
    /// Opcional: link de um app que roda no navegador (ex.: um jogo no itch.io).
    /// Quando preenchido, o card mostra "Jogar"/"Abrir no navegador" em vez de instalar.
    /// </summary>
    public string? Web { get; set; }

    /// <summary>Texto do botão para apps web (padrão: "Abrir no navegador").</summary>
    public string? WebAction { get; set; }

    /// <summary>
    /// Downloads por sistema. Chaves: "windows", "linux", "macos".
    /// Se o sistema não aparece (ou está null), o app é mostrado como indisponível nele.
    /// </summary>
    public Dictionary<string, CatalogDownload?> Downloads { get; set; } = new();
}

/// <summary>
/// De onde baixar o app em um sistema específico.
/// </summary>
public sealed class CatalogDownload
{
    /// <summary>
    /// Link do .zip (ou do executável solto). Aceita também um caminho local,
    /// útil para testar instalações antes de publicar nos Releases do GitHub.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Nome do executável depois de extraído (ex.: "HotSwap.exe").</summary>
    public string Executable { get; set; } = string.Empty;

    /// <summary>Opcional: hash SHA-256 do arquivo baixado, para conferir se chegou inteiro.</summary>
    public string? Sha256 { get; set; }

    /// <summary>Opcional: tamanho do arquivo em bytes (preenchido pelo robô do catálogo).</summary>
    public long? Size { get; set; }

    /// <summary>
    /// False enquanto o arquivo ainda não foi publicado nos Releases.
    /// Assim o link real já fica no catálogo, e o launcher mostra "Download em breve" até virar true.
    /// </summary>
    public bool Published { get; set; } = true;

    /// <summary>True enquanto o download ainda não pode ser usado.</summary>
    public bool IsPlaceholder =>
        !Published ||
        string.IsNullOrWhiteSpace(Url) ||
        Url.Contains("SEU-USUARIO", StringComparison.OrdinalIgnoreCase);
}
