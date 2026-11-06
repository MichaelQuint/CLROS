using System.Runtime.CompilerServices;
using OS.Net;
using Tcb = global::OS.Net.TcpConnectionTable;

internal static unsafe partial class TcpConnectionTableTests
{
    private sealed class OwnedFixture : IDisposable
    {
        private readonly void* allocation;
        internal readonly Tcb.Owner* Owner;
        internal readonly TcpConnectionTableEntry* Entries;
        internal readonly ulong* Generations;
        internal OwnedFixture(uint capacity, bool initialize = true)
        {
            HostCheck.True(capacity <= 64 && 128 + capacity * sizeof(TcpConnectionTableEntry) <= 4096);
            allocation = System.Runtime.InteropServices.NativeMemory.AlignedAlloc(8192, 4096);
            System.Runtime.InteropServices.NativeMemory.Clear(allocation, 8192);
            Owner = (Tcb.Owner*)allocation;
            Entries = (TcpConnectionTableEntry*)((byte*)allocation + 128);
            Generations = (ulong*)((byte*)allocation + 4096);
            if (initialize)
                HostCheck.Equal(Tcb.OwnershipStatus.Success, Tcb.InitializeOwned(Owner, Entries, capacity, Generations, capacity, 1));
        }

        public void Dispose() => System.Runtime.InteropServices.NativeMemory.AlignedFree(allocation);
    }
}
