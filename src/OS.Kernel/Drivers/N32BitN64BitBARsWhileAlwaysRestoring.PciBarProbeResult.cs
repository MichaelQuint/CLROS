namespace OS.Drivers;

public readonly struct PciBarProbeResult
{
    public readonly PciBarProbeStatus Status, OperationStatus;
    public readonly bool RestoreAttempted, Restored;
    internal PciBarProbeResult(PciBarProbeStatus operation, bool attempted, bool restored)
    {
        OperationStatus = operation;
        RestoreAttempted = attempted;
        Restored = restored;
        Status = attempted && !restored ? PciBarProbeStatus.RestoreFailed : operation;
    }

    public bool Succeeded => Status == PciBarProbeStatus.Success && RestoreAttempted && Restored;
}
