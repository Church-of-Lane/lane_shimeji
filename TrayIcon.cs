using System.Windows;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace ShimejiLane;

// Icon in the taskbar's notification area with an Exit menu
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon icon;

    public TrayIcon(Window window)
    {
        icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "ShimejiLane",
            ContextMenuStrip = new Forms.ContextMenuStrip(),
            Visible = true,
        };
        icon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => window.Close());
    }

    private static Drawing.Icon LoadIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/img/shime1.png")).Stream;
        using var bitmap = new Drawing.Bitmap(stream);
        return Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        // Without this the icon lingers in the tray until you hover over it
        icon.Visible = false;
        icon.Dispose();
    }
}
