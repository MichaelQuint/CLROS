using System.Buffers.Binary;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class LinuxMemoryProofFixtureTests
{
    [HostTest("m16-006 mechanical memory packages have independently checked static ELF headers and digest rejection", HostLane.Fast, 0)]
    private static void PackageGeometry()
    {
        foreach (string variant in LinuxMemoryProofFixture.Variants)
        {
            byte[] package = LinuxMemoryProofFixture.CreatePackage(variant);
            var view = LinuxElfPackage.Validate(package); HostCheck.True(view.Succeeded);
            ReadOnlySpan<byte> elf = package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes);
            HostCheck.True(elf[..7].SequenceEqual(new byte[] { 0x7f, 0x45, 0x4c, 0x46, 2, 1, 1 }));
            HostCheck.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(elf[16..]));
            HostCheck.Equal((ushort)62, BinaryPrimitives.ReadUInt16LittleEndian(elf[18..]));
            HostCheck.Equal(0x400200UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[24..]));
            HostCheck.Equal(64UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[32..]));
            HostCheck.Equal((ushort)56, BinaryPrimitives.ReadUInt16LittleEndian(elf[54..]));
            HostCheck.Equal((ushort)3, BinaryPrimitives.ReadUInt16LittleEndian(elf[56..]));
            HostCheck.Equal(5U, BinaryPrimitives.ReadUInt32LittleEndian(elf[68..]));
            HostCheck.Equal(6U, BinaryPrimitives.ReadUInt32LittleEndian(elf[124..]));
            HostCheck.Equal(4096UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[96..]));
            HostCheck.Equal(8192UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[152..]));
            HostCheck.Equal(0x6474e551U, BinaryPrimitives.ReadUInt32LittleEndian(elf[176..]));
            fixed (byte* bytes = package)
            {
                HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(bytes + view.PayloadOffset, view.PayloadBytes,
                    view.Manifest.MemoryBytes, view.Manifest.StackBytes, out var plan));
                HostCheck.Equal(2U, plan.LoadCount); HostCheck.Equal(12288UL, plan.ImageBytes);
                HostCheck.Equal(0x400040UL, plan.ProgramHeaders); HostCheck.Equal(0UL, plan.TlsMemoryBytes);
            }
            HostCheck.True(package.AsSpan().SequenceEqual(LinuxMemoryProofFixture.CreatePackage(variant)));
            byte[] corrupt = (byte[])package.Clone(); corrupt[^1] ^= 1;
            HostCheck.True(!LinuxElfPackage.Validate(corrupt).Succeeded);
        }
        bool rejected = false;
        try { _ = LinuxMemoryProofFixture.CreateElf("Hello"); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("m16-006 mechanical memory inventory preserves measured reservation six argument mmap and bounded startup calls", HostLane.Fast, 1)]
    private static void Inventory()
    {
        byte[] elf = LinuxMemoryProofFixture.CreateElf("Memory");
        ReadOnlySpan<byte> code = elf.AsSpan(0x200, 0xd00);
        ulong[] calls = Calls(code);
        ulong[] expected = [9, 10, 10, 10, 9, 10, 1, 11, 10, 10, 9, 9, 9, 28, 28, 204, 99, 324, 324, 324, 228, 228, 318, 318, 318, 12, 12, 12, 12, 25, 25, 11, 11, 231];
        HostCheck.True(calls.SequenceEqual(expected) && calls.Length == 34 && calls.Length <= 64);
        HostCheck.True(code[..70].SequenceEqual(new byte[] { 0x48, 0xb8, 9, 0, 0, 0, 0, 0, 0, 0,
            0x48, 0xbf, 0, 0, 0, 0, 0, 0, 0, 0,
            0x48, 0xbe, 0, 0xf0, 0xcc, 0xea, 1, 0, 0, 0,
            0x48, 0xba, 0, 0, 0, 0, 0, 0, 0, 0,
            0x49, 0xba, 0x22, 0, 0, 0, 0, 0, 0, 0,
            0x49, 0xb8, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
            0x49, 0xb9, 0, 0, 0, 0, 0, 0, 0, 0 }));
        HostCheck.True(code.IndexOf(new byte[] { 0xf3, 0x48, 0xaf }) >= 0 && code.IndexOf(new byte[] { 0xfc, 0xf3, 0xa4 }) >= 0);
        HostCheck.True(code.IndexOf(new byte[] { 0x48, 0x89, 0xe7, 0x48, 0x81, 0xe7, 0, 0xf0, 0xff, 0xff }) >= 0);
        HostCheck.True(code.IndexOf(new byte[] { 0x48, 0x85, 0xc0, 0x0f, 0x84 }) >= 0);
        HostCheck.True(code.IndexOf(new byte[] { 0x48, 0xb8, 0x3e, 1, 0, 0, 0, 0, 0, 0,
            0x48, 0xbf, 0x80, 0x10, 0, 0, 1, 0, 0, 0 }) >= 0);
        byte[] witness = Encoding.ASCII.GetBytes(LinuxMemoryProofFixture.Witness);
        HostCheck.Equal(24, witness.Length);
        HostCheck.True(elf.AsSpan(0x1200, witness.Length).SequenceEqual(witness));
        ulong highBuffer = 0x100002000 - 8;
        HostCheck.True(highBuffer / 4096 != (highBuffer + (ulong)witness.Length - 1) / 4096);
        HostCheck.True(Calls(elf.AsSpan(0xf00, 80)).SequenceEqual(new ulong[] { 231 }));
    }

    [HostTest("m16-006 controlled faults follow actual sparse ownership and target distinct absent leaves", HostLane.Fast, 2)]
    private static void NegativeVariants()
    {
        foreach (string variant in new[] { "GuardFault", "ProtNoneFault" })
        {
            byte[] elf = LinuxMemoryProofFixture.CreateElf(variant);
            ReadOnlySpan<byte> code = elf.AsSpan(0x200, 0xd00);
            HostCheck.True(Calls(code).SequenceEqual(variant == "GuardFault" ? new ulong[] { 9, 10 } : new ulong[] { 9 }));
            ulong address = variant == "GuardFault" ? 0x100000000UL : 0x100001000UL;
            byte[] fault = new byte[13]; fault[0] = 0x48; fault[1] = 0xb8;
            BinaryPrimitives.WriteUInt64LittleEndian(fault.AsSpan(2), address);
            new byte[] { 0x48, 0x8b, 0 }.CopyTo(fault, 10);
            HostCheck.True(code.IndexOf(fault) > code.IndexOf(new byte[] { 0x0f, 5 }));
            byte[] positive = LinuxMemoryProofFixture.CreateElf("Memory");
            HostCheck.True(!positive.AsSpan().SequenceEqual(elf));
        }
    }

    [HostTest("m16-006 heap witness executes mixed ownership and foreign readonly write challenge", HostLane.Fast, 3)]
    private static void HeapOwnershipVariants()
    {
        byte[] positive = LinuxMemoryProofFixture.CreateElf("HeapOwnership");
        byte[] negative = LinuxMemoryProofFixture.CreateElf("HeapOwnershipFault");
        HostCheck.True(Calls(positive.AsSpan(0x200, 0xd00)).SequenceEqual(new ulong[]
            { 9,12,12,10,12,12,12,12,9,10,12,9,12,11,12,9,12,1,9,12,11,231 }));
        HostCheck.True(Calls(negative.AsSpan(0x200, 0xd00)).SequenceEqual(new ulong[] { 9,12,9,10,12,12 }));
        HostCheck.True(positive.AsSpan(0x200, 0xd00).IndexOf(new byte[] { 0xf3,0x48,0xaf }) >= 0);
        // The terminal challenge is a real store of a different sentinel after
        // all six syscalls. The same store before mprotect cannot qualify it.
        int store = negative.AsSpan(0x200, 0xd00).LastIndexOf(new byte[] { 0x48,0x89,0x10 });
        int lastCall = negative.AsSpan(0x200, 0xd00).LastIndexOf(new byte[] { 0x0f,5 });
        HostCheck.True(store > lastCall);
        HostCheck.Equal(0xa5UL, BinaryPrimitives.ReadUInt64LittleEndian(negative.AsSpan(0x200 + store - 8)));
        HostCheck.True(Calls(negative.AsSpan(0xf00, 80)).SequenceEqual(new ulong[] { 231 }));
        HostCheck.True(!positive.AsSpan().SequenceEqual(negative));
    }

    [HostTest("m16.015 fragmentation ELF fills the real mechanical pool and crosses discontiguous backing before cleanup")]
    private static void FragmentedBacking()
    {
        byte[] elf = LinuxMemoryProofFixture.CreateElf("FragmentedBacking");
        HostCheck.True(Calls(elf.AsSpan(0x200, 0xd00)).SequenceEqual(new ulong[]
            { 9,9,9,9,9,11,11,9,10,28,10,1,12,12,10,9,11,231 }));
        HostCheck.Equal(18U, LinuxMemoryProofFixture.FragmentedBackingCalls);
        // The witness contains real full-region fill/compare instructions and
        // the cross-extent pointer, independent of synthetic transcript claims.
        HostCheck.True(elf.AsSpan(0x200, 0xd00).IndexOf(new byte[] { 0xf3,0x48,0xab }) >= 0);
        HostCheck.True(elf.AsSpan(0x200, 0xd00).IndexOf(new byte[] { 0xf3,0x48,0xaf }) >= 0);
        byte[] boundary = new byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(boundary, 0x1023ffff8);
        HostCheck.True(elf.AsSpan(0x200, 0xd00).IndexOf(boundary) >= 0);
        HostCheck.True(Calls(elf.AsSpan(0xf00, 80)).SequenceEqual(new ulong[] { 231 }));
        HostCheck.True(!elf.AsSpan().SequenceEqual(LinuxMemoryProofFixture.CreateElf("Memory")));
    }

    // Independent small inventory reader: only MOV RAX,imm64 identifies syscall
    // numbers; literal data and the separate exit-99 block are outside the span.
    private static ulong[] Calls(ReadOnlySpan<byte> code)
    {
        List<ulong> calls = []; ulong number = ulong.MaxValue;
        for (int i = 0; i + 1 < code.Length; i++)
        {
            if (code[i] == 0x48 && code[i + 1] == 0xb8 && i + 10 <= code.Length)
            { number = BinaryPrimitives.ReadUInt64LittleEndian(code[(i + 2)..]); i += 9; }
            else if (code[i] == 0x0f && code[i + 1] == 5) { calls.Add(number); i++; }
        }
        return calls.ToArray();
    }
}
