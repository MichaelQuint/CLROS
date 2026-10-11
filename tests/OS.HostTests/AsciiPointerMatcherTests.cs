using System.Text;
using OS.Std.Text;

internal static unsafe class AsciiPointerMatcherTests
{
    [HostTest("std.ascii.pointer exact packed tokens and bounded safe relative paths")]
    private static void Tokens()
    {
        byte[] source = "os-native-manifest"u8.ToArray();
        fixed (byte* p = source)
        {
            HostCheck.True(AsciiPointerMatcher.Matches(p, 18, 18, 0x766974616e2d736fUL, 0x6566696e616d2d65UL, 0x7473UL));
            HostCheck.True(!AsciiPointerMatcher.Matches(p, 17, 18, 0));
            source[17] ^= 1;
            HostCheck.True(!AsciiPointerMatcher.Matches(p, 18, 18, 0x766974616e2d736fUL, 0x6566696e616d2d65UL, 0x7473UL));
        }
        HostCheck.True(AsciiPointerMatcher.Matches(null, 0, 0, 0));
        HostCheck.True(!AsciiPointerMatcher.Matches(null, 1, 1, 0));
        HostCheck.True(!AsciiPointerMatcher.Matches((byte*)(ulong.MaxValue - 1), 2, 2, 0));
        HostCheck.True(!AsciiPointerMatcher.Matches(null, 25, 25, 0));
        foreach (string path in new[] { "app", "bin/hello.app", new string('a', 255), "bin/a b" })
        {
            byte[] bytes = Encoding.ASCII.GetBytes(path);
            fixed (byte* p = bytes) HostCheck.True(AsciiPointerMatcher.IsRelativePath(p, (uint)bytes.Length));
        }
        foreach (string path in new[] { "", ".", "..", "bin/../a", "bin/./a", "/a", "a/", "a//b", "C:/a", "a\\b", "a\0b", new string('a', 256) })
        {
            byte[] bytes = Encoding.ASCII.GetBytes(path);
            fixed (byte* p = bytes) HostCheck.True(!AsciiPointerMatcher.IsRelativePath(p, (uint)bytes.Length));
        }
        HostCheck.True(!AsciiPointerMatcher.IsRelativePath(null, 1));
        HostCheck.True(!AsciiPointerMatcher.IsRelativePath((byte*)(ulong.MaxValue - 1), 2));
    }
}
