using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Storage;

public static unsafe partial class WritableBlockReadPort
{
    /// <summary>Copied port identity only. It authenticates one admitted IN;
    /// successful Copy, held cancellation or drained cancellation revokes it.</summary>
    public readonly struct Token
    {
        public readonly ulong Owner, Epoch, Generation;
        internal Token(State* s) { Owner = (ulong)s; Epoch = s->Input.Epoch; Generation = s->Generation; }
        public bool IsInitialized => Owner != 0 && Epoch != 0 && Generation != 0;
    }
}
