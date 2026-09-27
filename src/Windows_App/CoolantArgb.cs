using System.Text.Json;
using HidSharp;

namespace WaterCoolingDevice;

internal sealed record CoolantArgbOptions
{
    public bool Enabled { get; init; }
    public int LowTemperature { get; init; } = 25;
    public int MidTemperature { get; init; } = 35;
    public int HighTemperature { get; init; } = 45;
    public int LowColor { get; init; } = Color.DeepSkyBlue.ToArgb();
    public int MidColor { get; init; } = Color.Gold.ToArgb();
    public int HighColor { get; init; } = Color.Red.ToArgb();
    public int Brightness { get; init; } = 50;
    private static string FilePath => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "WaterCoolingDevice", "coolant-argb.json");
    public bool Valid => LowTemperature >= -20 && HighTemperature <= 60 &&
        LowTemperature < MidTemperature && MidTemperature < HighTemperature && Brightness is >= 0 and <= 100;
    public static CoolantArgbOptions Load()
    {
        try {
            var options = JsonSerializer.Deserialize<CoolantArgbOptions>(File.ReadAllText(FilePath));
            return options is { Valid: true } ? options : new();
        } catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
    public Color ColorAt(float temperature)
    {
        if (!Valid || !float.IsFinite(temperature)) throw new ArgumentOutOfRangeException(nameof(temperature));
        var lower = temperature <= MidTemperature;
        var a = Color.FromArgb(lower ? LowColor : MidColor);
        var b = Color.FromArgb(lower ? MidColor : HighColor);
        var t = Math.Clamp((temperature - (lower ? LowTemperature : MidTemperature)) /
            (float)(lower ? MidTemperature - LowTemperature : HighTemperature - MidTemperature), 0, 1);
        int Mix(int x, int y) => (int)Math.Round((x + (y - x) * t) * Brightness / 100f);
        return Color.FromArgb(Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B));
    }
}

// Uses the two LampArray feature-report collections on the selected WCD controller.
// Other lighting software must release the device before this mode is enabled.
internal sealed class CoolantArgbOutput : IDisposable
{
    private readonly object gate = new();
    private readonly List<(HidStream Stream, int Length, int Count)> ports = new();
    private string? serial;
    private volatile bool disposed;
    private volatile bool turnOffOnRelease = true;
    private int generation;
    public int Generation => Volatile.Read(ref generation);
    internal static byte[] RangeReport(int length, int count, Color color)
    {
        if (length < 10 || count is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(count));
        var data = new byte[length];
        data[0] = 5; data[1] = 1; // report ID, completed frame
        data[4] = (byte)(count - 1); data[5] = 0;
        data[6] = color.R; data[7] = color.G; data[8] = color.B; data[9] = 255;
        return data;
    }
    public bool Update(string? selectedSerial, Color color, int expectedGeneration)
    {
        if (!Monitor.TryEnter(gate)) return false;
        try {
            if (disposed || generation != expectedGeneration) return false;
            if (string.IsNullOrWhiteSpace(selectedSerial)) throw new IOException("Controller serial unavailable");
            try {
                if (serial != selectedSerial || ports.Count == 0) {
                    ReleaseCore();
                    foreach (var device in DeviceList.Local.GetHidDevices(HidDeviceClient.VendorId, HidDeviceClient.ProductId)) {
                        if (device.GetMaxFeatureReportLength() < 51 ||
                            !string.Equals(device.GetSerialNumber()?.Trim(), selectedSerial, StringComparison.OrdinalIgnoreCase)) continue;
                        var stream = device.Open();
                        try {
                            stream.ReadTimeout = stream.WriteTimeout = 500;
                            var report = new byte[device.GetMaxFeatureReportLength()]; report[0] = 1;
                            stream.GetFeature(report);
                            var count = report[1] | report[2] << 8;
                            if (count is < 1 or > 64) throw new IOException("Invalid lamp count");
                            ports.Add((stream, report.Length, count));
                        } catch { stream.Dispose(); throw; }
                    }
                    if (ports.Count != 2) throw new IOException("Expected two WCD LampArray ports");
                    serial = selectedSerial;
                }
                foreach (var port in ports) {
                    if (disposed || generation != expectedGeneration) return false;
                    port.Stream.SetFeature(RangeReport(port.Length, port.Count, color));
                }
                return true;
            } catch { ReleaseCore(); throw; }
        } finally { Monitor.Exit(gate); }
    }
    private void ReleaseCore()
    {
        foreach (var port in ports) {
            try { if (turnOffOnRelease) port.Stream.SetFeature(RangeReport(port.Length, port.Count, Color.Black)); } catch { }
            port.Stream.Dispose();
        }
        ports.Clear(); serial = null;
    }
    public void Release()
    {
        Interlocked.Increment(ref generation);
        _ = Task.Run(() => { lock (gate) ReleaseCore(); });
    }
    public void Dispose() => Dispose(true);
    public void Dispose(bool turnOff)
    {
        turnOffOnRelease = turnOff;
        disposed = true;
        Release();
    }
}
