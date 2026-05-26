using OS.Std.Memory;

namespace OS.Pal;

public static class OSHostAppLogConstants
{
    public const uint StreamStdout = 1;

    public const uint StreamStderr = 2;

    public const uint FlagMachineReadable = 1;
}

public readonly struct OSHostAppLogWriteResult
{
    public readonly OSHostStatus Status;

    public readonly uint RecordIndex;

    public readonly uint BytesWritten;

    public readonly bool IsInitialized;

    public OSHostAppLogWriteResult(
        OSHostStatus status,
        uint recordIndex,
        uint bytesWritten,
        bool isInitialized = true)
    {
        Status = status;
        RecordIndex = recordIndex;
        BytesWritten = bytesWritten;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostAppLogWriteResult FromStatus(OSHostStatus status)
    {
        return new OSHostAppLogWriteResult(status, uint.MaxValue, bytesWritten: 0);
    }
}

public readonly struct OSHostAppLogReadResult
{
    public readonly OSHostStatus Status;

    public readonly uint StreamId;

    public readonly uint Flags;

    public readonly uint Sequence;

    public readonly uint BytesCopied;

    public readonly uint RequiredBytes;

    public readonly bool IsInitialized;

    public OSHostAppLogReadResult(
        OSHostStatus status,
        uint streamId,
        uint flags,
        uint sequence,
        uint bytesCopied,
        uint requiredBytes,
        bool isInitialized = true)
    {
        Status = status;
        StreamId = streamId;
        Flags = flags;
        Sequence = sequence;
        BytesCopied = bytesCopied;
        RequiredBytes = requiredBytes;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostAppLogReadResult FromStatus(OSHostStatus status)
    {
        return new OSHostAppLogReadResult(
            status,
            streamId: 0,
            flags: 0,
            sequence: 0,
            bytesCopied: 0,
            requiredBytes: 0);
    }
}

public readonly struct OSHostAppLogTableEntry
{
    public readonly uint StreamId;

    public readonly uint Flags;

    public readonly uint Sequence;

    public readonly uint ByteLength;

    public OSHostAppLogTableEntry(uint streamId, uint flags, uint sequence, uint byteLength)
    {
        StreamId = streamId;
        Flags = flags;
        Sequence = sequence;
        ByteLength = byteLength;
    }

    public bool IsStdout => StreamId == OSHostAppLogConstants.StreamStdout;

    public bool IsStderr => StreamId == OSHostAppLogConstants.StreamStderr;
}

public sealed class OSHostAppLogTable
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
        entry = new OSHostAppLogTableEntry(
            slot.StreamId,
            slot.Flags,
            slot.Sequence,
            slot.ByteLength);
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
            return new OSHostAppLogReadResult(
                OSHostStatus.BufferTooSmall,
                slot.StreamId,
                slot.Flags,
                slot.Sequence,
                bytesCopied: 0,
                requiredBytes: slot.ByteLength);
        }

        if (!slot.CopyTo(destination))
        {
            return new OSHostAppLogReadResult(
                OSHostStatus.BufferTooSmall,
                slot.StreamId,
                slot.Flags,
                slot.Sequence,
                bytesCopied: 0,
                requiredBytes: slot.ByteLength);
        }

        return new OSHostAppLogReadResult(
            OSHostStatus.Ok,
            slot.StreamId,
            slot.Flags,
            slot.Sequence,
            slot.ByteLength,
            slot.ByteLength);
    }

    public static bool IsValidStream(uint streamId)
    {
        return streamId is OSHostAppLogConstants.StreamStdout or OSHostAppLogConstants.StreamStderr;
    }

    private bool IsValidIndex(uint recordIndex)
    {
        return recordIndex < (uint)count;
    }

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
