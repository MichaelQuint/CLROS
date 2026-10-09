using System.Runtime.CompilerServices;
using OS.Std;

internal static class StdAsciiByteTests
{
    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        const string name = "std.ascii byte helpers";
        HostCheck.Run(name, () =>
        {
            ExpectBool("IsDigit", (byte)'0', true, Ascii.IsDigit((byte)'0'));
            ExpectBool("IsDigit", (byte)'9', true, Ascii.IsDigit((byte)'9'));
            ExpectBool("IsDigit", (byte)'/', false, Ascii.IsDigit((byte)'/'));
            ExpectBool("IsDigit", (byte)':', false, Ascii.IsDigit((byte)':'));
            ExpectBool("IsDigit", 0x80, false, Ascii.IsDigit(0x80));
            ExpectBool("IsDigit", 0xff, false, Ascii.IsDigit(0xff));

            ExpectBool("IsLetter", (byte)'A', true, Ascii.IsLetter((byte)'A'));
            ExpectBool("IsLetter", (byte)'Z', true, Ascii.IsLetter((byte)'Z'));
            ExpectBool("IsLetter", (byte)'a', true, Ascii.IsLetter((byte)'a'));
            ExpectBool("IsLetter", (byte)'z', true, Ascii.IsLetter((byte)'z'));
            ExpectBool("IsLetter", (byte)'@', false, Ascii.IsLetter((byte)'@'));
            ExpectBool("IsLetter", (byte)'[', false, Ascii.IsLetter((byte)'['));
            ExpectBool("IsLetter", (byte)'`', false, Ascii.IsLetter((byte)'`'));
            ExpectBool("IsLetter", (byte)'{', false, Ascii.IsLetter((byte)'{'));
            ExpectBool("IsLetter", 0x80, false, Ascii.IsLetter(0x80));
            ExpectBool("IsLetter", 0xff, false, Ascii.IsLetter(0xff));

            ExpectBool("IsLetterOrDigit", (byte)'A', true, Ascii.IsLetterOrDigit((byte)'A'));
            ExpectBool("IsLetterOrDigit", (byte)'m', true, Ascii.IsLetterOrDigit((byte)'m'));
            ExpectBool("IsLetterOrDigit", (byte)'5', true, Ascii.IsLetterOrDigit((byte)'5'));
            ExpectBool("IsLetterOrDigit", (byte)'-', false, Ascii.IsLetterOrDigit((byte)'-'));
            ExpectBool("IsLetterOrDigit", 0x80, false, Ascii.IsLetterOrDigit(0x80));

            ExpectBool("IsWhiteSpace", (byte)' ', true, Ascii.IsWhiteSpace((byte)' '));
            ExpectBool("IsWhiteSpace", (byte)'\t', true, Ascii.IsWhiteSpace((byte)'\t'));
            ExpectBool("IsWhiteSpace", (byte)'\n', true, Ascii.IsWhiteSpace((byte)'\n'));
            ExpectBool("IsWhiteSpace", (byte)'\v', true, Ascii.IsWhiteSpace((byte)'\v'));
            ExpectBool("IsWhiteSpace", (byte)'\f', true, Ascii.IsWhiteSpace((byte)'\f'));
            ExpectBool("IsWhiteSpace", (byte)'\r', true, Ascii.IsWhiteSpace((byte)'\r'));
            ExpectBool("IsWhiteSpace", (byte)'x', false, Ascii.IsWhiteSpace((byte)'x'));
            ExpectBool("IsWhiteSpace", 0x80, false, Ascii.IsWhiteSpace(0x80));

            ExpectBool("IsPrintable", 0x00, false, Ascii.IsPrintable(0x00));
            ExpectBool("IsPrintable", 0x1F, false, Ascii.IsPrintable(0x1F));
            ExpectBool("IsPrintable", (byte)' ', true, Ascii.IsPrintable((byte)' '));
            ExpectBool("IsPrintable", (byte)'!', true, Ascii.IsPrintable((byte)'!'));
            ExpectBool("IsPrintable", (byte)'~', true, Ascii.IsPrintable((byte)'~'));
            ExpectBool("IsPrintable", 0x7F, false, Ascii.IsPrintable(0x7F));
            ExpectBool("IsPrintable", 0x80, false, Ascii.IsPrintable(0x80));
            ExpectBool("IsPrintable", 0xFF, false, Ascii.IsPrintable(0xFF));

            ExpectBool("IsVisible", 0x00, false, Ascii.IsVisible(0x00));
            ExpectBool("IsVisible", 0x1F, false, Ascii.IsVisible(0x1F));
            ExpectBool("IsVisible", (byte)' ', false, Ascii.IsVisible((byte)' '));
            ExpectBool("IsVisible", (byte)'!', true, Ascii.IsVisible((byte)'!'));
            ExpectBool("IsVisible", (byte)'~', true, Ascii.IsVisible((byte)'~'));
            ExpectBool("IsVisible", 0x7F, false, Ascii.IsVisible(0x7F));
            ExpectBool("IsVisible", 0x80, false, Ascii.IsVisible(0x80));
            ExpectBool("IsVisible", 0xFF, false, Ascii.IsVisible(0xFF));

            ExpectByte("ToUpperInvariant", (byte)'a', (byte)'A', Ascii.ToUpperInvariant((byte)'a'));
            ExpectByte("ToUpperInvariant", (byte)'z', (byte)'Z', Ascii.ToUpperInvariant((byte)'z'));
            ExpectByte("ToUpperInvariant", (byte)'A', (byte)'A', Ascii.ToUpperInvariant((byte)'A'));
            ExpectByte("ToUpperInvariant", (byte)'7', (byte)'7', Ascii.ToUpperInvariant((byte)'7'));
            ExpectByte("ToUpperInvariant", 0x80, 0x80, Ascii.ToUpperInvariant(0x80));
            ExpectByte("ToUpperInvariant", 0xff, 0xff, Ascii.ToUpperInvariant(0xff));

            ExpectByte("ToLowerInvariant", (byte)'A', (byte)'a', Ascii.ToLowerInvariant((byte)'A'));
            ExpectByte("ToLowerInvariant", (byte)'Z', (byte)'z', Ascii.ToLowerInvariant((byte)'Z'));
            ExpectByte("ToLowerInvariant", (byte)'a', (byte)'a', Ascii.ToLowerInvariant((byte)'a'));
            ExpectByte("ToLowerInvariant", (byte)'7', (byte)'7', Ascii.ToLowerInvariant((byte)'7'));
            ExpectByte("ToLowerInvariant", 0x80, 0x80, Ascii.ToLowerInvariant(0x80));
            ExpectByte("ToLowerInvariant", 0xff, 0xff, Ascii.ToLowerInvariant(0xff));

            ExpectSpanEqual(Array.Empty<byte>(), Array.Empty<byte>(), true);
            ExpectSpanEqual(Bytes("Manifest"), Bytes("Manifest"), true);
            ExpectSpanEqual(Bytes("Manifest"), Bytes("manifest"), true);
            ExpectSpanEqual(Bytes("Manifest"), Bytes("Manifests"), false);
            ExpectSpanEqual(Bytes("Manifest.Json"), Bytes("Manifest-Json"), false);
            ExpectSpanEqual(new byte[] { 0x80 }, new byte[] { 0x80 }, true);
            ExpectSpanEqual(new byte[] { 0x80 }, new byte[] { 0x81 }, false);
        });
    }

    private static void ExpectBool(string methodName, byte input, bool expected, bool actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                methodName + "(" + Describe(input) + ") expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectByte(string methodName, byte input, byte expected, byte actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                methodName + "(" + Describe(input) + ") expected " + Describe(expected) + " but returned " + Describe(actual) + ".");
        }
    }

    private static void ExpectSpanEqual(byte[] left, byte[] right, bool expected)
    {
        bool actual = Ascii.EqualsIgnoreCase(left, right);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                "EqualsIgnoreCase expected " + expected + " but returned " + actual + ".");
        }
    }

    private static byte[] Bytes(string value)
    {
        byte[] bytes = new byte[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            bytes[i] = (byte)value[i];
        }

        return bytes;
    }

    private static string Describe(byte value)
    {
        return value switch
        {
            (byte)'\t' => "\\t",
            (byte)'\n' => "\\n",
            (byte)'\v' => "\\v",
            (byte)'\f' => "\\f",
            (byte)'\r' => "\\r",
            >= 0x20 and <= 0x7e => "'" + (char)value + "'",
            _ => "0x" + value.ToString("X2")
        };
    }
}
