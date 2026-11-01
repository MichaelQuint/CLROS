namespace OS.Kernel.Diagnostics;
/// <summary>Supplied observations and image summary, not ownership or execution authority.</summary>

public readonly struct DoubleFaultIstRequest
{
    public DoubleFaultIstRequest(ulong code, ulong data, ulong tables, OS.Kernel.Memory.X64PagingModePreflightObservation observation, ulong pat, SeparatePageFaultPageTableResult image, bool enabledByDefault = false, bool executionRequested = false, bool executionAttempted = false, bool claimedGuestCapture = false)
    {
        IsInitialized = true;
        Code = code;
        Data = data;
        Tables = tables;
        Observation = observation;
        Pat = pat;
        Image = image;
        EnabledByDefault = enabledByDefault;
        ExecutionRequested = executionRequested;
        ExecutionAttempted = executionAttempted;
        ClaimedGuestCapture = claimedGuestCapture;
    }

    public bool EnabledByDefault { get; }
    public bool ExecutionRequested { get; }
    public bool ExecutionAttempted { get; }
    public bool ClaimedGuestCapture { get; }
    public bool IsInitialized { get; }
    public ulong Code { get; }
    public ulong Data { get; }
    public ulong Tables { get; }
    public OS.Kernel.Memory.X64PagingModePreflightObservation Observation { get; }
    public ulong Pat { get; }
    public SeparatePageFaultPageTableResult Image { get; }
}
