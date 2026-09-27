namespace WaterCoolingDevice;

internal static class HardwareTemperatureAgent
{
    public static int Run()
    {
        using var mutex = new Mutex(
            true, HardwareTemperatureAgentOptions.MutexName, out var ownsMutex);
        if (!ownsMutex) return 0;
        var initialSettings = AppSettings.Load();
        if (!initialSettings.SendHardwareTemperatures && !initialSettings.GameOverlayEnabled)
            return 0;

        try
        {
            using var reader = new LocalHardwareTemperatureReader();
            using var fpsMonitor = new GameFpsMonitor();
            while (true)
            {
                var settings = AppSettings.Load();
                if (!settings.SendHardwareTemperatures && !settings.GameOverlayEnabled) break;
                HardwareTemperatureStorage.Write(reader.Read() with {
                    Fps = fpsMonitor.ReadForegroundFps()
                });
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
