using HidSharp;

namespace WaterCoolingDevice;

internal sealed class HidDeviceClient : IDisposable
{
    private const int VendorId = 0x2E8A;
    private const int ProductId = 0x1144;
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

    public async Task ConfigurePumpAsync(bool enabled, int duty)
    {
        duty = Math.Clamp(duty, 35, 100);
        if (await QueryAsync(SetPumpDuty, 0, duty) != duty)
            throw new IOException("Pump Dutyの確認値が一致しません。");
        var mode = enabled ? 1 : 0;
        if (await QueryAsync(SetPumpMode, 0, mode) != mode)
            throw new IOException("PUMPモードの確認値が一致しません。");
    }

    private HidDevice? device;
    private HidStream? stream;
    private readonly SemaphoreSlim ioLock = new(1, 1);

    public bool IsConnected => stream is not null;

    public void Connect()
    {
        DisposeStream();
        device = DeviceList.Local.GetHidDevices(VendorId, ProductId)
            .Where(item => item.GetMaxOutputReportLength() > 0)
            .OrderByDescending(item => item.GetMaxOutputReportLength())
            .FirstOrDefault()
            ?? throw new IOException("Vendor HIDが見つかりません。");

        stream = device.Open();
        stream.ReadTimeout = 1500;
        stream.WriteTimeout = 1500;
    }

    public async Task<int> QueryAsync(byte command, byte channel, int value = 0)
    {
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

    private void DisposeStream()
    {
        stream?.Dispose();
        stream = null;
        device = null;
    }

    public void Disconnect() => DisposeStream();

    public void Dispose()
    {
        DisposeStream();
        ioLock.Dispose();
    }
}
