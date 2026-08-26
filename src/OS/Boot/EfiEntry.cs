namespace OS.Boot;

#pragma warning disable 649

public static unsafe class EfiEntry
{
    private const int OwnedMemoryMapBufferSize = 128 * 1024;
    private const ulong OwnedMemoryRegionCapacity = 4096;
    private const int BootPageAllocatorNodeCapacity = 4096;
    private const int BootPageAllocatorNodeSlotCount = 6;
    private const ulong BootPageAllocatorInvalidNodeIndex = 0xffffffffffffffff;
    private const uint UefiConventionalMemoryType = 7;
    private const int BootPageAllocatorNodeSourceRegionIndexSlot = 0;
    private const int BootPageAllocatorNodePhysicalStartSlot = 1;
    private const int BootPageAllocatorNodePhysicalEndExclusiveSlot = 2;
    private const int BootPageAllocatorNodePageCountSlot = 3;
    private const int BootPageAllocatorNodeByteLengthSlot = 4;
    private const int BootPageAllocatorNodeNextIndexSlot = 5;
#if ACPI_PHYSICAL_DISCOVERY_PROBE
    private const int AcpiRsdpScratchSize = 4096;
    private const int AcpiRootSdtBufferSize = 65536;
    private const int AcpiChildHeaderScratchSize = 36;
    private const int AcpiChildTableBufferSize = 65536;
    private const int AcpiPointerScratchCapacity = 256;
    private const int AcpiEcamSegmentScratchCapacity = 16;
    private const uint UefiAcpiReclaimMemoryType = 9;
    private const uint UefiAcpiMemoryNvsType = 10;
    private const uint AcpiRsdtSignature = 0x54445352;
    private const uint AcpiXsdtSignature = 0x54445358;
    private const uint AcpiMcfgSignature = 0x4746434d;
    private const int AcpiSdtHeaderLength = 36;
    private const int AcpiMcfgAllocationOffset = 44;
    private const int AcpiMcfgAllocationLength = 16;
#endif

#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
    private const ulong M02OwnedGdtTssTerminalEfiSuccess = 0;
    private const uint M02OwnedGdtTssTerminalAllocateAnyPages = 0;
    private const uint M02OwnedGdtTssTerminalEfiLoaderCode = 1;
    private const uint M02OwnedGdtTssTerminalEfiLoaderData = 2;
    private const ulong M02OwnedGdtTssTerminalPageSize = 4096;
    private const ulong M02OwnedGdtTssTerminalPageCount = 1;
    private const byte M02OwnedGdtTssTerminalPreparedState = 1;
    private const byte M02OwnedGdtTssTerminalEnteredState = 2;
    private const byte M02OwnedGdtTssTerminalFailedState = 3;
    private const int M02OwnedGdtTssTerminalStartLine = 0;
    private const int M02OwnedGdtTssTerminalPreEntryFailureLine = 1;
    private const int M02OwnedGdtTssTerminalUnexpectedReturnLine = 2;

    private static byte s_m02OwnedGdtTssTerminalState;
    private static ulong s_m02OwnedGdtTssTerminalCodeAddress;
    private static ulong s_m02OwnedGdtTssTerminalDataAddress;
#endif

#if M02_UD2_VECTOR_STATE_PROBE
    private const ulong M02Ud2VectorStateEfiSuccess = 0;
    private const uint M02Ud2VectorStateAllocateAnyPages = 0;
    private const uint M02Ud2VectorStateEfiLoaderCode = 1;
    private const uint M02Ud2VectorStateEfiLoaderData = 2;
    private const ulong M02Ud2VectorStatePageSize = 4096;
    private const ulong M02Ud2VectorStateCodePageCount = 2;
    private const ulong M02Ud2VectorStateDataPageCount = 5;
    private const ulong M02Ud2VectorStateCodeByteCount =
        M02Ud2VectorStateCodePageCount * M02Ud2VectorStatePageSize;
    private const ulong M02Ud2VectorStateDataByteCount =
        M02Ud2VectorStateDataPageCount * M02Ud2VectorStatePageSize;
    private const byte M02Ud2VectorStatePreparedState = 1;
    private const byte M02Ud2VectorStateEnteredState = 2;
    private const byte M02Ud2VectorStateFailedState = 3;
    private const int M02Ud2VectorStateFailureHeadingLine = 0;
    private const int M02Ud2VectorStateFailureDetailLine = 1;

    private static byte s_m02Ud2VectorStateState;
    private static ulong s_m02Ud2VectorStateCodeAddress;
    private static ulong s_m02Ud2VectorStateDataAddress;
#endif

#if M01_OWNED_PAGING_PROBE
    private static class M01StageThreeAdapter
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
            if (b0 != 0) EfiEntry.Write(console, serial, b0);
            if (b1 != 0) EfiEntry.Write(console, serial, b1);
            if (b2 != 0) EfiEntry.Write(console, serial, b2);
            if (b3 != 0) EfiEntry.Write(console, serial, b3);
            if (b4 != 0) EfiEntry.Write(console, serial, b4);
            if (b5 != 0) EfiEntry.Write(console, serial, b5);
            if (b6 != 0) EfiEntry.Write(console, serial, b6);
            if (b7 != 0) EfiEntry.Write(console, serial, b7);
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

        private enum M01StageThreeAdapterStatus : ulong
        {
            Success = 0,
            RepeatedUse = 1,
            PrerequisiteRejected = 2,
            StageTwoMapRejected = 3,
            CodeAllocationFailed = 4,
            CodeAllocationRejected = 5,
            ProbeAllocationFailed = 6,
            ProbeAllocationRejected = 7,
            ArchitecturalPreflightRejected = 8,
            PlanningInputRejected = 9,
            ArenaPlanRejected = 10,
            TableAllocationFailed = 11,
            TableAllocationRejected = 12,
            PostAllocationMapRejected = 13,
            EvidenceRejected = 14,
            PreparationRejected = 15,
            StatePageRejected = 16,
            ArenaMaterializationRejected = 17,
            BindingOrReadbackRejected = 18,
            RuntimeAddressRejected = 19,
            RuntimeStateRejected = 20,
            GatePreparationRejected = 21,
            GateMaterializationRejected = 22,
            FinalReadbackRejected = 23,
            PreTransferRejected = 24,
            PostReturnAuditRejected = 25,
            InstructionPublicationRejected = 26
        }

        private struct M01StageThreeAllocationRecordBuffer
        {
            public BootMemoryRegionRecord Record0;
            public BootMemoryRegionRecord Record1;
            public BootMemoryRegionRecord Record2;
        }

        private struct M01StageThreeReservationLedgerBuffer
        {
            public OS.Kernel.Memory.BootReservedRange Range0;
            public OS.Kernel.Memory.BootReservedRange Range1;
            public OS.Kernel.Memory.BootReservedRange Range2;
            public OS.Kernel.Memory.BootReservedRange Range3;
            public OS.Kernel.Memory.BootReservedRange Range4;
            public OS.Kernel.Memory.BootReservedRange Range5;
            public OS.Kernel.Memory.BootReservedRange Range6;
            public OS.Kernel.Memory.BootReservedRange Range7;
        }

        private struct M01StageThreeFixedRequirementBuffer
        {
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement0;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement1;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement2;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement3;
        }

        private struct M01StageThreeRequirementBuffer
        {
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement0;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement1;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement2;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement3;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement4;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement5;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement Requirement6;
        }

        private struct M01StageThreeRangeBuffer
        {
            public OS.Kernel.Boot
                .X64BootIdentityMappingRange Range0;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRange Range1;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRange Range2;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRange Range3;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRange Range4;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRange Range5;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRange Range6;
        }

        private unsafe struct M01StageThreePageImageBuffer
        {
            public fixed ulong Values[192];
        }

        private unsafe struct M01StageThreePageEntryBuffer
        {
            public fixed ulong Values[32768];
        }

        private struct M01StageThreeBindingBuffer
        {
            public M01OwnedCr3CompositeTransitionRangeBinding
                Binding0;
            public M01OwnedCr3CompositeTransitionRangeBinding
                Binding1;
            public M01OwnedCr3CompositeTransitionRangeBinding
                Binding2;
            public M01OwnedCr3CompositeTransitionRangeBinding
                Binding3;
            public M01OwnedCr3CompositeTransitionRangeBinding
                Binding4;
            public M01OwnedCr3CompositeTransitionRangeBinding
                Binding5;
        }

        private unsafe struct M01StageThreeSemanticBuffer
        {
            public fixed ulong Values[48];
        }

        private unsafe struct M01StageThreeStateSnapshotBuffer
        {
            public fixed ulong Values[512];
        }

        private unsafe struct M01StageThreePlannerStackBuffer
        {
            public fixed ulong Values[
                PlannerStackUlongCount];
        }

#if M02_OWNED_X64_GDT_ENCODING_PROBE
        private unsafe struct M02OwnedX64GdtEncodingStackWorkspace
        {
            public fixed byte TssBytes[104];
            public fixed byte GdtBytes[40];
        }
#endif

        private unsafe struct
            M01StageThreePreparationCallContext
        {
            public ulong Signature;
            public ulong PlannerStackLowerBound;
            public ulong PlannerStackTop;
            public ulong ObservedStackAddress;
            public byte CompletionValue;
            public M01LiveAllocatorStaticState*
                StageTwoState;
            public OwnedBootStackSwitchPlanResult
                StackSwitchPlan;
            public BootMemoryRegionRecord*
                AllocationRecords;
            public M01LivePagingAllocationEvidence
                AllocationEvidence;
            public M01LivePagingMapLedgerEvidence
                MapEvidence;
            public OS.Kernel.Memory.BootReservedRange*
                ReservationLedger;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRequirement*
                    Requirements;
            public OS.Kernel.Boot
                .X64BootIdentityMappingRange* Ranges;
            public OS.Kernel.Memory.X64PageTableImagePage*
                PageImages;
            public ulong* PageEntries;
            public OS.Kernel.Memory
                .X64PageTableEncodingPreconditions
                    Preconditions;
            public byte* CodePage;
            public byte* ProbePage;
            public OS.Kernel.Memory
                .X64PagingModePreflightObservation*
                    PreflightObservation;
            public M01LivePagingPreparationState*
                PreparationState;
        }
    }
#endif

    private static EfiMemoryMapSnapshot s_memoryMapSnapshot;
    private static BootHandoffRecord s_bootHandoff;
    private static OwnedMemoryMapBuffer s_ownedMemoryMap;
    private static OwnedMemoryRegionBuffer s_ownedMemoryRegions;
    private static BootPageAllocatorNodeBuffer s_bootPageAllocatorNodes;
    private static EfiMemoryMapSummary s_memoryMapSummary;
    private static BootMemoryRegionMaterializationResult s_memoryRegionMaterialization;
    private static EfiBootPageAllocatorReadinessResult s_bootPageAllocatorReadiness;
    private static EfiConfigurationTableScanResult s_acpiRsdpScanResult;
    private static UefiMemoryMapCopyPlanStatus s_memoryMapCopyStatus;
#if ACPI_PHYSICAL_DISCOVERY_PROBE
    private static AcpiRsdpScratchBuffer s_acpiRsdpScratch;
    private static AcpiRootSdtBuffer s_acpiRootSdt;
    private static AcpiChildHeaderScratchBuffer s_acpiChildHeaderScratch;
    private static AcpiChildTableBuffer s_acpiChildTable;
    private static AcpiPointerScratchBuffer s_acpiPointerScratch;
    private static AcpiEcamSegmentScratchBuffer s_acpiEcamSegmentScratch;
#endif

#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
    private static bool PrepareM02OwnedGdtTssTerminalProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        ulong codeAddress = 0;
        ulong dataAddress = 0;
        bool codeAllocated = false;
        bool dataAllocated = false;
        EfiBootServices* bootServices = systemTable == null
            ? null
            : systemTable->BootServices;

        if (s_m02OwnedGdtTssTerminalState != 0 ||
            bootServices == null ||
            bootServices->AllocatePages == null ||
            bootServices->FreePages == null)
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong firmwareStatus = bootServices->AllocatePages(
            M02OwnedGdtTssTerminalAllocateAnyPages,
            M02OwnedGdtTssTerminalEfiLoaderCode,
            M02OwnedGdtTssTerminalPageCount,
            &codeAddress);
        codeAllocated = firmwareStatus == M02OwnedGdtTssTerminalEfiSuccess &&
            codeAddress != 0;
        if (!codeAllocated ||
            !IsValidM02OwnedGdtTssTerminalPage(codeAddress))
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        firmwareStatus = bootServices->AllocatePages(
            M02OwnedGdtTssTerminalAllocateAnyPages,
            M02OwnedGdtTssTerminalEfiLoaderData,
            M02OwnedGdtTssTerminalPageCount,
            &dataAddress);
        dataAllocated = firmwareStatus == M02OwnedGdtTssTerminalEfiSuccess &&
            dataAddress != 0;
        if (!dataAllocated ||
            !IsValidM02OwnedGdtTssTerminalPage(dataAddress) ||
            M02OwnedGdtTssTerminalPagesOverlap(codeAddress, dataAddress))
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        byte* codePage = (byte*)codeAddress;
        byte* dataPage = (byte*)dataAddress;
        for (ulong offset = 0;
            offset < M02OwnedGdtTssTerminalPageSize;
            offset++)
        {
            codePage[offset] = 0;
            dataPage[offset] = 0;
        }

        if (!IsM02OwnedGdtTssTerminalPageZero(codePage, 0) ||
            !IsM02OwnedGdtTssTerminalPageZero(dataPage, 0))
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext* context =
            (OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*)dataAddress;
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult prepared =
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .PrepareContext(
                    context,
                    dataAddress,
                    M02OwnedGdtTssTerminalPageSize);
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult emitted =
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .Emit(
                    codePage,
                    codeAddress,
                    M02OwnedGdtTssTerminalPageSize,
                    context,
                    dataAddress,
                    M02OwnedGdtTssTerminalPageSize);
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult validated =
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .ValidateReadBack(
                    codePage,
                    codeAddress,
                    M02OwnedGdtTssTerminalPageSize,
                    context,
                    dataAddress,
                    M02OwnedGdtTssTerminalPageSize);

        if (!IsExactM02OwnedGdtTssTerminalPreparation(
                prepared,
                emitted,
                validated) ||
            !IsM02OwnedGdtTssTerminalPageZero(
                codePage,
                (ulong)OS.Kernel.Diagnostics
                    .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                    .StubByteCount) ||
            !IsM02OwnedGdtTssTerminalPageZero(
                dataPage,
                (ulong)OS.Kernel.Diagnostics
                    .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                    .ContextByteCount))
        {
            return FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        s_m02OwnedGdtTssTerminalCodeAddress = codeAddress;
        s_m02OwnedGdtTssTerminalDataAddress = dataAddress;
        s_m02OwnedGdtTssTerminalState = M02OwnedGdtTssTerminalPreparedState;
        return true;
    }

    private static bool IsExactM02OwnedGdtTssTerminalPreparation(
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult prepared,
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult emitted,
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult validated)
    {
        return OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .ContextByteCount == 168 &&
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .StubByteCount == 2987 &&
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .StubFnv1A64 == 0xc9c0_4dee_967d_640aUL &&
            prepared.ContextPrepared &&
            prepared.ContextBytesWritten == 168 &&
            prepared.ContextBytesCompared == 168 &&
            prepared.StubBytesWritten == 0 &&
            prepared.StubBytesCompared == 0 &&
            prepared.FirstMismatchOffset == -1 &&
            prepared.ContextFingerprint != 0 &&
            emitted.StubEmitted &&
            emitted.ContextBytesWritten == 0 &&
            emitted.ContextBytesCompared == 168 &&
            emitted.StubBytesWritten == 2987 &&
            emitted.StubBytesCompared == 0 &&
            emitted.FirstMismatchOffset == -1 &&
            emitted.ContextFingerprint == prepared.ContextFingerprint &&
            emitted.StubFingerprint == 0xc9c0_4dee_967d_640aUL &&
            validated.ReadBackValidated &&
            validated.ContextBytesWritten == 0 &&
            validated.ContextBytesCompared == 168 &&
            validated.StubBytesWritten == 0 &&
            validated.StubBytesCompared == 2987 &&
            validated.FirstMismatchOffset == -1 &&
            validated.ContextFingerprint == prepared.ContextFingerprint &&
            validated.StubFingerprint == 0xc9c0_4dee_967d_640aUL;
    }

    private static bool ValidateM02OwnedGdtTssTerminalResources()
    {
        ulong codeAddress = s_m02OwnedGdtTssTerminalCodeAddress;
        ulong dataAddress = s_m02OwnedGdtTssTerminalDataAddress;
        if (s_m02OwnedGdtTssTerminalState !=
                M02OwnedGdtTssTerminalPreparedState ||
            !IsValidM02OwnedGdtTssTerminalPage(codeAddress) ||
            !IsValidM02OwnedGdtTssTerminalPage(dataAddress) ||
            M02OwnedGdtTssTerminalPagesOverlap(codeAddress, dataAddress))
        {
            return false;
        }

        byte* codePage = (byte*)codeAddress;
        byte* dataPage = (byte*)dataAddress;
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext* context =
            (OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*)dataAddress;
        OS.Kernel.Diagnostics.OwnedGdtTssTerminalStubResult validated =
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .ValidateReadBack(
                    codePage,
                    codeAddress,
                    M02OwnedGdtTssTerminalPageSize,
                    context,
                    dataAddress,
                    M02OwnedGdtTssTerminalPageSize);
        return validated.ReadBackValidated &&
            validated.ContextBytesCompared == 168 &&
            validated.StubBytesCompared == 2987 &&
            validated.ContextFingerprint != 0 &&
            validated.StubFingerprint == 0xc9c0_4dee_967d_640aUL &&
            IsM02OwnedGdtTssTerminalPageZero(codePage, 2987) &&
            IsM02OwnedGdtTssTerminalPageZero(dataPage, 168);
    }

    private static bool IsValidM02OwnedGdtTssTerminalPage(ulong address)
    {
        return address != 0 &&
            (address & (M02OwnedGdtTssTerminalPageSize - 1)) == 0 &&
            address <= ~0UL - M02OwnedGdtTssTerminalPageSize &&
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .IsCanonicalX64Address(address) &&
            OS.Kernel.Diagnostics
                .OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                .IsCanonicalX64Address(
                    address + M02OwnedGdtTssTerminalPageSize - 1);
    }

    private static bool M02OwnedGdtTssTerminalPagesOverlap(
        ulong codeAddress,
        ulong dataAddress)
    {
        ulong codeEnd = codeAddress + M02OwnedGdtTssTerminalPageSize;
        ulong dataEnd = dataAddress + M02OwnedGdtTssTerminalPageSize;
        return codeAddress < dataEnd && dataAddress < codeEnd;
    }

    private static bool IsM02OwnedGdtTssTerminalPageZero(
        byte* page,
        ulong startOffset)
    {
        if (page == null ||
            startOffset > M02OwnedGdtTssTerminalPageSize)
        {
            return false;
        }

        for (ulong offset = startOffset;
            offset < M02OwnedGdtTssTerminalPageSize;
            offset++)
        {
            if (page[offset] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void EnterM02OwnedGdtTssTerminalProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices)
    {
        if (!ValidateM02OwnedGdtTssTerminalResources() ||
            !M01StageThreeAdapter
                .TryWriteM02OwnedGdtTssTerminalStartLine() ||
            X64InstructionPublication.Serialize() !=
                X64InstructionPublication.ExpectedSuccessCookie)
        {
            _ = FailM02OwnedGdtTssTerminalBeforeEntry(
                console,
                serial,
                bootServices,
                s_m02OwnedGdtTssTerminalCodeAddress,
                s_m02OwnedGdtTssTerminalCodeAddress != 0,
                s_m02OwnedGdtTssTerminalDataAddress,
                s_m02OwnedGdtTssTerminalDataAddress != 0);
            SpinM02OwnedGdtTssTerminalProbe();
        }

        s_m02OwnedGdtTssTerminalState = M02OwnedGdtTssTerminalEnteredState;
        delegate* unmanaged<
            OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*,
            void> terminalEntry =
                (delegate* unmanaged<
                    OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*,
                    void>)s_m02OwnedGdtTssTerminalCodeAddress;
        terminalEntry(
            (OS.Kernel.Diagnostics.OwnedGdtTssTerminalContext*)
                s_m02OwnedGdtTssTerminalDataAddress);

        _ = M01StageThreeAdapter
            .TryWriteM02OwnedGdtTssTerminalUnexpectedReturnLine();
        SpinM02OwnedGdtTssTerminalProbe();
    }

    private static byte GetM02OwnedGdtTssTerminalCleanupPlan(
        bool codeAllocated,
        ulong codeAddress,
        bool dataAllocated,
        ulong dataAddress)
    {
        byte plan = 0;
        if (dataAllocated)
        {
            plan |= 1;
        }

        if (codeAllocated &&
            (!dataAllocated || codeAddress != dataAddress))
        {
            plan |= 2;
        }

        return plan;
    }

    private static bool FailM02OwnedGdtTssTerminalBeforeEntry(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices,
        ulong codeAddress,
        bool codeAllocated,
        ulong dataAddress,
        bool dataAllocated)
    {
        byte cleanupPlan = GetM02OwnedGdtTssTerminalCleanupPlan(
            codeAllocated,
            codeAddress,
            dataAllocated,
            dataAddress);
        if (bootServices != null && bootServices->FreePages != null)
        {
            if ((cleanupPlan & 1) != 0)
            {
                _ = bootServices->FreePages(
                    dataAddress,
                    M02OwnedGdtTssTerminalPageCount);
            }

            if ((cleanupPlan & 2) != 0)
            {
                _ = bootServices->FreePages(
                    codeAddress,
                    M02OwnedGdtTssTerminalPageCount);
            }
        }

        s_m02OwnedGdtTssTerminalCodeAddress = 0;
        s_m02OwnedGdtTssTerminalDataAddress = 0;
        s_m02OwnedGdtTssTerminalState = M02OwnedGdtTssTerminalFailedState;
        WriteM02OwnedGdtTssTerminalFirmwareLine(
            console,
            serial,
            M02OwnedGdtTssTerminalPreEntryFailureLine);
        return false;
    }

    private static void WriteM02OwnedGdtTssTerminalFirmwareLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        int lineKind)
    {
        int length = GetM02OwnedGdtTssTerminalLineLength(lineKind);
        for (int index = 0; index < length; index++)
        {
            if (!TryGetM02OwnedGdtTssTerminalLineByte(
                    lineKind,
                    index,
                    out byte value))
            {
                return;
            }

            Write(console, serial, value);
        }

        WriteCrLf(console, serial);
    }

    private static int GetM02OwnedGdtTssTerminalLineLength(int lineKind)
    {
        return lineKind switch
        {
            M02OwnedGdtTssTerminalStartLine => 18,
            M02OwnedGdtTssTerminalPreEntryFailureLine => 33,
            M02OwnedGdtTssTerminalUnexpectedReturnLine => 35,
            _ => 0
        };
    }

    private static bool TryGetM02OwnedGdtTssTerminalLineByte(
        int lineKind,
        int index,
        out byte value)
    {
        if (lineKind == M02OwnedGdtTssTerminalStartLine)
        {
            value = index switch
            {
                0 => (byte)'T', 1 => (byte)'E', 2 => (byte)'S',
                3 => (byte)'T', 4 => (byte)'_', 5 => (byte)'S',
                6 => (byte)'T', 7 => (byte)'A', 8 => (byte)'R',
                9 => (byte)'T', 10 => (byte)' ', 11 => (byte)'m',
                12 => (byte)'0', 13 => (byte)'2', 14 => (byte)'.',
                15 => (byte)'0', 16 => (byte)'0', 17 => (byte)'9',
                _ => 0
            };
            return index >= 0 && index < 18;
        }

        if (index >= 0 && index < 18)
        {
            value = index switch
            {
                0 => (byte)'M', 1 => (byte)'0', 2 => (byte)'2',
                3 => (byte)'_', 4 => (byte)'O', 5 => (byte)'W',
                6 => (byte)'N', 7 => (byte)'E', 8 => (byte)'D',
                9 => (byte)'_', 10 => (byte)'G', 11 => (byte)'D',
                12 => (byte)'T', 13 => (byte)'_', 14 => (byte)'T',
                15 => (byte)'S', 16 => (byte)'S', 17 => (byte)'_',
                _ => 0
            };
            return true;
        }

        if (lineKind == M02OwnedGdtTssTerminalPreEntryFailureLine)
        {
            value = index switch
            {
                18 => (byte)'P', 19 => (byte)'R', 20 => (byte)'E',
                21 => (byte)'E', 22 => (byte)'N', 23 => (byte)'T',
                24 => (byte)'R', 25 => (byte)'Y', 26 => (byte)'_',
                27 => (byte)'F', 28 => (byte)'A', 29 => (byte)'I',
                30 => (byte)'L', 31 => (byte)'E', 32 => (byte)'D',
                _ => 0
            };
            return index >= 18 && index < 33;
        }

        if (lineKind == M02OwnedGdtTssTerminalUnexpectedReturnLine)
        {
            value = index switch
            {
                18 => (byte)'U', 19 => (byte)'N', 20 => (byte)'E',
                21 => (byte)'X', 22 => (byte)'P', 23 => (byte)'E',
                24 => (byte)'C', 25 => (byte)'T', 26 => (byte)'E',
                27 => (byte)'D', 28 => (byte)'_', 29 => (byte)'R',
                30 => (byte)'E', 31 => (byte)'T', 32 => (byte)'U',
                33 => (byte)'R', 34 => (byte)'N',
                _ => 0
            };
            return index >= 18 && index < 35;
        }

        value = 0;
        return false;
    }

    private static void SpinM02OwnedGdtTssTerminalProbe()
    {
        while (true)
        {
        }
    }
#endif

#if M02_UD2_VECTOR_STATE_PROBE
    private static bool PrepareM02Ud2VectorStateProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        ulong codeAddress = 0;
        ulong dataAddress = 0;
        bool codeAllocated = false;
        bool dataAllocated = false;
        EfiBootServices* bootServices = systemTable == null
            ? null
            : systemTable->BootServices;

        if (s_m02Ud2VectorStateState != 0 ||
            bootServices == null ||
            bootServices->AllocatePages == null ||
            bootServices->FreePages == null)
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong firmwareStatus = bootServices->AllocatePages(
            M02Ud2VectorStateAllocateAnyPages,
            M02Ud2VectorStateEfiLoaderCode,
            M02Ud2VectorStateCodePageCount,
            &codeAddress);
        codeAllocated = firmwareStatus == M02Ud2VectorStateEfiSuccess &&
            codeAddress != 0;
        if (!codeAllocated ||
            !IsValidM02Ud2VectorStateRange(
                codeAddress,
                M02Ud2VectorStateCodeByteCount))
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        firmwareStatus = bootServices->AllocatePages(
            M02Ud2VectorStateAllocateAnyPages,
            M02Ud2VectorStateEfiLoaderData,
            M02Ud2VectorStateDataPageCount,
            &dataAddress);
        dataAllocated = firmwareStatus == M02Ud2VectorStateEfiSuccess &&
            dataAddress != 0;
        if (!dataAllocated ||
            !IsValidM02Ud2VectorStateRange(
                dataAddress,
                M02Ud2VectorStateDataByteCount) ||
            M02Ud2VectorStateRangesOverlap(
                codeAddress,
                M02Ud2VectorStateCodeByteCount,
                dataAddress,
                M02Ud2VectorStateDataByteCount))
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        byte* code = (byte*)codeAddress;
        byte* data = (byte*)dataAddress;
        for (ulong offset = 0;
            offset < M02Ud2VectorStateCodeByteCount;
            offset++)
        {
            code[offset] = 0;
        }
        for (ulong offset = 0;
            offset < M02Ud2VectorStateDataByteCount;
            offset++)
        {
            data[offset] = 0;
        }

        if (!IsM02Ud2VectorStateRangeZero(
                code,
                0,
                M02Ud2VectorStateCodeByteCount) ||
            !IsM02Ud2VectorStateRangeZero(
                data,
                0,
                M02Ud2VectorStateDataByteCount))
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong commonTargetAddress =
            codeAddress + M02Ud2VectorStatePageSize;
        ulong idtAddress = dataAddress;
        ulong contextAddress =
            dataAddress + M02Ud2VectorStatePageSize;
        ulong ist1StackTop =
            dataAddress + (3 * M02Ud2VectorStatePageSize);
        ulong ist2StackTop =
            dataAddress + (4 * M02Ud2VectorStatePageSize);
        ulong ist3StackTop =
            dataAddress + (5 * M02Ud2VectorStatePageSize);

        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedMaterialized =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .Materialize(
                        code,
                        M02Ud2VectorStatePageSize,
                        codeAddress,
                        commonTargetAddress);
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedReadBack =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        code,
                        M02Ud2VectorStatePageSize,
                        codeAddress,
                        commonTargetAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtMaterialized =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .Materialize(
                    data,
                    M02Ud2VectorStatePageSize,
                    idtAddress,
                    codeAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtReadBack =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .ValidateReadBack(
                    data,
                    M02Ud2VectorStatePageSize,
                    idtAddress,
                    codeAddress);

        byte* contextBytes = data + M02Ud2VectorStatePageSize;
        byte* stream = code + M02Ud2VectorStatePageSize;
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionResult
            contextPrepared =
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .PrepareContext(
                        contextBytes,
                        contextAddress,
                        M02Ud2VectorStatePageSize,
                        idtAddress,
                        ist1StackTop,
                        ist2StackTop,
                        ist3StackTop);
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionResult
            streamEmitted =
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .EmitStream(
                        stream,
                        commonTargetAddress,
                        M02Ud2VectorStatePageSize,
                        contextBytes,
                        contextAddress,
                        M02Ud2VectorStatePageSize,
                        idtAddress,
                        ist1StackTop,
                        ist2StackTop,
                        ist3StackTop);
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionResult
            streamReadBack =
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .ValidateStreamReadBack(
                        stream,
                        commonTargetAddress,
                        M02Ud2VectorStatePageSize,
                        contextBytes,
                        contextAddress,
                        M02Ud2VectorStatePageSize,
                        idtAddress,
                        ist1StackTop,
                        ist2StackTop,
                        ist3StackTop);
        OS.Kernel.Diagnostics.OwnedX64GdtVerificationResult gdtReadBack =
            OS.Kernel.Diagnostics
                .OwnedX64GdtContainingNullN64BitCode
                .Verify(
                    contextBytes + 24,
                    40,
                    contextAddress + 64,
                    103);

        if (!M02Ud2VectorStateNormalizedResultMatches(
                normalizedMaterialized,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .Materialize,
                codeAddress,
                commonTargetAddress,
                true) ||
            !M02Ud2VectorStateNormalizedResultMatches(
                normalizedReadBack,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .ValidateReadBack,
                codeAddress,
                commonTargetAddress,
                false) ||
            normalizedMaterialized.Fingerprint !=
                normalizedReadBack.Fingerprint ||
            !M02Ud2VectorStateIdtResultMatches(
                idtMaterialized,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation
                    .Materialize,
                idtAddress,
                codeAddress,
                true) ||
            !M02Ud2VectorStateIdtResultMatches(
                idtReadBack,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation
                    .ValidateReadBack,
                idtAddress,
                codeAddress,
                false) ||
            idtMaterialized.Fingerprint != idtReadBack.Fingerprint ||
            !M02Ud2VectorStateTransitionResultMatches(
                contextPrepared,
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionOperation
                    .PrepareContext,
                contextAddress,
                0,
                idtAddress,
                true) ||
            !M02Ud2VectorStateTransitionResultMatches(
                streamEmitted,
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionOperation
                    .EmitStream,
                contextAddress,
                commonTargetAddress,
                idtAddress,
                true) ||
            !M02Ud2VectorStateTransitionResultMatches(
                streamReadBack,
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack,
                contextAddress,
                commonTargetAddress,
                idtAddress,
                false) ||
            contextPrepared.ContextFingerprint !=
                streamEmitted.ContextFingerprint ||
            contextPrepared.ContextFingerprint !=
                streamReadBack.ContextFingerprint ||
            streamEmitted.StreamFingerprint !=
                streamReadBack.StreamFingerprint ||
            !M02Ud2VectorStateGdtResultMatches(gdtReadBack) ||
            !IsM02Ud2VectorStateRangeZero(
                contextBytes,
                (ulong)OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .ContextByteCount,
                M02Ud2VectorStatePageSize) ||
            !AreM02Ud2VectorStateIstPagesZero(data))
        {
            return FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        s_m02Ud2VectorStateCodeAddress = codeAddress;
        s_m02Ud2VectorStateDataAddress = dataAddress;
        s_m02Ud2VectorStateState = M02Ud2VectorStatePreparedState;
        return true;
    }

    private static bool ValidateM02Ud2VectorStateResources()
    {
        ulong codeAddress = s_m02Ud2VectorStateCodeAddress;
        ulong dataAddress = s_m02Ud2VectorStateDataAddress;
        if (s_m02Ud2VectorStateState !=
                M02Ud2VectorStatePreparedState ||
            !IsValidM02Ud2VectorStateRange(
                codeAddress,
                M02Ud2VectorStateCodeByteCount) ||
            !IsValidM02Ud2VectorStateRange(
                dataAddress,
                M02Ud2VectorStateDataByteCount) ||
            M02Ud2VectorStateRangesOverlap(
                codeAddress,
                M02Ud2VectorStateCodeByteCount,
                dataAddress,
                M02Ud2VectorStateDataByteCount))
        {
            return false;
        }

        byte* code = (byte*)codeAddress;
        byte* data = (byte*)dataAddress;
        byte* stream = code + M02Ud2VectorStatePageSize;
        byte* contextBytes = data + M02Ud2VectorStatePageSize;
        ulong commonTargetAddress =
            codeAddress + M02Ud2VectorStatePageSize;
        ulong idtAddress = dataAddress;
        ulong contextAddress =
            dataAddress + M02Ud2VectorStatePageSize;
        ulong ist1StackTop =
            dataAddress + (3 * M02Ud2VectorStatePageSize);
        ulong ist2StackTop =
            dataAddress + (4 * M02Ud2VectorStatePageSize);
        ulong ist3StackTop =
            dataAddress + (5 * M02Ud2VectorStatePageSize);

        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedReadBack =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        code,
                        M02Ud2VectorStatePageSize,
                        codeAddress,
                        commonTargetAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtReadBack =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .ValidateReadBack(
                    data,
                    M02Ud2VectorStatePageSize,
                    idtAddress,
                    codeAddress);
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionResult
            streamReadBack =
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .ValidateStreamReadBack(
                        stream,
                        commonTargetAddress,
                        M02Ud2VectorStatePageSize,
                        contextBytes,
                        contextAddress,
                        M02Ud2VectorStatePageSize,
                        idtAddress,
                        ist1StackTop,
                        ist2StackTop,
                        ist3StackTop);
        OS.Kernel.Diagnostics.OwnedX64GdtVerificationResult gdtReadBack =
            OS.Kernel.Diagnostics
                .OwnedX64GdtContainingNullN64BitCode
                .Verify(
                    contextBytes + 24,
                    40,
                    contextAddress + 64,
                    103);

        return M02Ud2VectorStateNormalizedResultMatches(
                normalizedReadBack,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .ValidateReadBack,
                codeAddress,
                commonTargetAddress,
                false) &&
            M02Ud2VectorStateIdtResultMatches(
                idtReadBack,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation
                    .ValidateReadBack,
                idtAddress,
                codeAddress,
                false) &&
            M02Ud2VectorStateTransitionResultMatches(
                streamReadBack,
                OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack,
                contextAddress,
                commonTargetAddress,
                idtAddress,
                false) &&
            M02Ud2VectorStateGdtResultMatches(gdtReadBack) &&
            IsM02Ud2VectorStateRangeZero(
                contextBytes,
                (ulong)OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransition
                    .ContextByteCount,
                M02Ud2VectorStatePageSize) &&
            AreM02Ud2VectorStateIstPagesZero(data);
    }

    private static bool M02Ud2VectorStateNormalizedResultMatches(
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult result,
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation operation,
        ulong pageAddress,
        ulong commonTargetAddress,
        bool destinationMutated)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageStatus
                    .Success &&
            result.ModeledPageAddress == pageAddress &&
            result.CommonTargetAddress == commonTargetAddress &&
            result.PageCapacity == M02Ud2VectorStatePageSize &&
            result.RequiredBytes == (int)M02Ud2VectorStatePageSize &&
            result.BytesWritten ==
                (destinationMutated
                    ? (int)M02Ud2VectorStatePageSize
                    : 0) &&
            result.BytesCompared == (int)M02Ud2VectorStatePageSize &&
            result.FirstMismatchOffset == -1 &&
            result.HardwareErrorCodeVectorCount == 9 &&
            result.SyntheticErrorCodeVectorCount == 247 &&
            result.Fingerprint != 0 &&
            result.DestinationMutated == destinationMutated &&
            result.ReadBackPerformed &&
            (destinationMutated
                ? result.Materialized
                : result.ReadBackValidated) &&
            result.Succeeded;
    }

    private static bool M02Ud2VectorStateIdtResultMatches(
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult result,
        OS.Kernel.Diagnostics.OwnedIdtGateTableOperation operation,
        ulong tableAddress,
        ulong stubPageAddress,
        bool destinationMutated)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics.OwnedIdtGateTableStatus.Success &&
            result.ModeledTableAddress == tableAddress &&
            result.OwnedStubPageAddress == stubPageAddress &&
            result.TableCapacity == M02Ud2VectorStatePageSize &&
            result.RequiredBytes == (int)M02Ud2VectorStatePageSize &&
            result.DescriptorCount == 256 &&
            result.PresentDescriptorCount == 256 &&
            result.IstRoutedDescriptorCount == 3 &&
            result.DefaultIstDescriptorCount == 253 &&
            result.FirmwareGateCopyCount == 0 &&
            result.BytesWritten ==
                (destinationMutated
                    ? (int)M02Ud2VectorStatePageSize
                    : 0) &&
            result.BytesCompared == (int)M02Ud2VectorStatePageSize &&
            result.FirstMismatchOffset == -1 &&
            result.Fingerprint != 0 &&
            result.NormalizedPolicyFingerprint ==
                OS.Kernel.Diagnostics
                    .PopulateAllN256IDTVectorsOwnedGatesNever
                    .NormalizedPolicyFnv1A64 &&
            result.DestinationMutated == destinationMutated &&
            result.ReadBackPerformed &&
            (destinationMutated
                ? result.Materialized
                : result.ReadBackValidated) &&
            result.Succeeded;
    }

    private static bool M02Ud2VectorStateTransitionResultMatches(
        OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransitionResult result,
        OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransitionOperation operation,
        ulong contextAddress,
        ulong streamAddress,
        ulong idtAddress,
        bool destinationMutated)
    {
        int contextBytes = OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransition.ContextByteCount;
        int streamBytes = OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransition.StreamByteCount;
        bool preparing = operation == OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransitionOperation
            .PrepareContext;
        bool emitting = operation == OS.Kernel.Diagnostics
            .DisabledDefaultUd2VectorStateTransitionOperation.EmitStream;

        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status == OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStateTransitionStatus.Success &&
            result.ModeledContextAddress == contextAddress &&
            result.ModeledStreamAddress == streamAddress &&
            result.OwnedIdtAddress == idtAddress &&
            result.ContextCapacity == M02Ud2VectorStatePageSize &&
            result.StreamCapacity ==
                (preparing ? 0 : M02Ud2VectorStatePageSize) &&
            result.ContextBytesWritten ==
                (preparing ? contextBytes : 0) &&
            result.ContextBytesCompared == contextBytes &&
            result.StreamBytesWritten == (emitting ? streamBytes : 0) &&
            result.StreamBytesCompared ==
                (preparing ? 0 : streamBytes) &&
            result.FirstMismatchOffset == -1 &&
            result.ContextFingerprint != 0 &&
            result.StreamFingerprint ==
                (preparing ? 0 : result.ExpectedStreamFingerprint) &&
            result.DestinationMutated == destinationMutated &&
            result.ReadBackPerformed &&
            (preparing
                ? result.ContextPrepared
                : emitting
                    ? result.StreamEmitted
                    : result.ReadBackValidated) &&
            result.Succeeded;
    }

    private static bool M02Ud2VectorStateGdtResultMatches(
        OS.Kernel.Diagnostics.OwnedX64GdtVerificationResult result)
    {
        return result.IsInitialized &&
            result.Status == OS.Kernel.Diagnostics
                .OwnedX64GdtVerificationStatus.Success &&
            result.BytesCompared == 40 &&
            result.FirstMismatchOffset == -1 &&
            result.ObservedFingerprint != 0 &&
            result.ObservedFingerprint == result.ExpectedFingerprint &&
            result.Succeeded;
    }

    private static bool IsValidM02Ud2VectorStateRange(
        ulong address,
        ulong byteCount)
    {
        return address != 0 &&
            byteCount != 0 &&
            (address & (M02Ud2VectorStatePageSize - 1)) == 0 &&
            address <= ~0UL - byteCount &&
            OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStateTransition
                .IsCanonicalX64Address(address) &&
            OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStateTransition
                .IsCanonicalX64Address(address + byteCount - 1);
    }

    private static bool M02Ud2VectorStateRangesOverlap(
        ulong firstAddress,
        ulong firstByteCount,
        ulong secondAddress,
        ulong secondByteCount)
    {
        ulong firstEnd = firstAddress + firstByteCount;
        ulong secondEnd = secondAddress + secondByteCount;
        return firstAddress < secondEnd && secondAddress < firstEnd;
    }

    private static bool IsM02Ud2VectorStateRangeZero(
        byte* source,
        ulong startOffset,
        ulong endOffset)
    {
        if (source == null || startOffset > endOffset)
        {
            return false;
        }

        for (ulong offset = startOffset; offset < endOffset; offset++)
        {
            if (source[offset] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreM02Ud2VectorStateIstPagesZero(byte* data)
    {
        return IsM02Ud2VectorStateRangeZero(
                data,
                2 * M02Ud2VectorStatePageSize,
                3 * M02Ud2VectorStatePageSize) &&
            IsM02Ud2VectorStateRangeZero(
                data,
                3 * M02Ud2VectorStatePageSize,
                4 * M02Ud2VectorStatePageSize) &&
            IsM02Ud2VectorStateRangeZero(
                data,
                4 * M02Ud2VectorStatePageSize,
                5 * M02Ud2VectorStatePageSize);
    }

    private static void EnterM02Ud2VectorStateProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices)
    {
        if (!ValidateM02Ud2VectorStateResources() ||
            !M01StageThreeAdapter.TryWriteM02Ud2VectorStateStartLine())
        {
            _ = FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                s_m02Ud2VectorStateCodeAddress,
                s_m02Ud2VectorStateCodeAddress != 0,
                s_m02Ud2VectorStateDataAddress,
                s_m02Ud2VectorStateDataAddress != 0);
            SpinM02Ud2VectorStateProbe();
        }

        delegate* unmanaged<
            OS.Kernel.Diagnostics
                .DisabledDefaultUd2VectorStateTransitionContext*,
            void> terminalEntry =
                (delegate* unmanaged<
                    OS.Kernel.Diagnostics
                        .DisabledDefaultUd2VectorStateTransitionContext*,
                    void>)(s_m02Ud2VectorStateCodeAddress +
                        (ulong)OS.Kernel.Diagnostics
                            .DisabledDefaultUd2VectorStateTransition
                            .TransitionEntryCompositeOffset);
        OS.Kernel.Diagnostics.DisabledDefaultUd2VectorStateTransitionContext*
            context =
                (OS.Kernel.Diagnostics
                    .DisabledDefaultUd2VectorStateTransitionContext*)
                    (s_m02Ud2VectorStateDataAddress +
                        M02Ud2VectorStatePageSize);

        s_m02Ud2VectorStateState = M02Ud2VectorStateEnteredState;
        if (X64InstructionPublication.Serialize() !=
            X64InstructionPublication.ExpectedSuccessCookie)
        {
            _ = FailM02Ud2VectorStateBeforeEntry(
                console,
                serial,
                bootServices,
                s_m02Ud2VectorStateCodeAddress,
                s_m02Ud2VectorStateCodeAddress != 0,
                s_m02Ud2VectorStateDataAddress,
                s_m02Ud2VectorStateDataAddress != 0);
            SpinM02Ud2VectorStateProbe();
        }
        terminalEntry(context);

        SpinM02Ud2VectorStateProbe();
    }

    private static byte GetM02Ud2VectorStateCleanupPlan(
        bool codeAllocated,
        ulong codeAddress,
        bool dataAllocated,
        ulong dataAddress)
    {
        byte plan = 0;
        if (dataAllocated && dataAddress != 0)
        {
            plan |= 1;
        }

        if (codeAllocated &&
            codeAddress != 0 &&
            (!dataAllocated ||
                dataAddress == 0 ||
                M02Ud2VectorStateAllocationRangesDefinitelyDisjoint(
                    codeAddress,
                    dataAddress)))
        {
            plan |= 2;
        }

        return plan;
    }

    private static bool
        M02Ud2VectorStateAllocationRangesDefinitelyDisjoint(
            ulong codeAddress,
            ulong dataAddress)
    {
        if (codeAddress > ~0UL - M02Ud2VectorStateCodeByteCount ||
            dataAddress > ~0UL - M02Ud2VectorStateDataByteCount)
        {
            return false;
        }

        ulong codeEnd =
            codeAddress + M02Ud2VectorStateCodeByteCount;
        ulong dataEnd =
            dataAddress + M02Ud2VectorStateDataByteCount;
        return codeEnd <= dataAddress || dataEnd <= codeAddress;
    }

    private static bool FailM02Ud2VectorStateBeforeEntry(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices,
        ulong codeAddress,
        bool codeAllocated,
        ulong dataAddress,
        bool dataAllocated)
    {
        byte cleanupPlan = GetM02Ud2VectorStateCleanupPlan(
            codeAllocated,
            codeAddress,
            dataAllocated,
            dataAddress);
        if (bootServices != null && bootServices->FreePages != null)
        {
            if ((cleanupPlan & 1) != 0)
            {
                _ = bootServices->FreePages(
                    dataAddress,
                    M02Ud2VectorStateDataPageCount);
            }

            if ((cleanupPlan & 2) != 0)
            {
                _ = bootServices->FreePages(
                    codeAddress,
                    M02Ud2VectorStateCodePageCount);
            }
        }

        s_m02Ud2VectorStateCodeAddress = 0;
        s_m02Ud2VectorStateDataAddress = 0;
        s_m02Ud2VectorStateState = M02Ud2VectorStateFailedState;
        WriteM02Ud2VectorStateFirmwareFailureLines(console, serial);
        return false;
    }

    private static void WriteM02Ud2VectorStateFirmwareFailureLines(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02Ud2VectorStateFirmwareFailureLine(
            console,
            serial,
            M02Ud2VectorStateFailureHeadingLine);
        WriteM02Ud2VectorStateFirmwareFailureLine(
            console,
            serial,
            M02Ud2VectorStateFailureDetailLine);
    }

    private static void WriteM02Ud2VectorStateFirmwareFailureLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        int lineKind)
    {
        int length = lineKind == M02Ud2VectorStateFailureHeadingLine
            ? 34
            : lineKind == M02Ud2VectorStateFailureDetailLine
                ? 45
                : 0;
        for (int index = 0; index < length; index++)
        {
            if (!TryGetM02Ud2VectorStateFailureLineByte(
                    lineKind,
                    index,
                    out byte value))
            {
                return;
            }

            Write(console, serial, value);
        }

        WriteCrLf(console, serial);
    }

    private static bool TryGetM02Ud2VectorStateFailureLineByte(
        int lineKind,
        int index,
        out byte value)
    {
        int length = lineKind == M02Ud2VectorStateFailureHeadingLine
            ? 34
            : lineKind == M02Ud2VectorStateFailureDetailLine
                ? 45
                : 0;
        if (index < 0 || index >= length)
        {
            value = 0;
            return false;
        }

        int wordIndex = index >> 3;
        ulong word;
        if (lineKind == M02Ud2VectorStateFailureHeadingLine)
        {
            word = wordIndex switch
            {
                0 => 0x5f32_4455_5f32_304dUL,
                1 => 0x535f_524f_5443_4556UL,
                2 => 0x5245_565f_4554_4154UL,
                3 => 0x4c49_4146_5f59_4649UL,
                4 => 0x0000_0000_0000_4445UL,
                _ => 0
            };
        }
        else
        {
            word = wordIndex switch
            {
                0 => 0x4941_465f_5453_4554UL,
                1 => 0x3130_2e32_306d_204cUL,
                2 => 0x726f_7463_6576_2033UL,
                3 => 0x6967_6572_5f72_6f5fUL,
                4 => 0x7369_6d5f_7265_7473UL,
                5 => 0x0000_0068_6374_616dUL,
                _ => 0
            };
        }

        value = (byte)(word >> ((index & 7) * 8));
        return true;
    }

    internal static bool TryGetM02Ud2VectorStateStartLineByte(
        int index,
        out byte value)
    {
        value = index switch
        {
            0 => (byte)'T', 1 => (byte)'E', 2 => (byte)'S',
            3 => (byte)'T', 4 => (byte)'_', 5 => (byte)'S',
            6 => (byte)'T', 7 => (byte)'A', 8 => (byte)'R',
            9 => (byte)'T', 10 => (byte)' ', 11 => (byte)'m',
            12 => (byte)'0', 13 => (byte)'2', 14 => (byte)'.',
            15 => (byte)'0', 16 => (byte)'1', 17 => (byte)'3',
            _ => 0
        };
        return index >= 0 && index < 18;
    }

    private static void SpinM02Ud2VectorStateProbe()
    {
        while (true)
        {
        }
    }
#endif

    [System.Runtime.RuntimeExport("EfiMain")]
    private static long EfiMain(void* imageHandle, EfiSystemTable* systemTable)
    {
        EfiSimpleTextOutputProtocol* console = GetFirmwareConsole(systemTable);
        EfiSerialIoProtocol* serial = console == null ? GetFirmwareSerial(systemTable) : null;
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
        if (!PrepareM02OwnedGdtTssTerminalProbe(
                console,
                serial,
                systemTable))
        {
            return 0;
        }
#endif
#if M02_UD2_VECTOR_STATE_PROBE
        if (!PrepareM02Ud2VectorStateProbe(
                console,
                serial,
                systemTable))
        {
            return 0;
        }
#endif
        WriteBootMarkers(imageHandle, console, serial, systemTable);

#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
        _ = FailM02OwnedGdtTssTerminalBeforeEntry(
            console,
            serial,
            systemTable == null ? null : systemTable->BootServices,
            s_m02OwnedGdtTssTerminalCodeAddress,
            s_m02OwnedGdtTssTerminalCodeAddress != 0,
            s_m02OwnedGdtTssTerminalDataAddress,
            s_m02OwnedGdtTssTerminalDataAddress != 0);
#endif
#if M02_UD2_VECTOR_STATE_PROBE
        _ = FailM02Ud2VectorStateBeforeEntry(
            console,
            serial,
            systemTable == null ? null : systemTable->BootServices,
            s_m02Ud2VectorStateCodeAddress,
            s_m02Ud2VectorStateCodeAddress != 0,
            s_m02Ud2VectorStateDataAddress,
            s_m02Ud2VectorStateDataAddress != 0);
#endif

        return 0;
    }

    private static void Main()
    {
    }

    private static EfiSimpleTextOutputProtocol* GetFirmwareConsole(EfiSystemTable* systemTable)
    {
        if (systemTable == null || systemTable->ConOut == null || systemTable->ConOut->OutputString == null)
        {
            return null;
        }

        return systemTable->ConOut;
    }

    private static EfiSerialIoProtocol* GetFirmwareSerial(EfiSystemTable* systemTable)
    {
        if (systemTable == null || systemTable->BootServices == null || systemTable->BootServices->LocateProtocol == null)
        {
            return null;
        }

        EfiGuid serialIoGuid;
        serialIoGuid.Data1 = 0xbb25cf6f;
        serialIoGuid.Data2 = 0xf1d4;
        serialIoGuid.Data3 = 0x11d2;
        serialIoGuid.Data4_0 = 0x9a;
        serialIoGuid.Data4_1 = 0x0c;
        serialIoGuid.Data4_2 = 0x00;
        serialIoGuid.Data4_3 = 0x90;
        serialIoGuid.Data4_4 = 0x27;
        serialIoGuid.Data4_5 = 0x3f;
        serialIoGuid.Data4_6 = 0xc1;
        serialIoGuid.Data4_7 = 0xfd;

        void* serialInterface = null;
        ulong status = systemTable->BootServices->LocateProtocol(&serialIoGuid, null, &serialInterface);
        if (status != 0 || serialInterface == null)
        {
            return null;
        }

        EfiSerialIoProtocol* serial = (EfiSerialIoProtocol*)serialInterface;
        if (serial->Write == null)
        {
            return null;
        }

        return serial;
    }

    private static void WriteBootMarkers(
        void* imageHandle,
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteEfiMainStart(console, serial);
        WriteEfiConsoleReady(console, serial);
#if M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE
        if (!WriteM04IndependentEndianConformanceProbe(console, serial))
        {
            return;
        }
#endif
#if M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE
        if (!WriteM04SpanMemoryConformanceProbe(console, serial))
        {
            return;
        }
#endif
#if M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE
        if (!WriteM02NormalizedInterruptFramePageProbe(
                console,
                serial,
                systemTable))
        {
            return;
        }
#endif
#if M02_OWNED_IDT_GATE_POPULATION_PROBE
        if (!WriteM02OwnedIdtGatePopulationProbe(
                console,
                serial,
                systemTable))
        {
            return;
        }
#endif
#if M02_IDTR_CONTINUED_EXECUTION_PROBE
        if (!WriteM02IdtrContinuedExecutionProbe(
                console,
                serial,
                systemTable))
        {
            return;
        }
#endif
        if (TryCaptureUefiMemoryMap(systemTable))
        {
            WriteUefiMemoryMapReady(console, serial);
            WriteUefiMemoryMapDescriptorCount(console, serial, s_memoryMapSnapshot.DescriptorCount);
            if (s_memoryMapSnapshot.IsOwned)
            {
                WriteUefiMemoryMapOwned(console, serial);
                EfiBootHandoffStatus handoffStatus = EfiBootHandoffValidator.Validate(s_bootHandoff);
                if (handoffStatus != EfiBootHandoffStatus.Success)
                {
                    WriteBootHandoffFail(console, serial, handoffStatus);
                    return;
                }

                WriteBootHandoffReady(console, serial);

                EfiMemoryMapNormalizeResult normalizeResult = EfiMemoryMapNormalizer.Normalize(
                    s_memoryMapSnapshot.Buffer,
                    s_memoryMapSnapshot.MapSize,
                    s_memoryMapSnapshot.DescriptorSize,
                    s_memoryMapSnapshot.DescriptorVersion,
                    s_memoryMapSnapshot.DescriptorCount);
                if (!normalizeResult.Succeeded)
                {
                    WriteUefiMemoryMapNormalizeFail(console, serial, normalizeResult.Status);
                    return;
                }

                s_memoryMapSummary = normalizeResult.Summary;
                WriteUefiMemoryMapNormalized(console, serial);
                BootMemoryMapAcceptanceResult memoryMapAcceptance =
                    BootMemoryMapAcceptance.Validate(s_memoryMapSummary);
                if (!memoryMapAcceptance.Succeeded)
                {
                    WriteBootMemoryMapRejected(console, serial, memoryMapAcceptance.Status);
                    return;
                }

                WriteBootMemoryMapAccepted(console, serial);
                if (!MaterializeAndWriteBootMemoryRegions(console, serial))
                {
                    return;
                }

                if (!ScanAndWriteAcpiRsdp(console, serial, systemTable))
                {
                    return;
                }

#if ACPI_PHYSICAL_DISCOVERY_PROBE
                if (!WriteAcpiPhysicalDiscoveryProbe(console, serial))
                {
                    return;
                }
#endif

#if EXIT_BOOT_SERVICES_FRESH_MAP_PROBE
                if (!WriteExitBootServicesFreshMapProbe(console, serial, systemTable))
                {
                    return;
                }
#endif

                if (M01StageOneAdapter.Run(
                        imageHandle,
                        console,
                        serial,
                        systemTable))
                {
                    return;
                }
            }
        }
        else
        {
            WriteUefiMemoryMapFail(console, serial);
            if (s_memoryMapCopyStatus != UefiMemoryMapCopyPlanStatus.Success)
            {
                WriteUefiMemoryMapCopyFail(console, serial, s_memoryMapCopyStatus);
            }
        }

#if UEFI_GONE_SIMULATION_PROBE
        WriteUefiGoneSimulation(console, serial);
#endif
        WriteBootSequencePlan(console, serial);
    }

    private static void WriteEfiMainStart(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, 84);
        Write(console, serial, 69);
        Write(console, serial, 83);
        Write(console, serial, 84);
        Write(console, serial, 95);
        Write(console, serial, 83);
        Write(console, serial, 84);
        Write(console, serial, 65);
        Write(console, serial, 82);
        Write(console, serial, 84);
        Write(console, serial, 32);
        Write(console, serial, 101);
        Write(console, serial, 102);
        Write(console, serial, 105);
        Write(console, serial, 46);
        Write(console, serial, 109);
        Write(console, serial, 97);
        Write(console, serial, 105);
        Write(console, serial, 110);
        WriteCrLf(console, serial);
    }

    private static void WriteEfiConsoleReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, 69);
        Write(console, serial, 70);
        Write(console, serial, 73);
        Write(console, serial, 95);
        Write(console, serial, 67);
        Write(console, serial, 79);
        Write(console, serial, 78);
        Write(console, serial, 83);
        Write(console, serial, 79);
        Write(console, serial, 76);
        Write(console, serial, 69);
        Write(console, serial, 95);
        Write(console, serial, 82);
        Write(console, serial, 69);
        Write(console, serial, 65);
        Write(console, serial, 68);
        Write(console, serial, 89);
        WriteCrLf(console, serial);
    }

#if M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE
    private static bool WriteM04IndependentEndianConformanceProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        OS.Std.Binary.IndependentEndianReadVector* readVectors =
            stackalloc OS.Std.Binary.IndependentEndianReadVector[
                OS.Std.Binary
                    .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .ReadVectorCount];
        OS.Std.Binary.IndependentEndianReverseVector* reverseVectors =
            stackalloc OS.Std.Binary.IndependentEndianReverseVector[
                OS.Std.Binary
                    .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .ReverseVectorCount];

        OS.Std.Binary.IndependentEndianConformanceResult copyResult =
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .CopyBuiltInCatalog(
                    readVectors,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ReadVectorCount,
                    reverseVectors,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ReverseVectorCount);
        OS.Std.Binary.IndependentEndianConformanceResult validationResult =
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ValidateCatalog(
                    readVectors,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ReadVectorCount,
                    reverseVectors,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ReverseVectorCount,
                    OS.Std.Binary
                        .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ExpectedCatalogFingerprint);

        if (!copyResult.Succeeded ||
            !validationResult.Succeeded ||
            copyResult.ReadVectorCount != 25 ||
            copyResult.ReverseVectorCount != 7 ||
            validationResult.ReadVectorCount != 25 ||
            validationResult.ReverseVectorCount != 7 ||
            validationResult.ActualFingerprint != 0xE81E4451E8D999F6UL ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ByteSeriesCount != 5 ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .UInt16SeriesCount != 7 ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ReferenceFilesRequired ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .RequiresHostConformance ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .AddsCanonicalSystemNamespace ||
            OS.Std.Binary
                .IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .SatisfiesRuntimeCompletionGate4 ||
            validationResult.SatisfiesRuntimeCompletionGate4)
        {
            WriteM04IndependentEndianConformanceFailureLine(console, serial);
            return false;
        }

        WriteM04IndependentEndianConformanceSuccessLine(console, serial);
        return true;
    }

    private static void WriteM04IndependentEndianConformanceSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04Ascii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM04Ascii(console, serial, (byte)'4', (byte)'_', (byte)'0', (byte)'0', (byte)'2', (byte)'_', (byte)'R', (byte)'E');
        WriteM04Ascii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'r', (byte)'e', (byte)'a', (byte)'d');
        WriteM04Ascii(console, serial, (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'s', (byte)'=');
        WriteM04Ascii(console, serial, (byte)'2', (byte)'5', (byte)' ', (byte)'r', (byte)'e', (byte)'v', (byte)'e', (byte)'r');
        WriteM04Ascii(console, serial, (byte)'s', (byte)'e', (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r');
        WriteM04Ascii(console, serial, (byte)'s', (byte)'=', (byte)'7', (byte)' ', (byte)'b', (byte)'y', (byte)'t', (byte)'e');
        WriteM04Ascii(console, serial, (byte)'S', (byte)'e', (byte)'r', (byte)'i', (byte)'e', (byte)'s', (byte)'=', (byte)'5');
        WriteM04Ascii(console, serial, (byte)' ', (byte)'u', (byte)'i', (byte)'n', (byte)'t', (byte)'1', (byte)'6', (byte)'S');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'r', (byte)'i', (byte)'e', (byte)'s', (byte)'=', (byte)'7', (byte)' ');
        WriteM04Ascii(console, serial, (byte)'f', (byte)'i', (byte)'n', (byte)'g', (byte)'e', (byte)'r', (byte)'p', (byte)'r');
        WriteM04Ascii(console, serial, (byte)'i', (byte)'n', (byte)'t', (byte)'=', (byte)'E', (byte)'8', (byte)'1', (byte)'E');
        WriteM04Ascii(console, serial, (byte)'4', (byte)'4', (byte)'5', (byte)'1', (byte)'E', (byte)'8', (byte)'D', (byte)'9');
        WriteM04Ascii(console, serial, (byte)'9', (byte)'9', (byte)'F', (byte)'6', (byte)' ', (byte)'r', (byte)'e', (byte)'f');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'r', (byte)'e', (byte)'n', (byte)'c', (byte)'e', (byte)'F', (byte)'i');
        WriteM04Ascii(console, serial, (byte)'l', (byte)'e', (byte)'s', (byte)'R', (byte)'e', (byte)'q', (byte)'u', (byte)'i');
        WriteM04Ascii(console, serial, (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s');
        WriteM04Ascii(console, serial, (byte)'e', (byte)' ', (byte)'h', (byte)'o', (byte)'s', (byte)'t', (byte)'C', (byte)'o');
        WriteM04Ascii(console, serial, (byte)'n', (byte)'f', (byte)'o', (byte)'r', (byte)'m', (byte)'a', (byte)'n', (byte)'c');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM04Ascii(console, serial, (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'I', (byte)'n', (byte)'t');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'g', (byte)'r', (byte)'a', (byte)'t', (byte)'e', (byte)'d', (byte)'=');
        WriteM04Ascii(console, serial, (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'a', (byte)'t');
        WriteM04Ascii(console, serial, (byte)'e', (byte)'4', (byte)'=', (byte)'F', (byte)'a', (byte)'i', (byte)'l', 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04IndependentEndianConformanceFailureLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04Ascii(console, serial, (byte)'M', (byte)'0', (byte)'4', (byte)'_', (byte)'E', (byte)'N', (byte)'D', (byte)'I');
        WriteM04Ascii(console, serial, (byte)'A', (byte)'N', (byte)'_', (byte)'C', (byte)'O', (byte)'N', (byte)'F', (byte)'O');
        WriteM04Ascii(console, serial, (byte)'R', (byte)'M', (byte)'A', (byte)'N', (byte)'C', (byte)'E', (byte)'_', (byte)'F');
        WriteM04Ascii(console, serial, (byte)'A', (byte)'I', (byte)'L', (byte)'E', (byte)'D', 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04Ascii(
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
        if (b0 != 0) Write(console, serial, b0);
        if (b1 != 0) Write(console, serial, b1);
        if (b2 != 0) Write(console, serial, b2);
        if (b3 != 0) Write(console, serial, b3);
        if (b4 != 0) Write(console, serial, b4);
        if (b5 != 0) Write(console, serial, b5);
        if (b6 != 0) Write(console, serial, b6);
        if (b7 != 0) Write(console, serial, b7);
    }
#endif

#if M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE
    private static bool WriteM04SpanMemoryConformanceProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        OS.Std.Memory.SpanMemoryConformanceVector* vectors =
            stackalloc OS.Std.Memory.SpanMemoryConformanceVector[16];
        byte* scratch = stackalloc byte[16];
        OS.Std.Memory.SpanMemoryVectorExecutionRecord* records =
            stackalloc OS.Std.Memory.SpanMemoryVectorExecutionRecord[16];

        OS.Std.Memory.SpanMemoryConformanceResult copyResult =
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .CopyBuiltInCatalog(vectors, 16);
        OS.Std.Memory.SpanMemoryExecutionResult executionResult =
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExecuteCatalog(
                    vectors,
                    16,
                    scratch,
                    16,
                    records,
                    16,
                    0xF03FF59D36F39ECDUL);

        if (!M04SpanMemoryCatalogResultMatches(copyResult) ||
            !executionResult.IsInitialized ||
            !executionResult.Succeeded ||
            executionResult.Status !=
                OS.Std.Memory.SpanMemoryExecutionStatus.Success ||
            !M04SpanMemoryCatalogResultMatches(
                executionResult.CatalogValidation) ||
            executionResult.RequiredScratchBytes != 16 ||
            executionResult.AvailableScratchBytes != 16 ||
            executionResult.RequiredOutputCount != 16 ||
            executionResult.AvailableOutputCount != 16 ||
            executionResult.ExecutedVectorCount != 16 ||
            executionResult.ProblemIndex != -1 ||
            executionResult.CatalogFingerprint !=
                0xF03FF59D36F39ECDUL ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .SliceVectorCount != 4 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .OverlapVectorCount != 5 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .MemoryCastVectorCount != 3 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .UnalignedWidthVectorCount != 4 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .TotalVectorCount != 16 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .PointerWidth != 8 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredScratchBytes != 16 ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredExecutionRecordCount != 16 ||
            !OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .MemoryCastAllowsUnalignedStart ||
            !OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .MemoryCastFloorTruncatesTrailingBytes ||
            !OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .UnsafeWidthUsesUnsignedReads ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .AddsCanonicalSystemNamespace ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ReferenceFilesRequired ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiresHostConformance ||
            OS.Std.Memory
                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .SatisfiesRuntimeCompletionGate4)
        {
            WriteM04SpanMemoryConformanceFailureLine(console, serial);
            return false;
        }

        const ulong Seed = 0x0807060504030201UL;
        if (!M04SpanMemoryVectorMatches(
                vectors[0],
                1,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                0,
                0,
                8,
                1,
                1,
                1,
                8,
                8,
                Seed,
                Seed) ||
            !M04SpanMemoryVectorMatches(
                vectors[1],
                2,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                2,
                0,
                4,
                1,
                1,
                1,
                4,
                4,
                Seed,
                0x0000000006050403UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[2],
                3,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                8,
                0,
                0,
                1,
                1,
                1,
                0,
                0,
                Seed,
                0) ||
            !M04SpanMemoryVectorMatches(
                vectors[3],
                4,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                7,
                0,
                2,
                1,
                1,
                0,
                0,
                0,
                Seed,
                0) ||
            !M04SpanMemoryVectorMatches(
                vectors[4],
                5,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                2,
                0,
                4,
                1,
                1,
                1,
                8,
                8,
                Seed,
                0x0807060506050403UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[5],
                6,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                0,
                2,
                4,
                1,
                1,
                1,
                8,
                8,
                Seed,
                0x0807040302010201UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[6],
                7,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                1,
                1,
                4,
                1,
                1,
                1,
                8,
                8,
                Seed,
                Seed) ||
            !M04SpanMemoryVectorMatches(
                vectors[7],
                8,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                8,
                8,
                0,
                1,
                1,
                1,
                8,
                8,
                Seed,
                Seed) ||
            !M04SpanMemoryVectorMatches(
                vectors[8],
                9,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                0,
                4,
                4,
                1,
                1,
                1,
                8,
                8,
                Seed,
                0x0403020104030201UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[9],
                10,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                0,
                0,
                8,
                1,
                2,
                1,
                8,
                4,
                Seed,
                Seed) ||
            !M04SpanMemoryVectorMatches(
                vectors[10],
                11,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                1,
                0,
                6,
                1,
                4,
                1,
                4,
                1,
                Seed,
                0x0000000004030201UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[11],
                12,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                0,
                0,
                4,
                4,
                1,
                1,
                4,
                4,
                0x00000000A1B2C3D4UL,
                0x00000000A1B2C3D4UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[12],
                13,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                1,
                0,
                1,
                1,
                8,
                1,
                8,
                1,
                0xFF,
                0xFF) ||
            !M04SpanMemoryVectorMatches(
                vectors[13],
                14,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                1,
                0,
                2,
                2,
                8,
                3,
                8,
                1,
                0xFF80,
                0xFFFFFFFFFFFFFF80UL) ||
            !M04SpanMemoryVectorMatches(
                vectors[14],
                15,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                1,
                0,
                4,
                4,
                8,
                1,
                8,
                1,
                0x89ABCDEF,
                0x0000000089ABCDEFUL) ||
            !M04SpanMemoryVectorMatches(
                vectors[15],
                16,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                1,
                0,
                8,
                8,
                4,
                1,
                4,
                1,
                0x1122334455667788UL,
                0x0000000055667788UL))
        {
            WriteM04SpanMemoryConformanceFailureLine(console, serial);
            return false;
        }

        if (!M04SpanMemoryRecordMatches(
                records[0], 1,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, Seed, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[1], 2,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                4, 4, 0, 0x0000000006050403UL, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[2], 3,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                0, 0, 0, 0, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[3], 4,
                OS.Std.Memory.SpanMemoryConformanceOperation.Slice,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.ExpectedRejection,
                0, 0, 0, 0, Seed, true) ||
            !M04SpanMemoryRecordMatches(
                records[4], 5,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, 0x0807060506050403UL,
                0x0807060506050403UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[5], 6,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, 0x0807040302010201UL,
                0x0807040302010201UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[6], 7,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, Seed, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[7], 8,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, Seed, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[8], 9,
                OS.Std.Memory.SpanMemoryConformanceOperation.Overlap,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 8, 0, 0x0403020104030201UL,
                0x0403020104030201UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[9], 10,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 4, 0, Seed, Seed, false) ||
            !M04SpanMemoryRecordMatches(
                records[10], 11,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                4, 1, 0, 0x0000000004030201UL,
                0x0006050403020100UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[11], 12,
                OS.Std.Memory.SpanMemoryConformanceOperation.MemoryCast,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                4, 4, 0, 0x00000000A1B2C3D4UL,
                0x00000000A1B2C3D4UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[12], 13,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 1, 0xFF, 0xFF, 0x000000000000FF00UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[13], 14,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 1, 0xFF80, 0xFFFFFFFFFFFFFF80UL,
                0x0000000000FF8000UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[14], 15,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                8, 1, 0x89ABCDEF, 0x0000000089ABCDEFUL,
                0x00000089ABCDEF00UL, false) ||
            !M04SpanMemoryRecordMatches(
                records[15], 16,
                OS.Std.Memory.SpanMemoryConformanceOperation.UnalignedWidth,
                OS.Std.Memory.SpanMemoryVectorExecutionStatus.Success,
                4, 1, 0x1122334455667788UL, 0x0000000055667788UL,
                0x2233445566778800UL, false))
        {
            WriteM04SpanMemoryConformanceFailureLine(console, serial);
            return false;
        }

        WriteM04SpanMemoryConformanceSuccessLine(console, serial);
        return true;
    }

    private static bool M04SpanMemoryCatalogResultMatches(
        OS.Std.Memory.SpanMemoryConformanceResult result)
    {
        return result.IsInitialized &&
            result.Succeeded &&
            result.Status == OS.Std.Memory.SpanMemoryConformanceStatus.Success &&
            result.ProblemIndex == -1 &&
            result.ConflictIndex == -1 &&
            result.VectorCount == 16 &&
            result.ExpectedFingerprint == 0xF03FF59D36F39ECDUL &&
            result.ActualFingerprint == 0xF03FF59D36F39ECDUL &&
            !result.SatisfiesRuntimeCompletionGate4;
    }

    private static bool M04SpanMemoryVectorMatches(
        OS.Std.Memory.SpanMemoryConformanceVector vector,
        byte key,
        OS.Std.Memory.SpanMemoryConformanceOperation operation,
        int sourceOffset,
        int destinationOffset,
        int byteCount,
        byte sourceWidth,
        byte destinationWidth,
        byte flags,
        byte expectedByteCount,
        byte expectedElementCount,
        ulong input,
        ulong expected)
    {
        return vector.IsInitialized &&
            vector.Key == key &&
            vector.Operation == operation &&
            vector.SourceOffset == sourceOffset &&
            vector.DestinationOffset == destinationOffset &&
            vector.ByteCount == byteCount &&
            vector.SourceWidth == sourceWidth &&
            vector.DestinationWidth == destinationWidth &&
            vector.Flags == flags &&
            vector.ExpectedByteCount == expectedByteCount &&
            vector.ExpectedElementCount == expectedElementCount &&
            vector.Input == input &&
            vector.Expected == expected &&
            vector.ExpectedSuccess == ((flags & 1) != 0) &&
            vector.SignExtend == ((flags & 2) != 0);
    }

    private static bool M04SpanMemoryRecordMatches(
        OS.Std.Memory.SpanMemoryVectorExecutionRecord record,
        byte key,
        OS.Std.Memory.SpanMemoryConformanceOperation operation,
        OS.Std.Memory.SpanMemoryVectorExecutionStatus status,
        byte outputByteCount,
        byte resultElementCount,
        ulong rawUnsignedValue,
        ulong outputBytes,
        ulong scratchAfter,
        bool rejectedInputPreserved)
    {
        return record.IsInitialized &&
            record.Succeeded &&
            record.Key == key &&
            record.Operation == operation &&
            record.Status == status &&
            record.OutputByteCount == outputByteCount &&
            record.ResultElementCount == resultElementCount &&
            record.RawUnsignedValue == rawUnsignedValue &&
            record.OutputBytes == outputBytes &&
            record.ScratchAfter == scratchAfter &&
            record.RejectedInputPreserved == rejectedInputPreserved;
    }

    private static void WriteM04SpanMemoryConformanceSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04SpanMemoryAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM04SpanMemoryAscii(console, serial, (byte)'4', (byte)'_', (byte)'0', (byte)'0', (byte)'3', (byte)'_', (byte)'R', (byte)'E');
        WriteM04SpanMemoryAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'v', (byte)'e', (byte)'c', (byte)'t');
        WriteM04SpanMemoryAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'1', (byte)'6', (byte)' ', (byte)'s');
        WriteM04SpanMemoryAscii(console, serial, (byte)'p', (byte)'a', (byte)'n', (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o');
        WriteM04SpanMemoryAscii(console, serial, (byte)'r', (byte)'s', (byte)'=', (byte)'4', (byte)' ', (byte)'o', (byte)'v', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'r', (byte)'l', (byte)'a', (byte)'p', (byte)'V', (byte)'e', (byte)'c', (byte)'t');
        WriteM04SpanMemoryAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'5', (byte)' ', (byte)'m', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'m', (byte)'o', (byte)'r', (byte)'y', (byte)'C', (byte)'a', (byte)'s', (byte)'t');
        WriteM04SpanMemoryAscii(console, serial, (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'s', (byte)'=');
        WriteM04SpanMemoryAscii(console, serial, (byte)'3', (byte)' ', (byte)'u', (byte)'n', (byte)'s', (byte)'a', (byte)'f', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'W', (byte)'i', (byte)'d', (byte)'t', (byte)'h', (byte)'V', (byte)'e', (byte)'c');
        WriteM04SpanMemoryAscii(console, serial, (byte)'t', (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'4', (byte)' ', (byte)'f');
        WriteM04SpanMemoryAscii(console, serial, (byte)'i', (byte)'n', (byte)'g', (byte)'e', (byte)'r', (byte)'p', (byte)'r', (byte)'i');
        WriteM04SpanMemoryAscii(console, serial, (byte)'n', (byte)'t', (byte)'=', (byte)'F', (byte)'0', (byte)'3', (byte)'F', (byte)'F');
        WriteM04SpanMemoryAscii(console, serial, (byte)'5', (byte)'9', (byte)'D', (byte)'3', (byte)'6', (byte)'F', (byte)'3', (byte)'9');
        WriteM04SpanMemoryAscii(console, serial, (byte)'E', (byte)'C', (byte)'D', (byte)' ', (byte)'p', (byte)'o', (byte)'i', (byte)'n');
        WriteM04SpanMemoryAscii(console, serial, (byte)'t', (byte)'e', (byte)'r', (byte)'W', (byte)'i', (byte)'d', (byte)'t', (byte)'h');
        WriteM04SpanMemoryAscii(console, serial, (byte)'=', (byte)'8', (byte)' ', (byte)'r', (byte)'e', (byte)'f', (byte)'e', (byte)'r');
        WriteM04SpanMemoryAscii(console, serial, (byte)'e', (byte)'n', (byte)'c', (byte)'e', (byte)'F', (byte)'i', (byte)'l', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'s', (byte)'R', (byte)'e', (byte)'q', (byte)'u', (byte)'i', (byte)'r', (byte)'e');
        WriteM04SpanMemoryAscii(console, serial, (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM04SpanMemoryAscii(console, serial, (byte)'h', (byte)'o', (byte)'s', (byte)'t', (byte)'C', (byte)'o', (byte)'n', (byte)'f');
        WriteM04SpanMemoryAscii(console, serial, (byte)'o', (byte)'r', (byte)'m', (byte)'a', (byte)'n', (byte)'c', (byte)'e', (byte)'=');
        WriteM04SpanMemoryAscii(console, serial, (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'g', (byte)'u');
        WriteM04SpanMemoryAscii(console, serial, (byte)'e', (byte)'s', (byte)'t', (byte)'I', (byte)'n', (byte)'t', (byte)'e', (byte)'g');
        WriteM04SpanMemoryAscii(console, serial, (byte)'r', (byte)'a', (byte)'t', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r');
        WriteM04SpanMemoryAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'a', (byte)'t', (byte)'e', (byte)'4');
        WriteM04SpanMemoryAscii(console, serial, (byte)'=', (byte)'F', (byte)'a', (byte)'i', (byte)'l', 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04SpanMemoryConformanceFailureLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM04SpanMemoryAscii(console, serial, (byte)'M', (byte)'0', (byte)'4', (byte)'_', (byte)'S', (byte)'P', (byte)'A', (byte)'N');
        WriteM04SpanMemoryAscii(console, serial, (byte)'_', (byte)'M', (byte)'E', (byte)'M', (byte)'O', (byte)'R', (byte)'Y', (byte)'_');
        WriteM04SpanMemoryAscii(console, serial, (byte)'C', (byte)'O', (byte)'N', (byte)'F', (byte)'O', (byte)'R', (byte)'M', (byte)'A');
        WriteM04SpanMemoryAscii(console, serial, (byte)'N', (byte)'C', (byte)'E', (byte)'_', (byte)'F', (byte)'A', (byte)'I', (byte)'L');
        WriteM04SpanMemoryAscii(console, serial, (byte)'E', (byte)'D', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM04SpanMemoryAscii(
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
        if (b0 != 0) Write(console, serial, b0);
        if (b1 != 0) Write(console, serial, b1);
        if (b2 != 0) Write(console, serial, b2);
        if (b3 != 0) Write(console, serial, b3);
        if (b4 != 0) Write(console, serial, b4);
        if (b5 != 0) Write(console, serial, b5);
        if (b6 != 0) Write(console, serial, b6);
        if (b7 != 0) Write(console, serial, b7);
    }
#endif

#if M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE
    private const uint M02NormalizedInterruptFrameAllocateAnyPages = 0;
    private const uint M02NormalizedInterruptFrameEfiLoaderData = 2;
    private const ulong M02NormalizedInterruptFramePageCount = 1;
    private const ulong M02NormalizedInterruptFramePageBytes = 4096;
    private const ulong M02NormalizedInterruptFrameExpectedFnv1A64 =
        0x428D0353A96B7ACEUL;

    private static bool WriteM02NormalizedInterruptFramePageProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteM02NormalizedInterruptFramePageStartLine(console, serial);

        EfiBootServices* bootServices =
            systemTable == null ? null : systemTable->BootServices;
        if (bootServices == null ||
            bootServices->AllocatePages == null ||
            bootServices->FreePages == null)
        {
            WriteM02NormalizedInterruptFramePageFailureLines(
                console,
                serial);
            return false;
        }

        ulong pageAddress = 0;
        ulong allocationStatus = bootServices->AllocatePages(
            M02NormalizedInterruptFrameAllocateAnyPages,
            M02NormalizedInterruptFrameEfiLoaderData,
            M02NormalizedInterruptFramePageCount,
            &pageAddress);
        bool allocationSucceeded = allocationStatus == 0;
        if (!allocationSucceeded)
        {
            WriteM02NormalizedInterruptFramePageFailureLines(
                console,
                serial);
            return false;
        }

        bool addressValid =
            pageAddress != 0 &&
            (pageAddress &
                (M02NormalizedInterruptFramePageBytes - 1)) == 0 &&
            OS.Kernel.Diagnostics
                .NormalizeErrorCodeNonErrorCodeInterruptFrame
                .IsCanonicalX64Address(pageAddress) &&
            pageAddress <=
                ~0UL - M02NormalizedInterruptFramePageBytes;
        ulong commonTargetAddress = addressValid
            ? pageAddress + M02NormalizedInterruptFramePageBytes
            : 0;
        addressValid =
            addressValid &&
            OS.Kernel.Diagnostics
                .NormalizeErrorCodeNonErrorCodeInterruptFrame
                .IsCanonicalX64Address(
                    pageAddress +
                    M02NormalizedInterruptFramePageBytes - 1) &&
            OS.Kernel.Diagnostics
                .NormalizeErrorCodeNonErrorCodeInterruptFrame
                .IsCanonicalX64Address(commonTargetAddress);

        bool resultsMatch = false;
        if (addressValid)
        {
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
                materializeResult =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .Materialize(
                        (byte*)pageAddress,
                        M02NormalizedInterruptFramePageBytes,
                        pageAddress,
                        commonTargetAddress);
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
                readBackResult =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        (byte*)pageAddress,
                        M02NormalizedInterruptFramePageBytes,
                        pageAddress,
                        commonTargetAddress);

            resultsMatch =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .VectorCount == 256 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .SlotByteCount == 16 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .PageByteCount == 4096 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .PageAlignment == 4096 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .NormalizedSemanticSlotCount == 3 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .HardwareErrorCodeVectorCount == 9 &&
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .SyntheticErrorCodeVectorCount == 247 &&
                M02NormalizedInterruptFramePageResultMatches(
                    materializeResult,
                    OS.Kernel.Diagnostics
                        .NormalizeInterruptFramePageOperation
                        .Materialize,
                    pageAddress,
                    commonTargetAddress,
                    true) &&
                M02NormalizedInterruptFramePageResultMatches(
                    readBackResult,
                    OS.Kernel.Diagnostics
                        .NormalizeInterruptFramePageOperation
                        .ValidateReadBack,
                    pageAddress,
                    commonTargetAddress,
                    false) &&
                materializeResult.Fingerprint ==
                    readBackResult.Fingerprint;
        }

        ulong releaseStatus = bootServices->FreePages(
            pageAddress,
            M02NormalizedInterruptFramePageCount);
        if (!addressValid || !resultsMatch || releaseStatus != 0)
        {
            WriteM02NormalizedInterruptFramePageFailureLines(
                console,
                serial);
            return false;
        }

        WriteM02NormalizedInterruptFramePageSuccessLine(console, serial);
        WriteM02NormalizedInterruptFramePagePassLine(console, serial);
        return true;
    }

    private static bool M02NormalizedInterruptFramePageResultMatches(
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult result,
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation operation,
        ulong pageAddress,
        ulong commonTargetAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics
                    .NormalizeInterruptFramePageStatus.Success &&
            result.ModeledPageAddress == pageAddress &&
            result.CommonTargetAddress == commonTargetAddress &&
            result.PageCapacity ==
                M02NormalizedInterruptFramePageBytes &&
            result.RequiredBytes ==
                (int)M02NormalizedInterruptFramePageBytes &&
            result.BytesWritten ==
                (materialized
                    ? (int)M02NormalizedInterruptFramePageBytes
                    : 0) &&
            result.BytesCompared ==
                (int)M02NormalizedInterruptFramePageBytes &&
            result.FirstMismatchOffset == -1 &&
            result.HardwareErrorCodeVectorCount == 9 &&
            result.SyntheticErrorCodeVectorCount == 247 &&
            result.Fingerprint ==
                M02NormalizedInterruptFrameExpectedFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static void WriteM02NormalizedInterruptFramePageStartLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'S', (byte)'T', (byte)'A');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'R', (byte)'T', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'1', (byte)'0', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02NormalizedInterruptFramePageSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'2', (byte)'_', (byte)'0', (byte)'1', (byte)'0', (byte)'_', (byte)'R', (byte)'E');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'v', (byte)'e', (byte)'c', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'2', (byte)'5', (byte)'6', (byte)' ');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'h', (byte)'a', (byte)'r', (byte)'d', (byte)'w', (byte)'a', (byte)'r', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'E', (byte)'r', (byte)'r', (byte)'o', (byte)'r', (byte)'C', (byte)'o', (byte)'d');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'e', (byte)'V', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'s');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'=', (byte)'9', (byte)' ', (byte)'s', (byte)'y', (byte)'n', (byte)'t', (byte)'h');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'e', (byte)'t', (byte)'i', (byte)'c', (byte)'E', (byte)'r', (byte)'r', (byte)'o');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'r', (byte)'C', (byte)'o', (byte)'d', (byte)'e', (byte)'V', (byte)'e', (byte)'c');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'t', (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'2', (byte)'4', (byte)'7');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)' ', (byte)'s', (byte)'l', (byte)'o', (byte)'t', (byte)'B', (byte)'y', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'e', (byte)'s', (byte)'=', (byte)'1', (byte)'6', (byte)' ', (byte)'p', (byte)'a');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'g', (byte)'e', (byte)'B', (byte)'y', (byte)'t', (byte)'e', (byte)'s', (byte)'=');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'4', (byte)'0', (byte)'9', (byte)'6', (byte)' ', (byte)'n', (byte)'o', (byte)'r');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'m', (byte)'a', (byte)'l', (byte)'i', (byte)'z', (byte)'e', (byte)'d', (byte)'S');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'l', (byte)'o', (byte)'t', (byte)'s', (byte)'=', (byte)'3', (byte)' ', (byte)'c');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'o', (byte)'m', (byte)'m', (byte)'o', (byte)'n', (byte)'T', (byte)'a', (byte)'r');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'g', (byte)'e', (byte)'t', (byte)'D', (byte)'e', (byte)'l', (byte)'t', (byte)'a');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'=', (byte)'4', (byte)'0', (byte)'9', (byte)'6', (byte)' ', (byte)'f', (byte)'n');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'v', (byte)'1', (byte)'a', (byte)'6', (byte)'4', (byte)'=', (byte)'4', (byte)'2');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'8', (byte)'D', (byte)'0', (byte)'3', (byte)'5', (byte)'3', (byte)'A', (byte)'9');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'6', (byte)'B', (byte)'7', (byte)'A', (byte)'C', (byte)'E', (byte)' ', (byte)'r');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'e', (byte)'a', (byte)'d', (byte)'B', (byte)'a', (byte)'c', (byte)'k', (byte)'=');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'u', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'s', (byte)'t', (byte)'M', (byte)'a', (byte)'t', (byte)'e', (byte)'r', (byte)'i');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'l', (byte)'i', (byte)'z', (byte)'a', (byte)'t', (byte)'i', (byte)'o');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'n', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'l');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'o', (byte)'a', (byte)'d', (byte)'e', (byte)'r', (byte)'D', (byte)'a', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'p');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'g', (byte)'e', (byte)'R', (byte)'e', (byte)'l', (byte)'e', (byte)'a');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'s', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)' ', (byte)'s', (byte)'t', (byte)'u', (byte)'b', (byte)'E', (byte)'x', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'c', (byte)'u', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'=', (byte)'f');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'i', (byte)'d', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'I', (byte)'n', (byte)'s', (byte)'t', (byte)'a', (byte)'l', (byte)'l', (byte)'e');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'f', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'T', (byte)'r', (byte)'i');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'g', (byte)'g', (byte)'e', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'f');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'m', (byte)'a', (byte)'r');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'k', (byte)'e', (byte)'r', (byte)'T', (byte)'r', (byte)'a', (byte)'n', (byte)'s');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'p', (byte)'o', (byte)'r', (byte)'t', (byte)'=', (byte)'u', (byte)'e', (byte)'f');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'i', (byte)'S', (byte)'e', (byte)'r', (byte)'i', (byte)'a', (byte)'l', (byte)' ');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'p', (byte)'o', (byte)'s', (byte)'t', (byte)'E', (byte)'x', (byte)'i', (byte)'t');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'S', (byte)'e', (byte)'r', (byte)'v');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'i', (byte)'c', (byte)'e', (byte)'s', (byte)'=', (byte)'f', (byte)'a', (byte)'l');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'s', (byte)'e', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02NormalizedInterruptFramePagePassLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'P', (byte)'A', (byte)'S');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'S', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'0', 0, 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02NormalizedInterruptFramePageFailureLines(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'M', (byte)'0', (byte)'2', (byte)'_', (byte)'N', (byte)'O', (byte)'R', (byte)'M');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'A', (byte)'L', (byte)'I', (byte)'Z', (byte)'E', (byte)'D', (byte)'_', (byte)'I');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'N', (byte)'T', (byte)'E', (byte)'R', (byte)'R', (byte)'U', (byte)'P', (byte)'T');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'_', (byte)'F', (byte)'R', (byte)'A', (byte)'M', (byte)'E', (byte)'_', (byte)'P');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'A', (byte)'G', (byte)'E', (byte)'_', (byte)'F', (byte)'A', (byte)'I', (byte)'L');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'E', (byte)'D', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'F', (byte)'A', (byte)'I');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'L', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'0', (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'_');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'m', (byte)'a', (byte)'t', (byte)'e', (byte)'r', (byte)'i', (byte)'a', (byte)'l');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'i', (byte)'z', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'_');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'r', (byte)'e', (byte)'a', (byte)'d', (byte)'b', (byte)'a', (byte)'c', (byte)'k');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'_', (byte)'o', (byte)'r', (byte)'_', (byte)'c', (byte)'l', (byte)'e', (byte)'a');
        WriteM02NormalizedInterruptFramePageAscii(console, serial, (byte)'n', (byte)'u', (byte)'p', 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02NormalizedInterruptFramePageAscii(
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
        if (b0 != 0) Write(console, serial, b0);
        if (b1 != 0) Write(console, serial, b1);
        if (b2 != 0) Write(console, serial, b2);
        if (b3 != 0) Write(console, serial, b3);
        if (b4 != 0) Write(console, serial, b4);
        if (b5 != 0) Write(console, serial, b5);
        if (b6 != 0) Write(console, serial, b6);
        if (b7 != 0) Write(console, serial, b7);
    }
#endif

#if M02_OWNED_IDT_GATE_POPULATION_PROBE
    private const uint M02OwnedIdtGateAllocateAnyPages = 0;
    private const uint M02OwnedIdtGateEfiLoaderData = 2;
    private const ulong M02OwnedIdtGateAllocationPages = 3;
    private const ulong M02OwnedIdtGatePageBytes = 4096;
    private const ulong M02OwnedIdtGateArenaBytes =
        M02OwnedIdtGateAllocationPages * M02OwnedIdtGatePageBytes;
    private const ulong M02OwnedIdtGateStubExpectedFnv1A64 =
        0x641BA73668C75E0EUL;
    private const ulong M02OwnedIdtGateNormalizedPolicyFnv1A64 =
        0x8514F42EBDFC5D55UL;

    private static bool WriteM02OwnedIdtGatePopulationProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteM02OwnedIdtGatePopulationStartLine(console, serial);

        EfiBootServices* bootServices =
            systemTable == null ? null : systemTable->BootServices;
        if (bootServices == null ||
            bootServices->AllocatePages == null ||
            bootServices->FreePages == null)
        {
            WriteM02OwnedIdtGatePopulationFailureLines(console, serial);
            return false;
        }

        ulong arenaAddress = 0;
        ulong allocationStatus = bootServices->AllocatePages(
            M02OwnedIdtGateAllocateAnyPages,
            M02OwnedIdtGateEfiLoaderData,
            M02OwnedIdtGateAllocationPages,
            &arenaAddress);
        if (allocationStatus != 0)
        {
            WriteM02OwnedIdtGatePopulationFailureLines(console, serial);
            return false;
        }

        bool addressValid =
            arenaAddress != 0 &&
            (arenaAddress & (M02OwnedIdtGatePageBytes - 1)) == 0 &&
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(arenaAddress) &&
            arenaAddress <= ~0UL - M02OwnedIdtGateArenaBytes;
        ulong idtPageAddress = addressValid
            ? arenaAddress + M02OwnedIdtGatePageBytes
            : 0;
        ulong commonTargetAddress = addressValid
            ? arenaAddress + (2 * M02OwnedIdtGatePageBytes)
            : 0;
        ulong arenaEndInclusive = addressValid
            ? arenaAddress + M02OwnedIdtGateArenaBytes - 1
            : 0;
        addressValid =
            addressValid &&
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(idtPageAddress) &&
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(commonTargetAddress) &&
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .IsCanonicalX64Address(arenaEndInclusive) &&
            ((arenaAddress >> 47) & 1UL) ==
                ((arenaEndInclusive >> 47) & 1UL);

        bool resultsMatch = false;
        if (addressValid)
        {
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
                stubMaterializeResult =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .Materialize(
                        (byte*)arenaAddress,
                        M02OwnedIdtGatePageBytes,
                        arenaAddress,
                        commonTargetAddress);
            OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
                stubReadBackResult =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        (byte*)arenaAddress,
                        M02OwnedIdtGatePageBytes,
                        arenaAddress,
                        commonTargetAddress);
            OS.Kernel.Diagnostics.OwnedIdtGateTableResult
                idtMaterializeResult =
                OS.Kernel.Diagnostics
                    .PopulateAllN256IDTVectorsOwnedGatesNever
                    .Materialize(
                        (byte*)idtPageAddress,
                        M02OwnedIdtGatePageBytes,
                        idtPageAddress,
                        arenaAddress);
            OS.Kernel.Diagnostics.OwnedIdtGateTableResult
                idtReadBackResult =
                OS.Kernel.Diagnostics
                    .PopulateAllN256IDTVectorsOwnedGatesNever
                    .ValidateReadBack(
                        (byte*)idtPageAddress,
                        M02OwnedIdtGatePageBytes,
                        idtPageAddress,
                        arenaAddress);

            resultsMatch =
                M02OwnedIdtGateStubResultMatches(
                    stubMaterializeResult,
                    OS.Kernel.Diagnostics
                        .NormalizeInterruptFramePageOperation.Materialize,
                    arenaAddress,
                    commonTargetAddress,
                    true) &&
                M02OwnedIdtGateStubResultMatches(
                    stubReadBackResult,
                    OS.Kernel.Diagnostics
                        .NormalizeInterruptFramePageOperation
                        .ValidateReadBack,
                    arenaAddress,
                    commonTargetAddress,
                    false) &&
                stubMaterializeResult.Fingerprint ==
                    stubReadBackResult.Fingerprint &&
                M02OwnedIdtGateTableResultMatches(
                    idtMaterializeResult,
                    OS.Kernel.Diagnostics
                        .OwnedIdtGateTableOperation.Materialize,
                    idtPageAddress,
                    arenaAddress,
                    true) &&
                M02OwnedIdtGateTableResultMatches(
                    idtReadBackResult,
                    OS.Kernel.Diagnostics
                        .OwnedIdtGateTableOperation.ValidateReadBack,
                    idtPageAddress,
                    arenaAddress,
                    false) &&
                idtMaterializeResult.Fingerprint ==
                    idtReadBackResult.Fingerprint;
        }

        ulong releaseStatus = bootServices->FreePages(
            arenaAddress,
            M02OwnedIdtGateAllocationPages);
        if (!addressValid || !resultsMatch || releaseStatus != 0)
        {
            WriteM02OwnedIdtGatePopulationFailureLines(console, serial);
            return false;
        }

        WriteM02OwnedIdtGatePopulationSuccessLine(console, serial);
        WriteM02OwnedIdtGatePopulationPassLine(console, serial);
        return true;
    }

    private static bool M02OwnedIdtGateStubResultMatches(
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult result,
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation operation,
        ulong pageAddress,
        ulong commonTargetAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics
                    .NormalizeInterruptFramePageStatus.Success &&
            result.ModeledPageAddress == pageAddress &&
            result.CommonTargetAddress == commonTargetAddress &&
            result.PageCapacity == M02OwnedIdtGatePageBytes &&
            result.RequiredBytes == (int)M02OwnedIdtGatePageBytes &&
            result.BytesWritten ==
                (materialized ? (int)M02OwnedIdtGatePageBytes : 0) &&
            result.BytesCompared == (int)M02OwnedIdtGatePageBytes &&
            result.FirstMismatchOffset == -1 &&
            result.HardwareErrorCodeVectorCount == 9 &&
            result.SyntheticErrorCodeVectorCount == 247 &&
            result.Fingerprint == M02OwnedIdtGateStubExpectedFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static bool M02OwnedIdtGateTableResultMatches(
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult result,
        OS.Kernel.Diagnostics.OwnedIdtGateTableOperation operation,
        ulong tableAddress,
        ulong stubPageAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics.OwnedIdtGateTableStatus.Success &&
            result.ModeledTableAddress == tableAddress &&
            result.OwnedStubPageAddress == stubPageAddress &&
            result.TableCapacity == M02OwnedIdtGatePageBytes &&
            result.RequiredBytes == (int)M02OwnedIdtGatePageBytes &&
            result.DescriptorCount == 256 &&
            result.PresentDescriptorCount == 256 &&
            result.IstRoutedDescriptorCount == 3 &&
            result.DefaultIstDescriptorCount == 253 &&
            result.FirmwareGateCopyCount == 0 &&
            result.BytesWritten ==
                (materialized ? (int)M02OwnedIdtGatePageBytes : 0) &&
            result.BytesCompared == (int)M02OwnedIdtGatePageBytes &&
            result.FirstMismatchOffset == -1 &&
            result.FirstMismatchVector == -1 &&
            result.NormalizedPolicyFingerprint ==
                M02OwnedIdtGateNormalizedPolicyFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static void WriteM02OwnedIdtGatePopulationStartLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'S', (byte)'T', (byte)'A');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'R', (byte)'T', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'1', (byte)'1', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02OwnedIdtGatePopulationSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'2', (byte)'_', (byte)'0', (byte)'1', (byte)'1', (byte)'_', (byte)'R', (byte)'E');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'v', (byte)'e', (byte)'c', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'o', (byte)'r', (byte)'s', (byte)'=', (byte)'2', (byte)'5', (byte)'6', (byte)' ');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'g', (byte)'a', (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'2', (byte)'5');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'6', (byte)' ', (byte)'g', (byte)'a', (byte)'t', (byte)'e', (byte)'B', (byte)'y');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'1', (byte)'6', (byte)' ', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'b', (byte)'l', (byte)'e', (byte)'B', (byte)'y', (byte)'t', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'s', (byte)'=', (byte)'4', (byte)'0', (byte)'9', (byte)'6', (byte)' ', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'e', (byte)'l', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'=');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'8', (byte)' ', (byte)'p', (byte)'r', (byte)'e', (byte)'s', (byte)'e', (byte)'n');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'t', (byte)'=', (byte)'2', (byte)'5', (byte)'6', (byte)' ', (byte)'d', (byte)'p');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'l', (byte)'0', (byte)'=', (byte)'2', (byte)'5', (byte)'6', (byte)' ', (byte)'i');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'n', (byte)'t', (byte)'e', (byte)'r', (byte)'r', (byte)'u', (byte)'p', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'G', (byte)'a', (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'2', (byte)'5');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'6', (byte)' ', (byte)'o', (byte)'w', (byte)'n', (byte)'e', (byte)'d', (byte)'H');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'n', (byte)'d', (byte)'l', (byte)'e', (byte)'r', (byte)'s', (byte)'=');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'2', (byte)'5', (byte)'6', (byte)' ', (byte)'f', (byte)'i', (byte)'r', (byte)'m');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'w', (byte)'a', (byte)'r', (byte)'e', (byte)'G', (byte)'a', (byte)'t', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'s', (byte)'C', (byte)'o', (byte)'p', (byte)'i', (byte)'e', (byte)'d', (byte)'=');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'0', (byte)' ', (byte)'n', (byte)'m', (byte)'i', (byte)'I', (byte)'s', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'=', (byte)'3', (byte)' ', (byte)'d', (byte)'o', (byte)'u', (byte)'b', (byte)'l');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'e', (byte)'F', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'I', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'t', (byte)'=', (byte)'1', (byte)' ', (byte)'p', (byte)'a', (byte)'g', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'F', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'I', (byte)'s', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'=', (byte)'2', (byte)' ', (byte)'n', (byte)'o', (byte)'r', (byte)'m', (byte)'a');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'l', (byte)'i', (byte)'z', (byte)'e', (byte)'d', (byte)'F', (byte)'n', (byte)'v');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'1', (byte)'a', (byte)'6', (byte)'4', (byte)'=', (byte)'8', (byte)'5', (byte)'1');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'4', (byte)'F', (byte)'4', (byte)'2', (byte)'E', (byte)'B', (byte)'D', (byte)'F');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'C', (byte)'5', (byte)'D', (byte)'5', (byte)'5', (byte)' ', (byte)'r', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'d', (byte)'B', (byte)'a', (byte)'c', (byte)'k', (byte)'=', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'t', (byte)'P', (byte)'o', (byte)'p', (byte)'u', (byte)'l', (byte)'a', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'i', (byte)'o', (byte)'n', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)' ', (byte)'l', (byte)'o', (byte)'a', (byte)'d', (byte)'e', (byte)'r', (byte)'D');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'t', (byte)'a', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)' ', (byte)'a', (byte)'l', (byte)'l', (byte)'o', (byte)'c', (byte)'a', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'i', (byte)'o', (byte)'n', (byte)'P', (byte)'a', (byte)'g', (byte)'e', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'=', (byte)'3', (byte)' ', (byte)'a', (byte)'l', (byte)'l', (byte)'o', (byte)'c');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'R', (byte)'e', (byte)'l');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'e', (byte)'a', (byte)'s', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'s', (byte)'t', (byte)'u', (byte)'b', (byte)'E');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'x', (byte)'e', (byte)'c', (byte)'u', (byte)'t', (byte)'i', (byte)'o', (byte)'n');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'i');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'d', (byte)'t', (byte)'r', (byte)'L', (byte)'o', (byte)'a', (byte)'d', (byte)'e');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'f', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'T', (byte)'r', (byte)'i');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'g', (byte)'g', (byte)'e', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'f');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'m', (byte)'a', (byte)'r');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'k', (byte)'e', (byte)'r', (byte)'T', (byte)'r', (byte)'a', (byte)'n', (byte)'s');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'p', (byte)'o', (byte)'r', (byte)'t', (byte)'=', (byte)'u', (byte)'e', (byte)'f');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'i', (byte)'S', (byte)'e', (byte)'r', (byte)'i', (byte)'a', (byte)'l', (byte)' ');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'p', (byte)'o', (byte)'s', (byte)'t', (byte)'E', (byte)'x', (byte)'i', (byte)'t');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'S', (byte)'e', (byte)'r', (byte)'v');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'i', (byte)'c', (byte)'e', (byte)'s', (byte)'=', (byte)'f', (byte)'a', (byte)'l');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'s', (byte)'e', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02OwnedIdtGatePopulationPassLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'P', (byte)'A', (byte)'S');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'S', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'1', 0, 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02OwnedIdtGatePopulationFailureLines(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'M', (byte)'0', (byte)'2', (byte)'_', (byte)'O', (byte)'W', (byte)'N', (byte)'E');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'D', (byte)'_', (byte)'I', (byte)'D', (byte)'T', (byte)'_', (byte)'G', (byte)'A');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'T', (byte)'E', (byte)'_', (byte)'P', (byte)'O', (byte)'P', (byte)'U', (byte)'L');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'A', (byte)'T', (byte)'I', (byte)'O', (byte)'N', (byte)'_', (byte)'F', (byte)'A');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'I', (byte)'L', (byte)'E', (byte)'D', 0, 0, 0, 0);
        WriteCrLf(console, serial);
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'F', (byte)'A', (byte)'I');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'L', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'1', (byte)' ', (byte)'g', (byte)'u', (byte)'e', (byte)'s', (byte)'t', (byte)'_');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'p', (byte)'o', (byte)'p', (byte)'u', (byte)'l', (byte)'a', (byte)'t', (byte)'i');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'o', (byte)'n', (byte)'_', (byte)'r', (byte)'e', (byte)'a', (byte)'d', (byte)'b');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'a', (byte)'c', (byte)'k', (byte)'_', (byte)'o', (byte)'r', (byte)'_', (byte)'c');
        WriteM02OwnedIdtGatePopulationAscii(console, serial, (byte)'l', (byte)'e', (byte)'a', (byte)'n', (byte)'u', (byte)'p', 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02OwnedIdtGatePopulationAscii(
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
        if (b0 != 0) Write(console, serial, b0);
        if (b1 != 0) Write(console, serial, b1);
        if (b2 != 0) Write(console, serial, b2);
        if (b3 != 0) Write(console, serial, b3);
        if (b4 != 0) Write(console, serial, b4);
        if (b5 != 0) Write(console, serial, b5);
        if (b6 != 0) Write(console, serial, b6);
        if (b7 != 0) Write(console, serial, b7);
    }
#endif

#if M02_IDTR_CONTINUED_EXECUTION_PROBE
    private const uint M02IdtrContinuedExecutionAllocateAnyPages = 0;
    private const uint M02IdtrContinuedExecutionEfiLoaderCode = 1;
    private const uint M02IdtrContinuedExecutionEfiLoaderData = 2;
    private const ulong M02IdtrContinuedExecutionPageBytes = 4096;
    private const ulong M02IdtrContinuedExecutionCodePages = 2;
    private const ulong M02IdtrContinuedExecutionDataPages = 1;
    private const ulong M02IdtrContinuedExecutionCodeBytes = 8192;
    private const ulong M02IdtrContinuedExecutionDataBytes = 4096;
    private const ulong M02IdtrContinuedExecutionCommonTargetOffset = 4096;
    private const ulong M02IdtrContinuedExecutionTransitionOffset = 0x1040;
    private const ulong M02IdtrContinuedExecutionContextWorkspaceBytes = 96;
    private const ulong M02IdtrContinuedExecutionNormalizedFnv1A64 =
        0x428D0353A96B7ACEUL;
    private const ulong M02IdtrContinuedExecutionPolicyFnv1A64 =
        0x8514F42EBDFC5D55UL;

    private static bool WriteM02IdtrContinuedExecutionProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteM02IdtrContinuedExecutionStartLine(console, serial);

        EfiBootServices* bootServices = systemTable == null
            ? null
            : systemTable->BootServices;
        ulong codeAddress = 0;
        ulong dataAddress = 0;
        bool codeAllocated = false;
        bool dataAllocated = false;

        if (bootServices == null ||
            bootServices->AllocatePages == null ||
            bootServices->FreePages == null)
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong allocationStatus = bootServices->AllocatePages(
            M02IdtrContinuedExecutionAllocateAnyPages,
            M02IdtrContinuedExecutionEfiLoaderCode,
            M02IdtrContinuedExecutionCodePages,
            &codeAddress);
        codeAllocated = allocationStatus == 0;
        if (!codeAllocated ||
            !IsValidM02IdtrContinuedExecutionRange(
                codeAddress,
                M02IdtrContinuedExecutionCodeBytes,
                M02IdtrContinuedExecutionPageBytes))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        allocationStatus = bootServices->AllocatePages(
            M02IdtrContinuedExecutionAllocateAnyPages,
            M02IdtrContinuedExecutionEfiLoaderData,
            M02IdtrContinuedExecutionDataPages,
            &dataAddress);
        dataAllocated = allocationStatus == 0;
        if (!dataAllocated ||
            !IsValidM02IdtrContinuedExecutionRange(
                dataAddress,
                M02IdtrContinuedExecutionDataBytes,
                M02IdtrContinuedExecutionPageBytes) ||
            M02IdtrContinuedExecutionRangesOverlap(
                codeAddress,
                M02IdtrContinuedExecutionCodeBytes,
                dataAddress,
                M02IdtrContinuedExecutionDataBytes))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        M02IdtrContinuedExecutionStackWorkspace stackWorkspace = default;
        byte* workspaceStart = stackWorkspace.Bytes;
        ulong workspaceAddress = (ulong)workspaceStart;
        if (workspaceAddress >
            ~0UL -
                (OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextAlignment - 1))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong contextAddress =
            (workspaceAddress +
                (OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextAlignment - 1)) &
            ~(OS.Kernel.Diagnostics.IDTRContinuedExecution
                .ContextAlignment - 1);
        if (!IsValidM02IdtrContinuedExecutionRange(
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount,
                OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextAlignment) ||
            contextAddress < workspaceAddress ||
            contextAddress +
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount >
                workspaceAddress +
                    M02IdtrContinuedExecutionContextWorkspaceBytes ||
            M02IdtrContinuedExecutionRangesOverlap(
                codeAddress,
                M02IdtrContinuedExecutionCodeBytes,
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount) ||
            M02IdtrContinuedExecutionRangesOverlap(
                dataAddress,
                M02IdtrContinuedExecutionDataBytes,
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        ulong commonTargetAddress =
            codeAddress + M02IdtrContinuedExecutionCommonTargetOffset;
        ulong transitionAddress =
            codeAddress + M02IdtrContinuedExecutionTransitionOffset;
        if (!IsValidM02IdtrContinuedExecutionRange(
                commonTargetAddress,
                6,
                1) ||
            !IsValidM02IdtrContinuedExecutionRange(
                transitionAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .StubByteCount,
                OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .StubAlignment))
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        byte* code = (byte*)codeAddress;
        byte* data = (byte*)dataAddress;
        for (ulong offset = 0;
            offset < M02IdtrContinuedExecutionCodeBytes;
            offset++)
        {
            code[offset] = 0;
        }
        for (ulong offset = 0;
            offset < M02IdtrContinuedExecutionDataBytes;
            offset++)
        {
            data[offset] = 0;
        }

        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedMaterialized =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .Materialize(
                        code,
                        M02IdtrContinuedExecutionPageBytes,
                        codeAddress,
                        commonTargetAddress);

        byte* commonTarget = (byte*)commonTargetAddress;
        commonTarget[0] = 0x48;
        commonTarget[1] = 0x83;
        commonTarget[2] = 0xC4;
        commonTarget[3] = 0x18;
        commonTarget[4] = 0x48;
        commonTarget[5] = 0xCF;

        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult
            normalizedReadBack =
                OS.Kernel.Diagnostics
                    .NormalizeErrorCodeNonErrorCodeInterruptFrame
                    .ValidateReadBack(
                        code,
                        M02IdtrContinuedExecutionPageBytes,
                        codeAddress,
                        commonTargetAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtMaterialized =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .Materialize(
                    data,
                    M02IdtrContinuedExecutionDataBytes,
                    dataAddress,
                    codeAddress);
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult idtReadBack =
            OS.Kernel.Diagnostics
                .PopulateAllN256IDTVectorsOwnedGatesNever
                .ValidateReadBack(
                    data,
                    M02IdtrContinuedExecutionDataBytes,
                    dataAddress,
                    codeAddress);

        OS.Kernel.Diagnostics.IDTRContinuedExecutionContext* context =
            (OS.Kernel.Diagnostics.IDTRContinuedExecutionContext*)
                contextAddress;
        OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult
            contextPrepared =
                OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .PrepareContext(
                        context,
                        contextAddress,
                        (ulong)OS.Kernel.Diagnostics
                            .IDTRContinuedExecution.ContextByteCount,
                        dataAddress);
        OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult stubEmitted =
            OS.Kernel.Diagnostics.IDTRContinuedExecution.EmitStub(
                (byte*)transitionAddress,
                transitionAddress,
                M02IdtrContinuedExecutionPageBytes - 0x40,
                context,
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount,
                dataAddress);
        OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult stubReadBack =
            OS.Kernel.Diagnostics.IDTRContinuedExecution
                .ValidateStubReadBack(
                    (byte*)transitionAddress,
                    transitionAddress,
                    M02IdtrContinuedExecutionPageBytes - 0x40,
                    context,
                    contextAddress,
                    (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                        .ContextByteCount,
                    dataAddress);

        if (!M02IdtrContinuedExecutionNormalizedResultMatches(
                normalizedMaterialized,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .Materialize,
                codeAddress,
                commonTargetAddress,
                true) ||
            !M02IdtrContinuedExecutionNormalizedResultMatches(
                normalizedReadBack,
                OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation
                    .ValidateReadBack,
                codeAddress,
                commonTargetAddress,
                false) ||
            normalizedMaterialized.Fingerprint !=
                normalizedReadBack.Fingerprint ||
            !M02IdtrContinuedExecutionIdtResultMatches(
                idtMaterialized,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation.Materialize,
                dataAddress,
                codeAddress,
                true) ||
            !M02IdtrContinuedExecutionIdtResultMatches(
                idtReadBack,
                OS.Kernel.Diagnostics.OwnedIdtGateTableOperation
                    .ValidateReadBack,
                dataAddress,
                codeAddress,
                false) ||
            idtMaterialized.Fingerprint != idtReadBack.Fingerprint ||
            !M02IdtrContinuedExecutionPlanMatches(
                contextPrepared,
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .PrepareContext,
                contextAddress,
                transitionAddress,
                dataAddress) ||
            !M02IdtrContinuedExecutionPlanMatches(
                stubEmitted,
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .EmitStub,
                contextAddress,
                transitionAddress,
                dataAddress) ||
            !M02IdtrContinuedExecutionPlanMatches(
                stubReadBack,
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .ValidateStubReadBack,
                contextAddress,
                transitionAddress,
                dataAddress) ||
            contextPrepared.ContextFingerprint !=
                stubEmitted.ContextFingerprint ||
            contextPrepared.ContextFingerprint !=
                stubReadBack.ContextFingerprint ||
            !M02IdtrContinuedExecutionCommonTargetMatches(commonTarget) ||
            OS.Kernel.Diagnostics.IDTRContinuedExecution.StubFnv1A64 !=
                0xAFF59D6480DE7F3CUL ||
            OS.Kernel.Diagnostics.IDTRContinuedExecution
                .HasExternalRelocations)
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        uint publicationCookie = X64InstructionPublication.Serialize();
        if (publicationCookie !=
            X64InstructionPublication.ExpectedSuccessCookie)
        {
            return FailM02IdtrContinuedExecutionBeforeInvocation(
                console,
                serial,
                bootServices,
                codeAddress,
                codeAllocated,
                dataAddress,
                dataAllocated);
        }

        delegate* unmanaged<
            OS.Kernel.Diagnostics.IDTRContinuedExecutionContext*,
            ulong> transition =
                (delegate* unmanaged<
                    OS.Kernel.Diagnostics.IDTRContinuedExecutionContext*,
                    ulong>)(void*)transitionAddress;
        ulong executionCookie = transition(context);

        if (!M02IdtrContinuedExecutionSafetyRestored(
                context,
                dataAddress))
        {
            SpinM02IdtrContinuedExecutionAfterUnverifiedRestore();
            return false;
        }

        OS.Kernel.Diagnostics.IDTRContinuedExecutionAuditResult audit =
            OS.Kernel.Diagnostics.IDTRContinuedExecution.AuditExecution(
                context,
                contextAddress,
                (ulong)OS.Kernel.Diagnostics.IDTRContinuedExecution
                    .ContextByteCount,
                dataAddress,
                executionCookie);
        if (!M02IdtrContinuedExecutionAuditMatches(
                audit,
                contextAddress,
                dataAddress,
                executionCookie))
        {
            _ = bootServices->FreePages(
                dataAddress,
                M02IdtrContinuedExecutionDataPages);
            _ = bootServices->FreePages(
                codeAddress,
                M02IdtrContinuedExecutionCodePages);
            WriteM02IdtrContinuedExecutionFailureLines(console, serial);
            return false;
        }

        ulong dataReleaseStatus = bootServices->FreePages(
            dataAddress,
            M02IdtrContinuedExecutionDataPages);
        ulong codeReleaseStatus = bootServices->FreePages(
            codeAddress,
            M02IdtrContinuedExecutionCodePages);
        if (dataReleaseStatus != 0 || codeReleaseStatus != 0)
        {
            WriteM02IdtrContinuedExecutionFailureLines(console, serial);
            return false;
        }

        WriteM02IdtrContinuedExecutionSuccessLine(console, serial);
        WriteM02IdtrContinuedExecutionPassLine(console, serial);
        return true;
    }

    private static bool M02IdtrContinuedExecutionNormalizedResultMatches(
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageResult result,
        OS.Kernel.Diagnostics.NormalizeInterruptFramePageOperation operation,
        ulong pageAddress,
        ulong commonTargetAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status == OS.Kernel.Diagnostics
                .NormalizeInterruptFramePageStatus.Success &&
            result.ModeledPageAddress == pageAddress &&
            result.CommonTargetAddress == commonTargetAddress &&
            result.PageCapacity == M02IdtrContinuedExecutionPageBytes &&
            result.RequiredBytes == 4096 &&
            result.BytesWritten == (materialized ? 4096 : 0) &&
            result.BytesCompared == 4096 &&
            result.FirstMismatchOffset == -1 &&
            result.HardwareErrorCodeVectorCount == 9 &&
            result.SyntheticErrorCodeVectorCount == 247 &&
            result.Fingerprint ==
                M02IdtrContinuedExecutionNormalizedFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static bool M02IdtrContinuedExecutionIdtResultMatches(
        OS.Kernel.Diagnostics.OwnedIdtGateTableResult result,
        OS.Kernel.Diagnostics.OwnedIdtGateTableOperation operation,
        ulong tableAddress,
        ulong stubPageAddress,
        bool materialized)
    {
        return result.IsInitialized &&
            result.Operation == operation &&
            result.Status ==
                OS.Kernel.Diagnostics.OwnedIdtGateTableStatus.Success &&
            result.ModeledTableAddress == tableAddress &&
            result.OwnedStubPageAddress == stubPageAddress &&
            result.TableCapacity == M02IdtrContinuedExecutionDataBytes &&
            result.RequiredBytes == 4096 &&
            result.DescriptorCount == 256 &&
            result.PresentDescriptorCount == 256 &&
            result.IstRoutedDescriptorCount == 3 &&
            result.DefaultIstDescriptorCount == 253 &&
            result.FirmwareGateCopyCount == 0 &&
            result.BytesWritten == (materialized ? 4096 : 0) &&
            result.BytesCompared == 4096 &&
            result.FirstMismatchOffset == -1 &&
            result.FirstMismatchVector == -1 &&
            result.NormalizedPolicyFingerprint ==
                M02IdtrContinuedExecutionPolicyFnv1A64 &&
            result.DestinationMutated == materialized &&
            result.ReadBackPerformed &&
            result.Materialized == materialized &&
            result.ReadBackValidated == !materialized &&
            result.Succeeded;
    }

    private static bool M02IdtrContinuedExecutionPlanMatches(
        OS.Kernel.Diagnostics.IDTRContinuedExecutionPlanResult result,
        OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation operation,
        ulong contextAddress,
        ulong transitionAddress,
        ulong idtAddress)
    {
        bool operationMatches = operation ==
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .PrepareContext
            ? result.ContextPrepared
            : operation ==
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .EmitStub
                ? result.StubEmitted
                : result.ReadBackValidated;
        return operationMatches &&
            result.Operation == operation &&
            result.Status == OS.Kernel.Diagnostics
                .IDTRContinuedExecutionStatus.Success &&
            result.ModeledContextAddress == contextAddress &&
            result.ModeledStubAddress ==
                (operation ==
                    OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                        .PrepareContext
                    ? 0
                    : transitionAddress) &&
            result.OwnedIdtAddress == idtAddress &&
            result.ContextCapacity == 80 &&
            result.StubCapacity ==
                (operation ==
                    OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                        .PrepareContext
                    ? 0
                    : M02IdtrContinuedExecutionPageBytes - 0x40) &&
            result.FirstMismatchOffset == -1 &&
            result.ContextFingerprint != 0 &&
            (operation ==
                OS.Kernel.Diagnostics.IDTRContinuedExecutionOperation
                    .PrepareContext ||
                result.StubFingerprint == 0xAFF59D6480DE7F3CUL) &&
            result.Succeeded;
    }

    private static bool M02IdtrContinuedExecutionAuditMatches(
        OS.Kernel.Diagnostics.IDTRContinuedExecutionAuditResult audit,
        ulong contextAddress,
        ulong idtAddress,
        ulong executionCookie)
    {
        return audit.IsInitialized &&
            audit.Operation == OS.Kernel.Diagnostics
                .IDTRContinuedExecutionOperation.AuditExecution &&
            audit.Status == OS.Kernel.Diagnostics
                .IDTRContinuedExecutionStatus.Success &&
            audit.ModeledContextAddress == contextAddress &&
            audit.OwnedIdtAddress == idtAddress &&
            audit.ContextCapacity == 80 &&
            audit.ExpectedCookie == 0x4F53494454523132UL &&
            audit.ObservedCookie == executionCookie &&
            executionCookie == 0x4F53494454523132UL &&
            audit.ContextBytesCompared == 80 &&
            audit.FirstMismatchOffset == -1 &&
            audit.TransitionObserved &&
            audit.OwnedIdtrLoaded &&
            audit.FirmwareIdtrRestored &&
            audit.InterruptFlagRestored &&
            audit.ContinuedExecution &&
            audit.Succeeded &&
            (audit.PreviousLimit != audit.LoadedLimit ||
                audit.PreviousBase != audit.LoadedBase);
    }

    private static bool M02IdtrContinuedExecutionSafetyRestored(
        OS.Kernel.Diagnostics.IDTRContinuedExecutionContext* context,
        ulong ownedIdtAddress)
    {
        byte* bytes = (byte*)context;
        ushort loadedLimit = ReadM02IdtrContinuedExecutionU16(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .LoadedIdtrOffset);
        ulong loadedBase = ReadM02IdtrContinuedExecutionU64(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .LoadedIdtrOffset + 2);
        if (loadedLimit !=
                OS.Kernel.Diagnostics.IDTRContinuedExecution.IdtrLimit ||
            loadedBase != ownedIdtAddress)
        {
            return false;
        }

        for (int offset = 0;
            offset < OS.Kernel.Diagnostics.IDTRContinuedExecution
                .IdtrByteCount;
            offset++)
        {
            if (bytes[
                    OS.Kernel.Diagnostics.IDTRContinuedExecution
                        .PreviousIdtrOffset + offset] !=
                bytes[
                    OS.Kernel.Diagnostics.IDTRContinuedExecution
                        .RestoredIdtrOffset + offset])
            {
                return false;
            }
        }

        ushort previousLimit = ReadM02IdtrContinuedExecutionU16(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .PreviousIdtrOffset);
        ulong previousBase = ReadM02IdtrContinuedExecutionU64(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .PreviousIdtrOffset + 2);
        if (previousLimit ==
                OS.Kernel.Diagnostics.IDTRContinuedExecution.IdtrLimit &&
            previousBase == ownedIdtAddress)
        {
            return false;
        }

        ulong rflagsBefore = ReadM02IdtrContinuedExecutionU64(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .RflagsBeforeOffset);
        ulong rflagsAfter = ReadM02IdtrContinuedExecutionU64(
            bytes + OS.Kernel.Diagnostics.IDTRContinuedExecution
                .RflagsAfterOffset);
        return ((rflagsBefore ^ rflagsAfter) &
            OS.Kernel.Diagnostics.IDTRContinuedExecution
                .RflagsInterruptEnableBit) == 0;
    }

    private static ushort ReadM02IdtrContinuedExecutionU16(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static ulong ReadM02IdtrContinuedExecutionU64(byte* source)
    {
        return (ulong)source[0] |
            ((ulong)source[1] << 8) |
            ((ulong)source[2] << 16) |
            ((ulong)source[3] << 24) |
            ((ulong)source[4] << 32) |
            ((ulong)source[5] << 40) |
            ((ulong)source[6] << 48) |
            ((ulong)source[7] << 56);
    }

    private static bool M02IdtrContinuedExecutionCommonTargetMatches(
        byte* commonTarget)
    {
        return commonTarget[0] == 0x48 &&
            commonTarget[1] == 0x83 &&
            commonTarget[2] == 0xC4 &&
            commonTarget[3] == 0x18 &&
            commonTarget[4] == 0x48 &&
            commonTarget[5] == 0xCF;
    }

    private static bool IsValidM02IdtrContinuedExecutionRange(
        ulong address,
        ulong length,
        ulong alignment)
    {
        if (address == 0 ||
            length == 0 ||
            alignment == 0 ||
            (address & (alignment - 1)) != 0 ||
            !OS.Kernel.Diagnostics.IDTRContinuedExecution
                .IsCanonicalX64Address(address) ||
            address > ~0UL - length)
        {
            return false;
        }

        ulong endInclusive = address + length - 1;
        return OS.Kernel.Diagnostics.IDTRContinuedExecution
                .IsCanonicalX64Address(endInclusive) &&
            ((address >> 47) & 1UL) ==
                ((endInclusive >> 47) & 1UL);
    }

    private static bool M02IdtrContinuedExecutionRangesOverlap(
        ulong firstAddress,
        ulong firstLength,
        ulong secondAddress,
        ulong secondLength)
    {
        return firstAddress <= secondAddress
            ? secondAddress - firstAddress < firstLength
            : firstAddress - secondAddress < secondLength;
    }

    private static bool FailM02IdtrContinuedExecutionBeforeInvocation(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootServices* bootServices,
        ulong codeAddress,
        bool codeAllocated,
        ulong dataAddress,
        bool dataAllocated)
    {
        if (bootServices != null && bootServices->FreePages != null)
        {
            if (dataAllocated)
            {
                _ = bootServices->FreePages(
                    dataAddress,
                    M02IdtrContinuedExecutionDataPages);
            }
            if (codeAllocated &&
                (!dataAllocated ||
                    !M02IdtrContinuedExecutionRangesOverlap(
                        codeAddress,
                        M02IdtrContinuedExecutionCodeBytes,
                        dataAddress,
                        M02IdtrContinuedExecutionDataBytes)))
            {
                _ = bootServices->FreePages(
                    codeAddress,
                    M02IdtrContinuedExecutionCodePages);
            }
        }

        WriteM02IdtrContinuedExecutionFailureLines(console, serial);
        return false;
    }

    private static void SpinM02IdtrContinuedExecutionAfterUnverifiedRestore()
    {
        while (true)
        {
        }
    }

    private static void WriteM02IdtrContinuedExecutionStartLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'S', (byte)'T', (byte)'A');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'R', (byte)'T', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'1', (byte)'2', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02IdtrContinuedExecutionSuccessLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'C', (byte)'L', (byte)'R', (byte)'O', (byte)'S', (byte)'_', (byte)'M', (byte)'0');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'2', (byte)'_', (byte)'0', (byte)'1', (byte)'2', (byte)'_', (byte)'R', (byte)'E');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'A', (byte)'D', (byte)'Y', (byte)' ', (byte)'t', (byte)'a', (byte)'b', (byte)'l');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'e', (byte)'B', (byte)'y', (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'4');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'0', (byte)'9', (byte)'6', (byte)' ', (byte)'i', (byte)'d', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'B', (byte)'y', (byte)'t', (byte)'e', (byte)'s', (byte)'=', (byte)'1', (byte)'0');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)' ', (byte)'i', (byte)'d', (byte)'t', (byte)'r', (byte)'L', (byte)'i', (byte)'m');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'t', (byte)'=', (byte)'4', (byte)'0', (byte)'9', (byte)'5', (byte)' ');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'p', (byte)'o', (byte)'l', (byte)'i', (byte)'c', (byte)'y', (byte)'F', (byte)'n');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'v', (byte)'=', (byte)'8', (byte)'5', (byte)'1', (byte)'4', (byte)'F', (byte)'4');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'2', (byte)'E', (byte)'B', (byte)'D', (byte)'F', (byte)'C', (byte)'5', (byte)'D');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'5', (byte)'5', (byte)' ', (byte)'f', (byte)'i', (byte)'r', (byte)'m', (byte)'w');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'a', (byte)'r', (byte)'e', (byte)'G', (byte)'a', (byte)'t', (byte)'e', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'C', (byte)'o', (byte)'p', (byte)'i', (byte)'e', (byte)'d', (byte)'=', (byte)'0');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)' ', (byte)'f', (byte)'i', (byte)'r', (byte)'m', (byte)'w', (byte)'a', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'e', (byte)'I', (byte)'d', (byte)'t', (byte)'r', (byte)'C', (byte)'a', (byte)'p');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'t', (byte)'u', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'o', (byte)'w', (byte)'n', (byte)'e', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'I', (byte)'d', (byte)'t', (byte)'r', (byte)'L', (byte)'o', (byte)'a', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'o', (byte)'w', (byte)'n', (byte)'e', (byte)'d', (byte)'I', (byte)'d', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'r', (byte)'R', (byte)'e', (byte)'a', (byte)'d', (byte)'B', (byte)'a', (byte)'c');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'k', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'f');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'r', (byte)'m', (byte)'w', (byte)'a', (byte)'r', (byte)'e', (byte)'I');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'d', (byte)'t', (byte)'r', (byte)'R', (byte)'e', (byte)'s', (byte)'t', (byte)'o');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)' ', (byte)'r', (byte)'e', (byte)'s', (byte)'t', (byte)'o', (byte)'r', (byte)'e');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'R', (byte)'e', (byte)'a', (byte)'d', (byte)'B', (byte)'a', (byte)'c', (byte)'k');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'o', (byte)'w');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'n', (byte)'e', (byte)'d', (byte)'I', (byte)'d', (byte)'t', (byte)'r', (byte)'A');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'c', (byte)'t', (byte)'i', (byte)'v', (byte)'e', (byte)'=', (byte)'f', (byte)'a');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'c', (byte)'o', (byte)'n', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'n', (byte)'u', (byte)'e', (byte)'d', (byte)'E', (byte)'x', (byte)'e');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'c', (byte)'u', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'=', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'c', (byte)'l', (byte)'i', (byte)'W');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'n', (byte)'d', (byte)'o', (byte)'w', (byte)'=', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'i', (byte)'f', (byte)'R', (byte)'e', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'t', (byte)'o', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'e', (byte)' ', (byte)'i', (byte)'n', (byte)'s', (byte)'t', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'c', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'P', (byte)'u');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'b', (byte)'l', (byte)'i', (byte)'s', (byte)'h', (byte)'e', (byte)'d', (byte)'=');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'a', (byte)'l', (byte)'l');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'o', (byte)'c', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'R', (byte)'e', (byte)'l', (byte)'e', (byte)'a', (byte)'s', (byte)'e', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'=', (byte)'t', (byte)'r', (byte)'u', (byte)'e', (byte)' ', (byte)'w', (byte)'x');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'E', (byte)'n', (byte)'f', (byte)'o', (byte)'r', (byte)'c', (byte)'e', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'i');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'n', (byte)'t', (byte)'e', (byte)'r', (byte)'r', (byte)'u', (byte)'p', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'r', (byte)'i', (byte)'g', (byte)'g', (byte)'e', (byte)'r', (byte)'e');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'d', (byte)'=', (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'f', (byte)'a', (byte)'u', (byte)'l', (byte)'t', (byte)'T', (byte)'r', (byte)'i');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'g', (byte)'g', (byte)'e', (byte)'r', (byte)'e', (byte)'d', (byte)'=', (byte)'f');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'a', (byte)'l', (byte)'s', (byte)'e', (byte)' ', (byte)'m', (byte)'a', (byte)'r');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'k', (byte)'e', (byte)'r', (byte)'T', (byte)'r', (byte)'a', (byte)'n', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'p', (byte)'o', (byte)'r', (byte)'t', (byte)'=', (byte)'u', (byte)'e', (byte)'f');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'S', (byte)'e', (byte)'r', (byte)'i', (byte)'a', (byte)'l', (byte)' ');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'p', (byte)'o', (byte)'s', (byte)'t', (byte)'E', (byte)'x', (byte)'i', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'S', (byte)'e', (byte)'r', (byte)'v');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'i', (byte)'c', (byte)'e', (byte)'s', (byte)'=', (byte)'f', (byte)'a', (byte)'l');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'s', (byte)'e', 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02IdtrContinuedExecutionPassLine(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'P', (byte)'A', (byte)'S');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'S', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'2', 0, 0, 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02IdtrContinuedExecutionFailureLines(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'M', (byte)'0', (byte)'2', (byte)'_', (byte)'I', (byte)'D', (byte)'T', (byte)'R');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'_', (byte)'C', (byte)'O', (byte)'N', (byte)'T', (byte)'I', (byte)'N', (byte)'U');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'E', (byte)'D', (byte)'_', (byte)'E', (byte)'X', (byte)'E', (byte)'C', (byte)'U');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'I', (byte)'O', (byte)'N', (byte)'_', (byte)'F', (byte)'A', (byte)'I');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'L', (byte)'E', (byte)'D', 0, 0, 0, 0, 0);
        WriteCrLf(console, serial);
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'T', (byte)'E', (byte)'S', (byte)'T', (byte)'_', (byte)'F', (byte)'A', (byte)'I');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'L', (byte)' ', (byte)'m', (byte)'0', (byte)'2', (byte)'.', (byte)'0', (byte)'1');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'2', (byte)' ', (byte)'i', (byte)'d', (byte)'t', (byte)'r', (byte)'_', (byte)'l');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'o', (byte)'a', (byte)'d', (byte)'_', (byte)'r', (byte)'e', (byte)'a', (byte)'d');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'b', (byte)'a', (byte)'c', (byte)'k', (byte)'_', (byte)'r', (byte)'e', (byte)'s');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'t', (byte)'o', (byte)'r', (byte)'e', (byte)'_', (byte)'r', (byte)'e', (byte)'t');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'u', (byte)'r', (byte)'n', (byte)'_', (byte)'o', (byte)'r', (byte)'_', (byte)'c');
        WriteM02IdtrContinuedExecutionAscii(console, serial, (byte)'l', (byte)'e', (byte)'a', (byte)'n', (byte)'u', (byte)'p', 0, 0);
        WriteCrLf(console, serial);
    }

    private static void WriteM02IdtrContinuedExecutionAscii(
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
        if (b0 != 0) Write(console, serial, b0);
        if (b1 != 0) Write(console, serial, b1);
        if (b2 != 0) Write(console, serial, b2);
        if (b3 != 0) Write(console, serial, b3);
        if (b4 != 0) Write(console, serial, b4);
        if (b5 != 0) Write(console, serial, b5);
        if (b6 != 0) Write(console, serial, b6);
        if (b7 != 0) Write(console, serial, b7);
    }

    private unsafe struct M02IdtrContinuedExecutionStackWorkspace
    {
        public fixed byte Bytes[96];
    }
#endif


    private static void WriteUefiMemoryMapReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapFail(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapDescriptorCount(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong descriptorCount)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, descriptorCount);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapOwned(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'W');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapNormalized(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'Z');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryMapAccepted(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryMapRejected(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        BootMemoryMapAcceptanceStatus status)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'J');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static bool MaterializeAndWriteBootMemoryRegions(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        fixed (ulong* regionSlots = &s_ownedMemoryRegions.Slots[0])
        {
            s_memoryRegionMaterialization = BootMemoryRegionMaterializer.Materialize(
                s_memoryMapSnapshot.Buffer,
                s_memoryMapSnapshot.MapSize,
                s_memoryMapSnapshot.DescriptorSize,
                s_memoryMapSnapshot.DescriptorVersion,
                s_memoryMapSnapshot.DescriptorCount,
                (BootMemoryRegionRecord*)regionSlots,
                OwnedMemoryRegionCapacity);
        }

        if (!s_memoryRegionMaterialization.Succeeded)
        {
            WriteBootMemoryRegionsFail(
                console,
                serial,
                s_memoryRegionMaterialization.Status);
            return false;
        }

        WriteBootMemoryRegionsReady(console, serial);
        WriteBootMemoryRegionCount(console, serial, s_memoryRegionMaterialization.RegionCount);
        WriteBootPageAllocatorReadiness(console, serial);
        return true;
    }

    private static void WriteBootMemoryRegionsReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryRegionCount(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong regionCount)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, regionCount);
        WriteCrLf(console, serial);
    }

    private static void WriteBootMemoryRegionsFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        BootMemoryRegionMaterializationStatus status)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootPageAllocatorReadiness(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        s_bootPageAllocatorReadiness = PlanBootPageAllocatorReadiness();
        if (s_bootPageAllocatorReadiness.IsReady)
        {
            WriteBootPageAllocatorReadyPrefix(console, serial);
            WriteReadyStatus(console, serial);
        }
        else
        {
            WriteBootPageAllocatorNotReadyPrefix(console, serial);
            if (s_bootPageAllocatorReadiness.Status == EfiBootPageAllocatorReadinessStatus.NoConventionalMemory)
            {
                WriteNoConventionalMemoryStatus(console, serial);
            }
            else
            {
                WriteLiveAllocatorNotInstalledStatus(console, serial);
            }
        }

        WriteBootPageAllocatorReadinessMetadata(console, serial, s_bootPageAllocatorReadiness);
    }

    private static EfiBootPageAllocatorReadinessResult PlanBootPageAllocatorReadiness()
    {
        EfiBootPageAllocatorReadinessResult result = default;
        result.Status = EfiBootPageAllocatorReadinessStatus.MaterializationMissing;
        result.RegionCount = s_memoryRegionMaterialization.RegionCount;
        result.ConventionalPages = s_memoryRegionMaterialization.ConventionalMemoryPages;
        result.ConventionalBytes = s_memoryRegionMaterialization.ConventionalMemoryBytes;
        result.PageSize = BootMemoryRegionRecord.PageSize;

        if (!s_memoryRegionMaterialization.Succeeded)
        {
            return result;
        }

        if (s_memoryRegionMaterialization.RegionCount == 0
            || s_memoryRegionMaterialization.RegionCount > OwnedMemoryRegionCapacity)
        {
            result.Status = EfiBootPageAllocatorReadinessStatus.InvalidRegionCount;
            return result;
        }

        fixed (ulong* regionSlots = &s_ownedMemoryRegions.Slots[0])
        fixed (ulong* nodeSlots = &s_bootPageAllocatorNodes.Slots[0])
        {
            BootMemoryRegionRecord* regions = (BootMemoryRegionRecord*)regionSlots;
            ulong previousStart = 0;
            ulong previousEndExclusive = 0;
            int nodeCount = 0;
            ulong freePages = 0;
            ulong freeBytes = 0;

            for (ulong regionIndex = 0; regionIndex < s_memoryRegionMaterialization.RegionCount; regionIndex++)
            {
                BootMemoryRegionRecord region = regions[(int)regionIndex];
                EfiBootPageAllocatorReadinessStatus regionStatus = ValidateBootAllocatorRegion(
                    region,
                    regionIndex,
                    previousStart,
                    previousEndExclusive,
                    out ulong byteLength,
                    out ulong physicalEndExclusive);
                if (regionStatus != EfiBootPageAllocatorReadinessStatus.Ready)
                {
                    result.Status = regionStatus;
                    result.FreeNodeCount = nodeCount;
                    result.FreePages = freePages;
                    result.FreeBytes = freeBytes;
                    return result;
                }

                if (region.UefiType == UefiConventionalMemoryType)
                {
                    if (nodeCount >= BootPageAllocatorNodeCapacity)
                    {
                        result.Status = EfiBootPageAllocatorReadinessStatus.NodeCapacityTooSmall;
                        result.FreeNodeCount = nodeCount;
                        result.FreePages = freePages;
                        result.FreeBytes = freeBytes;
                        return result;
                    }

                    if (nodeCount != 0)
                    {
                        SetBootPageAllocatorNodeNext(nodeSlots, nodeCount - 1, (ulong)nodeCount);
                    }

                    WriteBootPageAllocatorNodeUnchecked(
                        nodeSlots,
                        nodeCount,
                        regionIndex,
                        region.PhysicalStart,
                        region.PhysicalEndExclusive,
                        region.PageCount,
                        byteLength,
                        BootPageAllocatorInvalidNodeIndex);
                    nodeCount++;

                    if (!TryAdd(freePages, region.PageCount, out ulong nextFreePages)
                        || !TryAdd(freeBytes, byteLength, out ulong nextFreeBytes))
                    {
                        result.Status = EfiBootPageAllocatorReadinessStatus.TotalMismatch;
                        result.FreeNodeCount = nodeCount;
                        result.FreePages = freePages;
                        result.FreeBytes = freeBytes;
                        return result;
                    }

                    freePages = nextFreePages;
                    freeBytes = nextFreeBytes;
                }

                previousStart = region.PhysicalStart;
                previousEndExclusive = physicalEndExclusive;
            }

            result.FreeNodeCount = nodeCount;
            result.FreePages = freePages;
            result.FreeBytes = freeBytes;

            if (nodeCount == 0)
            {
                result.Status = EfiBootPageAllocatorReadinessStatus.NoConventionalMemory;
                return result;
            }

            if (freePages == 0)
            {
                result.Status = EfiBootPageAllocatorReadinessStatus.NoFreePages;
                return result;
            }

            result.Status = ValidateBootPageAllocatorNodeChain(nodeSlots, nodeCount, freePages, freeBytes);
            result.IsReady = result.Status == EfiBootPageAllocatorReadinessStatus.Ready;
            return result;
        }
    }

    private static EfiBootPageAllocatorReadinessStatus ValidateBootAllocatorRegion(
        BootMemoryRegionRecord region,
        ulong regionIndex,
        ulong previousStart,
        ulong previousEndExclusive,
        out ulong byteLength,
        out ulong physicalEndExclusive)
    {
        byteLength = 0;
        physicalEndExclusive = 0;

        if (region.PageCount == 0
            || region.PageCount > 0xffffffffffffffff / BootMemoryRegionRecord.PageSize)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        byteLength = region.PageCount * BootMemoryRegionRecord.PageSize;
        if (region.ByteLength != byteLength)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        if (!TryAdd(region.PhysicalStart, byteLength, out physicalEndExclusive)
            || region.PhysicalEndExclusive != physicalEndExclusive)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        if (regionIndex != 0)
        {
            if (region.PhysicalStart < previousStart)
            {
                return EfiBootPageAllocatorReadinessStatus.UnsortedRegion;
            }

            if (region.PhysicalStart < previousEndExclusive)
            {
                return EfiBootPageAllocatorReadinessStatus.OverlappingRegion;
            }
        }

        if (region.UefiType == UefiConventionalMemoryType
            && (region.PhysicalStart & (BootMemoryRegionRecord.PageSize - 1)) != 0)
        {
            return EfiBootPageAllocatorReadinessStatus.InvalidRegionShape;
        }

        return EfiBootPageAllocatorReadinessStatus.Ready;
    }

    private static EfiBootPageAllocatorReadinessStatus ValidateBootPageAllocatorNodeChain(
        ulong* nodeSlots,
        int nodeCount,
        ulong expectedFreePages,
        ulong expectedFreeBytes)
    {
        int currentNodeIndex = 0;
        int visitedNodeCount = 0;
        ulong totalPages = 0;
        ulong totalBytes = 0;

        while (currentNodeIndex != -1)
        {
            if (currentNodeIndex < 0 || currentNodeIndex >= nodeCount || visitedNodeCount >= nodeCount)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            ulong physicalStart = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodePhysicalStartSlot);
            ulong physicalEndExclusive = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodePhysicalEndExclusiveSlot);
            ulong pageCount = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodePageCountSlot);
            ulong byteLength = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodeByteLengthSlot);
            ulong nextNodeIndex = GetBootPageAllocatorNodeSlot(
                nodeSlots,
                currentNodeIndex,
                BootPageAllocatorNodeNextIndexSlot);

            if (pageCount == 0
                || (physicalStart & (BootMemoryRegionRecord.PageSize - 1)) != 0
                || pageCount > 0xffffffffffffffff / BootMemoryRegionRecord.PageSize)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            ulong calculatedByteLength = pageCount * BootMemoryRegionRecord.PageSize;
            if (byteLength != calculatedByteLength)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            if (!TryAdd(physicalStart, byteLength, out ulong calculatedEnd)
                || physicalEndExclusive != calculatedEnd)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }

            if (!TryAdd(totalPages, pageCount, out ulong nextTotalPages)
                || !TryAdd(totalBytes, byteLength, out ulong nextTotalBytes))
            {
                return EfiBootPageAllocatorReadinessStatus.TotalMismatch;
            }

            totalPages = nextTotalPages;
            totalBytes = nextTotalBytes;
            visitedNodeCount++;

            if (nextNodeIndex == BootPageAllocatorInvalidNodeIndex)
            {
                currentNodeIndex = -1;
            }
            else if (nextNodeIndex >= (ulong)nodeCount || nextNodeIndex == (ulong)currentNodeIndex)
            {
                return EfiBootPageAllocatorReadinessStatus.NodeValidationFailed;
            }
            else
            {
                currentNodeIndex = (int)nextNodeIndex;
            }
        }

        if (visitedNodeCount != nodeCount)
        {
            return EfiBootPageAllocatorReadinessStatus.NodeCountMismatch;
        }

        return totalPages == expectedFreePages && totalBytes == expectedFreeBytes
            ? EfiBootPageAllocatorReadinessStatus.Ready
            : EfiBootPageAllocatorReadinessStatus.TotalMismatch;
    }

    private static void WriteBootPageAllocatorNodeUnchecked(
        ulong* nodeSlots,
        int nodeIndex,
        ulong sourceRegionIndex,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        ulong nextNodeIndex)
    {
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeSourceRegionIndexSlot, sourceRegionIndex);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodePhysicalStartSlot, physicalStart);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodePhysicalEndExclusiveSlot, physicalEndExclusive);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodePageCountSlot, pageCount);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeByteLengthSlot, byteLength);
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeNextIndexSlot, nextNodeIndex);
    }

    private static void SetBootPageAllocatorNodeNext(ulong* nodeSlots, int nodeIndex, ulong nextNodeIndex)
    {
        SetBootPageAllocatorNodeSlot(nodeSlots, nodeIndex, BootPageAllocatorNodeNextIndexSlot, nextNodeIndex);
    }

    private static void SetBootPageAllocatorNodeSlot(
        ulong* nodeSlots,
        int nodeIndex,
        int slotIndex,
        ulong value)
    {
        nodeSlots[(nodeIndex * BootPageAllocatorNodeSlotCount) + slotIndex] = value;
    }

    private static ulong GetBootPageAllocatorNodeSlot(ulong* nodeSlots, int nodeIndex, int slotIndex)
    {
        return nodeSlots[(nodeIndex * BootPageAllocatorNodeSlotCount) + slotIndex];
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > 0xffffffffffffffff - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }

    private static void WriteBootPageAllocatorReadinessMetadata(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootPageAllocatorReadinessResult readiness)
    {
        Write(console, serial, (byte)' ');
        WriteRegionsField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.RegionCount);
        Write(console, serial, (byte)' ');
        WriteConventionalPagesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.ConventionalPages);
        Write(console, serial, (byte)' ');
        WriteConventionalBytesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.ConventionalBytes);
        Write(console, serial, (byte)' ');
        WriteFreeNodesField(console, serial);
        WriteUnsignedDecimal(console, serial, (ulong)readiness.FreeNodeCount);
        Write(console, serial, (byte)' ');
        WriteFreePagesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.FreePages);
        Write(console, serial, (byte)' ');
        WriteFreeBytesField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.FreeBytes);
        Write(console, serial, (byte)' ');
        WritePageSizeField(console, serial);
        WriteUnsignedDecimal(console, serial, readiness.PageSize);
        Write(console, serial, (byte)' ');
        WriteStatusCodeField(console, serial);
        WriteUnsignedDecimal(console, serial, (ulong)readiness.Status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootPageAllocatorReadyPrefix(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)' ');
        WriteStatusField(console, serial);
    }

    private static void WriteReadyStatus(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'d');
        Write(console, serial, (byte)'y');
    }

    private static void WriteBootPageAllocatorNotReadyPrefix(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)' ');
        WriteStatusField(console, serial);
    }

    private static void WriteStatusField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'u');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteStatusCodeField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'u');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'d');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'=');
    }

    private static void WriteLiveAllocatorNotInstalledStatus(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'c');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'d');
    }

    private static void WriteNoConventionalMemoryStatus(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'m');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'y');
    }

    private static void WriteRegionsField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteConventionalPagesField(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'c');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteConventionalBytesField(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'c');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'v');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'n');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'l');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'y');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteFreeNodesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'f');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'o');
        Write(console, serial, (byte)'d');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteFreePagesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'f');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WriteFreeBytesField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'f');
        Write(console, serial, (byte)'r');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'y');
        Write(console, serial, (byte)'t');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'s');
        Write(console, serial, (byte)'=');
    }

    private static void WritePageSizeField(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'p');
        Write(console, serial, (byte)'a');
        Write(console, serial, (byte)'g');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'i');
        Write(console, serial, (byte)'z');
        Write(console, serial, (byte)'e');
        Write(console, serial, (byte)'=');
    }

    private static bool ScanAndWriteAcpiRsdp(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        if (systemTable == null)
        {
            s_acpiRsdpScanResult = EfiConfigurationTableScanResult.Fail(
                EfiConfigurationTableScanStatus.InvalidConfigurationTableAddress);
            s_bootHandoff.ClearAcpiRsdp((uint)s_acpiRsdpScanResult.Status);
            WriteUefiAcpiRsdpInvalid(console, serial, s_acpiRsdpScanResult.Status);
            return true;
        }

        s_acpiRsdpScanResult = EfiConfigurationTableScanner.Scan(
            systemTable->ConfigurationTable,
            systemTable->NumberOfTableEntries);

        if (s_acpiRsdpScanResult.Succeeded)
        {
            s_bootHandoff.SetAcpiRsdp(
                s_acpiRsdpScanResult.RsdpAddress,
                (uint)s_acpiRsdpScanResult.TableKind,
                (uint)s_acpiRsdpScanResult.Status);

            EfiBootHandoffStatus handoffStatus = EfiBootHandoffValidator.Validate(s_bootHandoff);
            if (handoffStatus != EfiBootHandoffStatus.Success)
            {
                WriteBootHandoffFail(console, serial, handoffStatus);
                return false;
            }

            WriteBootAcpiRsdpHandoffReady(console, serial);
            WriteUefiAcpiRsdpReady(console, serial);
            return true;
        }

        s_bootHandoff.ClearAcpiRsdp((uint)s_acpiRsdpScanResult.Status);
        if (s_acpiRsdpScanResult.Status == EfiConfigurationTableScanStatus.NotFound)
        {
            WriteUefiAcpiRsdpMissing(console, serial);
            return true;
        }

        WriteUefiAcpiRsdpInvalid(console, serial, s_acpiRsdpScanResult.Status);
        return true;
    }

    private static void WriteBootAcpiRsdpHandoffReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiAcpiRsdpReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiAcpiRsdpMissing(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'G');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiAcpiRsdpInvalid(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiConfigurationTableScanStatus status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

#if ACPI_PHYSICAL_DISCOVERY_PROBE
    private static bool WriteAcpiPhysicalDiscoveryProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial)
    {
        if (!TryReadAndValidateAcpiRsdp(
            console,
            serial,
            out ulong rsdtAddress,
            out ulong xsdtAddress,
            out bool isAcpi20,
            out ulong rsdpStatus))
        {
            WriteAcpiDiscoveryFail(console, serial, 1, rsdpStatus);
            return false;
        }

        WriteAcpiRsdpValidated(console, serial);

        if (!TryReadAndValidateRootSdt(
            console,
            serial,
            rsdtAddress,
            xsdtAddress,
            isAcpi20,
            out int rootTableLength,
            out int rootPointerCount,
            out ulong rootStatus))
        {
            WriteAcpiDiscoveryFail(console, serial, 2, rootStatus);
            return false;
        }

        WriteAcpiRootSdtValidated(console, serial);

        if (!TryDiscoverMcfg(rootTableLength, rootPointerCount, out ulong mcfgStatus))
        {
            WriteAcpiDiscoveryFail(console, serial, 3, mcfgStatus);
            return false;
        }

        WriteAcpiMcfgDiscoveryReady(console, serial);
        return true;
    }

    private static bool TryReadAndValidateAcpiRsdp(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        out ulong rsdtAddress,
        out ulong xsdtAddress,
        out bool isAcpi20,
        out ulong status)
    {
        rsdtAddress = 0;
        xsdtAddress = 0;
        isAcpi20 = false;
        status = 0;

        if (!s_acpiRsdpScanResult.Succeeded || s_acpiRsdpScanResult.RsdpAddress == 0)
        {
            status = 10 + (ulong)s_acpiRsdpScanResult.Status;
            return false;
        }

        fixed (byte* rsdp = &s_acpiRsdpScratch.Bytes[0])
        {
            if (!TryReadAcpiPhysicalBytes(
                s_acpiRsdpScanResult.RsdpAddress,
                20,
                rsdp,
                AcpiRsdpScratchSize))
            {
                status = 20;
                return false;
            }

            WriteAcpiRsdpSnapshotReady(console, serial);

            if (!HasRsdpSignature(rsdp))
            {
                status = 21;
                return false;
            }

            if (HasZeroOemId(rsdp))
            {
                status = 22;
                return false;
            }

            if (!IsChecksumValid(rsdp, 20))
            {
                status = 23;
                return false;
            }

            byte revision = rsdp[15];
            if (revision == 0)
            {
                rsdtAddress = ReadUInt32LittleEndian(rsdp + 16);
                return true;
            }

            if (revision == 1)
            {
                status = 24;
                return false;
            }

            if (!TryReadAcpiPhysicalBytes(
                s_acpiRsdpScanResult.RsdpAddress,
                36,
                rsdp,
                AcpiRsdpScratchSize))
            {
                status = 25;
                return false;
            }

            uint declaredLength = ReadUInt32LittleEndian(rsdp + 20);
            if (declaredLength < 36 || declaredLength > AcpiRsdpScratchSize)
            {
                status = 26;
                return false;
            }

            if (!TryReadAcpiPhysicalBytes(
                s_acpiRsdpScanResult.RsdpAddress,
                (int)declaredLength,
                rsdp,
                AcpiRsdpScratchSize))
            {
                status = 27;
                return false;
            }

            if (!IsChecksumValid(rsdp, (int)declaredLength))
            {
                status = 28;
                return false;
            }

            rsdtAddress = ReadUInt32LittleEndian(rsdp + 16);
            xsdtAddress = ReadUInt64LittleEndian(rsdp + 24);
            isAcpi20 = true;
            return true;
        }
    }

    private static bool TryReadAndValidateRootSdt(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong rsdtAddress,
        ulong xsdtAddress,
        bool isAcpi20,
        out int rootTableLength,
        out int rootPointerCount,
        out ulong status)
    {
        rootTableLength = 0;
        rootPointerCount = 0;
        status = 0;

        bool useXsdt = isAcpi20 && xsdtAddress != 0;
        ulong rootAddress = useXsdt ? xsdtAddress : rsdtAddress;
        if (rootAddress == 0)
        {
            status = 30;
            return false;
        }

        fixed (byte* rootTable = &s_acpiRootSdt.Bytes[0])
        fixed (ulong* pointerScratch = &s_acpiPointerScratch.Slots[0])
        {
            if (!TryReadAcpiPhysicalBytes(
                rootAddress,
                AcpiSdtHeaderLength,
                rootTable,
                AcpiRootSdtBufferSize))
            {
                status = 31;
                return false;
            }

            uint signature = ReadUInt32LittleEndian(rootTable);
            uint expectedSignature = useXsdt ? AcpiXsdtSignature : AcpiRsdtSignature;
            if (signature != expectedSignature)
            {
                status = 32;
                return false;
            }

            uint declaredLength = ReadUInt32LittleEndian(rootTable + 4);
            if (declaredLength < AcpiSdtHeaderLength || declaredLength > AcpiRootSdtBufferSize)
            {
                status = 33;
                return false;
            }

            rootTableLength = (int)declaredLength;
            if (!TryReadAcpiPhysicalBytes(
                rootAddress,
                rootTableLength,
                rootTable,
                AcpiRootSdtBufferSize))
            {
                status = 34;
                return false;
            }

            WriteAcpiRootSdtSnapshotReady(console, serial);

            if (!IsChecksumValid(rootTable, rootTableLength))
            {
                status = 35;
                return false;
            }

            int pointerSize = useXsdt ? 8 : 4;
            int pointerBytes = rootTableLength - AcpiSdtHeaderLength;
            if ((pointerBytes % pointerSize) != 0)
            {
                status = 36;
                return false;
            }

            rootPointerCount = pointerBytes / pointerSize;
            if (rootPointerCount == 0 || rootPointerCount > AcpiPointerScratchCapacity)
            {
                status = 37;
                return false;
            }

            for (int index = 0; index < rootPointerCount; index++)
            {
                byte* pointerSource = rootTable + AcpiSdtHeaderLength + (index * pointerSize);
                ulong tableAddress = pointerSize == 8
                    ? ReadUInt64LittleEndian(pointerSource)
                    : ReadUInt32LittleEndian(pointerSource);
                if (tableAddress == 0)
                {
                    status = 38;
                    return false;
                }

                pointerScratch[index] = tableAddress;
            }

            return true;
        }
    }

    private static bool TryDiscoverMcfg(
        int rootTableLength,
        int rootPointerCount,
        out ulong status)
    {
        status = 0;
        if (rootTableLength < AcpiSdtHeaderLength || rootPointerCount <= 0)
        {
            status = 40;
            return false;
        }

        fixed (ulong* pointerScratch = &s_acpiPointerScratch.Slots[0])
        fixed (byte* childHeader = &s_acpiChildHeaderScratch.Bytes[0])
        fixed (byte* childTable = &s_acpiChildTable.Bytes[0])
        fixed (ulong* segmentScratch = &s_acpiEcamSegmentScratch.Slots[0])
        {
            for (int pointerIndex = 0; pointerIndex < rootPointerCount; pointerIndex++)
            {
                ulong tableAddress = pointerScratch[pointerIndex];
                if (!TryReadAcpiPhysicalBytes(
                    tableAddress,
                    AcpiChildHeaderScratchSize,
                    childHeader,
                    AcpiChildHeaderScratchSize))
                {
                    status = 41;
                    return false;
                }

                uint signature = ReadUInt32LittleEndian(childHeader);
                if (signature != AcpiMcfgSignature)
                {
                    continue;
                }

                uint declaredLength = ReadUInt32LittleEndian(childHeader + 4);
                if (declaredLength < AcpiSdtHeaderLength || declaredLength > AcpiChildTableBufferSize)
                {
                    status = 42;
                    return false;
                }

                int tableLength = (int)declaredLength;
                if (!TryReadAcpiPhysicalBytes(
                    tableAddress,
                    tableLength,
                    childTable,
                    AcpiChildTableBufferSize))
                {
                    status = 43;
                    return false;
                }

                if (!IsChecksumValid(childTable, tableLength))
                {
                    status = 44;
                    return false;
                }

                if (!TryParseMcfgSegments(childTable, tableLength, segmentScratch, out status))
                {
                    return false;
                }

                return true;
            }
        }

        status = 45;
        return false;
    }

    private static bool TryParseMcfgSegments(
        byte* table,
        int tableLength,
        ulong* segmentScratch,
        out ulong status)
    {
        status = 0;
        if (tableLength < AcpiMcfgAllocationOffset + AcpiMcfgAllocationLength)
        {
            status = 50;
            return false;
        }

        for (int index = 0; index < 8; index++)
        {
            if (table[AcpiSdtHeaderLength + index] != 0)
            {
                status = 51;
                return false;
            }
        }

        int allocationBytes = tableLength - AcpiMcfgAllocationOffset;
        if ((allocationBytes % AcpiMcfgAllocationLength) != 0)
        {
            status = 52;
            return false;
        }

        int segmentCount = allocationBytes / AcpiMcfgAllocationLength;
        if (segmentCount == 0 || segmentCount > AcpiEcamSegmentScratchCapacity)
        {
            status = 53;
            return false;
        }

        for (int index = 0; index < segmentCount; index++)
        {
            byte* entry = table + AcpiMcfgAllocationOffset + (index * AcpiMcfgAllocationLength);
            byte startBus = entry[10];
            byte endBus = entry[11];
            if (startBus > endBus)
            {
                status = 54;
                return false;
            }

            int slot = index * 4;
            segmentScratch[slot] = ReadUInt64LittleEndian(entry);
            segmentScratch[slot + 1] = ReadUInt16LittleEndian(entry + 8);
            segmentScratch[slot + 2] = startBus;
            segmentScratch[slot + 3] = endBus;
        }

        return true;
    }

    private static bool TryReadAcpiPhysicalBytes(
        ulong physicalAddress,
        int byteCount,
        byte* destination,
        int destinationCapacity)
    {
        if (physicalAddress == 0 || byteCount <= 0 || destination == null)
        {
            return false;
        }

        if (destinationCapacity < byteCount)
        {
            return false;
        }

        ulong byteLength = (ulong)byteCount;
        if (physicalAddress > 0xffffffffffffffff - byteLength)
        {
            return false;
        }

        if (!IsAcpiPhysicalRangeAllowed(physicalAddress, byteLength))
        {
            return false;
        }

        byte* source = (byte*)physicalAddress;
        for (int index = 0; index < byteCount; index++)
        {
            destination[index] = source[index];
        }

        return true;
    }

    private static bool IsAcpiPhysicalRangeAllowed(ulong physicalAddress, ulong byteLength)
    {
        if (!s_memoryRegionMaterialization.Succeeded || byteLength == 0)
        {
            return false;
        }

        ulong endExclusive = physicalAddress + byteLength;
        if (endExclusive <= physicalAddress)
        {
            return false;
        }

        fixed (ulong* regionSlots = &s_ownedMemoryRegions.Slots[0])
        {
            BootMemoryRegionRecord* regions = (BootMemoryRegionRecord*)regionSlots;
            for (ulong regionIndex = 0; regionIndex < s_memoryRegionMaterialization.RegionCount; regionIndex++)
            {
                BootMemoryRegionRecord region = regions[(int)regionIndex];
                if ((region.UefiType == UefiAcpiReclaimMemoryType || region.UefiType == UefiAcpiMemoryNvsType)
                    && physicalAddress >= region.PhysicalStart
                    && endExclusive <= region.PhysicalEndExclusive)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasRsdpSignature(byte* source)
    {
        return source[0] == (byte)'R'
            && source[1] == (byte)'S'
            && source[2] == (byte)'D'
            && source[3] == (byte)' '
            && source[4] == (byte)'P'
            && source[5] == (byte)'T'
            && source[6] == (byte)'R'
            && source[7] == (byte)' ';
    }

    private static bool HasZeroOemId(byte* source)
    {
        for (int index = 0; index < 6; index++)
        {
            if (source[9 + index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsChecksumValid(byte* source, int byteCount)
    {
        uint sum = 0;
        for (int index = 0; index < byteCount; index++)
        {
            sum += source[index];
        }

        return (sum & 0xff) == 0;
    }

    private static ushort ReadUInt16LittleEndian(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static uint ReadUInt32LittleEndian(byte* source)
    {
        return source[0]
            | ((uint)source[1] << 8)
            | ((uint)source[2] << 16)
            | ((uint)source[3] << 24);
    }

    private static ulong ReadUInt64LittleEndian(byte* source)
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

    private static void WriteAcpiRsdpSnapshotReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRsdp(console, serial);
        WriteUnderscore(console, serial);
        WriteSnapshotReady(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiRsdpValidated(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRsdp(console, serial);
        WriteUnderscore(console, serial);
        WriteValidated(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiRootSdtSnapshotReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRootSdt(console, serial);
        WriteUnderscore(console, serial);
        WriteSnapshotReady(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiRootSdtValidated(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        WriteRootSdt(console, serial);
        WriteUnderscore(console, serial);
        WriteValidated(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiMcfgDiscoveryReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteAcpiPrefix(console, serial);
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'G');
        WriteUnderscore(console, serial);
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        WriteUnderscore(console, serial);
        WriteReady(console, serial);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiDiscoveryFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong stage,
        ulong status)
    {
        WriteAcpiPrefix(console, serial);
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        WriteUnderscore(console, serial);
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, stage);
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, status);
        WriteCrLf(console, serial);
    }

    private static void WriteAcpiPrefix(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'I');
        WriteUnderscore(console, serial);
    }

    private static void WriteRsdp(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'P');
    }

    private static void WriteRootSdt(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        WriteUnderscore(console, serial);
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'T');
    }

    private static void WriteSnapshotReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        WriteUnderscore(console, serial);
        WriteReady(console, serial);
    }

    private static void WriteValidated(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'V');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
    }

    private static void WriteReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
    }

    private static void WriteUnderscore(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'_');
    }
#endif

    private static bool WriteExitBootServicesFreshMapProbe(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiSystemTable* systemTable)
    {
        WriteUefiEbsFreshMapProbeStart(console, serial);

        if (!TryCaptureUefiMemoryMap(systemTable))
        {
            WriteUefiEbsFreshMapFail(console, serial, (ulong)s_memoryMapCopyStatus);
            return false;
        }

        EfiMemoryMapNormalizeResult normalizeResult = EfiMemoryMapNormalizer.Normalize(
            s_memoryMapSnapshot.Buffer,
            s_memoryMapSnapshot.MapSize,
            s_memoryMapSnapshot.DescriptorSize,
            s_memoryMapSnapshot.DescriptorVersion,
            s_memoryMapSnapshot.DescriptorCount);
        if (!normalizeResult.Succeeded)
        {
            WriteUefiEbsFreshMapFail(console, serial, (ulong)normalizeResult.Status);
            return false;
        }

        UefiExitBootServicesPlanResult planResult = UefiExitBootServicesPlan.Evaluate(
            s_bootHandoff,
            freshMemoryMapCaptured: true,
            UefiExitBootServicesAttemptOutcome.ExitCallSkipped,
            attemptIndex: 0,
            maximumRetryCount: UefiExitBootServicesPlan.DefaultMaximumRetryCount);
        if (planResult.Status != UefiExitBootServicesPlanStatus.ExitCallSkipped)
        {
            WriteUefiEbsFreshMapFail(console, serial, (ulong)planResult.Status);
            return false;
        }

        WriteUefiEbsFreshMapReady(console, serial);
        WriteUefiEbsCallSkipped(console, serial);
        return true;
    }

    private static void WriteUefiEbsFreshMapProbeStart(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'T');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiEbsFreshMapReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiEbsFreshMapFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, status);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiEbsCallSkipped(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'K');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapNormalizeFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiMemoryMapNormalizeStatus status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'Z');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteBootHandoffReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteBootHandoffFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        EfiBootHandoffStatus status)
    {
        Write(console, serial, (byte)'B');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'H');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void WriteUefiMemoryMapCopyFail(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        UefiMemoryMapCopyPlanStatus status)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'Y');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)' ');
        WriteUnsignedDecimal(console, serial, (ulong)status);
        WriteCrLf(console, serial);
    }

    private static void
        WriteM01InstructionPublicationCookieFailure(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
    {
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'M',
            (byte)'0',
            (byte)'1',
            (byte)'_',
            (byte)'I',
            (byte)'N',
            (byte)'S',
            (byte)'T');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'R',
            (byte)'U',
            (byte)'C',
            (byte)'T',
            (byte)'I',
            (byte)'O',
            (byte)'N',
            (byte)'_');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'P',
            (byte)'U',
            (byte)'B',
            (byte)'L',
            (byte)'I',
            (byte)'C',
            (byte)'A',
            (byte)'T');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'I',
            (byte)'O',
            (byte)'N',
            (byte)'_',
            (byte)'C',
            (byte)'O',
            (byte)'O',
            (byte)'K');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'I',
            (byte)'E',
            (byte)'_',
            (byte)'F',
            (byte)'A',
            (byte)'I',
            (byte)'L',
            (byte)'E');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'D',
            0,
            0,
            0,
            0,
            0,
            0,
            0);
        WriteCrLf(console, serial);
    }

    private static void
        WriteM01InstructionPublicationEpochsVerified(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial)
    {
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'M',
            (byte)'0',
            (byte)'1',
            (byte)'_',
            (byte)'I',
            (byte)'N',
            (byte)'S',
            (byte)'T');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'R',
            (byte)'U',
            (byte)'C',
            (byte)'T',
            (byte)'I',
            (byte)'O',
            (byte)'N',
            (byte)'_');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'P',
            (byte)'U',
            (byte)'B',
            (byte)'L',
            (byte)'I',
            (byte)'C',
            (byte)'A',
            (byte)'T');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'I',
            (byte)'O',
            (byte)'N',
            (byte)'_',
            (byte)'E',
            (byte)'P',
            (byte)'O',
            (byte)'C');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'H',
            (byte)'S',
            (byte)'_',
            (byte)'V',
            (byte)'E',
            (byte)'R',
            (byte)'I',
            (byte)'F');
        WriteM01InstructionPublicationAscii(
            console,
            serial,
            (byte)'I',
            (byte)'E',
            (byte)'D',
            0,
            0,
            0,
            0,
            0);
        WriteCrLf(console, serial);
    }

    private static void WriteM01InstructionPublicationAscii(
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
        if (b0 != 0) Write(console, serial, b0);
        if (b1 != 0) Write(console, serial, b1);
        if (b2 != 0) Write(console, serial, b2);
        if (b3 != 0) Write(console, serial, b3);
        if (b4 != 0) Write(console, serial, b4);
        if (b5 != 0) Write(console, serial, b5);
        if (b6 != 0) Write(console, serial, b6);
        if (b7 != 0) Write(console, serial, b7);
    }

    private static void WriteBootSequencePlan(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            int length = BootSequencePlan.GetLineLength(lineIndex);
            for (int offset = 0; offset < length; offset++)
            {
                Write(console, serial, BootSequencePlan.GetLineByte(lineIndex, offset));
            }

            WriteCrLf(console, serial);
        }
    }

#if UEFI_GONE_SIMULATION_PROBE
    private static void WriteUefiGoneSimulation(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        WriteUefiGoneSimulationStart(console, serial);
        WriteUefiGoneSimulationPlatformDenied(console, serial);
        WritePostFirmwareDiagnosticsReady(console, serial);
        WriteUefiGoneSimulationPass(console, serial);
    }

    private static void WriteUefiGoneSimulationStart(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'T');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiGoneSimulationPlatformDenied(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'L');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'D');
        WriteCrLf(console, serial);
    }

    private static void WritePostFirmwareDiagnosticsReady(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'W');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'T');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'C');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'R');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'D');
        Write(console, serial, (byte)'Y');
        WriteCrLf(console, serial);
    }

    private static void WriteUefiGoneSimulationPass(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        Write(console, serial, (byte)'U');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'F');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'G');
        Write(console, serial, (byte)'O');
        Write(console, serial, (byte)'N');
        Write(console, serial, (byte)'E');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'I');
        Write(console, serial, (byte)'M');
        Write(console, serial, (byte)'_');
        Write(console, serial, (byte)'P');
        Write(console, serial, (byte)'A');
        Write(console, serial, (byte)'S');
        Write(console, serial, (byte)'S');
        WriteCrLf(console, serial);
    }
#endif

    private static bool TryCaptureUefiMemoryMap(EfiSystemTable* systemTable)
    {
        s_memoryMapCopyStatus = UefiMemoryMapCopyPlanStatus.Success;

        if (systemTable == null || systemTable->BootServices == null)
        {
            return false;
        }

        EfiBootServices* bootServices = systemTable->BootServices;
        if (bootServices->GetMemoryMap == null || bootServices->AllocatePool == null)
        {
            return false;
        }

        UefiMemoryMapCaptureResult captureResult;
        fixed (byte* ownedMemoryMapBytes = &s_ownedMemoryMap.Bytes[0])
        {
            captureResult = UefiMemoryMapCapture.Capture(
                bootServices->GetMemoryMap,
                bootServices->AllocatePool,
                ownedMemoryMapBytes,
                OwnedMemoryMapBufferSize);
        }

        if (!captureResult.Succeeded)
        {
            s_memoryMapCopyStatus = captureResult.CopyPlanStatus;
            return false;
        }

        FirmwareMemoryMapRecord sourceRecord = captureResult.SourceRecord;
        FirmwareMemoryMapRecord ownedRecord = captureResult.OwnedRecord;
        s_memoryMapSnapshot.Buffer = (void*)ownedRecord.BufferAddress;
        s_memoryMapSnapshot.BufferSize = ownedRecord.BufferSize;
        s_memoryMapSnapshot.SourceBuffer = (void*)sourceRecord.BufferAddress;
        s_memoryMapSnapshot.SourceBufferSize = sourceRecord.BufferSize;
        s_memoryMapSnapshot.MapSize = ownedRecord.MapSize;
        s_memoryMapSnapshot.MapKey = ownedRecord.MapKey;
        s_memoryMapSnapshot.DescriptorSize = ownedRecord.DescriptorSize;
        s_memoryMapSnapshot.DescriptorVersion = ownedRecord.DescriptorVersion;
        s_memoryMapSnapshot.DescriptorCount = ownedRecord.DescriptorCount;
        s_memoryMapSnapshot.IsOwned = true;
        s_bootHandoff = BootHandoffRecord.CreateUefiWithOwnedMemoryMap(
            sourceRecord.BufferAddress,
            sourceRecord.BufferSize,
            ownedRecord.BufferAddress,
            ownedRecord.BufferSize,
            ownedRecord.MapSize,
            ownedRecord.MapKey,
            ownedRecord.DescriptorSize,
            ownedRecord.DescriptorVersion,
            s_memoryMapSnapshot.DescriptorCount);

        return s_memoryMapSnapshot.DescriptorCount != 0;
    }

#if M01_LOADED_IMAGE_STACK_PROBE
    private static byte s_m01InstructionPublicationInitialized;
    private static byte s_m01InstructionPublicationEpochMask;

    private static class M01StageOneAdapter
    {
        private const ulong EfiSuccess = 0;
        private const uint AllocateAnyPages = 0;
        private const uint EfiLoaderCode = 1;
        private const uint EfiLoaderData = 2;
        private const ulong PageSize = 4096;
        private const ulong CodePageCount = 1;
        private const ulong GuardPageCount = 1;
        private const ulong UsableStackPageCount = 33;
        private const ulong DataArenaPageCount =
            GuardPageCount + UsableStackPageCount;
        private const ulong StubEntryOffset = 16;
        private const ulong StackProbeEntryOffset = 64;
        private const ulong StackProbeByteLength = 27;
        private const ulong CodeAllocationOrdinal = 1;
        private const ulong DataAllocationOrdinal = 2;
        private const ulong MapCaptureOrdinal = 3;
        private const ulong LedgerBuildOrdinal = 4;
        private const ulong InitialMapGeneration = 1;
        private const ulong RefreshedMapGeneration = 2;
        private const ulong ReservationCount = 4;
        private const ulong ReservationCapacity = 4;
        private const ulong NotAttemptedStatus = 0xffffffffffffffff;

        private static EfiSimpleTextOutputProtocol* s_console;
        private static EfiSerialIoProtocol* s_serial;
        private static EfiBootServices* s_bootServices;
        private static M01LoadedImageStackStaticState s_staticState;
        private static M01LoadedImageStackEvidence s_evidenceScratch;
        private static M01StageOneMemoryMapBuffer s_memoryMapSource;
        private static M01StageOneAllocationRecordBuffer
            s_allocationRecords;
        private static M01StageOneReservationLedgerBuffer
            s_reservationLedger;

        public static bool Run(
            void* imageHandle,
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiSystemTable* systemTable)
        {
            WriteSuccessLine(console, serial, 0);

            if (imageHandle == null
                || systemTable == null
                || systemTable->BootServices == null
                || systemTable->BootServices->HandleProtocol == null
                || systemTable->BootServices->AllocatePages == null
                || systemTable->BootServices->FreePages == null
                || systemTable->BootServices->GetMemoryMap == null
                || !EfiEntry.s_bootHandoff.MemoryMap.HasOwnedCopy())
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    systemTable == null
                        ? null
                        : systemTable->BootServices,
                    M01StageOneAdapterStatus.MissingFirmwareBoundary,
                    (ulong)M01LoadedImageStackStatus.Uninitialized,
                    (ulong)M01LoadedImageStackCheckpoint.Uninitialized,
                    0,
                    false,
                    0,
                    false);
            }

            EfiBootServices* bootServices = systemTable->BootServices;
            if (EfiEntry
                    .s_m01InstructionPublicationInitialized != 0
                || EfiEntry
                    .s_m01InstructionPublicationEpochMask != 0)
            {
                EfiEntry
                    .WriteM01InstructionPublicationCookieFailure(
                        console,
                        serial);
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus
                        .InstructionPublicationRejected,
                    (ulong)M01LoadedImageStackStatus
                        .Uninitialized,
                    (ulong)M01LoadedImageStackCheckpoint
                        .Uninitialized,
                    0,
                    false,
                    0,
                    false);
            }
            EfiEntry.s_m01InstructionPublicationEpochMask = 0;
            EfiEntry.s_m01InstructionPublicationInitialized = 1;

            M01StageOneAdapterStatus captureStatus =
                TryCaptureLoadedImage(
                    imageHandle,
                    systemTable,
                    bootServices,
                    out EfiLoadedImageRangeCapturePlan loadedImage,
                    out M01LoadedImageVirtualMappingEvidence
                        loadedImageMapping,
                    out ulong loadedImageFirmwareStatus);
            if (captureStatus != M01StageOneAdapterStatus.Success)
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    captureStatus,
                    captureStatus
                        == M01StageOneAdapterStatus
                            .LoadedImageProtocolFailed
                        ? loadedImageFirmwareStatus
                        : (ulong)M01LoadedImageStackStatus
                            .LoadedImageRejected,
                    (ulong)M01LoadedImageStackCheckpoint.Uninitialized,
                    0,
                    false,
                    0,
                    false);
            }

            ulong codeAddress = 0;
            ulong codeFirmwareStatus =
                bootServices->AllocatePages(
                    AllocateAnyPages,
                    EfiLoaderCode,
                    CodePageCount,
                    &codeAddress);
            bool codeAllocated =
                codeFirmwareStatus == EfiSuccess;
            if (codeFirmwareStatus != EfiSuccess
                || !TryValidateAllocation(
                    codeAddress,
                    CodePageCount,
                    out ulong codeByteLength,
                    out ulong codeEndExclusive))
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    codeFirmwareStatus == EfiSuccess
                        ? M01StageOneAdapterStatus
                            .InvalidCodeAllocation
                        : M01StageOneAdapterStatus
                            .CodeAllocationFailed,
                    codeFirmwareStatus,
                    (ulong)M01LoadedImageStackCheckpoint
                        .LoadedImageValidated,
                    codeAddress,
                    codeAllocated,
                    0,
                    false);
            }

            ulong dataAddress = 0;
            ulong dataFirmwareStatus =
                bootServices->AllocatePages(
                    AllocateAnyPages,
                    EfiLoaderData,
                    DataArenaPageCount,
                    &dataAddress);
            bool dataAllocated =
                dataFirmwareStatus == EfiSuccess;
            if (dataFirmwareStatus != EfiSuccess
                || !TryValidateAllocation(
                    dataAddress,
                    DataArenaPageCount,
                    out ulong dataByteLength,
                    out ulong dataEndExclusive))
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    dataFirmwareStatus == EfiSuccess
                        ? M01StageOneAdapterStatus
                            .InvalidDataAllocation
                        : M01StageOneAdapterStatus
                            .DataAllocationFailed,
                    dataFirmwareStatus,
                    (ulong)M01LoadedImageStackCheckpoint
                        .LoadedImageValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            if (RangesOverlap(
                    codeAddress,
                    codeEndExclusive,
                    dataAddress,
                    dataEndExclusive)
                || !TryGetReservationBounds(
                    loadedImage.Range.Reservation,
                    out ulong imageReservationStart,
                    out ulong imageReservationEnd)
                || RangesOverlap(
                    codeAddress,
                    codeEndExclusive,
                    imageReservationStart,
                    imageReservationEnd)
                || RangesOverlap(
                    dataAddress,
                    dataEndExclusive,
                    imageReservationStart,
                    imageReservationEnd))
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus.AllocationOverlap,
                    (ulong)M01LoadedImageStackStatus
                        .AllocationTableRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .LoadedImageValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            ulong previousMapKey =
                EfiEntry.s_bootHandoff.MemoryMap.MapKey;
            M01StageOneAdapterStatus mapStatus =
                TryRecaptureMemoryMap(
                    bootServices,
                    out FirmwareMemoryMapRecord refreshedMap,
                    out ulong memoryMapFirmwareStatus);
            if (mapStatus != M01StageOneAdapterStatus.Success
                || refreshedMap.MapKey == previousMapKey)
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    mapStatus != M01StageOneAdapterStatus.Success
                        ? mapStatus
                        : M01StageOneAdapterStatus
                            .MemoryMapKeyUnchanged,
                    mapStatus
                        == M01StageOneAdapterStatus
                            .MemoryMapRecaptureFailed
                        ? memoryMapFirmwareStatus
                        : (ulong)M01LoadedImageStackStatus
                            .MapLedgerEvidenceRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .AllocationEvidenceValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            if (!MapContainsAllocation(
                    refreshedMap,
                    codeAddress,
                    codeEndExclusive,
                    EfiLoaderCode)
                || !MapContainsAllocation(
                    refreshedMap,
                    dataAddress,
                    dataEndExclusive,
                    EfiLoaderData))
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus
                        .MemoryMapAllocationTypeMismatch,
                    (ulong)M01LoadedImageStackStatus
                        .MapLedgerEvidenceRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .AllocationEvidenceValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            M01StageOneAdapterStatus stackProbeStatus =
                MaterializeAndInstallStackProbe(
                    codeAddress,
                    codeByteLength);
            if (stackProbeStatus
                != M01StageOneAdapterStatus.Success)
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    stackProbeStatus,
                    (ulong)M01LoadedImageStackStatus
                        .SwitchPlanRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .MapLedgerValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            if (EfiEntry.s_m01InstructionPublicationEpochMask
                != 0)
            {
                EfiEntry
                    .WriteM01InstructionPublicationCookieFailure(
                        console,
                        serial);
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus
                        .InstructionPublicationRejected,
                    (ulong)M01LoadedImageStackStatus
                        .SwitchPlanRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .MapLedgerValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }
            uint epochOnePublicationCookie =
                global::OS.Boot.X64InstructionPublication.Serialize();
            if (epochOnePublicationCookie
                != global::OS.Boot.X64InstructionPublication
                    .ExpectedSuccessCookie)
            {
                EfiEntry
                    .WriteM01InstructionPublicationCookieFailure(
                        console,
                        serial);
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    M01StageOneAdapterStatus
                        .InstructionPublicationRejected,
                    (ulong)M01LoadedImageStackStatus
                        .SwitchPlanRejected,
                    (ulong)M01LoadedImageStackCheckpoint
                        .MapLedgerValidated,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }
            EfiEntry.s_m01InstructionPublicationEpochMask = 1;

            M01StageOneAdapterStatus prepareStatus =
                PrepareAndTransfer(
                    console,
                    serial,
                    bootServices,
                    loadedImage,
                    loadedImageMapping,
                    refreshedMap,
                    previousMapKey,
                    codeAddress,
                    codeByteLength,
                    codeEndExclusive,
                    codeFirmwareStatus,
                    dataAddress,
                    dataByteLength,
                    dataEndExclusive,
                    dataFirmwareStatus,
                    out ulong coordinatorStatus,
                    out ulong coordinatorCheckpoint);
            if (prepareStatus
                == M01StageOneAdapterStatus.UnexpectedSwitchReturn)
            {
                WriteFailure(
                    console,
                    serial,
                    prepareStatus,
                    coordinatorStatus,
                    coordinatorCheckpoint,
                    NotAttemptedStatus,
                    NotAttemptedStatus);
                Spin();
            }

            if (prepareStatus != M01StageOneAdapterStatus.Success)
            {
                return FailBeforeTransfer(
                    console,
                    serial,
                    bootServices,
                    prepareStatus,
                    coordinatorStatus,
                    coordinatorCheckpoint,
                    codeAddress,
                    codeAllocated,
                    dataAddress,
                    dataAllocated);
            }

            WriteFailure(
                console,
                serial,
                M01StageOneAdapterStatus.UnexpectedSwitchReturn,
                (ulong)M01LoadedImageStackStatus.UnexpectedReturn,
                (ulong)M01LoadedImageStackCheckpoint
                    .TransferRecorded,
                NotAttemptedStatus,
                NotAttemptedStatus);
            Spin();
            return true;
        }

        private static M01StageOneAdapterStatus
            TryCaptureLoadedImage(
                void* imageHandle,
                EfiSystemTable* systemTable,
                EfiBootServices* bootServices,
                out EfiLoadedImageRangeCapturePlan loadedImage,
                out M01LoadedImageVirtualMappingEvidence mapping,
                out ulong firmwareStatus)
        {
            loadedImage = default;
            mapping = default;
            firmwareStatus = NotAttemptedStatus;

            if (sizeof(EfiLoadedImageProtocol) != 96)
            {
                return M01StageOneAdapterStatus
                    .LoadedImageProtocolLayoutRejected;
            }

            EfiGuid loadedImageGuid;
            loadedImageGuid.Data1 = 0x5b1b31a1;
            loadedImageGuid.Data2 = 0x9562;
            loadedImageGuid.Data3 = 0x11d2;
            loadedImageGuid.Data4_0 = 0x8e;
            loadedImageGuid.Data4_1 = 0x3f;
            loadedImageGuid.Data4_2 = 0x00;
            loadedImageGuid.Data4_3 = 0xa0;
            loadedImageGuid.Data4_4 = 0xc9;
            loadedImageGuid.Data4_5 = 0x69;
            loadedImageGuid.Data4_6 = 0x72;
            loadedImageGuid.Data4_7 = 0x3b;

            void* loadedImageInterface = null;
            firmwareStatus = bootServices->HandleProtocol(
                imageHandle,
                &loadedImageGuid,
                &loadedImageInterface);
            ulong protocolAddress = (ulong)loadedImageInterface;
            if (firmwareStatus != EfiSuccess
                || loadedImageInterface == null
                || (protocolAddress
                    & (M01LoadedImageStackStaticState
                        .RequiredStorageAlignment - 1)) != 0
                || protocolAddress > ~0UL - 96)
            {
                return M01StageOneAdapterStatus
                    .LoadedImageProtocolFailed;
            }

            EfiLoadedImageProtocol* protocol =
                (EfiLoadedImageProtocol*)loadedImageInterface;
            if (protocol->SystemTable != systemTable
                || protocol->ImageBase == null
                || protocol->ImageSize == 0)
            {
                return M01StageOneAdapterStatus
                    .LoadedImageProtocolRejected;
            }

            EfiLoadedImageProtocolObservation observation =
                EfiLoadedImageProtocolObservation
                    .CaptureFromUefiBoundary(
                        firmwareStatus,
                        (ulong)imageHandle,
                        protocolAddress,
                        (ulong)protocol->ImageBase,
                        protocol->ImageSize);
            loadedImage =
                EfiLoadedImageRangeCapturePlan.Create(
                    &observation,
                    EfiLoadedImageRangeCapturePlan
                        .RequiredObservationCount,
                    EfiLoadedImageRangeCapturePlan
                        .RequiredObservationCount,
                    EfiLoadedImageRange.PageSize);
            if (!loadedImage.Succeeded)
            {
                return M01StageOneAdapterStatus
                    .LoadedImageRangeRejected;
            }

            mapping = M01LoadedImageVirtualMappingEvidence
                .CaptureCallerAttestedIdentity(loadedImage);
            return mapping.IsValidFor(loadedImage)
                ? M01StageOneAdapterStatus.Success
                : M01StageOneAdapterStatus
                    .LoadedImageMappingRejected;
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

        private static M01StageOneAdapterStatus
            TryRecaptureMemoryMap(
                EfiBootServices* bootServices,
                out FirmwareMemoryMapRecord refreshedMap,
                out ulong firmwareStatus)
        {
            refreshedMap = default;
            firmwareStatus = NotAttemptedStatus;
            if (bootServices == null
                || bootServices->GetMemoryMap == null)
            {
                return M01StageOneAdapterStatus
                    .MissingMemoryMapBoundary;
            }

            fixed (byte* source =
                &s_memoryMapSource.Bytes[0])
            fixed (byte* destination =
                &EfiEntry.s_ownedMemoryMap.Bytes[0])
            {
                ulong sourceAddress = (ulong)source;
                ulong destinationAddress = (ulong)destination;
                ulong capacity = OwnedMemoryMapBufferSize;
                if (sourceAddress == 0
                    || destinationAddress == 0
                    || sourceAddress > ~0UL - capacity
                    || destinationAddress > ~0UL - capacity
                    || RangesOverlap(
                        sourceAddress,
                        sourceAddress + capacity,
                        destinationAddress,
                        destinationAddress + capacity))
                {
                    return M01StageOneAdapterStatus
                        .MemoryMapStorageRejected;
                }

                ulong mapSize = capacity;
                ulong mapKey = 0;
                ulong descriptorSize = 0;
                uint descriptorVersion = 0;
                firmwareStatus = bootServices->GetMemoryMap(
                    &mapSize,
                    source,
                    &mapKey,
                    &descriptorSize,
                    &descriptorVersion);
                if (firmwareStatus != EfiSuccess)
                {
                    return M01StageOneAdapterStatus
                        .MemoryMapRecaptureFailed;
                }

                FirmwareMemoryMapRecord sourceRecord;
                sourceRecord.BufferAddress = sourceAddress;
                sourceRecord.BufferSize = capacity;
                sourceRecord.SourceBufferAddress = sourceAddress;
                sourceRecord.SourceBufferSize = capacity;
                sourceRecord.MapSize = mapSize;
                sourceRecord.MapKey = mapKey;
                sourceRecord.DescriptorSize = descriptorSize;
                sourceRecord.DescriptorVersion =
                    descriptorVersion;
                sourceRecord.DescriptorCount =
                    descriptorSize == 0
                        ? 0
                        : mapSize / descriptorSize;
                sourceRecord.Flags = 0;

                UefiMemoryMapCopyPlanResult copyPlan =
                    UefiMemoryMapCopyPlanResult.Create(
                        sourceRecord,
                        capacity);
                if (!copyPlan.Succeeded)
                {
                    return M01StageOneAdapterStatus
                        .MemoryMapMetadataRejected;
                }

                for (ulong index = 0;
                    index
                        < copyPlan.Plan.RequiredDestinationBytes;
                    index++)
                {
                    destination[index] = source[index];
                }

                refreshedMap.BufferAddress =
                    destinationAddress;
                refreshedMap.BufferSize = capacity;
                refreshedMap.SourceBufferAddress =
                    sourceAddress;
                refreshedMap.SourceBufferSize = capacity;
                refreshedMap.MapSize = mapSize;
                refreshedMap.MapKey = mapKey;
                refreshedMap.DescriptorSize =
                    descriptorSize;
                refreshedMap.DescriptorVersion =
                    descriptorVersion;
                refreshedMap.DescriptorCount =
                    sourceRecord.DescriptorCount;
                refreshedMap.Flags =
                    FirmwareMemoryMapRecord.FlagOwnedCopy;

                EfiMemoryMapNormalizeResult normalized =
                    EfiMemoryMapNormalizer.Normalize(
                        (void*)refreshedMap.BufferAddress,
                        refreshedMap.MapSize,
                        refreshedMap.DescriptorSize,
                        refreshedMap.DescriptorVersion,
                        refreshedMap.DescriptorCount);
                if (!normalized.Succeeded)
                {
                    refreshedMap = default;
                    return M01StageOneAdapterStatus
                        .MemoryMapMetadataRejected;
                }

                EfiEntry.s_memoryMapSummary =
                    normalized.Summary;
                EfiEntry.s_memoryMapSnapshot.Buffer =
                    (void*)refreshedMap.BufferAddress;
                EfiEntry.s_memoryMapSnapshot.BufferSize =
                    refreshedMap.BufferSize;
                EfiEntry.s_memoryMapSnapshot.SourceBuffer =
                    (void*)refreshedMap
                        .SourceBufferAddress;
                EfiEntry.s_memoryMapSnapshot
                    .SourceBufferSize =
                    refreshedMap.SourceBufferSize;
                EfiEntry.s_memoryMapSnapshot.MapSize =
                    refreshedMap.MapSize;
                EfiEntry.s_memoryMapSnapshot.MapKey =
                    refreshedMap.MapKey;
                EfiEntry.s_memoryMapSnapshot
                    .DescriptorSize =
                    refreshedMap.DescriptorSize;
                EfiEntry.s_memoryMapSnapshot
                    .DescriptorVersion =
                    refreshedMap.DescriptorVersion;
                EfiEntry.s_memoryMapSnapshot
                    .DescriptorCount =
                    refreshedMap.DescriptorCount;
                EfiEntry.s_memoryMapSnapshot.IsOwned = true;
                EfiEntry.s_bootHandoff.MemoryMap =
                    refreshedMap;
            }

            return M01StageOneAdapterStatus.Success;
        }

        private static bool MapContainsAllocation(
            FirmwareMemoryMapRecord map,
            ulong targetStart,
            ulong targetEndExclusive,
            uint expectedType)
        {
            if (!map.HasOwnedCopy()
                || targetStart == 0
                || targetStart >= targetEndExclusive
                || map.DescriptorSize
                    < UefiMemoryMapCopyPlan
                        .MinimumDescriptorSize
                || map.DescriptorCount == 0
                || map.DescriptorCount
                    > UefiMemoryMapCopyPlan
                        .DefaultMaximumDescriptorCount)
            {
                return false;
            }

            byte* mapBytes = (byte*)map.BufferAddress;
            ulong cursor = targetStart;
            for (ulong segment = 0;
                segment < map.DescriptorCount
                    && cursor < targetEndExclusive;
                segment++)
            {
                ulong bestEnd = cursor;
                for (ulong index = 0;
                    index < map.DescriptorCount;
                    index++)
                {
                    byte* descriptor =
                        mapBytes
                        + (index * map.DescriptorSize);
                    uint type =
                        ReadUInt32(descriptor);
                    ulong physicalStart =
                        ReadUInt64(
                            descriptor + 8);
                    ulong pageCount =
                        ReadUInt64(
                            descriptor + 24);
                    if (type != expectedType
                        || pageCount == 0
                        || pageCount > ~0UL / PageSize)
                    {
                        continue;
                    }

                    ulong byteLength =
                        pageCount * PageSize;
                    if (physicalStart
                        > ~0UL - byteLength)
                    {
                        return false;
                    }

                    ulong descriptorEnd =
                        physicalStart + byteLength;
                    if (physicalStart <= cursor
                        && cursor < descriptorEnd
                        && descriptorEnd > bestEnd)
                    {
                        bestEnd = descriptorEnd;
                    }
                }

                if (bestEnd == cursor)
                {
                    return false;
                }

                cursor = bestEnd < targetEndExclusive
                    ? bestEnd
                    : targetEndExclusive;
            }

            return cursor == targetEndExclusive;
        }

        private static M01StageOneAdapterStatus
            MaterializeAndInstallStackProbe(
                ulong codeAddress,
                ulong codeByteLength)
        {
            if (codeAddress == 0
                || codeByteLength != PageSize
                || StackProbeEntryOffset
                    < StubEntryOffset
                        + OwnedBootStackSwitchPlan
                            .RequiredStubByteLength
                || StackProbeEntryOffset
                    > codeByteLength - StackProbeByteLength
                || codeAddress
                    > ~0UL - StackProbeEntryOffset
                || ((codeAddress + StackProbeEntryOffset)
                    & (OwnedBootStackSwitchPlan
                        .StubEntryAlignmentBytes - 1)) != 0)
            {
                return M01StageOneAdapterStatus
                    .StackProbeLayoutRejected;
            }

            byte* codePage = (byte*)codeAddress;
            for (ulong index = 0;
                index < codeByteLength;
                index++)
            {
                codePage[index] = 0xcc;
            }

            for (ulong index = 0;
                index < codeByteLength;
                index++)
            {
                if (codePage[index] != 0xcc)
                {
                    return M01StageOneAdapterStatus
                        .StackProbeReadbackRejected;
                }
            }

            byte* probe =
                codePage + StackProbeEntryOffset;
            for (ulong index = 0;
                index < StackProbeByteLength;
                index++)
            {
                probe[index] = GetStackProbeByte(index);
            }

            for (ulong index = 0;
                index < StackProbeByteLength;
                index++)
            {
                if (probe[index]
                    != GetStackProbeByte(index))
                {
                    return M01StageOneAdapterStatus
                        .StackProbeReadbackRejected;
                }
            }

            return System.Runtime.X64StackProbeRuntime
                .TryInstall((ulong)probe)
                ? M01StageOneAdapterStatus.Success
                : M01StageOneAdapterStatus
                    .StackProbeInstallRejected;
        }

        private static byte GetStackProbeByte(ulong index)
        {
            return index switch
            {
                0 => 0xf3,
                1 => 0x0f,
                2 => 0x1e,
                3 => 0xfa,
                4 => 0x48,
                5 => 0x89,
                6 => 0xe0,
                7 => 0x48,
                8 => 0x25,
                9 => 0x00,
                10 => 0xf0,
                11 => 0xff,
                12 => 0xff,
                13 => 0x48,
                14 => 0x2d,
                15 => 0x00,
                16 => 0x10,
                17 => 0x00,
                18 => 0x00,
                19 => 0x85,
                20 => 0x00,
                21 => 0x4c,
                22 => 0x39,
                23 => 0xd8,
                24 => 0x7f,
                25 => 0xf3,
                26 => 0xc3,
                _ => 0
            };
        }

        private static M01StageOneAdapterStatus
            PrepareAndTransfer(
                EfiSimpleTextOutputProtocol* console,
                EfiSerialIoProtocol* serial,
                EfiBootServices* bootServices,
                EfiLoadedImageRangeCapturePlan loadedImage,
                M01LoadedImageVirtualMappingEvidence
                    loadedImageMapping,
                FirmwareMemoryMapRecord refreshedMap,
                ulong previousMapKey,
                ulong codeAddress,
                ulong codeByteLength,
                ulong codeEndExclusive,
                ulong codeFirmwareStatus,
                ulong dataAddress,
                ulong dataByteLength,
                ulong dataEndExclusive,
                ulong dataFirmwareStatus,
                out ulong coordinatorStatus,
                out ulong coordinatorCheckpoint)
        {
            coordinatorStatus =
                (ulong)M01LoadedImageStackStatus
                    .Uninitialized;
            coordinatorCheckpoint =
                (ulong)M01LoadedImageStackCheckpoint
                    .Uninitialized;

            fixed (M01StageOneAllocationRecordBuffer*
                allocationStorage = &s_allocationRecords)
            fixed (M01StageOneReservationLedgerBuffer*
                ledgerStorage = &s_reservationLedger)
            fixed (M01LoadedImageStackStaticState*
                staticState = &s_staticState)
            fixed (M01LoadedImageStackEvidence*
                evidenceScratch = &s_evidenceScratch)
            {
                if ((ulong)sizeof(
                        M01StageOneAllocationRecordBuffer)
                        != M01LoadedImageStackIntegration
                            .RequiredAllocationRecordCount
                            * (ulong)sizeof(
                                BootMemoryRegionRecord)
                    || (ulong)sizeof(
                        M01StageOneReservationLedgerBuffer)
                        != ReservationCapacity
                            * (ulong)sizeof(
                                OS.Kernel.Memory
                                    .BootReservedRange))
                {
                    return M01StageOneAdapterStatus
                        .StaticStorageLayoutRejected;
                }

                BootMemoryRegionRecord* allocationRecords =
                    &allocationStorage->Code;
                allocationStorage->Code =
                    new BootMemoryRegionRecord(
                        EfiLoaderCode,
                        codeAddress,
                        codeAddress,
                        CodePageCount,
                        0,
                        codeByteLength,
                        codeEndExclusive);
                allocationStorage->Data =
                    new BootMemoryRegionRecord(
                        EfiLoaderData,
                        dataAddress,
                        dataAddress,
                        DataArenaPageCount,
                        0,
                        dataByteLength,
                        dataEndExclusive);

                M01StageOneAllocationEvidence
                    allocationEvidence =
                    M01StageOneAllocationEvidence
                        .CaptureCallerAttested(
                            allocationRecords,
                            M01LoadedImageStackIntegration
                                .RequiredAllocationRecordCount,
                            M01LoadedImageStackIntegration
                                .RequiredAllocationRecordCount,
                            CodeAllocationOrdinal,
                            DataAllocationOrdinal,
                            codeFirmwareStatus,
                            dataFirmwareStatus);
                M01StageOneAddressMappingEvidence
                    addressMapping =
                    M01StageOneAddressMappingEvidence
                        .CaptureCallerAttestedIdentity(
                            allocationRecords,
                            M01LoadedImageStackIntegration
                                .RequiredAllocationRecordCount,
                            M01LoadedImageStackIntegration
                                .RequiredAllocationRecordCount,
                            refreshedMap);
                if (!allocationEvidence.IsWellFormed
                    || !addressMapping.IsValidFor(
                        allocationStorage->Code,
                        allocationStorage->Data,
                        refreshedMap))
                {
                    return M01StageOneAdapterStatus
                        .AllocationEvidenceRejected;
                }

                OS.Kernel.Memory.BootReservedRange*
                    reservationLedger =
                    &ledgerStorage->Range0;
                M01StageOneAdapterStatus ledgerStatus =
                    BuildReservationLedger(
                        loadedImage,
                        codeAddress,
                        dataAddress,
                        reservationLedger);
                if (ledgerStatus
                    != M01StageOneAdapterStatus.Success)
                {
                    return ledgerStatus;
                }

                OS.Kernel.Memory
                    .BootReservationTableValidationResult
                    ledgerValidation =
                    OS.Kernel.Memory
                        .BootReservationTableValidator
                        .Validate(
                            reservationLedger,
                            ReservationCount,
                            ReservationCapacity);
                if (!ledgerValidation.Succeeded
                    || ledgerValidation.ReservationCount
                        != ReservationCount
                    || ledgerValidation
                            .AvailableReservationCapacity
                        != ReservationCapacity)
                {
                    return M01StageOneAdapterStatus
                        .ReservationLedgerRejected;
                }

                M01StageOneMapLedgerEvidence
                    mapLedgerEvidence =
                    M01StageOneMapLedgerEvidence
                        .CaptureCallerAttested(
                            previousMapKey,
                            InitialMapGeneration,
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
                    return M01StageOneAdapterStatus
                        .MapLedgerEvidenceRejected;
                }

                delegate* unmanaged<
                    ulong,
                    OwnedBootStackSwitchHandoff*,
                    void> continuation =
                    &M01StageOneContinuation;
                ulong continuationRaw =
                    (ulong)(void*)continuation;
                if (VirtualAddress
                        .TryCreateFromUefiBoundary(
                            continuationRaw,
                            out VirtualAddress
                                continuationAddress)
                    != AddressValueStatus.Success)
                {
                    return M01StageOneAdapterStatus
                        .ContinuationAddressRejected;
                }

                M01LoadedImageStackStaticPreparationResult
                    prepared =
                    M01LoadedImageStackIntegration
                        .PrepareFromStaticScratch(
                        loadedImage,
                        loadedImageMapping,
                        addressMapping,
                        allocationRecords,
                        M01LoadedImageStackIntegration
                            .RequiredAllocationRecordCount,
                        M01LoadedImageStackIntegration
                            .RequiredAllocationRecordCount,
                        allocationEvidence,
                        mapLedgerEvidence,
                        reservationLedger,
                        ReservationCount,
                        ReservationCapacity,
                        (byte*)codeAddress,
                        codeByteLength,
                        PageSize,
                        StubEntryOffset,
                        OwnedBootStackSwitchPlan
                            .RequiredStubByteLength,
                        continuationAddress,
                        staticState,
                        M01LoadedImageStackStaticState
                            .RequiredStorageBytes,
                        evidenceScratch,
                        M01LoadedImageStackIntegration
                            .RequiredEvidenceScratchBytes);
                coordinatorStatus =
                    (ulong)prepared.Status;
                coordinatorCheckpoint =
                    (ulong)prepared.Checkpoint;
                if (!prepared.IsPrepared)
                {
                    return M01StageOneAdapterStatus
                        .CoordinatorPrepareRejected;
                }

                prepared = default;
                M01LoadedImageStackStaticTransitionResult
                    transfer =
                    M01LoadedImageStackIntegration
                        .RecordTransferFromStaticState(
                            staticState,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes);
                coordinatorStatus =
                    (ulong)transfer.Status;
                coordinatorCheckpoint =
                    (ulong)transfer.Checkpoint;
                if (!transfer.TransferRecorded)
                {
                    return M01StageOneAdapterStatus
                        .TransferRecordRejected;
                }

                M01LoadedImageStackTransferTarget target =
                    transfer.TransferTarget;
                OwnedBootStackSwitchPlan plan =
                    staticState->SwitchStateStorage.Plan;
                if (!target.IsValidFor(plan)
                    || target.Entry
                            .TryGetRawValueForUefiBoundary(
                                out ulong entryRaw)
                        != AddressValueStatus.Success
                    || target.NewRspTop
                            .TryGetRawValueForUefiBoundary(
                                out ulong newRspRaw)
                        != AddressValueStatus.Success
                    || target.HandoffAddress
                            .TryGetRawValueForUefiBoundary(
                                out ulong handoffRaw)
                        != AddressValueStatus.Success
                    || target.ContinuationAddress
                            .TryGetRawValueForUefiBoundary(
                                out ulong targetContinuationRaw)
                        != AddressValueStatus.Success
                    || plan.ExpectedContinuationStackPointer
                            .TryGetRawValueForUefiBoundary(
                                out ulong expectedRspRaw)
                        != AddressValueStatus.Success
                    || plan.UsableStackVirtualStart
                            .TryGetRawValueForUefiBoundary(
                                out ulong usableStartRaw)
                        != AddressValueStatus.Success
                    || plan.UsableStackVirtualEndExclusive
                            .TryGetRawValueForUefiBoundary(
                                out ulong usableEndRaw)
                        != AddressValueStatus.Success
                    || entryRaw != codeAddress
                        + StubEntryOffset
                    || (entryRaw
                        & (OwnedBootStackSwitchPlan
                            .StubEntryAlignmentBytes - 1)) != 0
                    || entryRaw >= codeEndExclusive
                    || newRspRaw
                        != usableEndRaw
                            - OwnedBootStackSwitchPlan
                                .X64ShadowSpaceBytes
                    || (newRspRaw
                        & (OwnedBootStackSwitchPlan
                            .X64StackAlignmentBytes - 1)) != 0
                    || expectedRspRaw != newRspRaw
                        - OwnedBootStackSwitchPlan
                            .FakeReturnAddressBytes
                    || expectedRspRaw < usableStartRaw
                    || expectedRspRaw >= usableEndRaw
                    || handoffRaw
                        != (ulong)&staticState
                            ->HandoffStorage
                    || (handoffRaw
                        & (M01LoadedImageStackStaticState
                            .RequiredStorageAlignment - 1)) != 0
                    || targetContinuationRaw
                        != continuationRaw)
                {
                    return M01StageOneAdapterStatus
                        .TransferTargetRejected;
                }

                s_console = console;
                s_serial = serial;
                s_bootServices = bootServices;
                transfer = default;

                if (EfiEntry
                        .s_m01InstructionPublicationEpochMask
                    != 1)
                {
                    EfiEntry
                        .WriteM01InstructionPublicationCookieFailure(
                            console,
                            serial);
                    return M01StageOneAdapterStatus
                        .InstructionPublicationRejected;
                }
                uint epochTwoPublicationCookie =
                    global::OS.Boot.X64InstructionPublication.Serialize();
                if (epochTwoPublicationCookie
                    != global::OS.Boot.X64InstructionPublication
                        .ExpectedSuccessCookie)
                {
                    EfiEntry
                        .WriteM01InstructionPublicationCookieFailure(
                            console,
                            serial);
                    return M01StageOneAdapterStatus
                        .InstructionPublicationRejected;
                }
                EfiEntry.s_m01InstructionPublicationEpochMask = 3;

                delegate* unmanaged<
                    ulong,
                    OwnedBootStackSwitchHandoff*,
                    delegate* unmanaged<
                        ulong,
                        OwnedBootStackSwitchHandoff*,
                        void>,
                    void> switchEntry =
                    (delegate* unmanaged<
                        ulong,
                        OwnedBootStackSwitchHandoff*,
                        delegate* unmanaged<
                            ulong,
                            OwnedBootStackSwitchHandoff*,
                            void>,
                        void>)(void*)entryRaw;
                switchEntry(
                    newRspRaw,
                    (OwnedBootStackSwitchHandoff*)
                        handoffRaw,
                    continuation);

                M01LoadedImageStackStaticTransitionResult
                    unexpected =
                    M01LoadedImageStackIntegration
                        .ReportUnexpectedReturnFromStaticState(
                            staticState,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes);
                coordinatorStatus =
                    (ulong)unexpected.Status;
                coordinatorCheckpoint =
                    (ulong)unexpected.Checkpoint;
                return M01StageOneAdapterStatus
                    .UnexpectedSwitchReturn;
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

        private static M01StageOneAdapterStatus
            BuildReservationLedger(
                EfiLoadedImageRangeCapturePlan loadedImage,
                ulong codeAddress,
                ulong dataAddress,
                OS.Kernel.Memory.BootReservedRange*
                    reservationLedger)
        {
            if (reservationLedger == null
                || PhysicalAddress.TryCreateFromUefiBoundary(
                        codeAddress,
                        out PhysicalAddress codeStart)
                    != AddressValueStatus.Success
                || PhysicalAddress.TryCreateFromUefiBoundary(
                        dataAddress,
                        out PhysicalAddress dataStart)
                    != AddressValueStatus.Success
                || dataAddress > ~0UL - PageSize
                || PhysicalAddress.TryCreateFromUefiBoundary(
                        dataAddress + PageSize,
                        out PhysicalAddress stackStart)
                    != AddressValueStatus.Success)
            {
                return M01StageOneAdapterStatus
                    .ReservationAddressRejected;
            }

            reservationLedger[0] =
                loadedImage.Range.Reservation;
            if (OS.Kernel.Memory.BootReservedRange
                    .TryCreate(
                        OS.Kernel.Memory.BootReservationKind
                            .BootStackSwitchStub,
                        codeStart,
                        CodePageCount,
                        out reservationLedger[1])
                    != OS.Kernel.Memory
                        .BootReservedRangeStatus.Success
                || OS.Kernel.Memory.BootReservedRange
                    .TryCreate(
                        OS.Kernel.Memory.BootReservationKind
                            .BootStackGuard,
                        dataStart,
                        GuardPageCount,
                        out reservationLedger[2])
                    != OS.Kernel.Memory
                        .BootReservedRangeStatus.Success
                || OS.Kernel.Memory.BootReservedRange
                    .TryCreate(
                        OS.Kernel.Memory.BootReservationKind
                            .BootStack,
                        stackStart,
                        UsableStackPageCount,
                        out reservationLedger[3])
                    != OS.Kernel.Memory
                        .BootReservedRangeStatus.Success)
            {
                return M01StageOneAdapterStatus
                    .ReservationConstructionRejected;
            }

            for (ulong index = 1;
                index < ReservationCount;
                index++)
            {
                OS.Kernel.Memory.BootReservedRange current =
                    reservationLedger[index];
                ulong destination = index;
                while (destination != 0)
                {
                    AddressValueStatus compareStatus =
                        reservationLedger[destination - 1]
                            .PhysicalStart.TryCompareTo(
                                current.PhysicalStart,
                                out int comparison);
                    if (compareStatus
                        != AddressValueStatus.Success)
                    {
                        return M01StageOneAdapterStatus
                            .ReservationSortRejected;
                    }

                    if (comparison <= 0)
                    {
                        break;
                    }

                    reservationLedger[destination] =
                        reservationLedger[
                            destination - 1];
                    destination--;
                }

                reservationLedger[destination] = current;
            }

            return M01StageOneAdapterStatus.Success;
        }

        internal static bool IsFinalStageOneSuccess(
            void* stageOneState,
            ulong stageOneStateCapacity)
        {
            ulong stateAddress = (ulong)stageOneState;
            ulong requiredCapacity =
                M01LoadedImageStackStaticState.RequiredStorageBytes;
            if ((ulong)sizeof(M01LoadedImageStackStaticState)
                    != requiredCapacity
                || stageOneState == null
                || (stateAddress
                    & (M01LoadedImageStackStaticState
                        .RequiredStorageAlignment - 1)) != 0
                || stageOneStateCapacity != requiredCapacity
                || stateAddress > ~0UL - stageOneStateCapacity)
            {
                return false;
            }

            M01LoadedImageStackStaticState* state =
                (M01LoadedImageStackStaticState*)stageOneState;
            fixed (M01LoadedImageStackStaticState*
                expectedState = &s_staticState)
            {
                return state == expectedState
                    && state->IsInitialized
                    && state->Status
                        == M01LoadedImageStackStatus.Success
                    && state->Checkpoint
                        == M01LoadedImageStackCheckpoint
                            .ContinuationRspContained
                    && state->SwitchState.State
                        == OwnedBootStackSwitchState
                            .ContinuationObserved
                    && state->StackLayout.Lifetime
                        == OwnedBootStackLifetime.Active;
            }
        }

        internal static bool TryGetFinalSwitchPlan(
            void* stageOneState,
            ulong stageOneStateCapacity,
            out OwnedBootStackSwitchPlanResult switchPlan,
            out BootMemoryRegionRecord stageOneCodeAllocation)
        {
            switchPlan = default;
            stageOneCodeAllocation = default;
            if (!IsFinalStageOneSuccess(
                    stageOneState,
                    stageOneStateCapacity))
            {
                return false;
            }

            switchPlan = s_evidenceScratch.SwitchPlanResult;
            stageOneCodeAllocation =
                s_evidenceScratch.CodeAllocation;
            return switchPlan.Succeeded
                && stageOneCodeAllocation.PhysicalStart
                    == switchPlan.Plan.CodeAllocation
                        .PhysicalStart
                && stageOneCodeAllocation.PhysicalEndExclusive
                    == switchPlan.Plan.CodeAllocation
                        .PhysicalEndExclusive
                && stageOneCodeAllocation.PageCount == 1
                && stageOneCodeAllocation.ByteLength == PageSize;
        }

        internal static bool TryValidateStageTwoAllocation(
            ulong address,
            ulong pageCount,
            out ulong byteLength,
            out ulong endExclusive)
        {
            return TryValidateAllocation(
                address,
                pageCount,
                out byteLength,
                out endExclusive);
        }

        internal static bool TryRecaptureStageTwoMemoryMap(
            EfiBootServices* bootServices,
            out FirmwareMemoryMapRecord refreshedMap,
            out ulong firmwareStatus)
        {
            M01StageOneAdapterStatus status =
                TryRecaptureMemoryMap(
                    bootServices,
                    out refreshedMap,
                    out firmwareStatus);
            return status == M01StageOneAdapterStatus.Success;
        }

        internal static bool StageTwoMapContainsAllocation(
            FirmwareMemoryMapRecord map,
            ulong targetStart,
            ulong targetEndExclusive,
            uint expectedType)
        {
            return MapContainsAllocation(
                map,
                targetStart,
                targetEndExclusive,
                expectedType);
        }

        internal static M01LiveAllocatorIntegrationResult
            ExecuteStageTwoCoordinator(
                void* stageOneState,
                ulong stageOneStateCapacity,
                BootMemoryRegionRecord* allocationRecords,
                ulong allocationRecordCount,
                ulong allocationRecordCapacity,
                M01LiveAllocatorAllocationEvidence
                    allocationEvidence,
                M01LiveAllocatorMapLedgerEvidence
                    mapLedgerEvidence,
                OS.Kernel.Memory.BootReservedRange*
                    reservationLedger,
                ulong reservationCount,
                ulong reservationCapacity,
                M01LiveAllocatorStaticState* staticState,
                ulong staticStateCapacity)
        {
            return M01LiveAllocatorIntegration
                .Execute(
                    (M01LoadedImageStackStaticState*)
                        stageOneState,
                    stageOneStateCapacity,
                    allocationRecords,
                    allocationRecordCount,
                    allocationRecordCapacity,
                    allocationEvidence,
                    mapLedgerEvidence,
                    reservationLedger,
                    reservationCount,
                    reservationCapacity,
                    staticState,
                    staticStateCapacity);
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void M01StageOneContinuation(
            ulong observedRsp,
            OwnedBootStackSwitchHandoff* handoff)
        {
            fixed (M01LoadedImageStackStaticState*
                staticState = &s_staticState)
            {
                if (EfiEntry
                        .s_m01InstructionPublicationEpochMask
                    != 3)
                {
                    EfiEntry
                        .WriteM01InstructionPublicationCookieFailure(
                            s_console,
                            s_serial);
                    WriteFailure(
                        s_console,
                        s_serial,
                        M01StageOneAdapterStatus
                            .InstructionPublicationRejected,
                        (ulong)M01LoadedImageStackStatus
                            .ContinuationRspRejected,
                        (ulong)M01LoadedImageStackCheckpoint
                            .TransferRecorded,
                        NotAttemptedStatus,
                        NotAttemptedStatus);
                    Spin();
                }

                VirtualAddress observedRspAddress = default;
                if (handoff == null
                    || handoff
                        != &staticState->HandoffStorage
                    || VirtualAddress
                            .TryCreateFromUefiBoundary(
                                observedRsp,
                                out observedRspAddress)
                        != AddressValueStatus.Success)
                {
                    WriteFailure(
                        s_console,
                        s_serial,
                        M01StageOneAdapterStatus
                            .ContinuationHandoffRejected,
                        (ulong)M01LoadedImageStackStatus
                            .ContinuationRspRejected,
                        (ulong)M01LoadedImageStackCheckpoint
                            .TransferRecorded,
                        NotAttemptedStatus,
                        NotAttemptedStatus);
                    Spin();
                }

                M01LoadedImageStackStaticTransitionResult
                    observation =
                    M01LoadedImageStackIntegration
                        .ObserveContinuationRspFromStaticState(
                            staticState,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes,
                            observedRspAddress);
                if (!observation.ContinuationRspContained)
                {
                    WriteFailure(
                        s_console,
                        s_serial,
                        M01StageOneAdapterStatus
                            .ContinuationRspRejected,
                        (ulong)observation.Status,
                        (ulong)observation.Checkpoint,
                        NotAttemptedStatus,
                        NotAttemptedStatus);
                    Spin();
                }

                for (int lineIndex = 1;
                    lineIndex
                        < BootSequencePlan
                            .M01StageOneSuccessLineCount;
                    lineIndex++)
                {
                    WriteSuccessLine(
                        s_console,
                        s_serial,
                        lineIndex);
                }

#if M01_LIVE_ALLOCATOR_PROBE
                fixed (M01StageOneReservationLedgerBuffer*
                    stageOneLedgerStorage = &s_reservationLedger)
                {
                    if (!M01StageTwoAdapter.RunAfterStageOne(
                            (void*)staticState,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes,
                            s_bootServices,
                            s_console,
                            s_serial,
                            &stageOneLedgerStorage->Range0,
                            ReservationCount,
                            ReservationCapacity))
                    {
                        Spin();
                    }
                }
#endif

                EfiEntry.WriteBootSequencePlan(
                    s_console,
                    s_serial);
#if M02_UD2_VECTOR_STATE_PROBE
                EfiEntry.EnterM02Ud2VectorStateProbe(
                    s_console,
                    s_serial,
                    s_bootServices);
#endif
#if M02_OWNED_GDT_TSS_TERMINAL_PROBE
                EfiEntry.EnterM02OwnedGdtTssTerminalProbe(
                    s_console,
                    s_serial,
                    s_bootServices);
#endif
                Spin();
            }
        }

        private static bool FailBeforeTransfer(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiBootServices* bootServices,
            M01StageOneAdapterStatus adapterStatus,
            ulong status,
            ulong checkpoint,
            ulong codeAddress,
            bool codeAllocated,
            ulong dataAddress,
            bool dataAllocated)
        {
            ulong dataCleanupStatus = NotAttemptedStatus;
            ulong codeCleanupStatus = NotAttemptedStatus;

            if (bootServices != null
                && bootServices->FreePages != null)
            {
                if (dataAllocated)
                {
                    dataCleanupStatus =
                        bootServices->FreePages(
                            dataAddress,
                            DataArenaPageCount);
                }

                if (codeAllocated)
                {
                    bool stackProbeCleared =
                        TryClearStackProbeBytes(
                            codeAddress);
                    if (stackProbeCleared)
                    {
                        codeCleanupStatus =
                            bootServices->FreePages(
                                codeAddress,
                                CodePageCount);
                    }
                    else
                    {
                        codeCleanupStatus =
                            NotAttemptedStatus - 1;
                    }
                }
            }

            WriteFailure(
                console,
                serial,
                adapterStatus,
                status,
                checkpoint,
                dataCleanupStatus,
                codeCleanupStatus);
            return true;
        }

        private static bool TryClearStackProbeBytes(
            ulong codeAddress)
        {
            if (!TryValidateAllocation(
                    codeAddress,
                    CodePageCount,
                    out ulong codeByteLength,
                    out _))
            {
                return true;
            }

            if (StackProbeEntryOffset
                    > codeByteLength - StackProbeByteLength
                || codeAddress
                    > ~0UL - StackProbeEntryOffset)
            {
                return false;
            }

            ulong stackProbeAddress =
                codeAddress + StackProbeEntryOffset;
            if (!System.Runtime.X64StackProbeRuntime
                    .TryClear(stackProbeAddress))
            {
                return false;
            }

            byte* probe = (byte*)stackProbeAddress;
            for (ulong index = 0;
                index < StackProbeByteLength;
                index++)
            {
                probe[index] = 0;
            }

            for (ulong index = 0;
                index < StackProbeByteLength;
                index++)
            {
                if (probe[index] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteSuccessLine(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            int lineIndex)
        {
            int length =
                BootSequencePlan
                    .GetM01StageOneSuccessLineLength(
                        lineIndex);
            for (int offset = 0;
                offset < length;
                offset++)
            {
                EfiEntry.Write(
                    console,
                    serial,
                    BootSequencePlan
                        .GetM01StageOneSuccessLineByte(
                            lineIndex,
                            offset));
            }

            EfiEntry.WriteCrLf(console, serial);
        }

        private static void WriteFailure(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            M01StageOneAdapterStatus adapterStatus,
            ulong status,
            ulong checkpoint,
            ulong dataCleanupStatus,
            ulong codeCleanupStatus)
        {
            WriteFailurePrefix(console, serial, false);
            WriteFailureFields(
                console,
                serial,
                adapterStatus,
                status,
                checkpoint,
                dataCleanupStatus,
                codeCleanupStatus);
            WriteFailurePrefix(console, serial, true);
            WriteFailureFields(
                console,
                serial,
                adapterStatus,
                status,
                checkpoint,
                dataCleanupStatus,
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
                    (byte)'1',
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
                (byte)'1',
                (byte)' ',
                0,
                0,
                0);
        }

        private static void WriteFailureFields(
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            M01StageOneAdapterStatus adapterStatus,
            ulong status,
            ulong checkpoint,
            ulong dataCleanupStatus,
            ulong codeCleanupStatus)
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
                status);
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
                checkpoint);
            WriteAscii(
                console,
                serial,
                (byte)' ',
                (byte)'f',
                (byte)'r',
                (byte)'e',
                (byte)'e',
                (byte)'_',
                (byte)'d',
                (byte)'a');
            WriteAscii(
                console,
                serial,
                (byte)'t',
                (byte)'a',
                (byte)'=',
                0,
                0,
                0,
                0,
                0);
            EfiEntry.WriteUnsignedDecimal(
                console,
                serial,
                dataCleanupStatus);
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
                (byte)'o');
            WriteAscii(
                console,
                serial,
                (byte)'d',
                (byte)'e',
                (byte)'=',
                0,
                0,
                0,
                0,
                0);
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
            if (b0 != 0) EfiEntry.Write(console, serial, b0);
            if (b1 != 0) EfiEntry.Write(console, serial, b1);
            if (b2 != 0) EfiEntry.Write(console, serial, b2);
            if (b3 != 0) EfiEntry.Write(console, serial, b3);
            if (b4 != 0) EfiEntry.Write(console, serial, b4);
            if (b5 != 0) EfiEntry.Write(console, serial, b5);
            if (b6 != 0) EfiEntry.Write(console, serial, b6);
            if (b7 != 0) EfiEntry.Write(console, serial, b7);
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

        private static void Spin()
        {
            while (true)
            {
            }
        }

        private enum M01StageOneAdapterStatus : ulong
        {
            Success = 0,
            MissingFirmwareBoundary = 1,
            LoadedImageProtocolLayoutRejected = 2,
            LoadedImageProtocolFailed = 3,
            LoadedImageProtocolRejected = 4,
            LoadedImageRangeRejected = 5,
            LoadedImageMappingRejected = 6,
            CodeAllocationFailed = 7,
            InvalidCodeAllocation = 8,
            DataAllocationFailed = 9,
            InvalidDataAllocation = 10,
            AllocationOverlap = 11,
            MissingMemoryMapBoundary = 12,
            MemoryMapStorageRejected = 13,
            MemoryMapRecaptureFailed = 14,
            MemoryMapMetadataRejected = 15,
            MemoryMapKeyUnchanged = 16,
            MemoryMapAllocationTypeMismatch = 17,
            StaticStorageLayoutRejected = 18,
            AllocationEvidenceRejected = 19,
            ReservationAddressRejected = 20,
            ReservationConstructionRejected = 21,
            ReservationSortRejected = 22,
            ReservationLedgerRejected = 23,
            MapLedgerEvidenceRejected = 24,
            ContinuationAddressRejected = 25,
            CoordinatorPrepareRejected = 26,
            TransferRecordRejected = 27,
            TransferTargetRejected = 28,
            UnexpectedSwitchReturn = 29,
            ContinuationHandoffRejected = 30,
            ContinuationRspRejected = 31,
            StackProbeLayoutRejected = 32,
            StackProbeReadbackRejected = 33,
            StackProbeInstallRejected = 34,
            InstructionPublicationRejected = 35
        }

        private unsafe struct EfiLoadedImageProtocol
        {
            public uint Revision;
            public void* ParentHandle;
            public EfiSystemTable* SystemTable;
            public void* DeviceHandle;
            public void* FilePath;
            public void* Reserved;
            public uint LoadOptionsSize;
            public void* LoadOptions;
            public void* ImageBase;
            public ulong ImageSize;
            public uint ImageCodeType;
            public uint ImageDataType;
            public void* Unload;
        }

        private unsafe struct M01StageOneMemoryMapBuffer
        {
            public fixed byte Bytes[OwnedMemoryMapBufferSize];
        }

        private struct M01StageOneAllocationRecordBuffer
        {
            public BootMemoryRegionRecord Code;
            public BootMemoryRegionRecord Data;
        }

        private struct M01StageOneReservationLedgerBuffer
        {
            public OS.Kernel.Memory.BootReservedRange Range0;
            public OS.Kernel.Memory.BootReservedRange Range1;
            public OS.Kernel.Memory.BootReservedRange Range2;
            public OS.Kernel.Memory.BootReservedRange Range3;
        }
    }
#else
    private static class M01StageOneAdapter
    {
        public static bool Run(
            void* imageHandle,
            EfiSimpleTextOutputProtocol* console,
            EfiSerialIoProtocol* serial,
            EfiSystemTable* systemTable)
        {
            return false;
        }
    }
#endif

#if M01_LIVE_ALLOCATOR_PROBE
    private static class M01StageTwoAdapter
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
            if (b0 != 0) EfiEntry.Write(console, serial, b0);
            if (b1 != 0) EfiEntry.Write(console, serial, b1);
            if (b2 != 0) EfiEntry.Write(console, serial, b2);
            if (b3 != 0) EfiEntry.Write(console, serial, b3);
            if (b4 != 0) EfiEntry.Write(console, serial, b4);
            if (b5 != 0) EfiEntry.Write(console, serial, b5);
            if (b6 != 0) EfiEntry.Write(console, serial, b6);
            if (b7 != 0) EfiEntry.Write(console, serial, b7);
        }

        private enum M01StageTwoAdapterStatus : ulong
        {
            Success = 0,
            StageOneRejected = 1,
            StageOneReservationLedgerRejected = 2,
            MissingFirmwareBoundary = 3,
            StageOneMemoryMapRejected = 4,
            StaticStorageRejected = 5,
            ArenaAllocationFailed = 6,
            ArenaAllocationRejected = 7,
            MemoryMapRecaptureFailed = 8,
            MemoryMapKeyUnchanged = 9,
            MemoryMapAllocationTypeMismatch = 10,
            AllocationEvidenceRejected = 11,
            ArenaReservationRejected = 12,
            ReservationSortRejected = 13,
            ReservationLedgerRejected = 14,
            MapLedgerEvidenceRejected = 15,
            CoordinatorResultRejected = 16,
            StaticStateNotDefault = 17
        }

        private struct M01StageTwoReservationLedgerBuffer
        {
            public OS.Kernel.Memory.BootReservedRange Range0;
            public OS.Kernel.Memory.BootReservedRange Range1;
            public OS.Kernel.Memory.BootReservedRange Range2;
            public OS.Kernel.Memory.BootReservedRange Range3;
            public OS.Kernel.Memory.BootReservedRange Range4;
        }
    }
#else
    private static class M01StageTwoAdapter
    {
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
            return false;
        }
    }
#endif

    private static void WriteCrLf(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial)
    {
        EfiConsoleAdapter adapter = new EfiConsoleAdapter(console, serial);
        adapter.WriteCrLf();
    }

    private static void Write(EfiSimpleTextOutputProtocol* console, EfiSerialIoProtocol* serial, byte value)
    {
        EfiConsoleAdapter adapter = new EfiConsoleAdapter(console, serial);
        adapter.WriteByte(value);
    }

    private static void WriteUnsignedDecimal(
        EfiSimpleTextOutputProtocol* console,
        EfiSerialIoProtocol* serial,
        ulong value)
    {
        if (value >= 10)
        {
            WriteUnsignedDecimal(console, serial, value / 10);
        }

        Write(console, serial, (byte)((value % 10) + (byte)'0'));
    }
}

internal struct EfiTableHeader
{
    public ulong Signature;
    public uint Revision;
    public uint HeaderSize;
    public uint Crc32;
    public uint Reserved;
}

internal unsafe struct EfiSimpleTextOutputProtocol
{
    public void* Reset;
    public delegate* unmanaged<EfiSimpleTextOutputProtocol*, ushort*, ulong> OutputString;
}

internal unsafe struct EfiBootServices
{
    public EfiTableHeader Hdr;
    public void* RaiseTpl;
    public void* RestoreTpl;
    public delegate* unmanaged<uint, uint, ulong, ulong*, ulong> AllocatePages;
    public delegate* unmanaged<ulong, ulong, ulong> FreePages;
    public delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> GetMemoryMap;
    public delegate* unmanaged<uint, ulong, void**, ulong> AllocatePool;
    public void* FreePool;
    public void* CreateEvent;
    public void* SetTimer;
    public void* WaitForEvent;
    public void* SignalEvent;
    public void* CloseEvent;
    public void* CheckEvent;
    public void* InstallProtocolInterface;
    public void* ReinstallProtocolInterface;
    public void* UninstallProtocolInterface;
    public delegate* unmanaged<void*, EfiGuid*, void**, ulong> HandleProtocol;
    public void* Reserved;
    public void* RegisterProtocolNotify;
    public void* LocateHandle;
    public void* LocateDevicePath;
    public void* InstallConfigurationTable;
    public void* LoadImage;
    public void* StartImage;
    public void* Exit;
    public void* UnloadImage;
    public void* ExitBootServices;
    public void* GetNextMonotonicCount;
    public void* Stall;
    public void* SetWatchdogTimer;
    public void* ConnectController;
    public void* DisconnectController;
    public void* OpenProtocol;
    public void* CloseProtocol;
    public void* OpenProtocolInformation;
    public void* ProtocolsPerHandle;
    public void* LocateHandleBuffer;
    public delegate* unmanaged<EfiGuid*, void*, void**, ulong> LocateProtocol;
}

internal unsafe struct EfiSystemTable
{
    public EfiTableHeader Hdr;
    public ushort* FirmwareVendor;
    public uint FirmwareRevision;
    public void* ConsoleInHandle;
    public void* ConIn;
    public void* ConsoleOutHandle;
    public EfiSimpleTextOutputProtocol* ConOut;
    public void* StandardErrorHandle;
    public EfiSimpleTextOutputProtocol* StdErr;
    public void* RuntimeServices;
    public EfiBootServices* BootServices;
    public ulong NumberOfTableEntries;
    public void* ConfigurationTable;
}

internal unsafe struct EfiMemoryMapSnapshot
{
    public void* Buffer;
    public ulong BufferSize;
    public void* SourceBuffer;
    public ulong SourceBufferSize;
    public ulong MapSize;
    public ulong MapKey;
    public ulong DescriptorSize;
    public uint DescriptorVersion;
    public ulong DescriptorCount;
    public bool IsOwned;
}

internal unsafe struct OwnedMemoryMapBuffer
{
    public fixed byte Bytes[131072];
}

internal unsafe struct OwnedMemoryRegionBuffer
{
    public fixed ulong Slots[32768];
}

internal unsafe struct BootPageAllocatorNodeBuffer
{
    public fixed ulong Slots[24576];
}

internal enum EfiBootPageAllocatorReadinessStatus
{
    Ready = 0,
    MaterializationMissing = 1,
    InvalidRegionCount = 2,
    InvalidRegionShape = 3,
    UnsortedRegion = 4,
    OverlappingRegion = 5,
    NoConventionalMemory = 6,
    NoFreePages = 7,
    NodeCapacityTooSmall = 8,
    NodeValidationFailed = 9,
    NodeCountMismatch = 10,
    TotalMismatch = 11
}

internal struct EfiBootPageAllocatorReadinessResult
{
    public EfiBootPageAllocatorReadinessStatus Status;
    public bool IsReady;
    public ulong RegionCount;
    public ulong ConventionalPages;
    public ulong ConventionalBytes;
    public int FreeNodeCount;
    public ulong FreePages;
    public ulong FreeBytes;
    public ulong PageSize;
}

#if ACPI_PHYSICAL_DISCOVERY_PROBE
internal unsafe struct AcpiRsdpScratchBuffer
{
    public fixed byte Bytes[4096];
}

internal unsafe struct AcpiRootSdtBuffer
{
    public fixed byte Bytes[65536];
}

internal unsafe struct AcpiChildHeaderScratchBuffer
{
    public fixed byte Bytes[36];
}

internal unsafe struct AcpiChildTableBuffer
{
    public fixed byte Bytes[65536];
}

internal unsafe struct AcpiPointerScratchBuffer
{
    public fixed ulong Slots[256];
}

internal unsafe struct AcpiEcamSegmentScratchBuffer
{
    public fixed ulong Slots[64];
}
#endif

internal struct EfiGuid
{
    public uint Data1;
    public ushort Data2;
    public ushort Data3;
    public byte Data4_0;
    public byte Data4_1;
    public byte Data4_2;
    public byte Data4_3;
    public byte Data4_4;
    public byte Data4_5;
    public byte Data4_6;
    public byte Data4_7;
}

internal unsafe struct EfiSerialIoProtocol
{
    public uint Revision;
    public void* Reset;
    public void* SetAttributes;
    public void* SetControl;
    public void* GetControl;
    public delegate* unmanaged<EfiSerialIoProtocol*, ulong*, void*, ulong> Write;
    public void* Read;
    public void* Mode;
}

internal struct EfiConsoleCharacter
{
    public ushort Value;
    public ushort NullTerminator;
}

#pragma warning restore 649
