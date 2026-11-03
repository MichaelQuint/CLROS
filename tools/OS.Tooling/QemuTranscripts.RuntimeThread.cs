using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class RuntimeThread(ulong identity, int tid, ulong bottom, ulong top, ulong fs, ulong rip)
    {
        internal readonly ulong Identity = identity, Bottom = bottom, Top = top;
        internal readonly int Tid = tid;
        internal ulong Fs = fs, Gs, ClearTid, Mask, SavedRsp = top, SavedRip = rip, LastSequence, WaitOrder, AddressOrder;
        internal ulong TailRip, TailRsp, TailFs, TailFlags;
        internal ulong LastUserIrq;
        internal ulong TotalCalls, TurnEpoch;
        internal uint TurnCalls;
        internal FutexReplay? WaitCall, TimedRestart;
        internal readonly Queue<CollectionSignal> SignalProofs = [];
        internal uint WaitSource, WaitMode, WaitBits;
        internal ulong WaitAddress, Deadline;
        internal long? Completion;
        internal SocketDescriptor? Epoll;
        internal uint EpollMaximum, EpollWinner;
        internal bool EpollObserved, EpollEligible;
        internal ulong? DeliveryRsp;
        internal uint DeliverySignal;
        internal CollectionSignal? DeliveryProof;
        internal bool PendingUsr2, PendingTermination;
        internal bool Parking, Exiting, Reaped, GroupRetired, NormalExit;
        internal DiskStaticPending? Disk;
        internal int Requests, Delivered, Returned;
        internal readonly Stack<(ulong Frame, ulong Rsp, CollectionSignal? Proof, uint Signal)> Frames = [];
    }
}
