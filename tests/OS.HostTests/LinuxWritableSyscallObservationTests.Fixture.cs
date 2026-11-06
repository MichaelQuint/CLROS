using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using O = OS.Kernel.Compatibility.LinuxX64.LinuxWritableSyscallObservation;

// Copied-value/owned-buffer controls only. No actual syscall, user copy,
// scheduler, native publication, device or persistence authority is exercised.
internal static unsafe partial class LinuxWritableSyscallObservationTests
{
    private sealed class Fixture : IDisposable
    {
        internal byte* Raw;
        internal O.State* State => (O.State*)(Raw + 64);

        internal Fixture(bool start = true)
        {
            Raw = (byte*)NativeMemory.AllocZeroed((nuint)(sizeof(O.State) + 128));
            new Span<byte>(Raw, 64).Fill(0xa5);
            new Span<byte>((byte*)State + sizeof(O.State), 64).Fill(0x5a);
            if (start)
                HostCheck.Equal(O.Status.Success, O.Start(State, Admission));
        }

        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Raw, sizeof(O.State) + 128).ToArray();
        internal void Same(byte[] before) => HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(Raw, before.Length)));
        public void Dispose()
        {
            HostCheck.True(new ReadOnlySpan<byte>(Raw, 64).IndexOfAnyExcept((byte)0xa5) < 0 && new ReadOnlySpan<byte>((byte*)State + sizeof(O.State), 64).IndexOfAnyExcept((byte)0x5a) < 0);
            NativeMemory.Free(Raw);
            Raw = null;
        }
    }
}
