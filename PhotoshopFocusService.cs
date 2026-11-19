using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Typesetterer3;

internal static partial class PhotoshopFocusService
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    public static bool IsPhotoshopForeground()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        var threadId = GetWindowThreadProcessId(foreground, out var processId);
        if (processId == 0) return false;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (!process.ProcessName.Equals("Photoshop", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var mainHandle = process.MainWindowHandle;
            if (mainHandle != IntPtr.Zero)
            {
                // If the main window is disabled, a modal dialog is open.
                if (!IsWindowEnabled(mainHandle))
                {
                    return false;
                }
            }

            // Check if in menu mode
            if (threadId != 0)
            {
                var guiInfo = new GUITHREADINFO();
                guiInfo.cbSize = Marshal.SizeOf(guiInfo);
                if (GetGUIThreadInfo(threadId, ref guiInfo))
                {
                    const int GUI_INMENUMODE = 0x00000004;
                    const int GUI_POPUPMENUMODE = 0x00000010;
                    const int GUI_SYSTEMMENUMODE = 0x00000020;
                    if ((guiInfo.flags & (GUI_INMENUMODE | GUI_POPUPMENUMODE | GUI_SYSTEMMENUMODE)) != 0)
                    {
                        return false;
                    }
                }
            }

            // Check standard menu class
            var className = new StringBuilder(256);
            if (GetClassName(foreground, className, className.Capacity) > 0)
            {
                if (className.ToString() == "#32768")
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsPhotoshopInMenuOrModal(IntPtr photoshopHandle)
    {
        if (photoshopHandle == IntPtr.Zero) return false;

        if (!IsWindowEnabled(photoshopHandle)) return true;

        uint threadId = GetWindowThreadProcessId(photoshopHandle, out var processId);
        if (threadId != 0)
        {
            var guiInfo = new GUITHREADINFO();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);
            if (GetGUIThreadInfo(threadId, ref guiInfo))
            {
                const int GUI_INMENUMODE = 0x00000004;
                const int GUI_POPUPMENUMODE = 0x00000010;
                const int GUI_SYSTEMMENUMODE = 0x00000020;
                if ((guiInfo.flags & (GUI_INMENUMODE | GUI_POPUPMENUMODE | GUI_SYSTEMMENUMODE)) != 0)
                {
                    return true;
                }
            }
        }

        IntPtr fgWindow = GetForegroundWindow();
        if (fgWindow != IntPtr.Zero)
        {
            _ = GetWindowThreadProcessId(fgWindow, out var fgProcessId);
            if (fgProcessId == processId)
            {
                // If the foreground window belongs to Photoshop but is NOT the main
                // window, it's a dialog (Layer Styles, Preferences, Save As, etc.)
                if (fgWindow != photoshopHandle)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowEnabled(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
