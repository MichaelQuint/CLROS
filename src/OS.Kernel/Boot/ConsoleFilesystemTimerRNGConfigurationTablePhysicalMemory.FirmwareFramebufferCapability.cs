using OS.Kernel.Platform;

namespace OS.Boot;

public unsafe struct FirmwareFramebufferCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareFramebufferCapability(FirmwareAdapterAuthority* authority) => _authority = authority;
    public FirmwareAdapterCallResult Snapshot(FirmwareAdapterFramebufferInfo* info)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success)
            return new(status);
        var callback = _authority->Backends.Framebuffer;
        if (callback == null)
            return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareFramebufferCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwareFramebufferCapability), (ulong)info, (ulong)sizeof(FirmwareAdapterFramebufferInfo), 8);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, info));
        }
    }
}
