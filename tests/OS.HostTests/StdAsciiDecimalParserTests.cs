using System.Runtime.CompilerServices;
using OS.Std;
using OS.Std.Text;

internal static class StdAsciiDecimalParserTests
{

    [HostTest("std.ascii.decimal parses uint32 values", HostLane.Fast, 0)]
    private static void TestParseUInt32Values()
    {
        ExpectBool("status is partial", true, AsciiDecimalParser.Status == CompatibilityStatus.Partial);
        ExpectUInt32Result(
            "zero",
            AsciiDecimalParseStatus.Success,
            0,
            1,
            -1,
            AsciiDecimalParser.ParseUInt32("0"u8));
        ExpectUInt32Result(
            "leading zeros",
            AsciiDecimalParseStatus.Success,
            123,
            8,
            -1,
            AsciiDecimalParser.ParseUInt32("00000123"u8));
        ExpectUInt32Result(
            "uint max",
            AsciiDecimalParseStatus.Success,
            uint.MaxValue,
            10,
            -1,
            AsciiDecimalParser.ParseUInt32("4294967295"u8));

        ExpectBool("try parse success", true, AsciiDecimalParser.TryParseUInt32("42"u8, out uint parsed));
        ExpectUInt32("try parse value", 42, parsed);
    }

    [HostTest("std.ascii.decimal parses uint64 values", HostLane.Fast, 1)]
    private static void TestParseUInt64Values()
    {
        ExpectUInt64Result(
            "zero",
            AsciiDecimalParseStatus.Success,
            0,
            1,
            -1,
            AsciiDecimalParser.ParseUInt64("0"u8));
        ExpectUInt64Result(
            "leading zeros",
            AsciiDecimalParseStatus.Success,
            9876543210UL,
            13,
            -1,
            AsciiDecimalParser.ParseUInt64("0009876543210"u8));
        ExpectUInt64Result(
            "ulong max",
            AsciiDecimalParseStatus.Success,
            ulong.MaxValue,
            20,
            -1,
            AsciiDecimalParser.ParseUInt64("18446744073709551615"u8));

        ExpectBool("try parse success", true, AsciiDecimalParser.TryParseUInt64("9001"u8, out ulong parsed));
        ExpectUInt64("try parse value", 9001, parsed);
    }

    [HostTest("std.ascii.decimal parses int32 values", HostLane.Fast, 2)]
    private static void TestParseInt32Values()
    {
        ExpectInt32Result(
            "zero",
            AsciiDecimalParseStatus.Success,
            0,
            1,
            -1,
            AsciiDecimalParser.ParseInt32("0"u8));
        ExpectInt32Result(
            "negative zero",
            AsciiDecimalParseStatus.Success,
            0,
            2,
            -1,
            AsciiDecimalParser.ParseInt32("-0"u8));
        ExpectInt32Result(
            "positive value",
            AsciiDecimalParseStatus.Success,
            1234567,
            7,
            -1,
            AsciiDecimalParser.ParseInt32("1234567"u8));
        ExpectInt32Result(
            "negative value",
            AsciiDecimalParseStatus.Success,
            -1234567,
            8,
            -1,
            AsciiDecimalParser.ParseInt32("-1234567"u8));
        ExpectInt32Result(
            "int min",
            AsciiDecimalParseStatus.Success,
            int.MinValue,
            11,
            -1,
            AsciiDecimalParser.ParseInt32("-2147483648"u8));
        ExpectInt32Result(
            "int max",
            AsciiDecimalParseStatus.Success,
            int.MaxValue,
            10,
            -1,
            AsciiDecimalParser.ParseInt32("2147483647"u8));

        ExpectBool("try parse success", true, AsciiDecimalParser.TryParseInt32("-42"u8, out int parsed));
        ExpectInt32("try parse value", -42, parsed);
    }

    [HostTest("std.ascii.decimal parses int64 values", HostLane.Fast, 3)]
    private static void TestParseInt64Values()
    {
        ExpectInt64Result(
            "zero",
            AsciiDecimalParseStatus.Success,
            0,
            1,
            -1,
            AsciiDecimalParser.ParseInt64("0"u8));
        ExpectInt64Result(
            "negative zero",
            AsciiDecimalParseStatus.Success,
            0,
            2,
            -1,
            AsciiDecimalParser.ParseInt64("-0"u8));
        ExpectInt64Result(
            "positive value",
            AsciiDecimalParseStatus.Success,
            987654321098765432L,
            18,
            -1,
            AsciiDecimalParser.ParseInt64("987654321098765432"u8));
        ExpectInt64Result(
            "negative value",
            AsciiDecimalParseStatus.Success,
            -987654321098765432L,
            19,
            -1,
            AsciiDecimalParser.ParseInt64("-987654321098765432"u8));
        ExpectInt64Result(
            "long min",
            AsciiDecimalParseStatus.Success,
            long.MinValue,
            20,
            -1,
            AsciiDecimalParser.ParseInt64("-9223372036854775808"u8));
        ExpectInt64Result(
            "long max",
            AsciiDecimalParseStatus.Success,
            long.MaxValue,
            19,
            -1,
            AsciiDecimalParser.ParseInt64("9223372036854775807"u8));

        ExpectBool("try parse success", true, AsciiDecimalParser.TryParseInt64("-9001"u8, out long parsed));
        ExpectInt64("try parse value", -9001, parsed);
    }

    [HostTest("std.ascii.decimal rejects invalid input", HostLane.Fast, 4)]
    private static void TestRejectsInvalidInput()
    {
        ExpectUInt32Result(
            "uint32 empty",
            AsciiDecimalParseStatus.Empty,
            0,
            0,
            -1,
            AsciiDecimalParser.ParseUInt32(ReadOnlySpan<byte>.Empty));
        ExpectUInt32Result(
            "uint32 plus rejected",
            AsciiDecimalParseStatus.InvalidDigit,
            0,
            0,
            0,
            AsciiDecimalParser.ParseUInt32("+1"u8));
        ExpectUInt32Result(
            "uint32 minus rejected",
            AsciiDecimalParseStatus.InvalidDigit,
            0,
            0,
            0,
            AsciiDecimalParser.ParseUInt32("-1"u8));
        ExpectUInt32Result(
            "uint32 interior invalid digit",
            AsciiDecimalParseStatus.InvalidDigit,
            0,
            2,
            2,
            AsciiDecimalParser.ParseUInt32("12a34"u8));
        ExpectUInt32Result(
            "uint32 overflow",
            AsciiDecimalParseStatus.Overflow,
            0,
            9,
            9,
            AsciiDecimalParser.ParseUInt32("4294967296"u8));
        ExpectUInt64Result(
            "uint64 minus rejected",
            AsciiDecimalParseStatus.InvalidDigit,
            0,
            0,
            0,
            AsciiDecimalParser.ParseUInt64("-1"u8));
        ExpectUInt64Result(
            "uint64 whitespace rejected",
            AsciiDecimalParseStatus.InvalidDigit,
            0,
            0,
            0,
            AsciiDecimalParser.ParseUInt64(" 1"u8));
        ExpectUInt64Result(
            "uint64 overflow",
            AsciiDecimalParseStatus.Overflow,
            0,
            19,
            19,
            AsciiDecimalParser.ParseUInt64("18446744073709551616"u8));

        ExpectBool("try parse invalid returns false", false, AsciiDecimalParser.TryParseUInt32("1x"u8, out uint parsed));
        ExpectUInt32("try parse invalid clears value", 0, parsed);
    }

    [HostTest("std.ascii.decimal rejects signed invalid input", HostLane.Fast, 5)]
    private static void TestRejectsSignedInvalidInput()
    {
        ExpectInt32Result(
            "int32 empty",
            AsciiDecimalParseStatus.Empty,
            0,
            0,
            -1,
            AsciiDecimalParser.ParseInt32(ReadOnlySpan<byte>.Empty));
        ExpectInt32Result(
            "int32 sign only",
            AsciiDecimalParseStatus.SignOnly,
            0,
            1,
            -1,
            AsciiDecimalParser.ParseInt32("-"u8));
        ExpectInt32Result(
            "int32 plus rejected",
            AsciiDecimalParseStatus.InvalidDigit,
            0,
            0,
            0,
            AsciiDecimalParser.ParseInt32("+1"u8));
        ExpectInt32Result(
            "int32 negative invalid digit",
            AsciiDecimalParseStatus.InvalidDigit,
            0,
            3,
            3,
            AsciiDecimalParser.ParseInt32("-12a"u8));
        ExpectInt32Result(
            "int32 positive overflow",
            AsciiDecimalParseStatus.Overflow,
            0,
            9,
            9,
            AsciiDecimalParser.ParseInt32("2147483648"u8));
        ExpectInt32Result(
            "int32 negative overflow",
            AsciiDecimalParseStatus.Overflow,
            0,
            10,
            10,
            AsciiDecimalParser.ParseInt32("-2147483649"u8));
        ExpectInt64Result(
            "int64 sign only",
            AsciiDecimalParseStatus.SignOnly,
            0,
            1,
            -1,
            AsciiDecimalParser.ParseInt64("-"u8));
        ExpectInt64Result(
            "int64 plus rejected",
            AsciiDecimalParseStatus.InvalidDigit,
            0,
            0,
            0,
            AsciiDecimalParser.ParseInt64("+1"u8));
        ExpectInt64Result(
            "int64 whitespace rejected",
            AsciiDecimalParseStatus.InvalidDigit,
            0,
            0,
            0,
            AsciiDecimalParser.ParseInt64(" 1"u8));
        ExpectInt64Result(
            "int64 positive overflow",
            AsciiDecimalParseStatus.Overflow,
            0,
            18,
            18,
            AsciiDecimalParser.ParseInt64("9223372036854775808"u8));
        ExpectInt64Result(
            "int64 negative overflow",
            AsciiDecimalParseStatus.Overflow,
            0,
            19,
            19,
            AsciiDecimalParser.ParseInt64("-9223372036854775809"u8));

        ExpectBool("try parse int32 invalid returns false", false, AsciiDecimalParser.TryParseInt32("-x"u8, out int parsed32));
        ExpectInt32("try parse int32 invalid clears value", 0, parsed32);
        ExpectBool("try parse int64 invalid returns false", false, AsciiDecimalParser.TryParseInt64("9223372036854775808"u8, out long parsed64));
        ExpectInt64("try parse int64 invalid clears value", 0, parsed64);
    }

    [HostTest("std.ascii.decimal default result is not success", HostLane.Fast, 6)]
    private static void TestDefaultResultContract()
    {
        AsciiUInt32ParseResult uint32Result = default;
        AsciiUInt64ParseResult uint64Result = default;
        AsciiInt32ParseResult int32Result = default;
        AsciiInt64ParseResult int64Result = default;

        ExpectBool("uint32 default status is zero", true, uint32Result.Status == AsciiDecimalParseStatus.Success);
        ExpectBool("uint32 default initialized", false, uint32Result.IsInitialized);
        ExpectBool("uint32 default is not success", false, uint32Result.Succeeded);
        ExpectBool(
            "uint32 result contains no managed references",
            false,
            RuntimeHelpers.IsReferenceOrContainsReferences<AsciiUInt32ParseResult>());

        ExpectBool("uint64 default status is zero", true, uint64Result.Status == AsciiDecimalParseStatus.Success);
        ExpectBool("uint64 default initialized", false, uint64Result.IsInitialized);
        ExpectBool("uint64 default is not success", false, uint64Result.Succeeded);
        ExpectBool(
            "uint64 result contains no managed references",
            false,
            RuntimeHelpers.IsReferenceOrContainsReferences<AsciiUInt64ParseResult>());

        ExpectBool("int32 default status is zero", true, int32Result.Status == AsciiDecimalParseStatus.Success);
        ExpectBool("int32 default initialized", false, int32Result.IsInitialized);
        ExpectBool("int32 default is not success", false, int32Result.Succeeded);
        ExpectBool(
            "int32 result contains no managed references",
            false,
            RuntimeHelpers.IsReferenceOrContainsReferences<AsciiInt32ParseResult>());

        ExpectBool("int64 default status is zero", true, int64Result.Status == AsciiDecimalParseStatus.Success);
        ExpectBool("int64 default initialized", false, int64Result.IsInitialized);
        ExpectBool("int64 default is not success", false, int64Result.Succeeded);
        ExpectBool(
            "int64 result contains no managed references",
            false,
            RuntimeHelpers.IsReferenceOrContainsReferences<AsciiInt64ParseResult>());
    }

    [HostTest("std.ascii.decimal uses shared digit helper", HostLane.Fast, 7)]
    private static void TestUsesSharedDigitHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "std", "no-runtime", "shared", "Text", "AsciiDecimalParser.cs"));

        ExpectBool(
            "parser uses shared digit helper",
            true,
            source.Contains("Ascii.IsDigit(current)", StringComparison.Ordinal));
        ExpectBool(
            "parser does not keep private digit helper",
            false,
            source.Contains("private static bool IsDigit(byte value)", StringComparison.Ordinal));
    }

    private static void ExpectUInt32Result(
        string scenario,
        AsciiDecimalParseStatus expectedStatus,
        uint expectedValue,
        int expectedBytesConsumed,
        int expectedInvalidInputIndex,
        AsciiUInt32ParseResult actual)
    {
        if (actual.Status != expectedStatus ||
            actual.Value != expectedValue ||
            actual.BytesConsumed != expectedBytesConsumed ||
            actual.InvalidInputIndex != expectedInvalidInputIndex)
        {
            throw new InvalidOperationException(
                scenario + " expected status=" + expectedStatus
                + " value=" + expectedValue
                + " consumed=" + expectedBytesConsumed
                + " invalidIndex=" + expectedInvalidInputIndex
                + " but returned status=" + actual.Status
                + " value=" + actual.Value
                + " consumed=" + actual.BytesConsumed
                + " invalidIndex=" + actual.InvalidInputIndex + ".");
        }

        ExpectBool(scenario + " initialized", true, actual.IsInitialized);
        ExpectBool(
            scenario + " success metadata",
            expectedStatus == AsciiDecimalParseStatus.Success,
            actual.Succeeded);
    }

    private static void ExpectUInt64Result(
        string scenario,
        AsciiDecimalParseStatus expectedStatus,
        ulong expectedValue,
        int expectedBytesConsumed,
        int expectedInvalidInputIndex,
        AsciiUInt64ParseResult actual)
    {
        if (actual.Status != expectedStatus ||
            actual.Value != expectedValue ||
            actual.BytesConsumed != expectedBytesConsumed ||
            actual.InvalidInputIndex != expectedInvalidInputIndex)
        {
            throw new InvalidOperationException(
                scenario + " expected status=" + expectedStatus
                + " value=" + expectedValue
                + " consumed=" + expectedBytesConsumed
                + " invalidIndex=" + expectedInvalidInputIndex
                + " but returned status=" + actual.Status
                + " value=" + actual.Value
                + " consumed=" + actual.BytesConsumed
                + " invalidIndex=" + actual.InvalidInputIndex + ".");
        }

        ExpectBool(scenario + " initialized", true, actual.IsInitialized);
        ExpectBool(
            scenario + " success metadata",
            expectedStatus == AsciiDecimalParseStatus.Success,
            actual.Succeeded);
    }

    private static void ExpectInt32Result(
        string scenario,
        AsciiDecimalParseStatus expectedStatus,
        int expectedValue,
        int expectedBytesConsumed,
        int expectedInvalidInputIndex,
        AsciiInt32ParseResult actual)
    {
        if (actual.Status != expectedStatus ||
            actual.Value != expectedValue ||
            actual.BytesConsumed != expectedBytesConsumed ||
            actual.InvalidInputIndex != expectedInvalidInputIndex)
        {
            throw new InvalidOperationException(
                scenario + " expected status=" + expectedStatus
                + " value=" + expectedValue
                + " consumed=" + expectedBytesConsumed
                + " invalidIndex=" + expectedInvalidInputIndex
                + " but returned status=" + actual.Status
                + " value=" + actual.Value
                + " consumed=" + actual.BytesConsumed
                + " invalidIndex=" + actual.InvalidInputIndex + ".");
        }

        ExpectBool(scenario + " initialized", true, actual.IsInitialized);
        ExpectBool(
            scenario + " success metadata",
            expectedStatus == AsciiDecimalParseStatus.Success,
            actual.Succeeded);
    }

    private static void ExpectInt64Result(
        string scenario,
        AsciiDecimalParseStatus expectedStatus,
        long expectedValue,
        int expectedBytesConsumed,
        int expectedInvalidInputIndex,
        AsciiInt64ParseResult actual)
    {
        if (actual.Status != expectedStatus ||
            actual.Value != expectedValue ||
            actual.BytesConsumed != expectedBytesConsumed ||
            actual.InvalidInputIndex != expectedInvalidInputIndex)
        {
            throw new InvalidOperationException(
                scenario + " expected status=" + expectedStatus
                + " value=" + expectedValue
                + " consumed=" + expectedBytesConsumed
                + " invalidIndex=" + expectedInvalidInputIndex
                + " but returned status=" + actual.Status
                + " value=" + actual.Value
                + " consumed=" + actual.BytesConsumed
                + " invalidIndex=" + actual.InvalidInputIndex + ".");
        }

        ExpectBool(scenario + " initialized", true, actual.IsInitialized);
        ExpectBool(
            scenario + " success metadata",
            expectedStatus == AsciiDecimalParseStatus.Success,
            actual.Succeeded);
    }

    private static void ExpectBool(string scenario, bool expected, bool actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectUInt32(string scenario, uint expected, uint actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectUInt64(string scenario, ulong expected, ulong actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectInt32(string scenario, int expected, int actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectInt64(string scenario, long expected, long actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }
}
