using System.Runtime.CompilerServices;
using System.Text;
using OS.Pal;

internal static class PalAppLogFacadeTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.app_log facade writes stdout", TestWritesStdout, ref failures);
        Run("pal.app_log facade writes stderr", TestWritesStderr, ref failures);
        Run("pal.app_log facade rejects invalid stream", TestRejectsInvalidStream, ref failures);
        Run("pal.app_log facade rejects oversized write", TestRejectsOversizedWrite, ref failures);
        Run("pal.app_log facade reports exhaustion", TestReportsExhaustion, ref failures);
        Run("pal.app_log facade preserves machine marker flag", TestPreservesMachineMarkerFlag, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL app log facade host tests failed: " + failures);
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

    private static bool TestWritesStdout()
    {
        OSHostAppLogTable table = new(recordCapacity: 2, maxRecordBytes: 64);
        OSHostAppLogFacade facade = new(table);
        byte[] payload = Encoding.UTF8.GetBytes("stdout from hosted app");

        OSHostAppLogWriteResult write = facade.WriteLogBytes(
            Request(OSHostAppLogConstants.StreamStdout, payload, flags: 0),
            payload);

        Span<byte> destination = stackalloc byte[payload.Length];
        OSHostAppLogReadResult read = table.TryCopyRecord(write.RecordIndex, destination);

        return write.Succeeded
            && write.RecordIndex == 0
            && write.BytesWritten == (uint)payload.Length
            && table.Count == 1
            && read.Succeeded
            && read.StreamId == OSHostAppLogConstants.StreamStdout
            && read.Flags == 0
            && BytesEqual(payload, destination);
    }

    private static bool TestWritesStderr()
    {
        OSHostAppLogTable table = new(recordCapacity: 2, maxRecordBytes: 64);
        OSHostAppLogFacade facade = new(table);
        byte[] payload = Encoding.UTF8.GetBytes("stderr from hosted app");

        OSHostAppLogWriteResult write = facade.WriteLogBytes(
            Request(OSHostAppLogConstants.StreamStderr, payload, OSHostAppLogConstants.FlagMachineReadable),
            payload);

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
            && read.Succeeded
            && read.StreamId == OSHostAppLogConstants.StreamStderr
            && BytesEqual(payload, destination);
    }

    private static bool TestRejectsInvalidStream()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 16);
        OSHostAppLogFacade facade = new(table);
        byte[] payload = [0x41];

        OSHostAppLogWriteResult write = facade.WriteLogBytes(
            Request(streamId: 99, payload, flags: 0),
            payload);

        return write.Status == OSHostStatus.InvalidArgument
            && write.RecordIndex == uint.MaxValue
            && write.BytesWritten == 0
            && table.Count == 0;
    }

    private static bool TestRejectsOversizedWrite()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 4);
        OSHostAppLogFacade facade = new(table);
        byte[] payload = [0x41, 0x42, 0x43, 0x44, 0x45];

        OSHostAppLogWriteResult write = facade.WriteLogBytes(
            Request(OSHostAppLogConstants.StreamStdout, payload, flags: 0),
            payload);
        OSHostAppLogReadResult read = table.TryCopyRecord(0, stackalloc byte[1]);

        return write.Status == OSHostStatus.BufferTooSmall
            && write.RecordIndex == uint.MaxValue
            && write.BytesWritten == 0
            && table.Count == 0
            && read.Status == OSHostStatus.InvalidArgument;
    }

    private static bool TestReportsExhaustion()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 16);
        OSHostAppLogFacade facade = new(table);
        byte[] firstPayload = [0x41];
        byte[] secondPayload = [0x42];

        OSHostAppLogWriteResult first = facade.WriteLogBytes(
            Request(OSHostAppLogConstants.StreamStdout, firstPayload, flags: 0),
            firstPayload);
        OSHostAppLogWriteResult second = facade.WriteLogBytes(
            Request(OSHostAppLogConstants.StreamStderr, secondPayload, flags: 0),
            secondPayload);

        return first.Succeeded
            && first.RecordIndex == 0
            && second.Status == OSHostStatus.OutOfMemory
            && second.RecordIndex == uint.MaxValue
            && second.BytesWritten == 0
            && table.Count == 1;
    }

    private static bool TestPreservesMachineMarkerFlag()
    {
        OSHostAppLogTable table = new(recordCapacity: 2, maxRecordBytes: 128);
        OSHostAppLogFacade facade = new(table);
        byte[] marker = Encoding.ASCII.GetBytes("APP_EXIT code=0 status=OSHOST_STATUS_OK");

        OSHostAppLogWriteResult write = facade.WriteLogBytes(
            Request(OSHostAppLogConstants.StreamStdout, marker, OSHostAppLogConstants.FlagMachineReadable),
            marker);

        Span<byte> destination = stackalloc byte[marker.Length];
        OSHostAppLogReadResult read = table.TryCopyRecord(write.RecordIndex, destination);

        return write.Succeeded
            && read.Succeeded
            && read.StreamId == OSHostAppLogConstants.StreamStdout
            && read.Flags == OSHostAppLogConstants.FlagMachineReadable
            && Encoding.ASCII.GetString(destination) == "APP_EXIT code=0 status=OSHOST_STATUS_OK";
    }

    private static OSHostWriteLogBytesRequest Request(uint streamId, byte[] payload, uint flags)
    {
        return new OSHostWriteLogBytesRequest(
            streamId,
            flags,
            new OSHostByteSpan(address: 0x1000, length: (ulong)payload.Length),
            reserved: 0);
    }

    private static bool BytesEqual(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        return expected.SequenceEqual(actual[..expected.Length]);
    }
}
