namespace OS.Storage;

public static class BlockDeviceRead
{
    public static BlockReadResult Validate(
        BlockDeviceInfo info,
        ulong firstBlock,
        uint blockCount,
        Span<byte> destination)
    {
        if (info.BlockSize == 0)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockSize);
        }

        if ((info.BlockSize % BlockDeviceInfo.MinimumBlockSizeBytes) != 0)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.UnalignedBlockSize);
        }

        if (info.BlockCount == 0)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockCount);
        }

        if (blockCount == 0)
        {
            return firstBlock <= info.BlockCount
                ? BlockReadResult.Success(0, 0)
                : BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
        }

        if (firstBlock >= info.BlockCount)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
        }

        ulong remainingBlocks = info.BlockCount - firstBlock;
        if (blockCount > remainingBlocks)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
        }

        ulong byteCount = (ulong)blockCount * info.BlockSize;
        if (byteCount > int.MaxValue)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.Overflow);
        }

        if (byteCount > (ulong)destination.Length)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.DestinationTooSmall);
        }

        return BlockReadResult.Success(blockCount, byteCount);
    }
}
