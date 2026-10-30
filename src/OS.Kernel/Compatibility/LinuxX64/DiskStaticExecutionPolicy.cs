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
    // Full-only qualification lifetime: allow the HTTP matrix and slow QEMU
    // browser startup to finish before the existing one-shot shutdown path.
    public const ulong RequestDelayNanoseconds = 6_000_000_000_000;
    public const ulong GraceNanoseconds = 120_000_000_000;
    public const ulong RuntimeNanoseconds = 6_600_000_000_000;
    // UTC is anchored before workload entry. Leave finite startup/teardown
    // headroom beyond the later-started execution budget; never renew it.
    public const ulong RealtimeMaximumElapsedSeconds = 7200;
}
