using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Typesetterer3;

internal static partial class PhotoshopFocusService
{
    public static bool IsPhotoshopForeground()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        _ = GetWindowThreadProcessId(foreground, out var processId);
        if (processId == 0) return false;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName.Equals("Photoshop", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
