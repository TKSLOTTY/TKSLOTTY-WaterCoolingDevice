using System.Security.Cryptography;
using System.Text;
using HidSharp;

namespace WaterCoolingDevice;

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
    public const byte ReportId = 7;
    public const byte ResponseFlag = 0x80;

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
    public const byte Ping = 0x70;

    private static readonly string DeviceLockFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WaterCoolingDevice", "DeviceLocks");

    private HidDevice? device;
    private HidStream? stream;
    private FileStream? deviceLock;
    private readonly SemaphoreSlim ioLock = new(1, 1);
    private bool disposed;

    public bool IsConnected => stream is not null;
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
        ObjectDisposedException.ThrowIf(disposed, this);
        ioLock.Wait();
        try
        {
            DisposeStreamCore();
            Directory.CreateDirectory(DeviceLockFolder);
            var lockPath = Path.Combine(DeviceLockFolder, LockFileName(target.Identity));

            try
            {
                deviceLock = new FileStream(lockPath, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                throw new DeviceInUseException(
                    "選択したコントローラは他のWater Cooling Deviceアプリで使用中です。", ex);
            }

            try
            {
                device = target.Device;
                stream = device.Open();
                stream.ReadTimeout = 1500;
                stream.WriteTimeout = 1500;
                ConnectedIdentity = target.Identity;
                ConnectedSerialNumber = target.SerialNumber;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DisposeStreamCore();
                throw new DeviceInUseException(
                    "選択したコントローラは他のアプリで使用中、またはアクセスできません。", ex);
            }
        }
        finally
        {
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

    public async Task<int> QueryAsync(byte command, byte channel, int value = 0)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await ioLock.WaitAsync();
        try
        {
            if (device is null || stream is null)
                throw new IOException("水冷ファンコントローラに接続されていません。");

            var output = new byte[Math.Max(64, device.GetMaxOutputReportLength())];
            output[0] = ReportId;
            output[1] = command;
            output[2] = channel;
            BitConverter.GetBytes(value).CopyTo(output, 3);
            await Task.Run(() => stream.Write(output));

            var expected = (byte)(ResponseFlag | command);
            var deadline = Environment.TickCount64 + 1500;
            while (Environment.TickCount64 < deadline)
            {
                var input = new byte[Math.Max(64, device.GetMaxInputReportLength())];
                var count = await Task.Run(() => stream.Read(input));
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
        if (disposed) return;
        ioLock.Wait();
        try { DisposeStreamCore(); }
        finally { ioLock.Release(); }
    }

    private void DisposeStreamCore()
    {
        stream?.Dispose();
        stream = null;
        device = null;
        ConnectedIdentity = null;
        ConnectedSerialNumber = null;
        deviceLock?.Dispose();
        deviceLock = null;
    }

    private static string LockFileName(string identity)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToUpperInvariant()));
        return $"{Convert.ToHexString(hash)}.lock";
    }

    public void Dispose()
    {
        if (disposed) return;
        ioLock.Wait();
        try
        {
            if (disposed) return;
            DisposeStreamCore();
            disposed = true;
        }
        finally
        {
            ioLock.Release();
        }
    }
}
