using System.Globalization;

namespace OS.Tooling;
public static partial class NativeAotDiskTimingPeer
{
    /// <summary>Copied timing-only dispatch observation. Selected is a raw
    /// clock tick sampled after validated selection; an IRQ can advance the
    /// return epoch. This value grants no clock, thread or execution authority.</summary>

    public readonly record struct SelectionObservation(ulong Sequence, ulong Identity, ulong SelectionEpoch, ulong ReturnEpoch, ulong Selected, ulong PeriodFemtoseconds);
}
