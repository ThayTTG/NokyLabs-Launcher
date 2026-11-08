using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NokyLabs.Models;
using NokyLabs.Services;

namespace NokyLabs.ViewModels;

/// <summary>
/// Uma linha de "Plataformas" no painel de detalhes: o sistema, o que dá para fazer nele
/// e, para os outros sistemas, só o link de download (nunca a instalação).
/// </summary>
public sealed partial class PlatformRowViewModel : ObservableObject
{
    private readonly Action<string> _report;

    public PlatformRowViewModel(string key, string name, bool isCurrent, string statusText,
                                string? downloadUrl, long? size, Action<string> report)
    {
        Key = key;
        Name = name;
        IsCurrent = isCurrent;
        StatusText = statusText;
        DownloadUrl = downloadUrl;
        _report = report;
        SizeText = size is > 0 ? FormatSize(size.Value) : null;
    }

    public string Key { get; }
    public string Name { get; }
    public bool IsCurrent { get; }
    public string StatusText { get; }
    public string? DownloadUrl { get; }
    public string? SizeText { get; }

    /// <summary>Só os outros sistemas ganham o botão de download (no atual, quem instala é o launcher).</summary>
    public bool CanDownload => !IsCurrent && DownloadUrl is not null;
    public string DownloadText => SizeText is null ? "Baixar" : $"Baixar · {SizeText}";
    public string Title => IsCurrent ? $"{Name} · este computador" : Name;

    [RelayCommand]
    private void Download()
    {
        if (DownloadUrl is null) return;
        try { LaunchService.OpenUrl(DownloadUrl); }
        catch (LauncherException ex) { _report(ex.Message); }
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}

/// <summary>Monta as linhas de "Plataformas". Fica separado do card para dar para testar sem abrir janela.</summary>
public static class PlatformRows
{
    private static readonly string[] Known = { "windows", "linux", "macos" };

    public static IReadOnlyList<PlatformRowViewModel> Build(CatalogApp app, string currentKey, bool isInstalled,
                                                           Action<string> report) => Known
        // Windows e Linux sempre aparecem; macOS só se o catálogo tiver
        .Where(k => k == currentKey || k != "macos" || app.Downloads.ContainsKey(k))
        .OrderBy(k => k == currentKey ? 0 : 1)
        .Select(k =>
        {
            var name = k switch { "windows" => "Windows", "macos" => "macOS", _ => "Linux" };
            var current = k == currentKey;
            app.Downloads.TryGetValue(k, out var dl);
            var ready = dl is not null && !dl.IsPlaceholder;

            string status;
            if (app.IsRestricted && dl is not null)
                status = "Uso restrito: peça acesso"; // existe, mas não é baixado direto (nem publicado)
            else if (!ready)
                status = "Sem versão";
            else if (current)
                status = isInstalled ? "Instalado" : "Instalação disponível";
            else
                status = "Apenas download";

            // Download direto só para os OUTROS sistemas; no atual quem instala é o launcher
            var url = !current && ready && !app.IsRestricted ? dl!.Url : null;
            return new PlatformRowViewModel(k, name, current, status, url, dl?.Size, report);
        })
        .ToList();
}
