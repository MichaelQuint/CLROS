using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Exact explicit runner frame plus its copied producer window.
    /// Ordinary UTC windows retain their original count. These values grant no
    /// ownership, original file/process, packet, native or guest authority.</summary>

    public sealed record WebRaceRunnerInspection(WebRunnerReport Runner, WebRaceRendezvous.Report Gate, WebUtcObservation ProducerWindow);
}
