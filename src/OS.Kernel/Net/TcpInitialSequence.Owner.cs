using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Net;

public static unsafe partial class TcpInitialSequence
{
    /// <summary>Zero before Initialize, inaccessible to applications and retained
    /// through Erase. Secret replacement and erasure require no outstanding lease.
    /// Epochs supplied by admission must be fresh and nonzero across storage reuse.</summary>

    public struct Owner
    {
        internal ulong Self, Epoch, Generation, Borrower, LastNow, QuietUntil, Issued, ClockStarted;
        internal fixed byte Key[32];
        public readonly ulong SequencesIssued => Issued;
        public readonly ulong QuietDeadline => QuietUntil;
        public readonly bool IsBorrowed => Borrower != 0;
    }
}
