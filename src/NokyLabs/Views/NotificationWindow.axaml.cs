using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace NokyLabs.Views;

/// <summary>
/// Notificação própria do NokyLabs (funciona igual no Windows e no Linux, sem depender
/// do sistema de notificações de cada um). Aparece no canto inferior direito da tela.
/// </summary>
public partial class NotificationWindow : Window
{
    private const int MarginPx = 16;
    private readonly DispatcherTimer _autoClose = new() { Interval = TimeSpan.FromSeconds(9) };

    /// <summary>Disparado quando a pessoa clica na notificação.</summary>
    public event EventHandler? Clicked;

    public NotificationWindow()
    {
        InitializeComponent();
        _autoClose.Tick += (_, _) => Close();

        // Passar o mouse por cima "segura" a notificação na tela
        PointerEntered += (_, _) => _autoClose.Stop();
        PointerExited += (_, _) => _autoClose.Start();
    }

    public NotificationWindow(string title, string message) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
    }

    /// <summary>Mostra no canto inferior direito da tela principal, sem roubar o foco.</summary>
    public void ShowInCorner()
    {
        var screen = Screens.Primary;
        if (screen is not null)
        {
            var area = screen.WorkingArea;          // tela sem a barra de tarefas
            var scale = screen.Scaling;              // zoom do Windows (125%, 150%...)
            var width = (int)Math.Ceiling(Width * scale);
            var height = (int)Math.Ceiling(Height * scale);
            var margin = (int)Math.Ceiling(MarginPx * scale);

            Position = new PixelPoint(area.Right - width - margin, area.Bottom - height - margin);
        }

        Show();
        _autoClose.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoClose.Stop();
        base.OnClosed(e);
    }

    private void OnBodyPressed(object? sender, PointerPressedEventArgs e)
    {
        Clicked?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Close();
    }
}
