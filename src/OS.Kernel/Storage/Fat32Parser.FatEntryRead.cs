using OS.Std.Binary;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Storage;

public static partial class Fat32Parser
{
    private readonly struct FatEntryRead
    {
        private FatEntryRead(Fat32Status status, BlockDeviceStatus blockStatus, uint nextCluster)
        {
            Status = status;
            BlockStatus = blockStatus;
            NextCluster = nextCluster;
        }

        public Fat32Status Status
        {
            get;
        }

        public BlockDeviceStatus BlockStatus
        {
            get;
        }

        public uint NextCluster
        {
            get;
        }

        public bool Succeeded => Status == Fat32Status.Success;

        public static FatEntryRead Success(uint nextCluster)
        {
            return new FatEntryRead(Fat32Status.Success, BlockDeviceStatus.Success, nextCluster);
        }

        public static FatEntryRead Fail(Fat32Status status)
        {
            return new FatEntryRead(status, BlockDeviceStatus.Success, 0);
        }

        public static FatEntryRead Fail(Fat32Status status, BlockDeviceStatus blockStatus)
        {
            return new FatEntryRead(status, blockStatus, 0);
        }
    }
}
