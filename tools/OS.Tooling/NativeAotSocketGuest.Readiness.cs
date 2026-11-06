using System.Globalization;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotSocketGuest
{
    /// <summary>An actual poll, retained source3 completion or repark record. Poll
    /// Identity is zero on the supervisor or the current worker; Ready is the
    /// actual FIFO count. Idle marks one completed native outer idle step.
    /// Immediate Expire retains the preceding call's Ordinal and actual deadline.
    /// A nonzero
    /// PreviousSequence denotes repark; other fields retain canonical kernel
    /// enum values and exact observed table clock, without execution authority.</summary>

    public readonly record struct Readiness(int RecordIndex, ObservationKind Kind, ulong Identity, ulong Sequence, uint Completion, uint State, ulong Clock, ulong PreviousSequence, ulong Deadline, ulong ValueBits, uint Ready = 0, bool Idle = false, ulong Ordinal = 0);
}
