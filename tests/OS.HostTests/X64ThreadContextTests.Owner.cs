using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Threading;

internal static unsafe partial class X64ThreadContextTests
{
    private sealed class Owner : IDisposable
    {
        public readonly byte* Buffer = (byte*)NativeMemory.AlignedAlloc(4096, 16);
        public X64ThreadContext* First => (X64ThreadContext*)(Buffer + 16);
        public X64ThreadContext* Second => (X64ThreadContext*)(Buffer + 1024);

        public Owner() => new Span<byte>(Buffer, 4096).Fill(0xa5);
        public void Dispose() => NativeMemory.AlignedFree(Buffer);
        public byte[] Snapshot() => new ReadOnlySpan<byte>(Buffer, 4096).ToArray();
    }
}
