#if M16_027_PROBE
#if !M16_025_PROBE || !M16_024_PROBE || !M16_023_PROBE
#error Writable disk admission requires the selected disk compile infrastructure.
#endif
using OS.Drivers;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Storage;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using K = OS.Storage.ContinuingFat32Bootstrap;
using P = OS.Storage.WritableBlockReadPort;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

namespace OS.Boot;

// Default-off owner preparation only. Shared boot/profile/worker/runner hooks
// and original native/guest qualification are separate integration obligations.
public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        private const ulong NativeWritableEpoch = 0x4d31365f30323701;
        private const uint NativeWritableSlots = 8, NativeWritableCaches = 4;
        private enum NativeWritableDiskProgress : byte { Rejected, Pending, Ready }
        // This opaque callback carrier is separate from every typed owner.
        // Its exact pointer and referents are checked before callback access.
        private struct NativeWritableCarrier
        { internal ulong Self, Owner, Epoch; internal Storage* Boot; }
        private struct NativeWritableOwner
        {
            internal Storage* Boot;
            internal ulong Arena, Pages, Bar, NicBar, Capacity, Deadline;
            internal uint Command, NotifyMultiplier, PrimaryPid;
            internal byte Phase; //1 retained,2 device,3 GPT,4 C mount,5 FS mount,6 pair ready,7 retired,8 quarantined.
            internal bool BootstrapStarted, EngineStarted, PortStarted, FsStarted, ProviderStarted, ReadAttached, WriteAttached, Attached;
            internal bool UserCleanup;
            internal uint Reaped, Transfers;
            internal ulong CleanupClock, Epochs;
            internal VirtioMappedRegion Common, Notify, Configuration;
            internal MsixCapability Msix;
            internal B.State* Device;
            internal DmaBufferPool* Pool;
            internal DmaBufferSlot* Slot;
            internal B.Observation* Observation;
            internal NativeWritableEvidence* Evidence;
            internal NativeWritableCarrier* Carrier;
            internal C.State* Engine;
            internal C.Admission EngineInput;
            internal K.State* Bootstrap;
            internal K.Result* Selected;
            internal byte* BootstrapSector;
            internal byte* Entries;
            internal byte* Header;
            internal P.State* Port;
            internal Fs.Mount* Mount;
            internal Fs.Operation* Operations;
            internal Fs.FileHandle* Files;
            internal Fs.CacheSlot* Cache;
            internal F.DiskState* ReadDescriptors;
            internal F.DiskBinding* ReadBindings;
            internal F.DiskRequest* ReadRequests;
            internal F.WritableDiskState* WriteDescriptors;
            internal F.WritableDiskBinding* WriteBindings;
            internal W.State* Provider;
            internal W.Descriptor* Fds;
            internal W.Description* Descriptions;
            internal byte* ProviderInput;
            internal W.ProcessSlot* Processes;
            internal W.RangeLock* Locks;
            internal W.RangeLock* LockScratch;
            internal LiveEfiPartitionResult Esp;
        }
        private static NativeWritableOwner s_nativeWritable;

        // The original outer supervisor owns the deadline and yields to the
        // live NIC between bounded parser/device turns. Failure retains every
        // partial owner; an iteration ceiling never establishes readiness.
        private static bool RunNativeWritableDiskBootstrap(Storage* state)
        {
            fixed (LinuxSocketStorage* network = &s_linuxSocketStorage)
            {
                if (state->CooperativePhase != 0 || state->LinuxDescriptorPhase != 4 || state->LinuxSyscallPhase != 4 ||
                    state->LinuxMemory.HasMutationGuard || !LinuxSocketIoCurrent(state, network) || network->Phase != 2 ||
                    !LinuxText(state, "TEST_START m16.027\r\n") || !StartNativeWritableDisk(state)) return false;
                uint pid = state->LinuxDescriptors.Owner.PackageProcessId;
                for (uint turn = 0; turn < 3_000_000; turn++)
                {
                    if (!PumpLinuxSocketFrames(network)) return false;
                    var progress = NativeWritableDiskBootstrapStep(state, pid);
                    if (progress == NativeWritableDiskProgress.Rejected) return false;
                    if (progress == NativeWritableDiskProgress.Ready) return true;
                }
                return false;
            }
        }

        // No live worker, pending native handoff or user copy survives entry.
        // Each turn services the NIC and gives each serialized disk source one
        // pump. Only actual idle owners authorize the following Release.
        private static bool QuiesceNativeWritableDisk(Storage* state, LinuxNativeControl* c)
        {
            fixed (LinuxSocketStorage* network = &s_linuxSocketStorage)
            {
                if (!NativeWritableControl(state, c) || !LinuxSocketIoCurrent(state, network) ||
                    !ReadNetworkTime(state, out ulong now) || now > (ulong)long.MaxValue - 120_000_000_000) return false;
                ulong deadline = now + 120_000_000_000;
                for (uint turn = 0; turn < 3_000_000; turn++)
                {
                    if (!PumpLinuxSocketFrames(network) || !ReadNetworkTime(state, out now) || now >= deadline) return false;
                    var progress = QuiesceNativeWritableDiskStep(state, c);
                    if (progress == NativeWritableDiskProgress.Rejected) return false;
                    if (progress == NativeWritableDiskProgress.Ready) return true;
                }
                return false;
            }
        }

        private static bool RecordNativeWritableUserCleanup(Storage* state, uint reaped, uint transfers, ulong epochs)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            {
                if (!NativeWritableDiskAllowsOuterTeardown(state) || o->UserCleanup || reaped == 0 || transfers == 0 ||
                    state->CooperativePhase != 6 || state->LinuxSyscallPhase != 6 || state->LinuxMemoryPhase != 6 ||
                    state->LinuxDescriptorPhase != 6 || state->LinuxSyscallExitCode != 42 || state->LinuxSyscallFailed != 0 ||
                    state->LinuxSyscallFaults != 0 || !ReadNetworkTime(state, out ulong now) || now < o->Evidence->LastClock) return false;
                o->Reaped = reaped; o->Transfers = transfers; o->Epochs = epochs; o->CleanupClock = now; o->UserCleanup = true;
                return true;
            }
        }

        // Called only after the ordinary network core has checked its original
        // allocator/PCI/canary receipts and actually retired/reset the NIC.
        private static bool WriteNativeWritableDiskCompletion(Storage* state)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            fixed (NetworkOwnerStorage* network = &s_networkOwner)
                return NativeWritableDiskAllowsOuterTeardown(state) && o->UserCleanup && state->NetworkPhase == 3 &&
                    network->Device.Retired && network->Pool.AllocatedMask == 0 &&
                    ReadOwnedNetworkClock(state, out ulong now) && now >= o->CleanupClock &&
                    LinuxText(state, "M16_027_END owner=") && LinuxNumber(state, (ulong)o->Device) &&
                    LinuxText(state, " epoch=") && LinuxNumber(state, NativeWritableEpoch) &&
                    LinuxText(state, " reaped=") && LinuxNumber(state, o->Reaped) &&
                    LinuxText(state, " transfers=") && LinuxNumber(state, o->Transfers) &&
                    LinuxText(state, " irqs=") && LinuxNumber(state, o->Epochs) &&
                    LinuxText(state, " submitted=") && LinuxNumber(state, o->Device->Submitted) &&
                    LinuxText(state, " completed=") && LinuxNumber(state, o->Device->Completed) &&
                    LinuxText(state, " submittedBytes=") && LinuxNumber(state, o->Device->SubmittedBytes) &&
                    LinuxText(state, " completedBytes=") && LinuxNumber(state, o->Device->CompletedBytes) &&
                    LinuxText(state, " flushAcknowledgedBytes=") && LinuxNumber(state, o->Device->DurableBytes) &&
                    LinuxText(state, " clock=") && LinuxNumber(state, now) &&
                    LinuxText(state, " process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=retired dma=zero reservation=retained\r\nCLROS_M16_027_READY\r\nTEST_PASS m16.027\r\n");
        }

        // Each pointer is either installed once into zeroed backing or compared
        // to its original sizeof-derived location without changing any owner.
        private static bool NativeWritablePlace(ref ulong cursor, ulong bytes, void** target, bool install)
        {
            if (bytes == 0 || cursor > ulong.MaxValue - 15) return false;
            ulong address = (cursor + 15) & ~15UL;
            if (bytes > ulong.MaxValue - address) return false;
            cursor = address + bytes;
            if (target == null) return true;
            if (install) *target = (void*)address;
            return (ulong)*target == address;
        }
        private static ulong NativeWritableLayout(NativeWritableOwner* o, ulong start, bool install)
        {
            if (start > ulong.MaxValue - Page) return 0;
            ulong p = start + Page; // The first page alone is queue0 DMA.
            if (!NativeWritablePlace(ref p, (ulong)sizeof(B.State), o == null ? null : (void**)&o->Device, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(DmaBufferPool), o == null ? null : (void**)&o->Pool, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(DmaBufferSlot), o == null ? null : (void**)&o->Slot, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(B.Observation), o == null ? null : (void**)&o->Observation, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(NativeWritableEvidence), o == null ? null : (void**)&o->Evidence, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(NativeWritableCarrier), o == null ? null : (void**)&o->Carrier, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(C.State), o == null ? null : (void**)&o->Engine, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(K.State), o == null ? null : (void**)&o->Bootstrap, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(K.Result), o == null ? null : (void**)&o->Selected, install) ||
                !NativeWritablePlace(ref p, 512, o == null ? null : (void**)&o->BootstrapSector, install) ||
                !NativeWritablePlace(ref p, K.EntryBytes, o == null ? null : (void**)&o->Entries, install) ||
                !NativeWritablePlace(ref p, 512, o == null ? null : (void**)&o->Header, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(P.State), o == null ? null : (void**)&o->Port, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(Fs.Mount), o == null ? null : (void**)&o->Mount, install) ||
                !NativeWritablePlace(ref p, NativeWritableSlots * (ulong)sizeof(Fs.Operation), o == null ? null : (void**)&o->Operations, install) ||
                !NativeWritablePlace(ref p, NativeWritableSlots * (ulong)sizeof(Fs.FileHandle), o == null ? null : (void**)&o->Files, install) ||
                !NativeWritablePlace(ref p, NativeWritableCaches * (ulong)sizeof(Fs.CacheSlot), o == null ? null : (void**)&o->Cache, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(F.DiskState), o == null ? null : (void**)&o->ReadDescriptors, install) ||
                !NativeWritablePlace(ref p, NativeWritableSlots * (ulong)sizeof(F.DiskBinding), o == null ? null : (void**)&o->ReadBindings, install) ||
                !NativeWritablePlace(ref p, NativeWritableSlots * (ulong)sizeof(F.DiskRequest), o == null ? null : (void**)&o->ReadRequests, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(F.WritableDiskState), o == null ? null : (void**)&o->WriteDescriptors, install) ||
                !NativeWritablePlace(ref p, W.MaximumDescriptions * (ulong)sizeof(F.WritableDiskBinding), o == null ? null : (void**)&o->WriteBindings, install) ||
                !NativeWritablePlace(ref p, (ulong)sizeof(W.State), o == null ? null : (void**)&o->Provider, install) ||
                !NativeWritablePlace(ref p, W.MaximumDescriptors * (ulong)sizeof(W.Descriptor), o == null ? null : (void**)&o->Fds, install) ||
                !NativeWritablePlace(ref p, W.MaximumDescriptions * (ulong)sizeof(W.Description), o == null ? null : (void**)&o->Descriptions, install) ||
                !NativeWritablePlace(ref p, W.IoBytes, o == null ? null : (void**)&o->ProviderInput, install) ||
                !NativeWritablePlace(ref p, W.MaximumProcesses * (ulong)sizeof(W.ProcessSlot), o == null ? null : (void**)&o->Processes, install) ||
                !NativeWritablePlace(ref p, W.LockSlots * (ulong)sizeof(W.RangeLock), o == null ? null : (void**)&o->Locks, install) ||
                !NativeWritablePlace(ref p, W.LockScratchSlots * (ulong)sizeof(W.RangeLock), o == null ? null : (void**)&o->LockScratch, install)) return 0;
            C.Admission a = o == null ? default : o->EngineInput;
            if (!NativeWritablePlace(ref p, C.JournalChanges * (ulong)sizeof(Fat32Mutation.Change), (void**)&a.Changes, install || o == null) ||
                !NativeWritablePlace(ref p, C.JournalChanges * 512UL, (void**)&a.Before, install || o == null) ||
                !NativeWritablePlace(ref p, C.JournalChanges * 512UL, (void**)&a.After, install || o == null) ||
                !NativeWritablePlace(ref p, C.MaximumClusters * 4UL, (void**)&a.DirectoryMap, install || o == null) ||
                !NativeWritablePlace(ref p, C.MaximumClusters * 4UL, (void**)&a.FileMap, install || o == null) ||
                !NativeWritablePlace(ref p, C.MaximumClusters * 4UL, (void**)&a.WalkMap, install || o == null) ||
                !NativeWritablePlace(ref p, 512, (void**)&a.Sector, install || o == null) ||
                !NativeWritablePlace(ref p, 512, (void**)&a.Fat0, install || o == null) ||
                !NativeWritablePlace(ref p, 512, (void**)&a.Fat1, install || o == null) ||
                !NativeWritablePlace(ref p, C.WriteBytes, (void**)&a.Input, install || o == null) ||
                !NativeWritablePlace(ref p, C.NameWords * 2UL, (void**)&a.Name, install || o == null) ||
                !NativeWritablePlace(ref p, C.VfatWords * 2UL, (void**)&a.LongName, install || o == null) ||
                !NativeWritablePlace(ref p, C.EncodedEntryBytes, (void**)&a.EncodedEntries, install || o == null)) return 0;
            if (o != null && install) o->EngineInput = a;
            for (uint i = 0; i < NativeWritableSlots; i++)
            {
                Fs.Scratch scratch = o == null ? default : o->Operations[i].Storage;
                if (!NativeWritablePlace(ref p, Fs.PathBytes, (void**)&scratch.Path, install || o == null) ||
                    !NativeWritablePlace(ref p, 512, (void**)&scratch.Sector, install || o == null) ||
                    !NativeWritablePlace(ref p, 520, (void**)&scratch.Name, install || o == null) ||
                    !NativeWritablePlace(ref p, Fs.MaximumClusters * 4UL, (void**)&scratch.History, install || o == null) ||
                    !NativeWritablePlace(ref p, F.DiskCompletionBytes, (void**)&scratch.Range, install || o == null)) return 0;
                if (o != null && install)
                { scratch.HistoryCapacity = Fs.MaximumClusters; scratch.RangeCapacity = F.DiskCompletionBytes; o->Operations[i].Storage = scratch; }
            }
            for (uint i = 0; i < NativeWritableSlots; i++)
            {
                if (!NativeWritablePlace(ref p, Fs.MaximumClusters * 4UL, o == null ? null : (void**)&o->Files[i].Clusters, install)) return 0;
                if (o != null && install) o->Files[i].ClusterCapacity = Fs.MaximumClusters;
            }
            for (uint i = 0; i < NativeWritableCaches; i++)
                if (!NativeWritablePlace(ref p, 512, o == null ? null : (void**)&o->Cache[i].Data, install)) return 0;
            return p - start;
        }

        private static bool PrepareNativeWritableDiskBacking(void* context)
        {
            Storage* state = (Storage*)context;
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            {
                if (!NativeWritableOriginalBoot(state) || state->Phase != 2 || state->SystemTable == null || o->Phase != 0 ||
                    !M01StageOneAdapter.TryGetOriginalTypeManagerImage(out var image, out var imageMapping) ||
                    !M01StageTwoAdapter.TryGetOriginalExitAllocator(out _, out var allocator)) return false;
                SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
                if (!FirstExitImageMappings.WriteRanges(image, imageMapping, ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) ||
                    !FirstExitImageMappings.Contains(ranges, count, (ulong)o, (ulong)sizeof(NativeWritableOwner), PageProtection.Read | PageProtection.Write)) return false;
                uint* words = stackalloc uint[64]; PciSizedBar* bars = stackalloc PciSizedBar[6];
                if (!ReadNativeWritableDiskFixture(state, words, out var binding) ||
                    BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(words[8], words[9], 0xffffc00c, 0xffffffff, 4, out bars[4]) != PciBarProbeStatus.Success) return false;
                ulong blockBar = bars[4].Base;
                if (!ReadVirtioFixture(state, &ReadVirtioSnapshotWord, words, out _)) return false;
                ulong nicBar = ((ulong)words[9] << 32) | (words[8] & ~15u);
                if (!NativeWritableRange(blockBar, 0x4000, Page) || !NativeWritableRange(nicBar, 0x4000, Page) ||
                    !Disjoint(blockBar, 0x4000, nicBar, 0x4000)) return false;
                ulong used = NativeWritableLayout(null, 0, false), pages = (used + Page - 1) / Page, candidate = 0xffffffff;
                var services = state->SystemTable->BootServices;
                if (used == 0 || pages == 0 || pages > 256 || services == null || services->AllocatePages == null ||
                    services->AllocatePages(1, 2, pages, &candidate) != 0) return false;
                // Keep a successful firmware allocation even on later rejection.
                o->Boot = state; o->Arena = candidate; o->Pages = pages; o->Bar = blockBar; o->NicBar = nicBar; o->Phase = 1;
                if (!NativeWritableRange(candidate, pages * Page, Page) || candidate > 0x100000000UL - pages * Page ||
                    !Disjoint(candidate, pages * Page, o->Bar, 0x4000) ||
                    !Disjoint(candidate, pages * Page, (ulong)state, (ulong)sizeof(Storage)) ||
                    !Disjoint(candidate, pages * Page, allocator.Start, allocator.PageCount * Page) ||
                    !NativeWritableOutsideBoot(o)) return false;
                for (ulong i = 0; i < pages * Page; i++) ((byte*)candidate)[i] = 0;
                if (NativeWritableLayout(o, candidate, true) != used) return false;
                *o->Carrier = new() { Self = (ulong)o->Carrier, Owner = (ulong)o, Epoch = NativeWritableEpoch, Boot = state };
                return true;
            }
        }

        private static bool GetNativeWritableDiskBacking(Storage* state, out ulong arena, out ulong pages, out ulong bar)
        {
            arena = pages = bar = 0;
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            {
                if (o->Boot != state || o->Phase == 0 || !NativeWritableShape(o)) return false;
                arena = o->Arena; pages = o->Pages; bar = o->Bar; return true;
            }
        }

        private static bool StartNativeWritableDisk(Storage* state)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            {
                if (o->Boot != state || o->Phase != 1 || state->OwnedPhase != 2 || state->PciPhase != 8 || state->NetworkPhase != 2 ||
                    !AuditNativeWritableMappings(o)) return false;
                byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
                byte* fence = state->Memory.Code + FirstExitCodeBundle.QueueFenceOffset;
                if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) || !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes)) return false;
                var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
                var write = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
                var write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
                uint* words = stackalloc uint[64];
                if (!ReadNativeWritableDiskFixture(state, words, out var binding) || (((ulong)words[9] << 32) | (words[8] & ~15u)) != o->Bar ||
                    MSIMSIXSetupInterruptDrivenVirtqueueCompletion.Discover((byte*)words, 256, out o->Msix) != MsixStatus.Success || (o->Msix.Control & 0x8000) != 0) return false;
                o->Command = (uint)((binding.OriginalCommand & 0xfffb) | 0x400); o->Phase = 2;
                if (SetNativeWritableBusMaster(o, false) != 1 || !ResetVirtio(o->Bar)) return false;
                PciSizedBar* bars = stackalloc PciSizedBar[6];
                var probe = N32BitN64BitBARsWhileAlwaysRestoring.Probe(state->PciWindow, 0, 7, 0, null, read, write, write16, bars, 6);
                VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4]; var caps = binding.Capabilities;
                if (!probe.Succeeded || bars[4].Kind != PciSizedBarKind.Memory64 || bars[4].Base != o->Bar || bars[4].Bytes != 0x4000 ||
                    bars[5].Kind != PciSizedBarKind.UpperHalf ||
                    CommonNotifyISRDeviceConfigurationRegions.PlanBlock(caps, bars, 6, regions, 4) != VirtioRegionStatus.Success ||
                    CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], o->Bar, out o->Common) != VirtioRegionStatus.Success ||
                    CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], caps.NotifyConfiguration.Length, o->Bar + 0x3000, out o->Notify) != VirtioRegionStatus.Success ||
                    CommonNotifyISRDeviceConfigurationRegions.Bind(regions[3], o->Bar + 0x2000, out o->Configuration) != VirtioRegionStatus.Success) return false;
                o->NotifyMultiplier = caps.NotifyConfiguration.NotifyOffsetMultiplier;
                VirtioBlkReadAccess access = new();
                access.Queue.Context = o->Carrier; access.Queue.Read8 = &ReadVirtioStatus; access.Queue.Write8 = &WriteVirtioStatus;
                access.Queue.Read16 = &ReadQueueWord; access.Queue.Write16 = write16; access.Queue.Read32 = read; access.Queue.Write32 = write;
                access.Queue.Fence = (delegate* unmanaged<void*, byte>)fence;
                access.Queue.BusMasterOff = &NativeWritableBusMasterOff; access.BusMasterOn = &NativeWritableBusMasterOn;
                var negotiation = B.Negotiate(o->Common, 1024, access.Queue);
                if (!SampleNativeWritableDisk(state) || !negotiation.Succeeded || negotiation.Selected != B.Features || !NativeWritableCapacity(o, read, out o->Capacity) ||
                    o->Capacity != 262144 || DmaAddressWindow.TryCreate(o->Arena, o->Arena, o->Arena, Page, 32, out var window) != DmaAddressStatus.Success ||
                    PageAlignedDmaBuffers.TryInitialize(o->Pool, window, o->Slot, 1, NativeWritableEpoch) != DmaBufferStatus.Success) return false;
                var admission = new B.Admission { Common = o->Common, Notify = o->Notify, Device = o->Configuration,
                    NotifyMultiplier = o->NotifyMultiplier, ResetLimit = 1024, Pool = o->Pool, Epoch = NativeWritableEpoch,
                    Negotiation = negotiation, ExpectedCapacity = o->Capacity, CompletionTimeoutNanos = 2_000_000_000,
                    Access = access, Clock = new() { Context = o->Carrier, Read = &NativeWritableClock }, Observation = o->Observation };
                var started = B.Start(o->Device, admission);
                if (!SampleNativeWritableDisk(state) || started != B.Status.Success || !ReadNetworkTime(state, out ulong now) ||
                    now > (ulong)long.MaxValue - 120_000_000_000) return false;
                o->Deadline = now + 120_000_000_000;
                var bootstrap = K.Start(o->Bootstrap, new() { Device = o->Device, DeviceEpoch = NativeWritableEpoch,
                    Epoch = NativeWritableEpoch + 1, Sector = o->BootstrapSector, Entries = o->Entries, SectorBudget = K.MaximumReads });
                if (!SampleNativeWritableDisk(state) || bootstrap != K.Status.Pending) return false;
                o->BootstrapStarted = true; o->Phase = 3; return true;
            }
        }

        // One logical step, then sample immediately. The root owns the outer
        // cooperative loop, NIC service and actual deadline/worker admission.
        private static NativeWritableDiskProgress NativeWritableDiskBootstrapStep(Storage* state, uint actualPrimaryPid)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            {
                if (o->Boot != state || actualPrimaryPid == 0 || actualPrimaryPid > int.MaxValue || !NativeWritableShape(o)) return NativeWritableDiskProgress.Rejected;
                if (o->Phase == 6) return o->PrimaryPid == actualPrimaryPid ? NativeWritableDiskProgress.Ready : NativeWritableDiskProgress.Rejected;
                if (!ReadNetworkTime(state, out ulong now) || now >= o->Deadline) return NativeWritableDiskProgress.Rejected;
                if (o->Phase == 3)
                {
                    var progress = K.Pump(o->Bootstrap);
                    if (!SampleNativeWritableDisk(state)) return NativeWritableDiskProgress.Rejected;
                    if (progress == K.Status.Pending) return NativeWritableDiskProgress.Pending;
                    if (progress != K.Status.Ready || K.CopyResult(o->Bootstrap, NativeWritableEpoch + 1, o->Selected) != K.Status.Success ||
                        !SampleNativeWritableDisk(state) || !o->Evidence->HeaderCopied) return NativeWritableDiskProgress.Rejected;
                    var header = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(new Span<byte>(o->Header, 512), o->Capacity, BlockReadResult.Success(1, 512));
                    o->Esp = FAT32ApplicationEFIPartition.Select(header, new Span<byte>(o->Entries, (int)K.EntryBytes));
                    var data = o->Selected->Volume;
                    if (!header.Succeeded || !o->Esp.Succeeded || o->Esp.Attributes != 0 || o->Esp.FirstLba != 2048 || o->Esp.LastLba != 131038 ||
                        data.PartitionFirstLba != 131072 || data.PartitionLastLba != 260062 || o->Esp.LastLba >= data.PartitionFirstLba) return NativeWritableDiskProgress.Rejected;
                    C.Admission input = o->EngineInput; input.Device = o->Device; input.Owner = (ulong)o; input.Epoch = NativeWritableEpoch + 2;
                    input.PartitionFirst = data.PartitionFirstLba; input.PartitionLast = data.PartitionLastLba;
                    input.ChangeCapacity = C.JournalChanges; input.ChainCapacity = C.MaximumClusters; input.InputCapacity = C.WriteBytes; input.WorkBudget = 1000000;
                    o->EngineInput = input; var mounted = C.Start(o->Engine, input);
                    if (!SampleNativeWritableDisk(state) || mounted != C.Status.Pending) return NativeWritableDiskProgress.Rejected;
                    o->EngineStarted = true; o->Phase = 4; return NativeWritableDiskProgress.Pending;
                }
                if (o->Phase == 4)
                {
                    var progress = C.Pump(o->Engine);
                    if (!SampleNativeWritableDisk(state)) return NativeWritableDiskProgress.Rejected;
                    if (progress == C.Status.Pending) return NativeWritableDiskProgress.Pending;
                    if (progress != C.Status.Ready || !o->Engine->IsMounted || o->Engine->RequestActive || o->Engine->Dirty ||
                        P.Start(o->Port, new() { Engine = o->Engine, Device = o->Device, Epoch = NativeWritableEpoch + 3,
                            EngineEpoch = NativeWritableEpoch + 2, DeviceEpoch = NativeWritableEpoch, FirstLba = o->Esp.FirstLba, LastLba = o->Esp.LastLba }) != P.Status.Success ||
                        !SampleNativeWritableDisk(state)) return NativeWritableDiskProgress.Rejected;
                    o->PortStarted = true;
                    var start = Fs.StartBorrowed(o->Mount, new() { ReadPort = o->Port, PortEpoch = NativeWritableEpoch + 3, Epoch = NativeWritableEpoch + 4,
                        Operations = o->Operations, Files = o->Files, Cache = o->Cache, OperationCount = NativeWritableSlots,
                        FileCount = NativeWritableSlots, CacheCount = NativeWritableCaches, DirectoryEntryBudget = 4096 });
                    if (!SampleNativeWritableDisk(state) || start != Fs.Status.Pending) return NativeWritableDiskProgress.Rejected;
                    o->FsStarted = true; o->Phase = 5; return NativeWritableDiskProgress.Pending;
                }
                if (o->Phase != 5) return NativeWritableDiskProgress.Rejected;
                var fs = Fs.PumpMount(o->Mount);
                if (!SampleNativeWritableDisk(state)) return NativeWritableDiskProgress.Rejected;
                if (fs is Fs.Status.Pending or Fs.Status.Busy) return NativeWritableDiskProgress.Pending;
                if (fs != Fs.Status.Ready || !o->Mount->Ready ||
                    !W.Start(o->Provider, new() { Engine = o->Engine, Epoch = NativeWritableEpoch + 2, Descriptors = o->Fds,
                        Descriptions = o->Descriptions, Input = o->ProviderInput, DescriptorCount = W.MaximumDescriptors,
                        DescriptionCount = W.MaximumDescriptions, InputBytes = W.IoBytes, ExclusiveOwnerValidated = true }) ||
                    !SampleNativeWritableDisk(state)) return NativeWritableDiskProgress.Rejected;
                o->ProviderStarted = true;
                if (!W.EnableLocks(o->Provider, new() { Processes = o->Processes, ProcessCount = W.MaximumProcesses, Locks = o->Locks,
                        LockCount = W.LockSlots, Scratch = o->LockScratch, ScratchCount = W.LockScratchSlots, PrimaryPid = actualPrimaryPid }) ||
                    !SampleNativeWritableDisk(state)) return NativeWritableDiskProgress.Rejected;
                bool readAttached = F.AttachDisk(&state->LinuxDescriptors, o->ReadDescriptors, new() { Mount = o->Mount, Bindings = o->ReadBindings,
                    Requests = o->ReadRequests, Epoch = NativeWritableEpoch + 4, FirmwareExited = true, SingleOwnerValidated = true });
                o->ReadAttached = readAttached;
                if (!SampleNativeWritableDisk(state) || !readAttached) return NativeWritableDiskProgress.Rejected;
                bool writeAttached = F.AttachWritableDisk(&state->LinuxDescriptors, o->WriteDescriptors, new() { Provider = o->Provider, Process = W.PrimaryProcess(o->Provider),
                        Search = new(2, 4096, 8192), Bindings = o->WriteBindings, BindingCount = W.MaximumDescriptions,
                        Epoch = NativeWritableEpoch + 2, FirmwareExited = true, SingleOwnerValidated = true });
                o->WriteAttached = writeAttached;
                if (!SampleNativeWritableDisk(state) || !writeAttached) return NativeWritableDiskProgress.Rejected;
                o->Attached = true; o->PrimaryPid = actualPrimaryPid; o->Phase = 6;
                if (WriteNativeWritableBinding(o)) return NativeWritableDiskProgress.Ready;
                o->Evidence->Failed = true; o->Phase = 8; return NativeWritableDiskProgress.Rejected;
            }
        }

        // Global/source4/source5 pumps own queued requests after attachment.
        // This helper only drives the private startup, never tokenless B.Poll.
        private static bool PumpNativeWritableDisk(Storage* state) => SampleNativeWritableDisk(state);

        // Root calls one turn at a time after genuine exits/reaps, before
        // Release. Root cancellation/exit hooks revoke live user-copy first.
        private static NativeWritableDiskProgress QuiesceNativeWritableDiskStep(Storage* state, LinuxNativeControl* c)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            {
                if (o->Boot != state || !o->Attached || !NativeWritableShape(o) || !NativeWritableControl(state, c) ||
                    c->Process.ActiveThreads != 0 || c->Threads.Current.IsValid || c->Threads.HandoffPending) return NativeWritableDiskProgress.Rejected;
                var read = F.PumpDiskOne(&state->LinuxDescriptors, (ulong)(&c->Process));
                if (!SampleNativeWritableDisk(state) || read.Progress == F.DiskProgress.Rejected) return NativeWritableDiskProgress.Rejected;
                var write = F.PumpWritableDiskOne(&state->LinuxDescriptors, (ulong)(&c->Process));
                if (!SampleNativeWritableDisk(state) || write.Progress is F.WritableDiskProgress.Rejected or F.WritableDiskProgress.RecoveryRequired || o->Engine->Dirty || o->Device->Dirty) return NativeWritableDiskProgress.Rejected;
                for (uint i = 0; i < NativeWritableSlots; i++) if (o->ReadRequests[i].Phase != 0) return NativeWritableDiskProgress.Pending;
                for (uint i = 0; i < c->Capacity; i++)
                    if (c->Slots[i].DiskOperation.IsActive || c->Slots[i].WritableDiskOperation.IsActive ||
                        c->Slots[i].DiskOutputBytes != 0 || c->Slots[i].WritableDiskOutputBytes != 0 ||
                        c->Slots[i].WaitSource is 4 or 5 || c->Slots[i].Wait.IsValid) return NativeWritableDiskProgress.Pending;
                return !o->WriteDescriptors->RequestActive && !o->Provider->RequestActive && !o->Engine->RequestActive &&
                    !o->Port->RequestActive && !o->Device->RequestPending && !o->Mount->IoActive
                    ? NativeWritableDiskProgress.Ready : NativeWritableDiskProgress.Pending;
            }
        }

        // AFTER root Process.Release/network/global F.End, BEFORE arena scrub.
        // No F.End, network shutdown, retry or dirty-reset shortcut is owned here.
        private static bool RetireNativeWritableDiskAttachment(Storage* state, LinuxNativeControl* c)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            {
                if (o->Boot != state || !NativeWritableShape(o) || c != null && (!NativeWritableControl(state, c) || c->Process.ActiveThreads != 0 || c->Threads.Current.IsValid || c->Threads.HandoffPending) ||
                    (o->ReadAttached || o->WriteAttached) && (state->LinuxDescriptorPhase != 6 || state->LinuxDescriptors.WritableDisk != null || state->LinuxDescriptors.Disk != null) ||
                    !SampleNativeWritableDisk(state)) return false;
                if (o->Phase == 7) return true;
                if (o->EngineStarted && o->Engine->Dirty || o->Device->Dirty) { o->Phase = 8; return false; }
                if (o->ProviderStarted)
                {
                    var result = W.Detach(o->Provider);
                    if (!SampleNativeWritableDisk(state) || result.Phase != W.Progress.Retired) return false;
                }
                if (o->FsStarted)
                { var result = Fs.Stop(o->Mount); if (!SampleNativeWritableDisk(state) || result != Fs.Status.Success) return false; }
                if (o->PortStarted)
                { var result = P.Stop(o->Port); if (!SampleNativeWritableDisk(state) || result != P.Status.Success) return false; }
                if (o->EngineStarted)
                { var result = C.Stop(o->Engine); if (!SampleNativeWritableDisk(state) || result != C.Status.Success) return false; }
                if (o->BootstrapStarted)
                { var result = K.Stop(o->Bootstrap); if (!SampleNativeWritableDisk(state) || result != K.Status.Success) return false; }
                if (o->Device->Self == 0 && o->Phase >= 2 && (SetNativeWritableBusMaster(o, false) != 1 || !ResetVirtio(o->Bar))) return false;
                var stopped = o->Device->RequiresCleanup ? B.Stop(o->Device) : B.Status.Success;
                if (!SampleNativeWritableDisk(state) || stopped != B.Status.Success || o->Device->RequiresCleanup || o->Pool->AllocatedMask != 0) return false;
                o->Phase = 7;
                if (WriteNativeWritableRetired(o)) return true;
                o->Evidence->Failed = true; return false;
            }
        }
        private static bool NativeWritableDiskAllowsOuterTeardown(Storage* state)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
                return o->Boot == state && NativeWritableShape(o) && o->Phase == 7 && !o->Device->RequiresCleanup &&
                    !o->Device->Dirty && o->Pool->AllocatedMask == 0 && !o->Evidence->Failed &&
                    (!o->ProviderStarted || o->Provider->Retired) && (!o->FsStarted || o->Mount->IsRetired) &&
                    (!o->PortStarted || o->Port->Retired) && (!o->EngineStarted || o->Engine->Retired) &&
                    (!o->BootstrapStarted || o->Bootstrap->Retired);
        }

        private static bool NativeWritableRange(ulong start, ulong bytes, ulong alignment) => start >= Page && start < 0x0000800000000000UL &&
            bytes != 0 && bytes <= 0x0000800000000000UL - start && (start & (alignment - 1)) == 0;
        private static bool NativeWritableOriginalBoot(Storage* state)
        { fixed (Storage* original = &s_storage) return state == original; }
        private static bool NativeWritableOriginalOwner(NativeWritableOwner* owner)
        { fixed (NativeWritableOwner* original = &s_nativeWritable) return owner == original; }
        private static bool NativeWritableShape(NativeWritableOwner* o) => NativeWritableOriginalOwner(o) && NativeWritableOriginalBoot(o->Boot) && o->Phase != 0 &&
            o->Pages is > 0 and <= 256 && NativeWritableRange(o->Arena, o->Pages * Page, Page) && NativeWritableRange(o->Bar, 0x4000, Page) &&
            Disjoint(o->Arena, o->Pages * Page, o->Bar, 0x4000) &&
            (NativeWritableLayout(null, 0, false) + Page - 1) / Page == o->Pages &&
            NativeWritableLayout(o, o->Arena, false) != 0 && o->Carrier->Self == (ulong)o->Carrier &&
            o->Carrier->Owner == (ulong)o && o->Carrier->Epoch == NativeWritableEpoch && o->Carrier->Boot == o->Boot &&
            (o->Phase == 1 || o->Boot->Memory.References.NativeDiskArenaStart == o->Arena &&
                o->Boot->Memory.References.NativeDiskArenaPages == o->Pages && o->Boot->Memory.References.NativeDiskBarStart == o->Bar) &&
            NativeWritableOriginalDevice(o);

        private static bool NativeWritableOriginalDevice(NativeWritableOwner* o)
        {
            B.State* device = o->Device;
            if (device->Self == 0) return device->Submitted == 0 && device->Completed == 0;
            return device->Self == (ulong)device && device->Input.Epoch == NativeWritableEpoch && device->Input.Pool == o->Pool &&
                device->PoolEpoch == NativeWritableEpoch && device->Input.Observation == o->Observation &&
                device->Input.Access.Queue.Context == o->Carrier && device->Input.Clock.Context == o->Carrier &&
                o->Common.VirtualStart == o->Bar && o->Common.PhysicalStart == o->Bar && o->Common.Bytes >= 56 && o->Common.Bytes <= Page &&
                o->Notify.VirtualStart == o->Bar + 0x3000 && o->Notify.PhysicalStart == o->Bar + 0x3000 && o->Notify.Bytes <= Page &&
                o->Configuration.VirtualStart == o->Bar + 0x2000 && o->Configuration.PhysicalStart == o->Bar + 0x2000 && o->Configuration.Bytes >= 8 && o->Configuration.Bytes <= Page &&
                device->Input.Common.VirtualStart == o->Common.VirtualStart && device->Input.Common.Bytes == o->Common.Bytes &&
                device->Input.Notify.VirtualStart == o->Notify.VirtualStart && device->Input.Notify.Bytes == o->Notify.Bytes &&
                device->Input.Device.VirtualStart == o->Configuration.VirtualStart && device->Input.Device.Bytes == o->Configuration.Bytes &&
                o->Pool->Self == (ulong)o->Pool && o->Pool->Epoch == NativeWritableEpoch && o->Pool->Slots == o->Slot && o->Pool->Count == 1 &&
                o->Pool->Window.CpuVirtualBase == o->Arena && o->Pool->Window.PhysicalBase == o->Arena &&
                o->Pool->Window.DeviceBase == o->Arena && o->Pool->Window.ByteLength == Page &&
                B.CanBorrowStorage(device, NativeWritableEpoch, (ulong)o->Carrier, (ulong)sizeof(NativeWritableCarrier)) &&
                B.CanBorrowStorage(device, NativeWritableEpoch, (ulong)o->Evidence, (ulong)sizeof(NativeWritableEvidence));
        }
        private static bool NativeWritableControl(Storage* state, LinuxNativeControl* c) =>
            NativeWritableRange(state->CooperativePool, CooperativePoolPages * Page, Page) && c != null &&
            (ulong)c == state->CooperativePool + LinuxThreadProgram.MetadataPage * Page &&
            c->State == state && c->Key == (state->CooperativePool ^ 0x4d31363030384f57UL) && c->Capacity is > 0 and <= LinuxNativeWorkers &&
            NativeWritableRange((ulong)c->Slots, c->Capacity * (ulong)sizeof(LinuxThreadServices.Slot), 8) &&
            (ulong)c->Slots >= state->CooperativePool &&
            (ulong)c->Slots - state->CooperativePool <= CooperativePoolPages * Page - c->Capacity * (ulong)sizeof(LinuxThreadServices.Slot);

        private static bool NativeWritableOutsideBoot(NativeWritableOwner* o)
        {
            Storage* s = o->Boot; ulong bytes = o->Pages * Page;
            if (!M01StageOneAdapter.TryGetOriginalTypeManagerImage(out var image, out _) ||
                image.Range.ImageBase.TryGetRawValueForUefiBoundary(out ulong imageStart) != AddressValueStatus.Success) return false;
            ulong* starts = stackalloc ulong[13] { imageStart, s->NetworkArena, s->CooperativePool, s->LinuxPackage, s->LinuxImageBacking,
                s->LinuxUserTables, s->LinuxSyscallCode, s->LinuxSyscallData, s->LinuxMemoryPool, s->LinuxMemoryCandidate,
                s->LinuxDescriptorArena, o->NicBar, (ulong)o };
            ulong* sizes = stackalloc ulong[13] { image.Range.ImageSize, 8 * Page, CooperativePoolPages * Page, s->LinuxPages * Page,
                s->LinuxImagePages * Page, X64UserAddressSpace.MaximumTables * Page, Page, X64LinuxUserTransition.DataBytes,
                LinuxMemoryPoolPages(s) * Page, X64UserAddressSpace.MaximumTables * Page, LinuxDescriptorPages * Page, 0x4000, (ulong)sizeof(NativeWritableOwner) };
            for (uint i = 0; i < 13; i++) if (sizes[i] != 0 && (!NativeWritableRange(starts[i], sizes[i], 1) || !Disjoint(o->Arena, bytes, starts[i], sizes[i]))) return false;
            if (!Disjoint(o->Arena, bytes, s->PciWindow.Start, 0x100000) ||
                !Disjoint(o->Arena, bytes, s->HpetDescription.Address & ~4095UL, Page)) return false;
            return true;
        }
        private static bool AuditNativeWritableMappings(NativeWritableOwner* o)
        {
            Storage* s = o->Boot;
            if (!NativeWritableShape(o) || !NativeWritableOutsideBoot(o) || s->Memory.References.NativeDiskArenaStart != o->Arena ||
                s->Memory.References.NativeDiskArenaPages != o->Pages || s->Memory.References.NativeDiskBarStart != o->Bar ||
                s->Memory.References.VirtioBarStart != o->NicBar ||
                !Disjoint(o->Arena, o->Pages * Page, s->Memory.References.LocalApicPage, Page) ||
                !Disjoint(o->Arena, o->Pages * Page, s->Memory.References.IoApicPage, Page)) return false;
            SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
            return FirstExitRetainedMappings.WriteInstalledRanges(ref s->Arena, s->Memory.Code, s->Memory.Capture, s->Memory.Mapping,
                    s->Memory.References, s->Memory.Allocator, s->Memory.Tables, ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) &&
                FirstExitImageMappings.Contains(ranges, count, o->Arena, o->Pages * Page, PageProtection.Read | PageProtection.Write) &&
                SeparateDisabledDefaultPageFaultPageTables.AuditRetainedAccessedImage(s->Memory.Tables, ranges, count, FirstExitRetainedMappings.MaximumRanges,
                    s->Memory.Pages, s->Memory.PageCapacity, s->Memory.Entries, s->Memory.EntryCapacity).Succeeded;
        }
        private static bool AuditNativeWritableDiskMappings(Storage* state)
        { fixed (NativeWritableOwner* o = &s_nativeWritable) return o->Boot == state && AuditNativeWritableMappings(o); }
        private static bool ReadNativeWritableDiskFixture(Storage* state, uint* words, out VirtioBlkPciBinding binding)
        {
            binding = default;
            if (state->PciWindow.FirstBus != 0 || state->PciWindow.LastBus != 0 ||
                !BoundedPCIConfigurationSpaceInterface.TryOpen(state->PciWindow, null, &ReadVirtioSnapshotWord, out var access)) return false;
            var snapshot = ECAMPageVendorDeviceHeader.ReadSnapshot(ref access, 0, 7, 0, words, 64);
            BoundedPCIConfigurationSpaceInterface.Revoke(ref access);
            if (snapshot != BoundedPciStatus.Success || (words[1] & 2) == 0 ||
                LiveVirtioBlkPCIDevice.Parse((byte*)words, 256, 0, 7, 0, out binding) != VirtioBlkBindingStatus.Success) return false;
            var caps = binding.Capabilities;
            return VirtioFixtureCapability(caps.CommonConfiguration, 0) && VirtioFixtureCapability(caps.NotifyConfiguration, 0x3000) &&
                VirtioFixtureCapability(caps.IsrStatus, 0x1000) && VirtioFixtureCapability(caps.DeviceConfiguration, 0x2000);
        }
        private static bool NativeWritableCapacity(NativeWritableOwner* o, delegate* unmanaged<void*, ulong, uint*, byte> read, out ulong capacity)
        {
            capacity = 0;
            for (uint i = 0; i < 16; i++)
            {
                byte before = *(byte*)(o->Common.VirtualStart + 21); uint low = 0, high = 0;
                if (read(null, o->Configuration.VirtualStart, &low) != 1 || read(null, o->Configuration.VirtualStart + 4, &high) != 1) return false;
                if (before != *(byte*)(o->Common.VirtualStart + 21)) continue;
                capacity = ((ulong)high << 32) | low; return capacity >= 68 && capacity <= (ulong)long.MaxValue / 512;
            }
            return false;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NativeWritableClock(void* context, ulong* time)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
            {
                if (time == null || context != o->Carrier || !NativeWritableShape(o) || !ReadNetworkTime(o->Boot, out ulong now)) return 0;
                *time = now; return 1;
            }
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NativeWritableBusMasterOn(void* context) => NativeWritableBusMaster(context, true);
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NativeWritableBusMasterOff(void* context) => NativeWritableBusMaster(context, false);
        private static byte NativeWritableBusMaster(void* context, bool enabled)
        {
            fixed (NativeWritableOwner* o = &s_nativeWritable)
                return context == o->Carrier && NativeWritableShape(o) ? SetNativeWritableBusMaster(o, enabled) : (byte)0;
        }
        private static byte SetNativeWritableBusMaster(NativeWritableOwner* o, bool enabled)
        {
            if (!NativeWritableShape(o) || o->Phase < 2 || (o->Command & 0x406) != 0x402 || !o->Msix.IsValid) return 0;
            byte* code = o->Boot->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes)) return 0;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            ulong configuration = o->Boot->PciWindow.Start + 0x38000; uint control = 0, command = 0, wanted = enabled ? o->Command | 4 : o->Command;
            return read(null, configuration + o->Msix.CapabilityOffset, &control) == 1 && (control >> 16) == o->Msix.Control &&
                (control & 0x80000000) == 0 && write(null, configuration + 4, wanted) == 1 &&
                read(null, configuration + 4, &command) == 1 && (command & 0xffff) == wanted ? (byte)1 : (byte)0;
        }
    }
}
#endif
