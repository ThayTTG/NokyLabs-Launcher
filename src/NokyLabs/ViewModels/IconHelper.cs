using Avalonia.Media;
using Avalonia.Media.Immutable;
using NokyLabs.Services;
using NokyLabs.UI;

namespace NokyLabs.ViewModels;

/// <summary>Converte as cores e ícones do catálogo (texto) em objetos de desenho.</summary>
public static class IconHelper
{
    private static readonly Geometry FallbackIcon = Geometry.Parse("M4,4 H20 V20 H4 Z");
    private static readonly Color FallbackColor = Color.Parse("#A99FC7");

    public static Geometry ParseIcon(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return FallbackIcon;
        try { return Geometry.Parse(data); }
        catch (Exception) { return FallbackIcon; }
    }

    public static Color ParseColor(string? hex) =>
        !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex, out var color) ? color : FallbackColor;

    public static IBrush AccentBrush(string? hex, double opacity = 1) =>
        new ImmutableSolidColorBrush(ParseColor(hex), opacity);

    /// <summary>
    /// A cor de um app pronta para desenhar ícone ou texto no tema atual: no claro, as cores
    /// claras (menta, verde-água…) escurecem até dar leitura; no escuro, as escuras clareiam.
    /// É a mesma conta da NokyLabs.UI (AccentPalette).
    /// </summary>
    public static Color ReadableColor(Color accent, bool light) => light
        ? AccentPalette.Readable(accent, AccentPalette.LightSurface, Colors.Black)
        : AccentPalette.Readable(accent, AccentPalette.DarkSurface, Colors.White);

    public static IBrush ReadableBrush(Color accent) =>
        new ImmutableSolidColorBrush(ReadableColor(accent, ThemeService.IsLight));
}
