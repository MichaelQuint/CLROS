using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>
    /// Checks the complete unchanged Hello leaf against its exact package,
    /// immutable reviewed instruction index and original stdout bytes. The full
    /// caller must separately validate firmware, startup, PCI/APIC and runner
    /// provenance. No synthetic model report is accepted as execution authority.
    /// Input storage remains caller-owned and unchanged for this synchronous call.
    /// Missing, forged, unsupported or incomplete records throw ToolFailure.
    /// </summary>

    public static void ValidateLinuxHelloTail(string root, string[] tail, byte[] package, NativeAotHelloProgram program, ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null, "hello_program_missing");
        NativeAotHelloGuest.RequireCurrentSources(root);
        NativeAotHelloGuest.RequirePackage(package);
        NativeAotHelloOutput.Validate(rawTail);
        int offset = 64 + System.Text.Encoding.ASCII.GetByteCount(NativeAotHelloGuest.Manifest);
        _ = new HelloProtocol(tail, package[offset..], program!.IsInstruction, program!.IsSyscallReturn, userRoot, kernelRoot).Validate();
        NativeAotHelloGuest.RequireCurrentSources(root);
    }

    /// <summary>
    /// Independent parser/model test seam with caller-supplied ELF and instruction
    /// predicates. Returned counts grant no prepared-program or QEMU authority.
    /// Production admission uses only ValidateLinuxHelloTail and the sealed exact
    /// package/index. Synthetic rows never establish execution or runtime support.
    /// </summary>

    public static HelloProtocolReport InspectLinuxHelloProtocolForTests(string[] tail, byte[] elf, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot) => new HelloProtocol(tail, elf, instruction, syscallReturn, userRoot, kernelRoot).Validate();
    // Optional diagnostics never replace clock, scheduling or cleanup evidence.
    private static void LinuxTiming(string line, ulong identity, ulong epoch)
    {
        var r = ReadRecord(line, "M16_008_TIMING", "identity:dec", "epoch:dec", "start:dec", "arm:dec", "capture:dec", "ready:dec", "periodFs:dec");
        Need(r["identity"] == identity && r["epoch"] == epoch && r["periodFs"] is> 0 and <= 1_000_000_000 && r["start"] <= r["arm"] && r["arm"] <= r["capture"] && r["capture"] <= r["ready"], "linux_timing_observation");
    }

    private static Dictionary<string, ulong> LinuxSyscallTiming(string line, ulong identity, ulong ordinal, ulong number)
    {
        var r = ReadRecord(line, "M16_008_SYSCALL_TIME", "identity:dec", "ordinal:dec", "nr:dec", "entered:dec", "admitted:dec", "dispatched:dec", "prepared:dec", "periodFs:dec");
        Need(r["identity"] == identity && r["ordinal"] == ordinal && r["nr"] == number && r["periodFs"] is> 0 and <= 1_000_000_000 && r["entered"] <= r["admitted"] && r["admitted"] <= r["dispatched"] && r["dispatched"] <= r["prepared"], "linux_syscall_timing_observation");
        return r;
    }

    // Validate original bytes before removing only the three known payload LFs
    // for the existing CRLF record parser and runner's trimmed marker summary.
    private static string NormalizeHelloWrites(string text)
    {
        const string start = "TEST_START m16-009\r\n";
        int at = text.IndexOf(start, StringComparison.Ordinal);
        Need(at >= 0 && text.IndexOf(start, at + start.Length, StringComparison.Ordinal) < 0, "hello_raw_leaf");
        NativeAotHelloOutput.Validate(System.Text.Encoding.UTF8.GetBytes(text[at..]));
        foreach (string payload in new[]
        {
            "TEST_START nativeaot.hello",
            "NATIVEAOT_HELLO_OK",
            "TEST_PASS nativeaot.hello"
        }

        )
            text = text.Replace("data=" + payload + "\n\r\n", "data=" + payload + "\r\n", StringComparison.Ordinal);
        return text;
    }
}
