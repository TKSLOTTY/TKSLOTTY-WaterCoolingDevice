using System.Diagnostics;
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
    public int OledDisplayMode { get; set; } = 5;
    public int OledDisplayIntervalSeconds { get; set; } = 8;
    public bool SendHardwareTemperatures { get; set; } = true;

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
        if (current.OledDisplayMode != original.OledDisplayMode)
            destination.OledDisplayMode = current.OledDisplayMode;
        if (current.OledDisplayIntervalSeconds != original.OledDisplayIntervalSeconds)
            destination.OledDisplayIntervalSeconds = current.OledDisplayIntervalSeconds;
        if (current.SendHardwareTemperatures != original.SendHardwareTemperatures)
            destination.SendHardwareTemperatures = current.SendHardwareTemperatures;
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
        destination.OledDisplayMode = source.OledDisplayMode;
        destination.OledDisplayIntervalSeconds = source.OledDisplayIntervalSeconds;
        destination.SendHardwareTemperatures = source.SendHardwareTemperatures;
    }
}

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WaterCoolingDevice";
    private const string LegacyElevatedTaskName = "WaterCoolingDevice";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string || LegacyScheduledTaskExists();
    }

    public static bool NeedsMigration() => LegacyScheduledTaskExists();

    public static void RepairExecutablePathIfEnabled()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (key.GetValue(ValueName) is not string registeredPath) return;

        var expectedPath = $"\"{Application.ExecutablePath}\"";
        if (!string.Equals(registeredPath, expectedPath, StringComparison.OrdinalIgnoreCase))
            key.SetValue(ValueName, expectedPath);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
            if (LegacyScheduledTaskExists())
                RunTaskScheduler("/Delete", "/TN", LegacyElevatedTaskName, "/F");
        }
    }

    public static void MigrateLegacyElevatedStartup()
    {
        if (!LegacyScheduledTaskExists()) return;
        using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            key.SetValue(ValueName, $"\"{Application.ExecutablePath}\"");
        RunTaskScheduler("/Delete", "/TN", LegacyElevatedTaskName, "/F");
    }

    private static bool LegacyScheduledTaskExists()
    {
        using var process = Process.Start(new ProcessStartInfo {
            FileName = TaskSchedulerPath(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "/Query", "/TN", LegacyElevatedTaskName }
        });
        if (process is null) return false;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static void RunTaskScheduler(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo {
            FileName = TaskSchedulerPath(),
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("タスクスケジューラを起動できませんでした。");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"自動起動タスクを更新できませんでした（終了コード {process.ExitCode}）。");
    }

    private static string TaskSchedulerPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");

}
