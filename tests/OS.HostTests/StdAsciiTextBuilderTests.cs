using System.Runtime.CompilerServices;
using System.Text;
using OS.Std;
using OS.Std.Text;

internal static class StdAsciiTextBuilderTests
{

    [HostTest("std.ascii.builder appends bytes and chars", HostLane.Fast, 0)]
    private static void TestAppendsBytesAndChars()
    {
        byte[] destination = Filled(64, 0xCC);
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, 0, "HTTP/1.1"u8);
        ExpectSuccess("append protocol", result, 8, 8);

        result = AsciiTextBuilder.AppendByte(destination, result.NextOffset, (byte)' ');
        ExpectSuccess("append space", result, 1, 9);

        result = AsciiTextBuilder.AppendChars(destination, result.NextOffset, "200 OK");
        ExpectSuccess("append chars", result, 6, 15);

        result = AsciiTextBuilder.AppendBytes(destination, result.NextOffset, "\r\n"u8);
        ExpectSuccess("append crlf", result, 2, 17);
        ExpectText("combined text", "HTTP/1.1 200 OK\r\n", destination, result.NextOffset);
        ExpectByte("tail unchanged", 0xCC, destination[result.NextOffset]);

        byte[] overlap = Encoding.ASCII.GetBytes("ABCDE");
        result = AsciiTextBuilder.AppendBytes(overlap, 1, overlap.AsSpan(0, 4));
        ExpectSuccess("overlap append", result, 4, 5);
        ExpectText("overlap text", "AABCD", overlap, overlap.Length);
    }

    [HostTest("std.ascii.builder appends decimal and hex", HostLane.Fast, 1)]
    private static void TestAppendsDecimalAndHex()
    {
        byte[] destination = Filled(96, 0xEE);
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendChars(destination, 0, "len=");
        ExpectSuccess("append label", result, 4, 4);

        result = AsciiTextBuilder.AppendUInt64Decimal(destination, result.NextOffset, 18446744073709551615UL);
        ExpectSuccess("append ulong max", result, 20, 24);

        result = AsciiTextBuilder.AppendByte(destination, result.NextOffset, (byte)' ');
        ExpectSuccess("append separator", result, 1, 25);

        result = AsciiTextBuilder.AppendInt64Decimal(destination, result.NextOffset, long.MinValue);
        ExpectSuccess("append long min", result, 20, 45);

        result = AsciiTextBuilder.AppendByte(destination, result.NextOffset, (byte)' ');
        ExpectSuccess("append second separator", result, 1, 46);

        result = AsciiTextBuilder.AppendUInt64Hex(destination, result.NextOffset, 0x2AUL, minimumDigits: 4, includePrefix: true);
        ExpectSuccess("append padded hex", result, 6, 52);

        ExpectText(
            "numeric text",
            "len=18446744073709551615 -9223372036854775808 0x002A",
            destination,
            result.NextOffset);
    }

    [HostTest("std.ascii.builder rejects capacity and input", HostLane.Fast, 2)]
    private static void TestRejectsCapacityAndInput()
    {
        byte[] shortDestination = new byte[] { 1, 2, 3 };
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(shortDestination, 1, "abcd"u8);
        ExpectStatus("short destination", AsciiTextBuilderStatus.DestinationTooSmall, result);
        ExpectInt("short required bytes", 5, result.RequiredBytes);
        ExpectBytes("short destination unchanged", new byte[] { 1, 2, 3 }, shortDestination);

        byte[] nonAsciiDestination = new byte[] { 9, 9, 9 };
        result = AsciiTextBuilder.AppendChars(nonAsciiDestination, 0, new char[] { 'O', (char)0x80 });
        ExpectStatus("non ascii chars", AsciiTextBuilderStatus.NonAsciiInput, result);
        ExpectInt("invalid char index", 1, result.InvalidInputIndex);
        ExpectBytes("non ascii unchanged", new byte[] { 9, 9, 9 }, nonAsciiDestination);

        result = AsciiTextBuilder.AppendByte(nonAsciiDestination, 0, 0);
        ExpectStatus("nul byte rejected", AsciiTextBuilderStatus.NonAsciiInput, result);

        result = AsciiTextBuilder.AppendBytes(nonAsciiDestination, 4, "x"u8);
        ExpectStatus("invalid offset", AsciiTextBuilderStatus.InvalidOffset, result);

        result = AsciiTextBuilder.AppendUInt64Hex(nonAsciiDestination, 0, 1, minimumDigits: 17, includePrefix: false);
        ExpectStatus("invalid minimum digits", AsciiTextBuilderStatus.InvalidMinimumDigits, result);
        ExpectBytes("invalid minimum unchanged", new byte[] { 9, 9, 9 }, nonAsciiDestination);
    }

    [HostTest("std.ascii.builder default result is not success", HostLane.Fast, 3)]
    private static void TestDefaultResultIsNotSuccess()
    {
        AsciiTextBuilderResult result = default;

        ExpectBool("status is partial", true, AsciiTextBuilder.Status == CompatibilityStatus.Partial);
        ExpectBool("default not initialized", false, result.IsInitialized);
        ExpectBool("default not success", false, result.Succeeded);
        ExpectBool(
            "result contains no managed references",
            false,
            RuntimeHelpers.IsReferenceOrContainsReferences<AsciiTextBuilderResult>());
    }

    private static byte[] Filled(int length, byte value)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static void ExpectSuccess(string scenario, AsciiTextBuilderResult result, int bytesWritten, int nextOffset)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(scenario + " failed with " + result.Status + ".");
        }

        ExpectInt(scenario + " bytes", bytesWritten, result.BytesWritten);
        ExpectInt(scenario + " next offset", nextOffset, result.NextOffset);
        ExpectInt(scenario + " required", nextOffset, result.RequiredBytes);
    }

    private static void ExpectStatus(
        string scenario,
        AsciiTextBuilderStatus expected,
        AsciiTextBuilderResult result)
    {
        if (result.Status != expected || result.Succeeded)
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned " + result.Status + ".");
        }
    }

    private static void ExpectText(string scenario, string expected, byte[] actual, int length)
    {
        string actualText = Encoding.ASCII.GetString(actual, 0, length);
        if (!StringComparer.Ordinal.Equals(expected, actualText))
        {
            throw new InvalidOperationException(
                scenario + " expected '" + expected + "' but returned '" + actualText + "'.");
        }
    }

    private static void ExpectBytes(string scenario, byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException(scenario + " bytes changed unexpectedly.");
        }
    }

    private static void ExpectByte(string scenario, byte expected, byte actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectBool(string scenario, bool expected, bool actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectInt(string scenario, int expected, int actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }
}
