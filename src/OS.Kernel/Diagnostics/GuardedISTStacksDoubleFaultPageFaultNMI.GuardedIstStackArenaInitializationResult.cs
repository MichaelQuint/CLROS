namespace OS.Kernel.Diagnostics;
public readonly struct GuardedIstStackArenaInitializationResult
{
    private GuardedIstStackArenaInitializationResult(bool isInitialized, GuardedIstStackArenaStatus status, ulong requiredBytes, ulong availableBytes)
    {
        IsInitialized = isInitialized;
        Status = status;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
    }

    public bool IsInitialized { get; }
    public GuardedIstStackArenaStatus Status { get; }
    public ulong RequiredBytes { get; }
    public ulong AvailableBytes { get; }
    public bool Succeeded => IsInitialized && Status == GuardedIstStackArenaStatus.Success && RequiredBytes != 0 && AvailableBytes >= RequiredBytes;

    internal static GuardedIstStackArenaInitializationResult Pass(ulong requiredBytes, ulong availableBytes)
    {
        return new GuardedIstStackArenaInitializationResult(true, GuardedIstStackArenaStatus.Success, requiredBytes, availableBytes);
    }

    internal static GuardedIstStackArenaInitializationResult Fail(GuardedIstStackArenaStatus status, ulong requiredBytes, ulong availableBytes)
    {
        return new GuardedIstStackArenaInitializationResult(true, status, requiredBytes, availableBytes);
    }
}
