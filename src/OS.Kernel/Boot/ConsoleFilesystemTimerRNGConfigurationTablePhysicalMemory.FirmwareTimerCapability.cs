using OS.Kernel.Platform;

namespace OS.Boot;

public unsafe struct FirmwareTimerCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareTimerCapability(FirmwareAdapterAuthority* authority) => _authority = authority;
    public FirmwareAdapterCallResult Read(ulong* value)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success)
            return new(status);
        var callback = _authority->Backends.Timer;
        if (callback == null)
            return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareTimerCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwareTimerCapability), (ulong)value, 8, 8);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, value));
        }
    }
}
