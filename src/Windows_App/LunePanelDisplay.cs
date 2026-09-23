using System.Drawing.Imaging;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace WaterCoolingDevice;

internal enum LunePanelTheme { Lune, Cyber, Custom }

internal sealed record LunePanelOptions
{
    public bool StudioEnabled { get; init; }
    public bool DesktopAutoShow { get; init; }
    public bool RestoreWindowPosition { get; init; } = true;
    public int? WindowX { get; init; }
    public int? WindowY { get; init; }
    public bool Enabled { get; init; }
    public bool Landscape { get; init; } = true;
    public bool Reverse { get; init; }
    public bool Animate { get; init; }
    public LunePanelTheme Theme { get; init; }
    public string ThemeFile { get; init; } = string.Empty;
    public int Brightness { get; init; } = 50;
    public string Port { get; init; } = "AUTO";
    public int Width => Landscape ? 480 : 320;
    public int Height => Landscape ? 320 : 480;
    public int Orientation => (Landscape ? 2 : 0) + (Reverse ? 1 : 0);
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WaterCoolingDevice", "lune-panel.json");
    public static LunePanelOptions Load()
    {
        try {
            var value = JsonSerializer.Deserialize<LunePanelOptions>(File.ReadAllText(SettingsPath)) ?? new();
            return value with {
                Brightness = Math.Clamp(value.Brightness, 0, 100),
                Port = value.Port ?? "AUTO",
                Theme = Enum.IsDefined(value.Theme) ? value.Theme : LunePanelTheme.Lune,
                ThemeFile = value.ThemeFile ?? string.Empty
            };
        } catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var temp = SettingsPath + $".{Environment.ProcessId}.tmp";
        try {
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, SettingsPath, true);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

// Protocol reconstructed from the supplied vendor binary and verified on USB35INCHIPSV2.
internal static class LunePanelProtocol
{
    public static byte[] Header(int command, int x = 0, int y = 0, int ex = 0, int ey = 0)
    {
        if ((uint)command > 255 || new[] { x, y, ex, ey }.Any(v => (uint)v > 1023))
            throw new ArgumentOutOfRangeException(nameof(x));
        return [(byte)(x >> 2), (byte)(((x & 3) << 6) | (y >> 4)),
            (byte)(((y & 15) << 4) | (ex >> 6)), (byte)(((ex & 63) << 2) | (ey >> 8)),
            (byte)ey, (byte)command];
    }
    public static byte[] Orientation(LunePanelOptions options)
    {
        var packet = new byte[16];
        packet[5] = 121; packet[6] = (byte)(100 + options.Orientation);
        packet[7] = (byte)(options.Width >> 8); packet[8] = (byte)options.Width;
        packet[9] = (byte)(options.Height >> 8); packet[10] = (byte)options.Height;
        return packet;
    }
    public static byte[] Encode(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bits = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try {
            var row = new byte[bitmap.Width * 4];
            var result = new byte[bitmap.Width * bitmap.Height * 2];
            var index = 0;
            for (var y = 0; y < bitmap.Height; y++) {
                Marshal.Copy(bits.Scan0 + y * bits.Stride, row, 0, row.Length);
                for (var x = 0; x < bitmap.Width; x++) {
                    var b = row[x * 4]; var g = row[x * 4 + 1]; var r = row[x * 4 + 2];
                    var pixel = ((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3);
                    result[index++] = (byte)pixel; result[index++] = (byte)(pixel >> 8);
                }
            }
            return result;
        } finally { bitmap.UnlockBits(bits); }
    }
    public static Rectangle ChangedBounds(byte[] current, byte[]? previous, int width, int height)
    {
        if (current.Length != width * height * 2) throw new ArgumentException("Invalid frame size");
        if (previous is null || previous.Length != current.Length) return new(0, 0, width, height);
        var left = width; var top = height; var right = -1; var bottom = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) {
                var i = (y * width + x) * 2;
                if (current[i] == previous[i] && current[i + 1] == previous[i + 1]) continue;
                left = Math.Min(left, x); right = Math.Max(right, x);
                top = Math.Min(top, y); bottom = y;
            }
        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    public static IReadOnlyList<Rectangle> ChangedRegions(
        byte[] current, byte[]? previous, int width, int height, int tileSize = 16)
    {
        if (current.Length != width * height * 2) throw new ArgumentException("Invalid frame size");
        if (previous is null || previous.Length != current.Length)
            return [new Rectangle(0, 0, width, height)];

        var tileColumns = (width + tileSize - 1) / tileSize;
        var tileRows = (height + tileSize - 1) / tileSize;
        var dirty = new bool[tileColumns, tileRows];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++) {
                var i = (y * width + x) * 2;
                if (current[i] != previous[i] || current[i + 1] != previous[i + 1])
                    dirty[x / tileSize, y / tileSize] = true;
            }

        var regions = new List<Rectangle>();
        for (var ty = 0; ty < tileRows; ty++) {
            for (var tx = 0; tx < tileColumns;) {
                if (!dirty[tx, ty]) { tx++; continue; }
                var start = tx;
                while (tx + 1 < tileColumns && dirty[tx + 1, ty]) tx++;
                var next = new Rectangle(start * tileSize, ty * tileSize,
                    Math.Min(width, (tx + 1) * tileSize) - start * tileSize,
                    Math.Min(tileSize, height - ty * tileSize));
                var mergeIndex = regions.FindLastIndex(region => region.Bottom == next.Top &&
                    region.Left == next.Left && region.Right == next.Right);
                if (mergeIndex >= 0) {
                    var prior = regions[mergeIndex];
                    regions[mergeIndex] = Rectangle.FromLTRB(
                        prior.Left, prior.Top, prior.Right, next.Bottom);
                } else regions.Add(next);
                tx++;
            }
        }

        if (regions.Count <= 12) return regions;
        var bounds = ChangedBounds(current, previous, width, height);
        return bounds.IsEmpty ? [] : [bounds];
    }
}

internal sealed class LunePanelDisplay : IDisposable
{
    private sealed record Frame(byte[] Pixels, LunePanelOptions Options);
    private readonly object gate = new();
    private Frame? pending;
    private LunePanelOptions options = new();
    private volatile bool stopping;
    private string status = "OFF";
    private readonly Task worker;
    public string Status => Volatile.Read(ref status);
    public LunePanelDisplay() => worker = Task.Run(RunAsync);
    public void Configure(LunePanelOptions value)
    {
        lock (gate) { options = value; pending = null; }
    }
    public void Submit(Bitmap bitmap, LunePanelOptions value)
    {
        byte[] pixels;
        if (bitmap.Width == value.Width && bitmap.Height == value.Height) {
            pixels = LunePanelProtocol.Encode(bitmap);
        } else {
            // Last line of defence against a theme/output orientation mismatch.
            // Copy without scaling; GDI clips overflow at the panel boundary.
            using var normalized = new Bitmap(value.Width, value.Height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(normalized);
            graphics.Clear(Color.Black);
            graphics.DrawImageUnscaled(bitmap, 0, 0);
            pixels = LunePanelProtocol.Encode(normalized);
        }
        var frame = new Frame(pixels, value);
        lock (gate) { if (!stopping && options == value) pending = frame; }
    }
    public static string? DetectPort()
    {
        using var devices = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Enum\USB\VID_1A86&PID_5722");
        if (devices is null) return null;
        var ports = SerialPort.GetPortNames();
        foreach (var name in devices.GetSubKeyNames().Where(n => n.StartsWith("USB35INCH", StringComparison.OrdinalIgnoreCase))) {
            using var parameters = devices.OpenSubKey(name + @"\Device Parameters");
            if (parameters?.GetValue("PortName") is string port && ports.Contains(port, StringComparer.OrdinalIgnoreCase))
                return port;
        }
        return null;
    }
    private async Task RunAsync()
    {
        SerialPort? port = null;
        LunePanelOptions? applied = null;
        byte[]? previous = null;
        var retryAt = DateTime.MinValue;
        var lastDiagnostic = DateTime.MinValue;
        var sentFrames = 0;
        void Diagnostic(string message, LunePanelOptions value)
        {
            if (!Environment.GetCommandLineArgs().Contains("--lune-panel-diagnostics") ||
                DateTime.UtcNow - lastDiagnostic < TimeSpan.FromSeconds(5)) return;
            lastDiagnostic = DateTime.UtcNow;
            try {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "lune-panel-live-status.json"),
                    JsonSerializer.Serialize(new { timestampUtc = DateTime.UtcNow, status = message,
                        sentFrames, value.Width, value.Height, value.Orientation },
                        new JsonSerializerOptions { WriteIndented = true }));
            } catch { /* Diagnostics must never interrupt display or telemetry. */ }
        }
        void Close(bool screenOff)
        {
            if (port is null) return;
            try {
                if (screenOff && port.IsOpen) {
                    port.Write(LunePanelProtocol.Header(108), 0, 6);
                    port.BaseStream.Flush();
                }
            } catch { }
            try { port.Dispose(); } catch { }
            port = null; previous = null; applied = null;
        }
        try {
            while (!stopping) {
                await Task.Delay(40).ConfigureAwait(false);
                Frame? frame; LunePanelOptions desired;
                lock (gate) { desired = options; frame = pending; pending = null; }
                if (!desired.Enabled) { Close(true); Volatile.Write(ref status, "OFF"); continue; }
                if (applied is not null && desired.Port != applied.Port) { Close(false); retryAt = DateTime.MinValue; }
                if (frame is null || frame.Options != desired || DateTime.UtcNow < retryAt) continue;
                try {
                    if (port is null) {
                        var name = desired.Port == "AUTO" ? DetectPort() : desired.Port;
                        if (name is null) throw new IOException(
                            "外部パネルが見つかりません。TURZXを接続すると画面外へ表示できます。");
                        port = new SerialPort(name, 115200, Parity.None, 8, StopBits.One) {
                            DtrEnable = true, RtsEnable = true, Handshake = Handshake.None,
                            ReadTimeout = 1000, WriteTimeout = 10000
                        };
                        port.Open();
                        await Task.Delay(100).ConfigureAwait(false);
                        port.Write(LunePanelProtocol.Header(255), 0, 6);
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                    if (applied != desired) {
                        previous = null;
                        var orientation = LunePanelProtocol.Orientation(desired);
                        port.Write(orientation, 0, orientation.Length);
                        await Task.Delay(20).ConfigureAwait(false);
                        port.Write(LunePanelProtocol.Header(109), 0, 6);
                        await Task.Delay(20).ConfigureAwait(false);
                        var brightness = 255 - Math.Clamp(desired.Brightness, 0, 100) * 255 / 100;
                        port.Write(LunePanelProtocol.Header(110, brightness), 0, 6);
                        await Task.Delay(20).ConfigureAwait(false);
                        applied = desired;
                    }
                    var regions = LunePanelProtocol.ChangedRegions(
                        frame.Pixels, previous, desired.Width, desired.Height);
                    if (regions.Count > 0) {
                        foreach (var bounds in regions) {
                            port.Write(LunePanelProtocol.Header(197, bounds.X, bounds.Y,
                                bounds.Right - 1, bounds.Bottom - 1), 0, 6);
                            // Send each compact dirty region as one contiguous RGB565 block.
                            var pixels = ExtractRectangle(frame.Pixels, desired.Width, bounds);
                            for (var offset = 0; offset < pixels.Length; offset += 2560)
                                port.Write(pixels, offset, Math.Min(2560, pixels.Length - offset));
                            port.BaseStream.Flush();
                            await Task.Delay(20).ConfigureAwait(false);
                        }
                        await Task.Delay(desired.Animate ? 800 : 300).ConfigureAwait(false);
                        previous = frame.Pixels;
                        sentFrames++;
                    }
                    Volatile.Write(ref status, $"送信中 / Sending · {port.PortName} · {desired.Width} × {desired.Height}");
                    Diagnostic(Status, desired);
                } catch (UnauthorizedAccessException) {
                    Close(false);
                    Volatile.Write(ref status,
                        "接続できません。純正UsbMonitorを通知領域も含めて終了し、TURZXを差し直してください。");
                    retryAt = DateTime.UtcNow.AddSeconds(3);
                    Diagnostic(Status, desired);
                } catch (Exception ex) {
                    Close(false);
                    Volatile.Write(ref status, "再接続待ち / Retrying · " + ex.Message);
                    retryAt = DateTime.UtcNow.AddSeconds(3);
                    Diagnostic(Status, desired);
                }
            }
        } finally { Close(true); }
    }
    public void Dispose()
    {
        stopping = true;
        lock (gate) pending = null;
        // Only the worker owns the serial handle; bounded writes keep shutdown finite.
        worker.Wait(TimeSpan.FromSeconds(12));
    }
    internal Task Completion => worker;
    internal static byte[] ExtractRectangle(byte[] pixels, int stridePixels, Rectangle bounds)
    {
        var result = new byte[bounds.Width * bounds.Height * 2];
        for (var row = 0; row < bounds.Height; row++)
            Buffer.BlockCopy(pixels, ((bounds.Y + row) * stridePixels + bounds.X) * 2,
                result, row * bounds.Width * 2, bounds.Width * 2);
        return result;
    }
}
