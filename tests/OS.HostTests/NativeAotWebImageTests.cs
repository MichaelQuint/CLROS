using System.Buffers.Binary;
using System.Text;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class NativeAotWebImageTests
{
    private static byte[] Image()
    {
        byte[] original = LinuxElfPackageTests.CapturePackage();
        LinuxElfPackageView view = LinuxElfPackage.Validate(original);
        return original.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).ToArray();
    }

    private static byte[] Package() => NativeAotWebPackage.Create(Image(), "{\"OS_SMOKE_EXPECTED_BODY\":\"OS_MINIMAL_API_OK\"}"u8);

    [HostTest("nativeaot.web image stages exact ELF manifest and immutable config bytes")]
    private static void Stage()
    {
        byte[] package = Package(), copied = new byte[package.Length], output = new byte[128];
        byte[] path = Encoding.ASCII.GetBytes("apps/appsettings.json");
        fixed (byte* source = package) fixed (byte* destination = copied) fixed (byte* name = path) fixed (byte* data = output)
        {
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.Copy(source, (uint)package.Length,
                destination, (uint)copied.Length, out var view));
            HostCheck.Equal(1u, view.DataFileCount); HostCheck.True(package.SequenceEqual(copied));
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.GetDataFile(destination, (uint)copied.Length, 0, out var file));
            HostCheck.Equal("apps/appsettings.json", Encoding.ASCII.GetString(copied.AsSpan((int)file.PathOffset, (int)file.PathBytes)));
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.ReadFile(destination, (uint)copied.Length,
                name, (uint)path.Length, 0, data, (uint)output.Length, out uint actual));
            HostCheck.Equal("{\"OS_SMOKE_EXPECTED_BODY\":\"OS_MINIMAL_API_OK\"}", Encoding.ASCII.GetString(output, 0, (int)actual));
            HostCheck.Equal(LinuxElfStatus.InvalidValue, LinuxElfPackage.GetDataFile(destination, (uint)copied.Length, 1, out _));
        }
    }

    [HostTest("nativeaot.web image rejects payload data digest overflow duplicate and legacy admission")]
    private static void RejectMalformed()
    {
        byte[] original = Package(); var view = LinuxElfPackage.Validate(original); int at = (int)view.DataTableOffset;
        void Reject(Action<byte[]> change, LinuxElfStatus expected)
        { byte[] bad = (byte[])original.Clone(); change(bad); HostCheck.Equal(expected, LinuxElfPackage.Validate(bad).Status); }
        Reject(bytes => bytes[(int)view.PayloadOffset + 300] ^= 1, LinuxElfStatus.DigestMismatch);
        Reject(bytes => bytes[^1] ^= 1, LinuxElfStatus.DigestMismatch);
        Reject(bytes => W32(bytes, 32, uint.MaxValue), LinuxElfStatus.InvalidLength);
        Reject(bytes => W32(bytes, at, uint.MaxValue), LinuxElfStatus.InvalidLength);
        Reject(bytes => W32(bytes, at + 4, uint.MaxValue), LinuxElfStatus.InvalidLength);
        Reject(bytes => W32(bytes, 36, 0), LinuxElfStatus.InvalidLength);
        Reject(bytes => bytes[0] = (byte)'O', LinuxElfStatus.InvalidHeader);
        Reject(bytes => W32(bytes, 8, 1), LinuxElfStatus.UnsupportedProfile);
        Reject(bytes => bytes[at + 48] = (byte)'/', LinuxElfStatus.InvalidPath);
        Reject(bytes => bytes[at + 12] = 1, LinuxElfStatus.InvalidHeader);
        int record = original.Length - at;
        byte[] duplicate = new byte[original.Length + record]; original.CopyTo(duplicate, 0);
        original.AsSpan(at).CopyTo(duplicate.AsSpan(original.Length));
        W32(duplicate, 32, (uint)(record * 2)); W32(duplicate, 36, 2);
        HostCheck.Equal(LinuxElfStatus.InvalidPath, LinuxElfPackage.Validate(duplicate).Status);
        foreach (byte[] missing in new[] { original[..at], original[..^1], Array.Empty<byte>() })
            HostCheck.Equal(LinuxElfStatus.InvalidLength, LinuxElfPackage.Validate(missing).Status);
    }

    [HostTest("nativeaot.web image builder rejects missing ELF malformed config and non-object config")]
    private static void BuilderFailures()
    {
        foreach ((byte[] elf, byte[] config) in new[] { (Array.Empty<byte>(), "{}"u8.ToArray()),
            (Image(), Array.Empty<byte>()), (Image(), "{"u8.ToArray()), (Image(), "[]"u8.ToArray()) })
        {
            bool rejected = false;
            try { NativeAotWebPackage.Create(elf, config); }
            catch (Exception error) when (error is InvalidDataException or System.Text.Json.JsonException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("nativeaot.web image corrupt data cannot publish and aliased reads preserve package bytes")]
    private static void NoPublicationOnFailure()
    {
        byte[] valid = Package(), corrupted = (byte[])valid.Clone(); corrupted[^1] ^= 1;
        byte[] destination = Enumerable.Repeat((byte)0xa5, valid.Length).ToArray();
        fixed (byte* bad = corrupted) fixed (byte* output = destination)
        {
            HostCheck.Equal(LinuxElfStatus.DigestMismatch, LinuxElfPackage.Copy(bad, (uint)corrupted.Length,
                output, (uint)destination.Length, out var rejected));
            HostCheck.True(!rejected.Succeeded && destination.All(value => value == 0xa5));
        }
        byte[] path = Encoding.ASCII.GetBytes("apps/appsettings.json");
        fixed (byte* package = valid) fixed (byte* name = path)
        {
            HostCheck.Equal(LinuxElfStatus.Success, LinuxElfPackage.GetDataFile(package, (uint)valid.Length, 0, out var file));
            byte[] before = (byte[])valid.Clone();
            HostCheck.Equal(LinuxElfStatus.InvalidDestination, LinuxElfPackage.ReadFile(package, (uint)valid.Length,
                name, (uint)path.Length, 0, package + file.DataOffset, 1, out uint actual));
            HostCheck.Equal(0u, actual); HostCheck.True(valid.SequenceEqual(before));
        }
    }

    private static void W32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
}
