namespace OS.Kernel.Diagnostics;
public readonly struct GuardedIstStackReleaseResult
{
    private GuardedIstStackReleaseResult(bool isInitialized, GuardedIstStackReleaseStatus status, ulong releasedGeneration, bool stateMutated)
    {
        IsInitialized = isInitialized;
        Status = status;
        ReleasedGeneration = releasedGeneration;
        StateMutated = stateMutated;
    }

    public bool IsInitialized { get; }
    public GuardedIstStackReleaseStatus Status { get; }
    public ulong ReleasedGeneration { get; }
    public bool StateMutated { get; }
    public bool Succeeded => IsInitialized && Status == GuardedIstStackReleaseStatus.Success && ReleasedGeneration != 0 && StateMutated;

    internal static GuardedIstStackReleaseResult Pass(ulong releasedGeneration)
    {
        return new GuardedIstStackReleaseResult(true, GuardedIstStackReleaseStatus.Success, releasedGeneration, true);
    }

    internal static GuardedIstStackReleaseResult Fail(GuardedIstStackReleaseStatus status)
    {
        return new GuardedIstStackReleaseResult(true, status, 0, false);
    }
}
