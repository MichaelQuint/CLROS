namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class QuotaTemporalConnection(ServiceQuotaSocketIdentity identity, uint description, ulong inode)
    {
        internal readonly ServiceQuotaSocketIdentity Identity = identity;
        internal readonly uint Description = description;
        internal readonly ulong Inode = inode;
        internal readonly List<ServiceQuotaSocketIo> Reads = [], Writes = [];
        internal ulong Sent, Received;
    }
}
