namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Observed TCP facts only. Missing FIN/RST is not an error or an
    /// invented orderly close. Flags do not prove fd/worker/kernel cleanup.</summary>

    public readonly record struct ServiceQuotaPacketFacts(int SynRecord, bool SynAck, bool ClientReady, bool ClientFin, bool ServerFin, bool ClientFinAcked, bool ServerFinAcked, bool ServerFinFirst, int ClientFinRecord, int ServerFinRecord, int ResetRecord);
}
