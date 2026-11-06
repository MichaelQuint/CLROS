namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One validated native accept or positive socket CALL in an added
    /// lifetime after the ordinary eight controls. CallRecordIndex is the actual
    /// original normalized CALL row, not an invented post-CALL diagnostic row.
    /// DescriptorGeneration/OpenDescription are replay IDs; tuple/generation
    /// comes from the checked native transport lifetime. Counters are replayed
    /// accepted/consumed bytes, not transmission, pointed bytes or allocation.</summary>

    public readonly record struct WebSignalRaceSocketCall(int CallRecordIndex, ulong Identity, ulong Ordinal, ulong Number, int Descriptor, uint DescriptorGeneration, int OpenDescription, ServiceQuotaSocketIdentity Connection, ulong Offset, uint Bytes, bool Peek, ulong Sent, ulong Received)
    {
        public bool Accept => Number is 43 or 288;
        public bool Response => Number is 44 or 46;
    }
}
