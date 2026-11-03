using System.Buffers.Binary;
using System.Text;
using OS.Std.Hash;
using OS.Storage;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class FatTranscriptDevice(FileStream disk, ulong capacity) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, capacity).Info;

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            ulong bytes = (ulong)blockCount * 512;
            if (blockCount == 0 || firstBlock >= capacity || blockCount > capacity - firstBlock || bytes > (ulong)destination.Length)
                return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            disk.Position = checked((long)firstBlock * 512);
            disk.ReadExactly(destination[..(int)bytes]);
            return BlockReadResult.Success(blockCount, bytes);
        }
    }
}
