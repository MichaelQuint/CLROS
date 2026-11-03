using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Net;
using TimeWait = OS.Net.TcpTimeWaitTable;

internal static unsafe partial class TcpTimeWaitTableTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly byte* memory;
        private readonly uint capacity;
        public TimeWait.Owner* Owner => (TimeWait.Owner*)memory;
        public TimeWait.Entry* Entries => (TimeWait.Entry*)(memory + 128);

        public Fixture(uint capacity = 1, ulong now = 100, bool initialize = true)
        {
            this.capacity = capacity;
            memory = (byte*)NativeMemory.AlignedAlloc(8192, 4096);
            NativeMemory.Clear(memory, 8192);
            HostCheck.True(sizeof(TimeWait.Entry) <= 64 && sizeof(TimeWait.Owner) <= 128);
            if (initialize)
                HostCheck.Equal(TimeWait.Status.Success, TimeWait.Initialize(Owner, Entries, capacity, 71, now));
        }

        public byte[] Snapshot() => new ReadOnlySpan<byte>(memory, 8192).ToArray();
        public void AssertScrubbed()
        {
            for (uint i = 0; i < capacity * (uint)sizeof(TimeWait.Entry); i++)
                HostCheck.Equal((byte)0, ((byte*)Entries)[i]);
        }

        public void Dispose() => NativeMemory.AlignedFree(memory);
    }
}
