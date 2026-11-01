using OS.Std.Memory;

namespace OS.Pal;
public sealed partial class OSHostAppLogTable
{
    private sealed class AppLogSlot
    {
        private readonly byte[] payload;
        public AppLogSlot(int maxRecordBytes)
        {
            payload = new byte[maxRecordBytes];
        }

        public uint StreamId { get; private set; }
        public uint Flags { get; private set; }
        public uint Sequence { get; private set; }
        public uint ByteLength { get; private set; }

        public bool Write(uint streamId, uint flags, uint sequence, ReadOnlySpan<byte> bytes)
        {
            BufferOperationResult copy = BufferOperations.MemoryCopy(bytes, payload);
            if (!copy.Succeeded)
            {
                return false;
            }

            StreamId = streamId;
            Flags = flags;
            Sequence = sequence;
            ByteLength = (uint)bytes.Length;
            return true;
        }

        public bool CopyTo(Span<byte> destination)
        {
            ReadOnlySpan<byte> source = payload.AsSpan(0, (int)ByteLength);
            BufferOperationResult copy = BufferOperations.MemoryCopy(source, destination);
            return copy.Succeeded;
        }
    }
}
