namespace OS.Kernel.Diagnostics;

public static class BootEventRecordFormatter
{
    public const int NoInvalidRecord = -1;
    private const int Hex64TextLength = 18;
    private const int LineEndingLength = 2;
    public static BootEventRecordFormatterResult FormatSnapshot(ReadOnlySpan<BootEventRecord> records, Span<byte> destination)
    {
        int requiredBytes = GetRequiredBytes(records, out int invalidRecordIndex);
        if (records.Length == 0)
        {
            return BootEventRecordFormatterResult.Fail(BootEventRecordFormatterStatus.NoRecords, PanicTextBufferStatus.Ok, 0, 0, 0, 0, NoInvalidRecord);
        }

        if (invalidRecordIndex != NoInvalidRecord)
        {
            return BootEventRecordFormatterResult.Fail(BootEventRecordFormatterStatus.InvalidRecord, PanicTextBufferStatus.Ok, records.Length, 0, 0, requiredBytes, invalidRecordIndex);
        }

        if (destination.Length < requiredBytes)
        {
            return BootEventRecordFormatterResult.Fail(BootEventRecordFormatterStatus.DestinationTooSmall, PanicTextBufferStatus.Ok, records.Length, 0, 0, requiredBytes, NoInvalidRecord);
        }

        PanicTextBuffer buffer = new PanicTextBuffer(destination);
        for (int index = 0; index < records.Length; index++)
        {
            BootEventRecordFormatterResult recordResult = WriteRecord(in records[index], ref buffer);
            if (!recordResult.Succeeded)
            {
                return BootEventRecordFormatterResult.Fail(recordResult.Status, recordResult.TextStatus, records.Length, index, buffer.Written, requiredBytes, recordResult.InvalidRecordIndex == NoInvalidRecord ? index : recordResult.InvalidRecordIndex);
            }
        }

        return BootEventRecordFormatterResult.Success(records.Length, records.Length, buffer.Written, requiredBytes);
    }

    public static BootEventRecordFormatterResult WriteRecord(in BootEventRecord record, ref PanicTextBuffer buffer)
    {
        if (!buffer.IsInitialized)
        {
            return BootEventRecordFormatterResult.Fail(BootEventRecordFormatterStatus.BufferUnavailable, buffer.Status, 1, 0, buffer.Written, 0, NoInvalidRecord);
        }

        if (!IsValidRecord(in record))
        {
            return BootEventRecordFormatterResult.Fail(BootEventRecordFormatterStatus.InvalidRecord, buffer.Status, 1, 0, buffer.Written, 0, 0);
        }

        int requiredBytes = GetRequiredBytes(in record);
        if (buffer.Remaining < requiredBytes)
        {
            return BootEventRecordFormatterResult.Fail(BootEventRecordFormatterStatus.DestinationTooSmall, buffer.Status, 1, 0, buffer.Written, requiredBytes, NoInvalidRecord);
        }

        int start = buffer.Written;
        WriteRecordUnchecked(in record, ref buffer);
        if (buffer.Status != PanicTextBufferStatus.Ok)
        {
            return BootEventRecordFormatterResult.Fail(BootEventRecordFormatterStatus.BufferWriteFailed, buffer.Status, 1, 0, buffer.Written, requiredBytes, NoInvalidRecord);
        }

        return BootEventRecordFormatterResult.Success(1, 1, buffer.Written - start, requiredBytes);
    }

    private static int GetRequiredBytes(ReadOnlySpan<BootEventRecord> records, out int invalidRecordIndex)
    {
        invalidRecordIndex = NoInvalidRecord;
        int requiredBytes = 0;
        for (int index = 0; index < records.Length; index++)
        {
            if (!IsValidRecord(in records[index]))
            {
                invalidRecordIndex = index;
                return requiredBytes;
            }

            requiredBytes += GetRequiredBytes(in records[index]);
        }

        return requiredBytes;
    }

    private static int GetRequiredBytes(in BootEventRecord record)
    {
        return "BOOT_EVENT_RECORD sequence=".Length + GetUnsignedDecimalDigitCount(record.Sequence) + " kind=".Length + GetKindTextLength(record.Kind) + " phase=".Length + GetUnsignedDecimalDigitCount(record.Phase) + " eventId=".Length + GetUnsignedDecimalDigitCount(record.EventId) + " arg0=".Length + Hex64TextLength + " arg1=".Length + Hex64TextLength + " arg2=".Length + Hex64TextLength + LineEndingLength;
    }

    private static void WriteRecordUnchecked(in BootEventRecord record, ref PanicTextBuffer buffer)
    {
        buffer.WriteAscii("BOOT_EVENT_RECORD sequence=");
        buffer.WriteUnsignedDecimal(record.Sequence);
        buffer.WriteAscii(" kind=");
        WriteKind(record.Kind, ref buffer);
        buffer.WriteAscii(" phase=");
        buffer.WriteUnsignedDecimal(record.Phase);
        buffer.WriteAscii(" eventId=");
        buffer.WriteUnsignedDecimal(record.EventId);
        buffer.WriteAscii(" arg0=");
        buffer.WriteHex64(record.Arg0);
        buffer.WriteAscii(" arg1=");
        buffer.WriteHex64(record.Arg1);
        buffer.WriteAscii(" arg2=");
        buffer.WriteHex64(record.Arg2);
        buffer.WriteCrLf();
    }

    private static void WriteKind(BootEventRecordKind kind, ref PanicTextBuffer buffer)
    {
        switch (kind)
        {
            case BootEventRecordKind.Marker:
                buffer.WriteAscii("marker");
                break;
            case BootEventRecordKind.Phase:
                buffer.WriteAscii("phase");
                break;
            case BootEventRecordKind.Diagnostic:
                buffer.WriteAscii("diagnostic");
                break;
            case BootEventRecordKind.Panic:
                buffer.WriteAscii("panic");
                break;
        }
    }

    private static bool IsValidRecord(in BootEventRecord record)
    {
        return record.IsInitialized && IsKnownKind(record.Kind);
    }

    private static bool IsKnownKind(BootEventRecordKind kind)
    {
        return kind == BootEventRecordKind.Marker || kind == BootEventRecordKind.Phase || kind == BootEventRecordKind.Diagnostic || kind == BootEventRecordKind.Panic;
    }

    private static int GetKindTextLength(BootEventRecordKind kind)
    {
        return kind switch
        {
            BootEventRecordKind.Marker => "marker".Length,
            BootEventRecordKind.Phase => "phase".Length,
            BootEventRecordKind.Diagnostic => "diagnostic".Length,
            BootEventRecordKind.Panic => "panic".Length,
            _ => 0
        };
    }

    private static int GetUnsignedDecimalDigitCount(ulong value)
    {
        int digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }

        return digits;
    }
}
