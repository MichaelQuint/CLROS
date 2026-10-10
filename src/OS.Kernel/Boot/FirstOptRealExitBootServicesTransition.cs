using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

namespace OS.Boot;

// Original image-resident objects and callback entries supplied by the adapter.
// Coverage does not establish their provenance, readiness or native stack use.
public struct FirstExitImageReferences
{
    public ulong EntryCallback, DiagnosticCallback, FaultCallback;
    public ulong AdapterState, AdapterStateBytes;
    public ulong AllocatorState, AllocatorStateBytes;
    public ulong CopiedAcpi, CopiedAcpiBytes;
    // Optional in host-only compositions. The EFI adapter must retain its
    // original installed NativeAOT stack-probe page, outside the loaded image.
    public ulong RuntimeSupportCode, RuntimeSupportCodeBytes;
#if M05_MONOTONIC_CLOCK_PROBE
    // Original retained ACPI HPET GAS, admitted against the owned firmware map.
    public ulong HpetDevicePage;
#endif
#if M05_PCI_ENUMERATION_PROBE
    public ulong EcamStart, EcamPages;
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE
    public ulong VirtioBarStart;
#endif
#if M05_PHYSICAL_MAPPING_PROBE
    // Original EFI allocation containing one exclusive aligned 2 MiB test span.
    public ulong MappingScratchBacking, MappingScratchBackingPages, MappingScratchStart;
#endif
}

public enum FirstExitDiagnosticStatus : uint
{
    Empty, AdmissionFailed, SerialFailed, ValidationFailed, Completed
}

public struct FirstExitDiagnosticState
{
    public FirstExitDiagnosticStatus Status;
}

// The adapter retains the original inputs in its image-resident state. All
// pointers must remain accessible/exclusive; declarations are not capabilities.
public unsafe struct FirstExitDiagnosticMemory
{
    public byte* Code;
    public EfiLoadedImageRangeCapturePlan Capture;
    public M01LoadedImageVirtualMappingEvidence Mapping;
    public FirstExitImageReferences References;
    public SeparatePageFaultMappingRange Allocator;
    public SeparatePageFaultPageTableRequest Tables;
    public X64PageTableImagePage* Pages;
    public ulong PageCapacity;
    public ulong* Entries;
    public ulong EntryCapacity;
}

/// <summary>
/// One-use reporting from the terminal owned callback. Admit the original native
/// observations, reconstruct essential mappings and audit the actual root backing
/// before advancing success markers. No firmware function or output is accepted.
/// The caller must authenticate this callback/native observation provenance and
/// halt after every return, including success. Host-supplied observations alone
/// are not proof that hardware performed ExitBootServices or loaded CR3.
/// </summary>
public static unsafe class FirstExitPostExitDiagnostics
{
    public static bool Report(ref FirstExitDiagnosticState state, ref FirstExitOwnedArenaState arena,
        FirstExitDiagnosticMemory memory)
    {
        if (state.Status != FirstExitDiagnosticStatus.Empty) return false;
        fixed (FirstExitDiagnosticState* address = &state)
        {
            ulong start = (ulong)address, owner = memory.References.AdapterState, bytes = memory.References.AdapterStateBytes;
            if (start < owner || bytes < (ulong)sizeof(FirstExitDiagnosticState) ||
                start - owner > bytes - (ulong)sizeof(FirstExitDiagnosticState)) return false;
            fixed (FirstExitOwnedArenaState* handle = &arena)
                if (FirstExitDescriptors.Overlaps(start, (ulong)sizeof(FirstExitDiagnosticState),
                    (ulong)handle, (ulong)sizeof(FirstExitOwnedArenaState))) return false;
        }
        state.Status = FirstExitDiagnosticStatus.AdmissionFailed;
        if (!arena.IsReady || !FirstExitOwnedArena.ValidateCanaries(arena) ||
            memory.Tables.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong root) != AddressValueStatus.Success ||
            root == 0 || (ulong)memory.Entries != root ||
            memory.PageCapacity != memory.Tables.ArenaPageCount || memory.EntryCapacity != memory.PageCapacity * 512 ||
            arena.Transition->Phase != FirstExitPhase.OwnedContinuation ||
            arena.Transition->Mode != FirmwareServicesMode.BootServicesExited || !arena.Transition->Attempt.Succeeded ||
            !arena.Transition->RetainedFirmwareCallbacksCleared || !arena.Binding->IsConsumed || !arena.Binding->RetainedPointersCleared)
            return false;
        FirstExitPermanentCr3Context* adoption = arena.Permanent;
        FirstExitOwnedStackContext* entry = arena.StackEntry;
        if (adoption->Phase != FirstExitAdoptionPhase.OwnedDiagnostic || adoption->Reserved != 0 ||
            adoption->OwnedCr3 != root || adoption->ObservedCr3 != root ||
            (adoption->FirmwareCr3 & ~0x18UL) == 0 || adoption->FirmwareCr3 >= 0x100000000 ||
            (adoption->FirmwareCr3 & 0xfe7) != 0 || (adoption->FirmwareCr3 & ~0xfffUL) == root ||
            (adoption->FirmwareCr4 & ~0x7ffUL) != 0 || (adoption->FirmwareCr4 & 0x20) == 0 ||
            adoption->ObservedCr4 != (adoption->FirmwareCr4 & ~0x80UL) ||
            adoption->StackBottom != arena.StackBottom || adoption->StackTop != arena.StackTop ||
            adoption->MinimumRsp <= arena.StackBottom || adoption->MinimumRsp >= arena.StackTop ||
            adoption->ObservedRsp < adoption->MinimumRsp || adoption->ObservedRsp >= arena.StackTop || (adoption->ObservedRsp & 15) != 8 ||
            adoption->LowerCanary != FirstExitOwnedArena.LowerCanary || adoption->UpperCanary != FirstExitOwnedArena.UpperCanary ||
            adoption->Descriptors != arena.Descriptors || (ulong)adoption->Diagnostic != memory.References.DiagnosticCallback ||
            (ulong)adoption->DiagnosticContext != memory.References.AdapterState ||
            entry->Phase != 2 || entry->Reserved != 0 || entry->FirmwareCr3 != adoption->FirmwareCr3 || entry->FirmwareCr4 != adoption->FirmwareCr4 ||
            entry->StackBottom != arena.StackBottom || entry->StackTop != arena.StackTop || entry->ObservedRsp != arena.StackTop - FirstExitOwnedStackStub.CallStackBytes ||
            (ulong)entry->Entry != memory.References.EntryCallback || (ulong)entry->EntryContext != memory.References.AdapterState)
            return false;
        SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
        if (!FirstExitRetainedMappings.WriteInstalledRanges(ref arena, memory.Code, memory.Capture, memory.Mapping,
            memory.References, memory.Allocator, memory.Tables, ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) ||
            !FirstExitImageMappings.Contains(ranges, count, (ulong)memory.Pages,
                memory.PageCapacity * (ulong)sizeof(X64PageTableImagePage), PageProtection.Read | PageProtection.Write)) return false;

        state.Status = FirstExitDiagnosticStatus.SerialFailed;
        Com1OwnedPortIoBackend backend = arena.Fault->Backend;
        if (!backend.ConfigureDefault().Succeeded || !WriteMarker(backend, 0)) return false;
        state.Status = FirstExitDiagnosticStatus.ValidationFailed;
        if (!SeparateDisabledDefaultPageFaultPageTables.AuditRetainedAccessedImage(memory.Tables,
            ranges, count, FirstExitRetainedMappings.MaximumRanges, memory.Pages, memory.PageCapacity,
            memory.Entries, memory.EntryCapacity).Succeeded || !FirstExitOwnedArena.ValidateCanaries(arena)) return false;
        state.Status = FirstExitDiagnosticStatus.SerialFailed;
        if (!WriteMarker(backend, 1)) return false;
        state.Status = FirstExitDiagnosticStatus.ValidationFailed;
        if (!FirstExitOwnedArena.ValidateCanaries(arena)) return false;
        state.Status = FirstExitDiagnosticStatus.SerialFailed;
        if (!WriteMarker(backend, 2)) return false;
        // Both diagnostic groups use the original owned transport. Recheck
        // canaries before every claim; a partial write consumes this attempt.
        for (uint marker = 3; marker < 10; marker++)
        {
            state.Status = FirstExitDiagnosticStatus.ValidationFailed;
            if (!FirstExitOwnedArena.ValidateCanaries(arena)) return false;
            state.Status = FirstExitDiagnosticStatus.SerialFailed;
            if (!WriteMarker(backend, marker)) return false;
        }
        state.Status = FirstExitDiagnosticStatus.Completed;
        return true;
    }

    private static bool WriteMarker(Com1OwnedPortIoBackend backend, uint marker)
    {
        // Fixed ASCII payloads include CRLF. No strings, formatting allocation or
        // arbitrary callback is used between observations and the owned UART.
        // POST_FIRMWARE_DIAGNOSTICS_READY plus CRLF needs 33 bytes.
        ulong* words = stackalloc ulong[5];
        words[4] = 0;
        uint bytes;
        if (marker == 0)
        {
            words[0] = 0x5f5342455f33304dUL; words[1] = 0x44454e5255544552UL;
            words[2] = 0x535345434355535fUL; words[3] = 0x0000000000000a0dUL; bytes = 26;
        }
        else if (marker == 1)
        {
            words[0] = 0x4d5245505f33304dUL; words[1] = 0x52435f544e454e41UL;
            words[2] = 0x4554504f44415f33UL; words[3] = 0x00000000000a0d44UL; bytes = 27;
        }
        else if (marker == 2)
        {
            words[0] = 0x54534f505f33304dUL; words[1] = 0x4e574f5f5342455fUL;
            words[2] = 0x4e474149445f4445UL; words[3] = 0x000a0d434954534fUL; bytes = 31;
        }
        else if (marker == 3)
        {
            words[0] = 0x304d5f534f524c43UL; words[1] = 0x45525f3730305f33UL;
            words[2] = 0x0000000a0d594441UL; words[3] = 0; bytes = 21;
        }
        else if (marker == 4)
        {
            words[0] = 0x5341505f54534554UL; words[1] = 0x30302e33306d2053UL;
            words[2] = 0x00000000000a0d37UL; words[3] = 0; bytes = 19;
        }
        else if (marker == 5)
        {
            words[0] = 0x4154535f54534554UL; words[1] = 0x302e33306d205452UL;
            words[2] = 0x000000000a0d3930UL; words[3] = 0; bytes = 20;
        }
        else if (marker == 6)
        {
            words[0] = 0x5342455f49464555UL; words[1] = 0x535345434355535fUL;
            words[2] = 0x0000000000000a0dUL; words[3] = 0; bytes = 18;
        }
        else if (marker == 7)
        {
            words[0] = 0x5249465f54534f50UL; words[1] = 0x49445f455241574dUL;
            words[2] = 0x434954534f4e4741UL; words[3] = 0x0d59444145525f53UL;
            words[4] = 0x000000000000000aUL; bytes = 33;
        }
        else if (marker == 8)
        {
            words[0] = 0x304d5f534f524c43UL; words[1] = 0x45525f3930305f33UL;
            words[2] = 0x0000000a0d594441UL; words[3] = 0; bytes = 21;
        }
        else if (marker == 9)
        {
            words[0] = 0x5341505f54534554UL; words[1] = 0x30302e33306d2053UL;
            words[2] = 0x00000000000a0d39UL; words[3] = 0; bytes = 19;
        }
        else return false;
        for (uint index = 0; index < bytes; index++)
        {
            bool ready = false;
            for (int poll = 0; poll < PanicSafeFormattingSerialReportingOwnedCom1.DefaultTransmitReadyPollLimit; poll++)
            {
                Com1OwnedPortIoOperationResult read = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
                if (!read.Succeeded) return false;
                if ((read.Value & PanicSafeFormattingSerialReportingOwnedCom1.TransmitHoldingRegisterEmptyMask) != 0) { ready = true; break; }
            }
            if (!ready || !backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, ((byte*)words)[index]).Succeeded) return false;
        }
        return true;
    }
}

/// <summary>
/// Compose the complete first-exit mapping declarations before building tables:
/// original image, owned arena without guards, authenticated code and the original
/// allocator backing. The table builder adds its own RW/NX arena. All input
/// ownership, physical identity, object readiness and callback provenance remain
/// adapter obligations. No allocation, table write or CPU transition occurs here.
/// </summary>
public static unsafe class FirstExitRetainedMappings
{
    public const ulong MaximumRanges = FirstExitImageMappings.MaximumSections + 1 +
        FirstExitOwnedArena.MappingRangeCount + 3
#if M05_PHYSICAL_MAPPING_PROBE
        + 1
#endif
#if M05_MONOTONIC_CLOCK_PROBE || M05_PCI_ENUMERATION_PROBE
        + 1
#endif
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE
        + 2
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE
        + 4
#endif
        ;
    private const ulong LowEnd = 0x0000800000000000;
    private const PageProtection Data = PageProtection.Read | PageProtection.Write;
    private const PageProtection Code = PageProtection.Read | PageProtection.Execute;

    public static bool WriteRanges(ref FirstExitOwnedArenaState arena, byte* code,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        FirstExitImageReferences references, SeparatePageFaultMappingRange allocator,
        SeparatePageFaultPageTableRequest tables,
        SeparatePageFaultMappingRange* ranges, ulong capacity, out ulong written)
        => WriteRangesCore(ref arena, code, capture, mapping, references, allocator, tables, ranges, capacity, false, out written);

    // Reconstruct expected declarations after the native continuation recorded
    // installed descriptors. Caller-owned scratch may lie on the owned main stack.
    public static bool WriteInstalledRanges(ref FirstExitOwnedArenaState arena, byte* code,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        FirstExitImageReferences references, SeparatePageFaultMappingRange allocator,
        SeparatePageFaultPageTableRequest tables,
        SeparatePageFaultMappingRange* ranges, ulong capacity, out ulong written)
        => WriteRangesCore(ref arena, code, capture, mapping, references, allocator, tables, ranges, capacity, true, out written);

    private static bool WriteRangesCore(ref FirstExitOwnedArenaState arena, byte* code,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping,
        FirstExitImageReferences references, SeparatePageFaultMappingRange allocator,
        SeparatePageFaultPageTableRequest tables,
        SeparatePageFaultMappingRange* ranges, ulong capacity, bool installed, out ulong written)
    {
        written = 0;
        if (ranges == null || ((ulong)ranges & 7) != 0 || capacity == 0 || capacity > MaximumRanges ||
            !allocator.IsInitialized || allocator.Protection != Data || allocator.CachePolicy != PageCachePolicy.WriteBack ||
            allocator.PageCount == 0 || allocator.PageCount > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRangePages ||
            allocator.Start < 4096 || (allocator.Start & 4095) != 0 || allocator.Start >= LowEnd - allocator.PageCount * 4096 ||
            (references.RuntimeSupportCode == 0 ? references.RuntimeSupportCodeBytes != 0 :
                references.RuntimeSupportCodeBytes != 4096 || (references.RuntimeSupportCode & 4095) != 0 ||
                references.RuntimeSupportCode >= LowEnd - 4096) ||
            SeparateDisabledDefaultPageFaultPageTables.ValidateRequest(tables) != SeparatePageFaultPageTableStatus.Success ||
            tables.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong root) != AddressValueStatus.Success ||
            root >= 0x100000000UL - tables.ArenaPageCount * 4096 ||
            !(installed ? FirstExitCodeBundle.ValidateInstalledReadBack(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, references.FaultCallback) :
                FirstExitCodeBundle.ValidateReadBack(code, FirstExitCodeBundle.Bytes, ref arena, capture, mapping, references.FaultCallback)))
            return false;

        SeparatePageFaultMappingRange* pending = stackalloc SeparatePageFaultMappingRange[(int)MaximumRanges];
        if (!FirstExitImageMappings.WriteRanges(capture, mapping, pending, MaximumRanges, out ulong count) ||
            !FirstExitImageMappings.Contains(pending, count, references.EntryCallback, 1, Code) ||
            !FirstExitImageMappings.Contains(pending, count, references.DiagnosticCallback, 1, Code) ||
            !FirstExitImageMappings.Contains(pending, count, references.AdapterState, references.AdapterStateBytes, Data) ||
            !FirstExitImageMappings.Contains(pending, count, references.AllocatorState, references.AllocatorStateBytes, Data) ||
            !FirstExitImageMappings.Contains(pending, count, references.CopiedAcpi, references.CopiedAcpiBytes, Data)) return false;
        // The original arena handle must itself survive the switch as part of
        // the image-resident adapter state, not on the discarded firmware stack.
        fixed (FirstExitOwnedArenaState* state = &arena)
            if ((ulong)state < references.AdapterState || references.AdapterStateBytes < (ulong)sizeof(FirstExitOwnedArenaState) ||
                (ulong)state - references.AdapterState > references.AdapterStateBytes - (ulong)sizeof(FirstExitOwnedArenaState)) return false;

#if M05_VIRTIO_NEGOTIATION_PROBE
        ulong* starts = stackalloc ulong[8];
        ulong* lengths = stackalloc ulong[8];
#elif M05_PHYSICAL_MAPPING_PROBE || M05_MONOTONIC_CLOCK_PROBE || M05_PCI_ENUMERATION_PROBE
        ulong* starts = stackalloc ulong[7];
        ulong* lengths = stackalloc ulong[7];
#else
        ulong* starts = stackalloc ulong[6];
        ulong* lengths = stackalloc ulong[6];
#endif
        starts[0] = pending[0].Start; lengths[0] = capture.Range.ImageSize;
        starts[1] = (ulong)arena.Storage; lengths[1] = FirstExitOwnedArena.Bytes;
        starts[2] = (ulong)code; lengths[2] = FirstExitCodeBundle.Bytes;
        starts[3] = allocator.Start; lengths[3] = allocator.PageCount * 4096;
        starts[4] = root; lengths[4] = tables.ArenaPageCount * 4096;
        int allocations = references.RuntimeSupportCode == 0 ? 5 : 6;
        starts[5] = references.RuntimeSupportCode; lengths[5] = references.RuntimeSupportCodeBytes;
#if M05_PHYSICAL_MAPPING_PROBE
        if (references.RuntimeSupportCode == 0 || references.MappingScratchBacking < 0x100000 ||
            (references.MappingScratchBacking & 4095) != 0 || references.MappingScratchBackingPages != 1023 ||
            references.MappingScratchBacking >= LowEnd - 1023 * 4096 ||
            (references.MappingScratchStart & 0x1fffff) != 0 ||
            references.MappingScratchStart < references.MappingScratchBacking ||
            references.MappingScratchStart - references.MappingScratchBacking > (1023 - 512) * 4096) return false;
        starts[6] = references.MappingScratchBacking; lengths[6] = 1023 * 4096;
        allocations = 7;
#endif
#if M05_MONOTONIC_CLOCK_PROBE
        if (references.RuntimeSupportCode == 0 || references.HpetDevicePage < 0x100000 ||
            references.HpetDevicePage > 0xfffff000 || (references.HpetDevicePage & 4095) != 0) return false;
        starts[6] = references.HpetDevicePage; lengths[6] = 4096; allocations = 7;
#endif
#if M05_PCI_ENUMERATION_PROBE
        if (references.RuntimeSupportCode == 0 || references.EcamStart < 0x100000 ||
            (references.EcamStart & 0xfffff) != 0 || references.EcamPages != 256 ||
            references.EcamStart > 0x100000000UL - 0x100000) return false;
        starts[6] = references.EcamStart; lengths[6] = 0x100000; allocations = 7;
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE
        if (references.VirtioBarStart < 0x100000 || (references.VirtioBarStart & 0x3fff) != 0 ||
            references.VirtioBarStart >= LowEnd - 0x4000) return false;
        starts[7] = references.VirtioBarStart; lengths[7] = 0x4000; allocations = 8;
#endif
        for (int i = 0; i < allocations; i++) for (int j = 0; j < i; j++)
            if (FirstExitDescriptors.Overlaps(starts[i], lengths[i], starts[j], lengths[j])) return false;

        if (!FirstExitOwnedArena.WriteMappingRanges(ref arena, pending + count,
            MaximumRanges - count, out ulong arenaCount)) return false;
        count += arenaCount;
        pending[count++] = new SeparatePageFaultMappingRange((ulong)code, FirstExitCodeBundle.Bytes / 4096, Code);
        pending[count++] = allocator;
        if (references.RuntimeSupportCode != 0) pending[count++] = new SeparatePageFaultMappingRange(references.RuntimeSupportCode, 1, Code);
#if M05_PHYSICAL_MAPPING_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.MappingScratchStart, 512, Data);
#endif
#if M05_MONOTONIC_CLOCK_PROBE
        pending[count++] = new SeparatePageFaultMappingRange(references.HpetDevicePage, 1, Data, PageCachePolicy.Uncached);
#endif
#if M05_PCI_ENUMERATION_PROBE
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE
        // Only the dedicated quiescent test function at device 6/function 0 is
        // writable. The rest of the admitted one-bus ECAM window stays read-only.
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart, 48, PageProtection.Read, PageCachePolicy.Uncached);
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart + 0x30000, 1, Data, PageCachePolicy.Uncached);
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart + 0x31000, 207, PageProtection.Read, PageCachePolicy.Uncached);
#else
        pending[count++] = new SeparatePageFaultMappingRange(references.EcamStart, references.EcamPages, PageProtection.Read, PageCachePolicy.Uncached);
#endif
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE
        for (ulong page = 0; page < 4; page++)
            pending[count++] = new SeparatePageFaultMappingRange(references.VirtioBarStart + page * 4096, 1, Data, PageCachePolicy.Uncached);
#endif
        ulong totalPages = 0;
        for (ulong i = 0; i < count; i++) totalPages += pending[i].PageCount;
        ulong outputBytes = count * (ulong)sizeof(SeparatePageFaultMappingRange);
        if (capacity < count || totalPages > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedMappedPages ||
            (ulong)ranges >= LowEnd - outputBytes) return false;
        for (int i = 0; i < allocations; i++)
        {
            if (i == 1 && FirstExitOwnedArena.InMainStack(arena, (ulong)ranges, outputBytes)) continue;
            if (FirstExitDescriptors.Overlaps((ulong)ranges, outputBytes, starts[i], lengths[i])) return false;
        }
        // Commit only after all declarations and aliases have passed. Caller may
        // build/audit from this scratch output, then discard it before entry.
        for (ulong i = 0; i < count; i++) ranges[i] = pending[i];
        written = count;
        return true;
    }
}

/// <summary>
/// Materialize and read back one three-page retained code allocation. Callers own
/// writable backing during preparation and must separately publish it RX, retain
/// its mappings and validate native stack budgets. No generated function is called
/// here. Keep the original arena/context and loaded image stable and exclusive.
/// </summary>
public static unsafe class FirstExitCodeBundle
{
    public const uint Bytes = 3 * 4096;
    public const uint FaultOffset = 4096;
    public const uint Out8Offset = 4352;
    public const uint In8Offset = 4368;
#if M05_MONOTONIC_CLOCK_PROBE
    public const uint ClockAccessOffset = 4608;
#endif
#if M05_PCI_ENUMERATION_PROBE
    public const uint PciReadOffset = 4608;
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE
    public const uint PciBarOffset = 4624;
#if M05_VIRTQUEUE_POLLING_PROBE
    public const uint QueueFenceOffset = 4672;
#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE
    public const uint MappingPublishOffset = 4608;
    public const uint MappingReadMsrOffset = 4800;
    public const uint MappingWritePatOffset = 4832;
    public const uint MappingReadCapabilitiesOffset = 5120;
#endif
    public const uint StackEntryOffset = 8192;
    public const uint PermanentOffset = 8448;
    private const ulong LowEnd = 0x0000800000000000;

    public static bool Materialize(byte* code, ulong capacity, ref FirstExitOwnedArenaState arena,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, ulong callback)
    {
        if (!Admit(code, capacity, ref arena, capture, mapping, callback, 0)) return false;
        for (uint index = 0; index < Bytes; index++) code[index] = 0;
        bool emitted = NormalizeErrorCodeNonErrorCodeInterruptFrame.Materialize(
                code, 4096, (ulong)code, (ulong)code + FaultOffset).Succeeded &&
            FirstExitTerminalFaultStub.Emit(code + FaultOffset, Out8Offset - FaultOffset,
                (ulong)arena.Fault, callback, out _) &&
            PortIoStubPlan.Out8.Emit(code + Out8Offset, PortIoStubPlan.Out8RequiredByteLength).Succeeded &&
            PortIoStubPlan.In8.Emit(code + In8Offset, PortIoStubPlan.In8RequiredByteLength).Succeeded &&
            FirstExitOwnedStackStub.Emit(code + StackEntryOffset, PermanentOffset - StackEntryOffset, out _) &&
            FirstExitPermanentCr3Stub.Emit(code + PermanentOffset, Bytes - PermanentOffset, out _) &&
#if M05_MONOTONIC_CLOCK_PROBE
            X64OwnedClockAccess.Emit(code + ClockAccessOffset, StackEntryOffset - ClockAccessOffset) &&
#endif
#if M05_PCI_ENUMERATION_PROBE
            OS.Drivers.X64PciConfigurationRead.Emit(code + PciReadOffset, StackEntryOffset - PciReadOffset) &&
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE
            OS.Drivers.X64PciBarAccess.Emit(code + PciBarOffset, StackEntryOffset - PciBarOffset) &&
#if M05_VIRTQUEUE_POLLING_PROBE
            OS.Drivers.X64VirtqueueMemoryFence.Emit(code + QueueFenceOffset, StackEntryOffset - QueueFenceOffset) &&
#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE
            X64PhysicalMappingPublication.Emit(code + MappingPublishOffset, MappingReadMsrOffset - MappingPublishOffset) &&
            X64PhysicalMappingPublication.EmitReadMsr(code + MappingReadMsrOffset, MappingWritePatOffset - MappingReadMsrOffset) &&
            X64PhysicalMappingPublication.EmitWritePat(code + MappingWritePatOffset, MappingReadCapabilitiesOffset - MappingWritePatOffset) &&
            X64PhysicalMappingPublication.EmitReadCapabilities(code + MappingReadCapabilitiesOffset, StackEntryOffset - MappingReadCapabilitiesOffset) &&
#endif
            CheckBytes(code, (ulong)arena.Fault, callback);
        if (!emitted)
        {
            arena.Fault->Phase = 4;
            return false;
        }
        // Only the actual read-back-checked canonical port stub bytes authorize
        // this binding. Publication/execution authority is still the adapter's job.
        var binding = InvokeIn8Out8CheckedUnmanagedFunctionPointers.Bind(
            CheckedPortIoFunctionPointerBindingRequest.Create(true, Com1OwnedPortIoBackend.BasePort,
                (nuint)(code + Out8Offset), (nuint)(code + In8Offset + PortIoStubPlan.In8RequiredByteLength),
                PortIoStubPlan.Out8RequiredByteLength + PortIoStubPlan.In8RequiredByteLength,
                0, In8Offset - Out8Offset, (nuint)(code + Out8Offset), (nuint)(code + In8Offset),
                PortIoStubPlan.Out8RequiredByteLength, PortIoStubPlan.In8RequiredByteLength,
                PortIoStubPlan.Out8RequiredByteLength, PortIoStubPlan.In8RequiredByteLength));
        if (!FirstExitTerminalFaultStub.Prepare(ref *arena.Fault, new Com1OwnedPortIoBackend(binding)))
        {
            arena.Fault->Phase = 4;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Check the complete byte layout before publication/entry, including padding
    /// and embedded context/callback addresses. Original data must remain unchanged;
    /// this is not an allocation certificate or a live permission observation.
    /// </summary>
    public static bool ValidateReadBack(byte* code, ulong capacity, ref FirstExitOwnedArenaState arena,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, ulong callback)
        => Admit(code, capacity, ref arena, capture, mapping, callback, 1) &&
            arena.Fault->Backend.IsReady && CheckBytes(code, (ulong)arena.Fault, callback);

    public static bool ValidateInstalledReadBack(byte* code, ulong capacity, ref FirstExitOwnedArenaState arena,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, ulong callback)
        => Admit(code, capacity, ref arena, capture, mapping, callback, 1, true) &&
            arena.Fault->Backend.IsReady && CheckBytes(code, (ulong)arena.Fault, callback);

    private static bool Admit(byte* code, ulong capacity, ref FirstExitOwnedArenaState arena,
        EfiLoadedImageRangeCapturePlan capture, M01LoadedImageVirtualMappingEvidence mapping, ulong callback, uint phase, bool installed = false)
    {
        ulong address = (ulong)code, data = (ulong)arena.Storage;
        if (capacity != Bytes || address < 4096 || (address & 4095) != 0 || address >= LowEnd - Bytes ||
            !arena.IsReady || data < 4096 || (data & 4095) != 0 || data >= LowEnd - FirstExitOwnedArena.Bytes ||
            FirstExitDescriptors.Overlaps(address, Bytes, data, FirstExitOwnedArena.Bytes)) return false;
        fixed (FirstExitOwnedArenaState* state = &arena)
            if (FirstExitDescriptors.Overlaps(address, Bytes, (ulong)state, (ulong)sizeof(FirstExitOwnedArenaState)) ||
                FirstExitDescriptors.Overlaps(data, FirstExitOwnedArena.Bytes, (ulong)state, (ulong)sizeof(FirstExitOwnedArenaState))) return false;
        SeparatePageFaultMappingRange* imageRanges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitImageMappings.MaximumSections + 1];
        if (!FirstExitImageMappings.WriteRanges(capture, mapping, imageRanges, FirstExitImageMappings.MaximumSections + 1, out ulong count) ||
            !FirstExitImageMappings.Contains(imageRanges, count, callback, 1, PageProtection.Read | PageProtection.Execute) ||
            FirstExitDescriptors.Overlaps(address, Bytes, imageRanges[0].Start, capture.Range.ImageSize) ||
            FirstExitDescriptors.Overlaps(data, FirstExitOwnedArena.Bytes, imageRanges[0].Start, capture.Range.ImageSize) ||
            !(installed ? FirstExitOwnedArena.ValidateInstalledDescriptors(ref arena, address) : FirstExitOwnedArena.ValidateDescriptors(ref arena, address)) ||
            arena.Fault->Phase != phase || arena.Fault->Reserved != 0) return false;
        return FirstExitTerminalFaultStub.RequiredBytes > 0 && FirstExitTerminalFaultStub.RequiredBytes <= Out8Offset - FaultOffset &&
            FirstExitOwnedStackStub.RequiredBytes > 0 && FirstExitOwnedStackStub.RequiredBytes <= PermanentOffset - StackEntryOffset &&
            FirstExitPermanentCr3Stub.RequiredBytes > 0 && FirstExitPermanentCr3Stub.RequiredBytes <= 1024;
    }

    private static bool CheckBytes(byte* code, ulong context, ulong callback)
    {
        if (!NormalizeErrorCodeNonErrorCodeInterruptFrame.ValidateReadBack(
            code, 4096, (ulong)code, (ulong)code + FaultOffset).Succeeded) return false;
        byte* scratch = stackalloc byte[1024];
        if (!FirstExitTerminalFaultStub.Emit(scratch, 1024, context, callback, out uint faultBytes) ||
            !Equal(scratch, code + FaultOffset, faultBytes) ||
            !PortIoStubPlan.Out8.Emit(scratch, PortIoStubPlan.Out8RequiredByteLength).Succeeded ||
            !Equal(scratch, code + Out8Offset, PortIoStubPlan.Out8RequiredByteLength) ||
            !PortIoStubPlan.In8.Emit(scratch, PortIoStubPlan.In8RequiredByteLength).Succeeded ||
            !Equal(scratch, code + In8Offset, PortIoStubPlan.In8RequiredByteLength) ||
            !FirstExitOwnedStackStub.Emit(scratch, 1024, out uint entryBytes) ||
            !Equal(scratch, code + StackEntryOffset, entryBytes) ||
            !FirstExitPermanentCr3Stub.Emit(scratch, 1024, out uint permanentBytes) ||
            !Equal(scratch, code + PermanentOffset, permanentBytes)) return false;
#if M05_MONOTONIC_CLOCK_PROBE
        if (!X64OwnedClockAccess.Validate(code + ClockAccessOffset, X64OwnedClockAccess.Bytes)) return false;
#endif
#if M05_PCI_ENUMERATION_PROBE
        if (!OS.Drivers.X64PciConfigurationRead.Validate(code + PciReadOffset, OS.Drivers.X64PciConfigurationRead.Bytes)) return false;
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE
        if (!OS.Drivers.X64PciBarAccess.Validate(code + PciBarOffset, OS.Drivers.X64PciBarAccess.Bytes)) return false;
#if M05_VIRTQUEUE_POLLING_PROBE
        if (!OS.Drivers.X64VirtqueueMemoryFence.Validate(code + QueueFenceOffset, OS.Drivers.X64VirtqueueMemoryFence.Bytes)) return false;
#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE
        if (!X64PhysicalMappingPublication.ValidateReadBack(code + MappingPublishOffset, X64PhysicalMappingPublication.RequiredBytes) ||
            !X64PhysicalMappingPublication.EmitReadMsr(scratch, 1024) ||
            !Equal(scratch, code + MappingReadMsrOffset, X64PhysicalMappingPublication.ReadMsrBytes) ||
            !X64PhysicalMappingPublication.ValidateWritePatReadBack(code + MappingWritePatOffset, X64PhysicalMappingPublication.WritePatBytes) ||
            !X64PhysicalMappingPublication.EmitReadCapabilities(scratch, 1024) ||
            !Equal(scratch, code + MappingReadCapabilitiesOffset, X64PhysicalMappingPublication.ReadCapabilitiesBytes)) return false;
#endif
        for (uint index = 4096; index < Bytes; index++)
            if (!Inside(index, FaultOffset, faultBytes) && !Inside(index, Out8Offset, PortIoStubPlan.Out8RequiredByteLength) &&
                !Inside(index, In8Offset, PortIoStubPlan.In8RequiredByteLength) && !Inside(index, StackEntryOffset, entryBytes) &&
#if M05_MONOTONIC_CLOCK_PROBE
                !Inside(index, ClockAccessOffset, X64OwnedClockAccess.Bytes) &&
#endif
#if M05_PCI_ENUMERATION_PROBE
                !Inside(index, PciReadOffset, OS.Drivers.X64PciConfigurationRead.Bytes) &&
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE
                !Inside(index, PciBarOffset, OS.Drivers.X64PciBarAccess.Bytes) &&
#if M05_VIRTQUEUE_POLLING_PROBE
                !Inside(index, QueueFenceOffset, OS.Drivers.X64VirtqueueMemoryFence.Bytes) &&
#endif
#endif
#endif
#if M05_PHYSICAL_MAPPING_PROBE
                !Inside(index, MappingPublishOffset, X64PhysicalMappingPublication.RequiredBytes) &&
                !Inside(index, MappingReadMsrOffset, X64PhysicalMappingPublication.ReadMsrBytes) &&
                !Inside(index, MappingWritePatOffset, X64PhysicalMappingPublication.WritePatBytes) &&
                !Inside(index, MappingReadCapabilitiesOffset, X64PhysicalMappingPublication.ReadCapabilitiesBytes) &&
#endif
                !Inside(index, PermanentOffset, permanentBytes) && code[index] != 0) return false;
        return true;
    }

    private static bool Inside(uint offset, uint start, uint bytes) => offset >= start && offset - start < bytes;
    private static bool Equal(byte* left, byte* right, uint bytes)
    {
        for (uint index = 0; index < bytes; index++) if (left[index] != right[index]) return false;
        return true;
    }
}

// Stack layout after the existing normalizer and the terminal stub's 15 pushes.
// The handler never restores registers or returns through this frame.
public struct FirstExitFaultFrame
{
    public ulong R15, R14, R13, R12, R11, R10, R9, R8;
    public ulong Rbp, Rdi, Rsi, Rdx, Rcx, Rbx, Rax;
    public ulong Vector, HasErrorCode, ErrorCode, Rip, Cs, Rflags, Rsp, Ss;
}

public struct FirstExitFaultContext
{
    // 0 empty, 1 ready, 2 entered by native stub, 3 reported, 4 refused/failed.
    public uint Phase;
    public uint Reserved;
    public Com1OwnedPortIoBackend Backend;
}

/// <summary>
/// Terminal adapter for the existing normalized interrupt vector page. Keep the
/// context, callback, all code and stacks retained/accessible under both roots.
/// The callback must be an authenticated owned Win64 target, never firmware.
/// This emitter does not publish code, install gates or prove a native stack budget.
/// </summary>
public static unsafe class FirstExitTerminalFaultStub
{
    public const uint FrameBytes = 184;
    public const uint MaximumCallBytesBelowFrame = 55; // alignment + shadow space + return address
    private const ulong LowEnd = 0x0000800000000000;
    public static uint RequiredBytes => Encode(null, 0, 0, 0);

    public static bool Prepare(ref FirstExitFaultContext context, Com1OwnedPortIoBackend backend)
    {
        if (context.Phase != 0 || context.Reserved != 0 || !backend.IsReady ||
            sizeof(FirstExitFaultFrame) != FrameBytes) return false;
        context.Backend = backend;
        context.Phase = 1;
        return true;
    }

    public static bool Emit(byte* destination, uint capacity, ulong context, ulong callback, out uint written)
    {
        written = 0;
        uint bytes = RequiredBytes;
        if (destination == null || capacity < bytes || (ulong)destination >= LowEnd - bytes ||
            context == 0 || (context & 7) != 0 || context >= LowEnd - (ulong)sizeof(FirstExitFaultContext) ||
            callback == 0 || callback >= LowEnd ||
            FirstExitDescriptors.Overlaps((ulong)destination, bytes, context, (ulong)sizeof(FirstExitFaultContext)) ||
            FirstExitDescriptors.Overlaps(callback, 1, context, (ulong)sizeof(FirstExitFaultContext)) ||
            (callback >= (ulong)destination && callback - (ulong)destination < bytes)) return false;
        written = Encode(destination, context, callback, bytes - 4);
        return written == bytes;
    }

    /// <summary>
    /// Called by the authenticated EFI callback, after the native one-use guard.
    /// Reports through the existing checked owned COM1 backend only. The native
    /// caller halts after any return, including a reporting failure or timeout.
    /// </summary>
    public static bool Report(FirstExitFaultFrame* frame, FirstExitFaultContext* context, ulong cr2, ulong cr3)
    {
        if (frame == null || context == null || ((ulong)frame & 7) != 0 || ((ulong)context & 7) != 0 ||
            (ulong)frame >= LowEnd - FrameBytes || (ulong)context >= LowEnd - (ulong)sizeof(FirstExitFaultContext) ||
            FirstExitDescriptors.Overlaps((ulong)frame, FrameBytes, (ulong)context, (ulong)sizeof(FirstExitFaultContext)) ||
            context->Phase != 2 || context->Reserved != 0) return false;
        context->Phase = 4; // consume even if reporting fails; no second callback/report
        if (frame->Vector > 255 || frame->HasErrorCode > 1 ||
            (frame->HasErrorCode == 1) != NormalizeErrorCodeNonErrorCodeInterruptFrame.HasHardwareErrorCode((int)frame->Vector) ||
            (frame->HasErrorCode == 0 && frame->ErrorCode != 0)) return false;
        PanicSafeOwnedCom1FaultRecord fault = new((int)frame->Vector, frame->HasErrorCode == 1,
            frame->ErrorCode, frame->Rip, frame->Rsp, frame->Rbp, frame->Rflags, cr2, cr3,
            frame->Rax, frame->Rbx, frame->Rcx, frame->Rdx, frame->Rsi, frame->Rdi,
            frame->R8, frame->R9, frame->R10, frame->R11, frame->R12, frame->R13, frame->R14, frame->R15);
        byte* subsystem = stackalloc byte[4]; *(uint*)subsystem = 0x746f6f62; // boot
        byte* reason = stackalloc byte[10]; *(ulong*)reason = 0x7561662d74697865; *(ushort*)(reason + 8) = 0x746c; // exit-fault
        byte* scratch = stackalloc byte[2048];
        PanicSafeOwnedCom1ReportResult report = PanicSafeFormattingSerialReportingOwnedCom1.ReportFaultSummary(
            context->Backend, scratch, 2048, subsystem, 4, reason, 10, in fault);
        if (!report.Succeeded) return false;
        context->Phase = 3;
        return true;
    }

    private static uint Encode(byte* destination, ulong context, ulong callback, uint halt)
    {
        uint p = 0;
        Op(destination, ref p, 0xfcfa, 2); // cli; cld
        Op(destination, ref p, 0x55575652515350, 7); // save rax,rbx,rcx,rdx,rsi,rdi,rbp
        Op(destination, ref p, 0x5341524151415041, 8); // r8..r11
        Op(destination, ref p, 0x5741564155415441, 8); // r12..r15
        Op(destination, ref p, 0xe18948, 3); // rcx = normalized saved frame
        Op(destination, ref p, 0xba48, 2); Op(destination, ref p, context, 8);
        Op(destination, ref p, 0x013a83, 3); // only the original ready context may enter
        Op(destination, ref p, 0x850f, 2); Op(destination, ref p, halt - (p + 4), 4);
        Op(destination, ref p, 0x0000000202c7, 6); // phase = entered
        Op(destination, ref p, 0xd0200f41, 4); // r8 = cr2
        Op(destination, ref p, 0xd9200f41, 4); // r9 = cr3
        Op(destination, ref p, 0xf0e48348, 4); // align Win64 stack
        Op(destination, ref p, 0x20ec8348, 4); // 32-byte shadow space
        Op(destination, ref p, 0xb848, 2); Op(destination, ref p, callback, 8);
        Op(destination, ref p, 0xd0ff, 2); // call owned callback
        Op(destination, ref p, 0xfdebf4fa, 4); // every return/refusal is terminal
        return p;
    }

    private static void Op(byte* destination, ref uint offset, ulong value, int bytes)
    {
        for (int index = 0; index < bytes; index++)
        {
            if (destination != null) destination[offset] = (byte)(value >> (index * 8));
            offset++;
        }
    }
}

/// <summary>
/// Derive retained identity ranges from the original loaded x64 EFI PE image.
/// Capture/mapping provenance and readable, stable backing remain caller obligations;
/// this validates supplied PE structure, not the loader or actual CPU permissions.
/// Keep headers RO/NX, code RX and writable data RW/NX, including static allocator
/// metadata and copied ACPI buffers within those data sections. Gaps remain absent.
/// Separate runtime allocations still require their own retained ranges.
/// </summary>
public static unsafe class FirstExitImageMappings
{
    public const ulong MaximumSections = 16;
    private const ulong Page = 4096;
    private const ulong LowEnd = 0x0000800000000000;
    private const uint Read = 0x40000000, Write = 0x80000000, Execute = 0x20000000;

    public static bool WriteRanges(EfiLoadedImageRangeCapturePlan capture,
        M01LoadedImageVirtualMappingEvidence mapping,
        SeparatePageFaultMappingRange* ranges, ulong capacity, out ulong written)
    {
        written = 0;
        ulong bytes = capture.Range.ImageSize;
        if (sizeof(void*) != 8 || !capture.Succeeded || !mapping.IsValidFor(capture) ||
            mapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong address) != AddressValueStatus.Success ||
            address < Page || (address & (Page - 1)) != 0 || bytes < Page ||
            bytes > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedMappedPages * Page ||
            (bytes & (Page - 1)) != 0 || address >= LowEnd - bytes) return false;
        byte* image = (byte*)address;
        if (*(ushort*)image != 0x5a4d) return false;
        ulong nt = *(uint*)(image + 60);
        if (nt < 64 || nt > bytes - 24) return false;
        byte* pe = image + nt;
        ulong optionalBytes = *(ushort*)(pe + 20), sections = *(ushort*)(pe + 6);
        if (*(uint*)pe != 0x00004550 || *(ushort*)(pe + 4) != 0x8664 ||
            (*(ushort*)(pe + 22) & 2) == 0 || sections == 0 || sections > MaximumSections ||
            optionalBytes < 112 || optionalBytes > bytes - nt - 24) return false;
        byte* optional = pe + 24;
        ulong directories = *(uint*)(optional + 108), fileAlignment = *(uint*)(optional + 36);
        if (*(ushort*)optional != 0x20b || *(uint*)(optional + 32) != Page ||
            *(uint*)(optional + 56) != bytes || *(ushort*)(optional + 68) != 10 ||
            fileAlignment < 512 || fileAlignment > Page || (fileAlignment & (fileAlignment - 1)) != 0 ||
            directories > 16 || 112 + directories * 8 > optionalBytes) return false;
        ulong table = nt + 24 + optionalBytes, headers = *(uint*)(optional + 60);
        if (table > bytes || sections * 40 > bytes - table || headers < table + sections * 40 ||
            headers > bytes || (headers & (fileAlignment - 1)) != 0) return false;
        ulong headerPages = (headers + Page - 1) / Page;
        if (headerPages > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRangePages) return false;
        ulong priorEnd = headerPages * Page, mappedPages = headerPages;
        ulong entry = *(uint*)(optional + 16);
        bool entryCovered = false;
        for (ulong index = 0; index < sections; index++)
        {
            byte* section = image + table + index * 40;
            ulong virtualBytes = *(uint*)(section + 8), start = *(uint*)(section + 12);
            ulong rawBytes = *(uint*)(section + 16);
            uint flags = *(uint*)(section + 36);
            ulong extent = virtualBytes > rawBytes ? virtualBytes : rawBytes;
            if (virtualBytes == 0 || (rawBytes & (fileAlignment - 1)) != 0 ||
                (start & (Page - 1)) != 0 || start < priorEnd || start > bytes ||
                extent > bytes - start || (flags & Read) == 0 ||
                (flags & (Write | Execute)) == (Write | Execute) ||
                (flags & 0x04000000) != 0) return false; // no noncached section policy
            ulong pages = (extent + Page - 1) / Page;
            if (pages > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRangePages ||
                pages * Page > bytes - start) return false;
            priorEnd = start + pages * Page;
            mappedPages += pages;
            if ((flags & Execute) != 0 && entry >= start && entry - start < virtualBytes)
                entryCovered = true;
        }
        if (!entryCovered || mappedPages > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedMappedPages ||
            ranges == null || ((ulong)ranges & 7) != 0 || capacity < sections + 1 ||
            capacity > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRanges) return false;
        ulong outputBytes = (sections + 1) * (ulong)sizeof(SeparatePageFaultMappingRange);
        if ((ulong)ranges >= LowEnd - outputBytes ||
            FirstExitDescriptors.Overlaps((ulong)ranges, outputBytes, address, bytes)) return false;

        // All admission checks precede output writes. The original image must
        // remain stable for both passes; this method does not grant lifetime authority.
        ranges[0] = new SeparatePageFaultMappingRange(address, headerPages, PageProtection.Read);
        for (ulong index = 0; index < sections; index++)
        {
            byte* section = image + table + index * 40;
            ulong extent = *(uint*)(section + 8), rawBytes = *(uint*)(section + 16);
            if (rawBytes > extent) extent = rawBytes;
            uint flags = *(uint*)(section + 36);
            PageProtection protection = PageProtection.Read;
            if ((flags & Write) != 0) protection |= PageProtection.Write;
            if ((flags & Execute) != 0) protection |= PageProtection.Execute;
            ranges[index + 1] = new SeparatePageFaultMappingRange(address + *(uint*)(section + 12),
                (extent + Page - 1) / Page, protection);
        }
        written = sections + 1;
        return true;
    }

    /// <summary>
    /// Check a required object/function interval against a previously validated,
    /// stable range list. Numeric containment is not allocation or execution authority.
    /// </summary>
    public static bool Contains(SeparatePageFaultMappingRange* ranges, ulong count,
        ulong address, ulong bytes, PageProtection protection)
    {
        if (ranges == null || ((ulong)ranges & 7) != 0 || count == 0 ||
            count > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRanges ||
            (ulong)ranges >= LowEnd - count * (ulong)sizeof(SeparatePageFaultMappingRange) ||
            address == 0 || bytes == 0 || bytes >= LowEnd || address >= LowEnd - bytes) return false;
        for (ulong index = 0; index < count; index++)
        {
            SeparatePageFaultMappingRange range = ranges[index];
            if (!range.IsInitialized || range.PageCount == 0 ||
                range.PageCount > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRangePages) return false;
            ulong length = range.PageCount * Page;
            if (range.Protection == protection && address >= range.Start &&
                address - range.Start < length && bytes <= length - (address - range.Start)) return true;
        }
        return false;
    }
}

public enum FirstExitBindingStatus
{
    NotStarted, Ready, Consumed, AlreadyBound, NotReady, FirmwareUnavailable, InvalidStorage,
    InvalidMap, InvalidTable, WrongBootServicesTable, InvalidFunction, NotFirmwareCode,
    Changed
}

public unsafe struct FirstExitFirmwareBindingState
{
    internal uint Phase;
    internal byte* SystemTable;
    internal byte* BootServices;
    internal ulong SystemBytes, BootBytes, MapFunction, ExitFunction;
    internal uint SystemCrc, BootCrc;
    internal void* ImageHandle;
    public bool IsReady => Phase == 1;
    public bool IsConsumed => Phase == 2;
    public bool RetainedPointersCleared => SystemTable == null && BootServices == null &&
        MapFunction == 0 && ExitFunction == 0 && ImageHandle == null;
}

/// <summary>
/// Bind the two entries from the original x64 EFI tables, never caller-supplied
/// replacement callbacks. The adapter supplies the actual EfiMain table/handle,
/// valid readable extents and an owned firmware-map snapshot. Table CRCs detect
/// corruption, not malicious firmware or forged provenance. Memory-map code
/// classification is not a page-table execute-permission check. Actual firmware
/// CR3/mapping authority remains the adapter's obligation before the final call.
/// No firmware function is called here. Consume rechecks table identity/content
/// before the final window and clears this binding before exposing its entries.
/// Retain one original state exclusively; do not copy/reset it or alias outputs.
/// </summary>
public static unsafe class FirstExitFirmwareBinding
{
    private const ulong LowEnd = 0x0000800000000000;
    private const ulong SystemSignature = 0x5453595320494249;
    private const ulong BootSignature = 0x56524553544f4f42;

    public static FirstExitBindingStatus Bind(ref FirstExitFirmwareBindingState state,
        FirmwareServicesMode mode, void* imageHandle, byte* systemTable, ulong systemBytes,
        byte* bootServices, ulong bootBytes, FirmwareMemoryMapRecord ownedMap)
    {
        if (state.Phase != 0) return FirstExitBindingStatus.AlreadyBound;
        if (mode != FirmwareServicesMode.BootServicesAvailable) return FirstExitBindingStatus.FirmwareUnavailable;
        if (imageHandle == null || !Extent((ulong)systemTable, systemBytes) || systemBytes < 120 || systemBytes > 4096 ||
            !Extent((ulong)bootServices, bootBytes) || bootBytes < 240 || bootBytes > 4096 ||
            ((ulong)systemTable & 7) != 0 || ((ulong)bootServices & 7) != 0 ||
            FirstExitDescriptors.Overlaps((ulong)systemTable, systemBytes, (ulong)bootServices, bootBytes))
            return FirstExitBindingStatus.InvalidStorage;
        if (!ownedMap.HasOwnedCopy() || ownedMap.DescriptorVersion != 1 ||
            !UefiMemoryMapCopyPlanResult.Create(ownedMap, ownedMap.MapSize).Succeeded ||
            !Extent(ownedMap.BufferAddress, ownedMap.MapSize)) return FirstExitBindingStatus.InvalidMap;
        fixed (FirstExitFirmwareBindingState* address = &state)
        {
            ulong start = (ulong)address, bytes = (ulong)sizeof(FirstExitFirmwareBindingState);
            if (!Extent(start, bytes) || FirstExitDescriptors.Overlaps(start, bytes, (ulong)systemTable, systemBytes) ||
                FirstExitDescriptors.Overlaps(start, bytes, (ulong)bootServices, bootBytes) ||
                FirstExitDescriptors.Overlaps(start, bytes, ownedMap.BufferAddress, ownedMap.MapSize) ||
                FirstExitDescriptors.Overlaps(ownedMap.BufferAddress, ownedMap.MapSize, (ulong)systemTable, systemBytes) ||
                FirstExitDescriptors.Overlaps(ownedMap.BufferAddress, ownedMap.MapSize, (ulong)bootServices, bootBytes))
                return FirstExitBindingStatus.InvalidStorage;
        }
        if (!Header(systemTable, systemBytes, 120, SystemSignature) ||
            !Header(bootServices, bootBytes, 240, BootSignature)) return FirstExitBindingStatus.InvalidTable;
        if (*(ulong*)(systemTable + 96) != (ulong)bootServices) return FirstExitBindingStatus.WrongBootServicesTable;
        ulong map = *(ulong*)(bootServices + 56), exit = *(ulong*)(bootServices + 232);
        if (!Extent(map, 1) || !Extent(exit, 1) || map == exit) return FirstExitBindingStatus.InvalidFunction;

        uint mapMatches = 0, exitMatches = 0;
        bool mapCode = false, exitCode = false;
        for (ulong i = 0; i < ownedMap.DescriptorCount; i++)
        {
            byte* descriptor = (byte*)(ownedMap.BufferAddress + i * ownedMap.DescriptorSize);
            ulong start = *(ulong*)(descriptor + 8), pages = *(ulong*)(descriptor + 24);
            if ((start & 4095) != 0 || pages == 0 || pages > (UefiMemoryMapCopyPlan.MaximumUnsignedLong - start) / 4096)
                return FirstExitBindingStatus.InvalidMap;
            ulong end = start + pages * 4096;
            uint type = *(uint*)descriptor;
            bool code = type == 3 || type == 5; // EfiBootServicesCode / EfiRuntimeServicesCode
            if (start <= map && map < end) { mapMatches++; mapCode = code; }
            if (start <= exit && exit < end) { exitMatches++; exitCode = code; }
        }
        if (mapMatches != 1 || exitMatches != 1 || !mapCode || !exitCode) return FirstExitBindingStatus.NotFirmwareCode;
        state.SystemTable = systemTable; state.SystemBytes = systemBytes;
        state.BootServices = bootServices; state.BootBytes = bootBytes;
        state.SystemCrc = *(uint*)(systemTable + 16); state.BootCrc = *(uint*)(bootServices + 16);
        state.MapFunction = map; state.ExitFunction = exit; state.ImageHandle = imageHandle;
        state.Phase = 1;
        return FirstExitBindingStatus.Ready;
    }

    public static FirstExitBindingStatus Consume(ref FirstExitFirmwareBindingState state,
        FirmwareServicesMode mode,
        out delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap,
        out delegate* unmanaged<void*, ulong, ulong> exitBootServices, out void* imageHandle)
    {
        getMemoryMap = null; exitBootServices = null; imageHandle = null;
        if (!state.IsReady) return FirstExitBindingStatus.NotReady;
        // Do not dereference any firmware storage when services are unavailable.
        if (mode != FirmwareServicesMode.BootServicesAvailable)
        {
            state = default;
            state.Phase = 2;
            return FirstExitBindingStatus.FirmwareUnavailable;
        }
        bool unchanged = Header(state.SystemTable, state.SystemBytes, 120, SystemSignature) &&
            Header(state.BootServices, state.BootBytes, 240, BootSignature) &&
            *(uint*)(state.SystemTable + 16) == state.SystemCrc && *(uint*)(state.BootServices + 16) == state.BootCrc &&
            *(ulong*)(state.SystemTable + 96) == (ulong)state.BootServices &&
            *(ulong*)(state.BootServices + 56) == state.MapFunction && *(ulong*)(state.BootServices + 232) == state.ExitFunction;
        if (unchanged)
        {
            getMemoryMap = (delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong>)state.MapFunction;
            exitBootServices = (delegate* unmanaged<void*, ulong, ulong>)state.ExitFunction;
            imageHandle = state.ImageHandle;
        }
        state = default;
        state.Phase = 2;
        return unchanged ? FirstExitBindingStatus.Consumed : FirstExitBindingStatus.Changed;
    }

    private static bool Header(byte* table, ulong capacity, uint minimum, ulong signature)
    {
        uint size = *(uint*)(table + 12);
        if (*(ulong*)table != signature || *(uint*)(table + 8) < 0x20000 ||
            size < minimum || size > capacity || *(uint*)(table + 20) != 0) return false;
        uint crc = Crc32.InitialState;
        for (uint i = 0; i < size; i++) crc = Crc32.UpdateByte(crc, i >= 16 && i < 20 ? (byte)0 : table[i]);
        return Crc32.Finish(crc) == *(uint*)(table + 16);
    }

    private static bool Extent(ulong start, ulong bytes)
        => start != 0 && start < LowEnd && bytes != 0 && bytes < LowEnd - start;
}

// Native entry ABI. Keep one original context, exclusively owned and immutable
// after preparation. Entry receives this pointer and reads its EntryContext.
public unsafe struct FirstExitOwnedStackContext
{
    public ulong FirmwareCr3, FirmwareCr4, StackBottom, StackTop;
    public ulong LowerCanary, UpperCanary;
    public delegate* unmanaged<FirstExitOwnedStackContext*, void> Entry;
    public void* EntryContext;
    public ulong EntryStackBytes, ObservedRsp;
    public uint Phase;
    internal uint Reserved;
}

/// <summary>
/// Enter the final firmware-call coordinator on its retained owned stack, under
/// unchanged firmware CR3/CR4 and enabled interrupts. Refuse entry if IF is clear.
/// Never return to the
/// firmware caller, even when entry refuses/fails or unexpectedly returns.
/// The caller prepares valid mappings for both stacks, context and all reachable
/// code/data, authenticates Entry, publishes the emitted instructions and proves
/// EntryStackBytes covers its complete path (including firmware calls). This
/// emitter does not allocate, map pages or establish those authority claims.
/// </summary>
public static unsafe class FirstExitOwnedStackStub
{
    public const uint ContextBytes = 88;
    public const ulong CallStackBytes = 40; // Win64 shadow space plus return address
    private const ulong LowEnd = 0x0000800000000000;

    public static bool Prepare(ref FirstExitOwnedStackContext context,
        ulong firmwareCr3, ulong firmwareCr4, ulong stackBottom, ulong stackTop,
        ulong lowerCanary, ulong upperCanary, ulong entryStackBytes,
        delegate* unmanaged<FirstExitOwnedStackContext*, void> entry,
        void* entryContext, ulong entryContextBytes)
    {
        if (context.Phase != 0 || sizeof(FirstExitOwnedStackContext) != ContextBytes ||
            (firmwareCr3 & ~0x18UL) == 0 || firmwareCr3 >= 0x100000000 || (firmwareCr3 & 0xfe7) != 0 ||
            (firmwareCr4 & ~0x7ffUL) != 0 || (firmwareCr4 & 0x20) == 0 ||
            stackBottom < 8 || stackTop >= LowEnd - 8 || stackTop <= stackBottom ||
            (stackBottom & 7) != 0 || (stackTop & 15) != 0 ||
            stackTop - stackBottom <= CallStackBytes || entryStackBytes == 0 ||
            entryStackBytes > stackTop - stackBottom - CallStackBytes ||
            lowerCanary == 0 || upperCanary == 0 || !Extent((ulong)entry, 1) ||
            !Extent((ulong)entryContext, entryContextBytes)) return false;

        fixed (FirstExitOwnedStackContext* address = &context)
        {
            ulong start = (ulong)address, stack = stackBottom - 8, bytes = stackTop - stackBottom + 16;
            if (!Extent(start, ContextBytes) ||
                FirstExitDescriptors.Overlaps(start, ContextBytes, stack, bytes) ||
                FirstExitDescriptors.Overlaps(start, ContextBytes, (ulong)entryContext, entryContextBytes) ||
                FirstExitDescriptors.Overlaps(stack, bytes, (ulong)entryContext, entryContextBytes) ||
                FirstExitDescriptors.Overlaps((ulong)entry, 1, start, ContextBytes) ||
                FirstExitDescriptors.Overlaps((ulong)entry, 1, stack, bytes) ||
                FirstExitDescriptors.Overlaps((ulong)entry, 1, (ulong)entryContext, entryContextBytes)) return false;
        }
        context = default;
        context.FirmwareCr3 = firmwareCr3; context.FirmwareCr4 = firmwareCr4;
        context.StackBottom = stackBottom; context.StackTop = stackTop;
        context.LowerCanary = lowerCanary; context.UpperCanary = upperCanary;
        context.Entry = entry; context.EntryContext = entryContext;
        context.EntryStackBytes = entryStackBytes; context.Phase = 1;
        return true;
    }

    public static uint RequiredBytes => Encode(null, 0);

    public static bool Emit(byte* destination, uint capacity, out uint written)
    {
        written = 0;
        uint bytes = RequiredBytes;
        if (destination == null || capacity < bytes || !Extent((ulong)destination, bytes)) return false;
        written = Encode(destination, bytes - 4);
        return true;
    }

    private static uint Encode(byte* destination, uint halt)
    {
        uint p = 0;
        Op(destination, ref p, 0xc98548, 3);           // test rcx,rcx
        StopIf(destination, ref p, halt, 0x84);
        Op(destination, ref p, 0x01507983, 4);         // cmp dword [rcx+80],Prepared
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x5a9c, 2);             // pushfq; pop rdx (original stack)
        Op(destination, ref p, 0x00000200c2f7, 6);     // test edx,IF; UEFI requires interrupts
        StopIf(destination, ref p, halt, 0x84);
        Op(destination, ref p, 0xd8200f, 3);           // mov rax,cr3
        Op(destination, ref p, 0x013b48, 3);           // cmp rax,[rcx]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0xe0200f, 3);           // mov rax,cr4
        Op(destination, ref p, 0x08413b48, 4);         // cmp rax,[rcx+8]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x10418b48, 4);         // mov rax,[rcx+16] (bottom)
        Op(destination, ref p, 0xf8408b48, 4);         // mov rax,[rax-8]
        Op(destination, ref p, 0x20413b48, 4);         // cmp rax,[rcx+32] (lower canary)
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x18418b48, 4);         // mov rax,[rcx+24] (top)
        Op(destination, ref p, 0x108b48, 3);           // mov rdx,[rax]
        Op(destination, ref p, 0x28513b48, 4);         // cmp rdx,[rcx+40] (upper canary)
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0xc28948, 3);           // mov rdx,rax
        Op(destination, ref p, 0x0fe283, 3);           // and edx,15
        StopIf(destination, ref p, halt, 0x85);        // jnz halt (unaligned top)
        Op(destination, ref p, 0x30418b48, 4);         // mov rax,[rcx+48] (entry)
        Op(destination, ref p, 0xc08548, 3);           // test rax,rax
        StopIf(destination, ref p, halt, 0x84);
        Op(destination, ref p, 0x000000025041c7, 7);   // mov dword [rcx+80],Consumed
        Op(destination, ref p, 0xfc, 1);               // cld; IF is unchanged
        Op(destination, ref p, 0x18618b48, 4);         // mov rsp,[rcx+24]
        Op(destination, ref p, 0x20ec8348, 4);         // sub rsp,32 (shadow space)
        Op(destination, ref p, 0xf824548d48, 5);       // lea rdx,[rsp-8] (callee RSP)
        Op(destination, ref p, 0x48518948, 4);         // mov [rcx+72],rdx
        Op(destination, ref p, 0xd0ff, 2);             // call rax, RCX is original context
        Op(destination, ref p, 0xfcebf4fa, 4);         // cli; hlt; jmp cli, never restore RSP
        return p;
    }

    private static bool Extent(ulong start, ulong bytes)
        => start != 0 && start < LowEnd && bytes != 0 && bytes < LowEnd - start;

    private static void StopIf(byte* destination, ref uint p, uint halt, byte condition)
    {
        Op(destination, ref p, 0x0f | ((ulong)condition << 8), 2);
        Op(destination, ref p, unchecked(halt - p - 4), 4);
    }

    private static void Op(byte* destination, ref uint p, ulong value, uint bytes)
    {
        for (uint i = 0; i < bytes; i++, p++)
            if (destination != null) destination[p] = (byte)(value >> (int)(i * 8));
    }
}

public unsafe struct FirstExitOwnedArenaState
{
    internal byte* Data;
    internal uint Phase;
    internal uint Reserved;
    public bool IsReady => Phase == 2;
    public bool FailedAfterWrite => Phase == 3;
    public byte* Storage => Data;
    public FirstExitDescriptorContext* Descriptors => IsReady ? (FirstExitDescriptorContext*)Data : null;
    public FirstExitOwnedStackContext* StackEntry => IsReady ? (FirstExitOwnedStackContext*)(Data + 128) : null;
    public FirstExitPermanentCr3Context* Permanent => IsReady ? (FirstExitPermanentCr3Context*)(Data + 256) : null;
    public FirstExitFirmwareBindingState* Binding => IsReady ? (FirstExitFirmwareBindingState*)(Data + 384) : null;
    public FirstExitTransitionState* Transition => IsReady ? (FirstExitTransitionState*)(Data + 512) : null;
    public FirstExitFaultContext* Fault => IsReady ? (FirstExitFaultContext*)(Data + 1024) : null;
    public byte* Map => IsReady ? Data + 5 * 4096 : null;
    public ulong StackBottom => IsReady ? (ulong)Data + 22 * 4096 + 8 : 0;
    public ulong StackTop => IsReady ? (ulong)Data + 86 * 4096 - 16 : 0;
}

/// <summary>
/// Materialize retained data in one caller-owned writable EFI allocation.
/// Page 0 holds contexts; pages 1..4 hold GDT/TSS/final IDT/bridge IDT; 5..20
/// hold the map; 22..85 hold the main stack; 88..91,94..97,100..103 hold ISTs.
/// The remaining eight pages are guard reservations: this helper does not unmap
/// them. The eventual owned hierarchy must omit them and map all other pages RW,
/// NX. Firmware mappings remain unchanged while preparing/calling boot services.
/// The separate vector page is reserved by address during preparation and must
/// contain authenticated owned terminal stubs before entry. Retain the original
/// state exclusively; never copy/reset it or free its
/// storage after successful EBS. Preparation does not load any CPU descriptor.
/// </summary>
public static unsafe class FirstExitOwnedArena
{
    public const uint PageCount = 105;
    public const uint Bytes = PageCount * 4096;
    public const uint MapBytes = 16 * 4096;
    public const uint MappingRangeCount = 5;
    public const ulong LowerCanary = 0x4d3033535441434b;
    public const ulong UpperCanary = 0x4d30335550504552;
    private const ulong LowEnd = 0x0000800000000000;

    public static bool IsGuardPage(uint page)
        => page == 21 || page == 86 || page == 87 || page == 92 ||
           page == 93 || page == 98 || page == 99 || page == 104;

    /// <summary>
    /// Describe only the retained arena's non-guard pages for BuildRetainedImage.
    /// Other retained image/allocator/ACPI/code ranges remain caller obligations.
    /// This writes declarations, not page tables; it does not change live mappings.
    /// Keep the original ready state and accessible arena stable during this call.
    /// </summary>
    public static bool WriteMappingRanges(ref FirstExitOwnedArenaState state,
        SeparatePageFaultMappingRange* ranges, ulong capacity, out ulong written)
    {
        written = 0;
        ulong bytes = MappingRangeCount * (ulong)sizeof(SeparatePageFaultMappingRange);
        if (!state.IsReady || !PageRange((ulong)state.Data, Bytes) ||
            ranges == null || ((ulong)ranges & 7) != 0 || (ulong)ranges >= LowEnd - bytes ||
            capacity < MappingRangeCount || capacity > SeparateDisabledDefaultPageFaultPageTables.MaximumRetainedRanges ||
            (FirstExitDescriptors.Overlaps((ulong)ranges, bytes, (ulong)state.Data, Bytes) &&
                !InMainStack(state, (ulong)ranges, bytes))) return false;
        fixed (FirstExitOwnedArenaState* address = &state)
            if (FirstExitDescriptors.Overlaps((ulong)ranges, bytes, (ulong)address,
                (ulong)sizeof(FirstExitOwnedArenaState))) return false;
        if (!ValidateCanaries(state)) return false;

        // The shared guard predicate is the single source of interval boundaries.
        uint start = 0;
        for (uint page = 0; page <= PageCount; page++)
        {
            if (page != PageCount && !IsGuardPage(page)) continue;
            if (page > start)
                ranges[written++] = new SeparatePageFaultMappingRange(
                    (ulong)state.Data + start * 4096UL, page - start,
                    PageProtection.Read | PageProtection.Write);
            start = page + 1;
        }
        return written == MappingRangeCount;
    }

    public static bool Materialize(ref FirstExitOwnedArenaState state, byte* data,
        ulong capacity, ulong ownedVectorPage, ushort firmwareCs)
    {
        if (state.Phase != 0 || capacity != Bytes || !PageRange((ulong)data, capacity) ||
            !PageRange(ownedVectorPage, 4096) ||
            FirstExitDescriptors.Overlaps((ulong)data, capacity, ownedVectorPage, 4096) ||
            (firmwareCs & 7) != 0 || (firmwareCs != 8 && firmwareCs < 40) || firmwareCs > 4088 ||
            sizeof(FirstExitDescriptorContext) > 128 || sizeof(FirstExitOwnedStackContext) > 128 ||
            sizeof(FirstExitPermanentCr3Context) > 128 || sizeof(FirstExitFirmwareBindingState) > 128 ||
            sizeof(FirstExitTransitionState) > 512 || sizeof(FirstExitFaultContext) > 4096 - 1024) return false;
        fixed (FirstExitOwnedArenaState* address = &state)
        {
            ulong start = (ulong)address, bytes = (ulong)sizeof(FirstExitOwnedArenaState);
            if (start == 0 || start >= LowEnd - bytes || (start & 7) != 0 ||
                FirstExitDescriptors.Overlaps(start, bytes, (ulong)data, capacity) ||
                FirstExitDescriptors.Overlaps(start, bytes, ownedVectorPage, 4096)) return false;
        }

        // Admission is complete. A later encoding/readback failure consumes the
        // state and retains Storage for pre-EBS cleanup; no ready view escapes.
        state.Data = data; state.Phase = 1;
        for (uint i = 0; i < Bytes; i++) data[i] = 0;
        byte* gdt = data + 4096, tss = data + 8192, idt = data + 12288, bridge = data + 16384;
        *(ulong*)(tss + 4) = (ulong)data + 86 * 4096 - 16;
        *(ushort*)(tss + 102) = 104;
        WriteCanaries(data, 22, 86);
        for (uint stack = 0; stack < 3; stack++)
        {
            uint first = 88 + stack * 6, end = first + 4;
            *(ulong*)(tss + 36 + stack * 8) = (ulong)data + end * 4096 - 16;
            WriteCanaries(data, first, end);
        }
        bool prepared = OS.Kernel.Diagnostics.OwnedX64GdtContainingNullN64BitCode.Encode(gdt, 4096, (ulong)tss, 103).Succeeded &&
            OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever.Materialize(idt, 4096, (ulong)idt, ownedVectorPage).Succeeded &&
            FirstExitDescriptors.Prepare(ref *(FirstExitDescriptorContext*)data, gdt, 4096, tss, 104, idt, 4096, bridge, 4096, firmwareCs);
        state.Phase = prepared ? 2u : 3u;
        if (!prepared) return false;
        if (ValidateCanaries(state)) return true;
        state.Phase = 3;
        return false;
    }

    public static bool ValidateCanaries(FirstExitOwnedArenaState state)
    {
        if (!state.IsReady || !PageRange((ulong)state.Data, Bytes)) return false;
        if (!CanariesMatch(state.Data, 22, 86)) return false;
        for (uint stack = 0; stack < 3; stack++)
            if (!CanariesMatch(state.Data, 88 + stack * 6, 92 + stack * 6)) return false;
        return true;
    }

    /// <summary>
    /// Read back the original, not-yet-installed descriptor storage against the
    /// exact reserved vector page and owned stack locations. Refuse changed
    /// pointers before following them. This does not observe live GDTR/IDTR/TR.
    /// </summary>
    public static bool ValidateDescriptors(ref FirstExitOwnedArenaState state, ulong vectorPage)
        => ValidateDescriptorsCore(ref state, vectorPage, false);

    // Validate supplied installed storage and native-recorded observations. This
    // helper itself does not execute SGDT/SIDT/STR or establish their provenance.
    public static bool ValidateInstalledDescriptors(ref FirstExitOwnedArenaState state, ulong vectorPage)
        => ValidateDescriptorsCore(ref state, vectorPage, true);

    private static bool ValidateDescriptorsCore(ref FirstExitOwnedArenaState state, ulong vectorPage, bool installed)
    {
        if (state.Reserved != 0 || !PageRange(vectorPage, 4096) || !ValidateCanaries(state) ||
            FirstExitDescriptors.Overlaps((ulong)state.Data, Bytes, vectorPage, 4096)) return false;
        FirstExitDescriptorContext* context = state.Descriptors;
        byte* gdt = state.Data + 4096, tss = state.Data + 8192;
        byte* finalIdt = state.Data + 12288, bridge = state.Data + 16384;
        ushort cs = context->FirmwareCs;
        // The native adoption stub consumes the descriptor context before LIDT/LGDT/LTR.
        if (context->Prepared != (installed ? 2u : 1u) || (cs & 7) != 0 || (cs != 8 && cs < 40) || cs > 4088 ||
            context->Gdt != gdt || context->Tss != tss || context->FinalIdt != finalIdt || context->BridgeIdt != bridge ||
            !PseudoDescriptor(context->Gdtr, gdt, (ushort)(cs < 40 ? 39 : cs + 7)) ||
            !PseudoDescriptor(context->FinalIdtr, finalIdt, 4095) || !PseudoDescriptor(context->BridgeIdtr, bridge, 4095) ||
            context->ObservedTr != (installed ? 24 : 0) || context->ObservedCs != (installed ? 8 : 0) ||
            context->ObservedDs != (installed ? 16 : 0) || context->ObservedEs != (installed ? 16 : 0) || context->ObservedSs != (installed ? 16 : 0) ||
            !PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(finalIdt, 4096, (ulong)finalIdt, vectorPage).Succeeded)
            return false;
        byte* expectedGdt = stackalloc byte[40];
        if (!OwnedX64GdtContainingNullN64BitCode.Encode(expectedGdt, 40, (ulong)tss, 103).Succeeded) return false;
        if (installed) expectedGdt[29] |= 2; // LTR must have made this TSS busy.
        for (int i = 0; i < 40; i++)
        {
            byte observed = gdt[i];
            if (installed && (i == 13 || i == 21)) observed &= 0xfe; // hardware accessed bit only
            if (observed != expectedGdt[i]) return false;
        }
        for (int i = 0; i < 16; i++)
            if (context->ObservedGdtr[i] != (installed ? context->Gdtr[i] : 0) ||
                context->ObservedIdtr[i] != (installed ? context->FinalIdtr[i] : 0)) return false;
        for (int i = 40; i < 4096; i++)
        {
            byte expected = cs >= 40 && i >= cs && i - cs < 8 ? expectedGdt[8 + i - cs] : (byte)0;
            byte observed = gdt[i];
            if (installed && cs >= 40 && i == cs + 5) observed &= 0xfe;
            if (observed != expected) return false;
        }
        for (int i = 0; i < 4096; i++)
        {
            byte expected = 0;
            if (i >= 4 && i < 12) expected = (byte)(state.StackTop >> ((i - 4) * 8));
            else if (i >= 36 && i < 60)
            {
                int stack = (i - 36) / 8;
                ulong top = (ulong)state.Data + (ulong)(92 + stack * 6) * 4096 - 16;
                expected = (byte)(top >> (((i - 36) % 8) * 8));
            }
            else if (i == 102) expected = 104;
            if (tss[i] != expected) return false;
            int gateOffset = i & 15;
            expected = gateOffset == 2 ? (byte)cs : gateOffset == 3 ? (byte)(cs >> 8) :
                gateOffset == 4 ? (byte)0 : finalIdt[i];
            if (bridge[i] != expected) return false;
        }
        return true;
    }

    private static bool PseudoDescriptor(byte* descriptor, byte* table, ushort limit)
    {
        if (*(ushort*)descriptor != limit || *(ulong*)(descriptor + 2) != (ulong)table) return false;
        for (int i = 10; i < 16; i++) if (descriptor[i] != 0) return false;
        return true;
    }

    internal static bool InMainStack(FirstExitOwnedArenaState state, ulong address, ulong bytes)
        => state.IsReady && PageRange((ulong)state.Data, Bytes) && bytes != 0 &&
            address >= state.StackBottom && address < state.StackTop && bytes <= state.StackTop - address;

    private static bool PageRange(ulong start, ulong bytes)
        => start != 0 && start < LowEnd && (start & 4095) == 0 && bytes < LowEnd - start;

    private static void WriteCanaries(byte* data, uint firstPage, uint endPage)
    {
        *(ulong*)(data + firstPage * 4096) = LowerCanary;
        *(ulong*)(data + endPage * 4096 - 16) = UpperCanary;
    }

    private static bool CanariesMatch(byte* data, uint firstPage, uint endPage)
        => *(ulong*)(data + firstPage * 4096) == LowerCanary &&
           *(ulong*)(data + endPage * 4096 - 16) == UpperCanary;
}

public enum FirstExitPhase : uint
{
    Empty, InFlight, Failed, OwnedContinuation, UnexpectedContinuationReturn
}

public enum FirstExitAdoptionPhase : uint
{
    Empty, Prepared, ExitStateCheck, ProcessorCheck, StackCheck, CanaryBeforeCheck,
    Cr3ReadBackCheck, Cr4ReadBackCheck, CanaryAfterCheck, OwnedDiagnostic,
    UnexpectedDiagnosticReturn, DescriptorInstall, DescriptorReadBackCheck
}

// Fixed x64 layout consumed by FirstExitPermanentCr3Stub. Retain the original
// context exclusively; do not change inputs after preparation or reset Phase.
public unsafe struct FirstExitPermanentCr3Context
{
    public ulong FirmwareCr3, OwnedCr3, FirmwareCr4;
    public ulong StackBottom, StackTop, MinimumRsp;
    public ulong LowerCanary, UpperCanary;
    public delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void> Diagnostic;
    public void* DiagnosticContext;
    public ulong ObservedCr3, ObservedCr4;
    public FirstExitAdoptionPhase Phase;
    internal uint Reserved;
    public ulong ObservedRsp;
    public FirstExitDescriptorContext* Descriptors;
}

public unsafe struct FirstExitDescriptorContext
{
    public fixed byte Gdtr[16], BridgeIdtr[16], FinalIdtr[16];
    public fixed byte ObservedGdtr[16], ObservedIdtr[16];
    public ushort ObservedTr, FirmwareCs, ObservedCs, ObservedDs, ObservedEs, ObservedSs;
    public uint Prepared;
    public byte* Gdt;
    public byte* Tss;
    public byte* BridgeIdt;
    public byte* FinalIdt;
}

/// <summary>
/// Prepare a bridge from firmware descriptors to owned descriptors without a
/// selector/IST mismatch window. The initial owned IDT uses the still-valid
/// firmware CS and IST=0; the owned GDT retains an equivalent code alias there.
/// After loading owned TR, the stub can install the final IDT's owned CS/ISTs.
/// All handlers are terminal, owned and mapped under both roots. During the
/// bridge their complete stack requirement must fit the current owned stack.
/// </summary>
public static unsafe class FirstExitDescriptors
{
    public const uint ContextBytes = 128;
    private const ulong LowEnd = 0x0000800000000000;

    // Inputs are a fresh M02-shaped GDT/TSS and final IDT, already materialized
    // in exclusively owned buffers. Prepare changes only the GDT tail, bridge
    // IDT and descriptor context, after validating every input. It allocates no
    // storage and cannot establish page mappings or authenticate handler code.
    public static bool Prepare(ref FirstExitDescriptorContext context, byte* gdt, ulong gdtBytes,
        byte* tss, ulong tssBytes, byte* finalIdt, ulong finalIdtBytes,
        byte* bridgeIdt, ulong bridgeIdtBytes, ushort firmwareCs)
    {
        if (context.Prepared != 0 || sizeof(FirstExitDescriptorContext) != ContextBytes ||
            gdtBytes != 4096 || tssBytes != 104 || finalIdtBytes != 4096 || bridgeIdtBytes != 4096 ||
            (firmwareCs & 7) != 0 || (firmwareCs != 8 && firmwareCs < 40) || firmwareCs > 4088 ||
            !Storage(gdt, 4096, 16) || !Storage(tss, 104, 16) ||
            !Storage(finalIdt, 4096, 4096) || !Storage(bridgeIdt, 4096, 4096)) return false;

        fixed (FirstExitDescriptorContext* address = &context)
        {
            if (!Storage((byte*)address, ContextBytes, 8)) return false;
            ulong* starts = stackalloc ulong[5];
            starts[0] = (ulong)address; starts[1] = (ulong)gdt; starts[2] = (ulong)tss;
            starts[3] = (ulong)finalIdt; starts[4] = (ulong)bridgeIdt;
            for (int i = 0; i < 5; i++) for (int j = 0; j < i; j++)
                if (Overlaps(starts[i], Size(i), starts[j], Size(j))) return false;
        }

        ulong tssBase = (ulong)tss;
        ulong tssDescriptor = 0x0000890000000067UL | ((tssBase & 0xffffff) << 16) |
            (((tssBase >> 24) & 0xff) << 56);
        if (*(ulong*)gdt != 0 || *(ulong*)(gdt + 8) != 0x00af9a000000ffffUL ||
            *(ulong*)(gdt + 16) != 0x00cf92000000ffffUL ||
            *(ulong*)(gdt + 24) != tssDescriptor || *(ulong*)(gdt + 32) != tssBase >> 32 ||
            !StackTop(*(ulong*)(tss + 4)) || *(ushort*)(tss + 102) != 104) return false;
        ulong ist1 = *(ulong*)(tss + 36), ist2 = *(ulong*)(tss + 44), ist3 = *(ulong*)(tss + 52);
        if (!StackTop(ist1) || !StackTop(ist2) || !StackTop(ist3) ||
            ist1 == ist2 || ist1 == ist3 || ist2 == ist3) return false;
        for (int i = 0; i < 104; i++)
            if (!(i >= 4 && i < 12) && !(i >= 36 && i < 60) && i < 102 && tss[i] != 0) return false;
        for (int i = 40; i < 4096; i++) if (gdt[i] != 0) return false;
        for (int i = 0; i < 4096; i++) if (bridgeIdt[i] != 0) return false;
        for (int vector = 0; vector < 256; vector++)
        {
            byte* gate = finalIdt + vector * 16;
            ulong handler = *(ushort*)gate | ((ulong)*(ushort*)(gate + 6) << 16) | ((ulong)*(uint*)(gate + 8) << 32);
            byte ist = vector == 2 ? (byte)3 : vector == 8 ? (byte)1 : vector == 14 ? (byte)2 : (byte)0;
            if (handler == 0 || handler >= LowEnd || *(ushort*)(gate + 2) != 8 ||
                gate[4] != ist || gate[5] != 0x8e || *(uint*)(gate + 12) != 0) return false;
        }

        // No mutation above this point. The alias preserves the meaning of the
        // bridge's CS across LGDT; final gates use the canonical owned selector 8.
        if (firmwareCs >= 40) *(ulong*)(gdt + firmwareCs) = *(ulong*)(gdt + 8);
        for (int i = 0; i < 4096; i++) bridgeIdt[i] = finalIdt[i];
        for (int vector = 0; vector < 256; vector++)
        {
            *(ushort*)(bridgeIdt + vector * 16 + 2) = firmwareCs;
            bridgeIdt[vector * 16 + 4] = 0;
        }
        context = default;
        fixed (FirstExitDescriptorContext* address = &context)
        {
            *(ushort*)address->Gdtr = (ushort)(firmwareCs < 40 ? 39 : firmwareCs + 7);
            *(ulong*)(address->Gdtr + 2) = (ulong)gdt;
            *(ushort*)address->BridgeIdtr = 4095;
            *(ulong*)(address->BridgeIdtr + 2) = (ulong)bridgeIdt;
            *(ushort*)address->FinalIdtr = 4095;
            *(ulong*)(address->FinalIdtr + 2) = (ulong)finalIdt;
        }
        context.FirmwareCs = firmwareCs;
        context.Gdt = gdt; context.Tss = tss; context.BridgeIdt = bridgeIdt; context.FinalIdt = finalIdt;
        context.Prepared = 1;
        return true;
    }

    internal static bool Overlaps(ulong a, ulong bytes, ulong b, ulong otherBytes)
        => a < b + otherBytes && b < a + bytes;
    private static ulong Size(int i) => i == 0 ? ContextBytes : i == 2 ? 104UL : 4096UL;
    private static bool Storage(byte* p, ulong bytes, ulong alignment)
        => (ulong)p != 0 && ((ulong)p & (alignment - 1)) == 0 && (ulong)p < LowEnd - bytes;
    private static bool StackTop(ulong value) => value >= 4096 && value < LowEnd && (value & 15) == 0;
}

/// <summary>
/// Emits the terminal Win64 continuation passed to AttemptAndTransfer. It uses
/// the current, already-owned stack, checks successful exit and the exact CPU
/// state, clears PGE, loads the owned root, checks readback and both canaries,
/// then calls the owned diagnostic. Every explicit refusal and unexpected return
/// halts; there is no near RET, firmware callback, CR3 restoration or output here.
///
/// Preparation accepts only low identity-mapped addresses and roots below 4 GiB,
/// four-level/non-PCID paging and CR4 bits 0..10 with PAE set. The adapter must
/// prove all actual mappings and CPU support, authenticate Diagnostic, and have
/// owned descriptors, panic state and COM1 prepared before the final map/exit.
/// After exit, the stub loads a bridge IDT, GDT, segments, TR and final IDT, reloads
/// CS using one local far transfer and validates descriptor readback before CR3.
/// CLI does not mask NMI or exceptions. Essential mappings and terminal handler
/// authentication remain adapter obligations. The declared stack bound must cover
/// both the diagnostic and the temporary IST=0 bridge handler's reachable graph.
/// </summary>
public static unsafe class FirstExitPermanentCr3Stub
{
    public const uint ContextBytes = 120;
    // 56-byte call frame (32-byte shadow space, retained context, alignment)
    // plus the diagnostic call's eight-byte return address.
    public const ulong CallStackBytes = 64;
    private const ulong LowAddressLimit = 0x0000800000000000;

    public static bool Prepare(ref FirstExitPermanentCr3Context context,
        ulong firmwareCr3, ulong ownedCr3, ulong firmwareCr4,
        ulong stackBottom, ulong stackTop, ulong ownedStackBytes,
        ulong lowerCanary, ulong upperCanary,
        delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void> diagnostic,
        void* diagnosticContext, FirstExitDescriptorContext* descriptors)
    {
        if (context.Phase != FirstExitAdoptionPhase.Empty || sizeof(FirstExitPermanentCr3Context) != ContextBytes ||
            (firmwareCr3 & ~0x18UL) == 0 || firmwareCr3 >= 0x100000000 || (firmwareCr3 & 0xfe7) != 0 ||
            ownedCr3 == 0 || ownedCr3 >= 0x100000000 || (ownedCr3 & 0xfff) != 0 ||
            ownedCr3 == (firmwareCr3 & ~0xfffUL) ||
            (firmwareCr4 & ~0x7ffUL) != 0 || (firmwareCr4 & 0x20) == 0 ||
            stackBottom < 8 || stackTop >= LowAddressLimit - 8 || stackTop <= stackBottom ||
            (stackBottom & 7) != 0 || (stackTop & 7) != 0 ||
            ownedStackBytes == 0 || ownedStackBytes >= stackTop - stackBottom ||
            stackTop - stackBottom - ownedStackBytes <= CallStackBytes ||
            lowerCanary == 0 || upperCanary == 0 || diagnostic == null ||
            (ulong)diagnostic >= LowAddressLimit || descriptors == null ||
            (ulong)descriptors >= LowAddressLimit - FirstExitDescriptors.ContextBytes ||
            descriptors->Prepared != 1 || descriptors->Tss == null ||
            (ulong)descriptors->Tss >= LowAddressLimit - 104 || *(ulong*)(descriptors->Tss + 4) != stackTop)
            return false;

        fixed (FirstExitPermanentCr3Context* address = &context)
        {
            ulong start = (ulong)address;
            if (start == 0 || start >= LowAddressLimit - ContextBytes ||
                (start < stackTop + 8 && stackBottom - 8 < start + ContextBytes))
                return false;
            ulong* descriptorRanges = stackalloc ulong[5];
            descriptorRanges[0] = (ulong)descriptors; descriptorRanges[1] = (ulong)descriptors->Gdt;
            descriptorRanges[2] = (ulong)descriptors->Tss; descriptorRanges[3] = (ulong)descriptors->BridgeIdt;
            descriptorRanges[4] = (ulong)descriptors->FinalIdt;
            for (int i = 0; i < 5; i++)
            {
                ulong length = i == 0 ? FirstExitDescriptors.ContextBytes : i == 2 ? 104UL : 4096UL;
                if (FirstExitDescriptors.Overlaps(start, ContextBytes, descriptorRanges[i], length) ||
                    FirstExitDescriptors.Overlaps(stackBottom - 8, stackTop - stackBottom + 16, descriptorRanges[i], length))
                    return false;
            }
        }

        context = default;
        context.FirmwareCr3 = firmwareCr3;
        context.OwnedCr3 = ownedCr3;
        context.FirmwareCr4 = firmwareCr4;
        context.StackBottom = stackBottom;
        context.StackTop = stackTop;
        context.MinimumRsp = stackBottom + ownedStackBytes + CallStackBytes;
        context.LowerCanary = lowerCanary;
        context.UpperCanary = upperCanary;
        context.Diagnostic = diagnostic;
        context.DiagnosticContext = diagnosticContext;
        context.Descriptors = descriptors;
        context.Phase = FirstExitAdoptionPhase.Prepared;
        return true;
    }

    public static uint RequiredBytes => Encode(null, 0);

    public static bool Emit(byte* destination, uint capacity, out uint written)
    {
        written = 0;
        uint bytes = RequiredBytes;
        if (destination == null || capacity < bytes ||
            (ulong)destination >= LowAddressLimit - bytes)
            return false;
        written = Encode(destination, bytes - 4);
        return true;
    }

    private static uint Encode(byte* destination, uint halt)
    {
        uint p = 0;
        Op(destination, ref p, 0xfcfa, 2);              // cli; cld
        Op(destination, ref p, 0xd28548, 3);            // test rdx,rdx
        StopIf(destination, ref p, halt, 0x84);         // jz halt
        Op(destination, ref p, 0xc98548, 3);            // test rcx,rcx
        StopIf(destination, ref p, halt, 0x84);
        Op(destination, ref p, 0x01607a83, 4);          // cmp dword [rdx+96],Prepared
        StopIf(destination, ref p, halt, 0x85);
        Phase(destination, ref p, FirstExitAdoptionPhase.ExitStateCheck);
        Op(destination, ref p, 0x033983, 3);            // cmp dword [rcx],OwnedContinuation
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x02047983, 4);          // cmp dword [rcx+4],BootServicesExited
        StopIf(destination, ref p, halt, 0x85);

        Phase(destination, ref p, FirstExitAdoptionPhase.ProcessorCheck);
        Op(destination, ref p, 0xd8200f, 3);            // mov rax,cr3
        Op(destination, ref p, 0x023b48, 3);            // cmp rax,[rdx]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0xe0200f, 3);            // mov rax,cr4
        Op(destination, ref p, 0x10423b48, 4);          // cmp rax,[rdx+16]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x704a8b4c, 4);          // mov r9,[rdx+112]
        Op(destination, ref p, 0xc9854d, 3);            // test r9,r9
        StopIf(destination, ref p, halt, 0x84);
        Op(destination, ref p, 0x015c798341, 5);        // cmp dword [r9+92],1
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0xc88c66, 3);            // mov ax,cs
        Op(destination, ref p, 0x52413b4166, 5);        // cmp ax,[r9+82]
        StopIf(destination, ref p, halt, 0x85);

        Phase(destination, ref p, FirstExitAdoptionPhase.StackCheck);
        Op(destination, ref p, 0x68628948, 4);          // mov [rdx+104],rsp
        Op(destination, ref p, 0xe08948, 3);            // mov rax,rsp
        Op(destination, ref p, 0x0fe083, 3);            // and eax,15
        Op(destination, ref p, 0x08f883, 3);            // cmp eax,8 (Win64 entry alignment)
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x28623b48, 4);          // cmp rsp,[rdx+40]
        StopIf(destination, ref p, halt, 0x82);         // jb halt
        Op(destination, ref p, 0x20623b48, 4);          // cmp rsp,[rdx+32]
        StopIf(destination, ref p, halt, 0x83);         // jae halt
        Phase(destination, ref p, FirstExitAdoptionPhase.CanaryBeforeCheck);
        Canaries(destination, ref p, halt);
        Descriptors(destination, ref p, halt);

        // No call or output before permanent CR3 adoption. PGE stays cleared:
        // flushing it prevents firmware global translations surviving the load.
        Op(destination, ref p, 0xe0200f, 3);            // mov rax,cr4
        Op(destination, ref p, 0xffffff7f2548, 6);      // and rax,~0x80
        Op(destination, ref p, 0xe0220f, 3);            // mov cr4,rax
        Op(destination, ref p, 0x08428b48, 4);          // mov rax,[rdx+8]
        Op(destination, ref p, 0xd8220f, 3);            // mov cr3,rax (only CR3 write)
        Phase(destination, ref p, FirstExitAdoptionPhase.Cr3ReadBackCheck);
        Op(destination, ref p, 0xd8200f, 3);            // mov rax,cr3
        Op(destination, ref p, 0x50428948, 4);          // mov [rdx+80],rax
        Op(destination, ref p, 0x08423b48, 4);          // cmp rax,[rdx+8]
        StopIf(destination, ref p, halt, 0x85);
        Phase(destination, ref p, FirstExitAdoptionPhase.Cr4ReadBackCheck);
        Op(destination, ref p, 0xe0200f, 3);            // mov rax,cr4
        Op(destination, ref p, 0x58428948, 4);          // mov [rdx+88],rax
        Op(destination, ref p, 0x10428b4c, 4);          // mov r8,[rdx+16]
        Op(destination, ref p, 0xffffff7fe08149, 7);    // and r8,~0x80
        Op(destination, ref p, 0xc0394c, 3);            // cmp rax,r8
        StopIf(destination, ref p, halt, 0x85);
        Phase(destination, ref p, FirstExitAdoptionPhase.CanaryAfterCheck);
        Canaries(destination, ref p, halt);

        Phase(destination, ref p, FirstExitAdoptionPhase.OwnedDiagnostic);
        Op(destination, ref p, 0x38ec8348, 4);          // sub rsp,56 (shadow/context/alignment)
        Op(destination, ref p, 0x2024548948, 5);        // mov [rsp+32],rdx
        Op(destination, ref p, 0x40428b48, 4);          // mov rax,[rdx+64]
        Op(destination, ref p, 0xd0ff, 2);              // call rax (owned diagnostic only)
        Op(destination, ref p, 0x2024548b48, 5);        // mov rdx,[rsp+32]
        Phase(destination, ref p, FirstExitAdoptionPhase.UnexpectedDiagnosticReturn);
        Op(destination, ref p, 0xfcebf4fa, 4);          // halt: cli; hlt; jmp halt
        return p;
    }

    private static void Canaries(byte* destination, ref uint p, uint halt)
    {
        Op(destination, ref p, 0x18428b48, 4);          // mov rax,[rdx+24]
        Op(destination, ref p, 0xf8408b48, 4);          // mov rax,[rax-8]
        Op(destination, ref p, 0x30423b48, 4);          // cmp rax,[rdx+48]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x20428b48, 4);          // mov rax,[rdx+32]
        Op(destination, ref p, 0x008b48, 3);            // mov rax,[rax]
        Op(destination, ref p, 0x38423b48, 4);          // cmp rax,[rdx+56]
        StopIf(destination, ref p, halt, 0x85);
    }

    private static void Descriptors(byte* destination, ref uint p, uint halt)
    {
        Phase(destination, ref p, FirstExitAdoptionPhase.DescriptorInstall);
        Op(destination, ref p, 0x000000025c41c741, 8);  // mov dword [r9+92],2 (consume)
        Op(destination, ref p, 0x1059010f41, 5);        // lidt [r9+16]: firmware CS, IST=0
        Op(destination, ref p, 0x11010f41, 4);          // lgdt [r9]: retain bridge CS alias
        Op(destination, ref p, 0x0010b866, 4);          // mov ax,16
        Op(destination, ref p, 0xd08ec08ed88e, 6);      // mov ds,ax; mov es,ax; mov ss,ax
        Op(destination, ref p, 0x0018b866, 4);          // mov ax,24
        Op(destination, ref p, 0xd8000f, 3);            // ltr ax
        Op(destination, ref p, 0x2059010f41, 5);        // lidt [r9+32]: owned CS and ISTs

        // One local far transfer refreshes the hidden CS descriptor. The pushed
        // target is RIP+3 (immediately after push rax/retfq), never an EFI return.
        Op(destination, ref p, 0x086a, 2);              // push 8
        Op(destination, ref p, 0x00000003058d48, 7);    // lea rax,[rip+3]
        Op(destination, ref p, 0xcb4850, 3);            // push rax; retfq

        Phase(destination, ref p, FirstExitAdoptionPhase.DescriptorReadBackCheck);
        Op(destination, ref p, 0x3041010f41, 5);        // sgdt [r9+48]
        Op(destination, ref p, 0x4049010f41, 5);        // sidt [r9+64]
        Op(destination, ref p, 0x5049000f41, 5);        // str [r9+80]
        Op(destination, ref p, 0x018b49, 3);            // mov rax,[r9]
        Op(destination, ref p, 0x30413b49, 4);          // cmp rax,[r9+48]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x08418b4166, 5);        // mov ax,[r9+8]
        Op(destination, ref p, 0x38413b4166, 5);        // cmp ax,[r9+56]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x20418b49, 4);          // mov rax,[r9+32]
        Op(destination, ref p, 0x40413b49, 4);          // cmp rax,[r9+64]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x28418b4166, 5);        // mov ax,[r9+40]
        Op(destination, ref p, 0x48413b4166, 5);        // cmp ax,[r9+72]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x185079834166, 6);      // cmp word [r9+80],24
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x60418b49, 4);          // mov rax,[r9+96] (GDT)
        Op(destination, ref p, 0x8b1d7880, 4);          // cmp byte [rax+29],0x8b (busy TSS)
        StopIf(destination, ref p, halt, 0x85);
        for (uint segment = 0; segment < 4; segment++)
        {
            byte modrm = segment == 0 ? (byte)0xc8 : segment == 1 ? (byte)0xd8 : segment == 2 ? (byte)0xc0 : (byte)0xd0;
            Op(destination, ref p, 0x8c66 | ((ulong)modrm << 16), 3); // mov ax,cs/ds/es/ss
            Op(destination, ref p, 0x41894166 | ((ulong)(84 + segment * 2) << 32), 5); // retain observation
            Op(destination, ref p, 0x3d66 | ((segment == 0 ? 8UL : 16UL) << 16), 4); // cmp ax,selector
            StopIf(destination, ref p, halt, 0x85);
        }
    }

    private static void Phase(byte* destination, ref uint p, FirstExitAdoptionPhase phase)
    {
        Op(destination, ref p, 0x6042c7, 3);            // mov dword [rdx+96],phase
        Op(destination, ref p, (uint)phase, 4);
    }

    private static void StopIf(byte* destination, ref uint p, uint halt, byte condition)
    {
        Op(destination, ref p, 0x0f | ((ulong)condition << 8), 2);
        Op(destination, ref p, unchecked(halt - p - 4), 4);
    }

    // Fixed x64 instruction encoding, also used without a destination to measure.
    private static void Op(byte* destination, ref uint p, ulong instruction, uint bytes)
    {
        for (uint i = 0; i < bytes; i++)
        {
            if (destination != null) destination[p] = (byte)(instruction >> (int)(i * 8));
            p++;
        }
    }
}

// Every returned value is a refusal/failure. Successful exit never returns.
public enum FirstExitReturn
{
    NotStarted, ReentryRejected, AlreadyConsumed, FirmwareUnavailable,
    InvalidContinuation, InvalidStorage, ExitNotCompleted
}

public unsafe struct FirstExitTransitionState
{
    internal FirstExitPhase PhaseValue;
    internal FirmwareServicesMode ModeValue;
    internal BoundedExitRetryState Retry;
    internal BoundedExitRetryResult AttemptValue;

    public FirstExitPhase Phase => PhaseValue;
    public FirmwareServicesMode Mode => ModeValue;
    public BoundedExitRetryResult Attempt => AttemptValue;
    public bool RetainedFirmwareCallbacksCleared =>
        Retry.First.Exit.Function == null && Retry.First.Exit.Handle == null &&
        Retry.Retry.Exit.Function == null && Retry.Retry.Exit.Handle == null;
}

/// <summary>
/// One terminal handoff using the accepted final-map/two-attempt retry core.
/// A successful exit clears this coordinator's retained firmware callbacks and
/// transfers to the original owned continuation; it cannot return to an EFI
/// caller. An unexpected continuation return spins without calling anything.
///
/// Before entry, the adapter must authenticate the firmware pointers and owned
/// continuation, prepare all mappings/descriptors/COM1/stack/canaries, and prove
/// that state, map buffer, continuation code and context survive permanent CR3
/// adoption. The continuation must adopt that hierarchy before any output or
/// other callback and terminate using owned facilities. This coordinator does
/// not load CR3, authenticate pointers, or revoke independently copied handles.
///
/// Retain one zero-initialized state; never copy/reset it. Access is exclusive
/// and callbacks must not mutate its storage or throw. Returned failures leave
/// the last attempt available for owned diagnostics; after an issued exit call
/// the adapter must not resume ordinary firmware work even if exit failed.
/// </summary>
public static unsafe class FirstOptRealExitBootServicesTransition
{
    public static FirstExitReturn AttemptAndTransfer(ref FirstExitTransitionState state,
        FirmwareServicesMode mode,
        delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap,
        delegate* unmanaged<void*, ulong, ulong> exitBootServices,
        void* imageHandle, byte* ownedMap, ulong mapCapacity,
        delegate* unmanaged<FirstExitTransitionState*, void*, void> ownedContinuation,
        void* ownedContext, ulong contextBytes)
    {
        if (state.PhaseValue == FirstExitPhase.InFlight)
            return FirstExitReturn.ReentryRejected;
        if (state.PhaseValue != FirstExitPhase.Empty)
            return FirstExitReturn.AlreadyConsumed;
        if (mode != FirmwareServicesMode.BootServicesAvailable)
            return FirstExitReturn.FirmwareUnavailable;
        if (ownedContinuation == null)
            return FirstExitReturn.InvalidContinuation;

        ulong map = (ulong)ownedMap, context = (ulong)ownedContext;
        if (!Extent(map, mapCapacity) || mapCapacity < UefiMemoryMapCopyPlan.MinimumDescriptorSize ||
            !Extent(context, contextBytes) || Overlap(map, mapCapacity, context, contextBytes))
            return FirstExitReturn.InvalidStorage;

        fixed (FirstExitTransitionState* address = &state)
        {
            ulong stateStart = (ulong)address, stateBytes = (ulong)sizeof(FirstExitTransitionState);
            if (!Extent(stateStart, stateBytes) ||
                Overlap(stateStart, stateBytes, map, mapCapacity) ||
                Overlap(stateStart, stateBytes, context, contextBytes))
                return FirstExitReturn.InvalidStorage;

            state.PhaseValue = FirstExitPhase.InFlight;
            state.ModeValue = mode;
            BoundedExitRetryResult attempt = BoundedRetryStaleKeyStatus.Execute(ref state.Retry,
                mode, getMemoryMap, exitBootServices, imageHandle, ownedMap, mapCapacity);
            // No logging, allocation or firmware operation follows a successful exit.
            if (attempt.Succeeded)
                state.ModeValue = FirmwareServicesMode.BootServicesExited;
            state.AttemptValue = attempt;
            state.Retry = default;
            if (!attempt.Succeeded)
            {
                state.PhaseValue = FirstExitPhase.Failed;
                return FirstExitReturn.ExitNotCompleted;
            }

            state.PhaseValue = FirstExitPhase.OwnedContinuation;
            ownedContinuation(address, ownedContext);
            state.PhaseValue = FirstExitPhase.UnexpectedContinuationReturn;
            while (true) { }
        }
    }

    private static bool Extent(ulong start, ulong bytes) =>
        start != 0 && bytes != 0 && bytes <= UefiMemoryMapCopyPlan.MaximumUnsignedLong - start;

    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) =>
        a < b + bn && b < a + an;
}
