using System.Buffers.Binary;
using OS.Kernel.Compatibility.LinuxX64;

internal static unsafe class LinuxElfLoaderTests
{
    private const ulong Base = 0x400000;
    private static void W16(byte[] b, int o, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(o), value);
    private static void W32(byte[] b, int o, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o), value);
    private static void W64(byte[] b, int o, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(o), value);
    private static int Row(int index) => 64 + index * 56;
    private static void Segment(byte[] b, int index, uint type, uint flags, ulong offset,
        ulong address, ulong fileBytes, ulong memoryBytes, ulong alignment)
    {
        int at = Row(index); W32(b, at, type); W32(b, at + 4, flags);
        W64(b, at + 8, offset); W64(b, at + 16, address);
        W64(b, at + 32, fileBytes); W64(b, at + 40, memoryBytes); W64(b, at + 48, alignment);
    }
    private static byte[] Image()
    {
        byte[] b = new byte[12288];
        W32(b, 0, 0x464c457f); b[4] = 2; b[5] = 1; b[6] = 1;
        W16(b, 16, 2); W16(b, 18, 62); W32(b, 20, 1); W64(b, 24, Base + 4096 + 32);
        W64(b, 32, 64); W16(b, 52, 64); W16(b, 54, 56); W16(b, 56, 8);
        Segment(b, 0, 6, 4, 64, Base + 64, 8 * 56, 8 * 56, 8);
        Segment(b, 1, 1, 4, 0, Base, 1024, 1024, 4096);
        Segment(b, 2, 1, 5, 4096, Base + 4096, 1024, 4096, 4096);
        Segment(b, 3, 1, 6, 8192, Base + 8192, 256, 4096, 4096);
        Segment(b, 4, 7, 4, 8192, Base + 8192, 32, 64, 8);
        Segment(b, 5, 0x6474e551, 6, 0, 0, 0, 0, 16);
        Segment(b, 6, 0x6474e552, 4, 8192, Base + 8192, 128, 128, 1);
        Segment(b, 7, 0x6474e553, 4, 512, Base + 512, 32, 32, 8);
        W32(b, 512, 4); W32(b, 516, 16); W32(b, 520, 5); W32(b, 524, 0x00554e47);
        W32(b, 528, 1); W32(b, 532, 8); W64(b, 536, 4096);
        b[4096 + 32] = 0xc3; b[8192] = 0x5a;
        return b;
    }
    private static LinuxElfLoadPlan Validate(byte[] bytes, uint? length = null, uint memory = 1024 * 1024, uint stack = 4096)
    {
        fixed (byte* source = bytes)
        {
            var status = LinuxElfLoader.Validate(source, length ?? (uint)bytes.Length, memory, stack, out var plan);
            HostCheck.Equal(status, plan.Status);
            return plan;
        }
    }
    private static void Rejected(byte[] bytes, LinuxElfLoadStatus expected, uint? length = null)
    {
        byte[] original = bytes.ToArray();
        var plan = Validate(bytes, length);
        HostCheck.Equal(expected, plan.Status);
        HostCheck.True(!plan.Succeeded && plan.SourceBytes == 0 && plan.ProgramHeaderCount == 0 && plan.LoadCount == 0 &&
            plan.ImageStart == 0 && plan.ImageBytes == 0 && plan.Entry == 0 && plan.ProgramHeaders == 0 &&
            plan.TlsAddress == 0 && plan.TlsFileBytes == 0 && plan.TlsMemoryBytes == 0 && plan.TlsAlignment == 0);
        for (int i = 0; i < 80; i++) HostCheck.Equal(0UL, plan.Loads[i]);
        HostCheck.True(bytes.AsSpan().SequenceEqual(original));
    }

    [HostTest("m16-004 independent ELF load TLS PHDR and stack-note geometry", HostLane.Fast, 0)]
    private static void Geometry()
    {
        var plan = Validate(Image());
        HostCheck.True(plan.Succeeded && plan.SourceBytes == 12288 && plan.ProgramHeaderCount == 8 && plan.LoadCount == 3);
        HostCheck.True(plan.ImageStart == Base && plan.ImageBytes == 12288 && plan.Entry == Base + 4128 && plan.ProgramHeaders == Base + 64);
        HostCheck.True(plan.TlsAddress == Base + 8192 && plan.TlsFileBytes == 32 && plan.TlsMemoryBytes == 64 && plan.TlsAlignment == 8);
        HostCheck.True(plan.Loads[0] == Base && plan.Loads[4] == 4 && plan.Loads[5] == Base + 4096 && plan.Loads[9] == 5 &&
            plan.Loads[10] == Base + 8192 && plan.Loads[11] == 8192 && plan.Loads[12] == 256 && plan.Loads[13] == 4096 && plan.Loads[14] == 6);
        byte[] noTls = Image(); W32(noTls, Row(4), 0);
        HostCheck.True(Validate(noTls).Succeeded && Validate(noTls).TlsMemoryBytes == 0);
        byte[] noExplicitHeaders = Image(); W32(noExplicitHeaders, Row(0), 0);
        HostCheck.True(Validate(noExplicitHeaders).ProgramHeaders == Base + 64);
        byte[] physical = Image(); W64(physical, Row(2) + 24, ulong.MaxValue);
        HostCheck.True(Validate(physical).Succeeded); // ELF p_paddr never confers ownership.
    }

    [HostTest("m16-004 malformed identity short source and overflowing program tables reject atomically", HostLane.Fast, 1)]
    private static void Headers()
    {
        Rejected(Image(), LinuxElfLoadStatus.InvalidSource, 63);
        for (int offset = 4; offset <= 9; offset++)
        {
            byte[] b = Image(); b[offset] = 9;
            Rejected(b, LinuxElfLoadStatus.InvalidIdentification);
        }
        byte[] image = Image(); W16(image, 16, 3); Rejected(image, LinuxElfLoadStatus.InvalidHeader);
        image = Image(); W64(image, 32, ulong.MaxValue); Rejected(image, LinuxElfLoadStatus.InvalidProgramTable);
        image = Image(); W16(image, 56, 129); Rejected(image, LinuxElfLoadStatus.InvalidProgramTable);
        image = Image(); W16(image, 54, 55); Rejected(image, LinuxElfLoadStatus.InvalidHeader);
        image = Image(); W32(image, 48, 1); Rejected(image, LinuxElfLoadStatus.InvalidHeader);
    }

    [HostTest("m16-004 unsupported interpreter dynamic image executable stack and unknown headers reject", HostLane.Fast, 2)]
    private static void Unsupported()
    {
        foreach (var pair in new[] { (3U, LinuxElfLoadStatus.InterpreterRequired), (2U, LinuxElfLoadStatus.DynamicLinking),
            (0x76543210U, LinuxElfLoadStatus.UnknownProgramType) })
        {
            byte[] b = Image(); W32(b, Row(7), pair.Item1); Rejected(b, pair.Item2);
        }
        byte[] stack = Image(); W32(stack, Row(5) + 4, 7); Rejected(stack, LinuxElfLoadStatus.ExecutableStack);
    }

    [HostTest("m16-004 overflow file size page overlap alignment and entry permissions reject", HostLane.Fast, 3)]
    private static void Loads()
    {
        byte[] b = Image(); W64(b, Row(3) + 8, ulong.MaxValue); Rejected(b, LinuxElfLoadStatus.InvalidFileExtent);
        b = Image(); W64(b, Row(2) + 32, 4097); Rejected(b, LinuxElfLoadStatus.InvalidLoad);
        b = Image(); W32(b, Row(2) + 4, 7); Rejected(b, LinuxElfLoadStatus.InvalidLoad);
        b = Image(); W64(b, Row(3) + 16, Base + 4096); Rejected(b, LinuxElfLoadStatus.OverlappingLoads);
        b = Image(); W64(b, Row(3) + 16, Base + 4096 + 3072); W64(b, Row(3) + 8, 8192 + 3072);
        W64(b, Row(2) + 40, 1024); W64(b, Row(3) + 40, 512); Rejected(b, LinuxElfLoadStatus.OverlappingLoads);
        b = Image(); W64(b, Row(2) + 48, 3); Rejected(b, LinuxElfLoadStatus.InvalidAlignment);
        b = Image(); W64(b, Row(2) + 16, Base + 4097); Rejected(b, LinuxElfLoadStatus.InvalidAlignment);
        b = Image(); W64(b, 24, Base + 8192); Rejected(b, LinuxElfLoadStatus.InvalidEntry);
        b = Image(); W64(b, 24, Base + 4096 + 1024); Rejected(b, LinuxElfLoadStatus.InvalidEntry);
        b = Image(); W64(b, Row(3) + 40, ulong.MaxValue); Rejected(b, LinuxElfLoadStatus.InvalidLoad);
        b = Image(); Rejected(b, LinuxElfLoadStatus.InvalidFileExtent, 8191);
    }

    [HostTest("m16-004 metadata aliases TLS geometry and PHDR disagreement reject", HostLane.Fast, 4)]
    private static void Metadata()
    {
        byte[] b = Image(); W64(b, Row(0) + 16, Base + 65); Rejected(b, LinuxElfLoadStatus.InvalidProgramHeaders);
        b = Image(); W64(b, Row(4) + 48, 3); Rejected(b, LinuxElfLoadStatus.InvalidTls);
        b = Image(); W64(b, Row(4) + 32, 65); Rejected(b, LinuxElfLoadStatus.InvalidTls);
        b = Image(); W64(b, Row(4) + 16, Base + 12288); Rejected(b, LinuxElfLoadStatus.InvalidTls);
        b = Image(); W32(b, Row(7), 7); Rejected(b, LinuxElfLoadStatus.DuplicateMetadata);
        b = Image(); W64(b, Row(6) + 16, Base + 12288); Rejected(b, LinuxElfLoadStatus.InvalidRelro);
        b = Image(); W64(b, Row(1) + 32, 64); Rejected(b, LinuxElfLoadStatus.ProgramHeadersNotMapped);
    }

    [HostTest("m16-004 GNU properties reject malformed notes unsupported features and excessive stack demand", HostLane.Fast, 5)]
    private static void Properties()
    {
        byte[] b = Image(); W32(b, 520, 4); Rejected(b, LinuxElfLoadStatus.InvalidProperties);
        b = Image(); W32(b, 516, 17); Rejected(b, LinuxElfLoadStatus.InvalidProperties);
        b = Image(); W32(b, 532, uint.MaxValue); Rejected(b, LinuxElfLoadStatus.InvalidProperties);
        b = Image(); W64(b, 536, 4097); Rejected(b, LinuxElfLoadStatus.InvalidProperties);
        b = Image(); W32(b, 528, 0xc0008002); W32(b, 532, 4); W64(b, 536, 1);
        HostCheck.True(Validate(b).Succeeded);
        W32(b, 536, 2); Rejected(b, LinuxElfLoadStatus.InvalidProperties);
        b = Image(); W32(b, 528, 0xc0000002); W32(b, 532, 4); W64(b, 536, 0);
        HostCheck.True(Validate(b).Succeeded);
        b[540] = 1; Rejected(b, LinuxElfLoadStatus.InvalidProperties);
    }

    [HostTest("m16-004 limits large sparse images and excess load count reject", HostLane.Fast, 6)]
    private static void Limits()
    {
        byte[] b = Image(); HostCheck.Equal(LinuxElfLoadStatus.InvalidResourceLimits, Validate(b, stack: 0).Status);
        HostCheck.Equal(LinuxElfLoadStatus.InvalidResourceLimits, Validate(b, memory: uint.MaxValue).Status);
        HostCheck.Equal(LinuxElfLoadStatus.MemoryLimitExceeded, Validate(b, memory: 8192).Status);
        W64(b, Row(3) + 16, 0x08000000); Rejected(b, LinuxElfLoadStatus.InvalidImageSpan);
        b = Image(); W16(b, 56, 19); W64(b, 24, Base + 32);
        Segment(b, 0, 6, 4, 64, Base + 64, 19 * 56, 19 * 56, 8);
        for (int i = 0; i < 18; i++) Segment(b, i + 1, 1, i == 0 ? 5U : 4U, 0, Base + (ulong)i * 4096, i == 0 ? 2048UL : 0, 4096, 4096);
        Rejected(b, LinuxElfLoadStatus.TooManyLoads);
    }

    [HostTest("m16-004 geometry validation allocates no managed storage", HostLane.Fast, 7)]
    private static void NoAllocation()
    {
        byte[] b = Image(); fixed (byte* p = b)
        {
            LinuxElfLoader.Validate(p, (uint)b.Length, 1024 * 1024, 4096, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var status = LinuxElfLoader.Validate(p, (uint)b.Length, 1024 * 1024, 4096, out var plan);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            HostCheck.True(status == LinuxElfLoadStatus.Ready && plan.Succeeded && bytes == 0);
        }
    }
}
