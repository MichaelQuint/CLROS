using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Immutable default-pthread observations and backing history.
    /// Custom/unproved shapes fail closed. Failed allocations that never clone
    /// remain unclassified native backing. Static main stack is separate.</summary>

    public sealed record ServiceQuotaPthreadInspection(IReadOnlyList<ServiceQuotaPthread> Threads, IReadOnlyList<ServiceQuotaPthreadSnapshot> Snapshots, ulong PeakStackBackedBytes, ulong PeakPthreadBackedBytes);
}
