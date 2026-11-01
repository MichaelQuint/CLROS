namespace OS.Drivers;
public readonly struct PciCapabilityWalkResult
{
    public readonly PciCapabilityWalkStatus Status;
    public readonly uint Count;
    public readonly uint FailingOffset;
    public bool IsInitialized => Status != PciCapabilityWalkStatus.Uninitialized;
    public bool Succeeded => Status == PciCapabilityWalkStatus.CompleteEmpty || Status == PciCapabilityWalkStatus.CompletePresent;

    internal PciCapabilityWalkResult(PciCapabilityWalkStatus status, uint count, uint failingOffset)
    {
        Status = status;
        Count = count;
        FailingOffset = failingOffset;
    }
}
