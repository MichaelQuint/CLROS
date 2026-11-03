namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Derived random accounting and exact fixture-control counts
    /// (16384/GRND_NONBLOCK partial, zero/nonblock, flag8 and NULL32/nonblock).
    /// These values authenticate no source, generator, mapping or guest capture.</summary>

    public readonly record struct ServiceRandomInspection(ulong Requests, ulong GeneratedBytes, int PartialRequests, int ZeroRequests, int InvalidFlagRequests, int FaultRequests);
}
