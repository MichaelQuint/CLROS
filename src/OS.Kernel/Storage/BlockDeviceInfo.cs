namespace OS.Storage;

public readonly struct BlockDeviceInfo
{
    public const uint MinimumBlockSizeBytes = 512;

    private BlockDeviceInfo(uint blockSize, ulong blockCount, ulong capacityBytes)
    {
        BlockSize = blockSize;
        BlockCount = blockCount;
        CapacityBytes = capacityBytes;
    }

    public uint BlockSize { get; }

    public ulong BlockCount { get; }

    public ulong CapacityBytes { get; }

    public static BlockDeviceInfoResult Create(uint blockSize, ulong blockCount)
    {
        if (blockSize == 0)
        {
            return BlockDeviceInfoResult.Fail(BlockDeviceStatus.InvalidBlockSize);
        }

        if ((blockSize % MinimumBlockSizeBytes) != 0)
        {
            return BlockDeviceInfoResult.Fail(BlockDeviceStatus.UnalignedBlockSize);
        }

        if (blockCount == 0)
        {
            return BlockDeviceInfoResult.Fail(BlockDeviceStatus.InvalidBlockCount);
        }

        if (blockCount > (ulong.MaxValue / blockSize))
        {
            return BlockDeviceInfoResult.Fail(BlockDeviceStatus.Overflow);
        }

        return BlockDeviceInfoResult.Success(new BlockDeviceInfo(
            blockSize,
            blockCount,
            blockCount * blockSize));
    }
}
