using System.Runtime.CompilerServices;
using OS.Std;

internal static class StdAsciiTests
{
    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        const string name = "std.ascii helpers";
        HostCheck.Run(name, () =>
        {
            ExpectBool("IsDigit", '0', true, Ascii.IsDigit('0'));
            ExpectBool("IsDigit", '9', true, Ascii.IsDigit('9'));
            ExpectBool("IsDigit", '/', false, Ascii.IsDigit('/'));
            ExpectBool("IsDigit", ':', false, Ascii.IsDigit(':'));

            ExpectBool("IsLetter", 'A', true, Ascii.IsLetter('A'));
            ExpectBool("IsLetter", 'Z', true, Ascii.IsLetter('Z'));
            ExpectBool("IsLetter", 'a', true, Ascii.IsLetter('a'));
            ExpectBool("IsLetter", 'z', true, Ascii.IsLetter('z'));
            ExpectBool("IsLetter", '@', false, Ascii.IsLetter('@'));
            ExpectBool("IsLetter", '[', false, Ascii.IsLetter('['));
            ExpectBool("IsLetter", '`', false, Ascii.IsLetter('`'));
            ExpectBool("IsLetter", '{', false, Ascii.IsLetter('{'));
            ExpectBool("IsLetter", '\u00C0', false, Ascii.IsLetter('\u00C0'));

            ExpectBool("IsLetterOrDigit", 'A', true, Ascii.IsLetterOrDigit('A'));
            ExpectBool("IsLetterOrDigit", 'm', true, Ascii.IsLetterOrDigit('m'));
            ExpectBool("IsLetterOrDigit", '5', true, Ascii.IsLetterOrDigit('5'));
            ExpectBool("IsLetterOrDigit", '-', false, Ascii.IsLetterOrDigit('-'));
            ExpectBool("IsLetterOrDigit", '\u00E9', false, Ascii.IsLetterOrDigit('\u00E9'));

            ExpectBool("IsWhiteSpace", ' ', true, Ascii.IsWhiteSpace(' '));
            ExpectBool("IsWhiteSpace", '\t', true, Ascii.IsWhiteSpace('\t'));
            ExpectBool("IsWhiteSpace", '\n', true, Ascii.IsWhiteSpace('\n'));
            ExpectBool("IsWhiteSpace", '\v', true, Ascii.IsWhiteSpace('\v'));
            ExpectBool("IsWhiteSpace", '\f', true, Ascii.IsWhiteSpace('\f'));
            ExpectBool("IsWhiteSpace", '\r', true, Ascii.IsWhiteSpace('\r'));
            ExpectBool("IsWhiteSpace", 'x', false, Ascii.IsWhiteSpace('x'));
            ExpectBool("IsWhiteSpace", '\u00A0', false, Ascii.IsWhiteSpace('\u00A0'));

            ExpectChar("ToUpperInvariant", 'a', 'A', Ascii.ToUpperInvariant('a'));
            ExpectChar("ToUpperInvariant", 'z', 'Z', Ascii.ToUpperInvariant('z'));
            ExpectChar("ToUpperInvariant", 'A', 'A', Ascii.ToUpperInvariant('A'));
            ExpectChar("ToUpperInvariant", '7', '7', Ascii.ToUpperInvariant('7'));
            ExpectChar("ToUpperInvariant", '\u00E9', '\u00E9', Ascii.ToUpperInvariant('\u00E9'));

            ExpectChar("ToLowerInvariant", 'A', 'a', Ascii.ToLowerInvariant('A'));
            ExpectChar("ToLowerInvariant", 'Z', 'z', Ascii.ToLowerInvariant('Z'));
            ExpectChar("ToLowerInvariant", 'a', 'a', Ascii.ToLowerInvariant('a'));
            ExpectChar("ToLowerInvariant", '7', '7', Ascii.ToLowerInvariant('7'));
            ExpectChar("ToLowerInvariant", '\u00C9', '\u00C9', Ascii.ToLowerInvariant('\u00C9'));
        });

        const string upperLowerName = "std.ascii upper lower letter helpers";
        HostCheck.Run(upperLowerName, () =>
        {
            ExpectBool("IsUpperLetter", 'A', true, Ascii.IsUpperLetter('A'));
            ExpectBool("IsUpperLetter", 'Z', true, Ascii.IsUpperLetter('Z'));
            ExpectBool("IsUpperLetter", '@', false, Ascii.IsUpperLetter('@'));
            ExpectBool("IsUpperLetter", '[', false, Ascii.IsUpperLetter('['));
            ExpectBool("IsUpperLetter", 'a', false, Ascii.IsUpperLetter('a'));
            ExpectBool("IsUpperLetter", '0', false, Ascii.IsUpperLetter('0'));
            ExpectBool("IsUpperLetter", '\u00C0', false, Ascii.IsUpperLetter('\u00C0'));

            ExpectBool("IsLowerLetter", 'a', true, Ascii.IsLowerLetter('a'));
            ExpectBool("IsLowerLetter", 'z', true, Ascii.IsLowerLetter('z'));
            ExpectBool("IsLowerLetter", '`', false, Ascii.IsLowerLetter('`'));
            ExpectBool("IsLowerLetter", '{', false, Ascii.IsLowerLetter('{'));
            ExpectBool("IsLowerLetter", 'A', false, Ascii.IsLowerLetter('A'));
            ExpectBool("IsLowerLetter", '0', false, Ascii.IsLowerLetter('0'));
            ExpectBool("IsLowerLetter", '\u00E9', false, Ascii.IsLowerLetter('\u00E9'));

            ExpectBool("IsUpperLetter", (byte)'A', true, Ascii.IsUpperLetter((byte)'A'));
            ExpectBool("IsUpperLetter", (byte)'Z', true, Ascii.IsUpperLetter((byte)'Z'));
            ExpectBool("IsUpperLetter", (byte)'@', false, Ascii.IsUpperLetter((byte)'@'));
            ExpectBool("IsUpperLetter", (byte)'[', false, Ascii.IsUpperLetter((byte)'['));
            ExpectBool("IsUpperLetter", (byte)'a', false, Ascii.IsUpperLetter((byte)'a'));
            ExpectBool("IsUpperLetter", (byte)'0', false, Ascii.IsUpperLetter((byte)'0'));
            ExpectBool("IsUpperLetter", (byte)0x80, false, Ascii.IsUpperLetter((byte)0x80));

            ExpectBool("IsLowerLetter", (byte)'a', true, Ascii.IsLowerLetter((byte)'a'));
            ExpectBool("IsLowerLetter", (byte)'z', true, Ascii.IsLowerLetter((byte)'z'));
            ExpectBool("IsLowerLetter", (byte)'`', false, Ascii.IsLowerLetter((byte)'`'));
            ExpectBool("IsLowerLetter", (byte)'{', false, Ascii.IsLowerLetter((byte)'{'));
            ExpectBool("IsLowerLetter", (byte)'A', false, Ascii.IsLowerLetter((byte)'A'));
            ExpectBool("IsLowerLetter", (byte)'0', false, Ascii.IsLowerLetter((byte)'0'));
            ExpectBool("IsLowerLetter", (byte)0x80, false, Ascii.IsLowerLetter((byte)0x80));
        });

        const string spaceOrTabName = "std.ascii space-or-tab helper";
        HostCheck.Run(spaceOrTabName, () =>
        {
            ExpectBool("IsSpaceOrTab", ' ', true, Ascii.IsSpaceOrTab(' '));
            ExpectBool("IsSpaceOrTab", '\t', true, Ascii.IsSpaceOrTab('\t'));
            ExpectBool("IsSpaceOrTab", '\n', false, Ascii.IsSpaceOrTab('\n'));
            ExpectBool("IsSpaceOrTab", '\r', false, Ascii.IsSpaceOrTab('\r'));
            ExpectBool("IsSpaceOrTab", '\v', false, Ascii.IsSpaceOrTab('\v'));
            ExpectBool("IsSpaceOrTab", '\f', false, Ascii.IsSpaceOrTab('\f'));
            ExpectBool("IsSpaceOrTab", 'x', false, Ascii.IsSpaceOrTab('x'));
            ExpectBool("IsSpaceOrTab", '\u00A0', false, Ascii.IsSpaceOrTab('\u00A0'));

            ExpectBool("IsSpaceOrTab", (byte)' ', true, Ascii.IsSpaceOrTab((byte)' '));
            ExpectBool("IsSpaceOrTab", (byte)'\t', true, Ascii.IsSpaceOrTab((byte)'\t'));
            ExpectBool("IsSpaceOrTab", (byte)'\n', false, Ascii.IsSpaceOrTab((byte)'\n'));
            ExpectBool("IsSpaceOrTab", (byte)'\r', false, Ascii.IsSpaceOrTab((byte)'\r'));
            ExpectBool("IsSpaceOrTab", (byte)'\v', false, Ascii.IsSpaceOrTab((byte)'\v'));
            ExpectBool("IsSpaceOrTab", (byte)'\f', false, Ascii.IsSpaceOrTab((byte)'\f'));
            ExpectBool("IsSpaceOrTab", (byte)'x', false, Ascii.IsSpaceOrTab((byte)'x'));
            ExpectBool("IsSpaceOrTab", 0x80, false, Ascii.IsSpaceOrTab(0x80));
        });
    }

    private static void ExpectBool(string methodName, char input, bool expected, bool actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                methodName + "(" + Describe(input) + ") expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectBool(string methodName, byte input, bool expected, bool actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                methodName + "(" + Describe(input) + ") expected " + expected + " but returned " + actual + ".");
        }
    }

    private static void ExpectChar(string methodName, char input, char expected, char actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                methodName + "(" + Describe(input) + ") expected " + Describe(expected) + " but returned " + Describe(actual) + ".");
        }
    }

    private static string Describe(char value)
    {
        return value switch
        {
            '\t' => "\\t",
            '\n' => "\\n",
            '\v' => "\\v",
            '\f' => "\\f",
            '\r' => "\\r",
            _ when char.IsControl(value) => "U+" + ((int)value).ToString("X4"),
            _ => "'" + value + "'"
        };
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
