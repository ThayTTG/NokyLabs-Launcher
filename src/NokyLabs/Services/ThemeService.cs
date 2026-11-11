using Avalonia;
using Avalonia.Styling;

namespace NokyLabs.Services;

/// <summary>
/// Tema do launcher (Kit NokyLabs): escuro (o principal), claro ou igual ao sistema.
/// A escolha fica no settings.json, em "theme".
/// </summary>
public static class ThemeService
{
    public const string Dark = "escuro";
    public const string Light = "claro";
    public const string System = "sistema";

    /// <summary>Qualquer valor desconhecido (ou de uma versão antiga) vira o escuro, que era o único até a v0.8.</summary>
    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Light => Light,
        System => System,
        _ => Dark
    };

    public static ThemeVariant VariantFor(string? value) => Normalize(value) switch
    {
        Light => ThemeVariant.Light,
        System => ThemeVariant.Default,   // o Avalonia segue o tema do Windows/Linux
        _ => ThemeVariant.Dark
    };

    public static void Apply(string? value)
    {
        if (Application.Current is { } app) app.RequestedThemeVariant = VariantFor(value);
    }

    /// <summary>O tema que está na tela agora (já resolvendo o "igual ao sistema").</summary>
    public static bool IsLight => Application.Current?.ActualThemeVariant == ThemeVariant.Light;
}
