using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class ContinuingReadOnlyBlockService
{
    /// <summary>Copyable observation of one request, not device authority. Only
    /// the exact live owner/epoch/request may copy or cancel it. Tokens are
    /// revoked after copy, drained cancellation, fault or Stop.</summary>
    public readonly struct ReadToken
    {
        internal readonly ulong Owner, Epoch, Generation;
        internal ReadToken(ulong owner, ulong epoch, ulong generation)
        {
            Owner = owner;
            Epoch = epoch;
            Generation = generation;
        }
        public bool IsInitialized => Owner != 0 && Epoch != 0 && Generation != 0;
    }
}
