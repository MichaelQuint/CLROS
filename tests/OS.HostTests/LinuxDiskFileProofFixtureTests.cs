using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe class LinuxDiskFileProofFixtureTests
{
    [HostTest("disk mechanical ELF admits separate executable and writable bounds", HostLane.Fast, 0)]
    private static void LoadGeometry()
    {
        byte[] package = LinuxDiskFileProofFixture.CreatePackage();
        var view = LinuxElfPackage.Validate(package);
        HostCheck.True(view.Succeeded && view.DataFileCount == 0);
        HostCheck.Equal((ulong)LinuxDiskFileProofFixture.ElfBytes, view.PayloadBytes);
        HostCheck.True(package.AsSpan().SequenceEqual(LinuxDiskFileProofFixture.CreatePackage()));
        fixed (byte* bytes = package)
        {
            HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(bytes + view.PayloadOffset,
                view.PayloadBytes, view.Manifest.MemoryBytes, view.Manifest.StackBytes, out var plan));
            HostCheck.True(plan.LoadCount == 2 && plan.Entry == 0x400200 && plan.ImageBytes == 0x7000);
        }
        var elf = package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes);
        HostCheck.Equal(5U, BinaryPrimitives.ReadUInt32LittleEndian(elf[68..]));
        HostCheck.Equal(6U, BinaryPrimitives.ReadUInt32LittleEndian(elf[124..]));
        HostCheck.Equal(4096UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[96..]));
        HostCheck.Equal(6UL * 4096, BinaryPrimitives.ReadUInt64LittleEndian(elf[152..]));
    }

    [HostTest("disk mechanical package admission rejects changed executable and policy", HostLane.Fast, 1)]
    private static void CanonicalEnvelope()
    {
        byte[] package = LinuxDiskFileProofFixture.CreatePackage();
        LinuxDiskFileProofFixture.RequirePackage(package);
        byte[] changed = (byte[])package.Clone(); changed[^1] ^= 1;
        Reject(changed);
        Reject(package.AsSpan(0, package.Length - 1));
        Reject(LinuxThreadProofFixture.CreatePackage("Threads"));
        Reject([]);

        static void Reject(ReadOnlySpan<byte> input)
        {
            bool rejected = false;
            try { LinuxDiskFileProofFixture.RequirePackage(input); }
            catch (ToolFailure failure) when (failure.Reason == "m16_025_fixture_package_identity") { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("disk mechanical syscall continuations exclude assertion-failure exit", HostLane.Fast, 2)]
    private static void Continuations()
    {
        byte[] elf = LinuxDiskFileProofFixture.CreateElf();
        var observed = LinuxDiskFileProofFixture.ExpectedReturnRips();
        HostCheck.True(observed.Count is > 40 and <= 128 && observed.Distinct().Count() == observed.Count);
        HostCheck.Equal((uint)observed.Count, LinuxDiskFileProofFixture.ExpectedCalls);
        foreach (ulong rip in observed)
        {
            int offset = checked((int)(rip - LinuxDiskFileProofFixture.ImageBase));
            HostCheck.True(offset > LinuxDiskFileProofFixture.EntryOffset && offset < LinuxDiskFileProofFixture.FailureOffset);
            HostCheck.True(elf[offset - 2] == 0x0f && elf[offset - 1] == 5);
        }
        HostCheck.Equal(observed[^1], LinuxDiskFileProofFixture.SuccessRip);
        HostCheck.True(!observed.Contains(LinuxDiskFileProofFixture.FailureExitRip));
        HostCheck.True(elf.AsSpan().IndexOf("/app/wwwroot/../../private/sentinel.txt\0"u8) >= 4096);
        HostCheck.True(elf.AsSpan().IndexOf("/app/wwwroot/fixture/fragmented.bin\0"u8) >= 4096);
    }

    [HostTest("disk mechanical instruction inventory matches independent decoder and LLVM22 boundaries", HostLane.Fast, 3)]
    private static void Instructions()
    {
        byte[] elf = LinuxDiskFileProofFixture.CreateElf();
        var starts = LinuxDiskFileProofFixture.MainInstructionRips();
        ulong[] decoded = DecodeMain(elf);
        HostCheck.True(starts.SequenceEqual(decoded));
        // Independently retained LLVM22.1.3 disassembly of the original exact
        // PT_LOAD bytes: little-endian u64 starts, ending at the final JMP.
        HostCheck.Equal(481, starts.Count);
        HostCheck.Equal("7DBB30C2F91FE47919D8849CC45B16175C5C25E6A8230BBC7E43984B132FD1FC", RipsHash(starts));
        HostCheck.Equal(LinuxDiskFileProofFixture.ImageBase + LinuxDiskFileProofFixture.EntryOffset, starts[0]);
        HostCheck.Equal(LinuxDiskFileProofFixture.SuccessRip, starts[^1]);
        HostCheck.True(starts.Distinct().Count() == starts.Count && starts.Zip(starts.Skip(1)).All(p => p.First < p.Second));
        foreach (ulong rip in LinuxDiskFileProofFixture.ExpectedReturnRips()) HostCheck.True(starts.Contains(rip));
        HostCheck.True(starts.All(r => r < LinuxDiskFileProofFixture.ImageBase + LinuxDiskFileProofFixture.FailureOffset));
        HostCheck.True(!starts.Contains(LinuxDiskFileProofFixture.FailureExitRip));
        var set = starts.ToHashSet();
        for (int i = 0; i < decoded.Length; i++)
        {
            ulong next = i + 1 == decoded.Length ? LinuxDiskFileProofFixture.SuccessRip + 5 : decoded[i + 1];
            for (ulong interior = decoded[i] + 1; interior < next; interior++) HostCheck.True(!set.Contains(interior));
        }
        // Dropped instructions, duplicate entries and a REX/immediate fragment
        // must not accidentally equal the independently decoded exact vector.
        HostCheck.True(!starts.Skip(1).SequenceEqual(decoded));
        HostCheck.True(!starts.Append(starts[^1]).SequenceEqual(decoded));
        ulong[] fragment = starts.ToArray(); fragment[1]++; HostCheck.True(!fragment.SequenceEqual(decoded));
    }

    [HostTest("disk mechanical metadata preserves original executable package continuations and immutable results", HostLane.Fast, 4)]
    private static void UnchangedBytes()
    {
        byte[] elf = LinuxDiskFileProofFixture.CreateElf(), package = LinuxDiskFileProofFixture.CreatePackage();
        HostCheck.Equal("2AC396B35C5A7FA292BDACB2BECA14C7D8747A89D65C4FDE786227769CE2B2A8", Convert.ToHexString(SHA256.HashData(elf)));
        HostCheck.Equal("31984EBA9958DD1AB644C835297EE40BD1075D235DC491071D068575CBDE9579", Convert.ToHexString(SHA256.HashData(package)));
        HostCheck.Equal("396C6736520491061919E255D4AB85030A50DD7C4338B67A54F0BC7E34B50B4F", RipsHash(LinuxDiskFileProofFixture.ExpectedReturnRips()));
        var starts = LinuxDiskFileProofFixture.MainInstructionRips(); ulong[] before = starts.ToArray();
        bool rejected = false;
        try { ((IList<ulong>)starts)[0] = 1; } catch (NotSupportedException) { rejected = true; }
        HostCheck.True(rejected && starts.SequenceEqual(before));
        Array.Fill(elf, (byte)0); Array.Fill(package, (byte)0); before[0] = 0;
        HostCheck.True(starts.SequenceEqual(LinuxDiskFileProofFixture.MainInstructionRips()));
        HostCheck.True(!ReferenceEquals(starts, LinuxDiskFileProofFixture.MainInstructionRips()));
    }

    private static string RipsHash(IReadOnlyList<ulong> rips)
    {
        byte[] bytes = new byte[rips.Count * 8];
        for (int i = 0; i < rips.Count; i++) BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(i * 8), rips[i]);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    // Deliberately independent of Writer's emission/marking operations. Decode
    // only the finite fixture's existing x64 opcode forms; no general x64 or
    // native execution claim follows from recognizing these exact bytes.
    private static ulong[] DecodeMain(byte[] elf)
    {
        List<ulong> starts = []; int at = LinuxDiskFileProofFixture.EntryOffset;
        int end = checked((int)(LinuxDiskFileProofFixture.SuccessRip - LinuxDiskFileProofFixture.ImageBase + 5));
        while (at < end)
        {
            starts.Add(LinuxDiskFileProofFixture.ImageBase + (uint)at);
            ReadOnlySpan<byte> code = elf.AsSpan(at); int bytes;
            if (code.StartsWith(new byte[] { 0x31, 0xc0 })) bytes = 2;
            else if (code.StartsWith(new byte[] { 0x66, 0x8c, 0xc8 })) bytes = 3;
            else if (code[0] == 0x41 && code[1] is >= 0xb8 and <= 0xbf) bytes = 6;
            else if (code[0] is >= 0xb8 and <= 0xbf) bytes = 5;
            else if (code.StartsWith(new byte[] { 0x48, 0xba })) bytes = 10;
            else if (code.StartsWith(new byte[] { 0x48, 0x3d })) bytes = 6;
            else if (code.StartsWith(new byte[] { 0x48, 0x39, 0xd0 })) bytes = 3;
            else if (code.StartsWith(new byte[] { 0x0f, 0x05 })) bytes = 2;
            else if (code.StartsWith(new byte[] { 0x0f, 0xb6, 0x05 })) bytes = 7;
            else if (code.StartsWith(new byte[] { 0x8b, 0x05 })) bytes = 6;
            else if (code.StartsWith(new byte[] { 0x48, 0x8b, 0x05 })) bytes = 7;
            else if (code.StartsWith(new byte[] { 0x0f, 0x85 }))
            {
                bytes = 6; HostCheck.Equal(LinuxDiskFileProofFixture.FailureOffset, at + bytes + BinaryPrimitives.ReadInt32LittleEndian(code[2..]));
            }
            else if (code[0] == 0xe9)
            {
                bytes = 5; HostCheck.Equal(end, at + bytes);
                HostCheck.Equal(LinuxDiskFileProofFixture.FailureOffset, at + bytes + BinaryPrimitives.ReadInt32LittleEndian(code[1..]));
            }
            else throw new InvalidOperationException("unsupported fixture instruction at " + at.ToString("x"));
            HostCheck.True(bytes > 0 && at + bytes <= end); at += bytes;
        }
        HostCheck.Equal(end, at); HostCheck.Equal((byte)0xcc, elf[end]);
        return starts.ToArray();
    }
}
