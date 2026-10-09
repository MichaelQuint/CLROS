using System.Runtime.CompilerServices;
using OS.Std;
using OS.Std.Text;

internal static class StdAsciiHexParserTests
{

    [HostTest("std.ascii.hex parses uint32 values", HostLane.Fast, 0)]
    private static void TestParseUInt32Values()
    {
        ExpectBool("status is partial", true, AsciiHexParser.Status == CompatibilityStatus.Partial);
        ExpectUInt32Result(
            "zero",
            AsciiHexParseStatus.Success,
            0,
            1,
            -1,
            AsciiHexParser.ParseUInt32("0"u8));
        ExpectUInt32Result(
            "lowercase",
            AsciiHexParseStatus.Success,
            0x1A2B3C4D,
            8,
            -1,
            AsciiHexParser.ParseUInt32("1a2b3c4d"u8));
        ExpectUInt32Result(
            "uppercase",
            AsciiHexParseStatus.Success,
            0xFFFFFFFF,
            8,
            -1,
            AsciiHexParser.ParseUInt32("FFFFFFFF"u8));
        ExpectUInt32Result(
            "leading zeros",
            AsciiHexParseStatus.Success,
            0x2A,
            8,
            -1,
            AsciiHexParser.ParseUInt32("0000002a"u8));

        ExpectBool("try parse success", true, AsciiHexParser.TryParseUInt32("2A"u8, out uint parsed));
        ExpectUInt32("try parse value", 0x2A, parsed);
    }

    [HostTest("std.ascii.hex parses uint64 values", HostLane.Fast, 1)]
    private static void TestParseUInt64Values()
    {
        ExpectUInt64Result(
            "zero",
            AsciiHexParseStatus.Success,
            0,
            1,
            -1,
            AsciiHexParser.ParseUInt64("0"u8));
        ExpectUInt64Result(
            "mixed case",
            AsciiHexParseStatus.Success,
            0x1234ABCDef987650UL,
            16,
            -1,
            AsciiHexParser.ParseUInt64("1234ABCDef987650"u8));
        ExpectUInt64Result(
            "ulong max",
            AsciiHexParseStatus.Success,
            ulong.MaxValue,
            16,
            -1,
            AsciiHexParser.ParseUInt64("FFFFFFFFFFFFFFFF"u8));

        ExpectBool("try parse success", true, AsciiHexParser.TryParseUInt64("100000000"u8, out ulong parsed));
        ExpectUInt64("try parse value", 0x100000000UL, parsed);
    }

    [HostTest("std.ascii.hex accepts optional 0x prefix", HostLane.Fast, 2)]
    private static void TestAcceptsOptionalPrefix()
    {
        ExpectUInt32Result(
            "uint32 lowercase prefix",
            AsciiHexParseStatus.Success,
            0xDEADBEEF,
            10,
            -1,
            AsciiHexParser.ParseUInt32("0xdeadbeef"u8));
        ExpectUInt32Result(
            "uint32 uppercase prefix",
            AsciiHexParseStatus.Success,
            0x10,
            4,
            -1,
            AsciiHexParser.ParseUInt32("0X10"u8));
        ExpectUInt64Result(
            "uint64 lowercase prefix",
            AsciiHexParseStatus.Success,
            0xABCDEF0123456789UL,
            18,
            -1,
            AsciiHexParser.ParseUInt64("0xabcdef0123456789"u8));
        ExpectUInt64Result(
            "uint64 uppercase prefix",
            AsciiHexParseStatus.Success,
            0xABCDEF0123456789UL,
            18,
            -1,
            AsciiHexParser.ParseUInt64("0XABCDEF0123456789"u8));
    }

    [HostTest("std.ascii.hex rejects invalid input", HostLane.Fast, 3)]
    private static void TestRejectsInvalidInput()
    {
        ExpectUInt32Result(
            "uint32 empty",
            AsciiHexParseStatus.Empty,
            0,
            0,
            -1,
            AsciiHexParser.ParseUInt32(ReadOnlySpan<byte>.Empty));
        ExpectUInt32Result(
            "uint32 prefix only lowercase",
            AsciiHexParseStatus.PrefixOnly,
            0,
            2,
            -1,
            AsciiHexParser.ParseUInt32("0x"u8));
        ExpectUInt32Result(
            "uint32 prefix only uppercase",
            AsciiHexParseStatus.PrefixOnly,
            0,
            2,
            -1,
            AsciiHexParser.ParseUInt32("0X"u8));
        ExpectUInt32Result(
            "uint32 sign rejected",
            AsciiHexParseStatus.NonHexDigit,
            0,
            0,
            0,
            AsciiHexParser.ParseUInt32("-1"u8));
        ExpectUInt32Result(
            "uint32 non hex digit",
            AsciiHexParseStatus.NonHexDigit,
            0,
            2,
            2,
            AsciiHexParser.ParseUInt32("12g4"u8));
        ExpectUInt64Result(
            "uint64 whitespace rejected",
            AsciiHexParseStatus.NonHexDigit,
            0,
            0,
            0,
            AsciiHexParser.ParseUInt64(" ff"u8));
        ExpectUInt64Result(
            "uint64 prefix non hex digit",
            AsciiHexParseStatus.NonHexDigit,
            0,
            2,
            2,
            AsciiHexParser.ParseUInt64("0x_"u8));

        ExpectBool("try parse invalid returns false", false, AsciiHexParser.TryParseUInt32("0xz"u8, out uint parsed));
        ExpectUInt32("try parse invalid clears value", 0, parsed);
    }

    [HostTest("std.ascii.hex rejects overflow", HostLane.Fast, 4)]
    private static void TestRejectsOverflow()
    {
        ExpectUInt32Result(
            "uint32 overflow by digit count",
            AsciiHexParseStatus.Overflow,
            0,
            8,
            8,
            AsciiHexParser.ParseUInt32("100000000"u8));
        ExpectUInt32Result(
            "uint32 prefixed overflow",
            AsciiHexParseStatus.Overflow,
            0,
            10,
            10,
            AsciiHexParser.ParseUInt32("0x100000000"u8));
        ExpectUInt64Result(
            "uint64 overflow by digit count",
            AsciiHexParseStatus.Overflow,
            0,
            16,
            16,
            AsciiHexParser.ParseUInt64("10000000000000000"u8));
        ExpectUInt64Result(
            "uint64 prefixed overflow",
            AsciiHexParseStatus.Overflow,
            0,
            18,
            18,
            AsciiHexParser.ParseUInt64("0x10000000000000000"u8));

        ExpectBool("try parse overflow returns false", false, AsciiHexParser.TryParseUInt64("10000000000000000"u8, out ulong parsed));
        ExpectUInt64("try parse overflow clears value", 0, parsed);
    }

    [HostTest("std.ascii.hex default result is not success", HostLane.Fast, 5)]
    private static void TestDefaultResultContract()
    {
        AsciiHexUInt32ParseResult uint32Result = default;
        AsciiHexUInt64ParseResult uint64Result = default;

        ExpectBool("uint32 default status is zero", true, uint32Result.Status == AsciiHexParseStatus.Success);
        ExpectBool("uint32 default initialized", false, uint32Result.IsInitialized);
        ExpectBool("uint32 default is not success", false, uint32Result.Succeeded);

        ExpectBool("uint64 default status is zero", true, uint64Result.Status == AsciiHexParseStatus.Success);
        ExpectBool("uint64 default initialized", false, uint64Result.IsInitialized);
        ExpectBool("uint64 default is not success", false, uint64Result.Succeeded);
    }

    [HostTest("std.ascii.hex layouts contain no managed references", HostLane.Fast, 6)]
    private static void TestLayoutContract()
    {
        ExpectBool(
            "uint32 result contains no managed references",
            false,
            RuntimeHelpers.IsReferenceOrContainsReferences<AsciiHexUInt32ParseResult>());
        ExpectBool(
            "uint64 result contains no managed references",
            false,
            RuntimeHelpers.IsReferenceOrContainsReferences<AsciiHexUInt64ParseResult>());
    }

    private static void ExpectUInt32Result(
        string scenario,
        AsciiHexParseStatus expectedStatus,
        uint expectedValue,
        int expectedBytesConsumed,
        int expectedInvalidInputIndex,
        AsciiHexUInt32ParseResult actual)
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
            expectedStatus == AsciiHexParseStatus.Success,
            actual.Succeeded);
    }

    private static void ExpectUInt64Result(
        string scenario,
        AsciiHexParseStatus expectedStatus,
        ulong expectedValue,
        int expectedBytesConsumed,
        int expectedInvalidInputIndex,
        AsciiHexUInt64ParseResult actual)
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
            expectedStatus == AsciiHexParseStatus.Success,
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
}
