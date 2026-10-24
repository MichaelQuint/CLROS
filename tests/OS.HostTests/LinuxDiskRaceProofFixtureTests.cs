using System.Buffers.Binary;
using System.Text;
using OS.Manifest;
using OS.Tooling;
using F = OS.Tooling.LinuxDiskRaceProofFixture;

internal static class LinuxDiskRaceProofFixtureTests
{
    [HostTest("disk race packages are canonical deterministic two-worker cooperative children")]
    private static void Packages()
    {
        foreach (string variant in F.Variants)
        {
            byte[] package = F.CreatePackage(variant), elf = F.CreateElf(variant);
            var view = LinuxElfPackage.Validate(package);
            HostCheck.True(view.Succeeded && view.Manifest.Threads == 2 && view.Manifest.Descriptors == 16);
            HostCheck.Equal(variant, F.PackageVariant(package));
            HostCheck.True(package.SequenceEqual(F.CreatePackage(variant)) && elf.SequenceEqual(F.CreateElf(variant)));
            HostCheck.True(package.AsSpan((int)view.PayloadOffset, (int)view.PayloadBytes).SequenceEqual(elf));
            string ascii = Encoding.ASCII.GetString(package, (int)view.ManifestOffset, (int)view.ManifestBytes);
            HostCheck.True(ascii.Contains("app.path=apps/m16diskrace.elf\n", StringComparison.Ordinal) &&
                ascii.Contains("args=DiskRace" + variant + "\n", StringComparison.Ordinal) && !ascii.Contains("CLROS_USER_IRQS", StringComparison.Ordinal));
            byte[] changed = (byte[])package.Clone(); changed[^1] ^= 1; Reject(() => F.PackageVariant(changed));
            Reject(() => F.PackageVariant(package.AsSpan(0, package.Length - 1)));
        }
        HostCheck.True(!F.CreatePackage("Progress").SequenceEqual(F.CreatePackage("CloseReuse")));
        Reject(() => F.CreateElf("progress")); Reject(() => F.Calls("unknown"));
        Reject(() => F.PackageVariant(LinuxDiskFileProofFixture.CreatePackage()));
        HostCheck.Equal(59U, LinuxDiskFileProofFixture.ExpectedCalls);
    }

    [HostTest("disk race exact instruction vectors agree with independent finite x64 decoding")]
    private static void Instructions()
    {
        foreach (string variant in F.Variants)
        {
            byte[] elf = F.CreateElf(variant);
            var main = Decode(elf, F.EntryOffset, checked((int)(F.MainEndRip(variant) - F.ImageBase)));
            var child = Decode(elf, F.ChildOffset, checked((int)(F.ChildEndRip(variant) - F.ImageBase)));
            HostCheck.True(F.InstructionRips(variant).SequenceEqual(main.Starts));
            ulong clone = F.CloneReturnRip(variant);
            // Decode the actual child's inherited clone continuation through
            // its JE, rather than assuming every Writer.Op is an instruction.
            var inherited = Decode(elf, (int)(clone - F.ImageBase), (int)(clone - F.ImageBase + 21));
            HostCheck.Equal(4, inherited.Starts.Length);
            HostCheck.True(F.InstructionRips(variant, true).SequenceEqual(inherited.Starts.Concat(child.Starts)));
            HostCheck.True(F.InstructionRips(variant).All(r => r < F.ImageBase + F.ChildOffset));
            HostCheck.True(F.InstructionRips(variant, true).All(r => r < F.ImageBase + F.FailureOffset));
            HostCheck.True(F.InstructionRips(variant).Contains(clone) && F.InstructionRips(variant, true).Contains(clone));
            foreach (bool isChild in new[] { false, true })
            {
                var decoded = isChild ? child.Calls : main.Calls; var expected = F.Calls(variant, isChild);
                HostCheck.Equal(expected.Count, decoded.Length);
                for (int i = 0; i < decoded.Length; i++)
                {
                    var a = expected[i]; var b = decoded[i];
                    HostCheck.True(a.Number == b.Number && a.A0 == b.A0 && a.A1 == b.A1 && a.A2 == b.A2 &&
                        a.A3 == b.A3 && a.A4 == b.A4 && a.A5 == b.A5 && a.ReturnRip == b.Rip);
                    HostCheck.True(F.InstructionRips(variant, isChild).Contains(a.ReturnRip));
                    if (a.Number is not (56 or 60 or 202 or 231))
                    {
                        int at = (int)(a.ReturnRip - F.ImageBase);
                        HostCheck.True(elf[at] == 0x48 && elf[at + 1] == 0x3d);
                        HostCheck.Equal(a.Result, (long)BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(at + 2)));
                    }
                }
            }
            int cloneBranch = (int)(clone - F.ImageBase) + 15;
            HostCheck.True(elf[cloneBranch] == 0x0f && elf[cloneBranch + 1] == 0x84);
            HostCheck.Equal(F.ChildOffset, cloneBranch + 6 + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(cloneBranch + 2)));
            HostCheck.True(elf[(int)(F.FailureExitRip - F.ImageBase) - 2] == 0x0f && elf[(int)(F.FailureExitRip - F.ImageBase) - 1] == 5);
            HostCheck.True(!F.InstructionRips(variant).Contains(F.FailureExitRip));
        }
    }

    [HostTest("disk race metadata names disjoint sentinels stacks clone TIDs and exact progress or close operations")]
    private static void Ownership()
    {
        HostCheck.Equal(0x404000UL, F.Memory.ParentBuffer); HostCheck.Equal(8192U, F.Memory.ParentBytes);
        HostCheck.Equal(0x406000UL, F.Memory.ChildBuffer); HostCheck.Equal(4096U, F.Memory.ChildBytes);
        HostCheck.True(F.Memory.ChildStack <= F.Memory.ParentBuffer && F.ChildTls + 4096 <= F.Memory.ChildStack - 4096);
        foreach (string variant in F.Variants)
        {
            byte[] elf = F.CreateElf(variant);
            HostCheck.True(elf.AsSpan((int)(F.ParentOutput - F.ImageBase), 8192).IndexOfAnyExcept((byte)0xa5) < 0 &&
                elf.AsSpan((int)(F.ChildOutput - F.ImageBase), 4096).IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.Equal(2U, BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan((int)(F.ClearTid - F.ImageBase))));
            HostCheck.Equal(0x45434152U, BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan((int)(F.Payload - F.ImageBase))));
            var main = F.Calls(variant); var child = F.Calls(variant, true); var clone = main.Single(c => c.Number == 56);
            HostCheck.True(clone.A0 == 0x7d0f00 && clone.A1 == F.ChildStackTop && clone.A2 == F.ParentTid && clone.A3 == F.ClearTid && clone.A4 == F.ChildTls && clone.Result == 2);
            HostCheck.True(main.Single(c => c.Role == "main.read") is { Number: 0, A0: 3, A2: 8192, OutputBytes: 8192 });
            HostCheck.True(child.Single(c => c.Role == "child.pin") is { Number: 10, A0: F.ParentOutput, A1: 4096, A2: 1, Result: -12 });
            HostCheck.True(child.Single(c => c.Role == "child.disjoint-writable") is { Number: 10, A0: F.ChildOutput, A2: 3, Result: 0 });
            HostCheck.True(child[^1] is { Number: 60, A0: 0 } && main[^1] is { Number: 231, A0: 42 });
            HostCheck.True(main.Single(c => c.Role == "main.join") is { Number: 202, A0: F.ClearTid, A2: 2, Wait: F.WaitPolicy.Join });
            if (variant == "CloseReuse")
            {
                HostCheck.Equal(-9L, main.Single(c => c.Role == "main.read").Result);
                HostCheck.True(child.Single(c => c.Role == "child.reopen") is { Number: 2, A0: F.EmptyPath, Result: 3 });
                HostCheck.Equal(0L, main.Single(c => c.Role == "main.replacement").Result);
            }
            else
            {
                HostCheck.Equal(8192L, main.Single(c => c.Role == "main.read").Result);
                HostCheck.True(child.Single(c => c.Role == "child.duplicate") is { Number: 0, A0: 7, A2: 8, Result: 8 });
                HostCheck.True(child.Single(c => c.Role == "child.pread") is { Number: 17, A3: 65535, Result: 512 });
                HostCheck.Equal(8200L, main.Single(c => c.Role == "main.cursor").Result);
            }
        }
    }

    [HostTest("disk race copied metadata cannot mutate later package call or instruction identities")]
    private static void Immutable()
    {
        foreach (string variant in F.Variants)
        {
            var calls = F.Calls(variant); var rips = F.InstructionRips(variant, true);
            var savedCalls = calls.ToArray(); var savedRips = rips.ToArray();
            bool callsRejected = false, ripsRejected = false;
            try { ((IList<F.CallSpec>)calls)[0] = default; } catch (NotSupportedException) { callsRejected = true; }
            try { ((IList<ulong>)rips)[0] = 0; } catch (NotSupportedException) { ripsRejected = true; }
            HostCheck.True(callsRejected && ripsRejected);
            byte[] elf = F.CreateElf(variant), package = F.CreatePackage(variant); Array.Clear(elf); Array.Clear(package);
            HostCheck.True(calls.SequenceEqual(savedCalls) && rips.SequenceEqual(savedRips));
            HostCheck.True(F.Calls(variant).SequenceEqual(savedCalls) && F.InstructionRips(variant, true).SequenceEqual(savedRips));
            HostCheck.True(!ReferenceEquals(calls, F.Calls(variant)) && !ReferenceEquals(rips, F.InstructionRips(variant, true)));
        }
    }

    internal readonly record struct DecodedCall(ulong Rip, ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5);
    internal static DecodedCall[] Calls(string variant, bool child = false)
    {
        byte[] elf = F.CreateElf(variant);
        return Decode(elf, child ? F.ChildOffset : F.EntryOffset,
            checked((int)((child ? F.ChildEndRip(variant) : F.MainEndRip(variant)) - F.ImageBase))).Calls;
    }
    private static (ulong[] Starts, DecodedCall[] Calls) Decode(byte[] elf, int start, int end)
    {
        List<ulong> starts = []; List<DecodedCall> calls = []; ulong[] registers = new ulong[16];
        for (int at = start; at < end;)
        {
            starts.Add(F.ImageBase + (uint)at); ReadOnlySpan<byte> code = elf.AsSpan(at); int bytes;
            if (code[0] is >= 0xb8 and <= 0xbf)
            { registers[code[0] - 0xb8] = BinaryPrimitives.ReadUInt32LittleEndian(code[1..]); bytes = 5; }
            else if (code[0] == 0x41 && code[1] is >= 0xb8 and <= 0xbf)
            { registers[8 + code[1] - 0xb8] = BinaryPrimitives.ReadUInt32LittleEndian(code[2..]); bytes = 6; }
            else if (code.StartsWith(new byte[] { 0x0f, 5 }))
            { calls.Add(new(F.ImageBase + (uint)at + 2, registers[0], registers[7], registers[6], registers[2], registers[10], registers[8], registers[9])); bytes = 2; }
            else if (code.StartsWith(new byte[] { 0x48, 0x3d })) bytes = 6;
            else if (code.StartsWith(new byte[] { 0x48, 0x85, 0xc0 })) bytes = 3;
            else if (code.StartsWith(new byte[] { 0x0f, 0xb6, 5 })) bytes = 7;
            else if (code.StartsWith(new byte[] { 0x8b, 5 })) bytes = 6;
            else if (code.StartsWith(new byte[] { 0x48, 0x8b, 5 })) bytes = 7;
            else if (code[0] == 0x0f && code[1] is 0x84 or 0x85)
            { bytes = 6; int target = at + 6 + BinaryPrimitives.ReadInt32LittleEndian(code[2..]); HostCheck.True(target == F.FailureOffset || target == F.ChildOffset || target > at && target < F.ChildOffset); }
            else if (code[0] == 0xe9)
            { bytes = 5; HostCheck.Equal(F.FailureOffset, at + 5 + BinaryPrimitives.ReadInt32LittleEndian(code[1..])); HostCheck.Equal(end, at + 5); }
            else if (code[0] == 0xfc) bytes = 1;
            else if (code.StartsWith(new byte[] { 0xf3, 0xae })) bytes = 2;
            else throw new InvalidOperationException("unsupported disk race opcode " + at.ToString("x"));
            HostCheck.True(bytes > 0 && at + bytes <= end); at += bytes;
        }
        return (starts.ToArray(), calls.ToArray());
    }
    private static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
}
