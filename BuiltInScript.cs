using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Typesetterer3;

internal static class BuiltInScript
{

    [DllImport("ole32.dll")]
    private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    private static bool TryGetActiveObject(string progId, out object? obj)
    {
        obj = null;
        try
        {
            int hr = CLSIDFromProgID(progId, out Guid clsid);
            if (hr >= 0)
            {
                GetActiveObject(ref clsid, IntPtr.Zero, out object ppunk);
                obj = ppunk;
                return true;
            }
        }
        catch
        {
            // Ignore and try next
        }
        return false;
    }

    private static object? GetActivePhotoshopInstance()
    {
        // 1. Try default version-independent ProgID
        if (TryGetActiveObject("Photoshop.Application", out var obj))
        {
            return obj;
        }

        // 2. Try common versioned ProgIDs (from CC 2026/v270 down to CS3/v100)
        for (int v = 280; v >= 100; v -= 10)
        {
            string vStr = v.ToString();
            string majorStr = (v / 10).ToString();

            if (TryGetActiveObject($"Photoshop.Application.{vStr}", out obj)) return obj;
            if (TryGetActiveObject($"Photoshop.Application.{majorStr}", out obj)) return obj;
        }

        return null;
    }

    private static Type? FindPhotoshopType()
    {
        var t = Type.GetTypeFromProgID("Photoshop.Application");
        if (t != null) return t;

        for (int v = 280; v >= 100; v -= 10)
        {
            t = Type.GetTypeFromProgID($"Photoshop.Application.{v}") ?? Type.GetTypeFromProgID($"Photoshop.Application.{v / 10}");
            if (t != null) return t;
        }
        return null;
    }

    public static async Task ExecuteAsync()
    {
        try
        {
            var jsxCode = DecodeScript();

            // Prefer the running instance (works with versioned ProgIDs only, e.g. Photoshop.Application.270).
            object? app = GetActivePhotoshopInstance();
            Type? photoshopType = app?.GetType();
            if (app == null)
            {
                photoshopType = FindPhotoshopType();
                if (photoshopType == null)
                {
                    MessageBox.Show(
                        "Adobe Photoshop is not detected or not registered via COM on this system.\n\n" +
                        "Make sure Photoshop is running and was launched with the same permissions (both normal or both as administrator). " +
                        "If it still fails, repair/reinstall Photoshop to re-register its COM server.",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    app = Activator.CreateInstance(photoshopType);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Failed to connect to Adobe Photoshop COM server.\n\n" +
                        "This can happen if Photoshop is busy, displaying a modal dialog (like Layer Styles), running with different administrator permissions, or not running.\n\n" +
                        $"Details: {ex.Message}",
                        "Photoshop COM Connection Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            if (app == null || photoshopType == null)
            {
                MessageBox.Show("Failed to connect to Adobe Photoshop COM server.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            await InvokeDoJavaScriptWithRetryAsync(photoshopType, app, jsxCode);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "An error occurred while executing the built-in script.\n\n" +
                "Please make sure Photoshop is not displaying a dialog or menu, and try again.\n\n" +
                $"Details: {ex.Message}",
                "Script Execution Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    // Keep synchronous entry point for callers that need fire-and-forget from the UI thread.
    public static void Execute()
    {
        _ = ExecuteAsync();
    }

    /// <summary>
    /// Extracts the COM HRESULT from an exception, handling both bare
    /// <see cref="COMException"/> and <see cref="System.Reflection.TargetInvocationException"/>-wrapped variants.
    /// </summary>
    private static int? GetComErrorCode(Exception ex)
    {
        if (ex is COMException comEx)
            return comEx.ErrorCode;
        if (ex is System.Reflection.TargetInvocationException { InnerException: COMException innerCom })
            return innerCom.ErrorCode;
        return null;
    }

    /// <summary>
    /// Invokes DoJavaScript on the Photoshop COM object, retrying up to <paramref name="maxRetries"/>
    /// times when Photoshop returns RPC_E_SERVERCALL_RETRYLATER (0x8001010A).
    /// Registers a COM <see cref="IMessageFilter"/> before the call so that the COM runtime
    /// can automatically negotiate retries at the protocol level — the standard fix for
    /// busy COM servers like Photoshop.
    /// </summary>
    private static async Task InvokeDoJavaScriptWithRetryAsync(Type photoshopType, object app, string jsxCode, int maxRetries = 8)
    {
        const int RPC_E_SERVERCALL_RETRYLATER = unchecked((int)0x8001010A);
        const int RPC_E_CALL_REJECTED = unchecked((int)0x80010001);
        int delayMs = 250;

        // Register the COM message filter so the COM runtime can handle
        // SERVERCALL_RETRYLATER at the protocol level before it becomes an exception.
        ComMessageFilter.Register();
        try
        {
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    photoshopType.InvokeMember(
                        "DoJavaScript",
                        System.Reflection.BindingFlags.InvokeMethod,
                        null,
                        app,
                        new object?[] { jsxCode, null, null });
                    return; // Success
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    var errorCode = GetComErrorCode(ex);
                    if (errorCode == RPC_E_SERVERCALL_RETRYLATER || errorCode == RPC_E_CALL_REJECTED)
                    {
                        // Photoshop is momentarily busy — wait and retry.
                        await Task.Delay(delayMs);
                        delayMs = Math.Min(delayMs * 2, 3000); // Exponential backoff capped at 3s
                        continue;
                    }
                    throw; // Not a retryable error — let it bubble up.
                }
                // On the final attempt, any exception bubbles up to the caller's catch block.
            }
        }
        finally
        {
            ComMessageFilter.Revoke();
        }
    }

    private static string DecodeScript()
    {
        using var stream = typeof(BuiltInScript).Assembly.GetManifestResourceStream("Typesetterer3.BuiltInScript.jsx")
            ?? throw new InvalidDataException("Built-in Photoshop script resource is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// COM IMessageFilter implementation that tells the COM runtime to automatically retry
/// calls when the server (Photoshop) returns SERVERCALL_RETRYLATER or rejects the call.
/// Without this, the COM runtime immediately throws an exception instead of retrying.
/// This is the standard Microsoft-documented solution for automating busy COM servers.
/// See: https://learn.microsoft.com/en-us/windows/win32/api/objidl/nn-objidl-imessagefilter
/// </summary>
internal sealed class ComMessageFilter : IOleMessageFilter
{
    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);

    private static IOleMessageFilter? _oldFilter;

    public static void Register()
    {
        IOleMessageFilter filter = new ComMessageFilter();
        CoRegisterMessageFilter(filter, out _oldFilter);
    }

    public static void Revoke()
    {
        CoRegisterMessageFilter(_oldFilter, out _);
        _oldFilter = null;
    }

    /// <summary>
    /// Called on the server side — not applicable for our client, so always allow.
    /// </summary>
    int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
    {
        return 0; // SERVERCALL_ISHANDLED
    }

    /// <summary>
    /// Called when the COM server rejects our call. This is the key method:
    /// returning a positive value tells COM to retry the call after that many milliseconds.
    /// Returning -1 would cancel the call (which is the default behavior without a filter).
    /// </summary>
    int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
    {
        // dwRejectType == 2 means SERVERCALL_RETRYLATER (server is busy, try again)
        // For any rejection within the first ~10 seconds, keep retrying every 200ms
        if (dwRejectType == 2)
        {
            // Return value > 0 && < 100 means retry immediately
            // Return value >= 100 means retry after that many milliseconds
            return 200;
        }

        // For SERVERCALL_REJECTED (dwRejectType == 1), also retry if we haven't been
        // waiting too long (dwTickCount is ms since first attempt).
        if (dwRejectType == 1 && dwTickCount < 10000)
        {
            return 500;
        }

        return -1; // Cancel — give up after too many rejections.
    }

    /// <summary>
    /// Called when a message arrives while waiting for a COM response.
    /// Return PENDINGMSG_WAITDEFPROCESS to handle Windows messages normally.
    /// </summary>
    int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
    {
        return 2; // PENDINGMSG_WAITDEFPROCESS — process the message normally
    }
}

/// <summary>
/// COM IOleMessageFilter interface declaration for interop.
/// </summary>
[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    [PreserveSig]
    int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

    [PreserveSig]
    int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

    [PreserveSig]
    int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
}
