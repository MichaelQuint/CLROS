using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static unsafe partial class M01OwnedCr3CompositeTransitionTests
{
    private const string IssueTestName = "m01.027";
    private const string LiveIntervalIssueTestName = "m01.030";
    private const string LiveIntervalReadyMarker = "CLROS_M01_030_READY";
    private const string CallGateIssueTestName = "m01.032";
    private const string CallGateReadyMarker = "CLROS_M01_032_READY";
    private const int BuildTimeoutMilliseconds = 90_000;
    private const ulong ExpectedRequiredByteLength = 3830;
    private const ulong ExpectedInstructionCount = 732;
    private const ulong ExpectedFullPageFnv1a64 = 0xf004_46f6_1be1_3cddUL;
    private const string ExpectedFullPageSha256 = "C70C7BDD0B4E5F877074C2C881F67B079B5165D6F7D48EA1B8E3E2011B1F24BC";
    private const string ExpectedInstructionPrefixSha256 = "12CADD81A9C5EFB9D47AAFEA49988D271656216140B87FF1AE3E0224567F5387";
    private const ulong ExpectedCallGateFnv1a64 = 0xf903_c5de_16d3_a5b2UL;
    private const ulong ExpectedCombinedFnv1a64 = 0x42ca_f3b4_a6c2_119aUL;
    private const string ExpectedCallGateSha256 = "8B2965E741AC40B725B898D7B2BB2FFBDF7CC4C50DF9AEB163C17485270BD88A";
    private const string ExpectedCombinedPageSha256 = "FA1B2EC2332CF70A5222B22B672758A1E948FD0F179FB91A16D7DD4280FD5631";
    private const ulong ExpectedCallGateBoundaryCanary = 0x434c_524f_535f_4347UL;
    private const ulong RflagsTrapFlag = 1UL << 8;
    private const ulong RflagsAlwaysOneBit = 1UL << 1;
    private const ulong RflagsAlignmentCheckFlag = 1UL << 18;
    private const ulong RflagsArithmeticMask = (1UL << 0) | (1UL << 2) | (1UL << 4) | (1UL << 6) | (1UL << 7) | (1UL << 11);
    private const ulong Cr4ControlFlowEnforcementBit = 1UL << 23;
    private const ulong OwnedCr3 = 0x0000_0000_0020_0000UL;
    private const ulong TablePageArenaStart = OwnedCr3;
    private const ulong TablePageArenaEnd = 0x0000_0000_0020_5000UL;
    private const ulong TableStateArenaStart = 0x0000_0000_0020_4000UL;
    private const ulong TableStateArenaEnd = 0x0000_0000_0020_5000UL;
    private const ulong CompositeCodeStart = 0x0000_0000_0040_0000UL;
    private const ulong CompositeCodeEnd = CompositeCodeStart + 0x1000;
    private const ulong ContinuationPageStart = 0x0000_0000_0060_0000UL;
    private const ulong ContinuationPageEnd = ContinuationPageStart + 0x1000;
    private const ulong ExpectedReturnAddress = ContinuationPageStart + 0x100;
    private const ulong StackStart = 0x0000_0000_0080_0000UL;
    private const ulong StackEnd = StackStart + 0x1000;
    private const ulong ProbePageStart = 0x0000_0000_0090_0000UL;
    private const ulong ProbePageEnd = ProbePageStart + 0x1000;
    private const ulong ProbeEntry = ProbePageStart;
    private const ulong ProbePteAddress = TablePageArenaStart + 0x3800;
    private const ulong InitialProbePte = ProbePageStart | M01OwnedCr3CompositeTransition.PagePresentBit | M01OwnedCr3CompositeTransition.PageWritableBit | M01OwnedCr3CompositeTransition.PageNoExecuteBit;
    private const ulong FinalProbePte = ProbePageStart | M01OwnedCr3CompositeTransition.PagePresentBit;
    private const ulong CanaryAddress = TableStateArenaStart + M01OwnedCr3CompositeTransitionRuntimeState.StructureSize;
    private const ulong ExpectedCanary = 0x434c_524f_535f_494eUL;
    private const ulong ReplacementCanary = 0x434c_524f_535f_4f4bUL;
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;
        Console.WriteLine("TEST_START " + IssueTestName);
        Run("m01.027 stable enums pod layouts defaults and capabilities", TestStableSurfaceAndDefaults, ref failures);
        Run("m01.027 canonical range binding and adversarial inputs", TestRangeBindingAndAdversarialInputs, ref failures);
        Run("m01.027 materialization readback corruption and atomicity", TestMaterializationReadbackAndAtomicity, ref failures);
        Run("m01.027 independent byte decode control flow and abi", TestIndependentBytePlanAndAbi, ref failures);
        Run("m01.027 observed runtime audit and recovery classification", TestObservedRuntimeAuditAndRecovery, ref failures);
        Run("m01.027 production source and test marker firewall", TestSourceAndMarkerFirewall, ref failures);
        Run("m01.027 exact nostdlib efi compile closure", TestNoStdLibEfiCompileClosure, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException("M01-027 composite transition tests failed: " + failures);
        }

        Console.WriteLine("CLROS_M01_027_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);
        RunLiveIntervalContractTests();
    }

    private static void RunLiveIntervalContractTests()
    {
        int failures = 0;
        Console.WriteLine("TEST_START " + LiveIntervalIssueTestName);
        Run("m01.030 exact identity planner profile is closed", X64BootIdentityMappingPlanTests.TestM01030ExactLiveIntervalContract, ref failures);
        Run("m01.030 exact preparation profile is closed", M01LivePagingIntegrationTests.TestM01030ExactPreparationContract, ref failures);
        Run("m01.030 exact composite roles and provenance are closed", TestM01030ExactCompositeContract, ref failures);
        Run("m01.030 exact marker and evidence boundary is closed", TestM01030MarkerAndEvidenceBoundary, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + LiveIntervalIssueTestName + " failures=" + failures);
            throw new InvalidOperationException("M01-030 exact live-interval contract tests failed: " + failures);
        }

        Console.WriteLine(LiveIntervalReadyMarker);
        Console.WriteLine("TEST_PASS " + LiveIntervalIssueTestName);
        RunCallGateContractTests();
    }

    private static void RunCallGateContractTests()
    {
        int failures = 0;
        Console.WriteLine("TEST_START " + CallGateIssueTestName);
        Run("m01.032 stable enums pod layouts defaults and constants", TestM01032StableSurfaceAndDefaults, ref failures);
        Run("m01.032 preparation bindings policy and adversarial inputs", TestM01032PreparationAndAdversarialInputs, ref failures);
        Run("m01.032 materialization readback hashes and atomicity", TestM01032MaterializationReadbackAndAtomicity, ref failures);
        Run("m01.032 independent call gate decode", TestM01032IndependentCallGateDecode, ref failures);
        Run("m01.032 modeled observation transcript and recovery", TestM01032ObservationAuditAndRecovery, ref failures);
        Run("m01.032 marker source and nostdlib boundaries", TestM01032MarkerSourceAndNoStdLibBoundary, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + CallGateIssueTestName + " failures=" + failures);
            throw new InvalidOperationException("M01-032 composite call-gate tests failed: " + failures);
        }

        Console.WriteLine(CallGateReadyMarker);
        Console.WriteLine("TEST_PASS " + CallGateIssueTestName);
    }

    private static bool TestM01032StableSurfaceAndDefaults()
    {
        (string Name, byte Value)[] statuses = [("Uninitialized", 0), ("Success", 1), ("MissingPriorPlanningEvidence", 2), ("InvalidPriorPlanningEvidence", 3), ("MissingCompositePreparation", 4), ("InvalidCompositePreparation", 5), ("MissingAllocationRecords", 6), ("InvalidAllocationRecordCount", 7), ("AllocationRecordCapacityMismatch", 8), ("MissingReservationRecords", 9), ("InvalidReservationRecordCount", 10), ("ReservationRecordCapacityMismatch", 11), ("MissingIdentityMappings", 12), ("InvalidIdentityMappingCount", 13), ("IdentityMappingCapacityMismatch", 14), ("MissingCodeBinding", 15), ("InvalidCodeBindingCount", 16), ("CodeBindingCapacityMismatch", 17), ("InvalidCodeBinding", 18), ("MissingObservation", 19), ("InvalidObservationCount", 20), ("ObservationCapacityMismatch", 21), ("MisalignedObservation", 22), ("ObservationSpanOverflow", 23), ("ObservationAlias", 24), ("ObservationNotPristine", 25), ("RepeatedUse", 26), ("InvalidCodeBase", 27), ("MisalignedCodeBase", 28), ("CodeRangeOverflow", 29), ("InvalidCompositeEntry", 30), ("InvalidGateEntry", 31), ("InvalidReturnSite", 32), ("InvalidCallTarget", 33), ("CodeLayoutOverlap", 34), ("InvalidModePreflight", 35), ("TrapFlagSet", 36), ("DirectionFlagSet", 37), ("CetUnsupported", 38), ("FiveLevelPagingUnsupported", 39), ("PcideUnsupported", 40), ("InvalidInterruptPolicy", 41), ("InvalidPgePolicy", 42), ("MissingCodeStorage", 43), ("InvalidCodeStorageCount", 44), ("CodeStorageCapacityMismatch", 45), ("MisalignedCodeStorage", 46), ("CodeStorageSpanOverflow", 47), ("StorageAlias", 48), ("MissingSemanticStorage", 49), ("InvalidSemanticCount", 50), ("SemanticCapacityMismatch", 51), ("MisalignedSemanticStorage", 52), ("SemanticSpanOverflow", 53), ("InternalEncodingFailure", 54), ("ByteMismatch", 55), ("MissingEvidence", 56), ("InvalidEvidence", 57), ("InvalidTerminalKind", 58), ("Timeout", 59), ("Reset", 60), ("TripleFault", 61), ("CompletionSignatureMismatch", 62), ("CallCountMismatch", 63), ("ObservationCountMismatch", 64), ("CompositeReturnMismatch", 65), ("PostReturnRflagsMismatch", 66), ("PostReturnInterruptMismatch", 67), ("FirmwareCr3Mismatch", 68), ("FirmwareCr4Mismatch", 69), ("BoundaryCanaryMismatch", 70), ("CompositeStateAuditFailed", 71), ("ReservedStateNonzero", 72), ("InvalidGateStackTranscript", 73)];
        (string Name, byte Value)[] phases = [("Uninitialized", 0), ("Prepared", 1), ("Materialized", 2), ("ReadbackVerified", 3), ("ObservedAudit", 4)];
        (string Name, byte Value)[] lifecycles = [("Uninitialized", 0), ("Armed", 1), ("InProgress", 2), ("Completed", 3)];
        (string Name, byte Value)[] interruptPolicies = [("Uninitialized", 0), ("PreserveEntryState", 1)];
        (string Name, byte Value)[] pgePolicies = [("Uninitialized", 0), ("ClearDuringOwnedIntervalAndRestoreExact", 1)];
        M01OwnedCr3CompositeCallGateResult result = default;
        M01OwnedCr3CompositeCallGateObservation observation = default;
        bool offsets = Offset<M01OwnedCr3CompositeCallGateObservation>("HeaderSize") == 0 && Offset<M01OwnedCr3CompositeCallGateObservation>("HeaderVersion") == 4 && Offset<M01OwnedCr3CompositeCallGateObservation>("IsInitializedValue") == 6 && Offset<M01OwnedCr3CompositeCallGateObservation>("LifecycleValue") == 7 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedSelfAddressValue") == 8 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedCodeBaseValue") == 16 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedCompositeEntryAddressValue") == 24 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedGateEntryAddressValue") == 32 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedReturnSiteAddressValue") == 40 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedOriginalRflagsValue") == 48 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedFirmwareCr3RawValue") == 56 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedFirmwareCr4RawValue") == 64 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedBoundaryCanaryValue") == 72 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedCompositeReturnValueValue") == 80 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedInterruptEnabledValue") == 84 && Offset<M01OwnedCr3CompositeCallGateObservation>("ExpectedPgeWasSetValue") == 85 && Offset<M01OwnedCr3CompositeCallGateObservation>("InterruptPolicyValue") == 86 && Offset<M01OwnedCr3CompositeCallGateObservation>("PgePolicyValue") == 87 && Offset<M01OwnedCr3CompositeCallGateObservation>("BoundaryCanaryValue") == 88 && Offset<M01OwnedCr3CompositeCallGateObservation>("CanaryBeforeValue") == 96 && Offset<M01OwnedCr3CompositeCallGateObservation>("PostReturnRflagsValue") == 104 && Offset<M01OwnedCr3CompositeCallGateObservation>("PostReturnCr3RawValue") == 112 && Offset<M01OwnedCr3CompositeCallGateObservation>("PostReturnCr4RawValue") == 120 && Offset<M01OwnedCr3CompositeCallGateObservation>("CanaryAfterValue") == 128 && Offset<M01OwnedCr3CompositeCallGateObservation>("CompositeReturnValueValue") == 136 && Offset<M01OwnedCr3CompositeCallGateObservation>("CallCountValue") == 140 && Offset<M01OwnedCr3CompositeCallGateObservation>("PostReturnObservationCountValue") == 141 && Offset<M01OwnedCr3CompositeCallGateObservation>("ReservedOutput0") == 142 && Offset<M01OwnedCr3CompositeCallGateObservation>("CompletionSignatureValue") == 144 && Offset<M01OwnedCr3CompositeCallGateObservation>("ReservedOutput1") == 148 && Offset<M01OwnedCr3CompositeCallGateObservation>("ReservedOutput2") == 152;
        return EnumContractMatches<M01OwnedCr3CompositeCallGateStatus>(statuses) && EnumContractMatches<M01OwnedCr3CompositeCallGatePhase>(phases) && EnumContractMatches<M01OwnedCr3CompositeCallGateObservationLifecycle>(lifecycles) && EnumContractMatches<M01OwnedCr3CompositeCallGateInterruptPolicy>(interruptPolicies) && EnumContractMatches<M01OwnedCr3CompositeCallGatePgePolicy>(pgePolicies) && sizeof(M01OwnedCr3CompositeCallGateObservation) == M01OwnedCr3CompositeCallGateObservation.StructureSize && sizeof(M01OwnedCr3CompositeCallGateResult) == checked((int)M01OwnedCr3CompositeCallGateResult.StructureSize) && Marshal.SizeOf<M01OwnedCr3CompositeCallGateObservation>() == 160 && Marshal.SizeOf<M01OwnedCr3CompositeCallGateResult>() == 776 && offsets && M01OwnedCr3CompositeTransition.CompositeEntryOffset == 0 && M01OwnedCr3CompositeTransition.CallGateOffset == 0xf00 && M01OwnedCr3CompositeTransition.CallGateByteLength == 0x100 && M01OwnedCr3CompositeTransition.MaximumCallGateByteLength == 0x100 && M01OwnedCr3CompositeTransition.CallGateInstructionCount == 74 && M01OwnedCr3CompositeTransition.CallInstructionOffset == 0xfc4 && M01OwnedCr3CompositeTransition.CallNextReturnSiteOffset == 0xfc9 && M01OwnedCr3CompositeTransition.CallGateObservationPointerBias == 127 && M01OwnedCr3CompositeTransition.CallGateRequiredEntryRspModulo == 8 && M01OwnedCr3CompositeTransition.CallGateRequiredLowerHeadroomBytes == 128 && M01OwnedCr3CompositeTransition.CallGateRequiredUpperFrameBytes == 40 && M01OwnedCr3CompositeTransition.GateToCompositeEntryRspDelta == 48 && M01OwnedCr3CompositeTransition.CallGateGapByteLength == 10 && M01OwnedCr3CompositeTransition.CallGateTailByteLength == 0 && M01OwnedCr3CompositeTransition.CombinedInstructionCount == 806 && M01OwnedCr3CompositeTransition.RequiredCallGateFnv1a64 == ExpectedCallGateFnv1a64 && M01OwnedCr3CompositeTransition.RequiredCombinedFnv1a64 == ExpectedCombinedFnv1a64 && M01OwnedCr3CompositeTransition.CallToCompositeRel32 == 0xffff_f037U && M01OwnedCr3CompositeTransition.CallGateRejectedReturnValue == (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState && M01OwnedCr3CompositeTransition.ExternalPatchCount == 0 && !M01OwnedCr3CompositeTransition.HasExternalRelocations && M01OwnedCr3CompositeTransition.UsesWindowsX64Abi && !M01OwnedCr3CompositeTransition.CallGateExecutesGeneratedCode && !M01OwnedCr3CompositeTransition.CallGatePerformsLiveControlRegisterRead && !M01OwnedCr3CompositeTransition.CallGateProvidesGuestEvidence && !M01OwnedCr3CompositeTransition.CallGateInvokesFirmwareCallbacks && !M01OwnedCr3CompositeTransition.CallGateInvokesManagedCallbacks && !M01OwnedCr3CompositeTransition.CallGateTouchesXmm6ThroughXmm15 && M01OwnedCr3CompositeTransition.CallGateUsesOnlyVolatileRegistersBeforeGuardsPass && !M01OwnedCr3CompositeTransition.CallGateRejectPathWritesObservation && !M01OwnedCr3CompositeTransition.CallGateRejectPathInvokesComposite && M01OwnedCr3CompositeTransition.CallGateBytePlanReadsCr3AndCr4AfterReturn && M01OwnedCr3CompositeTransition.CallGateRestoresPreparedRflagsImmediatelyBeforeCompositeCall && !M01OwnedCr3CompositeTransition.CallGateSelfValidatesLiveEntryRflags && M01OwnedCr3CompositeTransition.CallGateRequiresExternalEntryRflagsMatchPreparedSnapshot && M01OwnedCr3CompositeTransition.CallGateRequiresExternalPointerMappingTrust && !M01OwnedCr3CompositeTransition.CallGateSelfValidatesPointerMapping && M01OwnedCr3CompositeTransition.CallGateExactCompositeCallCount == 1 && M01OwnedCr3CompositeTransition.CallGateExactPostReturnObservationCount == 1 && observation.Size == 0 && observation.Version == 0 && !observation.IsInitialized && !observation.IsArmed && !observation.IsCompleted && result.Status == M01OwnedCr3CompositeCallGateStatus.Uninitialized && result.Phase == M01OwnedCr3CompositeCallGatePhase.Uninitialized && !result.IsSuccess && !result.IsPrepared && !result.IsMaterialized && !result.IsReadbackVerified && !result.IsObservedAudit && result.InvalidIndex == 0 && result.Fnv1a64 == 0 && result.UsesWindowsX64Abi && !result.HasExternalRelocations && !result.InvokesFirmwareCallbacks && !result.InvokesManagedCallbacks && !result.TouchesXmm6ThroughXmm15 && !result.ExecutesGeneratedCode && !result.PerformsLiveCr3Load && !result.PerformsLiveControlRegisterRead && !result.IsGuestEvidence;
    }

    private static M01OwnedCr3CompositeCallGateResult PrepareCallGateForTest(CompositeFixture fixture, M01LivePagingPreparationResult prior, M01OwnedCr3CompositeTransitionResult composite, M01OwnedCr3CompositeCallGateObservation* observation, M01OwnedCr3CompositeTransitionRangeBinding[]? bindings = null, CallGateMissingInput missing = CallGateMissingInput.None, ulong priorCount = 1, ulong priorCapacity = 1, ulong compositeCount = 1, ulong compositeCapacity = 1, ulong? allocationCount = null, ulong? allocationCapacity = null, ulong? reservationCount = null, ulong? reservationCapacity = null, ulong? mappingCount = null, ulong? mappingCapacity = null, ulong bindingCount = 1, ulong bindingCapacity = 1, ulong observationCount = 1, ulong observationCapacity = 1, ulong? observationAddress = null, ulong expectedBoundaryCanary = ExpectedCallGateBoundaryCanary)
    {
        M01OwnedCr3CompositeTransitionRangeBinding[] selectedBindings = bindings ?? [fixture.Bindings[0]];
        fixed (X64BootIdentityMappingRange* mappings = fixture.Mappings)
        fixed (M01OwnedCr3CompositeTransitionRangeBinding* bindingPointer = selectedBindings)
        {
            return M01OwnedCr3CompositeTransition.PrepareCallGate(missing == CallGateMissingInput.Prior ? null : &prior, priorCount, priorCapacity, missing == CallGateMissingInput.Composite ? null : &composite, compositeCount, compositeCapacity, missing == CallGateMissingInput.Allocations ? null : fixture.AllocationPointer, allocationCount ?? fixture.AllocationCount, allocationCapacity ?? fixture.AllocationCapacity, missing == CallGateMissingInput.Reservations ? null : fixture.ReservationPointer, reservationCount ?? fixture.ReservationCount, reservationCapacity ?? fixture.ReservationCapacity, missing == CallGateMissingInput.Mappings ? null : mappings, mappingCount ?? fixture.MappingCount, mappingCapacity ?? (ulong)fixture.Mappings.Length, missing == CallGateMissingInput.Binding ? null : bindingPointer, bindingCount, bindingCapacity, missing == CallGateMissingInput.Observation ? null : observation, observationCount, observationCapacity, Virtual(observationAddress ?? (ulong)observation), expectedBoundaryCanary);
        }
    }

    private static M01OwnedCr3CompositeCallGateObservation* GetCallGateObservation(CompositeFixture fixture)
    {
        return (M01OwnedCr3CompositeCallGateObservation*)((byte*)fixture.StateDestination + 0x300);
    }

    private static void ClearCallGateObservation(M01OwnedCr3CompositeCallGateObservation* observation)
    {
        new Span<byte>(observation, checked((int)M01OwnedCr3CompositeCallGateObservation.StructureSize)).Clear();
    }

    private static bool IsZeroCallGateObservation(M01OwnedCr3CompositeCallGateObservation* observation)
    {
        ReadOnlySpan<byte> bytes = new(observation, checked((int)M01OwnedCr3CompositeCallGateObservation.StructureSize));
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void SetPriorPreflightRflags(ref M01LivePagingPreparationResult prior, ulong rflags)
    {
        int evidenceOffset = checked((int)Offset<M01LivePagingPreparationResult>("_evidence"));
        int preflightOffset = checked((int)Offset<M01LivePagingPreparationEvidence>("PreflightValue"));
        int preflightRflagsOffset = checked((int)Offset<X64PagingModePreflightResult>("_rflags"));
        int stateOffset = checked((int)Offset<M01LivePagingPreparationResult>("_state"));
        int stateRflagsOffset = checked((int)Offset<M01LivePagingPreparationState>("OriginalRflagsValue"));
        OverwriteUlong(ref prior, checked(evidenceOffset + preflightOffset + preflightRflagsOffset), rflags);
        OverwriteUlong(ref prior, checked(stateOffset + stateRflagsOffset), rflags);
    }

    private static void SetPriorPreflightCr4(ref M01LivePagingPreparationResult prior, ulong rawCr4)
    {
        int evidenceOffset = checked((int)Offset<M01LivePagingPreparationResult>("_evidence"));
        int preflightOffset = checked((int)Offset<M01LivePagingPreparationEvidence>("PreflightValue"));
        int preflightCr4Offset = checked((int)Offset<X64PagingModePreflightResult>("_rawCr4"));
        int stateOffset = checked((int)Offset<M01LivePagingPreparationResult>("_state"));
        int stateCr4Offset = checked((int)Offset<M01LivePagingPreparationState>("RawCr4Value"));
        OverwriteUlong(ref prior, checked(evidenceOffset + preflightOffset + preflightCr4Offset), rawCr4);
        OverwriteUlong(ref prior, checked(stateOffset + stateCr4Offset), rawCr4);
    }

    private static bool TestM01032PreparationAndAdversarialInputs()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionResult composite = fixture.Execute();
        if (!composite.IsStatePrepared)
        {
            return false;
        }

        M01OwnedCr3CompositeCallGateObservation* observation = GetCallGateObservation(fixture);
        ClearCallGateObservation(observation);
        M01LivePagingPreparationResult prior = fixture.PriorEvidence;
        M01OwnedCr3CompositeCallGateResult prepared = PrepareCallGateForTest(fixture, prior, composite, observation);
        X64PagingModePreflightResult preflight = prior.Evidence.Preflight;
        ulong codeBase = composite.State.CompositeCodeStart;
        if (!prepared.IsPrepared || prepared.Status != M01OwnedCr3CompositeCallGateStatus.Success || prepared.Phase != M01OwnedCr3CompositeCallGatePhase.Prepared || prepared.TerminalKind != M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized || prepared.CodeBase != codeBase || prepared.CodeCapacity != M01OwnedCr3CompositeTransition.PageSize || prepared.CompositeOffset != 0 || prepared.CompositeLength != M01OwnedCr3CompositeTransition.RequiredByteLength || prepared.GateOffset != 0xf00 || prepared.GateLength != 0x100 || prepared.GateEntryAddress != codeBase + 0xf00 || prepared.CallInstructionOffset != 0xfc4 || prepared.ReturnSiteOffset != 0xfc9 || prepared.ReturnSiteAddress != codeBase + 0xfc9 || prepared.PreparedState.ExpectedReturnAddress != codeBase + 0xfc9 || prepared.FullPageByteCount != 0 || prepared.InstructionCount != 0 || prepared.InvalidIndex != M01OwnedCr3CompositeCallGateResult.NoIndex || prepared.Fnv1a64 != 0 || !prepared.Observation.IsArmed || !observation->IsArmed || prepared.Observation.ExpectedSelfAddress != (ulong)observation || prepared.Observation.ExpectedCodeBase != codeBase || prepared.Observation.ExpectedCompositeEntryAddress != codeBase || prepared.Observation.ExpectedGateEntryAddress != codeBase + 0xf00 || prepared.Observation.ExpectedReturnSiteAddress != codeBase + 0xfc9 || prepared.Observation.ExpectedOriginalRflags != preflight.Rflags || prepared.Observation.ExpectedFirmwareCr3Raw != preflight.RawFirmwareCr3 || prepared.Observation.ExpectedFirmwareCr4Raw != preflight.RawCr4 || prepared.Observation.ExpectedBoundaryCanary != ExpectedCallGateBoundaryCanary || prepared.Observation.BoundaryCanary != ExpectedCallGateBoundaryCanary || prepared.Observation.ExpectedCompositeReturnValue != (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success || prepared.Observation.ExpectedInterruptEnabled != ((preflight.Rflags & M01OwnedCr3CompositeTransition.RflagsInterruptEnableBit) != 0) || prepared.Observation.ExpectedPgeWasSet != ((preflight.RawCr4 & M01OwnedCr3CompositeTransition.Cr4PageGlobalEnableBit) != 0) || prepared.Observation.InterruptPolicy != M01OwnedCr3CompositeCallGateInterruptPolicy.PreserveEntryState || prepared.Observation.PgePolicy != M01OwnedCr3CompositeCallGatePgePolicy.ClearDuringOwnedIntervalAndRestoreExact)
        {
            return false;
        }

        M01OwnedCr3CompositeCallGateResult repeat = PrepareCallGateForTest(fixture, prior, composite, observation);
        if (repeat.Status != M01OwnedCr3CompositeCallGateStatus.RepeatedUse)
        {
            return false;
        }

        ClearCallGateObservation(observation);
        ((byte*)observation)[M01OwnedCr3CompositeTransition.CallGateObservationCanaryAfterOffset] = 1;
        if (PrepareCallGateForTest(fixture, prior, composite, observation).Status != M01OwnedCr3CompositeCallGateStatus.ObservationNotPristine)
        {
            return false;
        }

        (CallGateMissingInput Missing, M01OwnedCr3CompositeCallGateStatus Status)[] missingCases = [(CallGateMissingInput.Prior, M01OwnedCr3CompositeCallGateStatus.MissingPriorPlanningEvidence), (CallGateMissingInput.Composite, M01OwnedCr3CompositeCallGateStatus.MissingCompositePreparation), (CallGateMissingInput.Allocations, M01OwnedCr3CompositeCallGateStatus.MissingAllocationRecords), (CallGateMissingInput.Reservations, M01OwnedCr3CompositeCallGateStatus.MissingReservationRecords), (CallGateMissingInput.Mappings, M01OwnedCr3CompositeCallGateStatus.MissingIdentityMappings), (CallGateMissingInput.Binding, M01OwnedCr3CompositeCallGateStatus.MissingCodeBinding), (CallGateMissingInput.Observation, M01OwnedCr3CompositeCallGateStatus.MissingObservation)];
        foreach (var item in missingCases)
        {
            ClearCallGateObservation(observation);
            if (PrepareCallGateForTest(fixture, prior, composite, observation, missing: item.Missing).Status != item.Status || !IsZeroCallGateObservation(observation))
            {
                return false;
            }
        }

        ClearCallGateObservation(observation);
        if (PrepareCallGateForTest(fixture, prior, composite, observation, priorCount: 0).Status != M01OwnedCr3CompositeCallGateStatus.InvalidPriorPlanningEvidence || PrepareCallGateForTest(fixture, prior, composite, observation, compositeCapacity: 2).Status != M01OwnedCr3CompositeCallGateStatus.InvalidCompositePreparation || PrepareCallGateForTest(fixture, prior, composite, observation, allocationCount: 0).Status != M01OwnedCr3CompositeCallGateStatus.InvalidAllocationRecordCount || PrepareCallGateForTest(fixture, prior, composite, observation, allocationCapacity: M01OwnedCr3CompositeTransition.MaximumAllocationRecordCount + 1).Status != M01OwnedCr3CompositeCallGateStatus.AllocationRecordCapacityMismatch || PrepareCallGateForTest(fixture, prior, composite, observation, reservationCount: 0).Status != M01OwnedCr3CompositeCallGateStatus.InvalidReservationRecordCount || PrepareCallGateForTest(fixture, prior, composite, observation, reservationCapacity: M01OwnedCr3CompositeTransition.MaximumReservationRecordCount + 1).Status != M01OwnedCr3CompositeCallGateStatus.ReservationRecordCapacityMismatch || PrepareCallGateForTest(fixture, prior, composite, observation, mappingCount: 0).Status != M01OwnedCr3CompositeCallGateStatus.InvalidIdentityMappingCount || PrepareCallGateForTest(fixture, prior, composite, observation, mappingCapacity: M01OwnedCr3CompositeTransition.MaximumIdentityMappingCount + 1).Status != M01OwnedCr3CompositeCallGateStatus.IdentityMappingCapacityMismatch || PrepareCallGateForTest(fixture, prior, composite, observation, bindingCount: 0).Status != M01OwnedCr3CompositeCallGateStatus.InvalidCodeBindingCount || PrepareCallGateForTest(fixture, prior, composite, observation, bindingCapacity: 2).Status != M01OwnedCr3CompositeCallGateStatus.CodeBindingCapacityMismatch || PrepareCallGateForTest(fixture, prior, composite, observation, observationCount: 0).Status != M01OwnedCr3CompositeCallGateStatus.InvalidObservationCount || PrepareCallGateForTest(fixture, prior, composite, observation, observationCapacity: 2).Status != M01OwnedCr3CompositeCallGateStatus.ObservationCapacityMismatch || !IsZeroCallGateObservation(observation))
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRangeBinding[] wrongBinding = [fixture.Bindings[1]];
        if (PrepareCallGateForTest(fixture, prior, composite, observation, wrongBinding).Status != M01OwnedCr3CompositeCallGateStatus.InvalidCodeBinding)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionResult wrongComposite = composite;
        OverwriteUlong(ref wrongComposite, checked((int)Offset<M01OwnedCr3CompositeTransitionResult>("_entryAddress")), composite.EntryAddress + 16);
        if (PrepareCallGateForTest(fixture, prior, wrongComposite, observation).Status != M01OwnedCr3CompositeCallGateStatus.InvalidCompositeEntry)
        {
            return false;
        }

        if (PrepareCallGateForTest(fixture, prior, composite, (M01OwnedCr3CompositeCallGateObservation*)((byte*)observation + 1), observationAddress: (ulong)observation + 1).Status != M01OwnedCr3CompositeCallGateStatus.MisalignedObservation || PrepareCallGateForTest(fixture, prior, composite, (M01OwnedCr3CompositeCallGateObservation*)0xffff_ffff_ffff_ff80UL, observationAddress: 0xffff_ffff_ffff_ff80UL).Status != M01OwnedCr3CompositeCallGateStatus.ObservationSpanOverflow || PrepareCallGateForTest(fixture, prior, composite, (M01OwnedCr3CompositeCallGateObservation*)fixture.StateDestination, observationAddress: (ulong)fixture.StateDestination).Status != M01OwnedCr3CompositeCallGateStatus.ObservationAlias || PrepareCallGateForTest(fixture, prior, composite, observation, observationAddress: (ulong)observation + 8).Status != M01OwnedCr3CompositeCallGateStatus.MisalignedObservation || PrepareCallGateForTest(fixture, prior, composite, observation, expectedBoundaryCanary: 0).Status != M01OwnedCr3CompositeCallGateStatus.BoundaryCanaryMismatch)
        {
            return false;
        }

        (ulong RflagsBit, M01OwnedCr3CompositeCallGateStatus Status)[] rflagsCases = [(M01OwnedCr3CompositeTransition.RflagsTrapFlagBit, M01OwnedCr3CompositeCallGateStatus.TrapFlagSet), (1UL << 10, M01OwnedCr3CompositeCallGateStatus.DirectionFlagSet)];
        foreach (var item in rflagsCases)
        {
            M01LivePagingPreparationResult corrupted = prior;
            SetPriorPreflightRflags(ref corrupted, preflight.Rflags | item.RflagsBit);
            ClearCallGateObservation(observation);
            if (PrepareCallGateForTest(fixture, corrupted, composite, observation).Status != item.Status || !IsZeroCallGateObservation(observation))
            {
                return false;
            }
        }

        M01LivePagingPreparationResult cetPrior = prior;
        SetPriorPreflightCr4(ref cetPrior, preflight.RawCr4 | M01OwnedCr3CompositeTransition.Cr4ControlFlowEnforcementBit);
        M01LivePagingPreparationResult la57Prior = prior;
        SetPriorPreflightCr4(ref la57Prior, preflight.RawCr4 | M01OwnedCr3CompositeTransition.Cr4FiveLevelPagingBit);
        M01LivePagingPreparationResult pcidePrior = prior;
        SetPriorPreflightCr4(ref pcidePrior, preflight.RawCr4 | M01OwnedCr3CompositeTransition.Cr4PcideBit);
        if (PrepareCallGateForTest(fixture, cetPrior, composite, observation).Status != M01OwnedCr3CompositeCallGateStatus.CetUnsupported // LA57 and PCIDE make the upstream evidence itself invalid;
        // the call-gate rejects that evidence before its narrower
        // diagnostic statuses can be reached.
        || PrepareCallGateForTest(fixture, la57Prior, composite, observation).Status != M01OwnedCr3CompositeCallGateStatus.InvalidPriorPlanningEvidence || PrepareCallGateForTest(fixture, pcidePrior, composite, observation).Status != M01OwnedCr3CompositeCallGateStatus.InvalidPriorPlanningEvidence)
        {
            return false;
        }

        M01LivePagingPreparationResult flippedIf = prior;
        SetPriorPreflightRflags(ref flippedIf, preflight.Rflags ^ M01OwnedCr3CompositeTransition.RflagsInterruptEnableBit);
        ClearCallGateObservation(observation);
        M01OwnedCr3CompositeCallGateResult ifPrepared = PrepareCallGateForTest(fixture, flippedIf, composite, observation);
        M01LivePagingPreparationResult flippedPge = prior;
        SetPriorPreflightCr4(ref flippedPge, preflight.RawCr4 ^ M01OwnedCr3CompositeTransition.Cr4PageGlobalEnableBit);
        ClearCallGateObservation(observation);
        M01OwnedCr3CompositeCallGateResult pgePrepared = PrepareCallGateForTest(fixture, flippedPge, composite, observation);
        return ifPrepared.IsPrepared && ifPrepared.Observation.ExpectedInterruptEnabled == ((flippedIf.Evidence.Preflight.Rflags & M01OwnedCr3CompositeTransition.RflagsInterruptEnableBit) != 0) && pgePrepared.IsPrepared && pgePrepared.Observation.ExpectedPgeWasSet == ((flippedPge.Evidence.Preflight.RawCr4 & M01OwnedCr3CompositeTransition.Cr4PageGlobalEnableBit) != 0);
    }

    private static M01OwnedCr3CompositeCallGateResult MaterializeCallGateForTest(M01OwnedCr3CompositeCallGateResult evidence, M01OwnedCr3CompositeTransitionRuntimeState* state, byte* bytes, M01OwnedCr3CompositeTransitionSemanticRecord* semantics, bool missingEvidence = false, ulong evidenceCount = 1, ulong evidenceCapacity = 1, bool missingState = false, ulong stateCount = 1, ulong stateCapacity = 1, ulong byteCount = M01OwnedCr3CompositeTransition.MaterializedPageByteLength, ulong byteCapacity = M01OwnedCr3CompositeTransition.MaterializedPageByteLength, ulong? codeBase = null, ulong semanticCount = M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount, ulong semanticCapacity = M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount)
    {
        return M01OwnedCr3CompositeTransition.TryMaterializeCallGate(missingEvidence ? null : &evidence, evidenceCount, evidenceCapacity, missingState ? null : state, stateCount, stateCapacity, bytes, byteCount, byteCapacity, codeBase ?? (ulong)bytes, semantics, semanticCount, semanticCapacity);
    }

    private static M01OwnedCr3CompositeCallGateResult ReadbackCallGateForTest(M01OwnedCr3CompositeCallGateResult evidence, M01OwnedCr3CompositeTransitionRuntimeState* state, byte* bytes, M01OwnedCr3CompositeTransitionSemanticRecord* semantics, bool missingEvidence = false, ulong evidenceCount = 1, ulong evidenceCapacity = 1, bool missingState = false, ulong stateCount = 1, ulong stateCapacity = 1, ulong byteCount = M01OwnedCr3CompositeTransition.MaterializedPageByteLength, ulong byteCapacity = M01OwnedCr3CompositeTransition.MaterializedPageByteLength, ulong? codeBase = null, ulong semanticCount = M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount, ulong semanticCapacity = M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount)
    {
        return M01OwnedCr3CompositeTransition.ValidateCallGateReadback(missingEvidence ? null : &evidence, evidenceCount, evidenceCapacity, missingState ? null : state, stateCount, stateCapacity, bytes, byteCount, byteCapacity, codeBase ?? (ulong)bytes, semantics, semanticCount, semanticCapacity);
    }

    private static bool TestM01032MaterializationReadbackAndAtomicity()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionResult composite = fixture.Execute();
        M01OwnedCr3CompositeCallGateObservation* observation = GetCallGateObservation(fixture);
        ClearCallGateObservation(observation);
        M01OwnedCr3CompositeCallGateResult preparation = PrepareCallGateForTest(fixture, fixture.PriorEvidence, composite, observation);
        if (!preparation.IsPrepared)
        {
            return false;
        }

        using CompositePlanStorage storage = new((byte*)preparation.CodeBase);
        storage.Fill(0xa5);
        byte[] bytesBefore = storage.CopyAllBytes();
        byte[] semanticsBefore = storage.CopyAllSemantics();
        M01OwnedCr3CompositeTransitionRuntimeState* state = fixture.StateDestination;
        (M01OwnedCr3CompositeCallGateStatus Status, Func<M01OwnedCr3CompositeCallGateResult> Invoke)[] invalidCalls = [(M01OwnedCr3CompositeCallGateStatus.MissingEvidence, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics, missingEvidence: true)), (M01OwnedCr3CompositeCallGateStatus.InvalidEvidence, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics, evidenceCount: 0)), (M01OwnedCr3CompositeCallGateStatus.MissingCompositePreparation, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics, missingState: true)), (M01OwnedCr3CompositeCallGateStatus.InvalidCompositePreparation, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics, stateCapacity: 2)), (M01OwnedCr3CompositeCallGateStatus.MissingCodeStorage, () => MaterializeCallGateForTest(preparation, state, null, storage.Semantics, codeBase: preparation.CodeBase)), (M01OwnedCr3CompositeCallGateStatus.InvalidCodeStorageCount, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics, byteCount: 4095)), (M01OwnedCr3CompositeCallGateStatus.CodeStorageCapacityMismatch, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics, byteCapacity: 4095)), (M01OwnedCr3CompositeCallGateStatus.MisalignedCodeStorage, () => MaterializeCallGateForTest(preparation, state, storage.Bytes + 1, storage.Semantics, codeBase: (ulong)storage.Bytes + 1)), (M01OwnedCr3CompositeCallGateStatus.CodeStorageSpanOverflow, () => MaterializeCallGateForTest(preparation, state, (byte*)0xffff_ffff_ffff_f000UL, storage.Semantics, codeBase: 0xffff_ffff_ffff_f000UL)), (M01OwnedCr3CompositeCallGateStatus.MissingSemanticStorage, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, null)), (M01OwnedCr3CompositeCallGateStatus.InvalidSemanticCount, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics, semanticCount: 15)), (M01OwnedCr3CompositeCallGateStatus.SemanticCapacityMismatch, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics, semanticCapacity: 17)), (M01OwnedCr3CompositeCallGateStatus.MisalignedSemanticStorage, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, (M01OwnedCr3CompositeTransitionSemanticRecord*)((byte*)storage.Semantics + 1))), (M01OwnedCr3CompositeCallGateStatus.SemanticSpanOverflow, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, (M01OwnedCr3CompositeTransitionSemanticRecord*)0xffff_ffff_ffff_fff8UL)), (M01OwnedCr3CompositeCallGateStatus.InvalidCodeBase, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics, codeBase: preparation.CodeBase + 4096)), (M01OwnedCr3CompositeCallGateStatus.StorageAlias, () => MaterializeCallGateForTest(preparation, state, storage.Bytes, (M01OwnedCr3CompositeTransitionSemanticRecord*)storage.Bytes))];
        foreach (var item in invalidCalls)
        {
            M01OwnedCr3CompositeCallGateResult result = item.Invoke();
            if (result.Status != item.Status || !storage.AllStorageEquals(bytesBefore, semanticsBefore))
            {
                Console.WriteLine("M01_032_STORAGE_ATOMICITY expected=" + item.Status + " observed=" + result.Status);
                return false;
            }
        }

        M01OwnedCr3CompositeCallGateResult materialized = MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics);
        if (!materialized.IsMaterialized || materialized.Status != M01OwnedCr3CompositeCallGateStatus.Success || materialized.FullPageByteCount != 4096 || materialized.InstructionCount != 806 || materialized.ExpectedValue != 806 || materialized.ObservedValue != 806 || materialized.Fnv1a64 != ExpectedCombinedFnv1a64 || !materialized.Observation.IsArmed || !storage.TailsEqual(0xa5))
        {
            return false;
        }

        ReadOnlySpan<byte> page = new(storage.Bytes, 4096);
        string prefixSha = Convert.ToHexString(SHA256.HashData(page[..checked((int)M01OwnedCr3CompositeTransition.RequiredByteLength)]));
        string gateSha = Convert.ToHexString(SHA256.HashData(page.Slice(checked((int)M01OwnedCr3CompositeTransition.CallGateOffset), checked((int)M01OwnedCr3CompositeTransition.CallGateByteLength))));
        string combinedSha = Convert.ToHexString(SHA256.HashData(page));
        if (ExpectedCallGateSha256.Length == 0 || ExpectedCombinedPageSha256.Length == 0)
        {
            Console.WriteLine("M01_032_HASH_DIAGNOSTIC gate=" + gateSha + " combined=" + combinedSha);
        }

        if (prefixSha != ExpectedInstructionPrefixSha256 || gateSha != ExpectedCallGateSha256 || combinedSha != ExpectedCombinedPageSha256 || ComputeFnv1a64(new ReadOnlySpan<byte>(storage.Bytes + M01OwnedCr3CompositeTransition.CallGateOffset, checked((int)M01OwnedCr3CompositeTransition.CallGateByteLength))) != ExpectedCallGateFnv1a64 || ComputeFnv1a64(new ReadOnlySpan<byte>(storage.Bytes, 4096)) != ExpectedCombinedFnv1a64)
        {
            return false;
        }

        for (ulong index = M01OwnedCr3CompositeTransition.RequiredByteLength; index < M01OwnedCr3CompositeTransition.CallGateOffset; index++)
        {
            if (storage.Bytes[index] != 0)
            {
                return false;
            }
        }

        for (ulong index = M01OwnedCr3CompositeTransition.CallGateOffset + M01OwnedCr3CompositeTransition.CallGateByteLength; index < 4096; index++)
        {
            if (storage.Bytes[index] != 0)
            {
                return false;
            }
        }

        byte[] canonicalBytes = storage.CopyBytes(4096);
        byte[] canonicalSemantics = storage.CopySemantics(M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        M01OwnedCr3CompositeCallGateResult readback = ReadbackCallGateForTest(materialized, state, storage.Bytes, storage.Semantics);
        if (!readback.IsReadbackVerified || readback.Fnv1a64 != ExpectedCombinedFnv1a64 || !storage.ExactPrefixesEqual(canonicalBytes, canonicalSemantics))
        {
            return false;
        }

        byte[] readbackBytesBefore = storage.CopyAllBytes();
        byte[] readbackSemanticsBefore = storage.CopyAllSemantics();
        (M01OwnedCr3CompositeCallGateStatus Status, Func<M01OwnedCr3CompositeCallGateResult> Invoke)[] invalidReadbacks = [(M01OwnedCr3CompositeCallGateStatus.MissingEvidence, () => ReadbackCallGateForTest(materialized, state, storage.Bytes, storage.Semantics, missingEvidence: true)), (M01OwnedCr3CompositeCallGateStatus.InvalidEvidence, () => ReadbackCallGateForTest(materialized, state, storage.Bytes, storage.Semantics, evidenceCapacity: 2)), (M01OwnedCr3CompositeCallGateStatus.MissingCompositePreparation, () => ReadbackCallGateForTest(materialized, state, storage.Bytes, storage.Semantics, missingState: true)), (M01OwnedCr3CompositeCallGateStatus.InvalidCodeStorageCount, () => ReadbackCallGateForTest(materialized, state, storage.Bytes, storage.Semantics, byteCount: 4095)), (M01OwnedCr3CompositeCallGateStatus.CodeStorageCapacityMismatch, () => ReadbackCallGateForTest(materialized, state, storage.Bytes, storage.Semantics, byteCapacity: 4095)), (M01OwnedCr3CompositeCallGateStatus.MisalignedCodeStorage, () => ReadbackCallGateForTest(materialized, state, storage.Bytes + 1, storage.Semantics, codeBase: (ulong)storage.Bytes + 1)), (M01OwnedCr3CompositeCallGateStatus.InvalidSemanticCount, () => ReadbackCallGateForTest(materialized, state, storage.Bytes, storage.Semantics, semanticCount: 15)), (M01OwnedCr3CompositeCallGateStatus.SemanticCapacityMismatch, () => ReadbackCallGateForTest(materialized, state, storage.Bytes, storage.Semantics, semanticCapacity: 17)), (M01OwnedCr3CompositeCallGateStatus.StorageAlias, () => ReadbackCallGateForTest(materialized, state, storage.Bytes, (M01OwnedCr3CompositeTransitionSemanticRecord*)storage.Bytes))];
        foreach (var item in invalidReadbacks)
        {
            M01OwnedCr3CompositeCallGateResult invalid = item.Invoke();
            if (invalid.Status != item.Status || !storage.AllStorageEquals(readbackBytesBefore, readbackSemanticsBefore))
            {
                return false;
            }
        }

        ulong[] corruptions = [0, M01OwnedCr3CompositeTransition.RequiredByteLength, M01OwnedCr3CompositeTransition.CallGateOffset, M01OwnedCr3CompositeTransition.CallInstructionOffset, 4095];
        foreach (ulong index in corruptions)
        {
            storage.Bytes[index] ^= 0x5a;
            M01OwnedCr3CompositeCallGateResult corrupted = ReadbackCallGateForTest(materialized, state, storage.Bytes, storage.Semantics);
            storage.Bytes[index] ^= 0x5a;
            if (corrupted.Status != M01OwnedCr3CompositeCallGateStatus.ByteMismatch || corrupted.InvalidIndex != index)
            {
                Console.WriteLine("M01_032_READBACK_CORRUPTION index=" + index + " observed=" + corrupted.Status + "/" + corrupted.InvalidIndex);
                return false;
            }
        }

        byte[] firstPage = storage.CopyBytes(4096);
        byte[] firstSemantics = storage.CopySemantics(M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        M01OwnedCr3CompositeCallGateResult repeated = MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics);
        return repeated.IsMaterialized && storage.ExactPrefixesEqual(firstPage, firstSemantics);
    }

    private static M01OwnedCr3CompositeCallGateResult AuditCallGateForTest(CompositeFixture fixture, M01OwnedCr3CompositeCallGateResult readback, M01OwnedCr3CompositeTransitionRuntimeState observedState, M01OwnedCr3CompositeCallGateObservation observedObservation, M01OwnedCr3CompositeTransitionTerminalKind terminalKind = M01OwnedCr3CompositeTransitionTerminalKind.Returned, uint observedRuntimeReturnValue = (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, bool missingEvidence = false, ulong evidenceCount = 1, ulong evidenceCapacity = 1, bool missingState = false, ulong stateCount = 1, ulong stateCapacity = 1, bool missingObservation = false, ulong observationCount = 1, ulong observationCapacity = 1)
    {
        M01OwnedCr3CompositeCallGateObservation* observation = GetCallGateObservation(fixture);
        *fixture.StateDestination = observedState;
        *observation = observedObservation;
        return M01OwnedCr3CompositeTransition.AuditCallGateObservation(missingEvidence ? null : &readback, evidenceCount, evidenceCapacity, missingState ? null : fixture.StateDestination, stateCount, stateCapacity, missingObservation ? null : observation, observationCount, observationCapacity, terminalKind, observedRuntimeReturnValue);
    }

    private static M01OwnedCr3CompositeCallGateObservation CreateCanonicalCallGateObservation(M01OwnedCr3CompositeCallGateObservation armed, M01OwnedCr3CompositeCallGateObservationLifecycle lifecycle = M01OwnedCr3CompositeCallGateObservationLifecycle.Completed, ulong? boundaryCanary = null, ulong? canaryBefore = null, ulong? postReturnRflags = null, ulong? postReturnCr3 = null, ulong? postReturnCr4 = null, ulong? canaryAfter = null, uint? compositeReturn = null, uint callCount = 1, uint observationCount = 1, uint? signature = null, ulong reserved = 0)
    {
        return M01OwnedCr3CompositeCallGateObservation.CreateObserved(armed, lifecycle, boundaryCanary ?? armed.ExpectedBoundaryCanary, canaryBefore ?? armed.ExpectedBoundaryCanary, postReturnRflags ?? armed.ExpectedOriginalRflags, postReturnCr3 ?? armed.ExpectedFirmwareCr3Raw, postReturnCr4 ?? armed.ExpectedFirmwareCr4Raw, canaryAfter ?? armed.ExpectedBoundaryCanary, compositeReturn ?? armed.ExpectedCompositeReturnValue, callCount, observationCount, signature ?? M01OwnedCr3CompositeCallGateObservation.CompletionSignature, reserved);
    }

    private static void SetCallGateObservationUlong(ref M01OwnedCr3CompositeCallGateObservation observation, uint byteOffset, ulong value)
    {
        if (byteOffset > M01OwnedCr3CompositeCallGateObservation.StructureSize - sizeof(ulong) || (byteOffset & (sizeof(ulong) - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        fixed (M01OwnedCr3CompositeCallGateObservation* pointer = &observation)
        {
            *(ulong*)((byte*)pointer + byteOffset) = value;
        }
    }

    private static void SetCallGateObservationByte(ref M01OwnedCr3CompositeCallGateObservation observation, uint byteOffset, byte value)
    {
        if (byteOffset >= M01OwnedCr3CompositeCallGateObservation.StructureSize)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        fixed (M01OwnedCr3CompositeCallGateObservation* pointer = &observation)
        {
            *((byte*)pointer + byteOffset) = value;
        }
    }

    private static bool TestM01032ObservationAuditAndRecovery()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionResult composite = fixture.Execute();
        M01OwnedCr3CompositeCallGateObservation* destination = GetCallGateObservation(fixture);
        ClearCallGateObservation(destination);
        M01OwnedCr3CompositeCallGateResult preparation = PrepareCallGateForTest(fixture, fixture.PriorEvidence, composite, destination);
        using CompositePlanStorage storage = new((byte*)preparation.CodeBase);
        storage.Fill(0);
        M01OwnedCr3CompositeTransitionRuntimeState* preparedState = fixture.StateDestination;
        M01OwnedCr3CompositeCallGateResult materialized = MaterializeCallGateForTest(preparation, preparedState, storage.Bytes, storage.Semantics);
        M01OwnedCr3CompositeCallGateResult readback = ReadbackCallGateForTest(materialized, preparedState, storage.Bytes, storage.Semantics);
        if (!readback.IsReadbackVerified)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState observedState = CreateCanonicalObservedState(fixture, *preparedState);
        M01OwnedCr3CompositeCallGateObservation canonical = CreateCanonicalCallGateObservation(readback.Observation);
        M01OwnedCr3CompositeCallGateResult success = AuditCallGateForTest(fixture, readback, observedState, canonical);
        if (!success.IsObservedAudit || success.Status != M01OwnedCr3CompositeCallGateStatus.Success || success.TerminalKind != M01OwnedCr3CompositeTransitionTerminalKind.Returned || success.Fnv1a64 != ExpectedCombinedFnv1a64 || !success.Observation.IsCompleted || success.Observation.CallCount != 1 || success.Observation.PostReturnObservationCount != 1 || success.Observation.CompositeReturnValue != (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success || success.Observation.PostReturnRflags != success.Observation.ExpectedOriginalRflags || success.Observation.PostReturnCr3Raw != success.Observation.ExpectedFirmwareCr3Raw || success.Observation.PostReturnCr4Raw != success.Observation.ExpectedFirmwareCr4Raw || success.Observation.CanaryBefore != ExpectedCallGateBoundaryCanary || success.Observation.CanaryAfter != ExpectedCallGateBoundaryCanary)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState impossibleUpperStackTranscript = observedState;
        ulong impossibleUpperCompositeRsp = observedState.StackEndExclusive - 72;
        OverwriteUlong(ref impossibleUpperStackTranscript, checked((int)M01OwnedCr3CompositeTransition.StateEntryRspOffset), impossibleUpperCompositeRsp);
        OverwriteUlong(ref impossibleUpperStackTranscript, checked((int)M01OwnedCr3CompositeTransition.StateExitRspOffset), impossibleUpperCompositeRsp);
        M01OwnedCr3CompositeTransitionRuntimeState impossibleLowerStackTranscript = observedState;
        ulong impossibleLowerCompositeRsp = observedState.StackStart + 72;
        OverwriteUlong(ref impossibleLowerStackTranscript, checked((int)M01OwnedCr3CompositeTransition.StateEntryRspOffset), impossibleLowerCompositeRsp);
        OverwriteUlong(ref impossibleLowerStackTranscript, checked((int)M01OwnedCr3CompositeTransition.StateExitRspOffset), impossibleLowerCompositeRsp);
        if (AuditCallGateForTest(fixture, readback, impossibleUpperStackTranscript, canonical).Status != M01OwnedCr3CompositeCallGateStatus.InvalidGateStackTranscript || AuditCallGateForTest(fixture, readback, impossibleLowerStackTranscript, canonical).Status != M01OwnedCr3CompositeCallGateStatus.InvalidGateStackTranscript)
        {
            return false;
        }

        (M01OwnedCr3CompositeTransitionTerminalKind Terminal, M01OwnedCr3CompositeCallGateStatus Status)[] terminals = [(M01OwnedCr3CompositeTransitionTerminalKind.Timeout, M01OwnedCr3CompositeCallGateStatus.Timeout), (M01OwnedCr3CompositeTransitionTerminalKind.Reset, M01OwnedCr3CompositeCallGateStatus.Reset), (M01OwnedCr3CompositeTransitionTerminalKind.TripleFault, M01OwnedCr3CompositeCallGateStatus.TripleFault)];
        foreach (var item in terminals)
        {
            M01OwnedCr3CompositeCallGateResult terminal = AuditCallGateForTest(fixture, readback, observedState, canonical, item.Terminal);
            if (terminal.Status != item.Status || terminal.TerminalKind != item.Terminal)
            {
                return false;
            }
        }

        if (AuditCallGateForTest(fixture, readback, observedState, canonical, M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized).Status != M01OwnedCr3CompositeCallGateStatus.InvalidTerminalKind || AuditCallGateForTest(fixture, readback, observedState, canonical, missingEvidence: true).Status != M01OwnedCr3CompositeCallGateStatus.MissingEvidence || AuditCallGateForTest(fixture, readback, observedState, canonical, evidenceCapacity: 2).Status != M01OwnedCr3CompositeCallGateStatus.InvalidEvidence || AuditCallGateForTest(fixture, readback, observedState, canonical, missingState: true).Status != M01OwnedCr3CompositeCallGateStatus.MissingCompositePreparation || AuditCallGateForTest(fixture, readback, observedState, canonical, stateCount: 0).Status != M01OwnedCr3CompositeCallGateStatus.InvalidCompositePreparation || AuditCallGateForTest(fixture, readback, observedState, canonical, missingObservation: true).Status != M01OwnedCr3CompositeCallGateStatus.MissingObservation || AuditCallGateForTest(fixture, readback, observedState, canonical, observationCount: 0).Status != M01OwnedCr3CompositeCallGateStatus.InvalidObservationCount || AuditCallGateForTest(fixture, readback, observedState, canonical, observationCapacity: 2).Status != M01OwnedCr3CompositeCallGateStatus.ObservationCapacityMismatch)
        {
            return false;
        }

        M01OwnedCr3CompositeCallGateObservation forgedExpectedReturn = canonical;
        SetCallGateObservationUlong(ref forgedExpectedReturn, M01OwnedCr3CompositeTransition.CallGateObservationExpectedReturnSiteAddressOffset, canonical.ExpectedReturnSiteAddress + 8);
        M01OwnedCr3CompositeCallGateObservation forgedInterruptPolicy = canonical;
        SetCallGateObservationByte(ref forgedInterruptPolicy, M01OwnedCr3CompositeTransition.CallGateObservationInterruptPolicyOffset, 0);
        M01OwnedCr3CompositeCallGateObservation forgedPgePolicy = canonical;
        SetCallGateObservationByte(ref forgedPgePolicy, M01OwnedCr3CompositeTransition.CallGateObservationPgePolicyOffset, 0);
        M01OwnedCr3CompositeCallGateObservation forgedIfExpectation = canonical;
        SetCallGateObservationByte(ref forgedIfExpectation, M01OwnedCr3CompositeTransition.CallGateObservationExpectedInterruptEnabledOffset, canonical.ExpectedInterruptEnabled ? (byte)0 : (byte)1);
        M01OwnedCr3CompositeCallGateObservation forgedPgeExpectation = canonical;
        SetCallGateObservationByte(ref forgedPgeExpectation, M01OwnedCr3CompositeTransition.CallGateObservationExpectedPgeWasSetOffset, canonical.ExpectedPgeWasSet ? (byte)0 : (byte)1);
        (M01OwnedCr3CompositeCallGateObservation Observation, uint ReturnValue, M01OwnedCr3CompositeCallGateStatus Status)[] corruptions = [(forgedExpectedReturn, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.InvalidEvidence), (forgedInterruptPolicy, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.InvalidEvidence), (forgedPgePolicy, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.InvalidEvidence), (forgedIfExpectation, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.InvalidEvidence), (forgedPgeExpectation, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.InvalidEvidence), (CreateCanonicalCallGateObservation(readback.Observation, signature: 0), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.CompletionSignatureMismatch), (CreateCanonicalCallGateObservation(readback.Observation, callCount: 0), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.CallCountMismatch), (CreateCanonicalCallGateObservation(readback.Observation, observationCount: 0), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.ObservationCountMismatch), (CreateCanonicalCallGateObservation(readback.Observation, compositeReturn: 7), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.CompositeReturnMismatch), (CreateCanonicalCallGateObservation(readback.Observation, postReturnRflags: readback.Observation.ExpectedOriginalRflags ^ RflagsArithmeticMask), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.PostReturnRflagsMismatch), (CreateCanonicalCallGateObservation(readback.Observation, postReturnCr3: readback.Observation.ExpectedFirmwareCr3Raw + 4096), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.FirmwareCr3Mismatch), (CreateCanonicalCallGateObservation(readback.Observation, postReturnCr4: readback.Observation.ExpectedFirmwareCr4Raw ^ M01OwnedCr3CompositeTransition.Cr4PageGlobalEnableBit), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.FirmwareCr4Mismatch), (CreateCanonicalCallGateObservation(readback.Observation, canaryAfter: ExpectedCallGateBoundaryCanary ^ 1), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.BoundaryCanaryMismatch), (CreateCanonicalCallGateObservation(readback.Observation, M01OwnedCr3CompositeCallGateObservationLifecycle.InProgress), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.InvalidEvidence), // Reserved output corruption invalidates the canonical header
        // before the narrower terminal reserved-state diagnostic.
        (CreateCanonicalCallGateObservation(readback.Observation, reserved: 1), (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeCallGateStatus.InvalidEvidence)];
        foreach (var item in corruptions)
        {
            M01OwnedCr3CompositeCallGateResult rejected = AuditCallGateForTest(fixture, readback, observedState, item.Observation, observedRuntimeReturnValue: item.ReturnValue);
            if (rejected.Status != item.Status)
            {
                Console.WriteLine("M01_032_OBSERVATION_DIAGNOSTIC expected=" + item.Status + " observed=" + rejected.Status);
                return false;
            }
        }

        M01OwnedCr3CompositeTransitionRuntimeState corruptedState = observedState;
        SetStateUInt32(ref corruptedState, M01OwnedCr3CompositeTransition.StateInvlpgCountOffset, 2);
        if (AuditCallGateForTest(fixture, readback, corruptedState, canonical).Status != M01OwnedCr3CompositeCallGateStatus.CompositeStateAuditFailed)
        {
            return false;
        }

        M01OwnedCr3CompositeCallGateResult repeatedAudit = AuditCallGateForTest(fixture, readback, observedState, canonical);
        return repeatedAudit.IsObservedAudit && repeatedAudit.Status == M01OwnedCr3CompositeCallGateStatus.Success;
    }

    private static bool TestM01032MarkerSourceAndNoStdLibBoundary()
    {
        HostCheck.SingleFixture(typeof(M01OwnedCr3CompositeTransitionTests), nameof(RunAll));
        string root = HostToolProcess.FindRepositoryRoot();
        string ownerPath = Path.Combine(root, "tests", "OS.HostTests", "M01OwnedCr3CompositeTransitionTests.cs");
        string productionPath = Path.Combine(root, "src", "OS.Kernel", "Boot", "M01OwnedCr3CompositeTransition.cs");
        if (!File.Exists(ownerPath) || !File.Exists(productionPath))
        {
            return false;
        }

        string owner = File.ReadAllText(ownerPath);
        string production = RemoveComments(File.ReadAllText(productionPath));
        int runner = owner.IndexOf("private static void RunCallGateContractTests()", StringComparison.Ordinal);
        int start = owner.IndexOf("\"TEST_START \" + CallGateIssueTestName", runner, StringComparison.Ordinal);
        int failureGate = owner.IndexOf("if (failures != 0)", runner, StringComparison.Ordinal);
        int failure = owner.IndexOf("\"TEST_FAIL \" + CallGateIssueTestName", runner, StringComparison.Ordinal);
        int ready = owner.IndexOf("Console.WriteLine(CallGateReadyMarker)", runner, StringComparison.Ordinal);
        int pass = owner.IndexOf("\"TEST_PASS \" + CallGateIssueTestName", runner, StringComparison.Ordinal);
        if (runner < 0 || start < runner || failureGate <= start || failure <= failureGate || ready <= failure || pass <= ready || CountOccurrences(owner, "\"" + CallGateReadyMarker + "\"") != 1 || CountOccurrences(owner, "\"TEST_START \" + CallGateIssueTestName") != 1 || CountOccurrences(owner, "\"TEST_FAIL \" + CallGateIssueTestName") != 1 || CountOccurrences(owner, "\"TEST_PASS \" + CallGateIssueTestName") != 1 || production.Contains(CallGateReadyMarker, StringComparison.Ordinal) || production.Contains("OS_" + "BOOT_OK", StringComparison.Ordinal) || production.Contains("OS_" + "SMOKE_PASS", StringComparison.Ordinal) || production.Contains("OS_" + "SMOKE_FAIL", StringComparison.Ordinal) || production.Contains("RuntimeExport", StringComparison.Ordinal) || production.Contains("EfiEntry", StringComparison.Ordinal) || !production.Contains("public const bool CallGateExecutesGeneratedCode" + " = false;", StringComparison.Ordinal) || !production.Contains("public const bool CallGateProvidesGuestEvidence" + " = false;", StringComparison.Ordinal) || !production.Contains("public const bool" + " CallGatePerformsLiveControlRegisterRead" + " = false;", StringComparison.Ordinal) || production.Contains("public const bool CallGateExecutesGeneratedCode" + " = true;", StringComparison.Ordinal) || production.Contains("public const bool CallGateProvidesGuestEvidence" + " = true;", StringComparison.Ordinal))
        {
            Console.WriteLine("M01_032_MARKER_DIAGNOSTIC ownership indexes=" + runner + "," + start + "," + failureGate + "," + failure + "," + ready + "," + pass);
            return false;
        }

        string[] roots = ["src", "std", "apps", "tools", "tests", "scripts", Path.Combine(".gitea", "workflows")];
        string ownerFullPath = Path.GetFullPath(ownerPath);
        foreach (string relativeRoot in roots)
        {
            string scanRoot = Path.Combine(root, relativeRoot);
            if (!Directory.Exists(scanRoot))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(scanRoot, "*", SearchOption.AllDirectories))
            {
                string fullPath = Path.GetFullPath(path);
                string extension = Path.GetExtension(path);
                if (fullPath.Equals(ownerFullPath, StringComparison.OrdinalIgnoreCase) || HasPathSegment(path, "bin") || HasPathSegment(path, "obj") || HasPathSegment(path, "artifacts") || extension is not (".cs" or ".ps1" or ".yml" or ".yaml" or ".props" or ".targets" or ".csproj" or ".slnx"))
                {
                    continue;
                }

                if (File.ReadAllText(path).Contains(CallGateReadyMarker, StringComparison.Ordinal))
                {
                    Console.WriteLine("M01_032_MARKER_DIAGNOSTIC duplicate=" + Path.GetRelativePath(root, path));
                    return false;
                }
            }
        }

        return TestSourceAndMarkerFirewall() && TestNoStdLibEfiCompileClosure();
    }

    private static bool TestM01032IndependentCallGateDecode()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionResult composite = fixture.Execute();
        M01OwnedCr3CompositeCallGateObservation* observation = GetCallGateObservation(fixture);
        ClearCallGateObservation(observation);
        M01OwnedCr3CompositeCallGateResult preparation = PrepareCallGateForTest(fixture, fixture.PriorEvidence, composite, observation);
        if (!preparation.IsPrepared)
        {
            return false;
        }

        using CompositePlanStorage storage = new((byte*)preparation.CodeBase);
        storage.Fill(0);
        M01OwnedCr3CompositeTransitionRuntimeState* state = fixture.StateDestination;
        M01OwnedCr3CompositeCallGateResult materialized = MaterializeCallGateForTest(preparation, state, storage.Bytes, storage.Semantics);
        if (!materialized.IsMaterialized)
        {
            return false;
        }

        byte[] gate = new ReadOnlySpan<byte>(storage.Bytes + M01OwnedCr3CompositeTransition.CallGateOffset, checked((int)M01OwnedCr3CompositeTransition.CallGateByteLength)).ToArray();
        if (!TryDecodeComposite(gate, out var instructions) || instructions.Count != checked((int)M01OwnedCr3CompositeTransition.CallGateInstructionCount) || instructions.Sum(item => item.Length) != gate.Length)
        {
            Console.WriteLine("M01_032_GATE_DECODE count=" + instructions.Count + " bytes=" + gate.Length);
            return false;
        }

        int firstPush = instructions.FindIndex(item => item.Kind == CompositeInstructionKind.Push);
        int firstWrite = instructions.FindIndex(IsCallGateMemoryWrite);
        int callIndex = instructions.FindIndex(item => item.Kind == CompositeInstructionKind.DirectCall);
        if (firstPush <= 0 || firstWrite <= firstPush || callIndex <= firstWrite || instructions[firstPush].Offset != 0xa9 || instructions.Take(firstPush).Any(item => item.Kind is CompositeInstructionKind.Push or CompositeInstructionKind.PushMemory or CompositeInstructionKind.DirectCall or CompositeInstructionKind.IndirectCall or CompositeInstructionKind.OtherIndirectControl || IsCallGateMemoryWrite(item)))
        {
            return false;
        }

        List<CompositeDecodedInstruction> guardBranches = instructions.Take(firstPush).Where(item => item.Kind == CompositeInstructionKind.ConditionalBranch).ToList();
        int[] rejectTargets = guardBranches.Select(item => item.BranchTarget ?? int.MinValue).Distinct().ToArray();
        if (guardBranches.Count < 16 || rejectTargets.Length != 1 || rejectTargets[0] != 0x5d || !instructions.Any(item => item.Offset == 0x62) || !RejectPathHasNoWriteOrCall(instructions, rejectTargets[0]))
        {
            return false;
        }

        (int BaseRegister, uint Offset)[] stateGuards = [(1, M01OwnedCr3CompositeTransition.StateStructureSizeOffset), (1, M01OwnedCr3CompositeTransition.StateExpectedSelfAddressOffset), (1, M01OwnedCr3CompositeTransition.StateStackStartOffset), (1, M01OwnedCr3CompositeTransition.StateStackEndOffset), (1, M01OwnedCr3CompositeTransition.StateTableStateArenaStartOffset), (1, M01OwnedCr3CompositeTransition.StateExpectedReturnAddressOffset)];
        (int BaseRegister, uint Offset)[] observationGuards = [(2, M01OwnedCr3CompositeTransition.CallGateObservationStructureSizeOffset), (2, M01OwnedCr3CompositeTransition.CallGateObservationExpectedSelfAddressOffset)];
        if (!stateGuards.Concat(observationGuards).All(required => HasPrePushGuardedMemoryRead(instructions, firstPush, required.BaseRegister, required.Offset)) || !HasPrePushRegisterGuard(instructions, firstPush, 1) || !HasPrePushRegisterGuard(instructions, firstPush, 2))
        {
            return false;
        }

        ulong[] guardImmediates = instructions.Take(firstPush).Select(item => item.Immediate).ToArray();
        bool hasRspGuard = instructions.Take(firstPush).Any(item => (item.RegisterMask & (1UL << 4)) != 0);
        bool hasLowerHeadroomProof = guardImmediates.Contains(M01OwnedCr3CompositeTransition.CallGateRequiredLowerHeadroomBytes) || instructions.Take(firstPush).Any(item => item.Opcode == 0xc1 && ((item.ModRm >> 3) & 7) == 5 && item.Immediate == 7);
        if (!hasRspGuard || !hasLowerHeadroomProof || !guardImmediates.Contains(M01OwnedCr3CompositeTransition.CallGateRequiredUpperFrameBytes) || !guardImmediates.Contains(15UL) || !CallGateStackWindowAccepts(0x1000, 0x3000, 0x1000 + 136) || CallGateStackWindowAccepts(0x1000, 0x3000, 0x1000 + 120) || CallGateStackWindowAccepts(0x1000, 0x3000, 0x3000 - 24) || CallGateStackWindowAccepts(0x1000, 0x3000, 0x1000 + 144) || CallGateStackWindowAccepts(0x3000, 0x1000, 0x2008) || CallGateStackWindowAccepts(0, ulong.MaxValue, ulong.MaxValue - 31))
        {
            return false;
        }

        var preCallIncrements = instructions.Select((instruction, index) => (instruction, index)).Where(item => item.instruction.Opcode == 0xfe && ((item.instruction.ModRm >> 3) & 7) == 0).ToArray();
        uint[] incrementedObservationOffsets = preCallIncrements.Select(item => NormalizeCallGateObservationOffset(item.instruction.MemoryDisplacement ?? uint.MaxValue)).ToArray();
        if (preCallIncrements.Length != 2 || preCallIncrements.Any(item => item.index >= callIndex || item.instruction.MemoryBaseRegister != 6) || !incrementedObservationOffsets.SequenceEqual([M01OwnedCr3CompositeTransition.CallGateObservationLifecycleOffset, M01OwnedCr3CompositeTransition.CallGateObservationCallCountOffset]) || !gate.AsSpan(0xae, 4).SequenceEqual(new byte[] { 0x48, 0x8d, 0x72, 0x7f }) || !gate.AsSpan(0xc0, 9).SequenceEqual(new byte[] { 0xff, 0x72, 0x30, 0x9d, 0xe8, 0x37, 0xf0, 0xff, 0xff }))
        {
            return false;
        }

        if (instructions.Count(item => item.Kind == CompositeInstructionKind.DirectCall) != 1 || instructions.Any(item => item.Kind is CompositeInstructionKind.IndirectCall or CompositeInstructionKind.OtherIndirectControl) || instructions[callIndex].Offset + checked((int)M01OwnedCr3CompositeTransition.CallGateOffset) != checked((int)M01OwnedCr3CompositeTransition.CallInstructionOffset) || instructions[callIndex].BranchTarget != -checked((int)M01OwnedCr3CompositeTransition.CallGateOffset) || callIndex + 1 >= instructions.Count || callIndex < 2 || instructions[callIndex - 2].Kind != CompositeInstructionKind.PushMemory || instructions[callIndex - 2].MemoryBaseRegister != 2 || instructions[callIndex - 2].MemoryDisplacement != M01OwnedCr3CompositeTransition.CallGateObservationExpectedOriginalRflagsOffset || instructions[callIndex - 1].Kind != CompositeInstructionKind.Popfq || instructions[callIndex + 1].Offset + checked((int)M01OwnedCr3CompositeTransition.CallGateOffset) != checked((int)M01OwnedCr3CompositeTransition.CallNextReturnSiteOffset) || instructions[callIndex + 1].Kind != CompositeInstructionKind.Pushfq)
        {
            return false;
        }

        int successRet = instructions.FindIndex(callIndex + 1, item => item.Kind == CompositeInstructionKind.Ret);
        if (successRet <= callIndex || instructions.Skip(callIndex + 1).Take(successRet - callIndex - 1).Any(item => item.DestinationRegister == 0) || instructions.Count(item => item.Kind == CompositeInstructionKind.ReadCr3) != 1 || instructions.Count(item => item.Kind == CompositeInstructionKind.ReadCr4) != 1 || instructions.Any(item => item.Kind is CompositeInstructionKind.WriteCr3 or CompositeInstructionKind.WriteCr4 or CompositeInstructionKind.OtherControlRegister))
        {
            return false;
        }

        List<uint> observationWrites = instructions.Skip(firstWrite).Take(successRet - firstWrite).Where(item => IsCallGateMemoryWrite(item) && item.MemoryBaseRegister == 6 && item.MemoryDisplacement.HasValue).Select(item => NormalizeCallGateObservationOffset(item.MemoryDisplacement!.Value)).ToList();
        uint[] expectedWrites = [M01OwnedCr3CompositeTransition.CallGateObservationLifecycleOffset, M01OwnedCr3CompositeTransition.CallGateObservationCanaryBeforeOffset, M01OwnedCr3CompositeTransition.CallGateObservationCallCountOffset, M01OwnedCr3CompositeTransition.CallGateObservationPostReturnRflagsOffset, M01OwnedCr3CompositeTransition.CallGateObservationPostReturnCr3Offset, M01OwnedCr3CompositeTransition.CallGateObservationPostReturnCr4Offset, M01OwnedCr3CompositeTransition.CallGateObservationCompositeReturnOffset, M01OwnedCr3CompositeTransition.CallGateObservationCanaryAfterOffset, M01OwnedCr3CompositeTransition.CallGateObservationPostReturnCountOffset, M01OwnedCr3CompositeTransition.CallGateObservationLifecycleOffset, M01OwnedCr3CompositeTransition.CallGateObservationCompletionSignatureOffset];
        if (!observationWrites.SequenceEqual(expectedWrites) || observationWrites[^1] != M01OwnedCr3CompositeTransition.CallGateObservationCompletionSignatureOffset)
        {
            return false;
        }

        CompositeDecodedInstruction[] postReturnByteMoves = instructions.Skip(callIndex + 1).Take(successRet - callIndex - 1).Where(item => item.Opcode == 0xc6 && ((item.ModRm >> 3) & 7) == 0 && item.MemoryBaseRegister == 6).ToArray();
        if (postReturnByteMoves.Length != 2 || NormalizeCallGateObservationOffset(postReturnByteMoves[0].MemoryDisplacement ?? uint.MaxValue) != M01OwnedCr3CompositeTransition.CallGateObservationPostReturnCountOffset || postReturnByteMoves[0].Immediate != M01OwnedCr3CompositeTransition.CallGateExactPostReturnObservationCount || NormalizeCallGateObservationOffset(postReturnByteMoves[1].MemoryDisplacement ?? uint.MaxValue) != M01OwnedCr3CompositeTransition.CallGateObservationLifecycleOffset || postReturnByteMoves[1].Immediate != (byte)M01OwnedCr3CompositeCallGateObservationLifecycle.Completed)
        {
            return false;
        }

        int[] nonvolatileRegisters = [3, 5, 6, 7, 12, 13, 14, 15];
        foreach (int register in nonvolatileRegisters)
        {
            int pushes = instructions.Take(successRet).Count(item => item.Kind == CompositeInstructionKind.Push && item.Register == register);
            int pops = instructions.Take(successRet).Count(item => item.Kind == CompositeInstructionKind.Pop && item.Register == register);
            if (pushes != pops)
            {
                return false;
            }
        }

        int stackDepth = 0;
        int stackReservation = 0;
        for (int index = firstPush; index < callIndex; index++)
        {
            CompositeDecodedInstruction instruction = instructions[index];
            if (instruction.Kind == CompositeInstructionKind.Push)
            {
                stackDepth += 8;
            }
            else if (instruction.Kind == CompositeInstructionKind.PushMemory)
            {
                stackDepth += 8;
            }
            else if (instruction.Kind == CompositeInstructionKind.SubRsp)
            {
                stackDepth += checked((int)instruction.Immediate);
                stackReservation = checked((int)instruction.Immediate);
            }
            else if (instruction.Kind == CompositeInstructionKind.Pop)
            {
                stackDepth -= 8;
            }
            else if (instruction.Kind == CompositeInstructionKind.Popfq)
            {
                stackDepth -= 8;
            }
            else if (instruction.Kind == CompositeInstructionKind.AddRsp)
            {
                stackDepth -= checked((int)instruction.Immediate);
            }
        }

        int preCallRspMod16 = (8 - stackDepth) & 15;
        return stackReservation >= 32 && preCallRspMod16 == 0 && stackDepth + sizeof(ulong) == checked((int)M01OwnedCr3CompositeTransition.GateToCompositeEntryRspDelta) && instructions[successRet].Offset == 0xff && instructions[successRet].Kind == CompositeInstructionKind.Ret;
    }

    private static bool HasPrePushGuardedMemoryRead(List<CompositeDecodedInstruction> instructions, int firstPush, int baseRegister, uint displacement)
    {
        for (int index = 0; index < firstPush; index++)
        {
            CompositeDecodedInstruction instruction = instructions[index];
            if (instruction.MemoryBaseRegister != baseRegister || instruction.MemoryDisplacement != displacement || IsCallGateMemoryWrite(instruction))
            {
                continue;
            }

            int end = Math.Min(firstPush, index + 5);
            for (int candidate = index + 1; candidate < end; candidate++)
            {
                if (instructions[candidate].Kind == CompositeInstructionKind.ConditionalBranch)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasPrePushRegisterGuard(List<CompositeDecodedInstruction> instructions, int firstPush, int register)
    {
        for (int index = 0; index + 1 < firstPush; index++)
        {
            if ((instructions[index].RegisterMask & (1UL << register)) != 0 && instructions[index + 1].Kind == CompositeInstructionKind.ConditionalBranch)
            {
                return true;
            }
        }

        return false;
    }

    private static bool RejectPathHasNoWriteOrCall(List<CompositeDecodedInstruction> instructions, int rejectOffset)
    {
        int index = instructions.FindIndex(item => item.Offset == rejectOffset);
        if (index < 0)
        {
            return false;
        }

        for (; index < instructions.Count; index++)
        {
            CompositeDecodedInstruction instruction = instructions[index];
            if (instruction.Kind is CompositeInstructionKind.DirectCall or CompositeInstructionKind.IndirectCall or CompositeInstructionKind.OtherIndirectControl or CompositeInstructionKind.Push or CompositeInstructionKind.PushMemory || IsCallGateMemoryWrite(instruction))
            {
                return false;
            }

            if (instruction.Kind == CompositeInstructionKind.Ret)
            {
                return true;
            }

            if (instruction.Kind is CompositeInstructionKind.Jump or CompositeInstructionKind.ConditionalBranch)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsCallGateMemoryWrite(CompositeDecodedInstruction instruction)
    {
        if (instruction.MemoryBaseRegister < 0)
        {
            return false;
        }

        return instruction.Opcode is 0x01 or 0x09 or 0x21 or 0x88 or 0x89 or 0xc6 or 0xc7 or 0xfe;
    }

    private static uint NormalizeCallGateObservationOffset(uint rsiDisplacement)
    {
        int observationBias = checked((int)M01OwnedCr3CompositeTransition.CallGateObservationPointerBias);
        int signedDisplacement = unchecked((int)rsiDisplacement);
        return checked((uint)(observationBias + signedDisplacement));
    }

    private static bool CallGateStackWindowAccepts(ulong stackStart, ulong stackEndExclusive, ulong entryRsp)
    {
        const ulong LowerHeadroom = 128;
        const ulong UpperHeadroom = 40;
        return stackStart < stackEndExclusive && (entryRsp & 15) == 8 && entryRsp >= LowerHeadroom && entryRsp - LowerHeadroom >= stackStart && entryRsp <= ~0UL - UpperHeadroom && entryRsp + UpperHeadroom <= stackEndExclusive;
    }

    private static bool TestStableSurfaceAndDefaults()
    {
        (string Name, byte Value)[] statuses = [("Uninitialized", 0), ("Success", 1), ("MissingDestination", 2), ("DestinationCapacityTooSmall", 3), ("CapacityExceedsLimit", 4), ("MisalignedDestination", 5), ("InvalidEntryAddress", 6), ("MisalignedEntryAddress", 7), ("EntryAddressOverflow", 8), ("MissingSemanticDestination", 9), ("SemanticCapacityTooSmall", 10), ("MisalignedSemanticDestination", 11), ("DestinationSemanticOverlap", 12), ("InternalEncodingFailure", 13), ("MissingSource", 14), ("InvalidSourceCount", 15), ("SourceCapacityTooSmall", 16), ("MisalignedSource", 17), ("SourceSpanOverflow", 18), ("ByteMismatch", 19), ("MissingSemanticSource", 20), ("InvalidSemanticCount", 21), ("SemanticSourceCapacityTooSmall", 22), ("MisalignedSemanticSource", 23), ("SemanticSpanOverflow", 24), ("SemanticMismatch", 25), ("MissingAllocationRecords", 26), ("InvalidAllocationRecordCount", 27), ("AllocationRecordCapacityTooSmall", 28), ("MissingReservationRecords", 29), ("InvalidReservationRecordCount", 30), ("ReservationRecordCapacityTooSmall", 31), ("MissingIdentityMappings", 32), ("InvalidIdentityMappingCount", 33), ("IdentityMappingCapacityTooSmall", 34), ("MissingRangeBindings", 35), ("InvalidRangeBindingCount", 36), ("RangeBindingCapacityTooSmall", 37), ("MisalignedInputTable", 38), ("InputTableSpanOverflow", 39), ("InputOutputAlias", 40), ("InvalidRangeBinding", 41), ("DuplicateRangeRole", 42), ("MissingRangeRole", 43), ("AllocationProvenanceMismatch", 44), ("ReservationProvenanceMismatch", 45), ("IdentityMappingProvenanceMismatch", 46), ("InvalidProtection", 47), ("WritableExecutableRejected", 48), ("InvalidOwnedCr3", 49), ("InvalidProbePte", 50), ("InvalidCanary", 51), ("InvalidProbeContract", 52), ("MissingObservedState", 53), ("InvalidObservedStateCount", 54), ("ObservedStateCapacityTooSmall", 55), ("MisalignedObservedState", 56), ("InvalidTerminalKind", 57), ("Timeout", 58), ("Reset", 59), ("TripleFault", 60), ("DefaultRuntimeResult", 61), ("RuntimeReportedFailure", 62), ("ModePrerequisiteMismatch", 63), ("FirmwareCallbackObserved", 64), ("InterruptStateMismatch", 65), ("RawCr3BitLoss", 66), ("RawCr4BitLoss", 67), ("StackEscape", 68), ("ReturnAddressMismatch", 69), ("CanaryMismatch", 70), ("ProbePteAddressMismatch", 71), ("ProbePteReadbackMismatch", 72), ("InvlpgMismatch", 73), ("ProbeMismatch", 74), ("PageZeroPresent", 75), ("PageZeroMalformed", 76), ("CheckpointMismatch", 77), ("FirmwareCr3ReadbackMismatch", 78), ("Cr4RestoreReadbackMismatch", 79), ("RflagsRestoreReadbackMismatch", 80), ("SeparateLeavesNotCompositeEvidence", 81), ("ReservedStateNonzero", 82), ("MissingPageImages", 83), ("InvalidPageImageCount", 84), ("PageImageCapacityTooSmall", 85), ("MissingPageEntries", 86), ("InvalidPageEntryCount", 87), ("PageEntryCapacityTooSmall", 88), ("InvalidPageImage", 89), ("PageImageEntryMismatch", 90), ("MissingStateDestination", 91), ("InvalidStateDestinationCount", 92), ("StateDestinationCapacityTooSmall", 93), ("MisalignedStateDestination", 94), ("StateDestinationSpanOverflow", 95), ("StateAddressMismatch", 96), ("MissingPriorPlanningEvidence", 97), ("InvalidPriorPlanningEvidence", 98), ("MissingCanaryDestination", 99), ("InvalidCanaryDestinationCount", 100), ("CanaryDestinationCapacityTooSmall", 101), ("MisalignedCanaryDestination", 102), ("CanaryDestinationSpanOverflow", 103), ("RflagsRestoreIntentMismatch", 104)];
        (string Name, byte Value)[] runtimeStatuses = [("Uninitialized", 0), ("Success", 1), ("InvalidState", 2), ("FirmwareCallbackForbidden", 3), ("InvalidCodeRange", 4), ("InvalidStackRange", 5), ("InvalidReturnAddress", 6), ("InvalidTableArena", 7), ("InvalidOwnedCr3", 8), ("InvalidProbeContract", 9), ("InvalidCanary", 10), ("InvalidMode", 11), ("PhysicalAddressWidthUnsupported", 12), ("InterruptDisableReadbackMismatch", 13), ("PgeClearReadbackMismatch", 14), ("OwnedCr3ReadbackMismatch", 15), ("CanaryBeforeMismatch", 16), ("CanaryAfterMismatch", 17), ("ProbeHierarchyMalformed", 18), ("ProbePteAddressMismatch", 19), ("ProbePteBeforeMismatch", 20), ("ProbePteAfterMismatch", 21), ("ProbeReturnMismatch", 22), ("PageZeroPresent", 23), ("PageZeroMalformed", 24), ("FirmwareCr3ReadbackMismatch", 25), ("Cr4RestoreReadbackMismatch", 26), ("RflagsRestoreReadbackMismatch", 27), ("RspRestoreMismatch", 28), ("PostRestoreCanaryMismatch", 29)];
        (string Name, byte Value)[] checkpoints = [("Uninitialized", 0), ("EntryValidated", 1), ("ModeValidated", 2), ("InterruptsDisabled", 3), ("PgeCleared", 4), ("OwnedCr3Loaded", 5), ("CanaryUpdated", 6), ("ProbePteLocated", 7), ("ProbePteCommitted", 8), ("ProbeInvalidated", 9), ("ProbePteVerified", 10), ("ProbeReturned", 11), ("PageZeroAbsent", 12), ("FirmwareCr3Restored", 13), ("Cr4Restored", 14), ("RflagsRestored", 15), ("Returned", 16), ("ReturnPrepared", 17)];
        (string Name, byte Value)[] terminals = [("Uninitialized", 0), ("Returned", 1), ("Timeout", 2), ("Reset", 3), ("TripleFault", 4)];
        (string Name, byte Value)[] phases = [("Uninitialized", 0), ("StatePrepared", 1), ("Materialized", 2), ("ReadbackVerified", 3), ("ObservedAudit", 4)];
        (string Name, byte Value)[] pageZeroLevels = [("Uninitialized", 0), ("AbsentAtPml4", 1), ("AbsentAtPdpt", 2), ("AbsentAtPd", 3), ("AbsentAtPt", 4), ("Present", 5), ("Malformed", 6)];
        (string Name, byte Value)[] roles = [("Uninitialized", 0), ("CompositeCode", 1), ("OwnedStack", 2), ("LoadedImageContinuationPage", 3), ("ProbePage", 4), ("CompleteTableArena", 5), ("RuntimeStateAndCanary", 6), ("OwnedRoot", 7), ("CompositeCodeAndCallGate", 8), ("CompositeOwnedStack", 9), ("ProtectionProbePage", 10), ("CompleteTableAndStateArena", 11), ("RuntimeStateAndCanaryPage", 12), ("OwnedRootPage", 13)];
        (string Name, byte Value)[] provenances = [("Uninitialized", 0), ("AllocationReservationAndIdentityMapping", 1), ("ReservationAndIdentityMapping", 2), ("ContainedInAllocationReservationAndIdentityMapping", 3)];
        (string Name, byte Value)[] operations = [("Uninitialized", 0), ("ValidateState", 1), ("ValidateMode", 2), ("DisableInterrupts", 3), ("ClearPge", 4), ("LoadOwnedCr3", 5), ("UpdateCanary", 6), ("WalkProbeHierarchy", 7), ("CommitProbePte", 8), ("InvalidateProbePage", 9), ("VerifyProbePte", 10), ("InvokeExactProbe", 11), ("AuditPageZero", 12), ("RestoreFirmwareCr3", 13), ("RestoreCr4", 14), ("RestoreRflags", 15), ("Return", 16)];
        (string Name, byte Value)[] recoveryClasses = [("Uninitialized", 0), ("PreCliNoStateMutation", 1), ("RestoreRflags", 2), ("RestoreCr4ThenRflags", 3), ("RestoreCr3ThenCr4ThenRflags", 4), ("CommittedProbeRemainsReadOnlyExecutableThenRestoreRegisters", 5), ("SuccessRestoreRegisters", 6), ("MutationAttemptedThenRestoreRegisters", 7)];
        M01OwnedCr3CompositeTransitionResult emptyResult = default;
        M01OwnedCr3CompositeTransitionRuntimeState emptyState = default;
        M01OwnedCr3CompositeTransitionRangeBinding emptyBinding = default;
        M01OwnedCr3CompositeTransitionSemanticRecord emptySemantic = default;
        return EnumContractMatches<M01OwnedCr3CompositeTransitionStatus>(statuses) && EnumContractMatches<M01OwnedCr3CompositeTransitionRuntimeStatus>(runtimeStatuses) && EnumContractMatches<M01OwnedCr3CompositeTransitionCheckpoint>(checkpoints) && EnumContractMatches<M01OwnedCr3CompositeTransitionTerminalKind>(terminals) && EnumContractMatches<M01OwnedCr3CompositeTransitionResultPhase>(phases) && EnumContractMatches<M01OwnedCr3CompositeTransitionPageZeroLevel>(pageZeroLevels) && EnumContractMatches<M01OwnedCr3CompositeTransitionRangeRole>(roles) && EnumContractMatches<M01OwnedCr3CompositeTransitionRangeProvenance>(provenances) && EnumContractMatches<M01OwnedCr3CompositeTransitionOperation>(operations) && EnumContractMatches<M01OwnedCr3CompositeTransitionRecoveryClass>(recoveryClasses) && (ulong)Unsafe.SizeOf<M01OwnedCr3CompositeTransitionRangeBinding>() == M01OwnedCr3CompositeTransitionRangeBinding.StructureSize && (ulong)Unsafe.SizeOf<M01OwnedCr3CompositeTransitionSemanticRecord>() == M01OwnedCr3CompositeTransitionSemanticRecord.StructureSize && Unsafe.SizeOf<M01OwnedCr3CompositeTransitionRuntimeState>() == M01OwnedCr3CompositeTransitionRuntimeState.StructureSize && Unsafe.SizeOf<M01OwnedCr3CompositeTransitionResult>() == 560 && Offset<M01OwnedCr3CompositeTransitionRuntimeState>("_ownedCr3Raw") == M01OwnedCr3CompositeTransition.StateOwnedCr3RawOffset && Offset<M01OwnedCr3CompositeTransitionRuntimeState>("_expectedProbePteAddress") == M01OwnedCr3CompositeTransition.StateExpectedProbePteAddressOffset && Offset<M01OwnedCr3CompositeTransitionRuntimeState>("_expectedSelfAddress") == M01OwnedCr3CompositeTransition.StateExpectedSelfAddressOffset && Offset<M01OwnedCr3CompositeTransitionRuntimeState>("_expectedProbeHierarchyIdentity") == M01OwnedCr3CompositeTransition.StateExpectedProbeHierarchyIdentityOffset && Offset<M01OwnedCr3CompositeTransitionRuntimeState>("_originalRflags") == M01OwnedCr3CompositeTransition.StateOriginalRflagsOffset && Offset<M01OwnedCr3CompositeTransitionRuntimeState>("_committedProbePteReadback") == M01OwnedCr3CompositeTransition.StateCommittedProbePteReadbackOffset && Offset<M01OwnedCr3CompositeTransitionRuntimeState>("_runtimeStatus") == M01OwnedCr3CompositeTransition.StateRuntimeStatusOffset && Offset<M01OwnedCr3CompositeTransitionRuntimeState>("_progressCheckpoint") == M01OwnedCr3CompositeTransition.StateProgressCheckpointOffset && Offset<M01OwnedCr3CompositeTransitionRuntimeState>("_reservedOutput1") == M01OwnedCr3CompositeTransition.StateReservedOutput1Offset && !RuntimeHelpers.IsReferenceOrContainsReferences<M01OwnedCr3CompositeTransitionRangeBinding>() && !RuntimeHelpers.IsReferenceOrContainsReferences<M01OwnedCr3CompositeTransitionSemanticRecord>() && !RuntimeHelpers.IsReferenceOrContainsReferences<M01OwnedCr3CompositeTransitionRuntimeState>() && !RuntimeHelpers.IsReferenceOrContainsReferences<M01OwnedCr3CompositeTransitionResult>() && emptyResult.Status == M01OwnedCr3CompositeTransitionStatus.Uninitialized && !emptyResult.IsSuccess && emptyResult.Phase == M01OwnedCr3CompositeTransitionResultPhase.Uninitialized && !emptyResult.IsStatePrepared && !emptyResult.IsMaterialized && !emptyResult.IsReadbackVerified && !emptyResult.IsObservedAudit && !emptyResult.ProvesCompositeEncodingReadback && emptyResult.InvalidIndex == 0 && emptyResult.DefinesSingleCompositeOwnedCr3Interval && !emptyResult.ExecutesCompositeOwnedCr3Interval && !emptyResult.IsGuestEvidence && !emptyState.IsInitialized && emptyState.Size == 0 && emptyState.Version == 0 && emptyBinding.Role == M01OwnedCr3CompositeTransitionRangeRole.Uninitialized && emptySemantic.Operation == M01OwnedCr3CompositeTransitionOperation.Uninitialized && M01OwnedCr3CompositeTransition.DefinesSingleCompositeOwnedCr3Interval && !M01OwnedCr3CompositeTransition.ExecutesCompositeOwnedCr3Interval && !M01OwnedCr3CompositeTransition.IsGuestEvidence && !M01OwnedCr3CompositeTransition.CrossBindsOwnedSerialStub && !M01OwnedCr3CompositeTransition.CrossBindsOwnedMapIdentity && !M01OwnedCr3CompositeTransition.CrossBindsDiagnosticState && M01OwnedCr3CompositeTransition.SerialPurposeIsCompatibilityAliasOnly && M01OwnedCr3CompositeTransition.UsesExactLiveIntervalBindings && !M01OwnedCr3CompositeTransition.AcceptsHistoricalIdentityAliases && !M01OwnedCr3CompositeTransition.RequiresLoadedImageContinuationPage && M01OwnedCr3CompositeTransition.RequiredRangeBindingCount == 7 && M01OwnedCr3CompositeTransition.RequiredLiveIntervalRangeBindingCount == 6 && M01OwnedCr3CompositeTransition.RequiredLiveIntervalRoleMask == 0x3f00 && !M01OwnedCr3CompositeTransition.InvokesFirmwareCallbacks && !M01OwnedCr3CompositeTransition.InvokesArbitraryCallbacks && !M01OwnedCr3CompositeTransition.CallsFirmware && !M01OwnedCr3CompositeTransition.CallsConsole && !M01OwnedCr3CompositeTransition.CallsSerialIo && !M01OwnedCr3CompositeTransition.CallsCom1 && !M01OwnedCr3CompositeTransition.AllocatesFirmwarePages && !M01OwnedCr3CompositeTransition.PerformsLiveCr3Load && !M01OwnedCr3CompositeTransition.PerformsLivePageProtection && !M01OwnedCr3CompositeTransition.PerformsLiveTlbInvalidation && !M01OwnedCr3CompositeTransition.PerformsLiveProbeExecution && !M01OwnedCr3CompositeTransition.DereferencesPageZero && !M01OwnedCr3CompositeTransition.CrossBindsAcpiProvenance && !M01OwnedCr3CompositeTransition.CrossBindsFramebufferProvenance && !M01OwnedCr3CompositeTransition.SupportsPcide && !M01OwnedCr3CompositeTransition.SupportsFiveLevelPaging && !M01OwnedCr3CompositeTransition.EliminatesNmiOrSmiRisk && !M01OwnedCr3CompositeTransition.ProvesAcpiOrFramebufferSourceProvenance && !M01OwnedCr3CompositeTransition.AcceptsSeparateLeavesAsCompositeEvidence && !M01OwnedCr3CompositeTransition.IsEfiExecutionEvidence && !M01OwnedCr3CompositeTransition.IsQemuEvidence && M01OwnedCr3CompositeTransition.ExactProbeIsSoleInternalTransfer && M01OwnedCr3CompositeTransition.RetainsCommittedProbePteOnRecoverableFailure && M01OwnedCr3CompositeTransition.PreservesRbp && M01OwnedCr3CompositeTransition.PreservesRdi && M01OwnedCr3CompositeTransition.PreservesR12 && M01OwnedCr3CompositeTransition.PreservesR13 && M01OwnedCr3CompositeTransition.PreservesR14 && M01OwnedCr3CompositeTransition.PreservesR15 && M01OwnedCr3CompositeTransition.PreservesRsp && M01OwnedCr3CompositeTransition.PreservesRbx && M01OwnedCr3CompositeTransition.PreservesRsi && !M01OwnedCr3CompositeTransition.TouchesXmm6ThroughXmm15 && M01OwnedCr3CompositeTransition.ExactInvlpgCount == 1 && M01OwnedCr3CompositeTransition.ExactProbeInvocationCount == 1 && M01OwnedCr3CompositeTransition.RequiredCallerShadowSpaceBytes == 32 && M01OwnedCr3CompositeTransition.RequiredStackHeadroomBytes >= 40 && M01OwnedCr3CompositeTransition.ExternalPatchCount == 0 && M01OwnedCr3CompositeTransition.MaterializedPageByteLength == M01OwnedCr3CompositeTransition.PageSize && M01OwnedCr3CompositeTransition.RequiredByteLength == ExpectedRequiredByteLength && M01OwnedCr3CompositeTransition.AuditedInstructionCount == ExpectedInstructionCount && M01OwnedCr3CompositeTransition.RequiredFnv1a64 == ExpectedFullPageFnv1a64 && !M01OwnedCr3CompositeTransition.HasExternalRelocations && M01OwnedCr3CompositeTransition.UsesWindowsX64Abi;
    }

    private static bool TestRangeBindingAndAdversarialInputs()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionResult canonical = fixture.Execute();
        ulong fullPhysicalMask = fixture.PriorEvidence.Evidence.Preflight.PhysicalAddressMask;
        ulong pagePhysicalMask = fullPhysicalMask & ~0xfffUL;
        X64PageMapResult priorMutation = fixture.PriorEvidence.Evidence.ProtectionMutation;
        (ulong probePml4, ulong probePdpt, ulong probePd, _) = fixture.ProbeWalkEntryIndexes();
        ulong expectedPdptOrdinal = ((fixture.PageEntries[probePml4] & pagePhysicalMask) - canonical.State.TablePageArenaStart) / M01OwnedCr3CompositeTransition.PageSize;
        ulong expectedPdOrdinal = ((fixture.PageEntries[probePdpt] & pagePhysicalMask) - canonical.State.TablePageArenaStart) / M01OwnedCr3CompositeTransition.PageSize;
        ulong expectedPtOrdinal = ((fixture.PageEntries[probePd] & pagePhysicalMask) - canonical.State.TablePageArenaStart) / M01OwnedCr3CompositeTransition.PageSize;
        if (!canonical.IsSuccess || canonical.Status != M01OwnedCr3CompositeTransitionStatus.Success || canonical.InvalidIndex != M01OwnedCr3CompositeTransitionResult.NoIndex || !canonical.State.IsInitialized || canonical.State.OwnedCr3Raw != fixture.OwnedCr3Value || canonical.State.ExpectedProbePteAddress != fixture.ExpectedProbePteAddressValue || canonical.State.InitialProbePteRaw != fixture.InitialProbePteValue || canonical.State.FinalProbePteRaw != fixture.FinalProbePteValue || canonical.State.ProbeEntry != fixture.ProbeStartValue || canonical.State.TableStateArenaStart != fixture.RuntimeStateAddress || canonical.State.ExpectedSelfAddress != fixture.RuntimeStateAddress || canonical.State.CanaryAddress != fixture.CanaryAddressValue || canonical.State.ExpectedProbeHierarchyIdentity == 0 || canonical.State.ExpectedCriticalMappingPageCount == 0 || canonical.State.ExpectedCriticalMappingPageCount > M01OwnedCr3CompositeTransition.MaximumCriticalMappingPageCount || canonical.State.ExpectedProbePdptPageOrdinal != expectedPdptOrdinal || canonical.State.ExpectedProbePdPageOrdinal != expectedPdOrdinal || canonical.State.ExpectedProbePtPageOrdinal != expectedPtOrdinal || canonical.Phase != M01OwnedCr3CompositeTransitionResultPhase.StatePrepared || !canonical.IsStatePrepared || canonical.IsMaterialized || canonical.IsReadbackVerified || canonical.IsObservedAudit || canonical.ProvesCompositeEncodingReadback || *fixture.CanaryDestination != ExpectedCanary || (fullPhysicalMask & 0xfffUL) != 0xfffUL || pagePhysicalMask == 0 || (fixture.OwnedCr3Value & ~pagePhysicalMask) != 0 || priorMutation.PageImageCount != fixture.PageImageCount || priorMutation.PageImageCapacity != (ulong)fixture.PageImages.Length || priorMutation.EntryCount != fixture.PageEntryCount || priorMutation.EntryCapacity != (ulong)fixture.PageEntries.Length || priorMutation.TargetEntryIndex != fixture.PriorEvidence.Evidence.TableBuild.ProbeLeafEntryIndex)
        {
            Console.WriteLine("M01_027_RANGE_CANONICAL" + " status=" + canonical.Status + " phase=" + canonical.Phase + " invalidIndex=" + canonical.InvalidIndex + " hierarchy=0x" + canonical.State.ExpectedProbeHierarchyIdentity.ToString("X16") + " pdpt=" + canonical.State.ExpectedProbePdptPageOrdinal + "/" + expectedPdptOrdinal + " pd=" + canonical.State.ExpectedProbePdPageOrdinal + "/" + expectedPdOrdinal + " pt=" + canonical.State.ExpectedProbePtPageOrdinal + "/" + expectedPtOrdinal + " state=0x" + canonical.State.ExpectedSelfAddress.ToString("X16") + "/0x" + fixture.RuntimeStateAddress.ToString("X16"));
            return false;
        }

        if (ExecuteMissingAllocationTable(fixture).Status != M01OwnedCr3CompositeTransitionStatus.MissingAllocationRecords || fixture.Execute(allocationCount: 0).Status != M01OwnedCr3CompositeTransitionStatus.InvalidAllocationRecordCount || fixture.Execute(allocationCapacity: fixture.AllocationCount - 1).Status != M01OwnedCr3CompositeTransitionStatus.AllocationRecordCapacityTooSmall || ExecuteMissingReservationTable(fixture).Status != M01OwnedCr3CompositeTransitionStatus.MissingReservationRecords || fixture.Execute(reservationCount: 0).Status != M01OwnedCr3CompositeTransitionStatus.InvalidReservationRecordCount || fixture.Execute(reservationCapacity: fixture.ReservationCount - 1).Status != M01OwnedCr3CompositeTransitionStatus.ReservationRecordCapacityTooSmall || ExecuteMissingMappingTable(fixture).Status != M01OwnedCr3CompositeTransitionStatus.MissingIdentityMappings || fixture.Execute(mappingCount: 0).Status != M01OwnedCr3CompositeTransitionStatus.InvalidIdentityMappingCount || fixture.Execute(mappingCapacity: fixture.MappingCount - 1).Status != M01OwnedCr3CompositeTransitionStatus.IdentityMappingCapacityTooSmall || fixture.Execute(pageImageCount: 0).Status != M01OwnedCr3CompositeTransitionStatus.InvalidPageImageCount || fixture.Execute(pageImageCapacity: fixture.PageImageCount - 1).Status != M01OwnedCr3CompositeTransitionStatus.PageImageCapacityTooSmall || fixture.Execute(pageEntryCount: fixture.PageEntryCount - 1).Status != M01OwnedCr3CompositeTransitionStatus.InvalidPageEntryCount || fixture.Execute(pageEntryCapacity: fixture.PageEntryCount - 1).Status != M01OwnedCr3CompositeTransitionStatus.PageEntryCapacityTooSmall || ExecuteMissingBindingTable(fixture).Status != M01OwnedCr3CompositeTransitionStatus.MissingRangeBindings || fixture.Execute(bindingCount: 5).Status != M01OwnedCr3CompositeTransitionStatus.InvalidRangeBindingCount || fixture.Execute(bindingCapacity: 5).Status != M01OwnedCr3CompositeTransitionStatus.RangeBindingCapacityTooSmall || fixture.Execute(stateDestinationCount: 0).Status != M01OwnedCr3CompositeTransitionStatus.InvalidStateDestinationCount || fixture.Execute(stateDestinationCapacity: 0).Status != M01OwnedCr3CompositeTransitionStatus.StateDestinationCapacityTooSmall || fixture.Execute(useNullStateDestination: true).Status != M01OwnedCr3CompositeTransitionStatus.MissingStateDestination || fixture.Execute(stateDestination: (M01OwnedCr3CompositeTransitionRuntimeState*)((byte*)fixture.StateDestination + 1)).Status != M01OwnedCr3CompositeTransitionStatus.MisalignedStateDestination || fixture.Execute(stateDestination: (M01OwnedCr3CompositeTransitionRuntimeState*)(~0UL - 7)).Status != M01OwnedCr3CompositeTransitionStatus.StateDestinationSpanOverflow || fixture.Execute(canaryDestinationCount: 0).Status != M01OwnedCr3CompositeTransitionStatus.InvalidCanaryDestinationCount || fixture.Execute(canaryDestinationCapacity: 0).Status != M01OwnedCr3CompositeTransitionStatus.CanaryDestinationCapacityTooSmall || fixture.Execute(useNullCanaryDestination: true).Status != M01OwnedCr3CompositeTransitionStatus.MissingCanaryDestination || fixture.Execute(canaryDestination: (ulong*)((byte*)fixture.CanaryDestination + 1)).Status != M01OwnedCr3CompositeTransitionStatus.MisalignedCanaryDestination || fixture.Execute(canaryDestination: (ulong*)(~0UL - 7)).Status != M01OwnedCr3CompositeTransitionStatus.CanaryDestinationSpanOverflow || ExecuteMisalignedAllocationTable(fixture).Status != M01OwnedCr3CompositeTransitionStatus.MisalignedInputTable || ExecuteOverflowingAllocationTable(fixture).Status != M01OwnedCr3CompositeTransitionStatus.InputTableSpanOverflow || ExecuteAliasedTables(fixture).Status != M01OwnedCr3CompositeTransitionStatus.InputOutputAlias || ExecuteCapacityTailAliasedTables(fixture).Status != M01OwnedCr3CompositeTransitionStatus.InputOutputAlias)
        {
            Console.WriteLine("M01_027_RANGE_STAGE input-preflight-matrix");
            return false;
        }

        if ((ulong)fixture.PageImages.Length > fixture.PageImageCount && fixture.Execute(pageImageCapacity: fixture.PageImageCount).Status != M01OwnedCr3CompositeTransitionStatus.InvalidPriorPlanningEvidence)
        {
            Console.WriteLine("M01_027_RANGE_STAGE page-image-capacity");
            return false;
        }

        if ((ulong)fixture.PageEntries.Length > fixture.PageEntryCount && fixture.Execute(pageEntryCapacity: fixture.PageEntryCount).Status != M01OwnedCr3CompositeTransitionStatus.InvalidPriorPlanningEvidence)
        {
            Console.WriteLine("M01_027_RANGE_STAGE page-entry-capacity");
            return false;
        }

        using CompositeFixture wrongType = CompositeFixture.Create();
        BootMemoryRegionRecord codeAllocation = wrongType.AllocationPointer[0];
        wrongType.SetAllocation(0, Record(UefiMemoryType.LoaderData, codeAllocation.PhysicalStart, codeAllocation.PageCount));
        if (wrongType.Execute().Status != M01OwnedCr3CompositeTransitionStatus.AllocationProvenanceMismatch)
        {
            Console.WriteLine("M01_027_RANGE_STAGE wrong-allocation-type status=" + wrongType.Execute().Status);
            return false;
        }

        using CompositeFixture writableCode = CompositeFixture.Create();
        writableCode.SetMappingProtection(writableCode.CodeMappingIndex, PageProtection.Read | PageProtection.Write | PageProtection.Execute);
        if (writableCode.Execute().Status != M01OwnedCr3CompositeTransitionStatus.WritableExecutableRejected)
        {
            Console.WriteLine("M01_027_RANGE_STAGE writable-code status=" + writableCode.Execute().Status);
            return false;
        }

        using CompositeFixture executableTables = CompositeFixture.Create();
        executableTables.SetMappingProtection(executableTables.TableMappingIndex, PageProtection.Read | PageProtection.Execute);
        if (executableTables.Execute().Status != M01OwnedCr3CompositeTransitionStatus.InvalidProtection)
        {
            Console.WriteLine("M01_027_RANGE_STAGE executable-tables status=" + executableTables.Execute().Status);
            return false;
        }

        using CompositeFixture wrongMappingPages = CompositeFixture.Create();
        X64BootIdentityMappingRange tableMapping = wrongMappingPages.Mappings[wrongMappingPages.TableMappingIndex];
        wrongMappingPages.SetMappingUlong(wrongMappingPages.TableMappingIndex, "_pageCount", tableMapping.PageCount + 1);
        using CompositeFixture wrongMappingBytes = CompositeFixture.Create();
        tableMapping = wrongMappingBytes.Mappings[wrongMappingBytes.TableMappingIndex];
        wrongMappingBytes.SetMappingUlong(wrongMappingBytes.TableMappingIndex, "_byteLength", tableMapping.ByteLength + 4096);
        using CompositeFixture unknownMappingPurpose = CompositeFixture.Create();
        tableMapping = unknownMappingPurpose.Mappings[unknownMappingPurpose.TableMappingIndex];
        unknownMappingPurpose.SetMappingUlong(unknownMappingPurpose.TableMappingIndex, "_contributingPurposeMask", tableMapping.ContributingPurposeMask | (1UL << 63));
        if (wrongMappingPages.Execute().Status != M01OwnedCr3CompositeTransitionStatus.IdentityMappingProvenanceMismatch || wrongMappingBytes.Execute().Status != M01OwnedCr3CompositeTransitionStatus.IdentityMappingProvenanceMismatch || unknownMappingPurpose.Execute().Status != M01OwnedCr3CompositeTransitionStatus.IdentityMappingProvenanceMismatch)
        {
            Console.WriteLine("M01_027_RANGE_STAGE malformed-mapping-metadata");
            return false;
        }

        using CompositeFixture missingMapping = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionRangeBinding probeBinding = missingMapping.Bindings[2];
        missingMapping.Bindings[2] = Binding(probeBinding.Role, probeBinding.Provenance, Raw(probeBinding.PhysicalStart), Raw(probeBinding.PhysicalEndExclusive), probeBinding.AllocationIndex, probeBinding.ReservationIndex, M01OwnedCr3CompositeTransitionRangeBinding.NoIndex);
        if (missingMapping.Execute().Status != M01OwnedCr3CompositeTransitionStatus.IdentityMappingProvenanceMismatch)
        {
            Console.WriteLine("M01_027_RANGE_STAGE missing-mapping status=" + missingMapping.Execute().Status);
            return false;
        }

        using CompositeFixture duplicateRole = CompositeFixture.Create();
        duplicateRole.Bindings[5] = duplicateRole.Bindings[4];
        if (duplicateRole.Execute().Status != M01OwnedCr3CompositeTransitionStatus.DuplicateRangeRole)
        {
            Console.WriteLine("M01_027_RANGE_STAGE duplicate-role status=" + duplicateRole.Execute().Status);
            return false;
        }

        using CompositeFixture wrongCodeIndex = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionRangeBinding codeBinding = wrongCodeIndex.Bindings[0];
        wrongCodeIndex.Bindings[0] = Binding(codeBinding.Role, codeBinding.Provenance, Raw(codeBinding.PhysicalStart), Raw(codeBinding.PhysicalEndExclusive), 1, codeBinding.ReservationIndex, codeBinding.IdentityMappingIndex);
        if (wrongCodeIndex.Execute().Status != M01OwnedCr3CompositeTransitionStatus.AllocationProvenanceMismatch)
        {
            Console.WriteLine("M01_027_RANGE_STAGE wrong-code-index status=" + wrongCodeIndex.Execute().Status);
            return false;
        }

        using CompositeFixture wrongProbeIndex = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionRangeBinding wrongProbe = wrongProbeIndex.Bindings[2];
        wrongProbeIndex.ReplaceBinding(2, wrongProbe.Provenance, 2, wrongProbe.ReservationIndex, wrongProbe.IdentityMappingIndex);
        using CompositeFixture wrongArenaReservation = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionRangeBinding wrongArena = wrongArenaReservation.Bindings[3];
        ulong probeReservationIndex = wrongArenaReservation.Bindings[2].ReservationIndex;
        wrongArenaReservation.ReplaceBinding(3, wrongArena.Provenance, wrongArena.AllocationIndex, probeReservationIndex, wrongArena.IdentityMappingIndex);
        using CompositeFixture allocationBackedStack = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionRangeBinding stackBinding = allocationBackedStack.Bindings[1];
        allocationBackedStack.ReplaceBinding(1, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, 0, stackBinding.ReservationIndex, stackBinding.IdentityMappingIndex);
        using CompositeFixture exactState = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionRangeBinding stateBinding = exactState.Bindings[4];
        exactState.ReplaceBinding(4, M01OwnedCr3CompositeTransitionRangeProvenance.AllocationReservationAndIdentityMapping, stateBinding.AllocationIndex, stateBinding.ReservationIndex, stateBinding.IdentityMappingIndex);
        if (wrongProbeIndex.Execute().Status != M01OwnedCr3CompositeTransitionStatus.AllocationProvenanceMismatch || wrongArenaReservation.Execute().Status != M01OwnedCr3CompositeTransitionStatus.ReservationProvenanceMismatch || allocationBackedStack.Execute().Status != M01OwnedCr3CompositeTransitionStatus.AllocationProvenanceMismatch || exactState.Execute().Status != M01OwnedCr3CompositeTransitionStatus.AllocationProvenanceMismatch)
        {
            Console.WriteLine("M01_027_RANGE_STAGE binding-provenance-matrix");
            return false;
        }

        int[] dereferencedBindings = [0, 1, 2, 3, 4, 5];
        for (int index = 0; index < dereferencedBindings.Length; index++)
        {
            if (ExecuteWithMutatedDereferencedLeaf(dereferencedBindings[index], remove: true).IsSuccess || ExecuteWithMutatedDereferencedLeaf(dereferencedBindings[index], remove: false).IsSuccess)
            {
                Console.WriteLine("M01_027_RANGE_STAGE dereferenced-binding index=" + dereferencedBindings[index]);
                return false;
            }
        }

        using CompositeFixture wrongPteBase = CompositeFixture.Create();
        wrongPteBase.SetProbeLeaf((wrongPteBase.InitialProbePteValue & ~M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask) | ((wrongPteBase.ProbeStartValue + 4096) & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask));
        Span<byte> atomicState = new(wrongPteBase.StateDestination, checked((int)M01OwnedCr3CompositeTransitionRuntimeState.StructureSize + sizeof(ulong)));
        atomicState.Fill(0xa5);
        byte[] atomicBefore = atomicState.ToArray();
        M01OwnedCr3CompositeTransitionResult wrongBaseResult = wrongPteBase.Execute();
        using CompositeFixture wrongPteCache = CompositeFixture.Create();
        wrongPteCache.SetProbeLeaf(wrongPteCache.InitialProbePteValue | (1UL << 3));
        using CompositeFixture wrongPteReserved = CompositeFixture.Create();
        wrongPteReserved.SetProbeLeaf(wrongPteReserved.InitialProbePteValue | (1UL << 52));
        if (wrongBaseResult.Status != M01OwnedCr3CompositeTransitionStatus.InvalidProbePte || !atomicState.SequenceEqual(atomicBefore) || wrongPteCache.Execute().Status != M01OwnedCr3CompositeTransitionStatus.InvalidProbePte || wrongPteReserved.Execute().Status != M01OwnedCr3CompositeTransitionStatus.InvalidProbePte)
        {
            Console.WriteLine("M01_027_RANGE_STAGE malformed-probe-pte");
            return false;
        }

        ulong[] forbiddenLeafBits = [1UL << 2, 1UL << 3, 1UL << 4, 1UL << 7, 1UL << 8, 1UL << 9, 1UL << 48];
        for (int index = 0; index < forbiddenLeafBits.Length; index++)
        {
            if (ExecuteWithProbeWalkBit(level: 3, forbiddenLeafBits[index]).Status != M01OwnedCr3CompositeTransitionStatus.InvalidProbePte)
            {
                Console.WriteLine("M01_027_RANGE_STAGE forbidden-leaf-bit bit=0x" + forbiddenLeafBits[index].ToString("X16"));
                return false;
            }
        }

        ulong[] forbiddenParentBits = [1UL << 2, 1UL << 3, 1UL << 4, 1UL << 6, 1UL << 7, 1UL << 8, 1UL << 9, 1UL << 63, 1UL << 48];
        for (int level = 0; level < 3; level++)
        {
            for (int index = 0; index < forbiddenParentBits.Length; index++)
            {
                if (ExecuteWithProbeWalkBit(level, forbiddenParentBits[index]).Status != M01OwnedCr3CompositeTransitionStatus.InvalidProbePte)
                {
                    Console.WriteLine("M01_027_RANGE_STAGE forbidden-parent-bit" + " level=" + level + " bit=0x" + forbiddenParentBits[index].ToString("X16"));
                    return false;
                }
            }
        }

        for (int level = 0; level < 3; level++)
        {
            if (!ExecuteWithProbeWalkBit(level, 1UL << 5).IsSuccess)
            {
                Console.WriteLine("M01_027_RANGE_STAGE parent-accessed" + " level=" + level);
                return false;
            }
        }

        using CompositeFixture accessedDirty = CompositeFixture.Create();
        accessedDirty.SetProbeLeaf(accessedDirty.InitialProbePteValue | (1UL << 5) | (1UL << 6));
        M01OwnedCr3CompositeTransitionResult accessedDirtyResult = accessedDirty.Execute();
        if (!accessedDirtyResult.IsSuccess || accessedDirtyResult.State.FinalProbePteRaw != ((accessedDirty.InitialProbePteValue | (1UL << 5) | (1UL << 6)) & ~(M01OwnedCr3CompositeTransition.PageWritableBit | M01OwnedCr3CompositeTransition.PageNoExecuteBit)))
        {
            Console.WriteLine("M01_027_RANGE_STAGE accessed-dirty-leaf status=" + accessedDirtyResult.Status);
            return false;
        }

        M01OwnedCr3CompositeTransitionStatus invalidCr3Status = fixture.Execute(ownedCr3Raw: fixture.OwnedCr3Value | (1UL << 7)).Status;
        M01OwnedCr3CompositeTransitionStatus returnAddressStatus = fixture.Execute(expectedReturnAddress: fixture.ExpectedReturnAddressValue + 4096).Status;
        M01OwnedCr3CompositeTransitionStatus canaryAddressStatus = fixture.Execute(canaryAddress: fixture.CanaryAddressValue + 8).Status;
        M01OwnedCr3CompositeTransitionStatus duplicateCanaryStatus = fixture.Execute(expectedCanary: ExpectedCanary, replacementCanary: ExpectedCanary).Status;
        M01OwnedCr3CompositeTransitionStatus zeroExpectedCanaryStatus = fixture.Execute(expectedCanary: 0).Status;
        M01OwnedCr3CompositeTransitionStatus zeroReplacementCanaryStatus = fixture.Execute(replacementCanary: 0).Status;
        bool finalBoundaryMatrix = invalidCr3Status == M01OwnedCr3CompositeTransitionStatus.InvalidOwnedCr3 && returnAddressStatus == M01OwnedCr3CompositeTransitionStatus.InvalidProbeContract && canaryAddressStatus == M01OwnedCr3CompositeTransitionStatus.StateAddressMismatch && duplicateCanaryStatus == M01OwnedCr3CompositeTransitionStatus.InvalidCanary && zeroExpectedCanaryStatus == M01OwnedCr3CompositeTransitionStatus.InvalidCanary && zeroReplacementCanaryStatus == M01OwnedCr3CompositeTransitionStatus.InvalidCanary;
        if (!finalBoundaryMatrix)
        {
            Console.WriteLine("M01_027_RANGE_STAGE final-boundary-matrix" + " cr3=" + invalidCr3Status + " return=" + returnAddressStatus + " canaryAddress=" + canaryAddressStatus + " duplicateCanary=" + duplicateCanaryStatus + " zeroExpected=" + zeroExpectedCanaryStatus + " zeroReplacement=" + zeroReplacementCanaryStatus);
        }

        return finalBoundaryMatrix;
    }

    private static bool TestM01030ExactCompositeContract()
    {
        return TestM01030CanonicalCompositeBindings() && TestM01030RoleCardinalityAndAliases() && TestM01030MappingPoliciesAndReturnContainment() && TestM01030MalformedBindingsAndAtomicity();
    }

    private static bool TestM01030CanonicalCompositeBindings()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        M01OwnedCr3CompositeTransitionResult result = fixture.Execute();
        M01OwnedCr3CompositeTransitionRangeRole[] expectedRoles = [M01OwnedCr3CompositeTransitionRangeRole.CompositeCodeAndCallGate, M01OwnedCr3CompositeTransitionRangeRole.CompositeOwnedStack, M01OwnedCr3CompositeTransitionRangeRole.ProtectionProbePage, M01OwnedCr3CompositeTransitionRangeRole.CompleteTableAndStateArena, M01OwnedCr3CompositeTransitionRangeRole.RuntimeStateAndCanaryPage, M01OwnedCr3CompositeTransitionRangeRole.OwnedRootPage];
        if (!result.IsSuccess || !fixture.PriorEvidence.Succeeded || !fixture.PriorEvidence.PreparedForCompositeTransition || !fixture.PriorEvidence.IsPreparationOnly || fixture.PriorEvidence.IsModelOnly || fixture.PriorEvidence.IsExecutionEvidence || fixture.Bindings.Length != expectedRoles.Length || fixture.PriorEvidence.State.MappingRequirementCount != M01LivePagingIntegration.RequiredLiveIntervalMappingRequirementCount || fixture.PriorEvidence.Evidence.MappingPlan.RequiredPurposeMask != X64BootIdentityMappingPlan.RequiredLiveIntervalPurposeMask || fixture.PriorEvidence.Evidence.MappingPlan.MissingPurposeMask != 0 || fixture.PriorEvidence.Evidence.MappingPlan.GuardSourceIndex == X64BootIdentityMappingPlanResult.NoIndex || result.State.ContinuationPageStart != result.State.CompositeCodeStart || result.State.ContinuationPageEndExclusive != result.State.CompositeCodeEndExclusive || result.State.ExpectedReturnAddress != fixture.ExpectedReturnAddressValue || fixture.ExpectedReturnAddressValue < result.State.CompositeCodeStart + M01OwnedCr3CompositeTransition.RequiredByteLength || fixture.ExpectedReturnAddressValue >= result.State.CompositeCodeEndExclusive)
        {
            Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC canonical" + " status=" + result.Status + " binding_count=" + fixture.Bindings.Length + " required_mask=0x" + fixture.PriorEvidence.Evidence.MappingPlan.RequiredPurposeMask.ToString("X") + " observed_mask=0x" + fixture.PriorEvidence.Evidence.MappingPlan.ObservedPurposeMask.ToString("X"));
            return false;
        }

        for (int index = 0; index < expectedRoles.Length; index++)
        {
            if (fixture.Bindings[index].Role != expectedRoles[index])
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC role index=" + index + " observed=" + fixture.Bindings[index].Role);
                return false;
            }
        }

        ulong exactMask = X64BootIdentityMappingPlan.RequiredLiveIntervalPurposeMask;
        ulong historicalMask = 0x1feUL;
        bool observedMergedAdjacentRange = fixture.Bindings[2].IdentityMappingIndex == fixture.Bindings[3].IdentityMappingIndex;
        for (ulong mappingIndex = 0; mappingIndex < fixture.MappingCount; mappingIndex++)
        {
            X64BootIdentityMappingRange mapping = fixture.Mappings[mappingIndex];
            ulong expectedMask = 0;
            int containedBindings = 0;
            for (int bindingIndex = 0; bindingIndex < fixture.Bindings.Length; bindingIndex++)
            {
                M01OwnedCr3CompositeTransitionRangeBinding binding = fixture.Bindings[bindingIndex];
                if (ContainsRawRange(mapping, binding))
                {
                    expectedMask |= PurposeBitForRole(binding.Role);
                    containedBindings++;
                }
            }

            if (!mapping.IsInitialized || expectedMask == 0 || mapping.ContributingPurposeMask != expectedMask || (mapping.ContributingPurposeMask & ~exactMask) != 0 || (mapping.ContributingPurposeMask & historicalMask) != 0 || mapping.CachePolicy != PageCachePolicy.WriteBack)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC mapping index=" + mappingIndex + " expected_mask=0x" + expectedMask.ToString("X") + " observed_mask=0x" + mapping.ContributingPurposeMask.ToString("X") + " contained=" + containedBindings);
                return false;
            }
        }

        if (!observedMergedAdjacentRange)
        {
            Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC " + "merged_adjacent_profile_not_exercised");
            return false;
        }

        return true;
    }

    private static bool TestM01030RoleCardinalityAndAliases()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionRangeBinding[] reversed = fixture.Bindings.Reverse().ToArray();
        if (!ExecuteWithBindings(fixture, reversed).IsSuccess)
        {
            Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC reordered");
            return false;
        }

        using CompositeFixture negative = CompositeFixture.Create();
        for (int missingIndex = 0; missingIndex < negative.Bindings.Length; missingIndex++)
        {
            M01OwnedCr3CompositeTransitionRangeBinding[] missing = negative.Bindings.Where((_, index) => index != missingIndex).ToArray();
            if (ExecuteWithBindings(negative, missing).Status != M01OwnedCr3CompositeTransitionStatus.InvalidRangeBindingCount)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC missing index=" + missingIndex);
                return false;
            }
        }

        for (int duplicateIndex = 0; duplicateIndex < negative.Bindings.Length; duplicateIndex++)
        {
            M01OwnedCr3CompositeTransitionRangeBinding[] duplicate = [..negative.Bindings];
            duplicate[duplicateIndex] = duplicate[(duplicateIndex + 1) % duplicate.Length];
            if (ExecuteWithBindings(negative, duplicate).Status != M01OwnedCr3CompositeTransitionStatus.DuplicateRangeRole)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC duplicate index=" + duplicateIndex);
                return false;
            }
        }

        M01OwnedCr3CompositeTransitionRangeBinding[] extra = new M01OwnedCr3CompositeTransitionRangeBinding[negative.Bindings.Length + 1];
        negative.Bindings.CopyTo(extra, 0);
        extra[^1] = negative.Bindings[0];
        if (ExecuteWithBindings(negative, extra).Status != M01OwnedCr3CompositeTransitionStatus.InvalidRangeBindingCount)
        {
            Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC extra");
            return false;
        }

        byte[] forbiddenRoleValues = [0, 1, 2, 3, 4, 5, 6, 7, 255];
        for (int index = 0; index < forbiddenRoleValues.Length; index++)
        {
            M01OwnedCr3CompositeTransitionRangeBinding[] bindings = [..negative.Bindings];
            M01OwnedCr3CompositeTransitionRangeBinding source = bindings[0];
            bindings[0] = Binding((M01OwnedCr3CompositeTransitionRangeRole)forbiddenRoleValues[index], source.Provenance, Raw(source.PhysicalStart), Raw(source.PhysicalEndExclusive), source.AllocationIndex, source.ReservationIndex, source.IdentityMappingIndex);
            if (ExecuteWithBindings(negative, bindings).Status != M01OwnedCr3CompositeTransitionStatus.InvalidRangeBinding)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC historical_role=" + forbiddenRoleValues[index]);
                return false;
            }
        }

        for (int targetIndex = 0; targetIndex < negative.Bindings.Length; targetIndex++)
        {
            for (int sourceIndex = 0; sourceIndex < negative.Bindings.Length; sourceIndex++)
            {
                if (targetIndex == sourceIndex)
                {
                    continue;
                }

                M01OwnedCr3CompositeTransitionRangeBinding[] aliased = [..negative.Bindings];
                M01OwnedCr3CompositeTransitionRangeBinding target = aliased[targetIndex];
                M01OwnedCr3CompositeTransitionRangeBinding source = aliased[sourceIndex];
                aliased[targetIndex] = Binding(target.Role, target.Provenance, Raw(source.PhysicalStart), Raw(source.PhysicalEndExclusive), target.AllocationIndex, target.ReservationIndex, target.IdentityMappingIndex);
                if (ExecuteWithBindings(negative, aliased).IsSuccess)
                {
                    Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC cross_alias=" + targetIndex + "<-" + sourceIndex);
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TestM01030MappingPoliciesAndReturnContainment()
    {
        using (CompositeFixture additional = CompositeFixture.Create())
        {
            HashSet<ulong> indexes = additional.Bindings.Select(binding => binding.IdentityMappingIndex).ToHashSet();
            foreach (ulong mappingIndex in indexes)
            {
                X64BootIdentityMappingRange mapping = additional.Mappings[mappingIndex];
                additional.SetMappingUlong(mappingIndex, "_contributingPurposeMask", mapping.ContributingPurposeMask | (1UL << (byte)X64BootIdentityMappingPurpose.AdditionalRequired));
                if (additional.Execute().Status != M01OwnedCr3CompositeTransitionStatus.IdentityMappingProvenanceMismatch)
                {
                    Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC " + "additional_required mapping=" + mappingIndex);
                    return false;
                }

                additional.SetMappingUlong(mappingIndex, "_contributingPurposeMask", mapping.ContributingPurposeMask);
            }
        }

        using (CompositeFixture historical = CompositeFixture.Create())
        {
            X64BootIdentityMappingRange mapping = historical.Mappings[historical.CodeMappingIndex];
            historical.SetMappingUlong(historical.CodeMappingIndex, "_contributingPurposeMask", mapping.ContributingPurposeMask | (1UL << (byte)X64BootIdentityMappingPurpose.SerialStub));
            if (historical.Execute().Status != M01OwnedCr3CompositeTransitionStatus.IdentityMappingProvenanceMismatch)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC historical_mask");
                return false;
            }
        }

        using (CompositeFixture wrongCache = CompositeFixture.Create())
        {
            wrongCache.SetMappingCachePolicy(wrongCache.Bindings[1].IdentityMappingIndex, PageCachePolicy.Uncached);
            if (wrongCache.Execute().Status != M01OwnedCr3CompositeTransitionStatus.IdentityMappingProvenanceMismatch)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC cache");
                return false;
            }
        }

        using (CompositeFixture wrongCode = CompositeFixture.Create())
        {
            wrongCode.SetMappingProtection(wrongCode.CodeMappingIndex, PageProtection.Read | PageProtection.Write);
            if (wrongCode.Execute().Status != M01OwnedCr3CompositeTransitionStatus.InvalidProtection)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC code_policy");
                return false;
            }
        }

        using (CompositeFixture wxCode = CompositeFixture.Create())
        {
            wxCode.SetMappingProtection(wxCode.CodeMappingIndex, PageProtection.Read | PageProtection.Write | PageProtection.Execute);
            if (wxCode.Execute().Status != M01OwnedCr3CompositeTransitionStatus.WritableExecutableRejected)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC wx");
                return false;
            }
        }

        using (CompositeFixture executableStack = CompositeFixture.Create())
        {
            executableStack.SetMappingProtection(executableStack.Bindings[1].IdentityMappingIndex, PageProtection.Read | PageProtection.Execute);
            if (executableStack.Execute().Status != M01OwnedCr3CompositeTransitionStatus.InvalidProtection)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC stack_policy");
                return false;
            }
        }

        using (CompositeFixture guard = CompositeFixture.Create())
        {
            ulong guardReservation = FindReservationIndex(guard, BootReservationKind.BootStackGuard);
            ulong guardStart = Raw(guard.ReservationPointer[guardReservation].PhysicalStart);
            ulong guardLeaf = guard.LeafEntryIndex(guardStart);
            guard.PageEntries[guardLeaf] = guardStart | M01OwnedCr3CompositeTransition.PagePresentBit | M01OwnedCr3CompositeTransition.PageWritableBit | M01OwnedCr3CompositeTransition.PageNoExecuteBit;
            if (guard.Execute().Status != M01OwnedCr3CompositeTransitionStatus.IdentityMappingProvenanceMismatch)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC guard_present");
                return false;
            }
        }

        using CompositeFixture returns = CompositeFixture.Create();
        ulong codeStart = Raw(returns.Bindings[0].VirtualStart);
        ulong codeEnd = Raw(returns.Bindings[0].VirtualEndExclusive);
        ulong[] rejectedReturns = [codeStart - 1, codeStart, codeStart + M01OwnedCr3CompositeTransition.RequiredByteLength - 1, codeEnd, ContinuationPageStart];
        for (int index = 0; index < rejectedReturns.Length; index++)
        {
            if (returns.Execute(expectedReturnAddress: rejectedReturns[index]).Status != M01OwnedCr3CompositeTransitionStatus.InvalidProbeContract)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC return index=" + index);
                return false;
            }
        }

        return true;
    }

    private static bool TestM01030MalformedBindingsAndAtomicity()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionRangeBinding[] malformed = [..fixture.Bindings];
        malformed[0] = default;
        if (ExecuteWithBindings(fixture, malformed).Status != M01OwnedCr3CompositeTransitionStatus.InvalidRangeBinding)
        {
            Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC default_binding");
            return false;
        }

        M01OwnedCr3CompositeTransitionRangeBinding source = fixture.Bindings[0];
        ulong sourceStart = Raw(source.PhysicalStart);
        ulong sourceEnd = Raw(source.PhysicalEndExclusive);
        (ulong Start, ulong End)[] malformedGeometry = [(sourceStart + 1, sourceEnd), (sourceStart, sourceEnd - 1), (sourceEnd, sourceStart), (sourceStart, sourceStart)];
        for (int index = 0; index < malformedGeometry.Length; index++)
        {
            malformed = [..fixture.Bindings];
            malformed[0] = Binding(source.Role, source.Provenance, malformedGeometry[index].Start, malformedGeometry[index].End, source.AllocationIndex, source.ReservationIndex, source.IdentityMappingIndex);
            if (ExecuteWithBindings(fixture, malformed).IsSuccess)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC geometry index=" + index);
                return false;
            }
        }

        for (int index = 0; index < fixture.Bindings.Length; index++)
        {
            M01OwnedCr3CompositeTransitionRangeBinding binding = fixture.Bindings[index];
            ulong start = Raw(binding.PhysicalStart);
            ulong end = Raw(binding.PhysicalEndExclusive);
            malformed = [..fixture.Bindings];
            malformed[index] = Binding(binding.Role, binding.Provenance, start + 8, end, binding.AllocationIndex, binding.ReservationIndex, binding.IdentityMappingIndex);
            if (ExecuteWithBindings(fixture, malformed).IsSuccess)
            {
                Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC partial index=" + index);
                return false;
            }
        }

        Span<byte> output = new(fixture.StateDestination, checked((int)M01OwnedCr3CompositeTransitionRuntimeState.StructureSize + sizeof(ulong)));
        output.Fill(0xa5);
        byte[] before = output.ToArray();
        M01OwnedCr3CompositeTransitionResult rejected = fixture.Execute(expectedReturnAddress: Raw(fixture.Bindings[0].VirtualEndExclusive));
        if (rejected.Status != M01OwnedCr3CompositeTransitionStatus.InvalidProbeContract || !output.SequenceEqual(before))
        {
            Console.WriteLine("M01_030_COMPOSITE_DIAGNOSTIC atomic" + " status=" + rejected.Status);
            return false;
        }

        return true;
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteWithBindings(CompositeFixture fixture, M01OwnedCr3CompositeTransitionRangeBinding[] bindings)
    {
        fixed (X64BootIdentityMappingRange* mappings = fixture.Mappings)
        fixed (M01OwnedCr3CompositeTransitionRangeBinding* pointer = bindings)
        {
            return ExecuteRaw(fixture, fixture.AllocationPointer, fixture.AllocationCount, fixture.AllocationCapacity, fixture.ReservationPointer, fixture.ReservationCount, fixture.ReservationCapacity, mappings, fixture.MappingCount, (ulong)fixture.Mappings.Length, pointer, (ulong)bindings.Length, (ulong)bindings.Length);
        }
    }

    private static ulong PurposeBitForRole(M01OwnedCr3CompositeTransitionRangeRole role)
    {
        X64BootIdentityMappingPurpose purpose = role switch
        {
            M01OwnedCr3CompositeTransitionRangeRole.CompositeCodeAndCallGate => X64BootIdentityMappingPurpose.CompositeCodeAndCallGate,
            M01OwnedCr3CompositeTransitionRangeRole.CompositeOwnedStack => X64BootIdentityMappingPurpose.CompositeOwnedStack,
            M01OwnedCr3CompositeTransitionRangeRole.ProtectionProbePage => X64BootIdentityMappingPurpose.ProtectionProbePage,
            M01OwnedCr3CompositeTransitionRangeRole.CompleteTableAndStateArena => X64BootIdentityMappingPurpose.CompleteTableAndStateArena,
            M01OwnedCr3CompositeTransitionRangeRole.RuntimeStateAndCanaryPage => X64BootIdentityMappingPurpose.RuntimeStateAndCanaryPage,
            M01OwnedCr3CompositeTransitionRangeRole.OwnedRootPage => X64BootIdentityMappingPurpose.OwnedRootPage,
            _ => X64BootIdentityMappingPurpose.Uninitialized
        };
        return purpose == X64BootIdentityMappingPurpose.Uninitialized ? 0 : 1UL << (byte)purpose;
    }

    private static bool ContainsRawRange(X64BootIdentityMappingRange mapping, M01OwnedCr3CompositeTransitionRangeBinding binding)
    {
        ulong mappingStart = Raw(mapping.PhysicalStart);
        ulong mappingEnd = Raw(mapping.PhysicalEndExclusive);
        ulong bindingStart = Raw(binding.PhysicalStart);
        ulong bindingEnd = Raw(binding.PhysicalEndExclusive);
        return mappingStart <= bindingStart && bindingEnd <= mappingEnd;
    }

    private static ulong FindReservationIndex(CompositeFixture fixture, BootReservationKind kind)
    {
        ulong found = M01OwnedCr3CompositeTransitionResult.NoIndex;
        for (ulong index = 0; index < fixture.ReservationCount; index++)
        {
            if (fixture.ReservationPointer[index].Kind != kind)
            {
                continue;
            }

            if (found != M01OwnedCr3CompositeTransitionResult.NoIndex)
            {
                throw new InvalidOperationException("Duplicate canonical reservation: " + kind);
            }

            found = index;
        }

        if (found == M01OwnedCr3CompositeTransitionResult.NoIndex)
        {
            throw new InvalidOperationException("Missing canonical reservation: " + kind);
        }

        return found;
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteWithProbeWalkBit(int level, ulong bit)
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        (ulong pml4, ulong pdpt, ulong pd, ulong pt) = fixture.ProbeWalkEntryIndexes();
        ulong[] indexes = [pml4, pdpt, pd, pt];
        fixture.PageEntries[indexes[level]] |= bit;
        return fixture.Execute();
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteWithMutatedDereferencedLeaf(int bindingIndex, bool remove)
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionRangeBinding binding = fixture.Bindings[bindingIndex];
        ulong leafIndex = fixture.LeafEntryIndex(Raw(binding.VirtualStart));
        if (remove)
        {
            fixture.PageEntries[leafIndex] = 0;
        }
        else if (binding.Role == M01OwnedCr3CompositeTransitionRangeRole.CompositeCodeAndCallGate)
        {
            fixture.PageEntries[leafIndex] |= M01OwnedCr3CompositeTransition.PageWritableBit | M01OwnedCr3CompositeTransition.PageNoExecuteBit;
        }
        else
        {
            fixture.PageEntries[leafIndex] &= ~M01OwnedCr3CompositeTransition.PageNoExecuteBit;
        }

        return fixture.Execute();
    }

    private static bool TestMaterializationReadbackAndAtomicity()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionResult prepared = fixture.Execute();
        if (!prepared.IsSuccess)
        {
            Console.WriteLine("M01_027_MATERIALIZE_PREPARE status=" + prepared.Status + " phase=" + prepared.Phase + " invalidIndex=" + prepared.InvalidIndex + " expected=" + prepared.ExpectedValue + " observed=" + prepared.ObservedValue);
            return false;
        }

        using CompositePlanStorage storage = new((byte*)prepared.State.CompositeCodeStart);
        storage.Fill(0xa5);
        ulong entryAddress = prepared.State.CompositeCodeStart;
        byte[] bytesBefore = storage.CopyAllBytes();
        byte[] semanticsBefore = storage.CopyAllSemantics();
        if (!RejectsForgedPreparedStateBeforeWriting(fixture, storage, entryAddress, bytesBefore, semanticsBefore))
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE forged-prepared-state");
            return false;
        }

        M01OwnedCr3CompositeTransitionResult missingDestination = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, null, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, storage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (missingDestination.Status != M01OwnedCr3CompositeTransitionStatus.MissingDestination || !storage.AllStorageEquals(bytesBefore, semanticsBefore))
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE missing-destination" + " status=" + missingDestination.Status);
            return false;
        }

        M01OwnedCr3CompositeTransitionResult missingSemantics = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, storage.Bytes, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, null, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (missingSemantics.Status != M01OwnedCr3CompositeTransitionStatus.MissingSemanticDestination || !storage.AllStorageEquals(bytesBefore, semanticsBefore))
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE missing-semantics" + " status=" + missingSemantics.Status);
            return false;
        }

        M01OwnedCr3CompositeTransitionResult shortSemantics = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, storage.Bytes, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, storage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount - 1);
        if (shortSemantics.Status != M01OwnedCr3CompositeTransitionStatus.SemanticCapacityTooSmall || !storage.AllStorageEquals(bytesBefore, semanticsBefore))
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE short-semantics" + " status=" + shortSemantics.Status);
            return false;
        }

        M01OwnedCr3CompositeTransitionResult materialized = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, storage.Bytes, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, storage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (!materialized.IsSuccess || materialized.Phase != M01OwnedCr3CompositeTransitionResultPhase.Materialized || !materialized.IsMaterialized || materialized.IsStatePrepared || materialized.IsReadbackVerified || materialized.IsObservedAudit || materialized.ProvesCompositeEncodingReadback || materialized.ByteCount != M01OwnedCr3CompositeTransition.MaterializedPageByteLength || materialized.RequiredByteCount != M01OwnedCr3CompositeTransition.MaterializedPageByteLength || materialized.RequiredMaterializedPageByteLength != M01OwnedCr3CompositeTransition.MaterializedPageByteLength || materialized.RequiredInstructionByteLength != M01OwnedCr3CompositeTransition.RequiredByteLength || M01OwnedCr3CompositeTransition.RequiredByteLength == 0 || M01OwnedCr3CompositeTransition.RequiredByteLength >= M01OwnedCr3CompositeTransition.MaterializedPageByteLength || materialized.ByteCount > M01OwnedCr3CompositeTransition.MaximumByteCapacity || materialized.SemanticCount != M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount || materialized.ExpectedValue != M01OwnedCr3CompositeTransition.AuditedInstructionCount || materialized.ObservedValue != M01OwnedCr3CompositeTransition.AuditedInstructionCount || materialized.Fnv1a64 == 0 || !storage.ZeroFilledPageTail(M01OwnedCr3CompositeTransition.RequiredByteLength) || !storage.TailsEqual(0xa5))
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE canonical" + " status=" + materialized.Status + " phase=" + materialized.Phase + " bytes=" + materialized.ByteCount + " semantics=" + materialized.SemanticCount + " expected=" + materialized.ExpectedValue + " observed=" + materialized.ObservedValue);
            return false;
        }

        byte[] exactBytes = storage.CopyBytes(materialized.ByteCount);
        byte[] exactSemantics = storage.CopySemantics(materialized.SemanticCount);
        M01OwnedCr3CompositeTransitionResult readback = M01OwnedCr3CompositeTransition.ValidateReadback(fixture.StateDestination, 1, 1, storage.Bytes, materialized.ByteCount, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, storage.Semantics, materialized.SemanticCount, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (!readback.IsSuccess || readback.Phase != M01OwnedCr3CompositeTransitionResultPhase.ReadbackVerified || !readback.IsReadbackVerified || !readback.ProvesCompositeEncodingReadback || readback.IsStatePrepared || readback.IsMaterialized || readback.IsObservedAudit || readback.Fnv1a64 != materialized.Fnv1a64 || !storage.ExactPrefixesEqual(exactBytes, exactSemantics))
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE readback" + " status=" + readback.Status + " phase=" + readback.Phase + " invalidIndex=" + readback.InvalidIndex);
            return false;
        }

        M01OwnedCr3CompositeTransitionResult shortDestination = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, storage.Bytes, materialized.ByteCount - 1, entryAddress, storage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (shortDestination.Status != M01OwnedCr3CompositeTransitionStatus.DestinationCapacityTooSmall || !storage.ExactPrefixesEqual(exactBytes, exactSemantics))
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE short-destination" + " status=" + shortDestination.Status);
            return false;
        }

        M01OwnedCr3CompositeTransitionResult alias = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, storage.Bytes, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, (M01OwnedCr3CompositeTransitionSemanticRecord*)storage.Bytes, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (alias.Status != M01OwnedCr3CompositeTransitionStatus.DestinationSemanticOverlap)
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE alias" + " status=" + alias.Status);
            return false;
        }

        for (ulong index = 0; index < materialized.ByteCount; index++)
        {
            storage.Bytes[index] ^= 0x80;
            M01OwnedCr3CompositeTransitionResult corrupt = M01OwnedCr3CompositeTransition.ValidateReadback(fixture.StateDestination, 1, 1, storage.Bytes, materialized.ByteCount, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, storage.Semantics, materialized.SemanticCount, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
            storage.Bytes[index] ^= 0x80;
            if (corrupt.Status != M01OwnedCr3CompositeTransitionStatus.ByteMismatch || !IsExpectedByteMismatchLocation(storage.Bytes, materialized.ByteCount, index, corrupt.InvalidIndex))
            {
                Console.WriteLine("M01_027_MATERIALIZE_STAGE byte-corruption" + " index=" + index + " status=" + corrupt.Status + " invalidIndex=" + corrupt.InvalidIndex);
                return false;
            }
        }

        for (ulong index = 0; index < materialized.SemanticCount; index++)
        {
            M01OwnedCr3CompositeTransitionSemanticRecord original = storage.Semantics[index];
            storage.Semantics[index] = default;
            M01OwnedCr3CompositeTransitionResult corrupt = M01OwnedCr3CompositeTransition.ValidateReadback(fixture.StateDestination, 1, 1, storage.Bytes, materialized.ByteCount, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, storage.Semantics, materialized.SemanticCount, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
            storage.Semantics[index] = original;
            if (corrupt.Status != M01OwnedCr3CompositeTransitionStatus.SemanticMismatch || corrupt.InvalidIndex != index)
            {
                Console.WriteLine("M01_027_MATERIALIZE_STAGE semantic-corruption" + " index=" + index + " status=" + corrupt.Status + " invalidIndex=" + corrupt.InvalidIndex);
                return false;
            }
        }

        M01OwnedCr3CompositeTransitionStatus missingSourceStatus = M01OwnedCr3CompositeTransition.ValidateReadback(fixture.StateDestination, 1, 1, null, materialized.ByteCount, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, storage.Semantics, materialized.SemanticCount, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount).Status;
        M01OwnedCr3CompositeTransitionStatus invalidSourceCountStatus = M01OwnedCr3CompositeTransition.ValidateReadback(fixture.StateDestination, 1, 1, storage.Bytes, materialized.ByteCount - 1, M01OwnedCr3CompositeTransition.MaximumByteCapacity, entryAddress, storage.Semantics, materialized.SemanticCount, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount).Status;
        M01OwnedCr3CompositeTransitionStatus shortSourceCapacityStatus = M01OwnedCr3CompositeTransition.ValidateReadback(fixture.StateDestination, 1, 1, storage.Bytes, materialized.ByteCount, materialized.ByteCount - 1, entryAddress, storage.Semantics, materialized.SemanticCount, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount).Status;
        bool prefixesEqual = storage.ExactPrefixesEqual(exactBytes, exactSemantics);
        bool finalReadbackMatrix = missingSourceStatus == M01OwnedCr3CompositeTransitionStatus.MissingSource && invalidSourceCountStatus == M01OwnedCr3CompositeTransitionStatus.InvalidSourceCount && shortSourceCapacityStatus == M01OwnedCr3CompositeTransitionStatus.SourceCapacityTooSmall && prefixesEqual;
        if (!finalReadbackMatrix)
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE final-readback-matrix" + " missing=" + missingSourceStatus + " count=" + invalidSourceCountStatus + " capacity=" + shortSourceCapacityStatus + " prefixes=" + prefixesEqual);
        }

        return finalReadbackMatrix;
    }

    private static bool RejectsForgedPreparedStateBeforeWriting(CompositeFixture fixture, CompositePlanStorage storage, ulong entryAddress, byte[] expectedBytes, byte[] expectedSemantics)
    {
        Span<byte> stateBytes = new(fixture.StateDestination, checked((int)M01OwnedCr3CompositeTransitionRuntimeState.StructureSize));
        byte[] canonicalState = stateBytes.ToArray();
        fixture.SetStateUlong(M01OwnedCr3CompositeTransition.StateCompositeCodeEndOffset, entryAddress - 1);
        M01OwnedCr3CompositeTransitionResult underflow = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, storage.Bytes, M01OwnedCr3CompositeTransition.MaterializedPageByteLength, entryAddress, storage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        canonicalState.CopyTo(stateBytes);
        if (underflow.IsSuccess || !storage.AllStorageEquals(expectedBytes, expectedSemantics))
        {
            Console.WriteLine("M01_027_MATERIALIZE_STAGE pointer-underflow" + " status=" + underflow.Status);
            return false;
        }

        const ulong LastAlignedPage = 0xffff_ffff_ffff_f000UL;
        fixture.SetStateUlong(M01OwnedCr3CompositeTransition.StateCompositeCodeStartOffset, LastAlignedPage);
        fixture.SetStateUlong(M01OwnedCr3CompositeTransition.StateCompositeCodeEndOffset, 0);
        M01OwnedCr3CompositeTransitionResult wrapped = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, (byte*)LastAlignedPage, M01OwnedCr3CompositeTransition.MaterializedPageByteLength, LastAlignedPage, storage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        canonicalState.CopyTo(stateBytes);
        return !wrapped.IsSuccess && storage.AllStorageEquals(expectedBytes, expectedSemantics);
    }

    private static bool TestIndependentBytePlanAndAbi()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionResult prepared = fixture.Execute();
        if (!prepared.IsStatePrepared)
        {
            Console.WriteLine("M01_027_DECODE_PREPARE status=" + prepared.Status + " phase=" + prepared.Phase + " invalidIndex=" + prepared.InvalidIndex);
            return false;
        }

        using CompositePlanStorage storage = new((byte*)prepared.State.CompositeCodeStart);
        storage.Fill(0xa5);
        M01OwnedCr3CompositeTransitionResult materialized = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, storage.Bytes, M01OwnedCr3CompositeTransition.MaterializedPageByteLength, (ulong)storage.Bytes, storage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (!materialized.IsMaterialized || materialized.Fnv1a64 != ExpectedFullPageFnv1a64 || M01OwnedCr3CompositeTransition.RequiredFnv1a64 != ExpectedFullPageFnv1a64)
        {
            Console.WriteLine("M01_027_DECODE_STAGE materialize" + " status=" + materialized.Status + " phase=" + materialized.Phase + " fnv=0x" + materialized.Fnv1a64.ToString("X16"));
            return false;
        }

        byte[] fullPage = storage.CopyBytes(M01OwnedCr3CompositeTransition.MaterializedPageByteLength);
        byte[] prefix = storage.CopyBytes(M01OwnedCr3CompositeTransition.RequiredByteLength);
        ulong observedFullPageFnv = ComputeFnv1a64(fullPage);
        string observedFullPageSha = Convert.ToHexString(SHA256.HashData(fullPage));
        string observedPrefixSha = Convert.ToHexString(SHA256.HashData(prefix));
        bool decoded = TryDecodeComposite(prefix, out List<CompositeDecodedInstruction> instructions);
        if (observedFullPageFnv != ExpectedFullPageFnv1a64 || observedFullPageSha != ExpectedFullPageSha256 || observedPrefixSha != ExpectedInstructionPrefixSha256 || !decoded || (ulong)instructions.Count != M01OwnedCr3CompositeTransition.AuditedInstructionCount)
        {
            Console.WriteLine("M01_027_FREEZE_HASH" + " length=" + prefix.Length + " instructions=" + instructions.Count + " fnv=0x" + observedFullPageFnv.ToString("X16") + " fullSha256=" + observedFullPageSha + " prefixSha256=" + observedPrefixSha + " decoded=" + decoded);
            return false;
        }

        using CompositeFixture secondFixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionResult secondPrepared = secondFixture.Execute();
        if (!secondPrepared.IsStatePrepared)
        {
            Console.WriteLine("M01_027_DECODE_STAGE second-prepare");
            return false;
        }

        using CompositePlanStorage secondStorage = new((byte*)secondPrepared.State.CompositeCodeStart);
        secondStorage.Fill(0x3c);
        M01OwnedCr3CompositeTransitionResult secondMaterialized = M01OwnedCr3CompositeTransition.TryMaterialize(secondFixture.StateDestination, 1, 1, secondStorage.Bytes, M01OwnedCr3CompositeTransition.MaterializedPageByteLength, (ulong)secondStorage.Bytes, secondStorage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (!secondMaterialized.IsMaterialized || secondFixture.StateDestination == fixture.StateDestination || secondStorage.Bytes == storage.Bytes || !new ReadOnlySpan<byte>(secondStorage.Bytes, checked((int)M01OwnedCr3CompositeTransition.MaterializedPageByteLength)).SequenceEqual(fullPage) || !new ReadOnlySpan<byte>(secondStorage.Semantics, checked((int)(M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount * M01OwnedCr3CompositeTransitionSemanticRecord.StructureSize))).SequenceEqual(storage.CopySemantics(M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount)))
        {
            Console.WriteLine("M01_027_DECODE_STAGE relocation-freedom");
            return false;
        }

        HashSet<int> boundaries = instructions.Select(item => item.Offset).ToHashSet();
        boundaries.Add(prefix.Length);
        if (instructions.Any(item => item.BranchTarget is int target && (!boundaries.Contains(target) || target < 0 || target > prefix.Length)))
        {
            Console.WriteLine("M01_027_DECODE_STAGE branch-boundaries");
            return false;
        }

        uint semanticEnd = 0;
        ExpectedSemanticRecord[] expectedSemantics = [new(M01OwnedCr3CompositeTransitionOperation.ValidateState, M01OwnedCr3CompositeTransitionRecoveryClass.PreCliNoStateMutation, 0, 1104, 184), new(M01OwnedCr3CompositeTransitionOperation.ValidateMode, M01OwnedCr3CompositeTransitionRecoveryClass.PreCliNoStateMutation, 1104, 335, 208), new(M01OwnedCr3CompositeTransitionOperation.DisableInterrupts, M01OwnedCr3CompositeTransitionRecoveryClass.RestoreRflags, 1439, 132, 272), new(M01OwnedCr3CompositeTransitionOperation.ClearPge, M01OwnedCr3CompositeTransitionRecoveryClass.RestoreCr4ThenRflags, 1571, 89, 280), new(M01OwnedCr3CompositeTransitionOperation.LoadOwnedCr3, M01OwnedCr3CompositeTransitionRecoveryClass.RestoreCr3ThenCr4ThenRflags, 1660, 70, 288), new(M01OwnedCr3CompositeTransitionOperation.UpdateCanary, M01OwnedCr3CompositeTransitionRecoveryClass.RestoreCr3ThenCr4ThenRflags, 1730, 105, 304), new(M01OwnedCr3CompositeTransitionOperation.WalkProbeHierarchy, M01OwnedCr3CompositeTransitionRecoveryClass.RestoreCr3ThenCr4ThenRflags, 1835, 871, 128), new(M01OwnedCr3CompositeTransitionOperation.CommitProbePte, M01OwnedCr3CompositeTransitionRecoveryClass.MutationAttemptedThenRestoreRegisters, 2706, 16, 0x8000_0000_0000_0002UL), new(M01OwnedCr3CompositeTransitionOperation.InvalidateProbePage, M01OwnedCr3CompositeTransitionRecoveryClass.MutationAttemptedThenRestoreRegisters, 2722, 51, 1), new(M01OwnedCr3CompositeTransitionOperation.VerifyProbePte, M01OwnedCr3CompositeTransitionRecoveryClass.MutationAttemptedThenRestoreRegisters, 2773, 58, 344), new(M01OwnedCr3CompositeTransitionOperation.InvokeExactProbe, M01OwnedCr3CompositeTransitionRecoveryClass.CommittedProbeRemainsReadOnlyExecutableThenRestoreRegisters, 2831, 93, 1_330_859_851UL), new(M01OwnedCr3CompositeTransitionOperation.AuditPageZero, M01OwnedCr3CompositeTransitionRecoveryClass.CommittedProbeRemainsReadOnlyExecutableThenRestoreRegisters, 2924, 663, 368), new(M01OwnedCr3CompositeTransitionOperation.RestoreFirmwareCr3, M01OwnedCr3CompositeTransitionRecoveryClass.SuccessRestoreRegisters, 3587, 108, 408), new(M01OwnedCr3CompositeTransitionOperation.RestoreCr4, M01OwnedCr3CompositeTransitionRecoveryClass.SuccessRestoreRegisters, 3695, 56, 424), new(M01OwnedCr3CompositeTransitionOperation.RestoreRflags, M01OwnedCr3CompositeTransitionRecoveryClass.SuccessRestoreRegisters, 3751, 48, 432), new(M01OwnedCr3CompositeTransitionOperation.Return, M01OwnedCr3CompositeTransitionRecoveryClass.SuccessRestoreRegisters, 3799, 31, 464)];
        bool semanticFreezeMismatch = false;
        for (ulong index = 0; index < M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount; index++)
        {
            M01OwnedCr3CompositeTransitionSemanticRecord observed = storage.Semantics[index];
            ExpectedSemanticRecord expected = expectedSemantics[index];
            if (observed.Operation != expected.Operation || observed.RecoveryClass != expected.RecoveryClass || observed.ByteOffset != expected.ByteOffset || observed.ByteLength != expected.ByteLength || observed.StateOffsetOrConstant != expected.StateOffsetOrConstant || observed.BranchTargetOffset != M01OwnedCr3CompositeTransitionSemanticRecord.NoBranchTarget)
            {
                semanticFreezeMismatch = true;
            }
        }

        if (semanticFreezeMismatch)
        {
            for (ulong index = 0; index < M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount; index++)
            {
                M01OwnedCr3CompositeTransitionSemanticRecord observed = storage.Semantics[index];
                Console.WriteLine("M01_027_FREEZE_SEMANTIC" + " index=" + index + " operation=" + observed.Operation + " recovery=" + observed.RecoveryClass + " offset=" + observed.ByteOffset + " length=" + observed.ByteLength + " stateOrConstant=" + observed.StateOffsetOrConstant + " branch=" + observed.BranchTargetOffset);
            }

            return false;
        }

        for (ulong index = 0; index < M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount; index++)
        {
            M01OwnedCr3CompositeTransitionSemanticRecord semantic = storage.Semantics[index];
            ExpectedSemanticRecord expected = expectedSemantics[index];
            if (semantic.Operation != expected.Operation || semantic.RecoveryClass != expected.RecoveryClass || semantic.ByteOffset != expected.ByteOffset || semantic.ByteLength != expected.ByteLength || semantic.StateOffsetOrConstant != expected.StateOffsetOrConstant || semantic.BranchTargetOffset != M01OwnedCr3CompositeTransitionSemanticRecord.NoBranchTarget || semantic.ByteOffset != semanticEnd || !boundaries.Contains(checked((int)semantic.ByteOffset)) || !boundaries.Contains(checked((int)(semantic.ByteOffset + semantic.ByteLength))) || (semantic.BranchTargetOffset != M01OwnedCr3CompositeTransitionSemanticRecord.NoBranchTarget && !boundaries.Contains(checked((int)semantic.BranchTargetOffset))))
            {
                Console.WriteLine("M01_027_DECODE_STAGE semantic-structure" + " index=" + index);
                return false;
            }

            semanticEnd = checked(semantic.ByteOffset + semantic.ByteLength);
        }

        if (semanticEnd != M01OwnedCr3CompositeTransition.RequiredByteLength)
        {
            Console.WriteLine("M01_027_DECODE_STAGE semantic-end" + " observed=" + semanticEnd);
            return false;
        }

        if (instructions.Count(item => item.Kind == CompositeInstructionKind.Cli) != 1 || instructions.Any(item => item.Kind == CompositeInstructionKind.Sti) || instructions.Count(item => item.Kind == CompositeInstructionKind.Invlpg) != 1 || instructions.Count(item => item.Kind == CompositeInstructionKind.IndirectCall) != 1 || instructions.Any(item => item.Kind == CompositeInstructionKind.DirectCall || item.Kind == CompositeInstructionKind.OtherIndirectControl) || instructions.Count(item => item.Kind == CompositeInstructionKind.ReadCr0) != 1 || instructions.Count(item => item.Kind == CompositeInstructionKind.ReadCr3) != 3 || instructions.Count(item => item.Kind == CompositeInstructionKind.WriteCr3) != 2 || instructions.Count(item => item.Kind == CompositeInstructionKind.ReadCr4) != 3 || instructions.Count(item => item.Kind == CompositeInstructionKind.WriteCr4) != 2 || instructions.Count(item => item.Kind == CompositeInstructionKind.Cpuid) != 2 || instructions.Count(item => item.Kind == CompositeInstructionKind.Rdmsr) != 1)
        {
            Console.WriteLine("M01_027_DECODE_STAGE opcode-census");
            return false;
        }

        int cliIndex = instructions.FindIndex(item => item.Kind == CompositeInstructionKind.Cli);
        bool hasPreCliTrapFlagGuard = instructions.Take(cliIndex).Any(item => (item.RegisterMask & (1UL << 8)) != 0 && (item.Immediate & RflagsTrapFlag) != 0);
        bool hasPreCliCetGuard = instructions.Take(cliIndex).Any(item => (item.RegisterMask & (1UL << 11)) != 0 && (item.Immediate & Cr4ControlFlowEnforcementBit) != 0);
        if (cliIndex <= 0 || !hasPreCliTrapFlagGuard || !hasPreCliCetGuard || !HasPreCliSameRootComparison(instructions, cliIndex))
        {
            Console.WriteLine("M01_027_DECODE_STAGE pre-cli-guards" + " cliIndex=" + cliIndex + " tf=" + hasPreCliTrapFlagGuard + " cet=" + hasPreCliCetGuard + " sameRoot=" + HasPreCliSameRootComparison(instructions, cliIndex));
            return false;
        }

        HashSet<ulong> reachableRuntimeFailures = instructions.Where(item => item.Kind == CompositeInstructionKind.MoveRegisterImmediate && item.Register is 0 or 12).Select(item => item.Immediate).ToHashSet();
        for (ulong status = (ulong)M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState; status <= (ulong)M01OwnedCr3CompositeTransitionRuntimeStatus.PostRestoreCanaryMismatch; status++)
        {
            if (status == (ulong)M01OwnedCr3CompositeTransitionRuntimeStatus.RflagsRestoreReadbackMismatch)
            {
                continue;
            }

            if (!reachableRuntimeFailures.Contains(status))
            {
                Console.WriteLine("M01_027_DECODE_STAGE unreachable-runtime-status" + " status=" + status);
                return false;
            }
        }

        if (reachableRuntimeFailures.Contains((ulong)M01OwnedCr3CompositeTransitionRuntimeStatus.RflagsRestoreReadbackMismatch))
        {
            Console.WriteLine("M01_027_DECODE_STAGE impossible-rflags-status");
            return false;
        }

        int callIndex = instructions.FindIndex(item => item.Kind == CompositeInstructionKind.IndirectCall);
        if (callIndex <= 0 || callIndex + 1 >= instructions.Count || instructions[callIndex - 1].Kind != CompositeInstructionKind.SubRsp || instructions[callIndex - 1].Immediate != M01OwnedCr3CompositeTransition.RequiredCallerShadowSpaceBytes || instructions[callIndex + 1].Kind != CompositeInstructionKind.AddRsp || instructions[callIndex + 1].Immediate != M01OwnedCr3CompositeTransition.RequiredCallerShadowSpaceBytes)
        {
            Console.WriteLine("M01_027_DECODE_STAGE call-shadow-space");
            return false;
        }

        if (instructions.Count < 3 || instructions[0].Kind != CompositeInstructionKind.Push || instructions[0].Register != 3 || instructions[1].Kind != CompositeInstructionKind.Push || instructions[1].Register != 6 || instructions[2].Kind != CompositeInstructionKind.Push || instructions[2].Register != 12)
        {
            Console.WriteLine("M01_027_DECODE_STAGE prologue");
            return false;
        }

        ulong forbiddenRegisterMask = (1UL << 5) | (1UL << 7) | (1UL << 13) | (1UL << 14) | (1UL << 15);
        if (instructions.Any(item => (item.RegisterMask & forbiddenRegisterMask) != 0))
        {
            Console.WriteLine("M01_027_DECODE_STAGE forbidden-register");
            return false;
        }

        if (!ValidateRflagsRestorationAndPreCliReturns(instructions))
        {
            Console.WriteLine("M01_027_DECODE_STAGE rflags-return-paths");
            return false;
        }

        int rdmsrIndex = instructions.FindIndex(item => item.Kind == CompositeInstructionKind.Rdmsr);
        if (rdmsrIndex < 0 || rdmsrIndex + 2 >= instructions.Count || !instructions[rdmsrIndex + 1].IsShiftLeftRdx32 || !instructions[rdmsrIndex + 2].IsOrRaxRdx || !instructions.Any(item => item.IsCr4PaeTest))
        {
            Console.WriteLine("M01_027_DECODE_STAGE mode-reconstruction");
            return false;
        }

        HashSet<uint> stateStores = instructions.Where(item => item.StateStoreOffset.HasValue).Select(item => item.StateStoreOffset!.Value).ToHashSet();
        uint[] requiredStores = [M01OwnedCr3CompositeTransition.StateOriginalRflagsOffset, M01OwnedCr3CompositeTransition.StateOriginalCr0Offset, M01OwnedCr3CompositeTransition.StateOriginalCr3RawOffset, M01OwnedCr3CompositeTransition.StateOriginalCr4RawOffset, M01OwnedCr3CompositeTransition.StateOriginalEferOffset, M01OwnedCr3CompositeTransition.StatePhysicalAddressMaskOffset, M01OwnedCr3CompositeTransition.StateEntryRspOffset, M01OwnedCr3CompositeTransition.StateObservedReturnAddressOffset, M01OwnedCr3CompositeTransition.StateObservedRipOffset, M01OwnedCr3CompositeTransition.StateCliRflagsReadbackOffset, M01OwnedCr3CompositeTransition.StateClearedCr4ReadbackOffset, M01OwnedCr3CompositeTransition.StateOwnedCr3ReadbackOffset, M01OwnedCr3CompositeTransition.StateCanaryBeforeOffset, M01OwnedCr3CompositeTransition.StateCanaryAfterOffset, M01OwnedCr3CompositeTransition.StateProbePml4eOffset, M01OwnedCr3CompositeTransition.StateProbePdpteOffset, M01OwnedCr3CompositeTransition.StateProbePdeOffset, M01OwnedCr3CompositeTransition.StateProbePteBeforeOffset, M01OwnedCr3CompositeTransition.StateProbePteAfterOffset, M01OwnedCr3CompositeTransition.StateInvlpgTargetOffset, M01OwnedCr3CompositeTransition.StateProbeReturnOffset, M01OwnedCr3CompositeTransition.StatePageZeroPml4eOffset, M01OwnedCr3CompositeTransition.StatePageZeroPdpteOffset, M01OwnedCr3CompositeTransition.StatePageZeroPdeOffset, M01OwnedCr3CompositeTransition.StatePageZeroPteOffset, M01OwnedCr3CompositeTransition.StateCommittedProbePteReadbackOffset, M01OwnedCr3CompositeTransition.StateFirmwareCr3ReadbackOffset, M01OwnedCr3CompositeTransition.StatePostRestoreCanaryOffset, M01OwnedCr3CompositeTransition.StateRestoredCr4Offset, M01OwnedCr3CompositeTransition.StateRestoredRflagsIntentOffset, M01OwnedCr3CompositeTransition.StateExitRspOffset, M01OwnedCr3CompositeTransition.StateInvlpgCountOffset, M01OwnedCr3CompositeTransition.StateProbeInvocationCountOffset, M01OwnedCr3CompositeTransition.StateRuntimeStatusOffset, M01OwnedCr3CompositeTransition.StateCheckpointOffset, M01OwnedCr3CompositeTransition.StatePageZeroLevelOffset, M01OwnedCr3CompositeTransition.StateMutationCommittedOffset, M01OwnedCr3CompositeTransition.StateOriginalPgeWasSetOffset, M01OwnedCr3CompositeTransition.StateOwnedRootWasLoadedOffset, M01OwnedCr3CompositeTransition.StateProbeReturnedOffset, M01OwnedCr3CompositeTransition.StateProgressCheckpointOffset];
        return requiredStores.All(stateStores.Contains);
    }

    private static bool TestObservedRuntimeAuditAndRecovery()
    {
        using CompositeFixture fixture = CompositeFixture.Create();
        M01OwnedCr3CompositeTransitionResult prepared = fixture.Execute();
        if (!prepared.IsStatePrepared)
        {
            Console.WriteLine("M01_027_AUDIT_PREPARE status=" + prepared.Status + " phase=" + prepared.Phase + " invalidIndex=" + prepared.InvalidIndex);
            return false;
        }

        using CompositePlanStorage storage = new((byte*)prepared.State.CompositeCodeStart);
        storage.Fill(0xa5);
        ulong entryAddress = prepared.State.CompositeCodeStart;
        M01OwnedCr3CompositeTransitionResult materialized = M01OwnedCr3CompositeTransition.TryMaterialize(fixture.StateDestination, 1, 1, storage.Bytes, M01OwnedCr3CompositeTransition.MaterializedPageByteLength, entryAddress, storage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (!materialized.IsMaterialized)
        {
            Console.WriteLine("M01_027_AUDIT_STAGE materialize" + " status=" + materialized.Status + " phase=" + materialized.Phase + " invalidIndex=" + materialized.InvalidIndex + " expected=" + materialized.ExpectedValue + " observed=" + materialized.ObservedValue);
            return false;
        }

        M01OwnedCr3CompositeTransitionResult readback = M01OwnedCr3CompositeTransition.ValidateReadback(fixture.StateDestination, 1, 1, storage.Bytes, M01OwnedCr3CompositeTransition.MaterializedPageByteLength, M01OwnedCr3CompositeTransition.MaterializedPageByteLength, entryAddress, storage.Semantics, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount, M01OwnedCr3CompositeTransition.AuditedSemanticRecordCount);
        if (!readback.IsReadbackVerified)
        {
            Console.WriteLine("M01_027_AUDIT_STAGE readback" + " status=" + readback.Status + " phase=" + readback.Phase + " invalidIndex=" + readback.InvalidIndex + " expected=" + readback.ExpectedValue + " observed=" + readback.ObservedValue);
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState observed = CreateCanonicalObservedState(fixture, readback.State);
        *fixture.StateDestination = observed;
        M01OwnedCr3CompositeTransitionResult success = AuditObserved(&readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success);
        if (!success.IsSuccess || success.Phase != M01OwnedCr3CompositeTransitionResultPhase.ObservedAudit || !success.IsObservedAudit || success.IsStatePrepared || success.IsMaterialized || success.IsReadbackVerified || success.ProvesCompositeEncodingReadback || success.TerminalKind != M01OwnedCr3CompositeTransitionTerminalKind.Returned)
        {
            Console.WriteLine("M01_027_AUDIT_STAGE canonical-success" + " status=" + success.Status + " phase=" + success.Phase + " terminal=" + success.TerminalKind + " success=" + success.IsSuccess + " audit=" + success.IsObservedAudit + " checkpoint=" + success.State.Checkpoint + " progress=" + success.State.ProgressCheckpoint + " expected=" + success.ExpectedValue + " observed=" + success.ObservedValue + " rflags=0x" + success.State.OriginalRflags.ToString("X16") + " cr0=0x" + success.State.OriginalCr0.ToString("X16") + " cr3=0x" + success.State.OriginalCr3Raw.ToString("X16") + " ownedCr3=0x" + success.State.OwnedCr3Raw.ToString("X16") + " cr4=0x" + success.State.OriginalCr4Raw.ToString("X16") + " efer=0x" + success.State.OriginalEfer.ToString("X16") + " physicalMask=0x" + success.State.PhysicalAddressMask.ToString("X16"));
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState interruptFailure = CreateInterruptDisableFailureState(fixture, readback.State);
        *fixture.StateDestination = interruptFailure;
        M01OwnedCr3CompositeTransitionResult interruptFailureAudit = AuditObserved(&readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.InterruptDisableReadbackMismatch);
        if (interruptFailureAudit.Status != M01OwnedCr3CompositeTransitionStatus.InterruptStateMismatch || interruptFailureAudit.State.ProgressCheckpoint != M01OwnedCr3CompositeTransitionCheckpoint.Uninitialized || interruptFailureAudit.State.Checkpoint != M01OwnedCr3CompositeTransitionCheckpoint.ReturnPrepared || interruptFailureAudit.State.ClearedCr4Readback != 0 || interruptFailureAudit.State.RestoredCr4Readback != 0 || interruptFailureAudit.State.RestoredRflagsIntent != interruptFailureAudit.State.OriginalRflags)
        {
            Console.WriteLine("M01_027_AUDIT_STAGE interrupt-disable-failure" + " status=" + interruptFailureAudit.Status + " checkpoint=" + interruptFailureAudit.State.Checkpoint + " progress=" + interruptFailureAudit.State.ProgressCheckpoint + " clearedCr4=0x" + interruptFailureAudit.State.ClearedCr4Readback.ToString("X16") + " restoredCr4=0x" + interruptFailureAudit.State.RestoredCr4Readback.ToString("X16") + " rflagsIntent=0x" + interruptFailureAudit.State.RestoredRflagsIntent.ToString("X16"));
            return false;
        }

        if (!TestAllPostCliFailureTranscripts(fixture, &readback))
        {
            Console.WriteLine("M01_027_AUDIT_STAGE post-cli-failure-transcripts");
            return false;
        }

        if (!TestCleanupOverrideStages(fixture, &readback))
        {
            Console.WriteLine("M01_027_AUDIT_STAGE cleanup-override-stages");
            return false;
        }

        if (!TestSuccessfulTranscriptHostileMutations(fixture, &readback, observed))
        {
            Console.WriteLine("M01_027_AUDIT_STAGE hostile-success-mutations");
            return false;
        }

        *fixture.StateDestination = observed;
        M01OwnedCr3CompositeTransitionResult defaultProof = default;
        M01OwnedCr3CompositeTransitionResult forgedHash = readback;
        OverwriteUlong(ref forgedHash, checked((int)Offset<M01OwnedCr3CompositeTransitionResult>("_fnv1a64")), readback.Fnv1a64 ^ 1);
        M01OwnedCr3CompositeTransitionRuntimeState baseline = readback.State;
        M01OwnedCr3CompositeTransitionResult legacy = M01OwnedCr3CompositeTransition.AuditObservedState(&baseline, 1, 1, fixture.StateDestination, 1, 1, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success);
        if (legacy.Status != M01OwnedCr3CompositeTransitionStatus.SeparateLeavesNotCompositeEvidence || legacy.IsSuccess || legacy.IsObservedAudit || AuditObserved(&defaultProof, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.SeparateLeavesNotCompositeEvidence || AuditObserved(&prepared, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.SeparateLeavesNotCompositeEvidence || AuditObserved(&materialized, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.SeparateLeavesNotCompositeEvidence || AuditObserved(&forgedHash, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.SeparateLeavesNotCompositeEvidence)
        {
            Console.WriteLine("M01_027_AUDIT_STAGE evidence-provenance-matrix" + " legacy=" + legacy.Status);
            return false;
        }

        byte* observedStorage = stackalloc byte[checked((int)M01OwnedCr3CompositeTransitionRuntimeState.StructureSize + 8)];
        M01OwnedCr3CompositeTransitionRuntimeState* misalignedObserved = (M01OwnedCr3CompositeTransitionRuntimeState*)(observedStorage + 1);
        bool invalidBoundaryMatrix = AuditObserved(&readback, null, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status == M01OwnedCr3CompositeTransitionStatus.MissingObservedState && AuditObserved(&readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Uninitialized, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status == M01OwnedCr3CompositeTransitionStatus.InvalidTerminalKind && AuditObserved(&readback, fixture.StateDestination, (M01OwnedCr3CompositeTransitionTerminalKind)0xff, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status == M01OwnedCr3CompositeTransitionStatus.InvalidTerminalKind && AuditObserved(&readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Timeout, M01OwnedCr3CompositeTransition.NoObservedRuntimeReturnValue).Status == M01OwnedCr3CompositeTransitionStatus.Timeout && AuditObserved(&readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Reset, M01OwnedCr3CompositeTransition.NoObservedRuntimeReturnValue).Status == M01OwnedCr3CompositeTransitionStatus.Reset && AuditObserved(&readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.TripleFault, M01OwnedCr3CompositeTransition.NoObservedRuntimeReturnValue).Status == M01OwnedCr3CompositeTransitionStatus.TripleFault && AuditObserved(&readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, M01OwnedCr3CompositeTransition.NoObservedRuntimeReturnValue).Status == M01OwnedCr3CompositeTransitionStatus.DefaultRuntimeResult && M01OwnedCr3CompositeTransition.AuditObservedState(&readback, 0, 1, fixture.StateDestination, 1, 1, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status == M01OwnedCr3CompositeTransitionStatus.SeparateLeavesNotCompositeEvidence && M01OwnedCr3CompositeTransition.AuditObservedState(&readback, 1, 0, fixture.StateDestination, 1, 1, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status == M01OwnedCr3CompositeTransitionStatus.SeparateLeavesNotCompositeEvidence && M01OwnedCr3CompositeTransition.AuditObservedState(&readback, 1, 1, fixture.StateDestination, 0, 1, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status == M01OwnedCr3CompositeTransitionStatus.InvalidObservedStateCount && M01OwnedCr3CompositeTransition.AuditObservedState(&readback, 1, 1, fixture.StateDestination, 1, 0, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status == M01OwnedCr3CompositeTransitionStatus.ObservedStateCapacityTooSmall && M01OwnedCr3CompositeTransition.AuditObservedState(&readback, 1, 1, misalignedObserved, 1, 1, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status == M01OwnedCr3CompositeTransitionStatus.MisalignedObservedState;
        if (!invalidBoundaryMatrix)
        {
            Console.WriteLine("M01_027_AUDIT_STAGE invalid-boundary-matrix");
        }

        return invalidBoundaryMatrix;
    }

    private static bool IsExpectedByteMismatchLocation(byte* canonicalBytes, ulong byteCount, ulong mutatedIndex, ulong invalidIndex)
    {
        if (invalidIndex == mutatedIndex)
        {
            return true;
        }

        if (invalidIndex > mutatedIndex || mutatedIndex - invalidIndex >= sizeof(uint) || invalidIndex > byteCount - sizeof(uint))
        {
            return false;
        }

        bool directBranchDisplacement = invalidIndex >= 1 && canonicalBytes[invalidIndex - 1] is 0xe8 or 0xe9;
        bool conditionalBranchDisplacement = invalidIndex >= 2 && canonicalBytes[invalidIndex - 2] == 0x0f && (canonicalBytes[invalidIndex - 1] & 0xf0) == 0x80;
        return directBranchDisplacement || conditionalBranchDisplacement;
    }

    private static M01OwnedCr3CompositeTransitionResult AuditObserved(M01OwnedCr3CompositeTransitionResult* readbackEvidence, M01OwnedCr3CompositeTransitionRuntimeState* observed, M01OwnedCr3CompositeTransitionTerminalKind terminalKind, uint observedRuntimeReturnValue)
    {
        return M01OwnedCr3CompositeTransition.AuditObservedState(readbackEvidence, 1, 1, observed, observed == null ? 0UL : 1UL, observed == null ? 0UL : 1UL, terminalKind, observedRuntimeReturnValue);
    }

    private static M01OwnedCr3CompositeTransitionRuntimeState CreateCanonicalObservedState(CompositeFixture fixture, M01OwnedCr3CompositeTransitionRuntimeState prepared)
    {
        X64PagingModePreflightResult preflight = fixture.PriorEvidence.Evidence.Preflight;
        (ulong probePml4, ulong probePdpt, ulong probePd, ulong probePt) = fixture.ProbeWalkEntryIndexes();
        (ulong pageZeroPml4e, ulong pageZeroPdpte, ulong pageZeroPde, ulong pageZeroPte, M01OwnedCr3CompositeTransitionPageZeroLevel pageZeroLevel) = fixture.PageZeroObservation();
        ulong entryRsp = fixture.Bindings[1].VirtualEndExclusive.TryGetRawValueForUefiBoundary(out ulong stackEnd) == AddressValueStatus.Success ? stackEnd - M01OwnedCr3CompositeTransition.GateToCompositeEntryRspDelta - M01OwnedCr3CompositeTransition.CallGateRequiredUpperFrameBytes : 0;
        ulong clearedCr4 = preflight.RawCr4 & ~M01OwnedCr3CompositeTransition.Cr4PageGlobalEnableBit;
        ulong cliRflags = preflight.Rflags & ~M01OwnedCr3CompositeTransition.RflagsInterruptEnableBit;
        return M01OwnedCr3CompositeTransitionRuntimeState.CreateObserved(prepared, preflight.Rflags, preflight.Cr0 | 1UL, preflight.RawFirmwareCr3, preflight.RawCr4, preflight.Ia32Efer, preflight.PhysicalAddressMask, entryRsp, prepared.ExpectedReturnAddress, prepared.CompositeCodeStart, cliRflags, clearedCr4, prepared.OwnedCr3Raw, prepared.ExpectedCanary, prepared.ReplacementCanary, fixture.PageEntries[probePml4], fixture.PageEntries[probePdpt], fixture.PageEntries[probePd], fixture.PageEntries[probePt], prepared.FinalProbePteRaw, prepared.ProbeEntry, prepared.ExpectedProbeReturn, pageZeroPml4e, pageZeroPdpte, pageZeroPde, pageZeroPte, prepared.FinalProbePteRaw, preflight.RawFirmwareCr3, prepared.ReplacementCanary, preflight.RawCr4, preflight.Rflags, entryRsp, 1, 1, 0, M01OwnedCr3CompositeTransitionRuntimeStatus.Success, M01OwnedCr3CompositeTransitionCheckpoint.ReturnPrepared, pageZeroLevel, mutationCommitted: true, pgeWasCleared: true, ownedRootWasLoaded: true, probeReturned: true, progressCheckpoint: M01OwnedCr3CompositeTransitionCheckpoint.PageZeroAbsent);
    }

    private static M01OwnedCr3CompositeTransitionRuntimeState CreateInterruptDisableFailureState(CompositeFixture fixture, M01OwnedCr3CompositeTransitionRuntimeState prepared)
    {
        X64PagingModePreflightResult preflight = fixture.PriorEvidence.Evidence.Preflight;
        ulong entryRsp = fixture.Bindings[1].VirtualEndExclusive.TryGetRawValueForUefiBoundary(out ulong stackEnd) == AddressValueStatus.Success ? stackEnd - 72 : 0;
        return M01OwnedCr3CompositeTransitionRuntimeState.CreateObserved(initializedState: prepared, originalRflags: preflight.Rflags, originalCr0: preflight.Cr0 | 1UL, originalCr3Raw: preflight.RawFirmwareCr3, originalCr4Raw: preflight.RawCr4, originalEfer: preflight.Ia32Efer, physicalAddressMask: preflight.PhysicalAddressMask, entryRsp: entryRsp, observedReturnAddress: prepared.ExpectedReturnAddress, observedRip: prepared.CompositeCodeStart, // A set IF bit is the exact failed CLI readback.
        cliRflagsReadback: preflight.Rflags, clearedCr4Readback: 0, ownedCr3Readback: 0, canaryBefore: 0, canaryAfter: 0, probePml4e: 0, probePdpte: 0, probePde: 0, probePteBefore: 0, probePteAfter: 0, invlpgTarget: 0, probeReturn: 0, pageZeroPml4e: 0, pageZeroPdpte: 0, pageZeroPde: 0, pageZeroPte: 0, committedProbePteReadback: 0, firmwareCr3Readback: 0, postRestoreCanaryReadback: 0, restoredCr4Readback: 0, restoredRflagsIntent: preflight.Rflags, exitRsp: entryRsp, invlpgCount: 0, probeInvocationCount: 0, firmwareCallbackCount: 0, runtimeStatus: M01OwnedCr3CompositeTransitionRuntimeStatus.InterruptDisableReadbackMismatch, checkpoint: M01OwnedCr3CompositeTransitionCheckpoint.ReturnPrepared, pageZeroLevel: M01OwnedCr3CompositeTransitionPageZeroLevel.Uninitialized, mutationCommitted: false, pgeWasCleared: false, ownedRootWasLoaded: false, probeReturned: false, progressCheckpoint: M01OwnedCr3CompositeTransitionCheckpoint.Uninitialized);
    }

    private static bool TestAllPostCliFailureTranscripts(CompositeFixture fixture, M01OwnedCr3CompositeTransitionResult* readback)
    {
        FailureExpectation[] expectations = [new(M01OwnedCr3CompositeTransitionRuntimeStatus.InterruptDisableReadbackMismatch, M01OwnedCr3CompositeTransitionStatus.InterruptStateMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.PgeClearReadbackMismatch, M01OwnedCr3CompositeTransitionStatus.RawCr4BitLoss), new(M01OwnedCr3CompositeTransitionRuntimeStatus.OwnedCr3ReadbackMismatch, M01OwnedCr3CompositeTransitionStatus.RawCr3BitLoss), new(M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryBeforeMismatch, M01OwnedCr3CompositeTransitionStatus.CanaryMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryAfterMismatch, M01OwnedCr3CompositeTransitionStatus.CanaryMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeHierarchyMalformed, M01OwnedCr3CompositeTransitionStatus.ProbePteAddressMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAddressMismatch, M01OwnedCr3CompositeTransitionStatus.ProbePteAddressMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteBeforeMismatch, M01OwnedCr3CompositeTransitionStatus.ProbePteReadbackMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAfterMismatch, M01OwnedCr3CompositeTransitionStatus.ProbePteReadbackMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeReturnMismatch, M01OwnedCr3CompositeTransitionStatus.ProbeMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.PageZeroPresent, M01OwnedCr3CompositeTransitionStatus.PageZeroPresent), new(M01OwnedCr3CompositeTransitionRuntimeStatus.PageZeroMalformed, M01OwnedCr3CompositeTransitionStatus.PageZeroMalformed), new(M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch, M01OwnedCr3CompositeTransitionStatus.FirmwareCr3ReadbackMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.Cr4RestoreReadbackMismatch, M01OwnedCr3CompositeTransitionStatus.Cr4RestoreReadbackMismatch), new(M01OwnedCr3CompositeTransitionRuntimeStatus.RspRestoreMismatch, M01OwnedCr3CompositeTransitionStatus.StackEscape), new(M01OwnedCr3CompositeTransitionRuntimeStatus.PostRestoreCanaryMismatch, M01OwnedCr3CompositeTransitionStatus.CanaryMismatch)];
        foreach (FailureExpectation expectation in expectations)
        {
            M01OwnedCr3CompositeTransitionRuntimeState transcript = CreatePostCliFailureState(fixture, readback->State, expectation.RuntimeStatus);
            *fixture.StateDestination = transcript;
            M01OwnedCr3CompositeTransitionResult audit = AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)expectation.RuntimeStatus);
            if (audit.Status != expectation.ExpectedStatus)
            {
                Console.WriteLine("M01_027_FAILURE_TRANSCRIPT " + expectation.RuntimeStatus + " observed=" + audit.Status + " expected=" + expectation.ExpectedStatus);
                return false;
            }

            M01OwnedCr3CompositeTransitionRuntimeState forgedCause = transcript;
            RemoveReportedFailureCause(fixture, ref forgedCause, expectation.RuntimeStatus);
            *fixture.StateDestination = forgedCause;
            M01OwnedCr3CompositeTransitionResult forgedCauseAudit = AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)expectation.RuntimeStatus);
            if (forgedCauseAudit.Status == expectation.ExpectedStatus)
            {
                Console.WriteLine("M01_027_FORGED_FAILURE_ACCEPTED " + expectation.RuntimeStatus);
                return false;
            }

            M01OwnedCr3CompositeTransitionRuntimeState wrongProgress = transcript;
            SetStateByte(ref wrongProgress, M01OwnedCr3CompositeTransition.StateProgressCheckpointOffset, 0xff);
            *fixture.StateDestination = wrongProgress;
            if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)expectation.RuntimeStatus).Status != M01OwnedCr3CompositeTransitionStatus.CheckpointMismatch)
            {
                return false;
            }

            M01OwnedCr3CompositeTransitionRuntimeState wrongIntent = transcript;
            OverwriteUlong(ref wrongIntent, checked((int)M01OwnedCr3CompositeTransition.StateRestoredRflagsIntentOffset), 0);
            *fixture.StateDestination = wrongIntent;
            if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)expectation.RuntimeStatus).Status != M01OwnedCr3CompositeTransitionStatus.RflagsRestoreIntentMismatch)
            {
                return false;
            }

            M01OwnedCr3CompositeTransitionRuntimeState wrongStatus = transcript;
            SetStateByte(ref wrongStatus, M01OwnedCr3CompositeTransition.StateRuntimeStatusOffset, (byte)M01OwnedCr3CompositeTransitionRuntimeStatus.Success);
            *fixture.StateDestination = wrongStatus;
            if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)expectation.RuntimeStatus).Status != M01OwnedCr3CompositeTransitionStatus.RuntimeReportedFailure)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestCleanupOverrideStages(CompositeFixture fixture, M01OwnedCr3CompositeTransitionResult* readback)
    {
        (string Name, M01OwnedCr3CompositeTransitionRuntimeStatus Primary, M01OwnedCr3CompositeTransitionRuntimeStatus Override, M01OwnedCr3CompositeTransitionStatus Expected, bool IsLate)[] cases = [("firmware-cr3-early", M01OwnedCr3CompositeTransitionRuntimeStatus.OwnedCr3ReadbackMismatch, M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch, M01OwnedCr3CompositeTransitionStatus.FirmwareCr3ReadbackMismatch, false), ("firmware-cr3-late", M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch, M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch, M01OwnedCr3CompositeTransitionStatus.FirmwareCr3ReadbackMismatch, true), ("cr4-early", M01OwnedCr3CompositeTransitionRuntimeStatus.PgeClearReadbackMismatch, M01OwnedCr3CompositeTransitionRuntimeStatus.Cr4RestoreReadbackMismatch, M01OwnedCr3CompositeTransitionStatus.Cr4RestoreReadbackMismatch, false), ("cr4-late", M01OwnedCr3CompositeTransitionRuntimeStatus.Cr4RestoreReadbackMismatch, M01OwnedCr3CompositeTransitionRuntimeStatus.Cr4RestoreReadbackMismatch, M01OwnedCr3CompositeTransitionStatus.Cr4RestoreReadbackMismatch, true), ("rsp-early", M01OwnedCr3CompositeTransitionRuntimeStatus.InterruptDisableReadbackMismatch, M01OwnedCr3CompositeTransitionRuntimeStatus.RspRestoreMismatch, M01OwnedCr3CompositeTransitionStatus.StackEscape, false), ("rsp-late", M01OwnedCr3CompositeTransitionRuntimeStatus.RspRestoreMismatch, M01OwnedCr3CompositeTransitionRuntimeStatus.RspRestoreMismatch, M01OwnedCr3CompositeTransitionStatus.StackEscape, true)];
        foreach (var cleanupCase in cases)
        {
            M01OwnedCr3CompositeTransitionRuntimeState transcript = CreatePostCliFailureState(fixture, readback->State, cleanupCase.Primary);
            SetStateByte(ref transcript, M01OwnedCr3CompositeTransition.StateRuntimeStatusOffset, (byte)cleanupCase.Override);
            if (!cleanupCase.IsLate)
            {
                switch (cleanupCase.Override)
                {
                    case M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch:
                        OverwriteUlong(ref transcript, checked((int)M01OwnedCr3CompositeTransition.StateFirmwareCr3ReadbackOffset), transcript.OwnedCr3Raw);
                        OverwriteUlong(ref transcript, checked((int)M01OwnedCr3CompositeTransition.StatePostRestoreCanaryOffset), 0);
                        break;
                    case M01OwnedCr3CompositeTransitionRuntimeStatus.Cr4RestoreReadbackMismatch:
                        OverwriteUlong(ref transcript, checked((int)M01OwnedCr3CompositeTransition.StateRestoredCr4Offset), transcript.OriginalCr4Raw ^ 1UL);
                        break;
                    case M01OwnedCr3CompositeTransitionRuntimeStatus.RspRestoreMismatch:
                        OverwriteUlong(ref transcript, checked((int)M01OwnedCr3CompositeTransition.StateExitRspOffset), transcript.EntryRsp + 8);
                        break;
                }
            }

            *fixture.StateDestination = transcript;
            M01OwnedCr3CompositeTransitionResult audit = AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)cleanupCase.Override);
            if (audit.Status != cleanupCase.Expected)
            {
                Console.WriteLine("M01_027_CLEANUP_OVERRIDE name=" + cleanupCase.Name + " observed=" + audit.Status + " expected=" + cleanupCase.Expected);
                return false;
            }

            // Keep this a single-defect checkpoint test. RSP restore
            // transcripts intentionally retain ExitRsp != EntryRsp, and
            // StackEscape has precedence over a simultaneous checkpoint
            // mismatch.
            if (cleanupCase.Override != M01OwnedCr3CompositeTransitionRuntimeStatus.RspRestoreMismatch)
            {
                M01OwnedCr3CompositeTransitionRuntimeState wrongFinalCheckpoint = transcript;
                SetStateByte(ref wrongFinalCheckpoint, M01OwnedCr3CompositeTransition.StateCheckpointOffset, (byte)M01OwnedCr3CompositeTransitionCheckpoint.Cr4Restored);
                *fixture.StateDestination = wrongFinalCheckpoint;
                M01OwnedCr3CompositeTransitionStatus wrongFinalCheckpointStatus = AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)cleanupCase.Override).Status;
                if (wrongFinalCheckpointStatus != M01OwnedCr3CompositeTransitionStatus.CheckpointMismatch)
                {
                    Console.WriteLine("M01_027_CLEANUP_CHECKPOINT name=" + cleanupCase.Name + " observed=" + wrongFinalCheckpointStatus + " expected=" + M01OwnedCr3CompositeTransitionStatus.CheckpointMismatch);
                    return false;
                }
            }

            M01OwnedCr3CompositeTransitionRuntimeState junk = transcript;
            uint junkOffset;
            ulong junkValue;
            if (cleanupCase.IsLate && cleanupCase.Override == M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch)
            {
                junkOffset = M01OwnedCr3CompositeTransition.StatePostRestoreCanaryOffset;
                junkValue = transcript.ReplacementCanary;
            }
            else if (cleanupCase.IsLate)
            {
                junkOffset = M01OwnedCr3CompositeTransition.StateProbeInvocationCountOffset;
                junkValue = 2;
            }
            else if (cleanupCase.Override == M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch)
            {
                junkOffset = M01OwnedCr3CompositeTransition.StateCanaryBeforeOffset;
                junkValue = transcript.ExpectedCanary;
            }
            else if (cleanupCase.Override == M01OwnedCr3CompositeTransitionRuntimeStatus.Cr4RestoreReadbackMismatch)
            {
                junkOffset = M01OwnedCr3CompositeTransition.StateOwnedCr3ReadbackOffset;
                junkValue = transcript.OwnedCr3Raw;
            }
            else
            {
                junkOffset = M01OwnedCr3CompositeTransition.StateClearedCr4ReadbackOffset;
                junkValue = transcript.OriginalCr4Raw;
            }

            OverwriteUlong(ref junk, checked((int)junkOffset), junkValue);
            *fixture.StateDestination = junk;
            if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)cleanupCase.Override).Status == cleanupCase.Expected)
            {
                Console.WriteLine("M01_027_CLEANUP_JUNK_ACCEPTED name=" + cleanupCase.Name + " offset=" + junkOffset);
                return false;
            }
        }

        return true;
    }

    private static bool TestSuccessfulTranscriptHostileMutations(CompositeFixture fixture, M01OwnedCr3CompositeTransitionResult* readback, M01OwnedCr3CompositeTransitionRuntimeState canonical)
    {
        (uint Offset, ulong Value, M01OwnedCr3CompositeTransitionStatus Expected)[] mutations = [(M01OwnedCr3CompositeTransition.StateOwnedCr3RawOffset, canonical.OwnedCr3Raw + M01OwnedCr3CompositeTransition.PageSize, M01OwnedCr3CompositeTransitionStatus.InvalidProbeContract), (M01OwnedCr3CompositeTransition.StateOriginalRflagsOffset, 0, M01OwnedCr3CompositeTransitionStatus.ModePrerequisiteMismatch), (M01OwnedCr3CompositeTransition.StateRestoredRflagsIntentOffset, 0, M01OwnedCr3CompositeTransitionStatus.RflagsRestoreIntentMismatch), (M01OwnedCr3CompositeTransition.StateFirmwareCr3ReadbackOffset, canonical.OriginalCr3Raw ^ 0x1000, M01OwnedCr3CompositeTransitionStatus.FirmwareCr3ReadbackMismatch), (M01OwnedCr3CompositeTransition.StateRestoredCr4Offset, canonical.OriginalCr4Raw ^ 1, M01OwnedCr3CompositeTransitionStatus.Cr4RestoreReadbackMismatch), (M01OwnedCr3CompositeTransition.StateOriginalCr0Offset, 0, M01OwnedCr3CompositeTransitionStatus.ModePrerequisiteMismatch), (M01OwnedCr3CompositeTransition.StateOwnedCr3ReadbackOffset, canonical.OwnedCr3Raw ^ 0x1000, M01OwnedCr3CompositeTransitionStatus.RawCr3BitLoss), (M01OwnedCr3CompositeTransition.StateClearedCr4ReadbackOffset, canonical.OriginalCr4Raw, M01OwnedCr3CompositeTransitionStatus.RawCr4BitLoss), (M01OwnedCr3CompositeTransition.StateCliRflagsReadbackOffset, canonical.CliRflagsReadback | M01OwnedCr3CompositeTransition.RflagsInterruptEnableBit, M01OwnedCr3CompositeTransitionStatus.InterruptStateMismatch), (M01OwnedCr3CompositeTransition.StateExitRspOffset, canonical.EntryRsp + 8, M01OwnedCr3CompositeTransitionStatus.StackEscape), (M01OwnedCr3CompositeTransition.StateObservedReturnAddressOffset, canonical.ExpectedReturnAddress + 8, M01OwnedCr3CompositeTransitionStatus.ReturnAddressMismatch), (M01OwnedCr3CompositeTransition.StateCanaryBeforeOffset, canonical.ExpectedCanary ^ 1, M01OwnedCr3CompositeTransitionStatus.CanaryMismatch), (M01OwnedCr3CompositeTransition.StateCanaryAfterOffset, canonical.ReplacementCanary ^ 1, M01OwnedCr3CompositeTransitionStatus.CanaryMismatch), (M01OwnedCr3CompositeTransition.StatePostRestoreCanaryOffset, canonical.ReplacementCanary ^ 1, M01OwnedCr3CompositeTransitionStatus.CanaryMismatch), (M01OwnedCr3CompositeTransition.StateProbePml4eOffset, 1, M01OwnedCr3CompositeTransitionStatus.ProbePteAddressMismatch), (M01OwnedCr3CompositeTransition.StateProbePteBeforeOffset, canonical.InitialProbePteRaw ^ 0x1000, M01OwnedCr3CompositeTransitionStatus.ProbePteReadbackMismatch), (M01OwnedCr3CompositeTransition.StateProbePteAfterOffset, canonical.FinalProbePteRaw | M01OwnedCr3CompositeTransition.PageWritableBit, M01OwnedCr3CompositeTransitionStatus.ProbePteReadbackMismatch), (M01OwnedCr3CompositeTransition.StateCommittedProbePteReadbackOffset, canonical.FinalProbePteRaw | M01OwnedCr3CompositeTransition.PageNoExecuteBit, M01OwnedCr3CompositeTransitionStatus.ProbePteReadbackMismatch), (M01OwnedCr3CompositeTransition.StateInvlpgTargetOffset, canonical.ProbeEntry + M01OwnedCr3CompositeTransition.PageSize, M01OwnedCr3CompositeTransitionStatus.InvlpgMismatch), (M01OwnedCr3CompositeTransition.StateInvlpgCountOffset, 2, M01OwnedCr3CompositeTransitionStatus.InvlpgMismatch), (M01OwnedCr3CompositeTransition.StateProbeReturnOffset, canonical.ExpectedProbeReturn ^ 1UL, M01OwnedCr3CompositeTransitionStatus.ProbeMismatch), (M01OwnedCr3CompositeTransition.StateProbeInvocationCountOffset, 2, M01OwnedCr3CompositeTransitionStatus.ProbeMismatch)];
        foreach (var mutation in mutations)
        {
            M01OwnedCr3CompositeTransitionRuntimeState hostile = canonical;
            OverwriteUlong(ref hostile, checked((int)mutation.Offset), mutation.Value);
            *fixture.StateDestination = hostile;
            M01OwnedCr3CompositeTransitionResult audit = AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success);
            if (audit.Status != mutation.Expected)
            {
                Console.WriteLine("M01_027_HOSTILE_FIELD offset=" + mutation.Offset + " observed=" + audit.Status + " expected=" + mutation.Expected);
                return false;
            }
        }

        M01OwnedCr3CompositeTransitionRuntimeState arithmeticFlagsChanged = canonical;
        ulong realisticCliRflags = canonical.CliRflagsReadback ^ RflagsArithmeticMask;
        OverwriteUlong(ref arithmeticFlagsChanged, checked((int)M01OwnedCr3CompositeTransition.StateCliRflagsReadbackOffset), realisticCliRflags);
        *fixture.StateDestination = arithmeticFlagsChanged;
        if (realisticCliRflags == canonical.CliRflagsReadback || (realisticCliRflags & ~RflagsArithmeticMask) != (canonical.CliRflagsReadback & ~RflagsArithmeticMask) || (realisticCliRflags & M01OwnedCr3CompositeTransition.RflagsInterruptEnableBit) != 0 || !AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).IsSuccess)
        {
            return false;
        }

        (ulong Value, string Name)[] invalidCliReadbacks = [(canonical.CliRflagsReadback & ~RflagsAlwaysOneBit, "reserved-bit-one-cleared"), (canonical.CliRflagsReadback | RflagsTrapFlag, "trap-flag-set"), (canonical.CliRflagsReadback ^ RflagsAlignmentCheckFlag, "stable-alignment-check-mutated")];
        foreach (var invalidCli in invalidCliReadbacks)
        {
            M01OwnedCr3CompositeTransitionRuntimeState hostile = canonical;
            OverwriteUlong(ref hostile, checked((int)M01OwnedCr3CompositeTransition.StateCliRflagsReadbackOffset), invalidCli.Value);
            *fixture.StateDestination = hostile;
            M01OwnedCr3CompositeTransitionResult audit = AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success);
            if (audit.Status != M01OwnedCr3CompositeTransitionStatus.InterruptStateMismatch)
            {
                Console.WriteLine("M01_027_CLI_INVARIANT_ACCEPTED name=" + invalidCli.Name + " observed=" + audit.Status);
                return false;
            }
        }

        M01OwnedCr3CompositeTransitionRuntimeState callback = canonical;
        SetStateUInt32(ref callback, M01OwnedCr3CompositeTransition.StateFirmwareCallbackCountOffset, 1);
        *fixture.StateDestination = callback;
        if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.FirmwareCallbackObserved)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState reserved = canonical;
        OverwriteUlong(ref reserved, checked((int)M01OwnedCr3CompositeTransition.StateReservedOutput1Offset), 1);
        *fixture.StateDestination = reserved;
        if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.ReservedStateNonzero)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState invalidBoolean = canonical;
        SetStateByte(ref invalidBoolean, M01OwnedCr3CompositeTransition.StateMutationCommittedOffset, 2);
        *fixture.StateDestination = invalidBoolean;
        if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.ReservedStateNonzero)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState monotonicAd = canonical;
        const ulong AccessedDirty = M01OwnedCr3CompositeTransition.PageAccessedBit | M01OwnedCr3CompositeTransition.PageDirtyBit;
        OverwriteUlong(ref monotonicAd, checked((int)M01OwnedCr3CompositeTransition.StateProbePteBeforeOffset), canonical.ProbePteBefore | AccessedDirty);
        OverwriteUlong(ref monotonicAd, checked((int)M01OwnedCr3CompositeTransition.StateProbePteAfterOffset), canonical.ProbePteAfter | AccessedDirty);
        OverwriteUlong(ref monotonicAd, checked((int)M01OwnedCr3CompositeTransition.StateCommittedProbePteReadbackOffset), canonical.CommittedProbePteReadback | AccessedDirty);
        *fixture.StateDestination = monotonicAd;
        if (!AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).IsSuccess)
        {
            return false;
        }

        if ((canonical.FinalProbePteRaw & AccessedDirty) != 0)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState independentlyToleratedButUnequalPte = canonical;
        OverwriteUlong(ref independentlyToleratedButUnequalPte, checked((int)M01OwnedCr3CompositeTransition.StateProbePteAfterOffset), canonical.FinalProbePteRaw | M01OwnedCr3CompositeTransition.PageAccessedBit);
        OverwriteUlong(ref independentlyToleratedButUnequalPte, checked((int)M01OwnedCr3CompositeTransition.StateCommittedProbePteReadbackOffset), canonical.FinalProbePteRaw | M01OwnedCr3CompositeTransition.PageDirtyBit);
        *fixture.StateDestination = independentlyToleratedButUnequalPte;
        if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.ProbePteReadbackMismatch)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionResult adBaselineProof = *readback;
        int resultStateOffset = checked((int)Offset<M01OwnedCr3CompositeTransitionResult>("_state"));
        ulong initialWithAd = canonical.InitialProbePteRaw | AccessedDirty;
        ulong finalWithAd = canonical.FinalProbePteRaw | AccessedDirty;
        OverwriteUlong(ref adBaselineProof, checked(resultStateOffset + (int)M01OwnedCr3CompositeTransition.StateInitialProbePteRawOffset), initialWithAd);
        OverwriteUlong(ref adBaselineProof, checked(resultStateOffset + (int)M01OwnedCr3CompositeTransition.StateFinalProbePteRawOffset), finalWithAd);
        M01OwnedCr3CompositeTransitionRuntimeState clearedAd = canonical;
        OverwriteUlong(ref clearedAd, checked((int)M01OwnedCr3CompositeTransition.StateInitialProbePteRawOffset), initialWithAd);
        OverwriteUlong(ref clearedAd, checked((int)M01OwnedCr3CompositeTransition.StateFinalProbePteRawOffset), finalWithAd);
        OverwriteUlong(ref clearedAd, checked((int)M01OwnedCr3CompositeTransition.StateProbePteBeforeOffset), initialWithAd & ~M01OwnedCr3CompositeTransition.PageAccessedBit);
        OverwriteUlong(ref clearedAd, checked((int)M01OwnedCr3CompositeTransition.StateProbePteAfterOffset), finalWithAd & ~M01OwnedCr3CompositeTransition.PageAccessedBit);
        OverwriteUlong(ref clearedAd, checked((int)M01OwnedCr3CompositeTransition.StateCommittedProbePteReadbackOffset), finalWithAd & ~M01OwnedCr3CompositeTransition.PageAccessedBit);
        *fixture.StateDestination = clearedAd;
        if (AuditObserved(&adBaselineProof, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.ProbePteReadbackMismatch)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState trapFlag = canonical;
        ulong trapRflags = canonical.OriginalRflags | RflagsTrapFlag;
        OverwriteUlong(ref trapFlag, checked((int)M01OwnedCr3CompositeTransition.StateOriginalRflagsOffset), trapRflags);
        OverwriteUlong(ref trapFlag, checked((int)M01OwnedCr3CompositeTransition.StateCliRflagsReadbackOffset), trapRflags & ~M01OwnedCr3CompositeTransition.RflagsInterruptEnableBit);
        OverwriteUlong(ref trapFlag, checked((int)M01OwnedCr3CompositeTransition.StateRestoredRflagsIntentOffset), trapRflags);
        *fixture.StateDestination = trapFlag;
        if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.ModePrerequisiteMismatch)
        {
            return false;
        }

        M01OwnedCr3CompositeTransitionRuntimeState cetEnabled = canonical;
        ulong cetCr4 = canonical.OriginalCr4Raw | Cr4ControlFlowEnforcementBit;
        OverwriteUlong(ref cetEnabled, checked((int)M01OwnedCr3CompositeTransition.StateOriginalCr4RawOffset), cetCr4);
        OverwriteUlong(ref cetEnabled, checked((int)M01OwnedCr3CompositeTransition.StateClearedCr4ReadbackOffset), cetCr4 & ~M01OwnedCr3CompositeTransition.Cr4PageGlobalEnableBit);
        OverwriteUlong(ref cetEnabled, checked((int)M01OwnedCr3CompositeTransition.StateRestoredCr4Offset), cetCr4);
        *fixture.StateDestination = cetEnabled;
        if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.ModePrerequisiteMismatch)
        {
            return false;
        }

        ulong physicalPageMask = canonical.PhysicalAddressMask & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask;
        ulong sameRootFirmwareCr3 = (canonical.OwnedCr3Raw & physicalPageMask) | (canonical.OriginalCr3Raw & M01OwnedCr3CompositeTransition.Cr3PermittedLowBitsMask);
        M01OwnedCr3CompositeTransitionRuntimeState sameRoot = canonical;
        OverwriteUlong(ref sameRoot, checked((int)M01OwnedCr3CompositeTransition.StateOriginalCr3RawOffset), sameRootFirmwareCr3);
        OverwriteUlong(ref sameRoot, checked((int)M01OwnedCr3CompositeTransition.StateFirmwareCr3ReadbackOffset), sameRootFirmwareCr3);
        *fixture.StateDestination = sameRoot;
        if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).Status != M01OwnedCr3CompositeTransitionStatus.ModePrerequisiteMismatch)
        {
            return false;
        }

        ulong pageMask = canonical.PhysicalAddressMask & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask;
        ulong currentPdpt = canonical.ProbePml4e & pageMask;
        ulong alternatePdpt = canonical.TablePageArenaStart;
        if (alternatePdpt == currentPdpt)
        {
            alternatePdpt += M01OwnedCr3CompositeTransition.PageSize;
        }

        M01OwnedCr3CompositeTransitionRuntimeState unrelatedChain = canonical;
        OverwriteUlong(ref unrelatedChain, checked((int)M01OwnedCr3CompositeTransition.StateProbePml4eOffset), alternatePdpt | M01OwnedCr3CompositeTransition.PagePresentBit | M01OwnedCr3CompositeTransition.PageWritableBit);
        *fixture.StateDestination = unrelatedChain;
        if (AuditObserved(readback, fixture.StateDestination, M01OwnedCr3CompositeTransitionTerminalKind.Returned, (uint)M01OwnedCr3CompositeTransitionRuntimeStatus.Success).IsSuccess)
        {
            Console.WriteLine("M01_027_UNRELATED_PARENT_CHAIN_ACCEPTED");
            return false;
        }

        return true;
    }

    private static M01OwnedCr3CompositeTransitionRuntimeState CreatePostCliFailureState(CompositeFixture fixture, M01OwnedCr3CompositeTransitionRuntimeState prepared, M01OwnedCr3CompositeTransitionRuntimeStatus status)
    {
        X64PagingModePreflightResult preflight = fixture.PriorEvidence.Evidence.Preflight;
        (ulong probePml4, ulong probePdpt, ulong probePd, ulong probePt) = fixture.ProbeWalkEntryIndexes();
        ulong pml4e = fixture.PageEntries[probePml4];
        ulong pdpte = fixture.PageEntries[probePdpt];
        ulong pde = fixture.PageEntries[probePd];
        ulong initialPte = fixture.PageEntries[probePt];
        ulong entryRsp = fixture.Bindings[1].VirtualEndExclusive.TryGetRawValueForUefiBoundary(out ulong stackEnd) == AddressValueStatus.Success ? stackEnd - 72 : 0;
        ulong cliRflags = preflight.Rflags & ~M01OwnedCr3CompositeTransition.RflagsInterruptEnableBit;
        ulong clearedCr4 = preflight.RawCr4 & ~M01OwnedCr3CompositeTransition.Cr4PageGlobalEnableBit;
        bool pgeAttempted = status != M01OwnedCr3CompositeTransitionRuntimeStatus.InterruptDisableReadbackMismatch;
        bool pgeSucceeded = pgeAttempted && status != M01OwnedCr3CompositeTransitionRuntimeStatus.PgeClearReadbackMismatch;
        bool ownedAttempted = pgeSucceeded;
        bool ownedSucceeded = ownedAttempted && status != M01OwnedCr3CompositeTransitionRuntimeStatus.OwnedCr3ReadbackMismatch;
        bool canaryRead = ownedSucceeded;
        bool canaryUpdated = canaryRead && status != M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryBeforeMismatch;
        bool canarySucceeded = canaryUpdated && status != M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryAfterMismatch;
        bool hierarchyAttempted = canarySucceeded;
        bool hierarchySucceeded = hierarchyAttempted && status != M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeHierarchyMalformed;
        bool pteLocated = hierarchySucceeded && status != M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAddressMismatch;
        bool pteBeforeValidated = pteLocated && status != M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteBeforeMismatch;
        bool invalidated = pteBeforeValidated;
        bool pteVerified = invalidated && status != M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAfterMismatch;
        bool probeInvoked = pteVerified;
        bool probeSucceeded = probeInvoked && status != M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeReturnMismatch;
        bool pageZeroAttempted = probeSucceeded;
        bool pageZeroAbsent = status == M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch || status == M01OwnedCr3CompositeTransitionRuntimeStatus.Cr4RestoreReadbackMismatch || status == M01OwnedCr3CompositeTransitionRuntimeStatus.RspRestoreMismatch || status == M01OwnedCr3CompositeTransitionRuntimeStatus.PostRestoreCanaryMismatch;
        M01OwnedCr3CompositeTransitionCheckpoint progress = status switch
        {
            M01OwnedCr3CompositeTransitionRuntimeStatus.InterruptDisableReadbackMismatch => M01OwnedCr3CompositeTransitionCheckpoint.Uninitialized,
            M01OwnedCr3CompositeTransitionRuntimeStatus.PgeClearReadbackMismatch => M01OwnedCr3CompositeTransitionCheckpoint.InterruptsDisabled,
            M01OwnedCr3CompositeTransitionRuntimeStatus.OwnedCr3ReadbackMismatch => M01OwnedCr3CompositeTransitionCheckpoint.PgeCleared,
            M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryBeforeMismatch or M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryAfterMismatch => M01OwnedCr3CompositeTransitionCheckpoint.OwnedCr3Loaded,
            M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeHierarchyMalformed or M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAddressMismatch => M01OwnedCr3CompositeTransitionCheckpoint.CanaryUpdated,
            M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteBeforeMismatch => M01OwnedCr3CompositeTransitionCheckpoint.ProbePteLocated,
            M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAfterMismatch => M01OwnedCr3CompositeTransitionCheckpoint.ProbeInvalidated,
            M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeReturnMismatch => M01OwnedCr3CompositeTransitionCheckpoint.ProbePteVerified,
            M01OwnedCr3CompositeTransitionRuntimeStatus.PageZeroPresent or M01OwnedCr3CompositeTransitionRuntimeStatus.PageZeroMalformed => M01OwnedCr3CompositeTransitionCheckpoint.ProbeReturned,
            _ => M01OwnedCr3CompositeTransitionCheckpoint.PageZeroAbsent
        };
        ulong observedPml4e = 0;
        ulong observedPdpte = 0;
        ulong observedPde = 0;
        if (hierarchyAttempted)
        {
            if (status == M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeHierarchyMalformed)
            {
                observedPml4e = 1;
            }
            else
            {
                observedPml4e = pml4e;
                observedPdpte = pdpte;
                observedPde = pde;
            }
        }

        if (status == M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAddressMismatch)
        {
            ulong pageMask = preflight.PhysicalAddressMask & M01OwnedCr3CompositeTransition.Cr3ArchitecturalBaseMask;
            ulong canonicalPt = pde & pageMask;
            ulong alternatePt = prepared.TablePageArenaStart;
            if (alternatePt == canonicalPt)
            {
                alternatePt += M01OwnedCr3CompositeTransition.PageSize;
            }

            observedPde = alternatePt | M01OwnedCr3CompositeTransition.PagePresentBit | M01OwnedCr3CompositeTransition.PageWritableBit;
        }

        ulong canaryBefore = canaryRead ? status == M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryBeforeMismatch ? prepared.ExpectedCanary ^ 1 : prepared.ExpectedCanary : 0;
        ulong canaryAfter = canaryUpdated ? status == M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryAfterMismatch ? prepared.ReplacementCanary ^ 1 : prepared.ReplacementCanary : 0;
        ulong postRestoreCanary = 0;
        if (ownedAttempted && status != M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch)
        {
            postRestoreCanary = canaryUpdated ? canaryAfter : canaryRead ? canaryBefore : prepared.ExpectedCanary;
        }

        if (status == M01OwnedCr3CompositeTransitionRuntimeStatus.PostRestoreCanaryMismatch)
        {
            postRestoreCanary = prepared.ReplacementCanary ^ 1;
        }

        ulong pageZeroPml4e = 0;
        ulong pageZeroPdpte = 0;
        ulong pageZeroPde = 0;
        ulong pageZeroPte = 0;
        M01OwnedCr3CompositeTransitionPageZeroLevel pageZeroLevel = M01OwnedCr3CompositeTransitionPageZeroLevel.Uninitialized;
        if (status == M01OwnedCr3CompositeTransitionRuntimeStatus.PageZeroPresent)
        {
            pageZeroPml4e = pml4e;
            pageZeroPdpte = pdpte;
            pageZeroPde = pde;
            pageZeroPte = prepared.FinalProbePteRaw;
            pageZeroLevel = M01OwnedCr3CompositeTransitionPageZeroLevel.Present;
        }
        else if (status == M01OwnedCr3CompositeTransitionRuntimeStatus.PageZeroMalformed)
        {
            pageZeroPml4e = 1;
            pageZeroLevel = M01OwnedCr3CompositeTransitionPageZeroLevel.Malformed;
        }
        else if (pageZeroAbsent)
        {
            (pageZeroPml4e, pageZeroPdpte, pageZeroPde, pageZeroPte, pageZeroLevel) = fixture.PageZeroObservation();
        }

        ulong firmwareCr3 = ownedAttempted ? status == M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch ? prepared.OwnedCr3Raw : preflight.RawFirmwareCr3 : 0;
        ulong restoredCr4 = pgeAttempted ? status == M01OwnedCr3CompositeTransitionRuntimeStatus.Cr4RestoreReadbackMismatch ? clearedCr4 : preflight.RawCr4 : 0;
        return M01OwnedCr3CompositeTransitionRuntimeState.CreateObserved(initializedState: prepared, originalRflags: preflight.Rflags, originalCr0: preflight.Cr0 | 1UL, originalCr3Raw: preflight.RawFirmwareCr3, originalCr4Raw: preflight.RawCr4, originalEfer: preflight.Ia32Efer, physicalAddressMask: preflight.PhysicalAddressMask, entryRsp: entryRsp, observedReturnAddress: prepared.ExpectedReturnAddress, observedRip: prepared.CompositeCodeStart, cliRflagsReadback: status == M01OwnedCr3CompositeTransitionRuntimeStatus.InterruptDisableReadbackMismatch ? preflight.Rflags : cliRflags, clearedCr4Readback: pgeAttempted ? status == M01OwnedCr3CompositeTransitionRuntimeStatus.PgeClearReadbackMismatch ? preflight.RawCr4 : clearedCr4 : 0, ownedCr3Readback: ownedAttempted ? status == M01OwnedCr3CompositeTransitionRuntimeStatus.OwnedCr3ReadbackMismatch ? preflight.RawFirmwareCr3 : prepared.OwnedCr3Raw : 0, canaryBefore: canaryBefore, canaryAfter: canaryAfter, probePml4e: observedPml4e, probePdpte: observedPdpte, probePde: observedPde, probePteBefore: pteLocated ? status == M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteBeforeMismatch ? initialPte ^ 0x1000 : initialPte : 0, probePteAfter: invalidated ? status == M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAfterMismatch ? prepared.FinalProbePteRaw | M01OwnedCr3CompositeTransition.PageWritableBit : prepared.FinalProbePteRaw : 0, invlpgTarget: invalidated ? prepared.ProbeEntry : 0, probeReturn: probeInvoked ? status == M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeReturnMismatch ? prepared.ExpectedProbeReturn ^ 1UL : prepared.ExpectedProbeReturn : 0, pageZeroPml4e: pageZeroPml4e, pageZeroPdpte: pageZeroPdpte, pageZeroPde: pageZeroPde, pageZeroPte: pageZeroPte, committedProbePteReadback: pteVerified ? prepared.FinalProbePteRaw : 0, firmwareCr3Readback: firmwareCr3, postRestoreCanaryReadback: postRestoreCanary, restoredCr4Readback: restoredCr4, restoredRflagsIntent: preflight.Rflags, exitRsp: status == M01OwnedCr3CompositeTransitionRuntimeStatus.RspRestoreMismatch ? entryRsp + 8 : entryRsp, invlpgCount: invalidated ? 1UL : 0, probeInvocationCount: probeInvoked ? 1UL : 0, firmwareCallbackCount: 0, runtimeStatus: status, checkpoint: M01OwnedCr3CompositeTransitionCheckpoint.ReturnPrepared, pageZeroLevel: pageZeroLevel, mutationCommitted: pteVerified, pgeWasCleared: pgeSucceeded && (preflight.RawCr4 & M01OwnedCr3CompositeTransition.Cr4PageGlobalEnableBit) != 0, ownedRootWasLoaded: ownedSucceeded, probeReturned: probeInvoked, progressCheckpoint: progress);
    }

    private static void RemoveReportedFailureCause(CompositeFixture fixture, ref M01OwnedCr3CompositeTransitionRuntimeState state, M01OwnedCr3CompositeTransitionRuntimeStatus status)
    {
        X64PagingModePreflightResult preflight = fixture.PriorEvidence.Evidence.Preflight;
        (ulong probePml4, ulong probePdpt, ulong probePd, _) = fixture.ProbeWalkEntryIndexes();
        switch (status)
        {
            case M01OwnedCr3CompositeTransitionRuntimeStatus.InterruptDisableReadbackMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateCliRflagsReadbackOffset), preflight.Rflags & ~M01OwnedCr3CompositeTransition.RflagsInterruptEnableBit);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.PgeClearReadbackMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateClearedCr4ReadbackOffset), preflight.RawCr4 & ~M01OwnedCr3CompositeTransition.Cr4PageGlobalEnableBit);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.OwnedCr3ReadbackMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateOwnedCr3ReadbackOffset), state.OwnedCr3Raw);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryBeforeMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateCanaryBeforeOffset), state.ExpectedCanary);
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StatePostRestoreCanaryOffset), state.ExpectedCanary);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.CanaryAfterMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateCanaryAfterOffset), state.ReplacementCanary);
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StatePostRestoreCanaryOffset), state.ReplacementCanary);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeHierarchyMalformed:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateProbePml4eOffset), fixture.PageEntries[probePml4]);
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateProbePdpteOffset), fixture.PageEntries[probePdpt]);
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateProbePdeOffset), fixture.PageEntries[probePd]);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAddressMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateProbePdeOffset), fixture.PageEntries[probePd]);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteBeforeMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateProbePteBeforeOffset), state.InitialProbePteRaw);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.ProbePteAfterMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateProbePteAfterOffset), state.FinalProbePteRaw);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.ProbeReturnMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateProbeReturnOffset), state.ExpectedProbeReturn);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.PageZeroPresent:
            case M01OwnedCr3CompositeTransitionRuntimeStatus.PageZeroMalformed:
                (ulong pml4e, ulong pdpte, ulong pde, ulong pte, M01OwnedCr3CompositeTransitionPageZeroLevel level) = fixture.PageZeroObservation();
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StatePageZeroPml4eOffset), pml4e);
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StatePageZeroPdpteOffset), pdpte);
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StatePageZeroPdeOffset), pde);
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StatePageZeroPteOffset), pte);
                SetStateByte(ref state, M01OwnedCr3CompositeTransition.StatePageZeroLevelOffset, (byte)level);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.FirmwareCr3ReadbackMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateFirmwareCr3ReadbackOffset), state.OriginalCr3Raw);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.Cr4RestoreReadbackMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateRestoredCr4Offset), state.OriginalCr4Raw);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.RspRestoreMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StateExitRspOffset), state.EntryRsp);
                break;
            case M01OwnedCr3CompositeTransitionRuntimeStatus.PostRestoreCanaryMismatch:
                OverwriteUlong(ref state, checked((int)M01OwnedCr3CompositeTransition.StatePostRestoreCanaryOffset), state.ReplacementCanary);
                break;
        }
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteMissingAllocationTable(CompositeFixture fixture)
    {
        fixed (BootReservedRange* reservations = fixture.Reservations)
        fixed (X64BootIdentityMappingRange* mappings = fixture.Mappings)
        fixed (M01OwnedCr3CompositeTransitionRangeBinding* bindings = fixture.Bindings)
        {
            return ExecuteRaw(fixture, null, fixture.AllocationCount, fixture.AllocationCapacity, fixture.ReservationPointer, fixture.ReservationCount, fixture.ReservationCapacity, mappings, fixture.MappingCount, (ulong)fixture.Mappings.Length, bindings, (ulong)fixture.Bindings.Length, (ulong)fixture.Bindings.Length);
        }
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteMissingReservationTable(CompositeFixture fixture)
    {
        fixed (BootMemoryRegionRecord* allocations = fixture.Allocations)
        fixed (X64BootIdentityMappingRange* mappings = fixture.Mappings)
        fixed (M01OwnedCr3CompositeTransitionRangeBinding* bindings = fixture.Bindings)
        {
            return ExecuteRaw(fixture, fixture.AllocationPointer, fixture.AllocationCount, fixture.AllocationCapacity, null, fixture.ReservationCount, fixture.ReservationCapacity, mappings, fixture.MappingCount, (ulong)fixture.Mappings.Length, bindings, (ulong)fixture.Bindings.Length, (ulong)fixture.Bindings.Length);
        }
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteMissingMappingTable(CompositeFixture fixture)
    {
        fixed (BootMemoryRegionRecord* allocations = fixture.Allocations)
        fixed (BootReservedRange* reservations = fixture.Reservations)
        fixed (M01OwnedCr3CompositeTransitionRangeBinding* bindings = fixture.Bindings)
        {
            return ExecuteRaw(fixture, fixture.AllocationPointer, fixture.AllocationCount, fixture.AllocationCapacity, fixture.ReservationPointer, fixture.ReservationCount, fixture.ReservationCapacity, null, fixture.MappingCount, (ulong)fixture.Mappings.Length, bindings, (ulong)fixture.Bindings.Length, (ulong)fixture.Bindings.Length);
        }
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteMissingBindingTable(CompositeFixture fixture)
    {
        fixed (BootMemoryRegionRecord* allocations = fixture.Allocations)
        fixed (BootReservedRange* reservations = fixture.Reservations)
        fixed (X64BootIdentityMappingRange* mappings = fixture.Mappings)
        {
            return ExecuteRaw(fixture, fixture.AllocationPointer, fixture.AllocationCount, fixture.AllocationCapacity, fixture.ReservationPointer, fixture.ReservationCount, fixture.ReservationCapacity, mappings, fixture.MappingCount, (ulong)fixture.Mappings.Length, null, (ulong)fixture.Bindings.Length, (ulong)fixture.Bindings.Length);
        }
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteMisalignedAllocationTable(CompositeFixture fixture)
    {
        fixed (BootReservedRange* reservations = fixture.Reservations)
        fixed (X64BootIdentityMappingRange* mappings = fixture.Mappings)
        fixed (M01OwnedCr3CompositeTransitionRangeBinding* bindings = fixture.Bindings)
        {
            return ExecuteRaw(fixture, (BootMemoryRegionRecord*)1, 1, 1, fixture.ReservationPointer, fixture.ReservationCount, fixture.ReservationCapacity, mappings, fixture.MappingCount, (ulong)fixture.Mappings.Length, bindings, (ulong)fixture.Bindings.Length, (ulong)fixture.Bindings.Length);
        }
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteOverflowingAllocationTable(CompositeFixture fixture)
    {
        fixed (BootReservedRange* reservations = fixture.Reservations)
        fixed (X64BootIdentityMappingRange* mappings = fixture.Mappings)
        fixed (M01OwnedCr3CompositeTransitionRangeBinding* bindings = fixture.Bindings)
        {
            return ExecuteRaw(fixture, (BootMemoryRegionRecord*)(~0UL - 7), 1, 1, fixture.ReservationPointer, fixture.ReservationCount, fixture.ReservationCapacity, mappings, fixture.MappingCount, (ulong)fixture.Mappings.Length, bindings, (ulong)fixture.Bindings.Length, (ulong)fixture.Bindings.Length);
        }
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteAliasedTables(CompositeFixture fixture)
    {
        fixed (BootMemoryRegionRecord* allocations = fixture.Allocations)
        fixed (X64BootIdentityMappingRange* mappings = fixture.Mappings)
        fixed (M01OwnedCr3CompositeTransitionRangeBinding* bindings = fixture.Bindings)
        {
            return ExecuteRaw(fixture, allocations, 1, 1, (BootReservedRange*)allocations, 1, 1, mappings, fixture.MappingCount, (ulong)fixture.Mappings.Length, bindings, (ulong)fixture.Bindings.Length, (ulong)fixture.Bindings.Length);
        }
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteCapacityTailAliasedTables(CompositeFixture fixture)
    {
        void* storage = NativeMemory.AlignedAlloc(256, 8);
        if (storage == null)
        {
            throw new OutOfMemoryException();
        }

        try
        {
            fixed (X64BootIdentityMappingRange* mappings = fixture.Mappings)
            fixed (M01OwnedCr3CompositeTransitionRangeBinding* bindings = fixture.Bindings)
            {
                byte* bytes = (byte*)storage;
                return ExecuteRaw(fixture, (BootMemoryRegionRecord*)bytes, 1, 2, (BootReservedRange*)(bytes + 64), 1, 1, mappings, fixture.MappingCount, (ulong)fixture.Mappings.Length, bindings, (ulong)fixture.Bindings.Length, (ulong)fixture.Bindings.Length);
            }
        }
        finally
        {
            NativeMemory.AlignedFree(storage);
        }
    }

    private static M01OwnedCr3CompositeTransitionResult ExecuteRaw(CompositeFixture fixture, BootMemoryRegionRecord* allocations, ulong allocationCount, ulong allocationCapacity, BootReservedRange* reservations, ulong reservationCount, ulong reservationCapacity, X64BootIdentityMappingRange* mappings, ulong mappingCount, ulong mappingCapacity, M01OwnedCr3CompositeTransitionRangeBinding* bindings, ulong bindingCount, ulong bindingCapacity, ulong? ownedCr3Raw = null, ulong? expectedReturnAddress = null, ulong? canaryAddress = null, ulong expectedCanary = ExpectedCanary, ulong replacementCanary = ReplacementCanary, ulong? pageImageCount = null, ulong? pageImageCapacity = null, ulong? pageEntryCount = null, ulong? pageEntryCapacity = null, ulong stateDestinationCount = 1, ulong stateDestinationCapacity = 1, M01OwnedCr3CompositeTransitionRuntimeState* stateDestination = null, bool useNullStateDestination = false, ulong* canaryDestination = null, bool useNullCanaryDestination = false, ulong canaryDestinationCount = 1, ulong canaryDestinationCapacity = 1)
    {
        M01LivePagingPreparationResult prior = fixture.PriorEvidence;
        fixed (X64PageTableImagePage* pageImages = fixture.PageImages)
        fixed (ulong* pageEntries = fixture.PageEntries)
        {
            return M01OwnedCr3CompositeTransition.CreateRuntimeState(&prior, 1, 1, allocations, allocationCount, allocationCapacity, reservations, reservationCount, reservationCapacity, mappings, mappingCount, mappingCapacity, pageImages, pageImageCount ?? fixture.PageImageCount, pageImageCapacity ?? (ulong)fixture.PageImages.Length, pageEntries, pageEntryCount ?? fixture.PageEntryCount, pageEntryCapacity ?? (ulong)fixture.PageEntries.Length, bindings, bindingCount, bindingCapacity, useNullStateDestination ? null : stateDestination == null ? fixture.StateDestination : stateDestination, stateDestinationCount, stateDestinationCapacity, useNullCanaryDestination ? null : canaryDestination == null ? fixture.CanaryDestination : canaryDestination, canaryDestinationCount, canaryDestinationCapacity, Virtual(fixture.RuntimeStateAddress), ownedCr3Raw ?? fixture.OwnedCr3Value, Virtual(expectedReturnAddress ?? fixture.ExpectedReturnAddressValue), Virtual(canaryAddress ?? fixture.CanaryAddressValue), expectedCanary, replacementCanary);
        }
    }

    private static BootMemoryRegionRecord Record(UefiMemoryType type, ulong start, ulong pages)
    {
        ulong length = checked(pages * 4096);
        return new BootMemoryRegionRecord((uint)type, start, start, pages, 0, length, checked(start + length));
    }

    private static BootReservedRange Reservation(BootReservationKind kind, ulong start, ulong pages)
    {
        BootReservedRangeStatus status = BootReservedRange.TryCreate(kind, Physical(start), pages, out BootReservedRange range);
        if (status != BootReservedRangeStatus.Success)
        {
            throw new InvalidOperationException("Invalid canonical reservation: " + status);
        }

        return range;
    }

    private static M01OwnedCr3CompositeTransitionRangeBinding Binding(M01OwnedCr3CompositeTransitionRangeRole role, M01OwnedCr3CompositeTransitionRangeProvenance provenance, ulong start, ulong end, ulong allocationIndex, ulong reservationIndex, ulong mappingIndex)
    {
        return new M01OwnedCr3CompositeTransitionRangeBinding(role, provenance, Physical(start), Physical(end), Virtual(start), Virtual(end), allocationIndex, reservationIndex, mappingIndex);
    }

    private static X64BootIdentityMappingRequirement Requirement(X64BootIdentityMappingPurpose purpose, ulong start, ulong end, PageProtection protection, PageCachePolicy cachePolicy = PageCachePolicy.WriteBack)
    {
        ulong length = checked(end - start);
        return new X64BootIdentityMappingRequirement(purpose, Physical(start), Physical(end), length / 4096, length, protection, cachePolicy);
    }

    private static PhysicalAddress Physical(ulong value)
    {
        AddressValueStatus status = PhysicalAddress.TryCreateFromHardwareRegisterBoundary(value, out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Invalid test physical address.");
        }

        return address;
    }

    private static VirtualAddress Virtual(ulong value)
    {
        AddressValueStatus status = VirtualAddress.TryCreateFromHardwareRegisterBoundary(value, out VirtualAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Invalid test virtual address.");
        }

        return address;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        if (address.TryGetRawValueForHardwareRegisterBoundary(out ulong value) != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Invalid test physical address.");
        }

        return value;
    }

    private static ulong Raw(VirtualAddress address)
    {
        if (address.TryGetRawValueForHardwareRegisterBoundary(out ulong value) != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Invalid test virtual address.");
        }

        return value;
    }

    private static X64PageTableEncodingPreconditions EncodingPreconditions()
    {
        const ulong PatFixture = 0x0006_0501_0007_0406UL;
        X64PageTableEntryStatus status = X64PatMappingToken.TryCreate(PatFixture, writeBackIndex: 0, uncachedIndex: 3, writeCombiningIndex: 4, out X64PatMappingToken token);
        if (status != X64PageTableEntryStatus.Success)
        {
            throw new InvalidOperationException("Invalid canonical PAT fixture.");
        }

        return X64PageTableEncodingPreconditions.CreateObserved(physicalAddressWidth: 48, nxeEnabled: true, writeProtectEnabled: true, fiveLevelPagingEnabled: false, oneGiBPagesSupported: false, token);
    }

    private static bool EnumContractMatches<T>((string Name, byte Value)[] expected)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
        {
            return false;
        }

        string[] names = Enum.GetNames<T>();
        T[] values = Enum.GetValues<T>();
        if (names.Length != expected.Length || values.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(names[index], expected[index].Name, StringComparison.Ordinal) || Convert.ToByte(values[index]) != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static long Offset<T>(string fieldName)
        where T : struct
    {
        return Marshal.OffsetOf<T>(fieldName).ToInt64();
    }

    private static void OverwriteUlong<T>(ref T value, int byteOffset, ulong replacement)
        where T : unmanaged
    {
        if (byteOffset < 0 || byteOffset > sizeof(T) - sizeof(ulong) || (byteOffset & (sizeof(ulong) - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        fixed (T* pointer = &value)
        {
            *(ulong*)((byte*)pointer + byteOffset) = replacement;
        }
    }

    private static void SetStateByte(ref M01OwnedCr3CompositeTransitionRuntimeState state, uint byteOffset, byte replacement)
    {
        if (byteOffset >= M01OwnedCr3CompositeTransitionRuntimeState.StructureSize)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        fixed (M01OwnedCr3CompositeTransitionRuntimeState* pointer = &state)
        {
            *((byte*)pointer + byteOffset) = replacement;
        }
    }

    private static void SetStateUInt32(ref M01OwnedCr3CompositeTransitionRuntimeState state, uint byteOffset, uint replacement)
    {
        if (byteOffset > M01OwnedCr3CompositeTransitionRuntimeState.StructureSize - sizeof(uint) || (byteOffset & (sizeof(uint) - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        }

        fixed (M01OwnedCr3CompositeTransitionRuntimeState* pointer = &state)
        {
            *(uint*)((byte*)pointer + byteOffset) = replacement;
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestM01030MarkerAndEvidenceBoundary()
    {
        HostCheck.SingleFixture(typeof(M01OwnedCr3CompositeTransitionTests), nameof(RunAll));
        string root = HostToolProcess.FindRepositoryRoot();
        string ownerPath = Path.Combine(root, "tests", "OS.HostTests", "M01OwnedCr3CompositeTransitionTests.cs");
        string productionPath = Path.Combine(root, "src", "OS.Kernel", "Boot", "M01OwnedCr3CompositeTransition.cs");
        if (!File.Exists(ownerPath) || !File.Exists(productionPath))
        {
            Console.WriteLine("M01_030_MARKER_DIAGNOSTIC missing_owner");
            return false;
        }

        string owner = File.ReadAllText(ownerPath);
        string production = RemoveComments(File.ReadAllText(productionPath));
        int runner = owner.IndexOf("private static void RunLiveIntervalContractTests()", StringComparison.Ordinal);
        int start = owner.IndexOf("\"TEST_START \" + LiveIntervalIssueTestName", runner, StringComparison.Ordinal);
        int failureGate = owner.IndexOf("if (failures != 0)", runner, StringComparison.Ordinal);
        int failure = owner.IndexOf("\"TEST_FAIL \" + LiveIntervalIssueTestName", runner, StringComparison.Ordinal);
        int ready = owner.IndexOf("Console.WriteLine(LiveIntervalReadyMarker)", runner, StringComparison.Ordinal);
        int pass = owner.IndexOf("\"TEST_PASS \" + LiveIntervalIssueTestName", runner, StringComparison.Ordinal);
        if (runner < 0 || start < runner || failureGate <= start || failure <= failureGate || ready <= failure || pass <= ready || CountOccurrences(owner, "\"" + LiveIntervalReadyMarker + "\"") != 1 || CountOccurrences(owner, "\"TEST_START \" + LiveIntervalIssueTestName") != 1 || CountOccurrences(owner, "\"TEST_FAIL \" + LiveIntervalIssueTestName") != 1 || CountOccurrences(owner, "\"TEST_PASS \" + LiveIntervalIssueTestName") != 1 || CountOccurrences(owner, "CLROS_" + "M01_027_READY") != 1 || production.Contains(LiveIntervalReadyMarker, StringComparison.Ordinal) || production.Contains("OS_" + "BOOT_OK", StringComparison.Ordinal) || production.Contains("OS_" + "SMOKE_PASS", StringComparison.Ordinal) || production.Contains("OS_" + "SMOKE_FAIL", StringComparison.Ordinal) || production.Contains("RuntimeExport", StringComparison.Ordinal) || production.Contains("EfiEntry", StringComparison.Ordinal) || CountOccurrences(production, "Qemu") != 1 || CountOccurrences(production, "public const bool IsQemuEvidence = false;") != 1 || production.Contains("public const bool IsQemuEvidence = true;", StringComparison.Ordinal))
        {
            Console.WriteLine("M01_030_MARKER_DIAGNOSTIC ownership" + " indexes=" + runner + "," + start + "," + failureGate + "," + failure + "," + ready + "," + pass);
            return false;
        }

        string[] roots = ["src", "std", "apps", "tools", "tests", "scripts", Path.Combine(".gitea", "workflows")];
        string ownerFullPath = Path.GetFullPath(ownerPath);
        foreach (string relativeRoot in roots)
        {
            string scanRoot = Path.Combine(root, relativeRoot);
            if (!Directory.Exists(scanRoot))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(scanRoot, "*", SearchOption.AllDirectories))
            {
                string fullPath = Path.GetFullPath(path);
                string extension = Path.GetExtension(path);
                if (fullPath.Equals(ownerFullPath, StringComparison.OrdinalIgnoreCase) || HasPathSegment(path, "bin") || HasPathSegment(path, "obj") || HasPathSegment(path, "artifacts") || extension is not (".cs" or ".ps1" or ".yml" or ".yaml" or ".props" or ".targets" or ".csproj" or ".slnx"))
                {
                    continue;
                }

                if (File.ReadAllText(path).Contains(LiveIntervalReadyMarker, StringComparison.Ordinal))
                {
                    Console.WriteLine("M01_030_MARKER_DIAGNOSTIC duplicate=" + Path.GetRelativePath(root, path));
                    return false;
                }
            }
        }

        return true;
    }

    private static bool HasPathSegment(string path, string segment)
    {
        string normalized = Path.GetFullPath(path).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string token = Path.DirectorySeparatorChar + segment + Path.DirectorySeparatorChar;
        return normalized.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TestSourceAndMarkerFirewall()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string productionPath = Path.Combine(root, "src", "OS.Kernel", "Boot", "M01OwnedCr3CompositeTransition.cs");
        string testPath = Path.Combine(root, "tests", "OS.HostTests", "M01OwnedCr3CompositeTransitionTests.cs");
        if (!File.Exists(productionPath) || !File.Exists(testPath))
        {
            return false;
        }

        string production = RemoveComments(File.ReadAllText(productionPath));
        string tests = RemoveComments(File.ReadAllText(testPath));
        string[] forbiddenProduction = ["Span<", "ReadOnlySpan<", "string ", "throw ", "System.", "Reflection", "Dynamic" + "Method", "Create" + "Delegate", "GetDelegateFor" + "FunctionPointer", "DllImport", "LibraryImport", "RuntimeImport", "NativeLibrary", "Marshal.", "delegate*", "List<", "IEnumerable<", "Task<", "new byte[", "Console.", "SerialIo.", "AllocatePages(", "FreePages(", "EfiEntry", "BootSequence.", "Probes."];
        string[] forbiddenHostExecution = ["GetDelegateFor" + "FunctionPointer", "delegate* " + "unmanaged", "Dynamic" + "Method", "Create" + "Delegate", "cal" + "li"];
        return forbiddenProduction.All(value => !production.Contains(value, StringComparison.Ordinal)) && forbiddenHostExecution.All(value => !tests.Contains(value, StringComparison.Ordinal)) && !Regex.IsMatch(production, @"\*\s*\(\s*(?:byte|sbyte|short|ushort|int|uint|long|ulong|nint|nuint)\s*\*\s*\)\s*0(?:UL|U|L)?\b", RegexOptions.CultureInvariant) && CountOccurrences(tests, "TEST_START \" + IssueTestName") == 1 && CountOccurrences(tests, "CLROS_" + "M01_027_READY") == 1 && CountOccurrences(tests, "TEST_PASS \" + IssueTestName") == 1 && CountOccurrences(tests, "TEST_FAIL \" + IssueTestName") == 1;
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string[] sources = CreateNoStdLibSourceClosure(repositoryRoot);
        string workspace = CreateNoStdLibWorkspace(repositoryRoot, sources);
        try
        {
            NoStdLibBuildResult result = RunNoStdLibBuild(workspace);
            string outputAssembly = Path.Combine(workspace, "out", "CLROS.M01.CompositeTransitionCompileClosure.dll");
            bool succeeded = !result.TimedOut && result.ExitCode == 0 && !ContainsInfrastructureFailure(result.Output) && HasNoExternalMetadata(outputAssembly);
            if (!succeeded)
            {
                Console.WriteLine("M01_027_NOSTDLIB_COMPILE_DIAGNOSTIC " + NormalizeCompilerOutput(result.Output));
            }

            return succeeded;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static string[] CreateNoStdLibSourceClosure(string repositoryRoot)
        => HostNoStdLibClosure.Kernel(repositoryRoot,
            "OS.Boot.M01LoadedImageStackIntegration", "OS.Boot.M01LiveAllocatorIntegration",
            "OS.Boot.M01LivePagingIntegration", "OS.Boot.M01OwnedCr3CompositeTransition");

    private static string CreateNoStdLibWorkspace(string repositoryRoot, string[] sources)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "clros-m01-027-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            string globalJson = Path.Combine(repositoryRoot, "global.json");
            if (File.Exists(globalJson))
            {
                File.Copy(globalJson, Path.Combine(workspace, "global.json"));
            }

            File.WriteAllText(Path.Combine(workspace, "NoStdLibShim.cs"), """
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
                """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(Path.Combine(workspace, "Probe.cs"), """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                namespace CLROS.M01
                {
                    internal static class CompositeCompileProbe
                    {
                        public static ulong Exercise()
                        {
                            return OS.Boot
                                .M01OwnedCr3CompositeTransition
                                .RequiredByteLength
                                ^ OS.Boot
                                    .M01OwnedCr3CompositeTransition
                                    .CallGateByteLength
                                ^ OS.Boot
                                    .M01OwnedCr3CompositeCallGateObservation
                                    .StructureSize
                                ^ OS.Boot
                                    .M01OwnedCr3CompositeCallGateResult
                                    .StructureSize;
                        }
                    }
                }
                """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            StringBuilder compileItems = new StringBuilder();
            for (int index = 0; index < sources.Length; index++)
            {
                compileItems.Append("    <Compile Include=\"");
                compileItems.Append(EscapeXml(sources[index]));
                compileItems.Append("\" Link=\"Closure\\");
                compileItems.Append(index.ToString("D2"));
                compileItems.Append('-');
                compileItems.Append(EscapeXml(Path.GetFileName(sources[index])));
                compileItems.AppendLine("\" />");
            }

            string projectText = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.CompositeTransitionCompileClosure</AssemblyName>
                <TargetFramework>net11.0</TargetFramework>
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
            """.Replace("__SOURCE_ITEMS__", compileItems.ToString().TrimEnd(), StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(workspace, "M01.027.NoStdLib.csproj"), projectText, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

    private static NoStdLibBuildResult RunNoStdLibBuild(string workspace)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workspace
        };
        foreach (string argument in new[]
        {
            "build",
            "M01.027.NoStdLib.csproj",
            "--nologo",
            "-m:1",
            "--no-dependencies",
            "--no-incremental",
            "--disable-build-servers",
            "-v:minimal",
            "-p:RestoreIgnoreFailedSources=true",
            "-p:NuGetAudit=false"
        }

        )
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";
        using Process process = new Process
        {
            StartInfo = start
        };
        if (!process.Start())
        {
            return new NoStdLibBuildResult(-1, timedOut: false, "dotnet process did not start");
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(BuildTimeoutMilliseconds);
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

        string output = standardOutput.Wait(5_000) ? standardOutput.Result : "stdout did not close";
        string error = standardError.Wait(5_000) ? standardError.Result : "stderr did not close";
        return new NoStdLibBuildResult(exited ? process.ExitCode : -1, timedOut: !exited, output + Environment.NewLine + error);
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
        if (metadata.AssemblyReferences.Count != 0 || metadata.GetTableRowCount(TableIndex.ModuleRef) != 0 || metadata.GetTableRowCount(TableIndex.ImplMap) != 0)
        {
            return false;
        }

        bool hasComposite = false;
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            if (string.Equals(metadata.GetString(type.Namespace), "OS.Boot", StringComparison.Ordinal) && string.Equals(metadata.GetString(type.Name), "M01OwnedCr3CompositeTransition", StringComparison.Ordinal))
            {
                hasComposite = true;
            }

            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
                if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
                {
                    return false;
                }
            }
        }

        return hasComposite;
    }

    private static bool ContainsInfrastructureFailure(string output)
    {
        return output.Contains("MSB1009", StringComparison.OrdinalIgnoreCase) || output.Contains("NU1301", StringComparison.OrdinalIgnoreCase) || output.Contains("A compatible .NET SDK was not found", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCompilerOutput(string output)
    {
        string normalized = Regex.Replace(output, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        return normalized.Length <= 1500 ? normalized : normalized[..1500] + "...";
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string fullWorkspace = Path.GetFullPath(workspace);
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string? parent = Directory.GetParent(fullWorkspace)?.FullName;
        string leafName = Path.GetFileName(fullWorkspace);
        if (parent is null || !Path.TrimEndingDirectorySeparator(parent).Equals(Path.TrimEndingDirectorySeparator(tempRoot), StringComparison.OrdinalIgnoreCase) || !Regex.IsMatch(leafName, @"\Aclros-m01-027-[0-9a-f]{32}\z", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Refusing to delete an unexpected M01-027 workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        if ((File.GetAttributes(fullWorkspace) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Refusing to delete a reparse-point M01-027 workspace.");
        }

        Directory.Delete(fullWorkspace, recursive: true);
    }

    private static string RemoveComments(string source)
    {
        return Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static bool HasPreCliSameRootComparison(List<CompositeDecodedInstruction> instructions, int cliIndex)
    {
        const byte Unknown = 0;
        const byte FirmwareRoot = 1;
        const byte OwnedRoot = 2;
        byte[] provenance = new byte[16];
        for (int index = 0; index < cliIndex; index++)
        {
            CompositeDecodedInstruction instruction = instructions[index];
            if (instruction.Kind == CompositeInstructionKind.CompareRegisterRegister && instruction.Register >= 0 && instruction.SourceRegister >= 0)
            {
                byte left = provenance[instruction.Register];
                byte right = provenance[instruction.SourceRegister];
                if (((left == FirmwareRoot && right == OwnedRoot) || (left == OwnedRoot && right == FirmwareRoot)) && index + 3 < cliIndex && instructions[index + 1].Kind == CompositeInstructionKind.ConditionalBranch && instructions[index + 2].Kind == CompositeInstructionKind.MoveRegisterImmediate && instructions[index + 2].Register == 0 && instructions[index + 2].Immediate == (ulong)M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidOwnedCr3 && instructions[index + 3].Kind == CompositeInstructionKind.Jump)
                {
                    return true;
                }
            }

            if (instruction.Kind is CompositeInstructionKind.AndRegisterRegister or CompositeInstructionKind.AndRegisterImmediate)
            {
                continue;
            }

            int destination = instruction.DestinationRegister;
            if (destination < 0)
            {
                continue;
            }

            if (instruction.Kind == CompositeInstructionKind.ReadCr3)
            {
                provenance[destination] = FirmwareRoot;
            }
            else if (instruction.StateLoadOffset == M01OwnedCr3CompositeTransition.StateOwnedCr3RawOffset)
            {
                provenance[destination] = OwnedRoot;
            }
            else if (instruction.Kind == CompositeInstructionKind.MoveRegisterRegister && instruction.SourceRegister >= 0)
            {
                provenance[destination] = provenance[instruction.SourceRegister];
            }
            else
            {
                provenance[destination] = Unknown;
            }
        }

        return false;
    }

    private static bool ValidateRflagsRestorationAndPreCliReturns(List<CompositeDecodedInstruction> instructions)
    {
        int[] returnIndexes = instructions.Select((instruction, index) => (instruction, index)).Where(item => item.instruction.Kind == CompositeInstructionKind.Ret).Select(item => item.index).ToArray();
        if (returnIndexes.Length != 2)
        {
            return false;
        }

        bool sawRegisterSavedFlagsReturn = false;
        bool sawStateSavedFlagsReturn = false;
        foreach (int returnIndex in returnIndexes)
        {
            // POPFQ must be the instruction immediately before RET. Merely
            // finding POPFQ somewhere in the epilogue does not prove that a
            // later instruction could not alter RFLAGS.
            if (returnIndex < 7 || instructions[returnIndex - 1].Kind != CompositeInstructionKind.Popfq || instructions[returnIndex - 2].Kind != CompositeInstructionKind.Push || instructions[returnIndex - 3].Kind != CompositeInstructionKind.Pop || instructions[returnIndex - 3].Register != 3 || instructions[returnIndex - 4].Kind != CompositeInstructionKind.Pop || instructions[returnIndex - 4].Register != 6 || instructions[returnIndex - 5].Kind != CompositeInstructionKind.Pop || instructions[returnIndex - 5].Register != 12)
            {
                return false;
            }

            int flagsRegister = instructions[returnIndex - 2].Register;
            int sourceIndex = -1;
            for (int index = returnIndex - 3; index >= 0; index--)
            {
                if (instructions[index].DestinationRegister == flagsRegister)
                {
                    sourceIndex = index;
                    break;
                }
            }

            if (sourceIndex < 0)
            {
                return false;
            }

            CompositeDecodedInstruction source = instructions[sourceIndex];
            if (source.StateLoadOffset == M01OwnedCr3CompositeTransition.StateOriginalRflagsOffset)
            {
                sawStateSavedFlagsReturn = true;
            }
            else if (source.Kind == CompositeInstructionKind.MoveRegisterRegister && source.SourceRegister == 8)
            {
                int savedFlagsIndex = instructions.FindIndex(instruction => instruction.Kind == CompositeInstructionKind.Pushfq);
                if (savedFlagsIndex < 0 || savedFlagsIndex + 1 >= sourceIndex || instructions[savedFlagsIndex + 1].Kind != CompositeInstructionKind.Pop || instructions[savedFlagsIndex + 1].Register != 8 || instructions.Skip(savedFlagsIndex + 2).Take(sourceIndex - savedFlagsIndex - 2).Any(instruction => instruction.DestinationRegister == 8))
                {
                    return false;
                }

                sawRegisterSavedFlagsReturn = true;
            }
            else
            {
                return false;
            }

            if (instructions.Skip(sourceIndex + 1).Take(returnIndex - sourceIndex - 3).Any(instruction => instruction.DestinationRegister == flagsRegister))
            {
                return false;
            }
        }

        if (!sawRegisterSavedFlagsReturn || !sawStateSavedFlagsReturn)
        {
            return false;
        }

        Dictionary<int, int> indexesByOffset = instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(item => item.Offset, item => item.index);
        for (ulong status = (ulong)M01OwnedCr3CompositeTransitionRuntimeStatus.InvalidState; status <= (ulong)M01OwnedCr3CompositeTransitionRuntimeStatus.PhysicalAddressWidthUnsupported; status++)
        {
            bool hasRestoringPreCliPath = false;
            for (int index = 0; index + 1 < instructions.Count; index++)
            {
                CompositeDecodedInstruction candidate = instructions[index];
                if (candidate.Kind != CompositeInstructionKind.MoveRegisterImmediate || candidate.Register != 0 || candidate.Immediate != status || instructions[index + 1].Kind != CompositeInstructionKind.Jump)
                {
                    continue;
                }

                if (CanReachRetWithoutCli(instructions, indexesByOffset, index + 1))
                {
                    hasRestoringPreCliPath = true;
                    break;
                }
            }

            if (!hasRestoringPreCliPath)
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanReachRetWithoutCli(List<CompositeDecodedInstruction> instructions, Dictionary<int, int> indexesByOffset, int startIndex)
    {
        Queue<int> pending = new();
        HashSet<int> visited = [];
        pending.Enqueue(startIndex);
        while (pending.Count != 0)
        {
            int index = pending.Dequeue();
            if (index < 0 || index >= instructions.Count || !visited.Add(index))
            {
                continue;
            }

            CompositeDecodedInstruction instruction = instructions[index];
            if (instruction.Kind == CompositeInstructionKind.Cli)
            {
                continue;
            }

            if (instruction.Kind == CompositeInstructionKind.Ret)
            {
                return true;
            }

            if (instruction.BranchTarget is int branchTarget && indexesByOffset.TryGetValue(branchTarget, out int targetIndex))
            {
                pending.Enqueue(targetIndex);
            }

            if (instruction.Kind != CompositeInstructionKind.Jump)
            {
                pending.Enqueue(index + 1);
            }
        }

        return false;
    }

    private static bool TryDecodeComposite(byte[] bytes, out List<CompositeDecodedInstruction> instructions)
    {
        instructions = [];
        int offset = 0;
        while (offset < bytes.Length)
        {
            if (!TryDecodeCompositeInstruction(bytes, offset, out CompositeDecodedInstruction instruction))
            {
                return false;
            }

            instructions.Add(instruction);
            offset += instruction.Length;
        }

        return offset == bytes.Length;
    }

    private static bool TryDecodeCompositeInstruction(byte[] bytes, int offset, out CompositeDecodedInstruction instruction)
    {
        instruction = default;
        int index = offset;
        bool operand16 = false;
        byte rex = 0;
        if (index < bytes.Length && bytes[index] == 0x66)
        {
            operand16 = true;
            index++;
        }

        if (index < bytes.Length && bytes[index] >= 0x40 && bytes[index] <= 0x4f)
        {
            rex = bytes[index++];
        }

        if (index >= bytes.Length)
        {
            return false;
        }

        byte opcode = bytes[index++];
        CompositeInstructionKind kind = CompositeInstructionKind.Other;
        int? branchTarget = null;
        int register = -1;
        ulong registerMask = 0;
        uint? stateStoreOffset = null;
        uint? stateLoadOffset = null;
        int sourceRegister = -1;
        int destinationRegister = -1;
        ulong immediate = 0;
        bool shiftLeftRdx32 = false;
        bool orRaxRdx = false;
        bool cr4PaeTest = false;
        byte decodedOpcode = opcode;
        byte decodedModRm = 0;
        int memoryBaseRegister = -1;
        uint? memoryDisplacement = null;
        if (opcode == 0xe9 || opcode == 0xe8)
        {
            if (!TryReadInt32(bytes, index, out int displacement))
            {
                return false;
            }

            index += 4;
            branchTarget = index + displacement;
            kind = opcode == 0xe9 ? CompositeInstructionKind.Jump : CompositeInstructionKind.DirectCall;
        }
        else if (opcode == 0xeb)
        {
            if (index >= bytes.Length)
            {
                return false;
            }

            int displacement = unchecked((sbyte)bytes[index++]);
            branchTarget = index + displacement;
            kind = CompositeInstructionKind.Jump;
        }
        else if (opcode == 0xe3 || opcode >= 0x70 && opcode <= 0x7f)
        {
            if (index >= bytes.Length)
            {
                return false;
            }

            int displacement = unchecked((sbyte)bytes[index++]);
            branchTarget = index + displacement;
            kind = CompositeInstructionKind.ConditionalBranch;
        }
        else if (opcode == 0x0f)
        {
            if (index >= bytes.Length)
            {
                return false;
            }

            byte second = bytes[index++];
            if (second >= 0x80 && second <= 0x8f)
            {
                if (!TryReadInt32(bytes, index, out int displacement))
                {
                    return false;
                }

                index += 4;
                branchTarget = index + displacement;
                kind = CompositeInstructionKind.ConditionalBranch;
            }
            else if (second == 0xa2)
            {
                kind = CompositeInstructionKind.Cpuid;
                registerMask = 0x0f;
            }
            else if (second == 0x32)
            {
                kind = CompositeInstructionKind.Rdmsr;
                registerMask = (1UL << 0) | (1UL << 1) | (1UL << 2);
            }
            else if (second == 0xae)
            {
                if (index >= bytes.Length || bytes[index++] != 0xf0)
                {
                    return false;
                }

                kind = CompositeInstructionKind.Mfence;
            }
            else if (second == 0x01)
            {
                if (index >= bytes.Length || bytes[index++] != 0x39)
                {
                    return false;
                }

                kind = CompositeInstructionKind.Invlpg;
                registerMask = 1UL << 1;
            }
            else if (second == 0x20 || second == 0x22)
            {
                if (index >= bytes.Length)
                {
                    return false;
                }

                byte modrm = bytes[index++];
                if ((modrm >> 6) != 3)
                {
                    return false;
                }

                int controlRegister = (modrm >> 3) & 7;
                register = (modrm & 7) + ((rex & 1) != 0 ? 8 : 0);
                registerMask = 1UL << register;
                bool read = second == 0x20;
                if (read)
                {
                    destinationRegister = register;
                }
                else
                {
                    sourceRegister = register;
                }

                kind = (read, controlRegister) switch
                {
                    (true, 0) => CompositeInstructionKind.ReadCr0,
                    (true, 3) => CompositeInstructionKind.ReadCr3,
                    (true, 4) => CompositeInstructionKind.ReadCr4,
                    (false, 3) => CompositeInstructionKind.WriteCr3,
                    (false, 4) => CompositeInstructionKind.WriteCr4,
                    _ => CompositeInstructionKind.OtherControlRegister
                };
            }
            else if (second == 0xb6)
            {
                if (!TryConsumeModRm(bytes, ref index, rex, out _, out _, out _, out ulong operandMask, out _))
                {
                    return false;
                }

                registerMask = operandMask;
            }
            else if (second == 0xba)
            {
                if (!TryConsumeModRm(bytes, ref index, rex, out byte bitModrm, out _, out _, out ulong operandMask, out _) || index >= bytes.Length)
                {
                    return false;
                }

                immediate = bytes[index++];
                registerMask = operandMask;
                int extensionRegister = ((bitModrm >> 3) & 7) + ((rex & 4) != 0 ? 8 : 0);
                registerMask &= ~(1UL << extensionRegister);
            }
            else
            {
                return false;
            }
        }
        else if (opcode == 0xc3)
        {
            kind = CompositeInstructionKind.Ret;
        }
        else if (opcode == 0x9c)
        {
            kind = CompositeInstructionKind.Pushfq;
        }
        else if (opcode == 0x9d)
        {
            kind = CompositeInstructionKind.Popfq;
        }
        else if (opcode == 0xfa)
        {
            kind = CompositeInstructionKind.Cli;
        }
        else if (opcode == 0xfb)
        {
            kind = CompositeInstructionKind.Sti;
        }
        else if (opcode >= 0x50 && opcode <= 0x57)
        {
            register = (opcode & 7) + ((rex & 1) != 0 ? 8 : 0);
            registerMask = 1UL << register;
            kind = CompositeInstructionKind.Push;
            sourceRegister = register;
        }
        else if (opcode >= 0x58 && opcode <= 0x5f)
        {
            register = (opcode & 7) + ((rex & 1) != 0 ? 8 : 0);
            registerMask = 1UL << register;
            kind = CompositeInstructionKind.Pop;
            destinationRegister = register;
        }
        else if (opcode >= 0xb8 && opcode <= 0xbf)
        {
            register = (opcode & 7) + ((rex & 1) != 0 ? 8 : 0);
            registerMask = 1UL << register;
            int immediateLength = (rex & 8) != 0 ? 8 : 4;
            if (index + immediateLength > bytes.Length)
            {
                return false;
            }

            immediate = ReadUnsigned(bytes, index, immediateLength);
            index += immediateLength;
            kind = CompositeInstructionKind.MoveRegisterImmediate;
            destinationRegister = register;
        }
        else if (opcode >= 0xb0 && opcode <= 0xb7)
        {
            if (index >= bytes.Length)
            {
                return false;
            }

            register = (opcode & 7) + ((rex & 1) != 0 ? 8 : 0);
            registerMask = 1UL << register;
            immediate = bytes[index++];
            kind = CompositeInstructionKind.MoveRegisterImmediate;
            destinationRegister = register;
        }
        else if (opcode is 0x01 or 0x09 or 0x21 or 0x2b or 0x31 or 0x39 or 0x3b or 0x80 or 0x81 or 0x83 or 0x85 or 0x88 or 0x89 or 0x8b or 0x8d or 0xc1 or 0xc6 or 0xc7 or 0xd3 or 0xf6 or 0xf7 or 0xfe or 0xff)
        {
            if (!TryConsumeModRm(bytes, ref index, rex, out byte modrm, out int mod, out int rmRegister, out ulong operandMask, out uint? displacement))
            {
                return false;
            }

            decodedModRm = modrm;
            if (mod != 3)
            {
                memoryBaseRegister = rmRegister;
                memoryDisplacement = displacement ?? 0;
            }

            registerMask = operandMask;
            int group = (modrm >> 3) & 7;
            int regRegister = group + ((rex & 4) != 0 ? 8 : 0);
            if (opcode is 0x80 or 0x81 or 0x83 or 0xc1 or 0xc6 or 0xc7 or 0xf6 or 0xf7 or 0xfe or 0xff)
            {
                int extensionRegister = group + ((rex & 4) != 0 ? 8 : 0);
                registerMask &= ~(1UL << extensionRegister);
                if (mod == 3)
                {
                    registerMask |= 1UL << rmRegister;
                }
            }

            int immediateLength = opcode switch
            {
                0x80 or 0x83 or 0xc1 or 0xc6 or 0xf6 => 1,
                0x81 or 0xc7 or 0xf7 => operand16 ? 2 : 4,
                _ => 0
            };
            if (index + immediateLength > bytes.Length)
            {
                return false;
            }

            if (immediateLength != 0)
            {
                immediate = ReadUnsigned(bytes, index, immediateLength);
                index += immediateLength;
            }

            if (mod == 2 && rmRegister == 3 && displacement.HasValue && opcode is 0x88 or 0x89 or 0xc6)
            {
                stateStoreOffset = displacement.Value;
            }

            if (mod == 2 && rmRegister == 3 && displacement.HasValue && opcode == 0x8b)
            {
                stateLoadOffset = displacement.Value;
                sourceRegister = 3;
                destinationRegister = regRegister;
            }

            if (opcode == 0x89 && mod == 3)
            {
                sourceRegister = regRegister;
                destinationRegister = rmRegister;
                kind = CompositeInstructionKind.MoveRegisterRegister;
            }
            else if (opcode == 0x8b && mod == 3)
            {
                sourceRegister = rmRegister;
                destinationRegister = regRegister;
                kind = CompositeInstructionKind.MoveRegisterRegister;
            }
            else if (opcode == 0x8d)
            {
                destinationRegister = regRegister;
            }
            else if (opcode == 0x21 && mod == 3)
            {
                sourceRegister = regRegister;
                destinationRegister = rmRegister;
                register = rmRegister;
                kind = CompositeInstructionKind.AndRegisterRegister;
            }
            else if (opcode is 0x81 or 0x83 && mod == 3 && group == 4)
            {
                destinationRegister = rmRegister;
                register = rmRegister;
                kind = CompositeInstructionKind.AndRegisterImmediate;
            }
            else if (opcode == 0x39 && mod == 3)
            {
                sourceRegister = regRegister;
                register = rmRegister;
                kind = CompositeInstructionKind.CompareRegisterRegister;
            }
            else if (opcode == 0x3b && mod == 3)
            {
                sourceRegister = rmRegister;
                register = regRegister;
                kind = CompositeInstructionKind.CompareRegisterRegister;
            }

            if (opcode == 0xff && mod != 3 && group == 6)
            {
                kind = CompositeInstructionKind.PushMemory;
            }
            else if (opcode == 0xff && mod == 3 && group == 2)
            {
                kind = CompositeInstructionKind.IndirectCall;
                register = rmRegister;
            }
            else if (opcode == 0xff && mod == 3 && group is 4 or 5)
            {
                kind = CompositeInstructionKind.OtherIndirectControl;
                register = rmRegister;
            }
            else if (opcode == 0x83 && mod == 3 && rmRegister == 4 && group == 5)
            {
                kind = CompositeInstructionKind.SubRsp;
            }
            else if (opcode == 0x83 && mod == 3 && rmRegister == 4 && group == 0)
            {
                kind = CompositeInstructionKind.AddRsp;
            }

            shiftLeftRdx32 = opcode == 0xc1 && mod == 3 && group == 4 && rmRegister == 2 && immediate == 32;
            orRaxRdx = opcode == 0x09 && mod == 3 && rmRegister == 0 && (((modrm >> 3) & 7) + ((rex & 4) != 0 ? 8 : 0)) == 2;
            cr4PaeTest = opcode == 0xf7 && mod == 3 && group == 0 && immediate == 0x20;
        }
        else
        {
            return false;
        }

        instruction = new CompositeDecodedInstruction(offset, index - offset, kind, branchTarget, register, registerMask, stateStoreOffset, stateLoadOffset, sourceRegister, destinationRegister, immediate, shiftLeftRdx32, orRaxRdx, cr4PaeTest, decodedOpcode, decodedModRm, memoryBaseRegister, memoryDisplacement);
        return instruction.Length > 0;
    }

    private static bool TryConsumeModRm(byte[] bytes, ref int index, byte rex, out byte modrm, out int mod, out int rmRegister, out ulong registerMask, out uint? displacement)
    {
        modrm = 0;
        mod = 0;
        rmRegister = -1;
        registerMask = 0;
        displacement = null;
        if (index >= bytes.Length)
        {
            return false;
        }

        modrm = bytes[index++];
        mod = modrm >> 6;
        int reg = ((modrm >> 3) & 7) + ((rex & 4) != 0 ? 8 : 0);
        int rmLow = modrm & 7;
        rmRegister = rmLow + ((rex & 1) != 0 ? 8 : 0);
        registerMask |= 1UL << reg;
        if (mod == 3)
        {
            registerMask |= 1UL << rmRegister;
            return true;
        }

        bool hasSib = rmLow == 4;
        int baseLow = rmLow;
        if (hasSib)
        {
            if (index >= bytes.Length)
            {
                return false;
            }

            byte sib = bytes[index++];
            int indexLow = (sib >> 3) & 7;
            baseLow = sib & 7;
            if (indexLow != 4)
            {
                int indexRegister = indexLow + ((rex & 2) != 0 ? 8 : 0);
                registerMask |= 1UL << indexRegister;
            }

            if (!(mod == 0 && baseLow == 5))
            {
                int baseRegister = baseLow + ((rex & 1) != 0 ? 8 : 0);
                registerMask |= 1UL << baseRegister;
            }
        }
        else if (!(mod == 0 && rmLow == 5))
        {
            registerMask |= 1UL << rmRegister;
        }

        if (mod == 1)
        {
            if (index >= bytes.Length)
            {
                return false;
            }

            displacement = unchecked((uint)(sbyte)bytes[index++]);
        }
        else if (mod == 2 || (mod == 0 && baseLow == 5))
        {
            if (!TryReadUInt32(bytes, index, out uint value))
            {
                return false;
            }

            displacement = value;
            index += 4;
        }

        return true;
    }

    private static bool TryReadUInt32(byte[] bytes, int offset, out uint value)
    {
        value = 0;
        if (offset < 0 || offset + 4 > bytes.Length)
        {
            return false;
        }

        value = (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
        return true;
    }

    private static bool TryReadInt32(byte[] bytes, int offset, out int value)
    {
        bool success = TryReadUInt32(bytes, offset, out uint raw);
        value = unchecked((int)raw);
        return success;
    }

    private static ulong ReadUnsigned(byte[] bytes, int offset, int length)
    {
        ulong value = 0;
        for (int index = 0; index < length; index++)
        {
            value |= (ulong)bytes[offset + index] << (index * 8);
        }

        return value;
    }

    private static ulong ComputeFnv1a64(ReadOnlySpan<byte> bytes)
    {
        ulong hash = 0xcbf2_9ce4_8422_2325UL;
        foreach (byte value in bytes)
        {
            hash ^= value;
            hash *= 0x0000_0100_0000_01b3UL;
        }

        return hash;
    }
}
