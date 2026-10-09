using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalRandomFacadeTests
{

    [HostTest("pal.random facade fills caller buffer", HostLane.Fast, 0)]
    private static bool TestFillsCallerBuffer()
    {
        OSHostRandomFacade facade = new(NewTable(maxRequestBytes: 32));
        Span<byte> destination = stackalloc byte[12];

        OSHostRandomBytesResult result = facade.GetRandomBytes(Request(destination.Length), destination);

        return HostCheck.Result(result.Succeeded
            && result.BytesWritten == 12
            && result.RequestSequence == 0
            && !AllEqual(destination, 0));
    }

    [HostTest("pal.random facade rejects malformed request", HostLane.Fast, 1)]
    private static bool TestRejectsMalformedRequest()
    {
        OSHostRandomFacade facade = new(NewTable(maxRequestBytes: 16));
        Span<byte> destination = stackalloc byte[4];
        destination.Fill(0xDD);

        OSHostRandomBytesResult zeroAddress = facade.GetRandomBytes(
            new OSHostRandomBytesRequest(
                new OSHostByteSpan(address: 0, length: (ulong)destination.Length),
                OSHostRandomConstants.FlagNone,
                reserved: 0),
            destination);
        OSHostRandomBytesResult lengthMismatch = facade.GetRandomBytes(Request(3), destination);
        OSHostRandomBytesResult reserved = facade.GetRandomBytes(
            new OSHostRandomBytesRequest(
                new OSHostByteSpan(address: 0x5000, length: (ulong)destination.Length),
                OSHostRandomConstants.FlagNone,
                reserved: 1),
            destination);

        return HostCheck.Result(zeroAddress.Status == OSHostStatus.InvalidArgument
            && lengthMismatch.Status == OSHostStatus.InvalidArgument
            && reserved.Status == OSHostStatus.InvalidArgument
            && destination[0] == 0xDD);
    }

    [HostTest("pal.random facade reports unavailable table", HostLane.Fast, 2)]
    private static bool TestReportsUnavailableTable()
    {
        OSHostRandomFacade facade = new(table: null);
        Span<byte> destination = stackalloc byte[4];
        destination.Fill(0xEE);

        OSHostRandomBytesResult result = facade.GetRandomBytes(Request(destination.Length), destination);

        return HostCheck.Result(result.Status == OSHostStatus.InvalidArgument
            && result.BytesWritten == 0
            && destination[0] == 0xEE);
    }

    [HostTest("pal.random facade propagates entropy status", HostLane.Fast, 3)]
    private static bool TestPropagatesEntropyStatus()
    {
        OSHostRandomFacade noEntropy = new(new OSHostRandomTable(ReadOnlySpan<byte>.Empty, maxRequestBytes: 16));
        OSHostRandomFacade small = new(NewTable(maxRequestBytes: 2));
        Span<byte> destination = stackalloc byte[4];

        OSHostRandomBytesResult unavailable = noEntropy.GetRandomBytes(Request(destination.Length), destination);
        OSHostRandomBytesResult tooLarge = small.GetRandomBytes(Request(destination.Length), destination);

        return HostCheck.Result(unavailable.Status == OSHostStatus.TryAgain
            && unavailable.BytesWritten == 0
            && tooLarge.Status == OSHostStatus.BufferTooSmall
            && tooLarge.BytesWritten == 0);
    }

    private static OSHostRandomTable NewTable(int maxRequestBytes)
    {
        return new OSHostRandomTable([0x4F, 0x53, 0x48, 0x6F, 0x73, 0x74], maxRequestBytes);
    }

    private static OSHostRandomBytesRequest Request(int length)
    {
        return new OSHostRandomBytesRequest(
            new OSHostByteSpan(address: 0x5000, length: (ulong)length),
            OSHostRandomConstants.FlagNone,
            reserved: 0);
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
