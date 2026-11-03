using OS.Kernel.Platform;

namespace OS.Boot;

public unsafe struct FirmwareFileSystemCapability
{
    private readonly FirmwareAdapterAuthority* _authority;
    internal FirmwareFileSystemCapability(FirmwareAdapterAuthority* authority) => _authority = authority;
    public FirmwareAdapterCallResult Read(ulong handle, ulong offset, byte* destination, ulong capacity, ulong* actualBytes)
    {
        var status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Admit(_authority);
        if (status != FirmwareAdapterStatus.Success)
            return new(status);
        var callback = _authority->Backends.FileSystem;
        if (callback == null)
            return new(FirmwareAdapterStatus.BackendUnavailable);
        if (handle == 0 || capacity > ulong.MaxValue - offset)
            return new(FirmwareAdapterStatus.InvalidRange);
        fixed (FirmwareFileSystemCapability* self = &this)
        {
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwareFileSystemCapability), (ulong)destination, capacity, 1);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            status = ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Buffer(_authority, (ulong)self, (ulong)sizeof(FirmwareFileSystemCapability), (ulong)actualBytes, 8, 8);
            if (status != FirmwareAdapterStatus.Success)
                return new(status);
            if (ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Overlap((ulong)destination, capacity, (ulong)actualBytes, 8))
                return new(FirmwareAdapterStatus.AliasedStorage);
            return ConsoleFilesystemTimerRNGConfigurationTablePhysicalMemory.Returned(callback(_authority->Backends.Context, handle, offset, destination, capacity, actualBytes));
        }
    }
}
