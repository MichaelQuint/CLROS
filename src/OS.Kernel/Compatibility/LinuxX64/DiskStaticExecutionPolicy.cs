namespace OS.Kernel.Compatibility.LinuxX64;

/// <summary>Reviewed finite DiskStaticFullV1 ceilings. Only the source-sealed
/// M10-018 full selection may admit them; these constants supply no device,
/// scheduler, syscall, HTTP or cleanup authority. Ordinary profiles retain their
/// existing limits. The per-gate call ceiling times the maximum64KiB accounted
/// completion fits uint; it is never renewed as a continuing-service turn.</summary>
public static class DiskStaticExecutionPolicy
{
    public const uint Version = 1;
    public const uint MaximumCalls = 32768;
    public const uint MaximumDeviceRecords = 196608;
    public const int MaximumSerialBytes = 128 * 1024 * 1024;
    public const ulong RequestDelayNanoseconds = 1_800_000_000_000;
    public const ulong GraceNanoseconds = 120_000_000_000;
    public const ulong RuntimeNanoseconds = 2_100_000_000_000;
    // UTC is anchored before workload entry. Leave finite startup/teardown
    // headroom beyond the later-started execution budget; never renew it.
    public const ulong RealtimeMaximumElapsedSeconds = 3600;
}
