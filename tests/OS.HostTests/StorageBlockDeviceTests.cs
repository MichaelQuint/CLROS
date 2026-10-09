using System.Runtime.CompilerServices;
using OS.Storage;

internal static class StorageBlockDeviceTests
{

    [HostTest("storage block device reads exact byte range", HostLane.Fast, 0)]
    private static bool TestExactRead()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 4);
        byte[] destination = new byte[1024];

        BlockReadResult result = device.ReadBlocks(1, 2, destination);

        return HostCheck.Result(result.Succeeded
            && result.Status == BlockDeviceStatus.Success
            && result.BlocksRead == 2
            && result.BytesRead == 1024
            && MatchesRange(destination, device.Bytes, 512));
    }

    [HostTest("storage block device reads final block boundary", HostLane.Fast, 1)]
    private static bool TestLastBlockRead()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 4);
        byte[] destination = new byte[512];

        BlockReadResult result = device.ReadBlocks(3, 1, destination);

        return HostCheck.Result(result.Succeeded
            && result.BlocksRead == 1
            && result.BytesRead == 512
            && MatchesRange(destination, device.Bytes, 1536));
    }

    [HostTest("storage block device rejects out-of-range reads", HostLane.Fast, 2)]
    private static bool TestOutOfRangeRead()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 4);
        byte[] destination = new byte[1024];

        BlockReadResult atEnd = device.ReadBlocks(4, 1, destination);
        BlockReadResult crossesEnd = device.ReadBlocks(3, 2, destination);

        return HostCheck.Result(atEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !atEnd.Succeeded
            && atEnd.BytesRead == 0
            && crossesEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !crossesEnd.Succeeded
            && crossesEnd.BlocksRead == 0);
    }

    [HostTest("storage block device rejects destination too small", HostLane.Fast, 3)]
    private static bool TestDestinationTooSmall()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 2);
        byte[] destination = new byte[511];
        destination[0] = 0xA5;

        BlockReadResult result = device.ReadBlocks(0, 1, destination);

        return HostCheck.Result(result.Status == BlockDeviceStatus.DestinationTooSmall
            && !result.Succeeded
            && result.BytesRead == 0
            && destination[0] == 0xA5);
    }

    [HostTest("storage block device rejects invalid block sizes", HostLane.Fast, 4)]
    private static bool TestInvalidBlockSizes()
    {
        BlockDeviceInfoResult zero = BlockDeviceInfo.Create(0, 1);
        BlockDeviceInfoResult unaligned = BlockDeviceInfo.Create(513, 1);
        BlockDeviceInfoResult zeroCount = BlockDeviceInfo.Create(512, 0);

        return HostCheck.Result(zero.Status == BlockDeviceStatus.InvalidBlockSize
            && !zero.Succeeded
            && unaligned.Status == BlockDeviceStatus.UnalignedBlockSize
            && !unaligned.Succeeded
            && zeroCount.Status == BlockDeviceStatus.InvalidBlockCount
            && !zeroCount.Succeeded);
    }

    [HostTest("storage block device detects oversized read overflow", HostLane.Fast, 5)]
    private static bool TestReadOverflow()
    {
        BlockDeviceInfoResult info = BlockDeviceInfo.Create(512, (ulong)uint.MaxValue + 10UL);
        if (!info.Succeeded)
        {
            return HostCheck.Result(false);
        }

        BlockReadResult result = BlockDeviceRead.Validate(info.Info, 0, uint.MaxValue, Span<byte>.Empty);

        return HostCheck.Result(result.Status == BlockDeviceStatus.Overflow
            && !result.Succeeded
            && result.BytesRead == 0);
    }

    [HostTest("storage block device accepts zero-block read", HostLane.Fast, 6)]
    private static bool TestZeroBlockRead()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 4);

        BlockReadResult atEnd = device.ReadBlocks(4, 0, Span<byte>.Empty);
        BlockReadResult pastEnd = device.ReadBlocks(5, 0, Span<byte>.Empty);

        return HostCheck.Result(atEnd.Succeeded
            && atEnd.BlocksRead == 0
            && atEnd.BytesRead == 0
            && pastEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !pastEnd.Succeeded);
    }

    [HostTest("storage block device result defaults are not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        BlockReadResult read = default;
        BlockDeviceInfoResult info = default;
        PartitionBlockDeviceCreateResult partition = default;

        return HostCheck.Result(!read.IsInitialized
            && !read.Succeeded
            && read.Status == BlockDeviceStatus.Success
            && read.BlocksRead == 0
            && read.BytesRead == 0
            && !info.IsInitialized
            && !info.Succeeded
            && info.Status == BlockDeviceStatus.Success
            && info.Info.BlockSize == 0
            && info.Info.BlockCount == 0
            && info.Info.CapacityBytes == 0
            && !partition.IsInitialized
            && !partition.Succeeded
            && partition.Status == BlockDeviceStatus.Success
            && partition.Device is null
            && !RuntimeHelpers.IsReferenceOrContainsReferences<BlockReadResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<BlockDeviceInfoResult>());
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
