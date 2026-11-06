using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied pinned-musl default allocation and original clone/reap
    /// positions. Stack is its logical extent; the pthread tail includes TLS,
    /// TCB/TSD and padding without assigning those individual sizes. Values
    /// grant no memory, scheduler, native-image or execution authority.</summary>

    public readonly record struct ServiceQuotaPthread(int RecordIndex, int CallRecordIndex, ulong Identity, ulong Ordinal, ulong ChildIdentity, int Tid, ulong Tls, ulong StackStart, ulong StackBytes, ulong GuardBytes, ulong MappingStart, ulong MappingBytes, int AllocationRecordIndex, int ReapRecordIndex)
    {
        public ulong InitialStackPointer
        {
            get; init;
        }
    }
}
