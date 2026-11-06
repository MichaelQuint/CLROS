using OS.Kernel.Platform;

namespace OS.Boot;

public unsafe struct FirmwareConfigurationTableCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareConfigurationTableCapability(FirmwareAdapterAuthority* authority) => _authority = authority;
    public FirmwareAdapterCallResult Find(ulong guidLow, ulong guidHigh, ulong* tableAddress)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success)
            return new(status);
        var callback = _authority->Backends.ConfigurationTable;
        if (callback == null)
            return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareConfigurationTableCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwareConfigurationTableCapability), (ulong)tableAddress, 8, 8);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, guidLow, guidHigh, tableAddress));
        }
    }
}
