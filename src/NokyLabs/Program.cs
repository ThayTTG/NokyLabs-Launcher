using Avalonia;

namespace NokyLabs;

internal sealed class Program
{
    // Ponto de entrada. Não use APIs do Avalonia antes de AppMain ser chamado.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Também é usado pelo previewer de XAML do Visual Studio / Rider.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()   // escolhe Win32 no Windows, X11 no Linux
            .WithInterFont()       // mesma fonte nos dois sistemas
            .LogToTrace();
}
