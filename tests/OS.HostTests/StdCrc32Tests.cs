using System.Runtime.CompilerServices;
using OS.Std.Hash;

internal static class StdCrc32Tests
{

    [HostTest("std.crc32 empty input", HostLane.Fast, 0)]
    private static void TestEmptyInput()
    {
        ExpectValue("empty", 0x00000000u, Crc32.Compute(ReadOnlySpan<byte>.Empty));
    }

    [HostTest("std.crc32 byte updates", HostLane.Fast, 4)]
    private static void TestByteUpdates()
    {
        ExpectValue("empty byte stream", 0u, Crc32.Finish(Crc32.InitialState));
        uint state = Crc32.InitialState;
        foreach (byte value in "123456789"u8) state = Crc32.UpdateByte(state, value);
        ExpectValue("bytewise ascii", 0xCBF43926u, Crc32.Finish(state));
        state = Crc32.InitialState;
        for (int value = 0; value < 256; value++) state = Crc32.UpdateByte(state, (byte)value);
        ExpectValue("all byte values", 0x29058C73u, Crc32.Finish(state));
    }

    [HostTest("std.crc32 ascii check vector", HostLane.Fast, 1)]
    private static void TestAsciiCheckVector()
    {
        ExpectValue("123456789", 0xCBF43926u, Crc32.Compute("123456789"u8));
    }

    [HostTest("std.crc32 binary vector", HostLane.Fast, 2)]
    private static void TestBinaryVector()
    {
        ReadOnlySpan<byte> value = new byte[]
        {
            0x00, 0x01, 0x02, 0x03, 0x04,
            0x05, 0x06, 0x07, 0x08, 0x09
        };

        ExpectValue("range 0..9", 0x456CD746u, Crc32.Compute(value));
    }

    [HostTest("std.crc32 incremental update", HostLane.Fast, 3)]
    private static void TestIncrementalUpdate()
    {
        ReadOnlySpan<byte> first = new byte[] { 0xDE, 0xAD };
        ReadOnlySpan<byte> second = new byte[] { 0xBE, 0xEF };

        uint state = Crc32.Update(Crc32.InitialState, first);
        state = Crc32.Update(state, second);

        ExpectValue("split deadbeef", 0x7C9CA35Au, Crc32.Finish(state));
    }

    private static void ExpectValue(string scenario, uint expected, uint actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                scenario + " expected 0x" + expected.ToString("X8") + " but returned 0x" + actual.ToString("X8") + ".");
        }
    }
}
