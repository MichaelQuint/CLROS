using OS.Drivers;
using OS.Std.Binary;
using Block = OS.Storage.WritableBlockSession;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Storage;

public static unsafe partial class ContinuingFat32Mutation
{
    /// <summary>Copied physical root-file identity. Every issued mutation
    /// revokes the volume revision, so old tokens cannot recapture another file.
    /// Four fingerprint words retain the complete original short entry.
    /// Logical POSIX inode/description lifetimes belong to a later fd port.</summary>
    public readonly struct FileToken
    {
        public readonly ulong Owner, Epoch, Revision, EntryOffset, Fingerprint0, Fingerprint1, Fingerprint2, Fingerprint3;
        public readonly uint FirstCluster, Length;
        internal FileToken(State* s, ulong offset, uint first, uint length, ReadOnlySpan<byte> raw)
        {
            Owner = (ulong)s; Epoch = s->Input.Epoch; Revision = s->Revision; EntryOffset = offset; FirstCluster = first; Length = length;
            Fingerprint0 = U64(raw, 0); Fingerprint1 = U64(raw, 8); Fingerprint2 = U64(raw, 16); Fingerprint3 = U64(raw, 24);
        }
    }
}
