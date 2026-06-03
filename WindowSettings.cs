using System.Text.Json;

namespace Typesetterer3;

internal sealed class WindowSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Typesetterer3",
        "window.json");

    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public ScriptSaveSlot? SaveSlot1 { get; set; }
    public ScriptSaveSlot? SaveSlot2 { get; set; }
    public string IgnoredTags { get; set; } = string.Empty;
    public int CustomHotkey { get; set; } = 0x56; // Default to 'V'
    public bool HotkeyRequiresCtrl { get; set; } = true;
    public bool HotkeyRequiresShift { get; set; } = false;
    public bool HotkeyRequiresAlt { get; set; } = false;
    public int ScriptHotkey { get; set; } = 0x71; // Default to F2 (0x71)
    public bool ScriptHotkeyRequiresCtrl { get; set; } = false;
    public bool ScriptHotkeyRequiresShift { get; set; } = false;
    public bool ScriptHotkeyRequiresAlt { get; set; } = false;

    public Rectangle Bounds => new(Left, Top, Width, Height);

    public static WindowSettings? Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return null;

            var settings = JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(SettingsPath));
            if (settings is null || settings.Width <= 0 || settings.Height <= 0) return null;

            return settings;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(Rectangle bounds)
    {
        var settings = Load() ?? new WindowSettings();
        settings.Left = bounds.Left;
        settings.Top = bounds.Top;
        settings.Width = bounds.Width;
        settings.Height = bounds.Height;
        Save(settings);
    }

    public static ScriptSaveSlot? GetSaveSlot(int slot)
    {
        var settings = Load();
        return slot == 1 ? settings?.SaveSlot1 : settings?.SaveSlot2;
    }

    public static void SaveSlot(int slot, ScriptSaveSlot saveSlot)
    {
        var settings = Load() ?? new WindowSettings();
        if (slot == 1)
        {
            settings.SaveSlot1 = saveSlot;
        }
        else
        {
            settings.SaveSlot2 = saveSlot;
        }

        Save(settings);
    }

    public static string GetIgnoredTags() => ScriptParser.FixBrokenUnicodeEscapes(Load()?.IgnoredTags ?? string.Empty);

    public static void SaveIgnoredTags(string tags)
    {
        var settings = Load() ?? new WindowSettings();
        settings.IgnoredTags = tags;
        Save(settings);
    }

    public static int GetCustomHotkey() => Load()?.CustomHotkey ?? 0x56;
    public static bool GetHotkeyRequiresCtrl() => Load()?.HotkeyRequiresCtrl ?? true;
    public static bool GetHotkeyRequiresShift() => Load()?.HotkeyRequiresShift ?? false;
    public static bool GetHotkeyRequiresAlt() => Load()?.HotkeyRequiresAlt ?? false;

    public static void SaveCustomHotkey(int vkCode, bool requiresCtrl, bool requiresShift, bool requiresAlt)
    {
        var settings = Load() ?? new WindowSettings();
        settings.CustomHotkey = vkCode;
        settings.HotkeyRequiresCtrl = requiresCtrl;
        settings.HotkeyRequiresShift = requiresShift;
        settings.HotkeyRequiresAlt = requiresAlt;
        Save(settings);
    }

    public static int GetScriptHotkey() => Load()?.ScriptHotkey ?? 0x71;
    public static bool GetScriptHotkeyRequiresCtrl() => Load()?.ScriptHotkeyRequiresCtrl ?? false;
    public static bool GetScriptHotkeyRequiresShift() => Load()?.ScriptHotkeyRequiresShift ?? false;
    public static bool GetScriptHotkeyRequiresAlt() => Load()?.ScriptHotkeyRequiresAlt ?? false;

    public static void SaveScriptHotkey(int vkCode, bool requiresCtrl, bool requiresShift, bool requiresAlt)
    {
        var settings = Load() ?? new WindowSettings();
        settings.ScriptHotkey = vkCode;
        settings.ScriptHotkeyRequiresCtrl = requiresCtrl;
        settings.ScriptHotkeyRequiresShift = requiresShift;
        settings.ScriptHotkeyRequiresAlt = requiresAlt;
        Save(settings);
    }

    private static void Save(WindowSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // Persistence is a convenience; ignore storage failures.
        }
    }
}

internal sealed class ScriptSaveSlot
{
    public int CurrentIndex { get; set; }
    public string? Name { get; set; }
    public DateTime SavedAt { get; set; }
    public List<SavedLine> Lines { get; set; } = [];
}

internal sealed class SavedLine
{
    public string RawText { get; set; } = string.Empty;
    public bool IsPasted { get; set; }
}
