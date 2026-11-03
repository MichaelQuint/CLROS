using System.Globalization;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>One contiguous guest response with a complete canonical Date
    /// header. End/State retain incomplete entity bodies; Ranges cover only the
    /// actual header and preserve original first-byte record/time identities.</summary>

    public sealed record WebSignalRaceDateObservation(int ConnectionId, uint RemotePort, int Start, int HeaderEnd, int End, string State, DateTimeOffset DateUtc, IReadOnlyList<ServiceQuotaHttpPacketRange> Ranges);
}
