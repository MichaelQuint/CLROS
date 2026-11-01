using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using M = OS.Storage.Fat32Mutation;
using Block = OS.Storage.WritableBlockSession;

internal static unsafe partial class Fat32MutationTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly byte[] Snapshot = new byte[Sectors * 512];
        internal readonly M.Change[] Changes = new M.Change[128];
        internal readonly byte[] Before = new byte[128 * 512], After = new byte[128 * 512];
        internal readonly uint[] Scratch = new uint[M.ScratchClusters];
        internal readonly Hardware* H = (Hardware*)NativeMemory.AllocZeroed((nuint)sizeof(Hardware));
        private readonly byte* dma = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        private readonly DmaBufferPool* pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* slot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
        private readonly List<nint> states = [];
        internal Block.State* State;
        internal readonly List<(uint Type, ulong Sector)> Trace = [];
        internal int FailAt;
        internal Span<byte> Volatile => new(H->Volatile, Snapshot.Length);
        internal Span<byte> Durable => new(H->Durable, Snapshot.Length);
        private VirtioMappedRegion common, notify, config;
        internal Fixture()
        {
            H->Data = dma; H->Now = 1; H->Volatile = (byte*)NativeMemory.AllocZeroed((nuint)Snapshot.Length); H->Durable = (byte*)NativeMemory.AllocZeroed((nuint)Snapshot.Length);
            Span<byte> boot = Snapshot.AsSpan(First * 512, 512); Put16(boot, 11, 512); boot[13] = 1; Put16(boot, 14, Reserved); boot[16] = 2;
            Put32(boot, 32, Sectors - First); Put32(boot, 36, FatSectors); Put32(boot, 44, 2); Put16(boot, 48, 1); Put16(boot, 50, 6); boot[510] = 0x55; boot[511] = 0xaa;
            boot.CopyTo(Snapshot.AsSpan((First + 6) * 512, 512));
            foreach (int lba in new[] { First + 1, First + 7 })
            { var fs = Snapshot.AsSpan(lba * 512, 512); Put32(fs, 0, 0x41615252); Put32(fs, 484, 0x61417272); Put32(fs, 488, Clusters - 2); Put32(fs, 492, 4); Put32(fs, 508, 0xaa550000); }
            foreach (int lba in new[] { First + Reserved, First + Reserved + FatSectors })
            { var fat = Snapshot.AsSpan(lba * 512); Put32(fat, 0, 0xffffff8); Put32(fat, 4, 0xfffffff); Put32(fat, 8, 0xfffffff); Put32(fat, 12, 0xfffffff); }
            var keep = Snapshot.AsSpan(Data * 512, 32); "KEEP    TXT"u8.CopyTo(keep); keep[11] = 0x20; Put16(keep, 26, 3); Put32(keep, 28, 3);
            new byte[] { 4, 5, 6 }.CopyTo(Snapshot, (Data + 1) * 512);
            Snapshot.CopyTo(Volatile); Snapshot.CopyTo(Durable);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)dma, Physical, Dma, 4096, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(pool, window, slot, 1, 75));
            PciSizedBar* bars = stackalloc PciSizedBar[6]; new Span<PciSizedBar>(bars, 6).Clear();
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0), new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 64, 4),
                new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out common));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 64, Notify, out notify));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], Config, out config)); Restart();
        }
        internal void Restart()
        {
            FailAt = 0; H->Produced = 0;
            State = (Block.State*)NativeMemory.AllocZeroed((nuint)sizeof(Block.State)); states.Add((nint)State);
            var queue = new VirtioBlkQueueAccess { Context = H, Read8 = &R8, Write8 = &W8, Read16 = &R16, Write16 = &W16, Read32 = &R32, Write32 = &W32, Fence = &Yes, BusMasterOff = &Yes };
            var negotiation = Block.Negotiate(common, 3, queue); HostCheck.True(negotiation.Succeeded);
            HostCheck.Equal(Block.Status.Success, Block.Start(State, new() { Common = common, Notify = notify, Device = config, NotifyMultiplier = 4, ResetLimit = 3,
                Pool = pool, Epoch = (ulong)states.Count, ExpectedCapacity = Sectors, CompletionTimeoutNanos = 1000, Negotiation = negotiation,
                Access = new() { Queue = queue, BusMasterOn = &Yes }, Clock = new() { Context = H, Read = &Clock } }));
        }
        internal M.Session Session(int capacity = 128, uint budget = 100000)
        {
            M.Session value = default; HostCheck.Equal(M.Status.Success, value.Start(Snapshot, First, Sectors - 1, 1234, 4321, State,
                Changes.AsSpan(0, capacity), Before.AsSpan(0, capacity * 512), After.AsSpan(0, capacity * 512), Scratch, budget)); return value;
        }
        internal void Complete()
        {
            HostCheck.True(H->Pending && H->Sector < Sectors); bool fail = H->Notifies == FailAt;
            Trace.Add((H->Type, H->Sector));
            if (H->Type == 1) new ReadOnlySpan<byte>(dma + 512, fail ? 257 : 512).CopyTo(Volatile.Slice((int)H->Sector * 512));
            else { HostCheck.Equal(4U, H->Type); if (!fail) Volatile.CopyTo(Durable); }
            dma[272] = fail ? (byte)1 : (byte)0; byte* item = dma + 152 + (H->Produced & 7) * 8; *(uint*)item = 0; *(uint*)(item + 4) = 1;
            H->Produced++; *(ushort*)(dma + 150) = unchecked((ushort)H->Produced); H->Pending = false;
        }
        public void Dispose()
        {
            if (State != null && !State->Retired) HostCheck.Equal(Block.Status.Success, Block.Stop(State));
            foreach (nint state in states) NativeMemory.Free((void*)state);
            NativeMemory.Free(H->Volatile); NativeMemory.Free(H->Durable); NativeMemory.Free(H); NativeMemory.Free(pool); NativeMemory.Free(slot); NativeMemory.AlignedFree(dma);
        }
    }
}
