using OS.Std.Binary;

namespace OS.Storage;

public static partial class GptParser
{
    private readonly struct PrimaryEfiSystemPartitionSelection
    {
        private PrimaryEfiSystemPartitionSelection(
            GptStatus status,
            BlockDeviceStatus blockStatus,
            GptHeader header,
            GptPartitionEntry partition,
            uint partitionEntryIndex,
            uint blockSizeBytes)
        {
            Status = status;
            BlockStatus = blockStatus;
            Header = header;
            Partition = partition;
            PartitionEntryIndex = partitionEntryIndex;
            BlockSizeBytes = blockSizeBytes;
        }

        public GptStatus Status
        {
            get;
        }

        public BlockDeviceStatus BlockStatus
        {
            get;
        }

        public GptHeader Header
        {
            get;
        }

        public GptPartitionEntry Partition
        {
            get;
        }

        public uint PartitionEntryIndex
        {
            get;
        }

        public uint BlockSizeBytes
        {
            get;
        }

        public bool Succeeded => Status == GptStatus.Success;

        public static PrimaryEfiSystemPartitionSelection Success(
            GptHeader header,
            GptPartitionEntry partition,
            uint partitionEntryIndex,
            uint blockSizeBytes)
        {
            return new PrimaryEfiSystemPartitionSelection(
                GptStatus.Success,
                BlockDeviceStatus.Success,
                header,
                partition,
                partitionEntryIndex,
                blockSizeBytes);
        }

        public static PrimaryEfiSystemPartitionSelection Fail(GptStatus status)
        {
            return new PrimaryEfiSystemPartitionSelection(
                status,
                BlockDeviceStatus.Success,
                default,
                default,
                0,
                0);
        }

        public static PrimaryEfiSystemPartitionSelection Fail(
            GptStatus status,
            BlockDeviceStatus blockStatus)
        {
            return new PrimaryEfiSystemPartitionSelection(
                status,
                blockStatus,
                default,
                default,
                0,
                0);
        }
    }
}
