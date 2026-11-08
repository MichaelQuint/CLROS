using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

internal static unsafe partial class ContinuingReadOnlyBlockServiceTests
{
    private sealed class Fixture : IDisposable
    {
        private static long epoch;
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(20480, 4096);
        internal byte* Data => backing + 4096;
        internal readonly Block.State* State = (Block.State*)NativeMemory.AllocZeroed((nuint)sizeof(Block.State));
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        internal readonly DmaBufferSlot* Slot = (DmaBufferSlot*)NativeMemory.AllocZeroed(3, (nuint)sizeof(DmaBufferSlot));
        internal readonly Hardware* Hardware = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        private static readonly int ownerOffset = (int)Marshal.OffsetOf<DmaBufferSlot>("Owner");
        internal DmaBufferOwnership Owner => *(DmaBufferOwnership*)((byte*)Slot + ownerOffset);
        private readonly ulong lifetime;
        private readonly VirtioMappedRegion common, notify, device;
        internal Block.ObservationAccess Observation;
        private readonly bool largeReads;
        internal Fixture(bool largeReads = false)
        {
            this.largeReads = largeReads;
            uint pages = largeReads ? 3U : 1U;
            new Span<byte>(backing, 20480).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, Physical, DeviceBase, pages * 4096UL, 64, out var window));
            lifetime = (ulong)Interlocked.Increment(ref epoch);
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, Slot, pages, lifetime));
            Hardware->Data = Data;
            Hardware->Pool = Pool;
            Hardware->Capacity = Capacity;
            Hardware->Status = 11;
            Hardware->Generation = 7;
            Hardware->ExpectedBytes = 512;
            Hardware->DataOffset = largeReads ? 4096U : 512U;
            Hardware->DmaMask = largeReads ? 7UL : 1UL;
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            new Span<PciSizedBar>(bars, 6).Clear();
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0),
                new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 64, 4),
                new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out common));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 64, Notify, out notify));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], Config, out device));
        }
        internal Block.Admission Input => new()
        {
            Common = common,
            Notify = notify,
            Device = device,
            NotifyMultiplier = 4,
            ResetLimit = 3,
            Pool = Pool,
            UseLargeReadLayout = largeReads,
            Epoch = lifetime,
            NegotiatedFeatures = BoundedVirtioFeatures.Version1 | BoundedVirtioFeatures.BlockReadOnly,
            ExpectedCapacity = Capacity,
            CompletionTimeoutNanos = 1000,
            Access = new()
            {
                Queue = new()
                {
                    Context = Hardware,
                    Read8 = &Read8,
                    Write8 = &Write8,
                    Read16 = &Read16,
                    Write16 = &Write16,
                    Read32 = &Read32,
                    Write32 = &Write32,
                    Fence = &Fence,
                    BusMasterOff = &BusOff
                },
                BusMasterOn = &BusOn
            },
            Clock = new()
            {
                Context = Hardware,
                Read = &Clock
            },
            Observation = Observation
        };
        internal Block.Status Start() => Block.Start(State, Input);
        internal void Complete(uint id = 0, uint length = 513, byte status = 0, ushort delta = 1)
        {
            if (!Hardware->Pending)
                Hardware->BadOrder = true;
            for (int i = 0; i < Hardware->ExpectedBytes; i++)
                Data[Hardware->DataOffset + i] = Payload(Hardware->Sector, i);
            Data[272] = status;
            byte* element = Data + 152 + (Hardware->Produced & 7) * 8;
            *(uint*)element = id;
            *(uint*)(element + 4) = length;
            Hardware->Produced += delta;
            *(ushort*)(Data + 150) = unchecked((ushort)Hardware->Produced);
            Hardware->Pending = false;
        }
        internal void CheckGuards() => HostCheck.True(!Hardware->BadOrder &&
            new ReadOnlySpan<byte>(backing, 4096).IndexOfAnyExcept((byte)0xa5) < 0 && new ReadOnlySpan<byte>(Data + (largeReads ? 12288 : 4096), 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        public void Dispose()
        {
            NativeMemory.AlignedFree(backing);
            NativeMemory.Free(State);
            NativeMemory.Free(Pool);
            NativeMemory.Free(Slot);
            NativeMemory.Free(Hardware);
        }
    }
}
