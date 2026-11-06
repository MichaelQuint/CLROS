using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Storage;

internal static partial class StorageFat32ParserTests
{
    private sealed class TruncatedDirectoryClusterBlockDevice : IReadOnlyBlockDevice
    {
        private readonly IReadOnlyBlockDevice _inner;
        private readonly uint _cluster;

        public TruncatedDirectoryClusterBlockDevice(IReadOnlyBlockDevice inner, uint cluster)
        {
            _inner = inner;
            _cluster = cluster;
        }

        public BlockDeviceInfo Info => _inner.Info;

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            if (firstBlock == ClusterLba(_cluster) && blockCount == 1)
            {
                return BlockReadResult.Success(1, 31);
            }

            return _inner.ReadBlocks(firstBlock, blockCount, destination);
        }
    }
}
