using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Threading;
using OS.Pal;

internal static unsafe partial class DeadlineQueueTests
{
    private sealed class Owner : IDisposable
    {
        public readonly byte* Buffer = (byte*)NativeMemory.AlignedAlloc(16384, 16);
        public DeadlineQueue.Entry* Entries => (DeadlineQueue.Entry*)(Buffer + 16);
        public DeadlineQueue.Entry* Output => (DeadlineQueue.Entry*)(Buffer + 8192);

        public Owner() => new Span<byte>(Buffer, 16384).Fill(0xa5);
        public void Dispose() => NativeMemory.AlignedFree(Buffer);
        public byte[] Snapshot() => new ReadOnlySpan<byte>(Buffer, 16384).ToArray();
        public DeadlineQueue Queue(uint capacity, ulong now = 0)
        {
            DeadlineQueue queue = default;
            HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Initialize(Key, Entries, capacity * 16, capacity, now));
            return queue; // Transfer sole test ownership before any operation.
        }

        public DeadlineQueue.DrainResult Drain(ref DeadlineQueue queue, ulong now, uint limit) => queue.Drain(Key, now, Output, limit * 16, limit);
    }
}
