namespace OS.Tooling;

public static partial class NativeDiskReadObservations
{
    /// <summary>
    /// Bounded diagnostic counters after ordered metadata and image-CRC checks.
    /// Summary supplies no exact-byte evidence. This type cannot substitute for
    /// the ordinary byte-evidence Inspection. Original image/source/native/runner
    /// lifetime and authority remain the caller's responsibility.
    /// </summary>
    public sealed class DiagnosticInspection
    {
        internal DiagnosticInspection(Inspection inspection, bool payloadIncluded)
        {
            Counters = inspection.Counters;
            Cancelled = inspection.Cancelled;
            Rows = inspection.Rows.Count;
            CopiedSectors = inspection.Rows.Count(row => row.Kind is Kind.Copied or Kind.CopyChunk);
            PayloadIncluded = payloadIncluded;
        }

        public Boundary Counters { get; }
        public ulong Cancelled { get; }
        public int Rows { get; }
        public int CopiedSectors { get; }
        public bool PayloadIncluded { get; }
    }
}
