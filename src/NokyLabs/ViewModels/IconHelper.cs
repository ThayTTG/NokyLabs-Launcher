using Avalonia.Media;
using Avalonia.Media.Immutable;

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
}
