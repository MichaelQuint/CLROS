using OS.Std.Binary;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Storage;

public static partial class Fat32Parser
{
    private readonly struct DirectoryClusterRead
    {
        private DirectoryClusterRead(Fat32Status status, BlockDeviceStatus blockStatus, byte[] bytes)
        {
            Status = status;
            BlockStatus = blockStatus;
            Bytes = bytes;
        }

        public Fat32Status Status
        {
            get;
        }

        public BlockDeviceStatus BlockStatus
        {
            get;
        }

        public byte[] Bytes
        {
            get;
        }

        public bool Succeeded => Status == Fat32Status.Success;

        public static DirectoryClusterRead Success(byte[] bytes)
        {
            return new DirectoryClusterRead(Fat32Status.Success, BlockDeviceStatus.Success, bytes);
        }

        public static DirectoryClusterRead Fail(Fat32Status status)
        {
            return new DirectoryClusterRead(status, BlockDeviceStatus.Success, Array.Empty<byte>());
        }

        public static DirectoryClusterRead Fail(Fat32Status status, BlockDeviceStatus blockStatus)
        {
            return new DirectoryClusterRead(status, blockStatus, Array.Empty<byte>());
        }
    }
}
