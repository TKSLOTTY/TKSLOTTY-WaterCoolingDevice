using System.Security.Cryptography;
using System.Text;
using HidSharp;

namespace WaterCoolingDevice;

internal enum LedLayoutKind
{
    Fan = 0,
    Strip = 1,
    Matrix = 2
}

internal sealed record LedPortConfiguration(
    int LedCount,
    LedLayoutKind Layout,
    int MatrixWidth = 8,
    int MatrixHeight = 8,
    bool MatrixSerpentine = true);

internal sealed class HidDeviceDescriptor
{
    internal HidDeviceDescriptor(HidDevice device, string productName, string serialNumber)
    {
        Device = device;
        ProductName = productName;
        SerialNumber = serialNumber;
        Identity = string.IsNullOrWhiteSpace(serialNumber)
            ? $"path:{device.DevicePath}"
            : $"serial:{serialNumber}";
    }

    internal HidDevice Device { get; }
    public string ProductName { get; }
    public string SerialNumber { get; }
    public string Identity { get; }
    public bool HasSerialNumber => !string.IsNullOrWhiteSpace(SerialNumber);

    public override string ToString() => HasSerialNumber
        ? $"{ProductName} — Serial: {SerialNumber}"
        : $"{ProductName} — Serial: (なし / unavailable)";
}

internal sealed class DeviceInUseException : IOException
{
    public DeviceInUseException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

internal sealed class HidDeviceClient : IDisposable
{
    public const int VendorId = 0x2E8A;
    public const int ProductId = 0x1144;
    // Current controller firmware exposes the vendor command channel as Report ID 7.
    private const byte ReportId = 7;
    private const byte ResponseFlag = 0x80;

    public const byte GetFanRpm = 0x30;
    public const byte GetDuty = 0x31;
    public const byte GetWaterTemperature = 0x32;
    public const byte SetDuty1Table = 0x41;
    public const byte SetDuty2Table = 0x42;
    public const byte GetDuty1Table = 0x45;
    public const byte GetDuty2Table = 0x46;
    public const byte GetWarningTemperature = 0x47;
    public const byte SetWarningTemperature = 0x44;
    public const byte SetPumpMode = 0x48;
    public const byte SetPumpDuty = 0x49;
    public const byte GetPumpStatus = 0x4A;
    public const byte GetSensorStatus = 0x4B;
    public const byte GetSettingsVersion = 0x4C;
    public const byte SetLedCount = 0x4D;
    public const byte GetLedCount = 0x4E;
    public const byte ApplyLedConfig = 0x4F;
    public const byte SetLedLayout = 0x59;
    public const byte GetLedLayout = 0x5A;
    private const int ApplyLedConfigMagic = 0x0044454C;

    private static readonly string DeviceLockFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WaterCoolingDevice", "DeviceLocks");

    private HidDevice? device;
    private HidStream? stream;
    private FileStream? deviceLock;
    private readonly SemaphoreSlim ioLock = new(1, 1);
    private readonly object stateLock = new();
    private bool disposed;

    public bool IsConnected
    {
        get { lock (stateLock) return stream is not null; }
    }
    public string? ConnectedIdentity { get; private set; }
    public string? ConnectedSerialNumber { get; private set; }

    public static IReadOnlyList<HidDeviceDescriptor> GetConnectedDevices()
    {
        var devices = new List<HidDeviceDescriptor>();
        foreach (var item in DeviceList.Local.GetHidDevices(VendorId, ProductId)
                     .Where(candidate => candidate.GetMaxOutputReportLength() > 0))
        {
            string serialNumber;
            string productName;
            try { serialNumber = item.GetSerialNumber()?.Trim() ?? string.Empty; }
            catch { serialNumber = string.Empty; }
            try { productName = item.GetProductName()?.Trim() ?? string.Empty; }
            catch { productName = string.Empty; }

            if (string.IsNullOrWhiteSpace(productName))
                productName = "Water Cooling Device";

            devices.Add(new HidDeviceDescriptor(item, productName, serialNumber));
        }

        // A single physical controller can expose more than one HID interface.
        // Keep only the vendor interface with the largest report for each USB serial.
        return devices
            .GroupBy(item => item.Identity, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.Device.GetMaxOutputReportLength())
                .ThenByDescending(item => item.Device.GetMaxInputReportLength())
                .First())
            .OrderBy(item => item.HasSerialNumber ? 0 : 1)
            .ThenBy(item => item.SerialNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Device.DevicePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void Connect(HidDeviceDescriptor target)
    {
        ThrowIfDisposed();
        ioLock.Wait();
        HidStream? openedStream = null;
        FileStream? openedDeviceLock = null;
        try
        {
            ThrowIfDisposed();
            DisposeStreamCore();
            Directory.CreateDirectory(DeviceLockFolder);
            var lockPath = Path.Combine(DeviceLockFolder, LockFileName(target.Identity));

            try
            {
                openedDeviceLock = new FileStream(lockPath, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                throw new DeviceInUseException(
                    "選択したコントローラは他のWater Cooling Deviceアプリで使用中です。", ex);
            }

            try
            {
                openedStream = target.Device.Open();
                openedStream.ReadTimeout = 1500;
                openedStream.WriteTimeout = 1500;

                lock (stateLock)
                {
                    ObjectDisposedException.ThrowIf(disposed, this);
                    device = target.Device;
                    stream = openedStream;
                    deviceLock = openedDeviceLock;
                    ConnectedIdentity = target.Identity;
                    ConnectedSerialNumber = target.SerialNumber;
                    openedStream = null;
                    openedDeviceLock = null;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new DeviceInUseException(
                    "選択したコントローラは他のアプリで使用中、またはアクセスできません。", ex);
            }
        }
        finally
        {
            openedStream?.Dispose();
            openedDeviceLock?.Dispose();
            ioLock.Release();
        }
    }

    public async Task ConfigurePumpAsync(bool enabled, int duty)
    {
        duty = Math.Clamp(duty, 35, 100);
        if (await QueryAsync(SetPumpDuty, 0, duty) != duty)
            throw new IOException("Pump Dutyの確認値が一致しません。");
        var mode = enabled ? 1 : 0;
        if (await QueryAsync(SetPumpMode, 0, mode) != mode)
            throw new IOException("PUMPモードの確認値が一致しません。");
    }

    public async Task<(int Port1, int Port2)> ReadLedCountsAsync()
    {
        var port1 = await QueryAsync(GetLedCount, 0);
        var port2 = await QueryAsync(GetLedCount, 1);
        if (port1 is < 1 or > 64 || port2 is < 1 or > 64)
            throw new InvalidDataException($"本体のLED数設定が不正です。GPIO0={port1}, GPIO1={port2}");
        return (port1, port2);
    }

    public async Task<(LedPortConfiguration Port1, LedPortConfiguration Port2)>
        ReadLedConfigurationsAsync()
    {
        var counts = await ReadLedCountsAsync();
        try
        {
            var port1 = UnpackLayout(counts.Port1, await QueryAsync(GetLedLayout, 0));
            var port2 = UnpackLayout(counts.Port2, await QueryAsync(GetLedLayout, 1));
            return (port1, port2);
        }
        catch (TimeoutException)
        {
            // Firmware before the layout command only knows circular fan geometry.
            return (new(counts.Port1, LedLayoutKind.Fan),
                    new(counts.Port2, LedLayoutKind.Fan));
        }
    }

    public async Task ConfigureLedLayoutsAsync(
        LedPortConfiguration port1, LedPortConfiguration port2)
    {
        ValidateLayout(port1, nameof(port1));
        ValidateLayout(port2, nameof(port2));
        if (await QueryAsync(SetLedCount, 0, port1.LedCount) != port1.LedCount)
            throw new IOException("GPIO 0 LED数の書込み確認値が一致しません。");
        if (await QueryAsync(SetLedCount, 1, port2.LedCount) != port2.LedCount)
            throw new IOException("GPIO 1 LED数の書込み確認値が一致しません。");
        var packed1 = PackLayout(port1);
        var packed2 = PackLayout(port2);
        if (await QueryAsync(SetLedLayout, 0, packed1) != packed1)
            throw new IOException("GPIO 0 LED配置の書込み確認値が一致しません。ファームウェアを更新してください。");
        if (await QueryAsync(SetLedLayout, 1, packed2) != packed2)
            throw new IOException("GPIO 1 LED配置の書込み確認値が一致しません。ファームウェアを更新してください。");
        var readBack = await ReadLedConfigurationsAsync();
        if (readBack.Port1 != port1 || readBack.Port2 != port2)
            throw new IOException("LED構成の再読込み値が一致しません。");
        if (await QueryAsync(ApplyLedConfig, 0, ApplyLedConfigMagic) != ApplyLedConfigMagic)
            throw new IOException("LED構成の適用応答が一致しません。");
    }

    private static void ValidateLayout(LedPortConfiguration configuration, string name)
    {
        if (configuration.LedCount is < 1 or > 64)
            throw new ArgumentOutOfRangeException(name, "LED数は1～64です。");
        if (configuration.Layout == LedLayoutKind.Matrix &&
            (configuration.MatrixWidth is < 1 or > 64 ||
             configuration.MatrixHeight is < 1 or > 64 ||
             configuration.MatrixWidth * configuration.MatrixHeight != configuration.LedCount))
            throw new ArgumentException("マトリックスの横×縦とLED数が一致しません。", name);
    }

    private static int PackLayout(LedPortConfiguration configuration)
    {
        var width = configuration.Layout == LedLayoutKind.Matrix
            ? configuration.MatrixWidth : 8;
        var height = configuration.Layout == LedLayoutKind.Matrix
            ? configuration.MatrixHeight : 8;
        var options = configuration.Layout == LedLayoutKind.Matrix &&
                      configuration.MatrixSerpentine ? 1 : 0;
        return (int)configuration.Layout | (width << 8) | (height << 16) |
               (options << 24);
    }

    private static LedPortConfiguration UnpackLayout(int ledCount, int packed)
    {
        var raw = unchecked((uint)packed);
        var layout = (LedLayoutKind)(raw & 0xFF);
        var width = (int)((raw >> 8) & 0xFF);
        var height = (int)((raw >> 16) & 0xFF);
        var serpentine = ((raw >> 24) & 1) != 0;
        var configuration = new LedPortConfiguration(
            ledCount, layout, width, height, serpentine);
        ValidateLayout(configuration, nameof(packed));
        if (layout is < LedLayoutKind.Fan or > LedLayoutKind.Matrix)
            throw new InvalidDataException($"本体のLED配置設定が不正です。Layout={layout}");
        return configuration;
    }

    public async Task<int> QueryAsync(byte command, byte channel, int value = 0)
    {
        ThrowIfDisposed();
        await ioLock.WaitAsync();
        try
        {
            HidDevice activeDevice;
            HidStream activeStream;
            lock (stateLock)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                activeDevice = device ??
                    throw new IOException("水冷ファンコントローラに接続されていません。");
                activeStream = stream ??
                    throw new IOException("水冷ファンコントローラに接続されていません。");
            }

            var output = new byte[Math.Max(64, activeDevice.GetMaxOutputReportLength())];
            output[0] = ReportId;
            output[1] = command;
            output[2] = channel;
            BitConverter.GetBytes(value).CopyTo(output, 3);
            await Task.Run(() => activeStream.Write(output));

            var expected = (byte)(ResponseFlag | command);
            var deadline = Environment.TickCount64 + 1500;
            while (Environment.TickCount64 < deadline)
            {
                var input = new byte[Math.Max(64, activeDevice.GetMaxInputReportLength())];
                var count = await Task.Run(() => activeStream.Read(input));
                if (count >= 7 && input[0] == ReportId &&
                    input[1] == expected && input[2] == channel)
                    return BitConverter.ToInt32(input, 3);
            }
            throw new TimeoutException("水冷ファンコントローラからの応答がありません。");
        }
        finally
        {
            ioLock.Release();
        }
    }

    public async Task<int[]> ReadTableAsync(bool fan1)
    {
        var command = fan1 ? GetDuty1Table : GetDuty2Table;
        var values = new int[5];
        for (byte i = 0; i < values.Length; i++) values[i] = await QueryAsync(command, i);
        return values;
    }

    public async Task WriteTableAsync(bool fan1, IReadOnlyList<int> values)
    {
        var command = fan1 ? SetDuty1Table : SetDuty2Table;
        for (byte i = 0; i < 5; i++)
        {
            var acknowledged = await QueryAsync(command, i, values[i]);
            if (acknowledged != values[i]) throw new IOException($"Point {i}の確認値が一致しません。");
        }
    }

    public void Disconnect()
    {
        if (IsDisposed()) return;
        ioLock.Wait();
        try
        {
            if (!IsDisposed()) DisposeStreamCore();
        }
        finally { ioLock.Release(); }
    }

    private void DisposeStreamCore()
    {
        var (streamToDispose, lockToDispose) = DetachStreamCore();
        DisposeDetachedResources(streamToDispose, lockToDispose);
    }

    private (HidStream? Stream, FileStream? DeviceLock) DetachStreamCore()
    {
        lock (stateLock)
        {
            var detachedStream = stream;
            var detachedDeviceLock = deviceLock;
            stream = null;
            device = null;
            ConnectedIdentity = null;
            ConnectedSerialNumber = null;
            deviceLock = null;
            return (detachedStream, detachedDeviceLock);
        }
    }

    private static void DisposeDetachedResources(
        HidStream? streamToDispose, FileStream? lockToDispose)
    {
        try { streamToDispose?.Dispose(); }
        finally { lockToDispose?.Dispose(); }
    }

    private bool IsDisposed()
    {
        lock (stateLock) return disposed;
    }

    private void ThrowIfDisposed()
    {
        lock (stateLock) ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static string LockFileName(string identity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToUpperInvariant()));
        return $"{Convert.ToHexString(hash)}.lock";
    }

    public void Dispose()
    {
        lock (stateLock)
        {
            if (disposed) return;
            disposed = true;
        }

        // Never wait for a HID operation or USB driver call on the UI thread.
        // Windows shutdown must continue even if a read is stalled.
        var (streamToDispose, lockToDispose) = DetachStreamCore();
        _ = Task.Run(() => DisposeDetachedResources(streamToDispose, lockToDispose));
    }
}
