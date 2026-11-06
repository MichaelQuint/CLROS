using System.Buffers.Binary;
using System.Text;
using Block = OS.Storage.WritableBlockSession;

namespace OS.Tooling;

public static partial class LinuxWritableDiskProofFixture
{
    /// <summary>Copied counter boundary supplied by the caller. Owner and epoch
    /// are compared as values, never dereferenced or treated as device authority.
    /// FlushAcknowledgedBytes describes protocol acknowledgements only.</summary>

    public readonly record struct BlockBoundary(ulong Owner, ulong Epoch, ulong SectorCount, ulong Submitted, ulong Completed, ulong SubmittedBytes, ulong CompletedBytes, ulong FlushAcknowledgedBytes);
}
