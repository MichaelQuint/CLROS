using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Hash;
using OS.Std.Time;
using OS.Storage;

namespace OS.Tooling;
public static partial class WritableDataImageFixture
{
    private sealed class Device(byte[] disk) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, (ulong)disk.Length / 512).Info;

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            ulong sectors = (ulong)disk.Length / 512;
            if (firstBlock >= sectors || blockCount == 0 || blockCount > sectors - firstBlock)
                return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            ulong bytes = (ulong)blockCount * 512;
            if ((ulong)destination.Length < bytes)
                return BlockReadResult.Fail(BlockDeviceStatus.DestinationTooSmall);
            disk.AsSpan(checked((int)firstBlock * 512), checked((int)bytes)).CopyTo(destination);
            return BlockReadResult.Success(blockCount, bytes);
        }
    }
}
