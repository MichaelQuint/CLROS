using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class WritableBlockSession
{
    /// <summary>Copied terminal request facts. DeviceResult is the observed
    /// wire outcome; Cancelled is a separate caller disposition and cannot undo
    /// completed writes or successful flush acknowledgements. Bytes is512 for a
    /// successful IN/OUT and0 for FLUSH/errors. IN bytes remain borrowed DMA
    /// until CopyRead; completion alone is not a delivered-byte claim.
    /// A fault preserves exact failure and
    /// requires retirement before an externally owned retry; no replay occurs.</summary>
    public readonly struct Completion
    {
        public readonly RequestToken Token;
        public readonly Operation Kind;
        public readonly ulong Sector;
        public readonly uint Bytes;
        public readonly Status DeviceResult;
        public readonly bool Cancelled;
        internal Completion(State* s, Status result)
        {
            Token = new(s); Kind = s->Kind; Sector = s->Sector;
            Bytes = result == Status.Success && Kind != Operation.Flush ? 512U : 0;
            DeviceResult = result; Cancelled = s->CancelRequested;
        }
    }
}
