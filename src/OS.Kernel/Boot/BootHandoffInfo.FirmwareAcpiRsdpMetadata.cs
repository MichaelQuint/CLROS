using OS.Kernel.Platform;

namespace OS.Boot;
public readonly struct FirmwareAcpiRsdpMetadata
{
    public FirmwareAcpiRsdpMetadata(ulong rsdpAddress, EfiAcpiTableKind tableKind, EfiConfigurationTableScanStatus scanStatus, bool isPresent)
    {
        RsdpAddress = rsdpAddress;
        TableKind = tableKind;
        ScanStatus = scanStatus;
        IsPresent = isPresent;
    }

    public ulong RsdpAddress { get; }
    public EfiAcpiTableKind TableKind { get; }
    public EfiConfigurationTableScanStatus ScanStatus { get; }
    public bool IsPresent { get; }
    public bool HasRsdp => IsPresent && RsdpAddress != 0;
}
