using NokyLabs.Models;

namespace NokyLabs.ViewModels;

/// <summary>A cor do selo de situação de um app (as classes de selo do Kit NokyLabs).</summary>
public enum StatusTone
{
    /// <summary>Selo cinza: "Em breve", "Indisponível".</summary>
    Neutral,
    /// <summary>Verde: instalado ou aberto agora.</summary>
    Success,
    /// <summary>Âmbar: tem atualização.</summary>
    Warning,
    /// <summary>Cor do launcher: disponível para instalar.</summary>
    Accent,
    /// <summary>Azul-céu: no navegador ou acesso restrito.</summary>
    Info
}

public static class StatusTones
{
    public static StatusTone For(AppStatus status, bool running) => running ? StatusTone.Success : status switch
    {
        AppStatus.Installed => StatusTone.Success,
        AppStatus.UpdateAvailable => StatusTone.Warning,
        AppStatus.NotInstalled => StatusTone.Accent,
        AppStatus.Web or AppStatus.Restricted => StatusTone.Info,
        _ => StatusTone.Neutral
    };
}
