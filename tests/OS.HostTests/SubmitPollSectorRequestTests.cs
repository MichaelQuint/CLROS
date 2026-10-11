using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;

internal static unsafe class SubmitPollSectorRequestTests
{
    [HostTest("virtio.blk.read submits an independently checked three-descriptor sector chain")]
    private static void Success()
    {
        HostCheck.True(!default(VirtioBlkReadResult).Succeeded);
        using var f = new BlockReadFixture(); var r = f.Run();
        HostCheck.True(r.Succeeded && r.Published && r.Notified && r.BytesRead == 512);
        HostCheck.True(r.Sector == BlockReadFixture.Sector && r.Capacity == BlockReadFixture.Capacity && r.CapacityReads == 1);
        HostCheck.True(r.NotifyOffset == 3 && r.NotifyAddress == BlockReadFixture.Notify + 12 && r.Generation == 7);
        HostCheck.True(r.UsedIndex == 1 && r.UsedId == 0 && r.UsedLength == 513 && r.DeviceStatus == 0 && r.Polls == 1);
        HostCheck.True(r.Queue.Succeeded && r.Queue.Fences == 2 && r.Fences == 4 && r.Queue.ResetReads == 1);
        HostCheck.True(f.State->ChainChecked && f.State->Acquired && f.State->ResetObserved &&
            f.State->CleanupBusOff && f.State->CleanupFence && !f.State->BusEnabled && f.Pool->AllocatedMask == 0);
        for (int i = 0; i < 512; i++) HostCheck.Equal(BlockReadFixture.Payload(i), f.Output[i]);
        f.Check();
    }

    [HostTest("virtio.blk.read rejects capacity range generation and notify errors before publication")]
    private static void Admission()
    {
        foreach (ulong capacity in new ulong[] { 0, ulong.MaxValue / 512 + 1, BlockReadFixture.Sector })
        {
            using var f = new BlockReadFixture(); f.State->Capacity = capacity;
            var r = f.Run(); HostCheck.Equal(capacity == BlockReadFixture.Sector ? VirtioBlkReadStatus.SectorOutOfRange :
                VirtioBlkReadStatus.InvalidCapacity, r.Status);
            HostCheck.True(!r.Published && f.Pool->AllocatedMask == 0 && r.Queue.ResetComplete);
            f.Unchanged(); f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            f.State->GenerationChurn = true; var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.ConfigurationChanged && r.CapacityReads == 16 && !r.Published);
            f.Unchanged(); f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            f.State->ChurnOnce = true; var r = f.Run();
            HostCheck.True(r.Succeeded && r.CapacityReads == 2); f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            f.State->ChangeBeforePublish = true; var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.ConfigurationChanged && !r.Published && r.Queue.DmaReleased);
            f.Unchanged(); f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            f.State->NotifyOffset = ushort.MaxValue; var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidNotify && !r.Published && r.Queue.DmaReleased);
            f.Unchanged(); f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            f.Multiplier = 0; var r = f.Run();
            HostCheck.True(r.Succeeded && r.NotifyAddress == BlockReadFixture.Notify); f.Check();
        }
    }

    [HostTest("virtio.blk.read rejects invalid callbacks limits and destinations without effects")]
    private static void InvalidInputs()
    {
        for (int missing = 0; missing < 9; missing++)
        {
            using var f = new BlockReadFixture(); var a = f.Access;
            switch (missing)
            {
                case 0: a.Queue.Read8 = null; break; case 1: a.Queue.Write8 = null; break;
                case 2: a.Queue.Read16 = null; break; case 3: a.Queue.Write16 = null; break;
                case 4: a.Queue.Read32 = null; break; case 5: a.Queue.Write32 = null; break;
                case 6: a.Queue.Fence = null; break; case 7: a.Queue.BusMasterOff = null; break;
                case 8: a.BusMasterOn = null; break;
            }
            var r = f.Run(a); HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidAccess && f.State->Calls == 0);
            f.Unchanged(); f.Check();
        }
        for (int input = 0; input < 10; input++)
        {
            using var f = new BlockReadFixture();
            if (input == 0) f.PollLimit = 0; if (input == 1) f.PollLimit = 1000001;
            if (input == 2) f.ResetLimit = 0; if (input == 3) f.ResetLimit = 1025;
            if (input == 4) f.Multiplier = 3;
            byte* output = input == 5 ? null : input == 6 ? (byte*)1 : input == 7 ? (byte*)BlockReadFixture.Common :
                input == 8 ? (byte*)0x0000800000000000UL : f.Output;
            var r = f.Run(f.Access, output, input == 9 ? 511U : 512U);
            HostCheck.True(!r.Succeeded && f.State->Calls == 0); f.Unchanged(); f.Check();
        }
    }

    [HostTest("virtio.blk.read interrupt and masked recovery preserve completion validation and cleanup")]
    private static void InterruptRoutes()
    {
        foreach (var route in new[] { BlockCompletionRoute.Interrupt, BlockCompletionRoute.PollingRecovery })
        {
            using var f = new BlockReadFixture();
            f.State->InterruptRoute = (byte)route;
            f.State->Delay = route == BlockCompletionRoute.Interrupt ? 0U : 2U;
            var r = f.Run(f.InterruptAccess);
            HostCheck.True(r.Succeeded && r.InterruptAttempted && r.InterruptPrepared && r.InterruptStopped &&
                r.CompletionRoute == route && f.State->InterruptWaited && f.State->InterruptStopped &&
                r.Polls == f.State->Delay + 1 && f.Pool->AllocatedMask == 0);
            f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            f.State->InterruptRoute = (byte)BlockCompletionRoute.Interrupt; f.State->Delay = 1;
            var r = f.Run(f.InterruptAccess);
            HostCheck.True(r.Status == VirtioBlkReadStatus.Timeout && r.Polls == 1 && r.InterruptStopped && r.Queue.DmaReleased);
            f.Unchanged(); f.Check();
        }
    }

    [HostTest("virtio.blk.read partial interrupt callback sets reject before hardware effects")]
    private static void InterruptAdmission()
    {
        for (int present = 1; present < 7; present++)
        {
            using var f = new BlockReadFixture(); var a = f.InterruptAccess;
            if ((present & 1) == 0) a.PrepareInterrupt = null;
            if ((present & 2) == 0) a.WaitInterrupt = null;
            if ((present & 4) == 0) a.StopInterrupt = null;
            var r = f.Run(a);
            HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidAccess && f.State->Calls == 0 && !r.InterruptAttempted);
            f.Unchanged(); f.Check();
        }
    }

    [HostTest("virtio.blk.read failed prepare or invalid wait shuts down before cleanup and never copies")]
    private static void InterruptFailures()
    {
        for (int fault = 0; fault < 4; fault++)
        {
            using var f = new BlockReadFixture();
            f.State->FailInterruptPrepare = fault == 0;
            f.State->InterruptRoute = fault == 1 ? (byte)0 : fault == 2 ? (byte)BlockCompletionRoute.InvalidWait : (byte)255;
            var r = f.Run(f.InterruptAccess);
            HostCheck.Equal(fault == 0 ? VirtioBlkReadStatus.InterruptPrepareFailed : VirtioBlkReadStatus.InterruptWaitFailed, r.Status);
            HostCheck.True(r.InterruptAttempted && r.InterruptStopped && f.State->InterruptStopped && r.Queue.ResetComplete &&
                r.Queue.BusMasterDisabled && f.Pool->AllocatedMask == 0 && (fault != 0 || !r.Published));
            f.Unchanged(); f.Check();
        }
    }

    [HostTest("virtio.blk.read uncertain interrupt shutdown retains device DMA and suppresses payload")]
    private static void InterruptShutdown()
    {
        using var f = new BlockReadFixture();
        f.State->InterruptRoute = (byte)BlockCompletionRoute.Interrupt; f.State->FailInterruptStop = true;
        var r = f.Run(f.InterruptAccess);
        HostCheck.True(r.Status == VirtioBlkReadStatus.InterruptShutdownFailed && r.BytesRead == 0 &&
            !r.InterruptStopped && !r.Queue.DmaReleased && r.Queue.ResetComplete && r.Queue.BusMasterDisabled && f.Pool->AllocatedMask == 1);
        HostCheck.Equal(DmaBufferStatus.Exhausted, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out _));
        f.Unchanged(); f.Check();
    }

    [HostTest("virtio.blk.read protects DMA and output from MMIO and metadata overlap")]
    private static void Aliases()
    {
        using (var f = new BlockReadFixture())
        {
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out var held));
            var r = f.Run();
            HostCheck.True(r.Status == VirtioBlkReadStatus.QueueRejected && !r.Queue.DmaReleased && f.Pool->AllocatedMask == 1);
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryRelease(f.Pool, held.Token));
            HostCheck.Equal(DmaBufferStatus.StaleToken, PageAlignedDmaBuffers.TryRelease(f.Pool, held.Token));
            f.Unchanged(); f.Check();
        }
        foreach (ulong physical in new ulong[] { BlockReadFixture.Bar, BlockReadFixture.Bar + 0x2000, BlockReadFixture.Bar + 0x3000 })
        {
            using var f = new BlockReadFixture(physical);
            var r = f.Run(); HostCheck.True(r.Status == VirtioBlkReadStatus.QueueRejected && r.Queue.Status == VirtioBlkQueueStatus.Overlap);
            HostCheck.True(r.Queue.DmaReleased && new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
            f.Unchanged(); f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            var r = f.Run(f.Access, f.Data, 512);
            HostCheck.True(r.Status == VirtioBlkReadStatus.QueueRejected && r.Queue.Status == VirtioBlkQueueStatus.Overlap && r.Queue.DmaReleased);
            HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0); f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            byte[] before = new ReadOnlySpan<byte>(f.Pool, sizeof(DmaBufferPool)).ToArray();
            var r = f.Run(f.Access, (byte*)f.Pool, 512);
            HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidDestination && f.State->Calls == 0 &&
                before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Pool, sizeof(DmaBufferPool))));
            f.Check();
        }
        using (var f = new BlockReadFixture())
        {
            byte[] before = new ReadOnlySpan<byte>(f.Slot, sizeof(DmaBufferSlot)).ToArray();
            var r = f.Run(f.Access, (byte*)f.Slot, 512);
            HostCheck.True(r.Status == VirtioBlkReadStatus.InvalidDestination && f.State->Calls == 0 &&
                before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(f.Slot, sizeof(DmaBufferSlot))));
            f.Check();
        }
    }
}

// Explicit fake device. It independently decodes descriptor bytes and verifies
// every release/publication boundary; it is never evidence of real device I/O.
internal sealed unsafe class BlockReadFixture : IDisposable
{
    internal const ulong Common = 0x400000, Notify = 0x403000, Config = 0x402000,
        Bar = 0xc000000000, Physical = 0x220000, DeviceBase = 0x123456780000,
        Sector = 0x123456789, Capacity = 0x223456789;
    internal struct FakeDevice
    {
        internal byte* Data, Output;
        internal DmaBufferPool* Pool;
        internal fixed uint Addresses[6];
        internal ulong Capacity, ExpectedSector;
        internal uint Calls, FailCall, Fences, FailFence, BusOffCalls, FailBusOff, GenerationReads, PollReads, Delay,
            UsedId, UsedLength, WriteMask, ReadMask;
        internal ushort Selected, Size, Enabled, ConfigVector, QueueVector, NotifyOffset, UsedIndex;
        internal byte Status, CompletionStatus, InterruptRoute;
        internal bool InterruptPrepared, InterruptWaited, InterruptStopped, FailInterruptPrepare, FailInterruptStop;
        internal bool FailAfterWrite, FailedCallback, BadAccess, BadOrder, BusEnabled, Notified, ChainChecked,
            IndexObserved, Acquired, ResetRequested, ResetObserved, CleanupBusOff, CleanupFence, ResetStuck,
            GenerationChurn, ChurnOnce, ChangeBeforePublish, ChangeAfterCompletion, NeverComplete, ProtectiveMbr;
    }
    private static long epoch;
    private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
    private readonly byte* destination = (byte*)NativeMemory.Alloc(544);
    internal byte* Data => backing + 4096;
    internal byte* Output => destination + 16;
    internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
    private readonly DmaBufferSlot* slot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
    internal DmaBufferSlot* Slot => slot;
    internal readonly FakeDevice* State = (FakeDevice*)NativeMemory.AllocZeroed((nuint)sizeof(FakeDevice));
    private readonly VirtioMappedRegion common, notify, config;
    internal uint PollLimit = 4, ResetLimit = 3, Multiplier = 4;
    internal VirtioBlkReadAccess Access => new() { Queue = new() { Context = State, Read8 = &Read8,
        Write8 = &Write8, Read16 = &Read16, Write16 = &Write16, Read32 = &Read32, Write32 = &Write32,
        Fence = &Fence, BusMasterOff = &BusOff }, BusMasterOn = &BusOn };
    internal VirtioBlkReadAccess InterruptAccess
    {
        get { var a = Access; a.PrepareInterrupt = &PrepareInterrupt; a.WaitInterrupt = &WaitInterrupt; a.StopInterrupt = &StopInterrupt; return a; }
    }
    internal BlockReadFixture(ulong physical = Physical)
    {
        new Span<byte>(backing, 12288).Fill(0xa5); new Span<byte>(destination, 544).Fill(0xa5);
        HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, physical, DeviceBase, 4096, 64, out var window));
        HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, slot, 1,
            (ulong)System.Threading.Interlocked.Increment(ref epoch)));
        State->Data = Data; State->Output = Output; State->Pool = Pool; State->Status = 11;
        State->Capacity = Capacity; State->ExpectedSector = Sector;
        State->Selected = 0xffff; State->NotifyOffset = 3; State->UsedIndex = 1; State->UsedLength = 513;
        PciSizedBar* bars = stackalloc PciSizedBar[6]; new Span<PciSizedBar>(bars, 6).Clear();
        HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
        var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0),
            new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 64, 4),
            new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
        VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4));
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out common));
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], 64, Notify, out notify));
        HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], Config, out config));
    }
    internal VirtioBlkReadResult Run() => Run(Access);
    internal LiveDeviceExistingIReadOnlyBlockDeviceContract CreateLiveDevice(ulong capacity = Capacity, uint blockSize = 512)
    {
        var info = BlockDeviceInfo.Create(blockSize, capacity);
        HostCheck.True(info.Succeeded);
        return new(info.Info, common, notify, config, Multiplier, Pool, Access, PollLimit, ResetLimit);
    }
    internal VirtioBlkReadResult Run(VirtioBlkReadAccess access) => Run(access, Output, 512);
    internal VirtioBlkReadResult Run(VirtioBlkReadAccess access, byte* output, uint bytes) =>
        SubmitPollSectorRequest.Run(common, notify, config, Multiplier, Pool, access, Sector, output, bytes, PollLimit, ResetLimit);
    internal static byte Payload(int i) => unchecked((byte)(i * 17 + 0x3d));
    internal void Unchanged() => HostCheck.True(new ReadOnlySpan<byte>(Output, 512).IndexOfAnyExcept((byte)0xa5) < 0);
    internal void Check() => HostCheck.True(!State->BadAccess && !State->BadOrder &&
        new ReadOnlySpan<byte>(backing, 4096).IndexOfAnyExcept((byte)0xa5) < 0 &&
        new ReadOnlySpan<byte>(Data + 4096, 4096).IndexOfAnyExcept((byte)0xa5) < 0 &&
        new ReadOnlySpan<byte>(destination, 16).IndexOfAnyExcept((byte)0xa5) < 0 &&
        new ReadOnlySpan<byte>(Output + 512, 16).IndexOfAnyExcept((byte)0xa5) < 0);
    public void Dispose() { NativeMemory.AlignedFree(backing); NativeMemory.Free(destination); NativeMemory.Free(State); NativeMemory.Free(slot); NativeMemory.Free(Pool); }

    private static bool Begin(FakeDevice* d, bool write = false)
    {
        d->Calls++;
        if (d->Calls != d->FailCall) return true;
        d->FailedCallback = true; return write && d->FailAfterWrite;
    }
    private static byte End(FakeDevice* d) => d->Calls == d->FailCall ? (byte)0 : (byte)1;
    private static byte Bad(FakeDevice* d) { d->BadAccess = true; return 0; }
    [UnmanagedCallersOnly]
    private static byte PrepareInterrupt(void* context, VirtioMappedRegion common)
    {
        var d = (FakeDevice*)context;
        if (common.VirtualStart != Common || d->Status != 11 || d->BusEnabled || d->Enabled != 0 ||
            d->Selected != 0xffff || d->Pool->AllocatedMask != 0) d->BadOrder = true;
        d->InterruptPrepared = true; return d->FailInterruptPrepare ? (byte)0 : (byte)1;
    }
    [UnmanagedCallersOnly]
    private static byte WaitInterrupt(void* context, uint limit)
    {
        var d = (FakeDevice*)context;
        if (!d->InterruptPrepared || d->InterruptStopped || !d->Notified || d->Status != 15 ||
            !d->BusEnabled || limit == 0 || d->PollReads != 0 || d->Pool->AllocatedMask != 1) d->BadOrder = true;
        d->InterruptWaited = true; return d->InterruptRoute;
    }
    [UnmanagedCallersOnly]
    private static byte StopInterrupt(void* context)
    {
        var d = (FakeDevice*)context;
        if (!d->InterruptPrepared || d->ResetRequested || d->InterruptStopped ||
            (d->Notified && d->Pool->AllocatedMask != 1)) d->BadOrder = true;
        d->InterruptStopped = true; return d->FailInterruptStop ? (byte)0 : (byte)1;
    }
    [UnmanagedCallersOnly]
    private static byte Read8(void* context, ulong address, byte* value)
    {
        var d = (FakeDevice*)context; if (!Begin(d)) return 0;
        if (address == Common + 20)
        {
            *value = d->Status;
            if (d->ResetRequested && *value == 0) d->ResetObserved = true;
        }
        else if (address == Common + 21)
        {
            d->GenerationReads++;
            *value = d->GenerationChurn ? (byte)d->GenerationReads : d->ChurnOnce && d->GenerationReads == 1 ? (byte)6 :
                (d->ChangeBeforePublish && d->Status == 15) || (d->ChangeAfterCompletion && d->Acquired) ? (byte)8 : (byte)7;
        }
        else if (address == (ulong)d->Data + 272)
        { if (!d->Acquired) d->BadOrder = true; *value = d->Data[272]; }
        else return Bad(d);
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write8(void* context, ulong address, byte value)
    {
        var d = (FakeDevice*)context; if (!Begin(d, true)) return 0;
        if (address != Common + 20) return Bad(d);
        if (value == 15)
        {
            if (!d->BusEnabled || d->Enabled != 1 || d->ReadMask != 63 || d->Fences != 2) d->BadOrder = true;
            d->Status = 15;
        }
        else if (value == 0)
        {
            d->ResetRequested = true;
            if (d->WriteMask != 0 && d->Pool->AllocatedMask != 1) d->BadOrder = true;
            if (new ReadOnlySpan<byte>(d->Output, 512).IndexOfAnyExcept((byte)0xa5) >= 0) d->BadOrder = true;
            if (!d->ResetStuck) d->Status = 0;
        }
        else return Bad(d);
        return End(d);
    }
    [UnmanagedCallersOnly]
    private static byte Read16(void* context, ulong address, ushort* value)
    {
        var d = (FakeDevice*)context; if (!Begin(d)) return 0;
        if (address == Common + 18) *value = 1;
        else if (address == Common + 22) *value = d->Selected;
        else if (address == Common + 24) *value = d->Size == 0 ? (ushort)16 : d->Size;
        else if (address == Common + 16) *value = d->ConfigVector;
        else if (address == Common + 26) *value = d->QueueVector;
        else if (address == Common + 28) *value = d->Enabled;
        else if (address == Common + 30) *value = d->NotifyOffset;
        else if (address == (ulong)d->Data + 150)
        {
            d->PollReads++;
            if (!d->Notified || d->Fences != 4) d->BadOrder = true;
            *value = d->NeverComplete || d->PollReads <= d->Delay ? (ushort)0 : d->UsedIndex;
            if (*value != 0) d->IndexObserved = true;
        }
        else return Bad(d);
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write16(void* context, ulong address, uint value)
    {
        var d = (FakeDevice*)context; if (!Begin(d, true)) return 0;
        if (address == Common + 22 && value == 0) d->Selected = 0;
        else if (address == Common + 16 && value == 0xffff) d->ConfigVector = 0xffff;
        else if (address == Common + 26 && (value == 0xffff || (value == 0 && d->InterruptPrepared))) d->QueueVector = (ushort)value;
        else if (address == Common + 24 && value == 8) d->Size = 8;
        else if (address == Common + 28 && value == 1)
        {
            if (d->ReadMask != 63 || d->Fences != 1 || d->Pool->AllocatedMask != 1 || d->BusEnabled ||
                d->ConfigVector != 0xffff || d->QueueVector != (d->InterruptPrepared ? 0 : 0xffff) || *(ushort*)(d->Data + 130) != 0) d->BadOrder = true;
            d->Enabled = 1;
        }
        else if ((address == Notify || address == Notify + 12) && value == 0)
        {
            if (!d->BusEnabled || d->Status != 15 || d->Enabled != 1 || d->Fences != 4 || d->Pool->AllocatedMask != 1) d->BadOrder = true;
            d->Notified = true; CheckChain(d);
            for (int i = 0; i < 512; i++) d->Data[512 + i] = Payload(i);
            if (d->ProtectiveMbr)
            {
                var mbr = new Span<byte>(d->Data + 512, 512); mbr.Clear();
                mbr[450] = 0xee; mbr[510] = 0x55; mbr[511] = 0xaa;
                BinaryPrimitives.WriteUInt32LittleEndian(mbr[454..], 1);
                BinaryPrimitives.WriteUInt32LittleEndian(mbr[458..], (uint)Math.Min(d->Capacity - 1, uint.MaxValue));
            }
            d->Data[272] = d->CompletionStatus;
        }
        else return Bad(d);
        return End(d);
    }
    [UnmanagedCallersOnly]
    private static byte Read32(void* context, ulong address, uint* value)
    {
        var d = (FakeDevice*)context; if (!Begin(d)) return 0;
        if (address == Config) *value = (uint)d->Capacity;
        else if (address == Config + 4) *value = (uint)(d->Capacity >> 32);
        else if (address >= Common + 32 && address <= Common + 52 && (address & 3) == 0)
        { uint i = (uint)(address - Common - 32) / 4; d->ReadMask |= 1u << (int)i; *value = d->Addresses[i]; }
        else if (address == (ulong)d->Data + 152 || address == (ulong)d->Data + 156)
        { if (!d->Acquired) d->BadOrder = true; *value = address == (ulong)d->Data + 152 ? d->UsedId : d->UsedLength; }
        else return Bad(d);
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write32(void* context, ulong address, uint value)
    {
        var d = (FakeDevice*)context; if (!Begin(d, true)) return 0;
        if (address < Common + 32 || address > Common + 52 || (address & 3) != 0) return Bad(d);
        uint i = (uint)(address - Common - 32) / 4;
        ulong expected = DeviceBase + (i < 2 ? 0U : i < 4 ? 128U : 148U);
        if (d->Fences != 1 || d->BusEnabled || d->Enabled != 0 || value != (uint)(expected >> ((i & 1) == 0 ? 0 : 32))) d->BadOrder = true;
        d->Addresses[i] = value; d->WriteMask |= 1U << (int)i; return End(d);
    }
    [UnmanagedCallersOnly]
    private static byte Fence(void* context)
    {
        var d = (FakeDevice*)context; d->Fences++; if (!Begin(d)) return 0;
        if (d->Fences == d->FailFence) { d->FailedCallback = true; return 0; }
        if (d->ResetRequested)
        { if (!d->ResetObserved || !d->CleanupBusOff) d->BadOrder = true; d->CleanupFence = true; }
        else if (d->IndexObserved) d->Acquired = true;
        else if (d->Fences == 4 && *(ushort*)(d->Data + 130) != 1) d->BadOrder = true;
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte BusOff(void* context)
    {
        var d = (FakeDevice*)context; d->BusOffCalls++; if (!Begin(d)) return 0;
        if (d->BusOffCalls == d->FailBusOff) { d->FailedCallback = true; return 0; }
        d->BusEnabled = false; if (d->ResetRequested) d->CleanupBusOff = true; return 1;
    }
    [UnmanagedCallersOnly]
    private static byte BusOn(void* context)
    {
        var d = (FakeDevice*)context; if (!Begin(d, true)) return 0;
        if (d->Fences != 2 || d->Pool->AllocatedMask != 1 || *(ushort*)(d->Data + 130) != 0) d->BadOrder = true;
        d->BusEnabled = true; return End(d);
    }
    private static void CheckChain(FakeDevice* d)
    {
        var bytes = new ReadOnlySpan<byte>(d->Data, 4096);
        bool good = true;
        for (int i = 0; i < 3; i++)
        {
            var descriptor = bytes.Slice(i * 16, 16);
            good &= BinaryPrimitives.ReadUInt64LittleEndian(descriptor) == DeviceBase + (i == 0 ? 256U : i == 1 ? 512U : 272U) &&
                BinaryPrimitives.ReadUInt32LittleEndian(descriptor[8..]) == (i == 0 ? 16U : i == 1 ? 512U : 1U) &&
                BinaryPrimitives.ReadUInt16LittleEndian(descriptor[12..]) == (i == 0 ? 1 : i == 1 ? 3 : 2) &&
                BinaryPrimitives.ReadUInt16LittleEndian(descriptor[14..]) == (i == 0 ? 1 : i == 1 ? 2 : 0);
        }
        good &= bytes.Slice(48, 80).IndexOfAnyExcept((byte)0) < 0 && bytes[128] == 1 && bytes[129] == 0 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[130..]) == 1 && bytes.Slice(132, 124).IndexOfAnyExcept((byte)0) < 0 &&
            BinaryPrimitives.ReadUInt64LittleEndian(bytes[256..]) == 0 && BinaryPrimitives.ReadUInt64LittleEndian(bytes[264..]) == d->ExpectedSector &&
            bytes[272] == 0xff && bytes.Slice(273).IndexOfAnyExcept((byte)0) < 0;
        d->ChainChecked = good; if (!good) d->BadOrder = true;
    }
}
