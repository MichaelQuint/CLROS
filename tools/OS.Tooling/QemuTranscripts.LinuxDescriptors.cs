using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static IReadOnlyList<string> M16LinuxDescriptorMarkers { get; } = Array.AsReadOnly<string>(
        ["TEST_START m16-007", "M16_006_POLICY", "M16_007_POLICY", "M16_005_ARMED", "M16_005_CALL", "M16_005_WRITE",
         "M16_005_RESTORE", "M16_005_EXIT", "M16_006_MEMORY", "M16_007_RELEASE", "CLROS_M16_007_READY", "TEST_PASS m16-007"]);

    /// <summary>Validate only the additive mechanical descriptor tail against
    /// the original complete package and emitted instructions. Parent firmware,
    /// startup, service and provenance validation still belongs to the original
    /// M03 entry point. Synthetic host rows are parser controls, never guests.</summary>
    public static string[] ValidateLinuxDescriptorTail(string[] lines, byte[] package)
    {
        var view = LinuxElfPackage.Validate(package);
        Need(view.Succeeded, "m16_007_package_invalid");
        string variant = Encoding.ASCII.GetString(package.AsSpan(checked((int)(view.ManifestOffset + view.Manifest.Args.Offset)), checked((int)view.Manifest.Args.Length)));
        Need(LinuxDescriptorProofFixture.Variants.Contains(variant, StringComparer.Ordinal) &&
            package.AsSpan().SequenceEqual(LinuxDescriptorProofFixture.CreatePackage(variant)), "m16_007_fixture_identity");
        int start = Array.IndexOf(lines, "TEST_START m16-007");
        Need(start == QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 7 + 17, "m16_007_parent_record_count");
        string[] tail = lines[start..];
        uint count = LinuxDescriptorProofFixture.ExpectedCalls(variant), expectedWrites = variant == "Descriptors" ? 4U : 1U;
        Need(tail.Length == count + expectedWrites + 10 && tail[^2] == "CLROS_M16_007_READY" && tail[^1] == "TEST_PASS m16-007", "m16_007_terminal");
        LinuxMemoryPolicy(tail[1]);
        const string memorySuffix = " poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel";
        var facts = ReadRecord(tail[1][..^memorySuffix.Length], "M16_006_POLICY", "physical:dec", "available:dec");
        Need(tail[2] == "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=12288 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=unsupported", "m16_007_descriptor_policy");
        var arm = LinuxSyscallArm(tail[3]);
        Need(tail[^6] == LinuxSyscallRestored, "m16_007_restoration");
        var exit = ReadRecord(tail[^5], "M16_005_EXIT", "code:dec", "calls:dec", "bytes:dec", "writes:dec");
        var memory = ReadRecord(tail[^4], "M16_006_MEMORY", "reserved:dec", "backed:dec", "tables:dec", "entropyRemaining:dec");
        var initial = ReadRecord(lines[QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 3], "M16_004_TABLES", "root:dec", "used:dec", "leaves:dec", "ranges:dec");
        Need(initial["root"] == arm["root"] && memory["reserved"] == 2097152 && memory["backed"] == 0 &&
            memory["tables"] == initial["used"] && memory["entropyRemaining"] == 32, "m16_007_memory_lifetime");
        Need(tail[^3] == "M16_007_RELEASE descriptors=0 pipes=0 scratch=zero snapshots=zero", "m16_007_descriptor_lifetime");
        byte[] elf = LinuxDescriptorProofFixture.CreateElf(variant);
        var rips = LinuxDescriptorProofFixture.ExpectedReturnRips(variant);
        uint calls = 0, writes = 0, positiveReads = 0;
        ulong writtenBytes = 0;
        string? pending = null;
        for (int i = 4; i < tail.Length - 6; i++)
        {
            string line = tail[i];
            if (line.StartsWith("M16_005_WRITE ", StringComparison.Ordinal))
            { Need(pending is null, "m16_007_extra_write"); pending = line; continue; }
            Need(calls < count, "m16_007_extra_call");
            var row = ReadRecord(line, "M16_005_CALL", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec",
                "rip:dec", "rsp:dec", "flags:dec", "valueBits:dec", "action:dec", "status:dec");
            int at = checked((int)(rips[(int)calls] - LinuxDescriptorProofFixture.ImageBase)), syscall = at - 2;
            bool fourth = elf[syscall - 6] == 0x41 && elf[syscall - 5] == 0xba;
            int setup = syscall - (fourth ? 26 : 20);
            Need(elf[syscall] == 0x0f && elf[syscall + 1] == 5 && elf[setup] == 0xb8 && elf[setup + 5] == 0xbf &&
                elf[setup + 10] == 0xbe && elf[setup + 15] == 0xba, "m16_007_instruction_binding");
            uint number = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(setup + 1));
            uint a0 = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(setup + 6));
            uint a1 = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(setup + 11));
            uint a2 = BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(setup + 16));
            uint a3 = fourth ? BinaryPrimitives.ReadUInt32LittleEndian(elf.AsSpan(syscall - 4)) : 0;
            Need(row["nr"] == number && row["a0"] == a0 && row["a1"] == a1 && row["a2"] == a2 && row["a3"] == a3 &&
                row["a4"] == 0 && row["a5"] == 0 && row["rip"] == rips[(int)calls] && row["rsp"] is >= 0x6fff0008 and <= 0x70000000 &&
                (row["flags"] & 2) != 0 && (row["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0,
                "m16_007_captured_frame");
            // pread resets R10 before its comparison. Every other returning
            // call compares RAX with a sign-extended imm32 or positive read.
            int compare = at + (fourth ? 6 : 0); long expected;
            if (number == 231) expected = 0;
            else if (elf[compare] == 0x48 && elf[compare + 1] == 0x3d)
                expected = BinaryPrimitives.ReadInt32LittleEndian(elf.AsSpan(compare + 2));
            else
            {
                Need(variant == "Descriptors" && number == 0 && a0 == 3 && a1 == 0x402000 && a2 == 128 &&
                    elf[compare] == 0x48 && elf[compare + 1] == 0x85 && elf[compare + 2] == 0xc0 && positiveReads < 2, "m16_007_positive_read");
                if (positiveReads++ == 0)
                {
                    string total = (facts["physical"] / 1024).ToString(CultureInfo.InvariantCulture);
                    string free = (facts["available"] / 1024).ToString(CultureInfo.InvariantCulture);
                    expected = ("MemTotal: " + total + " kB\nMemFree: " + free + " kB\nMemAvailable: " + free + " kB\n").Length;
                }
                else expected = 128;
            }
            bool exited = number == 231;
            Need(row["valueBits"] == unchecked((ulong)expected) && row["action"] == (exited ? 2UL : 1UL) &&
                row["status"] == (exited ? 2UL : 1UL) && (!exited || calls + 1 == count && a0 == 42 &&
                row["rip"] == LinuxDescriptorProofFixture.SuccessRip(variant)), "m16_007_return_binding");
            string? output = expected > 0 && (number == 1 && a0 is 1 or 2 || number == 20 && a0 == 1) ?
                variant == "Pipes" || calls + 2 == count ? LinuxDescriptorProofFixture.Witness(variant) :
                writes == 0 ? LinuxDescriptorProofFixture.FirstOutput : writes == 1 ? LinuxDescriptorProofFixture.SecondOutput : LinuxDescriptorProofFixture.VectorOutput : null;
            if (output is not null)
            {
                uint channel = writes == 1 && variant == "Descriptors" ? 2U : 1U;
                Need(pending == "M16_005_WRITE fd=" + channel + " bytes=" + output.Length + " data=" + output, "m16_007_owned_output");
                pending = null; writes++;
            }
            else Need(pending is null, "m16_007_write_order");
            if ((number == 1 || number == 20) && expected > 0) writtenBytes += (ulong)expected;
            calls++;
        }
        Need(calls == count && writes == expectedWrites && pending is null && positiveReads == (variant == "Descriptors" ? 2U : 0U) &&
            exit["code"] == 42 && exit["calls"] == count && exit["writes"] == writes && exit["bytes"] == writtenBytes, "m16_007_accounting");
        return lines[..start];
    }
}
