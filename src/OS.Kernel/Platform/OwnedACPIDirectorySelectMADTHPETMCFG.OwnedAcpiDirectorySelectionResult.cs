namespace OS.Kernel.Platform;

public readonly struct OwnedAcpiDirectorySelectionResult
{
    public readonly OwnedAcpiDirectorySelectionStatus Status;
    public readonly OwnedAcpiSelectedTable Mcfg;
    public readonly OwnedAcpiSelectedTable Madt;
    public readonly OwnedAcpiSelectedTable Hpet;
    public bool Succeeded => Status == OwnedAcpiDirectorySelectionStatus.Success;

    internal OwnedAcpiDirectorySelectionResult(OwnedAcpiDirectorySelectionStatus status)
    {
        Status = status;
        Mcfg = default;
        Madt = default;
        Hpet = default;
    }

    internal OwnedAcpiDirectorySelectionResult(OwnedAcpiSelectedTable mcfg, OwnedAcpiSelectedTable madt, OwnedAcpiSelectedTable hpet)
    {
        Status = OwnedAcpiDirectorySelectionStatus.Success;
        Mcfg = mcfg;
        Madt = madt;
        Hpet = hpet;
    }
}
