using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NokyLabs.Models;
using NokyLabs.ViewModels;

namespace NokyLabs.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // A janela "Instalar app" precisa de um dono para abrir como diálogo: quem empresta é esta janela
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
                vm.ShowInstallDialog = ShowInstallDialogAsync;
        };

        // Esc fecha o painel de detalhes
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape && DataContext is MainWindowViewModel { IsDetailsOpen: true } vm)
            {
                vm.CloseDetailsCommand.Execute(null);
                e.Handled = true;
            }
        };
    }

    // ------------------------------------------------------------------ painel de detalhes

    /// <summary>Clicar no card abre os detalhes; clicar nos botões do card continua fazendo só o que o botão faz.</summary>
    private void OnCardTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is not Control card || card.DataContext is not AppCardViewModel app) return;
        if (e.Source is Visual v && v.GetSelfAndVisualAncestors().TakeWhile(x => x != card).Any(x => x is Button))
            return;
        if (DataContext is MainWindowViewModel vm) vm.OpenDetailsCommand.Execute(app);
    }

    private void OnDetailsBackdropPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.CloseDetailsCommand.Execute(null);
    }

    private async Task<AppInstallChoices?> ShowInstallDialogAsync(AppInstallRequest request)
    {
        // Se a janela estiver escondida (bandeja), usa as escolhas padrão em vez de abrir um diálogo sem dono
        if (!IsVisible) return request.Defaults;

        var dialog = new AppInstallDialog { DataContext = new AppInstallDialogViewModel(request) };
        return await dialog.ShowDialog<AppInstallChoices?>(this);
    }

    /// <summary>
    /// Abre o seletor de pastas do sistema (funciona igual no Windows e no Linux).
    /// Fica no code-behind porque precisa da janela para abrir o diálogo.
    /// </summary>
    private async void OnPickFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Escolha a pasta de origem dos apps",
            AllowMultiple = false
        });

        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (path is null) return;

        vm.SourceFolder = path;
        vm.StatusMessage = "Pasta de origem atualizada.";
    }

    // ------------------------------------------------------------------ faixas da Biblioteca

    /// <summary>A faixa (título + setas + cards) em que um controle está.</summary>
    private static Control? ShelfRoot(Control? c) =>
        c?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(x => x.Classes.Contains("shelf-root"));

    private static ScrollViewer? ShelfScroller(Control? root) =>
        root?.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(x => x.Classes.Contains("shelf"));

    private void OnShelfPrevClick(object? sender, RoutedEventArgs e) => ScrollShelf(sender as Control, -1);
    private void OnShelfNextClick(object? sender, RoutedEventArgs e) => ScrollShelf(sender as Control, +1);

    /// <summary>Anda uma "página" de cards para o lado, com uma desaceleração suave no fim.</summary>
    private static void ScrollShelf(Control? button, int direction)
    {
        var sv = ShelfScroller(ShelfRoot(button));
        if (sv is null) return;

        const double cardStep = 276; // largura do card (260) + espaço entre eles (16)
        var page = Math.Max(cardStep, Math.Floor(sv.Viewport.Width / cardStep) * cardStep);
        var max = Math.Max(0, sv.Extent.Width - sv.Viewport.Width);
        var from = sv.Offset.X;
        var to = Math.Clamp(from + direction * page, 0, max);
        if (Math.Abs(to - from) < 1) return;

        var started = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(320);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        timer.Tick += (_, _) =>
        {
            var t = Math.Min(1, (DateTime.UtcNow - started) / duration);
            var eased = 1 - Math.Pow(1 - t, 3);
            sv.Offset = new Vector(from + (to - from) * eased, sv.Offset.Y);
            if (t >= 1) timer.Stop();
        };
        timer.Start();
    }

    /// <summary>Mostra as setas só quando há cards escondidos, e apaga a seta do lado que já chegou ao fim.</summary>
    private void OnShelfScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        var root = ShelfRoot(sv);
        if (root is null) return;

        var overflow = sv.Extent.Width - sv.Viewport.Width > 1;
        foreach (var c in root.GetVisualDescendants().OfType<Control>())
        {
            if (c.Classes.Contains("shelf-arrows")) c.IsVisible = overflow;
            else if (c.Classes.Contains("shelf-prev")) c.IsEnabled = sv.Offset.X > 1;
            else if (c.Classes.Contains("shelf-next")) c.IsEnabled = sv.Offset.X < sv.Extent.Width - sv.Viewport.Width - 1;
        }
    }
}
