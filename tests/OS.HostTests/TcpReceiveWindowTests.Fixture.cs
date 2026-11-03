using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Net;
using Rx = OS.Net.TcpReceiveWindow;
using Tcb = OS.Net.TcpConnectionTable;

internal static unsafe partial class TcpReceiveWindowTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly byte* memory;
        private readonly int capacity;
        public Rx.Owner* Owner => (Rx.Owner*)memory;
        public Rx.Owner* Other => (Rx.Owner*)(memory + 512);
        public Tcb.Owner* Table => (Tcb.Owner*)(memory + 1024);
        public TcpConnectionTableEntry* Entries => (TcpConnectionTableEntry*)(memory + 1152);
        public byte* Buffer => memory + 2048;
        public byte* OtherBuffer => memory + 2560;
        public Hardware* Hardware => (Hardware*)(memory + 3072);
        public Tcb.Handle Handle { get; private set; }

        public Fixture(int capacity = 8, uint remote = Remote, bool bind = true)
        {
            this.capacity = capacity;
            memory = (byte*)NativeMemory.AlignedAlloc(8192, 4096);
            NativeMemory.Clear(memory, 8192);
            HostCheck.True(sizeof(Rx.Owner) <= 512 && sizeof(TcpConnectionTableEntry) <= 128);
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.InitializeOwned(Table, Entries, 1, (ulong*)(memory + 1536), 1, 123));
            Reserve(remote);
            Hardware->SendResult = 1;
            if (bind)
                HostCheck.Equal(Rx.Status.Success, Bind(Owner, Buffer));
        }

        public void Reserve(uint remote)
        {
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.ReserveOwned(Table, new(new Ipv4Address(10, 0, 2, 15), 8080, new Ipv4Address(10, 0, 2, 2), 40000), new(Local, remote), out var handle));
            Handle = handle;
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(Table, Handle, TcpConnectionLifecycleState.SynReceived, TcpConnectionLifecycleState.Established));
        }

        public Rx.Status Bind(Rx.Owner* owner, byte* buffer) => Rx.Initialize(owner, Table, Handle, buffer, (uint)capacity, Info(), Hardware, &Send);
        public TcpEstablishedStreamIpv4FramePlannerInfo Info(int window = -1) => new(new MacAddress(2, 0, 0, 0, 0, 1), new MacAddress(0x52, 0x55, 0x0a, 0, 2, 2), new Ipv4Address(10, 0, 2, 15), new Ipv4Address(10, 0, 2, 2), 1460, (ushort)(window < 0 ? capacity : window), 51, 0x271);
        public Rx.Status Receive(uint sequence, string payload) => Rx.Receive(Owner, Packet(sequence, Encoding.ASCII.GetBytes(payload)), out _);
        public void Close() => HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.TransitionOwned(Table, Handle, TcpConnectionLifecycleState.Established, TcpConnectionLifecycleState.Closed));
        public void Dispose() => NativeMemory.AlignedFree(memory);
    }
}
