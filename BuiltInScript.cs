using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace Typesetterer3;

internal static class BuiltInScript
{
    public static string? LoadedPayloadBase64 { get; set; }
    public static string? LoadedPayloadKey { get; set; }
    public static string? LoadedScriptSha256 { get; set; }

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

    public static void Execute()
    {
        try
        {
            if (string.IsNullOrEmpty(LoadedPayloadBase64))
            {
                MessageBox.Show("Photoshop script payload has not been downloaded or validated. Please verify your license status.", "Authentication Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var jsxCode = DecodeScript();

            Type? photoshopType = Type.GetTypeFromProgID("Photoshop.Application");
            if (photoshopType == null)
            {
                MessageBox.Show("Adobe Photoshop is not detected or not registered via COM on this system.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            object? app = GetActivePhotoshopInstance();
            if (app == null)
            {
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

            if (app == null)
            {
                MessageBox.Show("Failed to connect to Adobe Photoshop COM server.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            photoshopType.InvokeMember(
                "DoJavaScript",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                app,
                new object?[] { jsxCode, null, null });
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

    private static string DecodeScript()
    {
        var payload = Convert.FromBase64String(LoadedPayloadBase64!);
        var key = Encoding.UTF8.GetBytes(LoadedPayloadKey!);

        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] ^= key[i % key.Length];
        }

        using var input = new MemoryStream(payload);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);

        var scriptBytes = output.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(scriptBytes));
        if (!string.Equals(hash, LoadedScriptSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Built-in script payload failed integrity validation.");
        }

        return Encoding.UTF8.GetString(scriptBytes);
    }
}
