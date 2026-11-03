using OS.Kernel.Platform;

namespace OS.Boot;

public unsafe struct FirmwareConsoleCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareConsoleCapability(FirmwareAdapterAuthority* authority) => _authority = authority;
    public FirmwareAdapterCallResult Write(char* text, ulong characters)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success)
            return new(status);
        var callback = _authority->Backends.Console;
        if (callback == null)
            return new(FirmwareAdapterStatus.BackendUnavailable);
        if (characters == 0 || characters > 0x0000400000000000UL)
            return new(FirmwareAdapterStatus.InvalidRange);
        fixed (FirmwareConsoleCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwareConsoleCapability), (ulong)text, characters * 2, 2);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, text, characters));
        }
    }
}
