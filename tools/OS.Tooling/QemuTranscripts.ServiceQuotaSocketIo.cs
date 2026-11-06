namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One original post-CALL accept or positive socket I/O observation.
    /// Offsets/counts measure application queue acceptance/consumption, not ACKs
    /// or transmission. Peek views bytes without advancing Received. Record
    /// indices address the supplied original normalized serial rows.</summary>

    public readonly record struct ServiceQuotaSocketIo(int RecordIndex, int CallRecordIndex, ulong Identity, ulong Ordinal, ulong Number, int Descriptor, uint DescriptorGeneration, uint Description, ulong Inode, ServiceQuotaSocketIdentity Connection, ulong Offset, uint Bytes, bool Peek, ulong Sent, ulong Received)
    {
        public bool Accept => Number is 43 or 288;
        public bool Response => Number is 1 or 20 or 44 or 46;
    }
}
