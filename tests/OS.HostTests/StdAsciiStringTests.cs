using System.Runtime.CompilerServices;
using OS.Std;
using OS.Std.Text;

internal static class StdAsciiStringTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        RunScenario("std.ascii.string startswith endswith ignorecase", TestStartsWithEndsWithIgnoreCase);
        RunScenario("std.ascii.string equality helpers", TestEqualityHelpers);
        RunScenario("std.ascii.string indexof ignorecase", TestIndexOfIgnoreCase);
        RunScenario("std.ascii.string trims space and tab", TestTrimSpaceOrTab);
        RunScenario("std.ascii.string trim uses shared space-or-tab helper", TestTrimUsesSharedSpaceOrTabHelper);
        RunScenario("std.ascii.string trims trailing space", TestTrimTrailingSpace);
        RunScenario("std.ascii.string compatibility boundary", TestCompatibilityBoundary);
    }

    private static void TestEqualityHelpers()
    {
        ReadOnlySpan<byte> value = "Content-Length"u8;

        ExpectBool("empty equals", true, AsciiString.Equals(ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty));
        ExpectBool("exact equals", true, AsciiString.Equals(value, "Content-Length"u8));
        ExpectBool("case-sensitive mismatch", false, AsciiString.Equals(value, "content-length"u8));
        ExpectBool("length mismatch", false, AsciiString.Equals(value, "Content-Lengths"u8));
        ExpectBool("ignorecase exact", true, AsciiString.EqualsIgnoreCase(value, "Content-Length"u8));
        ExpectBool("ignorecase folded", true, AsciiString.EqualsIgnoreCase(value, "content-length"u8));
        ExpectBool("ignorecase mismatch", false, AsciiString.EqualsIgnoreCase(value, "Transfer-Encoding"u8));
        ExpectBool("ignorecase length mismatch", false, AsciiString.EqualsIgnoreCase(value, "Content-Lengths"u8));
    }

    private static void TestStartsWithEndsWithIgnoreCase()
    {
        ReadOnlySpan<byte> header = "Content-Type: text/plain"u8;

        ExpectBool("empty prefix", true, AsciiString.StartsWithIgnoreCase(header, ReadOnlySpan<byte>.Empty));
        ExpectBool("empty suffix", true, AsciiString.EndsWithIgnoreCase(header, ReadOnlySpan<byte>.Empty));
        ExpectBool("exact prefix different case", true, AsciiString.StartsWithIgnoreCase(header, "content-type"u8));
        ExpectBool("exact suffix different case", true, AsciiString.EndsWithIgnoreCase(header, "TEXT/PLAIN"u8));
        ExpectBool("prefix mismatch", false, AsciiString.StartsWithIgnoreCase(header, "Content-Length"u8));
        ExpectBool("suffix mismatch", false, AsciiString.EndsWithIgnoreCase(header, "application/json"u8));
        ExpectBool("too-long prefix", false, AsciiString.StartsWithIgnoreCase("abc"u8, "abcd"u8));
        ExpectBool("too-long suffix", false, AsciiString.EndsWithIgnoreCase("abc"u8, "zabc"u8));
    }

    private static void TestIndexOfIgnoreCase()
    {
        ReadOnlySpan<byte> source = "GET /Healthz HTTP/1.1"u8;

        ExpectInt("empty needle", 0, AsciiString.IndexOfIgnoreCase(source, ReadOnlySpan<byte>.Empty));
        ExpectInt("prefix", 0, AsciiString.IndexOfIgnoreCase(source, "get"u8));
        ExpectInt("middle", 5, AsciiString.IndexOfIgnoreCase(source, "healthz"u8));
        ExpectInt("suffix", 13, AsciiString.IndexOfIgnoreCase(source, "http/1.1"u8));
        ExpectInt("missing", -1, AsciiString.IndexOfIgnoreCase(source, "POST"u8));
        ExpectInt("too long", -1, AsciiString.IndexOfIgnoreCase("abc"u8, "abcd"u8));
        ExpectInt("first repeated match", 1, AsciiString.IndexOfIgnoreCase("aBaBa"u8, "bab"u8));
    }

    private static void TestTrimSpaceOrTab()
    {
        ExpectBytes("empty", ReadOnlySpan<byte>.Empty, AsciiString.TrimSpaceOrTab(ReadOnlySpan<byte>.Empty));
        ExpectBytes("spaces", ReadOnlySpan<byte>.Empty, AsciiString.TrimSpaceOrTab("   "u8));
        ExpectBytes("tabs", ReadOnlySpan<byte>.Empty, AsciiString.TrimSpaceOrTab("\t\t"u8));
        ExpectBytes("mixed outer", "value"u8, AsciiString.TrimSpaceOrTab(" \tvalue\t "u8));
        ExpectBytes("internal preserved", "two words"u8, AsciiString.TrimSpaceOrTab("\ttwo words "u8));
        ExpectBytes("non ascii preserved", new byte[] { 0x80, (byte)'x', 0x81 }, AsciiString.TrimSpaceOrTab(new byte[] { (byte)' ', 0x80, (byte)'x', 0x81, (byte)'\t' }));
        ExpectBytes("other controls preserved", new byte[] { (byte)'\n', (byte)'x', (byte)'\r' }, AsciiString.TrimSpaceOrTab(new byte[] { (byte)'\n', (byte)'x', (byte)'\r' }));
    }

    private static void TestTrimTrailingSpace()
    {
        ExpectBytes("empty", ReadOnlySpan<byte>.Empty, AsciiString.TrimTrailingSpace(ReadOnlySpan<byte>.Empty));
        ExpectBytes("all space", ReadOnlySpan<byte>.Empty, AsciiString.TrimTrailingSpace("   "u8));
        ExpectBytes("no trim", "name\t"u8, AsciiString.TrimTrailingSpace("name\t"u8));
        ExpectBytes("partial trim", "  name"u8, AsciiString.TrimTrailingSpace("  name   "u8));
        ExpectBytes("tab after space not trimmed", "name \t"u8, AsciiString.TrimTrailingSpace("name \t"u8));
        ExpectBytes("space after tab trimmed", "name\t"u8, AsciiString.TrimTrailingSpace("name\t "u8));
    }

    private static void TestTrimUsesSharedSpaceOrTabHelper()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "std", "no-runtime", "shared", "Text", "AsciiString.cs"));

        ExpectBool(
            "trim uses shared helper",
            true,
            source.Contains("Ascii.IsSpaceOrTab(source[start])", StringComparison.Ordinal)
                && source.Contains("Ascii.IsSpaceOrTab(source[endExclusive - 1])", StringComparison.Ordinal)
                && !source.Contains("private static bool IsSpaceOrTab(byte value)", StringComparison.Ordinal));
    }

    private static void TestCompatibilityBoundary()
    {
        ExpectBool("status is partial", true, AsciiString.Status == CompatibilityStatus.Partial);

        ReadOnlySpan<byte> left = new byte[] { (byte)'A', 0x80, (byte)'Z' };
        ReadOnlySpan<byte> sameNonAscii = new byte[] { (byte)'a', 0x80, (byte)'z' };
        ReadOnlySpan<byte> differentNonAscii = new byte[] { (byte)'a', 0x81, (byte)'z' };

        ExpectBool("matching non-ASCII byte is preserved", true, AsciiString.StartsWithIgnoreCase(left, sameNonAscii));
        ExpectBool("different non-ASCII byte is not folded", false, AsciiString.StartsWithIgnoreCase(left, differentNonAscii));
        ExpectBool("equals preserves matching non-ASCII", true, AsciiString.Equals(left, new byte[] { (byte)'A', 0x80, (byte)'Z' }));
        ExpectBool("equals rejects different non-ASCII", false, AsciiString.Equals(left, differentNonAscii));
        ExpectBool("ignorecase preserves matching non-ASCII", true, AsciiString.EqualsIgnoreCase(left, sameNonAscii));
        ExpectBool("ignorecase rejects different non-ASCII", false, AsciiString.EqualsIgnoreCase(left, differentNonAscii));
        ExpectInt("non-ASCII search does not fold", -1, AsciiString.IndexOfIgnoreCase(left, differentNonAscii));
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

    private static void ExpectBytes(string scenario, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        if (!OS.Std.MemoryExtensions.SequenceEqual(expected, actual))
        {
            throw new InvalidOperationException(scenario + " returned unexpected bytes.");
        }
    }
}
