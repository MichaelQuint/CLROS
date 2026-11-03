#if M09_004_PROBE || M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE || M16_008_PROBE || M07_026_PROBE
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
#endif
#if M09_005_PROBE || M09_006_PROBE || M09_009_PROBE || M09_010_PROBE
using TimerIrq = OS.Kernel.Threading.X64TimerInterrupt;
using TickTimer = OS.Kernel.Threading.SchedulerTimer;
using Deadlines = OS.Kernel.Threading.DeadlineQueue;
#endif
#if M04_LAYOUT_CONTRACT_PROBE
using OS.Kernel.Runtime;
#endif
#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE || (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
using OS.Kernel.Memory;
#endif
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
#if M16_004_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
#if M05_PCI_ENUMERATION_PROBE || M16_006_PROBE
using OS.Drivers;
#endif
#if M07_ARP_RECEIVE_PROBE
using OS.Net;
#if !M07_VIRTIO_NET_PROBE
#error The ARP receive profile requires the accepted original Virtio-net profile.
#endif
#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
using OS.Storage;
#endif
#if M06_FAT_READ_PROBE
using FatChain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using FatDirectory = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;
using FatFile = OS.Storage.KnownFileSHAN256EquivalentKnownFixtureHash;
#endif
#if !(M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
#error The first real exit requires the original loaded-image, live allocator and copied ACPI adapters.
#endif
#if M01_OWNED_PAGING_PROBE || M04_MANAGED_HEAP_PAGE_RANGE_PROBE || M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE || M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE
#error The first real exit is an exclusive firmware-CR3 path, with no intervening paging, heap or controlled exit probe.
#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE || M02_UD2_VECTOR_STATE_PROBE || M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE || M02_DOUBLE_FAULT_IST_PROBE || M02_UNEXPECTED_VECTOR_PANIC_PROBE
#error The first real exit cannot be combined with another terminal exception probe.
#endif
#endif
namespace OS.Boot;
#pragma warning disable 649
public static unsafe partial class EfiEntry
{
#if (M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
    private static partial class M01StageTwoAdapter
    {
        private const ulong EfiSuccess = 0;
        private const uint AllocateAnyPages = 0;
        private const uint EfiLoaderData = 2;
        private const ulong PageSize = 4096;
        private const ulong AllocatorArenaPageCount = 2;
        private const ulong ArenaAllocationOrdinal = 5;
        private const ulong MapCaptureOrdinal = 6;
        private const ulong LedgerBuildOrdinal = 7;
        private const ulong PreviousMapGeneration = 2;
        private const ulong RefreshedMapGeneration = 3;
        private const ulong StageOneReservationCount = 4;
        private const ulong StageOneReservationCapacity = 4;
        private const ulong ReservationCount = 5;
        private const ulong ReservationCapacity = 5;
        private const ulong AllocationRecordCount = 1;
        private const ulong AllocationRecordCapacity = 1;
        private const ulong NotAttemptedStatus = 0xffffffffffffffff;

        private static BootMemoryRegionRecord s_allocationRecord;
        private static M01StageTwoReservationLedgerBuffer
            s_reservationLedger;
        private static M01LiveAllocatorStaticState s_staticState;
#if (M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE || CLROS_NORMAL_VERIFIED_EXIT)
        internal static bool TryGetOriginalExitAllocator(out ulong address,
            out SeparatePageFaultMappingRange backing)
        {
            address = 0;
            backing = default;
            fixed (M01LiveAllocatorStaticState* state = &s_staticState)
            fixed (BootMemoryRegionRecord* record = &s_allocationRecord)
            {
                if (!state->IsCompleted || !M01StageOneAdapter.IsFinalStageOneSuccess(
                        (void*)state->StageOneStateAddress, state->StageOneStateCapacity) ||
                    record->UefiType != EfiLoaderData || record->PageCount != AllocatorArenaPageCount ||
                    record->ByteLength != AllocatorArenaPageCount * PageSize ||
                    record->VirtualStart != record->PhysicalStart ||
                    state->ArenaReservation.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong start) != AddressValueStatus.Success ||
                    state->ArenaReservation.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong end) != AddressValueStatus.Success ||
                    record->PhysicalStart != start || record->PhysicalEndExclusive != end ||
                    state->ArenaReservation.PageCount != AllocatorArenaPageCount)
                    return false;
                address = (ulong)state;
                backing = new SeparatePageFaultMappingRange(start, AllocatorArenaPageCount,
                    PageProtection.Read | PageProtection.Write);
                return true;
            }
        }
#if M16_002_PROBE
        internal static bool CopyOriginalExitReservations(BootReservedRange* destination, uint capacity, out uint count)
        {
            count = 0;
            if (destination == null || capacity < 5 || !s_staticState.IsCompleted)
                return false;
            fixed (M01StageTwoReservationLedgerBuffer* ledger = &s_reservationLedger)
            {
                BootReservedRange* source = &ledger->Range0;
                if (!BootReservationTableValidator.Validate(source, 5, 5).Succeeded)
                    return false;
                for (uint i = 0; i < 5; i++)
                    destination[i] = source[i];
            }
            count = 5;
            return true;
        }
#endif
#endif
#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE
        private static ManagedHeapPageRangeState s_heapState;

        public static bool ValidateManagedHeapBeforeBootPlan(
            EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
        {
            fixed (M01LiveAllocatorStaticState* state = &s_staticState)
            fixed (BootMemoryRegionRecord* record = &s_allocationRecord)
            fixed (M01StageTwoReservationLedgerBuffer* ledger = &s_reservationLedger)
            fixed (ManagedHeapPageRangeState* heap = &s_heapState)
                return M04ManagedHeapAdapter.Checkpoint(false, state, record,
                    &ledger->Range0, heap, console, serial);
        }
#if M04_RHP_NEW_FAST_PROBE
        public static void RunRhpNewFastAfterZeroCheckpoint()
        {
            if (!M01StageOneAdapter.TryGetOriginalLoadedImage(out byte* image, out ulong imageBytes))
                OS.Runtime.RhpNewFastAllocation.RejectAdapter(OS.Runtime.RhpAdmissionStatus.InvalidImage, 13);
            fixed (M01LiveAllocatorStaticState* state = &s_staticState)
            fixed (BootMemoryRegionRecord* record = &s_allocationRecord)
            fixed (M01StageTwoReservationLedgerBuffer* ledger = &s_reservationLedger)
            fixed (ManagedHeapPageRangeState* heap = &s_heapState)
            {
                RhpNewFastHeapAccess original = new RhpNewFastHeapAccess
                {
                    Allocator = &state->AllocatorStateValue,
                    AllocatorCapacity = 1,
                    Nodes = &state->FreeNodeValue,
                    NodeCount = 1,
                    NodeCapacity = 1,
                    Reservations = &ledger->Range0,
                    ReservationCount = 5,
                    ReservationCapacity = 5,
                    ParentIndex = heap->ParentIndex,
                    Backing = (byte*)record->VirtualStart,
                    BackingCapacity = 8192,
                    Heap = heap,
                    HeapCapacity = ManagedHeapPageRangeLiveAllocator.RequiredStateBytes
                };
#if M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
                if (!M01StageOneAdapter.TryGetOriginalTypeManagerImage(out EfiLoadedImageRangeCapturePlan capture,
                        out M01LoadedImageVirtualMappingEvidence mapping))
                    OS.Runtime.RhpNewFastAllocation.RejectAdapter(OS.Runtime.RhpAdmissionStatus.InvalidImage, 14);
                OS.Runtime.RhpNewFastAllocation.RunGcStatics(original, capture, mapping);
#else
                OS.Runtime.RhpNewFastAllocation.Run(original, image, imageBytes);
#endif
            }
        }
#endif
#endif

        public static bool RunAfterStageOne(
            void* stageOneState,
            ulong stageOneStateCapacity,
            EfiBootServices* bootServices,
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            OS.Kernel.Memory.BootReservedRange*
                stageOneReservationLedger,
            ulong stageOneReservationCount,
            ulong stageOneReservationCapacity)
        {
            WriteSuccessLine(console, serial, 0);

            ulong coordinatorStatus =
                (ulong)M01LiveAllocatorStatus.Uninitialized;
            ulong coordinatorCheckpoint =
                (ulong)M01LiveAllocatorCheckpoint.Uninitialized;
            if (!M01StageOneAdapter.IsFinalStageOneSuccess(
                    stageOneState,
                    stageOneStateCapacity))
            {
                return Fail(
                    console,
                    serial,
                    bootServices,
                    M01StageTwoAdapterStatus.StageOneRejected,
                    coordinatorStatus,
                    coordinatorCheckpoint,
                    NotAttemptedStatus,
                    0,
                    false);
            }

            if (!ValidateStageOneReservationStorage(
                    stageOneReservationLedger,
                    stageOneReservationCount,
                    stageOneReservationCapacity))
            {
                return Fail(
                    console,
                    serial,
                    bootServices,
                    M01StageTwoAdapterStatus
                        .StageOneReservationLedgerRejected,
                    coordinatorStatus,
                    coordinatorCheckpoint,
                    NotAttemptedStatus,
                    0,
                    false);
            }

            if (bootServices == null
                || bootServices->AllocatePages == null
                || bootServices->FreePages == null
                || bootServices->GetMemoryMap == null)
            {
                return Fail(
                    console,
                    serial,
                    bootServices,
                    M01StageTwoAdapterStatus.MissingFirmwareBoundary,
                    coordinatorStatus,
                    coordinatorCheckpoint,
                    NotAttemptedStatus,
                    0,
                    false);
            }

            FirmwareMemoryMapRecord stageOneMap =
                EfiEntry.s_bootHandoff.MemoryMap;
            if (!stageOneMap.HasOwnedCopy())
            {
                return Fail(
                    console,
                    serial,
                    bootServices,
                    M01StageTwoAdapterStatus
                        .StageOneMemoryMapRejected,
                    coordinatorStatus,
                    coordinatorCheckpoint,
                    NotAttemptedStatus,
                    0,
                    false);
            }

            ulong previousMapKey = stageOneMap.MapKey;
            fixed (BootMemoryRegionRecord* allocationRecord =
                &s_allocationRecord)
            fixed (M01StageTwoReservationLedgerBuffer*
                ledgerStorage = &s_reservationLedger)
            fixed (M01LiveAllocatorStaticState* staticState =
                &s_staticState)
            {
                OS.Kernel.Memory.BootReservedRange*
                    reservationLedger = &ledgerStorage->Range0;
                if (!ValidateStaticStorage(
                        allocationRecord,
                        reservationLedger,
                        staticState))
                {
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        M01StageTwoAdapterStatus
                            .StaticStorageRejected,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        NotAttemptedStatus,
                        0,
                        false);
                }

                if (!IsZeroedStaticState(staticState))
                {
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        M01StageTwoAdapterStatus
                            .StaticStateNotDefault,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        NotAttemptedStatus,
                        0,
                        false);
                }

                *allocationRecord = default;
                ClearReservationLedger(reservationLedger);

                ulong arenaAddress = 0;
                ulong arenaFirmwareStatus =
                    bootServices->AllocatePages(
                        AllocateAnyPages,
                        EfiLoaderData,
                        AllocatorArenaPageCount,
                        &arenaAddress);
                bool arenaAllocated =
                    arenaFirmwareStatus == EfiSuccess;
                if (arenaFirmwareStatus != EfiSuccess
                    || !M01StageOneAdapter
                        .TryValidateStageTwoAllocation(
                            arenaAddress,
                            AllocatorArenaPageCount,
                            out ulong arenaByteLength,
                            out ulong arenaEndExclusive))
                {
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        arenaFirmwareStatus == EfiSuccess
                            ? M01StageTwoAdapterStatus
                                .ArenaAllocationRejected
                            : M01StageTwoAdapterStatus
                                .ArenaAllocationFailed,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        arenaFirmwareStatus,
                        arenaAddress,
                        arenaAllocated);
                }

                bool mapRecaptured =
                    M01StageOneAdapter
                        .TryRecaptureStageTwoMemoryMap(
                            bootServices,
                            out FirmwareMemoryMapRecord
                                refreshedMap,
                            out ulong mapFirmwareStatus);
                if (!mapRecaptured)
                {
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        M01StageTwoAdapterStatus
                            .MemoryMapRecaptureFailed,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        mapFirmwareStatus,
                        arenaAddress,
                        arenaAllocated);
                }

                if (refreshedMap.MapKey == previousMapKey)
                {
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        M01StageTwoAdapterStatus
                            .MemoryMapKeyUnchanged,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        mapFirmwareStatus,
                        arenaAddress,
                        arenaAllocated);
                }

                if (!M01StageOneAdapter
                    .StageTwoMapContainsAllocation(
                        refreshedMap,
                        arenaAddress,
                        arenaEndExclusive,
                        EfiLoaderData))
                {
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        M01StageTwoAdapterStatus
                            .MemoryMapAllocationTypeMismatch,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        mapFirmwareStatus,
                        arenaAddress,
                        arenaAllocated);
                }

                *allocationRecord =
                    new BootMemoryRegionRecord(
                        EfiLoaderData,
                        arenaAddress,
                        arenaAddress,
                        AllocatorArenaPageCount,
                        0,
                        arenaByteLength,
                        arenaEndExclusive);
                M01LiveAllocatorAllocationEvidence
                    allocationEvidence =
                    M01LiveAllocatorAllocationEvidence
                        .CaptureCallerAttested(
                            allocationRecord,
                            AllocationRecordCount,
                            AllocationRecordCapacity,
                            ArenaAllocationOrdinal,
                            arenaFirmwareStatus);
                if (!allocationEvidence.IsValidFor(
                        allocationRecord,
                        AllocationRecordCount,
                        AllocationRecordCapacity))
                {
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        M01StageTwoAdapterStatus
                            .AllocationEvidenceRejected,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        arenaFirmwareStatus,
                        arenaAddress,
                        arenaAllocated);
                }

                M01StageTwoAdapterStatus ledgerStatus =
                    BuildReservationLedger(
                        stageOneReservationLedger,
                        reservationLedger,
                        arenaAddress,
                        out OS.Kernel.Memory.BootReservedRange
                            arenaReservation);
                if (ledgerStatus != M01StageTwoAdapterStatus.Success)
                {
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        ledgerStatus,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        arenaFirmwareStatus,
                        arenaAddress,
                        arenaAllocated);
                }

                M01LiveAllocatorMapLedgerEvidence
                    mapLedgerEvidence =
                    M01LiveAllocatorMapLedgerEvidence
                        .CaptureCallerAttested(
                            previousMapKey,
                            PreviousMapGeneration,
                            refreshedMap,
                            RefreshedMapGeneration,
                            allocationEvidence,
                            MapCaptureOrdinal,
                            LedgerBuildOrdinal,
                            reservationLedger,
                            ReservationCount,
                            ReservationCapacity);
                if (!mapLedgerEvidence.IsValidFor(
                        allocationEvidence,
                        reservationLedger,
                        ReservationCount,
                        ReservationCapacity))
                {
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        M01StageTwoAdapterStatus
                            .MapLedgerEvidenceRejected,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        arenaFirmwareStatus,
                        arenaAddress,
                        arenaAllocated);
                }

                M01LiveAllocatorIntegrationResult result =
                    M01StageOneAdapter
                        .ExecuteStageTwoCoordinator(
                            stageOneState,
                            stageOneStateCapacity,
                            allocationRecord,
                            AllocationRecordCount,
                            AllocationRecordCapacity,
                            allocationEvidence,
                            mapLedgerEvidence,
                            reservationLedger,
                            ReservationCount,
                            ReservationCapacity,
                            staticState,
                            M01LiveAllocatorStaticState
                                .RequiredStorageBytes);
                coordinatorStatus = (ulong)result.Status;
                coordinatorCheckpoint =
                    (ulong)result.LastCompletedCheckpoint;
                if (!ValidateCompletedResult(
                        &result,
                        stageOneState,
                        stageOneStateCapacity,
                        allocationRecord,
                        allocationEvidence,
                        mapLedgerEvidence,
                        reservationLedger,
                        staticState,
                        arenaReservation,
                        arenaAddress,
                        arenaEndExclusive,
                        arenaByteLength))
                {
                    result = default;
                    *staticState = default;
                    return Fail(
                        console,
                        serial,
                        bootServices,
                        M01StageTwoAdapterStatus
                            .CoordinatorResultRejected,
                        coordinatorStatus,
                        coordinatorCheckpoint,
                        arenaFirmwareStatus,
                        arenaAddress,
                        arenaAllocated);
                }

                result = default;
                for (int lineIndex = 1;
                    lineIndex
                        < BootSequencePlan
                            .M01StageTwoSuccessLineCount;
                    lineIndex++)
                {
                    WriteSuccessLine(
                        console,
                        serial,
                        lineIndex);
                }

#if M01_OWNED_PAGING_PROBE
                if (!M01StageThreeAdapter.RunAfterStageTwo(
                        stageOneState,
                        stageOneStateCapacity,
                        staticState,
                        reservationLedger,
                        ReservationCount,
                        ReservationCapacity,
                        bootServices,
                        console,
                        serial))
                {
                    return false;
                }
#endif

#if M04_MANAGED_HEAP_PAGE_RANGE_PROBE
                // Every completed-state consumer is finished. This call mutates the
                // original allocator; the earlier M01 pass now records history.
                fixed (ManagedHeapPageRangeState* heap = &s_heapState)
                {
                    if (!M04ManagedHeapAdapter.Checkpoint(true, staticState,
                            allocationRecord, reservationLedger, heap, console, serial))
                    {
                        // The stage-one caller immediately enters its existing Spin.
                        return false;
                    }
                }
#endif

                return true;
            }
        }

        private static bool ValidateStageOneReservationStorage(
            OS.Kernel.Memory.BootReservedRange* reservationLedger,
            ulong reservationCount,
            ulong reservationCapacity)
        {
            ulong address = (ulong)reservationLedger;
            ulong byteLength =
                StageOneReservationCapacity
                * (ulong)sizeof(
                    OS.Kernel.Memory.BootReservedRange);
            if (reservationLedger == null
                || reservationCount != StageOneReservationCount
                || reservationCapacity
                    != StageOneReservationCapacity
                || (address
                    & (M01LiveAllocatorIntegration
                        .RequiredPointerAlignment - 1)) != 0
                || address > ~0UL - byteLength)
            {
                return false;
            }

            OS.Kernel.Memory
                .BootReservationTableValidationResult validation =
                OS.Kernel.Memory
                    .BootReservationTableValidator
                    .Validate(
                        reservationLedger,
                        reservationCount,
                        reservationCapacity);
            return validation.Succeeded
                && validation.ReservationCount
                    == StageOneReservationCount
                && validation.AvailableReservationCapacity
                    == StageOneReservationCapacity;
        }

        private static bool ValidateStaticStorage(
            BootMemoryRegionRecord* allocationRecord,
            OS.Kernel.Memory.BootReservedRange* reservationLedger,
            M01LiveAllocatorStaticState* staticState)
        {
            return (ulong)sizeof(
                    M01StageTwoReservationLedgerBuffer)
                    == ReservationCapacity
                        * (ulong)sizeof(
                            OS.Kernel.Memory.BootReservedRange)
                && (ulong)sizeof(M01LiveAllocatorStaticState)
                    == M01LiveAllocatorStaticState
                        .RequiredStorageBytes
                && allocationRecord != null
                && reservationLedger != null
                && staticState != null
                && (((ulong)allocationRecord
                    | (ulong)reservationLedger
                    | (ulong)staticState)
                    & (M01LiveAllocatorIntegration
                        .RequiredPointerAlignment - 1)) == 0;
        }

        private static bool IsZeroedStaticState(
            M01LiveAllocatorStaticState* staticState)
        {
            if (staticState == null)
            {
                return false;
            }

            byte* bytes = (byte*)staticState;
            for (ulong index = 0;
                index
                    < M01LiveAllocatorStaticState
                        .RequiredStorageBytes;
                index++)
            {
                if (bytes[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StaticStatesEqual(
            M01LiveAllocatorStaticState* left,
            M01LiveAllocatorStaticState* right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            byte* leftBytes = (byte*)left;
            byte* rightBytes = (byte*)right;
            for (ulong index = 0;
                index
                    < M01LiveAllocatorStaticState
                        .RequiredStorageBytes;
                index++)
            {
                if (leftBytes[index] != rightBytes[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void ClearReservationLedger(
            OS.Kernel.Memory.BootReservedRange* reservationLedger)
        {
            for (ulong index = 0;
                index < ReservationCapacity;
                index++)
            {
                reservationLedger[index] = default;
            }
        }

        private static M01StageTwoAdapterStatus
            BuildReservationLedger(
                OS.Kernel.Memory.BootReservedRange*
                    stageOneReservationLedger,
                OS.Kernel.Memory.BootReservedRange*
                    reservationLedger,
                ulong arenaAddress,
                out OS.Kernel.Memory.BootReservedRange
                    arenaReservation)
        {
            arenaReservation = default;
            if (stageOneReservationLedger == null
                || reservationLedger == null
                || PhysicalAddress.TryCreateFromUefiBoundary(
                        arenaAddress,
                        out PhysicalAddress arenaStart)
                    != AddressValueStatus.Success
                || OS.Kernel.Memory.BootReservedRange
                    .TryCreate(
                        OS.Kernel.Memory.BootReservationKind
                            .AllocatorArena,
                        arenaStart,
                        AllocatorArenaPageCount,
                        out arenaReservation)
                    != OS.Kernel.Memory
                        .BootReservedRangeStatus.Success)
            {
                return M01StageTwoAdapterStatus
                    .ArenaReservationRejected;
            }

            for (ulong index = 0;
                index < StageOneReservationCount;
                index++)
            {
                reservationLedger[index] =
                    stageOneReservationLedger[index];
            }

            reservationLedger[StageOneReservationCount] =
                arenaReservation;
            for (ulong index = 1;
                index < ReservationCount;
                index++)
            {
                OS.Kernel.Memory.BootReservedRange current =
                    reservationLedger[index];
                ulong destination = index;
                while (destination != 0)
                {
                    AddressValueStatus comparisonStatus =
                        reservationLedger[destination - 1]
                            .PhysicalStart.TryCompareTo(
                                current.PhysicalStart,
                                out int comparison);
                    if (comparisonStatus
                        != AddressValueStatus.Success)
                    {
                        return M01StageTwoAdapterStatus
                            .ReservationSortRejected;
                    }

                    if (comparison <= 0)
                    {
                        break;
                    }

                    reservationLedger[destination] =
                        reservationLedger[destination - 1];
                    destination--;
                }

                reservationLedger[destination] = current;
            }

            OS.Kernel.Memory
                .BootReservationTableValidationResult validation =
                OS.Kernel.Memory
                    .BootReservationTableValidator
                    .Validate(
                        reservationLedger,
                        ReservationCount,
                        ReservationCapacity);
            return validation.Succeeded
                && validation.ReservationCount == ReservationCount
                && validation.AvailableReservationCapacity
                    == ReservationCapacity
                ? M01StageTwoAdapterStatus.Success
                : M01StageTwoAdapterStatus
                    .ReservationLedgerRejected;
        }

        private static bool ValidateCompletedResult(
            M01LiveAllocatorIntegrationResult* result,
            void* stageOneState,
            ulong stageOneStateCapacity,
            BootMemoryRegionRecord* allocationRecord,
            M01LiveAllocatorAllocationEvidence
                allocationEvidence,
            M01LiveAllocatorMapLedgerEvidence
                mapLedgerEvidence,
            OS.Kernel.Memory.BootReservedRange*
                reservationLedger,
            M01LiveAllocatorStaticState* staticState,
            OS.Kernel.Memory.BootReservedRange
                arenaReservation,
            ulong arenaAddress,
            ulong arenaEndExclusive,
            ulong arenaByteLength)
        {
            if (result == null
                || !result->Succeeded
                || result->Status
                    != M01LiveAllocatorStatus.Success
                || result->LastCompletedCheckpoint
                    != M01LiveAllocatorCheckpoint
                        .DoubleFreeRejected
                || staticState == null)
            {
                return false;
            }

            M01LiveAllocatorStaticState resultState =
                result->StaticState;
            if (!staticState->IsCompleted
                || !resultState.IsCompleted
                || !StaticStatesEqual(
                    staticState,
                    &resultState)
                || result->Evidence.StageOneStateAddress
                    != (ulong)stageOneState
                || result->Evidence.StageOneStateCapacity
                    != stageOneStateCapacity
                || result->Evidence.StaticStateAddress
                    != (ulong)staticState
                || result->Evidence.StaticStateCapacity
                    != M01LiveAllocatorStaticState
                        .RequiredStorageBytes
                || !result->Evidence.AllocationEvidence
                    .IsValidFor(
                        allocationRecord,
                        AllocationRecordCount,
                        AllocationRecordCapacity)
                || !result->Evidence.MapLedgerEvidence
                    .IsValidFor(
                        allocationEvidence,
                        reservationLedger,
                        ReservationCount,
                        ReservationCapacity)
                || !mapLedgerEvidence.IsValidFor(
                        allocationEvidence,
                        reservationLedger,
                        ReservationCount,
                        ReservationCapacity)
                || !result->Evidence.ArenaReservation
                    .Equals(arenaReservation)
                || !resultState.ArenaReservation
                    .Equals(arenaReservation)
                || !staticState->ArenaReservation
                    .Equals(arenaReservation)
                || resultState.RefreshedMapKey
                    != mapLedgerEvidence.RefreshedMap.MapKey
                || resultState.RefreshedMapGeneration
                    != RefreshedMapGeneration
                || resultState.DoubleFreeStatus
                    != OS.Kernel.Memory
                        .BootPageAllocatorMutationStatus
                        .FreeRangeNotAllocated
                || !result->Evidence.Free.Succeeded
                || !result->Evidence.DoubleFree.IsInitialized
                || result->Evidence.DoubleFree.Succeeded
                || result->Evidence.DoubleFree.Status
                    != OS.Kernel.Memory
                        .BootPageAllocatorMutationStatus
                        .FreeRangeNotAllocated)
            {
                return false;
            }

            OS.Kernel.Memory.BootPageFreeListNode freeNode =
                resultState.FreeNode;
            OS.Kernel.Memory.BootPageAllocatorState allocatorState =
                resultState.AllocatorState;
            if (freeNode.PhysicalStart != arenaAddress
                || freeNode.PhysicalEndExclusive
                    != arenaEndExclusive
                || freeNode.PageCount
                    != AllocatorArenaPageCount
                || freeNode.ByteLength != arenaByteLength
                || freeNode.NextNodeIndex != -1
                || resultState.InitialFreePages
                    != AllocatorArenaPageCount
                || resultState.InitialFreeBytes
                    != arenaByteLength
                || allocatorState.FreePageCount
                    != AllocatorArenaPageCount
                || allocatorState.FreeByteCount
                    != arenaByteLength
                || allocatorState.AllocatedPageCount != 0
                || allocatorState.AllocatedByteCount != 0
                || resultState.AllocatedPageStart
                    .TryGetRawValueForUefiBoundary(
                        out ulong allocatedStart)
                    != AddressValueStatus.Success
                || resultState.AllocatedPageEndExclusive
                    .TryGetRawValueForUefiBoundary(
                        out ulong allocatedEnd)
                    != AddressValueStatus.Success
                || allocatedStart < arenaAddress
                || allocatedStart > ~0UL - PageSize
                || allocatedEnd != allocatedStart + PageSize
                || allocatedEnd > arenaEndExclusive)
            {
                return false;
            }

            return true;
        }

        private static bool Fail(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiBootServices* bootServices,
            M01StageTwoAdapterStatus adapterStatus,
            ulong coordinatorStatus,
            ulong coordinatorCheckpoint,
            ulong firmwareStatus,
            ulong arenaAddress,
            bool arenaAllocated)
        {
            ulong arenaCleanupStatus = NotAttemptedStatus;
            if (arenaAllocated
                && bootServices != null
                && bootServices->FreePages != null)
            {
                arenaCleanupStatus =
                    bootServices->FreePages(
                        arenaAddress,
                        AllocatorArenaPageCount);
            }

            WriteFailure(
                console,
                serial,
                adapterStatus,
                coordinatorStatus,
                coordinatorCheckpoint,
                firmwareStatus,
                arenaCleanupStatus);
            return false;
        }

        private static void WriteSuccessLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            int lineIndex)
        {
            int length =
                BootSequencePlan
                    .GetM01StageTwoSuccessLineLength(
                        lineIndex);
            for (int offset = 0;
                offset < length;
                offset++)
            {
                EfiEntry.Write(
                    console,
                    serial,
                    BootSequencePlan
                        .GetM01StageTwoSuccessLineByte(
                            lineIndex,
                            offset));
            }

            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteFailure(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            M01StageTwoAdapterStatus adapterStatus,
            ulong coordinatorStatus,
            ulong coordinatorCheckpoint,
            ulong firmwareStatus,
            ulong arenaCleanupStatus)
        {
            WriteFailurePrefix(console, serial, false);
            WriteFailureFields(
                console,
                serial,
                adapterStatus,
                coordinatorStatus,
                coordinatorCheckpoint,
                firmwareStatus,
                arenaCleanupStatus);
            WriteFailurePrefix(console, serial, true);
            WriteFailureFields(
                console,
                serial,
                adapterStatus,
                coordinatorStatus,
                coordinatorCheckpoint,
                firmwareStatus,
                arenaCleanupStatus);
        }

        private static void WriteFailurePrefix(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            bool stagePrefix)
        {
            if (stagePrefix)
            {
                WriteAscii(
                    console,
                    serial,
                    (byte)'M',
                    (byte)'0',
                    (byte)'1',
                    (byte)'_',
                    (byte)'S',
                    (byte)'T',
                    (byte)'A',
                    (byte)'G');
                WriteAscii(
                    console,
                    serial,
                    (byte)'E',
                    (byte)'2',
                    (byte)'_',
                    (byte)'F',
                    (byte)'A',
                    (byte)'I',
                    (byte)'L',
                    (byte)' ');
                return;
            }

            WriteAscii(
                console,
                serial,
                (byte)'T',
                (byte)'E',
                (byte)'S',
                (byte)'T',
                (byte)'_',
                (byte)'F',
                (byte)'A',
                (byte)'I');
            WriteAscii(
                console,
                serial,
                (byte)'L',
                (byte)' ',
                (byte)'m',
                (byte)'0',
                (byte)'1',
                (byte)'.',
                (byte)'s',
                (byte)'t');
            WriteAscii(
                console,
                serial,
                (byte)'a',
                (byte)'g',
                (byte)'e',
                (byte)'2',
                (byte)' ',
                0,
                0,
                0);
        }

        private static void WriteFailureFields(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            M01StageTwoAdapterStatus adapterStatus,
            ulong coordinatorStatus,
            ulong coordinatorCheckpoint,
            ulong firmwareStatus,
            ulong arenaCleanupStatus)
        {
            WriteAscii(
                console,
                serial,
                (byte)'s',
                (byte)'t',
                (byte)'a',
                (byte)'t',
                (byte)'u',
                (byte)'s',
                (byte)'=',
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                coordinatorStatus);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'a',
                (byte)'d',
                (byte)'a',
                (byte)'p',
                (byte)'t',
                (byte)'e',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'=',
                0,
                0,
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                (ulong)adapterStatus);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'c',
                (byte)'h',
                (byte)'e',
                (byte)'c',
                (byte)'k',
                (byte)'p',
                (byte)'o');
            WriteAscii(
                console,
                serial,
                (byte)'i',
                (byte)'n',
                (byte)'t',
                (byte)'=',
                0,
                0,
                0,
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                coordinatorCheckpoint);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'f',
                (byte)'i',
                (byte)'r',
                (byte)'m',
                (byte)'w',
                (byte)'a',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'=',
                0,
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                firmwareStatus);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'f',
                (byte)'r',
                (byte)'e',
                (byte)'e',
                (byte)'_',
                (byte)'a',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'n',
                (byte)'a',
                (byte)'=',
                0,
                0,
                0,
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                arenaCleanupStatus);
            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteAscii(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            byte b0,
            byte b1,
            byte b2,
            byte b3,
            byte b4,
            byte b5,
            byte b6,
            byte b7)
        {
            if (b0 != 0)
                EfiEntry.Write(console, serial, b0);
            if (b1 != 0)
                EfiEntry.Write(console, serial, b1);
            if (b2 != 0)
                EfiEntry.Write(console, serial, b2);
            if (b3 != 0)
                EfiEntry.Write(console, serial, b3);
            if (b4 != 0)
                EfiEntry.Write(console, serial, b4);
            if (b5 != 0)
                EfiEntry.Write(console, serial, b5);
            if (b6 != 0)
                EfiEntry.Write(console, serial, b6);
            if (b7 != 0)
                EfiEntry.Write(console, serial, b7);
        }

    }
#else
    private static partial class M01StageTwoAdapter
    {
        public static bool RunAfterStageOne(void* stageOneState, ulong stageOneStateCapacity, EfiBootServices* bootServices, EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, OS.Kernel.Memory.BootReservedRange* stageOneReservationLedger, ulong stageOneReservationCount, ulong stageOneReservationCapacity)
        {
            return false;
        }
    }
#endif
}
#pragma warning restore 649
