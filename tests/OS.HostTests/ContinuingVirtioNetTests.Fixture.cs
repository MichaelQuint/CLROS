using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using Net = OS.Drivers.ContinuingVirtioNet;

internal static unsafe partial class ContinuingVirtioNetTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly Net.State* State = (Net.State*)NativeMemory.AllocZeroed((nuint)sizeof(Net.State));
        internal readonly VirtqueueDescriptorSlot* Slots = (VirtqueueDescriptorSlot*)NativeMemory.AllocZeroed(16, (nuint)sizeof(VirtqueueDescriptorSlot));
        internal readonly VirtqueueDescriptorToken* Tokens = (VirtqueueDescriptorToken*)NativeMemory.AllocZeroed(16, (nuint)sizeof(VirtqueueDescriptorToken));
        internal readonly FenceState* Hook = (FenceState*)NativeMemory.AllocZeroed((nuint)sizeof(FenceState));
        internal readonly byte* Data = (byte*)NativeMemory.AlignedAlloc(32768, 4096);
        internal readonly DmaAddressRange Range;
        internal Fixture()
        {
            new Span<byte>(Data, 32768).Clear();
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, 0x200000, 0x400000, 32768, 64, out var window));
            HostCheck.Equal(DmaAddressStatus.Success, window.TrySlice(0, 32768, out Range));
        }

        internal Net.Status Start() => Net.Initialize(State, Range, Slots, 16, Tokens, 16, 1, Hook, &Fence);
        internal void Completion(bool tx, uint ordinal, uint id, uint length)
        {
            byte* e = Data + (tx ? TxUsed : RxUsed) + 4 + (ordinal & 7) * 8;
            *(uint*)e = id;
            *(uint*)(e + 4) = length;
        }

        internal void Packet(uint id, byte value)
        {
            byte* b = Data + 4096 + id * 1536;
            new Span<byte>(b, 26).Clear();
            *(ushort*)(b + 10) = 1;
            Frame(new Span<byte>(b + 12, 14));
            b[12] = value;
        }

        public void Dispose()
        {
            NativeMemory.Free(State);
            NativeMemory.Free(Slots);
            NativeMemory.Free(Tokens);
            NativeMemory.Free(Hook);
            NativeMemory.AlignedFree(Data);
        }
    }
}
