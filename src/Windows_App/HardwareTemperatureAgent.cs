namespace WaterCoolingDevice;

internal static class HardwareTemperatureAgent
{
    public static int Run()
    {
        using var mutex = new Mutex(
            true, HardwareTemperatureAgentOptions.MutexName, out var ownsMutex);
        if (!ownsMutex) return 0;
        if (!AppSettings.Load().SendHardwareTemperatures) return 0;

        try
        {
            using var reader = new LocalHardwareTemperatureReader();
            while (AppSettings.Load().SendHardwareTemperatures)
            {
                HardwareTemperatureStorage.Write(reader.Read());
                Thread.Sleep(1000);
            }
            return 0;
        }
        catch (Exception ex)
        {
            HardwareTemperatureStorage.Write(new(null, null, ex.Message));
            return 1;
        }
    }
}
