using System.Diagnostics;
using Microsoft.Win32;

namespace WaterCoolingDevice;

internal static class HardwareTemperatureAgentManager
{
    private const string TaskName = "WaterCoolingDevice Temperature Agent";
    private const int AgentStopWaitAttempts = 50;
    private const int AgentStartWaitAttempts = 50;
    private const int AgentStartAttempts = 2;
    private const string PawnIoUninstallKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO";

    public static bool IsPawnIoInstalled()
    {
        using var currentView = Registry.LocalMachine.OpenSubKey(PawnIoUninstallKey);
        if (currentView?.GetValue("DisplayVersion") is string) return true;
        using var registry64 = RegistryKey.OpenBaseKey(
            RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key64 = registry64.OpenSubKey(PawnIoUninstallKey);
        return key64?.GetValue("DisplayVersion") is string;
    }

    public static string PawnIoInstallerPath =>
        Path.Combine(AppContext.BaseDirectory, "Tools", "PawnIO_setup.exe");

    public static void InstallPawnIo()
    {
        if (!File.Exists(PawnIoInstallerPath))
            throw new FileNotFoundException("PawnIO installer was not found.", PawnIoInstallerPath);

        var startInfo = new ProcessStartInfo {
            FileName = PawnIoInstallerPath,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Normal
        };
        startInfo.ArgumentList.Add("-install");
        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("PawnIO installer could not be started.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"PawnIO installer exited with code {process.ExitCode}.");
    }

    public static void EnsureTaskAndStart()
    {
        var taskMatches = ScheduledTaskMatchesCurrentExecutable();

        // The task is configured for ONLOGON and may already be producing good
        // data when the UI starts. Restarting it here created a race with Task
        // Scheduler's IgnoreNew policy, so reuse a healthy agent unchanged.
        if (taskMatches && IsAgentRunning() &&
            HardwareTemperatureStorage.TryReadFresh(out _))
            return;

        StopAgentAndWait();
        if (!taskMatches) CreateScheduledTask();

        for (var startAttempt = 0; startAttempt < AgentStartAttempts; startAttempt++)
        {
            HardwareTemperatureStorage.Clear();
            RunTaskScheduler(false, "/Run", "/TN", TaskName);

            for (var waitAttempt = 0;
                 waitAttempt < AgentStartWaitAttempts; waitAttempt++)
            {
                Thread.Sleep(100);
                if (IsAgentRunning() &&
                    HardwareTemperatureStorage.TryReadFresh(out _))
                    return;
            }

            // A start request can be ignored while the previous scheduled-task
            // instance is still stopping. Wait and make one clean retry.
            StopAgentAndWait();
        }
        throw new TimeoutException("Temperature agent did not produce data.");
    }

    public static void StopAgent()
    {
        StopAgentAndWait();
        HardwareTemperatureStorage.Clear();
    }

    private static void StopAgentAndWait()
    {
        TryEndScheduledTask();
        for (var attempt = 0; attempt < AgentStopWaitAttempts; attempt++)
        {
            if (!IsAgentRunning()) return;
            Thread.Sleep(100);
        }
        throw new TimeoutException("Temperature agent did not stop.");
    }

    private static bool IsAgentRunning()
    {
        try
        {
            using var mutex = Mutex.OpenExisting(HardwareTemperatureAgentOptions.MutexName);
            var acquired = false;
            try { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }

            if (!acquired) return true;
            mutex.ReleaseMutex();
            return false;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // The elevated process owns the mutex. Treat an inaccessible mutex
            // as running instead of starting a second hardware reader.
            return true;
        }
    }

    private static bool ScheduledTaskMatchesCurrentExecutable()
    {
        var result = RunTaskSchedulerForOutput("/Query", "/TN", TaskName, "/XML");
        if (result.ExitCode != 0) return false;
        return result.Output.Contains(Application.ExecutablePath,
            StringComparison.OrdinalIgnoreCase) &&
            result.Output.Contains(HardwareTemperatureAgentOptions.AgentArgument,
                StringComparison.OrdinalIgnoreCase);
    }

    private static void CreateScheduledTask()
    {
        RunTaskScheduler(true,
            "/Create", "/TN", TaskName,
            "/TR", $"\"{Application.ExecutablePath}\" {HardwareTemperatureAgentOptions.AgentArgument}",
            "/SC", "ONLOGON", "/RL", "HIGHEST", "/F");
    }

    private static void TryEndScheduledTask()
    {
        try { _ = RunTaskSchedulerForOutput("/End", "/TN", TaskName); }
        catch { }
    }

    private static (int ExitCode, string Output) RunTaskSchedulerForOutput(
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo {
            FileName = TaskSchedulerPath(), UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo);
        if (process is null) return (-1, string.Empty);
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static void RunTaskScheduler(bool elevated, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo {
            FileName = TaskSchedulerPath(), UseShellExecute = elevated,
            Verb = elevated ? "runas" : string.Empty,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = !elevated
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Task Scheduler could not be started.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Temperature agent task could not be updated (exit code {process.ExitCode}).");
    }

    private static string TaskSchedulerPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
}
