using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static partial class StorageFat32ParserTests
{
    private sealed class TruncatedRootDirectoryBlockDevice : IReadOnlyBlockDevice
    {
        private readonly IReadOnlyBlockDevice _inner;

        public TruncatedRootDirectoryBlockDevice(IReadOnlyBlockDevice inner)
        {
            _inner = inner;
        }

        public BlockDeviceInfo Info => _inner.Info;

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            if (firstBlock == RootDirectoryLba && blockCount == 1)
            {
                return BlockReadResult.Success(1, 31);
            }

            return _inner.ReadBlocks(firstBlock, blockCount, destination);
        }
    }
}
