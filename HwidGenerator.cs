using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Typesetterer3;

public static class HwidGenerator
{
    public static string GetHwid()
    {
        var rawId = GetCpuId() + GetMotherboardId() + GetDiskId();
        return HashString(rawId);
    }

    private static string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                var val = obj["ProcessorId"]?.ToString();
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        catch { /* Ignored */ }
        return "UnknownCPU";
    }

    private static string GetMotherboardId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                var val = obj["SerialNumber"]?.ToString();
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        catch { /* Ignored */ }
        return "UnknownBoard";
    }

    private static string GetDiskId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType='Fixed hard disk media'");
            foreach (var obj in searcher.Get())
            {
                var val = obj["SerialNumber"]?.ToString();
                if (!string.IsNullOrEmpty(val)) return val.Trim();
            }
        }
        catch { /* Ignored */ }
        return "UnknownDisk";
    }

    private static string HashString(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("X2"));
        }
        return builder.ToString();
    }
}
