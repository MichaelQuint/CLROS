using System.Runtime.CompilerServices;
using System.Text;
using OS.Pal;

internal static class PalAppLogFacadeTests
{

    [HostTest("pal.app_log facade writes stdout", HostLane.Fast, 0)]
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

        return HostCheck.Result(write.Succeeded
            && write.RecordIndex == 0
            && write.BytesWritten == (uint)payload.Length
            && table.Count == 1
            && read.Succeeded
            && read.StreamId == OSHostAppLogConstants.StreamStdout
            && read.Flags == 0
            && BytesEqual(payload, destination));
    }

    [HostTest("pal.app_log facade writes stderr", HostLane.Fast, 1)]
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
            return HostCheck.Result(false);
        }

        Span<byte> destination = stackalloc byte[payload.Length];
        OSHostAppLogReadResult read = table.TryCopyRecord(write.RecordIndex, destination);

        return HostCheck.Result(entry.IsStderr
            && !entry.IsStdout
            && entry.Flags == OSHostAppLogConstants.FlagMachineReadable
            && read.Succeeded
            && read.StreamId == OSHostAppLogConstants.StreamStderr
            && BytesEqual(payload, destination));
    }

    [HostTest("pal.app_log facade rejects invalid stream", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidStream()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 16);
        OSHostAppLogFacade facade = new(table);
        byte[] payload = [0x41];

        OSHostAppLogWriteResult write = facade.WriteLogBytes(
            Request(streamId: 99, payload, flags: 0),
            payload);

        return HostCheck.Result(write.Status == OSHostStatus.InvalidArgument
            && write.RecordIndex == uint.MaxValue
            && write.BytesWritten == 0
            && table.Count == 0);
    }

    [HostTest("pal.app_log facade rejects oversized write", HostLane.Fast, 3)]
    private static bool TestRejectsOversizedWrite()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 4);
        OSHostAppLogFacade facade = new(table);
        byte[] payload = [0x41, 0x42, 0x43, 0x44, 0x45];

        OSHostAppLogWriteResult write = facade.WriteLogBytes(
            Request(OSHostAppLogConstants.StreamStdout, payload, flags: 0),
            payload);
        OSHostAppLogReadResult read = table.TryCopyRecord(0, stackalloc byte[1]);

        return HostCheck.Result(write.Status == OSHostStatus.BufferTooSmall
            && write.RecordIndex == uint.MaxValue
            && write.BytesWritten == 0
            && table.Count == 0
            && read.Status == OSHostStatus.InvalidArgument);
    }

    [HostTest("pal.app_log facade reports exhaustion", HostLane.Fast, 4)]
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

        return HostCheck.Result(first.Succeeded
            && first.RecordIndex == 0
            && second.Status == OSHostStatus.OutOfMemory
            && second.RecordIndex == uint.MaxValue
            && second.BytesWritten == 0
            && table.Count == 1);
    }

    [HostTest("pal.app_log facade preserves machine marker flag", HostLane.Fast, 5)]
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

        return HostCheck.Result(write.Succeeded
            && read.Succeeded
            && read.StreamId == OSHostAppLogConstants.StreamStdout
            && read.Flags == OSHostAppLogConstants.FlagMachineReadable
            && Encoding.ASCII.GetString(destination) == "APP_EXIT code=0 status=OSHOST_STATUS_OK");
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
