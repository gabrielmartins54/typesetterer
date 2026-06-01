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
    private const string PayloadKey = "Typesetterer3.BuiltInScript.v1";
    private const string ScriptSha256 = "08E66099A75A7AAAD812476B0FC6119C690A3A7A5BCA9EF10177983B3CD0E7B8";
    private const string PayloadBase64 = "S/J4ZXNldHRhcogkYkGZQ3kSe6mRI5B0LR5I9ziGalWDZVRrYgMMEEEcIw6PdTBWvyMHAUNbvh6j0cgSEVDmgAsg9RVKQQlq11M1gpiC5zHzRO7xfkCwflrj0Bh0emBY86MP97dq3X7oMZkz1K7RoRgUjoWZ05/ZXveNS95f+fejs0mRkghSWevoD2SWj1sqb2JwpUSvPJEz9dUbXgXjYBz6+blbNpD6GKPKIpPO+EyZkUkvieYvu+2BFVd06PXmQqcBZA9nkJP3USVk24vN20Rp910jV3BKFMqj53sbb9ZdZ3etAzT2g6UW9NJLkGhsGV+XkmCHerANad55eV/9kTDLYgggAPKDIUlaHVfW4JQJAQ1dCvB6f4vTyPbAe3XblIFDzLO5e43GboGbvOcxNu0A1uZMMae9MYjSQ3xeePwJIdUfxaZCfL09CVfldkUQvonNBzCc8gyhr7s9UTKbF9ylXPzo3xNYmKCGG0XcN/JkRSDBDpYPdoPAE0W7/8KSMyLHkt1dZTyZvLfITnoyG6XRCQQiPKUbsMS+BqeniIkCIBg366Z8o2TL0Q+gmmHvRJBWyKIo8Rb4gcSOCP9IFgi11fx9DDRD3WK3AFdSeX2HqX0VWY32rlehClkmugzc0Yow6/V2Ffzq9DuBX+Abxrxq0SyssC45MPgScD3ykv0KL8N4/xOptFc7OFCEmBGzVWWul1Z3c/r4uiA6KFhPFGAdD4ueg69Ur1Ev1VLFtzTvxfbhNz+rtdBd115w1Vit7JALhEjjeY5TonH/kiP/y9eVE4UnKwgb0CgOaI0jsuXk6aKCmDJEKWZaaTnrMsVDhjN5gyZ2UYYufvUstDCZNLWgjJQsGGC6SX2R1TW9UM36S9YA/ijMObj40cvzoyvzf0ruyjDS0YJwEQxn5gdRMHpeoZe3718CAK21/x3D9oKqF7SrYjJMnWHMZeR9RSnoq6WhZPicqvqbrd8dWPOfCrbWuc4fpCYrVetvXfoq99MDqt0kkx2irxOPDfanat2eqd391EzcrALq5Ku7OW4BjoGJS0ddl0Q7Vdh2fC4fEd/QtcVVq7ojBz2FI4E+e2VnwVGl2hULSxLDlyrpZltS0Cm6vU3VkSkabQX2WaUWRmh450FqalyL4tHrkPjw3VN0gzs4N2Pvv4WJK26YMct6SBRpPadjAVMc90LfF0PF4WR2Gv7hD0A4/SpNr0RSnd1Bo4SWqbK0BXTBhuGgPoyqJWyu37MMTM3T3aCcupDOCxy+czRb258b8lHnS/9aPDOe1syhQRpc43UNc8Kr56LH0gJ0OOriZriiHbMP/ZDPT/OeKOFweHRl";

    [DllImport("ole32.dll")]
    private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    private static object? GetActivePhotoshopInstance()
    {
        try
        {
            int hr = CLSIDFromProgID("Photoshop.Application", out Guid clsid);
            if (hr >= 0)
            {
                GetActiveObject(ref clsid, IntPtr.Zero, out object obj);
                return obj;
            }
        }
        catch
        {
            // Ignore and fallback
        }
        return null;
    }

    public static void Execute()
    {
        try
        {
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
        var payload = Convert.FromBase64String(PayloadBase64);
        var key = Encoding.UTF8.GetBytes(PayloadKey);

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
        if (!string.Equals(hash, ScriptSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Built-in script payload failed integrity validation.");
        }

        return Encoding.UTF8.GetString(scriptBytes);
    }
}
