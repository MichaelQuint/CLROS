using System.Runtime.CompilerServices;
using OS.Std;
using OS.Std.Text;

internal static class StdAsciiTokenSplitterTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunScenario("std.ascii tokens splits fields", TestSplitsFields);
        RunScenario("std.ascii tokens preserves empty fields", TestPreservesEmptyFields);
        RunScenario("std.ascii tokens trims fields", TestTrimsFields);
        RunScenario("std.ascii tokens removes empty fields", TestRemovesEmptyFields);
        RunScenario("std.ascii tokens trims then removes empty fields", TestTrimsThenRemovesEmptyFields);
        RunScenario("std.ascii tokens removes all empty fields", TestRemovesAllEmptyFields);
        RunScenario("std.ascii tokens reports destination capacity", TestReportsDestinationCapacity);
        RunScenario("std.ascii tokens rejects invalid ranges", TestRejectsInvalidRanges);
        RunScenario("std.ascii tokens rejects non ascii input", TestRejectsNonAsciiInput);
        RunScenario("std.ascii tokens compatibility boundary", TestCompatibilityBoundary);
    }

    private static void TestSplitsFields()
    {
        ReadOnlySpan<byte> source = "app.kind=package;runtime.kind=native-aot;listen=0.0.0.0:8080"u8;
        Span<AsciiTokenRange> tokens = stackalloc AsciiTokenRange[3];

        AsciiTokenSplitResult result = AsciiTokenSplitter.Split(source, (byte)';', tokens);

        ExpectStatus("split status", AsciiTokenSplitStatus.Success, result);
        ExpectInt("token count", 3, result.TokenCount);
        ExpectToken("first", source, "app.kind=package"u8, tokens[0]);
        ExpectToken("second", source, "runtime.kind=native-aot"u8, tokens[1]);
        ExpectToken("third", source, "listen=0.0.0.0:8080"u8, tokens[2]);
    }

    private static void TestPreservesEmptyFields()
    {
        ReadOnlySpan<byte> source = "alpha,,omega,"u8;
        Span<AsciiTokenRange> tokens = stackalloc AsciiTokenRange[4];

        AsciiTokenSplitResult result = AsciiTokenSplitter.Split(source, (byte)',', tokens);

        ExpectStatus("split status", AsciiTokenSplitStatus.Success, result);
        ExpectInt("token count", 4, result.TokenCount);
        ExpectToken("first", source, "alpha"u8, tokens[0]);
        ExpectToken("empty middle", source, ReadOnlySpan<byte>.Empty, tokens[1]);
        ExpectToken("third", source, "omega"u8, tokens[2]);
        ExpectToken("empty tail", source, ReadOnlySpan<byte>.Empty, tokens[3]);
    }

    private static void TestTrimsFields()
    {
        ReadOnlySpan<byte> source = " alpha ,\tbeta\r\n, gamma "u8;
        Span<AsciiTokenRange> tokens = stackalloc AsciiTokenRange[3];

        AsciiTokenSplitResult result = AsciiTokenSplitter.Split(
            source,
            (byte)',',
            tokens,
            AsciiTokenSplitOptions.TrimWhiteSpace);

        ExpectStatus("split status", AsciiTokenSplitStatus.Success, result);
        ExpectInt("token count", 3, result.TokenCount);
        ExpectToken("first", source, "alpha"u8, tokens[0]);
        ExpectToken("second", source, "beta"u8, tokens[1]);
        ExpectToken("third", source, "gamma"u8, tokens[2]);
    }

    private static void TestRemovesEmptyFields()
    {
        ReadOnlySpan<byte> source = "alpha,,beta,"u8;
        Span<AsciiTokenRange> tokens = stackalloc AsciiTokenRange[2];

        AsciiTokenSplitResult result = AsciiTokenSplitter.Split(
            source,
            (byte)',',
            tokens,
            AsciiTokenSplitOptions.RemoveEmptyEntries);

        ExpectStatus("split status", AsciiTokenSplitStatus.Success, result);
        ExpectInt("token count", 2, result.TokenCount);
        ExpectToken("first", source, "alpha"u8, tokens[0]);
        ExpectToken("second", source, "beta"u8, tokens[1]);
    }

    private static void TestTrimsThenRemovesEmptyFields()
    {
        ReadOnlySpan<byte> source = " alpha , \t , beta ,\r\n"u8;
        Span<AsciiTokenRange> tokens = stackalloc AsciiTokenRange[2];

        AsciiTokenSplitResult result = AsciiTokenSplitter.Split(
            source,
            (byte)',',
            tokens,
            AsciiTokenSplitOptions.TrimWhiteSpace | AsciiTokenSplitOptions.RemoveEmptyEntries);

        ExpectStatus("split status", AsciiTokenSplitStatus.Success, result);
        ExpectInt("token count", 2, result.TokenCount);
        ExpectToken("first", source, "alpha"u8, tokens[0]);
        ExpectToken("second", source, "beta"u8, tokens[1]);
    }

    private static void TestRemovesAllEmptyFields()
    {
        ReadOnlySpan<byte> source = " , \t ,\r\n"u8;
        Span<AsciiTokenRange> tokens = Span<AsciiTokenRange>.Empty;

        AsciiTokenSplitResult result = AsciiTokenSplitter.Split(
            source,
            (byte)',',
            tokens,
            AsciiTokenSplitOptions.TrimWhiteSpace | AsciiTokenSplitOptions.RemoveEmptyEntries);

        ExpectStatus("split status", AsciiTokenSplitStatus.Success, result);
        ExpectInt("token count", 0, result.TokenCount);
        ExpectInt("required tokens", 0, result.RequiredTokenCount);
        ExpectBool("zero-token success", true, result.Succeeded);
    }

    private static void TestReportsDestinationCapacity()
    {
        ReadOnlySpan<byte> source = "GET /healthz HTTP/1.1"u8;
        Span<AsciiTokenRange> tokens = stackalloc AsciiTokenRange[2];
        tokens[0] = AsciiTokenRange.Create(100, 7);

        AsciiTokenSplitResult result = AsciiTokenSplitter.Split(source, (byte)' ', tokens);

        ExpectStatus("split status", AsciiTokenSplitStatus.DestinationTooSmall, result);
        ExpectInt("required tokens", 3, result.RequiredTokenCount);
        ExpectBool("no partial write", true, tokens[0].Start == 100 && tokens[0].Length == 7);

        ReadOnlySpan<byte> removable = " alpha , , beta "u8;
        tokens[0] = AsciiTokenRange.Create(50, 2);
        result = AsciiTokenSplitter.Split(
            removable,
            (byte)',',
            tokens[..1],
            AsciiTokenSplitOptions.TrimWhiteSpace | AsciiTokenSplitOptions.RemoveEmptyEntries);

        ExpectStatus("trimmed destination status", AsciiTokenSplitStatus.DestinationTooSmall, result);
        ExpectInt("trimmed required tokens", 2, result.RequiredTokenCount);
        ExpectBool("trimmed no partial write", true, tokens[0].Start == 50 && tokens[0].Length == 2);
    }

    private static void TestRejectsInvalidRanges()
    {
        ReadOnlySpan<byte> source = "0123456789"u8;
        Span<AsciiTokenRange> tokens = stackalloc AsciiTokenRange[2];

        AsciiTokenSplitResult negativeStart = AsciiTokenSplitter.SplitRange(
            source,
            -1,
            3,
            (byte)',',
            tokens);
        AsciiTokenSplitResult negativeLength = AsciiTokenSplitter.SplitRange(
            source,
            1,
            -1,
            (byte)',',
            tokens);
        AsciiTokenSplitResult overEnd = AsciiTokenSplitter.SplitRange(
            source,
            8,
            3,
            (byte)',',
            tokens);

        ExpectStatus("negative start", AsciiTokenSplitStatus.InvalidRange, negativeStart);
        ExpectStatus("negative length", AsciiTokenSplitStatus.InvalidRange, negativeLength);
        ExpectStatus("over end", AsciiTokenSplitStatus.InvalidRange, overEnd);
    }

    private static void TestRejectsNonAsciiInput()
    {
        ReadOnlySpan<byte> source = new byte[] { (byte)'a', (byte)'=', 0x80 };
        Span<AsciiTokenRange> tokens = stackalloc AsciiTokenRange[1];

        AsciiTokenSplitResult result = AsciiTokenSplitter.Split(source, (byte)',', tokens);

        ExpectStatus("non ascii", AsciiTokenSplitStatus.NonAsciiInput, result);
        ExpectInt("invalid index", 2, result.InvalidInputIndex);

        ReadOnlySpan<byte> control = new byte[] { (byte)'a', 0x01 };
        result = AsciiTokenSplitter.Split(control, (byte)',', tokens);

        ExpectStatus("control", AsciiTokenSplitStatus.NonAsciiInput, result);
        ExpectInt("control index", 1, result.InvalidInputIndex);
    }

    private static void TestCompatibilityBoundary()
    {
        ExpectBool("status is partial", true, AsciiTokenSplitter.Status == CompatibilityStatus.Partial);

        AsciiTokenSplitResult defaultResult = default;
        ExpectBool("default result is not success", false, defaultResult.Succeeded);

        AsciiTokenRange defaultToken = default;
        ExpectBool("default token is not success", false, defaultToken.Succeeded);

        Span<AsciiTokenRange> tokens = stackalloc AsciiTokenRange[1];
        AsciiTokenSplitResult invalidDelimiter = AsciiTokenSplitter.Split("a,b"u8, 0x80, tokens);
        ExpectStatus("invalid delimiter", AsciiTokenSplitStatus.InvalidDelimiter, invalidDelimiter);

        AsciiTokenSplitResult nulDelimiter = AsciiTokenSplitter.Split("a,b"u8, 0, tokens);
        ExpectStatus("nul delimiter", AsciiTokenSplitStatus.InvalidDelimiter, nulDelimiter);

        AsciiTokenSplitResult invalidOptions = AsciiTokenSplitter.Split(
            "a,b"u8,
            (byte)',',
            tokens,
            (AsciiTokenSplitOptions)0x80);
        ExpectStatus("invalid options", AsciiTokenSplitStatus.InvalidOptions, invalidOptions);
    }

    private static void RunScenario(string name, Action test)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            test();
            Console.WriteLine("TEST_PASS " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void ExpectStatus(
        string scenario,
        AsciiTokenSplitStatus expected,
        AsciiTokenSplitResult actual)
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
