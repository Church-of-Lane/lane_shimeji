using System.Runtime.InteropServices;
using Avalonia;

namespace ShimejiLane;

// Asks the OS where the cursor is, in the same desktop pixels as Window.Position.
// Positions from pointer events are relative to the window and go stale while it is being moved.
public static class GlobalCursor
{
    private static bool failed;

    // Null if the platform can't be asked (e.g. a pure Wayland session); callers fall back to pointer events
    public static PixelPoint? Position()
    {
        if (failed) return null;
        try
        {
            if (OperatingSystem.IsWindows()) return Windows();
            if (OperatingSystem.IsMacOS()) return MacOS();
            if (OperatingSystem.IsLinux()) return X11();
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
        {
            failed = true;
        }
        return null;
    }

    // ---- Windows ----

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    private static PixelPoint? Windows() =>
        GetCursorPos(out POINT p) ? new PixelPoint(p.X, p.Y) : null;

    // ---- macOS ----
    // Points with a top-left origin, which is also what Avalonia uses for window positions on macOS

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint { public double X, Y; }

    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(CoreGraphics)]
    private static extern CGPoint CGEventGetLocation(IntPtr evt);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr obj);

    private static PixelPoint? MacOS()
    {
        IntPtr evt = CGEventCreate(IntPtr.Zero);
        if (evt == IntPtr.Zero) return null;
        CGPoint p = CGEventGetLocation(evt);
        CFRelease(evt);
        return new PixelPoint((int)Math.Round(p.X), (int)Math.Round(p.Y));
    }

    // ---- Linux (X11, including XWayland which Avalonia uses under Wayland) ----

    private const string LibX11 = "libX11.so.6";
    private static IntPtr display;

    [DllImport(LibX11)]
    private static extern IntPtr XOpenDisplay(IntPtr name);

    [DllImport(LibX11)]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(LibX11)]
    private static extern int XQueryPointer(IntPtr display, IntPtr window, out IntPtr root, out IntPtr child,
        out int rootX, out int rootY, out int winX, out int winY, out uint mask);

    private static PixelPoint? X11()
    {
        if (display == IntPtr.Zero)
        {
            display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
            {
                failed = true;
                return null;
            }
        }
        int ok = XQueryPointer(display, XDefaultRootWindow(display), out _, out _, out int x, out int y, out _, out _, out _);
        return ok != 0 ? new PixelPoint(x, y) : null;
    }
}
