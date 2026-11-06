using System.Text;
using OS.Manifest;
using OS.Storage;

internal static unsafe class NativeManifestDiskTests
{
    internal const string Valid = "format=os-native-manifest\nversion=0\napp.name=Hello\napp.path=bin/hello.app\nargs=\nnetwork=none\nlog=serial\n";
    [HostTest("m06.manifest validates bounded v0 bytes against the existing host parser")]
    private static void ValidCases()
    {
        HostCheck.True(!default(LiveManifestResult).Succeeded);
        foreach (string ending in new[] { "\n", "\r\n" })
            foreach (bool newline in new[] { false, true })
            {
                string text = Valid.TrimEnd('\n').Replace("\n", ending) +
                    ending + "app.kind=package" + ending + "runtime.kind=native-aot" + (newline ? ending : "");
                byte[] bytes = Encoding.ASCII.GetBytes(text);
                var parsed = NativeManifestDisk.Validate(bytes);
                var host = NativeManifestParser.Parse(bytes);
                HostCheck.True(parsed.Succeeded && host.Succeeded);
                HostCheck.Equal((uint)bytes.Length, parsed.ByteCount);
                HostCheck.Equal(host.Manifest.AppPath.Offset, (int)parsed.AppPathOffset);
                HostCheck.Equal(host.Manifest.AppPath.Length, (int)parsed.AppPathLength);
                HostCheck.Equal("bin/hello.app", Encoding.ASCII.GetString(bytes.AsSpan((int)parsed.AppPathOffset, (int)parsed.AppPathLength)));
                HostCheck.Equal((byte)1, parsed.LogMode);
            }
    }
    [HostTest("m06.manifest rejects duplicates omissions malformed ASCII unsafe paths and unsupported fields")]
    private static void Rejections()
    {
        var cases = new (string Text, LiveManifestStatus Status)[]
        {
            ("", LiveManifestStatus.InvalidLength),
            (Valid + "app.name=Again\n", LiveManifestStatus.DuplicateField),
            (Valid.Replace("version=0\n", ""), LiveManifestStatus.MissingField),
            (Valid.Replace("version=0", "version=1"), LiveManifestStatus.InvalidValue),
            (Valid.Replace("format=os-native-manifest", "format=other"), LiveManifestStatus.InvalidValue),
            (Valid.Replace("app.path=bin/hello.app", "app.path=../hello.app"), LiveManifestStatus.InvalidPath),
            (Valid.Replace("app.path=bin/hello.app", "app.path=/bin/hello.app"), LiveManifestStatus.InvalidPath),
            (Valid.Replace("app.path=bin/hello.app", "app.path=C:/hello.app"), LiveManifestStatus.InvalidPath),
            (Valid.Replace("args=", "args"), LiveManifestStatus.MalformedLine),
            (Valid + "\n", LiveManifestStatus.MalformedLine),
            (Valid + "env.TEST=value\n", LiveManifestStatus.UnsupportedField),
            (Valid + "listen=0.0.0.0:80\n", LiveManifestStatus.UnsupportedField),
            (Valid.Replace("Hello", "He\0llo"), LiveManifestStatus.InvalidCharacter),
            (Valid.Replace("Hello", "He\tllo"), LiveManifestStatus.InvalidCharacter),
            (Valid.Replace("network=none", "network=other"), LiveManifestStatus.InvalidValue),
            (Valid + "runtime.kind=coreclr\n", LiveManifestStatus.InvalidValue)
        };
        foreach (var c in cases)
        {
            var parsed = NativeManifestDisk.Validate(Encoding.ASCII.GetBytes(c.Text));
            HostCheck.Equal(c.Status, parsed.Status);
            HostCheck.True(!parsed.Succeeded && parsed.AppPathOffset == 0 && parsed.ByteCount == 0);
        }
        HostCheck.Equal(LiveManifestStatus.InvalidLength, NativeManifestDisk.Validate(new byte[4097]).Status);
        HostCheck.Equal(LiveManifestStatus.InvalidLength, NativeManifestDisk.Validate(new Span<byte>(null, 10)).Status);
        HostCheck.Equal(LiveManifestStatus.InvalidLength, NativeManifestDisk.Validate(new Span<byte>((void*)unchecked((nuint)(ulong.MaxValue - 8)), 10)).Status);
        byte[] highBit = Encoding.ASCII.GetBytes(Valid);
        highBit[highBit.Length - 2] = 0xff;
        HostCheck.Equal(LiveManifestStatus.InvalidCharacter, NativeManifestDisk.Validate(highBit).Status);
    }
    [HostTest("m06.manifest preserves delimiter values and rejects field limits without truncating")]
    private static void Boundaries()
    {
        string text = Valid.Replace("app.name=Hello", "app.name=" + new string('A', 64))
            .Replace("app.path=bin/hello.app", "app.path=" + new string('p', 128))
            .Replace("args=", "args=" + new string('a', 255) + "=");
        var bytes = Encoding.ASCII.GetBytes(text);
        HostCheck.True(NativeManifestDisk.Validate(bytes).Succeeded && NativeManifestParser.Parse(bytes).Succeeded);
        HostCheck.Equal(256u, NativeManifestDisk.Validate(bytes).ArgsLength);
        HostCheck.Equal(LiveManifestStatus.InvalidValue, NativeManifestDisk.Validate(Encoding.ASCII.GetBytes(text.Replace("app.name=", "app.name=A"))).Status);
        HostCheck.Equal(LiveManifestStatus.InvalidPath, NativeManifestDisk.Validate(Encoding.ASCII.GetBytes(text.Replace("app.path=", "app.path=p"))).Status);
        HostCheck.Equal(LiveManifestStatus.InvalidValue, NativeManifestDisk.Validate(Encoding.ASCII.GetBytes(text.Replace("args=", "args=a"))).Status);
        foreach (string mode in new[] { "none", "dhcp", "static" })
            HostCheck.True(NativeManifestDisk.Validate(Encoding.ASCII.GetBytes(Valid.Replace("network=none", "network=" + mode))).Succeeded);
    }
}
