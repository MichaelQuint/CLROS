#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using System;
using OS.Net;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Pending = OS.Net.TcpPassiveConnection;
using Rx = OS.Net.TcpReceiveWindow;
using Tx = OS.Net.TcpSendWindow;
using Waits = OS.Net.TcpTimeWaitTable;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxSocketNetwork
{
    /// <summary>Copied, serialized RX/TX borrower counts/bytes from this one-use
    /// owner. BackingBytes remains retained caller storage; current/peak bytes
    /// are successful window leases, not new physical pages or buffered data.
    /// Failed/disabled/inconsistent owners return false. Post-End succeeds only
    /// after actual release, empty slots/table and whole-buffer scrubbing.</summary>

    public readonly struct LeaseObservation
    {
        internal LeaseObservation(State* state)
        {
            Leases = state->Leased;
            PeakLeases = state->PeakLeased;
            Acquired = state->LeaseAcquired;
            Returned = state->LeaseReturned;
            ReceiveBytes = (ulong)Leases * state->ReceiveBytes;
            SendBytes = (ulong)Leases * state->SendBytes;
            PeakReceiveBytes = (ulong)PeakLeases * state->ReceiveBytes;
            PeakSendBytes = (ulong)PeakLeases * state->SendBytes;
            BackingBytes = (ulong)state->Count * (state->ReceiveBytes + state->SendBytes);
            Ended = state->Initialized == 2;
        }

        public readonly uint Leases, PeakLeases;
        public readonly ulong Acquired, Returned, ReceiveBytes, SendBytes, PeakReceiveBytes, PeakSendBytes, BackingBytes;
        public readonly bool Ended;
    }
}
#endif
