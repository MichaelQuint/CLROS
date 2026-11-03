namespace OS.Pal;

public readonly struct OSHostThreadCapabilityResult
{
    public readonly OSHostStatus Status;
    public readonly ulong CapabilityFlags;
    public readonly uint MaxThreadCount;
    public readonly uint ActiveThreadCount;
    public readonly bool ReadyForHostedRuntime;
    public readonly bool IsInitialized;
    public OSHostThreadCapabilityResult(OSHostStatus status, ulong capabilityFlags, uint maxThreadCount, uint activeThreadCount, bool readyForHostedRuntime, bool isInitialized = true)
    {
        Status = status;
        CapabilityFlags = capabilityFlags;
        MaxThreadCount = maxThreadCount;
        ActiveThreadCount = activeThreadCount;
        ReadyForHostedRuntime = readyForHostedRuntime;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok && CapabilityFlags != 0;

    public bool HasCapability(ulong capability)
    {
        return capability != 0 && (CapabilityFlags & capability) == capability;
    }

    public static OSHostThreadCapabilityResult FromStatus(OSHostStatus status)
    {
        return new OSHostThreadCapabilityResult(status, capabilityFlags: 0, maxThreadCount: 0, activeThreadCount: 0, readyForHostedRuntime: false);
    }
}
