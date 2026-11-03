using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using Nic = OS.Drivers.ContinuingVirtioNetDevice;

internal static unsafe partial class ContinuingVirtioNetDeviceTests
{
    private sealed class Fixture : IDisposable
    {
        private static long epoch;
        internal readonly byte* Data = (byte*)NativeMemory.AlignedAlloc(32768, 4096);
        internal readonly Nic.State* State = (Nic.State*)NativeMemory.AllocZeroed((nuint)sizeof(Nic.State));
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* poolSlots = (DmaBufferSlot*)NativeMemory.AllocZeroed(8, (nuint)sizeof(DmaBufferSlot));
        private readonly VirtqueueDescriptorSlot* slots = (VirtqueueDescriptorSlot*)NativeMemory.AllocZeroed(16, (nuint)sizeof(VirtqueueDescriptorSlot));
        private readonly VirtqueueDescriptorToken* tokens = (VirtqueueDescriptorToken*)NativeMemory.AllocZeroed(16, (nuint)sizeof(VirtqueueDescriptorToken));
        internal readonly Hardware* Hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        private readonly VirtioMappedRegion common, notify;
        internal long AllocationBefore;
        internal Fixture()
        {
            using var regions = new QueueTests.Fixture();
            common = regions.Common;
            notify = regions.Notify;
            new Span<byte>(Data, 32768).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, QueueTests.PhysicalAddress, QueueTests.DeviceAddress, 32768, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, poolSlots, 8, (ulong)Interlocked.Increment(ref epoch)));
            Hardware->Data = Data;
            Hardware->Pool = Pool;
            Hardware->Status = 11;
            Hardware->Rx.Maximum = 8;
            Hardware->Tx.Maximum = 16;
            Hardware->Rx.Offset = 1;
            Hardware->Tx.Offset = 3;
        }

        internal Nic.Admission Input => new()
        {
            Common = common,
            Notify = notify,
            NotifyMultiplier = 4,
            Pool = Pool,
            Slots = slots,
            SlotCount = 16,
            Tokens = tokens,
            TokenCount = 16,
            Epoch = 1,
            NegotiatedFeatures = BoundedVirtioFeatures.NetSingleBufferReceive,
            ResetLimit = 3,
            CompletionTimeoutNanos = 1000000000,
            Mmio = new()
            {
                Context = Hardware,
                Read8 = &Read8,
                Write8 = &Write8,
                Read16 = &Read16,
                Write16 = &Write16,
                Read32 = &Read32,
                Write32 = &Write32,
                Fence = &Fence,
                BusMaster = &BusMaster
            },
            Clock = new()
            {
                Context = Hardware,
                Read = &Clock
            }
        };

        internal Nic.Status Start() => Nic.Start(State, Input);
        internal void Tx(uint ordinal, uint id)
        {
            byte* e = Data + 2200 + (ordinal & 7) * 8;
            *(uint*)e = id;
            *(uint*)(e + 4) = 0;
            *(ushort*)(Data + 2198) = unchecked((ushort)(ordinal + 1));
        }

        internal void Rx(uint ordinal)
        {
            uint id = ordinal & 7;
            byte* buffer = Data + 4096 + id * 1536;
            new Span<byte>(buffer, 26).Clear();
            *(ushort*)(buffer + 10) = 1;
            Frame(new Span<byte>(buffer + 12, 14));
            byte* e = Data + 152 + (ordinal & 7) * 8;
            *(uint*)e = id;
            *(uint*)(e + 4) = 26;
            *(ushort*)(Data + 150) = unchecked((ushort)(ordinal + 1));
        }

        public void Dispose()
        {
            NativeMemory.AlignedFree(Data);
            NativeMemory.Free(State);
            NativeMemory.Free(Pool);
            NativeMemory.Free(poolSlots);
            NativeMemory.Free(slots);
            NativeMemory.Free(tokens);
            NativeMemory.Free(Hardware);
        }
    }
}
