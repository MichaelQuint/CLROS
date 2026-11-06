using System.Globalization;

namespace OS.Tooling;

public static partial class NativeDiskReadObservations
{
    /// <summary>Read-only copied observations and the inspected cumulative
    /// boundaries. Cancelled counts only zero-byte completions after bootstrap.
    /// No caller array, image alias or complete serial record is retained.</summary>

    public sealed class Inspection
    {
        internal Inspection(Boundary boundary, ulong cancelled, Observation[] rows, PendingResetRetirement? reset = null)
        {
            Counters = boundary;
            Cancelled = cancelled;
            Rows = Array.AsReadOnly(rows);
            ResetRetirement = reset;
        }

        public Boundary Counters
        {
            get;
        }
        public ulong Cancelled
        {
            get;
        }
        public IReadOnlyList<Observation> Rows
        {
            get;
        }
        public PendingResetRetirement? ResetRetirement
        {
            get;
        }
    }
}
