using System.Runtime.CompilerServices;
using System.Text;
using OS.Std;
using OS.Std.Text;

internal static class StdAsciiDecimalFormatterTests
{

    [HostTest("std.ascii.decimal.formatter formats uint32 values", HostLane.Fast, 0)]
    private static void TestFormatsUInt32Values()
    {
        ExpectBool("status is partial", true, AsciiDecimalFormatter.Status == CompatibilityStatus.Partial);

        byte[] zero = Filled(4, 0xCC);
        AsciiDecimalFormatResult result = AsciiDecimalFormatter.FormatUInt32(zero, 0);
        ExpectSuccess("uint32 zero", result, 1, zero.Length);
        ExpectText("uint32 zero text", "0", zero, result.BytesWritten);
        ExpectByte("uint32 zero tail", 0xCC, zero[result.BytesWritten]);

        byte[] exact = Filled(10, 0xCC);
        result = AsciiDecimalFormatter.FormatUInt32(exact, uint.MaxValue);
        ExpectSuccess("uint32 max", result, 10, exact.Length);
        ExpectText("uint32 max text", "4294967295", exact, result.BytesWritten);

        byte[] larger = Filled(12, 0xDD);
        result = AsciiDecimalFormatter.FormatUInt32(larger, 1234567890U);
        ExpectSuccess("uint32 larger destination", result, 10, larger.Length);
        ExpectText("uint32 larger text", "1234567890", larger, result.BytesWritten);
        ExpectByte("uint32 larger tail", 0xDD, larger[result.BytesWritten]);
    }

    [HostTest("std.ascii.decimal.formatter formats uint64 values", HostLane.Fast, 1)]
    private static void TestFormatsUInt64Values()
    {
        byte[] zero = Filled(4, 0xCC);
        AsciiDecimalFormatResult result = AsciiDecimalFormatter.FormatUInt64(zero, 0);
        ExpectSuccess("uint64 zero", result, 1, zero.Length);
        ExpectText("uint64 zero text", "0", zero, result.BytesWritten);
        ExpectByte("uint64 zero tail", 0xCC, zero[result.BytesWritten]);

        byte[] exact = Filled(20, 0xCC);
        result = AsciiDecimalFormatter.FormatUInt64(exact, ulong.MaxValue);
        ExpectSuccess("uint64 max", result, 20, exact.Length);
        ExpectText("uint64 max text", "18446744073709551615", exact, result.BytesWritten);

        byte[] larger = Filled(24, 0xDD);
        result = AsciiDecimalFormatter.FormatUInt64(larger, 9876543210987654321UL);
        ExpectSuccess("uint64 larger destination", result, 19, larger.Length);
        ExpectText("uint64 larger text", "9876543210987654321", larger, result.BytesWritten);
        ExpectByte("uint64 larger tail", 0xDD, larger[result.BytesWritten]);
    }

    [HostTest("std.ascii.decimal.formatter formats int32 values", HostLane.Fast, 2)]
    private static void TestFormatsInt32Values()
    {
        byte[] zero = Filled(4, 0xCC);
        AsciiDecimalFormatResult result = AsciiDecimalFormatter.FormatInt32(zero, 0);
        ExpectSuccess("int32 zero", result, 1, zero.Length);
        ExpectText("int32 zero text", "0", zero, result.BytesWritten);
        ExpectByte("int32 zero tail", 0xCC, zero[result.BytesWritten]);

        byte[] min = Filled(12, 0xCC);
        result = AsciiDecimalFormatter.FormatInt32(min, int.MinValue);
        ExpectSuccess("int32 min", result, 11, min.Length);
        ExpectText("int32 min text", "-2147483648", min, result.BytesWritten);
        ExpectByte("int32 min tail", 0xCC, min[result.BytesWritten]);

        byte[] max = Filled(10, 0xCC);
        result = AsciiDecimalFormatter.FormatInt32(max, int.MaxValue);
        ExpectSuccess("int32 max", result, 10, max.Length);
        ExpectText("int32 max text", "2147483647", max, result.BytesWritten);

        byte[] negative = Filled(8, 0xDD);
        result = AsciiDecimalFormatter.FormatInt32(negative, -1234567);
        ExpectSuccess("int32 negative", result, 8, negative.Length);
        ExpectText("int32 negative text", "-1234567", negative, result.BytesWritten);
    }

    [HostTest("std.ascii.decimal.formatter formats int64 values", HostLane.Fast, 3)]
    private static void TestFormatsInt64Values()
    {
        byte[] zero = Filled(4, 0xCC);
        AsciiDecimalFormatResult result = AsciiDecimalFormatter.FormatInt64(zero, 0);
        ExpectSuccess("int64 zero", result, 1, zero.Length);
        ExpectText("int64 zero text", "0", zero, result.BytesWritten);
        ExpectByte("int64 zero tail", 0xCC, zero[result.BytesWritten]);

        byte[] min = Filled(21, 0xCC);
        result = AsciiDecimalFormatter.FormatInt64(min, long.MinValue);
        ExpectSuccess("int64 min", result, 20, min.Length);
        ExpectText("int64 min text", "-9223372036854775808", min, result.BytesWritten);
        ExpectByte("int64 min tail", 0xCC, min[result.BytesWritten]);

        byte[] max = Filled(19, 0xCC);
        result = AsciiDecimalFormatter.FormatInt64(max, long.MaxValue);
        ExpectSuccess("int64 max", result, 19, max.Length);
        ExpectText("int64 max text", "9223372036854775807", max, result.BytesWritten);

        byte[] negative = Filled(20, 0xDD);
        result = AsciiDecimalFormatter.FormatInt64(negative, -987654321098765432L);
        ExpectSuccess("int64 negative", result, 19, negative.Length);
        ExpectText("int64 negative text", "-987654321098765432", negative, result.BytesWritten);
        ExpectByte("int64 negative tail", 0xDD, negative[result.BytesWritten]);
    }

    [HostTest("std.ascii.decimal.formatter reports capacity", HostLane.Fast, 4)]
    private static void TestReportsCapacity()
    {
        byte[] shortUInt32 = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        AsciiDecimalFormatResult result = AsciiDecimalFormatter.FormatUInt32(shortUInt32, uint.MaxValue);
        ExpectStatus("short uint32", AsciiDecimalFormatStatus.DestinationTooSmall, result);
        ExpectInt("short uint32 destination", shortUInt32.Length, result.DestinationLength);
        ExpectInt("short uint32 required", 10, result.RequiredBytes);
        ExpectInt("short uint32 written", 0, result.BytesWritten);
        ExpectBytes("short uint32 unchanged", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, shortUInt32);

        byte[] shortUInt64 = new byte[] { 1, 2, 3 };
        result = AsciiDecimalFormatter.FormatUInt64(shortUInt64, ulong.MaxValue);
        ExpectStatus("short uint64", AsciiDecimalFormatStatus.DestinationTooSmall, result);
        ExpectInt("short uint64 destination", shortUInt64.Length, result.DestinationLength);
        ExpectInt("short uint64 required", 20, result.RequiredBytes);
        ExpectInt("short uint64 written", 0, result.BytesWritten);
        ExpectBytes("short uint64 unchanged", new byte[] { 1, 2, 3 }, shortUInt64);

        result = AsciiDecimalFormatter.FormatUInt64(Span<byte>.Empty, 0);
        ExpectStatus("empty destination zero", AsciiDecimalFormatStatus.DestinationTooSmall, result);
        ExpectInt("empty destination required", 1, result.RequiredBytes);
    }

    [HostTest("std.ascii.decimal.formatter reports signed capacity", HostLane.Fast, 5)]
    private static void TestReportsSignedCapacity()
    {
        byte[] shortInt32 = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        AsciiDecimalFormatResult result = AsciiDecimalFormatter.FormatInt32(shortInt32, int.MinValue);
        ExpectStatus("short int32", AsciiDecimalFormatStatus.DestinationTooSmall, result);
        ExpectInt("short int32 destination", shortInt32.Length, result.DestinationLength);
        ExpectInt("short int32 required", 11, result.RequiredBytes);
        ExpectInt("short int32 written", 0, result.BytesWritten);
        ExpectBytes("short int32 unchanged", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, shortInt32);

        byte[] shortInt64 = new byte[] { 1, 2, 3 };
        result = AsciiDecimalFormatter.FormatInt64(shortInt64, long.MinValue);
        ExpectStatus("short int64", AsciiDecimalFormatStatus.DestinationTooSmall, result);
        ExpectInt("short int64 destination", shortInt64.Length, result.DestinationLength);
        ExpectInt("short int64 required", 20, result.RequiredBytes);
        ExpectInt("short int64 written", 0, result.BytesWritten);
        ExpectBytes("short int64 unchanged", new byte[] { 1, 2, 3 }, shortInt64);

        result = AsciiDecimalFormatter.FormatInt64(Span<byte>.Empty, -1);
        ExpectStatus("empty destination negative", AsciiDecimalFormatStatus.DestinationTooSmall, result);
        ExpectInt("empty destination negative required", 2, result.RequiredBytes);
    }

    [HostTest("std.ascii.decimal.formatter reports required byte counts", HostLane.Fast, 6)]
    private static void TestReportsRequiredByteCounts()
    {
        ExpectInt("uint32 zero", 1, AsciiDecimalFormatter.CountUInt32Digits(0));
        ExpectInt("uint32 nine", 1, AsciiDecimalFormatter.CountUInt32Digits(9));
        ExpectInt("uint32 ten", 2, AsciiDecimalFormatter.CountUInt32Digits(10));
        ExpectInt("uint32 max", 10, AsciiDecimalFormatter.CountUInt32Digits(uint.MaxValue));

        ExpectInt("uint64 zero", 1, AsciiDecimalFormatter.CountUInt64Digits(0));
        ExpectInt("uint64 boundary", 19, AsciiDecimalFormatter.CountUInt64Digits(9876543210987654321UL));
        ExpectInt("uint64 max", 20, AsciiDecimalFormatter.CountUInt64Digits(ulong.MaxValue));

        ExpectInt("int32 zero", 1, AsciiDecimalFormatter.CountInt32Digits(0));
        ExpectInt("int32 negative one", 2, AsciiDecimalFormatter.CountInt32Digits(-1));
        ExpectInt("int32 min", 11, AsciiDecimalFormatter.CountInt32Digits(int.MinValue));
        ExpectInt("int32 max", 10, AsciiDecimalFormatter.CountInt32Digits(int.MaxValue));

        ExpectInt("int64 zero", 1, AsciiDecimalFormatter.CountInt64Digits(0));
        ExpectInt("int64 negative one", 2, AsciiDecimalFormatter.CountInt64Digits(-1));
        ExpectInt("int64 min", 20, AsciiDecimalFormatter.CountInt64Digits(long.MinValue));
        ExpectInt("int64 max", 19, AsciiDecimalFormatter.CountInt64Digits(long.MaxValue));
    }

    [HostTest("std.ascii.decimal.formatter default result is not success", HostLane.Fast, 7)]
    private static void TestDefaultResultContract()
    {
        AsciiDecimalFormatResult result = default;

        ExpectBool("default status is zero", true, result.Status == AsciiDecimalFormatStatus.Success);
        ExpectBool("default initialized", false, result.IsInitialized);
        ExpectBool("default is not success", false, result.Succeeded);
        ExpectBool(
            "result contains no managed references",
            false,
            RuntimeHelpers.IsReferenceOrContainsReferences<AsciiDecimalFormatResult>());
    }

    private static byte[] Filled(int length, byte value)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static void ExpectSuccess(
        string scenario,
        AsciiDecimalFormatResult result,
        int bytesWritten,
        int destinationLength)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(scenario + " failed with " + result.Status + ".");
        }

        ExpectInt(scenario + " destination", destinationLength, result.DestinationLength);
        ExpectInt(scenario + " bytes", bytesWritten, result.BytesWritten);
        ExpectInt(scenario + " required", bytesWritten, result.RequiredBytes);
    }

    private static void ExpectStatus(
        string scenario,
        AsciiDecimalFormatStatus expected,
        AsciiDecimalFormatResult result)
    {
        if (result.Status != expected || result.Succeeded)
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned " + result.Status + ".");
        }

        ExpectBool(scenario + " initialized", true, result.IsInitialized);
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
