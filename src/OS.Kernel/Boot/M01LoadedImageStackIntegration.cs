namespace OS.Boot;
/// <summary>
/// Composes stage-one host evidence without allocating, invoking firmware,
/// changing page protections, or executing the stack-switch entry.
/// </summary>

public static unsafe class M01LoadedImageStackIntegration
{
    public const ulong RequiredAllocationRecordCount = 2;
    public const ulong CodeAllocationRecordIndex = 0;
    public const ulong DataAllocationRecordIndex = 1;
    public const ulong RequiredEvidenceScratchAlignment = 8;
    public const bool CallsFirmwarePageAllocator = false;
    public const bool InvokesFirmware = false;
    public const bool ExecutesStackSwitch = false;
    public const bool IsGuestEvidence = false;
    public const bool MakesGuardInaccessible = false;
    public const bool EnforcesWriteXorExecute = false;
    public const bool HostAuditResultMayCrossStackSwitch = false;
    public const bool StaticTransitionRequiresHostAuditResult = false;
    private const ulong RequiredTableAlignment = 8;
    private const ulong MaximumUnsignedLong = ~0UL;
    // UEFI specification values kept local so the frozen M01 EFI source
    // closure does not acquire the host-side UefiMemoryType catalog.
    private const uint EfiLoaderCodeMemoryType = 1;
    private const uint EfiLoaderDataMemoryType = 2;
    public static ulong RequiredEvidenceScratchBytes => (ulong)sizeof(M01LoadedImageStackEvidence);

    public static M01LoadedImageStackIntegrationResult Prepare(EfiLoadedImageRangeCapturePlan loadedImage, M01LoadedImageVirtualMappingEvidence loadedImageVirtualMapping, M01StageOneAddressMappingEvidence stageOneAddressMapping, BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, M01StageOneAllocationEvidence allocationEvidence, M01StageOneMapLedgerEvidence mapLedgerEvidence, OS.Kernel.Memory.BootReservedRange* reservationLedger, ulong reservationCount, ulong reservationCapacity, byte* codePage, ulong codePageCapacity, ulong materializedPageByteLength, ulong stubEntryOffset, ulong declaredStubByteLength, VirtualAddress continuationAddress, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity)
    {
        return PrepareWithStubByteMode(loadedImage, loadedImageVirtualMapping, stageOneAddressMapping, allocationRecords, allocationRecordCount, allocationRecordCapacity, allocationEvidence, mapLedgerEvidence, reservationLedger, reservationCount, reservationCapacity, codePage, codePageCapacity, materializedPageByteLength, stubEntryOffset, declaredStubByteLength, continuationAddress, staticState, staticStateCapacity, M01StageOneStubByteMode.EmitAndVerify);
    }

    public static M01LoadedImageStackIntegrationResult PrepareWithStubByteMode(EfiLoadedImageRangeCapturePlan loadedImage, M01LoadedImageVirtualMappingEvidence loadedImageVirtualMapping, M01StageOneAddressMappingEvidence stageOneAddressMapping, BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, M01StageOneAllocationEvidence allocationEvidence, M01StageOneMapLedgerEvidence mapLedgerEvidence, OS.Kernel.Memory.BootReservedRange* reservationLedger, ulong reservationCount, ulong reservationCapacity, byte* codePage, ulong codePageCapacity, ulong materializedPageByteLength, ulong stubEntryOffset, ulong declaredStubByteLength, VirtualAddress continuationAddress, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity, M01StageOneStubByteMode stubByteMode)
    {
        M01LoadedImageStackEvidence evidence = default;
        M01LoadedImageStackStaticPreparationResult preparation = PrepareWithCallerOwnedEvidenceScratch(loadedImage, loadedImageVirtualMapping, stageOneAddressMapping, allocationRecords, allocationRecordCount, allocationRecordCapacity, allocationEvidence, mapLedgerEvidence, reservationLedger, reservationCount, reservationCapacity, codePage, codePageCapacity, materializedPageByteLength, stubEntryOffset, declaredStubByteLength, continuationAddress, staticState, staticStateCapacity, &evidence, (ulong)sizeof(M01LoadedImageStackEvidence), false, stubByteMode);
        return preparation.IsPrepared ? M01LoadedImageStackIntegrationResult.Success(preparation.Checkpoint, evidence) : M01LoadedImageStackIntegrationResult.Fail(preparation.Status, preparation.Checkpoint, evidence);
    }

    public static M01LoadedImageStackStaticPreparationResult PrepareFromStaticScratch(EfiLoadedImageRangeCapturePlan loadedImage, M01LoadedImageVirtualMappingEvidence loadedImageVirtualMapping, M01StageOneAddressMappingEvidence stageOneAddressMapping, BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, M01StageOneAllocationEvidence allocationEvidence, M01StageOneMapLedgerEvidence mapLedgerEvidence, OS.Kernel.Memory.BootReservedRange* reservationLedger, ulong reservationCount, ulong reservationCapacity, byte* codePage, ulong codePageCapacity, ulong materializedPageByteLength, ulong stubEntryOffset, ulong declaredStubByteLength, VirtualAddress continuationAddress, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity, M01LoadedImageStackEvidence* evidenceScratch, ulong evidenceScratchCapacity)
    {
        if (!ValidateFirmwareEvidenceScratchPreflight(loadedImage, loadedImageVirtualMapping, allocationRecords, allocationRecordCount, allocationRecordCapacity, mapLedgerEvidence, reservationLedger, reservationCount, reservationCapacity, codePage, codePageCapacity, continuationAddress, staticState, staticStateCapacity, evidenceScratch, evidenceScratchCapacity, out M01LoadedImageStackStatus preflightStatus))
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(preflightStatus, M01LoadedImageStackCheckpoint.Uninitialized);
        }

        return PrepareWithCallerOwnedEvidenceScratch(loadedImage, loadedImageVirtualMapping, stageOneAddressMapping, allocationRecords, allocationRecordCount, allocationRecordCapacity, allocationEvidence, mapLedgerEvidence, reservationLedger, reservationCount, reservationCapacity, codePage, codePageCapacity, materializedPageByteLength, stubEntryOffset, declaredStubByteLength, continuationAddress, staticState, staticStateCapacity, evidenceScratch, evidenceScratchCapacity, true, M01StageOneStubByteMode.EmitAndVerify);
    }

    private static M01LoadedImageStackStaticPreparationResult PrepareWithCallerOwnedEvidenceScratch(EfiLoadedImageRangeCapturePlan loadedImage, M01LoadedImageVirtualMappingEvidence loadedImageVirtualMapping, M01StageOneAddressMappingEvidence stageOneAddressMapping, BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, M01StageOneAllocationEvidence allocationEvidence, M01StageOneMapLedgerEvidence mapLedgerEvidence, OS.Kernel.Memory.BootReservedRange* reservationLedger, ulong reservationCount, ulong reservationCapacity, byte* codePage, ulong codePageCapacity, ulong materializedPageByteLength, ulong stubEntryOffset, ulong declaredStubByteLength, VirtualAddress continuationAddress, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity, M01LoadedImageStackEvidence* evidenceScratch, ulong evidenceScratchCapacity, bool requireEvidenceScratchInLoadedImage, M01StageOneStubByteMode stubByteMode)
    {
        if (!ValidateEvidenceScratch(loadedImageVirtualMapping, evidenceScratch, evidenceScratchCapacity, requireEvidenceScratchInLoadedImage))
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.EvidenceScratchRejected, M01LoadedImageStackCheckpoint.Uninitialized);
        }

        ref M01LoadedImageStackEvidence evidence = ref *evidenceScratch;
        evidence.LoadedImageCaptureValue = loadedImage;
        evidence.LoadedImageVirtualMappingValue = loadedImageVirtualMapping;
        evidence.StageOneAddressMappingValue = stageOneAddressMapping;
        evidence.StubByteModeValue = stubByteMode;
        evidence.ObservedRspValue = default;
        if (stubByteMode != M01StageOneStubByteMode.EmitAndVerify && stubByteMode != M01StageOneStubByteMode.VerifyExisting)
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.StubByteModeRejected, M01LoadedImageStackCheckpoint.Uninitialized);
        }

        if (!loadedImage.Succeeded)
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.LoadedImageRejected, M01LoadedImageStackCheckpoint.Uninitialized);
        }

        if (!loadedImageVirtualMapping.IsValidFor(loadedImage))
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.LoadedImageVirtualMappingRejected, M01LoadedImageStackCheckpoint.Uninitialized);
        }

        M01LoadedImageStackCheckpoint checkpoint = M01LoadedImageStackCheckpoint.LoadedImageValidated;
        OwnedBootStackSwitchStatus allocationStatus = ValidateAllocationTableMetadata(allocationRecords, allocationRecordCount, allocationRecordCapacity, out AddressValueStatus allocationAddressStatus, out ulong invalidAllocationIndex);
        if (allocationStatus == OwnedBootStackSwitchStatus.Success)
        {
            allocationStatus = ValidateAllocationRecords(allocationRecords, out allocationAddressStatus, out invalidAllocationIndex);
        }

        evidence.AllocationValidationStatusValue = allocationStatus;
        evidence.AllocationAddressStatusValue = allocationAddressStatus;
        evidence.InvalidAllocationRecordIndexValue = invalidAllocationIndex;
        if (allocationStatus != OwnedBootStackSwitchStatus.Success)
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.AllocationTableRejected, checkpoint);
        }

        evidence.CodeAllocationValue = allocationRecords[CodeAllocationRecordIndex];
        evidence.DataAllocationValue = allocationRecords[DataAllocationRecordIndex];
        evidence.AllocationEvidenceValue = allocationEvidence;
        if (!allocationEvidence.IsValidFor(allocationRecords, allocationRecordCount, allocationRecordCapacity))
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.AllocationEvidenceRejected, checkpoint);
        }

        checkpoint = M01LoadedImageStackCheckpoint.AllocationEvidenceValidated;
        evidence.MapLedgerEvidenceValue = mapLedgerEvidence;
        if (!ValidateLedgerMetadata(reservationLedger, reservationCount, reservationCapacity) || !mapLedgerEvidence.IsValidFor(allocationEvidence, reservationLedger, reservationCount, reservationCapacity))
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.MapLedgerEvidenceRejected, checkpoint);
        }

        if (!stageOneAddressMapping.IsValidFor(evidence.CodeAllocationValue, evidence.DataAllocationValue, mapLedgerEvidence.RefreshedMap))
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.StageOneAddressMappingRejected, checkpoint);
        }

        OS.Kernel.Memory.BootReservationTableValidationResult reservationValidation = OS.Kernel.Memory.BootReservationTableValidator.Validate(reservationLedger, reservationCount, reservationCapacity);
        evidence.ReservationValidationValue = reservationValidation;
        if (!reservationValidation.Succeeded)
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.ReservationLedgerRejected, checkpoint);
        }

        if (!TryBindReservationIdentities(loadedImage, allocationRecords, reservationLedger, reservationCount, out OS.Kernel.Memory.BootReservedRange stubReservation, out OS.Kernel.Memory.BootReservedRange guardReservation, out OS.Kernel.Memory.BootReservedRange stackReservation))
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.ReservationIdentityRejected, checkpoint);
        }

        if (!ValidateOwnedMapBufferReservation(loadedImage.Range, stageOneAddressMapping, mapLedgerEvidence.RefreshedMap, allocationRecords, reservationLedger, reservationCount))
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.ReservationIdentityRejected, checkpoint);
        }

        evidence.StubReservationValue = stubReservation;
        evidence.GuardReservationValue = guardReservation;
        evidence.StackReservationValue = stackReservation;
        checkpoint = M01LoadedImageStackCheckpoint.MapLedgerValidated;
        OS.Kernel.Memory.BootReservedRange* layoutReservations = reservationLedger + reservationValidation.BootStackGuardIndex;
        ulong dataPageCount = allocationRecords[DataAllocationRecordIndex].PageCount;
        PhysicalAddress.TryCreateFromUefiBoundary(allocationRecords[DataAllocationRecordIndex].PhysicalStart, out PhysicalAddress dataPhysicalStart);
        PhysicalAddress.TryCreateFromUefiBoundary(allocationRecords[DataAllocationRecordIndex].PhysicalEndExclusive, out PhysicalAddress physicalStackTop);
        OwnedBootStackLayoutResult layoutResult = OwnedBootStackLayout.Create(layoutReservations, OwnedBootStackLayout.RequiredReservationCount, OwnedBootStackLayout.RequiredReservationCount, dataPhysicalStart, dataPageCount, physicalStackTop);
        evidence.StackLayoutResultValue = layoutResult;
        if (!layoutResult.Succeeded)
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.StackLayoutRejected, checkpoint);
        }

        OwnedBootStackLayoutResult readyLayout = layoutResult.Layout.TransitionTo(OwnedBootStackLifetime.Ready);
        evidence.StackLayoutResultValue = readyLayout;
        if (!readyLayout.Succeeded)
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.StackLifetimeRejected, checkpoint);
        }

        checkpoint = M01LoadedImageStackCheckpoint.StackLayoutReady;
        if (!ValidateStaticAndCodeStorage(loadedImageVirtualMapping, stageOneAddressMapping, allocationRecords, mapLedgerEvidence.RefreshedMap, codePage, codePageCapacity, materializedPageByteLength, continuationAddress, staticState, staticStateCapacity, reservationLedger, reservationCount, evidenceScratch, evidenceScratchCapacity, requireEvidenceScratchInLoadedImage))
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.StaticStateRejected, checkpoint);
        }

        evidence.StaticStateAddressValue = (ulong)staticState;
        evidence.StaticStateCapacityValue = staticStateCapacity;
        VirtualAddress.TryCreateFromUefiBoundary((ulong)&staticState->HandoffStorage, out VirtualAddress handoffAddress);
        VirtualAddress.TryCreateFromUefiBoundary((ulong)&staticState->SwitchStateStorage, out VirtualAddress stateAddress);
        OwnedBootStackSwitchPlanResult switchPlanResult = OwnedBootStackSwitchPlan.Create(allocationRecords, allocationRecordCount, allocationRecordCapacity, readyLayout.Layout, stubEntryOffset, declaredStubByteLength, handoffAddress, OwnedBootStackSwitchHandoff.StructureSize, OwnedBootStackSwitchHandoffProvenance.CallerAttestedStaticOwnedData, stateAddress, OwnedBootStackSwitchStateRecord.StructureSize, OwnedBootStackSwitchStateStorageProvenance.CallerAttestedStaticOwnedData, continuationAddress, OwnedBootStackSwitchContinuationContract.StaticNonReturningRspAndHandoff);
        evidence.SwitchPlanResultValue = switchPlanResult;
        if (!switchPlanResult.Succeeded)
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.SwitchPlanRejected, checkpoint);
        }

        checkpoint = M01LoadedImageStackCheckpoint.SwitchPlanReady;
        if (stubByteMode == M01StageOneStubByteMode.EmitAndVerify)
        {
            OwnedBootStackSwitchBytePlanResult emission = switchPlanResult.Plan.EmitAuditedStubPage(codePage, codePageCapacity, materializedPageByteLength);
            evidence.StubEmissionValue = emission;
            if (!emission.Succeeded)
            {
                return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.StubEmissionRejected, checkpoint);
            }
        }

        OwnedBootStackSwitchBytePlanResult verification = switchPlanResult.Plan.VerifyAuditedStubPage(codePage, codePageCapacity, materializedPageByteLength, declaredStubByteLength);
        evidence.StubVerificationValue = verification;
        if (!verification.Succeeded)
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.StubVerificationRejected, checkpoint);
        }

        if (stubByteMode == M01StageOneStubByteMode.VerifyExisting)
        {
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.StubByteModeRejected, checkpoint);
        }

        checkpoint = M01LoadedImageStackCheckpoint.StubVerified;
        staticState->HeaderSize = (uint)M01LoadedImageStackStaticState.RequiredStorageBytes;
        staticState->HeaderVersion = M01LoadedImageStackStaticState.CurrentVersion;
        staticState->CoordinatorStatus = M01LoadedImageStackStatus.Success;
        staticState->CoordinatorCheckpoint = checkpoint;
        staticState->Reserved0 = 0;
        staticState->Reserved1 = 0;
        staticState->Reserved2 = 0;
        staticState->Reserved3 = 0;
        staticState->Reserved4 = 0;
        staticState->Reserved5 = 0;
        staticState->HandoffStorage = switchPlanResult.Plan.Handoff;
        staticState->StackLayoutStorage = readyLayout.Layout;
        if (!HandoffsEqual(staticState->HandoffStorage, switchPlanResult.Plan.Handoff))
        {
            staticState->CoordinatorStatus = M01LoadedImageStackStatus.HandoffMaterializationRejected;
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.HandoffMaterializationRejected, checkpoint);
        }

        OwnedBootStackSwitchStateResult initialized = switchPlanResult.Plan.InitializeOneShotState(&staticState->SwitchStateStorage, OwnedBootStackSwitchStateRecord.StructureSize);
        evidence.SwitchStateResultValue = initialized;
        if (!initialized.Succeeded)
        {
            staticState->CoordinatorStatus = M01LoadedImageStackStatus.StateInitializationRejected;
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.StateInitializationRejected, checkpoint);
        }

        OwnedBootStackSwitchStateResult armed = switchPlanResult.Plan.Arm(&staticState->SwitchStateStorage, OwnedBootStackSwitchStateRecord.StructureSize, verification);
        evidence.SwitchStateResultValue = armed;
        if (!armed.Succeeded)
        {
            staticState->CoordinatorStatus = M01LoadedImageStackStatus.ArmRejected;
            return M01LoadedImageStackStaticPreparationResult.Fail(M01LoadedImageStackStatus.ArmRejected, checkpoint);
        }

        checkpoint = M01LoadedImageStackCheckpoint.SwitchArmed;
        staticState->CoordinatorCheckpoint = checkpoint;
        return M01LoadedImageStackStaticPreparationResult.Success(checkpoint);
    }

    public static M01LoadedImageStackStaticTransitionResult RecordTransferFromStaticState(M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity)
    {
        if (!ValidateStaticTransitionState(staticState, staticStateCapacity, M01LoadedImageStackCheckpoint.SwitchArmed))
        {
            return M01LoadedImageStackStaticTransitionResult.Fail(M01LoadedImageStackStatus.IllegalCoordinatorState, M01LoadedImageStackCheckpoint.Uninitialized);
        }

        OwnedBootStackSwitchPlan plan = staticState->SwitchStateStorage.Plan;
        OwnedBootStackSwitchStateResult transfer = plan.RecordTransfer(&staticState->SwitchStateStorage, OwnedBootStackSwitchStateRecord.StructureSize);
        if (!transfer.Succeeded)
        {
            staticState->CoordinatorStatus = M01LoadedImageStackStatus.TransferRejected;
            return M01LoadedImageStackStaticTransitionResult.Fail(M01LoadedImageStackStatus.TransferRejected, M01LoadedImageStackCheckpoint.SwitchArmed, transfer);
        }

        M01LoadedImageStackTransferTarget target = CreateTransferTarget(plan);
        if (!target.IsValidFor(plan))
        {
            staticState->CoordinatorStatus = M01LoadedImageStackStatus.TransferRejected;
            return M01LoadedImageStackStaticTransitionResult.Fail(M01LoadedImageStackStatus.TransferRejected, M01LoadedImageStackCheckpoint.SwitchArmed, transfer);
        }

        staticState->CoordinatorCheckpoint = M01LoadedImageStackCheckpoint.TransferRecorded;
        OwnedBootStackLayoutResult readyLayout = CreateReadyLayoutEvidence(staticState->StackLayoutStorage);
        return M01LoadedImageStackStaticTransitionResult.Success(M01LoadedImageStackCheckpoint.TransferRecorded, transfer, readyLayout, target);
    }

    public static M01LoadedImageStackStaticTransitionResult ObserveContinuationRspFromStaticState(M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity, VirtualAddress observedRsp)
    {
        if (!ValidateStaticTransitionState(staticState, staticStateCapacity, M01LoadedImageStackCheckpoint.TransferRecorded))
        {
            return M01LoadedImageStackStaticTransitionResult.Fail(M01LoadedImageStackStatus.IllegalCoordinatorState, M01LoadedImageStackCheckpoint.Uninitialized);
        }

        OwnedBootStackSwitchPlan plan = staticState->SwitchStateStorage.Plan;
        OwnedBootStackSwitchStateResult observation = plan.AuditContinuationRsp(&staticState->SwitchStateStorage, OwnedBootStackSwitchStateRecord.StructureSize, observedRsp);
        if (!observation.Succeeded)
        {
            staticState->CoordinatorStatus = M01LoadedImageStackStatus.ContinuationRspRejected;
            return M01LoadedImageStackStaticTransitionResult.Fail(M01LoadedImageStackStatus.ContinuationRspRejected, M01LoadedImageStackCheckpoint.TransferRecorded, observation);
        }

        OwnedBootStackLayoutResult activeLayout = staticState->StackLayoutStorage.TransitionTo(OwnedBootStackLifetime.Active);
        if (!activeLayout.Succeeded)
        {
            staticState->CoordinatorStatus = M01LoadedImageStackStatus.StackLifetimeRejected;
            return M01LoadedImageStackStaticTransitionResult.Fail(M01LoadedImageStackStatus.StackLifetimeRejected, M01LoadedImageStackCheckpoint.TransferRecorded, observation, activeLayout);
        }

        staticState->StackLayoutStorage = activeLayout.Layout;
        staticState->CoordinatorCheckpoint = M01LoadedImageStackCheckpoint.ContinuationRspContained;
        M01LoadedImageStackTransferTarget target = CreateTransferTarget(plan);
        return M01LoadedImageStackStaticTransitionResult.Success(M01LoadedImageStackCheckpoint.ContinuationRspContained, observation, activeLayout, target);
    }

    public static M01LoadedImageStackStaticTransitionResult ReportUnexpectedReturnFromStaticState(M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity)
    {
        if (!ValidateStaticTransitionState(staticState, staticStateCapacity, M01LoadedImageStackCheckpoint.TransferRecorded))
        {
            return M01LoadedImageStackStaticTransitionResult.Fail(M01LoadedImageStackStatus.IllegalCoordinatorState, M01LoadedImageStackCheckpoint.Uninitialized);
        }

        OwnedBootStackSwitchStateResult returned = staticState->SwitchStateStorage.Plan.ReportUnexpectedReturn(&staticState->SwitchStateStorage, OwnedBootStackSwitchStateRecord.StructureSize);
        staticState->CoordinatorStatus = M01LoadedImageStackStatus.UnexpectedReturn;
        return M01LoadedImageStackStaticTransitionResult.Fail(M01LoadedImageStackStatus.UnexpectedReturn, M01LoadedImageStackCheckpoint.TransferRecorded, returned);
    }

#if !NOSTDLIB_EFI
    // Host convenience only. A live continuation must use the static-state
    // APIs above and must not carry this audit aggregate across the switch.
    public static M01LoadedImageStackIntegrationResult RecordTransfer(M01LoadedImageStackIntegrationResult prepared, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity)
    {
        M01LoadedImageStackEvidence evidence = prepared.Evidence;
        if (!prepared.IsPrepared || !StaticStateMatches(prepared, staticState, staticStateCapacity, M01LoadedImageStackCheckpoint.SwitchArmed))
        {
            return M01LoadedImageStackIntegrationResult.Fail(M01LoadedImageStackStatus.IllegalCoordinatorState, prepared.LastCompletedCheckpoint, evidence);
        }

        M01LoadedImageStackStaticTransitionResult transition = RecordTransferFromStaticState(staticState, staticStateCapacity);
        evidence.SwitchStateResultValue = transition.StateResult;
        evidence.StackLayoutResultValue = transition.LayoutResult;
        if (!transition.TransferRecorded)
        {
            return M01LoadedImageStackIntegrationResult.Fail(transition.Status, M01LoadedImageStackCheckpoint.SwitchArmed, evidence);
        }

        return M01LoadedImageStackIntegrationResult.Success(M01LoadedImageStackCheckpoint.TransferRecorded, evidence, transition.TransferTarget);
    }

    public static M01LoadedImageStackIntegrationResult ObserveContinuationRsp(M01LoadedImageStackIntegrationResult transferred, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity, VirtualAddress observedRsp)
    {
        M01LoadedImageStackEvidence evidence = transferred.Evidence;
        if (!transferred.IsTransferRecorded || !StaticStateMatches(transferred, staticState, staticStateCapacity, M01LoadedImageStackCheckpoint.TransferRecorded))
        {
            return M01LoadedImageStackIntegrationResult.Fail(M01LoadedImageStackStatus.IllegalCoordinatorState, transferred.LastCompletedCheckpoint, evidence);
        }

        M01LoadedImageStackStaticTransitionResult transition = ObserveContinuationRspFromStaticState(staticState, staticStateCapacity, observedRsp);
        evidence.SwitchStateResultValue = transition.StateResult;
        evidence.StackLayoutResultValue = transition.LayoutResult;
        evidence.ObservedRspValue = observedRsp;
        if (!transition.ContinuationRspContained)
        {
            return M01LoadedImageStackIntegrationResult.Fail(transition.Status, M01LoadedImageStackCheckpoint.TransferRecorded, evidence);
        }

        return M01LoadedImageStackIntegrationResult.Success(M01LoadedImageStackCheckpoint.ContinuationRspContained, evidence, transition.TransferTarget);
    }

    public static M01LoadedImageStackIntegrationResult ReportUnexpectedReturn(M01LoadedImageStackIntegrationResult transferred, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity)
    {
        M01LoadedImageStackEvidence evidence = transferred.Evidence;
        if (!transferred.IsTransferRecorded || !StaticStateMatches(transferred, staticState, staticStateCapacity, M01LoadedImageStackCheckpoint.TransferRecorded))
        {
            return M01LoadedImageStackIntegrationResult.Fail(M01LoadedImageStackStatus.IllegalCoordinatorState, transferred.LastCompletedCheckpoint, evidence);
        }

        M01LoadedImageStackStaticTransitionResult transition = ReportUnexpectedReturnFromStaticState(staticState, staticStateCapacity);
        evidence.SwitchStateResultValue = transition.StateResult;
        return M01LoadedImageStackIntegrationResult.Fail(transition.Status, M01LoadedImageStackCheckpoint.TransferRecorded, evidence);
    }

#endif
    private static M01LoadedImageStackTransferTarget CreateTransferTarget(OwnedBootStackSwitchPlan plan)
    {
        return new M01LoadedImageStackTransferTarget(plan.StubVirtualEntry, plan.PreTransferStackPointer, plan.Handoff.StorageStart, plan.Handoff.Continuation);
    }

    private static OwnedBootStackLayoutResult CreateReadyLayoutEvidence(OwnedBootStackLayout layout)
    {
        if (!layout.IsValid || layout.Lifetime != OwnedBootStackLifetime.Ready)
        {
            return default;
        }

        return OwnedBootStackLayoutResult.Create(OwnedBootStackLayoutStatus.Success, AddressValueStatus.Success, OS.Kernel.Memory.BootReservationTableStatus.Uninitialized, OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized, OwnedBootStackLifetime.Reserved, OwnedBootStackLifetime.Ready, OwnedBootStackLifetime.Ready, 0, 0, 0, OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex, OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex, layout);
    }

    private static bool ValidateStaticTransitionState(M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity, M01LoadedImageStackCheckpoint requiredCheckpoint)
    {
        if (staticState == null)
        {
            return false;
        }

        ulong stateAddress = (ulong)staticState;
        if ((stateAddress & (M01LoadedImageStackStaticState.RequiredStorageAlignment - 1)) != 0 || staticStateCapacity < M01LoadedImageStackStaticState.RequiredStorageBytes || stateAddress > MaximumUnsignedLong - M01LoadedImageStackStaticState.RequiredStorageBytes || !staticState->IsInitialized || staticState->CoordinatorStatus != M01LoadedImageStackStatus.Success || staticState->CoordinatorCheckpoint != requiredCheckpoint || (ulong)&staticState->HandoffStorage - stateAddress != M01LoadedImageStackStaticState.HandoffStorageOffset || (ulong)&staticState->SwitchStateStorage - stateAddress != M01LoadedImageStackStaticState.SwitchStateStorageOffset || (ulong)&staticState->StackLayoutStorage - stateAddress != M01LoadedImageStackStaticState.StackLayoutStorageOffset)
        {
            return false;
        }

        OwnedBootStackSwitchPlan plan = staticState->SwitchStateStorage.Plan;
        if (!plan.IsValid || !HandoffsEqual(staticState->HandoffStorage, plan.Handoff) || !StaticLayoutTopologyEquals(staticState->StackLayoutStorage, plan.StackLayout) || staticState->StackLayoutStorage.Lifetime != OwnedBootStackLifetime.Ready)
        {
            return false;
        }

        if (requiredCheckpoint == M01LoadedImageStackCheckpoint.SwitchArmed)
        {
            return staticState->SwitchStateStorage.State == OwnedBootStackSwitchState.Armed;
        }

        return requiredCheckpoint == M01LoadedImageStackCheckpoint.TransferRecorded && staticState->SwitchStateStorage.State == OwnedBootStackSwitchState.TransferRecorded;
    }

    private static OwnedBootStackSwitchStatus ValidateAllocationTableMetadata(BootMemoryRegionRecord* records, ulong count, ulong capacity, out AddressValueStatus addressStatus, out ulong invalidRecordIndex)
    {
        addressStatus = AddressValueStatus.Uninitialized;
        invalidRecordIndex = OwnedBootStackSwitchPlan.NoIndex;
        if (count < RequiredAllocationRecordCount)
        {
            return OwnedBootStackSwitchStatus.InvalidAllocationRecordCount;
        }

        if (count > RequiredAllocationRecordCount)
        {
            return OwnedBootStackSwitchStatus.TooManyAllocationRecords;
        }

        if (capacity < count)
        {
            return OwnedBootStackSwitchStatus.AllocationRecordCapacityTooSmall;
        }

        if (records == null)
        {
            return OwnedBootStackSwitchStatus.MissingAllocationRecords;
        }

        ulong address = (ulong)records;
        ulong byteLength = RequiredAllocationRecordCount * (ulong)sizeof(BootMemoryRegionRecord);
        if ((address & (RequiredTableAlignment - 1)) != 0)
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return OwnedBootStackSwitchStatus.MisalignedAllocationRecords;
        }

        if (address > MaximumUnsignedLong - byteLength)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return OwnedBootStackSwitchStatus.AllocationRecordAddressRangeOverflow;
        }

        addressStatus = AddressValueStatus.Success;
        return OwnedBootStackSwitchStatus.Success;
    }

    private static OwnedBootStackSwitchStatus ValidateAllocationRecords(BootMemoryRegionRecord* records, out AddressValueStatus addressStatus, out ulong invalidRecordIndex)
    {
        BootMemoryRegionRecord code = records[CodeAllocationRecordIndex];
        BootMemoryRegionRecord data = records[DataAllocationRecordIndex];
        OwnedBootStackSwitchStatus status = ValidateAllocationRecord(code, isCode: true, out addressStatus);
        if (status != OwnedBootStackSwitchStatus.Success)
        {
            invalidRecordIndex = CodeAllocationRecordIndex;
            return status;
        }

        status = ValidateAllocationRecord(data, isCode: false, out addressStatus);
        if (status != OwnedBootStackSwitchStatus.Success)
        {
            invalidRecordIndex = DataAllocationRecordIndex;
            return status;
        }

        if (data.PageCount < 2)
        {
            addressStatus = AddressValueStatus.ZeroLength;
            invalidRecordIndex = DataAllocationRecordIndex;
            return OwnedBootStackSwitchStatus.InvalidDataPageCount;
        }

        ulong codeVirtualEnd = code.VirtualStart + code.ByteLength;
        ulong dataVirtualEnd = data.VirtualStart + data.ByteLength;
        if (RangesOverlap(code.PhysicalStart, code.PhysicalEndExclusive, data.PhysicalStart, data.PhysicalEndExclusive))
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            invalidRecordIndex = DataAllocationRecordIndex;
            return OwnedBootStackSwitchStatus.PhysicalAllocationOverlap;
        }

        if (RangesOverlap(code.VirtualStart, codeVirtualEnd, data.VirtualStart, dataVirtualEnd))
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            invalidRecordIndex = DataAllocationRecordIndex;
            return OwnedBootStackSwitchStatus.VirtualAllocationOverlap;
        }

        addressStatus = AddressValueStatus.Success;
        invalidRecordIndex = OwnedBootStackSwitchPlan.NoIndex;
        return OwnedBootStackSwitchStatus.Success;
    }

    private static OwnedBootStackSwitchStatus ValidateAllocationRecord(BootMemoryRegionRecord record, bool isCode, out AddressValueStatus addressStatus)
    {
        uint expectedType = isCode ? EfiLoaderCodeMemoryType : EfiLoaderDataMemoryType;
        if (record.UefiType != expectedType)
        {
            addressStatus = AddressValueStatus.Uninitialized;
            return isCode ? OwnedBootStackSwitchStatus.InvalidCodeMemoryType : OwnedBootStackSwitchStatus.InvalidDataMemoryType;
        }

        if (record.PhysicalStart == 0)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return isCode ? OwnedBootStackSwitchStatus.InvalidCodePhysicalStart : OwnedBootStackSwitchStatus.InvalidDataPhysicalStart;
        }

        if ((record.PhysicalStart & (BootMemoryRegionRecord.PageSize - 1)) != 0)
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return isCode ? OwnedBootStackSwitchStatus.MisalignedCodePhysicalStart : OwnedBootStackSwitchStatus.MisalignedDataPhysicalStart;
        }

        if (record.VirtualStart == 0)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return isCode ? OwnedBootStackSwitchStatus.InvalidCodeVirtualStart : OwnedBootStackSwitchStatus.InvalidDataVirtualStart;
        }

        if ((record.VirtualStart & (BootMemoryRegionRecord.PageSize - 1)) != 0)
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return isCode ? OwnedBootStackSwitchStatus.MisalignedCodeVirtualStart : OwnedBootStackSwitchStatus.MisalignedDataVirtualStart;
        }

        if (record.PageCount == 0 || (isCode && record.PageCount != 1))
        {
            addressStatus = AddressValueStatus.ZeroLength;
            return isCode ? OwnedBootStackSwitchStatus.InvalidCodePageCount : OwnedBootStackSwitchStatus.InvalidDataPageCount;
        }

        if (record.PageCount > MaximumUnsignedLong / BootMemoryRegionRecord.PageSize)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return isCode ? OwnedBootStackSwitchStatus.CodeByteCountOverflow : OwnedBootStackSwitchStatus.DataByteCountOverflow;
        }

        ulong expectedBytes = record.PageCount * BootMemoryRegionRecord.PageSize;
        if (record.ByteLength != expectedBytes)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return isCode ? OwnedBootStackSwitchStatus.CodeByteLengthMismatch : OwnedBootStackSwitchStatus.DataByteLengthMismatch;
        }

        if (record.PhysicalStart > MaximumUnsignedLong - expectedBytes)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return isCode ? OwnedBootStackSwitchStatus.CodePhysicalEndOverflow : OwnedBootStackSwitchStatus.DataPhysicalEndOverflow;
        }

        if (record.PhysicalEndExclusive != record.PhysicalStart + expectedBytes)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return isCode ? OwnedBootStackSwitchStatus.CodePhysicalEndMismatch : OwnedBootStackSwitchStatus.DataPhysicalEndMismatch;
        }

        if (record.VirtualStart > MaximumUnsignedLong - expectedBytes)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return isCode ? OwnedBootStackSwitchStatus.CodeVirtualEndOverflow : OwnedBootStackSwitchStatus.DataVirtualEndOverflow;
        }

        addressStatus = AddressValueStatus.Success;
        return OwnedBootStackSwitchStatus.Success;
    }

    private static bool ValidateLedgerMetadata(OS.Kernel.Memory.BootReservedRange* ledger, ulong count, ulong capacity)
    {
        if (count == 0 || count > OS.Kernel.Memory.BootReservationTableValidator.MaximumReservationCount || capacity < count || ledger == null)
        {
            return false;
        }

        ulong address = (ulong)ledger;
        ulong recordSize = (ulong)sizeof(OS.Kernel.Memory.BootReservedRange);
        return (address & (RequiredTableAlignment - 1)) == 0 && count <= MaximumUnsignedLong / recordSize && address <= MaximumUnsignedLong - (count * recordSize);
    }

    private static bool TryBindReservationIdentities(EfiLoadedImageRangeCapturePlan loadedImage, BootMemoryRegionRecord* allocations, OS.Kernel.Memory.BootReservedRange* ledger, ulong count, out OS.Kernel.Memory.BootReservedRange stub, out OS.Kernel.Memory.BootReservedRange guard, out OS.Kernel.Memory.BootReservedRange stack)
    {
        stub = default;
        guard = default;
        stack = default;
        PhysicalAddress.TryCreateFromUefiBoundary(allocations[CodeAllocationRecordIndex].PhysicalStart, out PhysicalAddress codePhysicalStart);
        if (OS.Kernel.Memory.BootReservedRange.TryCreate(OS.Kernel.Memory.BootReservationKind.BootStackSwitchStub, codePhysicalStart, allocations[CodeAllocationRecordIndex].PageCount, out OS.Kernel.Memory.BootReservedRange expectedStub) != OS.Kernel.Memory.BootReservedRangeStatus.Success)
        {
            return false;
        }

        ulong loadedCount = 0;
        ulong stubCount = 0;
        ulong guardCount = 0;
        ulong stackCount = 0;
        for (ulong index = 0; index < count; index++)
        {
            OS.Kernel.Memory.BootReservedRange current = ledger[index];
            if (current.Kind == OS.Kernel.Memory.BootReservationKind.LoadedImage)
            {
                loadedCount++;
                if (!current.Equals(loadedImage.Range.Reservation))
                {
                    return false;
                }
            }
            else if (current.Kind == OS.Kernel.Memory.BootReservationKind.BootStackSwitchStub)
            {
                stubCount++;
                stub = current;
                if (!current.Equals(expectedStub))
                {
                    return false;
                }
            }
            else if (current.Kind == OS.Kernel.Memory.BootReservationKind.BootStackGuard)
            {
                guardCount++;
                guard = current;
            }
            else if (current.Kind == OS.Kernel.Memory.BootReservationKind.BootStack)
            {
                stackCount++;
                stack = current;
            }
        }

        if (loadedCount != 1 || stubCount != 1 || guardCount != 1 || stackCount != 1)
        {
            return false;
        }

        BootMemoryRegionRecord data = allocations[DataAllocationRecordIndex];
        return guard.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong guardStart) == AddressValueStatus.Success && stack.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong stackEnd) == AddressValueStatus.Success && guardStart == data.PhysicalStart && stackEnd == data.PhysicalEndExclusive && guard.PageCount == 1 && guard.ByteLength == OS.Kernel.Memory.BootReservedRange.PageSize && guard.PageCount <= MaximumUnsignedLong - stack.PageCount && guard.PageCount + stack.PageCount == data.PageCount && guard.ByteLength <= MaximumUnsignedLong - stack.ByteLength && guard.ByteLength + stack.ByteLength == data.ByteLength;
    }

    private static bool ValidateOwnedMapBufferReservation(EfiLoadedImageRange loadedImage, M01StageOneAddressMappingEvidence addressMapping, FirmwareMemoryMapRecord map, BootMemoryRegionRecord* allocations, OS.Kernel.Memory.BootReservedRange* ledger, ulong ledgerCount)
    {
        if (!addressMapping.IsValidFor(allocations[CodeAllocationRecordIndex], allocations[DataAllocationRecordIndex], map) || addressMapping.OwnedMapPhysicalStart.TryGetRawValueForUefiBoundary(out ulong mapStart) != AddressValueStatus.Success || addressMapping.OwnedMapPhysicalStart.TryGetEndExclusive(addressMapping.OwnedMapByteLength, out PhysicalAddress mapEndAddress) != AddressValueStatus.Success || mapEndAddress.TryGetRawValueForUefiBoundary(out ulong mapEnd) != AddressValueStatus.Success)
        {
            return false;
        }

        if (RangesOverlap(mapStart, mapEnd, allocations[CodeAllocationRecordIndex].PhysicalStart, allocations[CodeAllocationRecordIndex].PhysicalEndExclusive) || RangesOverlap(mapStart, mapEnd, allocations[DataAllocationRecordIndex].PhysicalStart, allocations[DataAllocationRecordIndex].PhysicalEndExclusive) || loadedImage.ImageBase.TryGetRawValueForUefiBoundary(out ulong imageStart) != AddressValueStatus.Success || loadedImage.ImageEndExclusive.TryGetRawValueForUefiBoundary(out ulong imageEnd) != AddressValueStatus.Success)
        {
            return false;
        }

        if (mapStart >= imageStart && mapEnd <= imageEnd)
        {
            return true;
        }

        ulong ownedMapReservationCount = 0;
        bool covered = false;
        for (ulong index = 0; index < ledgerCount; index++)
        {
            OS.Kernel.Memory.BootReservedRange current = ledger[index];
            if (current.Kind != OS.Kernel.Memory.BootReservationKind.OwnedMemoryMap)
            {
                continue;
            }

            ownedMapReservationCount++;
            if (current.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong reservationStart) == AddressValueStatus.Success && current.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong reservationEnd) == AddressValueStatus.Success && mapStart >= reservationStart && mapEnd <= reservationEnd)
            {
                covered = true;
            }
        }

        return ownedMapReservationCount == 1 && covered;
    }

    private static bool ValidateEvidenceScratch(M01LoadedImageVirtualMappingEvidence loadedImageMapping, M01LoadedImageStackEvidence* evidenceScratch, ulong evidenceScratchCapacity, bool requireEvidenceScratchInLoadedImage)
    {
        ulong requiredBytes = (ulong)sizeof(M01LoadedImageStackEvidence);
        ulong scratchAddress = (ulong)evidenceScratch;
        if (evidenceScratch == null || evidenceScratchCapacity != requiredBytes || (scratchAddress & (RequiredEvidenceScratchAlignment - 1)) != 0 || scratchAddress > MaximumUnsignedLong - requiredBytes)
        {
            return false;
        }

        if (requireEvidenceScratchInLoadedImage && (loadedImageMapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong imageBase) != AddressValueStatus.Success || loadedImageMapping.VirtualImageEndExclusive.TryGetRawValueForUefiBoundary(out ulong imageEnd) != AddressValueStatus.Success || scratchAddress < imageBase || scratchAddress + requiredBytes > imageEnd))
        {
            return false;
        }

        return IsZeroed((byte*)evidenceScratch, requiredBytes);
    }

    private static bool ValidateFirmwareEvidenceScratchPreflight(EfiLoadedImageRangeCapturePlan loadedImage, M01LoadedImageVirtualMappingEvidence loadedImageMapping, BootMemoryRegionRecord* allocations, ulong allocationCount, ulong allocationCapacity, M01StageOneMapLedgerEvidence mapLedgerEvidence, OS.Kernel.Memory.BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, byte* codePage, ulong codePageCapacity, VirtualAddress continuationAddress, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity, M01LoadedImageStackEvidence* evidenceScratch, ulong evidenceScratchCapacity, out M01LoadedImageStackStatus failureStatus)
    {
        failureStatus = M01LoadedImageStackStatus.EvidenceScratchRejected;
        if (!loadedImage.Succeeded)
        {
            failureStatus = M01LoadedImageStackStatus.LoadedImageRejected;
            return false;
        }

        if (!loadedImageMapping.IsValidFor(loadedImage))
        {
            failureStatus = M01LoadedImageStackStatus.LoadedImageVirtualMappingRejected;
            return false;
        }

        if (!ValidateEvidenceScratch(loadedImageMapping, evidenceScratch, evidenceScratchCapacity, true))
        {
            return false;
        }

        ulong scratchStart = (ulong)evidenceScratch;
        ulong scratchLength = (ulong)sizeof(M01LoadedImageStackEvidence);
        ulong scratchEnd = scratchStart + scratchLength;
        OwnedBootStackSwitchStatus allocationStatus = ValidateAllocationTableMetadata(allocations, allocationCount, allocationCapacity, out AddressValueStatus _, out ulong _);
        if (allocationStatus != OwnedBootStackSwitchStatus.Success)
        {
            failureStatus = M01LoadedImageStackStatus.AllocationTableRejected;
            return false;
        }

        ulong allocationTableStart = (ulong)allocations;
        ulong allocationRecordSize = (ulong)sizeof(BootMemoryRegionRecord);
        if (allocationCapacity > MaximumUnsignedLong / allocationRecordSize || allocationTableStart > MaximumUnsignedLong - allocationCapacity * allocationRecordSize)
        {
            failureStatus = M01LoadedImageStackStatus.AllocationTableRejected;
            return false;
        }

        ulong allocationTableEnd = allocationTableStart + allocationCapacity * allocationRecordSize;
        if (RangesOverlap(scratchStart, scratchEnd, allocationTableStart, allocationTableEnd))
        {
            return false;
        }

        allocationStatus = ValidateAllocationRecords(allocations, out AddressValueStatus _, out ulong _);
        if (allocationStatus != OwnedBootStackSwitchStatus.Success)
        {
            failureStatus = M01LoadedImageStackStatus.AllocationTableRejected;
            return false;
        }

        BootMemoryRegionRecord code = allocations[CodeAllocationRecordIndex];
        BootMemoryRegionRecord data = allocations[DataAllocationRecordIndex];
        ulong codeEnd = code.VirtualStart + code.ByteLength;
        ulong dataEnd = data.VirtualStart + data.ByteLength;
        if (RangesOverlap(scratchStart, scratchEnd, code.VirtualStart, codeEnd) || RangesOverlap(scratchStart, scratchEnd, data.VirtualStart, dataEnd))
        {
            return false;
        }

        if (!ValidateLedgerMetadata(ledger, ledgerCount, ledgerCapacity))
        {
            failureStatus = M01LoadedImageStackStatus.MapLedgerEvidenceRejected;
            return false;
        }

        ulong ledgerRecordSize = (ulong)sizeof(OS.Kernel.Memory.BootReservedRange);
        ulong ledgerStart = (ulong)ledger;
        if (ledgerCapacity > OS.Kernel.Memory.BootReservationTableValidator.MaximumReservationCount || ledgerCapacity > MaximumUnsignedLong / ledgerRecordSize || ledgerStart > MaximumUnsignedLong - ledgerCapacity * ledgerRecordSize)
        {
            failureStatus = M01LoadedImageStackStatus.MapLedgerEvidenceRejected;
            return false;
        }

        ulong ledgerEnd = ledgerStart + ledgerCapacity * ledgerRecordSize;
        if (RangesOverlap(scratchStart, scratchEnd, ledgerStart, ledgerEnd))
        {
            return false;
        }

        if (!ValidateScratchAgainstOptionalRange(scratchStart, scratchEnd, (ulong)codePage, codePageCapacity) || !ValidateScratchAgainstOptionalRange(scratchStart, scratchEnd, (ulong)staticState, staticStateCapacity))
        {
            return false;
        }

        FirmwareMemoryMapRecord ownedMap = mapLedgerEvidence.RefreshedMap;
        if (!ValidateScratchAgainstOptionalRange(scratchStart, scratchEnd, ownedMap.BufferAddress, ownedMap.BufferSize) || !ValidateScratchAgainstOptionalRange(scratchStart, scratchEnd, ownedMap.SourceBufferAddress, ownedMap.SourceBufferSize))
        {
            return false;
        }

        if (continuationAddress.TryGetRawValueForUefiBoundary(out ulong continuation) == AddressValueStatus.Success && continuation >= scratchStart && continuation < scratchEnd)
        {
            return false;
        }

        failureStatus = M01LoadedImageStackStatus.Success;
        return true;
    }

    private static bool ValidateScratchAgainstOptionalRange(ulong scratchStart, ulong scratchEnd, ulong rangeStart, ulong rangeLength)
    {
        if (rangeLength == 0)
        {
            return true;
        }

        return rangeStart <= MaximumUnsignedLong - rangeLength && !RangesOverlap(scratchStart, scratchEnd, rangeStart, rangeStart + rangeLength);
    }

    private static bool ValidateStaticAndCodeStorage(M01LoadedImageVirtualMappingEvidence loadedImageMapping, M01StageOneAddressMappingEvidence addressMapping, BootMemoryRegionRecord* allocations, FirmwareMemoryMapRecord ownedMap, byte* codePage, ulong codePageCapacity, ulong materializedPageByteLength, VirtualAddress continuationAddress, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity, OS.Kernel.Memory.BootReservedRange* ledger, ulong ledgerCount, M01LoadedImageStackEvidence* evidenceScratch, ulong evidenceScratchCapacity, bool requireEvidenceScratchInLoadedImage)
    {
        BootMemoryRegionRecord code = allocations[CodeAllocationRecordIndex];
        BootMemoryRegionRecord data = allocations[DataAllocationRecordIndex];
        ulong codeAddress = (ulong)codePage;
        ulong codeLength = code.ByteLength;
        ulong dataAddress = data.VirtualStart;
        ulong dataLength = data.ByteLength;
        ulong stateAddress = (ulong)staticState;
        ulong stateLength = M01LoadedImageStackStaticState.RequiredStorageBytes;
        ulong allocationTableAddress = (ulong)allocations;
        ulong allocationTableLength = RequiredAllocationRecordCount * (ulong)sizeof(BootMemoryRegionRecord);
        ulong ledgerAddress = (ulong)ledger;
        ulong ledgerLength = ledgerCount * (ulong)sizeof(OS.Kernel.Memory.BootReservedRange);
        ulong evidenceScratchAddress = (ulong)evidenceScratch;
        ulong evidenceScratchLength = (ulong)sizeof(M01LoadedImageStackEvidence);
        if (!addressMapping.IsValidFor(code, data, ownedMap) || addressMapping.OwnedMapVirtualStart.TryGetRawValueForUefiBoundary(out ulong mapStart) != AddressValueStatus.Success || loadedImageMapping.VirtualImageBase.TryGetRawValueForUefiBoundary(out ulong imageBase) != AddressValueStatus.Success || loadedImageMapping.VirtualImageEndExclusive.TryGetRawValueForUefiBoundary(out ulong imageEnd) != AddressValueStatus.Success || continuationAddress.TryGetRawValueForUefiBoundary(out ulong continuation) != AddressValueStatus.Success || codePage == null || codeAddress != code.VirtualStart || codePageCapacity < codeLength || materializedPageByteLength != codeLength || codeAddress > MaximumUnsignedLong - codeLength || dataAddress > MaximumUnsignedLong - dataLength || mapStart > MaximumUnsignedLong - addressMapping.OwnedMapByteLength || allocationTableAddress > MaximumUnsignedLong - allocationTableLength || ledgerAddress > MaximumUnsignedLong - ledgerLength || staticState == null || (stateAddress & (M01LoadedImageStackStaticState.RequiredStorageAlignment - 1)) != 0 || staticStateCapacity < stateLength || stateAddress > MaximumUnsignedLong - stateLength || evidenceScratch == null || evidenceScratchCapacity != evidenceScratchLength || (evidenceScratchAddress & (RequiredEvidenceScratchAlignment - 1)) != 0 || evidenceScratchAddress > MaximumUnsignedLong - evidenceScratchLength || continuation == MaximumUnsignedLong)
        {
            return false;
        }

        ulong codeEnd = codeAddress + codeLength;
        ulong dataEnd = dataAddress + dataLength;
        ulong mapEnd = mapStart + addressMapping.OwnedMapByteLength;
        ulong stateEnd = stateAddress + stateLength;
        ulong allocationTableEnd = allocationTableAddress + allocationTableLength;
        ulong ledgerEnd = ledgerAddress + ledgerLength;
        ulong evidenceScratchEnd = evidenceScratchAddress + evidenceScratchLength;
        if (RangesOverlap(codeAddress, codeEnd, dataAddress, dataEnd) || RangesOverlap(codeAddress, codeEnd, imageBase, imageEnd) || RangesOverlap(codeAddress, codeEnd, mapStart, mapEnd) || RangesOverlap(codeAddress, codeEnd, stateAddress, stateEnd) || RangesOverlap(codeAddress, codeEnd, allocationTableAddress, allocationTableEnd) || RangesOverlap(codeAddress, codeEnd, ledgerAddress, ledgerEnd) || RangesOverlap(dataAddress, dataEnd, imageBase, imageEnd) || RangesOverlap(dataAddress, dataEnd, mapStart, mapEnd) || RangesOverlap(dataAddress, dataEnd, stateAddress, stateEnd) || RangesOverlap(dataAddress, dataEnd, allocationTableAddress, allocationTableEnd) || RangesOverlap(dataAddress, dataEnd, ledgerAddress, ledgerEnd) || RangesOverlap(mapStart, mapEnd, stateAddress, stateEnd) || RangesOverlap(mapStart, mapEnd, allocationTableAddress, allocationTableEnd) || RangesOverlap(mapStart, mapEnd, ledgerAddress, ledgerEnd) || RangesOverlap(allocationTableAddress, allocationTableEnd, ledgerAddress, ledgerEnd) || RangesOverlap(stateAddress, stateEnd, allocationTableAddress, allocationTableEnd) || RangesOverlap(stateAddress, stateEnd, ledgerAddress, ledgerEnd) || RangesOverlap(evidenceScratchAddress, evidenceScratchEnd, codeAddress, codeEnd) || RangesOverlap(evidenceScratchAddress, evidenceScratchEnd, dataAddress, dataEnd) || RangesOverlap(evidenceScratchAddress, evidenceScratchEnd, mapStart, mapEnd) || RangesOverlap(evidenceScratchAddress, evidenceScratchEnd, stateAddress, stateEnd) || RangesOverlap(evidenceScratchAddress, evidenceScratchEnd, allocationTableAddress, allocationTableEnd) || RangesOverlap(evidenceScratchAddress, evidenceScratchEnd, ledgerAddress, ledgerEnd) || (continuation >= codeAddress && continuation < codeEnd) || (continuation >= dataAddress && continuation < dataEnd) || (continuation >= mapStart && continuation < mapEnd) || (continuation >= stateAddress && continuation < stateEnd) || (continuation >= allocationTableAddress && continuation < allocationTableEnd) || (continuation >= ledgerAddress && continuation < ledgerEnd) || (continuation >= evidenceScratchAddress && continuation < evidenceScratchEnd) || (requireEvidenceScratchInLoadedImage && (evidenceScratchAddress < imageBase || evidenceScratchEnd > imageEnd)) || !IsZeroed((byte*)staticState, stateLength))
        {
            return false;
        }

        ulong handoffStart = (ulong)&staticState->HandoffStorage;
        ulong handoffEnd = handoffStart + OwnedBootStackSwitchHandoff.StructureSize;
        ulong switchStateStart = (ulong)&staticState->SwitchStateStorage;
        ulong switchStateEnd = switchStateStart + OwnedBootStackSwitchStateRecord.StructureSize;
        return handoffStart >= imageBase && stateAddress >= imageBase && stateEnd <= imageEnd && handoffEnd <= imageEnd && switchStateStart >= imageBase && switchStateEnd <= imageEnd && continuation >= imageBase && continuation < imageEnd;
    }

#if !NOSTDLIB_EFI
    private static bool StaticStateMatches(M01LoadedImageStackIntegrationResult result, M01LoadedImageStackStaticState* staticState, ulong staticStateCapacity, M01LoadedImageStackCheckpoint checkpoint)
    {
        if (staticState == null || (ulong)staticState != result.Evidence.StaticStateAddress || staticStateCapacity != result.Evidence.StaticStateCapacity || staticStateCapacity < M01LoadedImageStackStaticState.RequiredStorageBytes || !staticState->IsInitialized || staticState->CoordinatorCheckpoint != checkpoint || !HandoffsEqual(staticState->HandoffStorage, result.Evidence.SwitchPlanResult.Plan.Handoff) || !staticState->SwitchStateStorage.Plan.Equals(result.Evidence.SwitchPlanResult.Plan))
        {
            return false;
        }

        return true;
    }

#endif
    private static bool HandoffsEqual(OwnedBootStackSwitchHandoff left, OwnedBootStackSwitchHandoff right)
    {
        return left.Size == right.Size && left.Version == right.Version && left.StorageStart.Equals(right.StorageStart) && left.StorageEndExclusive.Equals(right.StorageEndExclusive) && left.StorageByteLength == right.StorageByteLength && left.StubEntry.Equals(right.StubEntry) && left.UsableStackStart.Equals(right.UsableStackStart) && left.UsableStackEndExclusive.Equals(right.UsableStackEndExclusive) && left.PreTransferStackPointer.Equals(right.PreTransferStackPointer) && left.ExpectedContinuationStackPointer.Equals(right.ExpectedContinuationStackPointer) && left.Continuation.Equals(right.Continuation) && left.Provenance == right.Provenance && left.ContinuationContract == right.ContinuationContract;
    }

    private static bool StaticLayoutTopologyEquals(OwnedBootStackLayout left, OwnedBootStackLayout right)
    {
        return left.BackingStart.Equals(right.BackingStart) && left.BackingEndExclusive.Equals(right.BackingEndExclusive) && left.BackingPageCount == right.BackingPageCount && left.BackingByteLength == right.BackingByteLength && left.GuardReservation.Equals(right.GuardReservation) && left.StackReservation.Equals(right.StackReservation) && left.PhysicalStackTop.Equals(right.PhysicalStackTop);
    }

    private static bool IsZeroed(byte* storage, ulong length)
    {
        for (ulong index = 0; index < length; index++)
        {
            if (storage[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool RangesOverlap(ulong firstStart, ulong firstEnd, ulong secondStart, ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }
}
