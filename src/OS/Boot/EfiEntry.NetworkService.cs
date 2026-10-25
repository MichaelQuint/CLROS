#if M07_026_PROBE
using OS.Drivers;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Net;
using NetDevice = OS.Drivers.ContinuingVirtioNetDevice;

#if !M07_ARP_RECEIVE_PROBE || !M16_003_PROBE
#error Continuing networking requires the original ARP and service-loop parents.
#endif

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        private struct NetworkOwnerStorage
        {
            internal NetDevice.State Device;
            internal DmaBufferPool Pool;
            internal fixed ulong Metadata[256];
        }
        // Image-resident and never recycled. Failed reset may outlive this call;
        // every driver metadata pointer remains mapped until explicit cleanup.
        private static NetworkOwnerStorage s_networkOwner;

        private static bool PrepareNetworkArena(void* context)
        {
            Storage* state = (Storage*)context;
            if (state == null || state->Phase != 2 || state->NetworkArena != 0 || state->SystemTable == null ||
                !M01StageOneAdapter.TryGetOriginalTypeManagerImage(out var capture, out var mapping) ||
                capture.Range.ImageBase.TryGetRawValueForUefiBoundary(out ulong image) != AddressValueStatus.Success ||
                !M01StageTwoAdapter.TryGetOriginalExitAllocator(out _, out var allocator)) return false;
            EfiBootServices* services = state->SystemTable->BootServices;
            if (services == null || services->AllocatePages == null) return false;
            fixed (NetworkOwnerStorage* owner = &s_networkOwner)
            fixed (NetworkTcpStorage* tcp = &s_networkTcp)
            {
                SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
                if (!FirstExitImageMappings.WriteRanges(capture, mapping, ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) ||
                    !FirstExitImageMappings.Contains(ranges, count, (ulong)owner, (ulong)sizeof(NetworkOwnerStorage), PageProtection.Read | PageProtection.Write) ||
                    !FirstExitImageMappings.Contains(ranges, count, (ulong)tcp, (ulong)sizeof(NetworkTcpStorage), PageProtection.Read | PageProtection.Write)) return false;
#if CLROS_LINUX_SOCKETS
                if (!LinuxSocketImageMapped(ranges, count)) return false;
#endif
                ulong candidate = 0xffffffff;
                if (services->AllocatePages(1, 2, 8, &candidate) != 0) return false;
                state->NetworkArena = candidate; // Retain even a rejected successful firmware allocation.
                if (candidate < Page || (candidate & 4095) != 0 || candidate > 0x100000000UL - 32768 ||
                    !Disjoint(candidate, 32768, image, capture.Range.ImageSize) ||
                    !Disjoint(candidate, 32768, allocator.Start, allocator.PageCount * Page) ||
                    state->LinuxPackage == 0 || !Disjoint(candidate, 32768, state->LinuxPackage, state->LinuxPages * Page)) return false;
                for (uint i = 0; i < 32768; i++) ((byte*)candidate)[i] = 0;
                for (uint i = 0; i < sizeof(NetworkOwnerStorage); i++) ((byte*)owner)[i] = 0;
            }
            return true;
        }

        private static bool RunOwnedNetworkService(void* context)
        {
            Storage* state = (Storage*)context;
            if (state == null || state->NetworkPhase != 0 || state->ArpPhase != 3 ||
                state->ServicePhase != 2 || state->OwnedPhase != 2 ||
                state->AdapterConsole != null || state->AdapterServices != null ||
                !FirstExitOwnedArena.ValidateCanaries(state->Arena)) return false;
            state->NetworkPhase = 1;
            if (!LinuxText(state, "TEST_START m07-026\r\n") ||
#if CLROS_LINUX_SOCKETS
                !RunNetworkServiceCore(state))
#else
                !RunNetworkScheduledService(state))
#endif
            { _ = LinuxText(state, "TEST_FAIL m07-026 service\r\n"); return false; }
#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE
            return LinuxWebRetainFailure(state, null, 7);
#else
#if M16_014_PROBE
            if (state->LinuxQuota != LinuxQuotaDisposition.None)
            {
                if (!LinuxQuotaPolicy(state) || state->LinuxQuota != LinuxQuotaDisposition.NetworkCleaned ||
                    state->LinuxSyscallExitCode != 99 || state->LinuxSyscallFailed != 0 || state->LinuxSyscallFaults != 0 ||
                    state->CooperativePhase != 6 || state->LinuxMemoryPhase != 6 || state->LinuxDescriptorPhase != 6)
                    return false;
                state->NetworkPhase = 3;
                if (!LinuxText(state, "M16_016_QUOTA_CLEANUP exit=99 process=failed workers=reaped memory=zero cpu=restored nic=retired dma=zero allocator=unchanged pci=restored canaries=valid reservation=retained\r\nCLROS_M16_016_QUOTA_CLEANUP\r\n")) return false;
                return false; // Stop before all ordinary leaf, boot and smoke success markers.
            }
#endif
            state->NetworkPhase = 3;
#if M16_027_PROBE
            if (!WriteNativeWritableDiskCompletion(state)) return false;
#elif M16_025_PROBE
            if (!WriteNativeDiskCompletion(state)) return false;
#endif
#if M10_017_PROBE || M10_018_PROBE
            if (!LinuxText(state, LinuxStaticFinish)) return false;
#endif
            return LinuxText(state, "CLROS_M07_026_READY\r\nTEST_PASS m07-026\r\n")
#if CLROS_LINUX_SOCKETS
                && LinuxText(state, "CLROS_M16_011_READY\r\nTEST_PASS m16-011\r\n")
#if M16_012_PROBE
                && LinuxText(state, "CLROS_M16_012_READY\r\nTEST_PASS m16-012\r\n")
#endif
#if M16_013_PROBE
                && LinuxText(state, "CLROS_M16_013_READY\r\nTEST_PASS m16-013\r\n")
#endif
#if M07_027_PROBE && M07_028_PROBE
                && LinuxText(state, "CLROS_M07_028_READY\r\nTEST_PASS m07.028\r\nCLROS_M07_027_READY\r\nTEST_PASS m07.027\r\n")
#endif
#endif
                ;
#endif
        }

        private static bool RunNetworkServiceCore(Storage* state)
        {
            byte* code = state->Memory.Code + FirstExitCodeBundle.PciBarOffset;
            byte* fence = state->Memory.Code + FirstExitCodeBundle.QueueFenceOffset;
            if (!X64PciBarAccess.Validate(code, X64PciBarAccess.Bytes) ||
                !X64VirtqueueMemoryFence.Validate(fence, X64VirtqueueMemoryFence.Bytes)) return false;
            var read = (delegate* unmanaged<void*, ulong, uint*, byte>)code;
            var write32 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 16);
            var write16 = (delegate* unmanaged<void*, ulong, uint, byte>)(code + 32);
            uint* words = stackalloc uint[64];
            if (!ReadVirtioFixture(state, &ReadVirtioSnapshotWord, words, out var priorCaps) ||
                LiveVirtioNetPCIDevice.Parse((byte*)words, 256, 0, 6, 0, out var binding) != VirtioNetBindingStatus.Success ||
                binding.OriginalCommand != state->QueueCommand || (binding.OriginalCommand & 0x406) != 0x402) return false;
            var caps = binding.Capabilities; ulong start = state->Memory.References.VirtioBarStart;
            PciSizedBar* bars = stackalloc PciSizedBar[6]; for (uint i = 0; i < 6; i++) bars[i] = default;
            VirtioConfigurationRegion* regions = stackalloc VirtioConfigurationRegion[4];
            if (BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(words[8], words[9], 0xffffc00c, 0xffffffff, 4, out bars[4]) != PciBarProbeStatus.Success ||
                bars[4].Base != start || bars[4].Bytes != 0x4000 || caps.CommonConfiguration.Offset != priorCaps.CommonConfiguration.Offset ||
                caps.NotifyConfiguration.Offset != priorCaps.NotifyConfiguration.Offset ||
                CommonNotifyISRDeviceConfigurationRegions.Plan(caps, bars, 6, regions, 4) != VirtioRegionStatus.Success ||
                CommonNotifyISRDeviceConfigurationRegions.Bind(regions[0], start, out var common) != VirtioRegionStatus.Success ||
                CommonNotifyISRDeviceConfigurationRegions.BindNotificationPage(regions[1], caps.NotifyConfiguration.Length,
                    start + 0x3000, out var notify) != VirtioRegionStatus.Success) return false;
            if (!M01StageTwoAdapter.TryGetOriginalExitAllocator(out ulong address, out var backing) ||
                address != state->Memory.References.AllocatorState || backing.Start != state->Memory.Allocator.Start ||
                backing.PageCount != 2 || backing.PageCount != state->Memory.Allocator.PageCount ||
                state->NetworkArena < Page || state->Memory.References.NetworkArenaStart != state->NetworkArena ||
                state->Memory.References.NetworkArenaPages != 8 || state->LinuxLedgerCount != LinuxLedgerCapacity) return false;
            M01LiveAllocatorStaticState* original = (M01LiveAllocatorStaticState*)address;
            if (!original->IsCompleted) return false;
            var before = original->AllocatorStateValue; var beforeNode = original->FreeNodeValue;
            ulong page = state->NetworkArena;
            fixed (NetworkOwnerStorage* owner = &s_networkOwner)
            {
                if ((sizeof(DmaBufferSlot) & 7) != 0 || (sizeof(VirtqueueDescriptorSlot) & 7) != 0 ||
                    8 * sizeof(DmaBufferSlot) + 16 * sizeof(VirtqueueDescriptorSlot) + 16 * sizeof(VirtqueueDescriptorToken) > 2048) return false;
                DmaBufferSlot* poolSlots = (DmaBufferSlot*)owner->Metadata;
                VirtqueueDescriptorSlot* slots = (VirtqueueDescriptorSlot*)(poolSlots + 8);
                VirtqueueDescriptorToken* tokens = (VirtqueueDescriptorToken*)(slots + 16);
                owner->Device = default; owner->Pool = default;
                NetDevice.State* device = &owner->Device; DmaBufferPool* pool = &owner->Pool;
                if (DmaAddressWindow.TryCreate(page, page, page, ContinuingVirtioNet.BackingBytes, 32, out var window) != DmaAddressStatus.Success ||
                    PageAlignedDmaBuffers.TryInitialize(pool, window, poolSlots, 8, 226) != DmaBufferStatus.Success) return false;
                var negotiated = ResetACKNOWLEDGEDRIVERFEATURESOKNegotiation.RunNetReceive(common, 1024, null, read, write32,
                    &ReadVirtioStatus, &WriteVirtioStatus);
                if (!negotiated.Succeeded || negotiated.Selected != BoundedVirtioFeatures.NetSingleBufferReceive) return false;
                VirtqueuePollingAccess access = new(); access.Context = state;
                access.Read8 = &ReadVirtioStatus; access.Write8 = &WriteVirtioStatus;
                access.Read16 = &ReadQueueWord; access.Write16 = write16; access.Read32 = read; access.Write32 = write32;
                access.Fence = (delegate* unmanaged<void*, byte>)fence; access.BusMaster = &QueueBusMaster;
                var admission = new NetDevice.Admission
                {
                    Common = common, Notify = notify, NotifyMultiplier = caps.NotifyConfiguration.NotifyOffsetMultiplier,
                    Pool = pool, Slots = slots, Tokens = tokens, SlotCount = 16, TokenCount = 16, Epoch = 226,
                    NegotiatedFeatures = negotiated.Selected, CompletionTimeoutNanos = 2000000000, ResetLimit = 1024,
                    Mmio = access, Clock = new NetDevice.ClockAccess { Context = state, Read = &ReadNetworkNanos }
                };
                state->NetworkPhase = 2;
                bool success = NetDevice.Start(device, admission) == NetDevice.Status.Success;
                success = success && WriteNetworkRow(state, 0, page, ContinuingVirtioNet.BackingBytes, negotiated.Selected, 8) &&
                    RunNetworkExchanges(state, device);
#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE
                return LinuxWebRetainFailure(state, null, success ? 5U : 6U);
#else
#if M16_027_PROBE
                if (!NativeWritableDiskAllowsOuterTeardown(state)) return false;
#elif M16_025_PROBE
                // No NIC/DMA teardown may follow an uncertain disk stop. Disk
                // backing and callbacks outlive pre-user bootstrap, including
                // failed Linux metadata publication and rejected preparation.
                if (!NativeDiskAllowsOuterTeardown(state)) return false;
#endif
                bool safe = !device->RequiresCleanup || NetDevice.Stop(device) == NetDevice.Status.Success;
                safe &= device->Retired && pool->AllocatedMask == 0;
                if (safe) for (uint i = 0; i < ContinuingVirtioNet.BackingBytes; i++) if (((byte*)page)[i] != 0) safe = false;
                if (!safe) return false; // All owner metadata and the arena remain image-resident/retained.
                var after = original->AllocatorStateValue; var afterNode = original->FreeNodeValue; uint command = 0;
#if M16_014_PROBE
                if (state->LinuxQuota != LinuxQuotaDisposition.None)
                {
                    // These actual audits and their original evidence rows all
                    // execute after NIC reset/scrub, including a failed child.
                    bool allocator = NetworkAllocatorEqual(before, after, beforeNode, afterNode);
                    bool pci = read(null, state->PciWindow.Start + 0x30004, &command) == 1;
                    pci &= (command & 0xffff) == state->QueueCommand;
                    bool canaries = FirstExitOwnedArena.ValidateCanaries(state->Arena);
                    bool row4 = WriteNetworkRow(state, 4, device->ReceivedFrames, device->ReclaimedTransmits, device->Receipt.ResetReads, device->Receipt.Fences);
                    bool row5 = WriteNetworkRow(state, 5, pool->Count, pool->AllocatedMask, 8, 1);
                    bool row6 = WriteNetworkRow(state, 6, before.FreePageCount, after.FreePageCount, after.AllocatedPageCount, command & 0xffff);
                    if (!(success & allocator & pci & canaries & row4 & row5 & row6) || !LinuxQuotaPolicy(state) ||
                        state->LinuxQuota != LinuxQuotaDisposition.CpuRestored) return false;
                    state->LinuxQuota = LinuxQuotaDisposition.NetworkCleaned; return true;
                }
#endif
                return success && NetworkAllocatorEqual(before, after, beforeNode, afterNode) &&
                    read(null, state->PciWindow.Start + 0x30004, &command) == 1 && (command & 0xffff) == state->QueueCommand &&
                    FirstExitOwnedArena.ValidateCanaries(state->Arena) &&
                    WriteNetworkRow(state, 4, device->ReceivedFrames, device->ReclaimedTransmits, device->Receipt.ResetReads, device->Receipt.Fences) &&
                    WriteNetworkRow(state, 5, pool->Count, pool->AllocatedMask, 8, 1) &&
                    WriteNetworkRow(state, 6, before.FreePageCount, after.FreePageCount, after.AllocatedPageCount, command & 0xffff);
#endif
            }
        }

        private static bool RunNetworkExchanges(Storage* state, NetDevice.State* device)
        {
#if CLROS_LINUX_SOCKETS
            return RunLinuxSocketNetwork(state, device);
#else
            Span<byte> tx = stackalloc byte[1514]; Span<byte> rx = stackalloc byte[1514];
            var localMac = new MacAddress(2, 0, 0, 0, 0, 1);
            var local = new Ipv4Address(10, 0, 2, 15); var peer = new Ipv4Address(10, 0, 2, 2);
            var broadcast = new MacAddress(255, 255, 255, 255, 255, 255);
            tx.Clear();
            var arp = new ArpPacket(ArpPacket.OperationRequest, localMac, local, default, peer);
            if (EthernetHeader.TryWrite(tx, new EthernetHeader(broadcast, localMac, EthernetHeader.EtherTypeArp)) != PacketParseStatus.Success ||
                ArpPacket.TryWrite(tx.Slice(14), arp, out int arpBytes) != PacketParseStatus.Success || arpBytes != 28) return false;
            for (int i = 0; i < 8; i++) if (NetDevice.Send(device, tx.Slice(0, 60)) != NetDevice.Status.Success) return false;
            if (NetDevice.Send(device, tx.Slice(0, 60)) != NetDevice.Status.Full ||
                !WriteNetworkRow(state, 1, 8, 8, 1, 0)) return false;
            MacAddress resolved = default;
            for (uint i = 0; i < 8; i++)
            {
                if (!WaitNetworkFrame(state, device, rx, out uint length) ||
                    !NetworkArpReply(rx.Slice(0, (int)length), localMac, local, peer, out var mac)) return false;
                if (i != 0 && mac != resolved) return false; resolved = mac;
            }
            for (uint i = 0; i < 12; i++)
            {
                if (NetDevice.Send(device, tx.Slice(0, 60)) != NetDevice.Status.Success ||
                    !WaitNetworkFrame(state, device, rx, out uint length) ||
                    !NetworkArpReply(rx.Slice(0, (int)length), localMac, local, peer, out var mac) || mac != resolved) return false;
            }
            if (!WriteNetworkRow(state, 2, 20, device->ReceivedFrames, device->ReclaimedTransmits, PackNetworkMac(resolved))) return false;
            const string tag = "CLROS continuing network";
            Span<byte> payload = stackalloc byte[tag.Length];
            for (int i = 0; i < tag.Length; i++) payload[i] = (byte)tag[i];
            var info = new Ipv4IcmpEchoFrameInfo(resolved, localMac, local, peer);
            for (ushort i = 0; i < 12; i++)
            {
                tx.Clear();
                if (Ipv4IcmpEchoFrame.TryWriteEchoRequest(tx, info, 226, i, payload, out int bytes) != PacketParseStatus.Success ||
                    NetDevice.Send(device, tx.Slice(0, bytes < 60 ? 60 : bytes)) != NetDevice.Status.Success ||
                    !WaitNetworkFrame(state, device, rx, out uint length) ||
                    Ipv4IcmpEchoFrame.TryParse(rx.Slice(0, (int)length), out var replyInfo, out var echo, out var actual) != PacketParseStatus.Success ||
                    !echo.IsEchoReply || echo.Identifier != 226 || echo.SequenceNumber != i ||
                    replyInfo.SourceAddress != peer || replyInfo.DestinationAddress != local ||
                    replyInfo.SourceMac != resolved || replyInfo.DestinationMac != localMac || actual.Length != payload.Length) return false;
                for (int j = 0; j < payload.Length; j++) if (actual[j] != payload[j]) return false;
            }
            if (!ReadNetworkTime(state, out ulong now) || now > ~0UL - 2000000000) return false;
            ulong deadline = now + 2000000000;
            for (uint poll = 0; poll < 1000000; poll++)
            {
                if (!NetworkSchedulerPoll(state, false) || NetDevice.Poll(device, 8, out _) != NetDevice.Status.Success) return false;
                if (device->ReclaimedTransmits == 32)
                    return device->ReceivedFrames == 32 && WriteNetworkRow(state, 3, 12, device->ReceivedFrames, device->ReclaimedTransmits, deadline) &&
                        RunNetworkTcp(state, device, resolved, tx, rx);
                if (!ReadNetworkTime(state, out now) || now >= deadline) return false;
            }
            return false;
#endif
        }

        private static bool WaitNetworkFrame(Storage* state, NetDevice.State* device, Span<byte> frame, out uint bytes)
        {
            bytes = 0;
            if (!ReadNetworkTime(state, out ulong now) || now > ~0UL - 2000000000) return false;
            ulong deadline = now + 2000000000;
            for (uint poll = 0; poll < 1000000; poll++)
            {
                if (!NetworkSchedulerPoll(state, false) || NetDevice.Poll(device, 8, out _) != NetDevice.Status.Success) return false;
                var status = NetDevice.Receive(device, frame, out bytes);
                if (status == NetDevice.Status.Success) return true;
                if (status != NetDevice.Status.Empty || !ReadNetworkTime(state, out now) || now >= deadline) return false;
            }
            return false;
        }

        private static bool NetworkArpReply(ReadOnlySpan<byte> frame, MacAddress mac, Ipv4Address local, Ipv4Address peer, out MacAddress resolved)
        {
            resolved = default;
            if (EthernetHeader.TryParse(frame, out var ethernet, out var payload) != PacketParseStatus.Success ||
                ethernet.EtherType != EthernetHeader.EtherTypeArp || ethernet.Destination != mac ||
                ArpPacket.TryParse(payload, out var arp) != PacketParseStatus.Success || arp.Operation != ArpPacket.OperationReply ||
                arp.SenderAddress != peer || arp.TargetAddress != local || arp.TargetMac != mac || arp.SenderMac != ethernet.Source) return false;
            resolved = arp.SenderMac; return true;
        }

        private static ulong PackNetworkMac(MacAddress mac) => ((ulong)mac.Octet0 << 40) | ((ulong)mac.Octet1 << 32) |
            ((ulong)mac.Octet2 << 24) | ((ulong)mac.Octet3 << 16) | ((ulong)mac.Octet4 << 8) | mac.Octet5;

        private static bool NetworkAllocatorEqual(BootPageAllocatorState a, BootPageAllocatorState b,
            BootPageFreeListNode x, BootPageFreeListNode y) =>
            a.IsInitialized == b.IsInitialized && a.PageSize == b.PageSize && a.HeadNodeIndex == b.HeadNodeIndex &&
            a.FreeNodeCount == b.FreeNodeCount && a.FreePageCount == b.FreePageCount && a.FreeByteCount == b.FreeByteCount &&
            a.AllocatedPageCount == b.AllocatedPageCount && a.AllocatedByteCount == b.AllocatedByteCount &&
            x.SourceFreeSegmentIndex == y.SourceFreeSegmentIndex && x.SourceUsableRangeIndex == y.SourceUsableRangeIndex &&
            x.PhysicalStart == y.PhysicalStart && x.PhysicalEndExclusive == y.PhysicalEndExclusive &&
            x.PageCount == y.PageCount && x.ByteLength == y.ByteLength && x.NextNodeIndex == y.NextNodeIndex;

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ReadNetworkNanos(void* context, ulong* value)
        {
            if (value == null || !ReadNetworkTime((Storage*)context, out ulong nanos)) return 0;
            *value = nanos; return 1;
        }

        private static bool ReadNetworkTime(Storage* state, out ulong nanos, bool preparing = false)
        {
            nanos = 0;
            // The scheduler timestamps readiness before NIC admission. Only
            // that private bind call admits phase1; device callbacks require2.
            if (state == null || (state->NetworkPhase != 2 && (!preparing || state->NetworkPhase != 1)) || state->ServicePhase != 2 ||
                state->AdapterConsole != null || state->AdapterServices != null ||
                !OwnedMonotonicClock.Read(ref state->Clock) || state->Clock.Source != OwnedClockSource.Hpet ||
                state->Clock.RecoveryAttempts > 1 || !FirstExitOwnedArena.ValidateCanaries(state->Arena)) return false;
            ulong frequency = state->Clock.FrequencyHz, ticks = state->Clock.Ticks;
            if (frequency == 0 || frequency > ~0UL / 1000000000 || ticks / frequency > ~0UL / 1000000000) return false;
            ulong whole = ticks / frequency * 1000000000, fraction = ticks % frequency * 1000000000 / frequency;
            if (whole > ~0UL - fraction) return false;
            nanos = whole + fraction; return true;
        }

        private static bool WriteNetworkRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d) =>
            LinuxText(state, "M07_026_ROW stage=") && LinuxNumber(state, stage) &&
            LinuxText(state, " a=") && LinuxNumber(state, a) && LinuxText(state, " b=") && LinuxNumber(state, b) &&
            LinuxText(state, " c=") && LinuxNumber(state, c) && LinuxText(state, " d=") && LinuxNumber(state, d) && LinuxText(state, "\r\n");
    }
}
#endif
