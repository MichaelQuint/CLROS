using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Check the entire raw focused runtime leaf against its canonical
    /// package and sealed reviewed instruction lookup. Caller-owned inputs are
    /// borrowed synchronously. Parent firmware/startup/PCI/APIC/root and runner
    /// provenance remain mandatory outside this leaf. Missing, inconsistent or
    /// unsupported records throw; success supplies no independent QEMU authority.</summary>

    public static void ValidateNativeAotThreadingTail(string root, byte[] package, NativeAotThreadingProgram program, ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null, "threading_program_missing");
        string app = program!.Application;
        NativeAotThreadingPackage.RequireCurrentSources(root, app);
        NativeAotThreadingPackage.RequirePackage(app, package);
        int offset = 64 + Encoding.ASCII.GetByteCount(NativeAotThreadingPackage.Manifest(app));
        var output = NativeAotThreadingOutput.Read(app, rawTail);
        _ = new ThreadingProtocol(app, Rows(output), package[offset..], program.IsInstruction, program.IsSyscallReturn, output.WriteLengths, userRoot, kernelRoot).Validate();
        NativeAotThreadingPackage.RequireCurrentSources(root, app);
    }

    /// <summary>Validate one complete broader runtime leaf against its exact
    /// canonical package, sealed complete instruction index and original raw
    /// stdout. Kernel scheduling, signals, waits, workload phases and cleanup
    /// must agree. Parent firmware/root/runner provenance remains mandatory;
    /// this leaf alone grants no guest or runtime acceptance. Matrix additionally
    /// requires the validated parent's HPET frequency to bind raw syscall timing
    /// bounds to its reported monotonic timestamps; missing domains fail closed.</summary>

    public static void ValidateNativeAotRuntimeTail(string root, byte[] package, NativeAotRuntimeProgram program, ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot, ulong hpetFrequencyHz = 0)
    {
        Need(program is not null, "runtime_program_missing");
        string app = program!.Application;
        NativeAotRuntimePackage.RequireCurrentSources(root, app);
        NativeAotRuntimePackage.RequirePackage(app, package);
        var output = NativeAotThreadingOutput.Read(app, rawTail);
        int offset = 64 + Encoding.ASCII.GetByteCount(NativeAotRuntimePackage.Manifest(app));
        _ = new ThreadingProtocol(app, Rows(output), package[offset..], program.IsInstruction, program.IsSyscallReturn, output.WriteLengths, userRoot, kernelRoot, output.Stdout, hpetFrequencyHz).Validate();
        NativeAotRuntimePackage.RequireCurrentSources(root, app);
    }

    /// <summary>Value-only broader parser seam for independent synthetic rows.
    /// No caller predicate, stdout or report constructs sealed program authority.</summary>

    public static ThreadingProtocolReport InspectNativeAotRuntimeProtocolForTests(string app, string[] rows, byte[] elf, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, IReadOnlyList<int> lengths, string stdout, ulong userRoot, ulong kernelRoot, ulong hpetFrequencyHz = 0) => new ThreadingProtocol(app, rows, elf, instruction, syscallReturn, lengths, userRoot, kernelRoot, stdout, hpetFrequencyHz).Validate();
    /// <summary>Value-only parser seam for independent synthetic vectors. Neither
    /// caller predicates nor these counts construct a prepared program or prove
    /// execution. Production uses the sealed package/index and original raw bytes.</summary>

    public static ThreadingProtocolReport InspectNativeAotThreadingProtocolForTests(string app, string[] rows, byte[] elf, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, IReadOnlyList<int> writeLengths, ulong userRoot, ulong kernelRoot) => new ThreadingProtocol(app, rows, elf, instruction, syscallReturn, writeLengths, userRoot, kernelRoot).Validate();
    private static string[] Rows(NativeAotThreadingOutput.Capture capture)
    {
        Need(capture.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "threading_raw_terminal");
        return capture.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
    }

    // Parent framing uses normalized text only after the original byte envelopes
    // and complete stdout pass. The complete leaf still validates original bytes.
    private static string NormalizeThreadingWrites(string text, string app)
    {
        const string start = "TEST_START m16-009\r\n";
        int at = text.IndexOf(start, StringComparison.Ordinal);
        Need(at >= 0 && text.IndexOf(start, at + start.Length, StringComparison.Ordinal) < 0, "threading_raw_leaf");
        var capture = NativeAotThreadingOutput.Read(app, Encoding.UTF8.GetBytes(text[at..]));
        return text[..at] + capture.NormalizedTail;
    }

    // Outer records are kernel output, never payload stdout. Validate the exact
    // wrapper before handing only the original inner bytes to the leaf decoder.
    private static ReadOnlySpan<byte> RuntimeRawLeaf(string app, ReadOnlySpan<byte> raw)
    {
        var markers = NativeAotRuntimeSelection.Markers(app);
        byte[] start = Encoding.ASCII.GetBytes(markers.Start + "\r\n");
        byte[] end = Encoding.ASCII.GetBytes(markers.Ready + "\r\n" + markers.Pass + "\r\n");
        int at = raw.IndexOf(start);
        Need(at >= 0 && raw[(at + start.Length)..].IndexOf(start) < 0 && raw.EndsWith(end), "runtime_profile_envelope");
        int length = raw.Length - at - start.Length - end.Length;
        Need(length > 0, "runtime_profile_extent");
        ReadOnlySpan<byte> leaf = raw.Slice(at + start.Length, length);
        Need(leaf.StartsWith("TEST_START m16-009\r\n"u8), "runtime_profile_inner_start");
        return leaf;
    }

    private static string NormalizeRuntimeWrites(string text, string app)
    {
        var markers = NativeAotRuntimeSelection.Markers(app);
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        var capture = NativeAotThreadingOutput.Read(app, RuntimeRawLeaf(app, bytes));
        int at = text.IndexOf(markers.Start + "\r\n", StringComparison.Ordinal) + markers.Start.Length + 2;
        return text[..at] + capture.NormalizedTail + markers.Ready + "\r\n" + markers.Pass + "\r\n";
    }

    /// <summary>Value-only framing inspection for synthetic controls. This
    /// validates no package, firmware, runner or sealed program authority.</summary>

    public static string InspectNativeAotRuntimeEnvelopeForTests(string app, ReadOnlySpan<byte> raw) => NormalizeRuntimeWrites(DecodeUtf8(raw.ToArray()), app);
    // Both wrapper capture-hash verification and direct complete validation use
    // these bounded plain, single-link, unchanged file authorities. Reopening
    // legacy parent/runner readers cannot switch evidence during acceptance.
    internal static AuthorityFiles HoldThreadingEvidence(string root, QemuSmokeOptions options)
    {
        AuthorityFiles files = new();
        try
        {
            string serial = Path.GetFullPath(options.SerialLog, root), summary = Path.GetFullPath(options.SummaryLog, root);
            var raw = files.Open(Path.GetDirectoryName(serial)!, serial, "threading serial", "threading_evidence_serial", 8 * 1024 * 1024, singleLink: true);
            var report = files.Open(Path.GetDirectoryName(summary)!, summary, "threading summary", "threading_evidence_summary", 16 * 1024 * 1024, singleLink: true);
            AuthorityFiles.RequireDistinct(raw, report);
            return files;
        }
        catch
        {
            files.Dispose();
            throw;
        }
    }
}
