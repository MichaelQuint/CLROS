using System.Runtime.CompilerServices;
using OS.Storage;

internal static partial class StorageBlockDeviceTests
{
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
