using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static partial class StorageGptParserTests
{
    private sealed class GeometryOnlyBlockDevice : IReadOnlyBlockDevice
    {
        public GeometryOnlyBlockDevice(uint blockSize, ulong blockCount)
        {
            BlockDeviceInfoResult info = BlockDeviceInfo.Create(blockSize, blockCount);
            if (!info.Succeeded)
            {
                throw new InvalidOperationException("Invalid geometry-only device: " + info.Status);
            }

            Info = info.Info;
        }

        public BlockDeviceInfo Info { get; }

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            return BlockReadResult.Fail(BlockDeviceStatus.IoError);
        }
    }
}
