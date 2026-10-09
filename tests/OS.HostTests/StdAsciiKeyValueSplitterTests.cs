using System.Runtime.CompilerServices;
using OS.Std;
using OS.Std.Text;

internal static class StdAsciiKeyValueSplitterTests
{

    [HostTest("std.ascii keyvalue splits first delimiter", HostLane.Fast, 0)]
    private static void TestSplitsFirstDelimiter()
    {
        ReadOnlySpan<byte> source = "app.kind=package"u8;

        AsciiKeyValueSplitResult result = AsciiKeyValueSplitter.Split(source, (byte)'=');

        ExpectStatus("split status", AsciiKeyValueSplitStatus.Success, result);
        ExpectBool("success flag", true, result.Succeeded);
        ExpectInt("source start", 0, result.SourceStart);
        ExpectInt("source length", source.Length, result.SourceLength);
        ExpectInt("delimiter index", 8, result.DelimiterIndex);
        ExpectToken("key", source, "app.kind"u8, result.Key);
        ExpectToken("value", source, "package"u8, result.Value);
    }

    [HostTest("std.ascii keyvalue preserves delimiter in value", HostLane.Fast, 1)]
    private static void TestPreservesDelimiterInValue()
    {
        ReadOnlySpan<byte> source = "xxurl=http://127.0.0.1:8080/a=b;tail"u8;

        AsciiKeyValueSplitResult result = AsciiKeyValueSplitter.SplitRange(
            source,
            2,
            "url=http://127.0.0.1:8080/a=b"u8.Length,
            (byte)'=');

        ExpectStatus("split status", AsciiKeyValueSplitStatus.Success, result);
        ExpectInt("delimiter index", 5, result.DelimiterIndex);
        ExpectToken("key", source, "url"u8, result.Key);
        ExpectToken("value", source, "http://127.0.0.1:8080/a=b"u8, result.Value);
    }

    [HostTest("std.ascii keyvalue trims key and value", HostLane.Fast, 2)]
    private static void TestTrimsKeyAndValue()
    {
        ReadOnlySpan<byte> source = " \t listen =  0.0.0.0:8080 \r\n"u8;

        AsciiKeyValueSplitResult result = AsciiKeyValueSplitter.Split(
            source,
            (byte)'=',
            AsciiTokenSplitOptions.TrimWhiteSpace);

        ExpectStatus("split status", AsciiKeyValueSplitStatus.Success, result);
        ExpectToken("key", source, "listen"u8, result.Key);
        ExpectToken("value", source, "0.0.0.0:8080"u8, result.Value);
    }

    [HostTest("std.ascii keyvalue allows empty value", HostLane.Fast, 3)]
    private static void TestAllowsEmptyValue()
    {
        ReadOnlySpan<byte> source = "feature.enabled="u8;

        AsciiKeyValueSplitResult result = AsciiKeyValueSplitter.Split(source, (byte)'=');

        ExpectStatus("split status", AsciiKeyValueSplitStatus.Success, result);
        ExpectBool("success flag", true, result.Succeeded);
        ExpectToken("key", source, "feature.enabled"u8, result.Key);
        ExpectToken("value", source, ReadOnlySpan<byte>.Empty, result.Value);
        ExpectInt("value start", source.Length, result.Value.Start);
    }

    [HostTest("std.ascii keyvalue rejects empty key", HostLane.Fast, 4)]
    private static void TestRejectsEmptyKey()
    {
        AsciiKeyValueSplitResult result = AsciiKeyValueSplitter.Split("=value"u8, (byte)'=');
        ExpectStatus("empty key", AsciiKeyValueSplitStatus.EmptyKey, result);
        ExpectBool("empty key not success", false, result.Succeeded);

        result = AsciiKeyValueSplitter.Split(
            " \t = value"u8,
            (byte)'=',
            AsciiTokenSplitOptions.TrimWhiteSpace);

        ExpectStatus("trimmed empty key", AsciiKeyValueSplitStatus.EmptyKey, result);
        ExpectBool("trimmed empty key not success", false, result.Succeeded);
    }

    [HostTest("std.ascii keyvalue rejects missing delimiter", HostLane.Fast, 5)]
    private static void TestRejectsMissingDelimiter()
    {
        AsciiKeyValueSplitResult result = AsciiKeyValueSplitter.Split("name"u8, (byte)'=');

        ExpectStatus("missing delimiter", AsciiKeyValueSplitStatus.MissingDelimiter, result);
        ExpectInt("delimiter index", -1, result.DelimiterIndex);
        ExpectBool("missing delimiter not success", false, result.Succeeded);
    }

    [HostTest("std.ascii keyvalue rejects invalid ranges", HostLane.Fast, 6)]
    private static void TestRejectsInvalidRanges()
    {
        ReadOnlySpan<byte> source = "0123456789"u8;

        AsciiKeyValueSplitResult negativeStart = AsciiKeyValueSplitter.SplitRange(
            source,
            -1,
            3,
            (byte)'=');
        AsciiKeyValueSplitResult negativeLength = AsciiKeyValueSplitter.SplitRange(
            source,
            1,
            -1,
            (byte)'=');
        AsciiKeyValueSplitResult overEnd = AsciiKeyValueSplitter.SplitRange(
            source,
            8,
            3,
            (byte)'=');

        ExpectStatus("negative start", AsciiKeyValueSplitStatus.InvalidRange, negativeStart);
        ExpectStatus("negative length", AsciiKeyValueSplitStatus.InvalidRange, negativeLength);
        ExpectStatus("over end", AsciiKeyValueSplitStatus.InvalidRange, overEnd);
    }

    [HostTest("std.ascii keyvalue rejects invalid delimiter and options", HostLane.Fast, 7)]
    private static void TestRejectsInvalidDelimiterAndOptions()
    {
        AsciiKeyValueSplitResult invalidDelimiter = AsciiKeyValueSplitter.Split("a=b"u8, 0x80);
        ExpectStatus("invalid delimiter", AsciiKeyValueSplitStatus.InvalidDelimiter, invalidDelimiter);

        AsciiKeyValueSplitResult nulDelimiter = AsciiKeyValueSplitter.Split("a=b"u8, 0);
        ExpectStatus("nul delimiter", AsciiKeyValueSplitStatus.InvalidDelimiter, nulDelimiter);

        AsciiKeyValueSplitResult invalidOptions = AsciiKeyValueSplitter.Split(
            "a=b"u8,
            (byte)'=',
            AsciiTokenSplitOptions.RemoveEmptyEntries);

        ExpectStatus("invalid options", AsciiKeyValueSplitStatus.InvalidOptions, invalidOptions);
    }

    [HostTest("std.ascii keyvalue rejects non ascii input", HostLane.Fast, 8)]
    private static void TestRejectsNonAsciiInput()
    {
        ReadOnlySpan<byte> nonAscii = new byte[] { (byte)'a', (byte)'=', 0x80 };

        AsciiKeyValueSplitResult result = AsciiKeyValueSplitter.Split(nonAscii, (byte)'=');

        ExpectStatus("non ascii", AsciiKeyValueSplitStatus.NonAsciiInput, result);
        ExpectInt("invalid index", 2, result.InvalidInputIndex);

        ReadOnlySpan<byte> control = new byte[] { (byte)'a', (byte)'=', 0x01 };
        result = AsciiKeyValueSplitter.Split(control, (byte)'=');

        ExpectStatus("control", AsciiKeyValueSplitStatus.NonAsciiInput, result);
        ExpectInt("control index", 2, result.InvalidInputIndex);
    }

    [HostTest("std.ascii keyvalue compatibility boundary", HostLane.Fast, 9)]
    private static void TestCompatibilityBoundary()
    {
        ExpectBool("status is partial", true, AsciiKeyValueSplitter.Status == CompatibilityStatus.Partial);

        AsciiKeyValueSplitResult defaultResult = default;
        ExpectBool("default result is not success", false, defaultResult.Succeeded);

        ExpectBool(
            "result contains no managed references",
            false,
            RuntimeHelpers.IsReferenceOrContainsReferences<AsciiKeyValueSplitResult>());
    }

    private static void ExpectStatus(
        string scenario,
        AsciiKeyValueSplitStatus expected,
        AsciiKeyValueSplitResult actual)
    {
        if (actual.Status != expected)
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned " + actual.Status + ".");
        }
    }

    private static void ExpectToken(
        string scenario,
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> expected,
        AsciiTokenRange actual)
    {
        if (!actual.Succeeded)
        {
            throw new InvalidOperationException(scenario + " token was not initialized.");
        }

        if (!source.Slice(actual.Start, actual.Length).SequenceEqual(expected))
        {
            throw new InvalidOperationException(scenario + " token did not match expected bytes.");
        }
    }

    private static void ExpectBool(string scenario, bool expected, bool actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectInt(string scenario, int expected, int actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(scenario + " expected " + expected + " but returned " + actual + ".");
        }
    }
}
