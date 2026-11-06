using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using OS.Pal;
using OS.Kernel.Threading;
using Memory = OS.Kernel.Compatibility.LinuxX64.LinuxMemoryServices;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using OS.Net;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using P = OS.Storage.WritableBlockReadPort;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Epoll = OS.Kernel.Compatibility.LinuxX64.LinuxEpollServices;
using Network = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Wire = TcpReceiveWindowTests;

// Real queued fake media, with separate C-writer and paired-global controls.
// Every positive pair uses one ORIGINAL B/C. Thread controls use copied host
// contexts and actual scheduler contracts; none establishes native execution.
internal static unsafe partial class LinuxBorrowedDiskDescriptorServicesTests
{
    // Explicitly allocated test arena, not a copied kernel image: both handshakes
    // actually establish against this one table before windows are initialized.
    private sealed class SharedTcp : IDisposable
    {
        private readonly byte* arena = (byte*)NativeMemory.AllocZeroed(32768);
        internal readonly TcpSendWindowTests.Fixture Carrier = new(capacity: 8, receiveCapacity: 8, setup: false);
        internal TcpConnectionTable.Owner* Table => (TcpConnectionTable.Owner*)arena;

        internal Rx.Owner* Receiver(uint i) => (Rx.Owner*)(arena + 4096 + i * 512);
        internal Tx.Owner* Sender(uint i) => (Tx.Owner*)(arena + 8192 + i * 512);
        internal SharedTcp()
        {
            HostCheck.True(sizeof(Rx.Owner) <= 512 && sizeof(Tx.Owner) <= 512 && sizeof(TcpPassiveConnection.State) <= 512);
            HostCheck.Equal(TcpConnectionTable.OwnershipStatus.Success, TcpConnectionTable.InitializeOwned(Table, (TcpConnectionTableEntry*)(arena + 256), 2, (ulong*)(arena + 2048), 2, 91));
            TcpListenerBacklogEntry* backlog = stackalloc TcpListenerBacklogEntry[1];
            for (uint i = 0; i < 2; i++)
            {
                var handshake = (TcpPassiveConnection.State*)(arena + 12288 + i * 512);
                var io = Carrier.Operations();
                uint iss = 699 + 100 * i;
                ushort port = (ushort)(40000 + i);
                var passiveInfo = new TcpPassiveIpv4FramePlannerInfo(new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2), new(10, 0, 2, 15), new(10, 0, 2, 2), 8080, port, iss, 8, 51, 0x272, maximumSegmentSize: 1460);
                HostCheck.Equal(TcpPassiveConnection.Status.Success, TcpPassiveConnection.Begin(handshake, Table, passiveInfo, Wire.Packet(99, ReadOnlySpan<byte>.Empty, ack: 0, port: port, flags: 2, window: 32, options: new byte[] { 2, 4, 0, 4 }), new byte[64], io, out _));
                *backlog = default;
                HostCheck.Equal(TcpPassiveConnection.Status.Success, TcpPassiveConnection.ReceiveAck(handshake, Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: iss + 1, port: port, flags: 0x10), new Span<TcpListenerBacklogEntry>(backlog, 1), out _, out _));
                var info = new TcpEstablishedStreamIpv4FramePlannerInfo(new(2, 0, 0, 0, 0, 1), new(0x52, 0x55, 0x0a, 0, 2, 2), new(10, 0, 2, 15), new(10, 0, 2, 2), 1460, 8, 51, 0x272);
                HostCheck.Equal(Rx.Status.Success, Rx.Initialize(Receiver(i), Table, handshake->Connection, arena + 16384 + i * 2048, 8, info, io.Context, io.Send));
                HostCheck.Equal(Tx.Status.Success, Tx.Initialize(Sender(i), Receiver(i), handshake, arena + 24576 + i * 2048, 8, 1500));
                HostCheck.Equal(Tx.Status.Success, Tx.ConfigureRetransmission(Sender(i), io.Context, io.NowNanoseconds, 1000000000000));
                HostCheck.Equal(Tx.Status.Success, Tx.ConfigureClose(Sender(i), 30000000000));
            }
        }

        public void Dispose()
        {
            NativeMemory.Free(arena);
            Carrier.Dispose();
        }
    }
}
