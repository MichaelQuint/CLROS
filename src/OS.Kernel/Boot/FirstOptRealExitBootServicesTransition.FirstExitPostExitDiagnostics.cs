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
/// One-use reporting from the terminal owned callback. Admit the original native
/// observations, reconstruct essential mappings and audit the actual root backing
/// before advancing success markers. No firmware function or output is accepted.
/// The caller must authenticate this callback/native observation provenance and
/// halt after every return, including success. Host-supplied observations alone
/// are not proof that hardware performed ExitBootServices or loaded CR3.
/// </summary>

public static unsafe class FirstExitPostExitDiagnostics
{
    public static bool Report(ref FirstExitDiagnosticState state, ref FirstExitOwnedArenaState arena, FirstExitDiagnosticMemory memory)
    {
        if (state.Status != FirstExitDiagnosticStatus.Empty)
            return false;
        fixed (FirstExitDiagnosticState* address = &state)
        {
            ulong start = (ulong)address, owner = memory.References.AdapterState, bytes = memory.References.AdapterStateBytes;
            if (start < owner || bytes < (ulong)sizeof(FirstExitDiagnosticState) || start - owner > bytes - (ulong)sizeof(FirstExitDiagnosticState))
                return false;
            fixed (FirstExitOwnedArenaState* handle = &arena)
                if (FirstExitDescriptors.Overlaps(start, (ulong)sizeof(FirstExitDiagnosticState), (ulong)handle, (ulong)sizeof(FirstExitOwnedArenaState)))
                    return false;
        }

        state.Status = FirstExitDiagnosticStatus.AdmissionFailed;
        if (!arena.IsReady || !FirstExitOwnedArena.ValidateCanaries(arena) || memory.Tables.ArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong root) != AddressValueStatus.Success || root == 0 || (ulong)memory.Entries != root || memory.PageCapacity != memory.Tables.ArenaPageCount || memory.EntryCapacity != memory.PageCapacity * 512 || arena.Transition->Phase != FirstExitPhase.OwnedContinuation || arena.Transition->Mode != FirmwareServicesMode.BootServicesExited || !arena.Transition->Attempt.Succeeded || !arena.Transition->RetainedFirmwareCallbacksCleared || !arena.Binding->IsConsumed || !arena.Binding->RetainedPointersCleared)
            return false;
        FirstExitPermanentCr3Context* adoption = arena.Permanent;
        FirstExitOwnedStackContext* entry = arena.StackEntry;
        if (adoption->Phase != FirstExitAdoptionPhase.OwnedDiagnostic || adoption->Reserved != 0 || adoption->OwnedCr3 != root || adoption->ObservedCr3 != root || (adoption->FirmwareCr3 & ~0x18UL) == 0 || adoption->FirmwareCr3 >= 0x100000000 || (adoption->FirmwareCr3 & 0xfe7) != 0 || (adoption->FirmwareCr3 & ~0xfffUL) == root || (adoption->FirmwareCr4 & ~0x7ffUL) != 0 || (adoption->FirmwareCr4 & 0x20) == 0 || adoption->ObservedCr4 != (adoption->FirmwareCr4 & ~0x80UL) || adoption->StackBottom != arena.StackBottom || adoption->StackTop != arena.StackTop || adoption->MinimumRsp <= arena.StackBottom || adoption->MinimumRsp >= arena.StackTop || adoption->ObservedRsp < adoption->MinimumRsp || adoption->ObservedRsp >= arena.StackTop || (adoption->ObservedRsp & 15) != 8 || adoption->LowerCanary != FirstExitOwnedArena.LowerCanary || adoption->UpperCanary != FirstExitOwnedArena.UpperCanary || adoption->Descriptors != arena.Descriptors || (ulong)adoption->Diagnostic != memory.References.DiagnosticCallback || (ulong)adoption->DiagnosticContext != memory.References.AdapterState || entry->Phase != 2 || entry->Reserved != 0 || entry->FirmwareCr3 != adoption->FirmwareCr3 || entry->FirmwareCr4 != adoption->FirmwareCr4 || entry->StackBottom != arena.StackBottom || entry->StackTop != arena.StackTop || entry->ObservedRsp != arena.StackTop - FirstExitOwnedStackStub.CallStackBytes || (ulong)entry->Entry != memory.References.EntryCallback || (ulong)entry->EntryContext != memory.References.AdapterState)
            return false;
        SeparatePageFaultMappingRange* ranges = stackalloc SeparatePageFaultMappingRange[(int)FirstExitRetainedMappings.MaximumRanges];
        if (!FirstExitRetainedMappings.WriteInstalledRanges(ref arena, memory.Code, memory.Capture, memory.Mapping, memory.References, memory.Allocator, memory.Tables, ranges, FirstExitRetainedMappings.MaximumRanges, out ulong count) || !FirstExitImageMappings.Contains(ranges, count, (ulong)memory.Pages, memory.PageCapacity * (ulong)sizeof(X64PageTableImagePage), PageProtection.Read | PageProtection.Write))
            return false;
        state.Status = FirstExitDiagnosticStatus.SerialFailed;
        Com1OwnedPortIoBackend backend = arena.Fault->Backend;
        if (!backend.ConfigureDefault().Succeeded || !WriteMarker(backend, 0))
            return false;
        state.Status = FirstExitDiagnosticStatus.ValidationFailed;
        if (!SeparateDisabledDefaultPageFaultPageTables.AuditRetainedAccessedImage(memory.Tables, ranges, count, FirstExitRetainedMappings.MaximumRanges, memory.Pages, memory.PageCapacity, memory.Entries, memory.EntryCapacity).Succeeded || !FirstExitOwnedArena.ValidateCanaries(arena))
            return false;
        state.Status = FirstExitDiagnosticStatus.SerialFailed;
        if (!WriteMarker(backend, 1))
            return false;
        state.Status = FirstExitDiagnosticStatus.ValidationFailed;
        if (!FirstExitOwnedArena.ValidateCanaries(arena))
            return false;
        state.Status = FirstExitDiagnosticStatus.SerialFailed;
        if (!WriteMarker(backend, 2))
            return false;
        // Both diagnostic groups use the original owned transport. Recheck
        // canaries before every claim; a partial write consumes this attempt.
        for (uint marker = 3; marker < 10; marker++)
        {
            state.Status = FirstExitDiagnosticStatus.ValidationFailed;
            if (!FirstExitOwnedArena.ValidateCanaries(arena))
                return false;
            state.Status = FirstExitDiagnosticStatus.SerialFailed;
            if (!WriteMarker(backend, marker))
                return false;
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
            words[0] = 0x5f5342455f33304dUL;
            words[1] = 0x44454e5255544552UL;
            words[2] = 0x535345434355535fUL;
            words[3] = 0x0000000000000a0dUL;
            bytes = 26;
        }
        else if (marker == 1)
        {
            words[0] = 0x4d5245505f33304dUL;
            words[1] = 0x52435f544e454e41UL;
            words[2] = 0x4554504f44415f33UL;
            words[3] = 0x00000000000a0d44UL;
            bytes = 27;
        }
        else if (marker == 2)
        {
            words[0] = 0x54534f505f33304dUL;
            words[1] = 0x4e574f5f5342455fUL;
            words[2] = 0x4e474149445f4445UL;
            words[3] = 0x000a0d434954534fUL;
            bytes = 31;
        }
        else if (marker == 3)
        {
            words[0] = 0x304d5f534f524c43UL;
            words[1] = 0x45525f3730305f33UL;
            words[2] = 0x0000000a0d594441UL;
            words[3] = 0;
            bytes = 21;
        }
        else if (marker == 4)
        {
            words[0] = 0x5341505f54534554UL;
            words[1] = 0x30302e33306d2053UL;
            words[2] = 0x00000000000a0d37UL;
            words[3] = 0;
            bytes = 19;
        }
        else if (marker == 5)
        {
            words[0] = 0x4154535f54534554UL;
            words[1] = 0x302e33306d205452UL;
            words[2] = 0x000000000a0d3930UL;
            words[3] = 0;
            bytes = 20;
        }
        else if (marker == 6)
        {
            words[0] = 0x5342455f49464555UL;
            words[1] = 0x535345434355535fUL;
            words[2] = 0x0000000000000a0dUL;
            words[3] = 0;
            bytes = 18;
        }
        else if (marker == 7)
        {
            words[0] = 0x5249465f54534f50UL;
            words[1] = 0x49445f455241574dUL;
            words[2] = 0x434954534f4e4741UL;
            words[3] = 0x0d59444145525f53UL;
            words[4] = 0x000000000000000aUL;
            bytes = 33;
        }
        else if (marker == 8)
        {
            words[0] = 0x304d5f534f524c43UL;
            words[1] = 0x45525f3930305f33UL;
            words[2] = 0x0000000a0d594441UL;
            words[3] = 0;
            bytes = 21;
        }
        else if (marker == 9)
        {
            words[0] = 0x5341505f54534554UL;
            words[1] = 0x30302e33306d2053UL;
            words[2] = 0x00000000000a0d39UL;
            words[3] = 0;
            bytes = 19;
        }
        else
            return false;
        for (uint index = 0; index < bytes; index++)
        {
            bool ready = false;
            for (int poll = 0; poll < PanicSafeFormattingSerialReportingOwnedCom1.DefaultTransmitReadyPollLimit; poll++)
            {
                Com1OwnedPortIoOperationResult read = backend.ReadByte(Com1OwnedPortIoBackend.LineStatusRegisterPort);
                if (!read.Succeeded)
                    return false;
                if ((read.Value & PanicSafeFormattingSerialReportingOwnedCom1.TransmitHoldingRegisterEmptyMask) != 0)
                {
                    ready = true;
                    break;
                }
            }

            if (!ready || !backend.WriteByte(Com1OwnedPortIoBackend.DataRegisterPort, ((byte*)words)[index]).Succeeded)
                return false;
        }

        return true;
    }
}
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
#if M16_006_PROBE

#endif
#if M16_007_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#endif
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
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
#if M16_006_PROBE

#endif
#if M16_007_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

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
#if M10_017_PROBE || M10_018_PROBE || M16_027_PROBE

#else
#endif
#if M16_002_PROBE

#endif
#if M16_004_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE

#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M16_025_PROBE

#endif
#endif
#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
#if M16_005_PROBE

#endif
#if M16_006_PROBE

#endif
#if M16_007_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
#if M16_008_PROBE

#else

#endif

#endif
#if M07_026_PROBE

#endif
#if M07_026_PROBE

#if M16_025_PROBE

#endif
#endif
#if M16_002_PROBE

#endif
#if M16_004_PROBE

#endif
#if M16_005_PROBE

#endif
#if M16_006_PROBE

#endif
#if M16_007_PROBE

#endif
#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE

#endif
#if M05_PHYSICAL_MAPPING_PROBE

#endif
#if M05_MONOTONIC_CLOCK_PROBE || M16_003_PROBE

#endif
#if M05_PCI_ENUMERATION_PROBE
#if M05_BAR_DMA_PROBE || M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#if M16_025_PROBE

#else

#endif
#else

#endif
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE || M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE

#endif
#if M05_APIC_SETUP_PROBE

#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE || M06_BLOCK_MSIX_PROBE

#endif
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
