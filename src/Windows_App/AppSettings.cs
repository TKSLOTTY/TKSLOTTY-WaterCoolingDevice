using System.Text.Json;
using System.Text.Json.Serialization;
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

    [JsonIgnore]
    private AppSettings? baseline;

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WaterCoolingDevice");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");
    private const string SettingsMutexName = @"Local\WaterCoolingDevice.Settings.v2";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        AppSettings settings;
        try { settings = ReadFromDisk(); }
        catch { settings = new AppSettings(); }
        settings.baseline = settings.CloneValues();
        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        using var mutex = new Mutex(false, SettingsMutexName);
        var lockTaken = false;
        try
        {
            try { lockTaken = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { lockTaken = true; }
            if (!lockTaken)
                throw new IOException("設定ファイルを他のプロセスが更新中です。しばらくしてから再試行してください。");

            AppSettings latest;
            try { latest = ReadFromDisk(); }
            catch { latest = new AppSettings(); }

            var original = baseline ?? CloneValues();
            MergeLocalChanges(latest, original, this);
            WriteAtomically(latest);
            CopyValues(latest, this);
            baseline = latest.CloneValues();
        }
        finally
        {
            if (lockTaken) mutex.ReleaseMutex();
        }
    }

    private static AppSettings ReadFromDisk() =>
        File.Exists(FilePath)
            ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new()
            : new();

    private static void WriteAtomically(AppSettings settings)
    {
        var temporaryPath = Path.Combine(Folder,
            $"settings.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporaryPath, FilePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void MergeLocalChanges(
        AppSettings destination, AppSettings original, AppSettings current)
    {
        if (current.English != original.English) destination.English = current.English;
        if (current.WarningColorArgb != original.WarningColorArgb)
            destination.WarningColorArgb = current.WarningColorArgb;
        if (current.LowColorArgb != original.LowColorArgb)
            destination.LowColorArgb = current.LowColorArgb;
        if (current.BeepOnWarning != original.BeepOnWarning)
            destination.BeepOnWarning = current.BeepOnWarning;
        if (current.AlwaysOnTop != original.AlwaysOnTop)
            destination.AlwaysOnTop = current.AlwaysOnTop;
        if (current.Fan2PumpMode != original.Fan2PumpMode)
            destination.Fan2PumpMode = current.Fan2PumpMode;
        if (current.PumpDuty != original.PumpDuty)
            destination.PumpDuty = current.PumpDuty;
    }

    private AppSettings CloneValues()
    {
        var clone = new AppSettings();
        CopyValues(this, clone);
        return clone;
    }

    private static void CopyValues(AppSettings source, AppSettings destination)
    {
        destination.English = source.English;
        destination.WarningColorArgb = source.WarningColorArgb;
        destination.LowColorArgb = source.LowColorArgb;
        destination.BeepOnWarning = source.BeepOnWarning;
        destination.AlwaysOnTop = source.AlwaysOnTop;
        destination.Fan2PumpMode = source.Fan2PumpMode;
        destination.PumpDuty = source.PumpDuty;
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
