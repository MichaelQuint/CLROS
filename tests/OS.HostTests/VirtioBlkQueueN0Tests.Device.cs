using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;

internal static unsafe partial class VirtioBlkQueueN0Tests
{
    private struct Device
    {
        public byte* Data;
        public DmaBufferPool* Pool;
        public fixed uint Addresses[6];
        public uint Calls, FailCall, Fences, BusCalls, StatusReads, ResetReads, FailFence, FailBus, BadStatusAt;
        public uint WriteMask, ReadMask, MismatchOffset;
        public ushort Maximum, Queues, Selected, Size, Enabled, ConfigVector, QueueVector;
        public byte Status;
        public bool BadAccess, BadOrder, FailedCallback, FailAfterWrite, BadSelect, RejectEnable, ResetStuck;
        public bool ResetRequested, ResetObserved, CleanupBusOff, CleanupFence, HadDma, HeldDuringReset, RingObserved;
    }
}
