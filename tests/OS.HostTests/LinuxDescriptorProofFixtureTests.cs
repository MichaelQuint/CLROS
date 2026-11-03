using System.Buffers.Binary;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;

internal static unsafe partial class LinuxDescriptorProofFixtureTests
{
    [HostTest("m16-007 descriptor and pipe packages retain actual full ELF immutable path and independent headers", HostLane.Fast, 0)]
    private static void PackageGeometry()
    {
        foreach (string variant in new[]
        {
            "Descriptors",
            "Pipes"
        }

        )
        {
            byte[] package = LinuxDescriptorProofFixture.CreatePackage(variant);
            var view = LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded);
            HostCheck.Equal(12288UL, view.PayloadBytes);
            HostCheck.Equal(16U, view.Manifest.Descriptors);
            ReadOnlySpan<byte> elf = package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes);
            HostCheck.True(elf[..7].SequenceEqual(new byte[] { 0x7f, 0x45, 0x4c, 0x46, 2, 1, 1 }));
            HostCheck.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(elf[16..]));
            HostCheck.Equal((ushort)62, BinaryPrimitives.ReadUInt16LittleEndian(elf[18..]));
            HostCheck.Equal(0x400200UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[24..]));
            HostCheck.Equal(64UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[32..]));
            HostCheck.Equal((ushort)64, BinaryPrimitives.ReadUInt16LittleEndian(elf[52..]));
            HostCheck.Equal((ushort)56, BinaryPrimitives.ReadUInt16LittleEndian(elf[54..]));
            HostCheck.Equal((ushort)3, BinaryPrimitives.ReadUInt16LittleEndian(elf[56..]));
            HostCheck.Equal(5U, BinaryPrimitives.ReadUInt32LittleEndian(elf[68..]));
            HostCheck.Equal(6U, BinaryPrimitives.ReadUInt32LittleEndian(elf[124..]));
            HostCheck.Equal(4096UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[96..]));
            HostCheck.Equal(8192UL, BinaryPrimitives.ReadUInt64LittleEndian(elf[152..]));
            HostCheck.Equal(0x6474e551U, BinaryPrimitives.ReadUInt32LittleEndian(elf[176..]));
            string manifest = Encoding.ASCII.GetString(package.AsSpan(64, checked((int)view.PayloadOffset - 64)));
            HostCheck.True(manifest.Contains("app.path=apps/m16descriptors.elf\n", StringComparison.Ordinal) && manifest.Contains("args=" + variant + "\n", StringComparison.Ordinal));
            fixed (byte* bytes = package)
            {
                HostCheck.Equal(LinuxElfLoadStatus.Ready, LinuxElfLoader.Validate(bytes + view.PayloadOffset, view.PayloadBytes, view.Manifest.MemoryBytes, view.Manifest.StackBytes, out var plan));
                HostCheck.True(plan.LoadCount == 2 && plan.ImageBytes == 12288 && plan.Entry == 0x400200 && plan.ProgramHeaders == 0x400040 && plan.TlsMemoryBytes == 0);
            }

            HostCheck.True(package.AsSpan().SequenceEqual(LinuxDescriptorProofFixture.CreatePackage(variant)));
            byte[] changed = (byte[])package.Clone();
            changed[^1] ^= 1;
            HostCheck.True(!LinuxElfPackage.Validate(changed).Succeeded);
        }
    }

    [HostTest("m16-007 descriptor fixture call inventory and actual CPL3 loads bind shared state and immutable bytes", HostLane.Fast, 1)]
    private static void DescriptorInventory()
    {
        byte[] elf = LinuxDescriptorProofFixture.CreateElf("Descriptors");
        var calls = Decode(elf, 0x200, 0xf00);
        uint[] expected = [0, 1, 1, 20, 16, 257, 5, 0, 17, 72, 0, 8, 17, 72, 72, 72, 72, 72, 8, 0, 3, 0, 2, 2, 257, 20, 20, 20, 257, 72, 0, 0, 72, 72, 72, 3, 3, 3, 2, 0, 3, 2, 0, 3, 293, 1, 0, 3, 0, 3, 1, 231];
        HostCheck.True(calls.Select(call => call.Number).SequenceEqual(expected) && calls.Count == 52 && calls.Count <= 64);
        HostCheck.Equal(52U, LinuxDescriptorProofFixture.ExpectedCalls("Descriptors"));
        HostCheck.True(calls.Select(call => call.Rip).SequenceEqual(LinuxDescriptorProofFixture.ExpectedReturnRips("Descriptors")));
        HostCheck.Equal(calls[^1].Rip, LinuxDescriptorProofFixture.SuccessRip("Descriptors"));
        HostCheck.True(calls[0].A0 == 0 && calls[1].A0 == 1 && calls[2].A0 == 2 && calls[3].Number == 20);
        HostCheck.True(calls[5].A0 == 0xffff_ff9c && calls[5].A1 == 0x401000 && calls[5].A2 == 0 && calls[8].A3 == 0);
        HostCheck.True(calls.Count(call => call.Number == 257) == 3 && calls[24].A0 == 0xffff_ff9c && calls[24].A1 == 0x4010a0 && calls[28].A0 == 0xffff_ff9c && calls[28].A1 == 0x401000 && calls[28].A2 == 0x80000);
        HostCheck.True(calls[9].A0 == 3 && calls[9].A1 == 0 && calls[9].A2 == 6);
        HostCheck.True(calls[12].Number == 17 && calls[12].A0 == 3 && calls[12].A2 == 1 && calls[12].A3 == 0x2000);
        HostCheck.True(calls[13].A0 == 6 && calls[13].A1 == 4 && calls[13].A2 == 0x800);
        HostCheck.True(calls[32].A0 == 3 && calls[32].A1 == 1030 && calls[32].A2 == 7);
        HostCheck.True(elf.AsSpan(0x1000, 29).SequenceEqual("/app/apps/m16descriptors.elf\0"u8));
        HostCheck.True(elf.AsSpan(0x1040, 14).SequenceEqual("/proc/meminfo\0"u8));
        HostCheck.True(elf.AsSpan(0x1060, 16).SequenceEqual("/proc/self/maps\0"u8));
        HostCheck.True(elf.AsSpan(0x10a0, 32).SequenceEqual("/app/../apps/m16descriptors.elf\0"u8));
        HostCheck.Equal(0x403000UL, BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan(0x11c0)));
        HostCheck.Equal(1UL, BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan(0x11c8)));
        ReadOnlySpan<byte> code = elf.AsSpan(0x200, 0xd00);
        HostCheck.True(code[..11].SequenceEqual(new byte[] { 0x31, 0xc0, 0x66, 0x8c, 0xc8, 0x48, 0x3d, 0x3b, 0, 0, 0 }));
        HostCheck.True(code.IndexOf(new byte[] { 0x48, 0x3d, 0x7f, 0x45, 0x4c, 0x46, 0x0f, 0x85 }) >= 0);
        HostCheck.True(code.IndexOf(new byte[] { 0x48, 0x3d, 0, 0x88, 0, 0, 0x0f, 0x85 }) >= 0 && code.IndexOf(new byte[] { 0x48, 0x3d, 0, 0x80, 0, 0, 0x0f, 0x85 }) >= 0); // Shared flags retain O_LARGEFILE.
        HostCheck.True(code.IndexOf(new byte[] { 0x48, 0xba, 0, 0x30, 0, 0, 0, 0, 0, 0, 0x48, 0x39, 0xd0, 0x0f, 0x85 }) >= 0);
        HostCheck.True(Loads(elf, 0x200, 0xf00).Contains(0x402130UL)); // fstat size must be loaded in CPL3.
        HostCheck.True(Loads(elf, 0x200, 0xf00).Contains(0x402000UL) && Loads(elf, 0x200, 0xf00).Contains(0x402008UL));
        HostCheck.True(code.IndexOf(new byte[] { 0xfc, 0xf3, 0xa6, 0x0f, 0x85 }) >= 0); // Compared proc/pipe bytes.
    }

    [HostTest("m16-007 pipe fixture inventory binds actual full atomic partial drain ownership and error edges", HostLane.Fast, 2)]
    private static void PipeInventory()
    {
        byte[] elf = LinuxDescriptorProofFixture.CreateElf("Pipes");
        var calls = Decode(elf, 0x200, 0xf00);
        uint[] expected = [293, 0, 72, 3, 1, 0, 3, 0, 0, 1, 3, 293, 1, 1, 1, 0, 1, 1, 0, 0, 72, 3, 1, 3, 1, 3, 293, 0, 1, 0, 1, 3, 1, 1, 3, 293, 293, 293, 1, 0, 1, 0, 0, 3, 3, 3, 3, 293, 293, 293, 293, 20, 0, 3, 3, 1, 231];
        HostCheck.True(calls.Select(call => call.Number).SequenceEqual(expected) && calls.Count == 57 && calls.Count <= 64);
        HostCheck.Equal(57U, LinuxDescriptorProofFixture.ExpectedCalls("Pipes"));
        HostCheck.True(calls.Select(call => call.Rip).SequenceEqual(LinuxDescriptorProofFixture.ExpectedReturnRips("Pipes")));
        HostCheck.Equal(calls[^1].Rip, LinuxDescriptorProofFixture.SuccessRip("Pipes"));
        HostCheck.True(calls[0].A0 == 0x402ff0 && calls[0].A1 == 0x80800);
        HostCheck.True(calls[12].A0 == 4 && calls[12].A1 == 0x401000 && calls[12].A2 == 4096);
        HostCheck.True(calls[16].A2 == 1025 && calls[17].A2 == 5000 && calls[18].A2 == 4096);
        HostCheck.True(calls[26].A1 == 0 && calls[27].A2 == 0 && calls[28].A2 == 0);
        HostCheck.True(calls[35].A1 == 0x800 && calls[36].A1 == 0x800 && calls[37].A1 == 0x800);
        HostCheck.True(calls[47].A1 == 1 && calls[48].A0 == 0 && calls[49].A0 == 0x402fff);
        HostCheck.True(calls[51].Number == 20 && calls[51].A0 == 4 && calls[51].A1 == 0x4001a0 && calls[51].A2 == 2);
        HostCheck.True(elf.AsSpan(0x1a0, 32).SequenceEqual(new byte[] { 0, 0x10, 0x40, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 2, 0x10, 0x40, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0 }));
        // Independent fixed bytes include the high-index term, so a page-sized
        // skip/repeat cannot pass solely because an 8-bit pattern repeats.
        HostCheck.True(elf.AsSpan(0x1000, 16).SequenceEqual(new byte[] { 7, 36, 65, 94, 123, 152, 181, 210, 239, 12, 41, 70, 99, 128, 157, 186 }));
        HostCheck.True(elf.AsSpan(0x1400, 8).SequenceEqual(new byte[] { 59, 88, 117, 146, 175, 204, 233, 6 }));
        HostCheck.True(elf.AsSpan(0x1ff8, 8).SequenceEqual(new byte[] { 226, 255, 28, 57, 86, 115, 144, 173 }));
        ReadOnlySpan<byte> code = elf.AsSpan(0x200, 0xd00);
        HostCheck.True(code.IndexOf(new byte[] { 0xbf, 0, 0x20, 0x40, 0, 0xbe, 0, 0x14, 0x40, 0, 0xb9, 0, 0x0c, 0, 0, 0xfc, 0xf3, 0xa6, 0x0f, 0x85 }) >= 0);
        HostCheck.True(code.IndexOf(new byte[] { 0xbf, 0, 0x2c, 0x40, 0, 0xbe, 0, 0x10, 0x40, 0, 0xb9, 0, 4, 0, 0, 0xfc, 0xf3, 0xa6, 0x0f, 0x85 }) >= 0);
        foreach (int errno in new[]
        {
            -9,
            -11,
            -14,
            -22,
            -23,
            -32,
            -38
        }

        )
        {
            byte[] compare = new byte[8]
            {
                0x48,
                0x3d,
                0,
                0,
                0,
                0,
                0x0f,
                0x85
            };
            BinaryPrimitives.WriteInt32LittleEndian(compare.AsSpan(2), errno);
            HostCheck.True(code.IndexOf(compare) >= 0);
        }
    }

    [HostTest("m16-007 fixture witnesses success return RIPs and separate exit99 are exact fresh byte contracts", HostLane.Fast, 3)]
    private static void WitnessAndFailure()
    {
        HostCheck.True(LinuxDescriptorProofFixture.Variants.SequenceEqual(new[] { "Descriptors", "Pipes" }));
        foreach (string variant in LinuxDescriptorProofFixture.Variants)
        {
            byte[] elf = LinuxDescriptorProofFixture.CreateElf(variant);
            byte[] witness = Encoding.ASCII.GetBytes(variant == "Descriptors" ? "M16_DESCRIPTORS_OK!" : "M16_PIPES_OK!");
            HostCheck.True(elf.AsSpan(0x180, witness.Length).SequenceEqual(witness));
            var positive = Decode(elf, 0x200, 0xf00);
            HostCheck.True(positive[^2].Number == 1 && positive[^2].A0 == 1 && positive[^2].A1 == 0x400180 && positive[^2].A2 == (uint)witness.Length && positive[^1].Number == 231 && positive[^1].A0 == 42);
            var failure = Decode(elf, 0xf00, 0xf18);
            HostCheck.True(failure.Count == 1 && failure[0].Number == 231 && failure[0].A0 == 99 && failure[0].Rip == 0x400f17);
            HostCheck.Equal(0x400f17UL, LinuxDescriptorProofFixture.FailureExitRip);
            HostCheck.True(!positive.Any(call => call.Rip == failure[0].Rip));
            HostCheck.True(elf.AsSpan(0xf00, 24).SequenceEqual(new byte[] { 0xfc, 0xb8, 0xe7, 0, 0, 0, 0xbf, 99, 0, 0, 0, 0xbe, 0, 0, 0, 0, 0xba, 0, 0, 0, 0, 0x0f, 5, 0xf4 }));
            elf[0x180] ^= 1;
            HostCheck.True(LinuxDescriptorProofFixture.CreateElf(variant).AsSpan(0x180, witness.Length).SequenceEqual(witness));
        }

        foreach (string malformed in new[]
        {
            "",
            "descriptors",
            "Pipes ",
            "Hello",
            "Memory"
        }

        )
        {
            Reject(() => LinuxDescriptorProofFixture.CreateElf(malformed));
            Reject(() => LinuxDescriptorProofFixture.CreatePackage(malformed));
            Reject(() => LinuxDescriptorProofFixture.ExpectedCalls(malformed));
            Reject(() => LinuxDescriptorProofFixture.ExpectedReturnRips(malformed));
            Reject(() => LinuxDescriptorProofFixture.SuccessRip(malformed));
            Reject(() => LinuxDescriptorProofFixture.Witness(malformed));
        }
    }

    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure error)
        {
            rejected = error.Message == "m16_007_fixture_variant";
        }

        HostCheck.True(rejected);
    }

    // An independent decoder walks instruction boundaries, so opcode-like
    // immediate/displacement bytes cannot invent syscall counts or return RIPs.
    // It accepts only the deliberately small emitted instruction vocabulary.
    private static List<Call> Decode(byte[] elf, int start, int end)
    {
        List<Call> calls = [];
        uint[] registers = new uint[16];
        for (int at = start; at < end && elf[at] != 0xcc;)
        {
            int size = InstructionSize(elf, at);
            if (elf[at] is >= 0xb8 and <= 0xbf)
                registers[elf[at] - 0xb8] = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(at + 1));
            else if (elf[at] == 0x41 && elf[at + 1] is >= 0xb8 and <= 0xbf)
                registers[8 + elf[at + 1] - 0xb8] = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(at + 2));
            else if (elf[at] == 0x0f && elf[at + 1] == 5)
                calls.Add(new(registers[0], registers[7], registers[6], registers[2], registers[10], 0x400000UL + (uint)(at + 2)));
            if (elf[at] == 0xe9 || (elf[at] == 0x0f && elf[at + 1] is 0x85 or 0x8e))
            {
                int displacement = BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(at + size - 4));
                HostCheck.Equal(0xf00, at + size + displacement);
            }

            HostCheck.True(at + size <= end);
            at += size;
        }

        return calls;
    }

    private static HashSet<ulong> Loads(byte[] elf, int start, int end)
    {
        HashSet<ulong> targets = [];
        for (int at = start; at < end && elf[at] != 0xcc;)
        {
            int size = InstructionSize(elf, at);
            bool load = elf[at] == 0x8b || (elf[at] == 0x48 && elf[at + 1] == 0x8b) || (elf[at] == 0x0f && elf[at + 1] == 0xb6);
            if (load)
                targets.Add((ulong)(0x400000L + at + size + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(at + size - 4))));
            at += size;
        }

        return targets;
    }

    private static int InstructionSize(byte[] code, int at)
    {
        byte op = code[at];
        if (op is >= 0xb8 and <= 0xbf)
            return 5;
        if (op == 0x41 && code[at + 1] is >= 0xb8 and <= 0xbf)
            return 6;
        if (op == 0x48 && code[at + 1] is >= 0xb8 and <= 0xbf)
            return 10;
        if (op == 0x48 && code[at + 1] == 0x3d)
            return 6;
        if (op == 0x48 && code[at + 1] == 0x8b && code[at + 2] == 5)
            return 7;
        if (op == 0x48 && (code[at + 1] == 0x39 || code[at + 1] == 0x85))
            return 3;
        if (op == 0x8b && code[at + 1] == 5)
            return 6;
        if (op == 0x0f && code[at + 1] == 0xb6 && code[at + 2] == 5)
            return 7;
        if (op == 0x0f && code[at + 1] is 0x85 or 0x8e)
            return 6;
        if (op == 0x0f && code[at + 1] == 5)
            return 2;
        if (op == 0x31 && code[at + 1] == 0xc0)
            return 2;
        if (op == 0x66 && code[at + 1] == 0x8c && code[at + 2] == 0xc8)
            return 3;
        if (op == 0xf3 && code[at + 1] == 0xa6)
            return 2;
        if (op == 0xfc || op == 0xf4)
            return 1;
        if (op == 0xe9)
            return 5;
        throw new InvalidOperationException("unexpected fixture opcode at " + at);
    }
}
