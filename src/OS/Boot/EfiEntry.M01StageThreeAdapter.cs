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
#if M01_OWNED_PAGING_PROBE
    private static partial class M01StageThreeAdapter
    {
        private const ulong EfiSuccess = 0;
        private const uint AllocateAnyPages = 0;
        private const uint AllocateAddress = 2;
        private const uint EfiLoaderCode = 1;
        private const uint EfiLoaderData = 2;
        private const uint EfiConventionalMemory = 7;
        private const ulong PageSize = 4096;
        private const ulong CodePageCount = 1;
        private const ulong ProbePageCount = 1;
        private const ulong CodeAllocationOrdinal = 8;
        private const ulong ProbeAllocationOrdinal = 9;
        private const ulong TableAllocationOrdinal = 10;
        private const ulong MapCaptureOrdinal = 11;
        private const ulong LedgerBuildOrdinal = 12;
        private const ulong RefreshedMapGeneration = 4;
        private const ulong StageTwoReservationCount = 5;
        private const ulong StageTwoReservationCapacity = 5;
        private const ulong PlanningReservationCount = 7;
        private const ulong FinalReservationCount = 8;
        private const ulong FinalReservationCapacity = 8;
        private const ulong AllocationRecordCount = 3;
        private const ulong AllocationRecordCapacity = 3;
        private const ulong FixedRequirementCount = 4;
        private const ulong RequirementCount = 7;
        private const ulong RangeCapacity = 7;
        private const ulong PageImageCapacity = 64;
        private const ulong PageEntryCapacity = 32768;
        private const ulong StateSnapshotByteCount = 4096;
        private const ulong SemanticRecordCount = 16;
        private const ulong BindingCount = 6;
        private const ulong PatReaderEntryOffset = 128;
        private const ulong PatReaderByteLength = 49;
        private const ulong TransferThunkEntryOffset = 128;
        private const ulong TransferThunkByteLength = 68;
        private const ulong PlannerCallThunkEntryOffset = 256;
        private const ulong PlannerCallThunkByteLength = 39;
        private const ulong PlannerStackByteCount = 256 * 1024;
        private const int PlannerStackUlongCount = 32768;
        private const ulong PlannerStackMinimumUsableBytes =
            224 * 1024;
        private const ulong GateObservationOffset = 0x300;
        private const ulong TransitionCanary =
            0x4d30_315f_4352_3341UL;
        private const ulong ReplacementCanary =
            0x4d30_315f_4f4b_4159UL;
        private const ulong BoundaryCanary =
            0x4d30_3132_3547_4154UL;
        private const ulong PlannerStackLowCanary =
            0x4d30_3150_4c4e_4c4fUL;
        private const ulong PlannerStackHighCanary =
            0x4d30_3150_4c4e_4849UL;
        private const ulong PreparationContextSignature =
            0x4d30_3150_5245_5041UL;
        private const ulong NotAttemptedStatus =
            0xffff_ffff_ffff_ffffUL;

        private static byte s_runState;
        private static M01StageThreeAllocationRecordBuffer
            s_allocationRecords;
        private static M01StageThreeReservationLedgerBuffer
            s_reservationLedger;
        private static M01StageThreeFixedRequirementBuffer
            s_fixedRequirements;
        private static M01StageThreeRequirementBuffer
            s_requirements;
        private static M01StageThreeRangeBuffer s_ranges;
        private static M01StageThreePageImageBuffer s_pageImages;
        private static M01StageThreePageEntryBuffer s_pageEntries;
        private static M01StageThreeBindingBuffer s_bindings;
        private static M01StageThreeSemanticBuffer s_semantics;
        private static M01StageThreeStateSnapshotBuffer
            s_stateSnapshot;
        private static M01StageThreePlannerStackBuffer
            s_plannerStack;
        private static M01StageThreePreparationCallContext
            s_preparationContext;
        private static OS.Kernel.Memory
            .X64PagingModePreflightObservation
                s_preflightObservation;
        private static OS.Kernel.Memory
            .X64PagingModePreflightResult
                s_preflightScratch;
        private static M01LivePagingPreparationState
            s_preparationState;
        private static M01LivePagingArenaPlanResult s_arenaPlan;
        private static M01LivePagingPreparationResult
            s_preparation;
        private static M01LivePagingArenaMaterializationResult
            s_arenaMaterialization;
        private static M01OwnedCr3CompositeTransitionResult
            s_compositePreparation;
        private static M01OwnedCr3CompositeCallGateResult
            s_gatePreparation;
        private static M01OwnedCr3CompositeCallGateResult
            s_gateMaterialization;
        private static M01OwnedCr3CompositeCallGateResult
            s_gateReadback;
        private static M01OwnedCr3CompositeCallGateResult
            s_gateAudit;
#if M02_SAFE_STUB_PROBE
        private static OS.Kernel.Diagnostics
            .ExistingIn8Out8StubPageState
                s_m02PageState;
        private static OS.Kernel.Diagnostics
            .ExistingIn8Out8StubPageResult
                s_m02Materialization;
        private static OS.Kernel.Diagnostics
            .ExistingIn8Out8StubPageResult
                s_m02Readback;
        private static OS.Kernel.Diagnostics
            .ExistingIn8Out8StubPageResult
                s_m02Finalization;
        private static M02SafeStubPageIntegrationResult
            s_m02MaterializationAudit;
        private static M02SafeStubPageIntegrationResult
            s_m02ReadbackAudit;
        private static M02SafeStubPageIntegrationResult
            s_m02FinalizationAudit;
        private static M02SafeStubPageIntegrationResult
            s_m02ExecutionAudit;
#endif
#if M02_CHECKED_PORT_IO_BINDING_PROBE
        private static M02CheckedPortIoFunctionPointerIntegrationResult
            s_m02CheckedPortIoBinding;
#endif
        private static ulong s_rawIa32Pat;

#if M02_UNEXPECTED_VECTOR_PANIC_PROBE
        internal static bool TryGetUnexpectedPanicBinding(
            out M02CheckedPortIoFunctionPointerIntegrationResult binding)
        {
            binding = default;
            // This is the original retained M01 lease and its completed execution audit.
            // A newly supplied binding or a range check is not lifetime authority.
            if (s_runState != 2 || !s_m02Finalization.Succeeded ||
                !s_m02ExecutionAudit.Succeeded || !s_m02CheckedPortIoBinding.Succeeded ||
                s_m02CheckedPortIoBinding.PageAddress != s_m02Finalization.ModeledPageAddress ||
                s_m02Finalization.PageByteCount != PageSize)
                return false;
            binding = s_m02CheckedPortIoBinding;
            return true;
        }
#endif

#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
        internal static bool TryWriteM02OwnedGdtTssTerminalStartLine()
        {
            return TryWriteM02OwnedGdtTssTerminalDirectLine(
                M02OwnedGdtTssTerminalStartLine);
        }

        internal static bool TryWriteM02OwnedGdtTssTerminalUnexpectedReturnLine()
        {
            return TryWriteM02OwnedGdtTssTerminalDirectLine(
                M02OwnedGdtTssTerminalUnexpectedReturnLine);
        }

        private static bool TryWriteM02OwnedGdtTssTerminalDirectLine(
            int lineKind)
        {
            if (!s_m02CheckedPortIoBinding.Succeeded)
            {
                return false;
            }

            OS.Kernel.Diagnostics.Com1OwnedPortIoBackend backend =
                new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(
                    s_m02CheckedPortIoBinding.Binding);
            if (!backend.IsReady)
            {
                return false;
            }

            int lineLength =
                EfiEntry.GetM02OwnedGdtTssTerminalLineLength(lineKind);
            for (int byteIndex = 0;
                byteIndex < lineLength + 2;
                byteIndex++)
            {
                byte value;
                if (byteIndex < lineLength)
                {
                    if (!EfiEntry.TryGetM02OwnedGdtTssTerminalLineByte(
                            lineKind,
                            byteIndex,
                            out value))
                    {
                        return false;
                    }
                }
                else
                {
                    value = byteIndex == lineLength
                        ? (byte)0x0d
                        : (byte)0x0a;
                }

                bool transmitReady = false;
                for (int poll = 0; poll < 128; poll++)
                {
                    OS.Kernel.Diagnostics.Com1OwnedPortIoOperationResult
                        readResult = backend.ReadByte(
                            OS.Kernel.Diagnostics.Com1OwnedPortIoBackend
                                .LineStatusRegisterPort);
                    if (!readResult.Succeeded)
                    {
                        return false;
                    }

                    if ((readResult.Value & 0x20) != 0)
                    {
                        transmitReady = true;
                        break;
                    }
                }

                if (!transmitReady ||
                    !backend.WriteByte(
                            OS.Kernel.Diagnostics.Com1OwnedPortIoBackend
                                .DataRegisterPort,
                            value)
                        .Succeeded)
                {
                    return false;
                }
            }

            return true;
        }
#endif

#if M02_UD2_VECTOR_STATE_PROBE
        internal static bool TryWriteM02Ud2VectorStateStartLine()
        {
            if (!s_m02CheckedPortIoBinding.Succeeded)
            {
                return false;
            }

            OS.Kernel.Diagnostics.Com1OwnedPortIoBackend backend =
                new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(
                    s_m02CheckedPortIoBinding.Binding);
            if (!backend.IsReady)
            {
                return false;
            }

            const int lineLength = 18;
            for (int byteIndex = 0;
                byteIndex < lineLength + 2;
                byteIndex++)
            {
                byte value;
                if (byteIndex < lineLength)
                {
                    if (!EfiEntry.TryGetM02Ud2VectorStateStartLineByte(
                            byteIndex,
                            out value))
                    {
                        return false;
                    }
                }
                else
                {
                    value = byteIndex == lineLength
                        ? (byte)0x0d
                        : (byte)0x0a;
                }

                bool transmitReady = false;
                for (int poll = 0; poll < 128; poll++)
                {
                    OS.Kernel.Diagnostics.Com1OwnedPortIoOperationResult
                        readResult = backend.ReadByte(
                            OS.Kernel.Diagnostics.Com1OwnedPortIoBackend
                                .LineStatusRegisterPort);
                    if (!readResult.Succeeded)
                    {
                        return false;
                    }

                    if ((readResult.Value & 0x20) != 0)
                    {
                        transmitReady = true;
                        break;
                    }
                }

                if (!transmitReady ||
                    !backend.WriteByte(
                            OS.Kernel.Diagnostics.Com1OwnedPortIoBackend
                                .DataRegisterPort,
                            value)
                        .Succeeded)
                {
                    return false;
                }
            }

            return true;
        }
#endif

        // NativeAOT emits GS-cookie reads for the prerequisite planners'
        // bounded stackalloc workspaces. The no-CRT EFI link therefore needs
        // a local, readable symbol even though the check itself is emitted
        // inline and no CRT cookie helper is imported. The method is never
        // called; its stable code bytes provide the linker-visible storage
        // anchor until the later entropy/hardening milestone owns randomized
        // cookie initialization.
        [System.Runtime.RuntimeExport("__security_cookie")]
        private static ulong SecurityCookieStorageAnchor()
        {
            return 0x434c_524f_5347_5331UL;
        }

        public static bool RunAfterStageTwo(
            void* stageOneState,
            ulong stageOneStateCapacity,
            M01LiveAllocatorStaticState* stageTwoState,
            OS.Kernel.Memory.BootReservedRange*
                stageTwoReservationLedger,
            ulong stageTwoReservationCount,
            ulong stageTwoReservationCapacity,
            EfiBootServices* bootServices,
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            ulong codeAddress = 0;
            ulong probeAddress = 0;
            ulong tableAddress = 0;
            ulong tablePageCount = 0;
            ulong transferEntry = 0;
            ulong plannerCallEntry = 0;
            bool codeAllocated = false;
            bool probeAllocated = false;
            bool tableAllocated = false;
            ulong firmwareStatus = NotAttemptedStatus;

            if (s_runState != 0)
            {
                return FailBeforeEntry(
                    console,
                    serial,
                    bootServices,
                    M01StageThreeAdapterStatus.RepeatedUse,
                    firmwareStatus,
                    codeAddress,
                    codeAllocated,
                    probeAddress,
                    probeAllocated,
                    tableAddress,
                    tablePageCount,
                    tableAllocated);
            }
            s_runState = 1;

            WriteSuccessLine(console, serial, bootServices, 0);

            if (stageTwoState == null
                || !stageTwoState->IsCompleted
                || stageTwoState->RefreshedMapGeneration != 3
                || stageTwoReservationLedger == null
                || stageTwoReservationCount
                    != StageTwoReservationCount
                || stageTwoReservationCapacity
                    != StageTwoReservationCapacity
                || bootServices == null
                || bootServices->AllocatePages == null
                || bootServices->FreePages == null
                || bootServices->GetMemoryMap == null
                || !M01StageOneAdapter.TryGetFinalSwitchPlan(
                    stageOneState,
                    stageOneStateCapacity,
                    out OwnedBootStackSwitchPlanResult
                        stackSwitchPlan,
                    out BootMemoryRegionRecord
                        stageOneCodeAllocation)
                || !ValidateStageTwoLedger(
                    stageTwoReservationLedger,
                    stageTwoReservationCount,
                    stageTwoReservationCapacity))
            {
                return FailBeforeEntry(
                    console,
                    serial,
                    bootServices,
                    M01StageThreeAdapterStatus
                        .PrerequisiteRejected,
                    firmwareStatus,
                    codeAddress,
                    codeAllocated,
                    probeAddress,
                    probeAllocated,
                    tableAddress,
                    tablePageCount,
                    tableAllocated);
            }

            FirmwareMemoryMapRecord stageTwoMap =
                EfiEntry.s_bootHandoff.MemoryMap;
            ulong previousMapKey = stageTwoMap.MapKey;
            if (!stageTwoMap.HasOwnedCopy()
                || previousMapKey == 0)
            {
                return FailBeforeEntry(
                    console,
                    serial,
                    bootServices,
                    M01StageThreeAdapterStatus
                        .StageTwoMapRejected,
                    firmwareStatus,
                    codeAddress,
                    codeAllocated,
                    probeAddress,
                    probeAllocated,
                    tableAddress,
                    tablePageCount,
                    tableAllocated);
            }

            firmwareStatus = bootServices->AllocatePages(
                AllocateAnyPages,
                EfiLoaderCode,
                CodePageCount,
                &codeAddress);
            codeAllocated = firmwareStatus == EfiSuccess;
            if (!codeAllocated
                || !TryValidateAllocation(
                    codeAddress,
                    CodePageCount,
                    out ulong ignoredCodeBytes,
                    out ulong codeEnd)
                || !ValidateNewAllocationAgainstStageTwo(
                    stageTwoMap,
                    stageTwoReservationLedger,
                    stageTwoReservationCount,
                    codeAddress,
                    codeEnd))
            {
                return FailBeforeEntry(
                    console,
                    serial,
                    bootServices,
                    codeAllocated
                        ? M01StageThreeAdapterStatus
                            .CodeAllocationRejected
                        : M01StageThreeAdapterStatus
                            .CodeAllocationFailed,
                    firmwareStatus,
                    codeAddress,
                    codeAllocated,
                    probeAddress,
                    probeAllocated,
                    tableAddress,
                    tablePageCount,
                    tableAllocated);
            }
            WriteSuccessLine(console, serial, bootServices, 1);

            firmwareStatus = bootServices->AllocatePages(
                AllocateAnyPages,
                EfiLoaderCode,
                ProbePageCount,
                &probeAddress);
            probeAllocated = firmwareStatus == EfiSuccess;
            if (!probeAllocated
                || !TryValidateAllocation(
                    probeAddress,
                    ProbePageCount,
                    out ulong ignoredProbeBytes,
                    out ulong probeEnd)
                || RangesOverlap(
                    codeAddress,
                    codeEnd,
                    probeAddress,
                    probeEnd)
                || !ValidateNewAllocationAgainstStageTwo(
                    stageTwoMap,
                    stageTwoReservationLedger,
                    stageTwoReservationCount,
                    probeAddress,
                    probeEnd))
            {
                return FailBeforeEntry(
                    console,
                    serial,
                    bootServices,
                    probeAllocated
                        ? M01StageThreeAdapterStatus
                            .ProbeAllocationRejected
                        : M01StageThreeAdapterStatus
                            .ProbeAllocationFailed,
                    firmwareStatus,
                    codeAddress,
                    codeAllocated,
                    probeAddress,
                    probeAllocated,
                    tableAddress,
                    tablePageCount,
                    tableAllocated);
            }
            WriteSuccessLine(console, serial, bootServices, 2);

            fixed (M01StageThreeAllocationRecordBuffer*
                allocationStorage = &s_allocationRecords)
            fixed (M01StageThreeReservationLedgerBuffer*
                ledgerStorage = &s_reservationLedger)
            fixed (M01StageThreeFixedRequirementBuffer*
                fixedStorage = &s_fixedRequirements)
            fixed (M01StageThreeRequirementBuffer*
                requirementStorage = &s_requirements)
            fixed (M01StageThreeRangeBuffer*
                rangeStorage = &s_ranges)
            fixed (M01StageThreePageImageBuffer*
                pageImageStorage = &s_pageImages)
            fixed (M01StageThreePageEntryBuffer*
                pageEntryStorage = &s_pageEntries)
            fixed (M01StageThreeBindingBuffer*
                bindingStorage = &s_bindings)
            fixed (M01StageThreeSemanticBuffer*
                semanticStorage = &s_semantics)
            fixed (M01StageThreeStateSnapshotBuffer*
                stateSnapshotStorage = &s_stateSnapshot)
            fixed (OS.Kernel.Memory
                .X64PagingModePreflightObservation*
                    preflightObservation =
                        &s_preflightObservation)
            fixed (OS.Kernel.Memory
                .X64PagingModePreflightResult*
                    preflightScratch =
                        &s_preflightScratch)
            fixed (M01LivePagingPreparationState*
                preparationState = &s_preparationState)
            fixed (ulong* rawIa32Pat = &s_rawIa32Pat)
            {
                BootMemoryRegionRecord* allocationRecords =
                    &allocationStorage->Record0;
                OS.Kernel.Memory.BootReservedRange*
                    reservationLedger =
                        &ledgerStorage->Range0;
                OS.Kernel.Boot
                    .X64BootIdentityMappingRequirement*
                        fixedRequirements =
                            &fixedStorage->Requirement0;
                OS.Kernel.Boot
                    .X64BootIdentityMappingRequirement*
                        requirements =
                            &requirementStorage->Requirement0;
                OS.Kernel.Boot.X64BootIdentityMappingRange*
                    ranges = &rangeStorage->Range0;
                OS.Kernel.Memory.X64PageTableImagePage*
                    pageImages =
                        (OS.Kernel.Memory
                            .X64PageTableImagePage*)
                        &pageImageStorage->Values[0];
                ulong* pageEntries =
                    &pageEntryStorage->Values[0];
                M01OwnedCr3CompositeTransitionRangeBinding*
                    bindings = &bindingStorage->Binding0;
                M01OwnedCr3CompositeTransitionSemanticRecord*
                    semantics =
                        (M01OwnedCr3CompositeTransitionSemanticRecord*)
                        &semanticStorage->Values[0];
                byte* stateSnapshot =
                    (byte*)&stateSnapshotStorage->Values[0];

                ClearStageThreeStorage(
                    allocationRecords,
                    reservationLedger,
                    fixedRequirements,
                    requirements,
                    ranges,
                    pageImages,
                    pageEntries,
                    bindings,
                    semantics,
                    stateSnapshot,
                    preflightObservation,
                    preflightScratch,
                    preparationState,
                    rawIa32Pat);

                // Publish the transfer thunk before invoking the architectural
                // preflight. Once the preflight stream begins, its CPUID
                // serializes this earlier thunk publication; the thunk's later
                // CPUID then serializes the final gate-page publication before
                // transferring to it. The first control transfer into the
                // newly written preflight stream is only the functional x64
                // self-modifying-code boundary. It is not an immutable-text
                // serializer and does not close Intel SCSB hardening by itself.
                bool instructionPublicationRejected = false;
                if (!MaterializeTransferThunk(
                        stageOneCodeAllocation,
                        out transferEntry)
                    || !MaterializePlannerCallThunk(
                        stageOneCodeAllocation,
                        out plannerCallEntry)
                    || !CaptureArchitecturalPreflight(
                        codeAddress,
                        preflightObservation,
                        preflightScratch,
                        rawIa32Pat,
                        console,
                        serial,
                        out OS.Kernel.Memory
                            .X64PageTableEncodingPreconditions
                                preconditions,
                        out instructionPublicationRejected))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        instructionPublicationRejected
                            ? M01StageThreeAdapterStatus
                                .InstructionPublicationRejected
                            : M01StageThreeAdapterStatus
                                .ArchitecturalPreflightRejected,
                        firmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                if (!BuildPlanningLedger(
                        stageTwoReservationLedger,
                        codeAddress,
                        probeAddress,
                        reservationLedger)
                    || !BuildFixedRequirements(
                        reservationLedger,
                        codeAddress,
                        probeAddress,
                        fixedRequirements))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .PlanningInputRejected,
                        firmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                if (!TryPlanArenaFromCapturedMap(
                        stageTwoMap,
                        fixedRequirements,
                        reservationLedger,
                        requirements,
                        ranges,
                        preconditions,
                        out s_arenaPlan)
                    || !s_arenaPlan.Succeeded
                    || !TryRaw(
                        s_arenaPlan.CandidatePhysicalBase,
                        out tableAddress))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .ArenaPlanRejected,
                        firmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                tablePageCount = s_arenaPlan.TotalPageCount;
                ulong requestedTableAddress = tableAddress;
                firmwareStatus = bootServices->AllocatePages(
                    AllocateAddress,
                    EfiLoaderData,
                    tablePageCount,
                    &tableAddress);
                tableAllocated = firmwareStatus == EfiSuccess;
                if (!tableAllocated
                    || tableAddress != requestedTableAddress
                    || !TryValidateAllocation(
                        tableAddress,
                        tablePageCount,
                        out ulong tableByteLength,
                        out ulong tableEnd)
                    || RangesOverlap(
                        codeAddress,
                        codeEnd,
                        tableAddress,
                        tableEnd)
                    || RangesOverlap(
                        probeAddress,
                        probeEnd,
                        tableAddress,
                        tableEnd)
                    || tableByteLength
                        != s_arenaPlan.TotalByteLength
                    || !ValidateNewAllocationAgainstStageTwo(
                        stageTwoMap,
                        reservationLedger,
                        PlanningReservationCount,
                        tableAddress,
                        tableEnd))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        tableAllocated
                            ? M01StageThreeAdapterStatus
                                .TableAllocationRejected
                            : M01StageThreeAdapterStatus
                                .TableAllocationFailed,
                        firmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }
                WriteSuccessLine(console, serial, bootServices, 3);

                if (!M01StageOneAdapter
                        .TryRecaptureStageTwoMemoryMap(
                            bootServices,
                            out FirmwareMemoryMapRecord
                                refreshedMap,
                            out ulong mapFirmwareStatus)
                    || refreshedMap.MapKey == previousMapKey
                    || !TryDescribeExactAllocation(
                        refreshedMap,
                        codeAddress,
                        CodePageCount,
                        EfiLoaderCode,
                        out allocationRecords[0])
                    || !TryDescribeExactAllocation(
                        refreshedMap,
                        probeAddress,
                        ProbePageCount,
                        EfiLoaderCode,
                        out allocationRecords[1])
                    || !TryDescribeExactAllocation(
                        refreshedMap,
                        tableAddress,
                        tablePageCount,
                        EfiLoaderData,
                        out allocationRecords[2])
                    || allocationRecords[2].Attribute
                        != s_arenaPlan.AllocationRecord.Attribute
                    || !BuildFinalLedger(
                        stageTwoReservationLedger,
                        codeAddress,
                        probeAddress,
                        tableAddress,
                        tablePageCount,
                        reservationLedger))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .PostAllocationMapRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                M01LivePagingAllocationEvidence
                    allocationEvidence =
                        M01LivePagingAllocationEvidence
                            .CaptureCallerAttested(
                                allocationRecords,
                                AllocationRecordCount,
                                AllocationRecordCapacity,
                                CodeAllocationOrdinal,
                                ProbeAllocationOrdinal,
                                TableAllocationOrdinal,
                                EfiSuccess,
                                EfiSuccess,
                                EfiSuccess);
                M01LivePagingMapLedgerEvidence mapEvidence =
                    M01LivePagingMapLedgerEvidence
                        .CaptureCallerAttested(
                            previousMapKey,
                            stageTwoState
                                ->RefreshedMapGeneration,
                            refreshedMap,
                            RefreshedMapGeneration,
                            allocationEvidence,
                            MapCaptureOrdinal,
                            LedgerBuildOrdinal,
                            reservationLedger,
                            FinalReservationCount,
                            FinalReservationCapacity);
                if (!allocationEvidence.IsValidFor(
                        allocationRecords,
                        AllocationRecordCount,
                        AllocationRecordCapacity)
                    || !mapEvidence.IsValidFor(
                        *stageTwoState,
                        allocationEvidence,
                        reservationLedger,
                        FinalReservationCount,
                        FinalReservationCapacity))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .EvidenceRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }
                WriteSuccessLine(console, serial, bootServices, 4);

                ClearRanges(ranges);
                if (!RunPreparationOnPlannerStack(
                        plannerCallEntry,
                        stageOneCodeAllocation,
                        stageTwoState,
                        stackSwitchPlan,
                        allocationRecords,
                        allocationEvidence,
                        mapEvidence,
                        reservationLedger,
                        requirements,
                        ranges,
                        pageImages,
                        pageEntries,
                        preconditions,
                        (byte*)codeAddress,
                        (byte*)probeAddress,
                        preflightObservation,
                        preparationState)
                    || !s_preparation.Succeeded
                    || !s_preparation
                        .PreparedForCompositeTransition
                    || !s_preparation
                        .UsesExactLiveIntervalBindings
                    || s_preparation.State.MappingRangeCount == 0
                    || s_preparation.State.MappingRangeCount
                        > RangeCapacity
                    || s_preparation.State.PageImageCount
                        != s_arenaPlan.RequiredPageImageCount
                    || s_preparation.State.PageEntryCount
                        != s_arenaPlan
                            .RequiredPageTableEntryCount
                    || !TryRaw(
                        s_preparation.State.OwnedRoot,
                        out ulong preparedRoot)
                    || !TryRaw(
                        s_arenaPlan.RootTable,
                        out ulong plannedRoot)
                    || preparedRoot != plannedRoot)
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .PreparationRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                if (!TryRaw(
                        s_arenaPlan.StatePage,
                        out ulong statePageAddress))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .StatePageRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }
                ZeroBytes(
                    (byte*)statePageAddress,
                    PageSize);

                s_arenaMaterialization =
                    M01LivePagingIntegration
                        .MaterializeOwnedTableArena(
                            s_arenaPlan,
                            allocationRecords[2],
                            pageImages,
                            s_preparation.State
                                .PageImageCount,
                            PageImageCapacity,
                            pageEntries,
                            s_preparation.State
                                .PageEntryCount,
                            PageEntryCapacity,
                            (byte*)tableAddress,
                            s_arenaPlan.TotalByteLength,
                            s_arenaPlan.TotalByteLength,
                            M01LivePagingArenaByteMode
                                .EmitAndVerify,
                            stateSnapshot,
                            StateSnapshotByteCount,
                            StateSnapshotByteCount);
                if (!s_arenaMaterialization.Succeeded)
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .ArenaMaterializationRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                s_arenaMaterialization =
                    M01LivePagingIntegration
                        .MaterializeOwnedTableArena(
                            s_arenaPlan,
                            allocationRecords[2],
                            pageImages,
                            s_preparation.State
                                .PageImageCount,
                            PageImageCapacity,
                            pageEntries,
                            s_preparation.State
                                .PageEntryCount,
                            PageEntryCapacity,
                            (byte*)tableAddress,
                            s_arenaPlan.TotalByteLength,
                            s_arenaPlan.TotalByteLength,
                            M01LivePagingArenaByteMode
                                .VerifyExisting,
                            stateSnapshot,
                            StateSnapshotByteCount,
                            StateSnapshotByteCount);
                if (!s_arenaMaterialization.Succeeded
                    || !BuildBindings(
                        allocationRecords,
                        reservationLedger,
                        ranges,
                        s_preparation.State
                            .MappingRangeCount,
                        statePageAddress,
                        preparedRoot,
                        bindings))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .BindingOrReadbackRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                M01OwnedCr3CompositeTransitionRuntimeState*
                    runtimeState =
                        (M01OwnedCr3CompositeTransitionRuntimeState*)
                            statePageAddress;
                ulong* transitionCanary =
                    (ulong*)(statePageAddress
                        + M01OwnedCr3CompositeTransitionRuntimeState
                            .StructureSize);
                ulong observationAddressRaw =
                    statePageAddress + GateObservationOffset;
                M01OwnedCr3CompositeCallGateObservation*
                    gateObservation =
                        (M01OwnedCr3CompositeCallGateObservation*)
                            observationAddressRaw;
                if (!TryVirtual(
                        statePageAddress,
                        out VirtualAddress stateAddress)
                    || !TryVirtual(
                        codeAddress
                            + M01OwnedCr3CompositeTransition
                                .CallNextReturnSiteOffset,
                        out VirtualAddress returnAddress)
                    || !TryVirtual(
                        (ulong)transitionCanary,
                        out VirtualAddress canaryAddress)
                    || !TryVirtual(
                        observationAddressRaw,
                        out VirtualAddress observationAddress))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .RuntimeAddressRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                fixed (M01LivePagingPreparationResult*
                    preparation = &s_preparation)
                {
                    s_compositePreparation =
                        M01OwnedCr3CompositeTransition
                            .CreateRuntimeState(
                                preparation,
                                1,
                                1,
                                allocationRecords,
                                AllocationRecordCount,
                                AllocationRecordCapacity,
                                reservationLedger,
                                FinalReservationCount,
                                FinalReservationCapacity,
                                ranges,
                                s_preparation.State
                                    .MappingRangeCount,
                                RangeCapacity,
                                pageImages,
                                s_preparation.State
                                    .PageImageCount,
                                PageImageCapacity,
                                pageEntries,
                                s_preparation.State
                                    .PageEntryCount,
                                PageEntryCapacity,
                                bindings,
                                BindingCount,
                                BindingCount,
                                runtimeState,
                                1,
                                1,
                                transitionCanary,
                                1,
                                1,
                                stateAddress,
                                preparedRoot,
                                returnAddress,
                                canaryAddress,
                                TransitionCanary,
                                ReplacementCanary);
                }
                if (!s_compositePreparation.IsStatePrepared)
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .RuntimeStateRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                fixed (M01LivePagingPreparationResult*
                    preparation = &s_preparation)
                fixed (M01OwnedCr3CompositeTransitionResult*
                    composite = &s_compositePreparation)
                {
                    s_gatePreparation =
                        M01OwnedCr3CompositeTransition
                            .PrepareCallGate(
                                preparation,
                                1,
                                1,
                                composite,
                                1,
                                1,
                                allocationRecords,
                                AllocationRecordCount,
                                AllocationRecordCapacity,
                                reservationLedger,
                                FinalReservationCount,
                                FinalReservationCapacity,
                                ranges,
                                s_preparation.State
                                    .MappingRangeCount,
                                RangeCapacity,
                                &bindings[0],
                                1,
                                1,
                                gateObservation,
                                1,
                                1,
                                observationAddress,
                                BoundaryCanary);
                }
                if (!s_gatePreparation.IsPrepared)
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .GatePreparationRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                fixed (M01OwnedCr3CompositeCallGateResult*
                    gatePreparation = &s_gatePreparation)
                {
                    s_gateMaterialization =
                        M01OwnedCr3CompositeTransition
                            .TryMaterializeCallGate(
                                gatePreparation,
                                1,
                                1,
                                runtimeState,
                                1,
                                1,
                                (byte*)codeAddress,
                                PageSize,
                                PageSize,
                                codeAddress,
                                semantics,
                                SemanticRecordCount,
                                SemanticRecordCount);
                }
                if (!s_gateMaterialization.IsMaterialized)
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .GateMaterializationRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

#if M02_SAFE_STUB_PROBE
                if (!PrepareM02SafeStubPage(
                        console,
                        serial,
                        bootServices,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated))
                {
                    return false;
                }
#endif

                fixed (M01OwnedCr3CompositeCallGateResult*
                    gateMaterialization =
                        &s_gateMaterialization)
                {
                    s_gateReadback =
                        M01OwnedCr3CompositeTransition
                            .ValidateCallGateReadback(
                                gateMaterialization,
                                1,
                                1,
                                runtimeState,
                                1,
                                1,
                                (byte*)codeAddress,
                                PageSize,
                                PageSize,
                                codeAddress,
                                semantics,
                                SemanticRecordCount,
                                SemanticRecordCount);
                }
                if (!s_gateReadback.IsReadbackVerified
                    || s_gateReadback.GateEntryAddress
                        != codeAddress
                            + M01OwnedCr3CompositeTransition
                                .CallGateOffset
                    || s_gateReadback.ReturnSiteAddress
                        != codeAddress
                            + M01OwnedCr3CompositeTransition
                                .CallNextReturnSiteOffset
                    || s_gateReadback.Fnv1a64
                        != M01OwnedCr3CompositeTransition
                            .RequiredCombinedFnv1a64
                    || !ValidateProtectionProbeReadback(
                        (byte*)probeAddress)
                    || !ValidateTransferThunkReadback(
                        stageOneCodeAllocation,
                        transferEntry)
                    || !ValidatePlannerCallThunkReadback(
                        stageOneCodeAllocation,
                        plannerCallEntry))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .FinalReadbackRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                WriteSuccessLine(console, serial, bootServices, 5);
                WriteSuccessLine(console, serial, bootServices, 6);
                WriteSuccessLine(console, serial, bootServices, 7);

                if (!ValidatePreTransferState(
                        preflightObservation,
                        gateObservation,
                        runtimeState,
                        s_gateReadback,
                        transferEntry))
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .PreTransferRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }

                delegate* unmanaged<
                    M01OwnedCr3CompositeTransitionRuntimeState*,
                    M01OwnedCr3CompositeCallGateObservation*,
                    ulong,
                    ulong,
                    uint> transfer =
                        (delegate* unmanaged<
                            M01OwnedCr3CompositeTransitionRuntimeState*,
                            M01OwnedCr3CompositeCallGateObservation*,
                            ulong,
                            ulong,
                            uint>)(void*)transferEntry;

                WriteSuccessLine(console, serial, bootServices, 8);

                if (EfiEntry
                        .s_m01InstructionPublicationEpochMask
                    != 7)
                {
                    return RejectInstructionPublicationBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }
                uint epochFourPublicationCookie =
                    global::OS.Boot.X64InstructionPublication.Serialize();
                if (epochFourPublicationCookie
                    != global::OS.Boot.X64InstructionPublication
                        .ExpectedSuccessCookie)
                {
                    return RejectInstructionPublicationBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }
                EfiEntry.s_m01InstructionPublicationEpochMask = 15;
                uint runtimeReturn = transfer(
                    runtimeState,
                    gateObservation,
                    s_gateReadback.GateEntryAddress,
                    s_preparation.State.OriginalRflags);

                fixed (M01OwnedCr3CompositeCallGateResult*
                    gateReadback = &s_gateReadback)
                {
                    s_gateAudit =
                        M01OwnedCr3CompositeTransition
                            .AuditCallGateObservation(
                                gateReadback,
                                1,
                                1,
                                runtimeState,
                                1,
                                1,
                                gateObservation,
                                1,
                                1,
                                M01OwnedCr3CompositeTransitionTerminalKind
                                    .Returned,
                                runtimeReturn);
                }

                if (!s_gateAudit.IsObservedAudit)
                {
                    return FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        (ulong)s_gateAudit.Status);
                }

                for (int lineIndex = 9;
                    lineIndex
                        < BootSequencePlan
                            .M01StageThreeSuccessLineCount;
                    lineIndex++)
                {
                    if (lineIndex == 13)
                    {
                        if (EfiEntry
                                .s_m01InstructionPublicationEpochMask
                            != 15)
                        {
                            return FailAfterEntry(
                                M01StageThreeAdapterStatus
                                    .InstructionPublicationRejected,
                                EfiEntry
                                    .s_m01InstructionPublicationEpochMask);
                        }
                        EfiEntry
                            .WriteM01InstructionPublicationEpochsVerified(
                                console,
                                serial);
                    }
                    WriteSuccessLine(
                        console,
                        serial,
                        bootServices,
                        lineIndex);
                }

                s_runState = 2;
                return true;
            }
        }

        private static bool ValidateStageTwoLedger(
            OS.Kernel.Memory.BootReservedRange* ledger,
            ulong count,
            ulong capacity)
        {
            OS.Kernel.Memory.BootReservationTableValidationResult
                validation =
                    OS.Kernel.Memory
                        .BootReservationTableValidator
                        .Validate(ledger, count, capacity);
            return validation.Succeeded
                && validation.ReservationCount
                    == StageTwoReservationCount
                && validation.AvailableReservationCapacity
                    == StageTwoReservationCapacity;
        }

        private static void ClearStageThreeStorage(
            BootMemoryRegionRecord* allocationRecords,
            OS.Kernel.Memory.BootReservedRange*
                reservationLedger,
            OS.Kernel.Boot.X64BootIdentityMappingRequirement*
                fixedRequirements,
            OS.Kernel.Boot.X64BootIdentityMappingRequirement*
                requirements,
            OS.Kernel.Boot.X64BootIdentityMappingRange* ranges,
            OS.Kernel.Memory.X64PageTableImagePage* pageImages,
            ulong* pageEntries,
            M01OwnedCr3CompositeTransitionRangeBinding*
                bindings,
            M01OwnedCr3CompositeTransitionSemanticRecord*
                semantics,
            byte* stateSnapshot,
            OS.Kernel.Memory.X64PagingModePreflightObservation*
                preflightObservation,
            OS.Kernel.Memory.X64PagingModePreflightResult*
                preflightScratch,
            M01LivePagingPreparationState* preparationState,
            ulong* rawIa32Pat)
        {
            for (ulong index = 0;
                index < AllocationRecordCapacity;
                index++)
            {
                allocationRecords[index] = default;
            }
            for (ulong index = 0;
                index < FinalReservationCapacity;
                index++)
            {
                reservationLedger[index] = default;
            }
            for (ulong index = 0;
                index < FixedRequirementCount;
                index++)
            {
                fixedRequirements[index] = default;
            }
            for (ulong index = 0;
                index < RequirementCount;
                index++)
            {
                requirements[index] = default;
                ranges[index] = default;
            }
            for (ulong index = 0;
                index < PageImageCapacity;
                index++)
            {
                pageImages[index] = default;
            }
            for (ulong index = 0;
                index < PageEntryCapacity;
                index++)
            {
                pageEntries[index] = 0;
            }
            for (ulong index = 0;
                index < BindingCount;
                index++)
            {
                bindings[index] = default;
            }
            for (ulong index = 0;
                index < SemanticRecordCount;
                index++)
            {
                semantics[index] = default;
            }
            for (ulong index = 0;
                index < StateSnapshotByteCount;
                index++)
            {
                stateSnapshot[index] = 0;
            }
            *preflightObservation = default;
            *preflightScratch = default;
            *preparationState = default;
            *rawIa32Pat = 0;
            s_arenaPlan = default;
            s_preparation = default;
            s_arenaMaterialization = default;
            s_compositePreparation = default;
            s_gatePreparation = default;
            s_gateMaterialization = default;
            s_gateReadback = default;
            s_gateAudit = default;
#if M02_SAFE_STUB_PROBE
            s_m02PageState =
                M02SafeStubPageIntegration.CreatePageState();
            s_m02Materialization = default;
            s_m02Readback = default;
            s_m02Finalization = default;
            s_m02MaterializationAudit = default;
            s_m02ReadbackAudit = default;
            s_m02FinalizationAudit = default;
            s_m02ExecutionAudit = default;
#endif
#if M02_CHECKED_PORT_IO_BINDING_PROBE
            s_m02CheckedPortIoBinding = default;
#endif
        }

        private static bool CaptureArchitecturalPreflight(
            ulong codeAddress,
            OS.Kernel.Memory.X64PagingModePreflightObservation*
                observation,
            OS.Kernel.Memory.X64PagingModePreflightResult*
                scratch,
            ulong* rawIa32Pat,
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            out OS.Kernel.Memory
                .X64PageTableEncodingPreconditions
                    preconditions,
            out bool instructionPublicationRejected)
        {
            preconditions = default;
            instructionPublicationRejected = false;
            if (codeAddress == 0
                || (codeAddress & (PageSize - 1)) != 0
                || observation == null
                || scratch == null
                || rawIa32Pat == null)
            {
                return false;
            }

            byte* codePage = (byte*)codeAddress;
            ZeroBytes(codePage, PageSize);
            *observation = default;
            *scratch = default;
            *rawIa32Pat = 0;

            OS.Kernel.Memory.X64PagingModePreflightStubResult
                materialized =
                    OS.Kernel.Memory
                        .X64PagingModePreflightStubPlan
                        .TryMaterialize(
                            codePage,
                            PageSize,
                            0,
                            OS.Kernel.Memory
                                .X64PagingModePreflightStubPlan
                                .RequiredByteLength,
                            observation,
                            OS.Kernel.Memory
                                .X64PagingModePreflightObservation
                                .StructureSize,
                            scratch,
                            OS.Kernel.Memory
                                .X64PagingModePreflightResult
                                .StructureSize);
            if (!materialized.Succeeded)
            {
                return false;
            }

            OS.Kernel.Memory.X64PagingModePreflightStubResult
                readback =
                    OS.Kernel.Memory
                        .X64PagingModePreflightStubPlan
                        .TryValidateMaterialized(
                            codePage,
                            PageSize,
                            0,
                            OS.Kernel.Memory
                                .X64PagingModePreflightStubPlan
                                .RequiredByteLength,
                            observation,
                            OS.Kernel.Memory
                                .X64PagingModePreflightObservation
                                .StructureSize,
                            scratch,
                            OS.Kernel.Memory
                                .X64PagingModePreflightResult
                                .StructureSize);
            if (!readback.Succeeded
                || PatReaderEntryOffset
                    < OS.Kernel.Memory
                        .X64PagingModePreflightStubPlan
                        .RequiredByteLength
                || PatReaderEntryOffset
                    > PageSize - PatReaderByteLength)
            {
                return false;
            }

            byte* patReader =
                codePage + PatReaderEntryOffset;
            for (ulong index = 0;
                index < PatReaderByteLength;
                index++)
            {
                patReader[index] = GetPatReaderByte(index);
            }
            for (ulong index = 0;
                index < PatReaderByteLength;
                index++)
            {
                if (patReader[index]
                    != GetPatReaderByte(index))
                {
                    return false;
                }
            }

            delegate* unmanaged<
                OS.Kernel.Memory
                    .X64PagingModePreflightObservation*,
                void> preflight =
                    (delegate* unmanaged<
                        OS.Kernel.Memory
                            .X64PagingModePreflightObservation*,
                        void>)(void*)codeAddress;
            delegate* unmanaged<ulong*, uint> readPat =
                (delegate* unmanaged<ulong*, uint>)
                    (void*)(codeAddress
                        + PatReaderEntryOffset);

            if (EfiEntry
                    .s_m01InstructionPublicationEpochMask
                != 3)
            {
                instructionPublicationRejected = true;
                EfiEntry
                    .WriteM01InstructionPublicationCookieFailure(
                        console,
                        serial);
                return false;
            }
            uint epochThreePublicationCookie =
                global::OS.Boot.X64InstructionPublication.Serialize();
            if (epochThreePublicationCookie
                != global::OS.Boot.X64InstructionPublication
                    .ExpectedSuccessCookie)
            {
                instructionPublicationRejected = true;
                EfiEntry
                    .WriteM01InstructionPublicationCookieFailure(
                        console,
                        serial);
                return false;
            }
            EfiEntry.s_m01InstructionPublicationEpochMask = 7;
            preflight(observation);
            if (!observation->IsInitialized)
            {
                return false;
            }

            if (readPat(rawIa32Pat) != 1)
            {
                return false;
            }

            ulong cr0 = observation->Cr0;
            ulong cr4 = observation->RawCr4;
            ulong efer = observation->Ia32Efer;
            ulong rflags = observation->Rflags;
            if ((cr0
                    & OS.Kernel.Memory
                        .X64PagingModePreflightStubPlan
                        .Cr0PagingBit) == 0
                || (cr0
                    & OS.Kernel.Memory
                        .X64PagingModePreflightStubPlan
                        .Cr0WriteProtectBit) == 0
                || (cr4
                    & OS.Kernel.Memory
                        .X64PagingModePreflightStubPlan
                        .Cr4PaeBit) == 0
                || (cr4
                    & (OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .Cr4La57Bit
                        | OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .Cr4PcideBit
                        | M01OwnedCr3CompositeTransition
                            .Cr4ControlFlowEnforcementBit)) != 0
                || (efer
                    & (OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .EferLongModeEnableBit
                        | OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .EferLongModeActiveBit
                        | OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .EferNoExecuteEnableBit))
                    != (OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .EferLongModeEnableBit
                        | OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .EferLongModeActiveBit
                        | OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .EferNoExecuteEnableBit)
                || (rflags
                    & OS.Kernel.Memory
                        .X64PagingModePreflightStubPlan
                        .RflagsArchitecturalOneBit) == 0
                || (rflags
                    & (M01OwnedCr3CompositeTransition
                            .RflagsTrapFlagBit
                        | (1UL << 10))) != 0
                || observation->PhysicalAddressWidth
                    < OS.Kernel.Memory
                        .X64PagingModePreflightStubPlan
                        .MinimumPhysicalAddressWidth
                || observation->PhysicalAddressWidth
                    > OS.Kernel.Memory
                        .X64PagingModePreflightStubPlan
                        .MaximumPhysicalAddressWidth)
            {
                return false;
            }

            OS.Kernel.Memory.X64PageTableEntryStatus
                patStatus =
                    OS.Kernel.Memory.X64PatMappingToken
                        .TryCreate(
                            *rawIa32Pat,
                            0,
                            OS.Kernel.Memory.X64PatMappingToken
                                .UnsupportedIndex,
                            OS.Kernel.Memory.X64PatMappingToken
                                .UnsupportedIndex,
                            out OS.Kernel.Memory
                                .X64PatMappingToken pat);
            if (patStatus
                != OS.Kernel.Memory
                    .X64PageTableEntryStatus.Success)
            {
                return false;
            }

            preconditions =
                OS.Kernel.Memory
                    .X64PageTableEncodingPreconditions
                    .CreateObserved(
                        observation->PhysicalAddressWidth,
                        nxeEnabled: true,
                        writeProtectEnabled: true,
                        fiveLevelPagingEnabled: false,
                        oneGiBPagesSupported: false,
                        pat);
            return preconditions.Validate()
                == OS.Kernel.Memory
                    .X64PageTableEntryStatus.Success;
        }

        private static byte GetPatReaderByte(ulong index)
        {
            return index switch
            {
                0 => 0xf3,
                1 => 0x0f,
                2 => 0x1e,
                3 => 0xfa,
                4 => 0x53,
                5 => 0x49,
                6 => 0x89,
                7 => 0xc8,
                8 => 0xb8,
                9 => 0x01,
                10 => 0x00,
                11 => 0x00,
                12 => 0x00,
                13 => 0x0f,
                14 => 0xa2,
                15 => 0x0f,
                16 => 0xba,
                17 => 0xe2,
                18 => 0x10,
                19 => 0x73,
                20 => 0x18,
                21 => 0xb9,
                22 => 0x77,
                23 => 0x02,
                24 => 0x00,
                25 => 0x00,
                26 => 0x0f,
                27 => 0x32,
                28 => 0x48,
                29 => 0xc1,
                30 => 0xe2,
                31 => 0x20,
                32 => 0x48,
                33 => 0x09,
                34 => 0xd0,
                35 => 0x49,
                36 => 0x89,
                37 => 0x00,
                38 => 0xb8,
                39 => 0x01,
                40 => 0x00,
                41 => 0x00,
                42 => 0x00,
                43 => 0x5b,
                44 => 0xc3,
                45 => 0x31,
                46 => 0xc0,
                47 => 0x5b,
                48 => 0xc3,
                _ => 0
            };
        }

        private static bool BuildPlanningLedger(
            OS.Kernel.Memory.BootReservedRange*
                stageTwoLedger,
            ulong codeAddress,
            ulong probeAddress,
            OS.Kernel.Memory.BootReservedRange* destination)
        {
            if (stageTwoLedger == null || destination == null)
            {
                return false;
            }
            for (ulong index = 0;
                index < StageTwoReservationCount;
                index++)
            {
                destination[index] = stageTwoLedger[index];
            }

            if (!TryCreateReservation(
                    OS.Kernel.Memory.BootReservationKind
                        .Cr3TransitionStub,
                    codeAddress,
                    CodePageCount,
                    out destination[5])
                || !TryCreateReservation(
                    OS.Kernel.Memory.BootReservationKind
                        .PageProtectionProbe,
                    probeAddress,
                    ProbePageCount,
                    out destination[6])
                || !SortAndValidateLedger(
                    destination,
                    PlanningReservationCount,
                    PlanningReservationCount))
            {
                return false;
            }

            destination[7] = default;
            return true;
        }

        private static bool BuildFixedRequirements(
            OS.Kernel.Memory.BootReservedRange* planningLedger,
            ulong codeAddress,
            ulong probeAddress,
            OS.Kernel.Boot.X64BootIdentityMappingRequirement*
                fixedRequirements)
        {
            if (planningLedger == null
                || fixedRequirements == null
                || !FindReservation(
                    planningLedger,
                    PlanningReservationCount,
                    OS.Kernel.Memory.BootReservationKind
                        .BootStackGuard,
                    out ulong guardStart,
                    out ulong guardEnd,
                    out ulong ignoredGuardIndex)
                || !FindReservation(
                    planningLedger,
                    PlanningReservationCount,
                    OS.Kernel.Memory.BootReservationKind
                        .BootStack,
                    out ulong stackStart,
                    out ulong stackEnd,
                    out ulong ignoredStackIndex)
                || !TryCreateRequirement(
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .BootStackGuard,
                    guardStart,
                    guardEnd,
                    OS.Kernel.Memory.PageProtection.None,
                    OS.Kernel.Memory.PageCachePolicy
                        .Uninitialized,
                    out fixedRequirements[0])
                || !TryCreateRequirement(
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .CompositeCodeAndCallGate,
                    codeAddress,
                    codeAddress + PageSize,
                    OS.Kernel.Memory.PageProtection.Read
                        | OS.Kernel.Memory
                            .PageProtection.Execute,
                    OS.Kernel.Memory.PageCachePolicy.WriteBack,
                    out fixedRequirements[1])
                || !TryCreateRequirement(
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .CompositeOwnedStack,
                    stackStart,
                    stackEnd,
                    OS.Kernel.Memory.PageProtection.Read
                        | OS.Kernel.Memory
                            .PageProtection.Write,
                    OS.Kernel.Memory.PageCachePolicy.WriteBack,
                    out fixedRequirements[2])
                || !TryCreateRequirement(
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .ProtectionProbePage,
                    probeAddress,
                    probeAddress + PageSize,
                    OS.Kernel.Memory.PageProtection.Read
                        | OS.Kernel.Memory
                            .PageProtection.Write,
                    OS.Kernel.Memory.PageCachePolicy.WriteBack,
                    out fixedRequirements[3]))
            {
                return false;
            }

            return true;
        }

        private static bool TryPlanArenaFromCapturedMap(
            FirmwareMemoryMapRecord map,
            OS.Kernel.Boot.X64BootIdentityMappingRequirement*
                fixedRequirements,
            OS.Kernel.Memory.BootReservedRange* reservations,
            OS.Kernel.Boot.X64BootIdentityMappingRequirement*
                requirements,
            OS.Kernel.Boot.X64BootIdentityMappingRange* ranges,
            OS.Kernel.Memory.X64PageTableEncodingPreconditions
                preconditions,
            out M01LivePagingArenaPlanResult plan)
        {
            plan = default;
            if (!map.HasOwnedCopy()
                || map.BufferAddress == 0
                || map.MapSize == 0
                || map.DescriptorSize < 40
                || (map.DescriptorSize & 7) != 0
                || map.DescriptorCount == 0
                || map.DescriptorCount > 4096
                || map.DescriptorCount
                    > ~0UL / map.DescriptorSize
                || map.MapSize
                    != map.DescriptorCount
                        * map.DescriptorSize
                || fixedRequirements == null
                || reservations == null
                || requirements == null
                || ranges == null)
            {
                return false;
            }

            byte* mapBytes = (byte*)map.BufferAddress;
            for (ulong descriptorIndex = 0;
                descriptorIndex < map.DescriptorCount;
                descriptorIndex++)
            {
                byte* descriptor =
                    mapBytes
                    + descriptorIndex * map.DescriptorSize;
                uint type = ReadUInt32(descriptor);
                ulong descriptorStart =
                    ReadUInt64(descriptor + 8);
                ulong descriptorVirtual =
                    ReadUInt64(descriptor + 16);
                ulong descriptorPages =
                    ReadUInt64(descriptor + 24);
                ulong descriptorAttribute =
                    ReadUInt64(descriptor + 32);
                if (type != EfiConventionalMemory
                    || descriptorStart == 0
                    || (descriptorStart
                        & (PageSize - 1)) != 0
                    || descriptorPages == 0
                    || descriptorPages > ~0UL / PageSize)
                {
                    continue;
                }

                ulong descriptorBytes =
                    descriptorPages * PageSize;
                if (descriptorStart
                    > ~0UL - descriptorBytes)
                {
                    continue;
                }
                ulong descriptorEnd =
                    descriptorStart + descriptorBytes;
                BootMemoryRegionRecord conventional =
                    new(
                        EfiConventionalMemory,
                        descriptorStart,
                        descriptorVirtual,
                        descriptorPages,
                        descriptorAttribute,
                        descriptorBytes,
                        descriptorEnd);

                if (TryPlanCandidate(
                        fixedRequirements,
                        conventional,
                        reservations,
                        descriptorStart,
                        requirements,
                        ranges,
                        preconditions,
                        out plan))
                {
                    return true;
                }

                for (ulong reservationIndex = 0;
                    reservationIndex
                        < PlanningReservationCount;
                    reservationIndex++)
                {
                    if (!TryGetReservationBounds(
                            reservations[reservationIndex],
                            out ulong ignoredStart,
                            out ulong reservationEnd)
                        || reservationEnd < descriptorStart
                        || reservationEnd >= descriptorEnd)
                    {
                        continue;
                    }

                    if (TryPlanCandidate(
                            fixedRequirements,
                            conventional,
                            reservations,
                            reservationEnd,
                            requirements,
                            ranges,
                            preconditions,
                            out plan))
                    {
                        return true;
                    }
                }
            }

            plan = default;
            return false;
        }

        private static bool TryPlanCandidate(
            OS.Kernel.Boot.X64BootIdentityMappingRequirement*
                fixedRequirements,
            BootMemoryRegionRecord conventional,
            OS.Kernel.Memory.BootReservedRange* reservations,
            ulong candidate,
            OS.Kernel.Boot.X64BootIdentityMappingRequirement*
                requirements,
            OS.Kernel.Boot.X64BootIdentityMappingRange* ranges,
            OS.Kernel.Memory.X64PageTableEncodingPreconditions
                preconditions,
            out M01LivePagingArenaPlanResult plan)
        {
            plan = default;
            if ((candidate & (PageSize - 1)) != 0
                || PhysicalAddress.TryCreateFromUefiBoundary(
                    candidate,
                    out PhysicalAddress candidateAddress)
                    != AddressValueStatus.Success)
            {
                return false;
            }

            for (ulong index = 0;
                index < RequirementCount;
                index++)
            {
                requirements[index] = default;
                ranges[index] = default;
            }

            plan =
                M01LivePagingIntegration
                    .PlanOwnedTableArena(
                        fixedRequirements,
                        FixedRequirementCount,
                        FixedRequirementCount,
                        conventional,
                        reservations,
                        PlanningReservationCount,
                        PlanningReservationCount,
                        candidateAddress,
                        preconditions,
                        M01LivePagingIntegration
                            .MaximumArenaIterationCount,
                        requirements,
                        0,
                        RequirementCount,
                        ranges,
                        0,
                        RangeCapacity);
            return plan.Succeeded
                && plan.TotalPageCount
                    >= M01LivePagingIntegration
                        .MinimumArenaTotalPageCount
                && plan.TotalPageCount
                    <= M01LivePagingIntegration
                        .MaximumArenaTotalPageCount
                && plan.RequiredRequirementWorkspaceCount
                    == RequirementCount
                && plan.RequiredPageImageCount
                    <= PageImageCapacity
                && plan.RequiredPageTableEntryCount
                    <= PageEntryCapacity;
        }

        private static bool TryCreateRequirement(
            OS.Kernel.Boot.X64BootIdentityMappingPurpose purpose,
            ulong start,
            ulong endExclusive,
            OS.Kernel.Memory.PageProtection protection,
            OS.Kernel.Memory.PageCachePolicy cachePolicy,
            out OS.Kernel.Boot
                .X64BootIdentityMappingRequirement requirement)
        {
            requirement = default;
            if (start == 0
                || start >= endExclusive
                || (start & (PageSize - 1)) != 0
                || (endExclusive & (PageSize - 1)) != 0
                || PhysicalAddress.TryCreateFromUefiBoundary(
                    start,
                    out PhysicalAddress physicalStart)
                    != AddressValueStatus.Success
                || PhysicalAddress.TryCreateFromUefiBoundary(
                    endExclusive,
                    out PhysicalAddress physicalEnd)
                    != AddressValueStatus.Success)
            {
                return false;
            }

            ulong byteLength = endExclusive - start;
            ulong pageCount = byteLength / PageSize;
            requirement =
                new OS.Kernel.Boot
                    .X64BootIdentityMappingRequirement(
                        purpose,
                        physicalStart,
                        physicalEnd,
                        pageCount,
                        byteLength,
                        protection,
                        cachePolicy);
            return requirement.IsInitialized;
        }

        private static bool TryDescribeExactAllocation(
            FirmwareMemoryMapRecord map,
            ulong targetStart,
            ulong targetPageCount,
            uint expectedType,
            out BootMemoryRegionRecord record)
        {
            record = default;
            if (!map.HasOwnedCopy()
                || targetStart == 0
                || (targetStart & (PageSize - 1)) != 0
                || targetPageCount == 0
                || targetPageCount > ~0UL / PageSize
                || map.BufferAddress == 0
                || map.DescriptorSize < 40
                || (map.DescriptorSize & 7) != 0
                || map.DescriptorCount == 0
                || map.DescriptorCount > 4096
                || map.DescriptorCount
                    > ~0UL / map.DescriptorSize)
            {
                return false;
            }

            ulong targetBytes = targetPageCount * PageSize;
            if (targetStart > ~0UL - targetBytes)
            {
                return false;
            }
            ulong targetEnd = targetStart + targetBytes;
            byte* mapBytes = (byte*)map.BufferAddress;
            ulong cursor = targetStart;
            ulong attribute = 0;
            bool attributeSet = false;

            for (ulong segment = 0;
                segment < map.DescriptorCount
                    && cursor < targetEnd;
                segment++)
            {
                bool found = false;
                ulong bestEnd = cursor;
                ulong bestAttribute = 0;
                for (ulong descriptorIndex = 0;
                    descriptorIndex < map.DescriptorCount;
                    descriptorIndex++)
                {
                    byte* descriptor =
                        mapBytes
                        + descriptorIndex * map.DescriptorSize;
                    uint type = ReadUInt32(descriptor);
                    ulong descriptorStart =
                        ReadUInt64(descriptor + 8);
                    ulong descriptorPages =
                        ReadUInt64(descriptor + 24);
                    ulong descriptorAttribute =
                        ReadUInt64(descriptor + 32);
                    if (type != expectedType
                        || descriptorPages == 0
                        || descriptorPages > ~0UL / PageSize)
                    {
                        continue;
                    }
                    ulong descriptorBytes =
                        descriptorPages * PageSize;
                    if (descriptorStart
                        > ~0UL - descriptorBytes)
                    {
                        return false;
                    }
                    ulong descriptorEnd =
                        descriptorStart + descriptorBytes;
                    if (descriptorStart <= cursor
                        && cursor < descriptorEnd
                        && descriptorEnd > bestEnd)
                    {
                        found = true;
                        bestEnd = descriptorEnd;
                        bestAttribute = descriptorAttribute;
                    }
                }

                if (!found
                    || (attributeSet
                        && bestAttribute != attribute))
                {
                    return false;
                }
                if (!attributeSet)
                {
                    attribute = bestAttribute;
                    attributeSet = true;
                }
                cursor = bestEnd < targetEnd
                    ? bestEnd
                    : targetEnd;
            }

            if (cursor != targetEnd || !attributeSet)
            {
                return false;
            }

            record = new BootMemoryRegionRecord(
                expectedType,
                targetStart,
                targetStart,
                targetPageCount,
                attribute,
                targetBytes,
                targetEnd);
            return record.PhysicalStart == targetStart
                && record.PhysicalEndExclusive == targetEnd
                && record.VirtualStart == targetStart
                && record.PageCount == targetPageCount
                && record.ByteLength == targetBytes;
        }

        private static bool BuildFinalLedger(
            OS.Kernel.Memory.BootReservedRange*
                stageTwoLedger,
            ulong codeAddress,
            ulong probeAddress,
            ulong tableAddress,
            ulong tablePageCount,
            OS.Kernel.Memory.BootReservedRange* destination)
        {
            if (!BuildPlanningLedger(
                    stageTwoLedger,
                    codeAddress,
                    probeAddress,
                    destination)
                || !TryCreateReservation(
                    OS.Kernel.Memory.BootReservationKind
                        .PageTableArena,
                    tableAddress,
                    tablePageCount,
                    out destination[7]))
            {
                return false;
            }

            return SortAndValidateLedger(
                destination,
                FinalReservationCount,
                FinalReservationCapacity);
        }

        private static bool TryCreateReservation(
            OS.Kernel.Memory.BootReservationKind kind,
            ulong start,
            ulong pageCount,
            out OS.Kernel.Memory.BootReservedRange reservation)
        {
            reservation = default;
            return PhysicalAddress.TryCreateFromUefiBoundary(
                    start,
                    out PhysicalAddress physicalStart)
                    == AddressValueStatus.Success
                && OS.Kernel.Memory.BootReservedRange.TryCreate(
                    kind,
                    physicalStart,
                    pageCount,
                    out reservation)
                    == OS.Kernel.Memory
                        .BootReservedRangeStatus.Success;
        }

        private static bool SortAndValidateLedger(
            OS.Kernel.Memory.BootReservedRange* ledger,
            ulong count,
            ulong capacity)
        {
            if (ledger == null
                || count == 0
                || count > capacity)
            {
                return false;
            }

            for (ulong index = 1; index < count; index++)
            {
                OS.Kernel.Memory.BootReservedRange current =
                    ledger[index];
                ulong destination = index;
                while (destination != 0)
                {
                    AddressValueStatus compareStatus =
                        ledger[destination - 1]
                            .PhysicalStart.TryCompareTo(
                                current.PhysicalStart,
                                out int comparison);
                    if (compareStatus != AddressValueStatus.Success)
                    {
                        return false;
                    }
                    if (comparison <= 0)
                    {
                        break;
                    }
                    ledger[destination] =
                        ledger[destination - 1];
                    destination--;
                }
                ledger[destination] = current;
            }

            OS.Kernel.Memory.BootReservationTableValidationResult
                validation =
                    OS.Kernel.Memory
                        .BootReservationTableValidator
                        .Validate(ledger, count, capacity);
            return validation.Succeeded
                && validation.ReservationCount == count
                && validation.AvailableReservationCapacity
                    == capacity;
        }

        private static bool FindReservation(
            OS.Kernel.Memory.BootReservedRange* ledger,
            ulong count,
            OS.Kernel.Memory.BootReservationKind kind,
            out ulong start,
            out ulong endExclusive,
            out ulong index)
        {
            start = 0;
            endExclusive = 0;
            index = M01LivePagingIntegration.NoIndex;
            bool found = false;
            for (ulong candidate = 0;
                candidate < count;
                candidate++)
            {
                if (ledger[candidate].Kind != kind)
                {
                    continue;
                }
                if (found
                    || !TryGetReservationBounds(
                        ledger[candidate],
                        out start,
                        out endExclusive))
                {
                    start = 0;
                    endExclusive = 0;
                    index = M01LivePagingIntegration.NoIndex;
                    return false;
                }
                found = true;
                index = candidate;
            }
            return found;
        }

        private static bool FindExactReservationIndex(
            OS.Kernel.Memory.BootReservedRange* ledger,
            ulong count,
            OS.Kernel.Memory.BootReservationKind kind,
            ulong start,
            ulong endExclusive,
            out ulong index)
        {
            index = M01LivePagingIntegration.NoIndex;
            bool found = false;
            for (ulong candidate = 0;
                candidate < count;
                candidate++)
            {
                if (ledger[candidate].Kind != kind
                    || !TryGetReservationBounds(
                        ledger[candidate],
                        out ulong candidateStart,
                        out ulong candidateEnd)
                    || candidateStart != start
                    || candidateEnd != endExclusive)
                {
                    continue;
                }
                if (found)
                {
                    index = M01LivePagingIntegration.NoIndex;
                    return false;
                }
                found = true;
                index = candidate;
            }
            return found;
        }

        private static void ClearRanges(
            OS.Kernel.Boot.X64BootIdentityMappingRange* ranges)
        {
            for (ulong index = 0;
                index < RangeCapacity;
                index++)
            {
                ranges[index] = default;
            }
        }

        private static bool BuildBindings(
            BootMemoryRegionRecord* allocations,
            OS.Kernel.Memory.BootReservedRange* reservations,
            OS.Kernel.Boot.X64BootIdentityMappingRange* ranges,
            ulong rangeCount,
            ulong statePageAddress,
            ulong rootAddress,
            M01OwnedCr3CompositeTransitionRangeBinding* bindings)
        {
            if (allocations == null
                || reservations == null
                || ranges == null
                || bindings == null
                || rangeCount == 0
                || rangeCount > RangeCapacity)
            {
                return false;
            }

            ulong codeStart = allocations[0].PhysicalStart;
            ulong codeEnd = allocations[0].PhysicalEndExclusive;
            ulong probeStart = allocations[1].PhysicalStart;
            ulong probeEnd = allocations[1].PhysicalEndExclusive;
            ulong tableStart = allocations[2].PhysicalStart;
            ulong tableEnd = allocations[2].PhysicalEndExclusive;
            if (!FindExactReservationIndex(
                    reservations,
                    FinalReservationCount,
                    OS.Kernel.Memory.BootReservationKind
                        .Cr3TransitionStub,
                    codeStart,
                    codeEnd,
                    out ulong codeReservation)
                || !FindExactReservationIndex(
                    reservations,
                    FinalReservationCount,
                    OS.Kernel.Memory.BootReservationKind
                        .PageProtectionProbe,
                    probeStart,
                    probeEnd,
                    out ulong probeReservation)
                || !FindExactReservationIndex(
                    reservations,
                    FinalReservationCount,
                    OS.Kernel.Memory.BootReservationKind
                        .PageTableArena,
                    tableStart,
                    tableEnd,
                    out ulong tableReservation)
                || !FindReservation(
                    reservations,
                    FinalReservationCount,
                    OS.Kernel.Memory.BootReservationKind
                        .BootStack,
                    out ulong stackStart,
                    out ulong stackEnd,
                    out ulong stackReservation)
                || codeReservation
                    != s_preparation.Evidence
                        .Cr3CodeReservationIndex
                || probeReservation
                    != s_preparation.Evidence
                        .ProtectionProbeReservationIndex
                || tableReservation
                    != s_preparation.Evidence
                        .TableStateReservationIndex
                || statePageAddress < tableStart
                || statePageAddress > tableEnd - PageSize
                || rootAddress < tableStart
                || rootAddress > tableEnd - PageSize)
            {
                return false;
            }

            if (!FindMappingIndex(
                    ranges,
                    rangeCount,
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .CompositeCodeAndCallGate,
                    codeStart,
                    codeEnd,
                    out ulong codeMapping)
                || !FindMappingIndex(
                    ranges,
                    rangeCount,
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .CompositeOwnedStack,
                    stackStart,
                    stackEnd,
                    out ulong stackMapping)
                || !FindMappingIndex(
                    ranges,
                    rangeCount,
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .ProtectionProbePage,
                    probeStart,
                    probeEnd,
                    out ulong probeMapping)
                || !FindMappingIndex(
                    ranges,
                    rangeCount,
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .CompleteTableAndStateArena,
                    tableStart,
                    tableEnd,
                    out ulong tableMapping)
                || !FindMappingIndex(
                    ranges,
                    rangeCount,
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .RuntimeStateAndCanaryPage,
                    statePageAddress,
                    statePageAddress + PageSize,
                    out ulong stateMapping)
                || !FindMappingIndex(
                    ranges,
                    rangeCount,
                    OS.Kernel.Boot
                        .X64BootIdentityMappingPurpose
                        .OwnedRootPage,
                    rootAddress,
                    rootAddress + PageSize,
                    out ulong rootMapping))
            {
                return false;
            }

            return TryCreateBinding(
                    M01OwnedCr3CompositeTransitionRangeRole
                        .CompositeCodeAndCallGate,
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .AllocationReservationAndIdentityMapping,
                    codeStart,
                    codeEnd,
                    0,
                    codeReservation,
                    codeMapping,
                    out bindings[0])
                && TryCreateBinding(
                    M01OwnedCr3CompositeTransitionRangeRole
                        .CompositeOwnedStack,
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .ReservationAndIdentityMapping,
                    stackStart,
                    stackEnd,
                    M01OwnedCr3CompositeTransitionRangeBinding
                        .NoIndex,
                    stackReservation,
                    stackMapping,
                    out bindings[1])
                && TryCreateBinding(
                    M01OwnedCr3CompositeTransitionRangeRole
                        .ProtectionProbePage,
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .AllocationReservationAndIdentityMapping,
                    probeStart,
                    probeEnd,
                    1,
                    probeReservation,
                    probeMapping,
                    out bindings[2])
                && TryCreateBinding(
                    M01OwnedCr3CompositeTransitionRangeRole
                        .CompleteTableAndStateArena,
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .AllocationReservationAndIdentityMapping,
                    tableStart,
                    tableEnd,
                    2,
                    tableReservation,
                    tableMapping,
                    out bindings[3])
                && TryCreateBinding(
                    M01OwnedCr3CompositeTransitionRangeRole
                        .RuntimeStateAndCanaryPage,
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .ContainedInAllocationReservationAndIdentityMapping,
                    statePageAddress,
                    statePageAddress + PageSize,
                    2,
                    tableReservation,
                    stateMapping,
                    out bindings[4])
                && TryCreateBinding(
                    M01OwnedCr3CompositeTransitionRangeRole
                        .OwnedRootPage,
                    M01OwnedCr3CompositeTransitionRangeProvenance
                        .ContainedInAllocationReservationAndIdentityMapping,
                    rootAddress,
                    rootAddress + PageSize,
                    2,
                    tableReservation,
                    rootMapping,
                    out bindings[5]);
        }

        private static bool FindMappingIndex(
            OS.Kernel.Boot.X64BootIdentityMappingRange* ranges,
            ulong count,
            OS.Kernel.Boot.X64BootIdentityMappingPurpose purpose,
            ulong targetStart,
            ulong targetEnd,
            out ulong index)
        {
            index = M01LivePagingIntegration.NoIndex;
            ulong purposeMask = 1UL << (byte)purpose;
            bool found = false;
            for (ulong candidate = 0;
                candidate < count;
                candidate++)
            {
                OS.Kernel.Boot.X64BootIdentityMappingRange range =
                    ranges[candidate];
                if (!range.IsInitialized
                    || (range.ContributingPurposeMask
                        & purposeMask) == 0
                    || !TryRaw(
                        range.PhysicalStart,
                        out ulong physicalStart)
                    || !TryRaw(
                        range.PhysicalEndExclusive,
                        out ulong physicalEnd)
                    || !TryRaw(
                        range.VirtualStart,
                        out ulong virtualStart)
                    || !TryRaw(
                        range.VirtualEndExclusive,
                        out ulong virtualEnd)
                    || physicalStart > targetStart
                    || physicalEnd < targetEnd
                    || virtualStart > targetStart
                    || virtualEnd < targetEnd)
                {
                    continue;
                }
                if (found)
                {
                    index = M01LivePagingIntegration.NoIndex;
                    return false;
                }
                found = true;
                index = candidate;
            }
            return found;
        }

        private static bool TryCreateBinding(
            M01OwnedCr3CompositeTransitionRangeRole role,
            M01OwnedCr3CompositeTransitionRangeProvenance
                provenance,
            ulong start,
            ulong endExclusive,
            ulong allocationIndex,
            ulong reservationIndex,
            ulong mappingIndex,
            out M01OwnedCr3CompositeTransitionRangeBinding
                binding)
        {
            binding = default;
            if (PhysicalAddress.TryCreateFromUefiBoundary(
                    start,
                    out PhysicalAddress physicalStart)
                    != AddressValueStatus.Success
                || PhysicalAddress.TryCreateFromUefiBoundary(
                    endExclusive,
                    out PhysicalAddress physicalEnd)
                    != AddressValueStatus.Success
                || VirtualAddress.TryCreateFromUefiBoundary(
                    start,
                    out VirtualAddress virtualStart)
                    != AddressValueStatus.Success
                || VirtualAddress.TryCreateFromUefiBoundary(
                    endExclusive,
                    out VirtualAddress virtualEnd)
                    != AddressValueStatus.Success)
            {
                return false;
            }

            binding =
                new M01OwnedCr3CompositeTransitionRangeBinding(
                    role,
                    provenance,
                    physicalStart,
                    physicalEnd,
                    virtualStart,
                    virtualEnd,
                    allocationIndex,
                    reservationIndex,
                    mappingIndex);
            return binding.Role == role
                && binding.Provenance == provenance;
        }

#if M02_SAFE_STUB_PROBE
        private static bool PrepareM02SafeStubPage(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiBootServices* bootServices,
            ulong mapFirmwareStatus,
            ulong codeAddress,
            bool codeAllocated,
            ulong probeAddress,
            bool probeAllocated,
            ulong tableAddress,
            ulong tablePageCount,
            bool tableAllocated)
        {
            if (!ValidateOriginalProtectionProbeBeforeM02(
                    (byte*)probeAddress))
            {
                return FailBeforeEntry(
                    console,
                    serial,
                    bootServices,
                    M01StageThreeAdapterStatus
                        .FinalReadbackRejected,
                    mapFirmwareStatus,
                    codeAddress,
                    codeAllocated,
                    probeAddress,
                    probeAllocated,
                    tableAddress,
                    tablePageCount,
                    tableAllocated);
            }

            WriteM02SuccessLine(console, serial, 0);
            fixed (OS.Kernel.Diagnostics
                .ExistingIn8Out8StubPageState*
                    m02PageState = &s_m02PageState)
            {
                s_m02MaterializationAudit =
                    M02SafeStubPageIntegration.Materialize(
                        (byte*)probeAddress,
                        probeAddress,
                        PageSize,
                        m02PageState,
                        out s_m02Materialization);
                if (!s_m02MaterializationAudit.Succeeded)
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .FinalReadbackRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }
                WriteM02SuccessLine(console, serial, 1);

                s_m02ReadbackAudit =
                    M02SafeStubPageIntegration.ValidateReadback(
                        (byte*)probeAddress,
                        probeAddress,
                        PageSize,
                        s_m02Materialization,
                        m02PageState,
                        out s_m02Readback);
                if (!s_m02ReadbackAudit.Succeeded)
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .FinalReadbackRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }
                WriteM02SuccessLine(console, serial, 2);

                s_m02FinalizationAudit =
                    M02SafeStubPageIntegration.AuditFinalization(
                        s_m02Readback,
                        probeAddress,
                        m02PageState,
                        out s_m02Finalization);
                if (!s_m02FinalizationAudit.Succeeded)
                {
                    return FailBeforeEntry(
                        console,
                        serial,
                        bootServices,
                        M01StageThreeAdapterStatus
                            .FinalReadbackRejected,
                        mapFirmwareStatus,
                        codeAddress,
                        codeAllocated,
                        probeAddress,
                        probeAllocated,
                        tableAddress,
                        tablePageCount,
                        tableAllocated);
                }
            }

            return true;
        }

        private static bool
            ValidateOriginalProtectionProbeBeforeM02(
                byte* probePage)
        {
            return ValidateProtectionProbeReadback(probePage);
        }
#endif

        private static bool ValidateProtectionProbeReadback(
            byte* probePage)
        {
            if (probePage == null)
            {
                return false;
            }

#if M02_SAFE_STUB_PROBE
            if (s_m02FinalizationAudit.Succeeded)
            {
                if (!s_m02MaterializationAudit.Succeeded
                    || !s_m02ReadbackAudit.Succeeded
                    || !s_m02Materialization.Succeeded
                    || !s_m02Readback.ReadBackVerified
                    || !s_m02Finalization.FinalizationAudited
                    || !M02SafeStubPageIntegration.ValidateExactPage(
                        probePage,
                        (ulong)probePage,
                        PageSize,
                        out ulong observedFnv1a64)
                    || observedFnv1a64
                        != M02SafeStubPageIntegration.ExpectedFnv1a64)
                {
                    return false;
                }

                return s_m02FinalizationAudit.Succeeded;
            }
#endif

            OS.Kernel.Memory.X64ProtectionProbeStubPlanResult
                readback =
                    OS.Kernel.Memory.X64ProtectionProbeStubPlan
                        .ValidateReadback(
                            probePage,
                            (ulong)probePage,
                            PageSize,
                            s_preparation.Evidence
                                .ProbeMaterialization);
            if (!readback.ReadBackVerified
                || !s_preparation.Evidence
                    .ProbeReadback.ReadBackVerified)
            {
                return false;
            }

            for (ulong index =
                    OS.Kernel.Memory
                        .X64ProtectionProbeStubPlan
                        .RequiredByteLength;
                index < PageSize;
                index++)
            {
                if (probePage[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MaterializeTransferThunk(
            BootMemoryRegionRecord stageOneCodeAllocation,
            out ulong entryAddress)
        {
            entryAddress = 0;
            if (stageOneCodeAllocation.UefiType
                    != EfiLoaderCode
                || stageOneCodeAllocation.PhysicalStart == 0
                || stageOneCodeAllocation.VirtualStart
                    != stageOneCodeAllocation.PhysicalStart
                || stageOneCodeAllocation.PageCount != 1
                || stageOneCodeAllocation.ByteLength
                    != PageSize
                || stageOneCodeAllocation.PhysicalEndExclusive
                    != stageOneCodeAllocation.PhysicalStart
                        + PageSize
                || (stageOneCodeAllocation.PhysicalStart
                    & (PageSize - 1)) != 0
                || TransferThunkEntryOffset
                    > PageSize - TransferThunkByteLength)
            {
                return false;
            }

            byte* destination =
                (byte*)stageOneCodeAllocation.VirtualStart
                    + TransferThunkEntryOffset;
            for (ulong index = 0;
                index < TransferThunkByteLength;
                index++)
            {
                if (destination[index] != 0xcc)
                {
                    return false;
                }
            }

            for (ulong index = 0;
                index < TransferThunkByteLength;
                index++)
            {
                destination[index] =
                    GetTransferThunkByte(index);
            }
            for (ulong index = 0;
                index < TransferThunkByteLength;
                index++)
            {
                if (destination[index]
                    != GetTransferThunkByte(index))
                {
                    return false;
                }
            }

            entryAddress =
                stageOneCodeAllocation.VirtualStart
                    + TransferThunkEntryOffset;
            return entryAddress
                == (ulong)destination;
        }

        private static bool ValidateTransferThunkReadback(
            BootMemoryRegionRecord stageOneCodeAllocation,
            ulong entryAddress)
        {
            if (entryAddress == 0
                || stageOneCodeAllocation.VirtualStart == 0
                || stageOneCodeAllocation.VirtualStart
                    > ~0UL - TransferThunkEntryOffset
                || entryAddress
                    != stageOneCodeAllocation.VirtualStart
                        + TransferThunkEntryOffset)
            {
                return false;
            }

            byte* source = (byte*)entryAddress;
            for (ulong index = 0;
                index < TransferThunkByteLength;
                index++)
            {
                if (source[index]
                    != GetTransferThunkByte(index))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MaterializePlannerCallThunk(
            BootMemoryRegionRecord stageOneCodeAllocation,
            out ulong entryAddress)
        {
            entryAddress = 0;
            if (stageOneCodeAllocation.UefiType
                    != EfiLoaderCode
                || stageOneCodeAllocation.PhysicalStart == 0
                || stageOneCodeAllocation.VirtualStart
                    != stageOneCodeAllocation.PhysicalStart
                || stageOneCodeAllocation.PageCount != 1
                || stageOneCodeAllocation.ByteLength
                    != PageSize
                || stageOneCodeAllocation.PhysicalEndExclusive
                    != stageOneCodeAllocation.PhysicalStart
                        + PageSize
                || (stageOneCodeAllocation.PhysicalStart
                    & (PageSize - 1)) != 0
                || PlannerCallThunkEntryOffset
                    > PageSize - PlannerCallThunkByteLength
                || PlannerCallThunkEntryOffset
                    < TransferThunkEntryOffset
                        + TransferThunkByteLength)
            {
                return false;
            }

            byte* destination =
                (byte*)stageOneCodeAllocation.VirtualStart
                    + PlannerCallThunkEntryOffset;
            for (ulong index = 0;
                index < PlannerCallThunkByteLength;
                index++)
            {
                if (destination[index] != 0xcc)
                {
                    return false;
                }
            }

            for (ulong index = 0;
                index < PlannerCallThunkByteLength;
                index++)
            {
                destination[index] =
                    GetPlannerCallThunkByte(index);
            }
            for (ulong index = 0;
                index < PlannerCallThunkByteLength;
                index++)
            {
                if (destination[index]
                    != GetPlannerCallThunkByte(index))
                {
                    return false;
                }
            }

            entryAddress =
                stageOneCodeAllocation.VirtualStart
                    + PlannerCallThunkEntryOffset;
            return entryAddress == (ulong)destination;
        }

        private static bool ValidatePlannerCallThunkReadback(
            BootMemoryRegionRecord stageOneCodeAllocation,
            ulong entryAddress)
        {
            if (entryAddress == 0
                || stageOneCodeAllocation.VirtualStart == 0
                || stageOneCodeAllocation.VirtualStart
                    > ~0UL - PlannerCallThunkEntryOffset
                || entryAddress
                    != stageOneCodeAllocation.VirtualStart
                        + PlannerCallThunkEntryOffset)
            {
                return false;
            }

            byte* source = (byte*)entryAddress;
            for (ulong index = 0;
                index < PlannerCallThunkByteLength;
                index++)
            {
                if (source[index]
                    != GetPlannerCallThunkByte(index))
                {
                    return false;
                }
            }

            return true;
        }

        private static byte GetPlannerCallThunkByte(ulong index)
        {
            return index switch
            {
                0 => 0xf3,
                1 => 0x0f,
                2 => 0x1e,
                3 => 0xfa,
                4 => 0x53,
                5 => 0x56,
                6 => 0x57,
                7 => 0x48,
                8 => 0x89,
                9 => 0xe3,
                10 => 0x48,
                11 => 0x89,
                12 => 0xd6,
                13 => 0x4c,
                14 => 0x89,
                15 => 0xc7,
                16 => 0x48,
                17 => 0x89,
                18 => 0xcc,
                19 => 0x48,
                20 => 0x83,
                21 => 0xec,
                22 => 0x20,
                23 => 0x48,
                24 => 0x89,
                25 => 0xf9,
                26 => 0xff,
                27 => 0xd6,
                28 => 0x48,
                29 => 0x83,
                30 => 0xc4,
                31 => 0x20,
                32 => 0x48,
                33 => 0x89,
                34 => 0xdc,
                35 => 0x5f,
                36 => 0x5e,
                37 => 0x5b,
                38 => 0xc3,
                _ => 0
            };
        }

        private static bool RunPreparationOnPlannerStack(
            ulong plannerCallEntry,
            BootMemoryRegionRecord stageOneCodeAllocation,
            M01LiveAllocatorStaticState* stageTwoState,
            OwnedBootStackSwitchPlanResult stackSwitchPlan,
            BootMemoryRegionRecord* allocationRecords,
            M01LivePagingAllocationEvidence allocationEvidence,
            M01LivePagingMapLedgerEvidence mapEvidence,
            OS.Kernel.Memory.BootReservedRange*
                reservationLedger,
            OS.Kernel.Boot.X64BootIdentityMappingRequirement*
                requirements,
            OS.Kernel.Boot.X64BootIdentityMappingRange* ranges,
            OS.Kernel.Memory.X64PageTableImagePage* pageImages,
            ulong* pageEntries,
            OS.Kernel.Memory.X64PageTableEncodingPreconditions
                preconditions,
            byte* codePage,
            byte* probePage,
            OS.Kernel.Memory.X64PagingModePreflightObservation*
                preflightObservation,
            M01LivePagingPreparationState* preparationState)
        {
            if (plannerCallEntry == 0
                || stageTwoState == null
                || allocationRecords == null
                || reservationLedger == null
                || requirements == null
                || ranges == null
                || pageImages == null
                || pageEntries == null
                || codePage == null
                || probePage == null
                || preflightObservation == null
                || preparationState == null)
            {
                return false;
            }

            fixed (M01StageThreePlannerStackBuffer*
                stackStorage = &s_plannerStack)
            fixed (M01StageThreePreparationCallContext*
                context = &s_preparationContext)
            {
                if ((ulong)sizeof(
                        M01StageThreePlannerStackBuffer)
                        != PlannerStackByteCount
                    || (((ulong)context) & 7) != 0)
                {
                    return false;
                }

                ulong stackBufferStart =
                    (ulong)&stackStorage->Values[0];
                if (stackBufferStart == 0
                    || stackBufferStart
                        > ~0UL - PlannerStackByteCount)
                {
                    return false;
                }

                ulong stackBufferEnd =
                    stackBufferStart + PlannerStackByteCount;
                if (stackBufferStart > ~0UL - 15
                    || stackBufferEnd < 16)
                {
                    return false;
                }

                ulong lowCanaryAddress =
                    (stackBufferStart + 15) & ~15UL;
                ulong stackTop =
                    (stackBufferEnd - 16) & ~15UL;
                ulong highCanaryAddress = stackTop + 8;
                if (lowCanaryAddress
                        > ~0UL - sizeof(ulong)
                    || highCanaryAddress
                        > stackBufferEnd - sizeof(ulong)
                    || stackTop <= lowCanaryAddress
                    || stackTop - lowCanaryAddress
                        < PlannerStackMinimumUsableBytes)
                {
                    return false;
                }

                ulong* lowCanary =
                    (ulong*)lowCanaryAddress;
                ulong* highCanary =
                    (ulong*)highCanaryAddress;
                *lowCanary = PlannerStackLowCanary;
                *highCanary = PlannerStackHighCanary;

                *context = default;
                context->Signature =
                    PreparationContextSignature;
                context->PlannerStackLowerBound =
                    lowCanaryAddress + sizeof(ulong);
                context->PlannerStackTop = stackTop;
                context->StageTwoState = stageTwoState;
                context->StackSwitchPlan = stackSwitchPlan;
                context->AllocationRecords = allocationRecords;
                context->AllocationEvidence = allocationEvidence;
                context->MapEvidence = mapEvidence;
                context->ReservationLedger =
                    reservationLedger;
                context->Requirements = requirements;
                context->Ranges = ranges;
                context->PageImages = pageImages;
                context->PageEntries = pageEntries;
                context->Preconditions = preconditions;
                context->CodePage = codePage;
                context->ProbePage = probePage;
                context->PreflightObservation =
                    preflightObservation;
                context->PreparationState =
                    preparationState;

                delegate* unmanaged<
                    M01StageThreePreparationCallContext*,
                    void> continuation =
                        &M01StageThreePreparationContinuation;
                ulong continuationAddress =
                    (ulong)(void*)continuation;
                if (continuationAddress == 0
                    || !ValidatePlannerCallThunkReadback(
                        stageOneCodeAllocation,
                        plannerCallEntry))
                {
                    *context = default;
                    return false;
                }

                s_preparation = default;
                delegate* unmanaged<
                    ulong,
                    ulong,
                    M01StageThreePreparationCallContext*,
                    void> invoke =
                        (delegate* unmanaged<
                            ulong,
                            ulong,
                            M01StageThreePreparationCallContext*,
                            void>)(void*)plannerCallEntry;
                invoke(
                    stackTop,
                    continuationAddress,
                    context);

                bool completed =
                    context->CompletionValue == 1;
                ulong observedStack =
                    context->ObservedStackAddress;
                bool stackWitnessInside =
                    observedStack
                        >= context->PlannerStackLowerBound
                    && observedStack
                        < context->PlannerStackTop;
                bool canariesIntact =
                    *lowCanary == PlannerStackLowCanary
                    && *highCanary == PlannerStackHighCanary;
                *context = default;
                return completed
                    && stackWitnessInside
                    && canariesIntact;
            }
        }

        [System.Runtime.InteropServices.UnmanagedCallers\u004fnly]
        private static void M01StageThreePreparationContinuation(
            M01StageThreePreparationCallContext* context)
        {
            ulong stackWitness = 0;
            ulong* stackWitnessPointer = &stackWitness;
            if (context == null
                || context->Signature
                    != PreparationContextSignature)
            {
                return;
            }

            context->ObservedStackAddress =
                (ulong)stackWitnessPointer;
            if (context->ObservedStackAddress
                    < context->PlannerStackLowerBound
                || context->ObservedStackAddress
                    >= context->PlannerStackTop)
            {
                return;
            }

            s_preparation =
                M01LivePagingIntegration.Prepare(
                    context->StageTwoState,
                    1,
                    1,
                    context->StackSwitchPlan,
                    context->AllocationRecords,
                    AllocationRecordCount,
                    AllocationRecordCapacity,
                    context->AllocationEvidence,
                    context->MapEvidence,
                    context->ReservationLedger,
                    FinalReservationCount,
                    FinalReservationCapacity,
                    context->Requirements,
                    RequirementCount,
                    RequirementCount,
                    context->Ranges,
                    0,
                    RangeCapacity,
                    context->PageImages,
                    0,
                    PageImageCapacity,
                    context->PageEntries,
                    0,
                    PageEntryCapacity,
                    context->Preconditions,
                    context->CodePage,
                    PageSize,
                    context->ProbePage,
                    PageSize,
                    context->PreflightObservation,
                    1,
                    1,
                    M01LivePagingByteMode.EmitAndVerify,
                    context->PreparationState,
                    1,
                    1);
            context->CompletionValue = 1;
        }

        private static byte GetTransferThunkByte(ulong index)
        {
            return index switch
            {
                0 => 0xf3,
                1 => 0x0f,
                2 => 0x1e,
                3 => 0xfa,
                4 => 0x53,
                5 => 0x48,
                6 => 0x83,
                7 => 0xec,
                8 => 0x40,
                9 => 0x48,
                10 => 0x89,
                11 => 0x4c,
                12 => 0x24,
                13 => 0x20,
                14 => 0x48,
                15 => 0x89,
                16 => 0x54,
                17 => 0x24,
                18 => 0x28,
                19 => 0x4c,
                20 => 0x89,
                21 => 0x44,
                22 => 0x24,
                23 => 0x30,
                24 => 0x4c,
                25 => 0x89,
                26 => 0x4c,
                27 => 0x24,
                28 => 0x38,
                29 => 0x31,
                30 => 0xc0,
                31 => 0x31,
                32 => 0xc9,
                33 => 0x0f,
                34 => 0xa2,
                35 => 0x48,
                36 => 0x8b,
                37 => 0x4c,
                38 => 0x24,
                39 => 0x20,
                40 => 0x48,
                41 => 0x8b,
                42 => 0x54,
                43 => 0x24,
                44 => 0x28,
                45 => 0x4c,
                46 => 0x8b,
                47 => 0x44,
                48 => 0x24,
                49 => 0x30,
                50 => 0x4c,
                51 => 0x8b,
                52 => 0x4c,
                53 => 0x24,
                54 => 0x38,
                55 => 0x41,
                56 => 0x51,
                57 => 0x9d,
                58 => 0x41,
                59 => 0xff,
                60 => 0xd0,
                61 => 0x48,
                62 => 0x8d,
                63 => 0x64,
                64 => 0x24,
                65 => 0x40,
                66 => 0x5b,
                67 => 0xc3,
                _ => 0
            };
        }

        private static bool ValidatePreTransferState(
            OS.Kernel.Memory.X64PagingModePreflightObservation*
                preflight,
            M01OwnedCr3CompositeCallGateObservation*
                observation,
            M01OwnedCr3CompositeTransitionRuntimeState*
                runtimeState,
            M01OwnedCr3CompositeCallGateResult gateReadback,
            ulong transferEntry)
        {
            if (preflight == null
                || observation == null
                || runtimeState == null
                || transferEntry == 0
                || !preflight->IsInitialized
                || !s_preparation.Succeeded
                || !s_preparation.State.IsPrepared
                || !s_compositePreparation.IsStatePrepared
                || !gateReadback.IsReadbackVerified
                || !observation->IsArmed
                || !runtimeState->IsInitialized
                || runtimeState->ExpectedSelfAddress
                    != (ulong)runtimeState
                || runtimeState->ProhibitedFirmwareCallbackCount
                    != 0
                || preflight->RawCr3
                    != s_preparation.State.RawFirmwareCr3
                || preflight->RawCr4
                    != s_preparation.State.RawCr4
                || preflight->Rflags
                    != s_preparation.State.OriginalRflags
                || observation->ExpectedOriginalRflags
                    != s_preparation.State.OriginalRflags
                || observation->ExpectedFirmwareCr3Raw
                    != s_preparation.State.RawFirmwareCr3
                || observation->ExpectedFirmwareCr4Raw
                    != s_preparation.State.RawCr4
                || observation->ExpectedGateEntryAddress
                    != gateReadback.GateEntryAddress
                || observation->ExpectedReturnSiteAddress
                    != gateReadback.ReturnSiteAddress
                || gateReadback.Observation
                        .ExpectedSelfAddress
                    != (ulong)observation
                || (s_preparation.State.OriginalRflags
                    & M01OwnedCr3CompositeTransition
                        .RflagsTrapFlagBit) != 0
                || (s_preparation.State.RawCr4
                    & (OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .Cr4La57Bit
                        | OS.Kernel.Memory
                            .X64PagingModePreflightStubPlan
                            .Cr4PcideBit
                        | M01OwnedCr3CompositeTransition
                            .Cr4ControlFlowEnforcementBit)) != 0)
            {
                return false;
            }

            ulong canaryAddress = runtimeState->CanaryAddress;
            return canaryAddress != 0
                && *(ulong*)canaryAddress == TransitionCanary;
        }

        private static bool RejectInstructionPublicationBeforeEntry(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiBootServices* bootServices,
            ulong firmwareStatus,
            ulong codeAddress,
            bool codeAllocated,
            ulong probeAddress,
            bool probeAllocated,
            ulong tableAddress,
            ulong tablePageCount,
            bool tableAllocated)
        {
            EfiEntry.WriteM01InstructionPublicationCookieFailure(
                console,
                serial);
            return FailBeforeEntry(
                console,
                serial,
                bootServices,
                M01StageThreeAdapterStatus
                    .InstructionPublicationRejected,
                firmwareStatus,
                codeAddress,
                codeAllocated,
                probeAddress,
                probeAllocated,
                tableAddress,
                tablePageCount,
                tableAllocated);
        }

        private static bool FailBeforeEntry(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiBootServices* bootServices,
            M01StageThreeAdapterStatus adapterStatus,
            ulong firmwareStatus,
            ulong codeAddress,
            bool codeAllocated,
            ulong probeAddress,
            bool probeAllocated,
            ulong tableAddress,
            ulong tablePageCount,
            bool tableAllocated)
        {
            ulong tableCleanupStatus = NotAttemptedStatus;
            ulong probeCleanupStatus = NotAttemptedStatus;
            ulong codeCleanupStatus = NotAttemptedStatus;

            if (bootServices != null
                && bootServices->FreePages != null)
            {
                if (tableAllocated)
                {
                    tableCleanupStatus =
                        bootServices->FreePages(
                            tableAddress,
                            tablePageCount);
                }
                if (probeAllocated)
                {
                    probeCleanupStatus =
                        bootServices->FreePages(
                            probeAddress,
                            ProbePageCount);
                }
                if (codeAllocated)
                {
                    codeCleanupStatus =
                        bootServices->FreePages(
                            codeAddress,
                            CodePageCount);
                }
            }

            s_runState = 3;
            WriteFailure(
                console,
                serial,
                adapterStatus,
                firmwareStatus,
                tableCleanupStatus,
                probeCleanupStatus,
                codeCleanupStatus);
            return false;
        }

        private static bool FailAfterEntry(
            M01StageThreeAdapterStatus adapterStatus,
            ulong detail)
        {
            _ = adapterStatus;
            _ = detail;
            s_runState = 3;
            while (true)
            {
            }
        }

        private static void WriteSuccessLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiBootServices* bootServices,
            int lineIndex)
        {
#if M02_SAFE_STUB_PROBE
            if (lineIndex == 9)
            {
                ulong probeAddress =
                    s_m02FinalizationAudit.ModeledPageAddress;
                ulong runtimeStateAddress =
                    s_gateAudit.PreparedState.ExpectedSelfAddress;
                fixed (M01OwnedCr3CompositeCallGateResult*
                    gateAudit = &s_gateAudit)
                {
                    s_m02ExecutionAudit =
                        M02SafeStubPageIntegration.AuditExecution(
                            (byte*)probeAddress,
                            probeAddress,
                            PageSize,
                            s_m02Finalization,
                            (M01OwnedCr3CompositeTransitionRuntimeState*)
                                (void*)runtimeStateAddress,
                            gateAudit);
                }

                if (!s_m02ExecutionAudit.Succeeded)
                {
                    _ = FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        (ulong)s_m02ExecutionAudit.Status);
                    return;
                }

#if M02_CHECKED_PORT_IO_BINDING_PROBE
                s_m02CheckedPortIoBinding =
                    M02CheckedPortIoFunctionPointerIntegration.Bind(
                        s_m02Finalization.ModeledPageAddress,
                        s_m02Finalization.PageByteCount,
                        s_m02Finalization.Out8Offset,
                        s_m02Finalization.In8Offset,
                        s_m02Finalization.FinalizationAudited,
                        s_m02ExecutionAudit.Succeeded,
                        s_m02ExecutionAudit.PortIoExecuted,
                        s_m02ExecutionAudit.PrivilegedStubTargeted);
                if (!s_m02CheckedPortIoBinding.Succeeded)
                {
                    WriteM02CheckedPortIoBindingFailureLine(
                        console,
                        serial);
                    _ = FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        (ulong)s_m02CheckedPortIoBinding.Status);
                    return;
                }

#endif
            }

            if (lineIndex == 18)
            {
#if M02_COM1_OWNED_PORT_IO_PROBE
                M02Com1OwnedPortIoBackendIntegrationResult
                    com1Configuration =
                        M02Com1OwnedPortIoBackendIntegration.Configure(
                            s_m02CheckedPortIoBinding);
                if (!com1Configuration.Succeeded)
                {
                    WriteM02Com1OwnedPortIoFailureLine(
                        console,
                        serial);
                    _ = FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        (ulong)com1Configuration.Status);
                    return;
                }
#endif
                WriteM02SuccessLine(console, serial, 3);
                WriteM02SuccessLine(console, serial, 4);
                WriteM02SuccessLine(console, serial, 5);
#if M02_CHECKED_PORT_IO_BINDING_PROBE
                WriteM02CheckedPortIoBindingSuccessLine(
                    console,
                    serial);
#if M02_COM1_OWNED_PORT_IO_PROBE
                WriteM02Com1OwnedPortIoSuccessLine(
                    console,
                    serial);
#if M02_OWNED_COM1_READY_PROBE
                OS.Kernel.Diagnostics.Com1OwnedPortIoBackend
                    ownedCom1Backend =
                        new OS.Kernel.Diagnostics.Com1OwnedPortIoBackend(
                            s_m02CheckedPortIoBinding.Binding);
                OS.Kernel.Diagnostics.OwnedCom1ReadyWriterResult
                    ownedCom1Ready =
                        OS.Kernel.Diagnostics
                            .OwnedCom1ReadyExclusivelyOwnedWriter
                                .Emit(ownedCom1Backend);
                if (!ownedCom1Ready.Succeeded ||
                    ownedCom1Ready.Configuration.WritesCompleted != 7 ||
                    ownedCom1Ready.MarkerBytesWritten != 16 ||
                    ownedCom1Ready.LineEndingBytesWritten != 2 ||
                    ownedCom1Ready.LineStatusPolls != 18 ||
                    !ownedCom1Ready.OwnedBackendUsed ||
                    ownedCom1Ready.FirmwareFallbackUsed)
                {
                    WriteM02OwnedCom1ReadyFailureLine(
                        console,
                        serial);
                    _ = FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        (ulong)ownedCom1Ready.Status);
                    return;
                }

                WriteM02OwnedCom1ReadySuccessLine(
                    console,
                    serial);
#if M02_PANIC_SAFE_OWNED_COM1_PROBE
                OS.Kernel.Diagnostics.PanicSafeOwnedCom1ReportResult
                    panicSafeOwnedCom1 =
                        OS.Kernel.Diagnostics
                            .PanicSafeFormattingSerialReportingOwnedCom1
                            .EmitReadinessMarker(ownedCom1Backend);
                if (!panicSafeOwnedCom1.Succeeded ||
                    !panicSafeOwnedCom1.Format.Succeeded ||
                    panicSafeOwnedCom1.Format.RequiredBytes !=
                        OS.Kernel.Diagnostics
                            .PanicSafeFormattingSerialReportingOwnedCom1
                            .ReadinessSerialByteCount ||
                    panicSafeOwnedCom1.BytesFormatted !=
                        OS.Kernel.Diagnostics
                            .PanicSafeFormattingSerialReportingOwnedCom1
                            .ReadinessSerialByteCount ||
                    panicSafeOwnedCom1.BytesWritten !=
                        OS.Kernel.Diagnostics
                            .PanicSafeFormattingSerialReportingOwnedCom1
                            .ReadinessSerialByteCount ||
                    panicSafeOwnedCom1.LineStatusPolls !=
                        OS.Kernel.Diagnostics
                            .PanicSafeFormattingSerialReportingOwnedCom1
                            .ReadinessSerialByteCount ||
                    panicSafeOwnedCom1.Configuration.WritesCompleted !=
                        OS.Kernel.Diagnostics.Com1OwnedPortIoBackend
                            .RequiredConfigurationWriteCount ||
                    panicSafeOwnedCom1.FailedByteIndex != -1 ||
                    !panicSafeOwnedCom1.OwnedBackendUsed ||
                    panicSafeOwnedCom1.FirmwareFallbackUsed ||
                    !panicSafeOwnedCom1.LastOperation.Succeeded ||
                    panicSafeOwnedCom1.LastOperation.OperationKind !=
                        OS.Kernel.Diagnostics.PortIoOperationKind.Out8 ||
                    panicSafeOwnedCom1.LastOperation.Port !=
                        OS.Kernel.Diagnostics.Com1OwnedPortIoBackend
                            .DataRegisterPort ||
                    panicSafeOwnedCom1.LastOperation.Value !=
                        OS.Kernel.Diagnostics
                            .PanicSafeFormattingSerialReportingOwnedCom1
                            .LineFeed)
                {
                    WriteM02PanicSafeOwnedCom1FailureLine(
                        console,
                        serial);
                    _ = FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        (ulong)panicSafeOwnedCom1.Status);
                    return;
                }

                WriteM02PanicSafeOwnedCom1EvidenceLine(
                    console,
                    serial);
#if M02_OWNED_X64_GDT_ENCODING_PROBE
                const int m02OwnedTssByteCount = 104;
                M02OwnedX64GdtEncodingStackWorkspace
                    m02OwnedWorkspace = default;
                byte* m02OwnedTss =
                    m02OwnedWorkspace.TssBytes;
                for (int tssOffset = 0;
                    tssOffset < m02OwnedTssByteCount;
                    tssOffset++)
                {
                    m02OwnedTss[tssOffset] = 0;
                }

                byte* m02OwnedGdt =
                    m02OwnedWorkspace.GdtBytes;
                OS.Kernel.Diagnostics.OwnedX64GdtEncodingResult
                    m02OwnedGdtEncoding =
                        OS.Kernel.Diagnostics
                            .OwnedX64GdtContainingNullN64BitCode
                            .Encode(
                                m02OwnedGdt,
                                OS.Kernel.Diagnostics
                                    .OwnedX64GdtContainingNullN64BitCode
                                    .RequiredByteCount,
                                (ulong)m02OwnedTss,
                                OS.Kernel.Diagnostics
                                    .OwnedX64GdtContainingNullN64BitCode
                                    .MinimumTssLimit);
                bool m02OwnedTssRemainsZero = true;
                for (int tssOffset = 0;
                    tssOffset < m02OwnedTssByteCount;
                    tssOffset++)
                {
                    if (m02OwnedTss[tssOffset] != 0)
                    {
                        m02OwnedTssRemainsZero = false;
                        break;
                    }
                }

                if (!m02OwnedGdtEncoding.IsInitialized ||
                    !m02OwnedGdtEncoding.Succeeded ||
                    m02OwnedGdtEncoding.Status !=
                        OS.Kernel.Diagnostics
                            .OwnedX64GdtEncodingStatus.Success ||
                    m02OwnedGdtEncoding.BytesWritten != 40 ||
                    m02OwnedGdtEncoding.RequiredBytes != 40 ||
                    m02OwnedGdtEncoding.TableLimit != 39 ||
                    !m02OwnedGdtEncoding.DestinationMutated ||
                    !m02OwnedGdtEncoding.Verification.IsInitialized ||
                    !m02OwnedGdtEncoding.Verification.Succeeded ||
                    m02OwnedGdtEncoding.Verification.Status !=
                        OS.Kernel.Diagnostics
                            .OwnedX64GdtVerificationStatus.Success ||
                    m02OwnedGdtEncoding.Verification.BytesCompared != 40 ||
                    m02OwnedGdtEncoding.Verification.FirstMismatchOffset != -1 ||
                    m02OwnedGdtEncoding.Fingerprint !=
                        m02OwnedGdtEncoding.Verification.ObservedFingerprint ||
                    m02OwnedGdtEncoding.Verification.ObservedFingerprint !=
                        m02OwnedGdtEncoding.Verification.ExpectedFingerprint ||
                    !m02OwnedTssRemainsZero ||
                    OS.Kernel.Diagnostics
                        .OwnedX64GdtContainingNullN64BitCode
                        .RequiredByteCount != 40 ||
                    OS.Kernel.Diagnostics
                        .OwnedX64GdtContainingNullN64BitCode
                        .TableLimit != 39 ||
                    OS.Kernel.Diagnostics
                        .OwnedX64GdtContainingNullN64BitCode
                        .NullSelector != 0 ||
                    OS.Kernel.Diagnostics
                        .OwnedX64GdtContainingNullN64BitCode
                        .KernelCodeSelector != 8 ||
                    OS.Kernel.Diagnostics
                        .OwnedX64GdtContainingNullN64BitCode
                        .KernelDataSelector != 16 ||
                    OS.Kernel.Diagnostics
                        .OwnedX64GdtContainingNullN64BitCode
                        .TssSelector != 24)
                {
                    WriteM02OwnedX64GdtEncodingFailureLine(
                        console,
                        serial);
                    _ = FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        (ulong)m02OwnedGdtEncoding.Status);
                    return;
                }

                WriteM02OwnedX64GdtEncodingSuccessLine(
                    console,
                    serial);
#if M02_GUARDED_IST_STACK_ALLOCATION_PROBE
                const ulong m02GuardedPageCount = 6;
                const ulong m02GuardedPageSize = 4096;
                const ulong m02GuardedArenaSize =
                    m02GuardedPageCount * m02GuardedPageSize;
                const ulong m02GuardedOwnerToken =
                    0x004D30322D303038UL;
                ulong m02GuardedArenaAddress = 0;

                if (bootServices == null ||
                    bootServices->AllocatePages == null ||
                    bootServices->FreePages == null)
                {
                    WriteM02GuardedIstStackAllocationFailureLine(
                        console,
                        serial);
                    _ = FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        1);
                    return;
                }

                ulong m02GuardedFirmwareStatus =
                    bootServices->AllocatePages(
                        AllocateAnyPages,
                        EfiLoaderData,
                        m02GuardedPageCount,
                        &m02GuardedArenaAddress);
                if (m02GuardedFirmwareStatus != EfiSuccess ||
                    m02GuardedArenaAddress == 0 ||
                    (m02GuardedArenaAddress &
                        (m02GuardedPageSize - 1)) != 0 ||
                    !OS.Kernel.Diagnostics
                        .GuardedISTStacksDoubleFaultPageFaultNMI
                        .IsCanonicalX64Address(
                            m02GuardedArenaAddress) ||
                    m02GuardedArenaAddress >
                        ulong.MaxValue - m02GuardedArenaSize ||
                    !OS.Kernel.Diagnostics
                        .GuardedISTStacksDoubleFaultPageFaultNMI
                        .IsCanonicalX64Address(
                            m02GuardedArenaAddress +
                                m02GuardedArenaSize - 1))
                {
                    if (m02GuardedFirmwareStatus == EfiSuccess &&
                        m02GuardedArenaAddress != 0)
                    {
                        _ = bootServices->FreePages(
                            m02GuardedArenaAddress,
                            m02GuardedPageCount);
                    }

                    WriteM02GuardedIstStackAllocationFailureLine(
                        console,
                        serial);
                    _ = FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        m02GuardedFirmwareStatus == EfiSuccess
                            ? 2UL
                            : m02GuardedFirmwareStatus);
                    return;
                }

                OS.Kernel.Diagnostics.GuardedIstStackArenaRequest
                    m02GuardedRequest =
                        new OS.Kernel.Diagnostics
                            .GuardedIstStackArenaRequest(
                                m02GuardedArenaAddress,
                                m02GuardedArenaSize,
                                m02GuardedPageSize,
                                m02GuardedPageSize,
                                m02GuardedOwnerToken,
                                stackWritable: true,
                                stackExecutable: false);
                OS.Kernel.Diagnostics
                    .GuardedIstStackArenaInitializationResult
                        m02GuardedInitialization =
                            OS.Kernel.Diagnostics
                                .GuardedISTStacksDoubleFaultPageFaultNMI
                                .Initialize(
                                    m02GuardedRequest,
                                    out OS.Kernel.Diagnostics
                                        .GuardedIstStackAllocatorState
                                            m02GuardedState);
                OS.Kernel.Diagnostics.GuardedIstStackAllocationResult
                    m02GuardedAllocation =
                        OS.Kernel.Diagnostics
                            .GuardedISTStacksDoubleFaultPageFaultNMI
                            .Allocate(ref m02GuardedState);

                OS.Kernel.Diagnostics.GuardedIstStackDescriptor
                    m02GuardedDoubleFault =
                        m02GuardedAllocation.DoubleFault;
                OS.Kernel.Diagnostics.GuardedIstStackDescriptor
                    m02GuardedPageFault =
                        m02GuardedAllocation.PageFault;
                OS.Kernel.Diagnostics.GuardedIstStackDescriptor
                    m02GuardedNmi =
                        m02GuardedAllocation.NonMaskableInterrupt;
                ulong m02GuardedDoubleFaultGuard =
                    m02GuardedArenaAddress;
                ulong m02GuardedDoubleFaultStack =
                    m02GuardedArenaAddress + m02GuardedPageSize;
                ulong m02GuardedPageFaultGuard =
                    m02GuardedArenaAddress +
                        (2 * m02GuardedPageSize);
                ulong m02GuardedPageFaultStack =
                    m02GuardedArenaAddress +
                        (3 * m02GuardedPageSize);
                ulong m02GuardedNmiGuard =
                    m02GuardedArenaAddress +
                        (4 * m02GuardedPageSize);
                ulong m02GuardedNmiStack =
                    m02GuardedArenaAddress +
                        (5 * m02GuardedPageSize);

                bool m02GuardedLayoutValid =
                    m02GuardedInitialization.IsInitialized &&
                    m02GuardedInitialization.Succeeded &&
                    m02GuardedInitialization.Status ==
                        OS.Kernel.Diagnostics
                            .GuardedIstStackArenaStatus.Success &&
                    m02GuardedInitialization.RequiredBytes ==
                        m02GuardedArenaSize &&
                    m02GuardedInitialization.AvailableBytes ==
                        m02GuardedArenaSize &&
                    m02GuardedState.IsInitialized &&
                    m02GuardedState.HasActiveLease &&
                    m02GuardedState.ArenaBaseAddress ==
                        m02GuardedArenaAddress &&
                    m02GuardedState.ArenaSizeBytes ==
                        m02GuardedArenaSize &&
                    m02GuardedState.RequiredBytes ==
                        m02GuardedArenaSize &&
                    m02GuardedState.OwnerToken ==
                        m02GuardedOwnerToken &&
                    m02GuardedAllocation.IsInitialized &&
                    m02GuardedAllocation.Succeeded &&
                    m02GuardedAllocation.Status ==
                        OS.Kernel.Diagnostics
                            .GuardedIstStackAllocationStatus.Success &&
                    m02GuardedAllocation.RequiredBytes ==
                        m02GuardedArenaSize &&
                    m02GuardedAllocation.AvailableBytes ==
                        m02GuardedArenaSize &&
                    m02GuardedAllocation.StateMutated &&
                    m02GuardedAllocation.Lease.IsUsable &&
                    m02GuardedAllocation.Lease.OwnerToken ==
                        m02GuardedOwnerToken &&
                    m02GuardedAllocation.Lease.Generation ==
                        m02GuardedState.ActiveGeneration &&
                    m02GuardedAllocation.Lease.ArenaBaseAddress ==
                        m02GuardedArenaAddress &&
                    m02GuardedAllocation.Lease.RequiredBytes ==
                        m02GuardedArenaSize &&
                    m02GuardedDoubleFault.IsUsable &&
                    m02GuardedDoubleFault.Kind ==
                        OS.Kernel.Diagnostics
                            .GuardedIstStackKind.DoubleFault &&
                    m02GuardedDoubleFault.IstIndex == 1 &&
                    m02GuardedDoubleFault.GuardBaseAddress ==
                        m02GuardedDoubleFaultGuard &&
                    m02GuardedDoubleFault.GuardSizeBytes ==
                        m02GuardedPageSize &&
                    m02GuardedDoubleFault.StackBaseAddress ==
                        m02GuardedDoubleFaultStack &&
                    m02GuardedDoubleFault.StackSizeBytes ==
                        m02GuardedPageSize &&
                    m02GuardedDoubleFault.StackTopAddress ==
                        m02GuardedDoubleFaultStack +
                            m02GuardedPageSize &&
                    !m02GuardedDoubleFault.GuardPagePresent &&
                    m02GuardedDoubleFault.StackWritable &&
                    !m02GuardedDoubleFault.StackExecutable &&
                    m02GuardedPageFault.IsUsable &&
                    m02GuardedPageFault.Kind ==
                        OS.Kernel.Diagnostics
                            .GuardedIstStackKind.PageFault &&
                    m02GuardedPageFault.IstIndex == 2 &&
                    m02GuardedPageFault.GuardBaseAddress ==
                        m02GuardedPageFaultGuard &&
                    m02GuardedPageFault.GuardSizeBytes ==
                        m02GuardedPageSize &&
                    m02GuardedPageFault.StackBaseAddress ==
                        m02GuardedPageFaultStack &&
                    m02GuardedPageFault.StackSizeBytes ==
                        m02GuardedPageSize &&
                    m02GuardedPageFault.StackTopAddress ==
                        m02GuardedPageFaultStack +
                            m02GuardedPageSize &&
                    !m02GuardedPageFault.GuardPagePresent &&
                    m02GuardedPageFault.StackWritable &&
                    !m02GuardedPageFault.StackExecutable &&
                    m02GuardedNmi.IsUsable &&
                    m02GuardedNmi.Kind ==
                        OS.Kernel.Diagnostics
                            .GuardedIstStackKind.NonMaskableInterrupt &&
                    m02GuardedNmi.IstIndex == 3 &&
                    m02GuardedNmi.GuardBaseAddress ==
                        m02GuardedNmiGuard &&
                    m02GuardedNmi.GuardSizeBytes ==
                        m02GuardedPageSize &&
                    m02GuardedNmi.StackBaseAddress ==
                        m02GuardedNmiStack &&
                    m02GuardedNmi.StackSizeBytes ==
                        m02GuardedPageSize &&
                    m02GuardedNmi.StackTopAddress ==
                        m02GuardedNmiStack + m02GuardedPageSize &&
                    !m02GuardedNmi.GuardPagePresent &&
                    m02GuardedNmi.StackWritable &&
                    !m02GuardedNmi.StackExecutable;

                bool m02GuardedStackReadBack = false;
                if (m02GuardedLayoutValid)
                {
                    byte* m02GuardedDoubleFaultBytes =
                        (byte*)m02GuardedDoubleFault.StackBaseAddress;
                    byte* m02GuardedPageFaultBytes =
                        (byte*)m02GuardedPageFault.StackBaseAddress;
                    byte* m02GuardedNmiBytes =
                        (byte*)m02GuardedNmi.StackBaseAddress;
                    m02GuardedDoubleFaultBytes[0] = 0xD1;
                    m02GuardedDoubleFaultBytes[m02GuardedPageSize - 1] =
                        0xD2;
                    m02GuardedPageFaultBytes[0] = 0xA1;
                    m02GuardedPageFaultBytes[m02GuardedPageSize - 1] =
                        0xA2;
                    m02GuardedNmiBytes[0] = 0xB1;
                    m02GuardedNmiBytes[m02GuardedPageSize - 1] =
                        0xB2;
                    m02GuardedStackReadBack =
                        m02GuardedDoubleFaultBytes[0] == 0xD1 &&
                        m02GuardedDoubleFaultBytes[
                            m02GuardedPageSize - 1] == 0xD2 &&
                        m02GuardedPageFaultBytes[0] == 0xA1 &&
                        m02GuardedPageFaultBytes[
                            m02GuardedPageSize - 1] == 0xA2 &&
                        m02GuardedNmiBytes[0] == 0xB1 &&
                        m02GuardedNmiBytes[
                            m02GuardedPageSize - 1] == 0xB2;
                }

                OS.Kernel.Diagnostics.GuardedIstStackReleaseResult
                    m02GuardedRelease = default;
                if (m02GuardedAllocation.Succeeded &&
                    m02GuardedAllocation.Lease.IsUsable)
                {
                    m02GuardedRelease =
                        OS.Kernel.Diagnostics
                            .GuardedISTStacksDoubleFaultPageFaultNMI
                            .Release(
                                ref m02GuardedState,
                                m02GuardedAllocation.Lease);
                }

                ulong m02GuardedFreeStatus =
                    bootServices->FreePages(
                        m02GuardedArenaAddress,
                        m02GuardedPageCount);
                if (!m02GuardedLayoutValid ||
                    !m02GuardedStackReadBack ||
                    !m02GuardedRelease.IsInitialized ||
                    !m02GuardedRelease.Succeeded ||
                    m02GuardedRelease.Status !=
                        OS.Kernel.Diagnostics
                            .GuardedIstStackReleaseStatus.Success ||
                    m02GuardedRelease.ReleasedGeneration !=
                        m02GuardedAllocation.Lease.Generation ||
                    !m02GuardedRelease.StateMutated ||
                    m02GuardedState.HasActiveLease ||
                    m02GuardedFreeStatus != EfiSuccess)
                {
                    WriteM02GuardedIstStackAllocationFailureLine(
                        console,
                        serial);
                    _ = FailAfterEntry(
                        M01StageThreeAdapterStatus
                            .PostReturnAuditRejected,
                        m02GuardedFreeStatus != EfiSuccess
                            ? m02GuardedFreeStatus
                            : 3UL);
                    return;
                }

                WriteM02GuardedIstStackAllocationSuccessLine(
                    console,
                    serial);
#endif
#endif
#endif
#endif
#endif
#endif
            }
#endif

            int length =
                BootSequencePlan
                    .GetM01StageThreeSuccessLineLength(
                        lineIndex);
            for (int offset = 0;
                offset < length;
                offset++)
            {
                EfiEntry.Write(
                    console,
                    serial,
                    BootSequencePlan
                        .GetM01StageThreeSuccessLineByte(
                            lineIndex,
                            offset));
            }
            EfiEntry.WriteCrLf(console, serial);
        }

#if M02_SAFE_STUB_PROBE
        private static void WriteM02SuccessLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            int lineIndex)
        {
            switch (lineIndex)
            {
                case 0:
                    WriteAscii(
                        console,
                        serial,
                        (byte)'T',
                        (byte)'E',
                        (byte)'S',
                        (byte)'T',
                        (byte)'_',
                        (byte)'S',
                        (byte)'T',
                        (byte)'A');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'R',
                        (byte)'T',
                        (byte)' ',
                        (byte)'m',
                        (byte)'0',
                        (byte)'2',
                        (byte)'.',
                        (byte)'0');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'1',
                        (byte)'7',
                        0,
                        0,
                        0,
                        0,
                        0,
                        0);
                    break;
                case 1:
                    WriteAscii(
                        console,
                        serial,
                        (byte)'M',
                        (byte)'0',
                        (byte)'2',
                        (byte)'_',
                        (byte)'S',
                        (byte)'T',
                        (byte)'U',
                        (byte)'B');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'_',
                        (byte)'P',
                        (byte)'A',
                        (byte)'G',
                        (byte)'E',
                        (byte)'_',
                        (byte)'M',
                        (byte)'A');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'T',
                        (byte)'E',
                        (byte)'R',
                        (byte)'I',
                        (byte)'A',
                        (byte)'L',
                        (byte)'I',
                        (byte)'Z');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'E',
                        (byte)'D',
                        (byte)' ',
                        (byte)'p',
                        (byte)'a',
                        (byte)'g',
                        (byte)'e',
                        (byte)'B');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'y',
                        (byte)'t',
                        (byte)'e',
                        (byte)'s',
                        (byte)'=',
                        (byte)'4',
                        (byte)'0',
                        (byte)'9');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'6',
                        (byte)' ',
                        (byte)'s',
                        (byte)'a',
                        (byte)'f',
                        (byte)'e',
                        (byte)'O',
                        (byte)'f');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'f',
                        (byte)'s',
                        (byte)'e',
                        (byte)'t',
                        (byte)'=',
                        (byte)'0',
                        (byte)' ',
                        (byte)'o');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'u',
                        (byte)'t',
                        (byte)'8',
                        (byte)'O',
                        (byte)'f',
                        (byte)'f',
                        (byte)'s',
                        (byte)'e');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'t',
                        (byte)'=',
                        (byte)'1',
                        (byte)'6',
                        (byte)' ',
                        (byte)'i',
                        (byte)'n',
                        (byte)'8');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'O',
                        (byte)'f',
                        (byte)'f',
                        (byte)'s',
                        (byte)'e',
                        (byte)'t',
                        (byte)'=',
                        (byte)'3');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'2',
                        (byte)' ',
                        (byte)'f',
                        (byte)'n',
                        (byte)'v',
                        (byte)'1',
                        (byte)'a',
                        (byte)'6');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'4',
                        (byte)'=',
                        (byte)'9',
                        (byte)'8',
                        (byte)'2',
                        (byte)'E',
                        (byte)'5',
                        (byte)'B');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'7',
                        (byte)'E',
                        (byte)'3',
                        (byte)'E',
                        (byte)'E',
                        (byte)'5',
                        (byte)'8',
                        (byte)'E');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'5',
                        (byte)'6',
                        0,
                        0,
                        0,
                        0,
                        0,
                        0);
                    break;
                case 2:
                    WriteAscii(
                        console,
                        serial,
                        (byte)'M',
                        (byte)'0',
                        (byte)'2',
                        (byte)'_',
                        (byte)'S',
                        (byte)'T',
                        (byte)'U',
                        (byte)'B');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'_',
                        (byte)'P',
                        (byte)'A',
                        (byte)'G',
                        (byte)'E',
                        (byte)'_',
                        (byte)'R',
                        (byte)'E');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'A',
                        (byte)'D',
                        (byte)'B',
                        (byte)'A',
                        (byte)'C',
                        (byte)'K',
                        (byte)'_',
                        (byte)'V');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'E',
                        (byte)'R',
                        (byte)'I',
                        (byte)'F',
                        (byte)'I',
                        (byte)'E',
                        (byte)'D',
                        (byte)' ');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'f',
                        (byte)'n',
                        (byte)'v',
                        (byte)'1',
                        (byte)'a',
                        (byte)'6',
                        (byte)'4',
                        (byte)'=');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'9',
                        (byte)'8',
                        (byte)'2',
                        (byte)'E',
                        (byte)'5',
                        (byte)'B',
                        (byte)'7',
                        (byte)'E');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'3',
                        (byte)'E',
                        (byte)'E',
                        (byte)'5',
                        (byte)'8',
                        (byte)'E',
                        (byte)'5',
                        (byte)'6');
                    break;
                case 3:
                    WriteAscii(
                        console,
                        serial,
                        (byte)'M',
                        (byte)'0',
                        (byte)'2',
                        (byte)'_',
                        (byte)'S',
                        (byte)'A',
                        (byte)'F',
                        (byte)'E');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'_',
                        (byte)'P',
                        (byte)'R',
                        (byte)'O',
                        (byte)'B',
                        (byte)'E',
                        (byte)'_',
                        (byte)'E');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'X',
                        (byte)'E',
                        (byte)'C',
                        (byte)'U',
                        (byte)'T',
                        (byte)'E',
                        (byte)'D',
                        (byte)' ');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'o',
                        (byte)'f',
                        (byte)'f',
                        (byte)'s',
                        (byte)'e',
                        (byte)'t',
                        (byte)'=',
                        (byte)'0');
                    WriteAscii(
                        console,
                        serial,
                        (byte)' ',
                        (byte)'r',
                        (byte)'e',
                        (byte)'t',
                        (byte)'u',
                        (byte)'r',
                        (byte)'n',
                        (byte)'=');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'0',
                        (byte)'x',
                        (byte)'4',
                        (byte)'F',
                        (byte)'5',
                        (byte)'3',
                        (byte)'4',
                        (byte)'F');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'4',
                        (byte)'B',
                        0,
                        0,
                        0,
                        0,
                        0,
                        0);
                    break;
                case 4:
                    WriteAscii(
                        console,
                        serial,
                        (byte)'C',
                        (byte)'L',
                        (byte)'R',
                        (byte)'O',
                        (byte)'S',
                        (byte)'_',
                        (byte)'M',
                        (byte)'0');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'2',
                        (byte)'_',
                        (byte)'0',
                        (byte)'1',
                        (byte)'7',
                        (byte)'_',
                        (byte)'R',
                        (byte)'E');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'A',
                        (byte)'D',
                        (byte)'Y',
                        (byte)' ',
                        (byte)'p',
                        (byte)'a',
                        (byte)'g',
                        (byte)'e');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'B',
                        (byte)'y',
                        (byte)'t',
                        (byte)'e',
                        (byte)'s',
                        (byte)'=',
                        (byte)'4',
                        (byte)'0');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'9',
                        (byte)'6',
                        (byte)' ',
                        (byte)'s',
                        (byte)'a',
                        (byte)'f',
                        (byte)'e',
                        (byte)'O');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'f',
                        (byte)'f',
                        (byte)'s',
                        (byte)'e',
                        (byte)'t',
                        (byte)'=',
                        (byte)'0',
                        (byte)' ');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'o',
                        (byte)'u',
                        (byte)'t',
                        (byte)'8',
                        (byte)'O',
                        (byte)'f',
                        (byte)'f',
                        (byte)'s');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'e',
                        (byte)'t',
                        (byte)'=',
                        (byte)'1',
                        (byte)'6',
                        (byte)' ',
                        (byte)'i',
                        (byte)'n');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'8',
                        (byte)'O',
                        (byte)'f',
                        (byte)'f',
                        (byte)'s',
                        (byte)'e',
                        (byte)'t',
                        (byte)'=');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'3',
                        (byte)'2',
                        (byte)' ',
                        (byte)'f',
                        (byte)'n',
                        (byte)'v',
                        (byte)'1',
                        (byte)'a');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'6',
                        (byte)'4',
                        (byte)'=',
                        (byte)'9',
                        (byte)'8',
                        (byte)'2',
                        (byte)'E',
                        (byte)'5');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'B',
                        (byte)'7',
                        (byte)'E',
                        (byte)'3',
                        (byte)'E',
                        (byte)'E',
                        (byte)'5',
                        (byte)'8');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'E',
                        (byte)'5',
                        (byte)'6',
                        (byte)' ',
                        (byte)'t',
                        (byte)'e',
                        (byte)'m',
                        (byte)'p');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'o',
                        (byte)'r',
                        (byte)'a',
                        (byte)'r',
                        (byte)'y',
                        (byte)'O',
                        (byte)'w',
                        (byte)'n');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'e',
                        (byte)'d',
                        (byte)'R',
                        (byte)'o',
                        (byte)'o',
                        (byte)'t',
                        (byte)'R',
                        (byte)'x');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'=',
                        (byte)'t',
                        (byte)'r',
                        (byte)'u',
                        (byte)'e',
                        (byte)' ',
                        (byte)'p',
                        (byte)'o');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'r',
                        (byte)'t',
                        (byte)'I',
                        (byte)'o',
                        (byte)'E',
                        (byte)'x',
                        (byte)'e',
                        (byte)'c');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'u',
                        (byte)'t',
                        (byte)'e',
                        (byte)'d',
                        (byte)'=',
                        (byte)'f',
                        (byte)'a',
                        (byte)'l');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'s',
                        (byte)'e',
                        0,
                        0,
                        0,
                        0,
                        0,
                        0);
                    break;
                case 5:
                    WriteAscii(
                        console,
                        serial,
                        (byte)'T',
                        (byte)'E',
                        (byte)'S',
                        (byte)'T',
                        (byte)'_',
                        (byte)'P',
                        (byte)'A',
                        (byte)'S');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'S',
                        (byte)' ',
                        (byte)'m',
                        (byte)'0',
                        (byte)'2',
                        (byte)'.',
                        (byte)'0',
                        (byte)'1');
                    WriteAscii(
                        console,
                        serial,
                        (byte)'7',
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0);
                    break;
                default:
                    return;
            }

            EfiEntry.WriteCrLf(console, serial);
        }
#endif

#if M02_CHECKED_PORT_IO_BINDING_PROBE
        private static void WriteM02CheckedPortIoBindingSuccessLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(
                console,
                serial,
                (byte)'C',
                (byte)'L',
                (byte)'R',
                (byte)'O',
                (byte)'S',
                (byte)'_',
                (byte)'M',
                (byte)'0');
            WriteAscii(
                console,
                serial,
                (byte)'2',
                (byte)'_',
                (byte)'0',
                (byte)'0',
                (byte)'3',
                (byte)'_',
                (byte)'R',
                (byte)'E');
            WriteAscii(
                console,
                serial,
                (byte)'A',
                (byte)'D',
                (byte)'Y',
                (byte)' ',
                (byte)'b',
                (byte)'i',
                (byte)'n',
                (byte)'d');
            WriteAscii(
                console,
                serial,
                (byte)'i',
                (byte)'n',
                (byte)'g',
                (byte)'R',
                (byte)'e',
                (byte)'a',
                (byte)'d',
                (byte)'y');
            WriteAscii(
                console,
                serial,
                (byte)'=',
                (byte)'t',
                (byte)'r',
                (byte)'u',
                (byte)'e',
                (byte)' ',
                (byte)'o',
                (byte)'u');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'8',
                (byte)'A',
                (byte)'d',
                (byte)'d',
                (byte)'r',
                (byte)'e',
                (byte)'s');
            WriteAscii(
                console,
                serial,
                (byte)'s',
                (byte)'C',
                (byte)'h',
                (byte)'e',
                (byte)'c',
                (byte)'k',
                (byte)'e',
                (byte)'d');
            WriteAscii(
                console,
                serial,
                (byte)'=',
                (byte)'t',
                (byte)'r',
                (byte)'u',
                (byte)'e',
                (byte)' ',
                (byte)'i',
                (byte)'n');
            WriteAscii(
                console,
                serial,
                (byte)'8',
                (byte)'A',
                (byte)'d',
                (byte)'d',
                (byte)'r',
                (byte)'e',
                (byte)'s',
                (byte)'s');
            WriteAscii(
                console,
                serial,
                (byte)'C',
                (byte)'h',
                (byte)'e',
                (byte)'c',
                (byte)'k',
                (byte)'e',
                (byte)'d',
                (byte)'=');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'r',
                (byte)'u',
                (byte)'e',
                (byte)' ',
                (byte)'g',
                (byte)'u',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)'s',
                (byte)'t',
                (byte)'I',
                (byte)'n',
                (byte)'v',
                (byte)'o',
                (byte)'c',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'i',
                (byte)'o',
                (byte)'n',
                (byte)'=',
                (byte)'f',
                (byte)'a',
                (byte)'l');
            WriteAscii(
                console,
                serial,
                (byte)'s',
                (byte)'e',
                (byte)' ',
                (byte)'p',
                (byte)'o',
                (byte)'r',
                (byte)'t',
                (byte)'I');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'E',
                (byte)'x',
                (byte)'e',
                (byte)'c',
                (byte)'u',
                (byte)'t',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)'d',
                (byte)'=',
                (byte)'f',
                (byte)'a',
                (byte)'l',
                (byte)'s',
                (byte)'e',
                0);
            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteM02CheckedPortIoBindingFailureLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(
                console,
                serial,
                (byte)'M',
                (byte)'0',
                (byte)'2',
                (byte)'_',
                (byte)'C',
                (byte)'H',
                (byte)'E',
                (byte)'C');
            WriteAscii(
                console,
                serial,
                (byte)'K',
                (byte)'E',
                (byte)'D',
                (byte)'_',
                (byte)'P',
                (byte)'O',
                (byte)'R',
                (byte)'T');
            WriteAscii(
                console,
                serial,
                (byte)'_',
                (byte)'I',
                (byte)'O',
                (byte)'_',
                (byte)'B',
                (byte)'I',
                (byte)'N',
                (byte)'D');
            WriteAscii(
                console,
                serial,
                (byte)'I',
                (byte)'N',
                (byte)'G',
                (byte)'_',
                (byte)'F',
                (byte)'A',
                (byte)'I',
                (byte)'L');
            WriteAscii(
                console,
                serial,
                (byte)'E',
                (byte)'D',
                0,
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteCrLf(console, serial);
        }
#endif

#if M02_COM1_OWNED_PORT_IO_PROBE
        private static void WriteM02Com1OwnedPortIoSuccessLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(
                console,
                serial,
                (byte)'C',
                (byte)'L',
                (byte)'R',
                (byte)'O',
                (byte)'S',
                (byte)'_',
                (byte)'M',
                (byte)'0');
            WriteAscii(
                console,
                serial,
                (byte)'2',
                (byte)'_',
                (byte)'0',
                (byte)'0',
                (byte)'4',
                (byte)'_',
                (byte)'R',
                (byte)'E');
            WriteAscii(
                console,
                serial,
                (byte)'A',
                (byte)'D',
                (byte)'Y',
                (byte)' ',
                (byte)'c',
                (byte)'o',
                (byte)'n',
                (byte)'f');
            WriteAscii(
                console,
                serial,
                (byte)'i',
                (byte)'g',
                (byte)'u',
                (byte)'r',
                (byte)'a',
                (byte)'t',
                (byte)'i',
                (byte)'o');
            WriteAscii(
                console,
                serial,
                (byte)'n',
                (byte)'W',
                (byte)'r',
                (byte)'i',
                (byte)'t',
                (byte)'e',
                (byte)'s',
                (byte)'=');
            WriteAscii(
                console,
                serial,
                (byte)'7',
                (byte)' ',
                (byte)'b',
                (byte)'i',
                (byte)'n',
                (byte)'d',
                (byte)'i',
                (byte)'n');
            WriteAscii(
                console,
                serial,
                (byte)'g',
                (byte)'R',
                (byte)'e',
                (byte)'a',
                (byte)'d',
                (byte)'y',
                (byte)'=',
                (byte)'t');
            WriteAscii(
                console,
                serial,
                (byte)'r',
                (byte)'u',
                (byte)'e',
                (byte)' ',
                (byte)'g',
                (byte)'u',
                (byte)'e',
                (byte)'s');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'P',
                (byte)'o',
                (byte)'r',
                (byte)'t',
                (byte)'I',
                (byte)'o',
                (byte)'=');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'r',
                (byte)'u',
                (byte)'e',
                (byte)' ',
                (byte)'g',
                (byte)'u',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)'s',
                (byte)'t',
                (byte)'I',
                (byte)'n',
                (byte)'8',
                (byte)'=',
                (byte)'f',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'l',
                (byte)'s',
                (byte)'e',
                (byte)' ',
                (byte)'m',
                (byte)'a',
                (byte)'r',
                (byte)'k');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'r',
                (byte)'T',
                (byte)'r',
                (byte)'a',
                (byte)'n',
                (byte)'s',
                (byte)'p');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'r',
                (byte)'t',
                (byte)'=',
                (byte)'u',
                (byte)'e',
                (byte)'f',
                (byte)'i');
            WriteAscii(
                console,
                serial,
                (byte)'S',
                (byte)'e',
                (byte)'r',
                (byte)'i',
                (byte)'a',
                (byte)'l',
                (byte)' ',
                (byte)'p');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'s',
                (byte)'t',
                (byte)'E',
                (byte)'x',
                (byte)'i',
                (byte)'t',
                (byte)'B');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'o',
                (byte)'t',
                (byte)'S',
                (byte)'e',
                (byte)'r',
                (byte)'v',
                (byte)'i');
            WriteAscii(
                console,
                serial,
                (byte)'c',
                (byte)'e',
                (byte)'s',
                (byte)'=',
                (byte)'f',
                (byte)'a',
                (byte)'l',
                (byte)'s');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                0,
                0,
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteM02Com1OwnedPortIoFailureLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(
                console,
                serial,
                (byte)'M',
                (byte)'0',
                (byte)'2',
                (byte)'_',
                (byte)'C',
                (byte)'O',
                (byte)'M',
                (byte)'1');
            WriteAscii(
                console,
                serial,
                (byte)'_',
                (byte)'C',
                (byte)'O',
                (byte)'N',
                (byte)'F',
                (byte)'I',
                (byte)'G',
                (byte)'U');
            WriteAscii(
                console,
                serial,
                (byte)'R',
                (byte)'A',
                (byte)'T',
                (byte)'I',
                (byte)'O',
                (byte)'N',
                (byte)'_',
                (byte)'F');
            WriteAscii(
                console,
                serial,
                (byte)'A',
                (byte)'I',
                (byte)'L',
                (byte)'E',
                (byte)'D',
                0,
                0,
                0);
            EfiEntry.WriteCrLf(console, serial);
        }
#endif

#if M02_OWNED_COM1_READY_PROBE
        private static void WriteM02OwnedCom1ReadySuccessLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(
                console,
                serial,
                (byte)'C',
                (byte)'L',
                (byte)'R',
                (byte)'O',
                (byte)'S',
                (byte)'_',
                (byte)'M',
                (byte)'0');
            WriteAscii(
                console,
                serial,
                (byte)'2',
                (byte)'_',
                (byte)'0',
                (byte)'0',
                (byte)'5',
                (byte)'_',
                (byte)'R',
                (byte)'E');
            WriteAscii(
                console,
                serial,
                (byte)'A',
                (byte)'D',
                (byte)'Y',
                (byte)' ',
                (byte)'m',
                (byte)'a',
                (byte)'r',
                (byte)'k');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'r',
                (byte)'=',
                (byte)'O',
                (byte)'W',
                (byte)'N',
                (byte)'E',
                (byte)'D');
            WriteAscii(
                console,
                serial,
                (byte)'_',
                (byte)'C',
                (byte)'O',
                (byte)'M',
                (byte)'1',
                (byte)'_',
                (byte)'R',
                (byte)'E');
            WriteAscii(
                console,
                serial,
                (byte)'A',
                (byte)'D',
                (byte)'Y',
                (byte)' ',
                (byte)'m',
                (byte)'a',
                (byte)'r',
                (byte)'k');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'r',
                (byte)'B',
                (byte)'y',
                (byte)'t',
                (byte)'e',
                (byte)'s',
                (byte)'=');
            WriteAscii(
                console,
                serial,
                (byte)'1',
                (byte)'6',
                (byte)' ',
                (byte)'l',
                (byte)'i',
                (byte)'n',
                (byte)'e',
                (byte)'E');
            WriteAscii(
                console,
                serial,
                (byte)'n',
                (byte)'d',
                (byte)'i',
                (byte)'n',
                (byte)'g',
                (byte)'B',
                (byte)'y',
                (byte)'t');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'s',
                (byte)'=',
                (byte)'2',
                (byte)' ',
                (byte)'c',
                (byte)'o',
                (byte)'n');
            WriteAscii(
                console,
                serial,
                (byte)'f',
                (byte)'i',
                (byte)'g',
                (byte)'u',
                (byte)'r',
                (byte)'a',
                (byte)'t',
                (byte)'i');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'n',
                (byte)'W',
                (byte)'r',
                (byte)'i',
                (byte)'t',
                (byte)'e',
                (byte)'s');
            WriteAscii(
                console,
                serial,
                (byte)'=',
                (byte)'7',
                (byte)' ',
                (byte)'l',
                (byte)'i',
                (byte)'n',
                (byte)'e',
                (byte)'S');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'a',
                (byte)'t',
                (byte)'u',
                (byte)'s',
                (byte)'P',
                (byte)'o',
                (byte)'l');
            WriteAscii(
                console,
                serial,
                (byte)'l',
                (byte)'s',
                (byte)'=',
                (byte)'1',
                (byte)'8',
                (byte)' ',
                (byte)'o',
                (byte)'w');
            WriteAscii(
                console,
                serial,
                (byte)'n',
                (byte)'e',
                (byte)'d',
                (byte)'W',
                (byte)'r',
                (byte)'i',
                (byte)'t',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)'r',
                (byte)'=',
                (byte)'t',
                (byte)'r',
                (byte)'u',
                (byte)'e',
                (byte)' ',
                (byte)'o');
            WriteAscii(
                console,
                serial,
                (byte)'w',
                (byte)'n',
                (byte)'e',
                (byte)'d',
                (byte)'M',
                (byte)'a',
                (byte)'r',
                (byte)'k');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'r',
                (byte)'T',
                (byte)'r',
                (byte)'a',
                (byte)'n',
                (byte)'s',
                (byte)'p');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'r',
                (byte)'t',
                (byte)'=',
                (byte)'c',
                (byte)'o',
                (byte)'m',
                (byte)'1');
            WriteAscii(
                console,
                serial,
                (byte)'P',
                (byte)'o',
                (byte)'r',
                (byte)'t',
                (byte)'I',
                (byte)'o',
                (byte)' ',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)'v',
                (byte)'i',
                (byte)'d',
                (byte)'e',
                (byte)'n',
                (byte)'c',
                (byte)'e',
                (byte)'M');
            WriteAscii(
                console,
                serial,
                (byte)'a',
                (byte)'r',
                (byte)'k',
                (byte)'e',
                (byte)'r',
                (byte)'T',
                (byte)'r',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'n',
                (byte)'s',
                (byte)'p',
                (byte)'o',
                (byte)'r',
                (byte)'t',
                (byte)'=',
                (byte)'u');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'f',
                (byte)'i',
                (byte)'S',
                (byte)'e',
                (byte)'r',
                (byte)'i',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'l',
                (byte)' ',
                (byte)'g',
                (byte)'u',
                (byte)'e',
                (byte)'s',
                (byte)'t',
                (byte)'P');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'r',
                (byte)'t',
                (byte)'I',
                (byte)'o',
                (byte)'=',
                (byte)'t',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'u',
                (byte)'e',
                (byte)' ',
                (byte)'g',
                (byte)'u',
                (byte)'e',
                (byte)'s',
                (byte)'t');
            WriteAscii(
                console,
                serial,
                (byte)'I',
                (byte)'n',
                (byte)'8',
                (byte)'=',
                (byte)'t',
                (byte)'r',
                (byte)'u',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'p',
                (byte)'o',
                (byte)'s',
                (byte)'t',
                (byte)'E',
                (byte)'x',
                (byte)'i');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'B',
                (byte)'o',
                (byte)'o',
                (byte)'t',
                (byte)'S',
                (byte)'e',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'v',
                (byte)'i',
                (byte)'c',
                (byte)'e',
                (byte)'s',
                (byte)'=',
                (byte)'f',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'l',
                (byte)'s',
                (byte)'e',
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteM02OwnedCom1ReadyFailureLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(
                console,
                serial,
                (byte)'M',
                (byte)'0',
                (byte)'2',
                (byte)'_',
                (byte)'O',
                (byte)'W',
                (byte)'N',
                (byte)'E');
            WriteAscii(
                console,
                serial,
                (byte)'D',
                (byte)'_',
                (byte)'C',
                (byte)'O',
                (byte)'M',
                (byte)'1',
                (byte)'_',
                (byte)'R');
            WriteAscii(
                console,
                serial,
                (byte)'E',
                (byte)'A',
                (byte)'D',
                (byte)'Y',
                (byte)'_',
                (byte)'F',
                (byte)'A',
                (byte)'I');
            WriteAscii(
                console,
                serial,
                (byte)'L',
                (byte)'E',
                (byte)'D',
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteCrLf(console, serial);
        }
#endif

#if M02_PANIC_SAFE_OWNED_COM1_PROBE
        private static void WriteM02PanicSafeOwnedCom1EvidenceLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(
                console,
                serial,
                (byte)'C',
                (byte)'L',
                (byte)'R',
                (byte)'O',
                (byte)'S',
                (byte)'_',
                (byte)'M',
                (byte)'0');
            WriteAscii(
                console,
                serial,
                (byte)'2',
                (byte)'_',
                (byte)'0',
                (byte)'0',
                (byte)'6',
                (byte)'_',
                (byte)'E',
                (byte)'V');
            WriteAscii(
                console,
                serial,
                (byte)'I',
                (byte)'D',
                (byte)'E',
                (byte)'N',
                (byte)'C',
                (byte)'E',
                (byte)' ',
                (byte)'m');
            WriteAscii(
                console,
                serial,
                (byte)'a',
                (byte)'r',
                (byte)'k',
                (byte)'e',
                (byte)'r',
                (byte)'B',
                (byte)'y',
                (byte)'t');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'s',
                (byte)'=',
                (byte)'1',
                (byte)'2',
                (byte)'5',
                (byte)' ',
                (byte)'s');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'r',
                (byte)'i',
                (byte)'a',
                (byte)'l',
                (byte)'B',
                (byte)'y',
                (byte)'t');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'s',
                (byte)'=',
                (byte)'1',
                (byte)'2',
                (byte)'7',
                (byte)' ',
                (byte)'c');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'n',
                (byte)'f',
                (byte)'i',
                (byte)'g',
                (byte)'u',
                (byte)'r',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'i',
                (byte)'o',
                (byte)'n',
                (byte)'W',
                (byte)'r',
                (byte)'i',
                (byte)'t');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'s',
                (byte)'=',
                (byte)'7',
                (byte)' ',
                (byte)'l',
                (byte)'i',
                (byte)'n');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'S',
                (byte)'t',
                (byte)'a',
                (byte)'t',
                (byte)'u',
                (byte)'s',
                (byte)'P');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'l',
                (byte)'l',
                (byte)'s',
                (byte)'=',
                (byte)'1',
                (byte)'2',
                (byte)'7');
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'m',
                (byte)'a',
                (byte)'r',
                (byte)'k',
                (byte)'e',
                (byte)'r',
                (byte)'T');
            WriteAscii(
                console,
                serial,
                (byte)'r',
                (byte)'a',
                (byte)'n',
                (byte)'s',
                (byte)'p',
                (byte)'o',
                (byte)'r',
                (byte)'t');
            WriteAscii(
                console,
                serial,
                (byte)'=',
                (byte)'c',
                (byte)'o',
                (byte)'m',
                (byte)'1',
                (byte)'P',
                (byte)'o',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'I',
                (byte)'o',
                (byte)' ',
                (byte)'e',
                (byte)'v',
                (byte)'i',
                (byte)'d');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)'n',
                (byte)'c',
                (byte)'e',
                (byte)'T',
                (byte)'r',
                (byte)'a',
                (byte)'n');
            WriteAscii(
                console,
                serial,
                (byte)'s',
                (byte)'p',
                (byte)'o',
                (byte)'r',
                (byte)'t',
                (byte)'=',
                (byte)'u',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)'f',
                (byte)'i',
                (byte)'S',
                (byte)'e',
                (byte)'r',
                (byte)'i',
                (byte)'a',
                (byte)'l');
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'g',
                (byte)'u',
                (byte)'e',
                (byte)'s',
                (byte)'t',
                (byte)'P',
                (byte)'o');
            WriteAscii(
                console,
                serial,
                (byte)'r',
                (byte)'t',
                (byte)'I',
                (byte)'o',
                (byte)'=',
                (byte)'t',
                (byte)'r',
                (byte)'u');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)' ',
                (byte)'g',
                (byte)'u',
                (byte)'e',
                (byte)'s',
                (byte)'t',
                (byte)'I');
            WriteAscii(
                console,
                serial,
                (byte)'n',
                (byte)'8',
                (byte)'=',
                (byte)'t',
                (byte)'r',
                (byte)'u',
                (byte)'e',
                (byte)' ');
            WriteAscii(
                console,
                serial,
                (byte)'p',
                (byte)'a',
                (byte)'n',
                (byte)'i',
                (byte)'c',
                (byte)'F',
                (byte)'o',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'m',
                (byte)'a',
                (byte)'t',
                (byte)'t',
                (byte)'e',
                (byte)'r',
                (byte)'=',
                (byte)'l');
            WriteAscii(
                console,
                serial,
                (byte)'i',
                (byte)'n',
                (byte)'k',
                (byte)'e',
                (byte)'d',
                (byte)' ',
                (byte)'p',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'n',
                (byte)'i',
                (byte)'c',
                (byte)'R',
                (byte)'e',
                (byte)'p',
                (byte)'o',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'e',
                (byte)'r',
                (byte)'=',
                (byte)'o',
                (byte)'w',
                (byte)'n',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)'d',
                (byte)'C',
                (byte)'o',
                (byte)'m',
                (byte)'1',
                (byte)' ',
                (byte)'c',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'l',
                (byte)'l',
                (byte)'e',
                (byte)'r',
                (byte)'S',
                (byte)'c',
                (byte)'r',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'c',
                (byte)'h',
                (byte)'=',
                (byte)'t',
                (byte)'r',
                (byte)'u',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'s',
                (byte)'t',
                (byte)'a',
                (byte)'t',
                (byte)'u',
                (byte)'s',
                (byte)'C');
            WriteAscii(
                console,
                serial,
                (byte)'o',
                (byte)'d',
                (byte)'e',
                (byte)'d',
                (byte)'=',
                (byte)'t',
                (byte)'r',
                (byte)'u');
            WriteAscii(
                console,
                serial,
                (byte)'e',
                (byte)' ',
                (byte)'f',
                (byte)'i',
                (byte)'r',
                (byte)'m',
                (byte)'w',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'r',
                (byte)'e',
                (byte)'F',
                (byte)'a',
                (byte)'l',
                (byte)'l',
                (byte)'b',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'c',
                (byte)'k',
                (byte)'=',
                (byte)'f',
                (byte)'a',
                (byte)'l',
                (byte)'s',
                (byte)'e');
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'p',
                (byte)'o',
                (byte)'s',
                (byte)'t',
                (byte)'E',
                (byte)'x',
                (byte)'i');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'B',
                (byte)'o',
                (byte)'o',
                (byte)'t',
                (byte)'S',
                (byte)'e',
                (byte)'r');
            WriteAscii(
                console,
                serial,
                (byte)'v',
                (byte)'i',
                (byte)'c',
                (byte)'e',
                (byte)'s',
                (byte)'=',
                (byte)'f',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'l',
                (byte)'s',
                (byte)'e',
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteM02PanicSafeOwnedCom1FailureLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(
                console,
                serial,
                (byte)'M',
                (byte)'0',
                (byte)'2',
                (byte)'_',
                (byte)'P',
                (byte)'A',
                (byte)'N',
                (byte)'I');
            WriteAscii(
                console,
                serial,
                (byte)'C',
                (byte)'_',
                (byte)'S',
                (byte)'A',
                (byte)'F',
                (byte)'E',
                (byte)'_',
                (byte)'O');
            WriteAscii(
                console,
                serial,
                (byte)'W',
                (byte)'N',
                (byte)'E',
                (byte)'D',
                (byte)'_',
                (byte)'C',
                (byte)'O',
                (byte)'M');
            WriteAscii(
                console,
                serial,
                (byte)'1',
                (byte)'_',
                (byte)'F',
                (byte)'A',
                (byte)'I',
                (byte)'L',
                (byte)'E',
                (byte)'D');
            EfiEntry.WriteCrLf(console, serial);
        }
#endif

#if M02_OWNED_X64_GDT_ENCODING_PROBE
        private static void WriteM02OwnedX64GdtEncodingSuccessLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
            WriteAscii(console, serial, (byte)'2', (byte)'_', (byte)'0', (byte)'0', (byte)'7', (byte)'_', (byte)'R', (byte)'E');
            WriteAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'e', (byte)'n', (byte)'c', (byte)'o');
            WriteAscii(console, serial, (byte)'d', (byte)'i', (byte)'n', (byte)'g', (byte)'=', (byte)'g', (byte)'u', (byte)'e');
            WriteAscii(console, serial, (byte)'s', (byte)'t', (byte)'V', (byte)'a', (byte)'l', (byte)'i', (byte)'d', (byte)'a');
            WriteAscii(console, serial, (byte)'t', (byte)'e', (byte)'d', (byte)' ', (byte)'c', (byte)'a', (byte)'l', (byte)'l');
            WriteAscii(console, serial, (byte)'e', (byte)'r', (byte)'O', (byte)'w', (byte)'n', (byte)'e', (byte)'d', (byte)'=');
            WriteAscii(console, serial, (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'r', (byte)'e', (byte)'a');
            WriteAscii(console, serial, (byte)'d', (byte)'B', (byte)'a', (byte)'c', (byte)'k', (byte)'=', (byte)'t', (byte)'r');
            WriteAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'d', (byte)'t', (byte)'B', (byte)'y');
            WriteAscii(console, serial, (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'4', (byte)'0', (byte)' ', (byte)'t');
            WriteAscii(console, serial, (byte)'s', (byte)'s', (byte)'B', (byte)'y', (byte)'t', (byte)'e', (byte)'s', (byte)'=');
            WriteAscii(console, serial, (byte)'1', (byte)'0', (byte)'4', (byte)' ', (byte)'t', (byte)'a', (byte)'b', (byte)'l');
            WriteAscii(console, serial, (byte)'e', (byte)'L', (byte)'i', (byte)'m', (byte)'i', (byte)'t', (byte)'=', (byte)'3');
            WriteAscii(console, serial, (byte)'9', (byte)' ', (byte)'s', (byte)'e', (byte)'l', (byte)'e', (byte)'c', (byte)'t');
            WriteAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'0', (byte)',', (byte)'8', (byte)',');
            WriteAscii(console, serial, (byte)'1', (byte)'6', (byte)',', (byte)'2', (byte)'4', (byte)' ', (byte)'g', (byte)'u');
            WriteAscii(console, serial, (byte)'e', (byte)'s', (byte)'t', (byte)'E', (byte)'n', (byte)'c', (byte)'o', (byte)'d');
            WriteAscii(console, serial, (byte)'i', (byte)'n', (byte)'g', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
            WriteAscii(console, serial, (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'L', (byte)'g');
            WriteAscii(console, serial, (byte)'d', (byte)'t', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e');
            WriteAscii(console, serial, (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'L', (byte)'t');
            WriteAscii(console, serial, (byte)'r', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
            WriteAscii(console, serial, (byte)'a', (byte)'l', (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'t', (byte)'i');
            WriteAscii(console, serial, (byte)'o', (byte)'n', (byte)'F', (byte)'r', (byte)'e', (byte)'e', (byte)'=', (byte)'t');
            WriteAscii(console, serial, (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'m', (byte)'a', (byte)'r', (byte)'k');
            WriteAscii(console, serial, (byte)'e', (byte)'r', (byte)'T', (byte)'r', (byte)'a', (byte)'n', (byte)'s', (byte)'p');
            WriteAscii(console, serial, (byte)'o', (byte)'r', (byte)'t', (byte)'=', (byte)'u', (byte)'e', (byte)'f', (byte)'i');
            WriteAscii(console, serial, (byte)'S', (byte)'e', (byte)'r', (byte)'i', (byte)'a', (byte)'l', (byte)' ', (byte)'p');
            WriteAscii(console, serial, (byte)'o', (byte)'s', (byte)'t', (byte)'E', (byte)'x', (byte)'i', (byte)'t', (byte)'B');
            WriteAscii(console, serial, (byte)'o', (byte)'o', (byte)'t', (byte)'S', (byte)'e', (byte)'r', (byte)'v', (byte)'i');
            WriteAscii(console, serial, (byte)'c', (byte)'e', (byte)'s', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s');
            WriteAscii(console, serial, (byte)'e', 0, 0, 0, 0, 0, 0, 0);
            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteM02OwnedX64GdtEncodingFailureLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
        {
            WriteAscii(console, serial, (byte)'M', (byte)'0', (byte)'2', (byte)'_', (byte)'O', (byte)'W', (byte)'N', (byte)'E');
            WriteAscii(console, serial, (byte)'D', (byte)'_', (byte)'X', (byte)'6', (byte)'4', (byte)'_', (byte)'G', (byte)'D');
            WriteAscii(console, serial, (byte)'T', (byte)'_', (byte)'E', (byte)'N', (byte)'C', (byte)'O', (byte)'D', (byte)'I');
            WriteAscii(console, serial, (byte)'N', (byte)'G', (byte)'_', (byte)'F', (byte)'A', (byte)'I', (byte)'L', (byte)'E');
            WriteAscii(console, serial, (byte)'D', 0, 0, 0, 0, 0, 0, 0);
            EfiEntry.WriteCrLf(console, serial);
        }
#endif

#if M02_GUARDED_IST_STACK_ALLOCATION_PROBE
        private static void
            WriteM02GuardedIstStackAllocationSuccessLine(
                EfiSimpleTextOutputProtocol* console,
                EfiSerialIoProtocol* serial)
        {
            WriteAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
            WriteAscii(console, serial, (byte)'2', (byte)'_', (byte)'0', (byte)'0', (byte)'8', (byte)'_', (byte)'R', (byte)'E');
            WriteAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'s', (byte)'t', (byte)'a', (byte)'c');
            WriteAscii(console, serial, (byte)'k', (byte)'s', (byte)'=', (byte)'3', (byte)' ', (byte)'g', (byte)'u', (byte)'a');
            WriteAscii(console, serial, (byte)'r', (byte)'d', (byte)'s', (byte)'=', (byte)'3', (byte)' ', (byte)'d', (byte)'o');
            WriteAscii(console, serial, (byte)'u', (byte)'b', (byte)'l', (byte)'e', (byte)'F', (byte)'a', (byte)'u', (byte)'l');
            WriteAscii(console, serial, (byte)'t', (byte)'I', (byte)'s', (byte)'t', (byte)'=', (byte)'1', (byte)' ', (byte)'p');
            WriteAscii(console, serial, (byte)'a', (byte)'g', (byte)'e', (byte)'F', (byte)'a', (byte)'u', (byte)'l', (byte)'t');
            WriteAscii(console, serial, (byte)'I', (byte)'s', (byte)'t', (byte)'=', (byte)'2', (byte)' ', (byte)'n', (byte)'m');
            WriteAscii(console, serial, (byte)'i', (byte)'I', (byte)'s', (byte)'t', (byte)'=', (byte)'3', (byte)' ', (byte)'w');
            WriteAscii(console, serial, (byte)'r', (byte)'i', (byte)'t', (byte)'a', (byte)'b', (byte)'l', (byte)'e', (byte)'M');
            WriteAscii(console, serial, (byte)'e', (byte)'t', (byte)'a', (byte)'d', (byte)'a', (byte)'t', (byte)'a', (byte)'=');
            WriteAscii(console, serial, (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'e', (byte)'x', (byte)'e');
            WriteAscii(console, serial, (byte)'c', (byte)'u', (byte)'t', (byte)'a', (byte)'b', (byte)'l', (byte)'e', (byte)'M');
            WriteAscii(console, serial, (byte)'e', (byte)'t', (byte)'a', (byte)'d', (byte)'a', (byte)'t', (byte)'a', (byte)'=');
            WriteAscii(console, serial, (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'a', (byte)'l');
            WriteAscii(console, serial, (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n');
            WriteAscii(console, serial, (byte)'F', (byte)'r', (byte)'e', (byte)'e', (byte)'=', (byte)'t', (byte)'r', (byte)'u');
            WriteAscii(console, serial, (byte)'e', (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'A');
            WriteAscii(console, serial, (byte)'r', (byte)'e', (byte)'n', (byte)'a', (byte)'A', (byte)'l', (byte)'l', (byte)'o');
            WriteAscii(console, serial, (byte)'c', (byte)'a', (byte)'t', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r');
            WriteAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t');
            WriteAscii(console, serial, (byte)'S', (byte)'t', (byte)'a', (byte)'c', (byte)'k', (byte)'W', (byte)'r', (byte)'i');
            WriteAscii(console, serial, (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
            WriteAscii(console, serial, (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'G', (byte)'u');
            WriteAscii(console, serial, (byte)'a', (byte)'r', (byte)'d', (byte)'M', (byte)'a', (byte)'p', (byte)'p', (byte)'i');
            WriteAscii(console, serial, (byte)'n', (byte)'g', (byte)'s', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s');
            WriteAscii(console, serial, (byte)'e', (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'T');
            WriteAscii(console, serial, (byte)'s', (byte)'s', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e');
            WriteAscii(console, serial, (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'I', (byte)'d');
            WriteAscii(console, serial, (byte)'t', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
            WriteAscii(console, serial, (byte)'a', (byte)'r', (byte)'e', (byte)'n', (byte)'a', (byte)'F', (byte)'r', (byte)'e');
            WriteAscii(console, serial, (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ');
            WriteAscii(console, serial, (byte)'m', (byte)'a', (byte)'r', (byte)'k', (byte)'e', (byte)'r', (byte)'T', (byte)'r');
            WriteAscii(console, serial, (byte)'a', (byte)'n', (byte)'s', (byte)'p', (byte)'o', (byte)'r', (byte)'t', (byte)'=');
            WriteAscii(console, serial, (byte)'u', (byte)'e', (byte)'f', (byte)'i', (byte)'S', (byte)'e', (byte)'r', (byte)'i');
            WriteAscii(console, serial, (byte)'a', (byte)'l', (byte)' ', (byte)'p', (byte)'o', (byte)'s', (byte)'t', (byte)'E');
            WriteAscii(console, serial, (byte)'x', (byte)'i', (byte)'t', (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'S');
            WriteAscii(console, serial, (byte)'e', (byte)'r', (byte)'v', (byte)'i', (byte)'c', (byte)'e', (byte)'s', (byte)'=');
            WriteAscii(console, serial, (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', 0, 0, 0);
            EfiEntry.WriteCrLf(console, serial);
        }

        private static void
            WriteM02GuardedIstStackAllocationFailureLine(
                EfiSimpleTextOutputProtocol* console,
                EfiSerialIoProtocol* serial)
        {
            WriteAscii(console, serial, (byte)'M', (byte)'0', (byte)'2', (byte)'_', (byte)'G', (byte)'U', (byte)'A', (byte)'R');
            WriteAscii(console, serial, (byte)'D', (byte)'E', (byte)'D', (byte)'_', (byte)'I', (byte)'S', (byte)'T', (byte)'_');
            WriteAscii(console, serial, (byte)'S', (byte)'T', (byte)'A', (byte)'C', (byte)'K', (byte)'_', (byte)'A', (byte)'L');
            WriteAscii(console, serial, (byte)'L', (byte)'O', (byte)'C', (byte)'A', (byte)'T', (byte)'I', (byte)'O', (byte)'N');
            WriteAscii(console, serial, (byte)'_', (byte)'F', (byte)'A', (byte)'I', (byte)'L', (byte)'E', (byte)'D', 0);
            EfiEntry.WriteCrLf(console, serial);
        }
#endif

        private static void WriteFailure(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            M01StageThreeAdapterStatus adapterStatus,
            ulong firmwareStatus,
            ulong tableCleanupStatus,
            ulong probeCleanupStatus,
            ulong codeCleanupStatus)
        {
            WriteFailurePrefix(console, serial, false);
            WriteFailureFields(
                console,
                serial,
                adapterStatus,
                firmwareStatus,
                tableCleanupStatus,
                probeCleanupStatus,
                codeCleanupStatus);
            WriteFailurePrefix(console, serial, true);
            WriteFailureFields(
                console,
                serial,
                adapterStatus,
                firmwareStatus,
                tableCleanupStatus,
                probeCleanupStatus,
                codeCleanupStatus);
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
                    (byte)'3',
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
                (byte)'3',
                (byte)' ',
                0,
                0,
                0);
        }

        private static void WriteFailureFields(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            M01StageThreeAdapterStatus adapterStatus,
            ulong firmwareStatus,
            ulong tableCleanupStatus,
            ulong probeCleanupStatus,
            ulong codeCleanupStatus)
        {
            WriteAscii(
                console,
                serial,
                (byte)'a',
                (byte)'d',
                (byte)'a',
                (byte)'p',
                (byte)'t',
                (byte)'e',
                (byte)'r',
                (byte)'=');
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                (ulong)adapterStatus);
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
                (byte)'t',
                (byte)'=');
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                tableCleanupStatus);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'f',
                (byte)'r',
                (byte)'e',
                (byte)'e',
                (byte)'_',
                (byte)'p',
                (byte)'=');
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                probeCleanupStatus);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'f',
                (byte)'r',
                (byte)'e',
                (byte)'e',
                (byte)'_',
                (byte)'c',
                (byte)'=');
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                codeCleanupStatus);
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

        private static bool TryValidateAllocation(
            ulong address,
            ulong pageCount,
            out ulong byteLength,
            out ulong endExclusive)
        {
            byteLength = 0;
            endExclusive = 0;
            if (address == 0
                || (address & (PageSize - 1)) != 0
                || pageCount == 0
                || pageCount > ~0UL / PageSize)
            {
                return false;
            }

            byteLength = pageCount * PageSize;
            if (address > ~0UL - byteLength)
            {
                byteLength = 0;
                return false;
            }

            endExclusive = address + byteLength;
            return endExclusive > address
                && (endExclusive & (PageSize - 1)) == 0;
        }

        private static bool
            ValidateNewAllocationAgainstStageTwo(
                FirmwareMemoryMapRecord stageTwoMap,
                OS.Kernel.Memory.BootReservedRange*
                    priorReservations,
                ulong priorReservationCount,
                ulong start,
                ulong endExclusive)
        {
            if (priorReservations == null
                || priorReservationCount == 0
                || start == 0
                || start >= endExclusive
                || (start & (PageSize - 1)) != 0
                || (endExclusive & (PageSize - 1)) != 0)
            {
                return false;
            }

            ulong pageCount =
                (endExclusive - start) / PageSize;
            if (pageCount == 0
                || !TryDescribeExactAllocation(
                    stageTwoMap,
                    start,
                    pageCount,
                    EfiConventionalMemory,
                    out BootMemoryRegionRecord priorRegion)
                || priorRegion.PhysicalStart != start
                || priorRegion.PhysicalEndExclusive
                    != endExclusive)
            {
                return false;
            }

            for (ulong index = 0;
                index < priorReservationCount;
                index++)
            {
                if (!TryGetReservationBounds(
                        priorReservations[index],
                        out ulong reservationStart,
                        out ulong reservationEnd)
                    || RangesOverlap(
                        start,
                        endExclusive,
                        reservationStart,
                        reservationEnd))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetReservationBounds(
            OS.Kernel.Memory.BootReservedRange reservation,
            out ulong start,
            out ulong endExclusive)
        {
            start = 0;
            endExclusive = 0;
            return reservation.IsValid
                && reservation.PhysicalStart
                        .TryGetRawValueForUefiBoundary(out start)
                    == AddressValueStatus.Success
                && reservation.PhysicalEndExclusive
                        .TryGetRawValueForUefiBoundary(
                            out endExclusive)
                    == AddressValueStatus.Success
                && start < endExclusive;
        }

        private static bool TryRaw(
            PhysicalAddress address,
            out ulong value)
        {
            return address.TryGetRawValueForUefiBoundary(
                    out value)
                == AddressValueStatus.Success;
        }

        private static bool TryRaw(
            VirtualAddress address,
            out ulong value)
        {
            return address.TryGetRawValueForUefiBoundary(
                    out value)
                == AddressValueStatus.Success;
        }

        private static bool TryVirtual(
            ulong value,
            out VirtualAddress address)
        {
            return VirtualAddress.TryCreateFromUefiBoundary(
                    value,
                    out address)
                == AddressValueStatus.Success;
        }

        private static bool RangesOverlap(
            ulong firstStart,
            ulong firstEndExclusive,
            ulong secondStart,
            ulong secondEndExclusive)
        {
            return firstStart < secondEndExclusive
                && secondStart < firstEndExclusive;
        }

        private static void ZeroBytes(
            byte* destination,
            ulong byteCount)
        {
            if (destination == null)
            {
                return;
            }
            for (ulong index = 0;
                index < byteCount;
                index++)
            {
                destination[index] = 0;
            }
        }

        private static uint ReadUInt32(byte* source)
        {
            return (uint)(
                source[0]
                | (source[1] << 8)
                | (source[2] << 16)
                | (source[3] << 24));
        }

        private static ulong ReadUInt64(byte* source)
        {
            return (ulong)source[0]
                | ((ulong)source[1] << 8)
                | ((ulong)source[2] << 16)
                | ((ulong)source[3] << 24)
                | ((ulong)source[4] << 32)
                | ((ulong)source[5] << 40)
                | ((ulong)source[6] << 48)
                | ((ulong)source[7] << 56);
        }


    }
#endif
}
#pragma warning restore 649
