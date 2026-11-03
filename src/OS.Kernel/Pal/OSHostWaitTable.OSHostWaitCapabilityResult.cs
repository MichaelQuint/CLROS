namespace OS.Pal;

public readonly struct OSHostWaitCapabilityResult
{
    public readonly OSHostStatus Status;
    public readonly ulong CapabilityFlags;
    public readonly uint MaxWaitableCount;
    public readonly uint ActiveWaitableCount;
    public readonly bool ReadyForHostedRuntime;
    public readonly bool IsInitialized;
    public OSHostWaitCapabilityResult(OSHostStatus status, ulong capabilityFlags, uint maxWaitableCount, uint activeWaitableCount, bool readyForHostedRuntime, bool isInitialized = true)
    {
        Status = status;
        CapabilityFlags = capabilityFlags;
        MaxWaitableCount = maxWaitableCount;
        ActiveWaitableCount = activeWaitableCount;
        ReadyForHostedRuntime = readyForHostedRuntime;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && CapabilityFlags != 0;

    public bool HasCapability(ulong capability)
    {
        return capability != 0 && (CapabilityFlags & capability) == capability;
    }

    public static OSHostWaitCapabilityResult FromStatus(OSHostStatus status)
    {
        return new OSHostWaitCapabilityResult(status, capabilityFlags: 0, maxWaitableCount: 0, activeWaitableCount: 0, readyForHostedRuntime: false);
    }
}
