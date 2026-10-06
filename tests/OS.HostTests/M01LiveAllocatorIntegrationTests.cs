using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static unsafe class M01LiveAllocatorIntegrationTests
{
    private const string IssueTestName = "m01.018";
    private const int PageSize = (int)BootReservedRange.PageSize;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run(
            "m01.018 stable enums layouts defaults and capabilities",
            TestStableSurfaceAndDefaults,
            ref failures);
        Run(
            "m01.018 canonical final stage one alloc free accounting",
            TestCanonicalSuccessAndAccounting,
            ref failures);
        Run(
            "m01.018 leaf audits classify every late failure",
            TestLeafAuditFailures,
            ref failures);
        Run(
            "m01.018 boot sequence success failure and null outputs",
            TestBootSequencePresenter,
            ref failures);
        Run(
            "m01.018 final stage one prerequisite is mandatory",
            TestFinalStageOnePrerequisite,
            ref failures);
        Run(
            "m01.018 pointer count capacity and overflow preflight",
            TestPointerCountCapacityPreflight,
            ref failures);
        Run(
            "m01.018 metadata map and arena aliases fail closed",
            TestAliasPreflight,
            ref failures);
        Run(
            "m01.018 allocation evidence status and binding are exact",
            TestAllocationEvidenceFailures,
            ref failures);
        Run(
            "m01.018 arena type range and accounting failures are exact",
            TestArenaFailures,
            ref failures);
        Run(
            "m01.018 refreshed map generations ordinals and owner bind",
            TestMapLedgerEvidenceFailures,
            ref failures);
        Run(
            "m01.018 canonical ledger missing duplicate mismatch unsorted",
            TestReservationLedgerFailures,
            ref failures);
        Run(
            "m01.018 arena confinement duplicate phase and state safety",
            TestConfinementDuplicateAndStateSafety,
            ref failures);
        Run(
            "m01.018 deterministic and nonmutating evidence",
            TestDeterminismAndNonMutation,
            ref failures);
        Run(
            "m01.018 source api marker and project firewalls",
            TestSourceApiAndMarkerFirewalls,
            ref failures);
        Run(
            "m01.018 exact source closure compiles under nostdlib efi",
            TestNoStdLibEfiCompileClosure,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-018 live allocator integration tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_018_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(
        string name,
        Func<bool> test,
        ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL "
                + name
                + " "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static bool TestStableSurfaceAndDefaults()
    {
        (string Name, byte Value)[] statuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("StageOneRejected", 2),
            ("CoordinatorStateRejected", 3),
            ("DuplicatePhase", 4),
            ("AllocationTableRejected", 5),
            ("AllocationEvidenceRejected", 6),
            ("ArenaAllocationRejected", 7),
            ("MapLedgerEvidenceRejected", 8),
            ("ReservationLedgerRejected", 9),
            ("ArenaReservationRejected", 10),
            ("SourceRangeRejected", 11),
            ("ReservationSubtractionRejected", 12),
            ("FreeRangeRejected", 13),
            ("StateMaterializationRejected", 14),
            ("AllocationRejected", 15),
            ("AllocationContainmentRejected", 16),
            ("FreeRejected", 17),
            ("FreeAccountingRejected", 18),
            ("DoubleFreeNotRejected", 19),
            ("IllegalCoordinatorState", 20)
        ];
        (string Name, byte Value)[] checkpoints =
        [
            ("Uninitialized", 0),
            ("StageOneValidated", 1),
            ("ArenaAllocationValidated", 2),
            ("MapLedgerValidated", 3),
            ("ReservationsValidated", 4),
            ("FreeListMaterialized", 5),
            ("PageAllocated", 6),
            ("PageContained", 7),
            ("PageFreed", 8),
            ("DoubleFreeRejected", 9)
        ];
        (string Name, byte Value)[] allocationProvenance =
        [
            ("Uninitialized", 0),
            ("CallerAttestedSuccessfulFirmwarePageAllocation", 1)
        ];
        (string Name, byte Value)[] mapProvenance =
        [
            ("Uninitialized", 0),
            ("CallerAttestedPostArenaAllocationOwnedMap", 1)
        ];
        (string Name, byte Value)[] arenaStatuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("InvalidMemoryType", 2),
            ("InvalidPhysicalStart", 3),
            ("MisalignedPhysicalStart", 4),
            ("InvalidVirtualStart", 5),
            ("MisalignedVirtualStart", 6),
            ("NonIdentityVirtualStart", 7),
            ("TooFewPages", 8),
            ("TooManyPages", 9),
            ("PageByteCountOverflow", 10),
            ("ByteLengthMismatch", 11),
            ("PhysicalEndOverflow", 12),
            ("PhysicalEndMismatch", 13),
            ("VirtualEndOverflow", 14)
        ];

        M01LiveAllocatorIntegrationResult empty = default;
        M01LiveAllocatorAllocationEvidence emptyAllocation = default;
        M01LiveAllocatorMapLedgerEvidence emptyMap = default;
        M01LiveAllocatorStaticState emptyState = default;

        return EnumContractMatches<M01LiveAllocatorStatus>(statuses)
            && EnumContractMatches<M01LiveAllocatorCheckpoint>(
                checkpoints)
            && EnumContractMatches<
                M01LiveAllocatorAllocationProvenance>(
                    allocationProvenance)
            && EnumContractMatches<
                M01LiveAllocatorMapLedgerProvenance>(
                    mapProvenance)
            && EnumContractMatches<M01LiveAllocatorArenaStatus>(
                arenaStatuses)
            && M01LiveAllocatorAllocationEvidence.CurrentVersion == 1
            && M01LiveAllocatorAllocationEvidence.StructureSize == 56
            && M01LiveAllocatorAllocationEvidence.EfiSuccess == 0
            && M01LiveAllocatorMapLedgerEvidence.CurrentVersion == 1
            && M01LiveAllocatorMapLedgerEvidence.StructureSize == 168
            && M01LiveAllocatorStaticState.CurrentVersion == 1
            && M01LiveAllocatorStaticState.RequiredStorageAlignment == 8
            && M01LiveAllocatorStaticState.RequiredStorageBytes == 312
            && Unsafe.SizeOf<M01LiveAllocatorAllocationEvidence>()
                == M01LiveAllocatorAllocationEvidence.StructureSize
            && Unsafe.SizeOf<M01LiveAllocatorMapLedgerEvidence>()
                == M01LiveAllocatorMapLedgerEvidence.StructureSize
            && (ulong)Unsafe.SizeOf<M01LiveAllocatorStaticState>()
                == M01LiveAllocatorStaticState.RequiredStorageBytes
            && Marshal.OffsetOf<M01LiveAllocatorStaticState>(
                    "StageOneStateCapacityValue").ToInt64() == 32
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LiveAllocatorAllocationEvidence>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LiveAllocatorMapLedgerEvidence>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LiveAllocatorStaticState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LiveAllocatorEvidence>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LiveAllocatorIntegrationResult>()
            && empty.Status == M01LiveAllocatorStatus.Uninitialized
            && empty.LastCompletedCheckpoint
                == M01LiveAllocatorCheckpoint.Uninitialized
            && !empty.Succeeded
            && !emptyAllocation.IsWellFormed
            && !emptyMap.IsWellFormedFor(emptyAllocation)
            && emptyState.Size == 0
            && emptyState.Version == 0
            && emptyState.Status == M01LiveAllocatorStatus.Uninitialized
            && emptyState.Checkpoint
                == M01LiveAllocatorCheckpoint.Uninitialized
            && !emptyState.IsCompleted
            && !M01LiveAllocatorIntegration.CallsFirmwarePageAllocator
            && !M01LiveAllocatorIntegration.InvokesFirmware
            && !M01LiveAllocatorIntegration
                .TouchesAllocatedPhysicalPage
            && !M01LiveAllocatorIntegration
                .PerformsPhysicalMemoryAccess
            && M01LiveAllocatorIntegration
                .MutatesCallerOwnedAllocatorMetadata
            && !M01LiveAllocatorIntegration.IsGuestEvidence
            && !M01LiveAllocatorIntegration
                .EstablishesPostFirmwareAllocator
            && !M01LiveAllocatorIntegration
                .TransfersConventionalMemoryOwnership
            && !M01LiveAllocatorIntegration.ConfiguresPaging
            && !M01LiveAllocatorIntegration
                .EnforcesNxOrWriteXorExecute
            && M01LiveAllocatorIntegration
                .RequiredAllocationRecordCount == 1
            && M01LiveAllocatorIntegration
                .MaximumAllocationRecordCapacity == 16
            && M01LiveAllocatorIntegration.MinimumArenaPageCount == 2
            && M01LiveAllocatorIntegration.MaximumArenaPageCount == 4096
            && M01LiveAllocatorIntegration.RequiredPageSize == 4096
            && M01LiveAllocatorIntegration.RequiredPageCount == 1
            && M01LiveAllocatorIntegration.RequiredPointerAlignment == 8
            && M01LiveAllocatorIntegration.NoIndex == ~0UL;
    }

    private static bool TestCanonicalSuccessAndAccounting()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        if (!fixture.HasFinalStageOne)
        {
            return CaseFailure("stage-one-not-final");
        }

        M01LiveAllocatorIntegrationResult result =
            fixture.ExecuteCurrent();
        M01LiveAllocatorEvidence evidence = result.Evidence;
        M01LiveAllocatorStaticState state = result.StaticState;
        M01LiveAllocatorStaticState wrappedPersistedCapacity =
            WithUlongAt(state, 32, ~0UL);
        ulong arenaStart = fixture.ArenaStart;
        ulong arenaEnd = arenaStart + fixture.ArenaBytes;
        ulong allocatedStart = Raw(evidence.Allocation.PhysicalStart);
        ulong allocatedEnd =
            Raw(evidence.Allocation.PhysicalEndExclusive);

        return result.Succeeded
            && result.Status == M01LiveAllocatorStatus.Success
            && result.LastCompletedCheckpoint
                == M01LiveAllocatorCheckpoint.DoubleFreeRejected
            && evidence.StageOneStateAddress
                == (ulong)fixture.StageOneState
            && evidence.StageOneStateCapacity
                == M01LoadedImageStackStaticState.RequiredStorageBytes
            && evidence.StaticStateAddress
                == (ulong)fixture.StaticState
            && evidence.StaticStateCapacity
                == M01LiveAllocatorStaticState.RequiredStorageBytes
            && evidence.AllocationEvidence.IsWellFormed
            && evidence.MapLedgerEvidence.IsWellFormedFor(
                evidence.AllocationEvidence)
            && evidence.ArenaStatus
                == M01LiveAllocatorArenaStatus.Success
            && evidence.ReservationValidation.Succeeded
            && evidence.ArenaReservationIndex
                == fixture.ArenaReservationIndex
            && evidence.ArenaReservation.Equals(
                fixture.Ledger[fixture.ArenaReservationIndex])
            && evidence.SourceRangeStatus
                == BootReservationSourceRangeStatus.Success
            && evidence.Subtraction.Succeeded
            && evidence.Subtraction.SourceRangeCount == 1
            && evidence.Subtraction.ReservationCount == 0
            && evidence.Subtraction.RequiredOutputRangeCount == 1
            && evidence.Subtraction.WrittenOutputRangeCount == 1
            && evidence.Subtraction.RemovedPages == 0
            && evidence.Subtraction.RemovedBytes == 0
            && evidence.Subtraction.RemainingPages
                == fixture.ArenaPages
            && evidence.Subtraction.RemainingBytes
                == fixture.ArenaBytes
            && evidence.StateMaterialization.Succeeded
            && evidence.Allocation.Succeeded
            && evidence.Allocation.PageCount == 1
            && evidence.Allocation.ByteLength
                == BootReservedRange.PageSize
            && evidence.Allocation.BeforeFreePageCount
                == fixture.ArenaPages
            && evidence.Allocation.AfterFreePageCount
                == fixture.ArenaPages - 1
            && evidence.Allocation.BeforeFreeByteCount
                == fixture.ArenaBytes
            && evidence.Allocation.AfterFreeByteCount
                == fixture.ArenaBytes - BootReservedRange.PageSize
            && evidence.Allocation.BeforeAllocatedPageCount == 0
            && evidence.Allocation.AfterAllocatedPageCount == 1
            && evidence.Allocation.BeforeAllocatedByteCount == 0
            && evidence.Allocation.AfterAllocatedByteCount
                == BootReservedRange.PageSize
            && allocatedStart >= arenaStart
            && allocatedEnd <= arenaEnd
            && allocatedEnd - allocatedStart
                == BootReservedRange.PageSize
            && evidence.Free.Succeeded
            && evidence.Free.BeforeFreePageCount
                == fixture.ArenaPages - 1
            && evidence.Free.AfterFreePageCount
                == fixture.ArenaPages
            && evidence.Free.BeforeAllocatedPageCount == 1
            && evidence.Free.AfterAllocatedPageCount == 0
            && evidence.DoubleFree.IsInitialized
            && !evidence.DoubleFree.Succeeded
            && evidence.DoubleFree.Status
                == BootPageAllocatorMutationStatus.FreeRangeNotAllocated
            && StateEqual(
                evidence.DoubleFree.BeforeState,
                evidence.DoubleFree.AfterState)
            && state.IsCompleted
            && state.Size
                == M01LiveAllocatorStaticState.RequiredStorageBytes
            && state.Version == M01LiveAllocatorStaticState.CurrentVersion
            && state.Status == M01LiveAllocatorStatus.Success
            && state.Checkpoint
                == M01LiveAllocatorCheckpoint.DoubleFreeRejected
            && state.DoubleFreeStatus
                == BootPageAllocatorMutationStatus.FreeRangeNotAllocated
            && state.StageOneStateAddress
                == (ulong)fixture.StageOneState
            && state.RefreshedMapKey == fixture.StageTwoMap.MapKey
            && state.RefreshedMapGeneration
                == CanonicalFixture.StageTwoMapGeneration
            && state.ArenaSourceRange.Equals(state.FreeRange)
            && state.AllocatorState.IsInitialized
            && state.AllocatorState.FreeNodeCount == 1
            && state.AllocatorState.FreePageCount
                == fixture.ArenaPages
            && state.AllocatorState.AllocatedPageCount == 0
            && state.InitialFreePages == fixture.ArenaPages
            && state.InitialFreeBytes == fixture.ArenaBytes
            && !wrappedPersistedCapacity.IsCompleted
            && StructBytesEqual(state, *fixture.StaticState);
    }

    private static bool TestLeafAuditFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LiveAllocatorIntegrationResult result =
            fixture.ExecuteCurrent();
        if (!result.Succeeded)
        {
            return CaseFailure("audit-fixture");
        }

        BootReservedRange arena = result.Evidence.ArenaReservation;
        BootReservationSourceRange source =
            result.StaticState.ArenaSourceRange;
        BootReservationSourceRange freeRange =
            result.StaticState.FreeRange;
        BootPageFreeListNode initialNode =
            result.StaticState.FreeNode;
        BootPageAllocatorState initialState =
            result.Evidence.Allocation.BeforeState;
        BootPageAllocatorOnePageResult allocation =
            result.Evidence.Allocation;
        BootPageAllocatorOnePageFreeResult free =
            result.Evidence.Free;
        BootPageAllocatorOnePageFreeResult doubleFree =
            result.Evidence.DoubleFree;
        ulong arenaStart = Raw(arena.PhysicalStart);
        ulong arenaEnd = Raw(arena.PhysicalEndExclusive);
        BootPageFreeListNode nodeAfterAllocation =
            new BootPageFreeListNode(
                sourceFreeSegmentIndex: 0,
                sourceUsableRangeIndex: 0,
                arenaStart + BootReservedRange.PageSize,
                arenaEnd,
                arena.PageCount - 1,
                arena.ByteLength - BootReservedRange.PageSize,
                nextNodeIndex: -1);

        BootReservedRange unrelatedArena = CreateRange(
            BootReservationKind.AllocatorArena,
            fixture.UnrelatedRangeStart,
            fixture.ArenaPages);
        if (BootReservationSourceRange.TryCreate(
                sourceRangeIndex: 0,
                unrelatedArena.PhysicalStart,
                unrelatedArena.PageCount,
                out BootReservationSourceRange unrelatedSource)
                != BootReservationSourceRangeStatus.Success)
        {
            return CaseFailure("unrelated-source");
        }

        BootPageFreeListNode unrelatedNode =
            new BootPageFreeListNode(
                sourceFreeSegmentIndex: 0,
                sourceUsableRangeIndex: 0,
                fixture.UnrelatedRangeStart,
                fixture.UnrelatedRangeStart
                    + unrelatedArena.ByteLength,
                unrelatedArena.PageCount,
                unrelatedArena.ByteLength,
                nextNodeIndex: -1);
        BootPageFreeListNode unrelatedNodeAfterAllocation =
            unrelatedNode;
        BootPageAllocatorState unrelatedState = default;
        BootPageAllocatorStateMaterializationResult
            unrelatedMaterialization =
                BootPageAllocatorStateMaterializer.Materialize(
                    &unrelatedNode,
                    sourceNodeCount: 1,
                    sourceNodeCapacity: 1,
                    sourceRangeCount: 1,
                    headNodeIndex: 0,
                    BootReservedRange.PageSize,
                    unrelatedArena.PageCount,
                    unrelatedArena.ByteLength,
                    &unrelatedState,
                    destinationStateCapacity: 1);
        if (!unrelatedMaterialization.Succeeded)
        {
            return CaseFailure("unrelated-materialization");
        }

        BootPageAllocatorOnePageResult unrelatedAllocation =
            BootPageAllocatorMutator.AllocateOnePage(
                unrelatedState,
                &unrelatedNodeAfterAllocation,
                nodeCount: 1,
                nodeCapacity: 1,
                reservations: null,
                reservationCount: 0,
                reservationCapacity: 0,
                pageCount: 1,
                alignment: BootReservedRange.PageSize);
        if (!unrelatedAllocation.Succeeded)
        {
            return CaseFailure("unrelated-allocation");
        }

        return M01LiveAllocatorIntegration.AuditArenaSource(
                arena,
                source,
                freeRange,
                result.Evidence.Subtraction,
                initialNode)
                == M01LiveAllocatorStatus.Success
            && M01LiveAllocatorIntegration.AuditArenaSource(
                default,
                source,
                freeRange,
                result.Evidence.Subtraction,
                initialNode)
                == M01LiveAllocatorStatus.SourceRangeRejected
            && M01LiveAllocatorIntegration.AuditArenaSource(
                arena,
                unrelatedSource,
                freeRange,
                result.Evidence.Subtraction,
                initialNode)
                == M01LiveAllocatorStatus.SourceRangeRejected
            && M01LiveAllocatorIntegration.AuditArenaSource(
                arena,
                source,
                freeRange,
                default,
                initialNode)
                == M01LiveAllocatorStatus
                    .ReservationSubtractionRejected
            && M01LiveAllocatorIntegration.AuditArenaSource(
                arena,
                source,
                default,
                result.Evidence.Subtraction,
                initialNode)
                == M01LiveAllocatorStatus.FreeRangeRejected
            && M01LiveAllocatorIntegration.AuditArenaSource(
                arena,
                source,
                freeRange,
                result.Evidence.Subtraction,
                unrelatedNode)
                == M01LiveAllocatorStatus.FreeRangeRejected
            && M01LiveAllocatorIntegration.AuditMaterialization(
                arena,
                initialNode,
                result.Evidence.StateMaterialization)
                == M01LiveAllocatorStatus.Success
            && M01LiveAllocatorIntegration.AuditMaterialization(
                arena,
                initialNode,
                default)
                == M01LiveAllocatorStatus.StateMaterializationRejected
            && M01LiveAllocatorIntegration.AuditMaterialization(
                arena,
                unrelatedNode,
                result.Evidence.StateMaterialization)
                == M01LiveAllocatorStatus.StateMaterializationRejected
            && M01LiveAllocatorIntegration.AuditAllocation(
                arena,
                initialState,
                initialNode,
                allocation,
                nodeAfterAllocation)
                == M01LiveAllocatorStatus.Success
            && M01LiveAllocatorIntegration.AuditAllocation(
                arena,
                initialState,
                initialNode,
                default,
                nodeAfterAllocation)
                == M01LiveAllocatorStatus.AllocationRejected
            && M01LiveAllocatorIntegration.AuditAllocation(
                arena,
                initialState,
                initialNode,
                allocation,
                default)
                == M01LiveAllocatorStatus
                    .AllocationContainmentRejected
            && M01LiveAllocatorIntegration.AuditAllocation(
                unrelatedArena,
                initialState,
                unrelatedNode,
                allocation,
                nodeAfterAllocation)
                == M01LiveAllocatorStatus
                    .AllocationContainmentRejected
            && M01LiveAllocatorIntegration.AuditAllocation(
                arena,
                initialState,
                initialNode,
                unrelatedAllocation,
                unrelatedNodeAfterAllocation)
                == M01LiveAllocatorStatus
                    .AllocationContainmentRejected
            && M01LiveAllocatorIntegration.AuditFree(
                initialState,
                initialNode,
                allocation,
                free,
                initialNode)
                == M01LiveAllocatorStatus.Success
            && M01LiveAllocatorIntegration.AuditFree(
                initialState,
                initialNode,
                allocation,
                default,
                initialNode)
                == M01LiveAllocatorStatus.FreeRejected
            && M01LiveAllocatorIntegration.AuditFree(
                initialState,
                initialNode,
                allocation,
                free,
                unrelatedNode)
                == M01LiveAllocatorStatus.FreeAccountingRejected
            && M01LiveAllocatorIntegration.AuditDoubleFree(
                arena,
                initialState,
                initialNode,
                allocation,
                doubleFree,
                initialNode)
                == M01LiveAllocatorStatus.Success
            && M01LiveAllocatorIntegration.AuditDoubleFree(
                arena,
                initialState,
                initialNode,
                allocation,
                default,
                initialNode)
                == M01LiveAllocatorStatus.DoubleFreeNotRejected
            && M01LiveAllocatorIntegration.AuditDoubleFree(
                arena,
                initialState,
                initialNode,
                allocation,
                doubleFree,
                unrelatedNode)
                == M01LiveAllocatorStatus.DoubleFreeNotRejected
            && M01LiveAllocatorIntegration.AuditDoubleFree(
                arena,
                initialState,
                initialNode,
                unrelatedAllocation,
                doubleFree,
                initialNode)
                == M01LiveAllocatorStatus.DoubleFreeNotRejected;
    }

    private static bool TestBootSequencePresenter()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LiveAllocatorIntegrationResult success =
            fixture.ExecuteCurrent();
        TestSerialSink successSerial = new TestSerialSink();
        BootEventRingBuffer successEvents =
            new BootEventRingBuffer(16);
        BootResult successResult =
            BootSequence.RunM01LiveAllocatorIntegration(
                success,
                successSerial,
                successEvents);
        string[] expected =
        [
            "TEST_START m01.stage2",
            "M01_ALLOCATOR_ARENA_ALLOCATED",
            "M01_STAGE2_MAP_RECAPTURED",
            "M01_RESERVATIONS_APPLIED",
            "M01_LIVE_ALLOCATED_4K",
            "M01_LIVE_PAGE_WITHIN_ARENA",
            "M01_LIVE_FREED_4K",
            "M01_DOUBLE_FREE_REJECTED",
            "TEST_PASS m01.stage2"
        ];
        if (!successResult.Succeeded
            || successSerial.Count != expected.Length)
        {
            return CaseFailure("presenter-success-count");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    successSerial.LineAt(index),
                    expected[index],
                    StringComparison.Ordinal))
            {
                return CaseFailure("presenter-success-line-" + index);
            }
        }

        using CanonicalFixture failureFixture = new CanonicalFixture();
        failureFixture.ClearStageOneState();
        M01LiveAllocatorIntegrationResult failure =
            failureFixture.ExecuteCurrent();
        TestSerialSink failureSerial = new TestSerialSink();
        BootEventRingBuffer failureEvents =
            new BootEventRingBuffer(8);
        BootResult failureResult =
            BootSequence.RunM01LiveAllocatorIntegration(
                failure,
                failureSerial,
                failureEvents);
        string detail =
            (byte)M01LiveAllocatorStatus.StageOneRejected
            + " checkpoint="
            + (byte)M01LiveAllocatorCheckpoint.Uninitialized;
        if (failureResult.Succeeded
            || failureSerial.Count != 3
            || !string.Equals(
                failureSerial.LineAt(0),
                "TEST_START m01.stage2",
                StringComparison.Ordinal)
            || !string.Equals(
                failureSerial.LineAt(1),
                BootSequence.M01StageTwoTestFailurePrefix + detail,
                StringComparison.Ordinal)
            || !string.Equals(
                failureSerial.LineAt(2),
                BootSequence.M01StageTwoFailurePrefix + detail,
                StringComparison.Ordinal)
            || failureSerial.Contains("TEST_PASS m01.stage2"))
        {
            return CaseFailure("presenter-failure");
        }

        M01LiveAllocatorIntegrationResult forgedNonfinalSuccess =
            WithByteAt(
                default(M01LiveAllocatorIntegrationResult),
                0,
                (byte)M01LiveAllocatorStatus.Success);
        TestSerialSink nonfinalSerial = new TestSerialSink();
        BootEventRingBuffer nonfinalEvents =
            new BootEventRingBuffer(8);
        BootResult nonfinal =
            BootSequence.RunM01LiveAllocatorIntegration(
                forgedNonfinalSuccess,
                nonfinalSerial,
                nonfinalEvents);
        string illegalDetail =
            (byte)M01LiveAllocatorStatus.IllegalCoordinatorState
            + " checkpoint=0";
        if (nonfinal.Succeeded
            || nonfinalSerial.Count != 3
            || !nonfinalSerial.Contains(
                BootSequence.M01StageTwoTestFailurePrefix
                    + illegalDetail)
            || nonfinalSerial.Contains("TEST_PASS m01.stage2"))
        {
            return CaseFailure("presenter-nonfinal-success");
        }

        TestSerialSink nullEventSerial = new TestSerialSink();
        BootEventRingBuffer nullSerialEvents =
            new BootEventRingBuffer(4);
        BootResult nullSerial =
            BootSequence.RunM01LiveAllocatorIntegration(
                default,
                null!,
                nullSerialEvents);
        BootResult nullEvents =
            BootSequence.RunM01LiveAllocatorIntegration(
                default,
                nullEventSerial,
                null!);
        return !nullSerial.Succeeded
            && string.Equals(
                nullSerial.Reason,
                "serial sink is required",
                StringComparison.Ordinal)
            && nullSerialEvents.Count == 0
            && !nullEvents.Succeeded
            && string.Equals(
                nullEvents.Reason,
                "boot event ring buffer is required",
                StringComparison.Ordinal)
            && nullEventSerial.Count == 0;
    }

    private static bool TestFinalStageOnePrerequisite()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        fixture.ClearStageOneState();
        if (!ExpectFailure(
                fixture.ExecuteCurrent(),
                M01LiveAllocatorStatus.StageOneRejected,
                M01LiveAllocatorCheckpoint.Uninitialized,
                "default-stage-one"))
        {
            return false;
        }

        fixture.ResetCanonical(StageOneCompletion.PreparedOnly);
        if (fixture.StageOneState->Checkpoint
                != M01LoadedImageStackCheckpoint.SwitchArmed
            || !ExpectFailure(
                fixture.ExecuteCurrent(),
                M01LiveAllocatorStatus.StageOneRejected,
                M01LiveAllocatorCheckpoint.Uninitialized,
                "prepared-only"))
        {
            return false;
        }

        fixture.ResetCanonical(StageOneCompletion.TransferRecorded);
        if (fixture.StageOneState->Checkpoint
                != M01LoadedImageStackCheckpoint.TransferRecorded
            || !ExpectFailure(
                fixture.ExecuteCurrent(),
                M01LiveAllocatorStatus.StageOneRejected,
                M01LiveAllocatorCheckpoint.Uninitialized,
                "transfer-only"))
        {
            return false;
        }

        fixture.ResetCanonical(StageOneCompletion.Final);
        M01LiveAllocatorIntegrationResult final =
            fixture.ExecuteCurrent();
        if (!fixture.HasFinalStageOne || !final.Succeeded)
        {
            return false;
        }

        fixture.ResetCanonical(StageOneCompletion.Final);
        byte* copiedStorage = (byte*)NativeMemory.AlignedAlloc(
            (nuint)M01LoadedImageStackStaticState.RequiredStorageBytes,
            (nuint)M01LoadedImageStackStaticState.RequiredStorageAlignment);
        if (copiedStorage == null)
        {
            throw new OutOfMemoryException(
                "Unable to allocate copied stage-one state.");
        }

        try
        {
            for (ulong index = 0;
                index
                    < M01LoadedImageStackStaticState
                        .RequiredStorageBytes;
                index++)
            {
                copiedStorage[index] =
                    ((byte*)fixture.StageOneState)[index];
            }

            M01LiveAllocatorAllocationEvidence allocation =
                fixture.CreateAllocationEvidence();
            M01LiveAllocatorIntegrationResult copied =
                fixture.Invoke(
                    (M01LoadedImageStackStaticState*)copiedStorage,
                    M01LoadedImageStackStaticState
                        .RequiredStorageBytes,
                    fixture.Records,
                    1,
                    1,
                    allocation,
                    fixture.CreateMapEvidence(allocation),
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes);
            return ExpectFailure(
                copied,
                M01LiveAllocatorStatus.StageOneRejected,
                M01LiveAllocatorCheckpoint.Uninitialized,
                "copied-self-bound-stage-one");
        }
        finally
        {
            NativeMemory.AlignedFree(copiedStorage);
        }
    }

    private static bool TestPointerCountCapacityPreflight()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LiveAllocatorAllocationEvidence allocation =
            fixture.CreateAllocationEvidence();
        M01LiveAllocatorMapLedgerEvidence map =
            fixture.CreateMapEvidence(allocation);

        if (!ExpectFailure(
                fixture.Invoke(
                    null,
                    0,
                    fixture.Records,
                    1,
                    1,
                    allocation,
                    map,
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes),
                M01LiveAllocatorStatus.StageOneRejected,
                M01LiveAllocatorCheckpoint.Uninitialized,
                "null-stage-one")
            || !ExpectFailure(
                fixture.Invoke(
                    (M01LoadedImageStackStaticState*)(
                        (byte*)fixture.StageOneState + 1),
                    M01LoadedImageStackStaticState.RequiredStorageBytes,
                    fixture.Records,
                    1,
                    1,
                    allocation,
                    map,
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes),
                M01LiveAllocatorStatus.StageOneRejected,
                M01LiveAllocatorCheckpoint.Uninitialized,
                "misaligned-stage-one")
            || !ExpectFailure(
                fixture.Invoke(
                    fixture.StageOneState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes
                        - 1,
                    fixture.Records,
                    1,
                    1,
                    allocation,
                    map,
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes),
                M01LiveAllocatorStatus.StageOneRejected,
                M01LiveAllocatorCheckpoint.Uninitialized,
                "short-stage-one")
            || !ExpectFailure(
                fixture.Invoke(
                    (M01LoadedImageStackStaticState*)(~0UL - 7),
                    M01LoadedImageStackStaticState.RequiredStorageBytes,
                    fixture.Records,
                    1,
                    1,
                    allocation,
                    map,
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes),
                M01LiveAllocatorStatus.StageOneRejected,
                M01LiveAllocatorCheckpoint.Uninitialized,
                "wrapped-stage-one")
            || !ExpectFailure(
                fixture.Invoke(
                    fixture.StageOneState,
                    ~0UL,
                    fixture.Records,
                    1,
                    1,
                    allocation,
                    map,
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes),
                M01LiveAllocatorStatus.StageOneRejected,
                M01LiveAllocatorCheckpoint.Uninitialized,
                "wrapped-stage-one-capacity"))
        {
            return false;
        }

        ulong stageOneTailIntoAllocation =
            (ulong)fixture.Records
            - (ulong)fixture.StageOneState
            + (ulong)sizeof(BootMemoryRegionRecord);
        if (!ExpectFailure(
                fixture.Invoke(
                    fixture.StageOneState,
                    stageOneTailIntoAllocation,
                    fixture.Records,
                    1,
                    1,
                    allocation,
                    map,
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes),
                M01LiveAllocatorStatus.CoordinatorStateRejected,
                M01LiveAllocatorCheckpoint.StageOneValidated,
                "stage-one-capacity-tail-allocation-alias"))
        {
            return false;
        }

        (nuint Address, ulong Count, ulong Capacity)
            [] badAllocations =
        [
            (0, 1, 1),
            ((nuint)fixture.Records, 0, 1),
            ((nuint)fixture.Records, 2, 2),
            ((nuint)fixture.Records, 1, 0),
            ((nuint)fixture.Records, 1, 17),
            ((nuint)((byte*)fixture.Records + 1), 1, 1),
            (unchecked((nuint)(~0UL - 7)), 1, 16)
        ];
        foreach (var bad in badAllocations)
        {
            if (!ExpectFailure(
                    fixture.Invoke(
                        fixture.StageOneState,
                        M01LoadedImageStackStaticState
                            .RequiredStorageBytes,
                        (BootMemoryRegionRecord*)bad.Address,
                        bad.Count,
                        bad.Capacity,
                        allocation,
                        map,
                        fixture.Ledger,
                        fixture.LedgerCount,
                        fixture.LedgerCapacity,
                        fixture.StaticState,
                        M01LiveAllocatorStaticState.RequiredStorageBytes),
                    M01LiveAllocatorStatus.AllocationTableRejected,
                    M01LiveAllocatorCheckpoint.StageOneValidated,
                    "allocation-storage"))
            {
                return false;
            }
        }

        (nuint Address, ulong Count, ulong Capacity)
            [] badLedgers =
        [
            (0, 1, 1),
            (
                (nuint)fixture.Ledger,
                0,
                fixture.LedgerCapacity),
            (
                (nuint)fixture.Ledger,
                fixture.LedgerCount,
                1),
            (
                (nuint)fixture.Ledger,
                BootReservationTableValidator.MaximumReservationCount + 1,
                BootReservationTableValidator.MaximumReservationCount + 1),
            (
                (nuint)fixture.Ledger,
                fixture.LedgerCount,
                BootReservationTableValidator.MaximumReservationCount + 1),
            (
                (nuint)((byte*)fixture.Ledger + 1),
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            (
                unchecked((nuint)(~0UL - 7)),
                1,
                BootReservationTableValidator.MaximumReservationCount)
        ];
        foreach (var bad in badLedgers)
        {
            if (!ExpectFailure(
                    fixture.Invoke(
                        fixture.StageOneState,
                        M01LoadedImageStackStaticState
                            .RequiredStorageBytes,
                        fixture.Records,
                        1,
                        1,
                        allocation,
                        map,
                        (BootReservedRange*)bad.Address,
                        bad.Count,
                        bad.Capacity,
                        fixture.StaticState,
                        M01LiveAllocatorStaticState.RequiredStorageBytes),
                    M01LiveAllocatorStatus.ReservationLedgerRejected,
                    M01LiveAllocatorCheckpoint.StageOneValidated,
                    "reservation-storage"))
            {
                return false;
            }
        }

        (nuint Address, ulong Capacity)
            [] badStates =
        [
            (0, 0),
            (
                (nuint)fixture.StaticState,
                M01LiveAllocatorStaticState.RequiredStorageBytes - 1),
            (
                (nuint)((byte*)fixture.StaticState + 1),
                M01LiveAllocatorStaticState.RequiredStorageBytes),
            (
                unchecked((nuint)(~0UL - 7)),
                M01LiveAllocatorStaticState.RequiredStorageBytes)
        ];
        foreach (var bad in badStates)
        {
            if (!ExpectFailure(
                    fixture.Invoke(
                        fixture.StageOneState,
                        M01LoadedImageStackStaticState
                            .RequiredStorageBytes,
                        fixture.Records,
                        1,
                        1,
                        allocation,
                        map,
                        fixture.Ledger,
                        fixture.LedgerCount,
                        fixture.LedgerCapacity,
                        (M01LiveAllocatorStaticState*)bad.Address,
                        bad.Capacity),
                    M01LiveAllocatorStatus.CoordinatorStateRejected,
                    M01LiveAllocatorCheckpoint.StageOneValidated,
                    "state-storage"))
            {
                return false;
            }
        }

        if (!ExpectFailure(
                fixture.Invoke(
                    fixture.StageOneState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes,
                    fixture.Records,
                    1,
                    1,
                    allocation,
                    map,
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    ~0UL),
                M01LiveAllocatorStatus.CoordinatorStateRejected,
                M01LiveAllocatorCheckpoint.StageOneValidated,
                "wrapped-output-capacity"))
        {
            return false;
        }

        ulong outputTailIntoMap =
            fixture.StageTwoMap.BufferAddress
            - (ulong)fixture.StaticState
            + fixture.StageTwoMap.BufferSize;
        return ExpectFailure(
            fixture.Invoke(
                fixture.StageOneState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                fixture.Records,
                1,
                1,
                allocation,
                map,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                outputTailIntoMap),
            M01LiveAllocatorStatus.CoordinatorStateRejected,
            M01LiveAllocatorCheckpoint.StageOneValidated,
            "output-capacity-tail-map-alias");
    }

    private static bool TestAliasPreflight()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        bool Rejects(
            M01LoadedImageStackStaticState* stageOne,
            BootMemoryRegionRecord* records,
            ulong recordCapacity,
            BootReservedRange* ledger,
            ulong ledgerCount,
            ulong ledgerCapacity,
            M01LiveAllocatorStaticState* state,
            FirmwareMemoryMapRecord mapRecord,
            string subcase)
        {
            M01LiveAllocatorAllocationEvidence allocation =
                M01LiveAllocatorAllocationEvidence
                    .CaptureCallerAttested(
                        records,
                        1,
                        recordCapacity,
                        CanonicalFixture.ArenaAllocationOrdinal,
                        M01LiveAllocatorAllocationEvidence.EfiSuccess);
            M01LiveAllocatorMapLedgerEvidence map =
                M01LiveAllocatorMapLedgerEvidence
                    .CaptureCallerAttested(
                        CanonicalFixture.StageOneMapKey,
                        CanonicalFixture.StageOneMapGeneration,
                        mapRecord,
                        CanonicalFixture.StageTwoMapGeneration,
                        allocation,
                        CanonicalFixture.StageTwoMapCaptureOrdinal,
                        CanonicalFixture.StageTwoLedgerBuildOrdinal,
                        ledger,
                        ledgerCount,
                        ledgerCapacity);
            return ExpectFailure(
                fixture.Invoke(
                    stageOne,
                    M01LoadedImageStackStaticState.RequiredStorageBytes,
                    records,
                    1,
                    recordCapacity,
                    allocation,
                    map,
                    ledger,
                    ledgerCount,
                    ledgerCapacity,
                    state,
                    M01LiveAllocatorStaticState.RequiredStorageBytes),
                M01LiveAllocatorStatus.CoordinatorStateRejected,
                M01LiveAllocatorCheckpoint.StageOneValidated,
                subcase);
        }

        if (!Rejects(
                fixture.StageOneState,
                (BootMemoryRegionRecord*)fixture.StageOneState,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                fixture.StageTwoMap,
                "stage-one-allocation")
            || !Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                (BootReservedRange*)fixture.StageOneState,
                fixture.LedgerCount,
                fixture.LedgerCount,
                fixture.StaticState,
                fixture.StageTwoMap,
                "stage-one-ledger")
            || !Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                (M01LiveAllocatorStaticState*)fixture.StageOneState,
                fixture.StageTwoMap,
                "stage-one-state")
            || !Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                (BootReservedRange*)fixture.Records,
                fixture.LedgerCount,
                fixture.LedgerCount,
                fixture.StaticState,
                fixture.StageTwoMap,
                "allocation-ledger")
            || !Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                (M01LiveAllocatorStaticState*)fixture.Records,
                fixture.StageTwoMap,
                "allocation-state")
            || !Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                (M01LiveAllocatorStaticState*)fixture.Ledger,
                fixture.StageTwoMap,
                "ledger-state"))
        {
            return false;
        }

        FirmwareMemoryMapRecord stageOneMapAlias = WithMap(
            fixture.StageTwoMap,
            bufferAddress: (ulong)fixture.StageOneState);
        FirmwareMemoryMapRecord allocationMapAlias = WithMap(
            fixture.StageTwoMap,
            bufferAddress: (ulong)fixture.Records);
        FirmwareMemoryMapRecord ledgerMapAlias = WithMap(
            fixture.StageTwoMap,
            bufferAddress: (ulong)fixture.Ledger);
        FirmwareMemoryMapRecord stateMapAlias = WithMap(
            fixture.StageTwoMap,
            bufferAddress: (ulong)fixture.StaticState);
        if (!Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                stageOneMapAlias,
                "map-stage-one")
            || !Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                allocationMapAlias,
                "map-allocation")
            || !Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                ledgerMapAlias,
                "map-ledger")
            || !Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                stateMapAlias,
                "map-state"))
        {
            return false;
        }

        if (!Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                (M01LiveAllocatorStaticState*)fixture.ArenaStart,
                fixture.StageTwoMap,
                "arena-state"))
        {
            return false;
        }

        *(BootMemoryRegionRecord*)fixture.ArenaStart =
            fixture.Records[0];
        return Rejects(
                fixture.StageOneState,
                (BootMemoryRegionRecord*)fixture.ArenaStart,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                fixture.StageTwoMap,
                "arena-allocation-metadata")
            && Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                WithMap(
                    fixture.StageTwoMap,
                    bufferAddress: fixture.ArenaStart),
                "arena-map-destination")
            && Rejects(
                fixture.StageOneState,
                fixture.Records,
                1,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                WithMap(
                    fixture.StageTwoMap,
                    sourceBufferAddress: fixture.ArenaStart),
                "arena-map-source");
    }

    private static bool TestAllocationEvidenceFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LiveAllocatorAllocationEvidence canonical =
            fixture.CreateAllocationEvidence();
        if (!canonical.IsWellFormed
            || !canonical.IsValidFor(fixture.Records, 1, 1)
            || canonical.Size
                != M01LiveAllocatorAllocationEvidence.StructureSize
            || canonical.Version
                != M01LiveAllocatorAllocationEvidence.CurrentVersion
            || canonical.Provenance
                != M01LiveAllocatorAllocationProvenance
                    .CallerAttestedSuccessfulFirmwarePageAllocation
            || canonical.AllocationTableAddress
                != (ulong)fixture.Records
            || canonical.AllocationRecordCount != 1
            || canonical.AllocationRecordCapacity != 1
            || canonical.AllocationOrdinal
                != CanonicalFixture.ArenaAllocationOrdinal
            || canonical.FirmwareStatus
                != M01LiveAllocatorAllocationEvidence.EfiSuccess)
        {
            return CaseFailure("canonical-allocation-evidence");
        }

        M01LiveAllocatorAllocationEvidence[] malformed =
        [
            default,
            WithUIntAt(canonical, 0, 0),
            WithUIntAt(canonical, 4, 0),
            WithByteAt(canonical, 8, 0),
            WithByteAt(canonical, 9, 1),
            M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(
                null,
                1,
                1,
                CanonicalFixture.ArenaAllocationOrdinal,
                0),
            M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(
                fixture.Records + 1,
                1,
                1,
                CanonicalFixture.ArenaAllocationOrdinal,
                0),
            M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                0,
                1,
                CanonicalFixture.ArenaAllocationOrdinal,
                0),
            M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                1,
                0,
                CanonicalFixture.ArenaAllocationOrdinal,
                0),
            M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                1,
                17,
                CanonicalFixture.ArenaAllocationOrdinal,
                0),
            M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                1,
                1,
                0,
                0),
            M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                1,
                1,
                ~0UL,
                0),
            M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                1,
                1,
                CanonicalFixture.ArenaAllocationOrdinal,
                1)
        ];

        M01LiveAllocatorMapLedgerEvidence map =
            fixture.CreateMapEvidence(canonical);
        foreach (M01LiveAllocatorAllocationEvidence evidence
            in malformed)
        {
            if (!ExpectFailure(
                    fixture.Invoke(
                        fixture.StageOneState,
                        M01LoadedImageStackStaticState
                            .RequiredStorageBytes,
                        fixture.Records,
                        1,
                        1,
                        evidence,
                        map,
                        fixture.Ledger,
                        fixture.LedgerCount,
                        fixture.LedgerCapacity,
                        fixture.StaticState,
                        M01LiveAllocatorStaticState.RequiredStorageBytes),
                    M01LiveAllocatorStatus.AllocationEvidenceRejected,
                    M01LiveAllocatorCheckpoint.StageOneValidated,
                    "allocation-evidence"))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestArenaFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        bool Rejects(
            BootMemoryRegionRecord arena,
            M01LiveAllocatorArenaStatus expected,
            string subcase)
        {
            fixture.ResetCanonical();
            fixture.Records[0] = arena;
            M01LiveAllocatorIntegrationResult result =
                fixture.ExecuteCurrent();
            if (ExpectFailure(
                    result,
                    M01LiveAllocatorStatus.ArenaAllocationRejected,
                    M01LiveAllocatorCheckpoint.StageOneValidated,
                    subcase)
                && result.Evidence.ArenaStatus == expected)
            {
                return true;
            }

            return CaseFailure(
                subcase
                + " arena-status="
                + result.Evidence.ArenaStatus);
        }

        BootMemoryRegionRecord canonical = fixture.Records[0];
        ulong nearEnd = ~0UL - (BootReservedRange.PageSize - 1);
        if (!Rejects(
                WithRecord(canonical, uefiType: 1),
                M01LiveAllocatorArenaStatus.InvalidMemoryType,
                "memory-type")
            || !Rejects(
                WithRecord(
                    canonical,
                    physicalStart: 0),
                M01LiveAllocatorArenaStatus.InvalidPhysicalStart,
                "physical-zero")
            || !Rejects(
                WithRecord(
                    canonical,
                    physicalStart: canonical.PhysicalStart + 1),
                M01LiveAllocatorArenaStatus.MisalignedPhysicalStart,
                "physical-misaligned")
            || !Rejects(
                WithRecord(canonical, virtualStart: 0),
                M01LiveAllocatorArenaStatus.InvalidVirtualStart,
                "virtual-zero")
            || !Rejects(
                WithRecord(
                    canonical,
                    virtualStart: canonical.VirtualStart + 1),
                M01LiveAllocatorArenaStatus.MisalignedVirtualStart,
                "virtual-misaligned")
            || !Rejects(
                WithRecord(
                    canonical,
                    virtualStart:
                        canonical.VirtualStart
                        + BootReservedRange.PageSize),
                M01LiveAllocatorArenaStatus.NonIdentityVirtualStart,
                "nonidentity")
            || !Rejects(
                WithRecord(
                    canonical,
                    pageCount: 1,
                    byteLength: BootReservedRange.PageSize,
                    physicalEndExclusive:
                        canonical.PhysicalStart
                        + BootReservedRange.PageSize),
                M01LiveAllocatorArenaStatus.TooFewPages,
                "too-few")
            || !Rejects(
                WithRecord(
                    canonical,
                    pageCount:
                        M01LiveAllocatorIntegration
                            .MaximumArenaPageCount + 1,
                    byteLength:
                        (M01LiveAllocatorIntegration
                            .MaximumArenaPageCount + 1)
                        * BootReservedRange.PageSize,
                    physicalEndExclusive:
                        canonical.PhysicalStart
                        + (M01LiveAllocatorIntegration
                            .MaximumArenaPageCount + 1)
                        * BootReservedRange.PageSize),
                M01LiveAllocatorArenaStatus.TooManyPages,
                "too-many")
            || !Rejects(
                WithRecord(
                    canonical,
                    byteLength: canonical.ByteLength
                        + BootReservedRange.PageSize),
                M01LiveAllocatorArenaStatus.ByteLengthMismatch,
                "byte-length")
            || !Rejects(
                new BootMemoryRegionRecord(
                    canonical.UefiType,
                    nearEnd,
                    nearEnd,
                    2,
                    canonical.Attribute,
                    2 * BootReservedRange.PageSize,
                    0),
                M01LiveAllocatorArenaStatus.PhysicalEndOverflow,
                "physical-overflow")
            || !Rejects(
                WithRecord(
                    canonical,
                    physicalEndExclusive:
                        canonical.PhysicalEndExclusive
                        + BootReservedRange.PageSize),
                M01LiveAllocatorArenaStatus.PhysicalEndMismatch,
                "physical-end"))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Records[0] = WithRecord(
            fixture.Records[0],
            attribute: 0x8000_0000_0000_0000UL);
        return fixture.ExecuteCurrent().Succeeded;
    }

    private static bool TestMapLedgerEvidenceFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LiveAllocatorAllocationEvidence allocation =
            fixture.CreateAllocationEvidence();
        M01LiveAllocatorMapLedgerEvidence canonical =
            fixture.CreateMapEvidence(allocation);
        if (!canonical.IsWellFormedFor(allocation)
            || !canonical.IsValidFor(
                allocation,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity)
            || canonical.Size
                != M01LiveAllocatorMapLedgerEvidence.StructureSize
            || canonical.Version
                != M01LiveAllocatorMapLedgerEvidence.CurrentVersion
            || canonical.Provenance
                != M01LiveAllocatorMapLedgerProvenance
                    .CallerAttestedPostArenaAllocationOwnedMap
            || canonical.PreviousMapKey
                != CanonicalFixture.StageOneMapKey
            || canonical.PreviousMapGeneration
                != CanonicalFixture.StageOneMapGeneration
            || canonical.RefreshedMapGeneration
                != CanonicalFixture.StageTwoMapGeneration
            || canonical.ArenaAllocationOrdinal
                != CanonicalFixture.ArenaAllocationOrdinal
            || canonical.MapCaptureOrdinal
                != CanonicalFixture.StageTwoMapCaptureOrdinal
            || canonical.LedgerBuildOrdinal
                != CanonicalFixture.StageTwoLedgerBuildOrdinal)
        {
            return CaseFailure("canonical-map-evidence");
        }

        bool Rejects(
            M01LiveAllocatorMapLedgerEvidence evidence,
            string subcase)
        {
            return ExpectFailure(
                fixture.Invoke(
                    fixture.StageOneState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes,
                    fixture.Records,
                    1,
                    1,
                    allocation,
                    evidence,
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes),
                M01LiveAllocatorStatus.MapLedgerEvidenceRejected,
                M01LiveAllocatorCheckpoint.StageOneValidated,
                subcase);
        }

        FirmwareMemoryMapRecord noOwnedFlag =
            WithMap(fixture.StageTwoMap, flags: 0);
        FirmwareMemoryMapRecord overlappingCopy =
            WithMap(
                fixture.StageTwoMap,
                sourceBufferAddress:
                    fixture.StageTwoMap.BufferAddress + 8);
        M01LiveAllocatorAllocationEvidence differentOrdinal =
            M01LiveAllocatorAllocationEvidence
                .CaptureCallerAttested(
                    fixture.Records,
                    1,
                    1,
                    CanonicalFixture.ArenaAllocationOrdinal + 1,
                    0);
        M01LiveAllocatorMapLedgerEvidence[] malformed =
        [
            default,
            WithUIntAt(canonical, 0, 0),
            WithUIntAt(canonical, 4, 0),
            WithByteAt(canonical, 8, 0),
            WithByteAt(canonical, 9, 1),
            fixture.CreateMapEvidence(
                allocation,
                previousMapKey: fixture.StageTwoMap.MapKey),
            fixture.CreateMapEvidence(
                allocation,
                previousMapGeneration: 0),
            fixture.CreateMapEvidence(
                allocation,
                previousMapGeneration: ~0UL,
                refreshedMapGeneration: 0),
            fixture.CreateMapEvidence(
                allocation,
                refreshedMapGeneration:
                    CanonicalFixture.StageTwoMapGeneration + 1),
            fixture.CreateMapEvidence(
                allocation,
                refreshedMap: noOwnedFlag),
            fixture.CreateMapEvidence(
                allocation,
                refreshedMap: overlappingCopy),
            fixture.CreateMapEvidence(
                differentOrdinal),
            fixture.CreateMapEvidence(
                allocation,
                mapCaptureOrdinal:
                    CanonicalFixture.ArenaAllocationOrdinal),
            fixture.CreateMapEvidence(
                allocation,
                ledgerBuildOrdinal:
                    CanonicalFixture.StageTwoMapCaptureOrdinal),
            fixture.CreateMapEvidence(
                allocation,
                ledger: fixture.Ledger + 1),
            fixture.CreateMapEvidence(
                allocation,
                ledgerCount: fixture.LedgerCount - 1),
            fixture.CreateMapEvidence(
                allocation,
                ledgerCapacity: fixture.LedgerCapacity - 1)
        ];
        foreach (M01LiveAllocatorMapLedgerEvidence evidence
            in malformed)
        {
            if (!Rejects(evidence, "map-ledger-evidence"))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestReservationLedgerFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        fixture.RemoveReservationKind(BootReservationKind.AllocatorArena);
        if (!ExpectArenaReservationFailure(
                fixture.ExecuteCurrent(),
                "missing-arena"))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.AddReservation(
            CreateRange(
                BootReservationKind.AllocatorArena,
                fixture.ArenaStart + fixture.ArenaBytes,
                2));
        if (!ExpectArenaReservationFailure(
                fixture.ExecuteCurrent(),
                "duplicate-arena"))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.ReplaceReservationKind(
            BootReservationKind.AllocatorArena,
            CreateRange(
                BootReservationKind.AllocatorArena,
                fixture.ArenaStart + fixture.ArenaBytes,
                fixture.ArenaPages));
        if (!ExpectArenaReservationFailure(
                fixture.ExecuteCurrent(),
                "mismatched-arena"))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.RemoveReservationKind(BootReservationKind.LoadedImage);
        if (!ExpectArenaReservationFailure(
                fixture.ExecuteCurrent(),
                "missing-loaded-image"))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.ReplaceReservationKind(
            BootReservationKind.LoadedImage,
            CreateRange(
                BootReservationKind.LoadedImage,
                fixture.UnrelatedRangeStart,
                CanonicalFixture.StageOneImagePages));
        if (!ExpectArenaReservationFailure(
                fixture.ExecuteCurrent(),
                "unrelated-loaded-image"))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.RemoveReservationKind(
            BootReservationKind.BootStackSwitchStub);
        if (!ExpectArenaReservationFailure(
                fixture.ExecuteCurrent(),
                "missing-stub"))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.SwapLedgerEntries(0, 1);
        M01LiveAllocatorIntegrationResult unsorted =
            fixture.ExecuteCurrent();
        if (!ExpectFailure(
                unsorted,
                M01LiveAllocatorStatus.ReservationLedgerRejected,
                M01LiveAllocatorCheckpoint.MapLedgerValidated,
                "unsorted-ledger")
            || unsorted.Evidence.ReservationValidation.Status
                != BootReservationTableStatus.UnsortedRange)
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Ledger[fixture.ArenaReservationIndex] =
            fixture.Ledger[fixture.ArenaReservationIndex - 1];
        M01LiveAllocatorIntegrationResult duplicate =
            fixture.ExecuteCurrent();
        return ExpectFailure(
                duplicate,
                M01LiveAllocatorStatus.ReservationLedgerRejected,
                M01LiveAllocatorCheckpoint.MapLedgerValidated,
                "duplicate-overlap")
            && (duplicate.Evidence.ReservationValidation.Status
                    == BootReservationTableStatus.DuplicateRange
                || duplicate.Evidence.ReservationValidation.Status
                    == BootReservationTableStatus.OverlappingRange);
    }

    private static bool TestConfinementDuplicateAndStateSafety()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LiveAllocatorIntegrationResult first =
            fixture.ExecuteCurrent();
        if (!first.Succeeded)
        {
            return CaseFailure("first-phase");
        }

        byte[] completedState = fixture.CopyStaticState();
        M01LiveAllocatorIntegrationResult duplicate =
            fixture.ExecuteCurrent();
        if (!ExpectFailure(
                duplicate,
                M01LiveAllocatorStatus.DuplicatePhase,
                M01LiveAllocatorCheckpoint.StageOneValidated,
                "duplicate-phase")
            || !completedState.AsSpan().SequenceEqual(
                fixture.CopyStaticState()))
        {
            return false;
        }

        fixture.ResetCanonical();
        ((byte*)fixture.StaticState)[0] = 1;
        byte[] nondefaultBefore = fixture.CopyStaticState();
        M01LiveAllocatorIntegrationResult nondefault =
            fixture.ExecuteCurrent();
        if (!ExpectFailure(
                nondefault,
                M01LiveAllocatorStatus.CoordinatorStateRejected,
                M01LiveAllocatorCheckpoint.StageOneValidated,
                "nondefault-state")
            || !nondefaultBefore.AsSpan().SequenceEqual(
                fixture.CopyStaticState()))
        {
            return false;
        }

        fixture.ResetCanonical();
        M01LiveAllocatorIntegrationResult success =
            fixture.ExecuteCurrent();
        ulong start = Raw(success.Evidence.Allocation.PhysicalStart);
        ulong end =
            Raw(success.Evidence.Allocation.PhysicalEndExclusive);
        return success.Succeeded
            && start >= fixture.ArenaStart
            && end <= fixture.ArenaStart + fixture.ArenaBytes
            && start != 0
            && end > start
            && success.StaticState.ArenaReservation.Equals(
                fixture.Ledger[fixture.ArenaReservationIndex])
            && success.StaticState.FreeRange.Equals(
                success.StaticState.ArenaSourceRange);
    }

    private static bool TestDeterminismAndNonMutation()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        byte[] stageOneBefore = fixture.CopyStageOneState();
        BootMemoryRegionRecord[] recordsBefore =
            fixture.CopyAllocationRecords();
        BootReservedRange[] ledgerBefore = fixture.CopyLedger();
        M01LiveAllocatorIntegrationResult first =
            fixture.ExecuteCurrent();
        if (!first.Succeeded
            || !stageOneBefore.AsSpan().SequenceEqual(
                fixture.CopyStageOneState())
            || !recordsBefore.AsSpan().SequenceEqual(
                fixture.CopyAllocationRecords())
            || !LedgerEqual(ledgerBefore, fixture.CopyLedger()))
        {
            return CaseFailure("canonical-input-mutation");
        }

        fixture.ResetCanonical();
        M01LiveAllocatorIntegrationResult second =
            fixture.ExecuteCurrent();
        if (!StructBytesEqual(first, second))
        {
            return CaseFailure("canonical-determinism");
        }

        fixture.ResetCanonical();
        byte[] stateBefore = fixture.CopyStaticState();
        stageOneBefore = fixture.CopyStageOneState();
        recordsBefore = fixture.CopyAllocationRecords();
        ledgerBefore = fixture.CopyLedger();
        M01LiveAllocatorIntegrationResult invalidFirst =
            fixture.Invoke(
                fixture.StageOneState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                fixture.Records,
                1,
                0,
                fixture.CreateAllocationEvidence(),
                fixture.CreateMapEvidence(
                    fixture.CreateAllocationEvidence()),
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LiveAllocatorStaticState.RequiredStorageBytes);
        M01LiveAllocatorIntegrationResult invalidSecond =
            fixture.Invoke(
                fixture.StageOneState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                fixture.Records,
                1,
                0,
                fixture.CreateAllocationEvidence(),
                fixture.CreateMapEvidence(
                    fixture.CreateAllocationEvidence()),
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LiveAllocatorStaticState.RequiredStorageBytes);
        if (!StructBytesEqual(invalidFirst, invalidSecond)
            || !ExpectFailure(
                invalidFirst,
                M01LiveAllocatorStatus.AllocationTableRejected,
                M01LiveAllocatorCheckpoint.StageOneValidated,
                "deterministic-failure")
            || !stateBefore.AsSpan().SequenceEqual(
                fixture.CopyStaticState())
            || !stageOneBefore.AsSpan().SequenceEqual(
                fixture.CopyStageOneState())
            || !recordsBefore.AsSpan().SequenceEqual(
                fixture.CopyAllocationRecords())
            || !LedgerEqual(ledgerBefore, fixture.CopyLedger()))
        {
            return false;
        }

        _ = fixture.Invoke(
            fixture.StageOneState,
            M01LoadedImageStackStaticState.RequiredStorageBytes,
            fixture.Records,
            1,
            0,
            fixture.CreateAllocationEvidence(),
            fixture.CreateMapEvidence(
                fixture.CreateAllocationEvidence()),
            fixture.Ledger,
            fixture.LedgerCount,
            fixture.LedgerCapacity,
            fixture.StaticState,
            M01LiveAllocatorStaticState.RequiredStorageBytes);
        long before = GC.GetAllocatedBytesForCurrentThread();
        M01LiveAllocatorIntegrationResult measured =
            fixture.Invoke(
                fixture.StageOneState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                fixture.Records,
                1,
                0,
                fixture.CreateAllocationEvidence(),
                fixture.CreateMapEvidence(
                    fixture.CreateAllocationEvidence()),
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LiveAllocatorStaticState.RequiredStorageBytes);
        long after = GC.GetAllocatedBytesForCurrentThread();
        return after == before
            && measured.Status
                == M01LiveAllocatorStatus.AllocationTableRejected;
    }

    private static bool TestSourceApiAndMarkerFirewalls()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string sourcePath = IntegrationSourcePath(repositoryRoot);
        string source = File.ReadAllText(sourcePath);
        string stripped = RemoveComments(source);
        string[] forbiddenPatterns =
        [
            @"\busing\b",
            @"\bSystem\.",
            @"\bthrow\b",
            @"\btry\b",
            @"\bcatch\b",
            @"\bfinally\b",
            @"\btypeof\b",
            @"\bReflection\b",
            @"\bActivator\b",
            @"\bdynamic\b",
            @"\bdelegate\b",
            @"\binterface\b",
            @"\brecord\s+(class|struct)\b",
            @"\bstring\b",
            @"\bobject\b",
            @"\bSpan\s*<",
            @"\bReadOnlySpan\s*<",
            @"\bList\s*<",
            @"\bDictionary\s*<",
            @"\bstackalloc\b",
            @"\bfixed\b",
            @"\bRuntimeImport\b",
            @"\bRuntimeExport\b",
            @"\bDllImport\b",
            @"\bLibraryImport\b",
            @"\bNativeLibrary\b",
            @"\bUnmanagedCallersOnly\b",
            @"\bMarshal\b",
            @"\bUnsafe\b",
            @"\bNativeMemory\b",
            @"\bEfiEntry\b",
            @"\bBootSequence\b",
            @"\bProbes\b",
            @"\bConsole\b",
            @"\bThread\b",
            @"\bTask\b",
            @"\bGC\.",
            @"\bGetMemoryMap\b",
            @"\bExitBootServices\b",
            @"\bBootServices\b",
            @"\bUefiMemoryType\b",
            @"\bConOut\b",
            @"\bSerialIo\b",
            @"\bOpenProtocol\b",
            @"\bHandleProtocol\b",
            @"\binvlpg\b",
            @"\bCR3\b"
        ];
        foreach (string pattern in forbiddenPatterns)
        {
            if (Regex.IsMatch(
                    stripped,
                    pattern,
                    RegexOptions.CultureInvariant))
            {
                return SourceFailure("forbidden=" + pattern);
            }
        }

        string[] requiredContracts =
        [
            "public const bool CallsFirmwarePageAllocator = false",
            "public const bool InvokesFirmware = false",
            "public const bool TouchesAllocatedPhysicalPage = false",
            "public const bool PerformsPhysicalMemoryAccess = false",
            "public const bool MutatesCallerOwnedAllocatorMetadata = true",
            "public const bool IsGuestEvidence = false",
            "public const bool EstablishesPostFirmwareAllocator = false",
            "public const bool TransfersConventionalMemoryOwnership = false",
            "public const bool ConfiguresPaging = false",
            "public const bool EnforcesNxOrWriteXorExecute = false",
            "public static M01LiveAllocatorIntegrationResult Execute(",
            "ContinuationRspContained",
            "ContinuationObserved",
            "OwnedBootStackLifetime",
            "AllocatorArena",
            "BootPageAllocatorMutator",
            "FreeRangeNotAllocated"
        ];
        foreach (string required in requiredContracts)
        {
            if (!stripped.Contains(required, StringComparison.Ordinal))
            {
                return SourceFailure("missing=" + required);
            }
        }

        int resultValidation = stripped.IndexOf(
            "if (!result.Succeeded)",
            StringComparison.Ordinal);
        int outputCommit = stripped.IndexOf(
            "*staticState = candidate",
            StringComparison.Ordinal);
        if (resultValidation < 0
            || outputCommit < 0
            || resultValidation >= outputCommit)
        {
            return SourceFailure("result-before-output-commit");
        }

        Type[] podTypes =
        [
            typeof(M01LiveAllocatorAllocationEvidence),
            typeof(M01LiveAllocatorMapLedgerEvidence),
            typeof(M01LiveAllocatorStaticState),
            typeof(M01LiveAllocatorEvidence),
            typeof(M01LiveAllocatorIntegrationResult)
        ];
        foreach (Type type in podTypes)
        {
            if (!type.IsValueType)
            {
                return SourceFailure("non-value-type=" + type.Name);
            }

            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType.IsArray
                    || !field.FieldType.IsValueType
                        && !field.FieldType.IsPointer)
                {
                    return SourceFailure(
                        "managed-field="
                        + type.Name
                        + "."
                        + field.Name);
                }
            }
        }

        MethodInfo[] publicMethods =
            typeof(M01LiveAllocatorIntegration).GetMethods(
                BindingFlags.Public
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly);
        (string Name, int ParameterCount)[] expectedMethods =
        [
            ("AuditArenaSource", 5),
            ("AuditMaterialization", 3),
            ("AuditAllocation", 5),
            ("AuditFree", 5),
            ("AuditDoubleFree", 6),
            ("Execute", 12)
        ];
        if (publicMethods.Length != expectedMethods.Length)
        {
            return SourceFailure("public-api-shape");
        }

        foreach ((string name, int parameterCount)
            in expectedMethods)
        {
            MethodInfo? method = Array.Find(
                publicMethods,
                candidate => string.Equals(
                    candidate.Name,
                    name,
                    StringComparison.Ordinal));
            if (method is null
                || method.GetParameters().Length != parameterCount
                || !IsPodOrPointer(method.ReturnType))
            {
                return SourceFailure("public-api=" + name);
            }

            foreach (ParameterInfo parameter
                in method.GetParameters())
            {
                if (!IsPodOrPointer(parameter.ParameterType))
                {
                    return SourceFailure(
                        "managed-parameter="
                        + method.Name
                        + "."
                        + parameter.Name);
                }
            }
        }

        string efiProjectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        string efiProject = File.ReadAllText(efiProjectPath);
        if (!efiProject.Contains(
                "<EnableDefaultCompileItems>false</EnableDefaultCompileItems>",
                StringComparison.Ordinal)
            || !BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Boot\M01LiveAllocatorIntegration.cs",
                @"Boot\M01LiveAllocatorIntegration.cs")
            || !BootSequencePlanTests.HasNoStdlibBootAuthorityBoundary())
        {
            return SourceFailure("efi-project-boundary");
        }

        string testPath = TestSourcePath(repositoryRoot);
        string testSource = File.ReadAllText(testPath);
        string readyMarker = "CLROS_" + "M01_018_READY";
        string readyStatement =
            "Console.WriteLine(\"CLROS_"
            + "M01_018_READY\")";
        if (CountOccurrences(
                testSource,
                readyMarker) != 1
            || CountOccurrences(
                testSource,
                "TEST_START \" + IssueTestName") != 1
            || CountOccurrences(
                testSource,
                "TEST_PASS \" + IssueTestName") != 1
            || CountOccurrences(
                testSource,
                "TEST_FAIL \" + IssueTestName") != 1
            || source.Contains(
                readyMarker,
                StringComparison.Ordinal)
            || source.Contains(
                "TEST_PASS m01.018",
                StringComparison.Ordinal))
        {
            return SourceFailure("marker-ownership");
        }

        string testsRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "OS.HostTests");
        int markerOwners = 0;
        foreach (string candidate in Directory.EnumerateFiles(
            testsRoot,
            "*.cs",
            SearchOption.AllDirectories))
        {
            if (!IsGeneratedOrReferencePath(candidate)
                && File.ReadAllText(candidate).Contains(
                    readyMarker,
                    StringComparison.Ordinal))
            {
                markerOwners++;
            }
        }

        return markerOwners == 1
            && testSource.IndexOf(
                "Console.WriteLine(\"TEST_START \" + IssueTestName)",
                StringComparison.Ordinal)
                < testSource.IndexOf(
                    readyStatement,
                    StringComparison.Ordinal)
            && testSource.IndexOf(
                readyStatement,
                StringComparison.Ordinal)
                < testSource.IndexOf(
                    "Console.WriteLine(\"TEST_PASS \" + IssueTestName)",
                    StringComparison.Ordinal);
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();

        try
        {
            NoStdLibBuildResult result =
                RunNoStdLibBuild(workspace);
            string outputAssembly = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.LiveAllocatorCompileClosure.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || ContainsInfrastructureFailure(result.Output)
                || !HasNoExternalMetadata(outputAssembly)
                || !HasExactNoStdLibIntegrationSurface(
                    outputAssembly))
            {
                Console.WriteLine(
                    "M01_018_NOSTDLIB_COMPILE_DIAGNOSTIC "
                    + NormalizeCompilerOutput(result.Output));
                return false;
            }

            return true;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static bool EnumContractMatches<TEnum>(
        (string Name, byte Value)[] expected)
        where TEnum : struct, Enum
    {
        string[] names = Enum.GetNames<TEnum>();
        TEnum[] values = Enum.GetValues<TEnum>();
        if (names.Length != expected.Length
            || values.Length != expected.Length
            || Unsafe.SizeOf<TEnum>() != 1)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    names[index],
                    expected[index].Name,
                    StringComparison.Ordinal)
                || Convert.ToByte(values[index])
                    != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ExpectFailure(
        M01LiveAllocatorIntegrationResult result,
        M01LiveAllocatorStatus status,
        M01LiveAllocatorCheckpoint checkpoint,
        string subcase)
    {
        if (result.Status == status
            && result.LastCompletedCheckpoint == checkpoint
            && !result.Succeeded
            && !result.StaticState.IsCompleted)
        {
            return true;
        }

        return CaseFailure(
            subcase
            + " status="
            + result.Status
            + " checkpoint="
            + result.LastCompletedCheckpoint);
    }

    private static bool ExpectArenaReservationFailure(
        M01LiveAllocatorIntegrationResult result,
        string subcase)
    {
        return ExpectFailure(
            result,
            M01LiveAllocatorStatus.ArenaReservationRejected,
            M01LiveAllocatorCheckpoint.MapLedgerValidated,
            subcase);
    }

    private static BootMemoryRegionRecord WithRecord(
        BootMemoryRegionRecord record,
        uint? uefiType = null,
        ulong? physicalStart = null,
        ulong? virtualStart = null,
        ulong? pageCount = null,
        ulong? attribute = null,
        ulong? byteLength = null,
        ulong? physicalEndExclusive = null)
    {
        return new BootMemoryRegionRecord(
            uefiType ?? record.UefiType,
            physicalStart ?? record.PhysicalStart,
            virtualStart ?? record.VirtualStart,
            pageCount ?? record.PageCount,
            attribute ?? record.Attribute,
            byteLength ?? record.ByteLength,
            physicalEndExclusive ?? record.PhysicalEndExclusive);
    }

    private static FirmwareMemoryMapRecord WithMap(
        FirmwareMemoryMapRecord record,
        ulong? bufferAddress = null,
        ulong? bufferSize = null,
        ulong? sourceBufferAddress = null,
        ulong? sourceBufferSize = null,
        ulong? mapSize = null,
        ulong? mapKey = null,
        ulong? descriptorSize = null,
        uint? descriptorVersion = null,
        ulong? descriptorCount = null,
        uint? flags = null)
    {
        record.BufferAddress = bufferAddress ?? record.BufferAddress;
        record.BufferSize = bufferSize ?? record.BufferSize;
        record.SourceBufferAddress =
            sourceBufferAddress ?? record.SourceBufferAddress;
        record.SourceBufferSize =
            sourceBufferSize ?? record.SourceBufferSize;
        record.MapSize = mapSize ?? record.MapSize;
        record.MapKey = mapKey ?? record.MapKey;
        record.DescriptorSize =
            descriptorSize ?? record.DescriptorSize;
        record.DescriptorVersion =
            descriptorVersion ?? record.DescriptorVersion;
        record.DescriptorCount =
            descriptorCount ?? record.DescriptorCount;
        record.Flags = flags ?? record.Flags;
        return record;
    }

    private static BootReservedRange CreateRange(
        BootReservationKind kind,
        ulong physicalStart,
        ulong pages)
    {
        if (PhysicalAddress.TryCreateFromUefiBoundary(
                physicalStart,
                out PhysicalAddress address)
                != AddressValueStatus.Success
            || BootReservedRange.TryCreate(
                kind,
                address,
                pages,
                out BootReservedRange range)
                != BootReservedRangeStatus.Success)
        {
            throw new InvalidOperationException(
                "Failed to create an M01-018 test reservation.");
        }

        return range;
    }

    private static VirtualAddress Virtual(ulong raw)
    {
        return VirtualAddress.TryCreateFromUefiBoundary(
                raw,
                out VirtualAddress address)
                == AddressValueStatus.Success
            ? address
            : default;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        return address.TryGetRawValueForUefiBoundary(out ulong raw)
                == AddressValueStatus.Success
            ? raw
            : 0;
    }

    private static T WithUIntAt<T>(
        T value,
        int offset,
        uint replacement)
        where T : unmanaged
    {
        if (offset < 0 || offset > sizeof(T) - sizeof(uint))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        *(uint*)((byte*)&value + offset) = replacement;
        return value;
    }

    private static T WithUlongAt<T>(
        T value,
        int offset,
        ulong replacement)
        where T : unmanaged
    {
        if (offset < 0 || offset > sizeof(T) - sizeof(ulong))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        *(ulong*)((byte*)&value + offset) = replacement;
        return value;
    }

    private static T WithByteAt<T>(
        T value,
        int offset,
        byte replacement)
        where T : unmanaged
    {
        if (offset < 0 || offset >= sizeof(T))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        *((byte*)&value + offset) = replacement;
        return value;
    }

    private static bool StructBytesEqual<T>(T left, T right)
        where T : unmanaged
    {
        byte* leftBytes = (byte*)&left;
        byte* rightBytes = (byte*)&right;
        for (int index = 0; index < sizeof(T); index++)
        {
            if (leftBytes[index] != rightBytes[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool StateEqual(
        BootPageAllocatorState left,
        BootPageAllocatorState right)
    {
        return left.IsInitialized == right.IsInitialized
            && left.PageSize == right.PageSize
            && left.HeadNodeIndex == right.HeadNodeIndex
            && left.FreeNodeCount == right.FreeNodeCount
            && left.FreePageCount == right.FreePageCount
            && left.FreeByteCount == right.FreeByteCount
            && left.AllocatedPageCount == right.AllocatedPageCount
            && left.AllocatedByteCount == right.AllocatedByteCount;
    }

    private static bool LedgerEqual(
        BootReservedRange[] left,
        BootReservedRange[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPodOrPointer(Type type)
    {
        return type == typeof(void)
            || type.IsPointer
            || type.IsValueType;
    }

    private static bool SourceFailure(string reason)
    {
        Console.WriteLine(
            "M01_018_SOURCE_FIREWALL_DIAGNOSTIC " + reason);
        return false;
    }

    private static bool CaseFailure(string reason)
    {
        Console.WriteLine(
            "M01_018_CASE_DIAGNOSTIC " + reason);
        return false;
    }

    private static string RemoveComments(string source)
    {
        string withoutBlocks = Regex.Replace(
            source,
            @"/\*.*?\*/",
            string.Empty,
            RegexOptions.Singleline
                | RegexOptions.CultureInvariant);
        return Regex.Replace(
            withoutBlocks,
            @"//.*?$",
            string.Empty,
            RegexOptions.Multiline
                | RegexOptions.CultureInvariant);
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(
            value,
            offset,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static bool IsGeneratedOrReferencePath(string fullPath)
    {
        string normalized = fullPath.Replace('\\', '/');
        return normalized.Contains(
                "/bin/",
                StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(
                "/obj/",
                StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(
                "/artifacts/",
                StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(
                "/references/",
                StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(
                "/.git/",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string IntegrationSourcePath(string root)
    {
        return Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Boot",
            "M01LiveAllocatorIntegration.cs");
    }

    private static string TestSourcePath(string root)
    {
        return Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "M01LiveAllocatorIntegrationTests.cs");
    }

    private enum StageOneCompletion : byte
    {
        PreparedOnly = 1,
        TransferRecorded = 2,
        Final = 3
    }

    private sealed class CanonicalFixture : IDisposable
    {
        public const ulong StageOneImagePages = 3;
        public const ulong StageOneImageBytes =
            StageOneImagePages * BootReservedRange.PageSize;
        public const ulong StageOneMapKey = 0x0010;
        public const ulong StageOneMapGeneration = 8;
        public const ulong ArenaAllocationOrdinal = 5;
        public const ulong StageTwoMapGeneration = 9;
        public const ulong StageTwoMapCaptureOrdinal = 6;
        public const ulong StageTwoLedgerBuildOrdinal = 7;

        private const ulong BackingPages = 48;
        private const ulong BackingBytes =
            BackingPages * BootReservedRange.PageSize;
        private const ulong StageOnePreviousMapKey = 0xf000;
        private const ulong StageOnePreviousMapGeneration = 7;
        private const ulong StageOneCodeAllocationOrdinal = 1;
        private const ulong StageOneDataAllocationOrdinal = 2;
        private const ulong StageOneMapCaptureOrdinal = 3;
        private const ulong StageOneLedgerBuildOrdinal = 4;
        private const ulong StageTwoMapKey = 0x0020;
        private const ulong GuardPages = 1;
        private const ulong StackPages = 3;
        private const ulong StageOneDataPages =
            GuardPages + StackPages;
        private const ulong StubEntryOffset = 16;
        private const ulong StageOneStaticStateOffset = 128;
        private const ulong ContinuationOffset =
            BootReservedRange.PageSize + 256;
        private const ulong CodePageOffset =
            4 * BootReservedRange.PageSize;
        private const ulong DataArenaOffset =
            6 * BootReservedRange.PageSize;
        private const ulong LiveArenaOffset =
            12 * BootReservedRange.PageSize;
        private const ulong StageOneAllocationTableOffset =
            18 * BootReservedRange.PageSize;
        private const ulong StageOneLedgerOffset =
            19 * BootReservedRange.PageSize;
        private const ulong StageTwoAllocationTableOffset =
            21 * BootReservedRange.PageSize;
        private const ulong StageTwoLedgerOffset =
            23 * BootReservedRange.PageSize;
        private const ulong StageTwoStaticStateOffset =
            25 * BootReservedRange.PageSize;
        private const ulong StageTwoMapDestinationOffset =
            30 * BootReservedRange.PageSize;
        private const ulong StageTwoMapSourceOffset =
            31 * BootReservedRange.PageSize;
        private const ulong StageOneMapSourceOffset =
            40 * BootReservedRange.PageSize;
        private const ulong StageOneLedgerCapacity = 8;
        private const ulong StageTwoLedgerCapacity = 8;

        private byte* _backing;
        private BootMemoryRegionRecord* _stageOneRecords;
        private BootReservedRange* _stageOneLedger;
        private byte* _codePage;
        private byte* _dataArena;
        private ulong _ledgerCount;

        public CanonicalFixture()
        {
            try
            {
                _backing = (byte*)NativeMemory.AlignedAlloc(
                    (nuint)BackingBytes,
                    (nuint)PageSize);
                if (_backing == null)
                {
                    throw new OutOfMemoryException(
                        "Unable to allocate the M01-018 fixture.");
                }

                _codePage = _backing + CodePageOffset;
                _dataArena = _backing + DataArenaOffset;
                _stageOneRecords = (BootMemoryRegionRecord*)(
                    _backing + StageOneAllocationTableOffset);
                _stageOneLedger = (BootReservedRange*)(
                    _backing + StageOneLedgerOffset);
                Records = (BootMemoryRegionRecord*)(
                    _backing + StageTwoAllocationTableOffset);
                Ledger = (BootReservedRange*)(
                    _backing + StageTwoLedgerOffset);
                StageOneState =
                    (M01LoadedImageStackStaticState*)(
                        _backing + StageOneStaticStateOffset);
                StaticState = (M01LiveAllocatorStaticState*)(
                    _backing + StageTwoStaticStateOffset);
                ResetCanonical();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public BootMemoryRegionRecord* Records { get; private set; }

        public BootReservedRange* Ledger { get; private set; }

        public M01LoadedImageStackStaticState* StageOneState
        {
            get;
            private set;
        }

        public M01LiveAllocatorStaticState* StaticState
        {
            get;
            private set;
        }

        public ulong ArenaPages => 4;

        public ulong ArenaBytes =>
            ArenaPages * BootReservedRange.PageSize;

        public ulong ArenaStart =>
            (ulong)(_backing + LiveArenaOffset);

        public ulong UnrelatedRangeStart =>
            (ulong)(_backing + 34 * BootReservedRange.PageSize);

        public ulong LedgerCount => _ledgerCount;

        public ulong LedgerCapacity => StageTwoLedgerCapacity;

        public ulong ArenaReservationIndex
        {
            get
            {
                for (ulong index = 0; index < _ledgerCount; index++)
                {
                    if (Ledger[index].Kind
                        == BootReservationKind.AllocatorArena)
                    {
                        return index;
                    }
                }

                return M01LiveAllocatorIntegration.NoIndex;
            }
        }

        public bool HasFinalStageOne =>
            StageOneState->IsInitialized
            && StageOneState->Status
                == M01LoadedImageStackStatus.Success
            && StageOneState->Checkpoint
                == M01LoadedImageStackCheckpoint
                    .ContinuationRspContained
            && StageOneState->SwitchState.State
                == OwnedBootStackSwitchState.ContinuationObserved
            && StageOneState->StackLayout.Lifetime
                == OwnedBootStackLifetime.Active;

        public FirmwareMemoryMapRecord StageTwoMap
        {
            get;
            private set;
        }

        public void ResetCanonical(
            StageOneCompletion completion = StageOneCompletion.Final)
        {
            Clear(_backing, BackingBytes);

            EfiLoadedImageProtocolObservation observation =
                EfiLoadedImageProtocolObservation
                    .CaptureFromUefiBoundary(
                        0,
                        1,
                        (ulong)_backing,
                        (ulong)_backing,
                        StageOneImageBytes);
            EfiLoadedImageRangeCapturePlan loadedImage =
                EfiLoadedImageRangeCapturePlan.Create(
                    &observation,
                    1,
                    1,
                    EfiLoadedImageRange.PageSize);
            if (!loadedImage.Succeeded)
            {
                throw new InvalidOperationException(
                    "Canonical loaded-image capture failed.");
            }

            ulong codeStart = (ulong)_codePage;
            ulong dataStart = (ulong)_dataArena;
            _stageOneRecords[0] = new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderCode,
                codeStart,
                codeStart,
                1,
                0,
                BootMemoryRegionRecord.PageSize,
                codeStart + BootMemoryRegionRecord.PageSize);
            _stageOneRecords[1] = new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderData,
                dataStart,
                dataStart,
                StageOneDataPages,
                0,
                StageOneDataPages
                    * BootMemoryRegionRecord.PageSize,
                dataStart
                    + StageOneDataPages
                    * BootMemoryRegionRecord.PageSize);

            _stageOneLedger[0] = loadedImage.Range.Reservation;
            _stageOneLedger[1] = CreateRange(
                BootReservationKind.BootStackSwitchStub,
                codeStart,
                1);
            _stageOneLedger[2] = CreateRange(
                BootReservationKind.BootStackGuard,
                dataStart,
                GuardPages);
            _stageOneLedger[3] = CreateRange(
                BootReservationKind.BootStack,
                dataStart
                    + GuardPages * BootReservedRange.PageSize,
                StackPages);

            FirmwareMemoryMapRecord stageOneMap =
                new FirmwareMemoryMapRecord
                {
                    BufferAddress =
                        (ulong)_backing
                        + 2 * BootReservedRange.PageSize,
                    BufferSize = 80,
                    SourceBufferAddress =
                        (ulong)(_backing + StageOneMapSourceOffset),
                    SourceBufferSize = 80,
                    MapSize = 80,
                    MapKey = StageOneMapKey,
                    DescriptorSize = 40,
                    DescriptorVersion = 1,
                    DescriptorCount = 2,
                    Flags = FirmwareMemoryMapRecord.FlagOwnedCopy
                };
            M01StageOneAllocationEvidence stageOneAllocation =
                M01StageOneAllocationEvidence
                    .CaptureCallerAttested(
                        _stageOneRecords,
                        2,
                        2,
                        StageOneCodeAllocationOrdinal,
                        StageOneDataAllocationOrdinal,
                        M01StageOneAllocationEvidence.EfiSuccess,
                        M01StageOneAllocationEvidence.EfiSuccess);
            M01StageOneMapLedgerEvidence stageOneMapLedger =
                M01StageOneMapLedgerEvidence
                    .CaptureCallerAttested(
                        StageOnePreviousMapKey,
                        StageOnePreviousMapGeneration,
                        stageOneMap,
                        StageOneMapGeneration,
                        stageOneAllocation,
                        StageOneMapCaptureOrdinal,
                        StageOneLedgerBuildOrdinal,
                        _stageOneLedger,
                        4,
                        StageOneLedgerCapacity);
            M01LoadedImageStackIntegrationResult prepared =
                M01LoadedImageStackIntegration
                    .PrepareWithStubByteMode(
                        loadedImage,
                        M01LoadedImageVirtualMappingEvidence
                            .CaptureCallerAttestedIdentity(
                                loadedImage),
                        M01StageOneAddressMappingEvidence
                            .CaptureCallerAttestedIdentity(
                                _stageOneRecords,
                                2,
                                2,
                                stageOneMap),
                        _stageOneRecords,
                        2,
                        2,
                        stageOneAllocation,
                        stageOneMapLedger,
                        _stageOneLedger,
                        4,
                        StageOneLedgerCapacity,
                        _codePage,
                        BootReservedRange.PageSize,
                        BootReservedRange.PageSize,
                        StubEntryOffset,
                        OwnedBootStackSwitchPlan
                            .RequiredStubByteLength,
                        Virtual(
                            (ulong)_backing
                            + ContinuationOffset),
                        StageOneState,
                        M01LoadedImageStackStaticState
                            .RequiredStorageBytes,
                        M01StageOneStubByteMode.EmitAndVerify);
            if (!prepared.IsPrepared)
            {
                throw new InvalidOperationException(
                    "Canonical stage-one prepare failed: "
                    + prepared.Status);
            }

            if (completion >= StageOneCompletion.TransferRecorded)
            {
                M01LoadedImageStackStaticTransitionResult transfer =
                    M01LoadedImageStackIntegration
                        .RecordTransferFromStaticState(
                            StageOneState,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes);
                if (!transfer.TransferRecorded)
                {
                    throw new InvalidOperationException(
                        "Canonical static transfer failed: "
                        + transfer.Status);
                }

                if (completion == StageOneCompletion.Final)
                {
                    M01LoadedImageStackStaticTransitionResult observed =
                        M01LoadedImageStackIntegration
                            .ObserveContinuationRspFromStaticState(
                                StageOneState,
                                M01LoadedImageStackStaticState
                                    .RequiredStorageBytes,
                                transfer.StateResult.Plan
                                    .ExpectedContinuationStackPointer);
                    if (!observed.ContinuationRspContained)
                    {
                        throw new InvalidOperationException(
                            "Canonical continuation failed: "
                            + observed.Status);
                    }
                }
            }

            Records[0] = new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderData,
                ArenaStart,
                ArenaStart,
                ArenaPages,
                0,
                ArenaBytes,
                ArenaStart + ArenaBytes);

            BootReservedRange[] ranges =
            [
                loadedImage.Range.Reservation,
                _stageOneLedger[1],
                _stageOneLedger[2],
                _stageOneLedger[3],
                CreateRange(
                    BootReservationKind.AllocatorArena,
                    ArenaStart,
                    ArenaPages)
            ];
            WriteSortedLedger(ranges);

            StageTwoMap = new FirmwareMemoryMapRecord
            {
                BufferAddress =
                    (ulong)(_backing + StageTwoMapDestinationOffset),
                BufferSize = 80,
                SourceBufferAddress =
                    (ulong)(_backing + StageTwoMapSourceOffset),
                SourceBufferSize = 80,
                MapSize = 80,
                MapKey = StageTwoMapKey,
                DescriptorSize = 40,
                DescriptorVersion = 1,
                DescriptorCount = 2,
                Flags = FirmwareMemoryMapRecord.FlagOwnedCopy
            };
        }

        public void ClearStageOneState()
        {
            Clear(
                (byte*)StageOneState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public M01LiveAllocatorAllocationEvidence
            CreateAllocationEvidence()
        {
            return M01LiveAllocatorAllocationEvidence
                .CaptureCallerAttested(
                    Records,
                    1,
                    1,
                    ArenaAllocationOrdinal,
                    M01LiveAllocatorAllocationEvidence.EfiSuccess);
        }

        public M01LiveAllocatorMapLedgerEvidence CreateMapEvidence(
            M01LiveAllocatorAllocationEvidence allocation,
            ulong previousMapKey = StageOneMapKey,
            ulong previousMapGeneration = StageOneMapGeneration,
            FirmwareMemoryMapRecord? refreshedMap = null,
            ulong refreshedMapGeneration = StageTwoMapGeneration,
            ulong mapCaptureOrdinal = StageTwoMapCaptureOrdinal,
            ulong ledgerBuildOrdinal = StageTwoLedgerBuildOrdinal,
            BootReservedRange* ledger = null,
            ulong? ledgerCount = null,
            ulong? ledgerCapacity = null)
        {
            BootReservedRange* selectedLedger =
                ledger == null ? Ledger : ledger;
            return M01LiveAllocatorMapLedgerEvidence
                .CaptureCallerAttested(
                    previousMapKey,
                    previousMapGeneration,
                    refreshedMap ?? StageTwoMap,
                    refreshedMapGeneration,
                    allocation,
                    mapCaptureOrdinal,
                    ledgerBuildOrdinal,
                    selectedLedger,
                    ledgerCount ?? LedgerCount,
                    ledgerCapacity ?? LedgerCapacity);
        }

        public M01LiveAllocatorIntegrationResult ExecuteCurrent()
        {
            M01LiveAllocatorAllocationEvidence allocation =
                CreateAllocationEvidence();
            return Invoke(
                StageOneState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                Records,
                1,
                1,
                allocation,
                CreateMapEvidence(allocation),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                StaticState,
                M01LiveAllocatorStaticState.RequiredStorageBytes);
        }

        public M01LiveAllocatorIntegrationResult Invoke(
            M01LoadedImageStackStaticState* stageOne,
            ulong stageOneCapacity,
            BootMemoryRegionRecord* records,
            ulong recordCount,
            ulong recordCapacity,
            M01LiveAllocatorAllocationEvidence allocation,
            M01LiveAllocatorMapLedgerEvidence map,
            BootReservedRange* ledger,
            ulong ledgerCount,
            ulong ledgerCapacity,
            M01LiveAllocatorStaticState* state,
            ulong stateCapacity)
        {
            return M01LiveAllocatorIntegration.Execute(
                stageOne,
                stageOneCapacity,
                records,
                recordCount,
                recordCapacity,
                allocation,
                map,
                ledger,
                ledgerCount,
                ledgerCapacity,
                state,
                stateCapacity);
        }

        public void AddReservation(BootReservedRange reservation)
        {
            BootReservedRange[] ranges =
                new BootReservedRange[checked((int)_ledgerCount + 1)];
            for (int index = 0; index < (int)_ledgerCount; index++)
            {
                ranges[index] = Ledger[index];
            }

            ranges[^1] = reservation;
            WriteSortedLedger(ranges);
        }

        public void RemoveReservationKind(BootReservationKind kind)
        {
            BootReservedRange[] ranges =
                new BootReservedRange[checked((int)_ledgerCount - 1)];
            int destination = 0;
            bool removed = false;
            for (int index = 0; index < (int)_ledgerCount; index++)
            {
                if (!removed && Ledger[index].Kind == kind)
                {
                    removed = true;
                    continue;
                }

                if (destination < ranges.Length)
                {
                    ranges[destination++] = Ledger[index];
                }
            }

            if (!removed || destination != ranges.Length)
            {
                throw new InvalidOperationException(
                    "Reservation kind was not present exactly once.");
            }

            WriteSortedLedger(ranges);
        }

        public void ReplaceReservationKind(
            BootReservationKind kind,
            BootReservedRange replacement)
        {
            BootReservedRange[] ranges = CopyLedger();
            bool replaced = false;
            for (int index = 0; index < ranges.Length; index++)
            {
                if (!replaced && ranges[index].Kind == kind)
                {
                    ranges[index] = replacement;
                    replaced = true;
                }
            }

            if (!replaced)
            {
                throw new InvalidOperationException(
                    "Reservation kind was not present.");
            }

            WriteSortedLedger(ranges);
        }

        public void SwapLedgerEntries(int first, int second)
        {
            (Ledger[first], Ledger[second]) =
                (Ledger[second], Ledger[first]);
        }

        public byte[] CopyStageOneState()
        {
            return CopyBytes(
                (byte*)StageOneState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public byte[] CopyStaticState()
        {
            return CopyBytes(
                (byte*)StaticState,
                M01LiveAllocatorStaticState.RequiredStorageBytes);
        }

        public BootMemoryRegionRecord[] CopyAllocationRecords()
        {
            return [Records[0]];
        }

        public BootReservedRange[] CopyLedger()
        {
            BootReservedRange[] copy =
                new BootReservedRange[(int)_ledgerCount];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = Ledger[index];
            }

            return copy;
        }

        public void Dispose()
        {
            if (_backing != null)
            {
                NativeMemory.AlignedFree(_backing);
                _backing = null;
            }

            _stageOneRecords = null;
            _stageOneLedger = null;
            _codePage = null;
            _dataArena = null;
            Records = null;
            Ledger = null;
            StageOneState = null;
            StaticState = null;
        }

        private void WriteSortedLedger(
            BootReservedRange[] ranges)
        {
            if ((ulong)ranges.Length > StageTwoLedgerCapacity)
            {
                throw new InvalidOperationException(
                    "M01-018 ledger capacity exceeded.");
            }

            Array.Sort(
                ranges,
                static (left, right) =>
                {
                    left.PhysicalStart.TryGetRawValueForUefiBoundary(
                        out ulong leftStart);
                    right.PhysicalStart
                        .TryGetRawValueForUefiBoundary(
                            out ulong rightStart);
                    return leftStart.CompareTo(rightStart);
                });
            Clear(
                (byte*)Ledger,
                StageTwoLedgerCapacity
                    * (ulong)sizeof(BootReservedRange));
            for (int index = 0; index < ranges.Length; index++)
            {
                Ledger[index] = ranges[index];
            }

            _ledgerCount = (ulong)ranges.Length;
        }

        private static void Clear(byte* start, ulong length)
        {
            for (ulong index = 0; index < length; index++)
            {
                start[index] = 0;
            }
        }

        private static byte[] CopyBytes(byte* start, ulong length)
        {
            byte[] copy = new byte[checked((int)length)];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = start[index];
            }

            return copy;
        }
    }

    private sealed class TestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = [];

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool Contains(string value)
        {
            return _lines.Contains(value);
        }
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-018-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            string globalJson = Path.Combine(
                repositoryRoot,
                "global.json");
            if (File.Exists(globalJson))
            {
                File.Copy(
                    globalJson,
                    Path.Combine(workspace, "global.json"));
            }

            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibShim.cs"),
                """
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType : Object { }
                    public abstract class Enum : ValueType { }
                    public abstract class Delegate : Object { }
                    public abstract class MulticastDelegate : Delegate { }
                    public class Attribute : Object { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Byte { }
                    public struct Int32 { }
                    public struct UInt32 { }
                    public struct UInt64 { }
                    public struct IntPtr { }
                    public struct UIntPtr { }
                    public enum AttributeTargets { }
                    public sealed class AttributeUsageAttribute : Attribute
                    {
                        public AttributeUsageAttribute(AttributeTargets validOn) { }
                        public bool AllowMultiple { get; set; }
                        public bool Inherited { get; set; }
                    }
                }

                namespace System.Runtime.CompilerServices
                {
                    public sealed class IsReadOnlyAttribute
                        : System.Attribute { }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                using OS.Boot;
                using OS.Kernel.Memory;

                internal static unsafe class M01LiveAllocatorProbe
                {
                    public static M01LiveAllocatorStatus Exercise(
                        M01LoadedImageStackStaticState* stageOne,
                        BootMemoryRegionRecord* records,
                        BootReservedRange* ledger,
                        M01LiveAllocatorStaticState* state)
                    {
                        M01LiveAllocatorAllocationEvidence allocation =
                            M01LiveAllocatorAllocationEvidence
                                .CaptureCallerAttested(
                                    records,
                                    1UL,
                                    1UL,
                                    5UL,
                                    M01LiveAllocatorAllocationEvidence
                                        .EfiSuccess);
                        FirmwareMemoryMapRecord map = default;
                        map.BufferAddress = 0xA000UL;
                        map.BufferSize = 80UL;
                        map.SourceBufferAddress = 0xB000UL;
                        map.SourceBufferSize = 80UL;
                        map.MapSize = 80UL;
                        map.MapKey = 2UL;
                        map.DescriptorSize = 40UL;
                        map.DescriptorVersion = 1U;
                        map.DescriptorCount = 2UL;
                        map.Flags =
                            FirmwareMemoryMapRecord.FlagOwnedCopy;
                        M01LiveAllocatorMapLedgerEvidence mapLedger =
                            M01LiveAllocatorMapLedgerEvidence
                                .CaptureCallerAttested(
                                    1UL,
                                    1UL,
                                    map,
                                    2UL,
                                    allocation,
                                    6UL,
                                    7UL,
                                    ledger,
                                    5UL,
                                    5UL);
                        return M01LiveAllocatorIntegration.Execute(
                            stageOne,
                            M01LoadedImageStackStaticState
                                .RequiredStorageBytes,
                            records,
                            1UL,
                            1UL,
                            allocation,
                            mapLedger,
                            ledger,
                            5UL,
                            5UL,
                            state,
                            M01LiveAllocatorStaticState
                                .RequiredStorageBytes).Status;
                    }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            (string Relative, string Link)[] sources =
            [
                (
                    "src/OS.Kernel/Boot/"
                        + "TypedPhysicalVirtualDeviceAddressPODWrappers.cs",
                    "Boot/TypedAddresses.cs"),
                (
                    "src/OS.Kernel/Memory/BootReservedRange.cs",
                    "Memory/BootReservedRange.cs"),
                (
                    "src/OS.Kernel/Memory/"
                        + "BootReservationTableValidator.cs",
                    "Memory/BootReservationTableValidator.cs"),
                (
                    "src/OS.Kernel/Boot/EfiLoadedImageRange.cs",
                    "Boot/EfiLoadedImageRange.cs"),
                (
                    "src/OS.Kernel/Boot/"
                        + "EfiLoadedImageRangeCapturePlan.cs",
                    "Boot/EfiLoadedImageRangeCapturePlan.cs"),
                (
                    "src/OS.Kernel/Boot/BootHandoffRecord.cs",
                    "Boot/BootHandoffRecord.cs"),
                (
                    "src/OS.Kernel/Boot/OwnedBootStackLayout.cs",
                    "Boot/OwnedBootStackLayout.cs"),
                (
                    "src/OS.Kernel/Boot/UefiMemoryType.cs",
                    "Boot/UefiMemoryType.cs"),
                (
                    "src/OS.Kernel/Boot/"
                        + "BootMemoryRegionMaterializer.cs",
                    "Boot/BootMemoryRegionMaterializer.cs"),
                (
                    "src/OS.Kernel/Boot/"
                        + "OwnedBootStackSwitchPlan.cs",
                    "Boot/OwnedBootStackSwitchPlan.cs"),
                (
                    "src/OS.Kernel/Boot/"
                        + "M01LoadedImageStackIntegration.cs",
                    "Boot/M01LoadedImageStackIntegration.cs"),
                (
                    "src/OS.Kernel/Memory/"
                        + "BootReservationSubtractor.cs",
                    "Memory/BootReservationSubtractor.cs"),
                (
                    "src/OS.Kernel/Memory/"
                        + "BootPageAllocatorCoreContracts.cs",
                    "Memory/BootPageAllocatorCoreContracts.cs"),
                (
                    "src/OS.Kernel/Memory/"
                        + "BootPageAllocatorStateMaterializer.cs",
                    "Memory/BootPageAllocatorStateMaterializer.cs"),
                (
                    "src/OS.Kernel/Memory/"
                        + "BootPageAllocatorMutation.cs",
                    "Memory/BootPageAllocatorMutation.cs"),
                (
                    "src/OS.Kernel/Boot/"
                        + "M01LiveAllocatorIntegration.cs",
                    "Boot/M01LiveAllocatorIntegration.cs")
            ];
            StringBuilder compileItems = new StringBuilder();
            foreach ((string relative, string link) in sources)
            {
                compileItems.Append(
                    "    <Compile Include=\"");
                compileItems.Append(EscapeXml(Path.Combine(
                    repositoryRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar))));
                compileItems.Append("\" Link=\"");
                compileItems.Append(EscapeXml(link));
                compileItems.AppendLine("\" />");
            }

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.LiveAllocatorCompileClosure</AssemblyName>
                <TargetFramework>net10.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <ImplicitUsings>disable</ImplicitUsings>
                <Nullable>disable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <NoStdLib>true</NoStdLib>
                <NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles>
                <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
                <EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                <DefineConstants>NOSTDLIB_EFI</DefineConstants>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <Deterministic>true</Deterministic>
                <DebugType>none</DebugType>
                <DebugSymbols>false</DebugSymbols>
                <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
                <OutputPath>$(MSBuildProjectDirectory)\out\</OutputPath>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="NoStdLibShim.cs" />
            __SOURCE_ITEMS__
                <Compile Include="Probe.cs" />
              </ItemGroup>
              <Target Name="CustomizeReferences"
                      BeforeTargets="BeforeCompile"
                      AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup>
                  <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" />
                </ItemGroup>
              </Target>
            </Project>
            """
            .Replace(
                "__SOURCE_ITEMS__",
                compileItems.ToString().TrimEnd(),
                StringComparison.Ordinal);
            File.WriteAllText(
                Path.Combine(workspace, "M01.018.NoStdLib.csproj"),
                projectText,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            return workspace;
        }
        catch
        {
            try
            {
                DeleteNoStdLibWorkspace(workspace);
            }
            catch
            {
            }

            throw;
        }
    }

    private static NoStdLibBuildResult RunNoStdLibBuild(
        string workspace)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workspace
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add("M01.018.NoStdLib.csproj");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-m:1");
        start.ArgumentList.Add("--no-dependencies");
        start.ArgumentList.Add("--no-incremental");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-v:minimal");
        start.ArgumentList.Add(
            "-p:RestoreIgnoreFailedSources=true");
        start.ArgumentList.Add("-p:NuGetAudit=false");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";

        using Process process = new Process { StartInfo = start };
        if (!process.Start())
        {
            return new NoStdLibBuildResult(
                -1,
                timedOut: false,
                "dotnet process did not start");
        }

        Task<string> standardOutput =
            process.StandardOutput.ReadToEndAsync();
        Task<string> standardError =
            process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90_000);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            process.WaitForExit(5_000);
        }

        string output = standardOutput.Wait(5_000)
            ? standardOutput.Result
            : "stdout did not close";
        string error = standardError.Wait(5_000)
            ? standardError.Result
            : "stderr did not close";
        return new NoStdLibBuildResult(
            exited ? process.ExitCode : -1,
            timedOut: !exited,
            output + Environment.NewLine + error);
    }

    private static bool HasNoExternalMetadata(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader reader = new PEReader(stream);
        if (!reader.HasMetadata)
        {
            return false;
        }

        MetadataReader metadata = reader.GetMetadataReader();
        if (metadata.AssemblyReferences.Count != 0
            || metadata.GetTableRowCount(TableIndex.ModuleRef) != 0
            || metadata.GetTableRowCount(TableIndex.ImplMap) != 0)
        {
            return false;
        }

        foreach (MethodDefinitionHandle handle
            in metadata.MethodDefinitions)
        {
            if ((metadata.GetMethodDefinition(handle).Attributes
                    & MethodAttributes.PinvokeImpl) != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExactNoStdLibIntegrationSurface(
        string assemblyPath)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader reader = new PEReader(stream);
        if (!reader.HasMetadata)
        {
            return false;
        }

        MetadataReader metadata = reader.GetMetadataReader();
        TypeDefinitionHandle integrationType = default;
        foreach (TypeDefinitionHandle handle
            in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            if (string.Equals(
                    metadata.GetString(type.Namespace),
                    "OS.Boot",
                    StringComparison.Ordinal)
                && string.Equals(
                    metadata.GetString(type.Name),
                    nameof(M01LiveAllocatorIntegration),
                    StringComparison.Ordinal))
            {
                integrationType = handle;
                break;
            }
        }

        if (integrationType.IsNil)
        {
            return false;
        }

        int arenaSourceCount = 0;
        int materializationCount = 0;
        int allocationCount = 0;
        int freeCount = 0;
        int doubleFreeCount = 0;
        int executeCount = 0;
        foreach (MethodDefinitionHandle handle in metadata
            .GetTypeDefinition(integrationType).GetMethods())
        {
            string name = metadata.GetString(
                metadata.GetMethodDefinition(handle).Name);
            if (string.Equals(
                name,
                "AuditArenaSource",
                StringComparison.Ordinal))
            {
                arenaSourceCount++;
            }
            else if (string.Equals(
                name,
                "AuditMaterialization",
                StringComparison.Ordinal))
            {
                materializationCount++;
            }
            else if (string.Equals(
                name,
                "AuditAllocation",
                StringComparison.Ordinal))
            {
                allocationCount++;
            }
            else if (string.Equals(
                name,
                "AuditFree",
                StringComparison.Ordinal))
            {
                freeCount++;
            }
            else if (string.Equals(
                name,
                "AuditDoubleFree",
                StringComparison.Ordinal))
            {
                doubleFreeCount++;
            }
            else if (string.Equals(
                name,
                nameof(M01LiveAllocatorIntegration.Execute),
                StringComparison.Ordinal))
            {
                executeCount++;
            }
        }

        return arenaSourceCount == 1
            && materializationCount == 1
            && allocationCount == 1
            && freeCount == 1
            && doubleFreeCount == 1
            && executeCount == 1;
    }

    private static bool ContainsInfrastructureFailure(string output)
    {
        return output.Contains(
                "MSB1009",
                StringComparison.OrdinalIgnoreCase)
            || output.Contains(
                "NU1301",
                StringComparison.OrdinalIgnoreCase)
            || output.Contains(
                "A compatible .NET SDK was not found",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCompilerOutput(string output)
    {
        string normalized = Regex.Replace(
            output,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant).Trim();
        return normalized.Length <= 1500
            ? normalized
            : normalized[..1500] + "...";
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string fullWorkspace = Path.GetFullPath(workspace);
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string? parent = Directory.GetParent(fullWorkspace)?.FullName;
        string leafName = Path.GetFileName(fullWorkspace);
        if (parent is null
            || !Path.TrimEndingDirectorySeparator(parent).Equals(
                Path.TrimEndingDirectorySeparator(tempRoot),
                StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(
                leafName,
                @"\Aclros-m01-018-[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-018 workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        if ((File.GetAttributes(fullWorkspace)
                & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Refusing to delete a reparse-point M01-018 workspace.");
        }

        Directory.Delete(fullWorkspace, recursive: true);
    }

    private readonly struct NoStdLibBuildResult
    {
        public NoStdLibBuildResult(
            int exitCode,
            bool timedOut,
            string output)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
        }

        public int ExitCode { get; }

        public bool TimedOut { get; }

        public string Output { get; }
    }
}
