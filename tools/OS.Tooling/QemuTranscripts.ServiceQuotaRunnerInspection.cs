using System.Globalization;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied capture observations only. Quiet/lifetime use milliseconds;
    /// terminal extent uses bytes and frame/PCAP observations use counts. No file,
    /// process, packet, peer, syscall, native or guest authority is constructed.</summary>

    public readonly record struct ServiceQuotaRunnerInspection(string FramesJson, int ProcessId, DateTimeOffset StartUtc, DateTimeOffset EndUtc, long LifetimeMilliseconds, long TerminalBytes, long QuietMilliseconds, long ForwardedFrames, QemuEthernetCaptureReceipt Pcap)
    {
        public string Outcome => "captured";
        public string Acceptance => "pending";
    }
}
