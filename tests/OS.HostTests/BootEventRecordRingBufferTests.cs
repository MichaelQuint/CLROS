using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class BootEventRecordRingBufferTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostics.boot_event_record records caller owned events", TestRecordsCallerOwnedEvents, ref failures);
        Run("diagnostics.boot_event_record overwrites oldest deterministically", TestOverwritesOldest, ref failures);
        Run("diagnostics.boot_event_record reports snapshot capacity", TestReportsSnapshotCapacity, ref failures);
        Run("diagnostics.boot_event_record rejects missing storage", TestRejectsMissingStorage, ref failures);
        Run("diagnostics.boot_event_record default results are not success", TestDefaultResultsAreNotSuccess, ref failures);
        Run("diagnostics.boot_event_record layouts contain no managed references", TestLayoutsContainNoManagedReferences, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Boot event record ring buffer tests failed: " + failures);
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

    private static bool TestRecordsCallerOwnedEvents()
    {
        Span<BootEventRecord> storage = stackalloc BootEventRecord[4];
        BootEventRecordRingBuffer buffer = new BootEventRecordRingBuffer(storage);

        BootEventRecordRingBufferResult first = buffer.Record(
            BootEventRecordKind.Marker,
            phase: 1,
            eventId: 10,
            arg0: 0xAA);
        BootEventRecordRingBufferResult second = buffer.Record(
            BootEventRecordKind.Diagnostic,
            phase: 2,
            eventId: 20,
            arg0: 0xBB,
            arg1: 0xCC);

        Span<BootEventRecord> snapshot = stackalloc BootEventRecord[4];
        BootEventRecordRingBufferResult snapshotResult = buffer.Snapshot(snapshot);

        return first.Succeeded
            && second.Succeeded
            && snapshotResult.Succeeded
            && snapshotResult.Written == 2
            && snapshotResult.Required == 2
            && snapshot[0].IsInitialized
            && snapshot[0].Sequence == 1
            && snapshot[0].Kind == BootEventRecordKind.Marker
            && snapshot[0].Phase == 1
            && snapshot[0].EventId == 10
            && snapshot[0].Arg0 == 0xAA
            && snapshot[1].IsInitialized
            && snapshot[1].Sequence == 2
            && snapshot[1].Kind == BootEventRecordKind.Diagnostic
            && snapshot[1].Phase == 2
            && snapshot[1].EventId == 20
            && snapshot[1].Arg0 == 0xBB
            && snapshot[1].Arg1 == 0xCC
            && buffer.Count == 2
            && buffer.NextSequence == 3;
    }

    private static bool TestOverwritesOldest()
    {
        Span<BootEventRecord> storage = stackalloc BootEventRecord[3];
        BootEventRecordRingBuffer buffer = new BootEventRecordRingBuffer(storage);

        buffer.Record(BootEventRecordKind.Phase, phase: 0, eventId: 100);
        buffer.Record(BootEventRecordKind.Phase, phase: 1, eventId: 101);
        buffer.Record(BootEventRecordKind.Phase, phase: 2, eventId: 102);
        buffer.Record(BootEventRecordKind.Panic, phase: 3, eventId: 103);

        Span<BootEventRecord> snapshot = stackalloc BootEventRecord[3];
        BootEventRecordRingBufferResult result = buffer.Snapshot(snapshot);

        return result.Succeeded
            && result.Written == 3
            && result.Required == 3
            && snapshot[0].Sequence == 2
            && snapshot[0].EventId == 101
            && snapshot[1].Sequence == 3
            && snapshot[1].EventId == 102
            && snapshot[2].Sequence == 4
            && snapshot[2].Kind == BootEventRecordKind.Panic
            && snapshot[2].EventId == 103
            && buffer.Count == 3
            && buffer.NextSequence == 5;
    }

    private static bool TestReportsSnapshotCapacity()
    {
        Span<BootEventRecord> storage = stackalloc BootEventRecord[4];
        BootEventRecordRingBuffer buffer = new BootEventRecordRingBuffer(storage);
        buffer.Record(BootEventRecordKind.Marker, phase: 0, eventId: 1);
        buffer.Record(BootEventRecordKind.Marker, phase: 0, eventId: 2);
        buffer.Record(BootEventRecordKind.Marker, phase: 0, eventId: 3);
        buffer.Record(BootEventRecordKind.Marker, phase: 0, eventId: 4);

        Span<BootEventRecord> snapshot = stackalloc BootEventRecord[2];
        BootEventRecordRingBufferResult result = buffer.Snapshot(snapshot);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == BootEventRecordRingBufferStatus.DestinationTooSmall
            && result.Written == 2
            && result.Required == 4
            && result.Count == 4
            && snapshot[0].Sequence == 3
            && snapshot[0].EventId == 3
            && snapshot[1].Sequence == 4
            && snapshot[1].EventId == 4;
    }

    private static bool TestRejectsMissingStorage()
    {
        Span<BootEventRecord> storage = stackalloc BootEventRecord[0];
        BootEventRecordRingBuffer buffer = new BootEventRecordRingBuffer(storage);

        BootEventRecordRingBufferResult write = buffer.Record(BootEventRecordKind.Marker, phase: 0, eventId: 1);
        Span<BootEventRecord> snapshot = stackalloc BootEventRecord[1];
        BootEventRecordRingBufferResult read = buffer.Snapshot(snapshot);

        return !buffer.IsInitialized
            && !write.Succeeded
            && write.Status == BootEventRecordRingBufferStatus.NoStorage
            && write.Required == 1
            && !read.Succeeded
            && read.Status == BootEventRecordRingBufferStatus.NotInitialized
            && read.Written == 0;
    }

    private static bool TestDefaultResultsAreNotSuccess()
    {
        BootEventRecordRingBufferResult result = default;
        BootEventRecord record = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == BootEventRecordRingBufferStatus.Success
            && !record.IsInitialized;
    }

    private static bool TestLayoutsContainNoManagedReferences()
    {
        return !RuntimeHelpers.IsReferenceOrContainsReferences<BootEventRecord>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<BootEventRecordRingBufferResult>();
    }
}
