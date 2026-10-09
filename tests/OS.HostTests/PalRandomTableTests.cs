using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalRandomTableTests
{

    [HostTest("pal.random table fills caller buffer", HostLane.Fast, 0)]
    private static bool TestFillsCallerBuffer()
    {
        OSHostRandomTable table = NewTable(maxRequestBytes: 32);
        Span<byte> destination = stackalloc byte[16];

        OSHostRandomBytesResult result = table.FillRandomBytes(destination, OSHostRandomConstants.FlagNone);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == OSHostStatus.Ok
            && result.BytesWritten == 16
            && result.RequestSequence == 0
            && table.RequestCount == 1
            && !AllEqual(destination, 0));
    }

    [HostTest("pal.random table advances deterministic stream", HostLane.Fast, 1)]
    private static bool TestAdvancesDeterministicStream()
    {
        OSHostRandomTable first = NewTable(maxRequestBytes: 32);
        OSHostRandomTable second = NewTable(maxRequestBytes: 32);
        Span<byte> firstA = stackalloc byte[8];
        Span<byte> firstB = stackalloc byte[8];
        Span<byte> secondA = stackalloc byte[8];
        Span<byte> secondB = stackalloc byte[8];

        OSHostRandomBytesResult a = first.FillRandomBytes(firstA, OSHostRandomConstants.FlagNone);
        OSHostRandomBytesResult b = first.FillRandomBytes(firstB, OSHostRandomConstants.FlagNone);
        OSHostRandomBytesResult c = second.FillRandomBytes(secondA, OSHostRandomConstants.FlagNone);
        OSHostRandomBytesResult d = second.FillRandomBytes(secondB, OSHostRandomConstants.FlagNone);

        return HostCheck.Result(a.Succeeded
            && b.Succeeded
            && c.Succeeded
            && d.Succeeded
            && b.RequestSequence == 1
            && firstA.SequenceEqual(secondA)
            && firstB.SequenceEqual(secondB)
            && !firstA.SequenceEqual(firstB));
    }

    [HostTest("pal.random table rejects invalid requests", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidRequests()
    {
        OSHostRandomTable table = NewTable(maxRequestBytes: 4);
        Span<byte> empty = stackalloc byte[0];
        Span<byte> oversized = stackalloc byte[5];
        Span<byte> valid = stackalloc byte[4];
        valid.Fill(0xCC);

        OSHostRandomBytesResult zero = table.FillRandomBytes(empty, OSHostRandomConstants.FlagNone);
        OSHostRandomBytesResult flags = table.FillRandomBytes(valid, flags: 1);
        OSHostRandomBytesResult large = table.FillRandomBytes(oversized, OSHostRandomConstants.FlagNone);

        return HostCheck.Result(zero.Status == OSHostStatus.InvalidArgument
            && zero.BytesWritten == 0
            && flags.Status == OSHostStatus.InvalidArgument
            && valid[0] == 0xCC
            && large.Status == OSHostStatus.BufferTooSmall
            && large.BytesWritten == 0
            && table.RequestCount == 0);
    }

    [HostTest("pal.random table reports unavailable entropy", HostLane.Fast, 3)]
    private static bool TestReportsUnavailableEntropy()
    {
        OSHostRandomTable table = new(ReadOnlySpan<byte>.Empty, maxRequestBytes: 16);
        Span<byte> destination = stackalloc byte[4];
        destination.Fill(0xAA);

        OSHostRandomBytesResult result = table.FillRandomBytes(destination, OSHostRandomConstants.FlagNone);

        return HostCheck.Result(result.Status == OSHostStatus.TryAgain
            && result.BytesWritten == 0
            && result.RequestSequence == uint.MaxValue
            && destination[0] == 0xAA
            && table.RequestCount == 0);
    }

    [HostTest("pal.random table default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostRandomBytesResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.Ok
            && result.BytesWritten == 0
            && result.RequestSequence == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostRandomBytesResult>());
    }

    private static OSHostRandomTable NewTable(int maxRequestBytes)
    {
        return new OSHostRandomTable([0x4F, 0x53, 0x48, 0x6F, 0x73, 0x74], maxRequestBytes);
    }

    private static bool AllEqual(ReadOnlySpan<byte> bytes, byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != value)
            {
                return false;
            }
        }

        return true;
    }
}
