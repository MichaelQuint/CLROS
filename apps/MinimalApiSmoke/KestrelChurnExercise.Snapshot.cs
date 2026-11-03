using System.Globalization;
using System.Diagnostics;

internal sealed partial class KestrelChurnExercise
{
    /// <summary>Atomic copied counters only. Waiting counts active timer awaits;
    /// Collections counts forced calls with a verified positive full-GC delta,
    /// which can overlap other requests' collections. No history is retained.</summary>

    internal readonly record struct Snapshot(int Active, int Waiting, int PeakActive, long Started, long Completed, long Canceled, long Failed, long Rejected, long Timers, long Workers, long Collections, long HeldBytes, long PeakHeldBytes);
}
