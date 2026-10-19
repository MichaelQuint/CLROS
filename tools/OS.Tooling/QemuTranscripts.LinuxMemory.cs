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
        bool ownership = package.AsSpan().SequenceEqual(LinuxMemoryProofFixture.CreatePackage("HeapOwnership"));
        bool fragmented = package.AsSpan().SequenceEqual(LinuxMemoryProofFixture.CreatePackage("FragmentedBacking"));
        Need(ownership || fragmented || package.AsSpan().SequenceEqual(LinuxMemoryProofFixture.CreatePackage("Memory")), "m16_006_fixture_identity");
        uint callCount = fragmented ? LinuxMemoryProofFixture.FragmentedBackingCalls :
            ownership ? LinuxMemoryProofFixture.HeapOwnershipCalls : LinuxMemoryProofFixture.MemoryCalls;
        int start = Array.IndexOf(lines, "TEST_START m16-006");
        Need(start == QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 7 + 17, "m16_006_parent_record_count");
        string[] tail = lines[start..];
        Need(tail.Length == callCount + 9 && tail[^2] == "CLROS_M16_006_READY" && tail[^1] == "TEST_PASS m16-006", "m16_006_terminal");
        LinuxMemoryPolicy(tail[1]);
        var arm = LinuxSyscallArm(tail[2]);
        Need(tail[^5] == LinuxSyscallRestored, "m16_006_restoration");
        var exit = ReadRecord(tail[^4], "M16_005_EXIT", "code:dec", "calls:dec", "bytes:dec", "writes:dec");
        var memory = ReadRecord(tail[^3], "M16_006_MEMORY", "reserved:dec", "backed:dec", "tables:dec", "entropyRemaining:dec");
        var initial = ReadRecord(lines[QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 3], "M16_004_TABLES", "root:dec", "used:dec", "leaves:dec", "ranges:dec");
        Need(initial["root"] == arm["root"] && memory["reserved"] == 2097152 && memory["backed"] == 0 &&
            memory["tables"] == initial["used"] && memory["entropyRemaining"] == (ownership || fragmented ? 32UL : 0UL), "m16_006_final_ownership");
        const ulong va = 0x100000000, heap = 0x400000000, huge = 8234266624;
        (ulong Nr, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, long Value)[] expected =
        fragmented ?
        [
            (9,0,huge,0,0x22,~0UL,(long)va),
            (9,va,4194304,3,0x32,~0UL,(long)va),
            (9,va+8388608,4194304,3,0x32,~0UL,(long)va+8388608),
            (9,va+16777216,4194304,3,0x32,~0UL,(long)va+16777216),
            (9,va+25165824,4194304,3,0x32,~0UL,(long)va+25165824),
            (11,va,4194304,0,0,0,0), (11,va+16777216,4194304,0,0,0,0),
            (9,va+33554432,6291456,3,0x32,~0UL,(long)va+33554432),
            (10,va+33554432,6291456,0,0,0,0), (28,va+33554432,6291456,4,0,0,0),
            (10,va+33554432,6291456,3,0,0,0), (1,1,va+37748728,24,0,0,24),
            (12,heap+2097152,0,0,0,0,(long)heap+2097152), (12,heap,0,0,0,0,(long)heap),
            (10,va+33554432,6291456,0,0,0,0),
            (9,va+33554432,6291456,3,0x32,~0UL,(long)va+33554432),
            (11,va,huge,0,0,0,0), (231,42,0,0,0,0,0)
        ] : ownership ?
        [
            (9,0,huge,0,0x22,~0UL,(long)va), (12,0,0,0,0,0,(long)heap),
            (12,heap+8192,0,0,0,0,(long)heap+8192), (10,heap,4096,1,0,0,0),
            (12,heap+4113,0,0,0,0,(long)heap+4113), (12,heap+4096,0,0,0,0,(long)heap+4096),
            (12,heap+8192,0,0,0,0,(long)heap+8192), (12,heap,0,0,0,0,(long)heap),
            (9,heap+4096,4096,3,0x32,~0UL,(long)heap+4096), (10,heap+4096,4096,1,0,0,0),
            (12,heap+8192,0,0,0,0,(long)heap), (9,heap+4096,4096,0,0x100022,~0UL,-17),
            (12,0,0,0,0,0,(long)heap), (11,heap,4096,0,0,0,0),
            (12,heap+4096,0,0,0,0,(long)heap), (9,heap,4096,0,0x32,~0UL,(long)heap),
            (12,heap+4096,0,0,0,0,(long)heap), (1,1,heap+4224,24,0,0,24),
            (9,heap+4096,4096,0,0x32,~0UL,(long)heap+4096), (12,heap+8192,0,0,0,0,(long)heap),
            (11,va,huge,0,0,0,0), (231,42,0,0,0,0,0)
        ] :
        [
            (9,0,huge,0,0x22,~0UL,(long)va), (10,va+4096,8192,3,0,0,0),
            (10,va+4096,4096,1,0,0,0), (10,va+4096,4096,3,0,0,0),
            (9,va+4096,4096,0,0x32,~0UL,(long)va+4096), (10,va+4096,4096,3,0,0,0),
            (1,1,va+8184,24,0,0,24), (11,va+8192,4096,0,0,0,0),
            (10,va+4096,8192,3,0,0,-12), (10,va+4096,4096,7,0,0,-22),
            (9,0,4096,3,0x800022,~0UL,-22), (9,va+4096,4096,0,0x100022,~0UL,-17),
            (9,0,16781312,3,0x22,~0UL,-12), (28,va+4096,4096,16,0,0,0), (28,va+4096,4096,17,0,0,0),
            (204,0,8,0x401100,0,0,8), (99,0x401180,0,0,0,0,0),
            (228,1,0x401120,0,0,0,0), (228,1,0x401130,0,0,0,0),
            (318,va+4224,16,0,0,0,16), (318,0x401150,16,0,0,0,16), (318,0x401140,16,1,0,0,-11),
            (12,0,0,0,0,0,(long)heap), (12,heap+8192,0,0,0,0,(long)heap+8192),
            (12,0,0,0,0,0,(long)heap+8192), (12,heap,0,0,0,0,(long)heap),
            (25,0,4096,8192,0,0,-12), (25,0x6ffef000,4096,8192,0,0,-14),
            (11,va,4096,0,0,0,0), (11,va,huge,0,0,0,0), (231,42,0,0,0,0,0)
        ];
        uint ordinal = 0, barriers = 0, calls = 0, writes = 0; bool pendingWrite = false;
        byte[] elf = LinuxMemoryProofFixture.CreateElf(fragmented ? "FragmentedBacking" : ownership ? "HeapOwnership" : "Memory");
        ulong[] instructionRips = LinuxMemoryCallRips(elf);
        Need(instructionRips.Length == callCount, "m16_006_instruction_inventory");
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
            Need(calls <= callCount && rip == instructionRips[calls - 1] && rip >= 0x400202 && rip < 0x400f00 && elf[(int)(rip - 0x400000 - 2)] == 0x0f &&
                elf[(int)(rip - 0x400000 - 1)] == 5 && row["rsp"] is >= 0x6fff0008 and <= 0x70000000 &&
                (row["flags"] & 2) != 0 && (row["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0 && row["a5"] == 0,
                "m16_006_captured_frame");
            if (row["nr"] == 324)
            {
                ulong command = barriers == 0 ? 0UL : barriers == 1 ? 16UL : 8UL;
                Need(!ownership && !fragmented && ordinal == 17 && barriers < 3 && row["a0"] == command && row["a1"] == 0 && row["a2"] == 0 &&
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
        Need(ordinal == expected.Length && barriers == (ownership || fragmented ? 0U : 3U) && calls == callCount && writes == 1 && !pendingWrite &&
            exit["code"] == 42 && exit["calls"] == calls && exit["writes"] == 1 && exit["bytes"] == 24, "m16_006_accounting");
        return lines[..start];
    }

    // Separate expected-negative entry point. The ordinary smoke profile still
    // requires TEST_PASS and fails these guests; this receipt proves only the
    // exact absent-leaf or foreign-protection challenge, parent and restoration.
    public static void AssertLinuxMemoryNegativeCapture(string serialPath, string packagePath, string variant)
    {
        Need(variant is "GuardFault" or "ProtNoneFault" or "HeapOwnershipFault", "m16_006_negative_variant");
        AssertM03AcpiBootstrapTranscript(serialPath, firstRealExit: true, ownedSurvival: true,
            linuxElfPackagePath: packagePath, workloadServiceLoop: true, linuxStartup: true, linuxSyscalls: true,
            linuxMemory: true, linuxMemoryNegativeVariant: variant);
    }

    public static string[] ValidateLinuxMemoryNegativeTail(string[] lines, byte[] package, string variant)
    {
        Need(variant is "GuardFault" or "ProtNoneFault" or "HeapOwnershipFault", "m16_006_negative_variant");
        Need(package.AsSpan().SequenceEqual(LinuxMemoryProofFixture.CreatePackage(variant)), "m16_006_negative_identity");
        bool heap = variant == "HeapOwnershipFault";
        int start = Array.IndexOf(lines, "TEST_START m16-006"), count = heap ? 6 : variant == "GuardFault" ? 2 : 1;
        Need(start == QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 7 + 17 && lines.Length == start + count + 6,
            "m16_006_negative_inventory");
        string[] tail = lines[start..]; LinuxMemoryPolicy(tail[1]); _ = LinuxSyscallArm(tail[2]);
        Need(tail[^2] == LinuxSyscallRestored && tail[^1] == "TEST_FAIL m16-006 user-boundary-terminated status=1", "m16_006_negative_restoration");
        byte[] elf = LinuxMemoryProofFixture.CreateElf(variant); ulong[] rips = LinuxMemoryCallRips(elf);
        Need(rips.Length == count, "m16_006_negative_instruction_inventory");
        const ulong va = 0x100000000, h = 0x400000000;
        (ulong Nr, ulong A0, ulong A1, ulong A2, ulong A3, ulong A4, ulong Value)[] expected = heap ?
        [
            (9,0,8234266624,0,0x22,~0UL,va), (12,0,0,0,0,0,h),
            (9,h+4096,4096,3,0x32,~0UL,h+4096), (10,h+4096,4096,1,0,0,0),
            (12,h+8192,0,0,0,0,h), (12,0,0,0,0,0,h)
        ] : variant == "GuardFault" ?
        [(9,0,8234266624,0,0x22,~0UL,va), (10,va+4096,4096,3,0,0,0)] :
        [(9,0,8234266624,0,0x22,~0UL,va)];
        for (int i = 0; i < count; i++)
        {
            var call = ReadRecord(tail[3 + i], "M16_005_CALL", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec", "rip:dec", "rsp:dec", "flags:dec", "valueBits:dec", "action:dec", "status:dec");
            var e = expected[i];
            Need(call["nr"] == e.Nr && call["a0"] == e.A0 && call["a1"] == e.A1 && call["a2"] == e.A2 &&
                call["a3"] == e.A3 && call["a4"] == e.A4 && call["a5"] == 0 &&
                call["valueBits"] == e.Value && call["rip"] == rips[i] &&
                call["rsp"] is >= 0x6fff0008 and <= 0x70000000 && (call["flags"] & 2) != 0 &&
                (call["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0 && call["action"] == 1 && call["status"] == 1, "m16_006_negative_call");
        }
        const string disposition = " disposition=terminate";
        Need(tail[^3].EndsWith(disposition, StringComparison.Ordinal), "m16_006_negative_disposition");
        var fault = ReadRecord(tail[^3][..^disposition.Length], "M16_005_FAULT", "vector:dec", "error:dec", "rip:dec", "rsp:dec", "cr2:dec", "cs:dec", "ss:dec");
        int instruction = heap ? elf.AsSpan(0x200, 0xd00).LastIndexOf(new byte[] { 0x48, 0x89, 0x10 })
            : elf.AsSpan(0x200, 0xd00).IndexOf(new byte[] { 0x48, 0x8b, 0 });
        Need(instruction >= 0 && fault["rip"] == 0x400200UL + (uint)instruction && fault["vector"] == 14 && fault["error"] == (heap ? 7UL : 4UL) &&
            fault["cr2"] == (heap ? h+4096 : variant == "GuardFault" ? va : va+4096) && fault["cs"] == 59 &&
            fault["ss"] == 51 && fault["rsp"] is >= 0x6fff0008 and <= 0x70000000, "m16_006_negative_fault");
        return lines[..start];
    }

    private static void LinuxMemoryPolicy(string line, uint entropy = 32,
        uint expectedPoolPages = LinuxMemoryServices.DefaultPoolPages, bool continuingService = false)
    {
        Need(continuingService?entropy==0:entropy is 32 or 256 or 4096,"m16_006_entropy_policy");
        Need(expectedPoolPages is LinuxMemoryServices.DefaultPoolPages or LinuxMemoryServices.ApplicationPoolPages ||
            continuingService && expectedPoolPages == OS.Manifest.LinuxAppConfiguration.ConfiguredServicePoolPages,"m16_006_backing_policy");
        string suffix = " poolPages="+expectedPoolPages.ToString(System.Globalization.CultureInfo.InvariantCulture)+
            " vaEnd=68719476736 reservationQuota=17179869184 entropyBytes="+
            entropy.ToString(System.Globalization.CultureInfo.InvariantCulture)+" cpus=1 root=inactive-kernel";
        Need(line.EndsWith(suffix, StringComparison.Ordinal), "m16_006_policy");
        var policy = ReadRecord(line[..^suffix.Length], "M16_006_POLICY", "physical:dec", "available:dec");
        Need(policy["physical"] <= 0x100000000 && policy["physical"] >= expectedPoolPages*4096UL &&
            policy["available"] >= expectedPoolPages*4096UL &&
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
