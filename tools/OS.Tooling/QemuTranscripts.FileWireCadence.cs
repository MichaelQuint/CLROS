namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Immutable host-only values for one selected GET. Witness ranges
    /// retain exact original record/time; entity witnesses are clipped to entity
    /// bytes. RequestComplete is the last first-appearance request byte record;
    /// RequestLastPayload includes later request retransmissions. Both request
    /// deltas are signed, preserving response-before-request-end observations.
    /// EntityComplete permits a full-body hash and complete entity span;
    /// an incomplete entity has neither. Header/body holes are never hashed.
    /// ParserModuleVersionId identifies the executing tooling module, not source
    /// provenance. Capture EOF is separate from observed FIN/reset/ACK facts and
    /// cannot establish deliberate host stop, natural cleanup or Full acceptance.
    /// Packets and Ranges are copied read-only collections with no input backing.</summary>
    public sealed record FileWireCadence(
        FileWireTarget Target, int CaptureBytes, string CaptureSha256,
        string ParserModuleVersionId, int AdmittedConnections, int QuietStartupSyns,
        int RetainedEstablishedPackets, ServiceWireByteRange RequestFirst,
        ServiceWireByteRange RequestComplete, ServiceWireByteRange? FirstResponseData,
        long? RequestCompleteToFirstResponseMicroseconds,
        long RequestLastPayloadRecord, ulong RequestLastPayloadTimestampMicroseconds,
        long? RequestLastPayloadToFirstResponseMicroseconds, int ResponseHeaderBytes,
        int ObservedEntityBytes, bool EntityComplete, string BodyStatus,
        string? VerifiedBodySha256, ServiceWireByteRange? FirstEntityByte,
        ServiceWireByteRange? LastEntityByte, long? CompleteEntitySpanMicroseconds,
        int? ClientFinRecord, int? ServerFinRecord, int? ResetRecord,
        bool ClientFinAcknowledged, bool ServerFinAcknowledged,
        bool CaptureEndedAtPcapEof, bool CaptureEndedWithOpenFlow,
        int IncomingAckPackets, int NonAdvancingAckPackets,
        IReadOnlyList<ServiceWireByteRange> Ranges,
        IReadOnlyList<FileWirePacketCadence> Packets);
}
