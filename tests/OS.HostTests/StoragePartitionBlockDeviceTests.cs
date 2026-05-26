using System.Runtime.CompilerServices;
using OS.Storage;

internal static class StoragePartitionBlockDeviceTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage partition device reads relative blocks", TestReadsRelativeBlocks, ref failures);
        Run("storage partition device rejects out of range partition", TestRejectsOutOfRangePartition, ref failures);
        Run("storage partition device rejects read past partition end", TestRejectsReadPastPartitionEnd, ref failures);
        Run(
            "storage partition device default result is not success",
            TestDefaultCreateResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Storage partition block device host tests failed: " + failures);
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

    private static bool TestReadsRelativeBlocks()
    {
        MemoryBlockDevice parent = MemoryBlockDevice.Create(512, 12);
        PartitionBlockDeviceCreateResult create = PartitionBlockDevice.Create(
            parent,
            startBlock: 4,
            blockCount: 3);
        if (!create.Succeeded || create.Device is null)
        {
            return false;
        }

        byte[] destination = new byte[1024];
        BlockReadResult read = create.Device.ReadBlocks(1, 2, destination);

        return create.Device.Info.BlockSize == parent.Info.BlockSize
            && create.Device.Info.BlockCount == 3
            && create.Device.Info.CapacityBytes == 1536
            && create.Device.ParentStartBlock == 4
            && read.Succeeded
            && read.BlocksRead == 2
            && read.BytesRead == 1024
            && MatchesRange(destination, parent.Bytes, 5 * 512);
    }

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

        return !zeroCount.Succeeded
            && zeroCount.Status == BlockDeviceStatus.InvalidBlockCount
            && !atEnd.Succeeded
            && atEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !crossesEnd.Succeeded
            && crossesEnd.Status == BlockDeviceStatus.InvalidBlockRange;
    }

    private static bool TestRejectsReadPastPartitionEnd()
    {
        MemoryBlockDevice parent = MemoryBlockDevice.Create(512, 8);
        PartitionBlockDeviceCreateResult create = PartitionBlockDevice.Create(
            parent,
            startBlock: 2,
            blockCount: 2);
        if (!create.Succeeded || create.Device is null)
        {
            return false;
        }

        byte[] destination = new byte[1024];
        BlockReadResult atEnd = create.Device.ReadBlocks(2, 1, destination);
        BlockReadResult crossesEnd = create.Device.ReadBlocks(1, 2, destination);

        return !atEnd.Succeeded
            && atEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !crossesEnd.Succeeded
            && crossesEnd.Status == BlockDeviceStatus.InvalidBlockRange;
    }

    private static bool TestDefaultCreateResultIsNotSuccess()
    {
        PartitionBlockDeviceCreateResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == BlockDeviceStatus.Success
            && result.Device is null;
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
