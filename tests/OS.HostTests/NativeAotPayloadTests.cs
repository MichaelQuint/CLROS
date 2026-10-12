using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Tooling;

internal static class NativeAotPayloadTests
{
    // Independent ELF byte vector: one RX load, one RW load and optional TLS.
    private static byte[] Image(bool tls = true)
    {
        byte[] bytes = new byte[4096];
        "\u007fELF"u8.CopyTo(bytes); bytes[4] = 2; bytes[5] = 1; bytes[6] = 1;
        W16(bytes, 16, 2); W16(bytes, 18, 62); W32(bytes, 20, 1);
        W64(bytes, 24, 0x400200); W64(bytes, 32, 64);
        W16(bytes, 52, 64); W16(bytes, 54, 56); W16(bytes, 56, (ushort)(tls ? 3 : 2));
        Row(bytes, 64, 1, 5, 0, 0x400000, 1024, 1024, 4096);
        Row(bytes, 120, 1, 6, 1024, 0x401400, 512, 1024, 4096);
        if (tls) Row(bytes, 176, 7, 4, 1024, 0x401400, 16, 64, 16);
        return bytes;
    }

    [HostTest("nativeaot.payload static ELF loads entry TLS and input preservation")]
    private static void StaticImage()
    {
        byte[] image = Image(); byte[] original = image.ToArray();
        var inspected = NativeAotPayload.Inspect(image);
        HostCheck.Equal(0x400200UL, inspected.Entry);
        HostCheck.Equal(3, inspected.ProgramHeaderCount);
        HostCheck.Equal(2, inspected.Loads.Count);
        HostCheck.Equal(0x401400UL, inspected.Tls!.Address);
        HostCheck.Equal(64UL, inspected.Tls.MemorySize);
        HostCheck.Equal(Convert.ToHexString(SHA256.HashData(original)), inspected.Sha256);
        HostCheck.True(image.SequenceEqual(original));
        HostCheck.True(NativeAotPayload.Inspect(Image(false)).Tls is null);
    }

    [HostTest("nativeaot.payload rejects every truncated header and program table boundary")]
    private static void Truncation()
    {
        byte[] bytes = Image();
        for (int length = 0; length < 232; length++) Reject(bytes[..length]);
        Mutate(bytes, b => W64(b, 32, ulong.MaxValue));
        Mutate(bytes, b => W64(b, 32, 63));
        Mutate(bytes, b => W16(b, 56, 129));
        Mutate(bytes, b => W16(b, 56, 0));
        Mutate(bytes, b => W16(b, 54, 55));
    }

    [HostTest("nativeaot.payload rejects unsupported ABI architecture dynamic PIE and executable stack")]
    private static void Unsupported()
    {
        byte[] bytes = Image();
        foreach (int index in new[] { 0, 4, 5, 6, 7, 8, 9, 15 }) Mutate(bytes, b => b[index] = 0xff);
        Mutate(bytes, b => W16(b, 16, 3)); // ET_DYN/PIE
        Mutate(bytes, b => W16(b, 18, 183)); // AArch64
        Mutate(bytes, b => W32(b, 20, 2));
        Mutate(bytes, b => W32(b, 48, 1));
        Mutate(bytes, b => W32(b, 176, 3)); // PT_INTERP
        Mutate(bytes, b => W32(b, 176, 2)); // PT_DYNAMIC
        Mutate(bytes, b => { W32(b, 176, 0x6474e551); W32(b, 180, 7); });
    }

    [HostTest("nativeaot.payload checks file memory alignment W^X overlap and file-backed entry")]
    private static void LoadBounds()
    {
        byte[] bytes = Image();
        Mutate(bytes, b => W64(b, 72, ulong.MaxValue));
        Mutate(bytes, b => W64(b, 96, 1025));
        Mutate(bytes, b => W64(b, 104, 0));
        Mutate(bytes, b => W64(b, 80, ulong.MaxValue - 100));
        Mutate(bytes, b => W64(b, 80, 0));
        Mutate(bytes, b => W64(b, 80, 0x800000000000UL));
        Mutate(bytes, b => W64(b, 104, NativeAotPayload.MaximumMappedBytes + 1));
        Mutate(bytes, b => W64(b, 112, 3));
        Mutate(bytes, b => W64(b, 112, 0x800000));
        Mutate(bytes, b => W32(b, 68, 7));
        Mutate(bytes, b => W32(b, 68, 8));
        Mutate(bytes, b => W32(b, 68, 1));
        Mutate(bytes, b => W64(b, 136, 0x400000));
        Mutate(bytes, b => W64(b, 24, 0x401400));
        Mutate(bytes, b => W64(b, 24, 0x400400));
        Mutate(bytes, b => W64(b, 96, 0));
        byte[] zeroAlignment = Image(); W64(zeroAlignment, 112, 0);
        HostCheck.Equal(0UL, NativeAotPayload.Inspect(zeroAlignment).Loads[0].Alignment);
        byte[] pageOverlap = Image(false);
        W64(pageOverlap, 128, 2048); W64(pageOverlap, 136, 0x400800); W64(pageOverlap, 168, 1);
        Reject(pageOverlap); // Disjoint byte intervals still share an RX/RW page.
        byte[] incongruent = Image(false); W64(incongruent, 168, 1); W64(incongruent, 136, 0x401401);
        Reject(incongruent); // p_align=1 does not relax page-offset congruence.
    }

    [HostTest("nativeaot.payload TLS requires original load and initialized-byte association")]
    private static void TlsBounds()
    {
        byte[] bytes = Image();
        Mutate(bytes, b => W64(b, 192, 0x401900));
        Mutate(bytes, b => W64(b, 184, 1025));
        Mutate(bytes, b => W64(b, 216, 1025));
        Mutate(bytes, b => W64(b, 208, 65));
        Mutate(bytes, b => { W32(b, 120, 7); W32(b, 124, 4); });
    }

    [HostTest("nativeaot.payload CLI rejects malformed inputs and wrong builder without false success")]
    private static void CommandFailures()
    {
        using var output = new StringWriter();
        HostCheck.Equal(1, NativeAotPayload.RunAsync(".", ["unknown"], output).GetAwaiter().GetResult());
        HostCheck.True(output.ToString().Contains("TEST_FAIL m16-001") && !output.ToString().Contains("TEST_PASS m16-001"));
        if (!OperatingSystem.IsLinux())
        {
            output.GetStringBuilder().Clear();
            HostCheck.Equal(1, NativeAotPayload.RunAsync(".", ["qualify", "--output", "unused"], output).GetAwaiter().GetResult());
            HostCheck.True(output.ToString().Contains("linux_builder_required") && !Directory.Exists("unused"));
        }
    }

    private static void Mutate(byte[] source, Action<byte[]> edit)
    {
        byte[] bytes = source.ToArray(); edit(bytes); Reject(bytes);
    }
    private static void Reject(byte[] bytes)
    {
        byte[] original = bytes.ToArray();
        try { NativeAotPayload.Inspect(bytes); throw new InvalidOperationException("malformed ELF accepted"); }
        catch (InvalidDataException) { }
        HostCheck.True(bytes.SequenceEqual(original));
    }
    private static void Row(byte[] b, int at, uint type, uint flags, ulong offset, ulong address, ulong file, ulong memory, ulong align)
    {
        W32(b, at, type); W32(b, at + 4, flags); W64(b, at + 8, offset); W64(b, at + 16, address);
        W64(b, at + 32, file); W64(b, at + 40, memory); W64(b, at + 48, align);
    }
    private static void W16(byte[] b, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at), value);
    private static void W32(byte[] b, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), value);
    private static void W64(byte[] b, int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(at), value);
}
