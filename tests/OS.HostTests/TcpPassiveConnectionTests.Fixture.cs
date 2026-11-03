using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Net;
using Passive = OS.Net.TcpPassiveConnection;
using Tcb = OS.Net.TcpConnectionTable;

internal static unsafe partial class TcpPassiveConnectionTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly byte* memory;
        public Passive.State* State => (Passive.State*)memory;
        public Passive.State* Other => (Passive.State*)(memory + 1024);
        public Tcb.Owner* Table => (Tcb.Owner*)(memory + 2048);
        public TcpConnectionTableEntry* Entries => (TcpConnectionTableEntry*)(memory + 3072);
        public Hardware* Hardware => (Hardware*)(memory + 4096);

        public Span<TcpListenerBacklogEntry> Backlog() => new(memory + 6144, 1);
        public Fixture(uint capacity = 1)
        {
            memory = (byte*)NativeMemory.AlignedAlloc(8192, 4096);
            NativeMemory.Clear(memory, 8192);
            HostCheck.True(sizeof(Passive.State) <= 1024 && sizeof(Hardware) <= 2048 && capacity * sizeof(TcpConnectionTableEntry) <= 512);
            HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.InitializeOwned(Table, Entries, capacity, (ulong*)(memory + 3584), capacity, 71));
            Hardware->Now = 100;
            Hardware->SendResult = 1;
        }

        public Passive.Operations Io() => new()
        {
            Context = Hardware,
            NowNanoseconds = &Clock,
            Send = &Send,
            LifetimeNanoseconds = 1000,
            RetryNanoseconds = 100,
            MaximumAttempts = 3
        };
        public Passive.Status Begin(Passive.State* state, byte[] output, uint iss = Iss, uint irs = Irs, ushort remotePort = 40000) => Passive.Begin(state, Table, Info(iss, remotePort), Packet(irs, 0, 2, remotePort), output, Io(), out _);
        public byte[] LastFrame() => new ReadOnlySpan<byte>(Hardware->Frame, 54).ToArray();
        public byte[] Snapshot() => new ReadOnlySpan<byte>(memory, 8192).ToArray();
        public void Dispose() => NativeMemory.AlignedFree(memory);
    }
}
