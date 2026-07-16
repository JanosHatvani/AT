using System.Runtime.InteropServices;

namespace AT.App.Interop;

// Csak a kurzor képernyő-pozíciójának lekérdezéséhez kell — a delay-alapú elem-kereső ezt hívja.
internal static class Win32
{
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Win32Point lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }

    public static (int X, int Y) GetCursorScreenPosition()
    {
        GetCursorPos(out var p);
        return (p.X, p.Y);
    }
}