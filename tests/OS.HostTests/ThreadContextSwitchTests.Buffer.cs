using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Threading;

internal static unsafe partial class ThreadContextSwitchTests
{
    private sealed class Buffer : IDisposable
    {
        public readonly byte* Bytes;
        private readonly int _length;
        public X64ThreadContext* First => (X64ThreadContext*)Bytes;
        public X64ThreadContext* Second => (X64ThreadContext*)(Bytes + 1024);

        public Buffer(int pages = 1)
        {
            _length = pages * 4096;
            Bytes = (byte*)NativeMemory.AlignedAlloc((nuint)_length, 4096);
            new Span<byte>(Bytes, _length).Fill(0xa5);
        }

        public void Dispose() => NativeMemory.AlignedFree(Bytes);
        public byte[] Snapshot() => new ReadOnlySpan<byte>(Bytes, _length).ToArray();
    }
}
