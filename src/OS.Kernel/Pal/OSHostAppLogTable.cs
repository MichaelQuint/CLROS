using OS.Std.Memory;

namespace OS.Pal;

public sealed partial class OSHostAppLogTable
{
    private readonly AppLogSlot[] slots;
    private readonly int maxRecordBytes;
    private uint nextSequence;
    private int count;
    public OSHostAppLogTable(int recordCapacity, int maxRecordBytes)
    {
        if (recordCapacity <= 0 || maxRecordBytes <= 0)
        {
            slots = [];
            this.maxRecordBytes = 0;
            return;
        }

        slots = new AppLogSlot[recordCapacity];
        this.maxRecordBytes = maxRecordBytes;
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new AppLogSlot(maxRecordBytes);
        }
    }

    public int Count => count;
    public int Capacity => slots.Length;
    public int MaxRecordBytes => maxRecordBytes;

    public OSHostAppLogWriteResult Write(uint streamId, ReadOnlySpan<byte> bytes, uint flags)
    {
        if (!IsValidStream(streamId))
        {
            return OSHostAppLogWriteResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        if (bytes.Length > maxRecordBytes)
        {
            return OSHostAppLogWriteResult.FromStatus(OSHostStatus.BufferTooSmall);
        }

        if (count >= slots.Length)
        {
            return OSHostAppLogWriteResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        uint sequence = nextSequence;
        if (!slots[count].Write(streamId, flags, sequence, bytes))
        {
            return OSHostAppLogWriteResult.FromStatus(OSHostStatus.BufferTooSmall);
        }

        nextSequence = sequence + 1;
        uint recordIndex = (uint)count;
        count++;
        return new OSHostAppLogWriteResult(OSHostStatus.Ok, recordIndex, (uint)bytes.Length);
    }

    public OSHostStatus TryGetEntry(uint recordIndex, out OSHostAppLogTableEntry entry)
    {
        if (!IsValidIndex(recordIndex))
        {
            entry = default;
            return OSHostStatus.InvalidArgument;
        }

        AppLogSlot slot = slots[(int)recordIndex];
        entry = new OSHostAppLogTableEntry(slot.StreamId, slot.Flags, slot.Sequence, slot.ByteLength);
        return OSHostStatus.Ok;
    }

    public OSHostAppLogReadResult TryCopyRecord(uint recordIndex, Span<byte> destination)
    {
        if (!IsValidIndex(recordIndex))
        {
            return OSHostAppLogReadResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        AppLogSlot slot = slots[(int)recordIndex];
        if (destination.Length < slot.ByteLength)
        {
            return new OSHostAppLogReadResult(OSHostStatus.BufferTooSmall, slot.StreamId, slot.Flags, slot.Sequence, bytesCopied: 0, requiredBytes: slot.ByteLength);
        }

        if (!slot.CopyTo(destination))
        {
            return new OSHostAppLogReadResult(OSHostStatus.BufferTooSmall, slot.StreamId, slot.Flags, slot.Sequence, bytesCopied: 0, requiredBytes: slot.ByteLength);
        }

        return new OSHostAppLogReadResult(OSHostStatus.Ok, slot.StreamId, slot.Flags, slot.Sequence, slot.ByteLength, slot.ByteLength);
    }

    public static bool IsValidStream(uint streamId)
    {
        return streamId is OSHostAppLogConstants.StreamStdout or OSHostAppLogConstants.StreamStderr;
    }

    private bool IsValidIndex(uint recordIndex)
    {
        return recordIndex < (uint)count;
    }
}
