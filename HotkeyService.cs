using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Typesetterer3;

internal sealed partial class HotkeyService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_MENU = 0x12;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;
    private const int VK_V = 0x56;
    private const int LLKHF_INJECTED = 0x00000010;

    private readonly LowLevelKeyboardProc _keyboardProc;
    private IntPtr _hookHandle;
    private bool _hotkeyDown;
    private bool _scriptHotkeyDown;

    public event EventHandler? PasteShortcutObserved;
    public event EventHandler? ScriptShortcutObserved;

    public HotkeyService()
    {
        _keyboardProc = KeyboardHookCallback;
    }

    public void StartPasteShortcutMonitor()
    {
        if (_hookHandle != IntPtr.Zero) return;

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule;
        var moduleHandle = currentModule?.ModuleName is { Length: > 0 } moduleName ? GetModuleHandle(moduleName) : IntPtr.Zero;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Paste Hotkey
            var pasteHotkey = WindowSettings.GetCustomHotkey();
            var pasteRequiresCtrl = WindowSettings.GetHotkeyRequiresCtrl();
            var pasteRequiresShift = WindowSettings.GetHotkeyRequiresShift();
            var pasteRequiresAlt = WindowSettings.GetHotkeyRequiresAlt();

            if (info.vkCode == pasteHotkey && (message == WM_KEYUP || message == WM_SYSKEYUP))
            {
                _hotkeyDown = false;
            }

            if (info.vkCode == pasteHotkey && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                if ((info.flags & LLKHF_INJECTED) == 0)
                {
                    var ctrlDown = IsKeyDown(VK_CONTROL) || IsKeyDown(VK_LCONTROL) || IsKeyDown(VK_RCONTROL);
                    var shiftDown = IsKeyDown(VK_SHIFT) || IsKeyDown(VK_LSHIFT) || IsKeyDown(VK_RSHIFT);
                    var altDown = IsKeyDown(VK_MENU) || IsKeyDown(VK_LMENU) || IsKeyDown(VK_RMENU);

                    var match = (ctrlDown == pasteRequiresCtrl) &&
                                (shiftDown == pasteRequiresShift) &&
                                (altDown == pasteRequiresAlt);

                    if (match && !_hotkeyDown && PhotoshopFocusService.IsPhotoshopForeground())
                    {
                        _hotkeyDown = true;
                        PasteShortcutObserved?.Invoke(this, EventArgs.Empty);
                        return (IntPtr)1; // Suppress original event
                    }
                }
            }

            // Script Hotkey
            var scriptHotkey = WindowSettings.GetScriptHotkey();
            var scriptRequiresCtrl = WindowSettings.GetScriptHotkeyRequiresCtrl();
            var scriptRequiresShift = WindowSettings.GetScriptHotkeyRequiresShift();
            var scriptRequiresAlt = WindowSettings.GetScriptHotkeyRequiresAlt();

            if (info.vkCode == scriptHotkey && (message == WM_KEYUP || message == WM_SYSKEYUP))
            {
                _scriptHotkeyDown = false;
            }

            if (info.vkCode == scriptHotkey && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                if ((info.flags & LLKHF_INJECTED) == 0)
                {
                    var ctrlDown = IsKeyDown(VK_CONTROL) || IsKeyDown(VK_LCONTROL) || IsKeyDown(VK_RCONTROL);
                    var shiftDown = IsKeyDown(VK_SHIFT) || IsKeyDown(VK_LSHIFT) || IsKeyDown(VK_RSHIFT);
                    var altDown = IsKeyDown(VK_MENU) || IsKeyDown(VK_LMENU) || IsKeyDown(VK_RMENU);

                    var match = (ctrlDown == scriptRequiresCtrl) &&
                                (shiftDown == scriptRequiresShift) &&
                                (altDown == scriptRequiresAlt);

                    if (match && !_scriptHotkeyDown && PhotoshopFocusService.IsPhotoshopForeground())
                    {
                        _scriptHotkeyDown = true;
                        ScriptShortcutObserved?.Invoke(this, EventArgs.Empty);
                        return (IntPtr)1; // Suppress original event
                    }
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static partial IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(IntPtr hhk);

    [LibraryImport("user32.dll")]
    private static partial IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandle(string lpModuleName);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }
}
