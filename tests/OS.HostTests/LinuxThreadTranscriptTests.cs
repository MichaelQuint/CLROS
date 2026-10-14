using System.Buffers.Binary;
using System.Globalization;
using OS.Manifest;
using OS.Tooling;

internal static class LinuxThreadTranscriptTests
{
    [HostTest("m16-008 synthetic thread tails bind independent calls dynamic identities and one or many real-clock ticks", HostLane.Fast, 0)]
    private static void Positive()
    {
        byte[] package = LinuxThreadProofFixture.CreatePackage("Threads");
        foreach (int ticks in new[] { 1, 3, 128 })
        {
            string[] rows = Synthetic(ticks, ticks == 1 ? 17UL : 103UL, ticks == 1 ? 7UL : 5UL, ticks == 1 ? 12UL : 9UL);
            QemuTranscripts.ValidateLinuxThreadTail(rows, package, 0x200000, 0x100000);
            HostCheck.Equal(103, Indices(rows, "M16_008_CALL ").Length);
            HostCheck.Equal(21, Indices(rows, "M16_008_RESUME ").Length);
            HostCheck.Equal(26, Indices(rows, "M16_008_SUPERVISOR ").Length);
            HostCheck.Equal(5, Indices(rows, "M16_008_REAP ").Length);
            HostCheck.True(Indices(rows, "M16_008_REAP ").Take(4).Select(i => Field(rows[i], "old"))
                .SequenceEqual(new ulong[] { 0x100000002, 0x200000002, 0x300000002, 0x400000002 }));
            HostCheck.Equal(ticks, Indices(rows, "M16_008_TICK ").Length);
            int prior = -1;
            foreach (string marker in QemuTranscripts.M16LinuxThreadMarkers)
            { int at = Array.FindIndex(rows, row => row == marker || row.StartsWith(marker + " ", StringComparison.Ordinal)); HostCheck.True(at > prior); prior = at; }
        }
        var p = Decode(false); var c = Decode(true);
        ulong[] parentPath = [.. p.Take(9).Select(x => x.Rip), .. Enumerable.Range(0, 4).SelectMany(_ => p.Skip(9).Take(10).Select(x => x.Rip)), .. p.Skip(19).Select(x => x.Rip)];
        HostCheck.True(parentPath.SequenceEqual(LinuxThreadProofFixture.ExpectedReturnRips("Threads")) &&
            Enumerable.Range(0, 4).SelectMany(_ => c.Select(x => x.Rip)).SequenceEqual(LinuxThreadProofFixture.ExpectedReturnRips("Threads", child: true)));
        HostCheck.True(p[9].Rip == 0x400439 && p[^1].Rip == 0x400861 && c[^1].Rip == 0x400c63);
        HostCheck.True(p.Select(x => x.Nr).SequenceEqual(new ulong[] { 39,186,158,14,293,56,202,14,158,56,0,56,39,158,14,202,0,202,0,202,202,3,3,1,231 }) &&
            c.Select(x => x.Nr).SequenceEqual(new ulong[] { 39,186,158,14,14,14,158,158,1,202,1,60 }));
        HostCheck.True(QemuTranscripts.M16LinuxThreadMarkers.Contains("M16_008_RESUME", StringComparer.Ordinal) &&
            QemuTranscripts.M16LinuxThreadMarkers.Contains("M16_008_REAP", StringComparer.Ordinal));
    }

    [HostTest("m16-008 thread tail rejects altered packages admission captured frames TLS fd and TID bindings", HostLane.Fast, 1)]
    private static void IdentityAndFrames()
    {
        string[] rows = Synthetic(); byte[] package = LinuxThreadProofFixture.CreatePackage("Threads");
        byte[] noncanonical = (byte[])package.Clone(); int at = noncanonical.AsSpan().IndexOf("threads=2\n"u8);
        HostCheck.True(at > 64); "threads=3\n"u8.CopyTo(noncanonical.AsSpan(at));
        HostCheck.True(LinuxElfPackage.Validate(noncanonical).Succeeded); Reject(rows, noncanonical);
        byte[] corrupt = (byte[])package.Clone(); corrupt[^1] ^= 1; Reject(rows, corrupt); Reject(rows, []); Reject(rows, package[..^1]);
        int first = Indices(rows, "M16_008_CALL ")[0];
        foreach (string key in new[] { "identity", "nr", "a0", "a1", "a2", "a3", "a4", "a5", "rip", "rsp", "fs", "gs", "clock", "valueBits", "action", "status" })
            Reject(Change(rows, first, key, Field(rows[first], key) + 1), package);
        Reject(Change(rows, first, "flags", 0), package); Reject(Change(rows, first, "flags", 0x3002), package);
        foreach (int call in Indices(rows, "M16_008_CALL "))
        {
            if (Field(rows[call], "nr") == 158) Reject(Change(rows, call, "fs", Field(rows[call], "fs") + 8), package);
            if (unchecked((long)Field(rows[call], "valueBits")) < 0) Reject(Change(rows, call, "valueBits", 0), package);
        }
        int[] clones = Indices(rows, "M16_008_CALL ").Where(i => Field(rows[i], "nr") == 56 &&
            Field(rows[i], "a0") == 0x7d0f00 && Field(rows[i], "valueBits") < int.MaxValue).ToArray();
        HostCheck.Equal(4, clones.Length); int clone = clones[0];
        Reject(Change(rows, clone, "a0", 0x7d0f01), package); Reject(Change(rows, clone, "valueBits", 999), package);
        foreach (int later in clones.Skip(1))
            Reject(Change(rows, later, "valueBits", Field(rows[clone], "valueBits")), package);
        foreach (int reused in Indices(rows, "M16_008_CALL ").Where(i => Field(rows[i], "identity") >> 32 > 1))
            Reject(Change(rows, reused, "identity", Field(rows[reused], "identity") - (1UL << 32)), package);
        int child = Indices(rows, "M16_008_CALL ")[11];
        Reject(Change(rows, child, "identity", 0x100000001), package); Reject(Change(rows, child, "rsp", 0x405000), package);
        int close = Array.FindIndex(rows, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && Field(row, "nr") == 3);
        Reject(Change(rows, close, "a0", 4), package);
        Reject(Change(rows, 4, "root", Field(rows[3], "root")), package);
        Reject(Change(rows, 4, "root", 0x400000), package); // Aligned/disjoint still must match actual parent readback.
        Reject(Change(rows, 4, "pool", Field(rows[4], "root")), package);
        foreach (string key in new[] { "workers", "code", "metadata", "userImage" }) Reject(Change(rows, 4, key, Field(rows[4], key) + 1), package);
        string[] badPolicy = (string[])rows.Clone(); badPolicy[2] = badPolicy[2].Replace("blocking=scheduler", "blocking=unsupported", StringComparison.Ordinal); Reject(badPolicy, package);
        string[] extraField = (string[])rows.Clone(); extraField[first] += " rip=4194304"; Reject(extraField, package);
    }

    [HostTest("m16-008 thread tail rejects replay reordered completion forged supervisor clear-TID reap and cleanup", HostLane.Fast, 2)]
    private static void Lifecycle()
    {
        string[] rows = Synthetic(); byte[] package = LinuxThreadProofFixture.CreatePackage("Threads");
        foreach (int at in Indices(rows, "M16_008_RESUME "))
        {
            Reject(Change(rows, at, "valueBits", Field(rows[at], "valueBits") ^ 1), package);
            Reject(Change(rows, at, "status", 9), package);
        }
        foreach (int at in Indices(rows, "M16_008_SUPERVISOR "))
            foreach (string key in new[] { "identity", "action", "transfers", "ready", "active" }) Reject(Change(rows, at, key, Field(rows[at], key) + 1), package);
        foreach (int at in Indices(rows, "M16_008_EXIT ")) Reject(Change(rows, at, "clearTidResult", unchecked((ulong)-14L)), package);
        foreach (int at in Indices(rows, "M16_008_REAP ")) Reject(Change(rows, at, "next", Field(rows[at], "old")), package);
        foreach (string prefix in new[] { "M16_008_CALL ", "M16_008_RESUME ", "M16_008_SUPERVISOR ", "M16_008_EXIT ", "M16_008_REAP ", "M16_008_TICK ", "M16_005_WRITE ", "M16_008_OWNER_END ", "M16_008_RESTORE ", "CLROS_M16_008_READY" })
        {
            int at = Indices(rows, prefix)[0]; Reject([.. rows[..at], .. rows[(at + 1)..]], package);
            Reject([.. rows[..at], rows[at], .. rows[at..]], package);
        }
        int reap = Indices(rows, "M16_008_REAP ")[0]; string[] earlyJoin = (string[])rows.Clone();
        (earlyJoin[reap], earlyJoin[reap + 1]) = (earlyJoin[reap + 1], earlyJoin[reap]); Reject(earlyJoin, package);
        int witness = Indices(rows, "M16_005_WRITE ")[0]; string[] lateWrite = (string[])rows.Clone();
        (lateWrite[witness], lateWrite[witness + 1]) = (lateWrite[witness + 1], lateWrite[witness]); Reject(lateWrite, package);
        int end = Indices(rows, "M16_008_OWNER_END ")[0];
        foreach (string key in new[] { "reaped", "transfers", "irqs", "exit" }) Reject(Change(rows, end, key, Field(rows[end], key) + 1), package);
        string[] dirty = (string[])rows.Clone(); dirty[^3] = dirty[^3].Replace("fp=true", "fp=false", StringComparison.Ordinal); Reject(dirty, package);
        Reject(rows[..^1], package); Reject([.. rows, "TEST_PASS m16-008"], package);
    }

    [HostTest("m16-008 thread tail requires admitted HPET timeout elapsed bound tick ownership and exact errno", HostLane.Fast, 3)]
    private static void Timeout()
    {
        string[] rows = Synthetic(); byte[] package = LinuxThreadProofFixture.CreatePackage("Threads");
        int[] ticks = Indices(rows, "M16_008_TICK ");
        int timeout = Array.FindIndex(rows, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && Field(row, "a0") == 0x401014);
        ulong start = Field(rows[timeout], "clock");
        Reject(Change(rows, ticks[0], "epoch", 2), package); Reject(Change(rows, ticks[0], "nanos", start), package);
        Reject(Change(rows, ticks[0], "ready", 1), package); Reject(Change(rows, ticks[^1], "nanos", start + 999_999), package);
        Reject(Change(rows, ticks[0], "nanos", start + 1_000_000), package); // A due sole waiter cannot remain unready.
        Reject(Change(rows, ticks[^1], "ready", 0), package); Reject(Synthetic(129), package);
        int wrong = Array.FindIndex(rows, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && Field(row, "nr") == 202 && Field(row, "valueBits") == unchecked((ulong)-11L));
        Reject(Change(rows, wrong, "valueBits", 0), package);
        Reject(Change(rows, timeout, "action", 1), package); Reject(Change(rows, timeout, "status", 1), package);
        Reject(Change(rows, timeout, "clock", (ulong)long.MaxValue), package);
        int[] resumes = Indices(rows, "M16_008_RESUME "); Reject(Change(rows, resumes[^1], "valueBits", 0), package);
    }

    // Entirely synthetic host parser input. Decode actual instruction boundaries
    // and arguments independently; do not use the producer's expected-RIP API.
    private static string[] Synthetic(int ticks = 3, ulong pid = 17, ulong readFd = 7, ulong writeFd = 12)
    {
        const ulong p = 0x100000001;
        ulong c = 0x100000002; int cycle = 0;
        var parent = Decode(false); var child = Decode(true); ulong clock = 0;
        List<string> rows = ["TEST_START m16-008",
            "M16_006_POLICY physical=268435456 available=134217728 poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel",
            "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=28672 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler",
            "M16_005_ARMED root=2097152 cs=59 ss=51 kernelStack=9437184 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null",
            "M16_008_BOUND root=1048576 pool=3145728 workers=2 code=16384 metadata=45056 userImage=728 timer=idle-only clock=hpet-nanoseconds reuse=off-stack"];
        void Call(bool isChild, int index)
        {
            var call = (isChild ? child : parent)[index];
            if (isChild && index == 9 || !isChild && index is 17 or 20) clock += 10_000;
            ulong a0 = call.FdOffset == 0x1000 ? readFd : call.FdOffset == 0x1004 ? writeFd : call.A0;
            long value = isChild ? index switch { 0 => (long)pid, 1 => (long)pid + cycle + 1, 8 => 4, _ => 0 } :
                index switch { 0 or 1 or 12 => (long)pid, 5 or 6 or 7 => -22, 8 => -14,
                    9 => (long)pid + cycle + 1, 11 or 19 => -11, 15 => 1, 18 => 4, 23 => 15, _ => 0 };
            bool blocked = isChild ? index is 9 or 10 : index is 10 or 16 or 17 or 20;
            ulong action = blocked ? 3UL : index == (isChild ? child.Count : parent.Count) - 1 ? isChild ? 4UL : 2UL : 1UL;
            ulong status = blocked ? 9UL : action is 2 or 4 ? 2UL : 1UL;
            ulong fs = isChild ? index < 6 ? 0x403000UL : 0x403040UL : index < 2 ? 0UL : 0x402000UL;
            rows.Add(FormattableString.Invariant($"M16_008_CALL identity={(isChild ? c : p)} nr={call.Nr} a0={a0} a1={call.A1} a2={call.A2} a3={call.A3} a4={call.A4} a5={call.A5} rip={call.Rip} rsp={(isChild ? 0x406000UL : 0x405000UL)} flags=2 fs={fs} gs=0 clock={clock} valueBits={unchecked((ulong)value)} action={action} status={status}"));
        }
        void Calls(bool isChild, int first, int last) { for (int i = first; i <= last; i++) Call(isChild, i); }
        void Resume(ulong identity, long value) => rows.Add(FormattableString.Invariant($"M16_008_RESUME identity={identity} valueBits={unchecked((ulong)value)} status=1"));
        void Supervisor(ulong identity, uint action, uint transfers, uint ready, uint active) => rows.Add(FormattableString.Invariant($"M16_008_SUPERVISOR identity={identity} action={action} transfers={transfers} ready={ready} active={active}"));
        void Exit(ulong identity) => rows.Add(FormattableString.Invariant($"M16_008_EXIT identity={identity} clearTidResult=0"));
        void Reap(ulong identity) => rows.Add(FormattableString.Invariant($"M16_008_REAP old={identity} next={identity + (1UL << 32)} context=zero stack=zero slot=zero"));
        Calls(false, 0, 8);
        for (; cycle < 4; cycle++)
        {
            c = ((ulong)(cycle + 1) << 32) | 2; uint transfers = (uint)cycle * 12;
            Calls(false, 9, 10); Supervisor(p, 3, transfers + 2, 1, 2); Calls(true, 0, 9); Supervisor(c, 3, transfers + 4, 1, 2);
            Resume(p, 4); Calls(false, 11, 16); Supervisor(p, 3, transfers + 6, 1, 2);
            Resume(c, 0); Call(true, 10); Supervisor(c, 3, transfers + 8, 1, 2);
            Resume(p, 4096); Call(false, 17); Supervisor(p, 3, transfers + 10, 1, 2);
            Resume(c, 4100); Call(true, 11); Exit(c); Supervisor(c, 4, transfers + 12, 1, 2); Reap(c); Resume(p, 0); Call(false, 18);
        }
        Calls(false, 19, 20); Supervisor(p, 3, 50, 0, 1);
        ulong timeoutClock = clock;
        for (int tick = 1; tick <= ticks; tick++)
        { clock = timeoutClock + (ulong)tick * 1_000_000 / (uint)ticks; rows.Add(FormattableString.Invariant($"M16_008_TICK epoch={tick} nanos={clock} ready={(tick == ticks ? 1 : 0)}")); }
        Resume(p, -110); Calls(false, 21, 22); rows.Add("M16_005_WRITE fd=1 bytes=15 data=M16_THREADS_OK!"); Call(false, 23);
        Call(false, 24); Exit(p); Supervisor(p, 2, 52, 0, 1); Reap(p);
        rows.Add(FormattableString.Invariant($"M16_008_OWNER_END reaped=5 transfers=52 irqs={ticks} exit=42 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed reservation=retained"));
        rows.Add("M16_008_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
        rows.Add("CLROS_M16_008_READY"); rows.Add("TEST_PASS m16-008"); return rows.ToArray();
    }

    private readonly record struct CallFrame(ulong Nr, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong A5, ulong Rip, int FdOffset);
    private static List<CallFrame> Decode(bool child)
    {
        byte[] elf = LinuxThreadProofFixture.CreateElf("Threads"); ulong[] r = new ulong[16]; List<CallFrame> calls = []; int fd = -1;
        for (int at = child ? 0x900 : 0x200, end = child ? 0xf00 : 0x900; at < end && elf[at] != 0xcc;)
        {
            byte op = elf[at]; int size;
            if (op is >= 0xb8 and <= 0xbf)
            { int reg = op - 0xb8; r[reg] = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(at + 1)); if (reg == 7) fd = -1; size = 5; }
            else if (op == 0x41 && elf[at + 1] is >= 0xb8 and <= 0xbf)
            { r[8 + elf[at + 1] - 0xb8] = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(at + 2)); size = 6; }
            else if (op == 0x0f && elf[at + 1] == 5)
            { calls.Add(new(r[0], r[7], r[6], r[2], r[10], r[8], r[9], 0x400000UL + (uint)at + 2, fd)); size = 2; }
            else size = op switch
            {
                0x31 => 2, 0x66 or 0x41 or 0x4c => 3, 0xe9 => 5, 0xfc or 0xf4 => 1,
                0x49 when elf[at + 1] == 0xff && elf[at + 2] == 0xcd => 3,
                0x8b or 0x89 or 0x3b or 0x0f => 6, 0xc7 => 10, 0x64 => 9,
                0x48 => elf[at + 1] switch { 0x3d => 6, 0x8b => 7, 0x85 or 0x89 => 3, _ => throw new InvalidOperationException("unexpected REX fixture opcode") },
                _ => throw new InvalidOperationException("unexpected fixture opcode")
            };
            HostCheck.True(at + size <= end);
            if (op == 0x8b && elf[at + 1] == 0x3d) fd = at + 6 + BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(at + 2));
            at += size;
        }
        return calls;
    }
    private static int[] Indices(string[] rows, string prefix) => Enumerable.Range(0, rows.Length).Where(i => rows[i].StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static ulong Field(string row, string key) => ulong.Parse(row.Split(' ').Single(x => x.StartsWith(key + "=", StringComparison.Ordinal))[(key.Length + 1)..], CultureInfo.InvariantCulture);
    private static string[] Change(string[] rows, int index, string key, ulong value)
    {
        string[] changed = (string[])rows.Clone(), tokens = changed[index].Split(' ');
        int token = Array.FindIndex(tokens, x => x.StartsWith(key + "=", StringComparison.Ordinal)); HostCheck.True(token > 0);
        tokens[token] = key + "=" + value.ToString(CultureInfo.InvariantCulture); changed[index] = string.Join(' ', tokens);
        HostCheck.True(changed[index] != rows[index]); return changed;
    }
    private static void Reject(string[] rows, byte[] package)
    {
        bool rejected = false; try { QemuTranscripts.ValidateLinuxThreadTail(rows, package, 0x200000, 0x100000); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
