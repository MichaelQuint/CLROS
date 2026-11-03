using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Value-only complete scheduler/syscall replay for the explicit
    /// configured quota fixture. Borrow normalized protocol rows, original write
    /// lengths/channels, caller ELF and predicates synchronously without mutation.
    /// Caller-selected metadata/transport rows are outside this seam; retain the
    /// actual QUOTA_ABORT, every native fd-close, MEMORY/BUDGET/OWNER_END99 and
    /// QUOTA_PROCESS_FAILURE/NATIVE/RESTORE rows in their original order.
    /// No caller input grants original framing, publication/index/GC-arena,
    /// physical-denial, parent, transport, app/peer/packet or guest authority.</summary>

    public static ThreadingProtocolReport InspectNativeAotServiceQuotaProtocolForTests(byte[] elf, string[] rows, IReadOnlyList<int> lengths, IReadOnlyList<uint> channels, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot, uint settingsBytes, int settingsFd, ulong settingsIdentity, ulong settingsOrdinal)
    {
        Need(elf is not null && rows is not null && lengths is not null && channels is not null && rows.Length is >= 25 and <= 1_000_000 && rows.All(row => !string.IsNullOrEmpty(row) && !row.Contains('\r') && !row.Contains('\n')) && lengths.Count == channels.Count && channels.All(channel => channel is 1 or 2), "service_quota_protocol_input");
        var framed = new NativeAotThreadingOutput.Capture("", string.Join("\r\n", rows!) + "\r\n", lengths!.ToArray(), channels!.ToArray(), "");
        var capture = NativeAotSocketGuest.ReadServiceReadiness(framed);
        return new ThreadingProtocol(NativeAotServicePackage.Application, Rows(capture.Output), elf!, instruction, syscallReturn, capture.Output.WriteLengths, userRoot, kernelRoot, socketCapture: capture, webSettingsBytes: settingsBytes, webSettingsFd: settingsFd, webSettingsCall: (settingsIdentity, settingsOrdinal), configuredServiceMemory: true, serviceQuotaFailure: true).Validate();
    }
}
