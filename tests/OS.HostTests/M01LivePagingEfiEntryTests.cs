using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static class M01LivePagingEfiEntryTests
{
    private const string IssueTestName = "m01.025";
    private const string ReadyMarker = "CLROS_M01_025_READY";
    private const string UnexpectedPanicSymbol =
        "M02_UNEXPECTED_VECTOR_PANIC_PROBE";
    private const string StageOneSymbol =
        "M01_LOADED_IMAGE_STACK_PROBE";
    private const string StageTwoSymbol =
        "M01_LIVE_ALLOCATOR_PROBE";
    private const string StageThreeSymbol =
        "M01_OWNED_PAGING_PROBE";

    private static readonly byte[] ExpectedPatReaderBytes =
    [
        0xf3, 0x0f, 0x1e, 0xfa,
        0x53,
        0x49, 0x89, 0xc8,
        0xb8, 0x01, 0x00, 0x00, 0x00,
        0x0f, 0xa2,
        0x0f, 0xba, 0xe2, 0x10,
        0x73, 0x18,
        0xb9, 0x77, 0x02, 0x00, 0x00,
        0x0f, 0x32,
        0x48, 0xc1, 0xe2, 0x20,
        0x48, 0x09, 0xd0,
        0x49, 0x89, 0x00,
        0xb8, 0x01, 0x00, 0x00, 0x00,
        0x5b,
        0xc3,
        0x31, 0xc0,
        0x5b,
        0xc3
    ];

    private static readonly byte[] ExpectedTransferThunkBytes =
    [
        0xf3, 0x0f, 0x1e, 0xfa,
        0x53,
        0x48, 0x83, 0xec, 0x40,
        0x48, 0x89, 0x4c, 0x24, 0x20,
        0x48, 0x89, 0x54, 0x24, 0x28,
        0x4c, 0x89, 0x44, 0x24, 0x30,
        0x4c, 0x89, 0x4c, 0x24, 0x38,
        0x31, 0xc0,
        0x31, 0xc9,
        0x0f, 0xa2,
        0x48, 0x8b, 0x4c, 0x24, 0x20,
        0x48, 0x8b, 0x54, 0x24, 0x28,
        0x4c, 0x8b, 0x44, 0x24, 0x30,
        0x4c, 0x8b, 0x4c, 0x24, 0x38,
        0x41, 0x51,
        0x9d,
        0x41, 0xff, 0xd0,
        0x48, 0x8d, 0x64, 0x24, 0x40,
        0x5b,
        0xc3
    ];

    private static readonly byte[] ExpectedPlannerCallThunkBytes =
    [
        0xf3, 0x0f, 0x1e, 0xfa,
        0x53,
        0x56,
        0x57,
        0x48, 0x89, 0xe3,
        0x48, 0x89, 0xd6,
        0x4c, 0x89, 0xc7,
        0x48, 0x89, 0xcc,
        0x48, 0x83, 0xec, 0x20,
        0x48, 0x89, 0xf9,
        0xff, 0xd6,
        0x48, 0x83, 0xc4, 0x20,
        0x48, 0x89, 0xdc,
        0x5f,
        0x5e,
        0x5b,
        0xc3
    ];

    private static readonly string[] ExpectedStageThreeLines =
    [
        "TEST_START m01.stage3",
        "M01_CR3_STUB_PAGE_ALLOCATED",
        "M01_PROTECTION_PROBE_PAGE_ALLOCATED",
        "M01_PAGE_TABLE_STATE_ARENA_ALLOCATED",
        "M01_STAGE3_MAP_RECAPTURED",
        "M01_CR3_STUB_MATERIALIZED",
        "M01_PAGE_TABLES_BUILT",
        "M01_PAGING_MODE_VALIDATED",
        "M01_PRE_CR3",
        "M01_FIRMWARE_CR3_RESTORED",
        "M01_RFLAGS_RESTORED",
        "M01_PGE_CYCLE_VERIFIED",
        "M01_OWNED_CR3_CANARY_VERIFIED",
        "M01_POST_CR3",
        "M01_RW_TO_RX_VERIFIED",
        "M01_TLB_INVALIDATION_COMPLETE",
        "M01_RX_PROBE_EXECUTED",
        "M01_PAGE_ZERO_ABSENT",
        "TEST_PASS m01.stage3",
        "CLROS_M01_LIVE_BOOT_MEMORY_READY"
    ];

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);
        Run(
            "m01.025 profile matrix is default-off and stage-two-gated",
            TestProfileMatrixAndStageTwoWiring,
            ref failures);
        Run(
            "m01.025 three allocation sites are exact ordered and checked",
            TestExactAllocationContract,
            ref failures);
        Run(
            "m01.025 fixed arena map ledger and evidence are live-bound",
            TestArenaMapLedgerAndEvidenceContract,
            ref failures);
        Run(
            "m01.025 PAT and architectural preflight bytes are audited",
            TestPatReaderAndArchitecturalPreflight,
            ref failures);
        Run(
            "m01.025 preparation uses zero output counts and exact roles",
            TestPreparationAndExactLiveBindings,
            ref failures);
        Run(
            "m01.025 planner-call thunk bytes and publication are exact",
            TestPlannerCallThunkContract,
            ref failures);
        Run(
            "m01.025 planner stack canaries and wrapper witness are exact",
            TestPlannerStackAndWrapperContract,
            ref failures);
        Run(
            "m01.025 physical arena is materialized then read back",
            TestPhysicalArenaMaterialization,
            ref failures);
        Run(
            "m01.025 runtime gate and transfer thunk are exact and one-shot",
            TestRuntimeGateAndTransferThunk,
            ref failures);
        Run(
            "m01.025 no-CRT security cookie closure stays local",
            TestSecurityCookieClosure,
            ref failures);
        Run(
            "m01.025 owned interval excludes callbacks and audits before output",
            TestCallbackFreeIntervalAndPostReturnAudit,
            ref failures);
        Run(
            "m01.025 guest marker topology and failure suppression are exact",
            TestMarkerTopologyAndFailureSuppression,
            ref failures);
        Run(
            "m01.025 cleanup default path and source firewalls fail closed",
            TestCleanupDefaultAndSourceFirewalls,
            ref failures);
        Run(
            "m01.025 representative bypass mutations are rejected",
            TestRepresentativeMutations,
            ref failures);
        Run(
            "m01.025 reviewed leaf contracts remain compatible",
            TestReviewedLeafContractCompatibility,
            ref failures);
        Run(
            "m01.025 issue marker is unique",
            TestIssueMarkerUniqueness,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL "
                + IssueTestName
                + " failures="
                + failures);
            throw new InvalidOperationException(
                "M01-025 EFI stage-three source/integration tests failed: "
                + failures);
        }

        Console.WriteLine(ReadyMarker);
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

    private static bool TestProfileMatrixAndStageTwoWiring()
    {
        if (!TestKnownFalsePanicSelector())
        {
            return false;
        }

        string source = LoadEfiEntrySource();
        string project = LoadOsProjectSource();
        if (CountExactIf(source, StageOneSymbol) != 1
            || CountExactIf(source, StageTwoSymbol) != 2
            || CountExactIf(source, StageThreeSymbol) != 2)
        {
            return Failure("guard-cardinality");
        }

        string profile000 = SelectProfile(
            source,
            stageOneEnabled: false,
            stageTwoEnabled: false,
            stageThreeEnabled: false);
        string profile100 = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: false,
            stageThreeEnabled: false);
        string profile110 = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: true,
            stageThreeEnabled: false);
        string profile111 = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: true,
            stageThreeEnabled: true);
        string disabledType =
            FindTypeBody(profile110, "M01StageThreeAdapter")
            ?? string.Empty;
        string enabledType =
            FindTypeBody(profile111, "M01StageThreeAdapter")
            ?? string.Empty;
        string enabledRun =
            FindMethodBody(enabledType, "RunAfterStageTwo")
            ?? string.Empty;
        string stageTwoRun =
            FindMethodBody(
                FindTypeBody(profile111, "M01StageTwoAdapter")
                    ?? string.Empty,
                "RunAfterStageOne")
            ?? string.Empty;

        if (disabledType.Length != 0
            || enabledType.Length == 0
            || enabledRun.Length == 0
            || CountFlexibleQualifiedCalls(
                profile000,
                "M01StageThreeAdapter.RunAfterStageTwo") != 0
            || CountFlexibleQualifiedCalls(
                profile100,
                "M01StageThreeAdapter.RunAfterStageTwo") != 0
            || CountFlexibleQualifiedCalls(
                profile110,
                "M01StageThreeAdapter.RunAfterStageTwo") != 0
            || CountFlexibleQualifiedCalls(
                profile111,
                "M01StageThreeAdapter.RunAfterStageTwo") != 1)
        {
            return Failure("profile-selection");
        }

        List<string[]> calls =
            FindInvocationArguments(stageTwoRun, "RunAfterStageTwo");
        string[] expectedArguments =
        [
            "stageOneState",
            "stageOneStateCapacity",
            "staticState",
            "reservationLedger",
            "ReservationCount",
            "ReservationCapacity",
            "bootServices",
            "console",
            "serial"
        ];
        if (calls.Count != 1
            || !InvocationArgumentsEqual(
                calls[0],
                expectedArguments))
        {
            return Failure("stage-three-call-arguments");
        }

        int stageTwoMarkers = stageTwoRun.IndexOf(
            "M01StageTwoSuccessLineCount",
            StringComparison.Ordinal);
        int stageThreeCall = FlexibleCallIndex(
            stageTwoRun,
            "M01StageThreeAdapter.RunAfterStageTwo");
        int finalReturn = stageTwoRun.LastIndexOf(
            "return true;",
            StringComparison.Ordinal);
        string stageTwoCompact = Compact(stageTwoRun);
        if (stageTwoMarkers < 0
            || stageThreeCall <= stageTwoMarkers
            || finalReturn <= stageThreeCall
            || !stageTwoCompact.Contains(
                "if(!M01StageThreeAdapter.RunAfterStageTwo(",
                StringComparison.Ordinal)
            || !stageTwoCompact.Contains(
                "){returnfalse;}",
                StringComparison.Ordinal))
        {
            return Failure("stage-two-order-or-failure-propagation");
        }

        string enabledCompact = Compact(enabledRun);
        if (!enabledCompact.Contains(
                "stageTwoState->IsCompleted",
                StringComparison.Ordinal)
            || !enabledCompact.Contains(
                "M01StageOneAdapter.TryGetFinalSwitchPlan(",
                StringComparison.Ordinal)
            || !enabledCompact.Contains(
                "EfiEntry.s_bootHandoff.MemoryMap",
                StringComparison.Ordinal)
            || !enabledCompact.Contains(
                "stageTwoMap.HasOwnedCopy()",
                StringComparison.Ordinal)
            || !enabledCompact.Contains(
                "bootServices->AllocatePages==null",
                StringComparison.Ordinal)
            || !enabledCompact.Contains(
                "bootServices->FreePages==null",
                StringComparison.Ordinal)
            || !enabledCompact.Contains(
                "bootServices->GetMemoryMap==null",
                StringComparison.Ordinal)
            || profile000.Contains(
                "CaptureArchitecturalPreflight",
                StringComparison.Ordinal))
        {
            return Failure("prerequisite-or-default-boundary");
        }

        return project.Contains(
                "EnableM01OwnedPagingProbe)' == 'true' and "
                    + "'$(EnableM01LiveAllocatorProbe)' != 'true'",
                StringComparison.Ordinal)
            && project.Contains(
                "M01_OWNED_PAGING_PROBE",
                StringComparison.Ordinal)
            || Failure("project-switch-dependency");
    }

    private static bool TestExactAllocationContract()
    {
        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        List<string[]> allocations =
            FindInvocationArguments(run, "AllocatePages");
        if (allocations.Count != 3
            || !InvocationArgumentsEqual(
                allocations[0],
                "AllocateAnyPages",
                "EfiLoaderCode",
                "CodePageCount",
                "&codeAddress")
            || !InvocationArgumentsEqual(
                allocations[1],
                "AllocateAnyPages",
                "EfiLoaderCode",
                "ProbePageCount",
                "&probeAddress")
            || !InvocationArgumentsEqual(
                allocations[2],
                "AllocateAddress",
                "EfiLoaderData",
                "tablePageCount",
                "&tableAddress"))
        {
            return Failure("allocation-call-shape");
        }

        string compact = Compact(stageThree);
        string runCompact = Compact(run);
        if (!compact.Contains(
                "privateconstuintAllocateAnyPages=0;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstuintAllocateAddress=2;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstuintEfiLoaderCode=1;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstuintEfiLoaderData=2;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongCodePageCount=1;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongProbePageCount=1;",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "codeAllocated="
                    + "firmwareStatus==EfiSuccess;",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "probeAllocated="
                    + "firmwareStatus==EfiSuccess;",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "tableAllocated="
                    + "firmwareStatus==EfiSuccess;",
                StringComparison.Ordinal)
            || CountOccurrences(
                runCompact,
                "codeAllocated="
                    + "firmwareStatus==EfiSuccess;") != 1
            || CountOccurrences(
                runCompact,
                "probeAllocated="
                    + "firmwareStatus==EfiSuccess;") != 1
            || CountOccurrences(
                runCompact,
                "tableAllocated="
                    + "firmwareStatus==EfiSuccess;") != 1
            || !runCompact.Contains(
                "ulongrequestedTableAddress=tableAddress;",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "tableAddress!=requestedTableAddress",
                StringComparison.Ordinal)
            || CountFlexibleQualifiedCalls(
                run,
                "TryValidateAllocation") != 3
            || CountFlexibleQualifiedCalls(
                run,
                "ValidateNewAllocationAgainstStageTwo") != 3)
        {
            return Failure("allocation-validation");
        }

        int plan = FlexibleCallIndex(
            run,
            "TryPlanArenaFromCapturedMap");
        int first = FindNthCallIndex(run, "AllocatePages", 0);
        int second = FindNthCallIndex(run, "AllocatePages", 1);
        int third = FindNthCallIndex(run, "AllocatePages", 2);
        int codeGeometry =
            FindNthCallIndex(run, "TryValidateAllocation", 0);
        int probeGeometry =
            FindNthCallIndex(run, "TryValidateAllocation", 1);
        int tableGeometry =
            FindNthCallIndex(run, "TryValidateAllocation", 2);
        int codePriorMap = FindNthCallIndex(
            run,
            "ValidateNewAllocationAgainstStageTwo",
            0);
        int probePriorMap = FindNthCallIndex(
            run,
            "ValidateNewAllocationAgainstStageTwo",
            1);
        int tablePriorMap = FindNthCallIndex(
            run,
            "ValidateNewAllocationAgainstStageTwo",
            2);
        int codeOwned = run.IndexOf(
            "codeAllocated =",
            first + 1,
            StringComparison.Ordinal);
        int probeOwned = run.IndexOf(
            "probeAllocated =",
            second + 1,
            StringComparison.Ordinal);
        int tableOwned = run.IndexOf(
            "tableAllocated =",
            third + 1,
            StringComparison.Ordinal);
        int recapture = FlexibleCallIndex(
            run,
            "M01StageOneAdapter.TryRecaptureStageTwoMemoryMap");
        if (first < 0
            || second <= first
            || plan <= second
            || third <= plan
            || recapture <= third
            || codeOwned <= first
            || codeGeometry <= codeOwned
            || codePriorMap <= codeGeometry
            || probeOwned <= second
            || probeGeometry <= probeOwned
            || probePriorMap <= probeGeometry
            || tableOwned <= third
            || tableGeometry <= tableOwned
            || tablePriorMap <= tableGeometry
            || CountOccurrences(
                runCompact,
                "WriteSuccessLine(console,serial,bootServices,1);") != 1
            || CountOccurrences(
                runCompact,
                "WriteSuccessLine(console,serial,bootServices,2);") != 1
            || CountOccurrences(
                runCompact,
                "WriteSuccessLine(console,serial,bootServices,3);") != 1)
        {
            return Failure("allocation-order-or-markers");
        }

        return runCompact.Contains(
                "RangesOverlap(codeAddress,codeEnd,probeAddress,probeEnd)",
                StringComparison.Ordinal)
            && runCompact.Contains(
                "RangesOverlap(codeAddress,codeEnd,tableAddress,tableEnd)",
                StringComparison.Ordinal)
            && runCompact.Contains(
                "RangesOverlap(probeAddress,probeEnd,tableAddress,tableEnd)",
                StringComparison.Ordinal)
            && runCompact.Contains(
                "tableByteLength!=s_arenaPlan.TotalByteLength",
                StringComparison.Ordinal)
            && Compact(
                FindMethodBody(
                    stageThree,
                    "ValidateNewAllocationAgainstStageTwo")
                    ?? string.Empty).Contains(
                "TryDescribeExactAllocation("
                    + "stageTwoMap,start,pageCount,"
                    + "EfiConventionalMemory",
                StringComparison.Ordinal)
            && Compact(
                FindMethodBody(
                    stageThree,
                    "ValidateNewAllocationAgainstStageTwo")
                    ?? string.Empty).Contains(
                "RangesOverlap(start,endExclusive,"
                    + "reservationStart,reservationEnd)",
                StringComparison.Ordinal)
            || Failure("allocation-nonoverlap-or-exact-size");
    }

    private static bool TestArenaMapLedgerAndEvidenceContract()
    {
        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string compact = Compact(stageThree);
        string runCompact = Compact(run);

        if (!compact.Contains(
                "privateconstulongCodeAllocationOrdinal=8;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongProbeAllocationOrdinal=9;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongTableAllocationOrdinal=10;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongMapCaptureOrdinal=11;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongLedgerBuildOrdinal=12;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongRefreshedMapGeneration=4;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongPlanningReservationCount=7;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongFinalReservationCount=8;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongFinalReservationCapacity=8;",
                StringComparison.Ordinal))
        {
            return Failure("ordinal-generation-ledger-constants");
        }

        List<string[]> descriptions =
            FindInvocationArguments(run, "TryDescribeExactAllocation");
        if (descriptions.Count != 3
            || !InvocationArgumentsEqual(
                descriptions[0],
                "refreshedMap",
                "codeAddress",
                "CodePageCount",
                "EfiLoaderCode",
                "outallocationRecords[0]")
            || !InvocationArgumentsEqual(
                descriptions[1],
                "refreshedMap",
                "probeAddress",
                "ProbePageCount",
                "EfiLoaderCode",
                "outallocationRecords[1]")
            || !InvocationArgumentsEqual(
                descriptions[2],
                "refreshedMap",
                "tableAddress",
                "tablePageCount",
                "EfiLoaderData",
                "outallocationRecords[2]"))
        {
            return Failure("recaptured-map-exact-allocation-records");
        }

        if (CountFlexibleQualifiedCalls(
                run,
                "M01StageOneAdapter.TryRecaptureStageTwoMemoryMap") != 1
            || CountFlexibleQualifiedCalls(
                run,
                "M01LivePagingAllocationEvidence.CaptureCallerAttested")
                != 1
            || CountFlexibleQualifiedCalls(
                run,
                "M01LivePagingMapLedgerEvidence.CaptureCallerAttested")
                != 1
            || CountFlexibleQualifiedCalls(
                run,
                "allocationEvidence.IsValidFor") != 1
            || CountFlexibleQualifiedCalls(
                run,
                "mapEvidence.IsValidFor") != 1
            || !runCompact.Contains(
                "refreshedMap.MapKey==previousMapKey",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "allocationRecords[2].Attribute"
                    + "!=s_arenaPlan.AllocationRecord.Attribute",
                StringComparison.Ordinal)
            || CountFlexibleQualifiedCalls(
                run,
                "BuildFinalLedger") != 1)
        {
            return Failure("map-ledger-evidence-linkage");
        }

        string planning =
            FindMethodBody(stageThree, "TryPlanArenaFromCapturedMap")
            ?? string.Empty;
        string candidate =
            FindMethodBody(stageThree, "TryPlanCandidate")
            ?? string.Empty;
        string planningCompact = Compact(planning);
        string candidateCompact = Compact(candidate);
        if (planning.Length == 0
            || candidate.Length == 0
            || CountFlexibleQualifiedCalls(
                candidate,
                "M01LivePagingIntegration.PlanOwnedTableArena") != 1
            || !planningCompact.Contains(
                "map.BufferAddress",
                StringComparison.Ordinal)
            || !planningCompact.Contains(
                "map.HasOwnedCopy()",
                StringComparison.Ordinal)
            || !planningCompact.Contains(
                "descriptorIndex<map.DescriptorCount",
                StringComparison.Ordinal)
            || !planningCompact.Contains(
                "EfiConventionalMemory",
                StringComparison.Ordinal)
            || !planningCompact.Contains(
                "PlanningReservationCount",
                StringComparison.Ordinal)
            || !planningCompact.Contains(
                "TryPlanCandidate(",
                StringComparison.Ordinal)
            || !candidateCompact.Contains(
                "candidateAddress",
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure("candidate-not-derived-from-owned-map");
        }

        string finalLedger =
            FindMethodBody(stageThree, "BuildFinalLedger")
            ?? string.Empty;
        string planningLedger =
            FindMethodBody(stageThree, "BuildPlanningLedger")
            ?? string.Empty;
        string ledgerValidator =
            FindMethodBody(stageThree, "SortAndValidateLedger")
            ?? string.Empty;
        string ledgerCompact = Compact(finalLedger);
        string planningLedgerCompact = Compact(planningLedger);
        string ledgerValidatorCompact = Compact(ledgerValidator);
        return planningLedgerCompact.Contains(
                "BootReservationKind.Cr3TransitionStub",
                StringComparison.Ordinal)
            && planningLedgerCompact.Contains(
                "BootReservationKind.PageProtectionProbe",
                StringComparison.Ordinal)
            && ledgerCompact.Contains(
                "BootReservationKind.PageTableArena",
                StringComparison.Ordinal)
            && ledgerCompact.Contains(
                "BuildPlanningLedger(",
                StringComparison.Ordinal)
            && ledgerCompact.Contains(
                "SortAndValidateLedger(",
                StringComparison.Ordinal)
            && ledgerValidatorCompact.Contains(
                "BootReservationTableValidator.Validate(",
                StringComparison.Ordinal)
            || Failure("final-ledger-kinds-or-validation");
    }

    private static bool TestPatReaderAndArchitecturalPreflight()
    {
        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string capture =
            FindMethodBody(stageThree, "CaptureArchitecturalPreflight")
            ?? string.Empty;
        string compact = Compact(stageThree);
        string captureCompact = Compact(capture);

        if (!compact.Contains(
                "privateconstulongPatReaderEntryOffset=128;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongPatReaderByteLength=49;",
                StringComparison.Ordinal)
            || capture.Length == 0
            || CountFlexibleQualifiedCalls(
                run,
                "CaptureArchitecturalPreflight") != 1
            || CountFlexibleQualifiedCalls(
                capture,
                "X64PagingModePreflightStubPlan.TryMaterialize") != 1
            || CountFlexibleQualifiedCalls(
                capture,
                "X64PagingModePreflightStubPlan"
                    + ".TryValidateMaterialized") != 1
            || !captureCompact.Contains(
                "delegate*unmanaged<OS.Kernel.Memory."
                    + "X64PagingModePreflightObservation*,void>",
                StringComparison.Ordinal)
            || !captureCompact.Contains(
                "delegate*unmanaged<ulong*,uint>",
                StringComparison.Ordinal))
        {
            return Failure("preflight-or-pat-reader-linkage");
        }

        if (!captureCompact.Contains(
                "X64PatMappingToken.TryCreate(",
                StringComparison.Ordinal)
            || !captureCompact.Contains(
                "0,OS.Kernel.Memory.X64PatMappingToken"
                    + ".UnsupportedIndex,"
                    + "OS.Kernel.Memory.X64PatMappingToken"
                    + ".UnsupportedIndex",
                StringComparison.Ordinal)
            || !captureCompact.Contains(
                "X64PageTableEncodingPreconditions.CreateObserved(",
                StringComparison.Ordinal)
            || captureCompact.Contains(
                "0007040600070406",
                StringComparison.OrdinalIgnoreCase)
            || captureCompact.Contains(
                "PatFixture",
                StringComparison.Ordinal))
        {
            return Failure("PAT-token-must-use-live-index-zero-WB");
        }

        byte[] patBytes =
            ExtractMaterializedBytes(
                stageThree,
                "GetPatReaderByte",
                ExpectedPatReaderBytes.Length);
        if (!BytesEqual(patBytes, ExpectedPatReaderBytes))
        {
            return Failure(
                "PAT-reader-bytes="
                + Convert.ToHexString(patBytes));
        }

        int codeAllocation =
            FindNthCallIndex(run, "AllocatePages", 0);
        int probeAllocation =
            FindNthCallIndex(run, "AllocatePages", 1);
        int thunk = FlexibleCallIndex(
            run,
            "MaterializeTransferThunk");
        int plannerThunk = FlexibleCallIndex(
            run,
            "MaterializePlannerCallThunk");
        int preflight = FlexibleCallIndex(
            run,
            "CaptureArchitecturalPreflight");
        int plan = FlexibleCallIndex(
            run,
            "TryPlanArenaFromCapturedMap");
        return codeAllocation >= 0
            && probeAllocation > codeAllocation
            && thunk > probeAllocation
            && plannerThunk > thunk
            && preflight > plannerThunk
            && plan > preflight
            || Failure("live-preflight-order");
    }

    private static bool TestPreparationAndExactLiveBindings()
    {
        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string wrapper =
            FindMethodBody(
                stageThree,
                "M01StageThreePreparationContinuation")
            ?? string.Empty;
        List<string[]> prepareCalls =
            FindInvocationArguments(wrapper, "Prepare");
        if (prepareCalls.Count != 1
            || prepareCalls[0].Length != 36)
        {
            return Failure("preparation-call-cardinality");
        }

        string[] expectedPrepareArguments =
        [
            "context->StageTwoState",
            "1",
            "1",
            "context->StackSwitchPlan",
            "context->AllocationRecords",
            "AllocationRecordCount",
            "AllocationRecordCapacity",
            "context->AllocationEvidence",
            "context->MapEvidence",
            "context->ReservationLedger",
            "FinalReservationCount",
            "FinalReservationCapacity",
            "context->Requirements",
            "RequirementCount",
            "RequirementCount",
            "context->Ranges",
            "0",
            "RangeCapacity",
            "context->PageImages",
            "0",
            "PageImageCapacity",
            "context->PageEntries",
            "0",
            "PageEntryCapacity",
            "context->Preconditions",
            "context->CodePage",
            "PageSize",
            "context->ProbePage",
            "PageSize",
            "context->PreflightObservation",
            "1",
            "1",
            "M01LivePagingByteMode.EmitAndVerify",
            "context->PreparationState",
            "1",
            "1"
        ];
        if (!InvocationArgumentsEqual(
                prepareCalls[0],
                expectedPrepareArguments))
        {
            return Failure("preparation-arguments-or-zero-output-counts");
        }

        string compact = Compact(stageThree);
        string runCompact = Compact(run);
        string wrapperCompact = Compact(wrapper);
        if (CountFlexibleQualifiedCalls(
                run,
                "M01LivePagingIntegration.Prepare") != 0
            || CountFlexibleQualifiedCalls(
                stageThree,
                "M01LivePagingIntegration.Prepare") != 1
            || CountFlexibleQualifiedCalls(
                run,
                "RunPreparationOnPlannerStack") != 1
            || compact.Contains(
                "M01LivePagingIntegration.Execute(",
                StringComparison.Ordinal)
            || compact.Contains(
                "M01LivePagingIntegration.ExecuteModelOnly(",
                StringComparison.Ordinal)
            || compact.Contains(
                "M01LivePagingExecutionEvent",
                StringComparison.Ordinal)
            || compact.Contains(
                "M01LivePagingExecutionTranscript",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "s_preparation.PreparedForCompositeTransition",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "s_preparation.UsesExactLiveIntervalBindings",
                StringComparison.Ordinal)
            || !wrapperCompact.Contains(
                "context->CompletionValue=1;",
                StringComparison.Ordinal))
        {
            return Failure("preparation-mode-or-synthetic-transcript");
        }

        string fixedRequirements =
            FindMethodBody(stageThree, "BuildFixedRequirements")
            ?? string.Empty;
        string requirementsCompact = Compact(fixedRequirements);
        string[] exactPurposes =
        [
            "BootStackGuard",
            "CompositeCodeAndCallGate",
            "CompositeOwnedStack",
            "ProtectionProbePage"
        ];
        foreach (string purpose in exactPurposes)
        {
            if (CountOccurrences(
                    requirementsCompact,
                    "X64BootIdentityMappingPurpose." + purpose) != 1)
            {
                return Failure("fixed-purpose=" + purpose);
            }
        }

        string[] historicalAliases =
        [
            "ExecutingImage",
            "OwnedStack",
            "BootHandoff",
            "SerialStub",
            "AcpiScratch",
            "FramebufferMetadata",
            "AdditionalRequired"
        ];
        foreach (string alias in historicalAliases)
        {
            if (requirementsCompact.Contains(
                    "X64BootIdentityMappingPurpose." + alias,
                    StringComparison.Ordinal))
            {
                return Failure("historical-purpose=" + alias);
            }
        }

        return requirementsCompact.Contains(
                "PageProtection.None",
                StringComparison.Ordinal)
            && requirementsCompact.Contains(
                "PageCachePolicy.Uninitialized",
                StringComparison.Ordinal)
            && requirementsCompact.Contains(
                "OS.Kernel.Memory.PageProtection.Read"
                    + "|OS.Kernel.Memory.PageProtection.Execute",
                StringComparison.Ordinal)
            && requirementsCompact.Contains(
                "OS.Kernel.Memory.PageProtection.Read"
                    + "|OS.Kernel.Memory.PageProtection.Write",
                StringComparison.Ordinal)
            && requirementsCompact.Contains(
                "PageCachePolicy.WriteBack",
                StringComparison.Ordinal)
            || Failure("fixed-requirement-policy");
    }

    private static bool TestPlannerCallThunkContract()
    {
        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string materialize =
            FindMethodBody(stageThree, "MaterializePlannerCallThunk")
            ?? string.Empty;
        string validate =
            FindMethodBody(
                stageThree,
                "ValidatePlannerCallThunkReadback")
            ?? string.Empty;
        string compact = Compact(stageThree);
        string runCompact = Compact(run);
        string materializeCompact = Compact(materialize);
        string validateCompact = Compact(validate);

        if (!compact.Contains(
                "privateconstulongPlannerCallThunkEntryOffset=256;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongPlannerCallThunkByteLength=39;",
                StringComparison.Ordinal)
            || CountFlexibleQualifiedCalls(
                run,
                "MaterializePlannerCallThunk") != 1
            || CountFlexibleQualifiedCalls(
                run,
                "ValidatePlannerCallThunkReadback") != 1
            || !runCompact.Contains(
                "MaterializePlannerCallThunk("
                    + "stageOneCodeAllocation,"
                    + "outplannerCallEntry)",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "ValidatePlannerCallThunkReadback("
                    + "stageOneCodeAllocation,"
                    + "plannerCallEntry)",
                StringComparison.Ordinal))
        {
            return Failure("planner-thunk-linkage-or-geometry");
        }

        byte[] bytes =
            ExtractMaterializedBytes(
                stageThree,
                "GetPlannerCallThunkByte",
                ExpectedPlannerCallThunkBytes.Length);
        if (!BytesEqual(bytes, ExpectedPlannerCallThunkBytes))
        {
            return Failure(
                "planner-thunk-bytes="
                + Convert.ToHexString(bytes));
        }

        if (!materializeCompact.Contains(
                "stageOneCodeAllocation.UefiType!=EfiLoaderCode",
                StringComparison.Ordinal)
            || !materializeCompact.Contains(
                "PlannerCallThunkEntryOffset"
                    + "<TransferThunkEntryOffset"
                    + "+TransferThunkByteLength",
                StringComparison.Ordinal)
            || !materializeCompact.Contains(
                "destination[index]!=0xcc",
                StringComparison.OrdinalIgnoreCase)
            || CountFlexibleQualifiedCalls(
                materialize,
                "GetPlannerCallThunkByte") != 2
            || !validateCompact.Contains(
                "entryAddress!="
                    + "stageOneCodeAllocation.VirtualStart"
                    + "+PlannerCallThunkEntryOffset",
                StringComparison.Ordinal)
            || CountFlexibleQualifiedCalls(
                validate,
                "GetPlannerCallThunkByte") != 1)
        {
            return Failure("planner-thunk-ownership-or-readback");
        }

        int transferPublication = FlexibleCallIndex(
            run,
            "MaterializeTransferThunk");
        int plannerPublication = FlexibleCallIndex(
            run,
            "MaterializePlannerCallThunk");
        int preflight = FlexibleCallIndex(
            run,
            "CaptureArchitecturalPreflight");
        int gateReadback = FlexibleCallIndex(
            run,
            "M01OwnedCr3CompositeTransition"
                + ".ValidateCallGateReadback");
        int transferReadback = FlexibleCallIndex(
            run,
            "ValidateTransferThunkReadback");
        int plannerReadback = FlexibleCallIndex(
            run,
            "ValidatePlannerCallThunkReadback");
        int preTransfer = FlexibleCallIndex(
            run,
            "ValidatePreTransferState");
        return transferPublication >= 0
            && plannerPublication > transferPublication
            && preflight > plannerPublication
            && gateReadback > preflight
            && transferReadback > gateReadback
            && plannerReadback > transferReadback
            && preTransfer > plannerReadback
            || Failure("planner-thunk-publication-or-final-readback-order");
    }

    private static bool TestPlannerStackAndWrapperContract()
    {
        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string planner =
            FindMethodBody(
                stageThree,
                "RunPreparationOnPlannerStack")
            ?? string.Empty;
        string wrapper =
            FindMethodBody(
                stageThree,
                "M01StageThreePreparationContinuation")
            ?? string.Empty;
        string compact = Compact(stageThree);
        string plannerCompact = Compact(planner);
        string wrapperCompact = Compact(wrapper);

        const int expectedStackBytes = 256 * 1024;
        const int expectedMinimumUsableBytes = 224 * 1024;
        if (expectedStackBytes != 32768 * sizeof(ulong)
            || expectedStackBytes < expectedMinimumUsableBytes
            || !compact.Contains(
                "privateconstulongPlannerStackByteCount=256*1024;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstintPlannerStackUlongCount=32768;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongPlannerStackMinimumUsableBytes="
                    + "224*1024;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privatestaticM01StageThreePlannerStackBuffer"
                    + "s_plannerStack;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateunsafestruct"
                    + "M01StageThreePlannerStackBuffer"
                    + "{publicfixedulongValues["
                    + "PlannerStackUlongCount];}",
                StringComparison.Ordinal))
        {
            return Failure("planner-stack-static-capacity");
        }

        if (!compact.Contains(
                "privateconstulongPlannerStackLowCanary="
                    + "0x4d30_3150_4c4e_4c4fUL;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongPlannerStackHighCanary="
                    + "0x4d30_3150_4c4e_4849UL;",
                StringComparison.Ordinal)
            || !plannerCompact.Contains(
                "stackTop-lowCanaryAddress"
                    + "<PlannerStackMinimumUsableBytes",
                StringComparison.Ordinal)
            || !plannerCompact.Contains(
                "*lowCanary=PlannerStackLowCanary;",
                StringComparison.Ordinal)
            || !plannerCompact.Contains(
                "*highCanary=PlannerStackHighCanary;",
                StringComparison.Ordinal)
            || !plannerCompact.Contains(
                "boolcanariesIntact="
                    + "*lowCanary==PlannerStackLowCanary"
                    + "&&*highCanary==PlannerStackHighCanary;",
                StringComparison.Ordinal))
        {
            return Failure("planner-stack-canaries-or-usable-size");
        }

        if (!compact.Contains(
                "[System.Runtime.InteropServices."
                    + "UnmanagedCallers\\u004fnly]"
                    + "privatestaticvoid"
                    + "M01StageThreePreparationContinuation("
                    + "M01StageThreePreparationCallContext*context)",
                StringComparison.Ordinal)
            || CountOccurrences(
                compact,
                "M01StageThreePreparationContinuation(") != 1
            || !plannerCompact.Contains(
                "delegate*unmanaged<"
                    + "M01StageThreePreparationCallContext*,void>"
                    + "continuation="
                    + "&M01StageThreePreparationContinuation;",
                StringComparison.Ordinal)
            || !plannerCompact.Contains(
                "delegate*unmanaged<ulong,ulong,"
                    + "M01StageThreePreparationCallContext*,void>"
                    + "invoke=",
                StringComparison.Ordinal)
            || CountOccurrences(
                plannerCompact,
                "invoke(stackTop,continuationAddress,context);") != 1)
        {
            return Failure("planner-wrapper-abi-or-one-shot-call");
        }

        if (!wrapperCompact.Contains(
                "ulongstackWitness=0;",
                StringComparison.Ordinal)
            || !wrapperCompact.Contains(
                "ulong*stackWitnessPointer=&stackWitness;",
                StringComparison.Ordinal)
            || !wrapperCompact.Contains(
                "context->ObservedStackAddress="
                    + "(ulong)stackWitnessPointer;",
                StringComparison.Ordinal)
            || !wrapperCompact.Contains(
                "context->ObservedStackAddress"
                    + "<context->PlannerStackLowerBound",
                StringComparison.Ordinal)
            || !wrapperCompact.Contains(
                "context->ObservedStackAddress"
                    + ">=context->PlannerStackTop",
                StringComparison.Ordinal)
            || !plannerCompact.Contains(
                "boolstackWitnessInside="
                    + "observedStack"
                    + ">=context->PlannerStackLowerBound"
                    + "&&observedStack"
                    + "<context->PlannerStackTop;",
                StringComparison.Ordinal)
            || !plannerCompact.Contains(
                "returncompleted&&stackWitnessInside"
                    + "&&canariesIntact;",
                StringComparison.Ordinal))
        {
            return Failure("planner-stack-local-witness");
        }

        return CountFlexibleQualifiedCalls(
                    run,
                    "RunPreparationOnPlannerStack") == 1
            && CountFlexibleQualifiedCalls(
                    run,
                    "M01LivePagingIntegration.Prepare") == 0
            && CountFlexibleQualifiedCalls(
                    wrapper,
                    "M01LivePagingIntegration.Prepare") == 1
            || Failure("planner-wrapper-call-boundary");
    }

    private static bool TestPhysicalArenaMaterialization()
    {
        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        List<string[]> calls =
            FindInvocationArguments(
                run,
                "MaterializeOwnedTableArena");
        if (calls.Count != 2
            || calls[0].Length != 15
            || calls[1].Length != 15)
        {
            return Failure("arena-materialization-cardinality");
        }

        string[] first = calls[0];
        string[] second = calls[1];
        string[] commonExpected =
        [
            "s_arenaPlan",
            "allocationRecords[2]",
            "pageImages",
            "s_preparation.State.PageImageCount",
            "PageImageCapacity",
            "pageEntries",
            "s_preparation.State.PageEntryCount",
            "PageEntryCapacity",
            "(byte*)tableAddress",
            "s_arenaPlan.TotalByteLength",
            "s_arenaPlan.TotalByteLength"
        ];
        for (int index = 0; index < commonExpected.Length; index++)
        {
            if (!string.Equals(
                    Compact(first[index]),
                    commonExpected[index],
                    StringComparison.Ordinal)
                || !string.Equals(
                    Compact(second[index]),
                    commonExpected[index],
                    StringComparison.Ordinal))
            {
                return Failure("arena-argument=" + index);
            }
        }

        if (!string.Equals(
                Compact(first[11]),
                "M01LivePagingArenaByteMode.EmitAndVerify",
                StringComparison.Ordinal)
            || !string.Equals(
                Compact(second[11]),
                "M01LivePagingArenaByteMode.VerifyExisting",
                StringComparison.Ordinal)
            || !string.Equals(
                Compact(first[12]),
                "stateSnapshot",
                StringComparison.Ordinal)
            || !string.Equals(
                Compact(second[12]),
                "stateSnapshot",
                StringComparison.Ordinal))
        {
            return Failure("arena-byte-mode-or-snapshot");
        }

        int prepare = FlexibleCallIndex(
            run,
            "RunPreparationOnPlannerStack");
        int emit = FindNthCallIndex(
            run,
            "MaterializeOwnedTableArena",
            0);
        int verify = FindNthCallIndex(
            run,
            "MaterializeOwnedTableArena",
            1);
        int createState = FlexibleCallIndex(
            run,
            "M01OwnedCr3CompositeTransition.CreateRuntimeState");
        string runCompact = Compact(run);
        return prepare >= 0
            && emit > prepare
            && verify > emit
            && createState > verify
            && CountOccurrences(
                runCompact,
                "if(!s_arenaMaterialization.Succeeded") >= 1
            && runCompact.Contains(
                "ZeroBytes((byte*)statePageAddress,PageSize);",
                StringComparison.Ordinal)
            || Failure("arena-order-or-readback-gating");
    }

    private static bool TestRuntimeGateAndTransferThunk()
    {
        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string compact = Compact(stageThree);
        string runCompact = Compact(run);

        if (!compact.Contains(
                "privateconstulongBindingCount=6;",
                StringComparison.Ordinal)
            || CountFlexibleQualifiedCalls(
                run,
                "BuildBindings") != 1
            || CountFlexibleQualifiedCalls(
                run,
                "M01OwnedCr3CompositeTransition.CreateRuntimeState")
                != 1
            || CountFlexibleQualifiedCalls(
                run,
                "M01OwnedCr3CompositeTransition.PrepareCallGate") != 1
            || CountFlexibleQualifiedCalls(
                run,
                "M01OwnedCr3CompositeTransition.TryMaterializeCallGate")
                != 1
            || CountFlexibleQualifiedCalls(
                run,
                "M01OwnedCr3CompositeTransition"
                    + ".ValidateCallGateReadback") != 1
            || CountFlexibleQualifiedCalls(
                run,
                "ValidateTransferThunkReadback") != 1
            || CountFlexibleQualifiedCalls(
                run,
                "M01OwnedCr3CompositeTransition"
                    + ".AuditCallGateObservation") != 1)
        {
            return Failure("gate-call-cardinality");
        }

        string bindings =
            FindMethodBody(stageThree, "BuildBindings")
            ?? string.Empty;
        string bindingsCompact = Compact(bindings);
        string[] roles =
        [
            "CompositeCodeAndCallGate",
            "CompositeOwnedStack",
            "ProtectionProbePage",
            "CompleteTableAndStateArena",
            "RuntimeStateAndCanaryPage",
            "OwnedRootPage"
        ];
        foreach (string role in roles)
        {
            if (CountOccurrences(
                    bindingsCompact,
                    "M01OwnedCr3CompositeTransitionRangeRole."
                        + role) != 1)
            {
                return Failure("binding-role=" + role);
            }
        }

        if (!runCompact.Contains(
                "statePageAddress"
                    + "+M01OwnedCr3CompositeTransitionRuntimeState"
                    + ".StructureSize",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "statePageAddress+GateObservationOffset",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongGateObservationOffset=0x300;",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "codeAddress"
                    + "+M01OwnedCr3CompositeTransition"
                    + ".CallNextReturnSiteOffset",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "codeAddress"
                    + "+M01OwnedCr3CompositeTransition.CallGateOffset",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "M01OwnedCr3CompositeTransition"
                    + ".RequiredCombinedFnv1a64",
                StringComparison.Ordinal))
        {
            return Failure("runtime-layout-or-gate-geometry");
        }

        if (!compact.Contains(
                "privateconstulongTransferThunkEntryOffset=128;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongTransferThunkByteLength=68;",
                StringComparison.Ordinal)
            || CountFlexibleQualifiedCalls(
                run,
                "MaterializeTransferThunk") != 1
            || !runCompact.Contains(
                "MaterializeTransferThunk(stageOneCodeAllocation,"
                    + "outtransferEntry)",
                StringComparison.Ordinal))
        {
            return Failure("stage-one-transfer-thunk-linkage");
        }

        byte[] thunkBytes =
            ExtractMaterializedBytes(
                stageThree,
                "GetTransferThunkByte",
                ExpectedTransferThunkBytes.Length);
        if (!BytesEqual(thunkBytes, ExpectedTransferThunkBytes))
        {
            return Failure(
                "transfer-thunk-bytes="
                + Convert.ToHexString(thunkBytes));
        }

        string thunk =
            FindMethodBody(stageThree, "MaterializeTransferThunk")
            ?? string.Empty;
        string thunkCompact = Compact(thunk);
        if (!thunkCompact.Contains(
                "BootReservationKind.BootStackSwitchStub",
                StringComparison.Ordinal)
            && !thunkCompact.Contains(
                "stageOneCodeAllocation",
                StringComparison.Ordinal)
            || !thunkCompact.Contains(
                "TransferThunkEntryOffset",
                StringComparison.Ordinal)
            || !thunkCompact.Contains(
                "0xcc",
                StringComparison.OrdinalIgnoreCase)
            || !thunkCompact.Contains(
                "TransferThunkByteLength",
                StringComparison.Ordinal))
        {
            return Failure("transfer-thunk-ownership-or-readback");
        }

        if (!runCompact.Contains(
                "delegate*unmanaged"
                    + "<M01OwnedCr3CompositeTransitionRuntimeState*,"
                    + "M01OwnedCr3CompositeCallGateObservation*,"
                    + "ulong,ulong,uint>",
                StringComparison.Ordinal)
            || CountOccurrences(
                runCompact,
                "uintruntimeReturn=transfer(") != 1
            || !runCompact.Contains(
                "runtimeState,gateObservation,"
                    + "s_gateReadback.GateEntryAddress,"
                    + "s_preparation.State.OriginalRflags",
                StringComparison.Ordinal))
        {
            return Failure("transfer-function-pointer-or-one-shot-call");
        }

        string preTransfer =
            FindMethodBody(stageThree, "ValidatePreTransferState")
            ?? string.Empty;
        string probeReadback =
            FindMethodBody(
                stageThree,
                "ValidateProtectionProbeReadback")
            ?? string.Empty;
        string preTransferCompact = Compact(preTransfer);
        string probeReadbackCompact = Compact(probeReadback);
        return CountFlexibleQualifiedCalls(
                    run,
                    "ValidatePreTransferState") == 1
            && CountFlexibleQualifiedCalls(
                    run,
                    "ValidateProtectionProbeReadback") == 1
            && preTransferCompact.Contains(
                "runtimeState->ProhibitedFirmwareCallbackCount!=0",
                StringComparison.Ordinal)
            && preTransferCompact.Contains(
                "preflight->RawCr3"
                    + "!=s_preparation.State.RawFirmwareCr3",
                StringComparison.Ordinal)
            && preTransferCompact.Contains(
                "preflight->RawCr4"
                    + "!=s_preparation.State.RawCr4",
                StringComparison.Ordinal)
            && preTransferCompact.Contains(
                "preflight->Rflags"
                    + "!=s_preparation.State.OriginalRflags",
                StringComparison.Ordinal)
            && probeReadbackCompact.Contains(
                "X64ProtectionProbeStubPlan.ValidateReadback(",
                StringComparison.Ordinal)
            && probeReadbackCompact.Contains(
                "probePage[index]!=0",
                StringComparison.Ordinal)
            || Failure("pre-transfer-or-probe-readback");
    }

    private static bool TestSecurityCookieClosure()
    {
        string source = LoadEfiEntrySource();
        string profile000 = SelectProfile(
            source,
            stageOneEnabled: false,
            stageTwoEnabled: false,
            stageThreeEnabled: false);
        string stageThree = LoadEnabledStageThreeType();
        string compact = Compact(stageThree);
        string method =
            FindMethodBody(
                stageThree,
                "SecurityCookieStorageAnchor")
            ?? string.Empty;
        string methodCompact = Compact(method);

        if (!stageThree.Contains(
                "[System.Runtime.RuntimeExport(\"__security_cookie\")]",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privatestaticulongSecurityCookieStorageAnchor()",
                StringComparison.Ordinal)
            || !methodCompact.Contains(
                "return0x434c_524f_5347_5331UL;",
                StringComparison.Ordinal)
            || CountOccurrences(
                compact,
                "SecurityCookieStorageAnchor(") != 1
            || CountOccurrences(
                stageThree,
                "\"__security_cookie\"") != 1)
        {
            return Failure("security-cookie-export-or-local-storage");
        }

        string[] forbidden =
        [
            "__security_check_cookie",
            "DllImport",
            "LibraryImport",
            "NativeLibrary",
            "GetProcAddress"
        ];
        foreach (string token in forbidden)
        {
            if (compact.Contains(token, StringComparison.Ordinal))
            {
                return Failure("security-cookie-import=" + token);
            }
        }

        return !profile000.Contains(
                "__security_cookie",
                StringComparison.Ordinal)
            && !profile000.Contains(
                "SecurityCookieStorageAnchor",
                StringComparison.Ordinal)
            || Failure("security-cookie-default-profile");
    }

    private static bool TestCallbackFreeIntervalAndPostReturnAudit()
    {
        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        int preMarker = FindLastFlexibleCallIndexBefore(
            run,
            "WriteSuccessLine",
            run.IndexOf(
                "uint runtimeReturn = transfer(",
                StringComparison.Ordinal));
        int transfer = run.IndexOf(
            "uint runtimeReturn = transfer(",
            StringComparison.Ordinal);
        int audit = FlexibleCallIndex(
            run,
            "M01OwnedCr3CompositeTransition"
                + ".AuditCallGateObservation");
        int auditEnd = FindInvocationEnd(
            run,
            audit);
        int postLoop = run.IndexOf(
            "for (int lineIndex = 9;",
            StringComparison.Ordinal);
        if (preMarker < 0
            || transfer <= preMarker
            || audit <= transfer
            || auditEnd <= audit
            || postLoop <= auditEnd)
        {
            return Failure("owned-interval-boundaries");
        }

        string beforeTransfer =
            run[preMarker..transfer];
        string interval =
            run[transfer..auditEnd];
        string intervalCompact = Compact(interval);
        string[] forbiddenIntervalTokens =
        [
            "WriteSuccessLine",
            "WriteFailure",
            "EfiEntry.Write",
            "WriteCrLf",
            "console",
            "serial",
            "ConOut",
            "SerialIo",
            "BootServices",
            "AllocatePages",
            "FreePages",
            "GetMemoryMap",
            "M01_OWNED_CR3_ENTERED"
        ];
        foreach (string token in forbiddenIntervalTokens)
        {
            if (interval.Contains(token, StringComparison.Ordinal))
            {
                return Failure("owned-interval-callback=" + token);
            }
        }

        if (!Compact(beforeTransfer).Contains(
                "WriteSuccessLine(console,serial,bootServices,8);",
                StringComparison.Ordinal)
            || !intervalCompact.Contains(
                "M01OwnedCr3CompositeTransitionTerminalKind.Returned",
                StringComparison.Ordinal)
            || !intervalCompact.Contains(
                "runtimeReturn",
                StringComparison.Ordinal))
        {
            return Failure("pre-marker-or-audit-terminal");
        }

        string afterAudit =
            run[auditEnd..postLoop];
        return Compact(afterAudit).Contains(
                "if(!s_gateAudit.IsObservedAudit)",
                StringComparison.Ordinal)
            || Failure("post-output-before-restoration-proof");
    }

    private static bool TestMarkerTopologyAndFailureSuppression()
    {
        if (BootSequencePlan.M01StageThreeSuccessLineCount
                != ExpectedStageThreeLines.Length)
        {
            return Failure("marker-count");
        }

        for (int line = 0;
            line < ExpectedStageThreeLines.Length;
            line++)
        {
            string actual = ReadStageThreeLine(line);
            if (!string.Equals(
                    actual,
                    ExpectedStageThreeLines[line],
                    StringComparison.Ordinal))
            {
                return Failure(
                    "marker-line="
                    + line
                    + " actual="
                    + actual);
            }
        }

        string stageThree = LoadEnabledStageThreeType();
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string compact = Compact(run);
        for (int line = 0; line <= 8; line++)
        {
            if (CountOccurrences(
                    compact,
                    "WriteSuccessLine(console,serial,bootServices,"
                        + line.ToString(
                            CultureInfo.InvariantCulture)
                        + ");") != 1)
            {
                return Failure("pre-marker-call=" + line);
            }
        }

        if (!compact.Contains(
                "for(intlineIndex=9;"
                    + "lineIndex<BootSequencePlan"
                    + ".M01StageThreeSuccessLineCount;"
                    + "lineIndex++)",
                StringComparison.Ordinal)
            || !compact.Contains(
                "WriteSuccessLine(console,serial,bootServices,lineIndex);",
                StringComparison.Ordinal)
            || compact.Contains(
                "M01_OWNED_CR3_ENTERED",
                StringComparison.Ordinal))
        {
            return Failure("post-marker-loop-or-owned-marker");
        }

        string failBefore =
            FindMethodBody(stageThree, "FailBeforeEntry")
            ?? string.Empty;
        string failAfter =
            FindMethodBody(stageThree, "FailAfterEntry")
            ?? string.Empty;
        string failures = Compact(failBefore + failAfter);
        if (!failures.Contains(
                "TEST_FAIL",
                StringComparison.Ordinal)
            && !failures.Contains(
                "WriteFailure",
                StringComparison.Ordinal)
            || failures.Contains(
                "WriteSuccessLine",
                StringComparison.Ordinal)
            || failures.Contains(
                "M01StageThreeSuccessLineCount",
                StringComparison.Ordinal))
        {
            return Failure("failure-marker-suppression");
        }

        return true;
    }

    private static bool TestCleanupDefaultAndSourceFirewalls()
    {
        string source = LoadEfiEntrySource();
        string stageThree = LoadEnabledStageThreeType();
        string failBefore =
            FindMethodBody(stageThree, "FailBeforeEntry")
            ?? string.Empty;
        string failAfter =
            FindMethodBody(stageThree, "FailAfterEntry")
            ?? string.Empty;
        string beforeCompact = Compact(failBefore);
        string afterCompact = Compact(failAfter);

        if (CountOccurrences(
                beforeCompact,
                "bootServices->FreePages(") != 3
            || !beforeCompact.Contains(
                "if(tableAllocated)",
                StringComparison.Ordinal)
            || !beforeCompact.Contains(
                "if(probeAllocated)",
                StringComparison.Ordinal)
            || !beforeCompact.Contains(
                "if(codeAllocated)",
                StringComparison.Ordinal)
            || afterCompact.Contains(
                "bootServices->",
                StringComparison.Ordinal)
            || afterCompact.Contains(
                "FreePages(",
                StringComparison.Ordinal)
            || !afterCompact.Contains(
                "s_runState=3;while(true){}",
                StringComparison.Ordinal)
            || afterCompact.Contains(
                "return",
                StringComparison.Ordinal)
            || afterCompact.Contains(
                "Write",
                StringComparison.Ordinal))
        {
            return Failure("cleanup-plan-or-post-entry-firmware-use");
        }

        int tableFree = FindFreePagesCallIndex(
            failBefore,
            "tableAddress");
        int probeFree = FindFreePagesCallIndex(
            failBefore,
            "probeAddress");
        int codeFree = FindFreePagesCallIndex(
            failBefore,
            "codeAddress");
        if (tableFree < 0
            || probeFree <= tableFree
            || codeFree <= probeFree)
        {
            return Failure("cleanup-reverse-order");
        }

        if (!ValidatePreEntryFailureOwnershipCalls(
                FindMethodBody(stageThree, "RunAfterStageTwo")
                    ?? string.Empty)
            || !ValidateCleanupImplementation(failBefore))
        {
            return Failure("cleanup-source-binding");
        }

        if (!TestAllocationCleanupMatrix())
        {
            return Failure("allocation-cleanup-matrix");
        }

        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string runCompact = Compact(run);
        string[] ownershipAndStatusContracts =
        [
            "codeAllocated=firmwareStatus==EfiSuccess;",
            "probeAllocated=firmwareStatus==EfiSuccess;",
            "tableAllocated=firmwareStatus==EfiSuccess;",
            "codeAllocated?"
                + "M01StageThreeAdapterStatus.CodeAllocationRejected:"
                + "M01StageThreeAdapterStatus.CodeAllocationFailed",
            "probeAllocated?"
                + "M01StageThreeAdapterStatus.ProbeAllocationRejected:"
                + "M01StageThreeAdapterStatus.ProbeAllocationFailed",
            "tableAllocated?"
                + "M01StageThreeAdapterStatus.TableAllocationRejected:"
                + "M01StageThreeAdapterStatus.TableAllocationFailed"
        ];
        foreach (string contract in ownershipAndStatusContracts)
        {
            if (!runCompact.Contains(
                    contract,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "allocation-ownership-contract="
                    + contract);
            }
        }

        string[] preEntryStatuses =
        [
            "RepeatedUse",
            "PrerequisiteRejected",
            "StageTwoMapRejected",
            "CodeAllocationFailed",
            "CodeAllocationRejected",
            "ProbeAllocationFailed",
            "ProbeAllocationRejected",
            "ArchitecturalPreflightRejected",
            "PlanningInputRejected",
            "ArenaPlanRejected",
            "TableAllocationFailed",
            "TableAllocationRejected",
            "PostAllocationMapRejected",
            "EvidenceRejected",
            "PreparationRejected",
            "StatePageRejected",
            "ArenaMaterializationRejected",
            "BindingOrReadbackRejected",
            "RuntimeAddressRejected",
            "RuntimeStateRejected",
            "GatePreparationRejected",
            "GateMaterializationRejected",
            "FinalReadbackRejected",
            "PreTransferRejected"
        ];
        foreach (string status in preEntryStatuses)
        {
            if (CountOccurrences(
                    runCompact,
                    "M01StageThreeAdapterStatus."
                        + status) != 1)
            {
                return Failure(
                    "pre-entry-status="
                    + status);
            }
        }

        string compact = Compact(stageThree);
        string[] forbidden =
        [
            "M01LivePagingIntegration.Execute(",
            "M01LivePagingIntegration.ExecuteModelOnly(",
            "NullPageFaultProbePlan",
            "FaultProbeStubPlan.NullWrite",
            "M01_OWNED_CR3_ENTERED",
            "System.Reflection",
            "System.Linq",
            "stackalloc",
            "thrownew",
            "newbyte[",
            "newulong[",
            "List<",
            "Span<"
        ];
        foreach (string token in forbidden)
        {
            if (compact.Contains(token, StringComparison.Ordinal))
            {
                return Failure("source-firewall=" + token);
            }
        }

        return !source.Contains(
                "#if true // M01_OWNED_PAGING_PROBE",
                StringComparison.Ordinal)
            || Failure("forced-on-probe");
    }

    private static bool TestAllocationCleanupMatrix()
    {
        AllocationCleanupScenario[] scenarios =
        [
            new(
                "code-status-failure",
                AdapterAllocationOutcome.FirmwareFailure,
                AdapterAllocationOutcome.NotAttempted,
                AdapterAllocationOutcome.NotAttempted,
                FailAfterAllocations: false,
                ExpectedCleanup: string.Empty),
            new(
                "code-success-zero-address",
                AdapterAllocationOutcome.SuccessRejected,
                AdapterAllocationOutcome.NotAttempted,
                AdapterAllocationOutcome.NotAttempted,
                FailAfterAllocations: false,
                ExpectedCleanup: "code"),
            new(
                "code-success-misaligned-address",
                AdapterAllocationOutcome.SuccessRejected,
                AdapterAllocationOutcome.NotAttempted,
                AdapterAllocationOutcome.NotAttempted,
                FailAfterAllocations: false,
                ExpectedCleanup: "code"),
            new(
                "code-success-wrapped-range",
                AdapterAllocationOutcome.SuccessRejected,
                AdapterAllocationOutcome.NotAttempted,
                AdapterAllocationOutcome.NotAttempted,
                FailAfterAllocations: false,
                ExpectedCleanup: "code"),
            new(
                "code-success-prior-map-overlap",
                AdapterAllocationOutcome.SuccessRejected,
                AdapterAllocationOutcome.NotAttempted,
                AdapterAllocationOutcome.NotAttempted,
                FailAfterAllocations: false,
                ExpectedCleanup: "code"),
            new(
                "probe-status-failure",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.FirmwareFailure,
                AdapterAllocationOutcome.NotAttempted,
                FailAfterAllocations: false,
                ExpectedCleanup: "code"),
            new(
                "probe-success-zero-or-misaligned-address",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessRejected,
                AdapterAllocationOutcome.NotAttempted,
                FailAfterAllocations: false,
                ExpectedCleanup: "probe,code"),
            new(
                "probe-success-code-overlap",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessRejected,
                AdapterAllocationOutcome.NotAttempted,
                FailAfterAllocations: false,
                ExpectedCleanup: "probe,code"),
            new(
                "probe-success-prior-map-overlap",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessRejected,
                AdapterAllocationOutcome.NotAttempted,
                FailAfterAllocations: false,
                ExpectedCleanup: "probe,code"),
            new(
                "table-status-failure",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.FirmwareFailure,
                FailAfterAllocations: false,
                ExpectedCleanup: "probe,code"),
            new(
                "table-success-wrong-fixed-address",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessRejected,
                FailAfterAllocations: false,
                ExpectedCleanup: "table,probe,code"),
            new(
                "table-success-zero-misaligned-or-wrapped",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessRejected,
                FailAfterAllocations: false,
                ExpectedCleanup: "table,probe,code"),
            new(
                "table-success-code-or-probe-overlap",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessRejected,
                FailAfterAllocations: false,
                ExpectedCleanup: "table,probe,code"),
            new(
                "post-allocation-map-recapture-failure",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                FailAfterAllocations: true,
                ExpectedCleanup: "table,probe,code"),
            new(
                "post-allocation-type-or-attribute-failure",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                FailAfterAllocations: true,
                ExpectedCleanup: "table,probe,code"),
            new(
                "accepted-live-path-retains-backing",
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                AdapterAllocationOutcome.SuccessAccepted,
                FailAfterAllocations: false,
                ExpectedCleanup: string.Empty)
        ];

        foreach (AllocationCleanupScenario scenario in scenarios)
        {
            if (string.IsNullOrWhiteSpace(scenario.Name))
            {
                return false;
            }

            for (byte failedFreeMask = 0;
                failedFreeMask < 8;
                failedFreeMask++)
            {
                CleanupOracleResult cleanup =
                    SimulateAllocationCleanup(
                        scenario,
                        failedFreeMask);
                int expectedFailedAttempts =
                    CountExpectedFailedCleanupAttempts(
                        scenario.ExpectedCleanup,
                        failedFreeMask);
                if (!string.Equals(
                        cleanup.Attempts,
                        scenario.ExpectedCleanup,
                        StringComparison.Ordinal)
                    || cleanup.FailedAttemptCount
                        != expectedFailedAttempts)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static CleanupOracleResult
        SimulateAllocationCleanup(
            AllocationCleanupScenario scenario,
            byte failedFreeMask)
    {
        bool codeOwned = false;
        bool probeOwned = false;
        bool tableOwned = false;

        if (scenario.Code == AdapterAllocationOutcome.NotAttempted)
        {
            return new("!invalid-code-state", 0);
        }

        codeOwned =
            scenario.Code
                != AdapterAllocationOutcome.FirmwareFailure;
        if (scenario.Code
            != AdapterAllocationOutcome.SuccessAccepted)
        {
            return DescribeCleanup(
                codeOwned,
                probeOwned,
                tableOwned,
                failedFreeMask);
        }

        if (scenario.Probe == AdapterAllocationOutcome.NotAttempted)
        {
            return new("!invalid-probe-state", 0);
        }

        probeOwned =
            scenario.Probe
                != AdapterAllocationOutcome.FirmwareFailure;
        if (scenario.Probe
            != AdapterAllocationOutcome.SuccessAccepted)
        {
            return DescribeCleanup(
                codeOwned,
                probeOwned,
                tableOwned,
                failedFreeMask);
        }

        if (scenario.Table == AdapterAllocationOutcome.NotAttempted)
        {
            return new("!invalid-table-state", 0);
        }

        tableOwned =
            scenario.Table
                != AdapterAllocationOutcome.FirmwareFailure;
        if (scenario.Table
                != AdapterAllocationOutcome.SuccessAccepted
            || scenario.FailAfterAllocations)
        {
            return DescribeCleanup(
                codeOwned,
                probeOwned,
                tableOwned,
                failedFreeMask);
        }

        return new(string.Empty, 0);
    }

    private static CleanupOracleResult DescribeCleanup(
        bool codeOwned,
        bool probeOwned,
        bool tableOwned,
        byte failedFreeMask)
    {
        StringBuilder cleanup = new();
        int failedAttempts = 0;
        if (tableOwned)
        {
            cleanup.Append("table");
            if ((failedFreeMask & 4) != 0)
            {
                failedAttempts++;
            }
        }
        if (probeOwned)
        {
            if (cleanup.Length != 0)
            {
                cleanup.Append(',');
            }

            cleanup.Append("probe");
            if ((failedFreeMask & 2) != 0)
            {
                failedAttempts++;
            }
        }
        if (codeOwned)
        {
            if (cleanup.Length != 0)
            {
                cleanup.Append(',');
            }

            cleanup.Append("code");
            if ((failedFreeMask & 1) != 0)
            {
                failedAttempts++;
            }
        }

        return new(cleanup.ToString(), failedAttempts);
    }

    private static int CountExpectedFailedCleanupAttempts(
        string expectedCleanup,
        byte failedFreeMask)
    {
        int count = 0;
        if (expectedCleanup.Contains(
                "table",
                StringComparison.Ordinal)
            && (failedFreeMask & 4) != 0)
        {
            count++;
        }
        if (expectedCleanup.Contains(
                "probe",
                StringComparison.Ordinal)
            && (failedFreeMask & 2) != 0)
        {
            count++;
        }
        if (expectedCleanup.Contains(
                "code",
                StringComparison.Ordinal)
            && (failedFreeMask & 1) != 0)
        {
            count++;
        }

        return count;
    }

    private static bool TestRepresentativeMutations()
    {
        string source =
            NormalizeLineEndings(LoadEfiEntrySource());
        if (!ValidateEssentialContract(
                source,
                out string baselineFailure))
        {
            return Failure("baseline=" + baselineFailure);
        }

        (string Name, string Old, string New)[] mutations =
        [
            (
                "guard",
                "#if M01_OWNED_PAGING_PROBE",
                "#if true // M01_OWNED_PAGING_PROBE"
            ),
            (
                "allocate-address-mode",
                "private const uint AllocateAddress = 2;",
                "private const uint AllocateAddress = 0;"
            ),
            (
                "code-type",
                "private const uint EfiLoaderCode = 1;",
                "private const uint EfiLoaderCode = 2;"
            ),
            (
                "allocation-count",
                "private const ulong AllocationRecordCount = 3;",
                "private const ulong AllocationRecordCount = 2;"
            ),
            (
                "fixed-address-readback",
                "tableAddress != requestedTableAddress",
                "false"
            ),
            (
                "map-key",
                "refreshedMap.MapKey == previousMapKey",
                "refreshedMap.MapKey != previousMapKey"
            ),
            (
                "final-ledger",
                "private const ulong FinalReservationCount = 8;",
                "private const ulong FinalReservationCount = 7;"
            ),
            (
                "PAT-length",
                "private const ulong PatReaderByteLength = 49;",
                "private const ulong PatReaderByteLength = 48;"
            ),
            (
                "PAT-live-value",
                "                            *rawIa32Pat,",
                "                            0UL,"
            ),
            (
                "prepare-only",
                "M01LivePagingIntegration.Prepare(",
                "M01LivePagingIntegration.Execute("
            ),
            (
                "arena-readback",
                ".VerifyExisting,",
                ".EmitAndVerify,"
            ),
            (
                "binding-count",
                "private const ulong BindingCount = 6;",
                "private const ulong BindingCount = 5;"
            ),
            (
                "transfer-offset",
                "private const ulong TransferThunkEntryOffset = 128;",
                "private const ulong TransferThunkEntryOffset = 64;"
            ),
            (
                "transfer-length",
                "private const ulong TransferThunkByteLength = 68;",
                "private const ulong TransferThunkByteLength = 67;"
            ),
            (
                "planner-thunk-offset",
                "private const ulong PlannerCallThunkEntryOffset = 256;",
                "private const ulong PlannerCallThunkEntryOffset = 192;"
            ),
            (
                "planner-thunk-length",
                "private const ulong PlannerCallThunkByteLength = 39;",
                "private const ulong PlannerCallThunkByteLength = 38;"
            ),
            (
                "planner-stack-capacity",
                "private const ulong PlannerStackByteCount = 256 * 1024;",
                "private const ulong PlannerStackByteCount = 128 * 1024;"
            ),
            (
                "planner-stack-minimum",
                "            224 * 1024;",
                "            128 * 1024;"
            ),
            (
                "planner-wrapper-attribute",
                "[System.Runtime.InteropServices.UnmanagedCallers\\u004fnly]",
                "[System.Runtime.CompilerServices.SkipLocalsInit]"
            ),
            (
                "planner-wrapper-boundary",
                "RunPreparationOnPlannerStack(",
                "BypassPlannerStack("
            ),
            (
                "prior-map-allocation-validation",
                "ValidateNewAllocationAgainstStageTwo(",
                "BypassPriorMapValidation("
            ),
            (
                "code-ownership-at-status",
                "codeAllocated = firmwareStatus == EfiSuccess;",
                "bool codeAllocationSucceeded = "
                    + "firmwareStatus == EfiSuccess;"
            ),
            (
                "probe-ownership-at-status",
                "probeAllocated = firmwareStatus == EfiSuccess;",
                "bool probeAllocationSucceeded = "
                    + "firmwareStatus == EfiSuccess;"
            ),
            (
                "table-ownership-at-status",
                "tableAllocated = firmwareStatus == EfiSuccess;",
                "bool tableAllocationSucceeded = "
                    + "firmwareStatus == EfiSuccess;"
            ),
            (
                "code-ownership-address-truthiness",
                "codeAllocated = firmwareStatus == EfiSuccess;",
                "codeAllocated = codeAddress != 0;"
            ),
            (
                "probe-ownership-address-truthiness",
                "probeAllocated = firmwareStatus == EfiSuccess;",
                "probeAllocated = probeAddress != 0;"
            ),
            (
                "table-ownership-address-truthiness",
                "tableAllocated = firmwareStatus == EfiSuccess;",
                "tableAllocated = tableAddress != 0;"
            ),
            (
                "failure-call-drops-code-ownership",
                "                    codeAddress,\n"
                    + "                    codeAllocated,\n"
                    + "                    probeAddress,",
                "                    codeAddress,\n"
                    + "                    false,\n"
                    + "                    probeAddress,"
            ),
            (
                "cleanup-removes-table-branch",
                "if (tableAllocated)",
                "if (false)"
            ),
            (
                "cleanup-removes-probe-branch",
                "if (probeAllocated)",
                "if (false)"
            ),
            (
                "cleanup-removes-code-branch",
                "if (codeAllocated)",
                "if (false)"
            ),
            (
                "cleanup-table-wrong-page-count",
                "                            tableAddress,\n"
                    + "                            tablePageCount);",
                "                            tableAddress,\n"
                    + "                            ProbePageCount);"
            ),
            (
                "cleanup-table-early-return",
                "                            tablePageCount);\n"
                    + "                }\n"
                    + "                if (probeAllocated)",
                "                            tablePageCount);\n"
                    + "                    return false;\n"
                    + "                }\n"
                    + "                if (probeAllocated)"
            ),
            (
                "planner-low-canary",
                "*lowCanary == PlannerStackLowCanary",
                "true"
            ),
            (
                "post-entry-fail-stop",
                "            while (true)",
                "            return false"
            ),
            (
                "one-shot-guard",
                "if (s_runState != 0)",
                "if (false)"
            ),
            (
                "one-shot-running-state",
                "s_runState = 1;",
                "s_runState = 0;"
            ),
            (
                "one-shot-success-state",
                "s_runState = 2;",
                "s_runState = 0;"
            ),
            (
                "one-shot-failure-state",
                "s_runState = 3;",
                "s_runState = 0;"
            ),
            (
                "observation-offset",
                "private const ulong GateObservationOffset = 0x300;",
                "private const ulong GateObservationOffset = 0x1e8;"
            ),
            (
                "gate-invocation",
                "uint runtimeReturn = transfer(",
                "uint runtimeReturn = BypassTransfer("
            ),
            (
                "post-audit",
                ".AuditCallGateObservation(",
                ".BypassCallGateAudit("
            ),
            (
                "post-marker-start",
                "for (int lineIndex = 9;",
                "for (int lineIndex = 8;"
            ),
            (
                "null-fault",
                "s_runState = 1;",
                "NullPageFaultProbePlan.Plan(default);"
                    + " s_runState = 1;"
            )
        ];

        foreach ((string name, string oldText, string newText)
            in mutations)
        {
            if (!source.Contains(
                    oldText,
                    StringComparison.Ordinal))
            {
                return Failure("mutation-anchor=" + name);
            }

            string mutated = ReplaceFirst(
                source,
                oldText,
                newText);
            if (ValidateEssentialContract(
                    mutated,
                    out string mutationFailure))
            {
                return Failure(
                    "mutation-accepted="
                    + name
                    + " detail="
                    + mutationFailure);
            }
        }

        return true;
    }

    private static bool TestReviewedLeafContractCompatibility()
    {
        const ulong architecturalPat =
            0x0007_0406_0007_0406UL;
        X64PageTableEntryStatus patStatus =
            X64PatMappingToken.TryCreate(
                architecturalPat,
                0,
                X64PatMappingToken.UnsupportedIndex,
                X64PatMappingToken.UnsupportedIndex,
                out X64PatMappingToken pat);

        return M01LivePagingIntegration
                .RequiredAllocationRecordCount == 3
            && M01LivePagingIntegration
                .RequiredLiveIntervalMappingRequirementCount == 7
            && M01LivePagingIntegration
                .RequiredArenaFixedRequirementCount == 4
            && M01LivePagingIntegration
                .RequiredArenaRequirementWorkspaceCount == 7
            && M01LivePagingIntegration
                .RequiredArenaStatePageCount == 1
            && M01OwnedCr3CompositeTransition
                .CallGateOffset == 0x0f00
            && M01OwnedCr3CompositeTransition
                .CallNextReturnSiteOffset == 0x0fc9
            && M01OwnedCr3CompositeTransition
                .CallGateExactCompositeCallCount == 1
            && M01OwnedCr3CompositeTransition
                .CallGateExactPostReturnObservationCount == 1
            && M01OwnedCr3CompositeTransitionRuntimeState
                .StructureSize == 480
            && M01OwnedCr3CompositeCallGateObservation
                .StructureSize == 160
            && patStatus == X64PageTableEntryStatus.Success
            && pat.IsInitialized
            && pat.WriteBackIndex == 0
            && pat.TryValidateMemoryTypeAtIndex(
                0,
                PageCachePolicy.WriteBack)
                == X64PageTableEntryStatus.Success
            || Failure("reviewed-leaf-contract-drift");
    }

    private static bool TestIssueMarkerUniqueness()
    {
        string repositoryRoot =
            HostToolProcess.FindRepositoryRoot();
        List<string> markerSources = [];
        foreach (string candidate in Directory.EnumerateFiles(
            repositoryRoot,
            "*.cs",
            SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(candidate);
            if (IsGeneratedOrReferencePath(fullPath)
                || !File.ReadAllText(fullPath).Contains(
                    ReadyMarker,
                    StringComparison.Ordinal))
            {
                continue;
            }

            markerSources.Add(fullPath);
        }

        string expected = Path.GetFullPath(
            Path.Combine(
                repositoryRoot,
                "tests",
                "OS.HostTests",
                "M01LivePagingEfiEntryTests.cs"));
        if (markerSources.Count != 1
            || !string.Equals(
                markerSources[0],
                expected,
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure("marker-source-count");
        }

        string testSource = File.ReadAllText(expected);
        int start = testSource.IndexOf(
            "Console.WriteLine(\"TEST_START \" + IssueTestName)",
            StringComparison.Ordinal);
        int ready = testSource.IndexOf(
            "Console.WriteLine(ReadyMarker)",
            StringComparison.Ordinal);
        int pass = testSource.IndexOf(
            "Console.WriteLine(\"TEST_PASS \" + IssueTestName)",
            StringComparison.Ordinal);
        return start >= 0
            && ready > start
            && pass > ready
            || Failure("marker-order");
    }

    private static bool ValidateEssentialContract(
        string source,
        out string failure)
    {
        try
        {
            if (CountExactIf(source, StageThreeSymbol) != 2)
            {
                failure = "guards";
                return false;
            }

            string profile = SelectProfile(
                source,
                stageOneEnabled: true,
                stageTwoEnabled: true,
                stageThreeEnabled: true);
            string stageThree =
                FindTypeBody(profile, "M01StageThreeAdapter")
                ?? string.Empty;
            string run =
                FindMethodBody(stageThree, "RunAfterStageTwo")
                ?? string.Empty;
            string wrapper =
                FindMethodBody(
                    stageThree,
                    "M01StageThreePreparationContinuation")
                ?? string.Empty;
            string planner =
                FindMethodBody(
                    stageThree,
                    "RunPreparationOnPlannerStack")
                ?? string.Empty;
            string failAfter =
                FindMethodBody(stageThree, "FailAfterEntry")
                ?? string.Empty;
            string failBefore =
                FindMethodBody(stageThree, "FailBeforeEntry")
                ?? string.Empty;
            string compact = Compact(stageThree);
            string runCompact = Compact(run);
            string plannerCompact = Compact(planner);
            string failAfterCompact = Compact(failAfter);
            List<string[]> allocations =
                FindInvocationArguments(run, "AllocatePages");

            if (stageThree.Length == 0
                || run.Length == 0
                || allocations.Count != 3
                || !InvocationArgumentsEqual(
                    allocations[0],
                    "AllocateAnyPages",
                    "EfiLoaderCode",
                    "CodePageCount",
                    "&codeAddress")
                || !InvocationArgumentsEqual(
                    allocations[1],
                    "AllocateAnyPages",
                    "EfiLoaderCode",
                    "ProbePageCount",
                    "&probeAddress")
                || !InvocationArgumentsEqual(
                    allocations[2],
                    "AllocateAddress",
                    "EfiLoaderData",
                    "tablePageCount",
                    "&tableAddress"))
            {
                failure = "allocations";
                return false;
            }

            string[] required =
            [
                "privateconstuintAllocateAddress=2;",
                "privateconstuintEfiLoaderCode=1;",
                "privateconstulongAllocationRecordCount=3;",
                "privateconstulongFinalReservationCount=8;",
                "privateconstulongPatReaderByteLength=49;",
                "privateconstulongBindingCount=6;",
                "privateconstulongTransferThunkEntryOffset=128;",
                "privateconstulongTransferThunkByteLength=68;",
                "privateconstulongPlannerCallThunkEntryOffset=256;",
                "privateconstulongPlannerCallThunkByteLength=39;",
                "privateconstulongPlannerStackByteCount=256*1024;",
                "privateconstintPlannerStackUlongCount=32768;",
                "privateconstulongPlannerStackMinimumUsableBytes="
                    + "224*1024;",
                "privateconstulongGateObservationOffset=0x300;",
                "tableAddress!=requestedTableAddress",
                "refreshedMap.MapKey==previousMapKey",
                "codeAllocated=firmwareStatus==EfiSuccess;",
                "probeAllocated=firmwareStatus==EfiSuccess;",
                "tableAllocated=firmwareStatus==EfiSuccess;",
                "M01LivePagingArenaByteMode.EmitAndVerify",
                "M01LivePagingArenaByteMode.VerifyExisting",
                "[System.Runtime.InteropServices."
                    + "UnmanagedCallers\\u004fnly]",
                "uintruntimeReturn=transfer(",
                "for(intlineIndex=9;"
            ];
            foreach (string token in required)
            {
                if (!compact.Contains(
                        token,
                        StringComparison.Ordinal))
                {
                    failure = "required=" + token;
                    return false;
                }
            }

            if (CountFlexibleQualifiedCalls(
                    run,
                    "M01LivePagingIntegration.Prepare") != 0
                || CountFlexibleQualifiedCalls(
                    wrapper,
                    "M01LivePagingIntegration.Prepare") != 1
                || CountFlexibleQualifiedCalls(
                    run,
                    "RunPreparationOnPlannerStack") != 1
                || CountFlexibleQualifiedCalls(
                    run,
                    "MaterializePlannerCallThunk") != 1
                || CountFlexibleQualifiedCalls(
                    run,
                    "ValidatePlannerCallThunkReadback") != 1
                || CountFlexibleQualifiedCalls(
                    run,
                    "ValidateNewAllocationAgainstStageTwo") != 3
                || CountFlexibleQualifiedCalls(
                    run,
                    "MaterializeOwnedTableArena") != 2
                || CountFlexibleQualifiedCalls(
                    run,
                    "M01OwnedCr3CompositeTransition"
                        + ".CreateRuntimeState") != 1
                || CountFlexibleQualifiedCalls(
                    run,
                    "M01OwnedCr3CompositeTransition"
                        + ".PrepareCallGate") != 1
                || CountFlexibleQualifiedCalls(
                    run,
                    "M01OwnedCr3CompositeTransition"
                        + ".TryMaterializeCallGate") != 1
                || CountFlexibleQualifiedCalls(
                    run,
                    "M01OwnedCr3CompositeTransition"
                        + ".ValidateCallGateReadback") != 1
                || CountFlexibleQualifiedCalls(
                    run,
                    "M01OwnedCr3CompositeTransition"
                        + ".AuditCallGateObservation") != 1
                || !Compact(
                    FindMethodBody(
                        stageThree,
                        "CaptureArchitecturalPreflight")
                        ?? string.Empty).Contains(
                    "X64PatMappingToken.TryCreate("
                        + "*rawIa32Pat,0,"
                        + "OS.Kernel.Memory.X64PatMappingToken"
                        + ".UnsupportedIndex,"
                        + "OS.Kernel.Memory.X64PatMappingToken"
                        + ".UnsupportedIndex",
                    StringComparison.Ordinal)
                || !plannerCompact.Contains(
                    "*lowCanary==PlannerStackLowCanary"
                        + "&&*highCanary==PlannerStackHighCanary",
                    StringComparison.Ordinal)
                || !plannerCompact.Contains(
                    "stackTop-lowCanaryAddress"
                        + "<PlannerStackMinimumUsableBytes",
                    StringComparison.Ordinal)
                || !failAfterCompact.Contains(
                    "s_runState=3;while(true){}",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "if(s_runState!=0)",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "s_runState=1;",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "s_runState=2;returntrue;",
                    StringComparison.Ordinal)
                || CountOccurrences(
                    compact,
                    "s_runState=3;") != 2
                || !ValidatePreEntryFailureOwnershipCalls(run)
                || !ValidateCleanupImplementation(failBefore)
                || failAfterCompact.Contains(
                    "return",
                    StringComparison.Ordinal)
                || runCompact.Contains(
                    "NullPageFaultProbePlan",
                    StringComparison.Ordinal)
                || compact.Contains(
                    "M01LivePagingIntegration.Execute(",
                    StringComparison.Ordinal)
                || compact.Contains(
                    "M01LivePagingIntegration.ExecuteModelOnly(",
                    StringComparison.Ordinal))
            {
                failure = "linkage-or-firewall";
                return false;
            }

            failure = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            failure = ex.GetType().Name + ": " + ex.Message;
            return false;
        }
    }

    private static byte[] ExtractMaterializedBytes(
        string typeSource,
        string methodName,
        int expectedLength)
    {
        string method =
            FindMethodBody(typeSource, methodName)
            ?? string.Empty;
        if (method.Length == 0)
        {
            return [];
        }

        byte?[] indexed = new byte?[expectedLength];
        MatchCollection switchArms = Regex.Matches(
            method,
            @"(?m)^\s*(?<index>\d+)\s*=>\s*"
                + @"(?<value>0x[0-9a-fA-F]+|\d+)\s*,?\s*$",
            RegexOptions.CultureInvariant);
        foreach (Match match in switchArms)
        {
            int index = ParseInteger(match.Groups["index"].Value);
            int value = ParseInteger(match.Groups["value"].Value);
            if ((uint)index >= (uint)indexed.Length
                || (uint)value > byte.MaxValue
                || indexed[index].HasValue)
            {
                return [];
            }

            indexed[index] = (byte)value;
        }

        if (switchArms.Count != 0)
        {
            return switchArms.Count == expectedLength
                    && indexed.All(item => item.HasValue)
                ? indexed.Select(item => item!.Value).ToArray()
                : [];
        }

        MatchCollection directAssignments = Regex.Matches(
            method,
            @"\b(?:destination|entry|stub|bytes)\s*"
                + @"\[\s*(?<index>\d+)\s*\]\s*=\s*"
                + @"(?<value>0x[0-9a-fA-F]+|\d+)\s*;",
            RegexOptions.CultureInvariant);
        foreach (Match match in directAssignments)
        {
            int index = ParseInteger(match.Groups["index"].Value);
            int value = ParseInteger(match.Groups["value"].Value);
            if ((uint)index < (uint)indexed.Length
                && (uint)value <= byte.MaxValue)
            {
                indexed[index] = (byte)value;
            }
        }

        if (indexed.All(item => item.HasValue))
        {
            return indexed.Select(item => item!.Value).ToArray();
        }

        List<byte> sequential = [];
        MatchCollection writes = Regex.Matches(
            method,
            @"\b(?:WriteByte|WriteStubByte|WritePatReaderByte"
                + @"|WriteTransferThunkByte)\s*\("
                + @"[\s\S]*?,\s*"
                + @"(?<value>0x[0-9a-fA-F]+|\d+)\s*\)",
            RegexOptions.CultureInvariant);
        foreach (Match match in writes)
        {
            int value = ParseInteger(match.Groups["value"].Value);
            if ((uint)value <= byte.MaxValue)
            {
                sequential.Add((byte)value);
            }
        }

        if (sequential.Count == expectedLength)
        {
            return [.. sequential];
        }

        MatchCollection literals = Regex.Matches(
            method,
            @"(?<![A-Za-z0-9_])0x(?<value>[0-9a-fA-F]{2})"
                + @"(?![A-Za-z0-9_])",
            RegexOptions.CultureInvariant);
        foreach (Match match in literals)
        {
            sequential.Add(
                byte.Parse(
                    match.Groups["value"].Value,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture));
        }

        return sequential.Count == expectedLength
            ? [.. sequential]
            : [];
    }

    private static int ParseInteger(string value)
    {
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.Parse(
                value.AsSpan(2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture)
            : int.Parse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
    }

    private static bool BytesEqual(
        byte[] actual,
        byte[] expected)
    {
        if (actual.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadStageThreeLine(int lineIndex)
    {
        int length =
            BootSequencePlan.GetM01StageThreeSuccessLineLength(
                lineIndex);
        byte[] bytes = new byte[length];
        for (int offset = 0; offset < length; offset++)
        {
            bytes[offset] =
                BootSequencePlan.GetM01StageThreeSuccessLineByte(
                    lineIndex,
                    offset);
        }

        return Encoding.ASCII.GetString(bytes);
    }

    private static int FindFreePagesCallIndex(
        string source,
        string addressName)
    {
        List<(int Index, string[] Arguments)> calls =
            FindInvocationArgumentsWithIndices(
                source,
                "FreePages");
        foreach ((int index, string[] arguments) in calls)
        {
            if (arguments.Length != 0
                && Compact(arguments[0]).Contains(
                    addressName,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool ValidatePreEntryFailureOwnershipCalls(
        string run)
    {
        List<string[]> calls =
            FindInvocationArguments(run, "FailBeforeEntry");
        if (calls.Count != 21)
        {
            return false;
        }

        foreach (string[] arguments in calls)
        {
            if (arguments.Length != 12
                || !string.Equals(
                    Compact(arguments[5]),
                    "codeAddress",
                    StringComparison.Ordinal)
                || !string.Equals(
                    Compact(arguments[6]),
                    "codeAllocated",
                    StringComparison.Ordinal)
                || !string.Equals(
                    Compact(arguments[7]),
                    "probeAddress",
                    StringComparison.Ordinal)
                || !string.Equals(
                    Compact(arguments[8]),
                    "probeAllocated",
                    StringComparison.Ordinal)
                || !string.Equals(
                    Compact(arguments[9]),
                    "tableAddress",
                    StringComparison.Ordinal)
                || !string.Equals(
                    Compact(arguments[10]),
                    "tablePageCount",
                    StringComparison.Ordinal)
                || !string.Equals(
                    Compact(arguments[11]),
                    "tableAllocated",
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateCleanupImplementation(
        string failBefore)
    {
        string compact = Compact(failBefore);
        if (CountOccurrences(
                compact,
                "if(tableAllocated)") != 1
            || CountOccurrences(
                compact,
                "if(probeAllocated)") != 1
            || CountOccurrences(
                compact,
                "if(codeAllocated)") != 1)
        {
            return false;
        }

        List<(int Index, string[] Arguments)> calls =
            FindInvocationArgumentsWithIndices(
                failBefore,
                "FreePages");
        if (calls.Count != 3
            || !InvocationArgumentsEqual(
                calls[0].Arguments,
                "tableAddress",
                "tablePageCount")
            || !InvocationArgumentsEqual(
                calls[1].Arguments,
                "probeAddress",
                "ProbePageCount")
            || !InvocationArgumentsEqual(
                calls[2].Arguments,
                "codeAddress",
                "CodePageCount"))
        {
            return false;
        }

        int returnIndex = failBefore.IndexOf(
            "return false;",
            StringComparison.Ordinal);
        return calls[0].Index < calls[1].Index
            && calls[1].Index < calls[2].Index
            && returnIndex > calls[2].Index
            && CountOccurrences(
                compact,
                "returnfalse;") == 1;
    }

    private static int FindLastFlexibleCallIndexBefore(
        string source,
        string methodName,
        int before)
    {
        if (before < 0)
        {
            return -1;
        }

        string[] parts = methodName.Split('.');
        string pattern = string.Join(
            @"\s*\.\s*",
            parts.Select(Regex.Escape));
        int result = -1;
        foreach (Match match in Regex.Matches(
            source,
            pattern + @"\s*\(",
            RegexOptions.CultureInvariant))
        {
            if (match.Index >= before)
            {
                break;
            }

            result = match.Index;
        }

        return result;
    }

    private static int FindInvocationEnd(
        string source,
        int invocationIndex)
    {
        if (invocationIndex < 0)
        {
            return -1;
        }

        string sanitized = SanitizeStringsAndCharacters(source);
        int open = sanitized.IndexOf('(', invocationIndex);
        int close = FindMatchingDelimiter(
            sanitized,
            open,
            '(',
            ')');
        if (close < 0)
        {
            return -1;
        }

        int semicolon = sanitized.IndexOf(';', close);
        return semicolon >= 0 ? semicolon + 1 : close + 1;
    }

    private static int CountExactIf(
        string source,
        string symbol)
    {
        return Regex.Matches(
            source,
            @"(?m)^[ \t]*#if[ \t]+"
                + Regex.Escape(symbol)
                + @"[ \t]*$",
            RegexOptions.CultureInvariant).Count;
    }

    private static bool TestKnownFalsePanicSelector()
    {
        string fixture = "#if " + UnexpectedPanicSymbol + "\nPanicOnly();\n#else\nPanicOff();\n#endif\n"
            + "#if " + StageOneSymbol + "\n#if " + UnexpectedPanicSymbol
            + "\nNestedPanic();\n#else\nNestedOff();\n#endif\n#else\nStageOff();\n#endif\n"
            + "#if UNRELATED_PROBE\nUnknown();\n#else\nUnknownElse();\n#endif\n";
        for (int mask = 0; mask < 8; mask++)
        {
            bool stageOne = (mask & 1) != 0;
            string selected = SelectProfile(fixture, stageOne, (mask & 2) != 0, (mask & 4) != 0);
            if (selected.Contains("PanicOnly();", StringComparison.Ordinal)
                || selected.Contains("NestedPanic();", StringComparison.Ordinal)
                || selected.Contains("#if " + UnexpectedPanicSymbol, StringComparison.Ordinal)
                || CountOccurrences(selected, "PanicOff();") != 1
                || selected.Contains("NestedOff();", StringComparison.Ordinal) != stageOne
                || selected.Contains("StageOff();", StringComparison.Ordinal) == stageOne
                || !selected.Contains("#if UNRELATED_PROBE", StringComparison.Ordinal)
                || !selected.Contains("Unknown();", StringComparison.Ordinal)
                || !selected.Contains("UnknownElse();", StringComparison.Ordinal))
            {
                return Failure("known-false-panic-selector=" + mask);
            }
        }

        string aliased = fixture.Replace(UnexpectedPanicSymbol,
            UnexpectedPanicSymbol.ToLowerInvariant(), StringComparison.Ordinal);
        string alias = SelectProfile(aliased, false, false, false);
        return alias.Contains("#if " + UnexpectedPanicSymbol.ToLowerInvariant(), StringComparison.Ordinal)
            && alias.Contains("PanicOnly();", StringComparison.Ordinal)
            && alias.Contains("PanicOff();", StringComparison.Ordinal)
            || Failure("panic-selector-ordinal-preservation");
    }

    private static string SelectProfile(
        string source,
        bool stageOneEnabled,
        bool stageTwoEnabled,
        bool stageThreeEnabled)
    {
        StringBuilder output = new();
        Stack<PreprocessorFrame> stack = new();
        bool emit = true;
        foreach (string line in NormalizeLineEndings(source)
            .Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
            {
                string condition = trimmed[4..].Trim();
                bool selected =
                    string.Equals(
                        condition,
                        StageOneSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(
                        condition,
                        StageTwoSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(
                        condition,
                        StageThreeSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(condition, UnexpectedPanicSymbol, StringComparison.Ordinal);
                bool selectedValue =
                    string.Equals(
                        condition,
                        StageOneSymbol,
                        StringComparison.Ordinal)
                        ? stageOneEnabled
                        : string.Equals(
                            condition,
                            StageTwoSymbol,
                            StringComparison.Ordinal)
                            ? stageTwoEnabled
                            : string.Equals(condition, StageThreeSymbol, StringComparison.Ordinal)
                                && stageThreeEnabled;
                stack.Push(
                    new PreprocessorFrame(
                        selected,
                        emit,
                        selectedValue,
                        SeenElse: false));
                if (selected)
                {
                    emit = emit && selectedValue;
                }
                else if (emit)
                {
                    output.AppendLine(line);
                }

                continue;
            }

            if (string.Equals(
                    trimmed,
                    "#else",
                    StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Orphan #else.");
                }

                PreprocessorFrame frame = stack.Pop();
                if (frame.SeenElse)
                {
                    throw new InvalidOperationException(
                        "Duplicate #else.");
                }

                frame = frame with { SeenElse = true };
                stack.Push(frame);
                if (frame.IsSelected)
                {
                    emit = frame.ParentEmit
                        && !frame.SelectedValue;
                }
                else if (emit)
                {
                    output.AppendLine(line);
                }

                continue;
            }

            if (string.Equals(
                    trimmed,
                    "#endif",
                    StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Orphan #endif.");
                }

                PreprocessorFrame frame = stack.Pop();
                if (frame.IsSelected)
                {
                    emit = frame.ParentEmit;
                }
                else if (emit)
                {
                    output.AppendLine(line);
                }

                continue;
            }

            if (emit)
            {
                output.AppendLine(line);
            }
        }

        if (stack.Count != 0)
        {
            throw new InvalidOperationException(
                "Unclosed preprocessor directive.");
        }

        return output.ToString();
    }

    private static string LoadEnabledStageThreeType()
    {
        string source = LoadEfiEntrySource();
        string profile = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: true,
            stageThreeEnabled: true);
        return FindTypeBody(profile, "M01StageThreeAdapter")
            ?? throw new InvalidOperationException(
                "Could not locate enabled stage-three adapter.");
    }

    private static string? FindTypeBody(
        string source,
        string typeName)
    {
        Match match = Regex.Match(
            source,
            @"\bclass\s+" + Regex.Escape(typeName) + @"\b",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        int open = source.IndexOf(
            '{',
            match.Index + match.Length);
        int close = FindMatchingDelimiter(
            source,
            open,
            '{',
            '}');
        return open >= 0 && close > open
            ? source[(open + 1)..close]
            : null;
    }

    private static string? FindMethodBody(
        string source,
        string methodName)
    {
        int search = 0;
        while (search < source.Length)
        {
            int name = source.IndexOf(
                methodName,
                search,
                StringComparison.Ordinal);
            if (name < 0)
            {
                return null;
            }

            int before = name - 1;
            int after = name + methodName.Length;
            if ((before >= 0
                    && IsIdentifierPart(source[before]))
                || (after < source.Length
                    && IsIdentifierPart(source[after])))
            {
                search = after;
                continue;
            }

            int openParen = SkipWhitespace(source, after);
            if (openParen >= source.Length
                || source[openParen] != '(')
            {
                search = after;
                continue;
            }

            int closeParen = FindMatchingDelimiter(
                source,
                openParen,
                '(',
                ')');
            int openBrace = SkipWhitespace(
                source,
                closeParen + 1);
            if (closeParen < 0
                || openBrace >= source.Length
                || source[openBrace] != '{')
            {
                search = after;
                continue;
            }

            int closeBrace = FindMatchingDelimiter(
                source,
                openBrace,
                '{',
                '}');
            return closeBrace > openBrace
                ? source[(openBrace + 1)..closeBrace]
                : null;
        }

        return null;
    }

    private static List<string[]> FindInvocationArguments(
        string source,
        string methodName)
    {
        return FindInvocationArgumentsWithIndices(
                source,
                methodName)
            .Select(item => item.Arguments)
            .ToList();
    }

    private static List<(int Index, string[] Arguments)>
        FindInvocationArgumentsWithIndices(
            string source,
            string methodName)
    {
        string sanitized =
            SanitizeStringsAndCharacters(source);
        List<(int Index, string[] Arguments)> results = [];
        int search = 0;
        while (search < sanitized.Length)
        {
            int name = sanitized.IndexOf(
                methodName,
                search,
                StringComparison.Ordinal);
            if (name < 0)
            {
                break;
            }

            int before = PreviousNonWhitespace(
                sanitized,
                name - 1);
            int open = SkipWhitespace(
                sanitized,
                name + methodName.Length);
            if ((name > 0
                    && IsIdentifierPart(sanitized[name - 1]))
                || (name + methodName.Length
                        < sanitized.Length
                    && IsIdentifierPart(
                        sanitized[name + methodName.Length]))
                || open >= sanitized.Length
                || sanitized[open] != '('
                || (before >= 0
                    && sanitized[before] == '*'))
            {
                search = name + methodName.Length;
                continue;
            }

            int close = FindMatchingDelimiter(
                sanitized,
                open,
                '(',
                ')');
            if (close < 0)
            {
                break;
            }

            results.Add(
                (
                    name,
                    SplitTopLevelArguments(
                        source[(open + 1)..close])
                ));
            search = close + 1;
        }

        return results;
    }

    private static bool InvocationArgumentsEqual(
        string[] actual,
        params string[] expected)
    {
        if (actual.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    Compact(actual[index]),
                    expected[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] SplitTopLevelArguments(
        string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        string sanitized =
            SanitizeStringsAndCharacters(arguments);
        List<string> parts = [];
        int start = 0;
        int round = 0;
        int square = 0;
        int angle = 0;
        for (int index = 0;
            index < sanitized.Length;
            index++)
        {
            switch (sanitized[index])
            {
                case '(':
                    round++;
                    break;
                case ')':
                    round--;
                    break;
                case '[':
                    square++;
                    break;
                case ']':
                    square--;
                    break;
                case '<':
                    angle++;
                    break;
                case '>':
                    if (angle > 0)
                    {
                        angle--;
                    }

                    break;
                case ',' when round == 0
                    && square == 0
                    && angle == 0:
                    parts.Add(
                        arguments[start..index].Trim());
                    start = index + 1;
                    break;
            }
        }

        parts.Add(arguments[start..].Trim());
        return [.. parts];
    }

    private static int CountFlexibleQualifiedCalls(
        string source,
        string qualifiedName)
    {
        string[] parts = qualifiedName.Split('.');
        string pattern = string.Join(
            @"\s*\.\s*",
            parts.Select(Regex.Escape));
        return Regex.Matches(
            SanitizeStringsAndCharacters(source),
            pattern + @"\s*\(",
            RegexOptions.CultureInvariant).Count;
    }

    private static int FlexibleCallIndex(
        string source,
        string qualifiedName)
    {
        string[] parts = qualifiedName.Split('.');
        string pattern = string.Join(
            @"\s*\.\s*",
            parts.Select(Regex.Escape));
        Match match = Regex.Match(
            SanitizeStringsAndCharacters(source),
            pattern + @"\s*\(",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Index : -1;
    }

    private static int FindNthCallIndex(
        string source,
        string methodName,
        int ordinal)
    {
        List<(int Index, string[] Arguments)> calls =
            FindInvocationArgumentsWithIndices(
                source,
                methodName);
        return (uint)ordinal < (uint)calls.Count
            ? calls[ordinal].Index
            : -1;
    }

    private static int FindMatchingDelimiter(
        string source,
        int openIndex,
        char open,
        char close)
    {
        if (openIndex < 0 || openIndex >= source.Length)
        {
            return -1;
        }

        string sanitized =
            SanitizeStringsAndCharacters(source);
        int depth = 0;
        for (int index = openIndex;
            index < sanitized.Length;
            index++)
        {
            if (sanitized[index] == open)
            {
                depth++;
            }
            else if (sanitized[index] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static string SanitizeStringsAndCharacters(
        string source)
    {
        StringBuilder sanitized = new(source);
        bool inString = false;
        bool inCharacter = false;
        bool inLineComment = false;
        bool inBlockComment = false;
        bool escaped = false;
        for (int index = 0; index < source.Length; index++)
        {
            char value = source[index];
            char next =
                index + 1 < source.Length
                    ? source[index + 1]
                    : '\0';

            if (inLineComment)
            {
                if (value == '\r' || value == '\n')
                {
                    inLineComment = false;
                }
                else
                {
                    sanitized[index] = ' ';
                }

                continue;
            }

            if (inBlockComment)
            {
                sanitized[index] =
                    value == '\r' || value == '\n'
                        ? value
                        : ' ';
                if (value == '*' && next == '/')
                {
                    sanitized[index + 1] = ' ';
                    index++;
                    inBlockComment = false;
                }

                continue;
            }

            if (inString || inCharacter)
            {
                if (value == '\r' || value == '\n')
                {
                    inString = false;
                    inCharacter = false;
                    escaped = false;
                    continue;
                }

                sanitized[index] = ' ';
                if (escaped)
                {
                    escaped = false;
                }
                else if (value == '\\')
                {
                    escaped = true;
                }
                else if ((inString && value == '"')
                    || (inCharacter && value == '\''))
                {
                    inString = false;
                    inCharacter = false;
                }

                continue;
            }

            if (value == '/' && next == '/')
            {
                sanitized[index] = ' ';
                sanitized[index + 1] = ' ';
                index++;
                inLineComment = true;
            }
            else if (value == '/' && next == '*')
            {
                sanitized[index] = ' ';
                sanitized[index + 1] = ' ';
                index++;
                inBlockComment = true;
            }
            else if (value == '"')
            {
                sanitized[index] = ' ';
                inString = true;
            }
            else if (value == '\'')
            {
                sanitized[index] = ' ';
                inCharacter = true;
            }
        }

        return sanitized.ToString();
    }

    private static int SkipWhitespace(
        string source,
        int index)
    {
        while (index >= 0
            && index < source.Length
            && char.IsWhiteSpace(source[index]))
        {
            index++;
        }

        return index;
    }

    private static int PreviousNonWhitespace(
        string source,
        int index)
    {
        while (index >= 0
            && char.IsWhiteSpace(source[index]))
        {
            index--;
        }

        return index;
    }

    private static bool IsIdentifierPart(char value)
    {
        return char.IsLetterOrDigit(value)
            || value == '_';
    }

    private static string Compact(string source)
    {
        return Regex.Replace(
            SanitizeStringsAndCharacters(source),
            @"\s+",
            string.Empty,
            RegexOptions.CultureInvariant);
    }

    private static string NormalizeLineEndings(
        string value)
    {
        return value.Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int search = 0;
        while ((search = source.IndexOf(
            value,
            search,
            StringComparison.Ordinal)) >= 0)
        {
            count++;
            search += value.Length;
        }

        return count;
    }

    private static string ReplaceFirst(
        string source,
        string oldText,
        string newText)
    {
        int index = source.IndexOf(
            oldText,
            StringComparison.Ordinal);
        if (index < 0)
        {
            throw new InvalidOperationException(
                "Mutation anchor not found: " + oldText);
        }

        return source[..index]
            + newText
            + source[(index + oldText.Length)..];
    }

    private static string LoadEfiEntrySource()
    {
        string repositoryRoot =
            HostToolProcess.FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "Boot",
                "EfiEntry.cs"));
    }

    private static string LoadOsProjectSource()
    {
        string repositoryRoot =
            HostToolProcess.FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "OS.csproj"));
    }

    private static bool IsGeneratedOrReferencePath(
        string fullPath)
    {
        string normalized = fullPath.Replace(
            Path.AltDirectorySeparatorChar,
            Path.DirectorySeparatorChar);
        string separator =
            Path.DirectorySeparatorChar.ToString();
        string[] excluded =
        [
            separator + "bin" + separator,
            separator + "obj" + separator,
            separator + "artifacts" + separator,
            separator + "references" + separator,
            separator + ".git" + separator
        ];
        return excluded.Any(
            token => normalized.Contains(
                token,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool Failure(string reason)
    {
        Console.WriteLine(
            "TEST_FAIL "
            + IssueTestName
            + " detail="
            + reason);
        return false;
    }

    private readonly record struct PreprocessorFrame(
        bool IsSelected,
        bool ParentEmit,
        bool SelectedValue,
        bool SeenElse);

    private enum AdapterAllocationOutcome : byte
    {
        NotAttempted = 0,
        FirmwareFailure = 1,
        SuccessRejected = 2,
        SuccessAccepted = 3
    }

    private readonly record struct AllocationCleanupScenario(
        string Name,
        AdapterAllocationOutcome Code,
        AdapterAllocationOutcome Probe,
        AdapterAllocationOutcome Table,
        bool FailAfterAllocations,
        string ExpectedCleanup);

    private readonly record struct CleanupOracleResult(
        string Attempts,
        int FailedAttemptCount);
}
