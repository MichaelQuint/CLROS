using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Serialize copied web-race stream values under their separate
    /// strict schema. Reuses the bounded recorder's generic value records; quota
    /// purposes/roles/routes remain rejected. Failed writes retain unknown transfer.
    /// Values grant no original I/O, HTTP framing, packet or guest authority.</summary>
    public static string SerializeSignalRaceWire(NativeAotServiceQuotaPeer.WireTrace trace) => NativeAotServiceQuotaPeer.SerializeSignalRaceWireReport(trace);

    /// <summary>Inspect at most32MiB of copied web-race raw-I/O JSON. Requires
    /// closed/joined terminal stream shapes, bounded byte/offset/tick values and
    /// consistent request-write/exchange IDs. Returns read-only copied records;
    /// performs no I/O and authenticates no original capture or guest execution.
    /// HTTP FIFO framing, selected response bytes and PCAP joins remain separate.</summary>
    public static NativeAotServiceQuotaPeer.WireTrace InspectSignalRaceWire(ReadOnlySpan<byte> utf8) => NativeAotServiceQuotaPeer.InspectSignalRaceWireReport(utf8);

    /// <summary>Join copied logical attempts to the separately copied actual-
    /// stream report by exact exchange IDs, methods/routes, outcomes and bounded
    /// body bytes. Checks the selected contract when the companion claims success.
    /// Starts no I/O; this is not HTTP frame/PCAP/native/runner/guest admission.</summary>
    public static void ValidateSignalRaceTransport(string attemptsJson, string selectedJson, ReadOnlySpan<byte> wireJson, string endpoint, string expectedHealthBody)
        => _ = InspectSignalRaceTransport(attemptsJson, selectedJson, wireJson, endpoint, expectedHealthBody);

    // Internal callers borrow only the result of both strict copied inspectors.
    internal static NativeAotServiceQuotaPeer.WireTrace InspectSignalRaceTransport(string attemptsJson, string selectedJson, ReadOnlySpan<byte> wireJson, string endpoint, string expectedHealthBody)
    {
        var summary = InspectSignalRaceAttempts(attemptsJson, selectedJson, endpoint, expectedHealthBody);
        var wire = InspectSignalRaceWire(wireJson);
        RaceNeed(endpoint == "http://127.0.0.1:" + wire.Port.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            wire.Exchanges.Count == summary.Attempts && summary.SemanticComplete == (wire.Purpose == "signalrace-run"), "wire_attempts");
        using var document = JsonDocument.Parse(attemptsJson);
        var attempts = document.RootElement.GetProperty("attempts");
        for (int i = 0; i < wire.Exchanges.Count; i++)
        {
            var exchange = wire.Exchanges[i];
            var attempt = attempts[i];
            RaceNeed(exchange.Id == attempt.GetProperty("id").GetInt32() && exchange.Method == attempt.GetProperty("method").GetString() && exchange.Path == attempt.GetProperty("path").GetString() &&
                exchange.Outcome == attempt.GetProperty("outcome").GetString() && exchange.Status == attempt.GetProperty("status").GetInt32() && exchange.BodyBase64 == attempt.GetProperty("bodyBase64").GetString(), "wire_attempt_join");
        }
        return wire;
    }
}
