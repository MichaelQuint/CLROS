using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;

internal static unsafe class LinuxElfMappingTests
{
    [HostTest("m16-004 independent CR3 bridge bytes retain Win64 shadow space RBX and original root", HostLane.Fast, 2)]
    private static void BridgeBytes()
    {
        byte[] expected = [0xf3, 0x0f, 0x1e, 0xfa, 0x53, 0x48, 0x83, 0xec, 0x20,
            0x0f, 0x20, 0xdb, 0x0f, 0x22, 0xd9, 0x48, 0x89, 0xd1, 0x41, 0xff, 0xd0,
            0x0f, 0x22, 0xdb, 0x48, 0x83, 0xc4, 0x20, 0x5b, 0xc3, 0, 0,
            0xf3, 0x0f, 0x1e, 0xfa, 0x0f, 0x20, 0xd8, 0xc3];
        byte* storage = stackalloc byte[42];
        storage[0] = storage[41] = 0xa5;
        HostCheck.True(X64LinuxElfReadBackBridge.Emit(storage + 1, 40));
        HostCheck.True(new ReadOnlySpan<byte>(storage + 1, 40).SequenceEqual(expected));
        HostCheck.True(storage[0] == 0xa5 && storage[41] == 0xa5);
        for (uint i = 0; i < 40; i++)
        {
            storage[i + 1] ^= 1;
            HostCheck.True(!X64LinuxElfReadBackBridge.Validate(storage + 1, 40));
            storage[i + 1] ^= 1;
        }
        byte[] retained = new ReadOnlySpan<byte>(storage, 42).ToArray();
        HostCheck.True(!X64LinuxElfReadBackBridge.Emit(storage + 1, 39) && !X64LinuxElfReadBackBridge.Emit(null, 40));
        HostCheck.True(new ReadOnlySpan<byte>(storage, 42).SequenceEqual(retained));
    }

    private const uint Memory = 65536, Stack = 4096, Span = 16384;
    // Independent small ELF: R headers, RX code, an unmapped hole, RW TLS/data.
    private static byte[] Image()
    {
        byte[] b = new byte[12288];
        W32(b, 0, 0x464c457f);
        b[4] = 2;
        b[5] = b[6] = 1;
        W16(b, 16, 2);
        W16(b, 18, 62);
        W32(b, 20, 1);
        W64(b, 24, 0x401010);
        W64(b, 32, 64);
        W16(b, 52, 64);
        W16(b, 54, 56);
        W16(b, 56, 4);
        Row(b, 0, 1, 4, 0, 0x400000, 512, 512, 4096);
        Row(b, 1, 1, 5, 4096, 0x401000, 32, 128, 4096);
        Row(b, 2, 1, 6, 8192, 0x403000, 64, 512, 4096);
        Row(b, 3, 7, 4, 8192, 0x403000, 16, 32, 8);
        for (int i = 4096; i < 4128; i++)
            b[i] = (byte)(i - 4095);
        for (int i = 8192; i < 8256; i++)
            b[i] = (byte)(i - 8100);
        return b;
    }
    private static void W16(byte[] b, int o, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o), v);
    private static void W32(byte[] b, int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o), v);
    private static void W64(byte[] b, int o, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(o), v);
    private static void Row(byte[] b, int index, uint type, uint flags, ulong file, ulong address,
        ulong bytes, ulong memory, ulong alignment)
    {
        int o = 64 + index * 56;
        W32(b, o, type);
        W32(b, o + 4, flags);
        W64(b, o + 8, file);
        W64(b, o + 16, address);
        W64(b, o + 32, bytes);
        W64(b, o + 40, memory);
        W64(b, o + 48, alignment);
    }

    [HostTest("m16-004 materialization copies file bytes and clears BSS holes padding with zero allocation", HostLane.Fast, 0)]
    private static void CopyAndReadBack()
    {
        byte[] source = Image(), original = source.ToArray();
        byte* backing = (byte*)NativeMemory.AlignedAlloc(Span, 4096);
        try
        {
            new Span<byte>(backing, (int)Span).Fill(0xa5);
            fixed (byte* elf = source)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                var status = LinuxElfMapping.Materialize(elf, (uint)source.Length, Memory, Stack, backing, Span, out var plan);
                long allocations = GC.GetAllocatedBytesForCurrentThread() - before;
                HostCheck.Equal(LinuxElfMaterializeStatus.Ready, status);
                HostCheck.True(allocations == 0 && plan.ImageBytes == Span && plan.TlsMemoryBytes == 32);
                HostCheck.True(LinuxElfMapping.ValidateReadBack(elf, (uint)source.Length, Memory, Stack, plan, backing, Span));
                HostCheck.True(new ReadOnlySpan<byte>(backing, 512).SequenceEqual(source.AsSpan(0, 512)));
                HostCheck.True(new ReadOnlySpan<byte>(backing + 4096, 32).SequenceEqual(source.AsSpan(4096, 32)));
                HostCheck.True(new ReadOnlySpan<byte>(backing + 12288, 64).SequenceEqual(source.AsSpan(8192, 64)));
                foreach (uint offset in new uint[] { 512, 4128, 8192, 12352, Span - 1 })
                    HostCheck.Equal((byte)0, backing[offset]);
                foreach (uint offset in new uint[] { 0, 4096, 8192, 12288, Span - 1 })
                {
                    backing[offset] ^= 1;
                    HostCheck.True(!LinuxElfMapping.ValidateReadBack(elf, (uint)source.Length, Memory, Stack, plan, backing, Span));
                    backing[offset] ^= 1;
                }
                var forged = plan;
                forged.Loads[2]++;
                HostCheck.True(!LinuxElfMapping.ValidateReadBack(elf, (uint)source.Length, Memory, Stack, forged, backing, Span));
                HostCheck.True(!LinuxElfMapping.ValidateReadBack(elf, 63, Memory, Stack, plan, backing, Span));
            }
            HostCheck.True(source.AsSpan().SequenceEqual(original));
        }
        finally { NativeMemory.AlignedFree(backing); }
    }

    [HostTest("m16-004 invalid ELF short misaligned aliased backing rejects before writes and explicit rollback clears span", HostLane.Fast, 1)]
    private static void Rejections()
    {
        byte[] source = Image();
        byte* backing = (byte*)NativeMemory.AlignedAlloc(Span, 4096);
        try
        {
            new Span<byte>(backing, (int)Span).Fill(0xa5);
            fixed (byte* elf = source)
            {
                foreach (uint bytes in new uint[] { 0, Span - 4096, Span + 4096 })
                {
                    HostCheck.Equal(LinuxElfMaterializeStatus.InvalidStorage,
                        LinuxElfMapping.Materialize(elf, (uint)source.Length, Memory, Stack, backing, bytes, out var rejected));
                    HostCheck.True(!rejected.Succeeded && rejected.ImageBytes == 0);
                }
                HostCheck.Equal(LinuxElfMaterializeStatus.InvalidStorage,
                    LinuxElfMapping.Materialize(elf, (uint)source.Length, Memory, Stack, backing + 1, Span, out _));
                HostCheck.Equal(LinuxElfMaterializeStatus.InvalidElf,
                    LinuxElfMapping.Materialize(elf, 63, Memory, Stack, backing, Span, out _));
                for (uint i = 0; i < Span; i++)
                    HostCheck.Equal((byte)0xa5, backing[i]);
                source.CopyTo(new Span<byte>(backing, source.Length));
                byte[] retained = new ReadOnlySpan<byte>(backing, (int)Span).ToArray();
                HostCheck.Equal(LinuxElfMaterializeStatus.OverlappingStorage,
                    LinuxElfMapping.Materialize(backing, (uint)source.Length, Memory, Stack, backing, Span, out _));
                HostCheck.True(new ReadOnlySpan<byte>(backing, (int)Span).SequenceEqual(retained));
            }
            HostCheck.True(LinuxElfMapping.Clear(backing, Span));
            for (uint i = 0; i < Span; i++)
                HostCheck.Equal((byte)0, backing[i]);
            HostCheck.True(!LinuxElfMapping.Clear(null, Span) && !LinuxElfMapping.Clear(backing, 0));
        }
        finally { NativeMemory.AlignedFree(backing); }
    }
}
