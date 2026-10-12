using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private const string LinuxSyscallRestored = "M16_005_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true";
    public static IReadOnlyList<string> M16LinuxSyscallMarkers { get; } = Array.AsReadOnly<string>(
        ["TEST_START m16-005", "M16_005_ARMED", "M16_005_CALL", "M16_005_WRITE", "M16_005_RESTORE", "M16_005_EXIT", "CLROS_M16_005_READY", "TEST_PASS m16-005"]);

    // Validate the syscall tail first, then retain the exact loader/service/owned
    // parent validators. Positive acceptance requires actual write and exit;
    // fault/rejection tails need a separate expected-negative receipt.
    public static string[] ValidateLinuxSyscallTail(string[] lines)
    {
        int start = Array.IndexOf(lines, "TEST_START m16-005");
        Need(start == QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 7 + 17, "m16_005_parent_record_count");
        string[] tail = lines[start..];
        Need(tail.Length >= 8 && tail[^2] == "CLROS_M16_005_READY" && tail[^1] == "TEST_PASS m16-005", "m16_005_terminal");
        _ = LinuxSyscallArm(tail[1]);
        Need(tail[^4] == LinuxSyscallRestored, "m16_005_restoration");
        var exit = ReadRecord(tail[^3], "M16_005_EXIT", "code:dec", "calls:dec", "bytes:dec", "writes:dec");
        ulong calls = 0, writes = 0, writtenBytes = 0, pendingFd = 0, pendingBytes = 0; bool exited = false;
        for (int index = 2; index < tail.Length - 4; index++)
        {
            string line = tail[index];
            if (line.StartsWith("M16_005_WRITE ", StringComparison.Ordinal))
            {
                int dataAt = line.IndexOf(" data=", StringComparison.Ordinal);
                Need(dataAt > 0, "m16_005_write_data_missing");
                var write = ReadRecord(line[..dataAt], "M16_005_WRITE", "fd:dec", "bytes:dec");
                Need(write["fd"] is 1 or 2 && write["bytes"] is > 0 and <= 4096 &&
                    (ulong)System.Text.Encoding.UTF8.GetByteCount(line[(dataAt + 6)..]) == write["bytes"] && !exited, "m16_005_write_extent");
                writes++; writtenBytes += write["bytes"];
                Need(pendingBytes == 0, "m16_005_duplicate_write"); pendingFd = write["fd"]; pendingBytes = write["bytes"];
                Need(index + 1 < tail.Length - 4 && tail[index + 1].StartsWith("M16_005_CALL nr=1 ", StringComparison.Ordinal), "m16_005_write_call_order");
                continue;
            }
            var call = ReadRecord(line, "M16_005_CALL", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec", "rip:dec", "rsp:dec", "flags:dec", "valueBits:dec", "action:dec", "status:dec");
            Need(!exited && ++calls <= 64 && call["rip"] is >= 4096 and < 0x100000000 && call["rsp"] is >= 4104 and <= 0x100000000 &&
                (call["flags"] & 2) != 0 && (call["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0, "m16_005_return_extent");
            if (call["nr"] is 60 or 231)
            {
                Need(call["action"] == (ulong)LinuxSyscallAction.Terminate && call["status"] == (ulong)LinuxSyscallGateStatus.Exited &&
                    (uint)call["a0"] == exit["code"] && index == tail.Length - 5, "m16_005_exit_call");
                exited = true;
            }
            else Need(call["action"] == (ulong)LinuxSyscallAction.Return && call["status"] == (ulong)LinuxSyscallGateStatus.Active, "m16_005_return_action");
            if (pendingBytes != 0)
            {
                Need(call["nr"] == 1 && (uint)call["a0"] == pendingFd && call["a2"] == pendingBytes && call["valueBits"] == pendingBytes,
                    "m16_005_write_result_binding"); pendingBytes = 0;
            }
            else if (call["nr"] == 1)
                Need(call["valueBits"] is 0 or unchecked((ulong)-9L) or unchecked((ulong)-14L) or unchecked((ulong)-22L), "m16_005_failed_write_result");
            else if (call["nr"] is not 60 and not 231)
                Need(call["valueBits"] == unchecked((ulong)-38L), "m16_005_unsupported_errno");
        }
        Need(exited && pendingBytes == 0 && calls >= 2 && writes > 0 && exit["code"] == 42 && exit["calls"] == calls && exit["writes"] == writes &&
            exit["bytes"] == writtenBytes && writtenBytes <= 64 * 4096 && exit["code"] <= uint.MaxValue, "m16_005_accounting");
        return lines[..start];
    }

    private static Dictionary<string, ulong> LinuxSyscallArm(string line)
    {
        const string suffix = " guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null";
        Need(line.EndsWith(suffix, StringComparison.Ordinal), "m16_005_arm_receipt");
        var armed = ReadRecord(line[..^suffix.Length], "M16_005_ARMED", "root:dec", "cs:dec", "ss:dec", "kernelStack:dec");
        Need(armed["root"] is >= 4096 and < 0x100000000 && armed["root"] % 4096 == 0 &&
            armed["cs"] == 59 && armed["ss"] == 51 && armed["kernelStack"] is >= 81920 and <= 0x100000000 &&
            armed["kernelStack"] % 4096 == 0, "m16_005_geometry");
        return armed;
    }

    // The ordinary smoke runner must still fail these guests. This separate
    // validator admits only the exact expected challenge, restoration and EOF.
    public static void AssertLinuxSyscallNegativeCapture(string serialPath, string packagePath, string variant)
    {
        Need(variant is "PrivilegedFault" or "KernelReadFault" or "InvalidRsp" or "ReturnToData", "m16_005_negative_variant");
        AssertM03AcpiBootstrapTranscript(serialPath, firstRealExit: true, ownedSurvival: true,
            linuxElfPackagePath: packagePath, workloadServiceLoop: true, linuxStartup: true, linuxSyscalls: true,
            linuxSyscallNegativeVariant: variant);
    }

    public static string[] ValidateLinuxSyscallNegativeTail(string[] lines, byte[] package, string variant)
    {
        Need(variant is "PrivilegedFault" or "KernelReadFault" or "InvalidRsp" or "ReturnToData", "m16_005_negative_variant");
        Need(package.AsSpan().SequenceEqual(LinuxSyscallProofFixture.CreatePackage(variant)), "m16_005_negative_package_identity");
        int start = Array.IndexOf(lines, "TEST_START m16-005");
        Need(start == QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + 7 + 17 && lines.Length == start + 5,
            "m16_005_negative_inventory");
        string[] tail = lines[start..]; var armed = LinuxSyscallArm(tail[1]);
        Need(tail[3] == LinuxSyscallRestored, "m16_005_negative_restoration");
        bool fault = variant is "PrivilegedFault" or "KernelReadFault";
        ulong status = (ulong)(fault ? LinuxSyscallGateStatus.Active : LinuxSyscallGateStatus.InvalidReturn);
        Need(tail[4] == "TEST_FAIL m16-005 user-boundary-terminated status=" + status, "m16_005_negative_terminal");
        byte[] elf = LinuxSyscallProofFixture.CreateElf(variant);
        if (fault)
        {
            const string disposition = " disposition=terminate";
            Need(tail[2].EndsWith(disposition, StringComparison.Ordinal), "m16_005_fault_disposition");
            var row = ReadRecord(tail[2][..^disposition.Length], "M16_005_FAULT", "vector:dec", "error:dec", "rip:dec", "rsp:dec", "cr2:dec", "cs:dec", "ss:dec");
            Need(row["cs"] == 59 && row["ss"] == 51 && row["rsp"] is >= 4104 and <= 0x100000000 &&
                row["rip"] is >= 0x400200 and < 0x400f00, "m16_005_fault_context");
            int offset = checked((int)(row["rip"] - LinuxSyscallProofFixture.ImageBase));
            if (variant == "PrivilegedFault")
                Need(row["vector"] == 13 && row["error"] == 0 && elf[offset] == 0xf4, "m16_005_privileged_fault");
            else
                Need(row["vector"] == 14 && row["error"] == 5 &&
                    row["cr2"] == armed["kernelStack"] - X64LinuxUserTransition.DataBytes + X64LinuxUserTransition.GdtOffset &&
                    elf.AsSpan(offset, 3).SequenceEqual(new byte[] { 0x48, 0x8b, 0 }), "m16_005_supervisor_fault");
        }
        else
        {
            var row = ReadRecord(tail[2], "M16_005_CALL", "nr:dec", "a0:dec", "a1:dec", "a2:dec", "a3:dec", "a4:dec", "a5:dec", "rip:dec", "rsp:dec", "flags:dec", "valueBits:dec", "action:dec", "status:dec");
            Need(row["action"] == (ulong)LinuxSyscallAction.Terminate && row["status"] == status && row["valueBits"] == 0 &&
                row["a0"] == 0 && row["a1"] == 0 && row["a2"] == 0 && row["a3"] == 0 && row["a4"] == 0 && row["a5"] == 0 &&
                (row["flags"] & 2) != 0 && (row["flags"] & ~LinuxSyscallGateway.UserRflagsMask) == 0,
                "m16_005_return_rejection");
            if (variant == "InvalidRsp")
            {
                Need(row["nr"] == 999 && row["rsp"] == 0xffff800000001000 && row["rip"] is >= 0x400202 and < 0x400f00,
                    "m16_005_hostile_rsp");
                int offset = checked((int)(row["rip"] - LinuxSyscallProofFixture.ImageBase));
                Need(elf.AsSpan(offset - 2, 2).SequenceEqual(new byte[] { 0x0f, 5 }), "m16_005_rsp_syscall_binding");
            }
            else Need(row["nr"] == 0 && row["rip"] == LinuxSyscallProofFixture.DataBase && row["rsp"] is >= 4104 and <= 0x100000000,
                "m16_005_nx_return");
        }
        return lines[..start];
    }
}
