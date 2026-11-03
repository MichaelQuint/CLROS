using OS.Kernel.Platform;

namespace OS.Boot;

public unsafe struct FirmwareMemoryMapCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareMemoryMapCapability(FirmwareAdapterAuthority* authority) => _authority = authority;
    public FirmwareAdapterCallResult Capture(byte* destination, ulong capacity, FirmwareAdapterMemoryMapInfo* info)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success)
            return new(status);
        var callback = _authority->Backends.MemoryMap;
        if (callback == null)
            return new(FirmwareAdapterStatus.BackendUnavailable);
        fixed (FirmwareMemoryMapCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwareMemoryMapCapability), (ulong)destination, capacity, 1);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwareMemoryMapCapability), (ulong)info, (ulong)sizeof(FirmwareAdapterMemoryMapInfo), 8);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            if (ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Overlap((ulong)destination, capacity, (ulong)info, (ulong)sizeof(FirmwareAdapterMemoryMapInfo)))
                return new(FirmwareAdapterStatus.AliasedStorage);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, destination, capacity, info));
        }
    }
}
