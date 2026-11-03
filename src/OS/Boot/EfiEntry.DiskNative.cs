#if M16_025_PROBE
#if !M16_023_PROBE || !M16_024_PROBE
#error Live disk descriptors require their explicit continuing block and filesystem parents.
#endif
using OS.Drivers;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

namespace OS.Boot;

// One retained BSP-owned diagnostic disk lifetime. Real device, mapping,
// native stack and guest admission are independently required by the runner.
public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        private const uint NativeDiskSlots = 8, NativeDiskCaches = 4;
#if M10_018_PROBE
        private const uint NativeDiskDirectoryCaches = 5;
#endif
#if M10_018_FULL_PROBE || M10_018_TIMING_PROBE
        private const uint NativeDiskFileChainCaches = 2;
#endif
        private const ulong NativeDiskEpoch = 0x4d31365f30323301;




        private static NativeDiskOwner s_nativeDisk;

        // One4KiB device DMA page; everything after it is ordinary supervisor
        // backing. Sizes follow real sizeof,8 operations/files,4 paired caches,
        // 4096-cluster maps and8192-byte per-completion ranges. No file preload.
        private static ulong NativeDiskTake(ref ulong cursor, ulong bytes)
        {
            ulong start = (cursor + 15) & ~15UL;
            cursor = start + bytes;
            return start;
        }
        private static ulong NativeDiskLayout(NativeDiskOwner* o, ulong start)
        {
            ulong cursor = start + Page;
            byte* sector = (byte*)NativeDiskTake(ref cursor, 512);
            byte* entries = (byte*)NativeDiskTake(ref cursor, PrimaryGPTIndependentlyBoundsCRC.ArrayBytes);
            var operations = (Fs.Operation*)NativeDiskTake(ref cursor, NativeDiskSlots * (ulong)sizeof(Fs.Operation));
            var files = (Fs.FileHandle*)NativeDiskTake(ref cursor, NativeDiskSlots * (ulong)sizeof(Fs.FileHandle));
            var cache = (Fs.CacheSlot*)NativeDiskTake(ref cursor, NativeDiskCaches * (ulong)sizeof(Fs.CacheSlot));
            var bindings = (LinuxDescriptorServices.DiskBinding*)NativeDiskTake(ref cursor, NativeDiskSlots * (ulong)sizeof(LinuxDescriptorServices.DiskBinding));
            var requests = (LinuxDescriptorServices.DiskRequest*)NativeDiskTake(ref cursor, NativeDiskSlots * (ulong)sizeof(LinuxDescriptorServices.DiskRequest));
            for (uint i = 0; i < NativeDiskSlots; i++)
            {
                var path = (byte*)NativeDiskTake(ref cursor, Fs.PathBytes);
                var io = (byte*)NativeDiskTake(ref cursor, 512);
                var name = (ushort*)NativeDiskTake(ref cursor, 520);
                var history = (uint*)NativeDiskTake(ref cursor, Fs.MaximumClusters * 4UL);
                var range = (byte*)NativeDiskTake(ref cursor, LinuxDescriptorServices.DiskCompletionBytes);
                if (o != null)
                    operations[i].Storage = new()
                    {
                        Path = path,
                        Sector = io,
                        Name = name,
                        History = history,
                        HistoryCapacity = Fs.MaximumClusters,
                        Range = range,
                        RangeCapacity = LinuxDescriptorServices.DiskCompletionBytes
                    };
            }
            for (uint i = 0; i < NativeDiskSlots; i++)
            {
                var map = (uint*)NativeDiskTake(ref cursor, Fs.MaximumClusters * 4UL);
                if (o != null)
                {
                    files[i].Clusters = map;
                    files[i].ClusterCapacity = Fs.MaximumClusters;
                }
            }
            for (uint i = 0; i < NativeDiskCaches; i++)
            {
                var data = (byte*)NativeDiskTake(ref cursor, 512);
                if (o != null)
                    cache[i].Data = data;
            }
            if (o != null)
            {
                o->Sector = sector;
                o->Entries = entries;
                o->Operations = operations;
                o->Files = files;
                o->Cache = cache;
                o->Bindings = bindings;
                o->Requests = requests;
            }
#if M10_018_PROBE
            // Separate immutable-media cache backing fits the existing arena's
            // tail; the four paired FAT caches and operation buffers stay owned.
            var directoryCache = (byte*)NativeDiskTake(ref cursor, Fs.ImmutableDirectoryCacheBytes(NativeDiskDirectoryCaches));
            if (o != null)
                o->DirectoryCache = directoryCache;
#endif
#if M10_018_FULL_PROBE || M10_018_TIMING_PROBE
            // Positive chain certificates lease admitted maps; no file data is
            // cached. Two records must remain inside the existing arena pages.
            var fileChainCache = (byte*)NativeDiskTake(ref cursor, Fs.ImmutableFileChainCacheBytes(NativeDiskFileChainCaches));
            if (o != null)
                o->FileChainCache = fileChainCache;
#endif
            return cursor - start;
        }

        // Called before final memory-map capture through a new BootSequence
        // preparation callback. The retained mapping writer admits this exact
        // allocation and separate disk BAR, disjoint from NIC0:6 and all owners.
        private static bool PrepareNativeDiskBacking(void* context) => PrepareNativeDisk((Storage*)context);

        private static bool PrepareNativeDisk(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->Phase != 0 || state == null || state->Phase != 2 || state->SystemTable == null)
                    return false;
                if (!M01StageOneAdapter.TryGetOriginalTypeManagerImage(out var imageCapture, out var imageMapping) ||
                    !M01StageTwoAdapter.TryGetOriginalExitAllocator(out _, out var allocator) ||
                    imageCapture.Range.ImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success)
                    return false;
                SeparatePageFaultMappingRange* imageRanges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
                if (!FirstExitImageMappings.WriteRanges(imageCapture, imageMapping, imageRanges, FirstExitRetainedMappings.MaximumRanges, out ulong imageCount) ||
                    !FirstExitImageMappings.Contains(imageRanges, imageCount, (ulong)o, (ulong)sizeof(NativeDiskOwner), PageProtection.Read | PageProtection.Write))
                    return false;
                uint* words = stackalloc uint[64];
                if (!ReadNativeDiskFixture(state, words, out var binding))
                    return false;
                PciSizedBar* bars = stackalloc PciSizedBar[6];
                for (uint i = 0; i < 6; i++)
                    bars[i] = default;
                VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
                if ((words[8] & 15) != 12 || BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(words[8], words[9],
                        0xffffc00c, 0xffffffff, 4, out bars[4]) != PciBarProbeStatus.Success ||
                    CommonNotifyISRDeviceConfigurationRegions.PlanBlock(binding.Capabilities, bars, 6, regions, 4) != VirtioRegionStatus.Success)
                    return false;
                for (uint i = 0; i < 4; i++)
                    if (!CommonNotifyISRDeviceConfigurationRegions.Admit(regions[i], s_bootHandoff.MemoryMap))
                        return false;
                ulong used = NativeDiskLayout(null, 0), pages = (used + 4095) / Page, candidate = 0xffffffff;
                var services = state->SystemTable->BootServices;
                if (services == null || services->AllocatePages == null || pages == 0 || pages > 256 ||
                    services->AllocatePages(1, 2, pages, &candidate) != 0)
                    return false;
                // A successful allocation is retained even if later admission
                // rejects. Never erase this receipt or infer firmware cleanup.
                o->Boot = state;
                o->Arena = candidate;
                o->ArenaPages = pages;
                o->Bar = bars[4].Base;
                o->Phase = 1;
                if (candidate < Page || (candidate & 4095) != 0 || candidate > 0x100000000UL - pages * Page ||
                    !Disjoint(candidate, pages * Page, o->Bar, 0x4000))
                    return false;
                ulong* starts = stackalloc ulong[13] { image, allocator.Start, state->NetworkArena, state->CooperativePool,
                    state->LinuxPackage, state->LinuxImageBacking, state->LinuxUserTables, state->LinuxSyscallCode,
                    state->LinuxSyscallData, state->LinuxMemoryPool, state->LinuxMemoryCandidate, state->LinuxDescriptorArena, (ulong)o };
                ulong* lengths = stackalloc ulong[13] { imageCapture.Range.ImageSize, allocator.PageCount * Page, 8 * Page,
                    CooperativePoolPages * Page, state->LinuxPages * Page, state->LinuxImagePages * Page,
                    X64UserAddressSpace.MaximumTables * Page, Page, X64LinuxUserTransition.DataBytes,
                    LinuxMemoryPoolPages(state) * Page, X64UserAddressSpace.MaximumTables * Page,
                    LinuxDescriptorPages * Page, (ulong)sizeof(NativeDiskOwner) };
                for (uint i = 0; i < 13; i++)
                    if (lengths[i] != 0 && !Disjoint(candidate, pages * Page, starts[i], lengths[i]))
                        return false;
                for (ulong i = 0; i < pages * Page; i++)
                    ((byte*)candidate)[i] = 0;
                return NativeDiskLayout(o, candidate) == used;
            }
        }

        private static bool ReadNativeDiskFixture(Storage* state, uint* words, out VirtioBlkPciBinding binding)
        {
            binding = default;
            if (state->PciWindow.FirstBus != 0 || state->PciWindow.LastBus != 0 ||
                !BoundedPCIConfigurationSpaceInterface.TryOpen(state->PciWindow, null, &ReadVirtioSnapshotWord, out var access))
                return false;
            var snapshot = ECAMPageVendorDeviceHeader.ReadSnapshot(ref access, 0, 7, 0, words, 64);
            BoundedPCIConfigurationSpaceInterface.Revoke(ref access);
            if (snapshot != BoundedPciStatus.Success || (words[1] & 2) == 0 ||
                LiveVirtioBlkPCIDevice.Parse((byte*)words, 256, 0, 7, 0, out binding) != VirtioBlkBindingStatus.Success)
                return false;
            var c = binding.Capabilities;
            return VirtioFixtureCapability(c.CommonConfiguration, 0) && VirtioFixtureCapability(c.NotifyConfiguration, 0x3000) &&
                VirtioFixtureCapability(c.IsrStatus, 0x1000) && VirtioFixtureCapability(c.DeviceConfiguration, 0x2000);
        }

        private static bool StartNativeDisk(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->Boot != state || o->Phase != 1 || state->OwnedPhase != 2 || state->PciPhase != 8 || state->NetworkPhase != 2 ||
                    !AuditNativeDiskMappings(state))
                    return false;
                byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
                byte* fence = state->Memory.Code + FirstExitCodeBundle.QueueFenceOffset;
                if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) || !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes))
                    return false;
                var read32 = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
                var write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
                var write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
                uint* words = stackalloc uint[64];
                if (!ReadNativeDiskFixture(state, words, out var binding) || (((ulong)words[9] << 32) | (words[8] & ~15u)) != o->Bar ||
                    MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover((byte*)words, 256, out o->Msix) != MsixStatus.Success ||
                    (o->Msix.Control & 0x8000) != 0)
                    return false;
                o->Command = (uint)((binding.OriginalCommand & 0xfffb) | 0x400);
                o->Phase = 2;
                if (SetNativeDiskBusMaster(o, false) != 1 || !ResetVirtio(o->Bar))
                    return false;
                PciSizedBar* bars = stackalloc PciSizedBar[6];
                var probe = N32BitN64BitBARsWhileAlwaysRestoring.Probe(state->PciWindow, 0, 7, 0, null, read32, write32, write16, bars, 6);
                if (!probe.Succeeded || bars[4].Kind != PciSizedBarKind.Memory64 || bars[4].Base != o->Bar || bars[4].Bytes != 0x4000 ||
                    bars[5].Kind != PciSizedBarKind.UpperHalf)
                    return false;
                var caps = binding.Capabilities;
                VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
                if (CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4) != VirtioRegionStatus.Success ||
                    CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], o->Bar, out o->Common) != VirtioRegionStatus.Success ||
                    CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], caps.NotifyConfiguration.Length,
                        o->Bar + 0x3000, out o->Notify) != VirtioRegionStatus.Success ||
                    CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], o->Bar + 0x2000, out o->Configuration) != VirtioRegionStatus.Success)
                    return false;
                o->NotifyMultiplier = caps.NotifyConfiguration.NotifyOffsetMultiplier;
                var negotiated = MinimalVirtioBlkFeatureSet.Negotiate(o->Common, 1024, null, read32, write32, &ReadVirtioStatus, &WriteVirtioStatus);
                if (!negotiated.Succeeded || negotiated.Selected != (BoundedVirtioFeatures.Version1 | BoundedVirtioFeatures.BlockReadOnly) ||
                    !NativeDiskCapacity(o, read32, out o->Capacity) ||
                    DmaAddressWindow.TryCreate(o->Arena, o->Arena, o->Arena, Page, 32, out var window) != DmaAddressStatus.Success ||
                    PageAlignedDmaBuffers.TryInitialize(&o->Pool, window, &o->DmaSlot, 1, NativeDiskEpoch) != DmaBufferStatus.Success)
                    return false;
                VirtioBlkReadAccess access = new();
                access.Queue.Context = o;
                access.Queue.Read8 = &ReadVirtioStatus;
                access.Queue.Write8 = &WriteVirtioStatus;
                access.Queue.Read16 = &ReadQueueWord;
                access.Queue.Write16 = write16;
                access.Queue.Read32 = read32;
                access.Queue.Write32 = write32;
                access.Queue.Fence = (delegate* unmanaged<void*, byte>)fence;
                access.Queue.BusMasterOff = &NativeDiskBusMasterOff;
                access.BusMasterOn = &NativeDiskBusMasterOn;
                var a = new Block.Admission
                {
                    Common = o->Common,
                    Notify = o->Notify,
                    Device = o->Configuration,
                    NotifyMultiplier = o->NotifyMultiplier,
                    ResetLimit = 1024,
                    Pool = &o->Pool,
                    Epoch = NativeDiskEpoch,
                    NegotiatedFeatures = negotiated.Selected,
                    ExpectedCapacity = o->Capacity,
                    CompletionTimeoutNanos = 2_000_000_000,
                    Access = access,
                    Clock = new() { Context = o, Read = &NativeDiskClock },
                    Observation = new() { Context = o, Observe = &ObserveNativeDisk }
                };
                if (Block.Start(&o->Device, a) != Block.Status.Success || !ReadNetworkTime(state, out ulong now) ||
                    now > (ulong)long.MaxValue - 30_000_000_000 ||
                    !LinuxText(state, "TEST_START m16.023\r\nTEST_START m16.024\r\nTEST_START m16.025\r\n"))
                    return false;
                o->BootstrapDeadline = now + 30_000_000_000;
                o->Phase = 3;
                return true;
            }
        }

        // Exactly one completion/parser/submit step per call. The enclosing
        // cooperative boot phase must also pump the actual NIC and real clock;
        // do not replace this with a synchronous ReadBlocks adapter/spin loop.
        private static NativeDiskProgress NativeDiskBootstrapStep(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->Boot != state || o->Phase < 3 || o->Phase > 7)
                    return NativeDiskProgress.Rejected;
                if (o->Phase == 7)
                    return NativeDiskProgress.Ready;
                if (!ReadNetworkTime(state, out ulong now) || now >= o->BootstrapDeadline)
                    return NativeDiskFail(o);
                if (o->Phase == 6)
                {
                    var progress = Fs.PumpMount(&o->Mount);
                    if (progress == Fs.Status.Ready)
                    {
                        o->Phase = 7;
                        return NativeDiskProgress.Ready;
                    }
                    return progress is Fs.Status.Pending or Fs.Status.Busy ? NativeDiskProgress.Pending : NativeDiskFail(o);
                }
                if (!o->IoActive)
                {
                    ulong lba = o->Phase == 3 ? 0 : o->Phase == 4 ? 1 : o->Header.EntryLba + o->ArraySector;
                    if (Block.Submit(&o->Device, lba, &o->Io) != Block.Status.Success)
                        return NativeDiskFail(o);
                    o->IoActive = true;
                    return NativeDiskProgress.Pending;
                }
                Block.Completion completion = default;
                var status = Block.Poll(&o->Device, &completion);
                if (status == Block.Status.Pending)
                    return NativeDiskProgress.Pending;
                if (status != Block.Status.Ready || completion.Bytes != 512 ||
                    Block.Copy(&o->Device, o->Io, new Span<byte>(o->Sector, 512)) != Block.Status.Success)
                    return NativeDiskFail(o);
                o->IoActive = false;
                o->Io = default;
                if (o->Phase == 3)
                {
                    if (!LBAN0Guest.Validate(new Span<byte>(o->Sector, 512), o->Capacity, BlockReadResult.Success(1, 512)).Succeeded)
                        return NativeDiskFail(o);
                    o->Phase = 4;
                }
                else if (o->Phase == 4)
                {
                    o->Header = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(new Span<byte>(o->Sector, 512), o->Capacity, BlockReadResult.Success(1, 512));
                    if (!o->Header.Succeeded)
                        return NativeDiskFail(o); // New fixture scope: fail-closed primary only; no silent fallback claim.
                    o->Phase = 5;
                }
                else
                {
                    for (uint i = 0; i < 512; i++)
                        o->Entries[o->ArraySector * 512 + i] = o->Sector[i];
                    if (++o->ArraySector != PrimaryGPTIndependentlyBoundsCRC.ArraySectors)
                        return NativeDiskProgress.Pending;
                    o->Partition = FAT32ApplicationEFIPartition.Select(o->Header, new Span<byte>(o->Entries, PrimaryGPTIndependentlyBoundsCRC.ArrayBytes));
                    if (!o->Partition.Succeeded)
                        return NativeDiskFail(o);
                    var a = new Fs.Admission
                    {
                        Device = &o->Device,
                        Epoch = NativeDiskEpoch,
                        PartitionFirst = o->Partition.FirstLba,
                        PartitionLast = o->Partition.LastLba,
                        Operations = o->Operations,
                        Files = o->Files,
                        Cache = o->Cache,
                        OperationCount = NativeDiskSlots,
                        FileCount = NativeDiskSlots,
                        CacheCount = NativeDiskCaches,
                        DirectoryEntryBudget = 4096
                    };
                    uint dataSectors = OS.Kernel.Diagnostics.Probes.EnableM10018FullProbe ||
                        OS.Kernel.Diagnostics.Probes.EnableM10018TimingProbe ? 4U : 1U;
                    if (Fs.Start(&o->Mount, a, dataSectors) != Fs.Status.Pending)
                        return NativeDiskFail(o);
#if M10_018_PROBE
                    // This frozen fixture has no writer. Configure before mount
                    // I/O; only ordinary mounted lookups may publish sectors.
                    if (Fs.EnableImmutableDirectoryCache(&o->Mount, o->DirectoryCache,
                        Fs.ImmutableDirectoryCacheBytes(NativeDiskDirectoryCaches), NativeDiskDirectoryCaches) != Fs.Status.Success)
                        return NativeDiskFail(o);
#endif
#if M10_018_FULL_PROBE || M10_018_TIMING_PROBE
                    // This frozen read-only device owns immutable FAT metadata
                    // for the complete mount lifetime. No borrowed/writer path.
                    if (Fs.EnableImmutableFileChainCache(&o->Mount, o->FileChainCache,
                        Fs.ImmutableFileChainCacheBytes(NativeDiskFileChainCaches), NativeDiskFileChainCaches) != Fs.Status.Success)
                        return NativeDiskFail(o);
#endif
                    o->Phase = 6;
                }
                return NativeDiskProgress.Pending;
            }
        }
        private static NativeDiskProgress NativeDiskFail(NativeDiskOwner* o)
        {
            _ = WriteNativeDiskRejection(o->Boot, 7, o); // Retain the failing parser phase before quarantine.
            o->Phase = 8;
            return NativeDiskProgress.Rejected;
        }

        // Attach before EnablePipes/EnableFutex so their disjoint-extent checks
        // include all78 disk/FS/DMA/MMIO ranges. Both owners remain retained.
        private static bool AttachNativeDiskDescriptors(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->Boot != state || o->Phase != 7 || !o->Mount.Ready || !o->Device.Running)
                    return false;
                o->BootstrapCompleted = o->Device.Completed;
                o->BootstrapCopied = o->Device.Copied;
                bool attached = LinuxDescriptorServices.AttachDisk(&state->LinuxDescriptors, &o->Descriptors,
                    new()
                    {
                        Mount = &o->Mount,
                        Bindings = o->Bindings,
                        Requests = o->Requests,
                        Epoch = NativeDiskEpoch,
                        FirmwareExited = true,
                        SingleOwnerValidated = true
                    });
#if M10_018_PROBE
                return attached && WriteNativeDiskBinding(state, o); // The web child arms at actual listen readiness.
#else
                return attached && ArmNativeDiskEvidence(o) && WriteNativeDiskBinding(state, o);
#endif
            }
        }

        private static bool WriteNativeDiskBinding(Storage* state, NativeDiskOwner* o) =>
            o->Device.Submitted == o->BootstrapCompleted && o->BootstrapCopied <= o->BootstrapCompleted &&
            ReadNetworkTime(state, out ulong now) &&
            LinuxText(state, "M16_023_BOUND owner=") && LinuxNumber(state, (ulong)(&o->Device)) &&
            LinuxText(state, " epoch=") && LinuxNumber(state, NativeDiskEpoch) &&
            LinuxText(state, " arena=") && LinuxNumber(state, o->Arena) &&
            LinuxText(state, " pages=") && LinuxNumber(state, o->ArenaPages) &&
            LinuxText(state, " bar=") && LinuxNumber(state, o->Bar) &&
            LinuxText(state, " capacity=") && LinuxNumber(state, o->Capacity) &&
            LinuxText(state, " bootstrapCompleted=") && LinuxNumber(state, o->BootstrapCompleted) &&
            LinuxText(state, " bootstrapCopied=") && LinuxNumber(state, o->BootstrapCopied) &&
            LinuxText(state, " clock=") && LinuxNumber(state, now) &&
            LinuxText(state, " pci=0:7:0 queue=0 outstanding=1 access=readonly progress=cooperative reservation=retained\r\n");

        // Invoke after all genuine user exits/reaps, BEFORE Process.Release.
        // A failed/uncertain reset retains every owner and stops final cleanup.
        // Once retired, PumpDiskOne can scrub orphan metadata without live FS I/O.
        private static bool RetireNativeDisk(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
            {
                if (o->Boot != state || o->Phase == 0)
                    return false;
                if (o->Phase == 9)
                    return true;
                if (!ObserveNativeDiskExitBeforeStop(o))
                    return false;
                var stopped = o->Device.RequiresCleanup ? Block.Stop(&o->Device) : Block.Status.Success;
                if (stopped != Block.Status.Success || !o->Device.Retired || !ObserveNativeDiskExitStopped(o, stopped))
                    return false;
                if (o->Mount.RequiresCleanup && Fs.Stop(&o->Mount) != Fs.Status.Success)
                    return false;
                if (o->Pool.AllocatedMask != 0)
                    return false;
                o->Phase = 9;
                return true;
            }
        }

        private static bool NativeDiskAllowsOuterTeardown(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
                return o->Boot == state && (o->Phase == 9 || o->Phase == 1 &&
                    !o->Device.RequiresCleanup && !o->Device.Running && o->Pool.AllocatedMask == 0);
        }

        private static bool WriteNativeDiskCompletion(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
                return o->Boot == state && o->Phase == 9 && o->Device.Retired && o->Mount.IsRetired &&
                    o->Device.Completed > o->BootstrapCompleted && o->Device.Copied > o->BootstrapCopied &&
                    o->Pool.AllocatedMask == 0 && state->LinuxDescriptorPhase == 6 && !o->Evidence.Failed &&
                    o->Evidence.Count == 0 && o->Evidence.CopiedRecords != 0 &&
                    LinuxText(state, "M16_025_END submitted=") && LinuxNumber(state, o->Device.Submitted) &&
                    LinuxText(state, " completed=") && LinuxNumber(state, o->Device.Completed) &&
                    LinuxText(state, " copied=") && LinuxNumber(state, o->Device.Copied) &&
                    LinuxText(state, " bootstrapCompleted=") && LinuxNumber(state, o->BootstrapCompleted) &&
                    LinuxText(state, " bootstrapCopied=") && LinuxNumber(state, o->BootstrapCopied) &&
                    LinuxText(state, " fsReads=") && LinuxNumber(state, o->Mount.SectorReads) &&
                    LinuxText(state, " cacheHits=") && LinuxNumber(state, o->Mount.CacheHits) &&
                    LinuxText(state, " cacheMisses=") && LinuxNumber(state, o->Mount.CacheMisses) &&
                    LinuxText(state, " device=retired fs=retired dma=zero descriptors=closed reservation=retained\r\n" +
                        "CLROS_M16_023_READY\r\nTEST_PASS m16.023\r\nCLROS_M16_024_READY\r\nTEST_PASS m16.024\r\n" +
                        "CLROS_M16_025_READY\r\nTEST_PASS m16.025\r\n");
        }

        private static bool NativeDiskCapacity(NativeDiskOwner* o, delegate* unmanaged<void*, ulong, uint*, byte> read, out ulong capacity)
        {
            capacity = 0;
            for (uint i = 0; i < 16; i++)
            {
                byte before = *(byte*)(o->Common.VirtualStart + 21);
                uint low = 0, high = 0;
                if (read(null, o->Configuration.VirtualStart, &low) != 1 || read(null, o->Configuration.VirtualStart + 4, &high) != 1)
                    return false;
                byte after = *(byte*)(o->Common.VirtualStart + 21);
                if (before != after)
                    continue;
                capacity = ((ulong)high << 32) | low;
                return capacity >= 68 && capacity <= (ulong)long.MaxValue / 512;
            }
            return false;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NativeDiskClock(void* context, ulong* time)
        {
            var o = (NativeDiskOwner*)context;
            if (time == null || !ReadNetworkTime(o->Boot, out ulong now))
                return 0;
            *time = now;
            return 1;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NativeDiskQuantumFlush(void* context)
        {
            var o = (NativeDiskOwner*)context;
            return o != null && o->Boot != null && FlushNativeDiskEvidence(o->Boot) ? (byte)1 : (byte)0;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NativeDiskBusMasterOn(void* context) => SetNativeDiskBusMaster((NativeDiskOwner*)context, true);
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NativeDiskBusMasterOff(void* context) => SetNativeDiskBusMaster((NativeDiskOwner*)context, false);
        private static byte SetNativeDiskBusMaster(NativeDiskOwner* o, bool enabled)
        {
            if (o == null || o->Boot == null || o->Phase < 2 || (o->Command & 0x406) != 0x402 || !o->Msix.IsValid)
                return 0;
            byte* code = o->Boot->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes))
                return 0;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            ulong configuration = o->Boot->PciWindow.Start + 0x38000;
            uint control = 0, command = 0, wanted = enabled ? o->Command | 4 : o->Command;
            return read(null, configuration + o->Msix.CapabilityOffset, &control) == 1 && (control >> 16) == o->Msix.Control &&
                (control & 0x80000000) == 0 && write(null, configuration + 4, wanted) == 1 &&
                read(null, configuration + 4, &command) == 1 && (command & 0xffff) == wanted ? (byte)1 : (byte)0;
        }

        // Requires the new guarded reference fields and mapping inventory patch.
        private static bool AuditNativeDiskMappings(Storage* state)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk)
                if (o->Boot != state || state->Memory.References.NativeDiskArenaStart != o->Arena ||
                    state->Memory.References.NativeDiskArenaPages != o->ArenaPages || state->Memory.References.NativeDiskBarStart != o->Bar ||
                    (NativeDiskLayout(null, 0) + Page - 1) / Page != o->ArenaPages)
                    return false;
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            return FirstExitRetainedMappings.WriteInstalledRanges(ref state->Arena, state->Memory.Code, state->Memory.Capture,
                    state->Memory.Mapping, state->Memory.References, state->Memory.Allocator, state->Memory.Tables, ranges,
                    FirstExitRetainedMappings.MaximumRanges, out ulong count) &&
                SeparateDisabledDefaultPageFaultPageTables.AuditRetainedAccessedImage(state->Memory.Tables, ranges, count,
                    FirstExitRetainedMappings.MaximumRanges, state->Memory.Pages, state->Memory.PageCapacity, state->Memory.Entries,
                    state->Memory.EntryCapacity).Succeeded;
        }
    }
}
#endif
