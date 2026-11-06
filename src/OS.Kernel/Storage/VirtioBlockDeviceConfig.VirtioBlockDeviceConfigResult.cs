using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public readonly struct VirtioBlockDeviceConfigResult
{
    private VirtioBlockDeviceConfigResult(
        VirtioBlockDeviceConfigStatus status,
        BlockDeviceStatus blockDeviceStatus,
        ulong rawCapacitySectors,
        uint sectorSizeBytes,
        BlockDeviceInfo blockDeviceInfo)
    {
        IsInitialized = true;
        Status = status;
        BlockDeviceStatus = blockDeviceStatus;
        RawCapacitySectors = rawCapacitySectors;
        SectorSizeBytes = sectorSizeBytes;
        BlockDeviceInfo = blockDeviceInfo;
    }

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockDeviceConfigStatus Status
    {
        get;
    }

    public BlockDeviceStatus BlockDeviceStatus
    {
        get;
    }

    public ulong RawCapacitySectors
    {
        get;
    }

    public uint SectorSizeBytes
    {
        get;
    }

    public BlockDeviceInfo BlockDeviceInfo
    {
        get;
    }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockDeviceConfigStatus.Success;

    public static VirtioBlockDeviceConfigResult Success(
        ulong rawCapacitySectors,
        uint sectorSizeBytes,
        BlockDeviceInfo blockDeviceInfo)
    {
        return new VirtioBlockDeviceConfigResult(
            VirtioBlockDeviceConfigStatus.Success,
            BlockDeviceStatus.Success,
            rawCapacitySectors,
            sectorSizeBytes,
            blockDeviceInfo);
    }

    public static VirtioBlockDeviceConfigResult Fail(
        VirtioBlockDeviceConfigStatus status,
        BlockDeviceStatus blockDeviceStatus,
        ulong rawCapacitySectors,
        uint sectorSizeBytes)
    {
        return new VirtioBlockDeviceConfigResult(
            status,
            blockDeviceStatus,
            rawCapacitySectors,
            sectorSizeBytes,
            default);
    }
}
