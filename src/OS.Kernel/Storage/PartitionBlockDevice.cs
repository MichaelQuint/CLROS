namespace OS.Storage;

public sealed class PartitionBlockDevice : IReadOnlyBlockDevice
{
    private readonly IReadOnlyBlockDevice _parent;
    private readonly ulong _startBlock;

    private PartitionBlockDevice(
        IReadOnlyBlockDevice parent,
        BlockDeviceInfo info,
        ulong startBlock)
    {
        _parent = parent;
        Info = info;
        _startBlock = startBlock;
    }

    public BlockDeviceInfo Info { get; }

    public ulong ParentStartBlock => _startBlock;

    public static PartitionBlockDeviceCreateResult Create(
        IReadOnlyBlockDevice? parent,
        ulong startBlock,
        ulong blockCount)
    {
        if (parent is null)
        {
            return PartitionBlockDeviceCreateResult.Fail(BlockDeviceStatus.InvalidBlockRange);
        }

        BlockReadResult parentInfoValidation = BlockDeviceRead.Validate(
            parent.Info,
            0,
            0,
            Span<byte>.Empty);
        if (!parentInfoValidation.Succeeded)
        {
            return PartitionBlockDeviceCreateResult.Fail(parentInfoValidation.Status);
        }

        if (blockCount == 0)
        {
            return PartitionBlockDeviceCreateResult.Fail(BlockDeviceStatus.InvalidBlockCount);
        }

        if (startBlock >= parent.Info.BlockCount || blockCount > parent.Info.BlockCount - startBlock)
        {
            return PartitionBlockDeviceCreateResult.Fail(BlockDeviceStatus.InvalidBlockRange);
        }

        BlockDeviceInfoResult info = BlockDeviceInfo.Create(parent.Info.BlockSize, blockCount);
        if (!info.Succeeded)
        {
            return PartitionBlockDeviceCreateResult.Fail(info.Status);
        }

        return PartitionBlockDeviceCreateResult.Success(new PartitionBlockDevice(
            parent,
            info.Info,
            startBlock));
    }

    public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
    {
        BlockReadResult result = BlockDeviceRead.Validate(Info, firstBlock, blockCount, destination);
        if (!result.Succeeded)
        {
            return result;
        }

        ulong parentFirstBlock = _startBlock + firstBlock;
        if (parentFirstBlock < _startBlock)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.Overflow);
        }

        return _parent.ReadBlocks(parentFirstBlock, blockCount, destination);
    }
}
