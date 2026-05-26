using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static class BootEventRecordFormatterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostics.boot_event_formatter formats snapshot records", TestFormatsSnapshotRecords, ref failures);
        Run("diagnostics.boot_event_formatter reports destination capacity", TestReportsDestinationCapacity, ref failures);
        Run("diagnostics.boot_event_formatter rejects invalid records", TestRejectsInvalidRecords, ref failures);
        Run("diagnostics.boot_event_formatter writes through panic buffer", TestWritesThroughPanicBuffer, ref failures);
        Run("diagnostics.boot_event_formatter default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("diagnostics.boot_event_formatter result contains no managed references", TestResultContainsNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Boot event record formatter tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

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

        return result.Succeeded
            && result.Status == BootEventRecordFormatterStatus.Success
            && result.TextStatus == PanicTextBufferStatus.Ok
            && result.RecordsRequested == 2
            && result.RecordsWritten == 2
            && result.BytesWritten == expected.Length
            && result.RequiredBytes == expected.Length
            && result.InvalidRecordIndex == BootEventRecordFormatter.NoInvalidRecord
            && ToText(destination.AsSpan(0, result.BytesWritten)) == expected;
    }

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

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == BootEventRecordFormatterStatus.DestinationTooSmall
            && result.TextStatus == PanicTextBufferStatus.Ok
            && result.RecordsRequested == 1
            && result.RecordsWritten == 0
            && result.BytesWritten == 0
            && result.RequiredBytes > destination.Length
            && destination[0] == 0xcc
            && destination[destination.Length - 1] == 0xcc;
    }

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

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == BootEventRecordFormatterStatus.InvalidRecord
            && result.TextStatus == PanicTextBufferStatus.Ok
            && result.RecordsRequested == 2
            && result.RecordsWritten == 0
            && result.BytesWritten == 0
            && result.RequiredBytes > 0
            && result.InvalidRecordIndex == 1
            && destination[0] == 0;
    }

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

        return result.Succeeded
            && result.RecordsRequested == 1
            && result.RecordsWritten == 1
            && result.BytesWritten == expected.Length
            && buffer.Succeeded
            && buffer.Written == expected.Length
            && ToText(buffer.WrittenSpan) == expected;
    }

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

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == BootEventRecordFormatterStatus.Success
            && !noRecords.Succeeded
            && noRecords.Status == BootEventRecordFormatterStatus.NoRecords
            && !missingBuffer.Succeeded
            && missingBuffer.Status == BootEventRecordFormatterStatus.BufferUnavailable;
    }

    private static bool TestResultContainsNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<BootEventRecordFormatterResult>();
    }

    private static string ToText(ReadOnlySpan<byte> bytes)
    {
        return Encoding.ASCII.GetString(bytes);
    }
}
