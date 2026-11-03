using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Threading;
using OS.Pal;

internal static unsafe partial class ReadyQueueTests
{
    private sealed class Owner : IDisposable
    {
        public readonly byte* Buffer = (byte*)NativeMemory.AlignedAlloc(16384, 16);
        public OSHostHandle* Entries => (OSHostHandle*)(Buffer + 16);
        public X64ThreadContext* Context => (X64ThreadContext*)(Buffer + 8192);

        public Owner()
        {
            new Span<byte>(Buffer, 16384).Fill(0xa5);
            HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Initialize(Context, 688, 0x400100, 0x70000108, 0xffbf));
        }

        public void Dispose() => NativeMemory.AlignedFree(Buffer);
        public byte[] Snapshot() => new ReadOnlySpan<byte>(Buffer, 16384).ToArray();
        public ReadyQueue Queue(uint capacity)
        {
            ReadyQueue queue = default;
            HostCheck.Equal(ReadyQueue.Status.Ok, queue.Initialize(Key, Entries, capacity * 8, capacity));
            return queue; // Transfer this sole test owner before any operation.
        }

        public ReadyQueue.Status Add(ref ReadyQueue queue, OSHostHandle id) => queue.Enqueue(Key, id, id, true, Context, 688, 0xffbf);
    }
}
