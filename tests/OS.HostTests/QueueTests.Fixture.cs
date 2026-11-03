using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;

internal static unsafe partial class QueueTests
{
    internal sealed class Fixture : IDisposable
    {
        private static long nextEpoch;
        internal readonly byte* Data = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* slot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
        internal readonly Device* State = (Device*)NativeMemory.AllocZeroed((nuint)sizeof(Device));
        internal readonly VirtioMappedRegion Common, Notify;
        internal VirtqueuePollingAccess Access => new()
        {
            Context = State,
            Read8 = &Read8,
            Write8 = &Write8,
            Read16 = &Read16,
            Write16 = &Write16,
            Read32 = &Read32,
            Write32 = &Write32,
            Fence = &Fence,
            BusMaster = &BusMaster
        };

        internal Fixture()
        {
            new Span<byte>(Data, 4096).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, PhysicalAddress, DeviceAddress, 4096, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, slot, 1, (ulong)System.Threading.Interlocked.Increment(ref nextEpoch)));
            State->Data = Data;
            State->Pool = Pool;
            State->Status = 11;
            State->Queues = 2;
            State->Multiplier = 4;
            State->Receive.Maximum = 8;
            State->Transmit.Maximum = 16;
            State->Receive.NotifyOffset = 1;
            State->Transmit.NotifyOffset = 3;
            State->CompletionIndex = 1;
            PciSizedBar* bars = stackalloc PciSizedBar[6];
            for (int i = 0; i < 6; i++)
                bars[i] = default;
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0x0000000c, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 4096, 0), new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 4096, 4), new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 4096, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 4096, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], CommonAddress, out Common));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 4096, NotifyAddress, out Notify));
        }

        internal VirtioNetQueueResult Run(bool transmit = false, uint multiplier = 4)
        {
            State->Sending = transmit;
            State->Multiplier = multiplier;
            return transmit ? FixedEthernetFrameReclaimDescriptors.Run(Common, Notify, multiplier, Pool, Access, 3, 3) : VirtioNetQueue.Run(Common, Notify, multiplier, Pool, Access, 3);
        }

        public void Dispose()
        {
            NativeMemory.Free(State);
            NativeMemory.Free(slot);
            NativeMemory.Free(Pool);
            NativeMemory.AlignedFree(Data);
        }
    }
}
