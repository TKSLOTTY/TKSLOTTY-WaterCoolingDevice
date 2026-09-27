using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace WaterCoolingDevice;

// Counts DXGI/D3D9 Present events in the already-elevated hardware agent.
// This avoids injecting code into games and degrades cleanly when ETW is unavailable.
internal sealed class GameFpsMonitor : IDisposable
{
    private static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid D3d9Provider = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");
    private const ulong PresentKeyword = 0x8000000000000002;
    private readonly ConcurrentDictionary<int, PresentHistory> histories = new();
    private readonly TraceEventSession? session;
    private readonly Thread? processingThread;
    private bool disposed;

    public GameFpsMonitor()
    {
        try
        {
            session = new TraceEventSession($"WaterCoolingDevice.Presents.{Environment.ProcessId}");
            session.StopOnDispose = true;
            session.Source.AllEvents += OnEvent;
            session.EnableProvider(DxgiProvider, TraceEventLevel.Always, PresentKeyword);
            session.EnableProvider(D3d9Provider, TraceEventLevel.Always, PresentKeyword);
            processingThread = new Thread(() => {
                try { session.Source.Process(); }
                catch when (disposed) { }
                catch { }
            }) { IsBackground = true, Name = "Game present monitor" };
            processingThread.Start();
        }
        catch
        {
            session?.Dispose();
            session = null;
        }
    }

    private void OnEvent(TraceEvent data)
    {
        var isDxgiPresent = data.ProviderGuid == DxgiProvider && data.ID == (TraceEventID)42;
        var isD3d9Present = data.ProviderGuid == D3d9Provider && data.ID == (TraceEventID)1;
        if (!isDxgiPresent && !isD3d9Present || data.ProcessID <= 0) return;
        // ETW delivers events in batches.  Use the event's own monotonic session
        // timestamp, not the callback arrival time, or a buffered batch appears
        // to contain millions of frames per second.
        histories.GetOrAdd(data.ProcessID, _ => new PresentHistory())
            .Add(data.TimeStampRelativeMSec);
    }

    public float? ReadForegroundFps()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return null;
        NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        return histories.TryGetValue((int)processId, out var history)
            ? history.Calculate() : null;
    }

    public void Dispose()
    {
        disposed = true;
        try { session?.Dispose(); } catch { }
        processingThread?.Join(1000);
    }

    private sealed class PresentHistory
    {
        private readonly object sync = new();
        private readonly Queue<double> timestamps = new();
        private long lastArrivalTimestamp;

        public void Add(double timestampMilliseconds)
        {
            lock (sync)
            {
                timestamps.Enqueue(timestampMilliseconds);
                lastArrivalTimestamp = Stopwatch.GetTimestamp();
                Trim(timestampMilliseconds, 2500);
            }
        }

        public float? Calculate()
        {
            lock (sync)
            {
                var now = Stopwatch.GetTimestamp();
                if (lastArrivalTimestamp == 0 ||
                    (now - lastArrivalTimestamp) / (double)Stopwatch.Frequency > 1.5)
                    return null;
                if (timestamps.Count < 2) return null;
                var last = timestamps.Last();
                Trim(last, 1500);
                if (timestamps.Count < 2) return null;
                var first = timestamps.Peek();
                var seconds = (last - first) / 1000.0;
                if (seconds < 0.05) return null;
                var fps = (float)((timestamps.Count - 1) / seconds);
                return fps is > 0 and < 2000 ? fps : null;
            }
        }

        private void Trim(double newestMilliseconds, double ageMilliseconds)
        {
            var threshold = newestMilliseconds - ageMilliseconds;
            while (timestamps.Count > 0 && timestamps.Peek() < threshold)
                timestamps.Dequeue();
        }
    }
}
