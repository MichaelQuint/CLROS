using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using M = OS.Storage.Fat32Mutation;
using Block = OS.Storage.WritableBlockSession;

internal static unsafe partial class Fat32MutationTests
{
    private sealed class Device(byte* bytes) : IReadOnlyBlockDevice
    {
        public BlockDeviceInfo Info => BlockDeviceInfo.Create(512, Sectors).Info;
        public BlockReadResult ReadBlocks(ulong first, uint count, Span<byte> destination)
        {
            if (first >= Sectors || count > Sectors - first || destination.Length != count * 512) return BlockReadResult.Fail(BlockDeviceStatus.InvalidBlockRange);
            new ReadOnlySpan<byte>(bytes + first * 512, checked((int)count * 512)).CopyTo(destination); return BlockReadResult.Success(count, count * 512UL);
        }
    }
}
