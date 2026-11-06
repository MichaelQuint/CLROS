namespace OS.Boot;
/// <summary>
/// A checked call descriptor for the later EFI adapter. This type never
/// converts the entry address to a function pointer and never invokes it.
/// </summary>

public readonly struct M01LoadedImageStackTransferTarget
{
    private readonly VirtualAddress _entry;
    private readonly VirtualAddress _newRspTop;
    private readonly VirtualAddress _handoffAddress;
    private readonly VirtualAddress _continuationAddress;
    internal M01LoadedImageStackTransferTarget(VirtualAddress entry, VirtualAddress newRspTop, VirtualAddress handoffAddress, VirtualAddress continuationAddress)
    {
        _entry = entry;
        _newRspTop = newRspTop;
        _handoffAddress = handoffAddress;
        _continuationAddress = continuationAddress;
    }

    public VirtualAddress Entry => _entry;
    public VirtualAddress NewRspTop => _newRspTop;
    public VirtualAddress HandoffAddress => _handoffAddress;
    public VirtualAddress ContinuationAddress => _continuationAddress;

    public bool IsValidFor(OwnedBootStackSwitchPlan plan)
    {
        return plan.IsValid && _entry.IsValid && _entry.Equals(plan.StubVirtualEntry) && _newRspTop.IsValid && _newRspTop.Equals(plan.PreTransferStackPointer) && _handoffAddress.IsValid && _handoffAddress.Equals(plan.Handoff.StorageStart) && _continuationAddress.IsValid && _continuationAddress.Equals(plan.Handoff.Continuation);
    }
}
