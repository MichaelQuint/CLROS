using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Storage;

public static unsafe partial class WritableBlockReadPort
{
    /// <summary>Client progress and the exact copied physical completion are
    /// separate. AvailableBytes is512 only for a held successful IN, otherwise0.
    /// Physical may report a successful512B cancelled IN without granting any
    /// caller bytes. The outer owner must retain global B.Observation separately.</summary>
    public readonly struct Completion
    {
        public readonly Token Request;
        public readonly B.Completion Physical;
        public readonly ulong Sector;
        public readonly uint AvailableBytes;
        public readonly Status Result;
        public readonly B.Status BlockFailure;
        internal Completion(State* s, Status result)
        {
            Request = new(s);
            Physical = s->Physical;
            Sector = s->Sector;
            Result = result;
            AvailableBytes = result == Status.Ready ? SectorBytes : 0;
            BlockFailure = s->Failure;
        }
    }
}
