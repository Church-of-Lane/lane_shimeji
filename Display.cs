using System.Runtime.InteropServices;

namespace ShimejiLane;

public static class Display
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int index);

    private const int VREFRESH = 116;

    public static int GetRefreshRate()
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        int hz = GetDeviceCaps(hdc, VREFRESH);
        ReleaseDC(IntPtr.Zero, hdc);
        return hz;
    }
}
