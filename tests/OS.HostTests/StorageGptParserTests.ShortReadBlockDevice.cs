using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static partial class StorageGptParserTests
{
    private sealed class ShortReadBlockDevice : IReadOnlyBlockDevice
    {
        private readonly IReadOnlyBlockDevice _inner;
        private readonly ulong _shortReadLba;

        public ShortReadBlockDevice(IReadOnlyBlockDevice inner, ulong shortReadLba)
        {
            _inner = inner;
            _shortReadLba = shortReadLba;
        }

        public BlockDeviceInfo Info => _inner.Info;

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            if (firstBlock == _shortReadLba && blockCount == 1)
            {
                return BlockReadResult.Success(0, 0);
            }

            return _inner.ReadBlocks(firstBlock, blockCount, destination);
        }
    }
}
