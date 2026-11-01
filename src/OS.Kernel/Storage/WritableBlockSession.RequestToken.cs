using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class WritableBlockSession
{
    /// <summary>Copied identity only. The exact retained owner, epoch and
    /// generation authenticate cancellation; OUT/FLUSH completion revokes the
    /// token. Successful IN retains it until CopyRead or cancellation.</summary>
    public readonly struct RequestToken
    {
        public readonly ulong Owner, Epoch, Generation;
        internal RequestToken(State* s) { Owner = (ulong)s; Epoch = s->Input.Epoch; Generation = s->Generation; }
        public bool IsInitialized => Owner != 0 && Epoch != 0 && Generation != 0;
    }
}
