using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static class BootEventRecordFormatterTests
{

    [HostTest("diagnostics.boot_event_formatter formats snapshot records", HostLane.Fast, 0)]
    private static bool TestFormatsSnapshotRecords()
    {
        Span<BootEventRecord> records = stackalloc BootEventRecord[2];
        records[0] = BootEventRecord.Create(
            42,
            BootEventRecordKind.Marker,
            phase: 1,
            eventId: 7,
            arg0: 0x00000000000000AA,
            arg1: 0,
            arg2: 0x0123456789ABCDEF);
        records[1] = BootEventRecord.Create(
            43,
            BootEventRecordKind.Panic,
            phase: 2,
            eventId: 8,
            arg0: 0xFFFFFFFFFFFFFFFF,
            arg1: 0x0000000000000001,
            arg2: 0);

        byte[] destination = new byte[384];
        BootEventRecordFormatterResult result = BootEventRecordFormatter.FormatSnapshot(records, destination);

        string expected =
            "BOOT_EVENT_RECORD sequence=42 kind=marker phase=1 eventId=7"
            + " arg0=0x00000000000000AA arg1=0x0000000000000000 arg2=0x0123456789ABCDEF\r\n"
            + "BOOT_EVENT_RECORD sequence=43 kind=panic phase=2 eventId=8"
            + " arg0=0xFFFFFFFFFFFFFFFF arg1=0x0000000000000001 arg2=0x0000000000000000\r\n";

        return HostCheck.Result(result.Succeeded
            && result.Status == BootEventRecordFormatterStatus.Success
            && result.TextStatus == PanicTextBufferStatus.Ok
            && result.RecordsRequested == 2
            && result.RecordsWritten == 2
            && result.BytesWritten == expected.Length
            && result.RequiredBytes == expected.Length
            && result.InvalidRecordIndex == BootEventRecordFormatter.NoInvalidRecord
            && ToText(destination.AsSpan(0, result.BytesWritten)) == expected);
    }

    [HostTest("diagnostics.boot_event_formatter reports destination capacity", HostLane.Fast, 1)]
    private static bool TestReportsDestinationCapacity()
    {
        Span<BootEventRecord> records = stackalloc BootEventRecord[1];
        records[0] = BootEventRecord.Create(
            1,
            BootEventRecordKind.Diagnostic,
            phase: 2,
            eventId: 99,
            arg0: 0x10,
            arg1: 0x20,
            arg2: 0x30);

        byte[] destination = new byte[32];
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = 0xcc;
        }

        BootEventRecordFormatterResult result = BootEventRecordFormatter.FormatSnapshot(records, destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == BootEventRecordFormatterStatus.DestinationTooSmall
            && result.TextStatus == PanicTextBufferStatus.Ok
            && result.RecordsRequested == 1
            && result.RecordsWritten == 0
            && result.BytesWritten == 0
            && result.RequiredBytes > destination.Length
            && destination[0] == 0xcc
            && destination[destination.Length - 1] == 0xcc);
    }

    [HostTest("diagnostics.boot_event_formatter rejects invalid records", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidRecords()
    {
        Span<BootEventRecord> records = stackalloc BootEventRecord[2];
        records[0] = BootEventRecord.Create(
            1,
            BootEventRecordKind.Phase,
            phase: 1,
            eventId: 2,
            arg0: 3,
            arg1: 4,
            arg2: 5);
        records[1] = default;

        byte[] destination = new byte[256];
        BootEventRecordFormatterResult result = BootEventRecordFormatter.FormatSnapshot(records, destination);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == BootEventRecordFormatterStatus.InvalidRecord
            && result.TextStatus == PanicTextBufferStatus.Ok
            && result.RecordsRequested == 2
            && result.RecordsWritten == 0
            && result.BytesWritten == 0
            && result.RequiredBytes > 0
            && result.InvalidRecordIndex == 1
            && destination[0] == 0);
    }

    [HostTest("diagnostics.boot_event_formatter writes through panic buffer", HostLane.Fast, 3)]
    private static bool TestWritesThroughPanicBuffer()
    {
        byte[] destination = new byte[192];
        PanicTextBuffer buffer = new PanicTextBuffer(destination);
        BootEventRecord record = BootEventRecord.Create(
            5,
            BootEventRecordKind.Phase,
            phase: 9,
            eventId: 11,
            arg0: 0,
            arg1: 0x2222,
            arg2: 0x3333);

        BootEventRecordFormatterResult result = BootEventRecordFormatter.WriteRecord(
            in record,
            ref buffer);

        string expected = "BOOT_EVENT_RECORD sequence=5 kind=phase phase=9 eventId=11"
            + " arg0=0x0000000000000000 arg1=0x0000000000002222 arg2=0x0000000000003333\r\n";

        return HostCheck.Result(result.Succeeded
            && result.RecordsRequested == 1
            && result.RecordsWritten == 1
            && result.BytesWritten == expected.Length
            && buffer.Succeeded
            && buffer.Written == expected.Length
            && ToText(buffer.WrittenSpan) == expected);
    }

    [HostTest("diagnostics.boot_event_formatter default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        BootEventRecordFormatterResult result = default;
        byte[] destination = new byte[16];
        BootEventRecordFormatterResult noRecords = BootEventRecordFormatter.FormatSnapshot(
            ReadOnlySpan<BootEventRecord>.Empty,
            destination);

        PanicTextBuffer buffer = default;
        BootEventRecord record = BootEventRecord.Create(
            1,
            BootEventRecordKind.Marker,
            phase: 0,
            eventId: 1,
            arg0: 0,
            arg1: 0,
            arg2: 0);
        BootEventRecordFormatterResult missingBuffer = BootEventRecordFormatter.WriteRecord(
            in record,
            ref buffer);

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == BootEventRecordFormatterStatus.Success
            && !noRecords.Succeeded
            && noRecords.Status == BootEventRecordFormatterStatus.NoRecords
            && !missingBuffer.Succeeded
            && missingBuffer.Status == BootEventRecordFormatterStatus.BufferUnavailable);
    }

    [HostTest("diagnostics.boot_event_formatter result contains no managed references", HostLane.Fast, 5)]
    private static bool TestResultContainsNoManagedReferences()
    {
        return HostCheck.Result(!RuntimeHelpers.IsReferenceOrContainsReferences<BootEventRecordFormatterResult>());
    }

    private static string ToText(ReadOnlySpan<byte> bytes)
    {
        return Encoding.ASCII.GetString(bytes);
    }
}
