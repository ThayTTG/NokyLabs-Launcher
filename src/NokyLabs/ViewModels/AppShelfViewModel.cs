using System.Collections.ObjectModel;
using NokyLabs.Models;

namespace NokyLabs.ViewModels;

/// <summary>
/// Uma faixa da Biblioteca: o título da categoria e os cards dela, lado a lado (rolam para a direita).
/// </summary>
public sealed class AppShelfViewModel
{
    public AppShelfViewModel(string category) => Category = category;

    public string Category { get; }
    public string Title => AppCategories.Title(Category);
    public string Subtitle => AppCategories.Subtitle(Category);
    public ObservableCollection<AppCardViewModel> Apps { get; } = new();
    public string CountText => Apps.Count == 1 ? "1 app" : $"{Apps.Count} apps";
}
