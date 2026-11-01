namespace OS.Boot;
/// <summary>
/// Composes the reviewed M01-002, M01-006, M01-007, M01-008, and M01-009
/// contracts over caller-supplied metadata. It never calls firmware, touches
/// the reported physical page, or executes guest code.
/// </summary>

public static unsafe class M01LiveAllocatorIntegration
{
    public const bool CallsFirmwarePageAllocator = false;
    public const bool InvokesFirmware = false;
    public const bool TouchesAllocatedPhysicalPage = false;
    public const bool PerformsPhysicalMemoryAccess = false;
    public const bool MutatesCallerOwnedAllocatorMetadata = true;
    public const bool IsGuestEvidence = false;
    public const bool EstablishesPostFirmwareAllocator = false;
    public const bool TransfersConventionalMemoryOwnership = false;
    public const bool ConfiguresPaging = false;
    public const bool EnforcesNxOrWriteXorExecute = false;
    public const ulong RequiredAllocationRecordCount = 1;
    public const ulong MaximumAllocationRecordCapacity = 16;
    public const ulong MinimumArenaPageCount = 2;
    public const ulong MaximumArenaPageCount = 4096;
    public const ulong RequiredPageSize = 4096;
    public const ulong RequiredPageCount = 1;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong NoIndex = ~0UL;
    private const uint EfiLoaderDataMemoryType = 2;
    private const ulong MaximumUnsignedLong = ~0UL;
    public static M01LiveAllocatorStatus AuditArenaSource(OS.Kernel.Memory.BootReservedRange arena, OS.Kernel.Memory.BootReservationSourceRange source, OS.Kernel.Memory.BootReservationSourceRange freeRange, OS.Kernel.Memory.BootReservationSubtractionResult subtraction, OS.Kernel.Memory.BootPageFreeListNode initialNode)
    {
        if (!arena.IsValid || arena.Kind != OS.Kernel.Memory.BootReservationKind.AllocatorArena || !source.IsValid || !SourceRangeMatchesReservation(source, arena))
        {
            return M01LiveAllocatorStatus.SourceRangeRejected;
        }

        if (!subtraction.Succeeded)
        {
            return M01LiveAllocatorStatus.ReservationSubtractionRejected;
        }

        return IsExactArenaIdentitySubtraction(subtraction, source, freeRange) && NodeMatchesRange(initialNode, freeRange) ? M01LiveAllocatorStatus.Success : M01LiveAllocatorStatus.FreeRangeRejected;
    }

    public static M01LiveAllocatorStatus AuditMaterialization(OS.Kernel.Memory.BootReservedRange arena, OS.Kernel.Memory.BootPageFreeListNode initialNode, OS.Kernel.Memory.BootPageAllocatorStateMaterializationResult materialization)
    {
        if (!materialization.Succeeded)
        {
            return M01LiveAllocatorStatus.StateMaterializationRejected;
        }

        OS.Kernel.Memory.BootPageAllocatorState state = materialization.State;
        return NodeMatchesReservation(initialNode, arena) && materialization.SourceNodeCount == 1 && materialization.SourceRangeCount == 1 && materialization.HeadNodeIndex == 0 && materialization.PageSize == RequiredPageSize && materialization.ExpectedFreePages == arena.PageCount && materialization.ExpectedFreeBytes == arena.ByteLength && materialization.ObservedFreePages == arena.PageCount && materialization.ObservedFreeBytes == arena.ByteLength && StateMatchesInitialArena(state, arena) ? M01LiveAllocatorStatus.Success : M01LiveAllocatorStatus.StateMaterializationRejected;
    }

    public static M01LiveAllocatorStatus AuditAllocation(OS.Kernel.Memory.BootReservedRange arena, OS.Kernel.Memory.BootPageAllocatorState initialState, OS.Kernel.Memory.BootPageFreeListNode initialNode, OS.Kernel.Memory.BootPageAllocatorOnePageResult allocation, OS.Kernel.Memory.BootPageFreeListNode nodeAfterAllocation)
    {
        if (!allocation.Succeeded)
        {
            return M01LiveAllocatorStatus.AllocationRejected;
        }

        return StatesEqual(allocation.BeforeState, initialState) && StateMatchesInitialArena(initialState, arena) && NodeMatchesReservation(initialNode, arena) && AllocationMetadataIsExact(allocation) && allocation.CanRestore && NodesEqual(allocation.RestoreReceipt.SourceNodeBefore, initialNode) && allocation.RestoreReceipt.PreviousNodeIndexBefore == -1 && NodesEqual(allocation.RestoreReceipt.PreviousNodeBefore, default) && IsAllocationInsideArena(allocation, arena) && StateMatchesAllocatedArena(allocation.AfterState, arena) && NodeMatchesAllocatedSuffix(nodeAfterAllocation, arena) ? M01LiveAllocatorStatus.Success : M01LiveAllocatorStatus.AllocationContainmentRejected;
    }

    public static M01LiveAllocatorStatus AuditFree(OS.Kernel.Memory.BootPageAllocatorState initialState, OS.Kernel.Memory.BootPageFreeListNode initialNode, OS.Kernel.Memory.BootPageAllocatorOnePageResult allocation, OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult free, OS.Kernel.Memory.BootPageFreeListNode nodeAfterFree)
    {
        if (!free.Succeeded)
        {
            return M01LiveAllocatorStatus.FreeRejected;
        }

        return allocation.Succeeded && allocation.CanRestore && AllocationMetadataIsExact(allocation) && AllocationsEqual(free.Allocation, allocation) && NodesEqual(allocation.RestoreReceipt.SourceNodeBefore, initialNode) && free.CoalescingKind == OS.Kernel.Memory.BootPageAllocatorOnePageCoalescingKind.Next && free.NodeCount == 1 && free.NodeCapacity == 1 && free.ReservationCount == 0 && free.ReservationCapacity == 0 && free.RestoredNodeIndex == allocation.NodeIndex && free.PreviousNodeIndex == allocation.RestoreReceipt.PreviousNodeIndexBefore && free.RemovedSuffixNodeIndex == -1 && free.VisitedNodeCount == 1 && free.InvalidReservationIndex == NoIndex && free.ConflictingIndex == NoIndex && StatesEqual(free.BeforeState, allocation.AfterState) && StatesEqual(free.AfterState, initialState) && NodesEqual(nodeAfterFree, initialNode) && free.BeforeFreePageCount == allocation.AfterFreePageCount && free.AfterFreePageCount == initialState.FreePageCount && free.BeforeFreeByteCount == allocation.AfterFreeByteCount && free.AfterFreeByteCount == initialState.FreeByteCount && free.BeforeAllocatedPageCount == allocation.AfterAllocatedPageCount && free.AfterAllocatedPageCount == initialState.AllocatedPageCount && free.BeforeAllocatedByteCount == allocation.AfterAllocatedByteCount && free.AfterAllocatedByteCount == initialState.AllocatedByteCount ? M01LiveAllocatorStatus.Success : M01LiveAllocatorStatus.FreeAccountingRejected;
    }

    public static M01LiveAllocatorStatus AuditDoubleFree(OS.Kernel.Memory.BootReservedRange arena, OS.Kernel.Memory.BootPageAllocatorState restoredState, OS.Kernel.Memory.BootPageFreeListNode restoredNode, OS.Kernel.Memory.BootPageAllocatorOnePageResult allocation, OS.Kernel.Memory.BootPageAllocatorOnePageFreeResult doubleFree, OS.Kernel.Memory.BootPageFreeListNode nodeAfterDoubleFree)
    {
        return StateMatchesInitialArena(restoredState, arena) && NodeMatchesReservation(restoredNode, arena) && allocation.Succeeded && allocation.CanRestore && AllocationMetadataIsExact(allocation) && StatesEqual(allocation.BeforeState, restoredState) && NodesEqual(allocation.RestoreReceipt.SourceNodeBefore, restoredNode) && IsAllocationInsideArena(allocation, arena) && doubleFree.IsInitialized && !doubleFree.Succeeded && doubleFree.Status == OS.Kernel.Memory.BootPageAllocatorMutationStatus.FreeRangeNotAllocated && doubleFree.ReservationStatus == OS.Kernel.Memory.BootReservedRangeStatus.Success && doubleFree.AddressStatus == AddressValueStatus.Success && AllocationsEqual(doubleFree.Allocation, allocation) && doubleFree.CoalescingKind == OS.Kernel.Memory.BootPageAllocatorOnePageCoalescingKind.Uninitialized && doubleFree.NodeCount == 1 && doubleFree.NodeCapacity == 1 && doubleFree.ReservationCount == 0 && doubleFree.ReservationCapacity == 0 && doubleFree.RestoredNodeIndex == allocation.NodeIndex && doubleFree.PreviousNodeIndex == allocation.RestoreReceipt.PreviousNodeIndexBefore && doubleFree.RemovedSuffixNodeIndex == allocation.InsertedNodeIndex && doubleFree.VisitedNodeCount == 1 && doubleFree.InvalidReservationIndex == NoIndex && doubleFree.ConflictingIndex == NoIndex && StatesEqual(doubleFree.BeforeState, restoredState) && StatesEqual(doubleFree.AfterState, restoredState) && NodesEqual(nodeAfterDoubleFree, restoredNode) ? M01LiveAllocatorStatus.Success : M01LiveAllocatorStatus.DoubleFreeNotRejected;
    }

    public static M01LiveAllocatorIntegrationResult Execute(M01LoadedImageStackStaticState* stageOneState, ulong stageOneStateCapacity, BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, M01LiveAllocatorAllocationEvidence allocationEvidence, M01LiveAllocatorMapLedgerEvidence mapLedgerEvidence, OS.Kernel.Memory.BootReservedRange* reservationLedger, ulong reservationCount, ulong reservationCapacity, M01LiveAllocatorStaticState* staticState, ulong staticStateCapacity)
    {
        M01LiveAllocatorEvidence evidence = default;
        evidence.StageOneStateAddressValue = (ulong)stageOneState;
        evidence.StageOneStateCapacityValue = stageOneStateCapacity;
        evidence.StaticStateAddressValue = (ulong)staticState;
        evidence.StaticStateCapacityValue = staticStateCapacity;
        evidence.AllocationEvidenceValue = allocationEvidence;
        evidence.MapLedgerEvidenceValue = mapLedgerEvidence;
        evidence.ArenaReservationIndexValue = NoIndex;
        M01LiveAllocatorCheckpoint checkpoint = M01LiveAllocatorCheckpoint.Uninitialized;
        if (!ValidateStageOneState(stageOneState, stageOneStateCapacity, out ulong stageOneStart, out ulong stageOneEnd))
        {
            return Fail(M01LiveAllocatorStatus.StageOneRejected, checkpoint, evidence);
        }

        checkpoint = M01LiveAllocatorCheckpoint.StageOneValidated;
        if (!ValidateAllocationStorage(allocationRecords, allocationRecordCount, allocationRecordCapacity, out ulong allocationTableStart, out ulong allocationTableEnd))
        {
            return Fail(M01LiveAllocatorStatus.AllocationTableRejected, checkpoint, evidence);
        }

        if (!allocationEvidence.IsValidFor(allocationRecords, allocationRecordCount, allocationRecordCapacity))
        {
            return Fail(M01LiveAllocatorStatus.AllocationEvidenceRejected, checkpoint, evidence);
        }

        if (!ValidateReservationStorage(reservationLedger, reservationCount, reservationCapacity, out ulong reservationTableStart, out ulong reservationTableEnd))
        {
            return Fail(M01LiveAllocatorStatus.ReservationLedgerRejected, checkpoint, evidence);
        }

        if (!mapLedgerEvidence.IsValidFor(allocationEvidence, reservationLedger, reservationCount, reservationCapacity))
        {
            return Fail(M01LiveAllocatorStatus.MapLedgerEvidenceRejected, checkpoint, evidence);
        }

        if (!ValidateStaticStateStorage(staticState, staticStateCapacity, out ulong staticStateStart, out ulong staticStateEnd))
        {
            return Fail(M01LiveAllocatorStatus.CoordinatorStateRejected, checkpoint, evidence);
        }

        if (!AreMetadataRangesDisjoint(stageOneStart, stageOneEnd, allocationTableStart, allocationTableEnd) || !AreMetadataRangesDisjoint(stageOneStart, stageOneEnd, reservationTableStart, reservationTableEnd) || !AreMetadataRangesDisjoint(stageOneStart, stageOneEnd, staticStateStart, staticStateEnd) || !AreMetadataRangesDisjoint(allocationTableStart, allocationTableEnd, reservationTableStart, reservationTableEnd) || !AreMetadataRangesDisjoint(allocationTableStart, allocationTableEnd, staticStateStart, staticStateEnd) || !AreMetadataRangesDisjoint(reservationTableStart, reservationTableEnd, staticStateStart, staticStateEnd) || !MetadataIsDisjointFromMap(stageOneStart, stageOneEnd, mapLedgerEvidence.RefreshedMap) || !MetadataIsDisjointFromMap(allocationTableStart, allocationTableEnd, mapLedgerEvidence.RefreshedMap) || !MetadataIsDisjointFromMap(reservationTableStart, reservationTableEnd, mapLedgerEvidence.RefreshedMap) || !MetadataIsDisjointFromMap(staticStateStart, staticStateEnd, mapLedgerEvidence.RefreshedMap))
        {
            return Fail(M01LiveAllocatorStatus.CoordinatorStateRejected, checkpoint, evidence);
        }

        if (staticState->IsCompleted)
        {
            return Fail(M01LiveAllocatorStatus.DuplicatePhase, checkpoint, evidence);
        }

        if (!IsDefaultState(*staticState))
        {
            return Fail(M01LiveAllocatorStatus.CoordinatorStateRejected, checkpoint, evidence);
        }

        BootMemoryRegionRecord arena = allocationRecords[0];
        evidence.ArenaAllocationValue = arena;
        evidence.ArenaStatusValue = ValidateArena(arena);
        if (evidence.ArenaStatusValue != M01LiveAllocatorArenaStatus.Success)
        {
            return Fail(M01LiveAllocatorStatus.ArenaAllocationRejected, checkpoint, evidence);
        }

        ulong arenaStart = arena.PhysicalStart;
        ulong arenaEnd = arena.PhysicalEndExclusive;
        if (!AreMetadataRangesDisjoint(stageOneStart, stageOneEnd, arenaStart, arenaEnd) || !AreMetadataRangesDisjoint(allocationTableStart, allocationTableEnd, arenaStart, arenaEnd) || !AreMetadataRangesDisjoint(reservationTableStart, reservationTableEnd, arenaStart, arenaEnd) || !AreMetadataRangesDisjoint(staticStateStart, staticStateEnd, arenaStart, arenaEnd) || !MetadataIsDisjointFromMap(arenaStart, arenaEnd, mapLedgerEvidence.RefreshedMap))
        {
            return Fail(M01LiveAllocatorStatus.CoordinatorStateRejected, checkpoint, evidence);
        }

        checkpoint = M01LiveAllocatorCheckpoint.ArenaAllocationValidated;
        checkpoint = M01LiveAllocatorCheckpoint.MapLedgerValidated;
        evidence.ReservationValidationValue = OS.Kernel.Memory.BootReservationTableValidator.Validate(reservationLedger, reservationCount, reservationCapacity);
        if (!evidence.ReservationValidationValue.Succeeded)
        {
            return Fail(M01LiveAllocatorStatus.ReservationLedgerRejected, checkpoint, evidence);
        }

        if (!TryFindExactRequiredReservations(stageOneState, reservationLedger, reservationCount, arena, out ulong arenaReservationIndex, out OS.Kernel.Memory.BootReservedRange arenaReservation))
        {
            return Fail(M01LiveAllocatorStatus.ArenaReservationRejected, checkpoint, evidence);
        }

        evidence.ArenaReservationIndexValue = arenaReservationIndex;
        evidence.ArenaReservationValue = arenaReservation;
        checkpoint = M01LiveAllocatorCheckpoint.ReservationsValidated;
        M01LiveAllocatorStaticState candidate = default;
        evidence.SourceRangeStatusValue = OS.Kernel.Memory.BootReservationSourceRange.TryCreate(sourceRangeIndex: 0, arenaReservation.PhysicalStart, arenaReservation.PageCount, out candidate.ArenaSourceRangeValue);
        if (evidence.SourceRangeStatusValue != OS.Kernel.Memory.BootReservationSourceRangeStatus.Success)
        {
            return Fail(M01LiveAllocatorStatus.SourceRangeRejected, checkpoint, evidence);
        }

        evidence.SubtractionValue = OS.Kernel.Memory.BootReservationSubtractor.Subtract(&candidate.ArenaSourceRangeValue, sourceRangeCount: 1, sourceRangeCapacity: 1, reservations: null, reservationCount: 0, reservationCapacity: 0, RequiredPageSize, &candidate.FreeRangeValue, destinationRangeCapacity: 1);
        candidate.FreeNodeValue = new OS.Kernel.Memory.BootPageFreeListNode(sourceFreeSegmentIndex: 0, sourceUsableRangeIndex: 0, candidate.FreeRangeValue.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong freeStart) == AddressValueStatus.Success ? freeStart : 0, candidate.FreeRangeValue.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong freeEnd) == AddressValueStatus.Success ? freeEnd : 0, candidate.FreeRangeValue.PageCount, candidate.FreeRangeValue.ByteLength, nextNodeIndex: -1);
        M01LiveAllocatorStatus phaseStatus = AuditArenaSource(arenaReservation, candidate.ArenaSourceRangeValue, candidate.FreeRangeValue, evidence.SubtractionValue, candidate.FreeNodeValue);
        if (phaseStatus != M01LiveAllocatorStatus.Success)
        {
            return Fail(phaseStatus, checkpoint, evidence);
        }

        evidence.StateMaterializationValue = OS.Kernel.Memory.BootPageAllocatorStateMaterializer.Materialize(&candidate.FreeNodeValue, sourceNodeCount: 1, sourceNodeCapacity: 1, sourceRangeCount: 1, headNodeIndex: 0, RequiredPageSize, candidate.FreeRangeValue.PageCount, candidate.FreeRangeValue.ByteLength, &candidate.AllocatorStateValue, destinationStateCapacity: 1);
        phaseStatus = AuditMaterialization(arenaReservation, candidate.FreeNodeValue, evidence.StateMaterializationValue);
        if (phaseStatus != M01LiveAllocatorStatus.Success)
        {
            return Fail(phaseStatus, checkpoint, evidence);
        }

        OS.Kernel.Memory.BootPageAllocatorState initialState = candidate.AllocatorStateValue;
        OS.Kernel.Memory.BootPageFreeListNode initialNode = candidate.FreeNodeValue;
        checkpoint = M01LiveAllocatorCheckpoint.FreeListMaterialized;
        evidence.AllocationValue = OS.Kernel.Memory.BootPageAllocatorMutator.AllocateOnePage(candidate.AllocatorStateValue, &candidate.FreeNodeValue, nodeCount: 1, nodeCapacity: 1, reservations: null, reservationCount: 0, reservationCapacity: 0, RequiredPageCount, RequiredPageSize);
        phaseStatus = AuditAllocation(arenaReservation, initialState, initialNode, evidence.AllocationValue, candidate.FreeNodeValue);
        if (phaseStatus == M01LiveAllocatorStatus.AllocationRejected)
        {
            return Fail(phaseStatus, checkpoint, evidence);
        }

        candidate.AllocatorStateValue = evidence.AllocationValue.AfterState;
        checkpoint = M01LiveAllocatorCheckpoint.PageAllocated;
        if (phaseStatus != M01LiveAllocatorStatus.Success)
        {
            return Fail(phaseStatus, checkpoint, evidence);
        }

        checkpoint = M01LiveAllocatorCheckpoint.PageContained;
        evidence.FreeValue = OS.Kernel.Memory.BootPageAllocatorMutator.FreeOnePage(candidate.AllocatorStateValue, &candidate.FreeNodeValue, nodeCount: 1, nodeCapacity: 1, reservations: null, reservationCount: 0, reservationCapacity: 0, evidence.AllocationValue);
        phaseStatus = AuditFree(initialState, initialNode, evidence.AllocationValue, evidence.FreeValue, candidate.FreeNodeValue);
        if (phaseStatus != M01LiveAllocatorStatus.Success)
        {
            return Fail(phaseStatus, checkpoint, evidence);
        }

        candidate.AllocatorStateValue = evidence.FreeValue.AfterState;
        checkpoint = M01LiveAllocatorCheckpoint.PageFreed;
        OS.Kernel.Memory.BootPageFreeListNode nodeBeforeDoubleFree = candidate.FreeNodeValue;
        evidence.DoubleFreeValue = OS.Kernel.Memory.BootPageAllocatorMutator.FreeOnePage(candidate.AllocatorStateValue, &candidate.FreeNodeValue, nodeCount: 1, nodeCapacity: 1, reservations: null, reservationCount: 0, reservationCapacity: 0, evidence.AllocationValue);
        phaseStatus = AuditDoubleFree(arenaReservation, candidate.AllocatorStateValue, nodeBeforeDoubleFree, evidence.AllocationValue, evidence.DoubleFreeValue, candidate.FreeNodeValue);
        if (phaseStatus != M01LiveAllocatorStatus.Success)
        {
            return Fail(phaseStatus, checkpoint, evidence);
        }

        checkpoint = M01LiveAllocatorCheckpoint.DoubleFreeRejected;
        candidate.HeaderSize = (uint)M01LiveAllocatorStaticState.RequiredStorageBytes;
        candidate.HeaderVersion = M01LiveAllocatorStaticState.CurrentVersion;
        candidate.CoordinatorStatus = M01LiveAllocatorStatus.Success;
        candidate.CoordinatorCheckpoint = checkpoint;
        candidate.Reserved0 = 0;
        candidate.Reserved1 = 0;
        candidate.Reserved2 = 0;
        candidate.Reserved3 = 0;
        candidate.Reserved4 = 0;
        candidate.Reserved5 = 0;
        candidate.ReservedWord = 0;
        candidate.DoubleFreeStatusValue = evidence.DoubleFreeValue.Status;
        candidate.StageOneStateAddressValue = (ulong)stageOneState;
        candidate.StageOneStateCapacityValue = stageOneStateCapacity;
        candidate.RefreshedMapKeyValue = mapLedgerEvidence.RefreshedMap.MapKey;
        candidate.RefreshedMapGenerationValue = mapLedgerEvidence.RefreshedMapGeneration;
        candidate.ArenaReservationValue = arenaReservation;
        candidate.AllocatedPageStartValue = evidence.AllocationValue.PhysicalStart;
        candidate.AllocatedPageEndExclusiveValue = evidence.AllocationValue.PhysicalEndExclusive;
        candidate.InitialFreePagesValue = initialState.FreePageCount;
        candidate.InitialFreeBytesValue = initialState.FreeByteCount;
        if (!IsCompletedState(candidate))
        {
            return Fail(M01LiveAllocatorStatus.IllegalCoordinatorState, checkpoint, evidence);
        }

        M01LiveAllocatorIntegrationResult result = M01LiveAllocatorIntegrationResult.Success(evidence, candidate);
        if (!result.Succeeded)
        {
            return Fail(M01LiveAllocatorStatus.IllegalCoordinatorState, checkpoint, evidence);
        }

        *staticState = candidate;
        return result;
    }

    internal static bool IsCompletedState(M01LiveAllocatorStaticState state)
    {
        if (state.HeaderSize != M01LiveAllocatorStaticState.RequiredStorageBytes || state.HeaderVersion != M01LiveAllocatorStaticState.CurrentVersion || state.CoordinatorStatus != M01LiveAllocatorStatus.Success || state.CoordinatorCheckpoint != M01LiveAllocatorCheckpoint.DoubleFreeRejected || state.Reserved0 != 0 || state.Reserved1 != 0 || state.Reserved2 != 0 || state.Reserved3 != 0 || state.Reserved4 != 0 || state.Reserved5 != 0 || state.ReservedWord != 0 || state.DoubleFreeStatusValue != OS.Kernel.Memory.BootPageAllocatorMutationStatus.FreeRangeNotAllocated || state.StageOneStateAddressValue == 0 || (state.StageOneStateAddressValue & (M01LoadedImageStackStaticState.RequiredStorageAlignment - 1)) != 0 || state.StageOneStateCapacityValue < M01LoadedImageStackStaticState.RequiredStorageBytes || state.StageOneStateAddressValue > MaximumUnsignedLong - state.StageOneStateCapacityValue || state.RefreshedMapGenerationValue == 0 || !state.ArenaReservationValue.IsValid || state.ArenaReservationValue.Kind != OS.Kernel.Memory.BootReservationKind.AllocatorArena || state.ArenaReservationValue.PageCount < MinimumArenaPageCount || state.ArenaReservationValue.PageCount > MaximumArenaPageCount || !state.ArenaSourceRangeValue.IsValid || !state.FreeRangeValue.IsValid || !state.ArenaSourceRangeValue.Equals(state.FreeRangeValue) || !SourceRangeMatchesReservation(state.ArenaSourceRangeValue, state.ArenaReservationValue) || !state.AllocatorStateValue.IsInitialized || state.AllocatorStateValue.PageSize != RequiredPageSize || state.AllocatorStateValue.HeadNodeIndex != 0 || state.AllocatorStateValue.FreeNodeCount != 1 || state.AllocatorStateValue.FreePageCount != state.ArenaReservationValue.PageCount || state.AllocatorStateValue.FreeByteCount != state.ArenaReservationValue.ByteLength || state.AllocatorStateValue.AllocatedPageCount != 0 || state.AllocatorStateValue.AllocatedByteCount != 0 || state.InitialFreePagesValue != state.ArenaReservationValue.PageCount || state.InitialFreeBytesValue != state.ArenaReservationValue.ByteLength || !NodeMatchesRange(state.FreeNodeValue, state.FreeRangeValue))
        {
            return false;
        }

        return IsAllocatedRangeInsideArena(state.AllocatedPageStartValue, state.AllocatedPageEndExclusiveValue, state.ArenaReservationValue);
    }

    private static M01LiveAllocatorIntegrationResult Fail(M01LiveAllocatorStatus status, M01LiveAllocatorCheckpoint checkpoint, M01LiveAllocatorEvidence evidence)
    {
        return M01LiveAllocatorIntegrationResult.Fail(status, checkpoint, evidence);
    }

    private static bool ValidateStageOneState(M01LoadedImageStackStaticState* stageOneState, ulong stageOneStateCapacity, out ulong start, out ulong end)
    {
        start = (ulong)stageOneState;
        end = 0;
        ulong length = M01LoadedImageStackStaticState.RequiredStorageBytes;
        if ((ulong)sizeof(M01LoadedImageStackStaticState) != length || stageOneState == null || (start & (M01LoadedImageStackStaticState.RequiredStorageAlignment - 1)) != 0 || stageOneStateCapacity < length || start > MaximumUnsignedLong - stageOneStateCapacity)
        {
            return false;
        }

        end = start + stageOneStateCapacity;
        OwnedBootStackSwitchPlan plan = stageOneState->SwitchState.Plan;
        if (stageOneState->Handoff.StorageStart.TryGetRawValueForUefiBoundary(out ulong handoffStorageAddress) != AddressValueStatus.Success)
        {
            return false;
        }

        return stageOneState->IsInitialized && stageOneState->Status == M01LoadedImageStackStatus.Success && stageOneState->Checkpoint == M01LoadedImageStackCheckpoint.ContinuationRspContained && stageOneState->SwitchState.State == OwnedBootStackSwitchState.ContinuationObserved && stageOneState->StackLayout.Lifetime == OwnedBootStackLifetime.Active && (ulong)&stageOneState->HandoffStorage - start == M01LoadedImageStackStaticState.HandoffStorageOffset && handoffStorageAddress == (ulong)&stageOneState->HandoffStorage && (ulong)&stageOneState->SwitchStateStorage - start == M01LoadedImageStackStaticState.SwitchStateStorageOffset && stageOneState->SwitchState.StorageAddress == (ulong)&stageOneState->SwitchStateStorage && (ulong)&stageOneState->StackLayoutStorage - start == M01LoadedImageStackStaticState.StackLayoutStorageOffset && plan.IsValid && HandoffsEqual(stageOneState->Handoff, plan.Handoff) && StackLayoutTopologyEquals(stageOneState->StackLayout, plan.StackLayout);
    }

    private static bool ValidateAllocationStorage(BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, out ulong start, out ulong end)
    {
        start = (ulong)allocationRecords;
        end = 0;
        ulong structureSize = (ulong)sizeof(BootMemoryRegionRecord);
        if (allocationRecordCount != RequiredAllocationRecordCount || allocationRecordCapacity < allocationRecordCount || allocationRecordCapacity > MaximumAllocationRecordCapacity || allocationRecordCapacity > MaximumUnsignedLong / structureSize || allocationRecords == null || (start & (RequiredPointerAlignment - 1)) != 0)
        {
            return false;
        }

        ulong byteLength = allocationRecordCapacity * structureSize;
        if (start > MaximumUnsignedLong - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }

    private static bool ValidateReservationStorage(OS.Kernel.Memory.BootReservedRange* reservationLedger, ulong reservationCount, ulong reservationCapacity, out ulong start, out ulong end)
    {
        start = (ulong)reservationLedger;
        end = 0;
        ulong structureSize = (ulong)sizeof(OS.Kernel.Memory.BootReservedRange);
        if (reservationCount == 0 || reservationCount > OS.Kernel.Memory.BootReservationTableValidator.MaximumReservationCount || reservationCapacity < reservationCount || reservationCapacity > OS.Kernel.Memory.BootReservationTableValidator.MaximumReservationCount || reservationCapacity > MaximumUnsignedLong / structureSize || reservationLedger == null || (start & (RequiredPointerAlignment - 1)) != 0)
        {
            return false;
        }

        ulong byteLength = reservationCapacity * structureSize;
        if (start > MaximumUnsignedLong - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }

    private static bool ValidateStaticStateStorage(M01LiveAllocatorStaticState* staticState, ulong staticStateCapacity, out ulong start, out ulong end)
    {
        start = (ulong)staticState;
        end = 0;
        ulong length = M01LiveAllocatorStaticState.RequiredStorageBytes;
        if ((ulong)sizeof(M01LiveAllocatorStaticState) != length || staticState == null || (start & (M01LiveAllocatorStaticState.RequiredStorageAlignment - 1)) != 0 || staticStateCapacity < length || start > MaximumUnsignedLong - staticStateCapacity)
        {
            return false;
        }

        end = start + staticStateCapacity;
        return true;
    }

    private static M01LiveAllocatorArenaStatus ValidateArena(BootMemoryRegionRecord arena)
    {
        if (arena.UefiType != EfiLoaderDataMemoryType)
        {
            return M01LiveAllocatorArenaStatus.InvalidMemoryType;
        }

        if (arena.PhysicalStart == 0)
        {
            return M01LiveAllocatorArenaStatus.InvalidPhysicalStart;
        }

        if ((arena.PhysicalStart & (RequiredPageSize - 1)) != 0)
        {
            return M01LiveAllocatorArenaStatus.MisalignedPhysicalStart;
        }

        if (arena.VirtualStart == 0)
        {
            return M01LiveAllocatorArenaStatus.InvalidVirtualStart;
        }

        if ((arena.VirtualStart & (RequiredPageSize - 1)) != 0)
        {
            return M01LiveAllocatorArenaStatus.MisalignedVirtualStart;
        }

        if (arena.VirtualStart != arena.PhysicalStart)
        {
            return M01LiveAllocatorArenaStatus.NonIdentityVirtualStart;
        }

        if (arena.PageCount < MinimumArenaPageCount)
        {
            return M01LiveAllocatorArenaStatus.TooFewPages;
        }

        if (arena.PageCount > MaximumArenaPageCount)
        {
            return M01LiveAllocatorArenaStatus.TooManyPages;
        }

        if (arena.PageCount > MaximumUnsignedLong / RequiredPageSize)
        {
            return M01LiveAllocatorArenaStatus.PageByteCountOverflow;
        }

        ulong expectedBytes = arena.PageCount * RequiredPageSize;
        if (arena.ByteLength != expectedBytes)
        {
            return M01LiveAllocatorArenaStatus.ByteLengthMismatch;
        }

        if (arena.PhysicalStart > MaximumUnsignedLong - expectedBytes)
        {
            return M01LiveAllocatorArenaStatus.PhysicalEndOverflow;
        }

        if (arena.PhysicalEndExclusive != arena.PhysicalStart + expectedBytes)
        {
            return M01LiveAllocatorArenaStatus.PhysicalEndMismatch;
        }

        return arena.VirtualStart > MaximumUnsignedLong - expectedBytes ? M01LiveAllocatorArenaStatus.VirtualEndOverflow : M01LiveAllocatorArenaStatus.Success;
    }

    private static bool TryFindExactRequiredReservations(M01LoadedImageStackStaticState* stageOneState, OS.Kernel.Memory.BootReservedRange* reservationLedger, ulong reservationCount, BootMemoryRegionRecord arena, out ulong arenaReservationIndex, out OS.Kernel.Memory.BootReservedRange arenaReservation)
    {
        arenaReservationIndex = NoIndex;
        arenaReservation = default;
        ulong arenaCount = 0;
        ulong loadedImageCount = 0;
        ulong switchStubCount = 0;
        ulong guardCount = 0;
        ulong stackCount = 0;
        BootMemoryRegionRecord codeAllocation = stageOneState->SwitchState.Plan.CodeAllocation;
        ulong stageOneStateStart = (ulong)stageOneState;
        ulong stageOneStateEnd = stageOneStateStart + M01LoadedImageStackStaticState.RequiredStorageBytes;
        if (stageOneState->Handoff.Continuation.TryGetRawValueForUefiBoundary(out ulong continuationAddress) != AddressValueStatus.Success)
        {
            return false;
        }

        for (ulong index = 0; index < reservationCount; index++)
        {
            OS.Kernel.Memory.BootReservedRange candidate = reservationLedger[index];
            if (candidate.Kind == OS.Kernel.Memory.BootReservationKind.LoadedImage)
            {
                loadedImageCount++;
                if (loadedImageCount != 1 || !RangeContains(candidate, stageOneStateStart, stageOneStateEnd) || !RangeContainsAddress(candidate, continuationAddress))
                {
                    return false;
                }

                continue;
            }

            if (candidate.Kind == OS.Kernel.Memory.BootReservationKind.BootStackSwitchStub)
            {
                switchStubCount++;
                if (switchStubCount != 1 || !RangeEqualsAllocation(candidate, codeAllocation))
                {
                    return false;
                }

                continue;
            }

            if (candidate.Kind == OS.Kernel.Memory.BootReservationKind.BootStackGuard)
            {
                guardCount++;
                if (guardCount != 1 || !candidate.Equals(stageOneState->StackLayout.GuardReservation))
                {
                    return false;
                }

                continue;
            }

            if (candidate.Kind == OS.Kernel.Memory.BootReservationKind.BootStack)
            {
                stackCount++;
                if (stackCount != 1 || !candidate.Equals(stageOneState->StackLayout.StackReservation))
                {
                    return false;
                }

                continue;
            }

            if (candidate.Kind != OS.Kernel.Memory.BootReservationKind.AllocatorArena)
            {
                continue;
            }

            arenaCount++;
            if (arenaCount != 1 || !RangeEqualsArena(candidate, arena))
            {
                return false;
            }

            arenaReservationIndex = index;
            arenaReservation = candidate;
        }

        return loadedImageCount == 1 && switchStubCount == 1 && guardCount == 1 && stackCount == 1 && arenaCount == 1 && arenaReservationIndex != NoIndex && arenaReservation.IsValid;
    }

    private static bool RangeEqualsAllocation(OS.Kernel.Memory.BootReservedRange reservation, BootMemoryRegionRecord allocation)
    {
        return reservation.IsValid && reservation.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong reservationStart) == AddressValueStatus.Success && reservation.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong reservationEnd) == AddressValueStatus.Success && reservationStart == allocation.PhysicalStart && reservationEnd == allocation.PhysicalEndExclusive && reservation.PageCount == allocation.PageCount && reservation.ByteLength == allocation.ByteLength;
    }

    private static bool RangeContains(OS.Kernel.Memory.BootReservedRange reservation, ulong start, ulong end)
    {
        return reservation.IsValid && start < end && reservation.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong reservationStart) == AddressValueStatus.Success && reservation.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong reservationEnd) == AddressValueStatus.Success && start >= reservationStart && end <= reservationEnd;
    }

    private static bool RangeContainsAddress(OS.Kernel.Memory.BootReservedRange reservation, ulong address)
    {
        return reservation.IsValid && reservation.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong reservationStart) == AddressValueStatus.Success && reservation.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong reservationEnd) == AddressValueStatus.Success && address >= reservationStart && address < reservationEnd;
    }

    private static bool RangeEqualsArena(OS.Kernel.Memory.BootReservedRange reservation, BootMemoryRegionRecord arena)
    {
        return reservation.IsValid && reservation.Kind == OS.Kernel.Memory.BootReservationKind.AllocatorArena && reservation.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong reservationStart) == AddressValueStatus.Success && reservation.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong reservationEnd) == AddressValueStatus.Success && reservationStart == arena.PhysicalStart && reservationEnd == arena.PhysicalEndExclusive && reservation.PageCount == arena.PageCount && reservation.ByteLength == arena.ByteLength;
    }

    private static bool IsExactArenaIdentitySubtraction(OS.Kernel.Memory.BootReservationSubtractionResult result, OS.Kernel.Memory.BootReservationSourceRange source, OS.Kernel.Memory.BootReservationSourceRange destination)
    {
        return result.Succeeded && result.SourceRangeCount == 1 && result.ReservationCount == 0 && result.RequiredOutputRangeCount == 1 && result.WrittenOutputRangeCount == 1 && result.TotalSourcePages == source.PageCount && result.TotalSourceBytes == source.ByteLength && result.RemovedPages == 0 && result.RemovedBytes == 0 && result.RemainingPages == source.PageCount && result.RemainingBytes == source.ByteLength && destination.Equals(source);
    }

    private static bool IsAllocationInsideArena(OS.Kernel.Memory.BootPageAllocatorOnePageResult allocation, OS.Kernel.Memory.BootReservedRange arena)
    {
        return allocation.Succeeded && allocation.PageCount == RequiredPageCount && allocation.ByteLength == RequiredPageSize && allocation.BeforeFreePageCount == arena.PageCount && allocation.AfterFreePageCount == arena.PageCount - RequiredPageCount && allocation.BeforeFreeByteCount == arena.ByteLength && allocation.AfterFreeByteCount == arena.ByteLength - RequiredPageSize && allocation.BeforeAllocatedPageCount == 0 && allocation.AfterAllocatedPageCount == RequiredPageCount && allocation.BeforeAllocatedByteCount == 0 && allocation.AfterAllocatedByteCount == RequiredPageSize && IsAllocatedRangeInsideArena(allocation.PhysicalStart, allocation.PhysicalEndExclusive, arena);
    }

    private static bool AllocationMetadataIsExact(OS.Kernel.Memory.BootPageAllocatorOnePageResult allocation)
    {
        return allocation.NodeCount == 1 && allocation.NodeCapacity == 1 && allocation.ReservationCount == 0 && allocation.ReservationCapacity == 0 && allocation.NodeIndex == 0 && allocation.InsertedNodeIndex == -1 && allocation.VisitedNodeCount == 1 && allocation.InvalidReservationIndex == NoIndex && allocation.ConflictingIndex == NoIndex;
    }

    private static bool StateMatchesInitialArena(OS.Kernel.Memory.BootPageAllocatorState state, OS.Kernel.Memory.BootReservedRange arena)
    {
        return arena.IsValid && arena.Kind == OS.Kernel.Memory.BootReservationKind.AllocatorArena && state.IsInitialized && state.PageSize == RequiredPageSize && state.HeadNodeIndex == 0 && state.FreeNodeCount == 1 && state.FreePageCount == arena.PageCount && state.FreeByteCount == arena.ByteLength && state.AllocatedPageCount == 0 && state.AllocatedByteCount == 0;
    }

    private static bool StateMatchesAllocatedArena(OS.Kernel.Memory.BootPageAllocatorState state, OS.Kernel.Memory.BootReservedRange arena)
    {
        return arena.IsValid && arena.Kind == OS.Kernel.Memory.BootReservationKind.AllocatorArena && arena.PageCount >= MinimumArenaPageCount && arena.ByteLength >= RequiredPageSize && state.IsInitialized && state.PageSize == RequiredPageSize && state.HeadNodeIndex == 0 && state.FreeNodeCount == 1 && state.FreePageCount == arena.PageCount - RequiredPageCount && state.FreeByteCount == arena.ByteLength - RequiredPageSize && state.AllocatedPageCount == RequiredPageCount && state.AllocatedByteCount == RequiredPageSize;
    }

    private static bool IsAllocatedRangeInsideArena(PhysicalAddress allocatedStart, PhysicalAddress allocatedEnd, OS.Kernel.Memory.BootReservedRange arena)
    {
        return allocatedStart.TryGetRawValueForUefiBoundary(out ulong start) == AddressValueStatus.Success && allocatedEnd.TryGetRawValueForUefiBoundary(out ulong end) == AddressValueStatus.Success && arena.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong arenaStart) == AddressValueStatus.Success && arena.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong arenaEnd) == AddressValueStatus.Success && start == arenaStart && end <= arenaEnd && start <= MaximumUnsignedLong - RequiredPageSize && end == start + RequiredPageSize;
    }

    private static bool NodeMatchesRange(OS.Kernel.Memory.BootPageFreeListNode node, OS.Kernel.Memory.BootReservationSourceRange range)
    {
        return range.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong start) == AddressValueStatus.Success && range.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong end) == AddressValueStatus.Success && node.SourceFreeSegmentIndex == 0 && node.SourceUsableRangeIndex == 0 && node.PhysicalStart == start && node.PhysicalEndExclusive == end && node.PageCount == range.PageCount && node.ByteLength == range.ByteLength && node.NextNodeIndex == -1;
    }

    private static bool NodeMatchesReservation(OS.Kernel.Memory.BootPageFreeListNode node, OS.Kernel.Memory.BootReservedRange arena)
    {
        return arena.IsValid && arena.Kind == OS.Kernel.Memory.BootReservationKind.AllocatorArena && arena.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong start) == AddressValueStatus.Success && arena.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong end) == AddressValueStatus.Success && node.SourceFreeSegmentIndex == 0 && node.SourceUsableRangeIndex == 0 && node.PhysicalStart == start && node.PhysicalEndExclusive == end && node.PageCount == arena.PageCount && node.ByteLength == arena.ByteLength && node.NextNodeIndex == -1;
    }

    private static bool NodeMatchesAllocatedSuffix(OS.Kernel.Memory.BootPageFreeListNode node, OS.Kernel.Memory.BootReservedRange arena)
    {
        return arena.IsValid && arena.Kind == OS.Kernel.Memory.BootReservationKind.AllocatorArena && arena.PageCount >= MinimumArenaPageCount && arena.ByteLength >= RequiredPageSize * MinimumArenaPageCount && arena.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong start) == AddressValueStatus.Success && arena.PhysicalEndExclusive.TryGetRawValueForUefiBoundary(out ulong end) == AddressValueStatus.Success && start <= MaximumUnsignedLong - RequiredPageSize && node.SourceFreeSegmentIndex == 0 && node.SourceUsableRangeIndex == 0 && node.PhysicalStart == start + RequiredPageSize && node.PhysicalEndExclusive == end && node.PageCount == arena.PageCount - RequiredPageCount && node.ByteLength == arena.ByteLength - RequiredPageSize && node.NextNodeIndex == -1;
    }

    private static bool SourceRangeMatchesReservation(OS.Kernel.Memory.BootReservationSourceRange range, OS.Kernel.Memory.BootReservedRange reservation)
    {
        return range.SourceRangeIndex == 0 && range.PhysicalStart.Equals(reservation.PhysicalStart) && range.PhysicalEndExclusive.Equals(reservation.PhysicalEndExclusive) && range.PageCount == reservation.PageCount && range.ByteLength == reservation.ByteLength;
    }

    private static bool AllocationsEqual(OS.Kernel.Memory.BootPageAllocatorOnePageResult left, OS.Kernel.Memory.BootPageAllocatorOnePageResult right)
    {
        return left.IsInitialized == right.IsInitialized && left.Status == right.Status && left.ReservationStatus == right.ReservationStatus && left.AddressStatus == right.AddressStatus && StatesEqual(left.BeforeState, right.BeforeState) && StatesEqual(left.AfterState, right.AfterState) && left.PhysicalStart.Equals(right.PhysicalStart) && left.PhysicalEndExclusive.Equals(right.PhysicalEndExclusive) && left.PageCount == right.PageCount && left.ByteLength == right.ByteLength && left.NodeCount == right.NodeCount && left.NodeCapacity == right.NodeCapacity && left.ReservationCount == right.ReservationCount && left.ReservationCapacity == right.ReservationCapacity && left.NodeIndex == right.NodeIndex && left.InsertedNodeIndex == right.InsertedNodeIndex && left.VisitedNodeCount == right.VisitedNodeCount && left.InvalidReservationIndex == right.InvalidReservationIndex && left.ConflictingIndex == right.ConflictingIndex && RestoreReceiptsEqual(left.RestoreReceipt, right.RestoreReceipt);
    }

    private static bool RestoreReceiptsEqual(OS.Kernel.Memory.BootPageAllocatorOnePageRestoreReceipt left, OS.Kernel.Memory.BootPageAllocatorOnePageRestoreReceipt right)
    {
        return left.IsInitialized == right.IsInitialized && NodesEqual(left.SourceNodeBefore, right.SourceNodeBefore) && left.PreviousNodeIndexBefore == right.PreviousNodeIndexBefore && NodesEqual(left.PreviousNodeBefore, right.PreviousNodeBefore);
    }

    private static bool HandoffsEqual(OwnedBootStackSwitchHandoff left, OwnedBootStackSwitchHandoff right)
    {
        return left.Size == right.Size && left.Version == right.Version && left.StorageStart.Equals(right.StorageStart) && left.StorageEndExclusive.Equals(right.StorageEndExclusive) && left.StorageByteLength == right.StorageByteLength && left.StubEntry.Equals(right.StubEntry) && left.UsableStackStart.Equals(right.UsableStackStart) && left.UsableStackEndExclusive.Equals(right.UsableStackEndExclusive) && left.PreTransferStackPointer.Equals(right.PreTransferStackPointer) && left.ExpectedContinuationStackPointer.Equals(right.ExpectedContinuationStackPointer) && left.Continuation.Equals(right.Continuation) && left.Provenance == right.Provenance && left.ContinuationContract == right.ContinuationContract;
    }

    private static bool StackLayoutTopologyEquals(OwnedBootStackLayout left, OwnedBootStackLayout right)
    {
        return left.BackingStart.Equals(right.BackingStart) && left.BackingEndExclusive.Equals(right.BackingEndExclusive) && left.BackingPageCount == right.BackingPageCount && left.BackingByteLength == right.BackingByteLength && left.GuardReservation.Equals(right.GuardReservation) && left.StackReservation.Equals(right.StackReservation) && left.PhysicalStackTop.Equals(right.PhysicalStackTop);
    }

    private static bool StatesEqual(OS.Kernel.Memory.BootPageAllocatorState left, OS.Kernel.Memory.BootPageAllocatorState right)
    {
        return left.IsInitialized == right.IsInitialized && left.PageSize == right.PageSize && left.HeadNodeIndex == right.HeadNodeIndex && left.FreeNodeCount == right.FreeNodeCount && left.FreePageCount == right.FreePageCount && left.FreeByteCount == right.FreeByteCount && left.AllocatedPageCount == right.AllocatedPageCount && left.AllocatedByteCount == right.AllocatedByteCount;
    }

    private static bool NodesEqual(OS.Kernel.Memory.BootPageFreeListNode left, OS.Kernel.Memory.BootPageFreeListNode right)
    {
        return left.SourceFreeSegmentIndex == right.SourceFreeSegmentIndex && left.SourceUsableRangeIndex == right.SourceUsableRangeIndex && left.PhysicalStart == right.PhysicalStart && left.PhysicalEndExclusive == right.PhysicalEndExclusive && left.PageCount == right.PageCount && left.ByteLength == right.ByteLength && left.NextNodeIndex == right.NextNodeIndex;
    }

    private static bool IsDefaultState(M01LiveAllocatorStaticState state)
    {
        return state.HeaderSize == 0 && state.HeaderVersion == 0 && state.CoordinatorStatus == M01LiveAllocatorStatus.Uninitialized && state.CoordinatorCheckpoint == M01LiveAllocatorCheckpoint.Uninitialized && state.Reserved0 == 0 && state.Reserved1 == 0 && state.Reserved2 == 0 && state.Reserved3 == 0 && state.Reserved4 == 0 && state.Reserved5 == 0 && state.DoubleFreeStatusValue == OS.Kernel.Memory.BootPageAllocatorMutationStatus.Success && state.ReservedWord == 0 && state.StageOneStateAddressValue == 0 && state.StageOneStateCapacityValue == 0 && state.RefreshedMapKeyValue == 0 && state.RefreshedMapGenerationValue == 0 && state.ArenaReservationValue.Equals(default) && state.ArenaSourceRangeValue.Equals(default) && state.FreeRangeValue.Equals(default) && NodesEqual(state.FreeNodeValue, default) && StatesEqual(state.AllocatorStateValue, default) && !state.AllocatedPageStartValue.IsValid && !state.AllocatedPageEndExclusiveValue.IsValid && state.InitialFreePagesValue == 0 && state.InitialFreeBytesValue == 0;
    }

    private static bool RangesOverlap(ulong firstStart, ulong firstEnd, ulong secondStart, ulong secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static bool AreMetadataRangesDisjoint(ulong firstStart, ulong firstEnd, ulong secondStart, ulong secondEnd)
    {
        return firstStart < firstEnd && secondStart < secondEnd && !RangesOverlap(firstStart, firstEnd, secondStart, secondEnd);
    }

    private static bool MetadataIsDisjointFromMap(ulong metadataStart, ulong metadataEnd, FirmwareMemoryMapRecord map)
    {
        ulong ownedMapEnd = map.BufferAddress + map.BufferSize;
        ulong sourceMapEnd = map.SourceBufferAddress + map.SourceBufferSize;
        return AreMetadataRangesDisjoint(metadataStart, metadataEnd, map.BufferAddress, ownedMapEnd) && AreMetadataRangesDisjoint(metadataStart, metadataEnd, map.SourceBufferAddress, sourceMapEnd);
    }
}
