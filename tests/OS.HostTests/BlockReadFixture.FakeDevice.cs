using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;

// Explicit fake device. It independently decodes descriptor bytes and verifies
// every release/publication boundary; it is never evidence of real device I/O.
internal sealed unsafe partial class BlockReadFixture
{
    internal struct FakeDevice
    {
        internal byte* Data, Output;
        internal DmaBufferPool* Pool;
        internal fixed uint Addresses[6];
        internal ulong Capacity, ExpectedSector;
        internal uint Calls, FailCall, Fences, FailFence, BusOffCalls, FailBusOff, GenerationReads, PollReads, Delay, UsedId, UsedLength, WriteMask, ReadMask;
        internal ushort Selected, Size, Enabled, ConfigVector, QueueVector, NotifyOffset, UsedIndex;
        internal byte Status, CompletionStatus, InterruptRoute;
        internal bool InterruptPrepared, InterruptWaited, InterruptStopped, FailInterruptPrepare, FailInterruptStop;
        internal bool FailAfterWrite, FailedCallback, BadAccess, BadOrder, BusEnabled, Notified, ChainChecked, IndexObserved, Acquired, ResetRequested, ResetObserved, CleanupBusOff, CleanupFence, ResetStuck, GenerationChurn, ChurnOnce, ChangeBeforePublish, ChangeAfterCompletion, NeverComplete, ProtectiveMbr;
    }
}
