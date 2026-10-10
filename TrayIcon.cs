using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace ShimejiLane;

// Icon in the notification area (Windows), menu bar (macOS) or status tray (Linux) with an Exit menu
public sealed class TrayIcon : IDisposable
{
    private readonly Avalonia.Controls.TrayIcon icon;

    public TrayIcon(Window window)
    {
        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => window.Close();

        icon = new Avalonia.Controls.TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ShimejiLane/img/shime1.png"))),
            ToolTipText = "ShimejiLane",
            Menu = new NativeMenu { exit },
            IsVisible = true,
        };
        Avalonia.Controls.TrayIcon.SetIcons(Application.Current!, new TrayIcons { icon });
    }

    public void Dispose()
    {
        // Without this the icon can linger in the tray until you hover over it
        icon.IsVisible = false;
        icon.Dispose();
    }
}
