using OS.Kernel.Platform;

namespace OS.Boot;

public unsafe struct FirmwareRandomCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareRandomCapability(FirmwareAdapterAuthority* authority) => _authority = authority;
    public FirmwareAdapterCallResult Fill(byte* destination, ulong bytes)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success)
            return new(status);
        var callback = _authority->Backends.Random;
        if (callback == null)
            return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareRandomCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwareRandomCapability), (ulong)destination, bytes, 1);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, destination, bytes));
        }
    }
}
