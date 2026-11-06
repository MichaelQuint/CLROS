using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Network = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Epoll = OS.Kernel.Compatibility.LinuxX64.LinuxEpollServices;
using Table = OS.Net.TcpConnectionTable;
using Wire = TcpReceiveWindowTests;
using Rx = OS.Net.TcpReceiveWindow;

internal static unsafe partial class LinuxSocketNetworkTests
{
    // A real packet/timer owner borrowing another test's actual socket table.
    // This owns only its declared host network buffers/carrier. The caller must
    // close/detach workers, Abort/End and retire the table before disposal.
    internal sealed class PacketCarrier : IDisposable
    {
        internal readonly Network.State* State = (Network.State*)NativeMemory.AllocZeroed((nuint)sizeof(Network.State));
        internal readonly Network.Slot* Slots = (Network.Slot*)NativeMemory.AllocZeroed(2, (nuint)sizeof(Network.Slot));
        internal readonly Table.Owner* Table = (Table.Owner*)NativeMemory.AllocZeroed((nuint)sizeof(Table.Owner));
        private readonly TcpConnectionTableEntry* entries = (TcpConnectionTableEntry*)NativeMemory.AllocZeroed(2, (nuint)sizeof(TcpConnectionTableEntry));
        private readonly ulong* generations = (ulong*)NativeMemory.AllocZeroed(2, 8);
        private readonly byte* buffers = (byte*)NativeMemory.AllocZeroed(32);
        private readonly Hardware* hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        private readonly Sockets.State* sockets;
        private readonly byte[] frame = new byte[1514];
        internal void* Context => hardware;
        internal uint ContextBytes => (uint)sizeof(Hardware);
        internal uint Sends => hardware->Calls;
        internal uint Clocks => hardware->ClockCalls;
        internal ReadOnlySpan<byte> Sent => new(hardware->Frame, (int)hardware->Length);

        internal PacketCarrier(Sockets.State* socketOwner)
        {
            sockets = socketOwner;
            hardware->Now = 1000;
            hardware->Result = 1;
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.InitializeOwned(Table, entries, 2, generations, 2, 0x626f756e646e6574));
        }

        internal bool Start() => Start(Context, ContextBytes);
        internal bool Start(void* context, uint bytes)
        {
            var io = new TcpPassiveConnection.Operations
            {
                Context = context,
                NowNanoseconds = &Now,
                Send = &Send,
                LifetimeNanoseconds = 10_000_000_000,
                RetryNanoseconds = 1_000_000_000,
                MaximumAttempts = 3
            };
            return Network.Begin(State, sockets, Table, Slots, 2, buffers, 8, 8, new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2), new(10, 0, 2, 2), 700, io, ioContextBytes: bytes);
        }

        internal uint Connect()
        {
            HostCheck.Equal(Network.Status.Success, Receive(Syn()));
            uint local = BinaryPrimitives.ReadUInt32BigEndian(Sent.Slice(38, 4)) + 1;
            HostCheck.Equal(Network.Status.Success, Receive(Wire.Packet(100, ReadOnlySpan<byte>.Empty, local, flags: 0x10)));
            return local;
        }

        internal Network.Status Receive(byte[] packet) => Network.Receive(State, packet, frame);
        internal Network.Status Pump() => Network.Pump(State, frame);
        internal IEnumerable<(ulong, ulong)> Extents() => new[]
        {
            ((ulong)State, (ulong)sizeof(Network.State)),
            ((ulong)Slots, 2UL * (ulong)sizeof(Network.Slot)),
            ((ulong)buffers, 32UL),
            ((ulong)Table, (ulong)sizeof(TcpConnectionTable.Owner)),
            ((ulong)entries, 2UL * (ulong)sizeof(TcpConnectionTableEntry)),
            ((ulong)generations, 16UL),
            ((ulong)hardware, (ulong)sizeof(Hardware))
        };
        internal void Retire()
        {
            HostCheck.True(Network.Abort(State));
            HostCheck.True(Network.End(State));
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.RetireOwned(Table));
            HostCheck.True(new ReadOnlySpan<byte>(buffers, 32).IndexOfAnyExcept((byte)0) < 0);
        }

        public void Dispose()
        {
            NativeMemory.Free(State);
            NativeMemory.Free(Slots);
            NativeMemory.Free(Table);
            NativeMemory.Free(entries);
            NativeMemory.Free(generations);
            NativeMemory.Free(buffers);
            NativeMemory.Free(hardware);
        }
    }
}
