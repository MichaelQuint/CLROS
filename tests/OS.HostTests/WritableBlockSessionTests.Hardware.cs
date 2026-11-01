using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.WritableBlockSession;

internal static unsafe partial class WritableBlockSessionTests
{
    internal struct Hardware
    {
        internal byte* Data;
        internal byte* Medium, Persistent;
        internal ulong MediumSectors;
        internal DmaBufferPool* Pool;
        internal ulong Capacity, Now, Descriptor, Available, Used, Sector, Offered, Chosen;
        internal uint FeatureSelect, DriverSelect, Type;
        internal bool RejectFeatures;
        internal fixed byte Volatile[512], Durable[512];
        internal uint Calls, Fences, FailCall, Resets, DriverOkWrites, Notifies, GenerationReads, Produced;
        internal ushort Selected, Size, Enabled, ConfigVector, QueueVector, AvailableSeen, FencedIndex;
        internal byte Status, Generation;
        internal bool BusEnabled, BadOrder, Pending, IndexObserved, Acquired, ResetRequested, ResetObserved, HeldAtResetFence;
        internal bool FailureFired, FailAfterWrite, ResetStuck, ResetWriteFailure, DisableFailure, ResetFenceFailure, ClockFailure, GenerationChurn;
    }
}
