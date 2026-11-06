using System.Globalization;

namespace OS.Tooling;

public static partial class NativeDiskReadObservations
{
    /// <summary>Separately authenticated owner/counter boundaries. Bootstrap is
    /// drained: its submitted and completed counts are BootstrapCompleted, with
    /// BootstrapCopied successful copies. Ordinary final Submitted must equal
    /// Completed; the separate reset-retirement inspector admits one pending
    /// submit with its exact reset receipt. Counts include bootstrap; they are
    /// not deltas or execution authority.</summary>

    public readonly record struct Boundary(ulong Owner, ulong Epoch, ulong BootstrapCompleted, ulong BootstrapCopied, ulong Submitted, ulong Completed, ulong Copied);
}
