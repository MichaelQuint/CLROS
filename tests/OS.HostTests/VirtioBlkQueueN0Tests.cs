using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;

internal static unsafe class VirtioBlkQueueN0Tests
{
    private const ulong Common = 0x400000, Physical = 0x220000, DeviceBase = 0x123456780000;
    private const uint Available = 128, Used = 148;
    private struct Device
    {
        public byte* Data;
        public DmaBufferPool* Pool;
        public fixed uint Addresses[6];
        public uint Calls, FailCall, Fences, BusCalls, StatusReads, ResetReads, FailFence, FailBus, BadStatusAt;
        public uint WriteMask, ReadMask, MismatchOffset;
        public ushort Maximum, Queues, Selected, Size, Enabled, ConfigVector, QueueVector;
        public byte Status;
        public bool BadAccess, BadOrder, FailedCallback, FailAfterWrite, BadSelect, RejectEnable, ResetStuck;
        public bool ResetRequested, ResetObserved, CleanupBusOff, CleanupFence, HadDma, HeldDuringReset, RingObserved;
    }

    [HostTest("virtio.blk.queue0 configures empty size-eight rings with distinct address domains and ordered release")]
    private static void Success()
    {
        HostCheck.True(!default(VirtioBlkQueueResult).Succeeded);
        using var f = new Fixture(); var r = f.Run();
        HostCheck.True(r.Succeeded && r.Configured && r.ResetComplete && r.BusMasterDisabled && r.DmaReleased);
        HostCheck.True(r.Size == 8 && r.MaximumSize == 16 && r.Fences == 2 && r.ResetReads == 1);
        HostCheck.True(r.Layout.Range.CpuVirtualAddress == (ulong)f.Data && r.Layout.Range.PhysicalAddress == Physical &&
            r.Layout.Range.DeviceAddress == DeviceBase && (ulong)f.Data != Physical && (ulong)f.Data != DeviceBase);
        HostCheck.True(Address(f.State, 0) == DeviceBase && Address(f.State, 2) == DeviceBase + Available &&
            Address(f.State, 4) == DeviceBase + Used && f.State->WriteMask == 63 && f.State->ReadMask == 63);
        HostCheck.True(f.State->ConfigVector == 0xffff && f.State->QueueVector == 0xffff && f.State->Size == 8 &&
            f.State->Selected == 0 && f.State->BusCalls == 2 && f.State->StatusReads == 3);
        HostCheck.True(f.State->RingObserved && EmptyRing(f.State) && f.State->HeldDuringReset &&
            f.State->ResetObserved && f.State->CleanupBusOff && f.State->CleanupFence && f.Pool->AllocatedMask == 0);
        Clean(f);
    }

    [HostTest("virtio.blk.queue0 rejects queue status and register readback failures")]
    private static void DeviceRejection()
    {
        foreach (byte status in new byte[] { 0, 3, 15, 0x4b, 0x8b })
        {
            using var f = new Fixture(); f.State->Status = status;
            HostCheck.Equal(VirtioBlkQueueStatus.StatusRejected, f.Run().Status); NoDma(f);
        }
        foreach (ushort maximum in new ushort[] { 0, 3, 4, 32769 })
        {
            using var f = new Fixture(); f.State->Maximum = maximum;
            HostCheck.Equal(VirtioBlkQueueStatus.InvalidQueue, f.Run().Status); NoDma(f);
        }
        for (int invalid = 0; invalid < 3; invalid++)
        {
            using var f = new Fixture();
            if (invalid == 0) f.State->Queues = 0;
            if (invalid == 1) f.State->BadSelect = true;
            if (invalid == 2) f.State->Enabled = 1;
            HostCheck.Equal(VirtioBlkQueueStatus.InvalidQueue, f.Run().Status); NoDma(f);
        }
        foreach (uint offset in new uint[] { 16, 24, 26, 32, 36, 40, 44, 48, 52 })
        {
            using var f = new Fixture(); f.State->MismatchOffset = offset;
            var r = f.Run(); HostCheck.Equal(VirtioBlkQueueStatus.ReadbackMismatch, r.Status);
            HostCheck.True(r.DmaReleased && r.ResetComplete && f.State->HeldDuringReset); Clean(f);
        }
        using (var f = new Fixture())
        {
            f.State->RejectEnable = true;
            var r = f.Run(); HostCheck.Equal(VirtioBlkQueueStatus.ReadbackMismatch, r.Status);
            HostCheck.True(r.DmaReleased); Clean(f);
        }
        foreach (uint stage in new uint[] { 2, 3 })
        {
            using var f = new Fixture(); f.State->BadStatusAt = stage;
            var r = f.Run(); HostCheck.Equal(VirtioBlkQueueStatus.StatusRejected, r.Status);
            HostCheck.True(r.DmaReleased && f.State->HeldDuringReset); Clean(f);
        }
    }

    [HostTest("virtio.blk.queue0 retains DMA on uncertain reset disable or cleanup ordering")]
    private static void CleanupFailures()
    {
        using (var f = new Fixture())
        {
            f.State->ResetStuck = true;
            var r = f.Run(); HostCheck.Equal(VirtioBlkQueueStatus.ResetFailed, r.CleanupStatus);
            HostCheck.True(r.ResetReads == 3 && f.State->ResetReads == 3 && !r.ResetComplete); Retained(f, r); Clean(f);
        }
        using (var f = new Fixture())
        {
            f.State->FailBus = 2;
            var r = f.Run(); HostCheck.Equal(VirtioBlkQueueStatus.BusMasterFailed, r.CleanupStatus);
            HostCheck.True(r.ResetComplete && !r.BusMasterDisabled); Retained(f, r); Clean(f);
        }
        foreach (uint fence in new uint[] { 1, 2 })
        {
            using var f = new Fixture(); f.State->FailFence = fence;
            var r = f.Run(); HostCheck.True(!r.Succeeded && f.State->FailedCallback);
            if (fence == 1)
            { HostCheck.Equal(VirtioBlkQueueStatus.OrderingFailed, r.Status); HostCheck.True(r.DmaReleased && r.ResetComplete); }
            else { HostCheck.Equal(VirtioBlkQueueStatus.OrderingFailed, r.CleanupStatus); Retained(f, r); }
            Clean(f);
        }
    }

    [HostTest("virtio.blk.queue0 rejects invalid access before effects and handles every callback failure")]
    private static void AccessFailures()
    {
        for (int missing = 0; missing < 8; missing++)
        {
            using var f = new Fixture(); var a = f.Access;
            switch (missing)
            {
                case 0: a.Read8 = null; break; case 1: a.Write8 = null; break;
                case 2: a.Read16 = null; break; case 3: a.Write16 = null; break;
                case 4: a.Read32 = null; break; case 5: a.Write32 = null; break;
                case 6: a.Fence = null; break; case 7: a.BusMasterOff = null; break;
            }
            HostCheck.Equal(VirtioBlkQueueStatus.InvalidAccess, VirtioBlkQueueN0.Run(f.Region, f.Pool, a, 3).Status);
            HostCheck.Equal(0U, f.State->Calls); NoDma(f);
        }
        foreach (uint limit in new uint[] { 0, 1025, uint.MaxValue })
        {
            using var f = new Fixture();
            HostCheck.Equal(VirtioBlkQueueStatus.InvalidAccess, VirtioBlkQueueN0.Run(f.Region, f.Pool, f.Access, limit).Status);
            HostCheck.Equal(0U, f.State->Calls); NoDma(f);
        }
        using (var f = new Fixture())
        {
            HostCheck.Equal(VirtioBlkQueueStatus.InvalidAccess, VirtioBlkQueueN0.Run(default, f.Pool, f.Access, 3).Status);
            HostCheck.Equal(0U, f.State->Calls); NoDma(f);
        }
        uint calls;
        using (var baseline = new Fixture()) { HostCheck.True(baseline.Run().Succeeded); calls = baseline.State->Calls; }
        foreach (bool afterWrite in new[] { false, true })
            for (uint fail = 1; fail <= calls; fail++)
            {
                using var f = new Fixture(); f.State->FailCall = fail; f.State->FailAfterWrite = afterWrite;
                var r = f.Run(); HostCheck.True(f.State->FailedCallback && !r.Succeeded); Clean(f);
                if (r.DmaReleased) HostCheck.True(r.ResetComplete && r.BusMasterDisabled && f.State->CleanupFence);
                if (f.Pool->AllocatedMask != 0) Retained(f, r);
            }
    }

    private static bool Begin(Device* d, bool write = false)
    {
        d->Calls++;
        if (d->ResetRequested && d->HadDma && d->Pool->AllocatedMask != 1) d->BadOrder = true;
        if (d->Calls != d->FailCall) return true;
        d->FailedCallback = true; return write && d->FailAfterWrite;
    }
    private static byte End(Device* d) => d->Calls == d->FailCall ? (byte)0 : (byte)1;
    private static byte Bad(Device* d) { d->BadAccess = true; return 0; }
    [UnmanagedCallersOnly]
    private static byte Read8(void* context, ulong address, byte* value)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (address != Common + 20) return Bad(d);
        if (d->ResetRequested)
        {
            d->ResetReads++; *value = d->ResetStuck ? (byte)11 : (byte)0;
            if (*value == 0) d->ResetObserved = true;
        }
        else { d->StatusReads++; *value = d->StatusReads == d->BadStatusAt ? (byte)0x4b : d->Status; }
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write8(void* context, ulong address, byte value)
    {
        Device* d = (Device*)context; if (!Begin(d, true)) return 0;
        // Any DRIVER_OK write, notification, wrong width or unknown address is an error.
        if (address != Common + 20 || value != 0) return Bad(d);
        d->ResetRequested = true; d->HadDma = d->Pool->AllocatedMask != 0; d->HeldDuringReset = d->HadDma;
        return End(d);
    }
    [UnmanagedCallersOnly]
    private static byte Read16(void* context, ulong address, ushort* value)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (address == Common + 18) *value = d->Queues;
        else if (address == Common + 22) *value = d->BadSelect ? (ushort)1 : d->Selected;
        else if (address == Common + 24) *value = d->Size == 0 ? d->Maximum : d->Size;
        else if (address == Common + 16) *value = d->ConfigVector;
        else if (address == Common + 26) *value = d->QueueVector;
        else if (address == Common + 28) *value = d->Enabled;
        else return Bad(d);
        if (d->Size != 0 && address - Common == d->MismatchOffset) *value ^= 1;
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write16(void* context, ulong address, uint value)
    {
        Device* d = (Device*)context; if (!Begin(d, true)) return 0;
        if (address == Common + 22 && value == 0) { d->Selected = 0; return End(d); }
        if (d->Pool->AllocatedMask != 1 || d->Fences != 1 || !EmptyRing(d)) d->BadOrder = true;
        if (address == Common + 16 && value == 0xffff) d->ConfigVector = 0xffff;
        else if (address == Common + 26 && value == 0xffff) d->QueueVector = 0xffff;
        else if (address == Common + 24 && value == 8) d->Size = 8;
        else if (address == Common + 28 && value == 1)
        {
            if (d->ReadMask != 63 || !Addresses(d) || d->ConfigVector != 0xffff || d->QueueVector != 0xffff) d->BadOrder = true;
            d->Enabled = d->RejectEnable ? (ushort)0 : (ushort)1;
        }
        else return Bad(d);
        return End(d);
    }
    [UnmanagedCallersOnly]
    private static byte Read32(void* context, ulong address, uint* value)
    {
        Device* d = (Device*)context; if (!Begin(d)) return 0;
        if (address < Common + 32 || address > Common + 52 || (address & 3) != 0) return Bad(d);
        uint i = (uint)(address - Common - 32) / 4;
        if (d->WriteMask != 63) d->BadOrder = true;
        d->ReadMask |= 1U << (int)i; *value = d->Addresses[i];
        if (address - Common == d->MismatchOffset) *value ^= 1;
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write32(void* context, ulong address, uint value)
    {
        Device* d = (Device*)context; if (!Begin(d, true)) return 0;
        if (address < Common + 32 || address > Common + 52 || (address & 3) != 0) return Bad(d);
        uint i = (uint)(address - Common - 32) / 4;
        ulong expected = DeviceBase + (i < 2 ? 0U : i < 4 ? Available : Used);
        if (d->Fences != 1 || d->Pool->AllocatedMask != 1 || d->Enabled != 0 ||
            d->WriteMask != (1U << (int)i) - 1 || value != (uint)(expected >> ((i & 1) == 0 ? 0 : 32))) d->BadOrder = true;
        d->Addresses[i] = value; d->WriteMask |= 1U << (int)i; return End(d);
    }
    [UnmanagedCallersOnly]
    private static byte Fence(void* context)
    {
        Device* d = (Device*)context; d->Fences++; if (!Begin(d)) return 0;
        if (d->Fences == d->FailFence) { d->FailedCallback = true; return 0; }
        if (d->ResetRequested)
        {
            if (!d->ResetObserved || !d->CleanupBusOff) d->BadOrder = true;
            d->CleanupFence = true;
        }
        else
        {
            if (d->Pool->AllocatedMask != 1 || d->WriteMask != 0 || !EmptyRing(d)) d->BadOrder = true;
            d->RingObserved = true;
        }
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte BusOff(void* context)
    {
        Device* d = (Device*)context; d->BusCalls++; if (!Begin(d)) return 0;
        if (d->BusCalls == d->FailBus) { d->FailedCallback = true; return 0; }
        if (d->ResetRequested) d->CleanupBusOff = true;
        return 1;
    }
    private static ulong Address(Device* d, int index) => d->Addresses[index] | ((ulong)d->Addresses[index + 1] << 32);
    private static bool Addresses(Device* d) => Address(d, 0) == DeviceBase && Address(d, 2) == DeviceBase + Available && Address(d, 4) == DeviceBase + Used;
    private static bool EmptyRing(Device* d)
    {
        for (int i = 0; i < 4096; i++) if (d->Data[i] != (i == 128 ? (byte)1 : i == 272 ? (byte)0xff : (byte)0)) return false;
        return true;
    }
    private static void Clean(Fixture f) => HostCheck.True(!f.State->BadAccess && !f.State->BadOrder &&
        new ReadOnlySpan<byte>(f.Data - 4096, 4096).IndexOfAnyExcept((byte)0xa5) < 0 &&
        new ReadOnlySpan<byte>(f.Data + 4096, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
    private static void NoDma(Fixture f)
    { HostCheck.True(f.Pool->AllocatedMask == 0 && new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0); Clean(f); }
    private static void Retained(Fixture f, VirtioBlkQueueResult r)
    {
        HostCheck.True(!r.Succeeded && !r.DmaReleased && f.Pool->AllocatedMask == 1);
        HostCheck.Equal(DmaBufferStatus.Exhausted, PageAlignedDmaBuffers.TryAllocate(f.Pool, 1, out _));
    }
    [HostTest("virtio.blk.queue0 rejects physical MMIO alias before clearing or programming DMA")]
    private static void OverlappingBacking()
    {
        using var f = new Fixture(physical: 0xc000000000);
        var r = f.Run();
        HostCheck.Equal(VirtioBlkQueueStatus.Overlap, r.Status);
        HostCheck.True(r.ResetComplete && r.BusMasterDisabled && r.DmaReleased && f.Pool->AllocatedMask == 0);
        HostCheck.True(f.State->WriteMask == 0 && f.State->ReadMask == 0);
        HostCheck.True(new ReadOnlySpan<byte>(f.Data, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
    }

    private sealed class Fixture : IDisposable
    {
        private static long epoch;
        private readonly byte* backing = (byte*)NativeMemory.AlignedAlloc(12288, 4096);
        internal byte* Data => backing + 4096;
        internal readonly DmaBufferPool* Pool = (DmaBufferPool*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferPool));
        private readonly DmaBufferSlot* slot = (DmaBufferSlot*)NativeMemory.AllocZeroed((nuint)sizeof(DmaBufferSlot));
        internal readonly Device* State = (Device*)NativeMemory.AllocZeroed((nuint)sizeof(Device));
        internal readonly VirtioMappedRegion Region;
        internal VirtioBlkQueueAccess Access => new() { Context = State, Read8 = &Read8, Write8 = &Write8,
            Read16 = &Read16, Write16 = &Write16, Read32 = &Read32, Write32 = &Write32, Fence = &Fence, BusMasterOff = &BusOff };
        internal Fixture(ulong physical = Physical)
        {
            new Span<byte>(backing, 12288).Fill(0xa5);
            HostCheck.Equal(DmaAddressStatus.Success, DmaAddressWindow.TryCreate((ulong)Data, physical, DeviceBase, 4096, 64, out var window));
            HostCheck.Equal(DmaBufferStatus.Success, PageAlignedDmaBuffers.TryInitialize(Pool, window, slot, 1,
                (ulong)System.Threading.Interlocked.Increment(ref epoch)));
            State->Data = Data; State->Pool = Pool; State->Status = 11; State->Queues = 1; State->Maximum = 16;
            State->Selected = 0xffff; State->MismatchOffset = uint.MaxValue;
            PciSizedBar* bars = stackalloc PciSizedBar[6]; new Span<PciSizedBar>(bars, 6).Clear();
            HostCheck.Equal(PciBarProbeStatus.Success, BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(0xc, 0xc0, 0xffffc00c, 0xffffffff, 4, out bars[4]));
            var caps = VirtioPciCapabilitySet.Create(new(VirtioPciCapabilityType.CommonConfiguration, 4, 0, 56, 0),
                new(VirtioPciCapabilityType.NotifyConfiguration, 4, 0x3000, 2, 4),
                new(VirtioPciCapabilityType.IsrStatus, 4, 0x1000, 1, 0), new(VirtioPciCapabilityType.DeviceConfiguration, 4, 0x2000, 8, 0));
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4));
            HostCheck.Equal(VirtioRegionStatus.Success, CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], Common, out Region));
        }
        internal VirtioBlkQueueResult Run() => VirtioBlkQueueN0.Run(Region, Pool, Access, 3);
        public void Dispose() { NativeMemory.AlignedFree(backing); NativeMemory.Free(State); NativeMemory.Free(slot); NativeMemory.Free(Pool); }
    }
}
