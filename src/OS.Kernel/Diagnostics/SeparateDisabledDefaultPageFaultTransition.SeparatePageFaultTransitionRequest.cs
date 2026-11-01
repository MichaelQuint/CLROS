namespace OS.Kernel.Diagnostics;
/// <summary>Supplied observations and image summary, not ownership or execution authority.</summary>

public readonly struct SeparatePageFaultTransitionRequest
{
    public SeparatePageFaultTransitionRequest(ulong code, ulong data, ulong tables, OS.Kernel.Memory.X64PagingModePreflightObservation observation, ulong pat, SeparatePageFaultPageTableResult image)
    {
        IsInitialized = true;
        Code = code;
        Data = data;
        Tables = tables;
        Observation = observation;
        Pat = pat;
        Image = image;
    }

    public bool IsInitialized { get; }
    public ulong Code { get; }
    public ulong Data { get; }
    public ulong Tables { get; }
    public OS.Kernel.Memory.X64PagingModePreflightObservation Observation { get; }
    public ulong Pat { get; }
    public SeparatePageFaultPageTableResult Image { get; }
}
