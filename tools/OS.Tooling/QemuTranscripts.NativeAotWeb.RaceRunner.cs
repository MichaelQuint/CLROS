using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Exact explicit runner frame plus its copied producer window.
    /// Ordinary UTC windows retain their original count. These values grant no
    /// ownership, original file/process, packet, native or guest authority.</summary>
    public sealed record WebRaceRunnerInspection(WebRunnerReport Runner,WebRaceRendezvous.Report Gate,WebUtcObservation ProducerWindow);

    /// <summary>Inspect borrowed explicit race runner bytes against a supplied
    /// canonical gate and producer UTC window. Reuse every exact ordinary
    /// process/HTTP/control/marker/tail/summary check, requiring both controls
    /// and normal logging. Only this explicit entry accepts the two gate rows.
    /// Require matching forward/PID/uppercase hashes and an exact gate profile.
    /// The optional explicit eventOwned expectation accepts a positive <=165s
    /// UTC producer window; the historical default remains <=45s. Both windows
    /// follow all ordinary controls and stay inside the original child lifetime.
    /// That window comes from the owning producer; copied values authenticate no
    /// timing, monotonic deadline or execution. Ordinary Date windows are not
    /// expanded or assigned to race requests. Missing, duplicate, misplaced,
    /// unknown or mismatched summary bytes throw; supplied data is not mutated.
    /// Original reports, owned runner/source/native/packet joins remain required.</summary>
    public static WebRaceRunnerInspection InspectNativeAotWebRaceRunnerForTests(QemuSmokeOptions options,QemuSmokePlan plan,
        ProcessResult runner,byte[] raw,byte[] summaryBytes,WebRunnerContext context,string body,string configuration,
        WebRaceRendezvous.Report gate,WebUtcObservation producerWindow,bool eventOwned=false)
    {
        Need(gate is not null,"web_race_runner_gate");
        var report=ReadWebRunner(options,plan,runner,raw,summaryBytes,context,body,configuration,false,true,gate,producerWindow,eventOwned);
        return new(report,gate! with{Digests=gate.Digests with{}},producerWindow);
    }

    private static void ValidateWebRaceRunnerWindow(WebRaceRendezvous.Report gate,WebUtcObservation producer,
        WebRunnerContext context,ulong pid,DateTime started,DateTime ended,IReadOnlyList<WebUtcObservation> ordinary,bool eventOwned)
    {
        bool Hash(string? text)=>text is {Length:64}&&!text.AsSpan().ContainsAnyExcept("0123456789ABCDEF");
        Need(gate.EventOwned==eventOwned&&gate.Port==context.ForwardPort&&gate.QemuProcessId>0&&(ulong)gate.QemuProcessId==pid&&gate.Digests is not null&&
            Hash(gate.Digests.SelectedSha256)&&Hash(gate.Digests.AttemptsSha256)&&Hash(gate.Digests.WireSha256),"web_race_runner_gate");
        Need(ordinary.Count==9&&producer.StartedUtc.Offset==TimeSpan.Zero&&producer.CompletedUtc.Offset==TimeSpan.Zero&&
            producer.StartedUtc.UtcDateTime>=started&&producer.CompletedUtc.UtcDateTime<=ended&&
            producer.StartedUtc>=ordinary[^1].CompletedUtc&&producer.CompletedUtc>producer.StartedUtc&&
            producer.CompletedUtc-producer.StartedUtc<=TimeSpan.FromSeconds(eventOwned?165:45),"web_race_runner_producer_window");
    }
}
