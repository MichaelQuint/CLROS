using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Net;
using Isn = OS.Net.TcpInitialSequence;

internal static unsafe partial class TcpInitialSequenceTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly byte* memory;
        internal readonly Isn.Owner* Owner;
        internal readonly byte* Secret;
        internal readonly void* Borrower;
        // Deterministic entropy is exclusively a host-test input, never firmware or application admission evidence.
        internal readonly byte[] SecretBytes = Enumerable.Range(0, 32).Select(i => (byte)(i * 7 + 3)).ToArray();
        internal Fixture(bool initialize = true)
        {
            HostCheck.True(sizeof(Isn.Owner) <= 128);
            memory = (byte*)NativeMemory.AlignedAlloc(256, 8);
            new Span<byte>(memory, 256).Clear();
            Owner = (Isn.Owner*)memory;
            Secret = memory + 128;
            Borrower = memory + 192;
            SecretBytes.CopyTo(new Span<byte>(Secret, 32));
            if (initialize)
                HostCheck.True(Isn.Initialize(Owner, Secret, 7));
        }

        internal byte[] Snapshot() => new ReadOnlySpan<byte>(memory, 256).ToArray();
        internal void Unchanged(byte[] before) => HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(memory, 256)));
        public void Dispose()
        {
            new Span<byte>(memory, 256).Clear();
            SecretBytes.AsSpan().Clear();
            NativeMemory.AlignedFree(memory);
        }
    }
}
