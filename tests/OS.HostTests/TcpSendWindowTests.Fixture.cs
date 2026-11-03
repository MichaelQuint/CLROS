using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Net;
using Rx = OS.Net.TcpReceiveWindow;
using Tx = OS.Net.TcpSendWindow;
using Tcb = OS.Net.TcpConnectionTable;
using Passive = OS.Net.TcpPassiveConnection;
using Wire = TcpReceiveWindowTests;

internal static unsafe partial class TcpSendWindowTests
{
    internal sealed class Fixture : IDisposable
    {
        private readonly byte* memory;
        private readonly int capacity, receiveCapacity;
        private readonly ushort mtu;
        public Tx.Owner* Owner => (Tx.Owner*)(memory + 2048);
        public Tx.Owner* Other => (Tx.Owner*)(memory + 2560);
        public Rx.Owner* Receiver => (Rx.Owner*)(memory + 1536);
        public Passive.State* Handshake => (Passive.State*)(memory + 1024);
        public Tcb.Owner* Table => (Tcb.Owner*)memory;
        public byte* Buffer => memory + 16384;
        public byte* OtherBuffer => memory + 49152;
        public byte* ReceiveBuffer => memory + 8192;
        public Hardware* Hardware => (Hardware*)(memory + 3072);
        public Tcb.Handle Handle => Handshake->Connection;

        public Fixture(int capacity = 32, int receiveCapacity = 32, ushort mss = 4, ushort window = 32, ushort mtu = 1500, uint iss = 699, bool setup = true, bool bind = true, ushort? finalWindow = null, bool retryHandshake = false)
        {
            this.capacity = capacity;
            this.receiveCapacity = receiveCapacity;
            this.mtu = mtu;
            memory = (byte*)NativeMemory.AlignedAlloc(131072, 4096);
            NativeMemory.Clear(memory, 131072);
            HostCheck.True(sizeof(Passive.State) <= 512 && sizeof(Rx.Owner) <= 512 && sizeof(Tx.Owner) <= 512 && sizeof(Hardware) <= 2048);
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.InitializeOwned(Table, (TcpConnectionTableEntry*)(memory + 128), 1, (ulong*)(memory + 512), 1, 29));
            Hardware->Result = 1;
            Hardware->Now = 100;
            if (!setup)
                return;
            byte[] options = mss == 0 ? [] : [2, 4, (byte)(mss >> 8), (byte)mss];
            byte[] syn = Wire.Packet(99, ReadOnlySpan<byte>.Empty, ack: 0, flags: 2, window: window, options: options), output = new byte[64];
            HostCheck.Equal(Passive.Status.Success, Passive.Begin(Handshake, Table, PassiveInfo(iss), syn, output, Operations(), out _));
            if (retryHandshake)
            {
                Hardware->Now += 100;
                HostCheck.Equal(Passive.Status.Success, Passive.Pump(Handshake, output, out _));
            }

            HostCheck.Equal(Passive.Status.Success, Passive.ReceiveAck(Handshake, Wire.Packet(100, ReadOnlySpan<byte>.Empty, ack: unchecked(iss + 1), flags: 0x10, window: finalWindow ?? window), new Span<TcpListenerBacklogEntry>(memory + 5120, 1), out _, out _));
            var info = new TcpEstablishedStreamIpv4FramePlannerInfo(new MacAddress(2, 0, 0, 0, 0, 1), new MacAddress(0x52, 0x55, 0x0a, 0, 2, 2), new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), 1460, (ushort)receiveCapacity, 51, 0x272);
            HostCheck.Equal(Rx.Status.Success, Rx.Initialize(Receiver, Table, Handle, ReceiveBuffer, (uint)receiveCapacity, info, Hardware, &Send));
            if (bind)
                HostCheck.Equal(Tx.Status.Success, Bind(Owner, Buffer));
            Hardware->Calls = 0;
        }

        public Passive.Operations Operations() => new()
        {
            Context = Hardware,
            NowNanoseconds = &Clock,
            Send = &Send,
            LifetimeNanoseconds = 1000,
            RetryNanoseconds = 100,
            MaximumAttempts = 3
        };
        public TcpPassiveIpv4FramePlannerInfo PassiveInfo(uint iss = 699, ushort localMss = 1460) => new(new MacAddress(2, 0, 0, 0, 0, 1), new MacAddress(0x52, 0x55, 0x0a, 0, 2, 2), new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), 8080, 40000, iss, (ushort)receiveCapacity, 51, 0x272, maximumSegmentSize: localMss);
        public Tx.Status Bind(Tx.Owner* owner, byte* buffer) => Tx.Initialize(owner, Receiver, Handshake, buffer, (uint)capacity, mtu);
        public Tx.Status Ack(uint ack, out int released, ushort window = 32, uint sequence = 100) => Tx.Acknowledge(Owner, Wire.Packet(sequence, ReadOnlySpan<byte>.Empty, ack: ack, flags: 0x10, window: window), out released);
        public void Close() => HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(Table, Handle, TcpConnectionLifecycleState.Established, TcpConnectionLifecycleState.Closed));
        public void Dispose() => NativeMemory.AlignedFree(memory);
    }
}
