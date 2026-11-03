using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
#if M16_025_PROBE

#endif
#if M07_026_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M16_002_PROBE

#endif
#if M16_004_PROBE

#endif
#if M16_005_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
/// <summary>
/// Compose the complete first-exit mapping declarations before building tables:
/// original image, owned arena without guards, authenticated code and the original
/// allocator backing. The table builder adds its own RW/NX arena. All input
/// ownership, physical identity, object readiness and callback provenance remain
/// adapter obligations. No allocation, table write or CPU transition occurs here.
/// </summary>

public static unsafe class FirstExitRetainedMappings
{
    public const ulong MaximumRanges =
#if M16_025_PROBE
        7 +
#endif
    FirstExitImageMappings.MaximumSections + 1 + FirstExitOwnedArena.MappingRangeCount + 3
#if M07_026_PROBE
        + 1
#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
        + 1
#endif
#if M16_002_PROBE
        + 1
#endif
#if M16_004_PROBE
        + 2
#endif
#if M16_005_PROBE
        + 3
#endif
#if M16_006_PROBE
        + 3
#endif
#if M16_007_PROBE
        + 1
#endif
#if M05_PHYSICAL_MAPPING_PROBE
        + 1
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE
        + 1
#endif
#if M05_PCI_ENUMERATION_PROBE
        + 1
#endif
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        + 2
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        + 4
#endif
#if M05_APIC_SETUP_PROBE
        + 2
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
        + 1
#endif
    ;
    private const int MaximumAllocations = 6
#if M16_025_PROBE
        + 2
#endif
#if M07_026_PROBE
        + 1
#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
        + 1
#endif
#if M16_002_PROBE
        + 1
#endif
#if M16_004_PROBE
        + 2
#endif
#if M16_005_PROBE
        + 2
#endif
#if M16_006_PROBE
        + 2
#endif
#if M16_007_PROBE
        + 1
#endif
#if M05_PHYSICAL_MAPPING_PROBE
        + 1
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE
        + 1
#endif
#if M05_PCI_ENUMERATION_PROBE
        + 1
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        + 1
#endif
#if M05_APIC_SETUP_PROBE
        + 2
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
        + 1
#endif
    ;
    // Static and writable fixtures own32 descriptions, including bounded proc snapshots.
    // Earlier profiles retain their original64-page admission ceiling.
#if M10_017_PROBE || M10_018_PROBE || M16_027_PROBE
    private const ulong MaximumDescriptorPages = 70;
#else
    private const ulong MaximumDescriptorPages = 64;
#endif
    private const ulong LowEnd = 0x0000800000000000;
    private const PageProtection Data = PageProtection.Read | PageProtection.Write;
    private const PageProtection Code = PageProtection.Read | PageProtection.Execute;
    public static bool WriteRanges(ref FirstExitOwnedArenaState arena, byte* code, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, FirstExitImageReferences references, SeparatePageFaultMappingRange allocator, SeparatePageFaultPageTableRequest tables, SeparatePageFaultMappingRange* ranges, ulong capacity, out ulong written) => WriteRangesCore(ref arena, code, capture, mapping, references, allocator, tables, ranges, capacity, false, out written);
    // Reconstruct expected declarations after the native continuation recorded
    // installed descriptors. Caller-owned scratch may lie on the owned main stack.
    public static bool WriteInstalledRanges(ref FirstExitOwnedArenaState arena, byte* code, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, FirstExitImageReferences references, SeparatePageFaultMappingRange allocator, SeparatePageFaultPageTableRequest tables, SeparatePageFaultMappingRange* ranges, ulong capacity, out ulong written) => WriteRangesCore(ref arena, code, capture, mapping, references, allocator, tables, ranges, capacity, true, out written);
    private static bool WriteRangesCore(ref FirstExitOwnedArenaState arena, byte* code, EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, FirstExitImageReferences references, SeparatePageFaultMappingRange allocator, SeparatePageFaultPageTableRequest tables, SeparatePageFaultMappingRange* ranges, ulong capacity, bool installed, out ulong written)
    {
        written = 0;
        if (ranges == null || ((ulong)ranges & 7) != 0 || capacity == 0 || capacity > MaximumRanges || !allocator.IsInitialized || allocator.Protection != Data || allocator.CachePolicy != PageCachePolicy.WriteBack || allocator.PageCount == 0 || allocator.PageCount > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRangePages || allocator.Start < 4096 || (allocator.Start & 4095) != 0 || allocator.Start >= LowEnd - allocator.PageCount * 4096 || (references.RuntimeSupportCode == 0 ? references.RuntimeSupportCodeBytes != 0 : references.RuntimeSupportCodeBytes != 4096 || (references.RuntimeSupportCode & 4095) != 0 || references.RuntimeSupportCode >= LowEnd - 4096) || SeparateDisabledDefaultPageFaultPageTables.ValidateRequest(tables) != SeparatePageFaultPageTableStatus.Success || tables.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong root) != AddressValueStatus.Success || root >= 0x100000000UL - tables.ArenaPageCount * 4096 || !(installed ? FirstExitCodeBundle.ValidateInstalledReadBack(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, references.FaultCallback) : FirstExitCodeBundle.ValidateReadBack(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, references.FaultCallback)))
            return false;
        SeparatePageFaultMappingRange* pending = stackalloc SeparatePageFaultMappingRange[(int)MaximumRanges];
        if (!FirstExitImageMappings.WriteRanges(capture, mapping, pending, MaximumRanges, out ulong count) || !FirstExitImageMappings.Contains(pending, count, references.EntryCallback, 1, Code) || !FirstExitImageMappings.Contains(pending, count, references.DiagnosticCallback, 1, Code) || !FirstExitImageMappings.Contains(pending, count, references.AdapterState, references.AdapterStateBytes, Data) || !FirstExitImageMappings.Contains(pending, count, references.AllocatorState, references.AllocatorStateBytes, Data) || !FirstExitImageMappings.Contains(pending, count, references.CopiedAcpi, references.CopiedAcpiBytes, Data))
            return false;
        // The original arena handle must itself survive the switch as part of
        // the image-resident adapter state, not on the discarded firmware stack.
        fixed (FirstExitOwnedArenaState* state = &arena)
            if ((ulong)state < references.AdapterState || references.AdapterStateBytes < (ulong)sizeof(FirstExitOwnedArenaState) || (ulong)state - references.AdapterState > references.AdapterStateBytes - (ulong)sizeof(FirstExitOwnedArenaState))
                return false;
        ulong* starts = stackalloc ulong[MaximumAllocations];
        ulong* lengths = stackalloc ulong[MaximumAllocations];
        starts[0] = pending[0].Start;
        lengths[0] = capture.Range.ImageSize;
        starts[1] = (ulong)arena.Storage;
        lengths[1] = FirstExitOwnedArena.Bytes;
        starts[2] = (ulong)code;
        lengths[2] = FirstExitCodeBundle.Bytes;
        starts[3] = allocator.Start;
        lengths[3] = allocator.PageCount * 4096;
        starts[4] = root;
        lengths[4] = tables.ArenaPageCount * 4096;
        int allocations = references.RuntimeSupportCode == 0 ? 5 : 6;
        starts[5] = references.RuntimeSupportCode;
        lengths[5] = references.RuntimeSupportCodeBytes;
#if M16_002_PROBE
        if (references.RuntimeSupportCode == 0 || references.LinuxPackageStart < 4096 ||
            (references.LinuxPackageStart & 4095) != 0 || references.LinuxPackagePages == 0 || references.LinuxPackagePages > 4096 ||
            references.LinuxPackageStart >= LowEnd - references.LinuxPackagePages * 4096)
            return false;
        starts[6] = references.LinuxPackageStart;
        lengths[6] = references.LinuxPackagePages * 4096;
        allocations = 7;
#endif
#if M16_004_PROBE
        if (references.LinuxBackingStart < 4096 || (references.LinuxBackingStart & 4095) != 0 ||
            references.LinuxBackingPages == 0 || references.LinuxBackingPages > 4096 ||
            references.LinuxBackingStart > 0x100000000UL - references.LinuxBackingPages * 4096 ||
            references.LinuxUserTablesStart < 4096 || (references.LinuxUserTablesStart & 4095) != 0 ||
            references.LinuxUserTablePages != X64UserAddressSpace.MaximumTables ||
            references.LinuxUserTablesStart > 0x100000000UL - references.LinuxUserTablePages * 4096)
            return false;
        starts[allocations] = references.LinuxBackingStart;
        lengths[allocations++] = references.LinuxBackingPages * 4096;
        starts[allocations] = references.LinuxUserTablesStart;
        lengths[allocations++] = references.LinuxUserTablePages * 4096;
#endif
#if M05_PHYSICAL_MAPPING_PROBE
        if (references.RuntimeSupportCode == 0 || references.MappingScratchBacking < 0x100000 ||
            (references.MappingScratchBacking & 4095) != 0 || references.MappingScratchBackingPages != 1023 ||
            references.MappingScratchBacking >= LowEnd - 1023 * 4096 ||
            (references.MappingScratchStart & 0x1fffff) != 0 ||
            references.MappingScratchStart < references.MappingScratchBacking ||
            references.MappingScratchStart - references.MappingScratchBacking > (1023 - 512) * 4096)
            return false;
        starts[6] = references.MappingScratchBacking;
        lengths[6] = 1023 * 4096;
        allocations = 7;
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE
        if (references.RuntimeSupportCode == 0 || references.HpetDevicePage < 0x100000 ||
            references.HpetDevicePage > 0xfffff000 || (references.HpetDevicePage & 4095) != 0)
            return false;
        starts[allocations] = references.HpetDevicePage;
        lengths[allocations++] = 4096;
#endif
#if M05_PCI_ENUMERATION_PROBE
        if (references.RuntimeSupportCode == 0 || references.EcamStart < 0x100000 ||
            (references.EcamStart & 0xfffff) != 0 || references.EcamPages != 256 ||
            references.EcamStart > 0x100000000UL - 0x100000)
            return false;
        starts[allocations] = references.EcamStart;
        lengths[allocations++] = 0x100000;
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        if (references.VirtioBarStart < 0x100000 || (references.VirtioBarStart & 0x3fff) != 0 ||
            references.VirtioBarStart >= LowEnd - 0x4000)
            return false;
        // Append after package, user tables, HPET and ECAM. A fixed index here
        // overwrote earlier owners and omitted them from the overlap audit.
        starts[allocations] = references.VirtioBarStart;
        lengths[allocations++] = 0x4000;
#if M16_025_PROBE
        if (references.NativeDiskBarStart < 0x100000 || (references.NativeDiskBarStart & 0x3fff) != 0 ||
            references.NativeDiskBarStart >= LowEnd - 0x4000 || references.NativeDiskArenaStart < 4096 ||
            (references.NativeDiskArenaStart & 4095) != 0 || references.NativeDiskArenaPages is 0 or > 256 ||
            references.NativeDiskArenaStart > 0x100000000UL - references.NativeDiskArenaPages * 4096)
            return false;
        starts[allocations] = references.NativeDiskBarStart;
        lengths[allocations++] = 0x4000;
        starts[allocations] = references.NativeDiskArenaStart;
        lengths[allocations++] = references.NativeDiskArenaPages * 4096;
#endif
#endif
#if M05_APIC_SETUP_PROBE
        if (references.LocalApicPage < 0x100000 || references.LocalApicPage > 0xfffff000 || (references.LocalApicPage & 4095) != 0 ||
            references.IoApicPage < 0x100000 || references.IoApicPage > 0xfffff000 || (references.IoApicPage & 4095) != 0)
            return false;
        starts[allocations] = references.LocalApicPage;
        lengths[allocations++] = 4096;
        starts[allocations] = references.IoApicPage;
        lengths[allocations++] = 4096;
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
        if (references.MsixPage < 0x100000 || references.MsixPage >= LowEnd - 4096 || (references.MsixPage & 4095) != 0)
            return false;
        starts[allocations] = references.MsixPage;
        lengths[allocations++] = 4096;
#endif
#if M16_005_PROBE
        if (references.LinuxSyscallCodeStart < 4096 || (references.LinuxSyscallCodeStart & 4095) != 0 ||
            references.LinuxSyscallCodeStart > 0x100000000UL - X64LinuxUserTransition.CodeBytes ||
            references.LinuxSyscallDataStart < 4096 || (references.LinuxSyscallDataStart & 4095) != 0 ||
            references.LinuxSyscallDataStart > 0x100000000UL - X64LinuxUserTransition.DataBytes)
            return false;
        // Ownership includes the unmapped guard, so no other retained allocation
        // may occupy any page of the original twenty-page syscall data backing.
        starts[allocations] = references.LinuxSyscallCodeStart;
        lengths[allocations++] = X64LinuxUserTransition.CodeBytes;
        starts[allocations] = references.LinuxSyscallDataStart;
        lengths[allocations++] = X64LinuxUserTransition.DataBytes;
#endif
#if M16_006_PROBE
        if (references.LinuxMemoryPoolPages == 0 || references.LinuxMemoryPoolPages > LinuxMemoryServices.MaximumPoolPages ||
            references.LinuxMemoryPoolStart < 4096 || (references.LinuxMemoryPoolStart & 4095) != 0 ||
            references.LinuxMemoryPoolStart > 0x100000000UL - references.LinuxMemoryPoolPages * 4096 ||
            references.LinuxMemoryCandidateStart < 4096 || (references.LinuxMemoryCandidateStart & 4095) != 0 ||
            references.LinuxMemoryCandidateStart > 0x100000000UL - X64UserAddressSpace.MaximumTables * 4096UL)
            return false;
        starts[allocations] = references.LinuxMemoryPoolStart;
        lengths[allocations++] = references.LinuxMemoryPoolPages * 4096;
        starts[allocations] = references.LinuxMemoryCandidateStart;
        lengths[allocations++] = X64UserAddressSpace.MaximumTables * 4096UL;
#endif
#if M16_007_PROBE
        if (references.LinuxDescriptorPages == 0 || references.LinuxDescriptorPages > MaximumDescriptorPages ||
            references.LinuxDescriptorStart < 4096 || (references.LinuxDescriptorStart & 4095) != 0 ||
            references.LinuxDescriptorStart > 0x100000000UL - references.LinuxDescriptorPages * 4096)
            return false;
        starts[allocations] = references.LinuxDescriptorStart;
        lengths[allocations++] = references.LinuxDescriptorPages * 4096;
#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
#if M16_008_PROBE
        const ulong cooperativePages = OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.PoolPages;
#else
        const ulong cooperativePages = OS.Kernel.Threading.ThreadContextSwitch.PoolPages;
#endif
        if (references.CooperativePoolPages != cooperativePages ||
            references.CooperativePoolStart < 4096 || (references.CooperativePoolStart & 4095) != 0 ||
            references.CooperativePoolStart > 0x100000000UL - cooperativePages * 4096UL)
            return false;
        starts[allocations] = references.CooperativePoolStart;
        lengths[allocations++] = cooperativePages * 4096UL;
#endif
#if M07_026_PROBE
        if (references.NetworkArenaPages != 8 || references.NetworkArenaStart < 4096 ||
            (references.NetworkArenaStart & 4095) != 0 || references.NetworkArenaStart > 0x100000000UL - 32768)
            return false;
        starts[allocations] = references.NetworkArenaStart;
        lengths[allocations++] = 32768;
#endif
        for (int i = 0; i < allocations; i++)
            for (int j = 0; j < i; j++)
                if (FirstExitDescriptors.Overlaps(starts[i], lengths[i], starts[j], lengths[j]))
                    return false;
        if (!FirstExitOwnedArena.WriteMappingRanges(ref arena, pending + count, MaximumRanges - count, out ulong arenaCount))
            return false;
        count += arenaCount;
        pending[count++] = new SeparatePageFaultMappingRange((ulong)code, FirstExitCodeBundle.Bytes / 4096, Code);
        pending[count++] = allocator;
#if M07_026_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.NetworkArenaStart, 8, Data, PageCachePolicy.WriteBack);
#if M16_025_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.NativeDiskArenaStart, references.NativeDiskArenaPages, Data, PageCachePolicy.WriteBack);
        for (uint diskPage = 0; diskPage < 4; diskPage++)
            pending[count++] = new SeparatePageFaultMappingRange(references.NativeDiskBarStart + diskPage * 4096, 1, Data, PageCachePolicy.Uncached);
#endif
#endif
        if (references.RuntimeSupportCode != 0)
            pending[count++] = new SeparatePageFaultMappingRange(references.RuntimeSupportCode, 1, Code);
#if M16_002_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.LinuxPackageStart, references.LinuxPackagePages, PageProtection.Read);
#endif
#if M16_004_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.LinuxBackingStart, references.LinuxBackingPages, Data);
        pending[count++] = new SeparatePageFaultMappingRange(references.LinuxUserTablesStart, references.LinuxUserTablePages, Data);
#endif
#if M16_005_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.LinuxSyscallCodeStart, X64LinuxUserTransition.CodeBytes / 4096, Code);
        pending[count++] = new SeparatePageFaultMappingRange(references.LinuxSyscallDataStart, X64LinuxUserTransition.GuardOffset / 4096, Data);
        pending[count++] = new SeparatePageFaultMappingRange(references.LinuxSyscallDataStart + X64LinuxUserTransition.StackOffset,
            (X64LinuxUserTransition.DataBytes - X64LinuxUserTransition.StackOffset) / 4096, Data);
#endif
#if M16_006_PROBE
        // Preserve the per-range bound while retaining one contiguous owner.
        for (ulong offset = 0; offset < references.LinuxMemoryPoolPages; offset += 4096)
        {
            ulong pages = references.LinuxMemoryPoolPages - offset;
            pending[count++] = new SeparatePageFaultMappingRange(references.LinuxMemoryPoolStart + offset * 4096,
                pages > 4096 ? 4096 : pages, Data);
        }
        pending[count++] = new SeparatePageFaultMappingRange(references.LinuxMemoryCandidateStart, X64UserAddressSpace.MaximumTables, Data);
#endif
#if M16_007_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.LinuxDescriptorStart, references.LinuxDescriptorPages, Data);
#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.CooperativePoolStart,
            references.CooperativePoolPages, Data, PageCachePolicy.WriteBack);
#endif
#if M05_PHYSICAL_MAPPING_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.MappingScratchStart, 512, Data);
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.HpetDevicePage, 1, Data, PageCachePolicy.Uncached);
#endif
#if M05_PCI_ENUMERATION_PROBE
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        // Only the dedicated quiescent test function at device 6/function 0 is
        // writable. The rest of the admitted one-bus ECAM window stays read-only.
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart, 48, PageProtection.Read, PageCachePolicy.Uncached);
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart + 0x30000, 1, Data, PageCachePolicy.Uncached);
#if M16_025_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart + 0x31000, 7, PageProtection.Read, PageCachePolicy.Uncached);
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart + 0x38000, 1, Data, PageCachePolicy.Uncached);
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart + 0x39000, 199, PageProtection.Read, PageCachePolicy.Uncached);
#else
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart + 0x31000, 207, PageProtection.Read, PageCachePolicy.Uncached);
#endif
#else
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart, references.EcamPages, PageProtection.Read, PageCachePolicy.Uncached);
#endif
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
        for (ulong page = 0; page < 4; page++)
            pending[count++] = new SeparatePageFaultMappingRange(references.VirtioBarStart + page * 4096, 1, Data, PageCachePolicy.Uncached);
#endif
#if M05_APIC_SETUP_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.LocalApicPage, 1, Data, PageCachePolicy.Uncached);
        pending[count++] = new SeparatePageFaultMappingRange(references.IoApicPage, 1, Data, PageCachePolicy.Uncached);
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.MsixPage, 1, Data, PageCachePolicy.Uncached);
#endif
        ulong totalPages = 0;
        for (ulong i = 0; i < count; i++)
            totalPages += pending[i].PageCount;
        ulong outputBytes = count * (ulong)sizeof(SeparatePageFaultMappingRange);
        if (capacity < count || totalPages > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedMappedPages || (ulong)ranges >= LowEnd - outputBytes)
            return false;
        for (int i = 0; i < allocations; i++)
        {
            if (i == 1 && FirstExitOwnedArena.InMainStack(arena, (ulong)ranges, outputBytes))
                continue;
            if (FirstExitDescriptors.Overlaps((ulong)ranges, outputBytes, starts[i], lengths[i]))
                return false;
        }

        // Commit only after all declarations and aliases have passed. Caller may
        // build/audit from this scratch output, then discard it before entry.
        for (ulong i = 0; i < count; i++)
            ranges[i] = pending[i];
        written = count;
        return true;
    }
}
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE
#if M07_026_PROBE

#else

#endif
#endif
#if M16_004_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE
#if M16_008_PROBE

#else

#endif
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
#if M16_008_PROBE

#else

#endif
#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M16_006_PROBE

#elif M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE

#endif
#if M16_004_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_APIC_SETUP_PROBE || M16_006_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE

#endif
#if M16_004_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_APIC_SETUP_PROBE || M16_006_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE || M16_004_PROBE

#endif
#if M16_004_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M05_APIC_SETUP_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M05_VIRTQUEUE_POLLING_PROBE || M05_VIRTQUEUE_INTERRUPT_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_APIC_SETUP_PROBE || M16_006_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
