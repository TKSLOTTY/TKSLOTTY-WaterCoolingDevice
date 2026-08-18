using System.Text.Json;
using Microsoft.Win32;

namespace WaterCoolingDevice;

internal sealed class AppSettings
{
    public bool English { get; set; }
    public int WarningColorArgb { get; set; } = Color.FromArgb(255, 70, 60).ToArgb();
    public int LowColorArgb { get; set; } = Color.FromArgb(40, 170, 255).ToArgb();
    public bool BeepOnWarning { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool Fan2PumpMode { get; set; }
    public int PumpDuty { get; set; } = 60;

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WaterCoolingDevice");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new()
                : new();
        } catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WaterCoolingDevice";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
        else key.DeleteValue(ValueName, false);
    }
}
