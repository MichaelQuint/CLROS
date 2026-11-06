using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;

internal static unsafe partial class SplitVirtqueuePollingNotificationCompletionTests
{
    private struct Device
    {
        public byte* Data;
        public DmaBufferPool* Pool;
        public ulong Descriptor, AvailableAddress, UsedAddress;
        public uint Calls, FailCall, Fences, FailFence, ExecutionFences, FenceMask, Multiplier;
        public uint CompletionId, CompletionLength, Notifications, ResetReads;
        public ushort Maximum, Queues, Enabled, SelectedQueue, Size, Vector, NotifyOffset, CompletionIndex;
        public byte Status;
        public bool InvalidAccess, InvalidOrder, FailedCallback, BusEnabled, Notified, PayloadObserved;
        public bool NoCompletion, ResetStuck, DisableFailure, EnableFailure, ResetRequested, ResetObserved;
        public bool HeldDuringReset, CleanupFenceObserved, BadAfterEnable, BadOnNotify, BadAfterUsed;
        public bool InterruptMode, WaitObserved, WaitFailure, MaskFailure, MaskAttempted, RejectVector;
        public uint WaitCalls, UsedReads;
    }
}
