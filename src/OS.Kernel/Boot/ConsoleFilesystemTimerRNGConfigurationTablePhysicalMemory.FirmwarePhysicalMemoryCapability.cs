using OS.Kernel.Platform;

namespace OS.Boot;
public unsafe struct FirmwarePhysicalMemoryCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwarePhysicalMemoryCapability(FirmwareAdapterAuthority* authority) => _authority = authority;
    public FirmwareAdapterCallResult Read(ulong address, byte* destination, ulong bytes)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success)
            return new(status);
        var callback = _authority->Backends.PhysicalMemory;
        if (callback == null)
            return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwarePhysicalMemoryCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwarePhysicalMemoryCapability), (ulong)destination, bytes, 1);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwarePhysicalMemoryCapability), (ulong)address, bytes, 1);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            if (ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Overlap((ulong)address, bytes, (ulong)destination, bytes))
                return new(FirmwareAdapterStatus.AliasedStorage);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, address, destination, bytes));
        }
    }
}
