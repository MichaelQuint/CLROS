using System.Runtime.CompilerServices;
using OS.Storage;

internal static class StorageBlockDeviceTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage block device reads exact byte range", TestExactRead, ref failures);
        Run("storage block device reads final block boundary", TestLastBlockRead, ref failures);
        Run("storage block device rejects out-of-range reads", TestOutOfRangeRead, ref failures);
        Run("storage block device rejects destination too small", TestDestinationTooSmall, ref failures);
        Run("storage block device rejects invalid block sizes", TestInvalidBlockSizes, ref failures);
        Run("storage block device detects oversized read overflow", TestReadOverflow, ref failures);
        Run("storage block device accepts zero-block read", TestZeroBlockRead, ref failures);
        Run(
            "storage block device result defaults are not success",
            TestDefaultResultsAreNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Storage block device host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestExactRead()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 4);
        byte[] destination = new byte[1024];

        BlockReadResult result = device.ReadBlocks(1, 2, destination);

        return result.Succeeded
            && result.Status == BlockDeviceStatus.Success
            && result.BlocksRead == 2
            && result.BytesRead == 1024
            && MatchesRange(destination, device.Bytes, 512);
    }

    private static bool TestLastBlockRead()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 4);
        byte[] destination = new byte[512];

        BlockReadResult result = device.ReadBlocks(3, 1, destination);

        return result.Succeeded
            && result.BlocksRead == 1
            && result.BytesRead == 512
            && MatchesRange(destination, device.Bytes, 1536);
    }

    private static bool TestOutOfRangeRead()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 4);
        byte[] destination = new byte[1024];

        BlockReadResult atEnd = device.ReadBlocks(4, 1, destination);
        BlockReadResult crossesEnd = device.ReadBlocks(3, 2, destination);

        return atEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !atEnd.Succeeded
            && atEnd.BytesRead == 0
            && crossesEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !crossesEnd.Succeeded
            && crossesEnd.BlocksRead == 0;
    }

    private static bool TestDestinationTooSmall()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 2);
        byte[] destination = new byte[511];
        destination[0] = 0xA5;

        BlockReadResult result = device.ReadBlocks(0, 1, destination);

        return result.Status == BlockDeviceStatus.DestinationTooSmall
            && !result.Succeeded
            && result.BytesRead == 0
            && destination[0] == 0xA5;
    }

    private static bool TestInvalidBlockSizes()
    {
        BlockDeviceInfoResult zero = BlockDeviceInfo.Create(0, 1);
        BlockDeviceInfoResult unaligned = BlockDeviceInfo.Create(513, 1);
        BlockDeviceInfoResult zeroCount = BlockDeviceInfo.Create(512, 0);

        return zero.Status == BlockDeviceStatus.InvalidBlockSize
            && !zero.Succeeded
            && unaligned.Status == BlockDeviceStatus.UnalignedBlockSize
            && !unaligned.Succeeded
            && zeroCount.Status == BlockDeviceStatus.InvalidBlockCount
            && !zeroCount.Succeeded;
    }

    private static bool TestReadOverflow()
    {
        BlockDeviceInfoResult info = BlockDeviceInfo.Create(512, (ulong)uint.MaxValue + 10UL);
        if (!info.Succeeded)
        {
            return false;
        }

        BlockReadResult result = BlockDeviceRead.Validate(info.Info, 0, uint.MaxValue, Span<byte>.Empty);

        return result.Status == BlockDeviceStatus.Overflow
            && !result.Succeeded
            && result.BytesRead == 0;
    }

    private static bool TestZeroBlockRead()
    {
        MemoryBlockDevice device = MemoryBlockDevice.Create(512, 4);

        BlockReadResult atEnd = device.ReadBlocks(4, 0, Span<byte>.Empty);
        BlockReadResult pastEnd = device.ReadBlocks(5, 0, Span<byte>.Empty);

        return atEnd.Succeeded
            && atEnd.BlocksRead == 0
            && atEnd.BytesRead == 0
            && pastEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !pastEnd.Succeeded;
    }

    private static bool TestDefaultResultsAreNotSuccess()
    {
        BlockReadResult read = default;
        BlockDeviceInfoResult info = default;
        PartitionBlockDeviceCreateResult partition = default;

        return !read.IsInitialized
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
            && !RuntimeHelpers.IsReferenceOrContainsReferences<BlockDeviceInfoResult>();
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
