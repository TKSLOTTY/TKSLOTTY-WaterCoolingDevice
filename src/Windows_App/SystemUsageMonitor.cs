using System.Runtime.InteropServices;

namespace WaterCoolingDevice;

internal sealed record SystemUsageSnapshot(float? CpuPercent, float MemoryPercent);

internal sealed class SystemUsageMonitor
{
    private readonly object sync = new();
    private ulong previousIdle;
    private ulong previousKernel;
    private ulong previousUser;
    private bool hasCpuBaseline;

    public SystemUsageMonitor()
    {
        hasCpuBaseline = CaptureCpuTimes(
            out previousIdle, out previousKernel, out previousUser);
    }

    public SystemUsageSnapshot Read()
    {
        lock (sync)
        {
            float? cpu = null;
            if (CaptureCpuTimes(out var idle, out var kernel, out var user))
            {
                if (hasCpuBaseline)
                {
                    var idleDelta = idle - previousIdle;
                    var totalDelta = (kernel - previousKernel) + (user - previousUser);
                    if (totalDelta > 0)
                        cpu = Math.Clamp((float)(100.0 * (totalDelta - idleDelta) / totalDelta), 0, 100);
                }
                previousIdle = idle;
                previousKernel = kernel;
                previousUser = user;
                hasCpuBaseline = true;
            }

            var memory = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(ref memory))
                throw new InvalidOperationException("メモリ使用率を取得できませんでした。");
            return new SystemUsageSnapshot(cpu, memory.MemoryLoad);
        }
    }

    private static bool CaptureCpuTimes(out ulong idle, out ulong kernel, out ulong user)
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            idle = kernel = user = 0;
            return false;
        }
        idle = ToUInt64(idleTime);
        kernel = ToUInt64(kernelTime);
        user = ToUInt64(userTime);
        return true;
    }

    private static ulong ToUInt64(FileTime value) =>
        ((ulong)value.High << 32) | value.Low;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
