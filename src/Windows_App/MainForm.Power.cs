using Microsoft.Win32;

namespace WaterCoolingDevice;

internal sealed partial class MainForm
{
    private bool powerSuspended;
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.StatusChange || !IsHandleCreated || IsDisposed) return;
        try { BeginInvoke(() => HandlePowerChange(e.Mode)); }
        catch (InvalidOperationException) { }
    }

    private void HandlePowerChange(PowerModes mode)
    {
        if (applicationClosing) return;
        powerSuspended = mode == PowerModes.Suspend;
        client.Disconnect();
        coolantArgbOutput.Dispose(false);
        coolantArgbOutput = new CoolantArgbOutput();
        lunePanelTimer.Stop();
        lunePanel?.Dispose();
        lunePanel = null;
        if (powerSuspended) {
            refreshTimer.Stop();
            hostTelemetryTimer.Stop();
        } else {
            nextReconnectAttemptUtc = DateTime.UtcNow.AddSeconds(3);
            nextDeviceScanUtc = DateTime.MinValue;
            nextHostTemperatureSendUtc = DateTime.MinValue;
            nextHardwareAgentRecoveryUtc = DateTime.MinValue;
            refreshTimer.Start();
            hostTelemetryTimer.Start();
            if (!studioTestMode && lunePanelOptions.StudioEnabled) {
                StartLunePanel();
                lunePanelTimer.Start();
            }
            SetConnectionStatus("● 復帰後の再接続待機中", "● Reconnecting after resume", Color.Silver);
        }
    }

    internal void VerifyResumeRecovery()
    {
        if (!studioTestMode) throw new InvalidOperationException("Test mode required");
        Show(); Application.DoEvents();
        // Simulate an in-flight query holding the I/O semaphore. Disconnect must
        // return without needing that query's UI continuation to run.
        using var testClient = new HidDeviceClient();
        var semaphore = (SemaphoreSlim)typeof(HidDeviceClient).GetField("ioLock",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(testClient)!;
        semaphore.Wait();
        try {
            var disconnect = Task.Run(testClient.Disconnect);
            if (!disconnect.Wait(1000)) throw new Exception("Disconnect waited for in-flight I/O");
        } finally { semaphore.Release(); }
        using var output = new CoolantArgbOutput();
        var gate = typeof(CoolantArgbOutput).GetField("gate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(output)!;
        using var entered = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        var blocked = Task.Run(() => { lock (gate) { entered.Set(); finish.Wait(); } });
        entered.Wait();
        try {
            var release = Task.Run(() => { _ = output.Generation; output.Release(); output.Dispose(); });
            if (!release.Wait(1000)) throw new Exception("ARGB cleanup blocked on USB gate");
        } finally { finish.Set(); blocked.Wait(); }
        for (var i = 0; i < 3; i++) {
            HandlePowerChange(PowerModes.Suspend);
            if (!powerSuspended || client.IsConnected) throw new Exception("Suspend did not reset connection");
            HandlePowerChange(PowerModes.Resume);
            if (powerSuspended || !refreshTimer.Enabled) throw new Exception("Resume did not restart polling");
        }
        Close();
    }
}

