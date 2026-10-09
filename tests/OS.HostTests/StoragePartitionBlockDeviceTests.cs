using System.Runtime.CompilerServices;
using OS.Storage;

internal static class StoragePartitionBlockDeviceTests
{

    [HostTest("storage partition device reads relative blocks", HostLane.Fast, 0)]
    private static bool TestReadsRelativeBlocks()
    {
        MemoryBlockDevice parent = MemoryBlockDevice.Create(512, 12);
        PartitionBlockDeviceCreateResult create = PartitionBlockDevice.Create(
            parent,
            startBlock: 4,
            blockCount: 3);
        if (!create.Succeeded || create.Device is null)
        {
            return HostCheck.Result(false);
        }

        byte[] destination = new byte[1024];
        BlockReadResult read = create.Device.ReadBlocks(1, 2, destination);

        return HostCheck.Result(create.Device.Info.BlockSize == parent.Info.BlockSize
            && create.Device.Info.BlockCount == 3
            && create.Device.Info.CapacityBytes == 1536
            && create.Device.ParentStartBlock == 4
            && read.Succeeded
            && read.BlocksRead == 2
            && read.BytesRead == 1024
            && MatchesRange(destination, parent.Bytes, 5 * 512));
    }

    [HostTest("storage partition device rejects out of range partition", HostLane.Fast, 1)]
    private static bool TestRejectsOutOfRangePartition()
    {
        MemoryBlockDevice parent = MemoryBlockDevice.Create(512, 4);

        PartitionBlockDeviceCreateResult zeroCount = PartitionBlockDevice.Create(
            parent,
            startBlock: 1,
            blockCount: 0);
        PartitionBlockDeviceCreateResult atEnd = PartitionBlockDevice.Create(
            parent,
            startBlock: 4,
            blockCount: 1);
        PartitionBlockDeviceCreateResult crossesEnd = PartitionBlockDevice.Create(
            parent,
            startBlock: 3,
            blockCount: 2);

        return HostCheck.Result(!zeroCount.Succeeded
            && zeroCount.Status == BlockDeviceStatus.InvalidBlockCount
            && !atEnd.Succeeded
            && atEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !crossesEnd.Succeeded
            && crossesEnd.Status == BlockDeviceStatus.InvalidBlockRange);
    }

    [HostTest("storage partition device rejects read past partition end", HostLane.Fast, 2)]
    private static bool TestRejectsReadPastPartitionEnd()
    {
        MemoryBlockDevice parent = MemoryBlockDevice.Create(512, 8);
        PartitionBlockDeviceCreateResult create = PartitionBlockDevice.Create(
            parent,
            startBlock: 2,
            blockCount: 2);
        if (!create.Succeeded || create.Device is null)
        {
            return HostCheck.Result(false);
        }

        byte[] destination = new byte[1024];
        BlockReadResult atEnd = create.Device.ReadBlocks(2, 1, destination);
        BlockReadResult crossesEnd = create.Device.ReadBlocks(1, 2, destination);

        return HostCheck.Result(!atEnd.Succeeded
            && atEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !crossesEnd.Succeeded
            && crossesEnd.Status == BlockDeviceStatus.InvalidBlockRange);
    }

    [HostTest("storage partition device default result is not success", HostLane.Fast, 3)]
    private static bool TestDefaultCreateResultIsNotSuccess()
    {
        PartitionBlockDeviceCreateResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == BlockDeviceStatus.Success
            && result.Device is null);
    }

    private static bool MatchesRange(byte[] actual, byte[] expected, int expectedOffset)
    {
        for (int i = 0; i < actual.Length; i++)
        {
            if (actual[i] != expected[expectedOffset + i])
            {
                return false;
            }
        }

        return true;
    }

    private sealed class MemoryBlockDevice : IReadOnlyBlockDevice
    {
        private MemoryBlockDevice(BlockDeviceInfo info, byte[] bytes)
        {
            Info = info;
            Bytes = bytes;
        }

        public BlockDeviceInfo Info { get; }

        public byte[] Bytes { get; }

        public static MemoryBlockDevice Create(uint blockSize, ulong blockCount)
        {
            BlockDeviceInfoResult info = BlockDeviceInfo.Create(blockSize, blockCount);
            if (!info.Succeeded)
            {
                throw new InvalidOperationException("Invalid test block device: " + info.Status);
            }

            byte[] bytes = new byte[checked((int)info.Info.CapacityBytes)];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i % 251);
            }

            return new MemoryBlockDevice(info.Info, bytes);
        }

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            BlockReadResult result = BlockDeviceRead.Validate(Info, firstBlock, blockCount, destination);
            if (!result.Succeeded)
            {
                return result;
            }

            int sourceOffset = checked((int)(firstBlock * Info.BlockSize));
            Bytes.AsSpan(sourceOffset, checked((int)result.BytesRead)).CopyTo(destination);
            return result;
        }
    }
}
