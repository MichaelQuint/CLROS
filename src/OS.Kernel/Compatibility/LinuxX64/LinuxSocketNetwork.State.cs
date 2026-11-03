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
    /// <summary>One-use owner borrowing an empty initialized connection table,
    /// zeroed slots and contiguous per-slot RX then TX backing. Buffers, metadata,
    /// table storage and descriptor ownership are mutually disjoint. Admission
    /// precedes thread attachment; the thread owner checks these extents before
    /// borrowing any worker metadata. Public counts are observations only.</summary>

    public struct State
    {
        internal ulong Self;
        internal LinuxSocketServices.State* Sockets;
        internal TcpConnectionTable.Owner* Table;
        internal Slot* Slots;
        internal byte* Buffers;
        internal uint Count, ReceiveBytes, SendBytes, NextSequence, Initialized, Failed;
        internal uint MeasureLeases, Leased, PeakLeased;
        internal ulong LeaseAcquired, LeaseReturned;
        internal MacAddress LocalMac, PeerMac;
        internal Ipv4Address PeerAddress;
        internal Pending.Operations Io;
#if !NOSTDLIB_EFI || M16_027_PROBE
        internal void* OriginalIoContext;
        internal uint IoContextBytes;
#endif
        internal TcpInitialSequence.Owner* Sequence;
        internal TcpInitialSequence.Lease SequenceLease;
        internal Waits.Owner TimeWait;
        internal uint CompactTimeWait;
        internal fixed ulong TimeWaitEntries[(int)Waits.MaximumEntries * 8];
        internal fixed ulong TimeWaitAcks[(int)Waits.MaximumEntries];
        public readonly uint CompactTimeWaitConnections => CompactTimeWait == 1 ? (uint)TimeWait.Count : 0;

        public readonly uint RetainedConnections
        {
            get
            {
                uint n = 0;
                if (Initialized == 1)
                {
                    n = CompactTimeWaitConnections;
                    for (uint i = 0; i < Count; i++)
                        if (Slots[i].Phase != 0)
                            n++;
                }

                return n;
            }
        }

        public readonly bool RequiresShutdown => Initialized == 1 && Failed != 0;
    }
}
#endif
