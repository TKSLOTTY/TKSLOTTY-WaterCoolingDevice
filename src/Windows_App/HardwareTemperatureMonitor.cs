using System.Text.Json;
using LibreHardwareMonitor.Hardware;

namespace WaterCoolingDevice;

internal sealed record HostTemperatureSnapshot(
    float? CpuCelsius, float? GpuCelsius, string? Error = null,
    string? CpuName = null, string? GpuName = null, float? GpuUsage = null);

internal static class HardwareTemperatureAgentOptions
{
    public const string AgentArgument = "--temperature-agent";
    public const string MutexName = @"Local\WaterCoolingDevice.TemperatureAgent.v3";
}

internal static class HardwareTemperatureStorage
{
    private sealed record StoredSnapshot(
        DateTime UpdatedUtc, float? CpuCelsius, float? GpuCelsius, string? Error,
        string? CpuName = null, string? GpuName = null, float? GpuUsage = null);

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WaterCoolingDevice");
    private static readonly string SnapshotPath = Path.Combine(
        Folder, "hardware-temperatures-v3.json");

    public static void Write(HostTemperatureSnapshot snapshot)
    {
        Directory.CreateDirectory(Folder);
        var temporaryPath = Path.Combine(Folder,
            $"hardware-temperatures.{Environment.ProcessId}.tmp");
        try
        {
            var stored = new StoredSnapshot(DateTime.UtcNow,
                snapshot.CpuCelsius, snapshot.GpuCelsius, snapshot.Error,
                snapshot.CpuName, snapshot.GpuName, snapshot.GpuUsage);
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(stored));
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(temporaryPath, SnapshotPath, true);
                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(25);
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    Thread.Sleep(25);
                }
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public static bool TryReadFresh(out HostTemperatureSnapshot snapshot)
    {
        try
        {
            using var stream = new FileStream(SnapshotPath, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var stored = JsonSerializer.Deserialize<StoredSnapshot>(stream);
            if (stored is null || DateTime.UtcNow - stored.UpdatedUtc > TimeSpan.FromSeconds(10))
                throw new IOException("Temperature data is stale.");
            snapshot = new(stored.CpuCelsius, stored.GpuCelsius, stored.Error,
                stored.CpuName, stored.GpuName, stored.GpuUsage);
            return true;
        }
        catch (Exception ex)
        {
            snapshot = new(null, null, ex.Message);
            return false;
        }
    }

    public static void Clear()
    {
        try { if (File.Exists(SnapshotPath)) File.Delete(SnapshotPath); }
        catch { }
    }
}

// The normal-privilege UI reads snapshots written by the elevated sensor agent.
internal sealed class HardwareTemperatureMonitor : IDisposable
{
    private bool enabled;
    private bool disposed;

    public void SetEnabled(bool value) => enabled = value && !disposed;

    public HostTemperatureSnapshot Read()
    {
        if (!enabled || disposed) return new(null, null);

        HardwareTemperatureStorage.TryReadFresh(out var snapshot);
        return snapshot;
    }

    public void Dispose()
    {
        disposed = true;
        enabled = false;
    }
}

// LibreHardwareMonitor is opened only inside the sensor agent.
internal sealed class LocalHardwareTemperatureReader : IDisposable
{
    private readonly Computer computer = new() {
        IsCpuEnabled = true,
        IsGpuEnabled = true
    };
    private readonly UpdateVisitor visitor = new();
    private readonly object sync = new();
    private bool opened;
    private bool disposed;
    private string? initializationError;

    public LocalHardwareTemperatureReader()
    {
        try
        {
            computer.Open();
            opened = true;
        }
        catch (Exception ex)
        {
            initializationError = ex.Message;
        }
    }

    public HostTemperatureSnapshot Read()
    {
        lock (sync)
        {
            if (disposed || !opened)
                return new(null, null,
                    initializationError ?? "Hardware monitor is unavailable.");

            try
            {
                computer.Accept(visitor);
                var cpu = ReadPreferredHardware(
                    new[] { HardwareType.Cpu },
                    new[] { "Tctl/Tdie", "CPU Package", "Package", "Core Average" });
                var gpu = ReadPreferredHardware(
                    new[] { HardwareType.GpuAmd, HardwareType.GpuNvidia, HardwareType.GpuIntel },
                    new[] { "GPU Core", "Core", "GPU Hot Spot" });
                return new(cpu.Temperature, gpu.Temperature, null, cpu.Name, gpu.Name, ReadGpuUsage(gpu.Name));
            }
            catch (Exception ex)
            {
                return new(null, null, ex.Message);
            }
        }
    }

    private float? ReadGpuUsage(string? gpuName)
    {
        var hardware = computer.Hardware.FirstOrDefault(item =>
            item.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel &&
            string.Equals(CleanName(item.Name), gpuName, StringComparison.Ordinal));
        if (hardware is null) return null;
        var sensor = hardware.Sensors.FirstOrDefault(item =>
            item.SensorType == SensorType.Load &&
            string.Equals(item.Name, "GPU Core", StringComparison.OrdinalIgnoreCase));
        return sensor?.Value is float value && float.IsFinite(value)
            ? Math.Clamp(value, 0, 100) : null;
    }

    private (float? Temperature, string? Name) ReadPreferredHardware(
        IReadOnlyCollection<HardwareType> types,
        IReadOnlyList<string> preferredNames)
    {
        var candidates = computer.Hardware
            .Where(item => types.Contains(item.HardwareType)).ToArray();

        foreach (var preferred in preferredNames)
        {
            foreach (var hardware in candidates)
            {
                var sensors = new List<ISensor>();
                CollectTemperatureSensors(hardware, sensors);
                var match = sensors.FirstOrDefault(sensor =>
                    sensor.Name.Contains(preferred, StringComparison.OrdinalIgnoreCase) &&
                    IsValid(sensor.Value));
                if (match?.Value is float value) return (value, CleanName(hardware.Name));
            }
        }

        foreach (var hardware in candidates)
        {
            var sensors = new List<ISensor>();
            CollectTemperatureSensors(hardware, sensors);
            var value = sensors.Select(sensor => sensor.Value).FirstOrDefault(IsValid);
            if (value is not null) return (value, CleanName(hardware.Name));
        }
        return (null, candidates.Select(item => CleanName(item.Name))
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)));
    }

    private static string? CleanName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void CollectTemperatureSensors(IHardware hardware, ICollection<ISensor> result)
    {
        foreach (var sensor in hardware.Sensors)
            if (sensor.SensorType == SensorType.Temperature) result.Add(sensor);
        foreach (var subHardware in hardware.SubHardware)
            CollectTemperatureSensors(subHardware, result);
    }

    // Unsupported CPU paths can expose a zero-valued placeholder sensor.
    private static bool IsValid(float? value) => value is > 0 and < 150;

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            if (opened) computer.Close();
            opened = false;
        }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitParameter(IParameter parameter) { }
        public void VisitSensor(ISensor sensor) { }
    }
}
