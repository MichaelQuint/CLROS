using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;

internal static unsafe partial class QueueTests
{
    internal struct Device
    {
        public byte* Data;
        public DmaBufferPool* Pool;
        public QueueRegisters Receive, Transmit;
        public uint Calls, FailCall, Fences, FailFence, ExecutionFences, Multiplier, Notifications, ResetReads;
        public uint CompletionId, CompletionLength, UsedReads, DriverOkWrites, EnableCalls;
        public ushort Queues, Selected, ConfigVector, CompletionIndex;
        public byte Status;
        public bool InvalidAccess, InvalidOrder, FailedCallback, BusEnabled, Sending, RxPrimed, WireObserved;
        public bool ReceiveNotified, TransmitNotified, NoCompletion, ResetStuck, DisableFailure, EnableFailure;
        public bool ResetRequested, ResetObserved, HeldDuringReset, CleanupFenceObserved;
        public bool WrongSelection, WrongAddress, BadOnNotify, BadAfterUsed;
        public bool Receiving, NoReceiveCompletion, ReadingReceive, OutputChangedBeforeQuiescence;
        public bool InvalidateDmaOnQuiescence;
        public ushort ReceiveCompletionIndex;
        public uint ReceiveCompletionId, ReceiveCompletionLength;
        public byte* ReceiveSource;
        public byte* OutputSentinel;
    }
}
