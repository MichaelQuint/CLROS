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
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static unsafe class M01LivePagingIntegrationTests
{
    private const string IssueTestName = "m01.019";
    private const string PreparationIssueTestName = "m01.029";
    private const int PageSize = (int)BootReservedRange.PageSize;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run(
            "m01.019 stable enums layouts defaults and capabilities",
            TestStableSurfaceAndDefaults,
            ref failures);
        Run(
            "m01.019 canonical aggregate and exact bindings",
            TestCanonicalAggregateAndBindings,
            ref failures);
        Run(
            "m01.019 emit verify corruption and padding rejection",
            TestByteModesAndCorruption,
            ref failures);
        Run(
            "m01.019 three allocation provenance is exact",
            TestAllocationEvidenceAndRecords,
            ref failures);
        Run(
            "m01.019 refreshed map ledger provenance is exact",
            TestMapLedgerEvidence,
            ref failures);
        Run(
            "m01.019 reservation identities are exact",
            TestReservationLedgerEvidence,
            ref failures);
        Run(
            "m01.019 identity requirements reject caller relabeling",
            TestIdentityMappingCrossBindings,
            ref failures);
        Run(
            "m01.019 pointer count capacity alias and overflow preflight",
            TestPointerCountCapacityAliasAndOverflow,
            ref failures);
        Run(
            "m01.019 cleanup permutations and restoration boundary",
            TestCleanupPlans,
            ref failures);
        Run(
            "m01.019 stage two and stack plan bindings are exact",
            TestStageTwoAndStackPlanBindings,
            ref failures);
        Run(
            "m01.019 transition protection and page zero negatives",
            TestNegativeLeafEvidence,
            ref failures);
        Run(
            "m01.019 duplicate state and deterministic repeat",
            TestDuplicateAndDeterministicRepeat,
            ref failures);
        Run(
            "m01.019 source firewalls preserve host only boundary",
            TestSourceFirewalls,
            ref failures);
        Run(
            "m01.019 exact source closure compiles under nostdlib efi",
            TestNoStdLibEfiCompileClosure,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-019 live paging integration tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_019_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);

        RunPreparationContractTests();
    }

    private static void RunPreparationContractTests()
    {
        int failures = 0;

        Console.WriteLine(
            "TEST_START " + PreparationIssueTestName);

        Run(
            "m01.029 stable pod defaults capabilities and api",
            TestPreparationSurfaceAndDefaults,
            ref failures);
        Run(
            "m01.029 canonical preparation is deterministic",
            TestCanonicalPreparation,
            ref failures);
        Run(
            "m01.029 legacy transcripts cannot promote preparation",
            TestPreparationRejectsLegacyPromotion,
            ref failures);
        Run(
            "m01.029 pointer count capacity and alias failures",
            TestPreparationPointerCountCapacityAndAliases,
            ref failures);
        Run(
            "m01.029 stale map ledger and preflight failures",
            TestPreparationMapLedgerAndPreflightFailures,
            ref failures);
        Run(
            "m01.029 byte readback workspace and repeated use",
            TestPreparationByteWorkspaceAndRepeatedUse,
            ref failures);
        Run(
            "m01.029 preflight failure preserves caller storage",
            TestPreparationPreflightFailureIsAtomic,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + PreparationIssueTestName
                + " failures="
                + failures);
            throw new InvalidOperationException(
                "M01-029 preparation contract tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_029_READY");
        Console.WriteLine(
            "TEST_PASS " + PreparationIssueTestName);
    }

    internal static M01LivePagingIntegrationResult
        CreateCanonicalSuccessForPresenterTests()
    {
        using CanonicalFixture fixture = new();
        M01LivePagingIntegrationResult result =
            fixture.ExecuteCurrent();
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "M01-019 canonical presenter fixture failed: "
                + result.Status
                + "/"
                + result.LastCompletedCheckpoint);
        }
        return result;
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

    private static bool TestPreparationSurfaceAndDefaults()
    {
        (string Name, byte Value)[] executionModes =
        [
            ("Uninitialized", 0),
            ("PreparationOnly", 1),
            ("ModelOnlyLegacy", 2)
        ];
        M01LivePagingProtectionPreparation emptyProtection = default;
        M01LivePagingPreparationEvidence emptyEvidence = default;
        M01LivePagingPreparationState emptyState = default;
        M01LivePagingPreparationResult emptyResult = default;
        M01LivePagingIntegrationResult emptyLegacy = default;

        MethodInfo[] prepareMethods =
            typeof(M01LivePagingIntegration)
                .GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Where(static method =>
                    string.Equals(
                        method.Name,
                        nameof(M01LivePagingIntegration.Prepare),
                        StringComparison.Ordinal))
                .ToArray();
        MethodInfo[] modelMethods =
            typeof(M01LivePagingIntegration)
                .GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Where(static method =>
                    string.Equals(
                        method.Name,
                        nameof(
                            M01LivePagingIntegration
                                .ExecuteModelOnly),
                        StringComparison.Ordinal))
                .ToArray();
        if (prepareMethods.Length != 1
            || modelMethods.Length != 1)
        {
            return false;
        }

        ParameterInfo[] parameters =
            prepareMethods[0].GetParameters();
        string[] expectedNames =
        [
            "stageTwoState",
            "stageTwoStateCount",
            "stageTwoStateCapacity",
            "stackSwitchPlan",
            "allocationRecords",
            "allocationRecordCount",
            "allocationRecordCapacity",
            "allocationEvidence",
            "mapLedgerEvidence",
            "reservationLedger",
            "reservationCount",
            "reservationCapacity",
            "mappingRequirements",
            "mappingRequirementCount",
            "mappingRequirementCapacity",
            "mappingRanges",
            "mappingRangeCount",
            "mappingRangeCapacity",
            "pageImages",
            "pageImageCount",
            "pageImageCapacity",
            "pageTableEntries",
            "pageTableEntryCount",
            "pageTableEntryCapacity",
            "pageTablePreconditions",
            "cr3CodePage",
            "cr3CodePageCapacity",
            "protectionProbePage",
            "protectionProbePageCapacity",
            "preflightObservation",
            "preflightObservationCount",
            "preflightObservationCapacity",
            "byteMode",
            "preparationState",
            "preparationStateCount",
            "preparationStateCapacity"
        ];
        if (parameters.Length != expectedNames.Length
            || !parameters
                .Select(static parameter => parameter.Name)
                .SequenceEqual(expectedNames)
            || prepareMethods[0].ReturnType
                != typeof(M01LivePagingPreparationResult)
            || PreparationSurfaceContainsSyntheticInput(parameters)
            || PreparationEvidenceContainsSyntheticTranscript()
            || HasPreparationLegacyConversion())
        {
            return false;
        }

        return EnumContractMatches<M01LivePagingExecutionMode>(
                executionModes)
            && M01LivePagingPreparationState.CurrentVersion == 1
            && M01LivePagingPreparationState
                .RequiredStorageAlignment == 8
            && M01LivePagingPreparationState
                .RequiredStorageBytes == 440
            && (ulong)Unsafe.SizeOf<M01LivePagingPreparationState>()
                == M01LivePagingPreparationState.RequiredStorageBytes
            && Offset<M01LivePagingPreparationState>(
                "StageTwoStateAddressValue") == 16
            && Offset<M01LivePagingPreparationState>(
                "MappingRangeAddressValue") == 112
            && Offset<M01LivePagingPreparationState>(
                "PageEntryAddressValue") == 160
            && Offset<M01LivePagingPreparationState>(
                "PreflightObservationAddressValue") == 216
            && Offset<M01LivePagingPreparationState>(
                "Cr3CodeReservationValue") == 256
            && Offset<M01LivePagingPreparationState>(
                "OwnedRootValue") == 376
            && Offset<M01LivePagingPreparationState>(
                "PageZeroAbsentValue") == 432
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingProtectionPreparation>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingPreparationEvidence>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingPreparationState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingPreparationResult>()
            && emptyProtection.Status
                == M01LivePagingStatus.Uninitialized
            && !emptyProtection.Succeeded
            && !emptyProtection.InitialLeafRestored
            && !emptyEvidence.Succeeded
            && emptyState.Size == 0
            && emptyState.Version == 0
            && emptyState.Status == M01LivePagingStatus.Uninitialized
            && emptyState.Checkpoint
                == M01LivePagingCheckpoint.Uninitialized
            && emptyState.ExecutionMode
                == M01LivePagingExecutionMode.Uninitialized
            && !emptyState.IsPrepared
            && emptyResult.Status
                == M01LivePagingStatus.Uninitialized
            && emptyResult.LastCompletedCheckpoint
                == M01LivePagingCheckpoint.Uninitialized
            && emptyResult.ExecutionMode
                == M01LivePagingExecutionMode.Uninitialized
            && !emptyResult.Succeeded
            && !emptyResult.IsPreparationOnly
            && !emptyResult.IsModelOnly
            && !emptyResult.IsExecutionEvidence
            && !emptyResult.PreparedForCompositeTransition
            && emptyLegacy.ExecutionMode
                == M01LivePagingExecutionMode.ModelOnlyLegacy
            && emptyLegacy.IsModelOnly
            && !emptyLegacy.IsExecutionEvidence
            && !emptyLegacy.PreparedForCompositeTransition
            && M01LivePagingIntegration.LegacyExecuteIsModelOnly
            && M01LivePagingIntegration.ExecuteModelOnlyIsModelOnly
            && !M01LivePagingIntegration
                .LegacyResultsCanPrepareCompositeTransition
            && !M01LivePagingIntegration
                .PreparationExecutesGeneratedCode
            && !M01LivePagingIntegration
                .PreparationExecutesPrivilegedInstructions
            && !M01LivePagingIntegration.PreparationInvokesFirmware
            && !M01LivePagingIntegration.PreparationIsGuestEvidence;
    }

    private static bool TestCanonicalPreparation()
    {
        using CanonicalFixture fixture = new();
        fixture.FillStubPages(0xcc);
        M01LivePagingPreparationResult first =
            fixture.PrepareCurrent();
        if (!first.Succeeded
            || !first.PreparedForCompositeTransition
            || !first.IsPreparationOnly
            || first.IsModelOnly
            || first.IsExecutionEvidence
            || first.ExecutionMode
                != M01LivePagingExecutionMode.PreparationOnly
            || first.LastCompletedCheckpoint
                != M01LivePagingCheckpoint
                    .PreparedForCompositeTransition)
        {
            return false;
        }

        M01LivePagingPreparationEvidence evidence =
            first.Evidence;
        M01LivePagingPreparationState state = first.State;
        if (!evidence.Succeeded
            || !evidence.AllocationEvidence.IsValidFor(
                fixture.Records,
                3,
                3)
            || !evidence.MapLedgerEvidence.IsValidFor(
                *fixture.StageTwoState,
                fixture.AllocationEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity)
            || !evidence.ReservationValidation.Succeeded
            || !evidence.MappingPlan.Succeeded
            || !evidence.TableBuild.Succeeded
            || !evidence.PreflightMaterialization.Succeeded
            || !evidence.PreflightReadback.Succeeded
            || !evidence.PreflightReadback.ExactBytes
            || !evidence.Preflight.Succeeded
            || !evidence.TransitionMaterialization.Succeeded
            || !evidence.TransitionReadback.Succeeded
            || !evidence.InvlpgMaterialization.Succeeded
            || !evidence.InvlpgReadback.ReadBackVerified
            || !evidence.ProbeMaterialization.Succeeded
            || !evidence.ProbeReadback.ReadBackVerified
            || !evidence.ProtectionPreparation.Succeeded
            || !evidence.ProtectionPreparation
                .InitialLeafRestored
            || !evidence.InvlpgInvocation.Succeeded
            || !evidence.PageZeroAudit
                .ProvesPageZeroAbsentInSuppliedImage
            || !evidence.CleanupPlan.Succeeded
            || evidence.CleanupPlan.ReleaseCount != 3
            || evidence.CleanupPlan.FirstReleaseIndex != 2
            || evidence.CleanupPlan.SecondReleaseIndex != 1
            || evidence.CleanupPlan.ThirdReleaseIndex != 0)
        {
            return false;
        }

        if (!state.IsPrepared
            || state.Status != M01LivePagingStatus.Success
            || state.Checkpoint
                != M01LivePagingCheckpoint
                    .PreparedForCompositeTransition
            || state.ExecutionMode
                != M01LivePagingExecutionMode.PreparationOnly
            || state.StageTwoStateAddress
                != (ulong)fixture.StageTwoState
            || state.StageTwoStateCount != 1
            || state.StageTwoStateCapacity != 1
            || state.AllocationRecordCount != 3
            || state.AllocationRecordCapacity != 3
            || state.ReservationCount != fixture.LedgerCount
            || state.ReservationCapacity != fixture.LedgerCapacity
            || state.MappingRequirementCount
                != fixture.MappingRequirementCount
            || state.MappingRangeCount
                != evidence.MappingPlan.OutputRangeCount
            || state.PageImageCount
                != evidence.TableBuild.TablePageCount
            || state.PageEntryCount
                != evidence.TableBuild.EntryCount
            || state.PreflightObservationCount != 1
            || state.PreflightObservationCapacity != 1
            || state.RawFirmwareCr3 != fixture.RawFirmwareCr3
            || state.TablePageCount
                != evidence.TableBuild.TablePageCount
            || state.MappedPageCount
                != evidence.TableBuild.MappedPageCount
            || state.ProbeLeafEntryIndex
                != evidence.TableBuild.ProbeLeafEntryIndex
            || !state.PageZeroAbsent
            || !state.StubBytesVerified
            || !state.InitialProbeLeafRestored
            || !state.CleanupPlanned)
        {
            return false;
        }

        byte[] code = fixture.CopyCodePage();
        byte[] probe = fixture.CopyProbePage();
        X64BootIdentityMappingRange[] ranges =
            (X64BootIdentityMappingRange[])
                fixture.MappingRanges.Clone();
        X64PageTableImagePage[] pages =
            (X64PageTableImagePage[])
                fixture.PageImages.Clone();
        ulong[] entries = (ulong[])fixture.Entries.Clone();
        fixture.ClearPreparationState();
        M01LivePagingPreparationResult second =
            fixture.PrepareCurrent(
                M01LivePagingByteMode.VerifyExisting);

        return second.Succeeded
            && fixture.CodePageEquals(code)
            && fixture.ProbePageEquals(probe)
            && fixture.MappingRanges.AsSpan().SequenceEqual(ranges)
            && fixture.PageImages.AsSpan().SequenceEqual(pages)
            && fixture.Entries.AsSpan().SequenceEqual(entries)
            && second.Evidence.MappingPlan.OutputRangeCount
                == evidence.MappingPlan.OutputRangeCount
            && second.Evidence.TableBuild.TablePageCount
                == evidence.TableBuild.TablePageCount
            && second.Evidence.TableBuild.MappedPageCount
                == evidence.TableBuild.MappedPageCount
            && second.State.PageEntryCount == state.PageEntryCount;
    }

    private static bool TestPreparationRejectsLegacyPromotion()
    {
        using CanonicalFixture legacy = new();
        M01LivePagingIntegrationResult legacySuccess =
            legacy.ExecuteCurrent();
        if (!legacySuccess.Succeeded
            || legacySuccess.ExecutionMode
                != M01LivePagingExecutionMode.ModelOnlyLegacy
            || !legacySuccess.IsModelOnly
            || legacySuccess.IsExecutionEvidence
            || legacySuccess.PreparedForCompositeTransition)
        {
            return false;
        }

        using CanonicalFixture irrelevant = new();
        *irrelevant.TransitionState = default;
        irrelevant.Cr3Events[0] = default;
        irrelevant.ProtectionObservation = default;
        irrelevant.ProtectionEvents[0] = default;
        M01LivePagingPreparationResult prepared =
            irrelevant.PrepareCurrent();
        if (!prepared.Succeeded
            || !prepared.PreparedForCompositeTransition)
        {
            return false;
        }

        using CanonicalFixture badTransition = new();
        *badTransition.TransitionState = default;
        M01LivePagingIntegrationResult transitionFailure =
            badTransition.ExecuteCurrent();

        using CanonicalFixture badCr3Events = new();
        badCr3Events.Cr3Events[0] = default;
        M01LivePagingIntegrationResult cr3Failure =
            badCr3Events.ExecuteCurrent();

        using CanonicalFixture badProtection = new();
        badProtection.ProtectionObservation = default;
        M01LivePagingIntegrationResult protectionFailure =
            badProtection.ExecuteCurrent();

        return transitionFailure.Status
                == M01LivePagingStatus.TransitionEvidenceRejected
            && cr3Failure.Status
                == M01LivePagingStatus.Cr3TranscriptRejected
            && protectionFailure.Status
                == M01LivePagingStatus.ProtectionProbeRejected
            && !typeof(M01LivePagingPreparationResult)
                .IsAssignableFrom(
                    typeof(M01LivePagingIntegrationResult))
            && !typeof(M01LivePagingIntegrationResult)
                .IsAssignableFrom(
                    typeof(M01LivePagingPreparationResult))
            && !HasPreparationLegacyConversion()
            && PreparationEvidenceContainsSyntheticTranscript()
                == false;
    }

    private static bool TestPreparationPointerCountCapacityAndAliases()
    {
        using CanonicalFixture fixture = new();
        bool passed = true;

        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareStageTwo(null, 1, 1),
            M01LivePagingStatus.StageTwoRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareStageTwo(
                fixture.StageTwoState,
                0,
                1),
            M01LivePagingStatus.StageTwoRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareStageTwo(
                fixture.StageTwoState,
                1,
                0),
            M01LivePagingStatus.StageTwoRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareStageTwo(
                fixture.StageTwoState,
                2,
                2),
            M01LivePagingStatus.StageTwoRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareStageTwo(
                (M01LiveAllocatorStaticState*)(
                    (byte*)fixture.StageTwoState + 1),
                1,
                1),
            M01LivePagingStatus.StageTwoRejected);

        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                null,
                3,
                3,
                fixture.AllocationEvidence),
            M01LivePagingStatus.AllocationTableRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                fixture.Records,
                0,
                3,
                fixture.AllocationEvidence),
            M01LivePagingStatus.AllocationTableRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                fixture.Records,
                2,
                3,
                fixture.AllocationEvidence),
            M01LivePagingStatus.AllocationTableRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                fixture.Records,
                3,
                4,
                fixture.AllocationEvidence),
            M01LivePagingStatus.AllocationTableRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                fixture.Records,
                3,
                M01LivePagingIntegration
                    .MaximumAllocationRecordCapacity + 1,
                fixture.AllocationEvidence),
            M01LivePagingStatus.AllocationTableRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                fixture.Records,
                3,
                3,
                default),
            M01LivePagingStatus.AllocationEvidenceRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                (BootMemoryRegionRecord*)(
                    (byte*)fixture.Records + 1),
                3,
                3,
                fixture.AllocationEvidence),
            M01LivePagingStatus.AllocationTableRejected);

        BootMemoryRegionRecord savedTable = fixture.Records[2];
        fixture.Records[2] = WithRecord(
            savedTable,
            physicalStart: fixture.Records[1].PhysicalStart,
            virtualStart: fixture.Records[1].VirtualStart,
            physicalEndExclusive:
                fixture.Records[1].PhysicalStart
                    + savedTable.ByteLength);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                fixture.Records,
                3,
                3,
                fixture.CaptureAllocationEvidence(
                    8,
                    9,
                    10)),
            M01LivePagingStatus.AllocationTableRejected);
        fixture.Records[2] = savedTable;

        BootMemoryRegionRecord savedCode = fixture.Records[0];
        fixture.Records[0] = WithRecord(
            savedCode,
            physicalStart: savedCode.PhysicalStart + 1,
            virtualStart: savedCode.VirtualStart + 1,
            physicalEndExclusive:
                savedCode.PhysicalEndExclusive + 1);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                fixture.Records,
                3,
                3,
                fixture.CaptureAllocationEvidence(
                    8,
                    9,
                    10)),
            M01LivePagingStatus.AllocationTableRejected);
        fixture.Records[0] = savedCode;
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareAllocation(
                (BootMemoryRegionRecord*)(~0UL - 7),
                3,
                3,
                fixture.AllocationEvidence),
            M01LivePagingStatus.AllocationTableRejected);

        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.MapEvidence,
                null,
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            M01LivePagingStatus.MapLedgerEvidenceRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.MapEvidence,
                fixture.Ledger,
                0,
                fixture.LedgerCapacity),
            M01LivePagingStatus.MapLedgerEvidenceRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCapacity + 1,
                fixture.LedgerCapacity),
            M01LivePagingStatus.MapLedgerEvidenceRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.MapEvidence,
                (BootReservedRange*)(
                    (byte*)fixture.Ledger + 1),
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            M01LivePagingStatus.MapLedgerEvidenceRejected);

        fixed (X64PagingModePreflightObservation* preflight =
            &fixture.PreflightObservation)
        {
            passed &= ExpectPreparationFailure(
                fixture,
                fixture.PreparePreflight(null, 1, 1),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                fixture,
                fixture.PreparePreflight(preflight, 0, 1),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                fixture,
                fixture.PreparePreflight(preflight, 1, 0),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                fixture,
                fixture.PreparePreflight(preflight, 1, 2),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                fixture,
                fixture.PreparePreflight(
                    (X64PagingModePreflightObservation*)(
                        (byte*)preflight + 1),
                    1,
                    1),
                M01LivePagingStatus.CoordinatorStateRejected);
        }

        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareState(null, 1, 1),
            M01LivePagingStatus.CoordinatorStateRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareState(
                fixture.PreparationState,
                0,
                1),
            M01LivePagingStatus.CoordinatorStateRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareState(
                fixture.PreparationState,
                1,
                0),
            M01LivePagingStatus.CoordinatorStateRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareState(
                fixture.PreparationState,
                2,
                2),
            M01LivePagingStatus.CoordinatorStateRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareState(
                (M01LivePagingPreparationState*)
                    fixture.StageTwoState,
                1,
                1),
            M01LivePagingStatus.CoordinatorStateRejected);

        byte[] preflightBefore;
        fixed (X64PagingModePreflightObservation* preflight =
            &fixture.PreflightObservation)
        {
            preflightBefore = CopyBytes(
                (byte*)preflight,
                X64PagingModePreflightObservation.StructureSize);
        }
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareStateAliasesPreflight(),
            M01LivePagingStatus.CoordinatorStateRejected);
        fixed (X64PagingModePreflightObservation* preflight =
            &fixture.PreflightObservation)
        {
            passed &= BytesEqual(
                preflightBefore,
                (byte*)preflight);
        }

        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareState(
                (M01LivePagingPreparationState*)(~0UL - 7),
                1,
                1),
            M01LivePagingStatus.CoordinatorStateRejected);

        M01LivePagingPreparationState dirty = default;
        ((byte*)&dirty)[0] = 1;
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareState(&dirty, 1, 1),
            M01LivePagingStatus.CoordinatorStateRejected);

        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareWorkspace(
                fixture.MappingRequirements,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCapacity,
                fixture.MappingRanges,
                1,
                fixture.MappingRangeCapacity,
                fixture.PageImages,
                0,
                fixture.PageImageCapacity,
                fixture.Entries,
                0,
                fixture.EntryCapacity,
                fixture.CodePage,
                BootReservedRange.PageSize,
                fixture.ProbePage,
                BootReservedRange.PageSize),
            M01LivePagingStatus.CoordinatorStateRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareWorkspace(
                fixture.MappingRequirements,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCapacity,
                fixture.MappingRanges,
                0,
                fixture.MappingRangeCapacity,
                fixture.PageImages,
                1,
                fixture.PageImageCapacity,
                fixture.Entries,
                0,
                fixture.EntryCapacity,
                fixture.CodePage,
                BootReservedRange.PageSize,
                fixture.ProbePage,
                BootReservedRange.PageSize),
            M01LivePagingStatus.CoordinatorStateRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareWorkspace(
                fixture.MappingRequirements,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCapacity,
                fixture.MappingRanges,
                0,
                fixture.MappingRangeCapacity,
                fixture.PageImages,
                0,
                fixture.PageImageCapacity,
                fixture.Entries,
                1,
                fixture.EntryCapacity,
                fixture.CodePage,
                BootReservedRange.PageSize,
                fixture.ProbePage,
                BootReservedRange.PageSize),
            M01LivePagingStatus.CoordinatorStateRejected);

        return passed;
    }

    private static bool TestPreparationMapLedgerAndPreflightFailures()
    {
        using CanonicalFixture fixture = new();
        bool passed = true;

        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                default,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            M01LivePagingStatus.MapLedgerEvidenceRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.CaptureMapEvidence(
                    previousMapKey:
                        fixture.StageTwoState->RefreshedMapKey + 1),
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            M01LivePagingStatus.MapLedgerEvidenceRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.CaptureMapEvidence(
                    previousMapGeneration:
                        fixture.StageTwoState
                            ->RefreshedMapGeneration + 1),
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            M01LivePagingStatus.MapLedgerEvidenceRejected);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.CaptureMapEvidence(
                    refreshedMapGeneration:
                        fixture.StageTwoState
                            ->RefreshedMapGeneration),
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            M01LivePagingStatus.MapLedgerEvidenceRejected);

        FirmwareMemoryMapRecord staleKey =
            fixture.StageThreeMap;
        staleKey.MapKey =
            fixture.StageTwoState->RefreshedMapKey;
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.CaptureMapEvidence(
                    refreshedMap: staleKey),
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            M01LivePagingStatus.MapLedgerEvidenceRejected);

        BootReservedRange[] saved = fixture.CopyLedger();
        (fixture.Ledger[0], fixture.Ledger[1]) =
            (fixture.Ledger[1], fixture.Ledger[0]);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.RefreshMapEvidence(),
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            M01LivePagingStatus.ReservationLedgerRejected);
        fixture.RestoreLedger(saved);

        fixture.Ledger[fixture.LedgerCount - 1] =
            fixture.Ledger[0];
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PrepareMapLedger(
                fixture.RefreshMapEvidence(),
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity),
            M01LivePagingStatus.ReservationLedgerRejected);
        fixture.RestoreLedger(saved);

        X64PagingModePreflightObservation empty = default;
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PreparePreflight(&empty, 1, 1),
            M01LivePagingStatus.PreflightRejected);

        X64PagingModePreflightObservation directionFlag =
            X64PagingModePreflightObservation.CreateObserved(
                CanonicalFixture.ValidCr0,
                fixture.RawFirmwareCr3,
                CanonicalFixture.ValidCr4,
                CanonicalFixture.ValidEfer,
                X64PagingModePreflightStubPlan
                    .CpuidAddressSizesLeaf,
                48,
                CanonicalFixture.ValidRflags | (1UL << 10));
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PreparePreflight(
                &directionFlag,
                1,
                1),
            M01LivePagingStatus.PreflightRejected);

        X64PagingModePreflightObservation invalidWidth =
            X64PagingModePreflightObservation.CreateObserved(
                CanonicalFixture.ValidCr0,
                fixture.RawFirmwareCr3,
                CanonicalFixture.ValidCr4,
                CanonicalFixture.ValidEfer,
                X64PagingModePreflightStubPlan
                    .CpuidAddressSizesLeaf,
                0,
                CanonicalFixture.ValidRflags);
        passed &= ExpectPreparationFailure(
            fixture,
            fixture.PreparePreflight(
                &invalidWidth,
                1,
                1),
            M01LivePagingStatus.PreflightRejected);

        return passed;
    }

    private static bool TestPreparationByteWorkspaceAndRepeatedUse()
    {
        bool passed = true;
        using (CanonicalFixture workspace = new())
        {
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    null,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    0,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCount - 1,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    null,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    0,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    null,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    0,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    null,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.Mapping.TotalTablePageCount
                        * X64PageTableImagePage
                            .EntryCountPerPage - 1,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.TableStorageRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize,
                    aliasPageStorage: true),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize - 1,
                    workspace.ProbePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.CodePage,
                    BootReservedRange.PageSize),
                M01LivePagingStatus.CoordinatorStateRejected);
            passed &= ExpectPreparationFailure(
                workspace,
                workspace.PrepareWorkspace(
                    workspace.MappingRequirements,
                    workspace.MappingRequirementCount,
                    workspace.MappingRequirementCapacity,
                    workspace.MappingRanges,
                    0,
                    workspace.MappingRangeCapacity,
                    workspace.PageImages,
                    0,
                    workspace.PageImageCapacity,
                    workspace.Entries,
                    0,
                    workspace.EntryCapacity,
                    workspace.CodePage,
                    BootReservedRange.PageSize,
                    workspace.ProbePage,
                    BootReservedRange.PageSize,
                    M01LivePagingByteMode.Uninitialized),
                M01LivePagingStatus.ByteModeRejected);
        }

        using (CanonicalFixture bytes = new())
        {
            bytes.FillStubPages(0xcc);
            M01LivePagingPreparationResult emitted =
                bytes.PrepareCurrent();
            if (!emitted.Succeeded || !bytes.PaddingIsZero())
                return false;

            byte[] exactCode = bytes.CopyCodePage();
            byte[] exactProbe = bytes.CopyProbePage();
            M01LivePagingStatus[] expected =
            [
                M01LivePagingStatus.PreflightStubRejected,
                M01LivePagingStatus.TransitionStubRejected,
                M01LivePagingStatus.InvlpgStubRejected,
                M01LivePagingStatus.ProtectionProbeStubRejected,
                M01LivePagingStatus.ProtectionProbeStubRejected
            ];
            ulong[] offsets =
            [
                M01LivePagingIntegration.PreflightEntryOffset,
                M01LivePagingIntegration.TransitionEntryOffset,
                M01LivePagingIntegration.InvlpgEntryOffset,
                M01LivePagingIntegration.ProtectionProbeEntryOffset,
                128
            ];

            for (int index = 0; index < expected.Length; index++)
            {
                bytes.RestoreStubPages(exactCode, exactProbe);
                bytes.ClearPreparationState();
                if (index == 3)
                    bytes.ProbePage[offsets[index]] ^= 0xff;
                else
                    bytes.CodePage[offsets[index]] ^= 0xff;

                M01LivePagingPreparationResult corrupted =
                    bytes.PrepareCurrent(
                        M01LivePagingByteMode.VerifyExisting);
                if (!ExpectPreparationFailure(
                        bytes,
                        corrupted,
                        expected[index]))
                {
                    return false;
                }
            }
        }

        using (CanonicalFixture repeated = new())
        {
            M01LivePagingPreparationResult first =
                repeated.PrepareCurrent();
            if (!first.Succeeded)
                return false;

            byte[] code = repeated.CopyCodePage();
            byte[] probe = repeated.CopyProbePage();
            X64BootIdentityMappingRange[] ranges =
                (X64BootIdentityMappingRange[])
                    repeated.MappingRanges.Clone();
            X64PageTableImagePage[] pages =
                (X64PageTableImagePage[])
                    repeated.PageImages.Clone();
            ulong[] entries =
                (ulong[])repeated.Entries.Clone();
            M01LivePagingPreparationResult duplicate =
                repeated.PrepareCurrent();
            passed &= duplicate.Status
                    == M01LivePagingStatus.DuplicatePhase
                && !duplicate.Succeeded
                && repeated.PreparationState->IsPrepared
                && repeated.CodePageEquals(code)
                && repeated.ProbePageEquals(probe)
                && repeated.MappingRanges.AsSpan()
                    .SequenceEqual(ranges)
                && repeated.PageImages.AsSpan()
                    .SequenceEqual(pages)
                && repeated.Entries.AsSpan()
                    .SequenceEqual(entries);
        }

        return passed;
    }

    private static bool TestPreparationPreflightFailureIsAtomic()
    {
        using CanonicalFixture fixture = new();
        fixture.FillStubPages(0xa5);
        FillUnmanagedArray(fixture.MappingRanges, 0x31);
        FillUnmanagedArray(fixture.PageImages, 0x52);
        FillUnmanagedArray(fixture.Entries, 0x73);
        ((byte*)fixture.PreparationState)[
            M01LivePagingPreparationState.RequiredStorageBytes] =
                0x94;

        byte[] recordsBefore = CopyBytes(
            (byte*)fixture.Records,
            3UL * (ulong)sizeof(BootMemoryRegionRecord));
        byte[] ledgerBefore = CopyBytes(
            (byte*)fixture.Ledger,
            fixture.LedgerCapacity
                * (ulong)sizeof(BootReservedRange));
        byte[] requirementsBefore =
            CopyUnmanagedArray(fixture.MappingRequirements);
        byte[] rangesBefore =
            CopyUnmanagedArray(fixture.MappingRanges);
        byte[] pagesBefore =
            CopyUnmanagedArray(fixture.PageImages);
        byte[] entriesBefore =
            CopyUnmanagedArray(fixture.Entries);
        byte[] codeBefore = fixture.CopyCodePage();
        byte[] probeBefore = fixture.CopyProbePage();
        byte[] statePageBefore = CopyBytes(
            (byte*)fixture.PreparationState,
            BootReservedRange.PageSize);

        X64PagingModePreflightObservation malformed = default;
        byte[] observationBefore = CopyBytes(
            (byte*)&malformed,
            X64PagingModePreflightObservation.StructureSize);
        M01LivePagingPreparationResult result =
            fixture.PreparePreflight(&malformed, 1, 1);

        return result.Status
                == M01LivePagingStatus.PreflightRejected
            && result.LastCompletedCheckpoint
                == M01LivePagingCheckpoint.ReservationsValidated
            && result.ExecutionMode
                == M01LivePagingExecutionMode.PreparationOnly
            && !result.Succeeded
            && !result.PreparedForCompositeTransition
            && result.State.Size == 0
            && fixture.PreparationStateIsZero()
            && BytesEqual(
                recordsBefore,
                (byte*)fixture.Records)
            && BytesEqual(
                ledgerBefore,
                (byte*)fixture.Ledger)
            && BytesEqual(
                requirementsBefore,
                fixture.MappingRequirements)
            && BytesEqual(
                rangesBefore,
                fixture.MappingRanges)
            && BytesEqual(
                pagesBefore,
                fixture.PageImages)
            && BytesEqual(
                entriesBefore,
                fixture.Entries)
            && fixture.CodePageEquals(codeBefore)
            && fixture.ProbePageEquals(probeBefore)
            && BytesEqual(
                statePageBefore,
                (byte*)fixture.PreparationState)
            && BytesEqual(
                observationBefore,
                (byte*)&malformed);
    }

    private static bool TestStableSurfaceAndDefaults()
    {
        (string Name, byte Value)[] statuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("StageTwoRejected", 2),
            ("AllocationTableRejected", 3),
            ("AllocationEvidenceRejected", 4),
            ("MapLedgerEvidenceRejected", 5),
            ("ReservationLedgerRejected", 6),
            ("ReservationIdentityRejected", 7),
            ("CoordinatorStateRejected", 8),
            ("DuplicatePhase", 9),
            ("IdentityMappingRejected", 10),
            ("TableStorageRejected", 11),
            ("TableConstructionRejected", 12),
            ("PreflightStubRejected", 13),
            ("PreflightRejected", 14),
            ("TransitionStubRejected", 15),
            ("TransitionEvidenceRejected", 16),
            ("Cr3PlanRejected", 17),
            ("Cr3TranscriptRejected", 18),
            ("InvlpgStubRejected", 19),
            ("ProtectionProbeStubRejected", 20),
            ("ProtectionMutationRejected", 21),
            ("ProtectionProbeRejected", 22),
            ("PageZeroRejected", 23),
            ("IllegalCoordinatorState", 24),
            ("ByteModeRejected", 25),
            ("CleanupPlanRejected", 26)
        ];
        (string Name, byte Value)[] checkpoints =
        [
            ("Uninitialized", 0),
            ("StageTwoValidated", 1),
            ("AllocationsValidated", 2),
            ("MapLedgerValidated", 3),
            ("ReservationsValidated", 4),
            ("IdentityMappingPlanned", 5),
            ("StubBytesVerified", 6),
            ("TableImageBuilt", 7),
            ("PagingModeValidated", 8),
            ("Cr3PlanValidated", 9),
            ("Cr3TranscriptValidated", 10),
            ("ProtectionPlanValidated", 11),
            ("PageZeroValidated", 12),
            ("Completed", 13),
            ("PreparedForCompositeTransition", 14)
        ];
        (string Name, byte Value)[] byteModes =
        [
            ("Uninitialized", 0),
            ("EmitAndVerify", 1),
            ("VerifyExisting", 2)
        ];
        (string Name, byte Value)[] cleanupStatuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("InvalidCompletedCount", 2),
            ("InvalidAllocationStorage", 3),
            ("InvalidAllocationRecord", 4),
            ("FirmwareServicesUnavailable", 5),
            ("OwnedRootNotRestored", 6)
        ];

        M01LivePagingIntegrationResult empty = default;
        M01LivePagingAllocationEvidence emptyAllocation = default;
        M01LivePagingMapLedgerEvidence emptyMap = default;
        M01LivePagingCleanupPlan emptyCleanup = default;
        M01LivePagingTableBuildResult emptyTable = default;
        M01LivePagingStaticState emptyState = default;

        return EnumContractMatches<M01LivePagingStatus>(statuses)
            && EnumContractMatches<M01LivePagingCheckpoint>(
                checkpoints)
            && EnumContractMatches<M01LivePagingByteMode>(byteModes)
            && EnumContractMatches<M01LivePagingCleanupStatus>(
                cleanupStatuses)
            && M01LivePagingAllocationEvidence.CurrentVersion == 1
            && M01LivePagingAllocationEvidence.StructureSize == 88
            && M01LivePagingAllocationEvidence.EfiSuccess == 0
            && M01LivePagingMapLedgerEvidence.CurrentVersion == 1
            && M01LivePagingMapLedgerEvidence.StructureSize == 168
            && M01LivePagingCleanupPlan.StructureSize == 48
            && M01LivePagingTableBuildResult.StructureSize == 80
            && M01LivePagingStaticState.CurrentVersion == 1
            && M01LivePagingStaticState.RequiredStorageAlignment == 8
            && M01LivePagingStaticState.RequiredStorageBytes == 256
            && Unsafe.SizeOf<M01LivePagingAllocationEvidence>()
                == M01LivePagingAllocationEvidence.StructureSize
            && Unsafe.SizeOf<M01LivePagingMapLedgerEvidence>()
                == M01LivePagingMapLedgerEvidence.StructureSize
            && (ulong)Unsafe.SizeOf<M01LivePagingCleanupPlan>()
                == M01LivePagingCleanupPlan.StructureSize
            && (ulong)Unsafe.SizeOf<M01LivePagingTableBuildResult>()
                == M01LivePagingTableBuildResult.StructureSize
            && (ulong)Unsafe.SizeOf<M01LivePagingStaticState>()
                == M01LivePagingStaticState.RequiredStorageBytes
            && Offset<M01LivePagingStaticState>(
                "StageTwoStateAddressValue") == 16
            && Offset<M01LivePagingStaticState>(
                "Cr3CodeReservationValue") == 48
            && Offset<M01LivePagingStaticState>(
                "OwnedRootValue") == 168
            && Offset<M01LivePagingStaticState>(
                "PageZeroAbsentValue") == 248
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingAllocationEvidence>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingMapLedgerEvidence>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingCleanupPlan>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingTableBuildResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingStaticState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingEvidence>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                M01LivePagingIntegrationResult>()
            && empty.Status == M01LivePagingStatus.Uninitialized
            && empty.LastCompletedCheckpoint
                == M01LivePagingCheckpoint.Uninitialized
            && !empty.Succeeded
            && emptyAllocation.Size == 0
            && emptyAllocation.Provenance
                == M01LivePagingAllocationProvenance.Uninitialized
            && emptyMap.Size == 0
            && emptyMap.Provenance
                == M01LivePagingMapLedgerProvenance.Uninitialized
            && emptyCleanup.Status
                == M01LivePagingCleanupStatus.Uninitialized
            && !emptyCleanup.Succeeded
            && emptyTable.Status
                == M01LivePagingTableBuildStatus.Uninitialized
            && !emptyTable.Succeeded
            && emptyState.Size == 0
            && emptyState.Version == 0
            && emptyState.Status == M01LivePagingStatus.Uninitialized
            && emptyState.Checkpoint
                == M01LivePagingCheckpoint.Uninitialized
            && !emptyState.IsCompleted
            && !M01LivePagingIntegration.CallsFirmwarePageAllocator
            && !M01LivePagingIntegration.InvokesFirmware
            && !M01LivePagingIntegration.ExecutesPrivilegedInstructions
            && !M01LivePagingIntegration.ExecutesGeneratedCode
            && !M01LivePagingIntegration.PerformsLiveCr3Load
            && !M01LivePagingIntegration.PerformsLivePageProtection
            && !M01LivePagingIntegration.PerformsLiveTlbInvalidation
            && !M01LivePagingIntegration.DereferencesPageZero
            && !M01LivePagingIntegration.IsGuestEvidence
            && !M01LivePagingIntegration
                .ProvesCompositeOwnedCr3MachineCodeInterval
            && !M01LivePagingIntegration
                .ModelsFutureCompositeOwnedCr3Sequence
            && M01LivePagingIntegration
                .ComposesSeparateFutureCompositeLeaves
            && !M01LivePagingIntegration
                .RestoresCallerOwnedInitialTableImage
            && M01LivePagingIntegration
                .RestoresModeledProbeLeafToInitialProtection
            && !M01LivePagingIntegration
                .ProvesOwnedSerialDiagnostics
            && !M01LivePagingIntegration
                .ProvesAcpiOrFramebufferSourceProvenance
            && !M01LivePagingIntegration
                .EnforcesPerSectionLoadedImageProtections
            && M01LivePagingIntegration
                .CrossBindsStageOneSwitchStub
            && !M01LivePagingIntegration
                .CrossBindsOwnedSerialStub
            && M01LivePagingIntegration
                .SerialPurposeIsCompatibilityAliasOnly
            && !M01LivePagingIntegration
                .CrossBindsOwnedMapIdentity
            && !M01LivePagingIntegration
                .CrossBindsDiagnosticState
            && !M01LivePagingIntegration.SupportsPcide
            && !M01LivePagingIntegration.SupportsFiveLevelPaging
            && !M01LivePagingIntegration.EliminatesNmiOrSmiRisk
            && M01LivePagingIntegration.RequiredAllocationRecordCount
                == 3
            && M01LivePagingIntegration.MaximumAllocationRecordCapacity
                == 16
            && M01LivePagingIntegration.Cr3CodeAllocationRecordIndex
                == 0
            && M01LivePagingIntegration
                .ProtectionProbeAllocationRecordIndex == 1
            && M01LivePagingIntegration
                .TableStateAllocationRecordIndex == 2
            && M01LivePagingIntegration.RequiredCodePageCount == 1
            && M01LivePagingIntegration.RequiredProbePageCount == 1
            && M01LivePagingIntegration.RequiredPointerAlignment == 8
            && M01LivePagingIntegration
                .RequiredNonImageMappingRequirementCount == 10
            && M01LivePagingIntegration.PageSize == 4096
            && M01LivePagingIntegration.PreflightEntryOffset == 0
            && M01LivePagingIntegration.TransitionEntryOffset == 256
            && M01LivePagingIntegration.InvlpgEntryOffset == 2240
            && M01LivePagingIntegration.ProtectionProbeEntryOffset == 0
            && M01LivePagingIntegration.NoIndex == ~0UL;
    }

    private static bool TestCanonicalAggregateAndBindings()
    {
        using CanonicalFixture fixture = new();
        M01LivePagingIntegrationResult result =
            fixture.ExecuteCurrent();
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "canonical status=" + result.Status
                + " checkpoint=" + result.LastCompletedCheckpoint
                + " table=" + result.Evidence.TableBuild.Status
                + " cr3=" + result.Evidence.Cr3Plan.Status
                + " protect=" + result.Evidence.ProtectionPlan.Status);
        }

        M01LivePagingEvidence evidence = result.Evidence;
        M01LivePagingStaticState state = result.StaticState;
        X64PageTableEntryResult before =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                evidence.ProtectionMutation.PreviousEntry.RawValue,
                fixture.Preconditions);
        X64PageTableEntryResult after =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                evidence.ProtectionMutation.ReplacementEntry.RawValue,
                fixture.Preconditions);
        X64PageTableEntryResult readback =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                fixture.Entries[
                    checked((int)evidence.TableBuild
                        .ProbeLeafEntryIndex)],
                fixture.Preconditions);
        bool everyTableStatePageIsReadWrite =
            AllocationPagesHaveExactProtection(
                fixture.Records[
                    M01LivePagingIntegration
                        .TableStateAllocationRecordIndex],
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                evidence.TableBuild.RootTable,
                fixture.PageImages,
                evidence.TableBuild.TablePageCount,
                fixture.Entries,
                evidence.TableBuild.EntryCount,
                fixture.Preconditions);

        return result.Status == M01LivePagingStatus.Success
            && result.LastCompletedCheckpoint
                == M01LivePagingCheckpoint.Completed
            && fixture.StaticState->IsCompleted
            && state.IsCompleted
            && state.Size
                == M01LivePagingStaticState.RequiredStorageBytes
            && state.Version == M01LivePagingStaticState.CurrentVersion
            && state.StageTwoStateAddress
                == (ulong)fixture.StageTwoState
            && state.StageTwoStateCapacity
                == M01LiveAllocatorStaticState.RequiredStorageBytes
            && state.RefreshedMapKey
                == fixture.StageThreeMap.MapKey
            && state.RefreshedMapGeneration
                == CanonicalFixture.StageThreeMapGeneration
            && state.Cr3CodeReservation.Equals(
                fixture.FindReservation(
                    BootReservationKind.Cr3TransitionStub))
            && state.ProtectionProbeReservation.Equals(
                fixture.FindReservation(
                    BootReservationKind.PageProtectionProbe))
            && state.TableStateReservation.Equals(
                fixture.FindReservation(
                    BootReservationKind.PageTableArena))
            && Raw(state.OwnedRoot) == fixture.TableStateStart
            && state.RawFirmwareCr3 == fixture.RawFirmwareCr3
            && state.RawCr4 == CanonicalFixture.ValidCr4
            && state.OriginalRflags == CanonicalFixture.ValidRflags
            && state.FinalCanary == CanonicalFixture.UpdatedCanary
            && state.Cr3EventCount
                == X64Cr3LoadStubPlan.RequiredEventCount
            && state.ProtectionEventCount
                == X64ProtectionProbeStubPlan.RequiredEventCount
            && state.TablePageCount
                == evidence.MappingPlan.TotalTablePageCount
            && state.MappedPageCount
                == evidence.MappingPlan.UnionPageCount
            && state.PageZeroAbsent
            && evidence.AllocationEvidence.IsValidFor(
                fixture.Records,
                3,
                3)
            && evidence.MapLedgerEvidence.IsValidFor(
                *fixture.StageTwoState,
                evidence.AllocationEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity)
            && evidence.ReservationValidation.Succeeded
            && evidence.MappingPlan.Succeeded
            && evidence.TableBuild.Succeeded
            && evidence.TableBuild.RootTable.Equals(state.OwnedRoot)
            && evidence.TableBuild.TablePageCount
                == evidence.MappingPlan.TotalTablePageCount
            && evidence.TableBuild.EntryCount
                == evidence.TableBuild.TablePageCount
                    * X64PageTableImagePage.EntryCountPerPage
            && fixture.MappingRequirementCount
                == fixture.FindReservation(
                        BootReservationKind.LoadedImage)
                    .PageCount
                    + M01LivePagingIntegration
                        .RequiredNonImageMappingRequirementCount
            && fixture.LedgerCount == 10
            && CanonicalIdentityRequirementsMatch(fixture)
            && everyTableStatePageIsReadWrite
            && before.Succeeded
            && before.Protection == M01014TestData.ReadWrite
            && after.Succeeded
            && after.Protection == M01014TestData.ReadExecute
            && after.PhysicalAddress.Equals(
                Physical(fixture.ProbeStart))
            && readback.Succeeded
            && readback.Protection == M01014TestData.ReadWrite
            && readback.PhysicalAddress.Equals(
                Physical(fixture.ProbeStart))
            && evidence.ProtectionMutation.TargetEntryIndex
                == evidence.TableBuild.ProbeLeafEntryIndex
            && evidence.InvlpgReadback.ReadBackVerified
            && evidence.InvlpgReadback.Target.Target.Equals(
                Virtual(fixture.ProbeStart))
            && evidence.ProbeReadback.ReadBackVerified
            && evidence.ProbeReadback.EntryAddress.Equals(
                Virtual(fixture.ProbeStart))
            && evidence.ProtectionPlan.Succeeded
            && !fixture.ProtectionObservation.IsLiveEvidence
            && evidence.PageZeroAudit
                .ProvesPageZeroAbsentInSuppliedImage
            && evidence.PageZeroAudit.Status
                == PageZeroMappingPolicyStatus.Success
            && evidence.Preflight.RawFirmwareCr3
                == fixture.RawFirmwareCr3
            && evidence.TransitionEvidence.State.OriginalCr3Raw
                == fixture.RawFirmwareCr3
            && evidence.TransitionEvidence.State.FirmwareCr3Readback
                == fixture.RawFirmwareCr3
            && evidence.Cr3Audit.FinalRawCr3
                == fixture.RawFirmwareCr3
            && evidence.Cr3Audit.FinalCanaryValue
                == CanonicalFixture.UpdatedCanary;
    }

    private static bool TestByteModesAndCorruption()
    {
        using CanonicalFixture fixture = new();
        fixture.FillStubPages(0xcc);
        M01LivePagingIntegrationResult emitted =
            fixture.ExecuteCurrent(M01LivePagingByteMode.EmitAndVerify);
        if (!emitted.Succeeded || !fixture.PaddingIsZero())
            return false;

        byte[] exactCode = fixture.CopyCodePage();
        byte[] exactProbe = fixture.CopyProbePage();
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult verified =
            fixture.ExecuteCurrent(M01LivePagingByteMode.VerifyExisting);
        if (!verified.Succeeded
            || !fixture.CodePageEquals(exactCode)
            || !fixture.ProbePageEquals(exactProbe))
        {
            return false;
        }

        M01LivePagingStatus[] expected =
        [
            M01LivePagingStatus.PreflightStubRejected,
            M01LivePagingStatus.TransitionStubRejected,
            M01LivePagingStatus.InvlpgStubRejected,
            M01LivePagingStatus.ProtectionProbeStubRejected,
            M01LivePagingStatus.ProtectionProbeStubRejected
        ];
        ulong[] offsets =
        [
            M01LivePagingIntegration.PreflightEntryOffset,
            M01LivePagingIntegration.TransitionEntryOffset,
            M01LivePagingIntegration.InvlpgEntryOffset,
            M01LivePagingIntegration.ProtectionProbeEntryOffset,
            128
        ];

        for (int index = 0; index < expected.Length; index++)
        {
            fixture.RestoreStubPages(exactCode, exactProbe);
            fixture.ClearStaticState();
            if (index == 3)
                fixture.ProbePage[offsets[index]] ^= 0xff;
            else
                fixture.CodePage[offsets[index]] ^= 0xff;

            M01LivePagingIntegrationResult corrupted =
                fixture.ExecuteCurrent(
                    M01LivePagingByteMode.VerifyExisting);
            if (corrupted.Succeeded
                || corrupted.Status != expected[index])
            {
                return false;
            }
        }

        fixture.RestoreStubPages(exactCode, exactProbe);
        fixture.ClearStaticState();
        return fixture.ExecuteCurrent(
                M01LivePagingByteMode.Uninitialized).Status
            == M01LivePagingStatus.ByteModeRejected;
    }

    private static bool TestAllocationEvidenceAndRecords()
    {
        using CanonicalFixture fixture = new();
        M01LivePagingAllocationEvidence canonical =
            fixture.AllocationEvidence;
        M01LivePagingIntegrationResult[] rejected =
        [
            fixture.ExecuteWithAllocation(
                default,
                fixture.Records,
                3,
                3),
            fixture.ExecuteWithAllocation(
                fixture.CaptureAllocationEvidence(
                    0,
                    9,
                    10),
                fixture.Records,
                3,
                3),
            fixture.ExecuteWithAllocation(
                fixture.CaptureAllocationEvidence(
                    8,
                    10,
                    11),
                fixture.Records,
                3,
                3),
            fixture.ExecuteWithAllocation(
                fixture.CaptureAllocationEvidence(
                    8,
                    9,
                    10,
                    codeStatus: 1),
                fixture.Records,
                3,
                3),
            fixture.ExecuteWithAllocation(
                fixture.CaptureAllocationEvidence(
                    8,
                    9,
                    10,
                    probeStatus: 1),
                fixture.Records,
                3,
                3),
            fixture.ExecuteWithAllocation(
                fixture.CaptureAllocationEvidence(
                    8,
                    9,
                    10,
                    tableStatus: 1),
                fixture.Records,
                3,
                3)
        ];
        foreach (M01LivePagingIntegrationResult result in rejected)
        {
            fixture.ClearStaticState();
            if (result.Status
                != M01LivePagingStatus.AllocationEvidenceRejected)
            {
                return false;
            }
        }

        BootMemoryRegionRecord saved = fixture.Records[0];
        fixture.Records[0] = WithRecord(
            saved,
            uefiType: (uint)UefiMemoryType.LoaderData);
        M01LivePagingIntegrationResult wrongCode =
            fixture.ExecuteWithAllocation(
                canonical,
                fixture.Records,
                3,
                3);
        fixture.Records[0] = saved;
        fixture.ClearStaticState();

        saved = fixture.Records[1];
        fixture.Records[1] = WithRecord(
            saved,
            pageCount: 2,
            byteLength: 2 * BootReservedRange.PageSize,
            physicalEndExclusive:
                saved.PhysicalStart
                + 2 * BootReservedRange.PageSize);
        M01LivePagingIntegrationResult wrongProbe =
            fixture.ExecuteWithAllocation(
                canonical,
                fixture.Records,
                3,
                3);
        fixture.Records[1] = saved;
        fixture.ClearStaticState();

        saved = fixture.Records[2];
        fixture.Records[2] = WithRecord(
            saved,
            physicalStart: fixture.Records[1].PhysicalStart,
            virtualStart: fixture.Records[1].VirtualStart,
            physicalEndExclusive:
                fixture.Records[1].PhysicalStart
                + saved.ByteLength);
        M01LivePagingIntegrationResult overlap =
            fixture.ExecuteWithAllocation(
                canonical,
                fixture.Records,
                3,
                3);
        fixture.Records[2] = saved;

        return wrongCode.Status
                == M01LivePagingStatus.AllocationTableRejected
            && wrongProbe.Status
                == M01LivePagingStatus.AllocationTableRejected
            && overlap.Status
                == M01LivePagingStatus.AllocationTableRejected;
    }

    private static bool TestMapLedgerEvidence()
    {
        using CanonicalFixture fixture = new();
        M01LivePagingMapLedgerEvidence[] bad =
        [
            default,
            fixture.CaptureMapEvidence(
                previousMapKey:
                    fixture.StageTwoState->RefreshedMapKey + 1),
            fixture.CaptureMapEvidence(
                previousMapGeneration:
                    fixture.StageTwoState->RefreshedMapGeneration + 1),
            fixture.CaptureMapEvidence(
                refreshedMapGeneration:
                    fixture.StageTwoState->RefreshedMapGeneration),
            fixture.CaptureMapEvidence(mapCaptureOrdinal: 10),
            fixture.CaptureMapEvidence(ledgerBuildOrdinal: 11),
            fixture.CaptureMapEvidence(
                ledger: fixture.Ledger + 1),
            fixture.CaptureMapEvidence(
                ledgerCount: fixture.LedgerCount - 1),
            fixture.CaptureMapEvidence(
                ledgerCapacity: fixture.LedgerCapacity - 1)
        ];

        foreach (M01LivePagingMapLedgerEvidence evidence in bad)
        {
            M01LivePagingIntegrationResult result =
                fixture.ExecuteWithMap(evidence);
            fixture.ClearStaticState();
            if (result.Status
                != M01LivePagingStatus.MapLedgerEvidenceRejected)
            {
                return false;
            }
        }

        FirmwareMemoryMapRecord sameKey = fixture.StageThreeMap;
        sameKey.MapKey = fixture.StageTwoState->RefreshedMapKey;
        M01LivePagingIntegrationResult duplicateKey =
            fixture.ExecuteWithMap(
                fixture.CaptureMapEvidence(refreshedMap: sameKey));
        fixture.ClearStaticState();

        FirmwareMemoryMapRecord alias = fixture.StageThreeMap;
        alias.BufferAddress = (ulong)fixture.StaticState;
        alias.SourceBufferAddress =
            alias.BufferAddress + alias.BufferSize + 8;
        M01LivePagingIntegrationResult metadataAlias =
            fixture.ExecuteWithMap(
                fixture.CaptureMapEvidence(refreshedMap: alias));

        return duplicateKey.Status
                == M01LivePagingStatus.MapLedgerEvidenceRejected
            && metadataAlias.Status
                == M01LivePagingStatus.CoordinatorStateRejected;
    }

    private static bool TestReservationLedgerEvidence()
    {
        using CanonicalFixture fixture = new();
        BootReservedRange[] saved = fixture.CopyLedger();

        fixture.RemoveReservation(
            BootReservationKind.Cr3TransitionStub);
        M01LivePagingIntegrationResult missing =
            fixture.ExecuteWithMap(fixture.RefreshMapEvidence());
        fixture.RestoreLedger(saved);
        fixture.ClearStaticState();

        fixture.ReplaceReservation(
            BootReservationKind.PageProtectionProbe,
            CreateRange(
                BootReservationKind.PageProtectionProbe,
                fixture.ProbeStart
                    - 4 * BootReservedRange.PageSize,
                1));
        M01LivePagingIntegrationResult mismatch =
            fixture.ExecuteWithMap(fixture.RefreshMapEvidence());
        fixture.RestoreLedger(saved);
        fixture.ClearStaticState();

        fixture.Ledger[fixture.LedgerCount - 1] =
            fixture.FindReservation(
                BootReservationKind.Cr3TransitionStub);
        M01LivePagingIntegrationResult duplicate =
            fixture.ExecuteWithMap(fixture.RefreshMapEvidence());
        fixture.RestoreLedger(saved);
        fixture.ClearStaticState();

        (fixture.Ledger[0], fixture.Ledger[1]) =
            (fixture.Ledger[1], fixture.Ledger[0]);
        M01LivePagingIntegrationResult unsorted =
            fixture.ExecuteWithMap(fixture.RefreshMapEvidence());

        bool passed =
            missing.Status
                == M01LivePagingStatus.ReservationIdentityRejected
            && mismatch.Status
                == M01LivePagingStatus.ReservationIdentityRejected
            && duplicate.Status
                == M01LivePagingStatus.ReservationLedgerRejected
            && unsorted.Status
                == M01LivePagingStatus.ReservationLedgerRejected;
        if (!passed)
        {
            throw new InvalidOperationException(
                "reservation statuses="
                + missing.Status + "/"
                + mismatch.Status + "/"
                + duplicate.Status + "/"
                + unsorted.Status);
        }
        return true;
    }

    private static bool TestIdentityMappingCrossBindings()
    {
        using CanonicalFixture fixture = new();
        X64BootIdentityMappingRequirement[] canonical =
            (X64BootIdentityMappingRequirement[])
                fixture.MappingRequirements.Clone();
        if (canonical.Length
                != checked((int)(
                    fixture.FindReservation(
                        BootReservationKind.LoadedImage).PageCount
                    + M01LivePagingIntegration
                        .RequiredNonImageMappingRequirementCount))
            || fixture.LedgerCount != 10)
        {
            return false;
        }

        int executingImagePage0 = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.ExecutingImage,
            occurrence: 0);
        int executingImagePage1 = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.ExecutingImage,
            occurrence: 1);
        int executingImagePage2 = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.ExecutingImage,
            occurrence: 2);
        int bootHandoff = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.BootHandoff);
        int acpi = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.AcpiScratch);
        int framebuffer = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.FramebufferMetadata);
        int stack = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.OwnedStack);
        int guard = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.BootStackGuard);
        int serial = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.SerialStub);
        int additional = FindRequirementIndex(
            canonical,
            X64BootIdentityMappingPurpose.AdditionalRequired);
        if (executingImagePage0 < 0
            || executingImagePage1 < 0
            || executingImagePage2 < 0
            || bootHandoff < 0
            || acpi < 0
            || framebuffer < 0
            || stack < 0
            || guard < 0
            || serial < 0
            || additional < 0)
        {
            return false;
        }

        BootReservedRange loadedImage =
            fixture.FindReservation(
                BootReservationKind.LoadedImage);
        BootReservedRange acpiReservation =
            fixture.FindReservation(
                BootReservationKind.AcpiScratch);
        BootReservedRange framebufferReservation =
            fixture.FindReservation(
                BootReservationKind.FramebufferMetadata);
        BootReservedRange transitionStub =
            fixture.FindReservation(
                BootReservationKind.Cr3TransitionStub);
        (
            string Name,
            int Index,
            X64BootIdentityMappingRequirement Replacement
        )[] arbitraryBindings =
        [
            (
                "executing-image",
                executingImagePage0,
                RequirementForReservation(
                    X64BootIdentityMappingPurpose.ExecutingImage,
                    acpiReservation,
                    PageProtection.Read)),
            (
                "boot-handoff",
                bootHandoff,
                RequirementForReservation(
                    X64BootIdentityMappingPurpose.BootHandoff,
                    framebufferReservation,
                    PageProtection.Read)),
            (
                "acpi-scratch",
                acpi,
                RequirementForReservation(
                    X64BootIdentityMappingPurpose.AcpiScratch,
                    framebufferReservation,
                    PageProtection.Read)),
            (
                "framebuffer-metadata",
                framebuffer,
                RequirementForReservation(
                    X64BootIdentityMappingPurpose
                        .FramebufferMetadata,
                    acpiReservation,
                    PageProtection.Read)),
            (
                "owned-stack",
                stack,
                RequirementForReservation(
                    X64BootIdentityMappingPurpose.OwnedStack,
                    loadedImage,
                    M01014TestData.ReadWrite)),
            (
                "stack-guard",
                guard,
                RequirementForReservation(
                    X64BootIdentityMappingPurpose.BootStackGuard,
                    acpiReservation,
                    PageProtection.None,
                    PageCachePolicy.Uninitialized)),
            (
                "serial-stub",
                serial,
                RequirementForReservation(
                    X64BootIdentityMappingPurpose.SerialStub,
                    transitionStub,
                    M01014TestData.ReadExecute)),
            (
                "additional-required",
                additional,
                RequirementForReservation(
                    X64BootIdentityMappingPurpose
                        .AdditionalRequired,
                    loadedImage,
                    M01014TestData.ReadWrite))
        ];

        for (int index = 0;
            index < arbitraryBindings.Length;
            index++)
        {
            X64BootIdentityMappingRequirement[] mutated =
                (X64BootIdentityMappingRequirement[])
                    canonical.Clone();
            mutated[arbitraryBindings[index].Index] =
                arbitraryBindings[index].Replacement;
            fixture.ClearStaticState();
            M01LivePagingIntegrationResult result =
                fixture.ExecuteRequirements(mutated);
            if (result.Status
                    != M01LivePagingStatus.IdentityMappingRejected
                || result.LastCompletedCheckpoint
                    != M01LivePagingCheckpoint
                        .ReservationsValidated)
            {
                Console.WriteLine(
                    "M01_019_BINDING_DIAGNOSTIC "
                    + arbitraryBindings[index].Name
                    + " status=" + result.Status
                    + " checkpoint="
                    + result.LastCompletedCheckpoint);
                return false;
            }
        }

        X64BootIdentityMappingRequirement[] missing =
            RemoveRequirementAt(canonical, executingImagePage2);
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult missingResult =
            fixture.ExecuteRequirements(missing);

        X64BootIdentityMappingRequirement[] duplicate =
            (X64BootIdentityMappingRequirement[])canonical.Clone();
        duplicate[executingImagePage2] =
            canonical[executingImagePage1];
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult duplicateResult =
            fixture.ExecuteRequirements(duplicate);

        return missingResult.Status
                == M01LivePagingStatus.IdentityMappingRejected
            && missingResult.LastCompletedCheckpoint
                == M01LivePagingCheckpoint.ReservationsValidated
            && duplicateResult.Status
                == M01LivePagingStatus.IdentityMappingRejected
            && duplicateResult.LastCompletedCheckpoint
                == M01LivePagingCheckpoint.ReservationsValidated;
    }

    private static bool TestPointerCountCapacityAliasAndOverflow()
    {
        using CanonicalFixture fixture = new();

        if (fixture.ExecuteMetadata(
                null,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes).Status
                != M01LivePagingStatus.StageTwoRejected
            || fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes - 1,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes).Status
                != M01LivePagingStatus.StageTwoRejected)
        {
            return false;
        }

        M01LivePagingIntegrationResult nullAlloc =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                null,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        M01LivePagingIntegrationResult shortAlloc =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                2,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        M01LivePagingIntegrationResult extraAllocCapacity =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                4,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        M01LivePagingIntegrationResult overflowAlloc =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                (BootMemoryRegionRecord*)(~0UL - 7),
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        if (nullAlloc.Status
                != M01LivePagingStatus.AllocationTableRejected
            || shortAlloc.Status
                != M01LivePagingStatus.AllocationTableRejected
            || extraAllocCapacity.Status
                != M01LivePagingStatus.AllocationTableRejected
            || overflowAlloc.Status
                != M01LivePagingStatus.AllocationTableRejected)
        {
            return false;
        }

        M01LivePagingIntegrationResult nullLedger =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                null,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        M01LivePagingIntegrationResult zeroLedgerCount =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                0,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        M01LivePagingIntegrationResult ledgerCountOverCapacity =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCapacity + 1,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        M01LivePagingIntegrationResult overflowLedger =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                (BootReservedRange*)(~0UL - 7),
                1,
                BootReservationTableValidator
                    .MaximumReservationCount,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        if (nullLedger.Status
                != M01LivePagingStatus.MapLedgerEvidenceRejected
            || zeroLedgerCount.Status
                != M01LivePagingStatus.MapLedgerEvidenceRejected
            || ledgerCountOverCapacity.Status
                != M01LivePagingStatus.MapLedgerEvidenceRejected
            || overflowLedger.Status
                != M01LivePagingStatus.MapLedgerEvidenceRejected)
        {
            return false;
        }

        M01LivePagingIntegrationResult nullState =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                null,
                M01LivePagingStaticState.RequiredStorageBytes);
        M01LivePagingIntegrationResult shortState =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                fixture.StaticState,
                M01LivePagingStaticState.RequiredStorageBytes - 1);
        M01LivePagingIntegrationResult stateAliasesStageTwo =
            fixture.ExecuteMetadata(
                fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                fixture.StackSwitchPlan,
                fixture.Records,
                3,
                3,
                fixture.AllocationEvidence,
                fixture.MapEvidence,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                (M01LivePagingStaticState*)fixture.StageTwoState,
                M01LivePagingStaticState.RequiredStorageBytes);
        if (nullState.Status
                != M01LivePagingStatus.CoordinatorStateRejected
            || shortState.Status
                != M01LivePagingStatus.CoordinatorStateRejected
            || stateAliasesStageTwo.Status
                != M01LivePagingStatus.CoordinatorStateRejected)
        {
            return false;
        }

        M01LivePagingIntegrationResult nullRequirements =
            fixture.ExecuteWorkspace(
                null,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCapacity,
                fixture.MappingRanges,
                fixture.MappingRangeCapacity,
                fixture.PageImages,
                fixture.PageImageCapacity,
                fixture.Entries,
                fixture.EntryCapacity,
                fixture.CodePage,
                BootReservedRange.PageSize,
                fixture.ProbePage,
                BootReservedRange.PageSize);
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult shortRequirementCapacity =
            fixture.ExecuteWorkspace(
                fixture.MappingRequirements,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCount - 1,
                fixture.MappingRanges,
                fixture.MappingRangeCapacity,
                fixture.PageImages,
                fixture.PageImageCapacity,
                fixture.Entries,
                fixture.EntryCapacity,
                fixture.CodePage,
                BootReservedRange.PageSize,
                fixture.ProbePage,
                BootReservedRange.PageSize);
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult nullPages =
            fixture.ExecuteWorkspace(
                fixture.MappingRequirements,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCapacity,
                fixture.MappingRanges,
                fixture.MappingRangeCapacity,
                null,
                fixture.PageImageCapacity,
                fixture.Entries,
                fixture.EntryCapacity,
                fixture.CodePage,
                BootReservedRange.PageSize,
                fixture.ProbePage,
                BootReservedRange.PageSize);
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult shortEntries =
            fixture.ExecuteWorkspace(
                fixture.MappingRequirements,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCapacity,
                fixture.MappingRanges,
                fixture.MappingRangeCapacity,
                fixture.PageImages,
                fixture.PageImageCapacity,
                fixture.Entries,
                fixture.Mapping.TotalTablePageCount
                    * X64PageTableImagePage.EntryCountPerPage - 1,
                fixture.CodePage,
                BootReservedRange.PageSize,
                fixture.ProbePage,
                BootReservedRange.PageSize);
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult aliasedPageStorage =
            fixture.ExecuteWorkspace(
                fixture.MappingRequirements,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCapacity,
                fixture.MappingRanges,
                fixture.MappingRangeCapacity,
                fixture.PageImages,
                fixture.PageImageCapacity,
                fixture.Entries,
                fixture.EntryCapacity,
                fixture.CodePage,
                BootReservedRange.PageSize,
                fixture.ProbePage,
                BootReservedRange.PageSize,
                aliasPageStorage: true);
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult shortCode =
            fixture.ExecuteWorkspace(
                fixture.MappingRequirements,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCapacity,
                fixture.MappingRanges,
                fixture.MappingRangeCapacity,
                fixture.PageImages,
                fixture.PageImageCapacity,
                fixture.Entries,
                fixture.EntryCapacity,
                fixture.CodePage,
                BootReservedRange.PageSize - 1,
                fixture.ProbePage,
                BootReservedRange.PageSize);
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult aliasedProbe =
            fixture.ExecuteWorkspace(
                fixture.MappingRequirements,
                fixture.MappingRequirementCount,
                fixture.MappingRequirementCapacity,
                fixture.MappingRanges,
                fixture.MappingRangeCapacity,
                fixture.PageImages,
                fixture.PageImageCapacity,
                fixture.Entries,
                fixture.EntryCapacity,
                fixture.CodePage,
                BootReservedRange.PageSize,
                fixture.CodePage,
                BootReservedRange.PageSize);

        bool workspacePassed =
            nullRequirements.Status
                == M01LivePagingStatus.CoordinatorStateRejected
            && shortRequirementCapacity.Status
                == M01LivePagingStatus.CoordinatorStateRejected
            && nullPages.Status
                == M01LivePagingStatus.CoordinatorStateRejected
            && shortEntries.Status
                == M01LivePagingStatus.TableStorageRejected
            && aliasedPageStorage.Status
                == M01LivePagingStatus.CoordinatorStateRejected
            && shortCode.Status
                == M01LivePagingStatus.CoordinatorStateRejected
            && aliasedProbe.Status
                == M01LivePagingStatus.CoordinatorStateRejected;
        if (!workspacePassed)
        {
            throw new InvalidOperationException(
                "workspace statuses="
                + nullRequirements.Status + "/"
                + shortRequirementCapacity.Status + "/"
                + nullPages.Status + "/"
                + shortEntries.Status + "/"
                + aliasedPageStorage.Status + "/"
                + shortCode.Status + "/"
                + aliasedProbe.Status);
        }
        return true;
    }

    private static bool TestCleanupPlans()
    {
        using CanonicalFixture fixture = new();
        for (ulong completed = 0; completed <= 3; completed++)
        {
            M01LivePagingCleanupPlan plan =
                M01LivePagingIntegration.PlanCleanup(
                    fixture.Records,
                    3,
                    3,
                    completed,
                    firmwareServicesAvailable: true,
                    ownedRootWasEntered: completed == 3,
                    ownedRootRestored: true);
            if (!plan.Succeeded
                || plan.CompletedAllocationCount != completed
                || plan.ReleaseCount != completed
                || plan.FirstReleaseIndex
                    != (completed == 0 ? ~0UL : completed - 1)
                || plan.SecondReleaseIndex
                    != (completed < 2 ? ~0UL : completed - 2)
                || plan.ThirdReleaseIndex
                    != (completed == 3 ? 0UL : ~0UL))
            {
                return false;
            }
        }

        M01LivePagingCleanupPlan noFirmware =
            M01LivePagingIntegration.PlanCleanup(
                fixture.Records,
                3,
                3,
                3,
                firmwareServicesAvailable: false,
                ownedRootWasEntered: false,
                ownedRootRestored: false);
        M01LivePagingCleanupPlan enteredNotRestored =
            M01LivePagingIntegration.PlanCleanup(
                fixture.Records,
                3,
                3,
                3,
                firmwareServicesAvailable: true,
                ownedRootWasEntered: true,
                ownedRootRestored: false);
        M01LivePagingCleanupPlan tooMany =
            M01LivePagingIntegration.PlanCleanup(
                fixture.Records,
                3,
                3,
                4,
                firmwareServicesAvailable: true,
                ownedRootWasEntered: false,
                ownedRootRestored: false);
        M01LivePagingCleanupPlan nullStorage =
            M01LivePagingIntegration.PlanCleanup(
                null,
                3,
                3,
                1,
                firmwareServicesAvailable: true,
                ownedRootWasEntered: false,
                ownedRootRestored: false);
        BootMemoryRegionRecord saved = fixture.Records[1];
        fixture.Records[1] = default;
        M01LivePagingCleanupPlan invalidRecord =
            M01LivePagingIntegration.PlanCleanup(
                fixture.Records,
                3,
                3,
                2,
                firmwareServicesAvailable: true,
                ownedRootWasEntered: false,
                ownedRootRestored: false);
        fixture.Records[1] = saved;
        fixture.Records[1] = WithRecord(
            saved,
            physicalStart: fixture.Records[0].PhysicalStart,
            virtualStart: fixture.Records[0].VirtualStart,
            physicalEndExclusive:
                fixture.Records[0].PhysicalEndExclusive);
        M01LivePagingCleanupPlan overlappingRecords =
            M01LivePagingIntegration.PlanCleanup(
                fixture.Records,
                3,
                3,
                2,
                firmwareServicesAvailable: true,
                ownedRootWasEntered: false,
                ownedRootRestored: false);
        fixture.Records[1] = saved;

        return noFirmware.Status
                == M01LivePagingCleanupStatus
                    .FirmwareServicesUnavailable
            && noFirmware.ReleaseCount == 0
            && enteredNotRestored.Status
                == M01LivePagingCleanupStatus.OwnedRootNotRestored
            && enteredNotRestored.ReleaseCount == 0
            && tooMany.Status
                == M01LivePagingCleanupStatus.InvalidCompletedCount
            && nullStorage.Status
                == M01LivePagingCleanupStatus.InvalidAllocationStorage
            && invalidRecord.Status
                == M01LivePagingCleanupStatus.InvalidAllocationRecord
            && overlappingRecords.Status
                == M01LivePagingCleanupStatus.InvalidAllocationRecord;
    }

    private static bool TestStageTwoAndStackPlanBindings()
    {
        using CanonicalFixture fixture = new();
        M01LivePagingIntegrationResult defaultPlan =
            fixture.ExecuteWithStack(default);
        fixture.ClearStaticState();
        M01LivePagingIntegrationResult mismatch =
            fixture.ExecuteWithStack(
                fixture.CreateMismatchedStackPlan());
        fixture.ClearStaticState();

        byte[] stageTwo =
            CopyBytes(
                (byte*)fixture.StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes);
        stageTwo[0] ^= 1;
        fixed (byte* copy = stageTwo)
        {
            M01LivePagingIntegrationResult corrupt =
                fixture.ExecuteMetadata(
                    (M01LiveAllocatorStaticState*)copy,
                    M01LiveAllocatorStaticState.RequiredStorageBytes,
                    fixture.StackSwitchPlan,
                    fixture.Records,
                    3,
                    3,
                    fixture.AllocationEvidence,
                    fixture.MapEvidence,
                    fixture.Ledger,
                    fixture.LedgerCount,
                    fixture.LedgerCapacity,
                    fixture.StaticState,
                    M01LivePagingStaticState.RequiredStorageBytes);
            if (corrupt.Status
                != M01LivePagingStatus.StageTwoRejected)
            {
                return false;
            }
        }

        return defaultPlan.Status
                == M01LivePagingStatus.StageTwoRejected
            && mismatch.Status
                == M01LivePagingStatus.StageTwoRejected;
    }

    private static bool TestNegativeLeafEvidence()
    {
        using CanonicalFixture transition = new();
        *transition.TransitionState = default;
        M01LivePagingIntegrationResult transitionFailure =
            transition.ExecuteCurrent();

        using CanonicalFixture transcript = new();
        transcript.Cr3Events[3] =
            transcript.CreateCr3Event(
                X64Cr3ProbeEventKind.OwnedRootReadBack,
                ownedRoot: false,
                updatedCanary: false,
                elapsed: 4);
        M01LivePagingIntegrationResult transcriptFailure =
            transcript.ExecuteCurrent();

        using CanonicalFixture protection = new();
        protection.ProtectionObservation =
            X64ProtectionProbeObservation.CreateModeled(
                X64ProtectionProbeTerminalKind.Timeout,
                Virtual(protection.ProbeStart),
                1,
                X64ProtectionProbeStubPlan.ExpectedReturnValue,
                CanonicalFixture.ProtectionCanary,
                CanonicalFixture.ProtectionCanary);
        M01LivePagingIntegrationResult protectionFailure =
            protection.ExecuteCurrent();

        using CanonicalFixture protectionEvents = new();
        protectionEvents.ProtectionEvents[4] =
            new X64ProtectionProbeEvent(
                X64ProtectionProbeEventKind
                    .InitialReadWriteNoExecuteVerified,
                4);
        M01LivePagingIntegrationResult eventFailure =
            protectionEvents.ExecuteCurrent();

        using M01014PageTableFixture pageZero = new();
        X64PageMapResult mapped = pageZero.Apply(
            X64PageMapRequest.CreateMap(
                pageZero.Root,
                pageZero.Target,
                Physical(0x0090_0000),
                M01014TestData.ReadWrite,
                PageCachePolicy.WriteBack,
                pageZero.Context));
        if (!mapped.Succeeded)
            return false;
        pageZero.Entries[1024] =
            M01014TestData.EncodeTablePointer(
                X64PageTableLevel.Pde,
                pageZero.Pages[3].PhysicalStart,
                pageZero.Context);
        PageZeroMappingPolicyResult pageZeroPresent;
        fixed (X64PageTableImagePage* pages = pageZero.Pages)
        fixed (ulong* entries = pageZero.Entries)
        {
            pageZeroPresent =
                PageZeroMappingPolicy.AuditTableImage(
                    pageZero.Root,
                    pageZero.Context,
                    pages,
                    (ulong)pageZero.Pages.Length,
                    (ulong)pageZero.Pages.Length,
                    entries,
                    (ulong)pageZero.Entries.Length,
                    (ulong)pageZero.Entries.Length);
        }

        return transitionFailure.Status
                == M01LivePagingStatus.TransitionEvidenceRejected
            && transcriptFailure.Status
                == M01LivePagingStatus.Cr3TranscriptRejected
            && protectionFailure.Status
                == M01LivePagingStatus.ProtectionProbeRejected
            && eventFailure.Status
                == M01LivePagingStatus.ProtectionProbeRejected
            && pageZeroPresent.Status
                == PageZeroMappingPolicyStatus.PageZeroPresent
            && !pageZeroPresent
                .ProvesPageZeroAbsentInSuppliedImage;
    }

    private static bool TestDuplicateAndDeterministicRepeat()
    {
        using CanonicalFixture fixture = new();
        M01LivePagingIntegrationResult first =
            fixture.ExecuteCurrent();
        if (!first.Succeeded)
            return false;

        byte[] code = fixture.CopyCodePage();
        byte[] probe = fixture.CopyProbePage();
        ulong[] entries = (ulong[])fixture.Entries.Clone();
        X64PageTableImagePage[] pages =
            (X64PageTableImagePage[])fixture.PageImages.Clone();
        M01LivePagingIntegrationResult duplicate =
            fixture.ExecuteCurrent();
        if (duplicate.Status != M01LivePagingStatus.DuplicatePhase)
            return false;

        fixture.ClearStaticState();
        M01LivePagingIntegrationResult second =
            fixture.ExecuteCurrent();
        if (!second.Succeeded
            || !fixture.CodePageEquals(code)
            || !fixture.ProbePageEquals(probe)
            || !fixture.Entries.AsSpan().SequenceEqual(entries)
            || !fixture.PageImages.AsSpan().SequenceEqual(pages))
        {
            return false;
        }

        return first.Evidence.MappingPlan.OutputRangeCount
                == second.Evidence.MappingPlan.OutputRangeCount
            && first.Evidence.TableBuild.TablePageCount
                == second.Evidence.TableBuild.TablePageCount
            && first.Evidence.TableBuild.MappedPageCount
                == second.Evidence.TableBuild.MappedPageCount
            && first.Evidence.TableBuild.ProbeLeafEntryIndex
                == second.Evidence.TableBuild.ProbeLeafEntryIndex
            && first.Evidence.PreflightReadback.EntryAddress
                == second.Evidence.PreflightReadback.EntryAddress
            && first.Evidence.PreflightReadback.BytesProcessed
                == second.Evidence.PreflightReadback.BytesProcessed
            && first.Evidence.TransitionReadback.EntryAddress
                == second.Evidence.TransitionReadback.EntryAddress
            && first.Evidence.TransitionReadback.BytesWritten
                == second.Evidence.TransitionReadback.BytesWritten
            && first.Evidence.Cr3Audit.FinalCanaryValue
                == second.Evidence.Cr3Audit.FinalCanaryValue
            && first.StaticState.RawFirmwareCr3
                == second.StaticState.RawFirmwareCr3
            && first.StaticState.TablePageCount
                == second.StaticState.TablePageCount;
    }

    private static bool TestSourceFirewalls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string productionPath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Boot",
            "M01LivePagingIntegration.cs");
        string testPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "M01LivePagingIntegrationTests.cs");
        string production = File.ReadAllText(productionPath);
        string tests = File.ReadAllText(testPath);
        string codeOnly = RemoveComments(production);

        return production.Contains(
                "public static M01LivePagingIntegrationResult Execute(",
                StringComparison.Ordinal)
            && production.Contains(
                "public static M01LivePagingPreparationResult Prepare(",
                StringComparison.Ordinal)
            && production.Contains(
                "public static M01LivePagingIntegrationResult "
                    + "ExecuteModelOnly(",
                StringComparison.Ordinal)
            && production.Contains(
                "public const bool LegacyExecuteIsModelOnly = true;",
                StringComparison.Ordinal)
            && production.Contains(
                "public const bool "
                    + "LegacyResultsCanPrepareCompositeTransition "
                    + "= false;",
                StringComparison.Ordinal)
            && production.Contains(
                "OwnedBootStackSwitchPlanResult stackSwitchPlan",
                StringComparison.Ordinal)
            && production.Contains(
                "public const bool DereferencesPageZero = false;",
                StringComparison.Ordinal)
            && production.Contains(
                "public const bool IsGuestEvidence = false;",
                StringComparison.Ordinal)
            && production.Contains(
                "PageZeroMappingPolicy.AuditTableImage(",
                StringComparison.Ordinal)
            && CountOccurrences(
                codeOnly,
                "PageZeroMappingPolicy.AuditTableImage(") == 2
            && !codeOnly.Contains(
                "NullPageFaultProbePlan",
                StringComparison.Ordinal)
            && !codeOnly.Contains(
                "EfiEntry",
                StringComparison.Ordinal)
            && !codeOnly.Contains(
                "AllocatePages(",
                StringComparison.Ordinal)
            && !codeOnly.Contains(
                "FreePages(",
                StringComparison.Ordinal)
            && !codeOnly.Contains(
                "stackSwitchPlan.Plan.Invoke",
                StringComparison.Ordinal)
            && !Regex.IsMatch(
                codeOnly,
                @"\*\s*\(\s*(?:byte|sbyte|short|ushort|int|uint|long|ulong|nint|nuint)\s*\*\s*\)\s*0(?:UL|U|L)?\b",
                RegexOptions.CultureInvariant)
            && !Regex.IsMatch(
                codeOnly,
                @"Unsafe\.(?:Read|Write)\s*<[^>]+>\s*\(\s*(?:null|0)",
                RegexOptions.CultureInvariant)
            && VerifyExistingBranchesAreReadOnly(codeOnly)
            && CountOccurrences(
                tests,
                "TEST_START \" + IssueTestName") == 1
            && CountOccurrences(
                tests,
                "CLROS_" + "M01_019_READY") == 1
            && CountOccurrences(
                tests,
                "TEST_PASS \" + IssueTestName") == 1
            && CountOccurrences(
                tests,
                "TEST_FAIL \" + IssueTestName") == 1
            && CountOccurrences(
                tests,
                "TEST_START \" + PreparationIssueTestName") == 1
            && CountOccurrences(
                tests,
                "CLROS_" + "M01_029_READY") == 1
            && CountOccurrences(
                tests,
                "TEST_PASS \" + PreparationIssueTestName") == 1
            && CountOccurrences(
                tests,
                "TEST_FAIL \" + PreparationIssueTestName") == 1;
    }

    private static bool VerifyExistingBranchesAreReadOnly(
        string production)
    {
        const string BranchStart =
            "if (byteMode == M01LivePagingByteMode.VerifyExisting)";
        string[] expectedMaterializationAssignments =
        [
            "evidence.PreflightMaterializationValue =",
            "evidence.TransitionMaterializationValue =",
            "evidence.InvlpgMaterializationValue =",
            "evidence.ProbeMaterializationValue =",
            "evidence.PreflightMaterializationValue =",
            "evidence.TransitionMaterializationValue =",
            "evidence.InvlpgMaterializationValue =",
            "evidence.ProbeMaterializationValue ="
        ];
        int searchOffset = 0;
        for (int branchIndex = 0;
            branchIndex < expectedMaterializationAssignments.Length;
            branchIndex++)
        {
            int condition = production.IndexOf(
                BranchStart,
                searchOffset,
                StringComparison.Ordinal);
            if (condition < 0)
            {
                return false;
            }

            int openingBrace = production.IndexOf(
                '{',
                condition + BranchStart.Length);
            if (openingBrace < 0
                || !TryFindMatchingBrace(
                    production,
                    openingBrace,
                    out int closingBrace))
            {
                return false;
            }

            string branch = production.Substring(
                openingBrace,
                closingBrace - openingBrace + 1);
            if (!branch.Contains(
                    expectedMaterializationAssignments[branchIndex],
                    StringComparison.Ordinal)
                || branch.Contains(
                    ".Emit(",
                    StringComparison.Ordinal)
                || branch.Contains(
                    ".TryMaterialize(",
                    StringComparison.Ordinal)
                || branch.Contains(
                    "ZeroBytes(",
                    StringComparison.Ordinal))
            {
                return false;
            }

            searchOffset = closingBrace + 1;
        }

        return production.IndexOf(
                BranchStart,
                searchOffset,
                StringComparison.Ordinal) < 0;
    }

    private static bool TryFindMatchingBrace(
        string source,
        int openingBrace,
        out int closingBrace)
    {
        closingBrace = -1;
        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    closingBrace = index;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string repositoryRoot =
            HostToolProcess.FindRepositoryRoot();
        string[] sources =
            CreateNoStdLibSourceClosure(repositoryRoot);
        if (!IsExactNoStdLibSourceClosure(
                repositoryRoot,
                sources))
        {
            return false;
        }

        string workspace =
            CreateNoStdLibWorkspace(repositoryRoot, sources);
        try
        {
            NoStdLibBuildResult result =
                RunNoStdLibBuild(workspace);
            string outputAssembly = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.LivePagingCompileClosure.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || ContainsInfrastructureFailure(result.Output)
                || !HasNoExternalMetadata(outputAssembly)
                || !HasExactNoStdLibIntegrationSurface(
                    outputAssembly))
            {
                Console.WriteLine(
                    "M01_019_NOSTDLIB_COMPILE_DIAGNOSTIC "
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

    private static string[] CreateNoStdLibSourceClosure(
        string repositoryRoot)
    {
        string[] relativeSources =
        [
            "src/OS.Kernel/Boot/"
                + "TypedPhysicalVirtualDeviceAddressPODWrappers.cs",
            "src/OS.Kernel/Memory/BootReservedRange.cs",
            "src/OS.Kernel/Memory/"
                + "BootReservationTableValidator.cs",
            "src/OS.Kernel/Boot/EfiLoadedImageRange.cs",
            "src/OS.Kernel/Boot/"
                + "EfiLoadedImageRangeCapturePlan.cs",
            "src/OS.Kernel/Boot/BootHandoffRecord.cs",
            "src/OS.Kernel/Boot/OwnedBootStackLayout.cs",
            "src/OS.Kernel/Boot/UefiMemoryType.cs",
            "src/OS.Kernel/Boot/"
                + "BootMemoryRegionMaterializer.cs",
            "src/OS.Kernel/Boot/OwnedBootStackSwitchPlan.cs",
            "src/OS.Kernel/Boot/"
                + "M01LoadedImageStackIntegration.cs",
            "src/OS.Kernel/Memory/BootReservationSubtractor.cs",
            "src/OS.Kernel/Memory/"
                + "BootPageAllocatorCoreContracts.cs",
            "src/OS.Kernel/Memory/"
                + "BootPageAllocatorStateMaterializer.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorMutation.cs",
            "src/OS.Kernel/Boot/M01LiveAllocatorIntegration.cs",
            "src/OS.Kernel/Memory/PageProtection.cs",
            "src/OS.Kernel/Memory/PageCachePolicy.cs",
            "src/OS.Kernel/Memory/X64PageTableEntry.cs",
            "src/OS.Kernel/Memory/X64TlbInvalidationPlan.cs",
            "src/OS.Kernel/Memory/X64PageMap.cs",
            "src/OS.Kernel/Memory/X64InvlpgStubPlan.cs",
            "src/OS.Kernel/Memory/"
                + "X64ProtectionProbeStubPlan.cs",
            "src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs",
            "src/OS.Kernel/Memory/PageZeroMappingPolicy.cs",
            "src/OS.Kernel/Memory/X64Cr3LoadStubPlan.cs",
            "src/OS.Kernel/Memory/"
                + "X64PagingModePreflightStubPlan.cs",
            "src/OS.Kernel/Memory/"
                + "X64ReversibleCr3TransitionStubPlan.cs",
            "src/OS.Kernel/Boot/M01LivePagingIntegration.cs"
        ];

        string[] absoluteSources =
            new string[relativeSources.Length];
        for (int index = 0;
            index < relativeSources.Length;
            index++)
        {
            absoluteSources[index] = Path.GetFullPath(
                Path.Combine(
                    repositoryRoot,
                    relativeSources[index].Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        return absoluteSources;
    }

    private static bool IsExactNoStdLibSourceClosure(
        string repositoryRoot,
        string[] sources)
    {
        if (sources.Length != 29
            || sources.Distinct(
                StringComparer.OrdinalIgnoreCase).Count()
                != sources.Length)
        {
            return false;
        }

        string productionRoot = Path.GetFullPath(
            Path.Combine(repositoryRoot, "src"))
            + Path.DirectorySeparatorChar;
        bool hasIntegration = false;
        bool hasUefiMemoryType = false;
        for (int index = 0; index < sources.Length; index++)
        {
            string source = Path.GetFullPath(sources[index]);
            string fileName = Path.GetFileName(source);
            if (!source.StartsWith(
                    productionRoot,
                    StringComparison.OrdinalIgnoreCase)
                || !File.Exists(source)
                || string.Equals(
                    fileName,
                    "BootSequence.cs",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    fileName,
                    "EfiEntry.cs",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    fileName,
                    "Probes.cs",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            hasIntegration |= string.Equals(
                fileName,
                "M01LivePagingIntegration.cs",
                StringComparison.Ordinal);
            hasUefiMemoryType |= string.Equals(
                fileName,
                "UefiMemoryType.cs",
                StringComparison.Ordinal);
        }

        return hasIntegration && hasUefiMemoryType;
    }

    private static string CreateNoStdLibWorkspace(
        string repositoryRoot,
        string[] sources)
    {
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-019-" + Guid.NewGuid().ToString("N"));
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
                    public struct SByte { }
                    public struct Int16 { }
                    public struct UInt16 { }
                    public struct Int32 { }
                    public struct UInt32 { }
                    public struct Int64 { }
                    public struct UInt64 { }
                    public struct IntPtr { }
                    public struct UIntPtr { }
                    public enum AttributeTargets { }
                    public sealed class AttributeUsageAttribute : Attribute
                    {
                        public AttributeUsageAttribute(
                            AttributeTargets validOn) { }
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

                internal static unsafe class M01LivePagingProbe
                {
                    public static M01LivePagingCleanupStatus Exercise()
                    {
                        return M01LivePagingIntegration.PlanCleanup(
                            null,
                            0UL,
                            0UL,
                            0UL,
                            firmwareServicesAvailable: true,
                            ownedRootWasEntered: false,
                            ownedRootRestored: false).Status;
                    }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            StringBuilder compileItems = new StringBuilder();
            for (int index = 0; index < sources.Length; index++)
            {
                compileItems.Append(
                    "    <Compile Include=\"");
                compileItems.Append(EscapeXml(sources[index]));
                compileItems.Append("\" Link=\"Closure\\");
                compileItems.Append(index.ToString("D2"));
                compileItems.Append('-');
                compileItems.Append(EscapeXml(
                    Path.GetFileName(sources[index])));
                compileItems.AppendLine("\" />");
            }

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.LivePagingCompileClosure</AssemblyName>
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
                Path.Combine(workspace, "M01.019.NoStdLib.csproj"),
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
        start.ArgumentList.Add("M01.019.NoStdLib.csproj");
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
                    nameof(M01LivePagingIntegration),
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

        int cleanupCount = 0;
        int prepareCount = 0;
        int executeCount = 0;
        int executeModelOnlyCount = 0;
        foreach (MethodDefinitionHandle handle in metadata
            .GetTypeDefinition(integrationType).GetMethods())
        {
            string name = metadata.GetString(
                metadata.GetMethodDefinition(handle).Name);
            if (string.Equals(
                    name,
                    nameof(M01LivePagingIntegration.PlanCleanup),
                    StringComparison.Ordinal))
            {
                cleanupCount++;
            }
            else if (string.Equals(
                    name,
                    nameof(M01LivePagingIntegration.Prepare),
                    StringComparison.Ordinal))
            {
                prepareCount++;
            }
            else if (string.Equals(
                    name,
                    nameof(M01LivePagingIntegration.Execute),
                    StringComparison.Ordinal))
            {
                executeCount++;
            }
            else if (string.Equals(
                    name,
                    nameof(
                        M01LivePagingIntegration
                            .ExecuteModelOnly),
                    StringComparison.Ordinal))
            {
                executeModelOnlyCount++;
            }
        }

        bool hasLegacyModelOnlyField = false;
        bool hasLegacyCannotPrepareField = false;
        foreach (FieldDefinitionHandle handle in metadata
            .GetTypeDefinition(integrationType).GetFields())
        {
            string name = metadata.GetString(
                metadata.GetFieldDefinition(handle).Name);
            hasLegacyModelOnlyField |= string.Equals(
                name,
                nameof(
                    M01LivePagingIntegration
                        .LegacyExecuteIsModelOnly),
                StringComparison.Ordinal);
            hasLegacyCannotPrepareField |= string.Equals(
                name,
                nameof(
                    M01LivePagingIntegration
                        .LegacyResultsCanPrepareCompositeTransition),
                StringComparison.Ordinal);
        }

        return cleanupCount == 1
            && prepareCount == 1
            && executeCount == 1
            && executeModelOnlyCount == 1
            && hasLegacyModelOnlyField
            && hasLegacyCannotPrepareField;
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
                @"\Aclros-m01-019-[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-019 workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        if ((File.GetAttributes(fullWorkspace)
                & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Refusing to delete a reparse-point M01-019 workspace.");
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

    private static bool EnumContractMatches<TEnum>(
        (string Name, byte Value)[] expected)
        where TEnum : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(TEnum)) != typeof(byte))
            return false;

        string[] names = Enum.GetNames<TEnum>();
        TEnum[] values = Enum.GetValues<TEnum>();
        if (names.Length != expected.Length
            || values.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    names[index],
                    expected[index].Name,
                    StringComparison.Ordinal)
                || Convert.ToByte(values[index]) != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static long Offset<T>(string field)
        where T : struct
    {
        return Marshal.OffsetOf<T>(field).ToInt64();
    }

    private static BootReservedRange CreateRange(
        BootReservationKind kind,
        ulong start,
        ulong pages)
    {
        BootReservedRangeStatus status =
            BootReservedRange.TryCreate(
                kind,
                Physical(start),
                pages,
                out BootReservedRange result);
        if (status != BootReservedRangeStatus.Success)
        {
            throw new InvalidOperationException(
                "reservation fixture failed: " + status);
        }
        return result;
    }

    private static BootMemoryRegionRecord Record(
        UefiMemoryType type,
        ulong start,
        ulong pages)
    {
        ulong bytes = checked(pages * BootReservedRange.PageSize);
        return new BootMemoryRegionRecord(
            (uint)type,
            start,
            start,
            pages,
            0,
            bytes,
            checked(start + bytes));
    }

    private static BootMemoryRegionRecord WithRecord(
        BootMemoryRegionRecord source,
        uint? uefiType = null,
        ulong? physicalStart = null,
        ulong? virtualStart = null,
        ulong? pageCount = null,
        ulong? attribute = null,
        ulong? byteLength = null,
        ulong? physicalEndExclusive = null)
    {
        return new BootMemoryRegionRecord(
            uefiType ?? source.UefiType,
            physicalStart ?? source.PhysicalStart,
            virtualStart ?? source.VirtualStart,
            pageCount ?? source.PageCount,
            attribute ?? source.Attribute,
            byteLength ?? source.ByteLength,
            physicalEndExclusive ?? source.PhysicalEndExclusive);
    }

    private static int FindRequirementIndex(
        X64BootIdentityMappingRequirement[] requirements,
        X64BootIdentityMappingPurpose purpose,
        int occurrence = 0)
    {
        for (int index = 0; index < requirements.Length; index++)
        {
            if (requirements[index].Purpose != purpose)
            {
                continue;
            }

            if (occurrence == 0)
            {
                return index;
            }
            occurrence--;
        }

        return -1;
    }

    private static X64BootIdentityMappingRequirement[]
        RemoveRequirementAt(
            X64BootIdentityMappingRequirement[] source,
            int removedIndex)
    {
        X64BootIdentityMappingRequirement[] result =
            new X64BootIdentityMappingRequirement[source.Length - 1];
        int destination = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if (index != removedIndex)
            {
                result[destination++] = source[index];
            }
        }

        return result;
    }

    private static X64BootIdentityMappingRequirement
        RequirementForReservation(
            X64BootIdentityMappingPurpose purpose,
            BootReservedRange reservation,
            PageProtection protection,
            PageCachePolicy cache = PageCachePolicy.WriteBack)
    {
        return Requirement(
            purpose,
            Raw(reservation.PhysicalStart),
            Raw(reservation.PhysicalEndExclusive),
            protection,
            cache);
    }

    private static bool CanonicalIdentityRequirementsMatch(
        CanonicalFixture fixture)
    {
        X64BootIdentityMappingRequirement[] requirements =
            fixture.MappingRequirements;
        if (requirements.Length != 13)
        {
            return false;
        }

        BootReservedRange loaded = fixture.FindReservation(
            BootReservationKind.LoadedImage);
        BootReservedRange acpi = fixture.FindReservation(
            BootReservationKind.AcpiScratch);
        BootReservedRange framebuffer = fixture.FindReservation(
            BootReservationKind.FramebufferMetadata);
        BootReservedRange allocator = fixture.FindReservation(
            BootReservationKind.AllocatorArena);
        ulong loadedStart = Raw(loaded.PhysicalStart);
        ulong page = BootReservedRange.PageSize;
        int image0 = FindRequirementIndex(
            requirements,
            X64BootIdentityMappingPurpose.ExecutingImage,
            occurrence: 0);
        int image1 = FindRequirementIndex(
            requirements,
            X64BootIdentityMappingPurpose.ExecutingImage,
            occurrence: 1);
        int image2 = FindRequirementIndex(
            requirements,
            X64BootIdentityMappingPurpose.ExecutingImage,
            occurrence: 2);
        int handoff = FindRequirementIndex(
            requirements,
            X64BootIdentityMappingPurpose.BootHandoff);
        int acpiIndex = FindRequirementIndex(
            requirements,
            X64BootIdentityMappingPurpose.AcpiScratch);
        int framebufferIndex = FindRequirementIndex(
            requirements,
            X64BootIdentityMappingPurpose.FramebufferMetadata);
        int serial = FindRequirementIndex(
            requirements,
            X64BootIdentityMappingPurpose.SerialStub);
        if (image0 < 0
            || image1 < 0
            || image2 < 0
            || FindRequirementIndex(
                requirements,
                X64BootIdentityMappingPurpose.ExecutingImage,
                occurrence: 3) >= 0
            || handoff < 0
            || acpiIndex < 0
            || framebufferIndex < 0
            || serial < 0)
        {
            return false;
        }

        ulong handoffStorageStart = Raw(
            fixture.StackSwitchPlan.Plan.Handoff.StorageStart);
        ulong handoffStorageEnd = Raw(
            fixture.StackSwitchPlan.Plan.Handoff.StorageEndExclusive);
        ulong handoffPageStart =
            handoffStorageStart & ~(page - 1);
        ulong handoffPageEnd =
            checked((handoffStorageEnd + page - 1)
                & ~(page - 1));
        if (!RequirementMatches(
                requirements[image0],
                loadedStart,
                loadedStart + page,
                PageProtection.Read,
                PageCachePolicy.WriteBack)
            || !RequirementMatches(
                requirements[image1],
                loadedStart + page,
                loadedStart + 2 * page,
                M01014TestData.ReadExecute,
                PageCachePolicy.WriteBack)
            || !RequirementMatches(
                requirements[image2],
                loadedStart + 2 * page,
                Raw(loaded.PhysicalEndExclusive),
                PageProtection.Read,
                PageCachePolicy.WriteBack)
            || !RequirementMatches(
                requirements[handoff],
                handoffPageStart,
                handoffPageEnd,
                PageProtection.Read,
                PageCachePolicy.WriteBack)
            || !RequirementMatchesReservation(
                requirements[acpiIndex],
                acpi,
                PageProtection.Read)
            || !RequirementMatchesReservation(
                requirements[framebufferIndex],
                framebuffer,
                PageProtection.Read))
        {
            return false;
        }

        // Compatibility-only: this binds the legacy stage-one switch stub.
        // It is not evidence of M02-owned serial diagnostics.
        if (!RequirementMatchesReservation(
                requirements[serial],
                fixture.StackSwitchPlan.Plan.StubReservation,
                M01014TestData.ReadExecute)
            || M01LivePagingIntegration.ProvesOwnedSerialDiagnostics)
        {
            return false;
        }

        int additionalCount = 0;
        bool allocatorMapped = false;
        for (int index = 0; index < requirements.Length; index++)
        {
            if (requirements[index].Purpose
                != X64BootIdentityMappingPurpose.AdditionalRequired)
            {
                continue;
            }

            additionalCount++;
            allocatorMapped |= RequirementMatchesReservation(
                requirements[index],
                allocator,
                M01014TestData.ReadWrite);
        }

        return additionalCount == 4 && allocatorMapped;
    }

    private static bool RequirementMatchesReservation(
        X64BootIdentityMappingRequirement requirement,
        BootReservedRange reservation,
        PageProtection protection)
    {
        return RequirementMatches(
            requirement,
            Raw(reservation.PhysicalStart),
            Raw(reservation.PhysicalEndExclusive),
            protection,
            PageCachePolicy.WriteBack);
    }

    private static bool RequirementMatches(
        X64BootIdentityMappingRequirement requirement,
        ulong start,
        ulong end,
        PageProtection protection,
        PageCachePolicy cache)
    {
        return requirement.IsInitialized
            && Raw(requirement.PhysicalStart) == start
            && Raw(requirement.PhysicalEndExclusive) == end
            && requirement.PageCount
                == (end - start) / BootReservedRange.PageSize
            && requirement.ByteLength == end - start
            && requirement.Protection == protection
            && requirement.CachePolicy == cache;
    }

    private static bool AllocationPagesHaveExactProtection(
        BootMemoryRegionRecord allocation,
        PageProtection expectedProtection,
        PageCachePolicy expectedCachePolicy,
        PhysicalAddress root,
        X64PageTableImagePage[] pageImages,
        ulong pageImageCount,
        ulong[] entries,
        ulong entryCount,
        X64PageTableEncodingPreconditions preconditions)
    {
        if (allocation.PageCount == 0
            || allocation.PageCount > 64)
        {
            return false;
        }

        for (ulong page = 0; page < allocation.PageCount; page++)
        {
            ulong address = checked(
                allocation.VirtualStart
                + page * BootReservedRange.PageSize);
            if (!TryReadFourLevelLeaf(
                    address,
                    root,
                    pageImages,
                    pageImageCount,
                    entries,
                    entryCount,
                    preconditions,
                    out X64PageTableEntryResult leaf)
                || leaf.Kind != X64PageTableEntryKind.Leaf
                || leaf.Level != X64PageTableLevel.Pte
                || leaf.PageSizeBytes != BootReservedRange.PageSize
                || leaf.Protection != expectedProtection
                || leaf.CachePolicy != expectedCachePolicy
                || Raw(leaf.PhysicalAddress) != address)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadFourLevelLeaf(
        ulong virtualAddress,
        PhysicalAddress root,
        X64PageTableImagePage[] pageImages,
        ulong pageImageCount,
        ulong[] entries,
        ulong entryCount,
        X64PageTableEncodingPreconditions preconditions,
        out X64PageTableEntryResult leaf)
    {
        leaf = default;
        PhysicalAddress table = root;
        X64PageTableLevel level = X64PageTableLevel.Pml4e;
        for (int depth = 0; depth < 4; depth++)
        {
            if (!TryFindPageImage(
                    pageImages,
                    pageImageCount,
                    table,
                    out X64PageTableImagePage image))
            {
                return false;
            }

            int shift = level == X64PageTableLevel.Pml4e
                ? 39
                : level == X64PageTableLevel.Pdpte
                    ? 30
                    : level == X64PageTableLevel.Pde
                        ? 21
                        : 12;
            ulong tableIndex = (virtualAddress >> shift) & 0x1ff;
            ulong entryIndex = image.EntryStartIndex + tableIndex;
            if (entryIndex >= entryCount
                || entryIndex >= (ulong)entries.Length)
            {
                return false;
            }

            X64PageTableEntryResult decoded =
                X64PageTableEntry.TryDecode(
                    level,
                    entries[checked((int)entryIndex)],
                    preconditions);
            if (!decoded.Succeeded)
            {
                return false;
            }

            if (level == X64PageTableLevel.Pte)
            {
                leaf = decoded;
                return true;
            }

            if (decoded.Kind
                != X64PageTableEntryKind.TablePointer)
            {
                return false;
            }

            table = decoded.PhysicalAddress;
            level = level == X64PageTableLevel.Pml4e
                ? X64PageTableLevel.Pdpte
                : level == X64PageTableLevel.Pdpte
                    ? X64PageTableLevel.Pde
                    : X64PageTableLevel.Pte;
        }

        return false;
    }

    private static bool TryFindPageImage(
        X64PageTableImagePage[] pageImages,
        ulong pageImageCount,
        PhysicalAddress physicalStart,
        out X64PageTableImagePage found)
    {
        found = default;
        if (pageImageCount > (ulong)pageImages.Length)
        {
            return false;
        }

        for (ulong index = 0; index < pageImageCount; index++)
        {
            X64PageTableImagePage candidate =
                pageImages[checked((int)index)];
            if (candidate.PhysicalStart.Equals(physicalStart))
            {
                found = candidate;
                return true;
            }
        }

        return false;
    }

    private static X64BootIdentityMappingRequirement Requirement(
        X64BootIdentityMappingPurpose purpose,
        ulong start,
        ulong end,
        PageProtection protection,
        PageCachePolicy? cache = null)
    {
        ulong bytes = checked(end - start);
        return new X64BootIdentityMappingRequirement(
            purpose,
            Physical(start),
            Physical(end),
            bytes / BootReservedRange.PageSize,
            bytes,
            protection,
            cache
                ?? (protection == PageProtection.None
                    ? PageCachePolicy.Uninitialized
                    : PageCachePolicy.WriteBack));
    }

    private static PageProtection Protection(
        PageProtection left,
        PageProtection right)
    {
        return (PageProtection)((byte)left | (byte)right);
    }

    private static PhysicalAddress Physical(ulong raw)
    {
        AddressValueStatus status =
            PhysicalAddress.TryCreateFromHardwareRegisterBoundary(
                raw,
                out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "physical fixture invalid: " + status);
        }
        return address;
    }

    private static VirtualAddress Virtual(ulong raw)
    {
        AddressValueStatus status =
            VirtualAddress.TryCreateFromHardwareRegisterBoundary(
                raw,
                out VirtualAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "virtual fixture invalid: " + status);
        }
        return address;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(
                out ulong value)
                == AddressValueStatus.Success
            ? value
            : 0;
    }

    private static ulong Raw(VirtualAddress address)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(
                out ulong value)
                == AddressValueStatus.Success
            ? value
            : 0;
    }

    private static byte[] CopyBytes(byte* source, ulong length)
    {
        byte[] copy = new byte[checked((int)length)];
        for (int index = 0; index < copy.Length; index++)
            copy[index] = source[index];
        return copy;
    }

    private static void Clear(byte* destination, ulong length)
    {
        for (ulong index = 0; index < length; index++)
            destination[index] = 0;
    }

    private static int CountOccurrences(
        string value,
        string search)
    {
        int count = 0;
        int offset = 0;
        while (offset <= value.Length - search.Length)
        {
            int index = value.IndexOf(
                search,
                offset,
                StringComparison.Ordinal);
            if (index < 0)
                break;
            count++;
            offset = index + search.Length;
        }
        return count;
    }

    private static string RemoveComments(string source)
    {
        string withoutBlocks = Regex.Replace(
            source,
            @"/\*.*?\*/",
            string.Empty,
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return Regex.Replace(
            withoutBlocks,
            @"//.*?$",
            string.Empty,
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
    }

    private static bool PreparationSurfaceContainsSyntheticInput(
        ParameterInfo[] parameters)
    {
        Type[] forbidden =
        [
            typeof(X64ReversibleCr3TransitionRuntimeState),
            typeof(X64Cr3ProbeEvent),
            typeof(X64ProtectionProbeObservation),
            typeof(X64ProtectionProbeEvent),
            typeof(X64Cr3ProbePolicy)
        ];
        foreach (ParameterInfo parameter in parameters)
        {
            Type type = parameter.ParameterType;
            if (type.IsPointer || type.IsByRef)
                type = type.GetElementType()!;
            if (forbidden.Contains(type))
                return true;
        }
        return false;
    }

    private static bool PreparationEvidenceContainsSyntheticTranscript()
    {
        Type[] forbidden =
        [
            typeof(X64ReversibleCr3TransitionRuntimeState),
            typeof(X64Cr3ProbeEvent),
            typeof(X64ProtectionProbeObservation),
            typeof(X64ProtectionProbeEvent),
            typeof(X64Cr3ProbePolicy),
            typeof(X64ReversibleCr3TransitionEvidenceResult),
            typeof(X64Cr3ProbeAuditResult),
            typeof(X64ProtectionProbePlanResult)
        ];
        Type[] preparationTypes =
        [
            typeof(M01LivePagingProtectionPreparation),
            typeof(M01LivePagingPreparationEvidence),
            typeof(M01LivePagingPreparationState),
            typeof(M01LivePagingPreparationResult)
        ];
        foreach (Type preparationType in preparationTypes)
        {
            foreach (FieldInfo field in preparationType.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic))
            {
                Type fieldType = field.FieldType;
                if (fieldType.IsPointer || fieldType.IsByRef)
                    fieldType = fieldType.GetElementType()!;
                if (forbidden.Contains(fieldType))
                    return true;
            }
        }
        return false;
    }

    private static bool HasPreparationLegacyConversion()
    {
        Type preparation =
            typeof(M01LivePagingPreparationResult);
        Type legacy = typeof(M01LivePagingIntegrationResult);
        foreach (Type owner in new[] { preparation, legacy })
        {
            foreach (MethodInfo method in owner.GetMethods(
                BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.NonPublic))
            {
                if (!method.IsSpecialName
                    || method.Name != "op_Implicit"
                        && method.Name != "op_Explicit")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1
                    && (parameters[0].ParameterType == preparation
                            && method.ReturnType == legacy
                        || parameters[0].ParameterType == legacy
                            && method.ReturnType == preparation))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool ExpectPreparationFailure(
        CanonicalFixture fixture,
        M01LivePagingPreparationResult result,
        M01LivePagingStatus expectedStatus,
        [CallerLineNumber] int callerLine = 0)
    {
        bool passed =
            result.Status == expectedStatus
            && !result.Succeeded
            && !result.PreparedForCompositeTransition
            && result.ExecutionMode
                == M01LivePagingExecutionMode.PreparationOnly
            && result.State.Size == 0
            && !result.State.IsPrepared
            && fixture.PreparationStateIsZero();
        if (!passed)
        {
            Console.WriteLine(
                "M01_029_FAILURE_CONTRACT_MISMATCH"
                + " line=" + callerLine
                + " expected=" + expectedStatus
                + " actual=" + result.Status
                + " succeeded=" + result.Succeeded
                + " prepared="
                    + result.PreparedForCompositeTransition
                + " mode=" + result.ExecutionMode
                + " result_state_size=" + result.State.Size
                + " result_state_prepared="
                    + result.State.IsPrepared
                + " storage_zero="
                    + fixture.PreparationStateIsZero());
        }
        fixture.ClearPreparationState();
        return passed;
    }

    private static void FillUnmanagedArray<T>(
        T[] values,
        byte value)
        where T : unmanaged
    {
        fixed (T* pointer = values)
        {
            ulong length =
                (ulong)values.Length * (ulong)sizeof(T);
            byte* bytes = (byte*)pointer;
            for (ulong index = 0; index < length; index++)
                bytes[index] = value;
        }
    }

    private static byte[] CopyUnmanagedArray<T>(T[] values)
        where T : unmanaged
    {
        fixed (T* pointer = values)
        {
            return CopyBytes(
                (byte*)pointer,
                (ulong)values.Length * (ulong)sizeof(T));
        }
    }

    private static bool BytesEqual(
        byte[] expected,
        byte* actual)
    {
        for (int index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index])
                return false;
        }
        return true;
    }

    private static bool BytesEqual<T>(
        byte[] expected,
        T[] actual)
        where T : unmanaged
    {
        if (expected.Length
            != checked(actual.Length * sizeof(T)))
        {
            return false;
        }
        fixed (T* pointer = actual)
            return BytesEqual(expected, (byte*)pointer);
    }

    private sealed unsafe class CanonicalFixture : IDisposable
    {
        internal const ulong ValidCr0 =
            X64PagingModePreflightStubPlan.Cr0PagingBit
            | X64PagingModePreflightStubPlan.Cr0WriteProtectBit;
        internal const ulong ValidCr4 =
            X64PagingModePreflightStubPlan.Cr4PaeBit
            | X64ReversibleCr3TransitionStubPlan
                .Cr4PageGlobalEnableBit;
        internal const ulong ValidEfer =
            X64PagingModePreflightStubPlan.EferLongModeEnableBit
            | X64PagingModePreflightStubPlan.EferLongModeActiveBit
            | X64PagingModePreflightStubPlan.EferNoExecuteEnableBit;
        internal const ulong ValidRflags = 0x202;
        internal const ulong InitialCanary =
            0x1122_3344_5566_7788UL;
        internal const ulong UpdatedCanary =
            0x8877_6655_4433_2211UL;
        internal const ulong ProtectionCanary =
            0x434c_524f_535f_4f4bUL;
        internal const ulong StageThreeMapGeneration = 10;

        private const ulong BackingPages = 128;
        private const ulong BackingBytes =
            BackingPages * BootReservedRange.PageSize;
        private const ulong LoadedImagePages = 3;
        private const ulong LoadedImageBytes =
            LoadedImagePages * BootReservedRange.PageSize;
        private const ulong StageOnePreviousMapKey = 0xf000;
        private const ulong StageOnePreviousMapGeneration = 7;
        private const ulong StageOneMapKey = 0x0010;
        private const ulong StageOneMapGeneration = 8;
        private const ulong StageOneCodeAllocationOrdinal = 1;
        private const ulong StageOneDataAllocationOrdinal = 2;
        private const ulong StageOneMapCaptureOrdinal = 3;
        private const ulong StageOneLedgerBuildOrdinal = 4;
        private const ulong ArenaAllocationOrdinal = 5;
        private const ulong StageTwoMapKey = 0x0020;
        private const ulong StageTwoMapGeneration = 9;
        private const ulong StageTwoMapCaptureOrdinal = 6;
        private const ulong StageTwoLedgerBuildOrdinal = 7;
        private const ulong Cr3CodeAllocationOrdinal = 8;
        private const ulong ProbeAllocationOrdinal = 9;
        private const ulong TableStateAllocationOrdinal = 10;
        private const ulong StageThreeMapCaptureOrdinal = 11;
        private const ulong StageThreeLedgerBuildOrdinal = 12;
        private const ulong GuardPages = 1;
        private const ulong StackPages = 3;
        private const ulong StackArenaPages = GuardPages + StackPages;
        private const ulong StageOneLedgerCapacity = 8;
        private const ulong StageTwoLedgerCapacity = 8;
        private const ulong StageThreeLedgerCapacity = 16;
        private const ulong StubEntryOffset = 16;
        private const ulong StageOneStaticStateOffset = 128;
        private const ulong ContinuationOffset =
            BootReservedRange.PageSize + 256;
        private const ulong StageOneMapDestinationPage = 2;
        private const ulong StageOneCodePage = 4;
        private const ulong StageOneDataPage = 6;
        private const ulong LiveArenaPage = 12;
        private const ulong StageOneAllocationTablePage = 18;
        private const ulong StageOneLedgerPage = 19;
        private const ulong StageTwoAllocationTablePage = 21;
        private const ulong StageTwoLedgerPage = 23;
        private const ulong StageTwoStaticStatePage = 25;
        private const ulong StageTwoMapDestinationPage = 30;
        private const ulong StageTwoMapSourcePage = 31;
        private const ulong StageThreeAllocationTablePage = 34;
        private const ulong StageThreeLedgerPage = 35;
        private const ulong StageThreeStaticStatePage = 36;
        private const ulong StageOneMapSourcePage = 40;
        private const ulong StageThreeCodePage = 64;
        private const ulong StageThreeProbePage = 65;
        private const ulong StageThreeTablePage = 66;
        private const ulong AcpiRequirementPage = 53;
        private const ulong FramebufferRequirementPage = 54;
        private const ulong FirmwareRootPage = 55;
        private const ulong StageThreeMapDestinationPage = 58;
        private const ulong StageThreeMapSourcePage = 59;

        private byte* _backing;
        private BootMemoryRegionRecord* _stageOneRecords;
        private BootReservedRange* _stageOneLedger;
        private BootMemoryRegionRecord* _stageTwoRecords;
        private BootReservedRange* _stageTwoLedger;

        internal CanonicalFixture()
        {
            try
            {
                _backing = (byte*)NativeMemory.AlignedAlloc(
                    (nuint)BackingBytes,
                    (nuint)BootReservedRange.PageSize);
                if (_backing == null)
                    throw new OutOfMemoryException();
                Clear(_backing, BackingBytes);

                _stageOneRecords =
                    (BootMemoryRegionRecord*)AtPage(
                        StageOneAllocationTablePage);
                _stageOneLedger =
                    (BootReservedRange*)AtPage(StageOneLedgerPage);
                _stageTwoRecords =
                    (BootMemoryRegionRecord*)AtPage(
                        StageTwoAllocationTablePage);
                _stageTwoLedger =
                    (BootReservedRange*)AtPage(StageTwoLedgerPage);
                StageOneState =
                    (M01LoadedImageStackStaticState*)(
                        _backing + StageOneStaticStateOffset);
                StageTwoState =
                    (M01LiveAllocatorStaticState*)AtPage(
                        StageTwoStaticStatePage);
                Records =
                    (BootMemoryRegionRecord*)AtPage(
                        StageThreeAllocationTablePage);
                Ledger =
                    (BootReservedRange*)AtPage(
                        StageThreeLedgerPage);
                StaticState =
                    (M01LivePagingStaticState*)AtPage(
                        StageThreeStaticStatePage);
                CodePage = AtPage(StageThreeCodePage);
                ProbePage = AtPage(StageThreeProbePage);

                BuildStageOneAndTwo();
                Preconditions = M01014TestData.Context();
                BuildStageThree();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal M01LoadedImageStackStaticState* StageOneState
        {
            get;
            private set;
        }

        internal M01LiveAllocatorStaticState* StageTwoState
        {
            get;
            private set;
        }

        internal BootMemoryRegionRecord* Records { get; private set; }

        internal BootReservedRange* Ledger { get; private set; }

        internal M01LivePagingStaticState* StaticState
        {
            get;
            private set;
        }

        internal M01LivePagingPreparationState* PreparationState =>
            (M01LivePagingPreparationState*)StaticState;

        internal byte* CodePage { get; private set; }

        internal byte* ProbePage { get; private set; }

        internal ulong ProbeStart => (ulong)ProbePage;

        internal ulong TableStateStart =>
            Records[M01LivePagingIntegration
                .TableStateAllocationRecordIndex].PhysicalStart;

        internal ulong RawFirmwareCr3 { get; private set; }

        internal ulong LedgerCount { get; private set; }

        internal ulong LedgerCapacity => StageThreeLedgerCapacity;

        internal FirmwareMemoryMapRecord StageThreeMap
        {
            get;
            private set;
        }

        internal OwnedBootStackSwitchPlanResult StackSwitchPlan
        {
            get;
            private set;
        }

        internal M01LivePagingAllocationEvidence AllocationEvidence
        {
            get;
            private set;
        }

        internal M01LivePagingMapLedgerEvidence MapEvidence
        {
            get;
            private set;
        }

        internal X64PageTableEncodingPreconditions Preconditions
        {
            get;
        }

        internal X64BootIdentityMappingRequirement[]
            MappingRequirements { get; private set; } = [];

        internal X64BootIdentityMappingRange[] MappingRanges
        {
            get;
            private set;
        } = [];

        internal X64BootIdentityMappingPlanResult Mapping
        {
            get;
            private set;
        }

        internal X64PageTableImagePage[] PageImages
        {
            get;
            private set;
        } = [];

        internal ulong[] Entries { get; private set; } = [];

        internal ulong MappingRequirementCount =>
            (ulong)MappingRequirements.Length;

        internal ulong MappingRequirementCapacity =>
            (ulong)MappingRequirements.Length;

        internal ulong MappingRangeCapacity =>
            (ulong)MappingRanges.Length;

        internal ulong PageImageCapacity =>
            (ulong)PageImages.Length;

        internal ulong EntryCapacity => (ulong)Entries.Length;

        internal X64PagingModePreflightObservation
            PreflightObservation;

        internal X64ReversibleCr3TransitionRuntimeState*
            TransitionState =>
                (X64ReversibleCr3TransitionRuntimeState*)(
                    Records[M01LivePagingIntegration
                        .TableStateAllocationRecordIndex].VirtualStart
                    + Mapping.TotalTableByteLength);

        internal X64Cr3ProbeEvent[] Cr3Events { get; private set; } =
            [];

        internal X64ProtectionProbeObservation
            ProtectionObservation;

        internal X64ProtectionProbeEvent[] ProtectionEvents
        {
            get;
            private set;
        } = [];

        internal X64Cr3ProbePolicy Cr3Policy { get; private set; }

        internal M01LivePagingIntegrationResult ExecuteCurrent(
            M01LivePagingByteMode byteMode =
                M01LivePagingByteMode.EmitAndVerify)
        {
            return ExecuteCore(
                StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                StackSwitchPlan,
                Records,
                3,
                3,
                AllocationEvidence,
                MapEvidence,
                Ledger,
                LedgerCount,
                LedgerCapacity,
                MappingRequirements,
                MappingRequirementCount,
                MappingRequirementCapacity,
                MappingRanges,
                MappingRangeCapacity,
                PageImages,
                PageImageCapacity,
                Entries,
                EntryCapacity,
                CodePage,
                BootReservedRange.PageSize,
                ProbePage,
                BootReservedRange.PageSize,
                byteMode,
                StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal M01LivePagingPreparationResult PrepareCurrent(
            M01LivePagingByteMode byteMode =
                M01LivePagingByteMode.EmitAndVerify)
        {
            fixed (X64PagingModePreflightObservation* preflight =
                &PreflightObservation)
            {
                return PrepareCore(
                    StageTwoState,
                    1,
                    1,
                    StackSwitchPlan,
                    Records,
                    3,
                    3,
                    AllocationEvidence,
                    MapEvidence,
                    Ledger,
                    LedgerCount,
                    LedgerCapacity,
                    MappingRequirements,
                    MappingRequirementCount,
                    MappingRequirementCapacity,
                    MappingRanges,
                    0,
                    MappingRangeCapacity,
                    PageImages,
                    0,
                    PageImageCapacity,
                    Entries,
                    0,
                    EntryCapacity,
                    CodePage,
                    BootReservedRange.PageSize,
                    ProbePage,
                    BootReservedRange.PageSize,
                    byteMode,
                    PreparationState,
                    1,
                    1,
                    preflight,
                    1,
                    1);
            }
        }

        internal M01LivePagingPreparationResult PrepareStageTwo(
            M01LiveAllocatorStaticState* stageTwo,
            ulong count,
            ulong capacity)
        {
            fixed (X64PagingModePreflightObservation* preflight =
                &PreflightObservation)
            {
                return PrepareMetadata(
                    stageTwo,
                    count,
                    capacity,
                    StackSwitchPlan,
                    Records,
                    3,
                    3,
                    AllocationEvidence,
                    MapEvidence,
                    Ledger,
                    LedgerCount,
                    LedgerCapacity,
                    preflight,
                    1,
                    1,
                    PreparationState,
                    1,
                    1);
            }
        }

        internal M01LivePagingPreparationResult PrepareAllocation(
            BootMemoryRegionRecord* records,
            ulong count,
            ulong capacity,
            M01LivePagingAllocationEvidence evidence)
        {
            fixed (X64PagingModePreflightObservation* preflight =
                &PreflightObservation)
            {
                return PrepareMetadata(
                    StageTwoState,
                    1,
                    1,
                    StackSwitchPlan,
                    records,
                    count,
                    capacity,
                    evidence,
                    MapEvidence,
                    Ledger,
                    LedgerCount,
                    LedgerCapacity,
                    preflight,
                    1,
                    1,
                    PreparationState,
                    1,
                    1);
            }
        }

        internal M01LivePagingPreparationResult PrepareMapLedger(
            M01LivePagingMapLedgerEvidence evidence,
            BootReservedRange* ledger,
            ulong count,
            ulong capacity)
        {
            fixed (X64PagingModePreflightObservation* preflight =
                &PreflightObservation)
            {
                return PrepareMetadata(
                    StageTwoState,
                    1,
                    1,
                    StackSwitchPlan,
                    Records,
                    3,
                    3,
                    AllocationEvidence,
                    evidence,
                    ledger,
                    count,
                    capacity,
                    preflight,
                    1,
                    1,
                    PreparationState,
                    1,
                    1);
            }
        }

        internal M01LivePagingPreparationResult PreparePreflight(
            X64PagingModePreflightObservation* observation,
            ulong count,
            ulong capacity)
        {
            return PrepareMetadata(
                StageTwoState,
                1,
                1,
                StackSwitchPlan,
                Records,
                3,
                3,
                AllocationEvidence,
                MapEvidence,
                Ledger,
                LedgerCount,
                LedgerCapacity,
                observation,
                count,
                capacity,
                PreparationState,
                1,
                1);
        }

        internal M01LivePagingPreparationResult PrepareState(
            M01LivePagingPreparationState* state,
            ulong count,
            ulong capacity)
        {
            fixed (X64PagingModePreflightObservation* preflight =
                &PreflightObservation)
            {
                return PrepareMetadata(
                    StageTwoState,
                    1,
                    1,
                    StackSwitchPlan,
                    Records,
                    3,
                    3,
                    AllocationEvidence,
                    MapEvidence,
                    Ledger,
                    LedgerCount,
                    LedgerCapacity,
                    preflight,
                    1,
                    1,
                    state,
                    count,
                    capacity);
            }
        }

        internal M01LivePagingPreparationResult
            PrepareStateAliasesPreflight()
        {
            fixed (X64PagingModePreflightObservation* preflight =
                &PreflightObservation)
            {
                return PrepareMetadata(
                    StageTwoState,
                    1,
                    1,
                    StackSwitchPlan,
                    Records,
                    3,
                    3,
                    AllocationEvidence,
                    MapEvidence,
                    Ledger,
                    LedgerCount,
                    LedgerCapacity,
                    preflight,
                    1,
                    1,
                    (M01LivePagingPreparationState*)preflight,
                    1,
                    1);
            }
        }

        internal M01LivePagingIntegrationResult ExecuteRequirements(
            X64BootIdentityMappingRequirement[] requirements)
        {
            return ExecuteWorkspace(
                requirements,
                (ulong)requirements.Length,
                (ulong)requirements.Length,
                MappingRanges,
                MappingRangeCapacity,
                PageImages,
                PageImageCapacity,
                Entries,
                EntryCapacity,
                CodePage,
                BootReservedRange.PageSize,
                ProbePage,
                BootReservedRange.PageSize);
        }

        internal M01LivePagingIntegrationResult ExecuteWithAllocation(
            M01LivePagingAllocationEvidence evidence,
            BootMemoryRegionRecord* records,
            ulong count,
            ulong capacity)
        {
            return ExecuteCore(
                StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                StackSwitchPlan,
                records,
                count,
                capacity,
                evidence,
                MapEvidence,
                Ledger,
                LedgerCount,
                LedgerCapacity,
                MappingRequirements,
                MappingRequirementCount,
                MappingRequirementCapacity,
                MappingRanges,
                MappingRangeCapacity,
                PageImages,
                PageImageCapacity,
                Entries,
                EntryCapacity,
                CodePage,
                BootReservedRange.PageSize,
                ProbePage,
                BootReservedRange.PageSize,
                M01LivePagingByteMode.EmitAndVerify,
                StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal M01LivePagingIntegrationResult ExecuteWithMap(
            M01LivePagingMapLedgerEvidence evidence)
        {
            return ExecuteCore(
                StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                StackSwitchPlan,
                Records,
                3,
                3,
                AllocationEvidence,
                evidence,
                Ledger,
                LedgerCount,
                LedgerCapacity,
                MappingRequirements,
                MappingRequirementCount,
                MappingRequirementCapacity,
                MappingRanges,
                MappingRangeCapacity,
                PageImages,
                PageImageCapacity,
                Entries,
                EntryCapacity,
                CodePage,
                BootReservedRange.PageSize,
                ProbePage,
                BootReservedRange.PageSize,
                M01LivePagingByteMode.EmitAndVerify,
                StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal M01LivePagingIntegrationResult ExecuteWithStack(
            OwnedBootStackSwitchPlanResult plan)
        {
            return ExecuteCore(
                StageTwoState,
                M01LiveAllocatorStaticState.RequiredStorageBytes,
                plan,
                Records,
                3,
                3,
                AllocationEvidence,
                MapEvidence,
                Ledger,
                LedgerCount,
                LedgerCapacity,
                MappingRequirements,
                MappingRequirementCount,
                MappingRequirementCapacity,
                MappingRanges,
                MappingRangeCapacity,
                PageImages,
                PageImageCapacity,
                Entries,
                EntryCapacity,
                CodePage,
                BootReservedRange.PageSize,
                ProbePage,
                BootReservedRange.PageSize,
                M01LivePagingByteMode.EmitAndVerify,
                StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal M01LivePagingIntegrationResult ExecuteMetadata(
            M01LiveAllocatorStaticState* stageTwo,
            ulong stageTwoCapacity,
            OwnedBootStackSwitchPlanResult stackPlan,
            BootMemoryRegionRecord* records,
            ulong recordCount,
            ulong recordCapacity,
            M01LivePagingAllocationEvidence allocations,
            M01LivePagingMapLedgerEvidence map,
            BootReservedRange* ledger,
            ulong ledgerCount,
            ulong ledgerCapacity,
            M01LivePagingStaticState* state,
            ulong stateCapacity)
        {
            return ExecuteCore(
                stageTwo,
                stageTwoCapacity,
                stackPlan,
                records,
                recordCount,
                recordCapacity,
                allocations,
                map,
                ledger,
                ledgerCount,
                ledgerCapacity,
                MappingRequirements,
                MappingRequirementCount,
                MappingRequirementCapacity,
                MappingRanges,
                MappingRangeCapacity,
                PageImages,
                PageImageCapacity,
                Entries,
                EntryCapacity,
                CodePage,
                BootReservedRange.PageSize,
                ProbePage,
                BootReservedRange.PageSize,
                M01LivePagingByteMode.EmitAndVerify,
                state,
                stateCapacity);
        }

        internal M01LivePagingPreparationResult PrepareMetadata(
            M01LiveAllocatorStaticState* stageTwo,
            ulong stageTwoCount,
            ulong stageTwoCapacity,
            OwnedBootStackSwitchPlanResult stackPlan,
            BootMemoryRegionRecord* records,
            ulong recordCount,
            ulong recordCapacity,
            M01LivePagingAllocationEvidence allocations,
            M01LivePagingMapLedgerEvidence map,
            BootReservedRange* ledger,
            ulong ledgerCount,
            ulong ledgerCapacity,
            X64PagingModePreflightObservation* preflight,
            ulong preflightCount,
            ulong preflightCapacity,
            M01LivePagingPreparationState* state,
            ulong stateCount,
            ulong stateCapacity)
        {
            return PrepareCore(
                stageTwo,
                stageTwoCount,
                stageTwoCapacity,
                stackPlan,
                records,
                recordCount,
                recordCapacity,
                allocations,
                map,
                ledger,
                ledgerCount,
                ledgerCapacity,
                MappingRequirements,
                MappingRequirementCount,
                MappingRequirementCapacity,
                MappingRanges,
                0,
                MappingRangeCapacity,
                PageImages,
                0,
                PageImageCapacity,
                Entries,
                0,
                EntryCapacity,
                CodePage,
                BootReservedRange.PageSize,
                ProbePage,
                BootReservedRange.PageSize,
                M01LivePagingByteMode.EmitAndVerify,
                state,
                stateCount,
                stateCapacity,
                preflight,
                preflightCount,
                preflightCapacity);
        }

        internal M01LivePagingIntegrationResult ExecuteWorkspace(
            X64BootIdentityMappingRequirement[]? requirements,
            ulong requirementCount,
            ulong requirementCapacity,
            X64BootIdentityMappingRange[]? ranges,
            ulong rangeCapacity,
            X64PageTableImagePage[]? pages,
            ulong pageCapacity,
            ulong[]? entries,
            ulong entryCapacity,
            byte* codePage,
            ulong codeCapacity,
            byte* probePage,
            ulong probeCapacity,
            bool aliasPageStorage = false)
        {
            fixed (X64BootIdentityMappingRequirement* requirementPointer =
                requirements)
            fixed (X64BootIdentityMappingRange* rangePointer = ranges)
            fixed (X64PageTableImagePage* pagePointer = pages)
            fixed (ulong* entryPointer = entries)
            {
                return ExecuteCoreRawWorkspace(
                    StageTwoState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes,
                    StackSwitchPlan,
                    Records,
                    3,
                    3,
                    AllocationEvidence,
                    MapEvidence,
                    Ledger,
                    LedgerCount,
                    LedgerCapacity,
                    requirementPointer,
                    requirementCount,
                    requirementCapacity,
                    rangePointer,
                    rangeCapacity,
                    aliasPageStorage
                        ? (X64PageTableImagePage*)entryPointer
                        : pagePointer,
                    pageCapacity,
                    entryPointer,
                    entryCapacity,
                    codePage,
                    codeCapacity,
                    probePage,
                    probeCapacity,
                    M01LivePagingByteMode.EmitAndVerify,
                    StaticState,
                    M01LivePagingStaticState.RequiredStorageBytes);
            }
        }

        internal M01LivePagingAllocationEvidence
            CaptureAllocationEvidence(
                ulong codeOrdinal,
                ulong probeOrdinal,
                ulong tableOrdinal,
                ulong codeStatus = 0,
                ulong probeStatus = 0,
                ulong tableStatus = 0,
                BootMemoryRegionRecord* records = null,
                ulong count = 3,
                ulong capacity = 3)
        {
            return M01LivePagingAllocationEvidence
                .CaptureCallerAttested(
                    records == null ? Records : records,
                    count,
                    capacity,
                    codeOrdinal,
                    probeOrdinal,
                    tableOrdinal,
                    codeStatus,
                    probeStatus,
                    tableStatus);
        }

        internal M01LivePagingMapLedgerEvidence CaptureMapEvidence(
            ulong? previousMapKey = null,
            ulong? previousMapGeneration = null,
            FirmwareMemoryMapRecord? refreshedMap = null,
            ulong? refreshedMapGeneration = null,
            ulong mapCaptureOrdinal =
                StageThreeMapCaptureOrdinal,
            ulong ledgerBuildOrdinal =
                StageThreeLedgerBuildOrdinal,
            BootReservedRange* ledger = null,
            ulong? ledgerCount = null,
            ulong? ledgerCapacity = null)
        {
            return M01LivePagingMapLedgerEvidence
                .CaptureCallerAttested(
                    previousMapKey
                        ?? StageTwoState->RefreshedMapKey,
                    previousMapGeneration
                        ?? StageTwoState->RefreshedMapGeneration,
                    refreshedMap ?? StageThreeMap,
                    refreshedMapGeneration
                        ?? StageThreeMapGeneration,
                    AllocationEvidence,
                    mapCaptureOrdinal,
                    ledgerBuildOrdinal,
                    ledger == null ? Ledger : ledger,
                    ledgerCount ?? LedgerCount,
                    ledgerCapacity ?? LedgerCapacity);
        }

        internal M01LivePagingMapLedgerEvidence
            RefreshMapEvidence()
        {
            MapEvidence = CaptureMapEvidence();
            return MapEvidence;
        }

        internal BootReservedRange FindReservation(
            BootReservationKind kind)
        {
            for (ulong index = 0; index < LedgerCount; index++)
            {
                if (Ledger[index].Kind == kind)
                    return Ledger[index];
            }
            return default;
        }

        internal BootReservedRange[] CopyLedger()
        {
            BootReservedRange[] copy =
                new BootReservedRange[checked((int)LedgerCount)];
            for (int index = 0; index < copy.Length; index++)
                copy[index] = Ledger[index];
            return copy;
        }

        internal void RestoreLedger(BootReservedRange[] ledger)
        {
            Clear(
                (byte*)Ledger,
                LedgerCapacity * (ulong)sizeof(BootReservedRange));
            for (int index = 0; index < ledger.Length; index++)
                Ledger[index] = ledger[index];
            LedgerCount = (ulong)ledger.Length;
            MapEvidence = CaptureMapEvidence();
        }

        internal void RemoveReservation(BootReservationKind kind)
        {
            List<BootReservedRange> ranges = [];
            bool removed = false;
            for (ulong index = 0; index < LedgerCount; index++)
            {
                if (!removed && Ledger[index].Kind == kind)
                {
                    removed = true;
                    continue;
                }
                ranges.Add(Ledger[index]);
            }
            if (!removed)
                throw new InvalidOperationException("reservation missing");
            WriteStageThreeLedger([.. ranges]);
        }

        internal void ReplaceReservation(
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
                throw new InvalidOperationException("reservation missing");
            WriteStageThreeLedger(ranges);
        }

        internal void ClearStaticState()
        {
            Clear(
                (byte*)StaticState,
                M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal void ClearPreparationState()
        {
            Clear(
                (byte*)PreparationState,
                M01LivePagingPreparationState.RequiredStorageBytes);
        }

        internal bool PreparationStateIsZero()
        {
            byte* state = (byte*)PreparationState;
            for (ulong index = 0;
                index
                    < M01LivePagingPreparationState
                        .RequiredStorageBytes;
                index++)
            {
                if (state[index] != 0)
                    return false;
            }
            return true;
        }

        internal void FillStubPages(byte value)
        {
            for (ulong index = 0;
                index < BootReservedRange.PageSize;
                index++)
            {
                CodePage[index] = value;
                ProbePage[index] = value;
            }
        }

        internal bool PaddingIsZero()
        {
            ulong preflightEnd =
                M01LivePagingIntegration.PreflightEntryOffset
                + X64PagingModePreflightStubPlan.RequiredByteLength;
            ulong transitionEnd =
                M01LivePagingIntegration.TransitionEntryOffset
                + X64ReversibleCr3TransitionStubPlan.RequiredByteLength;
            ulong invlpgEnd =
                M01LivePagingIntegration.InvlpgEntryOffset
                + X64InvlpgStubPlan.RequiredByteLength;
            ulong probeEnd =
                M01LivePagingIntegration.ProtectionProbeEntryOffset
                + X64ProtectionProbeStubPlan.RequiredByteLength;
            for (ulong index = 0;
                index < BootReservedRange.PageSize;
                index++)
            {
                bool codeByte =
                    (index >= M01LivePagingIntegration
                            .PreflightEntryOffset
                        && index < preflightEnd)
                    || (index >= M01LivePagingIntegration
                            .TransitionEntryOffset
                        && index < transitionEnd)
                    || (index >= M01LivePagingIntegration
                            .InvlpgEntryOffset
                        && index < invlpgEnd);
                if (!codeByte && CodePage[index] != 0)
                    return false;

                bool probeByte =
                    index >= M01LivePagingIntegration
                        .ProtectionProbeEntryOffset
                    && index < probeEnd;
                if (!probeByte && ProbePage[index] != 0)
                    return false;
            }
            return true;
        }

        internal byte[] CopyCodePage()
        {
            return CopyBytes(CodePage, BootReservedRange.PageSize);
        }

        internal byte[] CopyProbePage()
        {
            return CopyBytes(ProbePage, BootReservedRange.PageSize);
        }

        internal void RestoreStubPages(
            byte[] code,
            byte[] probe)
        {
            for (int index = 0; index < code.Length; index++)
                CodePage[index] = code[index];
            for (int index = 0; index < probe.Length; index++)
                ProbePage[index] = probe[index];
        }

        internal bool CodePageEquals(byte[] expected)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                if (CodePage[index] != expected[index])
                    return false;
            }
            return true;
        }

        internal bool ProbePageEquals(byte[] expected)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                if (ProbePage[index] != expected[index])
                    return false;
            }
            return true;
        }

        internal OwnedBootStackSwitchPlanResult
            CreateMismatchedStackPlan()
        {
            return MismatchedStackPlan;
        }

        internal X64Cr3ProbeEvent CreateCr3Event(
            X64Cr3ProbeEventKind kind,
            bool ownedRoot,
            bool updatedCanary,
            ulong elapsed)
        {
            return new X64Cr3ProbeEvent(
                kind,
                ownedRoot ? TableStateStart : RawFirmwareCr3,
                ProbeRsp,
                updatedCanary ? UpdatedCanary : InitialCanary,
                elapsed);
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
            _stageTwoRecords = null;
            _stageTwoLedger = null;
            StageOneState = null;
            StageTwoState = null;
            Records = null;
            Ledger = null;
            StaticState = null;
            CodePage = null;
            ProbePage = null;
        }

        private OwnedBootStackSwitchPlanResult MismatchedStackPlan
        {
            get;
            set;
        }

        private VirtualAddress ProbeRsp { get; set; }

        private byte* AtPage(ulong page)
        {
            return _backing + page * BootReservedRange.PageSize;
        }

        private void BuildStageOneAndTwo()
        {
            EfiLoadedImageProtocolObservation observation =
                EfiLoadedImageProtocolObservation
                    .CaptureFromUefiBoundary(
                        0,
                        1,
                        (ulong)_backing,
                        (ulong)_backing,
                        LoadedImageBytes);
            EfiLoadedImageRangeCapturePlan loadedImage =
                EfiLoadedImageRangeCapturePlan.Create(
                    &observation,
                    1,
                    1,
                    EfiLoadedImageRange.PageSize);
            if (!loadedImage.Succeeded)
                throw new InvalidOperationException(
                    "stage-one loaded image: " + loadedImage.Status);

            byte* stageOneCode = AtPage(StageOneCodePage);
            byte* stageOneData = AtPage(StageOneDataPage);
            _stageOneRecords[0] = Record(
                UefiMemoryType.LoaderCode,
                (ulong)stageOneCode,
                1);
            _stageOneRecords[1] = Record(
                UefiMemoryType.LoaderData,
                (ulong)stageOneData,
                StackArenaPages);
            _stageOneLedger[0] = loadedImage.Range.Reservation;
            _stageOneLedger[1] = CreateRange(
                BootReservationKind.BootStackSwitchStub,
                (ulong)stageOneCode,
                1);
            _stageOneLedger[2] = CreateRange(
                BootReservationKind.BootStackGuard,
                (ulong)stageOneData,
                GuardPages);
            _stageOneLedger[3] = CreateRange(
                BootReservationKind.BootStack,
                (ulong)stageOneData
                    + GuardPages * BootReservedRange.PageSize,
                StackPages);

            FirmwareMemoryMapRecord stageOneMap =
                CreateOwnedMap(
                    AtPage(StageOneMapDestinationPage),
                    AtPage(StageOneMapSourcePage),
                    StageOneMapKey);
            M01StageOneAllocationEvidence stageOneAllocation =
                M01StageOneAllocationEvidence.CaptureCallerAttested(
                    _stageOneRecords,
                    2,
                    2,
                    StageOneCodeAllocationOrdinal,
                    StageOneDataAllocationOrdinal,
                    M01StageOneAllocationEvidence.EfiSuccess,
                    M01StageOneAllocationEvidence.EfiSuccess);
            M01StageOneMapLedgerEvidence stageOneMapLedger =
                M01StageOneMapLedgerEvidence.CaptureCallerAttested(
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
                        stageOneCode,
                        BootReservedRange.PageSize,
                        BootReservedRange.PageSize,
                        StubEntryOffset,
                        OwnedBootStackSwitchPlan
                            .RequiredStubByteLength,
                        Virtual((ulong)_backing + ContinuationOffset),
                        StageOneState,
                        M01LoadedImageStackStaticState
                            .RequiredStorageBytes,
                        M01StageOneStubByteMode.EmitAndVerify);
            if (!prepared.IsPrepared)
            {
                throw new InvalidOperationException(
                    "stage-one prepare: " + prepared.Status);
            }
            StackSwitchPlan = prepared.Evidence.SwitchPlanResult;

            MismatchedStackPlan =
                OwnedBootStackSwitchPlan.Create(
                    _stageOneRecords,
                    2,
                    2,
                    prepared.Evidence.StackLayoutResult.Layout,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    Virtual(
                        (ulong)StageOneState
                        + M01LoadedImageStackStaticState
                            .HandoffStorageOffset),
                    OwnedBootStackSwitchHandoff.StructureSize,
                    OwnedBootStackSwitchHandoffProvenance
                        .CallerAttestedStaticOwnedData,
                    Virtual(
                        (ulong)StageOneState
                        + M01LoadedImageStackStaticState
                            .SwitchStateStorageOffset),
                    OwnedBootStackSwitchStateRecord.StructureSize,
                    OwnedBootStackSwitchStateStorageProvenance
                        .CallerAttestedStaticOwnedData,
                    Virtual(
                        (ulong)_backing + ContinuationOffset + 16),
                    OwnedBootStackSwitchContinuationContract
                        .StaticNonReturningRspAndHandoff);
            if (!MismatchedStackPlan.Succeeded)
            {
                throw new InvalidOperationException(
                    "mismatch stack fixture: "
                    + MismatchedStackPlan.Status);
            }

            M01LoadedImageStackStaticTransitionResult transfer =
                M01LoadedImageStackIntegration
                    .RecordTransferFromStaticState(
                        StageOneState,
                        M01LoadedImageStackStaticState
                            .RequiredStorageBytes);
            if (!transfer.TransferRecorded)
            {
                throw new InvalidOperationException(
                    "stage-one transfer: " + transfer.Status);
            }
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
                    "stage-one continuation: " + observed.Status);
            }

            ulong arenaStart = (ulong)AtPage(LiveArenaPage);
            const ulong arenaPages = 4;
            _stageTwoRecords[0] = Record(
                UefiMemoryType.LoaderData,
                arenaStart,
                arenaPages);
            BootReservedRange[] stageTwoReservations =
            [
                loadedImage.Range.Reservation,
                _stageOneLedger[1],
                _stageOneLedger[2],
                _stageOneLedger[3],
                CreateRange(
                    BootReservationKind.AllocatorArena,
                    arenaStart,
                    arenaPages)
            ];
            WriteLedger(
                _stageTwoLedger,
                StageTwoLedgerCapacity,
                stageTwoReservations);

            FirmwareMemoryMapRecord stageTwoMap =
                CreateOwnedMap(
                    AtPage(StageTwoMapDestinationPage),
                    AtPage(StageTwoMapSourcePage),
                    StageTwoMapKey);
            M01LiveAllocatorAllocationEvidence stageTwoAllocation =
                M01LiveAllocatorAllocationEvidence
                    .CaptureCallerAttested(
                        _stageTwoRecords,
                        1,
                        1,
                        ArenaAllocationOrdinal,
                        M01LiveAllocatorAllocationEvidence.EfiSuccess);
            M01LiveAllocatorMapLedgerEvidence stageTwoMapLedger =
                M01LiveAllocatorMapLedgerEvidence
                    .CaptureCallerAttested(
                        StageOneMapKey,
                        StageOneMapGeneration,
                        stageTwoMap,
                        StageTwoMapGeneration,
                        stageTwoAllocation,
                        StageTwoMapCaptureOrdinal,
                        StageTwoLedgerBuildOrdinal,
                        _stageTwoLedger,
                        (ulong)stageTwoReservations.Length,
                        StageTwoLedgerCapacity);
            M01LiveAllocatorIntegrationResult stageTwo =
                M01LiveAllocatorIntegration.Execute(
                    StageOneState,
                    M01LoadedImageStackStaticState
                        .RequiredStorageBytes,
                    _stageTwoRecords,
                    1,
                    1,
                    stageTwoAllocation,
                    stageTwoMapLedger,
                    _stageTwoLedger,
                    (ulong)stageTwoReservations.Length,
                    StageTwoLedgerCapacity,
                    StageTwoState,
                    M01LiveAllocatorStaticState.RequiredStorageBytes);
            if (!stageTwo.Succeeded || !StageTwoState->IsCompleted)
            {
                throw new InvalidOperationException(
                    "stage-two fixture: " + stageTwo.Status);
            }
        }

        private void BuildStageThree()
        {
            BuildMappingPlan(
                out X64BootIdentityMappingRequirement[] requirements,
                out X64BootIdentityMappingRange[] ranges,
                out X64BootIdentityMappingPlanResult mapping);
            MappingRequirements = requirements;
            MappingRanges = ranges;
            Mapping = mapping;
            if (!Mapping.Succeeded)
            {
                throw new InvalidOperationException(
                    "stage-three mapping: " + Mapping.Status);
            }

            Records[0] = Record(
                UefiMemoryType.LoaderCode,
                (ulong)CodePage,
                1);
            Records[1] = Record(
                UefiMemoryType.LoaderCode,
                (ulong)ProbePage,
                1);
            Records[2] = Record(
                UefiMemoryType.LoaderData,
                (ulong)AtPage(StageThreeTablePage),
                Mapping.TotalTablePageCount + 1);
            PageImages =
                new X64PageTableImagePage[
                    X64PageMap.MaximumPageImages];
            Entries =
                new ulong[checked((int)(
                    Mapping.TotalTablePageCount
                    * X64PageTableImagePage.EntryCountPerPage))];

            BootReservedRange[] reservations =
                new BootReservedRange[10];
            for (int index = 0; index < 5; index++)
                reservations[index] = _stageTwoLedger[index];
            reservations[5] = CreateRange(
                BootReservationKind.AcpiScratch,
                (ulong)AtPage(AcpiRequirementPage),
                1);
            reservations[6] = CreateRange(
                BootReservationKind.FramebufferMetadata,
                (ulong)AtPage(FramebufferRequirementPage),
                1);
            reservations[7] = CreateRange(
                BootReservationKind.Cr3TransitionStub,
                Records[0].PhysicalStart,
                Records[0].PageCount);
            reservations[8] = CreateRange(
                BootReservationKind.PageProtectionProbe,
                Records[1].PhysicalStart,
                Records[1].PageCount);
            reservations[9] = CreateRange(
                BootReservationKind.PageTableArena,
                Records[2].PhysicalStart,
                Records[2].PageCount);
            WriteStageThreeLedger(reservations);

            StageThreeMap = CreateOwnedMap(
                AtPage(StageThreeMapDestinationPage),
                AtPage(StageThreeMapSourcePage),
                0x0030);
            AllocationEvidence = CaptureAllocationEvidence(
                Cr3CodeAllocationOrdinal,
                ProbeAllocationOrdinal,
                TableStateAllocationOrdinal);
            MapEvidence = CaptureMapEvidence();

            RawFirmwareCr3 =
                (ulong)AtPage(FirmwareRootPage) | 0x18UL;
            PreflightObservation =
                X64PagingModePreflightObservation.CreateObserved(
                    ValidCr0,
                    RawFirmwareCr3,
                    ValidCr4,
                    ValidEfer,
                    X64PagingModePreflightStubPlan
                        .CpuidAddressSizesLeaf,
                    48,
                    ValidRflags);

            ulong stackStart = Raw(
                StackSwitchPlan.Plan.UsableStackVirtualStart);
            ulong stackEnd = Raw(
                StackSwitchPlan.Plan
                    .UsableStackVirtualEndExclusive);
            ulong rsp = Raw(
                StackSwitchPlan.Plan
                    .ExpectedContinuationStackPointer) - 64;
            ProbeRsp = Virtual(rsp);
            ulong clearedCr4 =
                ValidCr4
                & ~X64ReversibleCr3TransitionStubPlan
                    .Cr4PageGlobalEnableBit;
            *TransitionState =
                X64ReversibleCr3TransitionRuntimeState
                    .CreateObserved(
                        TableStateStart,
                        stackStart,
                        stackEnd,
                        (ulong)TransitionState
                            + X64ReversibleCr3TransitionRuntimeState
                                .StructureSize,
                        InitialCanary,
                        UpdatedCanary,
                        ValidRflags,
                        RawFirmwareCr3,
                        ValidCr4,
                        clearedCr4,
                        TableStateStart,
                        InitialCanary,
                        UpdatedCanary,
                        RawFirmwareCr3,
                        ValidCr4,
                        (ValidRflags
                            & ~X64ReversibleCr3TransitionStubPlan
                                .RflagsInterruptEnableBit)
                            ^ 1UL,
                        ValidRflags,
                        rsp,
                        rsp,
                        UpdatedCanary,
                        0,
                        X64ReversibleCr3TransitionRuntimeStatus
                            .Success,
                        X64ReversibleCr3TransitionCheckpoint
                            .Returned);

            Cr3Policy = new X64Cr3ProbePolicy(
                enabledByDefault: false,
                explicitOneShotOptIn: true,
                timeoutTicks: 1000);
            Cr3Events = CreateCr3Events();
            ProtectionObservation =
                X64ProtectionProbeObservation.CreateModeled(
                    X64ProtectionProbeTerminalKind.ModeledReturn,
                    Virtual(ProbeStart),
                    1,
                    X64ProtectionProbeStubPlan.ExpectedReturnValue,
                    ProtectionCanary,
                    ProtectionCanary);
            ProtectionEvents =
                new X64ProtectionProbeEvent[
                    X64ProtectionProbeStubPlan.RequiredEventCount];
            for (int index = 0;
                index < ProtectionEvents.Length;
                index++)
            {
                ProtectionEvents[index] =
                    new X64ProtectionProbeEvent(
                        (X64ProtectionProbeEventKind)(index + 1),
                        (ulong)index);
            }
        }

        private void BuildMappingPlan(
            out X64BootIdentityMappingRequirement[] requirements,
            out X64BootIdentityMappingRange[] ranges,
            out X64BootIdentityMappingPlanResult result)
        {
            ulong dataPages = 5;
            requirements = [];
            ranges = [];
            result = default;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                requirements = RequirementsFor(dataPages);
                ranges = new X64BootIdentityMappingRange[
                    X64BootIdentityMappingPlan.MaximumDestinationCount];
                fixed (X64BootIdentityMappingRequirement* source =
                    requirements)
                fixed (X64BootIdentityMappingRange* destination =
                    ranges)
                {
                    result = X64BootIdentityMappingPlan.Create(
                        source,
                        (ulong)requirements.Length,
                        (ulong)requirements.Length,
                        destination,
                        (ulong)ranges.Length,
                        X64PageMap.MaximumPageImages,
                        Preconditions);
                }
                if (!result.Succeeded)
                    return;

                ulong exactPages = result.TotalTablePageCount + 1;
                if (exactPages == dataPages)
                    return;
                dataPages = exactPages;
            }
            throw new InvalidOperationException(
                "stage-three page accounting did not converge");
        }

        private X64BootIdentityMappingRequirement[]
            RequirementsFor(ulong tableStatePages)
        {
            ulong page = BootReservedRange.PageSize;
            ulong guard = (ulong)AtPage(StageOneDataPage);
            ulong stack = guard + page;
            ulong table = (ulong)AtPage(StageThreeTablePage);
            ulong loadedImage = (ulong)_backing;
            BootReservedRange allocatorArena =
                _stageTwoLedger[4];
            ulong handoffStorageStart = Raw(
                StackSwitchPlan.Plan.Handoff.StorageStart);
            ulong handoffStorageEnd = Raw(
                StackSwitchPlan.Plan.Handoff.StorageEndExclusive);
            ulong handoffPageStart =
                handoffStorageStart & ~(page - 1);
            ulong handoffPageEnd =
                checked((handoffStorageEnd + page - 1)
                    & ~(page - 1));
            return
            [
                Requirement(
                    X64BootIdentityMappingPurpose.BootStackGuard,
                    guard,
                    guard + page,
                    PageProtection.None,
                    PageCachePolicy.Uninitialized),
                Requirement(
                    X64BootIdentityMappingPurpose.ExecutingImage,
                    loadedImage,
                    loadedImage + page,
                    PageProtection.Read),
                Requirement(
                    X64BootIdentityMappingPurpose.ExecutingImage,
                    loadedImage + page,
                    loadedImage + 2 * page,
                    M01014TestData.ReadExecute),
                Requirement(
                    X64BootIdentityMappingPurpose.ExecutingImage,
                    loadedImage + 2 * page,
                    loadedImage + LoadedImageBytes,
                    PageProtection.Read),
                Requirement(
                    X64BootIdentityMappingPurpose.OwnedStack,
                    stack,
                    stack + StackPages * page,
                    M01014TestData.ReadWrite),
                Requirement(
                    X64BootIdentityMappingPurpose.BootHandoff,
                    handoffPageStart,
                    handoffPageEnd,
                    PageProtection.Read),
                Requirement(
                    X64BootIdentityMappingPurpose.SerialStub,
                    (ulong)AtPage(StageOneCodePage),
                    (ulong)AtPage(StageOneCodePage + 1),
                    M01014TestData.ReadExecute),
                Requirement(
                    X64BootIdentityMappingPurpose.AcpiScratch,
                    (ulong)AtPage(AcpiRequirementPage),
                    (ulong)AtPage(AcpiRequirementPage + 1),
                    PageProtection.Read),
                Requirement(
                    X64BootIdentityMappingPurpose.FramebufferMetadata,
                    (ulong)AtPage(FramebufferRequirementPage),
                    (ulong)AtPage(FramebufferRequirementPage + 1),
                    PageProtection.Read),
                Requirement(
                    X64BootIdentityMappingPurpose.AdditionalRequired,
                    (ulong)CodePage,
                    (ulong)CodePage + page,
                    M01014TestData.ReadExecute),
                Requirement(
                    X64BootIdentityMappingPurpose.AdditionalRequired,
                    (ulong)ProbePage,
                    (ulong)ProbePage + page,
                    M01014TestData.ReadWrite),
                Requirement(
                    X64BootIdentityMappingPurpose.AdditionalRequired,
                    table,
                    table + tableStatePages * page,
                    M01014TestData.ReadWrite),
                RequirementForReservation(
                    X64BootIdentityMappingPurpose.AdditionalRequired,
                    allocatorArena,
                    M01014TestData.ReadWrite)
            ];
        }

        private M01LivePagingIntegrationResult ExecuteCore(
            M01LiveAllocatorStaticState* stageTwo,
            ulong stageTwoCapacity,
            OwnedBootStackSwitchPlanResult stackPlan,
            BootMemoryRegionRecord* records,
            ulong recordCount,
            ulong recordCapacity,
            M01LivePagingAllocationEvidence allocations,
            M01LivePagingMapLedgerEvidence map,
            BootReservedRange* ledger,
            ulong ledgerCount,
            ulong ledgerCapacity,
            X64BootIdentityMappingRequirement[]? requirements,
            ulong requirementCount,
            ulong requirementCapacity,
            X64BootIdentityMappingRange[]? ranges,
            ulong rangeCapacity,
            X64PageTableImagePage[]? pages,
            ulong pageCapacity,
            ulong[]? entries,
            ulong entryCapacity,
            byte* codePage,
            ulong codeCapacity,
            byte* probePage,
            ulong probeCapacity,
            M01LivePagingByteMode byteMode,
            M01LivePagingStaticState* state,
            ulong stateCapacity)
        {
            fixed (X64BootIdentityMappingRequirement* requirementPointer =
                requirements)
            fixed (X64BootIdentityMappingRange* rangePointer = ranges)
            fixed (X64PageTableImagePage* pagePointer = pages)
            fixed (ulong* entryPointer = entries)
            {
                return ExecuteCoreRawWorkspace(
                    stageTwo,
                    stageTwoCapacity,
                    stackPlan,
                    records,
                    recordCount,
                    recordCapacity,
                    allocations,
                    map,
                    ledger,
                    ledgerCount,
                    ledgerCapacity,
                    requirementPointer,
                    requirementCount,
                    requirementCapacity,
                    rangePointer,
                    rangeCapacity,
                    pagePointer,
                    pageCapacity,
                    entryPointer,
                    entryCapacity,
                    codePage,
                    codeCapacity,
                    probePage,
                    probeCapacity,
                    byteMode,
                    state,
                    stateCapacity);
            }
        }

        private M01LivePagingPreparationResult PrepareCore(
            M01LiveAllocatorStaticState* stageTwo,
            ulong stageTwoCount,
            ulong stageTwoCapacity,
            OwnedBootStackSwitchPlanResult stackPlan,
            BootMemoryRegionRecord* records,
            ulong recordCount,
            ulong recordCapacity,
            M01LivePagingAllocationEvidence allocations,
            M01LivePagingMapLedgerEvidence map,
            BootReservedRange* ledger,
            ulong ledgerCount,
            ulong ledgerCapacity,
            X64BootIdentityMappingRequirement[]? requirements,
            ulong requirementCount,
            ulong requirementCapacity,
            X64BootIdentityMappingRange[]? ranges,
            ulong rangeCount,
            ulong rangeCapacity,
            X64PageTableImagePage[]? pages,
            ulong pageCount,
            ulong pageCapacity,
            ulong[]? entries,
            ulong entryCount,
            ulong entryCapacity,
            byte* codePage,
            ulong codeCapacity,
            byte* probePage,
            ulong probeCapacity,
            M01LivePagingByteMode byteMode,
            M01LivePagingPreparationState* state,
            ulong stateCount,
            ulong stateCapacity,
            X64PagingModePreflightObservation* preflight,
            ulong preflightCount,
            ulong preflightCapacity)
        {
            fixed (X64BootIdentityMappingRequirement* requirementPointer =
                requirements)
            fixed (X64BootIdentityMappingRange* rangePointer = ranges)
            fixed (X64PageTableImagePage* pagePointer = pages)
            fixed (ulong* entryPointer = entries)
            {
                return PrepareCoreRawWorkspace(
                    stageTwo,
                    stageTwoCount,
                    stageTwoCapacity,
                    stackPlan,
                    records,
                    recordCount,
                    recordCapacity,
                    allocations,
                    map,
                    ledger,
                    ledgerCount,
                    ledgerCapacity,
                    requirementPointer,
                    requirementCount,
                    requirementCapacity,
                    rangePointer,
                    rangeCount,
                    rangeCapacity,
                    pagePointer,
                    pageCount,
                    pageCapacity,
                    entryPointer,
                    entryCount,
                    entryCapacity,
                    codePage,
                    codeCapacity,
                    probePage,
                    probeCapacity,
                    preflight,
                    preflightCount,
                    preflightCapacity,
                    byteMode,
                    state,
                    stateCount,
                    stateCapacity);
            }
        }

        private M01LivePagingPreparationResult
            PrepareCoreRawWorkspace(
                M01LiveAllocatorStaticState* stageTwo,
                ulong stageTwoCount,
                ulong stageTwoCapacity,
                OwnedBootStackSwitchPlanResult stackPlan,
                BootMemoryRegionRecord* records,
                ulong recordCount,
                ulong recordCapacity,
                M01LivePagingAllocationEvidence allocations,
                M01LivePagingMapLedgerEvidence map,
                BootReservedRange* ledger,
                ulong ledgerCount,
                ulong ledgerCapacity,
                X64BootIdentityMappingRequirement* requirements,
                ulong requirementCount,
                ulong requirementCapacity,
                X64BootIdentityMappingRange* ranges,
                ulong rangeCount,
                ulong rangeCapacity,
                X64PageTableImagePage* pages,
                ulong pageCount,
                ulong pageCapacity,
                ulong* entries,
                ulong entryCount,
                ulong entryCapacity,
                byte* codePage,
                ulong codeCapacity,
                byte* probePage,
                ulong probeCapacity,
                X64PagingModePreflightObservation* preflight,
                ulong preflightCount,
                ulong preflightCapacity,
                M01LivePagingByteMode byteMode,
                M01LivePagingPreparationState* state,
                ulong stateCount,
                ulong stateCapacity)
        {
            return M01LivePagingIntegration.Prepare(
                stageTwo,
                stageTwoCount,
                stageTwoCapacity,
                stackPlan,
                records,
                recordCount,
                recordCapacity,
                allocations,
                map,
                ledger,
                ledgerCount,
                ledgerCapacity,
                requirements,
                requirementCount,
                requirementCapacity,
                ranges,
                rangeCount,
                rangeCapacity,
                pages,
                pageCount,
                pageCapacity,
                entries,
                entryCount,
                entryCapacity,
                Preconditions,
                codePage,
                codeCapacity,
                probePage,
                probeCapacity,
                preflight,
                preflightCount,
                preflightCapacity,
                byteMode,
                state,
                stateCount,
                stateCapacity);
        }

        private M01LivePagingIntegrationResult
            ExecuteCoreRawWorkspace(
                M01LiveAllocatorStaticState* stageTwo,
                ulong stageTwoCapacity,
                OwnedBootStackSwitchPlanResult stackPlan,
                BootMemoryRegionRecord* records,
                ulong recordCount,
                ulong recordCapacity,
                M01LivePagingAllocationEvidence allocations,
                M01LivePagingMapLedgerEvidence map,
                BootReservedRange* ledger,
                ulong ledgerCount,
                ulong ledgerCapacity,
                X64BootIdentityMappingRequirement* requirements,
                ulong requirementCount,
                ulong requirementCapacity,
                X64BootIdentityMappingRange* ranges,
                ulong rangeCapacity,
                X64PageTableImagePage* pages,
                ulong pageCapacity,
                ulong* entries,
                ulong entryCapacity,
                byte* codePage,
                ulong codeCapacity,
                byte* probePage,
                ulong probeCapacity,
                M01LivePagingByteMode byteMode,
                M01LivePagingStaticState* state,
                ulong stateCapacity)
        {
            fixed (X64PagingModePreflightObservation* preflight =
                &PreflightObservation)
            fixed (X64Cr3ProbeEvent* cr3Events = Cr3Events)
            fixed (X64ProtectionProbeEvent* protectionEvents =
                ProtectionEvents)
            {
                return M01LivePagingIntegration.Execute(
                    stageTwo,
                    stageTwoCapacity,
                    stackPlan,
                    records,
                    recordCount,
                    recordCapacity,
                    allocations,
                    map,
                    ledger,
                    ledgerCount,
                    ledgerCapacity,
                    requirements,
                    requirementCount,
                    requirementCapacity,
                    ranges,
                    rangeCapacity,
                    pages,
                    pageCapacity,
                    entries,
                    entryCapacity,
                    Preconditions,
                    codePage,
                    codeCapacity,
                    probePage,
                    probeCapacity,
                    preflight,
                    1,
                    TransitionState,
                    cr3Events,
                    (ulong)Cr3Events.Length,
                    (ulong)Cr3Events.Length,
                    ProtectionObservation,
                    protectionEvents,
                    (ulong)ProtectionEvents.Length,
                    (ulong)ProtectionEvents.Length,
                    Cr3Policy,
                    byteMode,
                    state,
                    stateCapacity);
            }
        }

        private X64Cr3ProbeEvent[] CreateCr3Events()
        {
            return
            [
                CreateCr3Event(
                    X64Cr3ProbeEventKind.PreMarkerEmitted,
                    ownedRoot: false,
                    updatedCanary: false,
                    elapsed: 1),
                CreateCr3Event(
                    X64Cr3ProbeEventKind.TransitionEntered,
                    ownedRoot: false,
                    updatedCanary: false,
                    elapsed: 2),
                CreateCr3Event(
                    X64Cr3ProbeEventKind.OwnedRootLoaded,
                    ownedRoot: true,
                    updatedCanary: false,
                    elapsed: 3),
                CreateCr3Event(
                    X64Cr3ProbeEventKind.OwnedRootReadBack,
                    ownedRoot: true,
                    updatedCanary: false,
                    elapsed: 4),
                CreateCr3Event(
                    X64Cr3ProbeEventKind.CanaryUpdated,
                    ownedRoot: true,
                    updatedCanary: true,
                    elapsed: 5),
                CreateCr3Event(
                    X64Cr3ProbeEventKind.FirmwareRootRestored,
                    ownedRoot: false,
                    updatedCanary: true,
                    elapsed: 6),
                CreateCr3Event(
                    X64Cr3ProbeEventKind.FirmwareRootReadBack,
                    ownedRoot: false,
                    updatedCanary: true,
                    elapsed: 7),
                CreateCr3Event(
                    X64Cr3ProbeEventKind.CanaryValidated,
                    ownedRoot: false,
                    updatedCanary: true,
                    elapsed: 8),
                CreateCr3Event(
                    X64Cr3ProbeEventKind.PostMarkerEmitted,
                    ownedRoot: false,
                    updatedCanary: true,
                    elapsed: 9)
            ];
        }

        private void WriteStageThreeLedger(
            BootReservedRange[] reservations)
        {
            WriteLedger(
                Ledger,
                LedgerCapacity,
                reservations);
            LedgerCount = (ulong)reservations.Length;
            if (AllocationEvidence.Size != 0)
                MapEvidence = CaptureMapEvidence();
        }

        private static void WriteLedger(
            BootReservedRange* destination,
            ulong capacity,
            BootReservedRange[] reservations)
        {
            if ((ulong)reservations.Length > capacity)
                throw new InvalidOperationException(
                    "ledger capacity exceeded");
            Array.Sort(
                reservations,
                static (left, right) =>
                {
                    left.PhysicalStart
                        .TryGetRawValueForUefiBoundary(
                            out ulong leftStart);
                    right.PhysicalStart
                        .TryGetRawValueForUefiBoundary(
                            out ulong rightStart);
                    return leftStart.CompareTo(rightStart);
                });
            Clear(
                (byte*)destination,
                capacity * (ulong)sizeof(BootReservedRange));
            for (int index = 0;
                index < reservations.Length;
                index++)
            {
                destination[index] = reservations[index];
            }
        }

        private static FirmwareMemoryMapRecord CreateOwnedMap(
            byte* destination,
            byte* source,
            ulong mapKey)
        {
            return new FirmwareMemoryMapRecord
            {
                BufferAddress = (ulong)destination,
                BufferSize = 80,
                SourceBufferAddress = (ulong)source,
                SourceBufferSize = 80,
                MapSize = 80,
                MapKey = mapKey,
                DescriptorSize = 40,
                DescriptorVersion = 1,
                DescriptorCount = 2,
                Flags = FirmwareMemoryMapRecord.FlagOwnedCopy
            };
        }

        internal M01LivePagingPreparationResult PrepareWorkspace(
            X64BootIdentityMappingRequirement[]? requirements,
            ulong requirementCount,
            ulong requirementCapacity,
            X64BootIdentityMappingRange[]? ranges,
            ulong rangeCount,
            ulong rangeCapacity,
            X64PageTableImagePage[]? pages,
            ulong pageCount,
            ulong pageCapacity,
            ulong[]? entries,
            ulong entryCount,
            ulong entryCapacity,
            byte* codePage,
            ulong codeCapacity,
            byte* probePage,
            ulong probeCapacity,
            M01LivePagingByteMode byteMode =
                M01LivePagingByteMode.EmitAndVerify,
            bool aliasPageStorage = false)
        {
            fixed (X64BootIdentityMappingRequirement* requirementPointer =
                requirements)
            fixed (X64BootIdentityMappingRange* rangePointer = ranges)
            fixed (X64PageTableImagePage* pagePointer = pages)
            fixed (ulong* entryPointer = entries)
            fixed (X64PagingModePreflightObservation* preflight =
                &PreflightObservation)
            {
                return PrepareCoreRawWorkspace(
                    StageTwoState,
                    1,
                    1,
                    StackSwitchPlan,
                    Records,
                    3,
                    3,
                    AllocationEvidence,
                    MapEvidence,
                    Ledger,
                    LedgerCount,
                    LedgerCapacity,
                    requirementPointer,
                    requirementCount,
                    requirementCapacity,
                    rangePointer,
                    rangeCount,
                    rangeCapacity,
                    aliasPageStorage
                        ? (X64PageTableImagePage*)entryPointer
                        : pagePointer,
                    pageCount,
                    pageCapacity,
                    entryPointer,
                    entryCount,
                    entryCapacity,
                    codePage,
                    codeCapacity,
                    probePage,
                    probeCapacity,
                    preflight,
                    1,
                    1,
                    byteMode,
                    PreparationState,
                    1,
                    1);
            }
        }
    }
}
