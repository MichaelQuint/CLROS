using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
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
        const string armedSuffix = " guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse";
        Need(tail[1].EndsWith(armedSuffix, StringComparison.Ordinal), "m16_005_arm_receipt");
        var armed = ReadRecord(tail[1][..^armedSuffix.Length], "M16_005_ARMED", "root:dec", "cs:dec", "ss:dec", "kernelStack:dec");
        Need(armed["root"] is >= 4096 and < 0x100000000 && armed["root"] % 4096 == 0 &&
            armed["cs"] == 59 && armed["ss"] == 51 && armed["kernelStack"] is >= 81920 and <= 0x100000000 &&
            armed["kernelStack"] % 4096 == 0, "m16_005_geometry");
        Need(tail[^4] == "M16_005_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "m16_005_restoration");
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
        Need(exited && pendingBytes == 0 && calls >= 2 && writes > 0 && exit["calls"] == calls && exit["writes"] == writes &&
            exit["bytes"] == writtenBytes && writtenBytes <= 64 * 4096 && exit["code"] <= uint.MaxValue, "m16_005_accounting");
        return lines[..start];
    }
}
