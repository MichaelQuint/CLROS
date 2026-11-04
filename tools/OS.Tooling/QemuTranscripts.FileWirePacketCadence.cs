namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One validated outgoing TCP payload transmission, including
    /// retransmissions and header-only payload. Record/time retain original
    /// PCAP order and microsecond units. EntityProgressBytes counts first body
    /// appearances. FirstCoveringAck is the earliest later normal cumulative
    /// ACK covering the whole payload's end; prior coverage is reported separately.
    /// NextProgress is the literal next transmission adding entity bytes, even
    /// when it precedes this ACK. Null endpoints give no interval; negative
    /// AckToNextProgress values are preserved, never treated as positive waits.
    /// These are capture cadence values, not RTT, CPU cost or causal attribution.</summary>
    public sealed record FileWirePacketCadence(
        long Record, ulong TimestampMicroseconds, uint Sequence, int StreamOffset,
        int TcpPayloadBytes, int EntityBytes, int NovelTcpBytes, int EntityProgressBytes,
        int DuplicateTcpBytes, bool AlreadyCoveredBeforeTransmission,
        bool RetransmissionBeforeCoveringAck, long? FirstCoveringAckRecord,
        ulong? FirstCoveringAckTimestampMicroseconds, long? DataToAckMicroseconds,
        long? NextProgressRecord, ulong? NextProgressTimestampMicroseconds,
        long? AckToNextProgressMicroseconds, bool NextProgressBeforeAck)
    {
        public uint Seconds => checked((uint)(TimestampMicroseconds / 1_000_000));
        public uint Microseconds => (uint)(TimestampMicroseconds % 1_000_000);
        public uint EndSequence => unchecked(Sequence + (uint)TcpPayloadBytes);
        public bool CrossesSequenceZero => EndSequence < Sequence;
        public bool IsEntityProgress => EntityProgressBytes != 0;
        public bool MissingCoveringAck => !AlreadyCoveredBeforeTransmission && FirstCoveringAckRecord is null;
        public bool MissingNextProgress => NextProgressRecord is null;
    }
}
