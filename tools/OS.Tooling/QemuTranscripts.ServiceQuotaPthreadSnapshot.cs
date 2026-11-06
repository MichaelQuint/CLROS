using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Backing from original successful memory operations, counted
    /// once per still-owned allocation, including after reap and PROT_NONE.
    /// Tail includes the original pthread remainder and later in-place growth;
    /// it does not assign an unobserved logical stack or TLS size to new pages.
    /// Replacement pages belong to their new allocation. RecordIndex addresses
    /// the supplied original rows. These are bytes, not virtual reservations.</summary>

    public readonly record struct ServiceQuotaPthreadSnapshot(int RecordIndex, ulong DynamicBackedBytes, ulong StackBackedBytes, ulong GuardBackedBytes, ulong TailBackedBytes, ulong PthreadBackedBytes, ulong RetainedAfterReapBytes);
}
