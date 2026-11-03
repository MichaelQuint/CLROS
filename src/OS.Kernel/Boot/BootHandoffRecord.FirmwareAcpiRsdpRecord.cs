namespace OS.Boot;

public struct FirmwareAcpiRsdpRecord
{
    public const uint FlagPresent = 1;
    public const uint ScanStatusOk = 0;
    public const uint TableKindAcpi10 = 1;
    public const uint TableKindAcpi20 = 2;
    public ulong RsdpAddress;
    public uint TableKind;
    public uint ScanStatus;
    public uint Flags;
    public bool HasRsdp()
    {
        return RsdpAddress != 0 && (Flags & FlagPresent) != 0;
    }
}
