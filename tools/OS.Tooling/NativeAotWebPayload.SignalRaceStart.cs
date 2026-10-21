namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Capture the explicit v2 one-shot initial-start stimulus. Start
    /// the pending race and observe phase2 before short churn, then issue one
    /// start POST during observed churn progress. This diagnostic client owns
    /// its requests/reports but supplies no actual blocked-wait, scheduler,
    /// process/native or guest authority. Production owners add their separate
    /// original initial-wait preparation through the internal hook set.</summary>
    public static Task<string> CaptureStartedSignalRaceAsync(HttpClient client,string endpoint,string expectedHealthBody,
        string observationPath,CancellationToken cancellation=default)=>CaptureSignalRaceCoreAsync(client,endpoint,
            expectedHealthBody,observationPath,cancellation,null,startGate:true);

    // Copied, validated six-field phase2 status is only scheduling input. Each
    // production hook must bind it to its actual owned native/serial evidence.
    // The native owner separately owns restoration/session-stop in its finally.
    internal sealed record RaceStartHooks(Func<ulong[],CancellationToken,Task> BeforeChurn,
        Func<CancellationToken,Task> BeforeStart,Func<CancellationToken,Task> AfterAcknowledged);
}
