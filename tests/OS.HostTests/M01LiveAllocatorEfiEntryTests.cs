using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;

internal static class M01LiveAllocatorEfiEntryTests
{
    private const string MemoryMapSymbol = "M03_MEMORY_MAP_CAPTURE_PROBE";
    private const string TypeManagerSymbol = "M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE";
    private const string TypedEbsSymbol = "M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE";
    private const string GcStaticSymbol = "M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE";
    private const string EntropySymbol = "M03_FIRMWARE_ENTROPY_SEED_PROBE";
    private const string FirstExitSymbol = "M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE";
    private const string FirstExitPrerequisiteCondition =
        "!(M01_LOADED_IMAGE_STACK_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(M01_LIVE_ALLOCATOR_PROBE || CLROS_NORMAL_VERIFIED_EXIT) || !(ACPI_PHYSICAL_DISCOVERY_PROBE || CLROS_NORMAL_VERIFIED_EXIT)";
    private const string IssueTestName = "m01.023";
    private const string UnexpectedPanicSymbol =
        "M02_UNEXPECTED_VECTOR_PANIC_PROBE";
    private const string StageOneSymbol =
        "M01_LOADED_IMAGE_STACK_PROBE";
    private const string StageTwoSymbol =
        "M01_LIVE_ALLOCATOR_PROBE";
    private const string StageOneCondition = "(" + StageOneSymbol + " || CLROS_NORMAL_VERIFIED_EXIT)";
    private const string StageTwoCondition = "(" + StageTwoSymbol + " || CLROS_NORMAL_VERIFIED_EXIT)";

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);
        Run(
            "m01.023 profile matrix is exact default-off and stage-one-gated",
            TestProbeProfilesAndStageOneGate,
            ref failures);
        Run(
            "m01.023 arena allocation is one bounded loader-data transaction",
            TestArenaAllocationAndCleanupContract,
            ref failures);
        Run(
            "m01.023 post-allocation map recapture is fresh bounded and type-checked",
            TestPostAllocationMapRecaptureContract,
            ref failures);
        Run(
            "m01.023 canonical five-record ledger binds exactly one allocator arena",
            TestReservationLedgerContract,
            ref failures);
        Run(
            "m01.023 coordinator receives exact caller-owned buffers once",
            TestCoordinatorLinkageAndCallerOwnedBuffers,
            ref failures);
        Run(
            "m01.023 success requires final accounting and arena confinement",
            TestCompletedResultAndArenaConfinement,
            ref failures);
        Run(
            "m01.023 stage-two marker topology is exact and fail-closed",
            TestMarkerTopologyAndFailureSuppression,
            ref failures);
        Run(
            "m01.023 source firewalls preserve the prior EFI boundary",
            TestSourceFirewallsAndPriorCompatibility,
            ref failures);
        Run(
            "m01.023 contract validator rejects representative bypasses",
            TestRepresentativeMutations,
            ref failures);
        Run(
            "m01.023 issue marker is unique",
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
                "M01-023 EFI entry source-contract tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_023_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(
        string name,
        Func<bool> test,
        ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestProbeProfilesAndStageOneGate()
    {
        if (!TestKnownFalsePanicSelector())
        {
            return false;
        }

        string source = LoadEfiEntrySource();
        string project = LoadOsProjectSource();
        // The first-exit prerequisite rejects an invalid profile; it does not
        // enable another stage-two implementation block.
        int firstExitPrerequisiteBlocks = CountExactIf(source, FirstExitPrerequisiteCondition);
        if (CountExactIf(source, StageOneCondition) != 1
            || CountExactIf(source, StageTwoCondition) != 2
            || firstExitPrerequisiteBlocks != 1
            || CountExactIf(source, StageTwoCondition) + firstExitPrerequisiteBlocks != Regex.Matches(
                source,
                @"(?m)^[ \t]*#(?:if|elif)[^\r\n]*\b"
                    + Regex.Escape(StageTwoSymbol)
                    + @"\b[^\r\n]*$",
                RegexOptions.CultureInvariant).Count)
        {
            return Failure("guard-shape");
        }

        string profile00 = SelectProfile(
            source,
            stageOneEnabled: false,
            stageTwoEnabled: false);
        string profile10 = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: false);
        string profile01 = SelectProfile(
            source,
            stageOneEnabled: false,
            stageTwoEnabled: true);
        string profile11 = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: true);

        string stageTwoStub = FindTypeBody(
            profile00,
            "M01StageTwoAdapter")
            ?? string.Empty;
        string stageOneEnabled = FindTypeBody(
            profile10,
            "M01StageOneAdapter")
            ?? string.Empty;
        string stageOneDisabled = FindTypeBody(
            profile01,
            "M01StageOneAdapter")
            ?? string.Empty;
        string continuation = FindMethodBody(
            FindTypeBody(profile11, "M01StageOneAdapter")
                ?? string.Empty,
            "M01StageOneContinuation")
            ?? string.Empty;
        string stubBody = FindMethodBody(
            stageTwoStub,
            "RunAfterStageOne")
            ?? string.Empty;
        string stageOneFallback = FindMethodBody(
            stageOneDisabled,
            "Run")
            ?? string.Empty;
        string prepare = FindMethodBody(
            stageOneEnabled,
            "PrepareAndTransfer")
            ?? string.Empty;
        List<string[]> stageTwoCalls =
            FindInvocationArguments(
                continuation,
                "RunAfterStageOne");

        if (!string.Equals(
                Compact(stubBody),
                "returnfalse;",
                StringComparison.Ordinal)
            || !string.Equals(
                Compact(stageOneFallback),
                "returnfalse;",
                StringComparison.Ordinal)
            || CountFlexibleQualifiedCalls(
                profile00,
                "M01StageTwoAdapter.RunAfterStageOne") != 0
            || CountFlexibleQualifiedCalls(
                profile10,
                "M01StageTwoAdapter.RunAfterStageOne") != 0
            || CountFlexibleQualifiedCalls(
                profile01,
                "M01StageTwoAdapter.RunAfterStageOne") != 0
            || CountFlexibleQualifiedCalls(
                profile11,
                "M01StageTwoAdapter.RunAfterStageOne") != 1
            || stageOneEnabled.Length == 0
            || continuation.Length == 0
            || prepare.Length == 0
            || stageTwoCalls.Count != 1
            || stageTwoCalls[0].Length != 8)
        {
            return Failure("profile-selection");
        }

        string[] expectedStageTwoArguments =
        [
            "(void*)staticState",
            "M01LoadedImageStackStaticState.RequiredStorageBytes",
            "s_bootServices",
            "s_console",
            "s_serial",
            "&stageOneLedgerStorage->Range0",
            "ReservationCount",
            "ReservationCapacity"
        ];
        for (int index = 0;
            index < expectedStageTwoArguments.Length;
            index++)
        {
            if (!string.Equals(
                    Compact(stageTwoCalls[0][index]),
                    expectedStageTwoArguments[index],
                    StringComparison.Ordinal))
            {
                return Failure("stage-two-call-argument=" + index);
            }
        }

        string prepareCompact = Compact(prepare);
        int consoleCapture = prepareCompact.IndexOf(
            "s_console=console;",
            StringComparison.Ordinal);
        int serialCapture = prepareCompact.IndexOf(
            "s_serial=serial;",
            StringComparison.Ordinal);
        int bootServicesCapture = prepareCompact.IndexOf(
            "s_bootServices=bootServices;",
            StringComparison.Ordinal);
        int transferClear = prepareCompact.IndexOf(
            "transfer=default;",
            StringComparison.Ordinal);
        int stackSwitch = prepareCompact.IndexOf(
            "switchEntry(",
            StringComparison.Ordinal);
        if (CountOccurrences(
                prepareCompact,
                "s_bootServices=bootServices;") != 1
            || consoleCapture < 0
            || serialCapture <= consoleCapture
            || bootServicesCapture <= serialCapture
            || transferClear <= bootServicesCapture
            || stackSwitch <= transferClear
            || !Compact(continuation).Contains(
                "stageOneLedgerStorage=&s_reservationLedger",
                StringComparison.Ordinal))
        {
            return Failure("firmware-boundary-capture-or-call-chain");
        }

        int observation = FlexibleCallIndex(
            continuation,
            "M01LoadedImageStackIntegration"
                + ".ObserveContinuationRspFromStaticState");
        int stageOneRetrospective =
            continuation.IndexOf(
                "M01StageOneSuccessLineCount",
                StringComparison.Ordinal);
        int stageTwoCall = FlexibleCallIndex(
            continuation,
            "M01StageTwoAdapter.RunAfterStageOne");
        int defaultPlan = continuation.IndexOf(
            "WriteBootSequencePlan",
            StringComparison.Ordinal);
        string continuationCompact = Compact(continuation);
        if (observation < 0
            || stageOneRetrospective <= observation
            || stageTwoCall <= stageOneRetrospective
            || defaultPlan <= stageTwoCall
            || !continuationCompact.Contains(
                "if(!M01StageTwoAdapter.RunAfterStageOne(",
                StringComparison.Ordinal)
            || !Regex.IsMatch(
                continuation,
                @"if\s*\(\s*!\s*M01StageTwoAdapter\s*\.\s*"
                    + @"RunAfterStageOne\s*\([\s\S]*?\)\s*\)"
                    + @"\s*\{\s*Spin\s*\(\s*\)\s*;\s*\}",
                RegexOptions.CultureInvariant))
        {
            return Failure("continuation-order-or-failure-suppression");
        }

        string bridge = FindMethodBody(
            FindTypeBody(profile11, "M01StageOneAdapter")
                ?? string.Empty,
            "IsFinalStageOneSuccess")
            ?? string.Empty;
        string run = FindMethodBody(
            FindTypeBody(profile01, "M01StageTwoAdapter")
                ?? string.Empty,
            "RunAfterStageOne")
            ?? string.Empty;
        int finalToken = FlexibleCallIndex(
            run,
            "M01StageOneAdapter.IsFinalStageOneSuccess");
        int allocation = run.IndexOf(
            "bootServices->AllocatePages(",
            StringComparison.Ordinal);
        string bridgeCompact = Compact(bridge);
        if (finalToken < 0
            || allocation <= finalToken
            || !bridgeCompact.Contains(
                "stageOneState==null",
                StringComparison.Ordinal)
            || !bridgeCompact.Contains(
                "RequiredStorageAlignment-1",
                StringComparison.Ordinal)
            || !bridgeCompact.Contains(
                "stageOneStateCapacity!=requiredCapacity",
                StringComparison.Ordinal)
            || !bridgeCompact.Contains(
                "stateAddress>~0UL-stageOneStateCapacity",
                StringComparison.Ordinal)
            || !bridgeCompact.Contains(
                "state->IsInitialized",
                StringComparison.Ordinal)
            || !bridgeCompact.Contains(
                "state==expectedState",
                StringComparison.Ordinal)
            || !bridgeCompact.Contains(
                "ContinuationRspContained",
                StringComparison.Ordinal)
            || !bridgeCompact.Contains(
                "ContinuationObserved",
                StringComparison.Ordinal)
            || !bridgeCompact.Contains(
                "OwnedBootStackLifetime.Active",
                StringComparison.Ordinal)
            || !project.Contains(
                "EnableM01LiveAllocatorProbe)' == 'true' and "
                    + "'$(EnableM01LoadedImageStackProbe)' != 'true'",
                StringComparison.Ordinal))
        {
            return Failure("stage-one-token-or-project-gate");
        }

        return true;
    }

    private static bool TestArenaAllocationAndCleanupContract()
    {
        string stageTwo = LoadEnabledStageTwoType();
        string run = FindMethodBody(
            stageTwo,
            "RunAfterStageOne")
            ?? string.Empty;
        string fail = FindMethodBody(stageTwo, "Fail")
            ?? string.Empty;
        string compact = Compact(stageTwo);
        string runCompact = Compact(run);
        string failCompact = Compact(fail);
        List<string[]> allocations =
            FindInvocationArguments(run, "AllocatePages");
        List<string[]> frees =
            FindInvocationArguments(stageTwo, "FreePages");

        if (allocations.Count != 1
            || allocations[0].Length != 4
            || Compact(allocations[0][0]) != "AllocateAnyPages"
            || Compact(allocations[0][1]) != "EfiLoaderData"
            || Compact(allocations[0][2])
                != "AllocatorArenaPageCount"
            || Compact(allocations[0][3]) != "&arenaAddress"
            || frees.Count != 1
            || frees[0].Length != 2
            || Compact(frees[0][0]) != "arenaAddress"
            || Compact(frees[0][1])
                != "AllocatorArenaPageCount")
        {
            return Failure("allocate-or-free-shape");
        }

        Match pageCount = Regex.Match(
            compact,
            @"privateconstulongAllocatorArenaPageCount=(?<pages>\d+);",
            RegexOptions.CultureInvariant);
        if (!pageCount.Success
            || !ulong.TryParse(
                pageCount.Groups["pages"].Value,
                out ulong pages)
            || pages < 2
            || pages > 4096
            || !compact.Contains(
                "privateconstuintAllocateAnyPages=0;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstuintEfiLoaderData=2;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongPageSize=4096;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongArenaAllocationOrdinal=5;",
                StringComparison.Ordinal))
        {
            return Failure("bounded-constants");
        }

        if (!runCompact.Contains(
                "boolarenaAllocated="
                    + "arenaFirmwareStatus==EfiSuccess;",
                StringComparison.Ordinal)
            || runCompact.Contains(
                "arenaAllocated=arenaFirmwareStatus"
                    + "==EfiSuccess&&",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "TryValidateStageTwoAllocation("
                    + "arenaAddress,AllocatorArenaPageCount,"
                    + "outulongarenaByteLength,"
                    + "outulongarenaEndExclusive)",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "if(!IsZeroedStaticState(staticState))",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "newBootMemoryRegionRecord("
                    + "EfiLoaderData,arenaAddress,arenaAddress,"
                    + "AllocatorArenaPageCount,0,"
                    + "arenaByteLength,arenaEndExclusive)",
                StringComparison.Ordinal)
            || !failCompact.Contains(
                "if(arenaAllocated&&bootServices!=null"
                    + "&&bootServices->FreePages!=null)",
                StringComparison.Ordinal)
            || compact.Contains(
                "ConventionalMemory",
                StringComparison.Ordinal)
            || compact.Contains(
                "UefiConventionalMemoryType",
                StringComparison.Ordinal))
        {
            return Failure("ownership-or-record-contract");
        }

        int defaultStateCheck = runCompact.IndexOf(
            "if(!IsZeroedStaticState(staticState))",
            StringComparison.Ordinal);
        int allocationCall = runCompact.IndexOf(
            "bootServices->AllocatePages(",
            StringComparison.Ordinal);
        int entryStateReset = runCompact.IndexOf(
            "*staticState=default;",
            StringComparison.Ordinal);
        string zeroCheck = Compact(
            FindMethodBody(stageTwo, "IsZeroedStaticState")
                ?? string.Empty);
        if (defaultStateCheck < 0
            || allocationCall <= defaultStateCheck
            || (entryStateReset >= 0
                && entryStateReset < allocationCall)
            || !string.Equals(
                zeroCheck,
                "if(staticState==null){returnfalse;}"
                    + "byte*bytes=(byte*)staticState;"
                    + "for(ulongindex=0;"
                    + "index<M01LiveAllocatorStaticState"
                    + ".RequiredStorageBytes;index++){"
                    + "if(bytes[index]!=0){returnfalse;}}"
                    + "returntrue;",
                StringComparison.Ordinal))
        {
            return Failure("durable-state-reentry-guard");
        }

        string sharedValidator = FindMethodBody(
            LoadEfiEntrySource(),
            "TryValidateAllocation")
            ?? string.Empty;
        string validatorCompact = Compact(sharedValidator);
        return validatorCompact.Contains(
                "address==0",
                StringComparison.Ordinal)
            && validatorCompact.Contains(
                "(address&(PageSize-1))!=0",
                StringComparison.Ordinal)
            && validatorCompact.Contains(
                "pageCount==0",
                StringComparison.Ordinal)
            && validatorCompact.Contains(
                "pageCount>~0UL/PageSize",
                StringComparison.Ordinal)
            && validatorCompact.Contains(
                "address>~0UL-byteLength",
                StringComparison.Ordinal)
            && validatorCompact.Contains(
                "endExclusive>address",
                StringComparison.Ordinal)
            || Failure("shared-allocation-overflow-guards");
    }

    private static bool TestPostAllocationMapRecaptureContract()
    {
        string source = LoadEfiEntrySource();
        string stageTwo = LoadEnabledStageTwoType();
        string run = FindMethodBody(
            stageTwo,
            "RunAfterStageOne")
            ?? string.Empty;
        string stageOne = FindTypeBody(
            SelectProfile(
                source,
                stageOneEnabled: true,
                stageTwoEnabled: true),
            "M01StageOneAdapter")
            ?? string.Empty;
        string allocationBridge = Compact(
            FindMethodBody(
                stageOne,
                "TryValidateStageTwoAllocation")
                ?? string.Empty);
        string recaptureBridge = Compact(
            FindMethodBody(
                stageOne,
                "TryRecaptureStageTwoMemoryMap")
                ?? string.Empty);
        string typeBridge = Compact(
            FindMethodBody(
                stageOne,
                "StageTwoMapContainsAllocation")
                ?? string.Empty);
        int previousKey = run.IndexOf(
            "previousMapKey",
            StringComparison.Ordinal);
        int allocation = run.IndexOf(
            "bootServices->AllocatePages(",
            StringComparison.Ordinal);
        int recapture = run.IndexOf(
            "TryRecaptureStageTwoMemoryMap",
            StringComparison.Ordinal);
        int unchanged = run.IndexOf(
            "refreshedMap.MapKey == previousMapKey",
            StringComparison.Ordinal);
        int typeCheck = run.IndexOf(
            "StageTwoMapContainsAllocation",
            StringComparison.Ordinal);
        int allocationEvidence = run.IndexOf(
            "CaptureCallerAttested",
            StringComparison.Ordinal);
        string compact = Compact(stageTwo);

        if (!string.Equals(
                allocationBridge,
                "returnTryValidateAllocation(address,pageCount,"
                    + "outbyteLength,outendExclusive);",
                StringComparison.Ordinal)
            || !string.Equals(
                recaptureBridge,
                "M01StageOneAdapterStatusstatus="
                    + "TryRecaptureMemoryMap(bootServices,"
                    + "outrefreshedMap,outfirmwareStatus);"
                    + "returnstatus=="
                    + "M01StageOneAdapterStatus.Success;",
                StringComparison.Ordinal)
            || !string.Equals(
                typeBridge,
                "returnMapContainsAllocation(map,targetStart,"
                    + "targetEndExclusive,expectedType);",
                StringComparison.Ordinal)
            || previousKey < 0
            || allocation <= previousKey
            || recapture <= allocation
            || unchanged <= recapture
            || typeCheck <= unchanged
            || allocationEvidence <= typeCheck
            || CountFlexibleQualifiedCalls(
                stageTwo,
                "M01StageOneAdapter"
                    + ".TryRecaptureStageTwoMemoryMap") != 1
            || CountFlexibleQualifiedCalls(
                stageTwo,
                "M01StageOneAdapter"
                    + ".StageTwoMapContainsAllocation") != 1
            || !Compact(run).Contains(
                "StageTwoMapContainsAllocation("
                    + "refreshedMap,arenaAddress,"
                    + "arenaEndExclusive,EfiLoaderData)",
                StringComparison.Ordinal)
            || compact.Contains(
                "AllocatePool",
                StringComparison.Ordinal)
            || compact.Contains(
                "GetMemoryMap(",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongMapCaptureOrdinal=6;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongLedgerBuildOrdinal=7;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongPreviousMapGeneration=2;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongRefreshedMapGeneration=3;",
                StringComparison.Ordinal))
        {
            return Failure("recapture-order-or-evidence");
        }

        string shared = FindMethodBody(
            source,
            "TryRecaptureMemoryMap")
            ?? string.Empty;
        string sharedCompact = Compact(shared);
        return sharedCompact.Contains(
                "ulongcapacity=OwnedMemoryMapBufferSize;",
                StringComparison.Ordinal)
            && sharedCompact.Contains(
                "sourceAddress>~0UL-capacity",
                StringComparison.Ordinal)
            && sharedCompact.Contains(
                "destinationAddress>~0UL-capacity",
                StringComparison.Ordinal)
            && sharedCompact.Contains(
                "RangesOverlap(",
                StringComparison.Ordinal)
            && sharedCompact.Contains(
                "firmwareStatus=bootServices->GetMemoryMap(",
                StringComparison.Ordinal)
            && sharedCompact.Contains(
                "if(firmwareStatus!=EfiSuccess)",
                StringComparison.Ordinal)
            && sharedCompact.Contains(
                "UefiMemoryMapCopyPlanResult.Create(",
                StringComparison.Ordinal)
            && sharedCompact.Contains(
                "EfiMemoryMapNormalizer.Normalize(",
                StringComparison.Ordinal)
            && sharedCompact.IndexOf(
                "EfiEntry.s_bootHandoff.MemoryMap=refreshedMap;",
                StringComparison.Ordinal)
                > sharedCompact.IndexOf(
                    "EfiMemoryMapNormalizer.Normalize(",
                    StringComparison.Ordinal)
            || Failure("shared-map-growth-overflow-contract");
    }

    private static bool TestReservationLedgerContract()
    {
        string stageTwo = LoadEnabledStageTwoType();
        string run = FindMethodBody(
            stageTwo,
            "RunAfterStageOne")
            ?? string.Empty;
        string build = FindMethodBody(
            stageTwo,
            "BuildReservationLedger")
            ?? string.Empty;
        string stageOneValidation = FindMethodBody(
            stageTwo,
            "ValidateStageOneReservationStorage")
            ?? string.Empty;
        string compact = Compact(stageTwo);
        string runCompact = Compact(run);
        string buildCompact = Compact(build);
        string stageOneValidationCompact =
            Compact(stageOneValidation);
        int rangeFields = Regex.Matches(
            stageTwo,
            @"public\s+OS\.Kernel\.Memory\.BootReservedRange"
                + @"\s+Range[0-4]\s*;",
            RegexOptions.CultureInvariant).Count;

        if (rangeFields != 5
            || !compact.Contains(
                "privateconstulongStageOneReservationCount=4;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongStageOneReservationCapacity=4;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongReservationCount=5;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "privateconstulongReservationCapacity=5;",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "ValidateStageOneReservationStorage("
                    + "stageOneReservationLedger,"
                    + "stageOneReservationCount,"
                    + "stageOneReservationCapacity)",
                StringComparison.Ordinal)
            || !stageOneValidationCompact.Contains(
                "reservationLedger==null",
                StringComparison.Ordinal)
            || !stageOneValidationCompact.Contains(
                "reservationCount!=StageOneReservationCount",
                StringComparison.Ordinal)
            || !stageOneValidationCompact.Contains(
                "reservationCapacity"
                    + "!=StageOneReservationCapacity",
                StringComparison.Ordinal)
            || !stageOneValidationCompact.Contains(
                "(address&(M01LiveAllocatorIntegration"
                    + ".RequiredPointerAlignment-1))!=0",
                StringComparison.Ordinal)
            || !stageOneValidationCompact.Contains(
                "address>~0UL-byteLength",
                StringComparison.Ordinal)
            || CountFlexibleQualifiedCalls(
                stageOneValidation,
                "OS.Kernel.Memory"
                    + ".BootReservationTableValidator.Validate") != 1
            || !stageOneValidationCompact.Contains(
                "BootReservationTableValidator.Validate("
                    + "reservationLedger,reservationCount,"
                    + "reservationCapacity)",
                StringComparison.Ordinal)
            || !stageOneValidationCompact.Contains(
                "validation.Succeeded",
                StringComparison.Ordinal)
            || !stageOneValidationCompact.Contains(
                "validation.ReservationCount"
                    + "==StageOneReservationCount",
                StringComparison.Ordinal)
            || !stageOneValidationCompact.Contains(
                "validation.AvailableReservationCapacity"
                    + "==StageOneReservationCapacity",
                StringComparison.Ordinal)
            || !buildCompact.Contains(
                "reservationLedger[index]="
                    + "stageOneReservationLedger[index];",
                StringComparison.Ordinal)
            || !buildCompact.Contains(
                "BootReservationKind.AllocatorArena",
                StringComparison.Ordinal)
            || !buildCompact.Contains(
                "reservationLedger[StageOneReservationCount]="
                    + "arenaReservation;",
                StringComparison.Ordinal)
            || !buildCompact.Contains(
                "PhysicalStart.TryCompareTo(",
                StringComparison.Ordinal)
            || !buildCompact.Contains(
                "BootReservationTableValidator.Validate("
                    + "reservationLedger,ReservationCount,"
                    + "ReservationCapacity)",
                StringComparison.Ordinal)
            || !buildCompact.Contains(
                "validation.ReservationCount==ReservationCount",
                StringComparison.Ordinal)
            || !buildCompact.Contains(
                "validation.AvailableReservationCapacity"
                    + "==ReservationCapacity",
                StringComparison.Ordinal))
        {
            return Failure("five-record-ledger");
        }

        return CountFlexibleQualifiedCalls(
                run,
                "M01LiveAllocatorMapLedgerEvidence"
                    + ".CaptureCallerAttested") == 1
            && runCompact.Contains(
                "reservationLedger,ReservationCount,"
                    + "ReservationCapacity",
                StringComparison.Ordinal)
            || Failure("same-ledger-evidence-and-core");
    }

    private static bool
        TestCoordinatorLinkageAndCallerOwnedBuffers()
    {
        string source = LoadEfiEntrySource();
        string profile11 = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: true);
        string stageOne = FindTypeBody(
            profile11,
            "M01StageOneAdapter")
            ?? string.Empty;
        string stageTwo = FindTypeBody(
            SelectProfile(
                source,
                stageOneEnabled: false,
                stageTwoEnabled: true),
            "M01StageTwoAdapter")
            ?? string.Empty;
        string bridge = FindMethodBody(
            stageOne,
            "ExecuteStageTwoCoordinator")
            ?? string.Empty;
        string run = FindMethodBody(
            stageTwo,
            "RunAfterStageOne")
            ?? string.Empty;
        List<string[]> coreCalls =
            FindInvocationArguments(bridge, "Execute");
        List<string[]> bridgeCalls =
            FindInvocationArguments(
                run,
                "ExecuteStageTwoCoordinator");

        if (CountFlexibleQualifiedCalls(
                source,
                "M01LiveAllocatorIntegration.Execute") != 1
            || source.Contains(
                "M01LiveAllocatorIntegration.Execute",
                StringComparison.Ordinal)
            || coreCalls.Count != 1
            || coreCalls[0].Length != 12
            || bridgeCalls.Count != 1
            || bridgeCalls[0].Length != 12)
        {
            return Failure("single-twelve-argument-link");
        }

        string[] expectedCore =
        [
            "(M01LoadedImageStackStaticState*)stageOneState",
            "stageOneStateCapacity",
            "allocationRecords",
            "allocationRecordCount",
            "allocationRecordCapacity",
            "allocationEvidence",
            "mapLedgerEvidence",
            "reservationLedger",
            "reservationCount",
            "reservationCapacity",
            "staticState",
            "staticStateCapacity"
        ];
        for (int index = 0; index < expectedCore.Length; index++)
        {
            if (!string.Equals(
                    Compact(coreCalls[0][index]),
                    expectedCore[index],
                    StringComparison.Ordinal))
            {
                return Failure("core-argument-" + index);
            }
        }

        string[] expectedBridge =
        [
            "stageOneState",
            "stageOneStateCapacity",
            "allocationRecord",
            "AllocationRecordCount",
            "AllocationRecordCapacity",
            "allocationEvidence",
            "mapLedgerEvidence",
            "reservationLedger",
            "ReservationCount",
            "ReservationCapacity",
            "staticState",
            "M01LiveAllocatorStaticState.RequiredStorageBytes"
        ];
        for (int index = 0; index < expectedBridge.Length; index++)
        {
            if (!string.Equals(
                    Compact(bridgeCalls[0][index]),
                    expectedBridge[index],
                    StringComparison.Ordinal))
            {
                return Failure("bridge-argument-" + index);
            }
        }

        string forbidden =
            stageOne + Environment.NewLine + stageTwo;
        string[] forbiddenCalls =
        [
            "RunM01LiveAllocatorIntegration",
            "BootReservationSubtractor.Subtract",
            "BootPageAllocatorStateMaterializer.Materialize",
            "BootPageAllocatorMutator.AllocateOnePage",
            "BootPageAllocatorMutator.FreeOnePage"
        ];
        foreach (string call in forbiddenCalls)
        {
            if (forbidden.Contains(call, StringComparison.Ordinal))
            {
                return Failure("forbidden-call=" + call);
            }
        }

        return true;
    }

    private static bool TestCompletedResultAndArenaConfinement()
    {
        string stageTwo = LoadEnabledStageTwoType();
        string run = FindMethodBody(
            stageTwo,
            "RunAfterStageOne")
            ?? string.Empty;
        string validation = FindMethodBody(
            stageTwo,
            "ValidateCompletedResult")
            ?? string.Empty;
        string compact = Compact(validation);
        int validationCall = run.IndexOf(
            "ValidateCompletedResult",
            StringComparison.Ordinal);
        int successLoop = run.IndexOf(
            "for (int lineIndex = 1;",
            StringComparison.Ordinal);

        string[] required =
        [
            "result==null",
            "!result->Succeeded",
            "result->Status!=M01LiveAllocatorStatus.Success",
            "DoubleFreeRejected",
            "M01LiveAllocatorStaticStateresultState="
                + "result->StaticState;",
            "!staticState->IsCompleted",
            "!resultState.IsCompleted",
            "!StaticStatesEqual(staticState,&resultState)",
            "result->Evidence.StageOneStateAddress!=(ulong)stageOneState",
            "result->Evidence.StageOneStateCapacity!=stageOneStateCapacity",
            "result->Evidence.StaticStateAddress!=(ulong)staticState",
            "result->Evidence.ArenaReservation.Equals(arenaReservation)",
            "resultState.ArenaReservation.Equals(arenaReservation)",
            "FreeRangeNotAllocated",
            "!result->Evidence.Free.Succeeded",
            "!result->Evidence.DoubleFree.IsInitialized",
            "result->Evidence.DoubleFree.Succeeded",
            "freeNode.PhysicalStart!=arenaAddress",
            "freeNode.PhysicalEndExclusive!=arenaEndExclusive",
            "freeNode.PageCount!=AllocatorArenaPageCount",
            "freeNode.ByteLength!=arenaByteLength",
            "allocatorState.FreePageCount!=AllocatorArenaPageCount",
            "allocatorState.FreeByteCount!=arenaByteLength",
            "allocatorState.AllocatedPageCount!=0",
            "allocatorState.AllocatedByteCount!=0",
            "allocatedStart<arenaAddress",
            "allocatedStart>~0UL-PageSize",
            "allocatedEnd!=allocatedStart+PageSize",
            "allocatedEnd>arenaEndExclusive"
        ];
        foreach (string token in required)
        {
            if (!compact.Contains(
                    Compact(token),
                    StringComparison.Ordinal))
            {
                return Failure("completed-token=" + token);
            }
        }

        string stateEquality = Compact(
            FindMethodBody(stageTwo, "StaticStatesEqual")
                ?? string.Empty);
        return validationCall >= 0
            && successLoop > validationCall
            && Compact(run).Contains(
                "ValidateCompletedResult(&result,",
                StringComparison.Ordinal)
            && Compact(run).Contains(
                "result=default;*staticState=default;returnFail(",
                StringComparison.Ordinal)
            && string.Equals(
                stateEquality,
                "if(left==null||right==null){returnfalse;}"
                    + "byte*leftBytes=(byte*)left;"
                    + "byte*rightBytes=(byte*)right;"
                    + "for(ulongindex=0;"
                    + "index<M01LiveAllocatorStaticState"
                    + ".RequiredStorageBytes;index++){"
                    + "if(leftBytes[index]!=rightBytes[index]){"
                    + "returnfalse;}}returntrue;",
                StringComparison.Ordinal)
            || Failure("success-before-final-validation");
    }

    private static bool TestMarkerTopologyAndFailureSuppression()
    {
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
        if (BootSequencePlan.M01StageTwoSuccessLineCount
            != expected.Length)
        {
            return Failure("plan-count");
        }

        for (int lineIndex = 0;
            lineIndex < expected.Length;
            lineIndex++)
        {
            StringBuilder line = new();
            int length =
                BootSequencePlan
                    .GetM01StageTwoSuccessLineLength(lineIndex);
            for (int offset = 0; offset < length; offset++)
            {
                line.Append(
                    (char)BootSequencePlan
                        .GetM01StageTwoSuccessLineByte(
                            lineIndex,
                            offset));
            }

            if (!string.Equals(
                    line.ToString(),
                    expected[lineIndex],
                    StringComparison.Ordinal))
            {
                return Failure("plan-line=" + lineIndex);
            }
        }

        string stageTwo = LoadEnabledStageTwoType();
        string run = FindMethodBody(
            stageTwo,
            "RunAfterStageOne")
            ?? string.Empty;
        string writer = FindMethodBody(
            stageTwo,
            "WriteSuccessLine")
            ?? string.Empty;
        string failure = FindMethodBody(
            stageTwo,
            "WriteFailure")
            ?? string.Empty;
        string prefix = FindMethodBody(
            stageTwo,
            "WriteFailurePrefix")
            ?? string.Empty;
        string runCompact = Compact(run);
        string writerCompact = Compact(writer);
        string failureCompact = Compact(failure);
        string prefixCompact = Compact(prefix);
        List<string[]> successCalls =
            FindInvocationArguments(run, "WriteSuccessLine");
        List<string[]> failurePrefixCalls =
            FindInvocationArguments(
                failure,
                "WriteFailurePrefix");
        List<string[]> failureFieldCalls =
            FindInvocationArguments(
                failure,
                "WriteFailureFields");

        if (successCalls.Count != 2
            || !InvocationArgumentsEqual(
                successCalls[0],
                "console",
                "serial",
                "0")
            || !InvocationArgumentsEqual(
                successCalls[1],
                "console",
                "serial",
                "lineIndex")
            || failurePrefixCalls.Count != 2
            || !InvocationArgumentsEqual(
                failurePrefixCalls[0],
                "console",
                "serial",
                "false")
            || !InvocationArgumentsEqual(
                failurePrefixCalls[1],
                "console",
                "serial",
                "true")
            || failureFieldCalls.Count != 2
            || !InvocationArgumentsEqual(
                failureFieldCalls[0],
                "console",
                "serial",
                "adapterStatus",
                "coordinatorStatus",
                "coordinatorCheckpoint",
                "firmwareStatus",
                "arenaCleanupStatus")
            || !InvocationArgumentsEqual(
                failureFieldCalls[1],
                "console",
                "serial",
                "adapterStatus",
                "coordinatorStatus",
                "coordinatorCheckpoint",
                "firmwareStatus",
                "arenaCleanupStatus")
            || Regex.Matches(
                run,
                @"WriteSuccessLine\s*\(\s*console\s*,\s*serial\s*,\s*0\s*\)",
                RegexOptions.CultureInvariant).Count != 1
            || !runCompact.Contains(
                "for(intlineIndex=1;"
                    + "lineIndex<BootSequencePlan"
                    + ".M01StageTwoSuccessLineCount;"
                    + "lineIndex++)",
                StringComparison.Ordinal)
            || !writerCompact.Contains(
                "GetM01StageTwoSuccessLineLength(lineIndex)",
                StringComparison.Ordinal)
            || !writerCompact.Contains(
                "GetM01StageTwoSuccessLineByte("
                    + "lineIndex,offset)",
                StringComparison.Ordinal)
            || !writerCompact.EndsWith(
                "EfiEntry.WriteCrLf(console,serial);",
                StringComparison.Ordinal)
            || failureCompact.Contains(
                "WriteSuccessLine",
                StringComparison.Ordinal)
            || failureCompact.IndexOf(
                "WriteFailurePrefix(console,serial,false)",
                StringComparison.Ordinal)
                >= failureCompact.IndexOf(
                    "WriteFailurePrefix(console,serial,true)",
                    StringComparison.Ordinal)
            || !prefixCompact.Contains(
                "(byte)'M',(byte)'0',(byte)'1',(byte)'_'"
                    + ",(byte)'S',(byte)'T',(byte)'A',(byte)'G'",
                StringComparison.Ordinal)
            || !prefixCompact.Contains(
                "(byte)'L',(byte)'',(byte)'m'",
                StringComparison.Ordinal))
        {
            return Failure("marker-writer-or-failure-order");
        }

        return true;
    }

    private static bool
        TestSourceFirewallsAndPriorCompatibility()
    {
        string source = LoadEfiEntrySource();
        string stageTwo = LoadEnabledStageTwoType();
        string profile00 = SelectProfile(
            source,
            stageOneEnabled: false,
            stageTwoEnabled: false);
        string[] forbidden =
        [
            "throw ",
            "try {",
            "catch ",
            "stackalloc",
            "Span<",
            "ReadOnlySpan<",
            "new byte[",
            "List<",
            "Dictionary<",
            "NativeMemory",
            "Marshal.",
            "dynamic ",
            "System.Reflection",
            "Thread",
            "Task",
            "ExitBootServices",
            "AllocatePool",
            "RunM01LiveAllocatorIntegration",
            "BootSequence."
        ];
        foreach (string token in forbidden)
        {
            if (stageTwo.Contains(
                    token,
                    StringComparison.Ordinal))
            {
                return Failure("stage-two-firewall=" + token);
            }
        }

        string[] defaultForbidden =
        [
            "M01LoadedImageStackIntegration",
            "M01LoadedImageStackStaticState",
            "EfiLoadedImageProtocol",
            "UnmanagedCallersOnly"
        ];
        foreach (string token in defaultForbidden)
        {
            if (profile00.Contains(
                    token,
                    StringComparison.Ordinal))
            {
                return Failure("default-profile-token=" + token);
            }
        }

        return !stageTwo.Contains(
                "M01LoadedImageStackStaticState",
                StringComparison.Ordinal)
            && stageTwo.Contains(
                "void* stageOneState",
                StringComparison.Ordinal)
            && !source.Contains(
                "M01LiveAllocatorIntegration.Execute",
                StringComparison.Ordinal)
            && !source.Contains(
                "RunM01LiveAllocatorIntegration",
                StringComparison.Ordinal)
            || Failure("prior-linkage-boundary");
    }

    private static bool TestRepresentativeMutations()
    {
        string source = LoadEfiEntrySource();
        if (!ValidateContract(source, out string baselineFailure))
        {
            return Failure("baseline=" + baselineFailure);
        }

        (string Name, string Old, string New)[] mutations =
        [
            (
                "guard",
                "#if " + StageTwoCondition,
                "#if true"
            ),
            (
                "arena-too-small",
                "private const ulong AllocatorArenaPageCount = 2;",
                "private const ulong AllocatorArenaPageCount = 1;"
            ),
            (
                "wrong-type",
                "private const uint EfiLoaderData = 2;",
                "private const uint EfiLoaderData = 7;"
            ),
            (
                "status-plus-address-ownership",
                "arenaFirmwareStatus == EfiSuccess;",
                "arenaFirmwareStatus == EfiSuccess"
                    + " && arenaAddress != 0;"
            ),
            (
                "allocation-validator",
                ".TryValidateStageTwoAllocation(",
                ".BypassStageTwoAllocationValidation("
            ),
            (
                "durable-state-reentry",
                "if (!IsZeroedStaticState(staticState))",
                "if (false)"
            ),
            (
                "stale-map-key",
                "refreshedMap.MapKey == previousMapKey",
                "refreshedMap.MapKey != previousMapKey"
            ),
            (
                "map-type-check",
                ".StageTwoMapContainsAllocation(",
                ".BypassMapAllocationCheck("
            ),
            (
                "arena-kind",
                ".AllocatorArena,",
                ".BootStack,"
            ),
            (
                "ledger-capacity",
                "private const ulong ReservationCapacity = 5;",
                "private const ulong ReservationCapacity = 4;"
            ),
            (
                "coordinator-link",
                ".ExecuteStageTwoCoordinator(",
                ".BypassStageTwoCoordinator("
            ),
            (
                "result-success",
                "|| !result->Succeeded",
                "|| false"
            ),
            (
                "success-loop-start",
                "for (int lineIndex = 1;",
                "for (int lineIndex = 0;"
            ),
            (
                "success-loop-step",
                "lineIndex++)",
                "lineIndex += 2)"
            ),
            (
                "continuation-failure",
                "if (!M01StageTwoAdapter.RunAfterStageOne(",
                "if (M01StageTwoAdapter.RunAfterStageOne("
            ),
            (
                "failure-cleanup",
                "bootServices->FreePages(",
                "bootServices->AllocatePages("
            )
        ];

        foreach ((string name, string oldText, string newText)
            in mutations)
        {
            if (CountOccurrences(source, oldText) == 0)
            {
                return Failure("mutation-anchor=" + name);
            }

            string mutated = ReplaceLast(
                source,
                oldText,
                newText);
            if (ValidateContract(
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

    private static bool TestIssueMarkerUniqueness()
    {
        string repositoryRoot =
            HostToolProcess.FindRepositoryRoot();
        string marker = "CLROS_M01_023_READY";
        List<string> markerSources = [];
        foreach (string candidate in Directory.EnumerateFiles(
            repositoryRoot,
            "*.cs",
            SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(candidate);
            if (IsGeneratedOrReferencePath(fullPath)
                || !File.ReadAllText(fullPath).Contains(
                    marker,
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
                "M01LiveAllocatorEfiEntryTests.cs"));
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
            "Console.WriteLine(\"CLROS_M01_023_READY\")",
            StringComparison.Ordinal);
        int pass = testSource.IndexOf(
            "Console.WriteLine(\"TEST_PASS \" + IssueTestName)",
            StringComparison.Ordinal);
        return start >= 0
            && ready > start
            && pass > ready
            && Regex.Matches(
                testSource,
                "Console\\.WriteLine\\(\"CLROS_M01_023_READY\"\\)",
                RegexOptions.CultureInvariant).Count == 1
            || Failure("marker-order-or-count");
    }

    private static bool ValidateContract(
        string source,
        out string failure)
    {
        try
        {
            if (CountExactIf(source, StageOneCondition) != 1
                || CountExactIf(source, StageTwoCondition) != 2)
            {
                failure = "guards";
                return false;
            }

            string profile11 = SelectProfile(
                source,
                stageOneEnabled: true,
                stageTwoEnabled: true);
            string profile01 = SelectProfile(
                source,
                stageOneEnabled: false,
                stageTwoEnabled: true);
            string stageOne = FindTypeBody(
                profile11,
                "M01StageOneAdapter")
                ?? string.Empty;
            string stageTwo = FindTypeBody(
                profile01,
                "M01StageTwoAdapter")
                ?? string.Empty;
            string continuation = FindMethodBody(
                stageOne,
                "M01StageOneContinuation")
                ?? string.Empty;
            string run = FindMethodBody(
                stageTwo,
                "RunAfterStageOne")
                ?? string.Empty;
            string completed = FindMethodBody(
                stageTwo,
                "ValidateCompletedResult")
                ?? string.Empty;
            string fail = FindMethodBody(stageTwo, "Fail")
                ?? string.Empty;
            string compact = Compact(stageTwo);
            string runCompact = Compact(run);
            string completedCompact = Compact(completed);

            List<string[]> allocations =
                FindInvocationArguments(run, "AllocatePages");
            if (allocations.Count != 1
                || allocations[0].Length != 4
                || Compact(allocations[0][0])
                    != "AllocateAnyPages"
                || Compact(allocations[0][1])
                    != "EfiLoaderData"
                || Compact(allocations[0][2])
                    != "AllocatorArenaPageCount"
                || Compact(allocations[0][3])
                    != "&arenaAddress")
            {
                failure = "allocation";
                return false;
            }

            if (!compact.Contains(
                    "privateconstuintEfiLoaderData=2;",
                    StringComparison.Ordinal)
                || !compact.Contains(
                    "privateconstulongAllocatorArenaPageCount=2;",
                    StringComparison.Ordinal)
                || !compact.Contains(
                    "privateconstulongReservationCapacity=5;",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "boolarenaAllocated="
                        + "arenaFirmwareStatus==EfiSuccess;",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "TryValidateStageTwoAllocation(",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "if(!IsZeroedStaticState(staticState))",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "refreshedMap.MapKey==previousMapKey",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "StageTwoMapContainsAllocation(",
                    StringComparison.Ordinal)
                || !compact.Contains(
                    "BootReservationKind.AllocatorArena",
                    StringComparison.Ordinal)
                || CountFlexibleQualifiedCalls(
                    run,
                    "M01StageOneAdapter"
                        + ".ExecuteStageTwoCoordinator") != 1
                || !completedCompact.Contains(
                    "!result->Succeeded",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "for(intlineIndex=1;",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "lineIndex++)",
                    StringComparison.Ordinal)
                || !Compact(continuation).Contains(
                    "if(!M01StageTwoAdapter.RunAfterStageOne(",
                    StringComparison.Ordinal)
                || FindInvocationArguments(
                    stageTwo,
                    "FreePages").Count != 1
                || !Compact(fail).Contains(
                    "bootServices->FreePages(",
                    StringComparison.Ordinal))
            {
                failure = "contract-token";
                return false;
            }

            failure = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            failure = ex.GetType().Name + ":" + ex.Message;
            return false;
        }
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
        foreach (string disabledSymbol in new[] { UnexpectedPanicSymbol, MemoryMapSymbol, EntropySymbol, TypeManagerSymbol, TypedEbsSymbol, GcStaticSymbol, TypeManagerSymbol + " || " + GcStaticSymbol, "M04_CHECKED_REFERENCE_BARRIER_PROBE || " + GcStaticSymbol, "M04_CHECKED_REFERENCE_BARRIER_PROBE || " + GcStaticSymbol + " || M04_X64_UNWIND_METADATA_PROBE", FirstExitSymbol, "(" + FirstExitSymbol + " || CLROS_NORMAL_VERIFIED_EXIT)", "M04_MANAGED_HEAP_PAGE_RANGE_PROBE || " + FirstExitSymbol, TypeManagerSymbol + " || " + GcStaticSymbol + " || " + FirstExitSymbol, "M04_RHP_NEW_FAST_PROBE || " + UnexpectedPanicSymbol + " || " + TypeManagerSymbol + " || " + FirstExitSymbol })
        {
        string fixture = "#if " + disabledSymbol + "\nPanicOnly();\n#else\nPanicOff();\n#endif\n"
            + "#if " + StageOneSymbol + "\n#if " + disabledSymbol
            + "\nNestedPanic();\n#else\nNestedOff();\n#endif\n#else\nStageOff();\n#endif\n"
            + "#if UNRELATED_PROBE\nUnknown();\n#else\nUnknownElse();\n#endif\n";
        for (int mask = 0; mask < 4; mask++)
        {
            bool stageOne = (mask & 1) != 0;
            string selected = SelectProfile(fixture, stageOne, (mask & 2) != 0);
            if (selected.Contains("PanicOnly();", StringComparison.Ordinal)
                || selected.Contains("NestedPanic();", StringComparison.Ordinal)
                || selected.Contains("#if " + disabledSymbol, StringComparison.Ordinal)
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

        string aliased = fixture.Replace(disabledSymbol,
            disabledSymbol.ToLowerInvariant(), StringComparison.Ordinal);
        string alias = SelectProfile(aliased, false, false);
        if (!(alias.Contains("#if " + disabledSymbol.ToLowerInvariant(), StringComparison.Ordinal)
            && alias.Contains("PanicOnly();", StringComparison.Ordinal)
            && alias.Contains("PanicOff();", StringComparison.Ordinal)
            )) return Failure("panic-selector-ordinal-preservation");
        }
        return true;
    }

    private static string SelectProfile(
        string source,
        bool stageOneEnabled,
        bool stageTwoEnabled)
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
                // Model explicit Diagnostic selection, where the normal policy
                // symbol is false, while preserving unknown or misspelled terms.
                foreach (string symbol in new[] { StageOneSymbol, StageTwoSymbol,
                    FirstExitSymbol, "ACPI_PHYSICAL_DISCOVERY_PROBE" })
                    condition = condition.Replace("(" + symbol + " || CLROS_NORMAL_VERIFIED_EXIT)",
                        symbol, StringComparison.Ordinal);
                bool isStageOne = string.Equals(
                    condition,
                    StageOneSymbol,
                    StringComparison.Ordinal);
                bool isStageTwo = string.Equals(
                    condition,
                    StageTwoSymbol,
                    StringComparison.Ordinal);
                bool isSelected = isStageOne || isStageTwo
                    || string.Equals(condition, UnexpectedPanicSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, MemoryMapSymbol, StringComparison.Ordinal)
            || string.Equals(condition, EntropySymbol, StringComparison.Ordinal)
            || string.Equals(condition, TypeManagerSymbol, StringComparison.Ordinal)
            || string.Equals(condition, TypedEbsSymbol, StringComparison.Ordinal)
            || string.Equals(condition, GcStaticSymbol, StringComparison.Ordinal)
            || string.Equals(condition, FirstExitSymbol, StringComparison.Ordinal)
            || string.Equals(condition, "M04_MANAGED_HEAP_PAGE_RANGE_PROBE || " + FirstExitSymbol, StringComparison.Ordinal)
            || string.Equals(condition, TypeManagerSymbol + " || " + GcStaticSymbol + " || " + FirstExitSymbol, StringComparison.Ordinal)
            || string.Equals(condition, "M04_RHP_NEW_FAST_PROBE || " + UnexpectedPanicSymbol + " || " + TypeManagerSymbol + " || " + FirstExitSymbol, StringComparison.Ordinal)
            || string.Equals(condition, TypeManagerSymbol + " || " + GcStaticSymbol, StringComparison.Ordinal)
            || string.Equals(condition, "M04_CHECKED_REFERENCE_BARRIER_PROBE || " + GcStaticSymbol, StringComparison.Ordinal)
            || string.Equals(condition, "M04_CHECKED_REFERENCE_BARRIER_PROBE || " + GcStaticSymbol + " || M04_X64_UNWIND_METADATA_PROBE", StringComparison.Ordinal);
                bool selectedValue = isStageOne
                    ? stageOneEnabled
                    : isStageTwo && stageTwoEnabled;
                stack.Push(
                    new PreprocessorFrame(
                        isSelected,
                        emit,
                        selectedValue,
                        SeenElse: false));
                if (isSelected)
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
                if (!frame.IsSelected && emit)
                {
                    output.AppendLine(line);
                }

                emit = frame.ParentEmit;
                continue;
            }

            if (trimmed.StartsWith(
                    "#elif",
                    StringComparison.Ordinal))
            {
                if (stack.Count == 0
                    || stack.Peek().IsSelected)
                {
                    throw new InvalidOperationException(
                        "Unsupported selected #elif.");
                }

                if (emit)
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
                "Unterminated preprocessor block.");
        }

        return output.ToString();
    }

    private static string LoadEnabledStageTwoType()
    {
        string source = LoadEfiEntrySource();
        string profile = SelectProfile(
            source,
            stageOneEnabled: false,
            stageTwoEnabled: true);
        return FindTypeBody(profile, "M01StageTwoAdapter")
            ?? throw new InvalidOperationException(
                "Could not locate the enabled stage-two adapter.");
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

        int open = source.IndexOf('{', match.Index + match.Length);
        int close = FindMatchingDelimiter(source, open, '{', '}');
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
            int openBrace = SkipWhitespace(source, closeParen + 1);
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
        string sanitized = SanitizeStringsAndCharacters(source);
        List<string[]> results = [];
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
                SplitTopLevelArguments(
                    source[(open + 1)..close]));
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
        for (int index = 0; index < sanitized.Length; index++)
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
                    parts.Add(arguments[start..index].Trim());
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
            source,
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
            source,
            pattern + @"\s*\(",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Index : -1;
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
        bool escaped = false;
        for (int index = 0; index < source.Length; index++)
        {
            char value = source[index];
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

            if (value == '"')
            {
                inString = true;
                sanitized[index] = ' ';
            }
            else if (value == '\'')
            {
                inCharacter = true;
                sanitized[index] = ' ';
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
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private static string Compact(string source)
    {
        return Regex.Replace(
            source,
            @"\s+",
            string.Empty,
            RegexOptions.CultureInvariant);
    }

    private static string NormalizeLineEndings(
        string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal)
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

    private static string ReplaceLast(
        string source,
        string oldText,
        string newText)
    {
        int index = source.LastIndexOf(
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
                "EfiEntry.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
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
}
