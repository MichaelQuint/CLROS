namespace OS.Drivers;
public readonly struct VirtioNegotiationResult
{
    public readonly VirtioNegotiationStatus Status;
    public readonly ulong Offered, Selected;
    public readonly byte DeviceStatus;
    public readonly bool FailureStatusAttempted, FailureStatusWritten;
    internal VirtioNegotiationResult(VirtioNegotiationStatus status, ulong offered, ulong selected, byte deviceStatus, bool attempted, bool written)
    {
        Status = status;
        Offered = offered;
        Selected = selected;
        DeviceStatus = deviceStatus;
        FailureStatusAttempted = attempted;
        FailureStatusWritten = written;
    }

    public bool Succeeded => Status == VirtioNegotiationStatus.Success && DeviceStatus == 11;
}
