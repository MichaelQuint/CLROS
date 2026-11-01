using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public static class VirtioBlockDeviceConfig
{
    public const int CapacityOffset = 0;
    public const int CapacityLength = sizeof(ulong);
    public const uint SectorSizeBytes = VirtioBlockRequestPlan.SectorSizeBytes;
    public const bool PerformsMmio = false;
    public const bool ReadsDeviceConfig = false;

    public static VirtioBlockDeviceConfigResult Parse(ReadOnlySpan<byte> config)
    {
        if (!EndianPrimitives.TryReadUInt64LittleEndian(
            config.Slice(CapacityOffset),
            out ulong rawCapacitySectors))
        {
            return VirtioBlockDeviceConfigResult.Fail(
                VirtioBlockDeviceConfigStatus.ConfigTooSmall,
                BlockDeviceStatus.Success,
                rawCapacitySectors: 0,
                SectorSizeBytes);
        }

        BlockDeviceInfoResult blockInfo = BlockDeviceInfo.Create(
            SectorSizeBytes,
            rawCapacitySectors);
        if (blockInfo.Succeeded)
        {
            return VirtioBlockDeviceConfigResult.Success(
                rawCapacitySectors,
                SectorSizeBytes,
                blockInfo.Info);
        }

        VirtioBlockDeviceConfigStatus status = blockInfo.Status switch
        {
            BlockDeviceStatus.InvalidBlockCount => VirtioBlockDeviceConfigStatus.InvalidCapacity,
            BlockDeviceStatus.Overflow => VirtioBlockDeviceConfigStatus.CapacityOverflow,
            _ => VirtioBlockDeviceConfigStatus.BlockDeviceInfoRejected
        };
        return VirtioBlockDeviceConfigResult.Fail(
            status,
            blockInfo.Status,
            rawCapacitySectors,
            SectorSizeBytes);
    }
}
