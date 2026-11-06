using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;

internal static unsafe partial class VirtioBlkQueueN0Tests
{
    private sealed class Fixture : IDisposable
    {
        private static long epoch;
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
        internal byte* Data => backing + 4096;

        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* slot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
        internal readonly Device* State = (Device*)NativeMemory.AllocZeroed((nuint)sizeof(Device));
        internal readonly VirtioMappedRegion Region;
        internal VirtioBlkQueueAccess Access => new()
        {
            Context = State,
            Read8 = &Read8,
            Write8 = &Write8,
            Read16 = &Read16,
            Write16 = &Write16,
            Read32 = &Read32,
            Write32 = &Write32,
            Fence = &Fence,
            BusMasterOff = &BusOff
        };

        internal Fixture(ulong physical = Physical)
        {
            new Span<byte>(backing, 12288).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, physical, DeviceBase, 4096, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, slot, 1, (ulong)System.Threading.Interlocked.Increment(ref epoch)));
            State->Data = Data;
            State->Pool = Pool;
            State->Status = 11;
            State->Queues = 1;
            State->Maximum = 16;
            State->Selected = 0xffff;
            State->MismatchOffset = uint.MaxValue;
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            new Span<PciSizedBar>(bars, 6).Clear();
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0), new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 2, 4), new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out Region));
        }

        internal VirtioBlkQueueResult Run() => VirtioBlkQueueN0.Run(Region, Pool, Access, 3);
        public void Dispose()
        {
            NativeMemory.AlignedFree(backing);
            NativeMemory.Free(State);
            NativeMemory.Free(slot);
            NativeMemory.Free(Pool);
        }
    }
}
