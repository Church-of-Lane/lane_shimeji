using Avalonia;

namespace ShimejiLane;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args){
        //DotNetEnv.Env.Load();  
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        // Lives in the menu bar like a tray app instead of showing a Dock icon
        .With(new MacOSPlatformOptions { ShowInDock = false });
}
