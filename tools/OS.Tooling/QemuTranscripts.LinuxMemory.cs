using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static IReadOnlyList<string> M16LinuxMemoryMarkers { get; } = Array.AsReadOnly<string>(
        ["TEST_START m16-006", "M16_006_POLICY", "M16_005_ARMED", "M16_005_CALL", "M16_005_WRITE", "M16_005_RESTORE",
         "M16_005_EXIT", "M16_006_MEMORY", "CLROS_M16_006_READY", "TEST_PASS m16-006"]);

    // Canonical mechanical ELF identity and independent Linux ABI expectations.
    // Host synthetic rows exercise this parser; only original guest captures and
    // their complete parent/provenance validation can establish live acceptance.
    public static string[] ValidateLinuxMemoryTail(string[] lines, byte[] package)
    {
        Need(package.AsSpan().SequenceEqual(LinuxMemoryProofFixture.CreatePackage("Memory")), "m16_006_fixture_identity");
        int start = Array.IndexOf(lines, "TEST_START m16-006");
        Need(start == QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 7 + 17, "m16_006_parent_record_count");
        string[] tail = lines[start..];
        Need(tail.Length == 43 && tail[^2] == "CLROS_M16_006_READY" && tail[^1] == "TEST_PASS m16-006", "m16_006_terminal");
        LinuxMemoryPolicy(tail[1]);
        var arm = LinuxSyscallArm(tail[2]);
        Need(tail[^5] == LinuxSyscallRestored, "m16_006_restoration");
        var exit = ReadRecord(tail[^4], "M16_005_EXIT", "code:dec", "calls:dec", "bytes:dec", "writes:dec");
        var memory = ReadRecord(tail[^3], "M16_006_MEMORY", "reserved:dec", "backed:dec", "tables:dec", "entropyRemaining:dec");
        var initial = ReadRecord(lines[QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 3], "M16_004_TABLES", "root:dec", "used:dec", "leaves:dec", "ranges:dec");
        Need(initial["root"] == arm["root"] && memory["reserved"] == 2097152 && memory["backed"] == 0 &&
            memory["tables"] == initial["used"] && memory["entropyRemaining"] == 0, "m16_006_final_ownership");
        const ulong va = 0x100000000, heap = 0x400000000, huge = 8234266624;
        (ulong Nr, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, long Value)[] expected =
        [
            (9,0,huge,0,0x22,~0UL,(long)va), (10,va+4096,8192,3,0,0,0),
            (10,va+4096,4096,1,0,0,0), (10,va+4096,4096,3,0,0,0),
            (9,va+4096,4096,0,0x32,~0UL,(long)va+4096), (10,va+4096,4096,3,0,0,0),
            (1,1,va+8184,24,0,0,24), (11,va+8192,4096,0,0,0,0),
            (10,va+4096,8192,3,0,0,-12), (10,va+4096,4096,7,0,0,-22),
            (9,0,4096,3,0x800022,~0UL,-22), (9,va+4096,4096,0,0x100022,~0UL,-17),
            (9,0,16781312,3,0x22,~0UL,-12), (28,va+4096,4096,16,0,0,0), (28,va+4096,4096,17,0,0,0),
            (11,va,4096,0,0,0,0), (11,va,huge,0,0,0,0),
            (204,0,8,0x401100,0,0,8), (99,0x401180,0,0,0,0,0),
            (228,1,0x401120,0,0,0,0), (228,1,0x401130,0,0,0,0),
            (318,0x401140,16,0,0,0,16), (318,0x401150,16,0,0,0,16), (318,0x401140,16,1,0,0,-11),
            (12,0,0,0,0,0,(long)heap), (12,heap+8192,0,0,0,0,(long)heap+8192),
            (12,0,0,0,0,0,(long)heap+8192), (12,heap,0,0,0,0,(long)heap),
            (25,0,4096,8192,0,0,-12), (25,0x6ffef000,4096,8192,0,0,-14), (231,42,0,0,0,0,0)
        ];
        uint ordinal = 0, barriers = 0, calls = 0, writes = 0; bool pendingWrite = false;
        byte[] elf = LinuxMemoryProofFixture.CreateElf("Memory");
        ulong[] instructionRips = LinuxMemoryCallRips(elf);
        Need(instructionRips.Length == 34, "m16_006_instruction_inventory");
        for (int i = 3; i < tail.Length - 5; i++)
        {
            string line = tail[i];
            if (line.StartsWith("M16_005_WRITE ", StringComparison.Ordinal))
            {
                Need(!pendingWrite && writes++ == 0 && line == "M16_005_WRITE fd=1 bytes=24 data=" + LinuxMemoryProofFixture.Witness,
                    "m16_006_high_copy_witness");
                pendingWrite = true; continue;
            }
            var row = ReadRecord(line, "M16_005_CALL", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec",
                "rip:dec", "rsp:dec", "flags:dec", "valueBits:dec", "action:dec", "status:dec");
            calls++;
            ulong rip = row["rip"];
            Need(calls <= 34 && rip == instructionRips[calls - 1] && rip >= 0x400202 && rip < 0x400f00 && elf[(int)(rip - 0x400000 - 2)] == 0x0f &&
                elf[(int)(rip - 0x400000 - 1)] == 5 && row["rsp"] is >= 0x6fff0008 and <= 0x70000000 &&
                (row["flags"] & 2) != 0 && (row["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0 && row["a5"] == 0,
                "m16_006_captured_frame");
            if (row["nr"] == 324)
            {
                ulong command = barriers == 0 ? 0UL : barriers == 1 ? 16UL : 8UL;
                Need(ordinal == 19 && barriers < 3 && row["a0"] == command && row["a1"] == 0 && row["a2"] == 0 &&
                    row["a3"] == 0 && row["a4"] == 0 && row["valueBits"] == (barriers == 0 ? 24UL : 0UL) &&
                    row["action"] == 1 && row["status"] == 1 && !pendingWrite, "m16_006_process_barrier");
                barriers++; continue;
            }
            Need(ordinal < expected.Length, "m16_006_extra_call");
            var e = expected[ordinal++]; ulong a0 = e.Nr == 25 && e.A0 == 0 ? row["rsp"] & ~4095UL : e.A0;
            Need(row["nr"] == e.Nr && row["a0"] == a0 && row["a1"] == e.A1 && row["a2"] == e.A2 &&
                row["a3"] == e.A3 && row["a4"] == e.A4 && row["valueBits"] == unchecked((ulong)e.Value), "m16_006_call_binding_" + ordinal + " " + line);
            bool exited = e.Nr == 231;
            Need(row["action"] == (exited ? 2UL : 1UL) && row["status"] == (exited ? 2UL : 1UL) &&
                (!exited || i == tail.Length - 6), "m16_006_call_lifecycle");
            if (e.Nr == 1) { Need(pendingWrite, "m16_006_missing_high_copy"); pendingWrite = false; }
            else Need(!pendingWrite, "m16_006_write_call_order");
        }
        Need(ordinal == expected.Length && barriers == 3 && calls == 34 && writes == 1 && !pendingWrite &&
            exit["code"] == 42 && exit["calls"] == calls && exit["writes"] == 1 && exit["bytes"] == 24, "m16_006_accounting");
        return lines[..start];
    }

    // Separate expected-negative entry point. The ordinary smoke profile still
    // requires TEST_PASS and fails these guests; this receipt proves only the
    // exact absent-leaf challenge, original parent records and restoration.
    public static void AssertLinuxMemoryNegativeCapture(string serialPath, string packagePath, string variant)
    {
        Need(variant is "GuardFault" or "ProtNoneFault", "m16_006_negative_variant");
        AssertM03AcpiBootstrapTranscript(serialPath, firstRealExit: true, ownedSurvival: true,
            linuxElfPackagePath: packagePath, workloadServiceLoop: true, linuxStartup: true, linuxSyscalls: true,
            linuxMemory: true, linuxMemoryNegativeVariant: variant);
    }

    public static string[] ValidateLinuxMemoryNegativeTail(string[] lines, byte[] package, string variant)
    {
        Need(variant is "GuardFault" or "ProtNoneFault", "m16_006_negative_variant");
        Need(package.AsSpan().SequenceEqual(LinuxMemoryProofFixture.CreatePackage(variant)), "m16_006_negative_identity");
        int start = Array.IndexOf(lines, "TEST_START m16-006"), count = variant == "GuardFault" ? 2 : 1;
        Need(start == QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 7 + 17 && lines.Length == start + count + 6,
            "m16_006_negative_inventory");
        string[] tail = lines[start..]; LinuxMemoryPolicy(tail[1]); _ = LinuxSyscallArm(tail[2]);
        Need(tail[^2] == LinuxSyscallRestored && tail[^1] == "TEST_FAIL m16-006 user-boundary-terminated status=1", "m16_006_negative_restoration");
        byte[] elf = LinuxMemoryProofFixture.CreateElf(variant); ulong[] rips = LinuxMemoryCallRips(elf);
        Need(rips.Length == count, "m16_006_negative_instruction_inventory");
        for (int i = 0; i < count; i++)
        {
            var call = ReadRecord(tail[3 + i], "M16_005_CALL", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec", "rip:dec", "rsp:dec", "flags:dec", "valueBits:dec", "action:dec", "status:dec");
            Need(call["nr"] == (i == 0 ? 9UL : 10UL) && call["a0"] == (i == 0 ? 0UL : 0x100001000UL) &&
                call["a1"] == (i == 0 ? 8234266624UL : 4096UL) && call["a2"] == (i == 0 ? 0UL : 3UL) &&
                call["a3"] == (i == 0 ? 0x22UL : 0UL) && call["a4"] == (i == 0 ? ~0UL : 0UL) && call["a5"] == 0 &&
                call["valueBits"] == (i == 0 ? 0x100000000UL : 0UL) && call["rip"] == rips[i] &&
                call["rsp"] is >= 0x6fff0008 and <= 0x70000000 && (call["flags"] & 2) != 0 &&
                (call["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0 && call["action"] == 1 && call["status"] == 1, "m16_006_negative_call");
        }
        const string disposition = " disposition=terminate";
        Need(tail[^3].EndsWith(disposition, StringComparison.Ordinal), "m16_006_negative_disposition");
        var fault = ReadRecord(tail[^3][..^disposition.Length], "M16_005_FAULT", "vector:dec", "error:dec", "rip:dec", "rsp:dec", "cr2:dec", "cs:dec", "ss:dec");
        int read = elf.AsSpan(0x200, 0xd00).IndexOf(new byte[] { 0x48, 0x8b, 0 });
        Need(read >= 0 && fault["rip"] == 0x400200UL + (uint)read && fault["vector"] == 14 && fault["error"] == 4 &&
            fault["cr2"] == (variant == "GuardFault" ? 0x100000000UL : 0x100001000UL) && fault["cs"] == 59 &&
            fault["ss"] == 51 && fault["rsp"] is >= 0x6fff0008 and <= 0x70000000, "m16_006_negative_fault");
        return lines[..start];
    }

    private static void LinuxMemoryPolicy(string line)
    {
        const string suffix = " poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel";
        Need(line.EndsWith(suffix, StringComparison.Ordinal), "m16_006_policy");
        var policy = ReadRecord(line[..^suffix.Length], "M16_006_POLICY", "physical:dec", "available:dec");
        Need(policy["physical"] is >= 16777216 and <= 0x100000000 && policy["available"] >= 16777216 &&
            policy["available"] <= policy["physical"] && (policy["physical"] | policy["available"]) % 4096 == 0, "m16_006_resource_facts");
    }

    private static ulong[] LinuxMemoryCallRips(byte[] elf)
    {
        List<ulong> rips = [];
        for (int i = 0x200; i < 0xf00; i++)
        {
            if ((elf[i] == 0x48 || elf[i] == 0x49) && elf[i + 1] is >= 0xb8 and <= 0xbf) i += 9;
            else if (elf[i] == 0x0f && elf[i + 1] == 5) { rips.Add(0x400000UL + (uint)i + 2); i++; }
        }
        return rips.ToArray();
    }
}
