using System.Buffers.Binary;
using OS.Manifest;
using OS.Tooling;

internal static class LinuxDescriptorTranscriptTests
{
    [HostTest("m16-007 synthetic descriptor tails bind actual calls outputs ABI errors and immutable fixture identity", HostLane.Fast, 0)]
    private static void PositiveTails()
    {
        foreach (string variant in new[] { "Descriptors", "Pipes" })
        {
            string[] rows = SyntheticRows(variant);
            byte[] package = LinuxDescriptorProofFixture.CreatePackage(variant);
            string[] parents = QemuTranscripts.ValidateLinuxDescriptorTail(rows, package);
            HostCheck.Equal(107, parents.Length);
            HostCheck.True(parents.SequenceEqual(rows[..107]));
            HostCheck.True(rows[^5] == (variant == "Descriptors"
                ? "M16_005_EXIT code=42 calls=52 bytes=65 writes=4"
                : "M16_005_EXIT code=42 calls=57 bytes=5153 writes=1"));
            HostCheck.Equal(variant == "Descriptors" ? 4 : 1, rows.Count(IsOutput));
            if (variant == "Descriptors")
            {
                HostCheck.True(rows.Any(row => row.Contains("nr=17 a0=3 a1=4202496 a2=1 a3=8192", StringComparison.Ordinal)));
                HostCheck.True(rows.Any(row => row.Contains("valueBits=34816 action=1", StringComparison.Ordinal)) &&
                    rows.Any(row => row.Contains("valueBits=32768 action=1", StringComparison.Ordinal)));
                HostCheck.True(rows.Any(row => row.Contains("valueBits=61 action=1", StringComparison.Ordinal)));
            }
            else HostCheck.True(rows.Any(row => row.Contains("valueBits=18446744073709551584 action=1", StringComparison.Ordinal))); // -EPIPE
        }
    }

    [HostTest("m16-007 descriptor transcript rejects valid noncanonical packages and every altered captured frame field", HostLane.Fast, 1)]
    private static void IdentityAndFrames()
    {
        foreach (string variant in new[] { "Descriptors", "Pipes" })
        {
            string[] rows = SyntheticRows(variant); byte[] package = LinuxDescriptorProofFixture.CreatePackage(variant);
            byte[] noncanonical = (byte[])package.Clone();
            int quota = noncanonical.AsSpan().IndexOf("descriptors=16\n"u8);
            HostCheck.True(quota > 64);
            "descriptors=15\n"u8.CopyTo(noncanonical.AsSpan(quota));
            HostCheck.True(LinuxElfPackage.Validate(noncanonical).Succeeded);
            Reject(rows, noncanonical);
            byte[] corrupt = (byte[])package.Clone(); corrupt[^1] ^= 1; Reject(rows, corrupt);
            Reject(rows, package[..^1]); Reject(rows, []);
            Reject(rows, LinuxDescriptorProofFixture.CreatePackage(variant == "Descriptors" ? "Pipes" : "Descriptors"));

            int first = CallRows(rows)[0];
            foreach (string key in new[] { "nr", "a0", "a1", "a2", "a3", "a4", "a5", "rip", "valueBits", "action", "status" })
                Reject(Change(rows, first, key, Field(rows[first], key) + 1), package);
            Reject(Change(rows, first, "rsp", 0x6fff0007), package);
            Reject(Change(rows, first, "rsp", 0x70000008), package);
            Reject(Change(rows, first, "flags", 0), package);
            Reject(Change(rows, first, "flags", 0x3002), package);
            string[] duplicateField = (string[])rows.Clone(); duplicateField[first] += " rip=" + Field(rows[first], "rip");
            Reject(duplicateField, package);
            int exit = CallRows(rows)[^1];
            Reject(Change(rows, exit, "a0", 99), package);
            Reject(Change(rows, exit, "rip", 0x400f17), package);
            Reject(Change(rows, exit, "action", 1), package);
            Reject(Change(rows, exit, "status", 1), package);
        }
    }

    [HostTest("m16-007 descriptor transcript rejects forged errno flags partial counts and missing reordered extra I/O", HostLane.Fast, 2)]
    private static void ReturnValuesAndOrdering()
    {
        foreach (string variant in new[] { "Descriptors", "Pipes" })
        {
            string[] rows = SyntheticRows(variant); byte[] package = LinuxDescriptorProofFixture.CreatePackage(variant);
            int[] calls = CallRows(rows), outputs = Enumerable.Range(0, rows.Length).Where(i => IsOutput(rows[i])).ToArray();
            long[] values = Values(variant);
            for (int i = 0; i < values.Length; i++)
                if (values[i] < 0) Reject(Change(rows, calls[i], "valueBits", 0), package);
            if (variant == "Descriptors")
            {
                Reject(Change(rows, calls[12], "a3", 0), package); // Original payload data is a positioned read.
                Reject(Change(rows, calls[14], "valueBits", 0x800), package); // O_LARGEFILE must survive shared F_SETFL.
                Reject(Change(rows, calls[34], "valueBits", 0), package);
                Reject(Change(rows, calls[39], "valueBits", 59), package); // Literal physical/available facts produce 61 bytes.
                Reject(Change(rows, calls[42], "valueBits", 127), package);
            }
            else
            {
                Reject(Change(rows, calls[8], "valueBits", unchecked((ulong)-11L)), package); // Drained last-writer EOF.
                Reject(Change(rows, calls[16], "valueBits", 1025), package); // Atomic shortage cannot report success.
                Reject(Change(rows, calls[17], "valueBits", 5000), package); // Only 1024 bytes were available.
                Reject(Change(rows, calls[24], "valueBits", unchecked((ulong)-38L)), package); // Explicit ignored SIGPIPE is EPIPE.
            }
            Reject(Remove(rows, calls[8]), package);
            Reject(Insert(rows, calls[8], rows[calls[8]]), package);
            string[] reordered = (string[])rows.Clone();
            (reordered[calls[8]], reordered[calls[9]]) = (reordered[calls[9]], reordered[calls[8]]);
            Reject(reordered, package);
            Reject(Remove(rows, outputs[0]), package);
            Reject(Insert(rows, outputs[0], rows[outputs[0]]), package);
            Reject(Change(rows, outputs[0], "fd", 9), package);
            Reject(Change(rows, outputs[0], "bytes", Field(rows[outputs[0]], "bytes") + 1), package);
            string[] badData = (string[])rows.Clone(); badData[outputs[0]] += "!"; Reject(badData, package);
            // Preserve the total row count while moving a valid output after
            // its captured call. Counts alone cannot authenticate sink ordering.
            string[] lateOutput = (string[])rows.Clone();
            (lateOutput[outputs[0]], lateOutput[outputs[0] + 1]) = (lateOutput[outputs[0] + 1], lateOutput[outputs[0]]);
            Reject(lateOutput, package);
            // A pipe write cannot invent serial output, even if an actual sink
            // row is moved there so the overall output count stays identical.
            int pipeWrite = Array.FindIndex(calls, index => Field(rows[index], "nr") == 1 && Field(rows[index], "a0") == 4);
            var forged = rows.ToList(); string sink = forged[outputs[^1]]; forged.RemoveAt(outputs[^1]);
            forged.Insert(calls[pipeWrite], sink); Reject(forged.ToArray(), package);
            foreach (string key in new[] { "code", "calls", "bytes", "writes" })
                Reject(Change(rows, rows.Length - 5, key, Field(rows[^5], key) + 1), package);
        }
    }

    [HostTest("m16-007 descriptor transcript rejects changed admission policies restoration memory and buffer release", HostLane.Fast, 3)]
    private static void PolicyAndLifetime()
    {
        foreach (string variant in new[] { "Descriptors", "Pipes" })
        {
            string[] rows = SyntheticRows(variant); byte[] package = LinuxDescriptorProofFixture.CreatePackage(variant);
            foreach ((string Before, string After) edit in new (string, string)[]
            {
                ("physical=268435456", "physical=0"), ("available=33554432", "available=0"),
                ("poolPages=4096", "poolPages=1"), ("cpus=1", "cpus=2"), ("root=inactive-kernel", "root=active-user"),
                ("descriptors=16", "descriptors=15"), ("pipes=2", "pipes=1"), ("capacity=4096", "capacity=2048"),
                ("scratch=8192", "scratch=4096"), ("packageBytes=12288", "packageBytes=8192"),
                ("package=declared-immutable", "package=loaded-alias"), ("proc=owned-snapshot", "proc=static-mock"),
                ("stdin=eof", "stdin=mock"), ("stdoutStderr=serial", "stdoutStderr=mock"),
                ("sigpipe=ignored-mechanical", "sigpipe=unimplemented"), ("blocking=unsupported", "blocking=success-no-op"),
                ("root=true", "root=false"), ("descriptors=true", "descriptors=false"), ("ss=true", "ss=false"),
                ("msrs=true", "msrs=false"), ("fsGs=true", "fsGs=false"), ("fp=true", "fp=false"), ("stack=true", "stack=false"),
                ("reserved=2097152", "reserved=0"), ("backed=0", "backed=1"), ("tables=12", "tables=13"),
                ("entropyRemaining=32", "entropyRemaining=0"), ("descriptors=0", "descriptors=1"), ("pipes=0", "pipes=1"),
                ("scratch=zero", "scratch=dirty"), ("snapshots=zero", "snapshots=dirty"),
                ("CLROS_M16_007_READY", "CLROS_M16_006_READY"), ("TEST_PASS m16-007", "TEST_PASS m16-006")
            })
            {
                string[] changed = rows.Select(row => row.Replace(edit.Before, edit.After, StringComparison.Ordinal)).ToArray();
                HostCheck.True(!changed.SequenceEqual(rows), edit.Before); Reject(changed, package);
            }
            Reject(Change(rows, 86, "root", 1048576), package); Reject(Change(rows, 86, "used", 13), package);
            Reject(Change(rows, 110, "root", 1048576), package);
            Reject(rows[1..], package); Reject([.. rows, "extra"], package);
            Reject(Remove(rows, rows.Length - 6), package); Reject(Remove(rows, rows.Length - 3), package);
        }
    }

    // Synthetic parent rows deliberately prove no firmware, loader or guest
    // execution. Only the additive descriptor-tail parser is under test here.
    private static string[] SyntheticRows(string variant)
    {
        List<string> rows = Enumerable.Repeat("synthetic-parent", 107).ToList();
        rows[86] = "M16_004_TABLES root=2097152 used=12 leaves=500 ranges=10";
        rows.AddRange(["TEST_START m16-007",
            "M16_006_POLICY physical=268435456 available=33554432 poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel",
            "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=12288 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=unsupported",
            "M16_005_ARMED root=2097152 cs=59 ss=51 kernelStack=3145728 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null"]);
        var frames = Frames(LinuxDescriptorProofFixture.CreateElf(variant)); long[] values = Values(variant);
        HostCheck.Equal(variant == "Descriptors" ? 52 : 57, frames.Count); HostCheck.Equal(frames.Count, values.Length);
        for (int i = 0; i < frames.Count; i++)
        {
            if (variant == "Descriptors")
            {
                if (i == 1) rows.Add("M16_005_WRITE fd=1 bytes=14 data=M16_FD1_OWNED!");
                else if (i == 2) rows.Add("M16_005_WRITE fd=2 bytes=14 data=M16_FD2_OWNED!");
                else if (i == 3) rows.Add("M16_005_WRITE fd=1 bytes=14 data=M16_WRITEV_OK!");
                else if (i == 50) rows.Add("M16_005_WRITE fd=1 bytes=19 data=M16_DESCRIPTORS_OK!");
            }
            else if (i == 55) rows.Add("M16_005_WRITE fd=1 bytes=13 data=M16_PIPES_OK!");
            Frame frame = frames[i]; bool exit = i == frames.Count - 1;
            rows.Add($"M16_005_CALL nr={frame.Number} a0={frame.A0} a1={frame.A1} a2={frame.A2} a3={frame.A3} a4={frame.A4} a5={frame.A5} rip={frame.Rip} rsp=1879044096 flags=2 valueBits={unchecked((ulong)values[i])} action={(exit ? 2 : 1)} status={(exit ? 2 : 1)}");
        }
        rows.AddRange(["M16_005_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true",
            variant == "Descriptors" ? "M16_005_EXIT code=42 calls=52 bytes=65 writes=4" : "M16_005_EXIT code=42 calls=57 bytes=5153 writes=1",
            "M16_006_MEMORY reserved=2097152 backed=0 tables=12 entropyRemaining=32",
            "M16_007_RELEASE descriptors=0 pipes=0 scratch=zero snapshots=zero", "CLROS_M16_007_READY", "TEST_PASS m16-007"]);
        return rows.ToArray();
    }

    // Independent ABI/error vectors; results never come from the parser or the
    // fixture's comparison immediates. The two proc lengths use the stated facts.
    private static long[] Values(string variant) => variant == "Descriptors"
        ? [0, 14, 14, 14, -25, 3, 0, 4, 4, 6, 4, 8, 1, 0, 0x8800, 0, 0, 1, 12288, 0, 0, -9, -2, -30, -13,
            -14, -14, -22, 3, 1, 4, 0, 7, 1, 0x8000, 0, 0, 0, 3, 61, 0, 3, 128, 0, 0, 4, 4, 0, 0, 0, 19, 0]
        : [0, -11, 5, 0, 4, 2, 0, 2, 0, -9, 0, 0, 4096, -11, -11, 1024, -11, 1024, 4096, -11, 5, 0, 4, 0, -32,
            0, 0, 0, 0, -38, -38, 0, 0, -38, 0, 0, 0, -23, 4, -11, 4, 4, 4, 0, 0, 0, 0, -22, -14, -14, 0, 4, 4, 0, 0, 13, 0];

    private readonly record struct Frame(ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5, ulong Rip);

    // Walk forward from entry, independently tracking MOV immediates and each
    // actual SYSCALL. Do not call the producer's expected-RIP API or decode its
    // result comparisons; instruction boundaries prevent false opcode matches.
    private static List<Frame> Frames(byte[] code)
    {
        ulong[] registers = new ulong[16]; List<Frame> frames = [];
        for (int at = 0x200; at < 0xf00 && code[at] != 0xcc;)
        {
            byte op = code[at]; int size;
            if (op is >= 0xb8 and <= 0xbf)
            { registers[op - 0xb8] = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(at + 1)); size = 5; }
            else if (op == 0x41)
            { registers[8 + code[at + 1] - 0xb8] = BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(at + 2)); size = 6; }
            else if (op == 0x48 && code[at + 1] == 0xba)
            { registers[2] = BinaryPrimitives.ReadUInt64LittleEndian(code.AsSpan(at + 2)); size = 10; }
            else if (op == 0x0f && code[at + 1] == 5)
            {
                frames.Add(new(registers[0], registers[7], registers[6], registers[2], registers[10], registers[8], registers[9], 0x400000UL + (uint)at + 2));
                size = 2;
            }
            else size = op switch
            {
                0x31 => 2, 0x66 => 3, 0xe9 => 5, 0xfc or 0xf4 => 1, 0xf3 => 2, 0x8b => 6,
                0x0f => code[at + 1] == 0xb6 ? 7 : 6,
                0x48 => code[at + 1] switch { 0x3d => 6, 0x8b => 7, 0x39 or 0x85 => 3,
                    _ => throw new InvalidOperationException("unknown fixture REX opcode") },
                _ => throw new InvalidOperationException("unknown fixture opcode")
            };
            at += size;
        }
        return frames;
    }

    private static bool IsOutput(string row) => row.StartsWith("M16_005_WRITE ", StringComparison.Ordinal);
    private static int[] CallRows(string[] rows) => Enumerable.Range(0, rows.Length)
        .Where(i => rows[i].StartsWith("M16_005_CALL ", StringComparison.Ordinal)).ToArray();
    private static ulong Field(string row, string key) => ulong.Parse(row.Split(' ').Single(token => token.StartsWith(key + "=", StringComparison.Ordinal))[(key.Length + 1)..]);
    private static string[] Change(string[] rows, int index, string key, ulong value)
    {
        string[] changed = (string[])rows.Clone(); string[] tokens = changed[index].Split(' ');
        int token = Array.FindIndex(tokens, item => item.StartsWith(key + "=", StringComparison.Ordinal));
        HostCheck.True(token > 0); tokens[token] = key + "=" + value; changed[index] = string.Join(' ', tokens);
        HostCheck.True(changed[index] != rows[index]); return changed;
    }
    private static string[] Remove(string[] rows, int at) => [.. rows[..at], .. rows[(at + 1)..]];
    private static string[] Insert(string[] rows, int at, string row) => [.. rows[..at], row, .. rows[at..]];
    private static void Reject(string[] rows, byte[] package)
    {
        bool rejected = false;
        try { _ = QemuTranscripts.ValidateLinuxDescriptorTail(rows, package); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
