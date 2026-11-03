using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;
using OS.Tooling;
using Fixture = OS.Tooling.LinuxSignalProofFixture;

internal static unsafe class LinuxSignalIrqProofFixtureTests
{
    [HostTest("m16-009 activation package explicitly requests user IRQs and preserves original package policies")]
    private static void Package()
    {
        foreach (string variant in Fixture.Variants)
        {
            byte[] package = Fixture.CreatePackage(variant);
            var view = LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded && view.PayloadBytes == (Fixture.IsWaitVariant(variant) ? 40960U : 28672U) && view.Manifest.Threads == 2 && view.Manifest.Descriptors == 16);
            fixed (byte* p = package)
            {
                HostCheck.True(LinuxUserInterruptPolicy.TryRead(p + view.ManifestOffset, view.ManifestBytes, view.Manifest, out bool enabled));
                HostCheck.Equal(variant == "Activation", enabled);
            }
            HostCheck.Equal(variant, Fixture.PackageVariant(package));
        }
        byte[] input = Fixture.CreatePackage("Activation");
        var v = LinuxElfPackage.Validate(input);
        HostCheck.Equal("FA18CD2E6CA755316340BE222FE1CC7209AC2B97FC0B09E8F3E7EBD44CFAE7EA", Convert.ToHexString(SHA256.HashData(input)));
        string manifest = Encoding.ASCII.GetString(input, (int)v.ManifestOffset, (int)v.ManifestBytes);
        HostCheck.True(manifest.EndsWith("env.CLROS_USER_IRQS=1\n", StringComparison.Ordinal));
        int at = (int)v.ManifestOffset + manifest.IndexOf("CLROS_USER_IRQS=1", StringComparison.Ordinal) + 16;
        byte[] changed = input.ToArray();
        changed[at] = (byte)'0';
        HostCheck.True(LinuxElfPackage.Validate(changed).Succeeded);
        Reject(changed);
        Reject(input[..^1]);
        changed = input.ToArray();
        changed[^1] ^= 1;
        Reject(changed);
        using MemoryStream stream = new(input);
        Fixture.RequirePackage(stream);
        HostCheck.Equal(0L, stream.Position);
    }

    [HostTest("m16-009 activation uses actual clone tgkill35 exit and checked restorer instructions with independent ABI vectors")]
    private static void CallsAndVectors()
    {
        byte[] elf = Fixture.CreateElf("Activation");
        uint[] parent = [39, 186, 158, 158, 13, 131, 56, 186, 14, 1, 15, 1, 231], child = [158, 39, 186, 234, 60];
        foreach (bool isChild in new[] { false, true })
        {
            var calls = Fixture.ExpectedIrqInvocations(isChild);
            HostCheck.True(calls.Select(c => c.Number).SequenceEqual(isChild ? child : parent));
            foreach (var call in calls)
            {
                int end = (int)(call.ReturnRip - 0x400000), start = end - (call.Number == 15 ? 7 : 40);
                HostCheck.True(elf[end - 2] == 0x0f && elf[end - 1] == 5 && elf[start] == 0xb8 && U32(elf, start + 1) == call.Number);
            }
        }
        int clone = (int)(Fixture.ExpectedIrqInvocations(false)[6].ReturnRip - 0x400000);
        HostCheck.True(elf.AsSpan(clone, 5).SequenceEqual(new byte[] { 0x48, 0x85, 0xc0, 0x0f, 0x84 }));
        HostCheck.Equal(0x800, clone + 9 + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(clone + 5)));
        int spin = (int)(Fixture.ActivationLoopRip - 0x400000);
        HostCheck.True(elf.AsSpan(spin, 2).SequenceEqual(new byte[] { 0xeb, 0xfe }));
        HostCheck.Equal(Fixture.ActivationLoopRip + 2, Fixture.ActivationResumeRip);
        ReadOnlySpan<byte> handler = elf.AsSpan(0xa00, 0x400);
        HostCheck.True(handler.IndexOf(new byte[] { 0x48, 0xc7, 0x82, 0x90, 0, 0, 0, 0x78, 0x56, 0x34, 0x12 }) >= 0);
        byte[] resume = BitConverter.GetBytes((uint)Fixture.ActivationResumeRip);
        byte[] edit = [0x48, 0xc7, 0x82, 0xa8, 0, 0, 0, .. resume];
        HostCheck.True(handler.IndexOf(edit) >= 0);
        HostCheck.True(U64(elf, 0x1100) == 0x400a00 && U64(elf, 0x1108) == 0x0c000004 && U64(elf, 0x1110) == 0x400e00 &&
            U64(elf, 0x1120) == 0x406000 && U64(elf, 0x1130) == 4096);
        for (uint register = 0; register < 16; register++)
            if (register != 4)
            {
                ulong seed = 0x1122334400000000UL + register * 0x01010101UL;
                byte[] mov = [(byte)(register < 8 ? 0x48 : 0x49), (byte)(0xb8 + (register & 7)), .. BitConverter.GetBytes(seed)];
                HostCheck.True(elf.AsSpan(0x200, 0x600).IndexOf(mov) >= 0, "seed=" + register);
            }
        uint[] context = [40, 48, 56, 64, 72, 80, 88, 96, 104, 112, 120, 128, 136, 144, 152, 160, 168, 176, 192, 200];
        foreach (uint offset in context)
        {
            byte[] read = [0x48, 0x8b, 0x82, .. BitConverter.GetBytes(offset)];
            HostCheck.True(handler.IndexOf(read) >= 0, "context=" + offset);
        }
        HostCheck.True(handler.IndexOf(new byte[] { 0x41, 0x8b, 0x82, 0xa0, 0, 0, 0 }) >= 0 &&
            handler.IndexOf(new byte[] { 0x41, 0x8b, 0x82, 0x90, 1, 0, 0 }) >= 0 && handler.IndexOf(new byte[] { 0xdb, 0xe3 }) >= 0);
    }

    [HostTest("m16-009 activation IRQ metadata covers complete actual instructions including clone and temporary flags-stack depth")]
    private static void InstructionBoundaries()
    {
        byte[] elf = Fixture.CreateElf("Activation");
        var points = Fixture.ExpectedIrqPoints();
        HostCheck.Equal(points.Count, points.DistinctBy(p => (p.Child, p.Rip)).Count());
        foreach (var (start, end, child) in new[] { (0x200, 0x800, false), (0x800, 0xa00, true), (0xa00, 0xe00, false), (0xe00, 0xf00, false) })
        {
            var block = points.Where(p => p.Child == child && p.Rip >= 0x400000UL + (uint)start && p.Rip < 0x400000UL + (uint)end).OrderBy(p => p.Rip).ToArray();
            int position = start;
            foreach (var point in block)
            {
                HostCheck.Equal(0x400000UL + (uint)position, point.Rip);
                int length = Length(elf, position);
                HostCheck.True(length > 0 && position + length <= end, "opcode=" + position.ToString("x"));
                if (elf[position] == 0x58)
                    HostCheck.True(point.Rsp is 0x404ff8 or 0x406c30);
                position += length;
            }
            HostCheck.True(elf.AsSpan(position, end - position).IndexOfAnyExcept((byte)0xcc) < 0);
        }
        var initial = points.Where(p => p.Rsp == 0).ToArray();
        HostCheck.True(initial.Length == 1 && initial[0].Rip == 0x400200 && initial[0].Calls == 0);
        var spin = points.Single(p => p.Loop);
        HostCheck.True(!spin.Child && !spin.Handler && spin.Calls == 7 && spin.Rsp == 0x405000 && spin.Fs == 0x402000 && spin.Gs == 0x402100 && spin.Direction);
        int clone = (int)(Fixture.ExpectedIrqInvocations(false)[6].ReturnRip - 0x400000);
        var shared = points.Where(p => p.Child && p.Rip < 0x400800).OrderBy(p => p.Rip).ToArray();
        HostCheck.True(shared.Length == 2 && shared[0].Rip == 0x400000UL + (uint)clone && shared[1].Rip == shared[0].Rip + 3 &&
            shared.All(p => p.Calls == 0 && p.Rsp == 0x406000 && p.Fs == 0x403000 && p.Gs == 0x402100 && !p.Direction));
    }

    // Independent decoder for only this immutable mechanical instruction set.
    // Unknown prefixes/opcodes fail instead of inventing a permitted boundary.
    private static int Length(byte[] b, int p)
    {
        byte a = b[p], n = b[p + 1];
        if (a is 0x9c or 0x58 or 0xfc or 0xfd or 0xc3)
            return 1;
        if (a is >= 0xb8 and <= 0xbf || a is 0x25 or 0xe9)
            return 5;
        if (a == 0xeb || a == 0xdb && n == 0xe3)
            return 2;
        if (a == 0x0f)
            return n is 5 or 0x0b ? 2 : n is >= 0x80 and <= 0x8f ? 6 : n == 0xae ? 7 : 0;
        if (a is 0x48 or 0x49 or 0x4c)
        {
            if (n is >= 0xb8 and <= 0xbf)
                return 10;
            if (n == 0x3d)
                return 6;
            if (n is 0x89 or 0x39 or 0x85)
                return 3;
            if (n == 0x8b || n == 0x63)
                return b[p + 2] == 0x46 ? 4 : 7;
            if (n == 0xc7)
                return 11;
        }
        if (a == 0x41)
            return n is >= 0xb8 and <= 0xbf ? 6 : n == 0x0f ? 4 : n == 0x8b ? b[p + 2] == 0x82 ? 7 : 4 : 0;
        if (a == 0x66)
            return n is 0x44 or 0x45 ? 5 : n == 0x0f ? 4 : 0;
        if (a is 0x64 or 0x65)
            return 9;
        if (a == 0x89 || a == 0xd9)
            return 6;
        if (a == 0x8b)
            return n == 6 ? 2 : n == 0x46 ? 3 : 6;
        if (a == 0x83 && n == 0x3d)
            return 7;
        if (a == 0xc7 && n == 5)
            return 10;
        return 0;
    }
    private static uint U32(byte[] b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
    private static ulong U64(byte[] b, int at) => BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(at));
    private static void Reject(byte[] input)
    {
        bool rejected = false;
        try
        {
            _ = Fixture.PackageVariant(input);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
