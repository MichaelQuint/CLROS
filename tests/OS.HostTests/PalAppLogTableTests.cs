using System.Runtime.CompilerServices;
using System.Text;
using OS.Pal;

internal static class PalAppLogTableTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.app_log stdout write captured", TestStdoutWriteCaptured, ref failures);
        Run("pal.app_log stderr write captured", TestStderrWriteCaptured, ref failures);
        Run("pal.app_log invalid stream rejected", TestInvalidStreamRejected, ref failures);
        Run("pal.app_log oversized write rejected", TestOversizedWriteRejected, ref failures);
        Run("pal.app_log preserves app status marker", TestPreservesAppStatusMarker, ref failures);
        Run("pal.app_log reports deterministic exhaustion", TestReportsDeterministicExhaustion, ref failures);
        Run("pal.app_log default results are not success", TestDefaultResultsAreNotSuccess, ref failures);
        Run("pal.applog table uses shared buffer operations", TestUsesSharedBufferOperations, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL app log table host tests failed: " + failures);
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

    private static bool TestStdoutWriteCaptured()
    {
        OSHostAppLogTable table = new(recordCapacity: 2, maxRecordBytes: 64);
        byte[] payload = Encoding.UTF8.GetBytes("hello stdout");

        OSHostAppLogWriteResult write = table.Write(
            OSHostAppLogConstants.StreamStdout,
            payload,
            flags: 0);

        Span<byte> destination = stackalloc byte[payload.Length];
        OSHostAppLogReadResult read = table.TryCopyRecord(write.RecordIndex, destination);

        return write.Succeeded
            && write.IsInitialized
            && write.RecordIndex == 0
            && write.BytesWritten == (uint)payload.Length
            && table.Count == 1
            && read.Succeeded
            && read.IsInitialized
            && read.StreamId == OSHostAppLogConstants.StreamStdout
            && read.BytesCopied == (uint)payload.Length
            && BytesEqual(payload, destination);
    }

    private static bool TestStderrWriteCaptured()
    {
        OSHostAppLogTable table = new(recordCapacity: 2, maxRecordBytes: 64);
        byte[] payload = Encoding.UTF8.GetBytes("stderr warning");

        OSHostAppLogWriteResult write = table.Write(
            OSHostAppLogConstants.StreamStderr,
            payload,
            OSHostAppLogConstants.FlagMachineReadable);

        if (!write.Succeeded
            || table.TryGetEntry(write.RecordIndex, out OSHostAppLogTableEntry entry) != OSHostStatus.Ok)
        {
            return false;
        }

        Span<byte> destination = stackalloc byte[payload.Length];
        OSHostAppLogReadResult read = table.TryCopyRecord(write.RecordIndex, destination);

        return entry.IsStderr
            && !entry.IsStdout
            && entry.Flags == OSHostAppLogConstants.FlagMachineReadable
            && entry.ByteLength == (uint)payload.Length
            && read.Succeeded
            && read.IsInitialized
            && read.Sequence == entry.Sequence
            && BytesEqual(payload, destination);
    }

    private static bool TestInvalidStreamRejected()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 8);
        OSHostAppLogWriteResult write = table.Write(streamId: 99, [0x41], flags: 0);

        return write.Status == OSHostStatus.InvalidArgument
            && write.IsInitialized
            && write.RecordIndex == uint.MaxValue
            && write.BytesWritten == 0
            && table.Count == 0
            && !OSHostAppLogTable.IsValidStream(99);
    }

    private static bool TestOversizedWriteRejected()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 4);
        OSHostAppLogWriteResult write = table.Write(
            OSHostAppLogConstants.StreamStdout,
            [0x41, 0x42, 0x43, 0x44, 0x45],
            flags: 0);

        Span<byte> destination = stackalloc byte[1];
        OSHostAppLogReadResult read = table.TryCopyRecord(0, destination);

        return write.Status == OSHostStatus.BufferTooSmall
            && write.IsInitialized
            && write.BytesWritten == 0
            && table.Count == 0
            && read.IsInitialized
            && read.Status == OSHostStatus.InvalidArgument;
    }

    private static bool TestPreservesAppStatusMarker()
    {
        OSHostAppLogTable table = new(recordCapacity: 2, maxRecordBytes: 128);
        byte[] marker = Encoding.ASCII.GetBytes("APP_EXIT code=0 status=OSHOST_STATUS_OK");

        OSHostAppLogWriteResult write = table.Write(
            OSHostAppLogConstants.StreamStdout,
            marker,
            OSHostAppLogConstants.FlagMachineReadable);

        Span<byte> destination = stackalloc byte[marker.Length];
        OSHostAppLogReadResult read = table.TryCopyRecord(write.RecordIndex, destination);

        return write.Succeeded
            && write.IsInitialized
            && read.Succeeded
            && read.IsInitialized
            && read.StreamId == OSHostAppLogConstants.StreamStdout
            && read.Flags == OSHostAppLogConstants.FlagMachineReadable
            && Encoding.ASCII.GetString(destination) == "APP_EXIT code=0 status=OSHOST_STATUS_OK";
    }

    private static bool TestReportsDeterministicExhaustion()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 16);
        OSHostAppLogWriteResult first = table.Write(
            OSHostAppLogConstants.StreamStdout,
            [0x41],
            flags: 0);
        OSHostAppLogWriteResult second = table.Write(
            OSHostAppLogConstants.StreamStderr,
            [0x42],
            flags: 0);

        return first.Succeeded
            && first.IsInitialized
            && first.RecordIndex == 0
            && second.Status == OSHostStatus.OutOfMemory
            && second.IsInitialized
            && second.RecordIndex == uint.MaxValue
            && table.Count == 1;
    }

    private static bool TestDefaultResultsAreNotSuccess()
    {
        OSHostAppLogWriteResult write = default;
        OSHostAppLogReadResult read = default;

        return !write.IsInitialized
            && !write.Succeeded
            && write.Status == OSHostStatus.Ok
            && write.RecordIndex == 0
            && write.BytesWritten == 0
            && !read.IsInitialized
            && !read.Succeeded
            && read.Status == OSHostStatus.Ok
            && read.StreamId == 0
            && read.Flags == 0
            && read.Sequence == 0
            && read.BytesCopied == 0
            && read.RequiredBytes == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostAppLogWriteResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostAppLogReadResult>();
    }

    private static bool TestUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Pal",
            "OSHostAppLogTable.cs"));

        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(bytes, payload)", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(source, destination)", StringComparison.Ordinal)
            && !source.Contains("bytes.CopyTo(payload)", StringComparison.Ordinal)
            && !source.Contains(
                "payload.AsSpan(0, (int)ByteLength).CopyTo(destination)",
                StringComparison.Ordinal);
    }

    private static bool BytesEqual(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        return expected.SequenceEqual(actual[..expected.Length]);
    }
}
