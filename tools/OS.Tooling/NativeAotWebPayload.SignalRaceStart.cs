namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Capture the explicit v2 one-shot initial-start stimulus. Start
    /// the pending race and observe phase2 before short churn, then issue one
    /// start POST during observed churn progress. This diagnostic client owns
    /// its requests/reports but supplies no actual blocked-wait, scheduler,
    /// process/native or guest authority. Production owners add their separate
    /// original initial-wait preparation through the internal hook set.</summary>

    public static Task<string> CaptureStartedSignalRaceAsync(HttpClient client, string endpoint, string expectedHealthBody, string observationPath, CancellationToken cancellation = default) => CaptureSignalRaceCoreAsync(client, endpoint, expectedHealthBody, observationPath, cancellation, null, startGate: true);
    /// <summary>Capture the explicit event-owned v3 app contract. Own one
    /// 150-second total capture deadline and join every request on failure.
    /// The borrowed client must have an infinite or at least 150-second request
    /// timeout and no default signal/churn selector headers; it is never mutated.
    /// Race requests select the v3 header and
    /// copied prefixes/attempt fields must match. This produces copied HTTP
    /// evidence only; native waits, ownership and cleanup require original proof.</summary>

    public static Task<string> CaptureEventOwnedSignalRaceAsync(HttpClient client, string endpoint, string expectedHealthBody, string observationPath, CancellationToken cancellation = default) => CaptureSignalRaceCoreAsync(client, endpoint, expectedHealthBody, observationPath, cancellation, null, startGate: true, eventOwned: true);
    // Exact selectors for the unfinished v3 contract. Only its initial churn
    // uses continuing v2; final fresh churn and the ordinary probe retain v1.
    private static string EventSignalVersion(string method, string path) => path.StartsWith("/__clros/signalrace", StringComparison.Ordinal) ? "v3" : method == "POST" && path == "/__clros/signalprobe" ? "v1" : "";
    private static string EventChurnVersion(string method, string path, bool initial) => method == "POST" && path == "/__clros/churn" ? (initial ? "v2" : "v1") : "";
}
