using System.Runtime.CompilerServices;
using OS.Std.Net;

internal static class StdInternetChecksumTests
{

    [HostTest("std.internet.checksum empty input", HostLane.Fast, 0)]
    private static void TestEmptyInput()
    {
        ExpectValue("empty input", InternetChecksum.EmptyChecksum, InternetChecksum.Compute(ReadOnlySpan<byte>.Empty));
    }

    [HostTest("std.internet.checksum rfc1071 vector", HostLane.Fast, 1)]
    private static void TestRfc1071Vector()
    {
        ReadOnlySpan<byte> source = new byte[]
        {
            0x00, 0x01, 0xF2, 0x03, 0xF4, 0xF5, 0xF6, 0xF7
        };

        ExpectValue("RFC 1071 sample", 0x220D, InternetChecksum.Compute(source));
    }

    [HostTest("std.internet.checksum odd byte pads high", HostLane.Fast, 2)]
    private static void TestOddBytePadsHigh()
    {
        ReadOnlySpan<byte> source = new byte[] { 0x01, 0x02, 0x03 };

        ExpectValue("odd input", 0xFBFD, InternetChecksum.Compute(source));
    }

    [HostTest("std.internet.checksum segmented odd boundary", HostLane.Fast, 3)]
    private static void TestSegmentedOddBoundary()
    {
        ReadOnlySpan<byte> first = new byte[] { 0x01 };
        ReadOnlySpan<byte> second = new byte[] { 0x02, 0x03 };

        ExpectValue("segmented odd boundary", 0xFBFD, InternetChecksum.Compute(first, second));
    }

    [HostTest("std.internet.checksum four segment vector", HostLane.Fast, 4)]
    private static void TestFourSegmentVector()
    {
        ReadOnlySpan<byte> first = new byte[] { 0x00 };
        ReadOnlySpan<byte> second = new byte[] { 0x01, 0xF2 };
        ReadOnlySpan<byte> third = new byte[] { 0x03 };
        ReadOnlySpan<byte> fourth = new byte[] { 0xF4, 0xF5, 0xF6, 0xF7 };

        ExpectValue("four segment RFC 1071 sample", 0x220D, InternetChecksum.Compute(first, second, third, fourth));
    }

    [HostTest("std.internet.checksum accumulator preserves pending byte", HostLane.Fast, 5)]
    private static void TestAccumulatorPreservesPendingByte()
    {
        InternetChecksumAccumulator accumulator = InternetChecksum.CreateAccumulator();

        accumulator.Update(new byte[] { 0xDE });
        if (!accumulator.HasPendingHighByte || accumulator.PendingHighByte != 0xDE)
        {
            throw new InvalidOperationException("Expected accumulator to keep 0xDE as a pending high byte.");
        }

        accumulator.Update(new byte[] { 0xAD, 0xBE, 0xEF });

        if (accumulator.HasPendingHighByte)
        {
            throw new InvalidOperationException("Expected accumulator to consume the pending high byte.");
        }

        ExpectValue("split deadbeef", 0x6262, accumulator.Finish());
    }

    [HostTest("std.internet.checksum validates packet checksum", HostLane.Fast, 6)]
    private static void TestValidatesPacketChecksum()
    {
        byte[] packet =
        {
            0x08, 0x00, 0x00, 0x00,
            0x12, 0x34, 0x00, 0x01,
            0x61
        };

        ushort checksum = InternetChecksum.Compute(packet);
        packet[2] = (byte)(checksum >> 8);
        packet[3] = (byte)checksum;

        ExpectValue("packet validation checksum", InternetChecksum.ValidChecksumResult, InternetChecksum.Compute(packet));

        if (!InternetChecksum.IsValid(packet))
        {
            throw new InvalidOperationException("Expected packet with inserted checksum to validate.");
        }
    }

    [HostTest("std.internet.checksum reset clears state", HostLane.Fast, 7)]
    private static void TestResetClearsState()
    {
        InternetChecksumAccumulator accumulator = InternetChecksum.CreateAccumulator();
        accumulator.Update(new byte[] { 0xDE });
        accumulator.Reset();

        if (accumulator.HasPendingHighByte || accumulator.Sum != 0)
        {
            throw new InvalidOperationException("Expected reset to clear pending and sum state.");
        }

        ExpectValue("reset empty finish", InternetChecksum.EmptyChecksum, accumulator.Finish());
    }

    private static void ExpectValue(string scenario, ushort expected, ushort actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                scenario + " expected 0x" + expected.ToString("X4") + " but returned 0x" + actual.ToString("X4") + ".");
        }
    }
}
