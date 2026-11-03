using System.Buffers.Binary;
using OS.Storage;

internal static unsafe partial class PrimaryGPTIndependentlyBoundsCRCTests
{
    private struct RecordingDevice : IReadOnlyBlockDevice
    {
        public ulong Lba;
        public uint Count;
        public int Bytes;
        public readonly BlockDeviceInfo Info => BlockDeviceInfo.Create(512, GptTestData.Capacity).Info;

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            Lba = firstBlock;
            Count = blockCount;
            Bytes = destination.Length;
            destination[0] = 0x5a;
            return Complete;
        }
    }
}
