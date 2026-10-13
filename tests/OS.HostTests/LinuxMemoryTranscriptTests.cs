using System.Buffers.Binary;
using OS.Tooling;

internal static class LinuxMemoryTranscriptTests
{
    [HostTest("m16-006 memory profile selects exact prerequisite chain and original parent transcript entry", HostLane.Fast, 0)]
    private static void Profile()
    {
        string[] parents = ["M16002", "M16003", "M16004", "M16005", "M01LoadedImageStack", "M01LiveAllocator",
            "AcpiPhysicalDiscovery", "M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival"];
        foreach (bool build in new[] { false, true })
        {
            var profile = EfiOptions.Parse(["--EnableM16006Probe"], build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && profile.IsEnabled("M16006") && parents.All(profile.IsEnabled));
            HostCheck.True(profile.Flags.Where(p => p.Value).Count() == parents.Length + 1);
            foreach (string parent in parents)
                HostCheck.True(Rejected(() => EfiOptions.Parse(["--EnableM16006Probe", "--Enable" + parent + "Probe:false"], build).Profile()));
            HostCheck.True(Rejected(() => EfiOptions.Parse(["--boot-policy", "VerifiedExit", "--EnableM16006Probe"], build).Profile()));
        }
        var smoke = QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run", "--build-efi", "--EnableM16006Probe"]));
        HostCheck.True(smoke.IsEnabled("EnableM16006Probe") && smoke.Required.Contains("TEST_PASS m16-006") &&
            smoke.Required.Contains("M16_006_MEMORY") && !smoke.Required.Contains("TEST_PASS m16-005"));
        // Reject an incomplete profile before attempting file I/O or stripping any parent rows.
        HostCheck.True(Rejected(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript("missing", linuxMemory: true)));
        HostCheck.True(!OS.Kernel.Diagnostics.Probes.EnableM16006Probe);
    }

    [HostTest("m16-006 synthetic memory rows bind all actual fixture instructions results accounting and restoration", HostLane.Fast, 1)]
    private static void Tail()
    {
        var rows = SyntheticRows(); byte[] package = LinuxMemoryProofFixture.CreatePackage("Memory");
        HostCheck.Equal(107, QemuTranscripts.ValidateLinuxMemoryTail(rows, package).Length);
        foreach ((string Old, string New) change in new (string, string)[]
        {
            ("poolPages=4096", "poolPages=1"), ("cpus=1", "cpus=2"), ("available=134217728", "available=0"),
            ("root=inactive-kernel", "root=active-user"), ("data=M16_MEMORY_HIGH_COPY_OK!", "data=M16_MEMORY_HIGH_COPY_BAD"),
            ("ss=true", "ss=false"), ("backed=0", "backed=1"), ("entropyRemaining=0", "entropyRemaining=32"),
            ("calls=34", "calls=33"), ("TEST_PASS m16-006", "TEST_PASS m16-005"),
            ("valueBits=18446744073709551604", "valueBits=0"), ("a0=16 a1=0", "a0=32 a1=0"),
            ("a1=4294975480", "a1=4294967296"), ("flags=2", "flags=12290"),
            ("nr=318 a0=4294971520", "nr=318 a0=4198720")
        })
        {
            string[] altered = rows.Select(r => r.Replace(change.Old, change.New, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!altered.SequenceEqual(rows) && Rejected(() => QemuTranscripts.ValidateLinuxMemoryTail(altered, package)));
        }
        HostCheck.True(Rejected(() => QemuTranscripts.ValidateLinuxMemoryTail([.. rows, "extra"], package)));
        HostCheck.True(Rejected(() => QemuTranscripts.ValidateLinuxMemoryTail(rows[1..], package)));
        HostCheck.True(Rejected(() => QemuTranscripts.ValidateLinuxMemoryTail(rows, LinuxMemoryProofFixture.CreatePackage("GuardFault"))));
        package[^1] ^= 1; HostCheck.True(Rejected(() => QemuTranscripts.ValidateLinuxMemoryTail(rows, package)));
    }

    [HostTest("m16-006 controlled fault parser rejects wrong sparse target protection fault frame and fake success", HostLane.Fast, 2)]
    private static void Negatives()
    {
        foreach (string variant in new[] { "GuardFault", "ProtNoneFault" })
        {
            var rows = SyntheticRows().Take(110).ToList();
            byte[] elf = LinuxMemoryProofFixture.CreateElf(variant), package = LinuxMemoryProofFixture.CreatePackage(variant);
            List<ulong> rips = [];
            for (int i = 0x200; i < 0xf00; i++)
            {
                if ((elf[i] is 0x48 or 0x49) && elf[i + 1] is >= 0xb8 and <= 0xbf) i += 9;
                else if (elf[i] == 0x0f && elf[i + 1] == 5) { rips.Add(0x400000UL + (uint)i + 2); i++; }
            }
            rows.Add($"M16_005_CALL nr=9 a0=0 a1=8234266624 a2=0 a3=34 a4=18446744073709551615 a5=0 rip={rips[0]} rsp=1879044096 flags=2 valueBits=4294967296 action=1 status=1");
            if (variant == "GuardFault") rows.Add($"M16_005_CALL nr=10 a0=4294971392 a1=4096 a2=3 a3=0 a4=0 a5=0 rip={rips[1]} rsp=1879044096 flags=2 valueBits=0 action=1 status=1");
            int read = elf.AsSpan(0x200, 0xd00).IndexOf(new byte[] { 0x48, 0x8b, 0 });
            rows.Add($"M16_005_FAULT vector=14 error=4 rip={0x400200UL + (uint)read} rsp=1879044096 cr2={(variant == "GuardFault" ? 4294967296UL : 4294971392UL)} cs=59 ss=51 disposition=terminate");
            rows.AddRange(["M16_005_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "TEST_FAIL m16-006 user-boundary-terminated status=1"]);
            HostCheck.Equal(107, QemuTranscripts.ValidateLinuxMemoryNegativeTail(rows.ToArray(), package, variant).Length);
            foreach ((string Old, string New) change in new (string, string)[] { ("error=4", "error=5"), ("ss=51 disposition", "ss=16 disposition"),
                ("ss=true", "ss=false"), ("valueBits=4294967296", "valueBits=0"), ("status=1", "status=2"), ("vector=14", "vector=13") })
                HostCheck.True(Rejected(() => QemuTranscripts.ValidateLinuxMemoryNegativeTail(rows.Select(r => r.Replace(change.Old, change.New)).ToArray(), package, variant)));
            HostCheck.True(Rejected(() => QemuTranscripts.ValidateLinuxMemoryTail(rows.ToArray(), package)));
            HostCheck.True(Rejected(() => QemuTranscripts.ValidateLinuxMemoryNegativeTail([.. rows, "extra"], package, variant)));
        }
    }

    // Synthetic tail only: deliberately dummy parent rows cannot prove firmware,
    // ELF loading or startup. The complete public validator checks those separately.
    private static string[] SyntheticRows()
    {
        List<string> rows = Enumerable.Repeat("synthetic-parent", 107).ToList();
        rows[86] = "M16_004_TABLES root=1048576 used=12 leaves=500 ranges=10";
        rows.AddRange(["TEST_START m16-006", "M16_006_POLICY physical=268435456 available=134217728 poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel",
            "M16_005_ARMED root=1048576 cs=59 ss=51 kernelStack=2097152 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null"]);
        long[] values = [4294967296, 0, 0, 0, 4294971392, 0, 24, 0, -12, -22, -22, -17, -12, 0, 0,
            8, 0, 24, 0, 0, 0, 0, 16, 16, -11, 17179869184, 17179877376, 17179877376, 17179869184, -12, -14, 0, 0, 0];
        byte[] elf = LinuxMemoryProofFixture.CreateElf("Memory");
        ulong[] registers = new ulong[16]; int call = 0;
        for (int offset = 0x200; offset < 0xf00; offset++)
        {
            if ((elf[offset] == 0x48 || elf[offset] == 0x49) && elf[offset + 1] is >= 0xb8 and <= 0xbf)
            {
                int register = elf[offset + 1] - 0xb8 + (elf[offset] == 0x49 ? 8 : 0);
                registers[register] = BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan(offset + 2)); offset += 9;
            }
            else if (elf[offset] == 0x0f && elf[offset + 1] == 5)
            {
                ulong number = registers[0];
                if (number == 25 && call == 29) registers[7] = 0x6ffff000;
                if (number == 1) rows.Add("M16_005_WRITE fd=1 bytes=24 data=" + LinuxMemoryProofFixture.Witness);
                bool exit = number == 231;
                rows.Add($"M16_005_CALL nr={number} a0={registers[7]} a1={registers[6]} a2={registers[2]} a3={registers[10]} a4={registers[8]} a5={registers[9]} rip={0x400000UL + (uint)offset + 2} rsp=1879044096 flags=2 valueBits={unchecked((ulong)values[call++])} action={(exit ? 2 : 1)} status={(exit ? 2 : 1)}");
                offset++;
            }
        }
        HostCheck.Equal(34, call);
        rows.AddRange(["M16_005_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true",
            "M16_005_EXIT code=42 calls=34 bytes=24 writes=1", "M16_006_MEMORY reserved=2097152 backed=0 tables=12 entropyRemaining=0",
            "CLROS_M16_006_READY", "TEST_PASS m16-006"]);
        return rows.ToArray();
    }

    private static bool Rejected(Action action)
    {
        try { action(); return false; } catch (ToolFailure) { return true; }
    }
}
