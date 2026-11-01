namespace OS.Kernel.Diagnostics;
public readonly struct GuardedIstStackAllocationResult
{
    private GuardedIstStackAllocationResult(bool isInitialized, GuardedIstStackAllocationStatus status, GuardedIstStackDescriptor doubleFault, GuardedIstStackDescriptor pageFault, GuardedIstStackDescriptor nonMaskableInterrupt, GuardedIstStackLease lease, ulong requiredBytes, ulong availableBytes, bool stateMutated)
    {
        IsInitialized = isInitialized;
        Status = status;
        DoubleFault = doubleFault;
        PageFault = pageFault;
        NonMaskableInterrupt = nonMaskableInterrupt;
        Lease = lease;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        StateMutated = stateMutated;
    }

    public bool IsInitialized { get; }
    public GuardedIstStackAllocationStatus Status { get; }
    public GuardedIstStackDescriptor DoubleFault { get; }
    public GuardedIstStackDescriptor PageFault { get; }
    public GuardedIstStackDescriptor NonMaskableInterrupt { get; }
    public GuardedIstStackLease Lease { get; }
    public ulong RequiredBytes { get; }
    public ulong AvailableBytes { get; }
    public bool StateMutated { get; }
    public bool Succeeded => IsInitialized && Status == GuardedIstStackAllocationStatus.Success && DoubleFault.IsUsable && PageFault.IsUsable && NonMaskableInterrupt.IsUsable && Lease.IsUsable && RequiredBytes != 0 && AvailableBytes >= RequiredBytes && StateMutated;

    internal static GuardedIstStackAllocationResult Pass(GuardedIstStackDescriptor doubleFault, GuardedIstStackDescriptor pageFault, GuardedIstStackDescriptor nonMaskableInterrupt, GuardedIstStackLease lease, ulong requiredBytes, ulong availableBytes)
    {
        return new GuardedIstStackAllocationResult(true, GuardedIstStackAllocationStatus.Success, doubleFault, pageFault, nonMaskableInterrupt, lease, requiredBytes, availableBytes, true);
    }

    internal static GuardedIstStackAllocationResult Fail(GuardedIstStackAllocationStatus status, ulong requiredBytes, ulong availableBytes)
    {
        return new GuardedIstStackAllocationResult(true, status, default(GuardedIstStackDescriptor), default(GuardedIstStackDescriptor), default(GuardedIstStackDescriptor), default(GuardedIstStackLease), requiredBytes, availableBytes, false);
    }
}
