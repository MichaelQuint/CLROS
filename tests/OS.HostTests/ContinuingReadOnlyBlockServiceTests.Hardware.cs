using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class ContinuingReadOnlyBlockServiceTests
{
    private struct Hardware
    {
        internal byte* Data;
        internal DmaBufferPool* Pool;
        internal ulong Capacity, Now, Descriptor, Available, Used, Sector;
        internal uint Calls, Fences, FailCall, Resets, DriverOkWrites, Notifies, GenerationReads, Produced;
        internal uint ExpectedBytes;
        internal uint DataOffset;
        internal uint ClockCalls;
        internal ulong DmaMask;
        internal ushort Selected, Size, Enabled, ConfigVector, QueueVector, AvailableSeen, FencedIndex;
        internal byte Status, Generation;
        internal bool BusEnabled, BadOrder, Pending, IndexObserved, Acquired, ResetRequested, ResetObserved, HeldAtResetFence;
        internal bool FailureFired, FailAfterWrite, ResetStuck, ResetWriteFailure, DisableFailure, ResetFenceFailure, ClockFailure, GenerationChurn;
    }
}
