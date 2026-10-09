using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OS.Boot;
using OS.Kernel.Memory;

internal static unsafe class X64ProtectionProbeStubPlanTests
{
    private const string TestName = "m01.014.03.protection";
    private const ulong ProbeStart = 0x0080_0000;
    private const ulong TransitionCodeStart = 0x0060_0000;
    private const ulong TableStateStart = 0x0070_0000;
    private const ulong Canary = 0x434c_524f_535f_4f4b;
    private const string ExpectedProbeSha256 =
        "BBA15FCF0EEE9F4112C1ABB203EB833D44A09E9BCEDB29456ACAE9B466EBA374";

    internal static int RunM01014Protection()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "append only enums pods defaults and nonlive flags",
            TestTypeContracts,
            ref failures);
        RunCase(
            "constant return bytes and hash are exact",
            TestExactBytesAndHash,
            ref failures);
        RunCase(
            "materialization and readback failures are bounded",
            TestMaterializationAndReadbackFailures,
            ref failures);
        RunCase(
            "complete rw nx to ro x audit succeeds",
            TestCompleteAudit,
            ref failures);
        RunCase(
            "malformed allocation and reservation fail closed",
            TestAllocationAndReservationFailures,
            ref failures);
        RunCase(
            "allocation overlap is rejected",
            TestAllocationOverlap,
            ref failures);
        RunCase(
            "table image is bound to the state allocation",
            TestTableImageAllocationBinding,
            ref failures);
        RunCase(
            "probe stub cannot rebind to another allocation",
            TestProbeStubAllocationBinding,
            ref failures);
        RunCase(
            "leaf return canary count and entry mismatches fail",
            TestObservationAndLeafFailures,
            ref failures);
        RunCase(
            "event omissions and ordering failures are classified",
            TestEventFailures,
            ref failures);
        RunCase(
            "invlpg target and writable code fail closed",
            TestInvlpgFailures,
            ref failures);
        RunCase(
            "source remains plan only without live execution",
            TestSourceBoundary,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " failures=" + failures);
            return failures;
        }

        Console.WriteLine("TEST_PASS " + TestName);
        return 0;
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {
        HostCheck.Run(TestName + " " + name, () =>
        {
            try
            {
                if (test())
                    return true;
                Console.WriteLine(
                    "M01_014_PROTECTION_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "M01_014_PROTECTION_CASE_FAIL "
                    + Normalize(name)
                    + " "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestTypeContracts()
    {
        string[] phaseNames =
        [
            "Uninitialized",
            "Materialized",
            "ReadBackVerified"
        ];
        string[] terminalNames =
        [
            "Uninitialized",
            "ModeledReturn",
            "Timeout",
            "Reset",
            "Fault",
            "UnexpectedReturnValue",
            "CanaryMismatch"
        ];
        string[] eventNames =
        [
            "Uninitialized",
            "StubMaterialized",
            "StubReadBackVerified",
            "InitialReadWriteNoExecuteVerified",
            "ProtectionChangedReadExecute",
            "LeafReadBackVerified",
            "TargetInvalidationPlanned",
            "InvlpgInvocationPlanned",
            "ProbeInvocationPlanned",
            "ConstantReturnAndCanaryObserved"
        ];
        string[] statusNames =
        [
            "Uninitialized",
            "Success",
            "InvalidProbeAllocation",
            "ProbeAllocationMustBeLoaderCode",
            "ProbeAllocationMustBeOnePage",
            "ProbeAllocationNotIdentityMapped",
            "InvalidProbeReservation",
            "ProbeReservationKindMismatch",
            "ProbeReservationRangeMismatch",
            "InvalidEntryOffset",
            "MisalignedEntry",
            "EntryOutsideProbePage",
            "MissingDestination",
            "MisalignedDestination",
            "DestinationCapacityTooSmall",
            "DestinationCapacityExceedsPage",
            "DestinationSpanOverflow",
            "DestinationAddressMismatch",
            "MissingSource",
            "MisalignedSource",
            "SourceCapacityTooSmall",
            "SourceCapacityExceedsPage",
            "SourceSpanOverflow",
            "SourceAddressMismatch",
            "InvalidExpectedStub",
            "ExpectedStubNotMaterialized",
            "ByteReadbackMismatch",
            "InvalidTransitionCodeAllocation",
            "InvalidTableStateAllocation",
            "AllocationOverlap",
            "StubAllocationMismatch",
            "InvalidProtectionMutation",
            "ProtectionTargetMismatch",
            "InitialProtectionMismatch",
            "FinalProtectionMismatch",
            "ProtectionPhysicalMismatch",
            "ProtectionCacheMismatch",
            "LeafReadbackMismatch",
            "InvalidInvlpgStub",
            "InvlpgCodeAllocationMismatch",
            "InvalidInvlpgInvocation",
            "InvlpgTargetMismatch",
            "InvalidObservation",
            "InvocationEntryMismatch",
            "InvocationCountMustBeOne",
            "UnexpectedReturnValue",
            "CanaryMismatch",
            "InvalidTerminalKind",
            "InvalidEventCount",
            "EventCapacityTooSmall",
            "EventCapacityExceedsLimit",
            "MissingEvents",
            "MisalignedEvents",
            "EventSpanOverflow",
            "InvalidEvent",
            "EventOrderMismatch",
            "ExecuteBeforeProtectRejected",
            "ContractLayoutMismatch",
            "InvalidTableStateProtection",
            "TableImageOutsideStateAllocation"
        ];

        return HasExactSequentialByteEnum<
                X64ProtectionProbeStubPhase>(phaseNames)
            && HasExactSequentialByteEnum<
                X64ProtectionProbeTerminalKind>(terminalNames)
            && HasExactSequentialByteEnum<
                X64ProtectionProbeEventKind>(eventNames)
            && HasExactSequentialByteEnum<
                X64ProtectionProbeStatus>(statusNames)
            && (byte)X64ProtectionProbeStubPhase.Uninitialized == 0
            && (byte)X64ProtectionProbeStubPhase.Materialized == 1
            && (byte)X64ProtectionProbeStubPhase.ReadBackVerified == 2
            && (byte)X64ProtectionProbeTerminalKind.ModeledReturn == 1
            && (byte)X64ProtectionProbeTerminalKind.CanaryMismatch == 6
            && (byte)X64ProtectionProbeEventKind.StubMaterialized == 1
            && (byte)X64ProtectionProbeEventKind
                .ConstantReturnAndCanaryObserved == 9
            && (byte)X64ProtectionProbeStatus.Success == 1
            && (byte)X64ProtectionProbeStatus
                .ContractLayoutMismatch == 57
            && (byte)X64ProtectionProbeStatus
                .InvalidTableStateProtection == 58
            && (byte)X64ProtectionProbeStatus
                .TableImageOutsideStateAllocation == 59
            && (ulong)Unsafe.SizeOf<X64ProtectionProbeStubPlanResult>()
                == X64ProtectionProbeStubPlanResult.StructureSize
            && (ulong)Unsafe.SizeOf<X64ProtectionProbeEvent>()
                == X64ProtectionProbeEvent.StructureSize
            && (ulong)Unsafe.SizeOf<X64ProtectionProbeObservation>()
                == X64ProtectionProbeObservation.StructureSize
            && (ulong)Unsafe.SizeOf<X64ProtectionProbePlanResult>()
                == X64ProtectionProbePlanResult.StructureSize
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64ProtectionProbeStubPlanResult>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64ProtectionProbeEvent>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64ProtectionProbeObservation>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    X64ProtectionProbePlanResult>()
            && !default(X64ProtectionProbeStubPlanResult).Succeeded
            && default(X64ProtectionProbeEvent).Validate()
                == X64ProtectionProbeStatus.InvalidEvent
            && default(X64ProtectionProbeObservation).Validate()
                == X64ProtectionProbeStatus.InvalidObservation
            && !default(X64ProtectionProbePlanResult).Succeeded
            && X64ProtectionProbeStubPlan.PageSize == 4096
            && X64ProtectionProbeStubPlan.EntryAlignment == 16
            && X64ProtectionProbeStubPlan.RequiredByteLength == 6
            && X64ProtectionProbeStubPlan.AuditedInstructionCount == 2
            && X64ProtectionProbeStubPlan.ExpectedReturnValue
                == 0x4f53_4f4b
            && X64ProtectionProbeStubPlan.RequiredEventCount == 9
            && X64ProtectionProbeStubPlan.UsesWindowsX64Abi
            && !X64ProtectionProbeStubPlan.HasExternalRelocations
            && !X64ProtectionProbeStubPlan
                .TouchesNonvolatileRegisters
            && !X64ProtectionProbeStubPlan
                .TouchesXmm6ThroughXmm15
            && !X64ProtectionProbeStubPlan
                .PerformsLiveProtectionChange
            && !X64ProtectionProbeStubPlan.PerformsLiveExecution
            && !X64ProtectionProbeStubPlan.HostTestsInvokeBytes
            && !X64ProtectionProbeStubPlan.IsLiveEvidence;
    }

    private static bool TestExactBytesAndHash()
    {
        using ProtectionFixture fixture = new();
        byte[] actual = new byte[
            (int)X64ProtectionProbeStubPlan.RequiredByteLength];
        for (int index = 0; index < actual.Length; index++)
            actual[index] = fixture.ProbePage[index];

        string hash = Convert.ToHexString(SHA256.HashData(actual));
        return fixture.ProbeStub.ReadBackVerified
            && actual.AsSpan().SequenceEqual(
                new byte[] { 0xb8, 0x4b, 0x4f, 0x53, 0x4f, 0xc3 })
            && string.Equals(
                hash,
                ExpectedProbeSha256,
                StringComparison.Ordinal)
            && fixture.ProbePage[actual.Length] == 0xcc
            && fixture.ProbeStub.EntryOffset == 0
            && M01014TestData.Raw(fixture.ProbeStub.EntryAddress)
                == ProbeStart
            && fixture.ProbeStub.ReturnValue
                == X64ProtectionProbeStubPlan.ExpectedReturnValue
            && fixture.ProbeStub.InstructionCount
                == X64ProtectionProbeStubPlan
                    .AuditedInstructionCount;
    }

    private static bool TestMaterializationAndReadbackFailures()
    {
        using ProtectionFixture fixture = new();
        byte* page = (byte*)NativeMemory.AlignedAlloc(
            (nuint)X64ProtectionProbeStubPlan.PageSize,
            (nuint)X64ProtectionProbeStubPlan.PageSize);
        if (page == null)
            throw new OutOfMemoryException();

        try
        {
            new Span<byte>(
                page,
                (int)X64ProtectionProbeStubPlan.PageSize)
                .Fill(0xa7);
            X64ProtectionProbeStubPlanResult missing =
                X64ProtectionProbeStubPlan.Emit(
                    null,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    fixture.ProbeAllocation,
                    fixture.ProbeReservation,
                    0);
            X64ProtectionProbeStubPlanResult shortCapacity =
                X64ProtectionProbeStubPlan.Emit(
                    page,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.RequiredByteLength - 1,
                    fixture.ProbeAllocation,
                    fixture.ProbeReservation,
                    0);
            X64ProtectionProbeStubPlanResult oversized =
                X64ProtectionProbeStubPlan.Emit(
                    page,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize + 1,
                    fixture.ProbeAllocation,
                    fixture.ProbeReservation,
                    0);
            X64ProtectionProbeStubPlanResult misaligned =
                X64ProtectionProbeStubPlan.Emit(
                    page + 1,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    fixture.ProbeAllocation,
                    fixture.ProbeReservation,
                    0);
            X64ProtectionProbeStubPlanResult overflow =
                X64ProtectionProbeStubPlan.Emit(
                    (byte*)0xffff_ffff_ffff_fff0UL,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    fixture.ProbeAllocation,
                    fixture.ProbeReservation,
                    0);
            X64ProtectionProbeStubPlanResult wrongAddress =
                X64ProtectionProbeStubPlan.Emit(
                    page,
                    ProbeStart + X64ProtectionProbeStubPlan.PageSize,
                    X64ProtectionProbeStubPlan.PageSize,
                    fixture.ProbeAllocation,
                    fixture.ProbeReservation,
                    0);
            X64ProtectionProbeStubPlanResult badOffset =
                X64ProtectionProbeStubPlan.Emit(
                    page,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    fixture.ProbeAllocation,
                    fixture.ProbeReservation,
                    1);
            X64ProtectionProbeStubPlanResult outside =
                X64ProtectionProbeStubPlan.Emit(
                    page,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    fixture.ProbeAllocation,
                    fixture.ProbeReservation,
                    X64ProtectionProbeStubPlan.PageSize);
            if (missing.Status
                    != X64ProtectionProbeStatus.MissingDestination ||
                shortCapacity.Status
                    != X64ProtectionProbeStatus
                        .DestinationCapacityTooSmall ||
                oversized.Status
                    != X64ProtectionProbeStatus
                        .DestinationCapacityExceedsPage ||
                misaligned.Status
                    != X64ProtectionProbeStatus
                        .MisalignedDestination ||
                overflow.Status
                    != X64ProtectionProbeStatus
                        .DestinationSpanOverflow ||
                wrongAddress.Status
                    != X64ProtectionProbeStatus
                        .DestinationAddressMismatch ||
                badOffset.Status
                    != X64ProtectionProbeStatus.MisalignedEntry ||
                outside.Status
                    != X64ProtectionProbeStatus.EntryOutsideProbePage)
            {
                return false;
            }

            ReadOnlySpan<byte> untouched = new(
                page,
                (int)X64ProtectionProbeStubPlan.PageSize);
            foreach (byte value in untouched)
            {
                if (value != 0xa7)
                    return false;
            }

            X64ProtectionProbeStubPlanResult emitted =
                X64ProtectionProbeStubPlan.Emit(
                    page,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    fixture.ProbeAllocation,
                    fixture.ProbeReservation,
                    0);
            if (!emitted.Succeeded)
                return false;

            X64ProtectionProbeStubPlanResult readMissing =
                X64ProtectionProbeStubPlan.ValidateReadback(
                    null,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    emitted);
            X64ProtectionProbeStubPlanResult readShort =
                X64ProtectionProbeStubPlan.ValidateReadback(
                    page,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.RequiredByteLength - 1,
                    emitted);
            X64ProtectionProbeStubPlanResult readOversized =
                X64ProtectionProbeStubPlan.ValidateReadback(
                    page,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize + 1,
                    emitted);
            X64ProtectionProbeStubPlanResult readMisaligned =
                X64ProtectionProbeStubPlan.ValidateReadback(
                    page + 1,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    emitted);
            X64ProtectionProbeStubPlanResult readOverflow =
                X64ProtectionProbeStubPlan.ValidateReadback(
                    (byte*)0xffff_ffff_ffff_fff0UL,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    emitted);
            X64ProtectionProbeStubPlanResult readWrongAddress =
                X64ProtectionProbeStubPlan.ValidateReadback(
                    page,
                    ProbeStart + X64ProtectionProbeStubPlan.PageSize,
                    X64ProtectionProbeStubPlan.PageSize,
                    emitted);
            X64ProtectionProbeStubPlanResult defaultExpected =
                X64ProtectionProbeStubPlan.ValidateReadback(
                    page,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    default);
            page[2] ^= 0xff;
            X64ProtectionProbeStubPlanResult mismatch =
                X64ProtectionProbeStubPlan.ValidateReadback(
                    page,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    emitted);

            return readMissing.Status
                    == X64ProtectionProbeStatus.MissingSource
                && readShort.Status
                    == X64ProtectionProbeStatus.SourceCapacityTooSmall
                && readOversized.Status
                    == X64ProtectionProbeStatus.SourceCapacityExceedsPage
                && readMisaligned.Status
                    == X64ProtectionProbeStatus.MisalignedSource
                && readOverflow.Status
                    == X64ProtectionProbeStatus.SourceSpanOverflow
                && readWrongAddress.Status
                    == X64ProtectionProbeStatus.SourceAddressMismatch
                && defaultExpected.Status
                    == X64ProtectionProbeStatus
                        .ExpectedStubNotMaterialized
                && mismatch.Status
                    == X64ProtectionProbeStatus.ByteReadbackMismatch;
        }
        finally
        {
            NativeMemory.AlignedFree(page);
        }
    }

    private static bool TestCompleteAudit()
    {
        using ProtectionFixture fixture = new();
        X64ProtectionProbePlanResult result = fixture.Audit();
        X64PageTableEntryResult before =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                fixture.ProtectionMutation.PreviousEntry.RawValue,
                fixture.PageTables.Context);
        X64PageTableEntryResult after =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                fixture.ProtectionMutation.ReplacementEntry.RawValue,
                fixture.PageTables.Context);

        return result.Succeeded
            && result.Status == X64ProtectionProbeStatus.Success
            && result.StubStatus == X64ProtectionProbeStatus.Success
            && result.PageMapStatus == X64PageMapStatus.Success
            && result.InvlpgStatus
                == X64InvlpgStubPlanStatus.Success
            && result.TerminalKind
                == X64ProtectionProbeTerminalKind.ModeledReturn
            && result.EventCount == 9
            && result.EventCapacity == 9
            && result.ValidatedEventCount == 9
            && result.InvalidEventIndex
                == X64ProtectionProbePlanResult.NoIndex
            && result.LeafReadbackRaw
                == fixture.ProtectionMutation.ReplacementEntry.RawValue
            && result.ProbeStub.ReadBackVerified
            && result.InvlpgStub.ReadBackVerified
            && result.InvlpgInvocation.Succeeded
            && !result.Observation.IsLiveEvidence
            && before.Succeeded
            && after.Succeeded
            && before.Protection == M01014TestData.ReadWrite
            && after.Protection == M01014TestData.ReadExecute
            && before.PhysicalAddress.Equals(after.PhysicalAddress)
            && M01014TestData.Raw(after.PhysicalAddress) == ProbeStart
            && before.CachePolicy == PageCachePolicy.WriteBack
            && after.CachePolicy == PageCachePolicy.WriteBack
            && M01014TestData.Raw(
                result.ProtectionMutation
                    .PageImagePhysicalStart)
                == TableStateStart
            && M01014TestData.Raw(
                result.ProtectionMutation
                    .PageImagePhysicalEndExclusive)
                == TableStateStart + 4 * X64PageMap.PageSize
            && result.InvlpgInvocation.Request.Target.Target.Equals(
                result.ProtectionMutation.InvalidationRange
                    .VirtualStart)
            && result.Observation.ObservedReturnValue
                == X64ProtectionProbeStubPlan.ExpectedReturnValue
            && result.Observation.CanaryBefore == Canary
            && result.Observation.CanaryAfter == Canary;
    }

    private static bool TestAllocationAndReservationFailures()
    {
        using ProtectionFixture fixture = new();
        BootMemoryRegionRecord emptyAllocation = default;
        BootReservedRange emptyReservation = default;
        BootMemoryRegionRecord wrongType = Record(
            UefiMemoryType.LoaderData,
            ProbeStart,
            1);
        BootMemoryRegionRecord nonIdentity = new(
            (uint)UefiMemoryType.LoaderCode,
            ProbeStart,
            ProbeStart + X64ProtectionProbeStubPlan.PageSize,
            1,
            0,
            X64ProtectionProbeStubPlan.PageSize,
            ProbeStart + X64ProtectionProbeStubPlan.PageSize);
        BootMemoryRegionRecord twoPageProbe = Record(
            UefiMemoryType.LoaderCode,
            ProbeStart,
            2);
        BootMemoryRegionRecord invalidTransitionType = Record(
            UefiMemoryType.LoaderData,
            TransitionCodeStart,
            1);
        BootMemoryRegionRecord invalidTableType = Record(
            UefiMemoryType.LoaderCode,
            TableStateStart,
            4);
        BootMemoryRegionRecord nonCanonicalTransition = new(
            (uint)UefiMemoryType.LoaderCode,
            TransitionCodeStart,
            X64TlbInvalidationPlan.LowCanonicalEndExclusive,
            1,
            0,
            X64ProtectionProbeStubPlan.PageSize,
            TransitionCodeStart
                + X64ProtectionProbeStubPlan.PageSize);
        BootReservedRange.TryCreate(
            BootReservationKind.DiagnosticState,
            M01014TestData.Physical(ProbeStart),
            1,
            out BootReservedRange wrongKind);
        BootReservedRange.TryCreate(
            BootReservationKind.PageProtectionProbe,
            M01014TestData.Physical(
                ProbeStart + X64ProtectionProbeStubPlan.PageSize),
            1,
            out BootReservedRange wrongRange);

        return fixture.Audit(probeAllocation: emptyAllocation).Status
                == X64ProtectionProbeStatus
                    .ProbeAllocationMustBeLoaderCode
            && fixture.Audit(probeAllocation: wrongType).Status
                == X64ProtectionProbeStatus
                    .ProbeAllocationMustBeLoaderCode
            && fixture.Audit(probeAllocation: nonIdentity).Status
                == X64ProtectionProbeStatus
                    .ProbeAllocationNotIdentityMapped
            && fixture.Audit(probeAllocation: twoPageProbe).Status
                == X64ProtectionProbeStatus
                    .ProbeAllocationMustBeOnePage
            && fixture.Audit(
                    transitionCodeAllocation:
                        invalidTransitionType).Status
                == X64ProtectionProbeStatus
                    .InvalidTransitionCodeAllocation
            && fixture.Audit(
                    tableStateAllocation: invalidTableType).Status
                == X64ProtectionProbeStatus
                    .InvalidTableStateAllocation
            && fixture.Audit(
                    transitionCodeAllocation:
                        nonCanonicalTransition).Status
                == X64ProtectionProbeStatus
                    .InvalidTransitionCodeAllocation
            && fixture.Audit(probeReservation: emptyReservation).Status
                == X64ProtectionProbeStatus
                    .InvalidProbeReservation
            && fixture.Audit(probeReservation: wrongKind).Status
                == X64ProtectionProbeStatus
                    .ProbeReservationKindMismatch
            && fixture.Audit(probeReservation: wrongRange).Status
                == X64ProtectionProbeStatus
                    .ProbeReservationRangeMismatch
            && fixture.Audit(
                    tableStateProtection:
                        M01014TestData.ReadExecute).Status
                == X64ProtectionProbeStatus
                    .InvalidTableStateProtection;
    }

    private static bool TestAllocationOverlap()
    {
        using ProtectionFixture fixture = new();
        BootMemoryRegionRecord transitionOverProbe = Record(
            UefiMemoryType.LoaderCode,
            ProbeStart,
            1);
        BootMemoryRegionRecord tableOverTransition = Record(
            UefiMemoryType.LoaderData,
            TransitionCodeStart,
            4);
        BootMemoryRegionRecord transitionVirtualOverProbe = new(
            (uint)UefiMemoryType.LoaderCode,
            TransitionCodeStart,
            ProbeStart,
            1,
            0,
            X64ProtectionProbeStubPlan.PageSize,
            TransitionCodeStart
                + X64ProtectionProbeStubPlan.PageSize);
        BootMemoryRegionRecord tableVirtualOverTransition = new(
            (uint)UefiMemoryType.LoaderData,
            TableStateStart,
            TransitionCodeStart,
            4,
            0,
            4 * X64ProtectionProbeStubPlan.PageSize,
            TableStateStart
                + 4 * X64ProtectionProbeStubPlan.PageSize);

        return fixture.Audit(
                    transitionCodeAllocation: transitionOverProbe)
                .Status == X64ProtectionProbeStatus.AllocationOverlap
            && fixture.Audit(
                    tableStateAllocation: tableOverTransition)
                .Status == X64ProtectionProbeStatus.AllocationOverlap
            && fixture.Audit(
                    transitionCodeAllocation:
                        transitionVirtualOverProbe)
                .Status == X64ProtectionProbeStatus.AllocationOverlap
            && fixture.Audit(
                    tableStateAllocation:
                        tableVirtualOverTransition)
                .Status == X64ProtectionProbeStatus.AllocationOverlap;
    }

    private static bool TestTableImageAllocationBinding()
    {
        using ProtectionFixture fixture = new();
        BootMemoryRegionRecord unrelatedArena = Record(
            UefiMemoryType.LoaderData,
            0x0090_0000,
            4);
        BootMemoryRegionRecord truncatedArena = Record(
            UefiMemoryType.LoaderData,
            TableStateStart,
            3);

        return fixture.Audit(
                    tableStateAllocation: unrelatedArena)
                .Status
                    == X64ProtectionProbeStatus
                        .TableImageOutsideStateAllocation
            && fixture.Audit(
                    tableStateAllocation: truncatedArena)
                .Status
                    == X64ProtectionProbeStatus
                        .TableImageOutsideStateAllocation;
    }

    private static bool TestProbeStubAllocationBinding()
    {
        const ulong otherProbeStart = 0x00a0_0000;
        using ProtectionFixture fixture = new();
        BootMemoryRegionRecord otherAllocation = Record(
            UefiMemoryType.LoaderCode,
            otherProbeStart,
            1);
        BootReservedRangeStatus reservationStatus =
            BootReservedRange.TryCreate(
                BootReservationKind.PageProtectionProbe,
                M01014TestData.Physical(otherProbeStart),
                1,
                out BootReservedRange otherReservation);
        byte* page = (byte*)NativeMemory.AlignedAlloc(
            (nuint)X64ProtectionProbeStubPlan.PageSize,
            (nuint)X64ProtectionProbeStubPlan.PageSize);
        if (page == null)
            throw new OutOfMemoryException();

        try
        {
            new Span<byte>(
                page,
                (int)X64ProtectionProbeStubPlan.PageSize)
                .Fill(0xcc);
            X64ProtectionProbeStubPlanResult emitted =
                X64ProtectionProbeStubPlan.Emit(
                    page,
                    otherProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    otherAllocation,
                    otherReservation,
                    0);
            X64ProtectionProbeStubPlanResult verified =
                X64ProtectionProbeStubPlan.ValidateReadback(
                    page,
                    otherProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    emitted);
            X64ProtectionProbePlanResult rebound =
                fixture.Audit(probeStub: verified);

            return reservationStatus
                    == BootReservedRangeStatus.Success
                && verified.ReadBackVerified
                && M01014TestData.Raw(verified.ProbePageStart)
                    == otherProbeStart
                && rebound.Status
                    == X64ProtectionProbeStatus
                        .StubAllocationMismatch;
        }
        finally
        {
            NativeMemory.AlignedFree(page);
        }
    }

    private static bool TestObservationAndLeafFailures()
    {
        using ProtectionFixture fixture = new();
        X64ProtectionProbeObservation wrongReturn =
            X64ProtectionProbeObservation.CreateModeled(
                X64ProtectionProbeTerminalKind.ModeledReturn,
                fixture.ProbeStub.EntryAddress,
                1,
                X64ProtectionProbeStubPlan.ExpectedReturnValue ^ 1U,
                Canary,
                Canary);
        X64ProtectionProbeObservation wrongCanary =
            X64ProtectionProbeObservation.CreateModeled(
                X64ProtectionProbeTerminalKind.ModeledReturn,
                fixture.ProbeStub.EntryAddress,
                1,
                X64ProtectionProbeStubPlan.ExpectedReturnValue,
                Canary,
                Canary ^ 1UL);
        X64ProtectionProbeObservation wrongCount =
            X64ProtectionProbeObservation.CreateModeled(
                X64ProtectionProbeTerminalKind.ModeledReturn,
                fixture.ProbeStub.EntryAddress,
                2,
                X64ProtectionProbeStubPlan.ExpectedReturnValue,
                Canary,
                Canary);
        X64ProtectionProbeObservation wrongEntry =
            X64ProtectionProbeObservation.CreateModeled(
                X64ProtectionProbeTerminalKind.ModeledReturn,
                M01014TestData.VirtualUnaligned(
                    ProbeStart + 0x10),
                1,
                X64ProtectionProbeStubPlan.ExpectedReturnValue,
                Canary,
                Canary);
        X64ProtectionProbeObservation timeout =
            X64ProtectionProbeObservation.CreateModeled(
                X64ProtectionProbeTerminalKind.Timeout,
                fixture.ProbeStub.EntryAddress,
                1,
                X64ProtectionProbeStubPlan.ExpectedReturnValue,
                Canary,
                Canary);
        X64ProtectionProbeObservation zeroCanary =
            X64ProtectionProbeObservation.CreateModeled(
                X64ProtectionProbeTerminalKind.ModeledReturn,
                fixture.ProbeStub.EntryAddress,
                1,
                X64ProtectionProbeStubPlan.ExpectedReturnValue,
                0,
                0);
        X64ProtectionProbeObservation invalidObservation = default;

        return fixture.Audit(
                    leafReadbackRaw:
                        fixture.ProtectionMutation
                            .ReplacementEntry.RawValue ^ 1UL)
                .Status
                    == X64ProtectionProbeStatus
                        .LeafReadbackMismatch
            && fixture.Audit(observation: wrongReturn).Status
                == X64ProtectionProbeStatus.UnexpectedReturnValue
            && fixture.Audit(observation: wrongCanary).Status
                == X64ProtectionProbeStatus.CanaryMismatch
            && fixture.Audit(observation: wrongCount).Status
                == X64ProtectionProbeStatus
                    .InvocationCountMustBeOne
            && fixture.Audit(observation: wrongEntry).Status
                == X64ProtectionProbeStatus
                    .InvocationEntryMismatch
            && fixture.Audit(observation: timeout).Status
                == X64ProtectionProbeStatus.InvalidTerminalKind
            && fixture.Audit(observation: zeroCanary).Status
                == X64ProtectionProbeStatus.CanaryMismatch
            && fixture.Audit(
                    observation: invalidObservation).Status
                == X64ProtectionProbeStatus.InvalidObservation;
    }

    private static bool TestEventFailures()
    {
        using ProtectionFixture fixture = new();
        X64ProtectionProbeEvent[] reordered =
            (X64ProtectionProbeEvent[])fixture.Events.Clone();
        reordered[4] = new X64ProtectionProbeEvent(
            X64ProtectionProbeEventKind.LeafReadBackVerified,
            3);
        X64ProtectionProbeEvent[] executeFirst =
            (X64ProtectionProbeEvent[])fixture.Events.Clone();
        executeFirst[0] = new X64ProtectionProbeEvent(
            X64ProtectionProbeEventKind.ProbeInvocationPlanned,
            0);
        X64ProtectionProbeEvent[] executeAtProtection =
            (X64ProtectionProbeEvent[])fixture.Events.Clone();
        executeAtProtection[3] = new X64ProtectionProbeEvent(
            X64ProtectionProbeEventKind.ProbeInvocationPlanned,
            3);
        X64ProtectionProbeEvent[] invalid =
            (X64ProtectionProbeEvent[])fixture.Events.Clone();
        invalid[2] = default;

        X64ProtectionProbePlanResult reorderedResult =
            fixture.Audit(events: reordered);
        X64ProtectionProbePlanResult executeFirstResult =
            fixture.Audit(events: executeFirst);
        X64ProtectionProbePlanResult executeAtProtectionResult =
            fixture.Audit(events: executeAtProtection);
        X64ProtectionProbePlanResult invalidResult =
            fixture.Audit(events: invalid);
        X64ProtectionProbePlanResult misalignedEvents =
            fixture.AuditWithEventPointer(
                (X64ProtectionProbeEvent*)1UL,
                9,
                9);
        X64ProtectionProbePlanResult overflowingEvents =
            fixture.AuditWithEventPointer(
                (X64ProtectionProbeEvent*)
                    0xffff_ffff_ffff_fff8UL,
                9,
                9);
        return fixture.Audit(eventCount: 8).Status
                == X64ProtectionProbeStatus.InvalidEventCount
            && fixture.Audit(nullEvents: true).Status
                == X64ProtectionProbeStatus.MissingEvents
            && fixture.Audit(eventCapacity: 8).Status
                == X64ProtectionProbeStatus.EventCapacityTooSmall
            && fixture.Audit(eventCapacity: 10).Status
                == X64ProtectionProbeStatus
                    .EventCapacityExceedsLimit
            && reorderedResult.Status
                == X64ProtectionProbeStatus.EventOrderMismatch
            && reorderedResult.InvalidEventIndex == 4
            && executeFirstResult.Status
                == X64ProtectionProbeStatus
                    .ExecuteBeforeProtectRejected
            && executeFirstResult.InvalidEventIndex == 0
            && executeAtProtectionResult.Status
                == X64ProtectionProbeStatus
                    .ExecuteBeforeProtectRejected
            && executeAtProtectionResult.InvalidEventIndex == 3
            && invalidResult.Status
                == X64ProtectionProbeStatus.InvalidEvent
            && invalidResult.InvalidEventIndex == 2
            && invalidResult.ValidatedEventCount == 2
            && misalignedEvents.Status
                == X64ProtectionProbeStatus.MisalignedEvents
            && overflowingEvents.Status
                == X64ProtectionProbeStatus.EventSpanOverflow;
    }

    private static bool TestInvlpgFailures()
    {
        using ProtectionFixture fixture = new();
        fixture.CreateInvlpgEvidence(
            M01014TestData.Virtual(ProbeStart + 0x1000),
            M01014TestData.ReadExecute,
            out X64InvlpgStubPlanResult otherTargetStub,
            out X64InvlpgInvocationPlanResult otherTargetInvocation);
        PageProtection writableExecutable = (PageProtection)(
            (byte)PageProtection.Read
            | (byte)PageProtection.Write
            | (byte)PageProtection.Execute);
        X64InvlpgInvocationRequest writableRequest =
            X64InvlpgInvocationRequest.Create(
                fixture.InvlpgStub.EntryAddress,
                fixture.InvlpgStub.Target,
                writableExecutable,
                1);
        X64InvlpgInvocationPlanResult writableInvocation =
            X64InvlpgStubPlan.CreateInvocationPlan(
                fixture.InvlpgStub,
                writableRequest);
        X64InvlpgStubPlanResult emptyStub = default;
        X64ProtectionProbePlanResult invalidStubResult =
            fixture.Audit(invlpgStub: emptyStub);

        return otherTargetStub.ReadBackVerified
            && otherTargetInvocation.Succeeded
            && fixture.Audit(
                    invlpgStub: otherTargetStub,
                    invlpgInvocation: otherTargetInvocation)
                .Status
                    == X64ProtectionProbeStatus
                        .InvlpgTargetMismatch
            && fixture.Audit(
                    invlpgStub: otherTargetStub)
                .Status
                    == X64ProtectionProbeStatus
                        .InvlpgTargetMismatch
            && writableInvocation.Status
                == X64InvlpgStubPlanStatus
                    .WritableExecutableCodeRejected
            && fixture.Audit(
                    invlpgInvocation: writableInvocation)
                .Status
                    == X64ProtectionProbeStatus
                        .InvalidInvlpgInvocation
            && invalidStubResult.Status
                == X64ProtectionProbeStatus.InvalidInvlpgStub
            && invalidStubResult.InvlpgStatus
                == X64InvlpgStubPlanStatus.Uninitialized;
    }

    private static bool TestSourceBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string production = File.ReadAllText(
            Path.Combine(
                root,
                @"src\OS.Kernel\Memory\X64ProtectionProbeStubPlan.cs"));
        string testSource = File.ReadAllText(
            Path.Combine(
                root,
                @"tests\OS.HostTests\X64ProtectionProbeStubPlanTests.cs"));

        return production.Contains(
                "PerformsLiveProtectionChange = false",
                StringComparison.Ordinal)
            && production.Contains(
                "PerformsLiveExecution = false",
                StringComparison.Ordinal)
            && production.Contains(
                "HostTestsInvokeBytes = false",
                StringComparison.Ordinal)
            && production.Contains(
                "IsLiveEvidence = false",
                StringComparison.Ordinal)
            && !production.Contains(
                "delegate" + "*",
                StringComparison.Ordinal)
            && !production.Contains(
                "VirtualProtect",
                StringComparison.Ordinal)
            && !production.Contains(
                "GetDelegateForFunctionPointer",
                StringComparison.Ordinal)
            && !testSource.Contains(
                "delegate" + "*",
                StringComparison.Ordinal)
            && !X64ProtectionProbeStubPlan
                .PerformsLiveProtectionChange
            && !X64ProtectionProbeStubPlan.PerformsLiveExecution
            && !X64ProtectionProbeStubPlan.HostTestsInvokeBytes
            && !X64ProtectionProbeStubPlan.IsLiveEvidence;
    }

    private static BootMemoryRegionRecord Record(
        UefiMemoryType type,
        ulong start,
        ulong pages)
    {
        ulong bytes = pages * BootMemoryRegionRecord.PageSize;
        return new BootMemoryRegionRecord(
            (uint)type,
            start,
            start,
            pages,
            0,
            bytes,
            start + bytes);
    }

    private static string Normalize(string value)
    {
        return value
            .Replace(' ', '_')
            .Replace('-', '_');
    }

    private static bool HasExactSequentialByteEnum<T>(
        string[] expectedNames)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
            return false;

        string[] names = Enum.GetNames<T>();
        T[] values = Enum.GetValues<T>();
        if (names.Length != expectedNames.Length ||
            values.Length != expectedNames.Length)
        {
            return false;
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (!string.Equals(
                    names[index],
                    expectedNames[index],
                    StringComparison.Ordinal) ||
                Convert.ToByte(values[index]) != index)
            {
                return false;
            }
        }

        return true;
    }

    private sealed unsafe class ProtectionFixture : IDisposable
    {
        private const ulong InvlpgEntryOffset = 0x100;

        private byte* _probePage;
        private byte* _invlpgPage;

        internal byte* ProbePage => _probePage;

        internal M01014PageTableFixture PageTables { get; }

        internal BootMemoryRegionRecord ProbeAllocation { get; }

        internal BootReservedRange ProbeReservation { get; }

        internal BootMemoryRegionRecord TransitionCodeAllocation { get; }

        internal BootMemoryRegionRecord TableStateAllocation { get; }

        internal X64ProtectionProbeStubPlanResult ProbeStub { get; }

        internal X64PageMapResult ProtectionMutation { get; }

        internal ulong LeafReadbackRaw { get; }

        internal X64InvlpgStubPlanResult InvlpgStub { get; }

        internal X64InvlpgInvocationPlanResult InvlpgInvocation { get; }

        internal X64ProtectionProbeObservation Observation { get; }

        internal X64ProtectionProbeEvent[] Events { get; }

        internal ProtectionFixture()
        {
            _probePage = (byte*)NativeMemory.AlignedAlloc(
                (nuint)X64ProtectionProbeStubPlan.PageSize,
                (nuint)X64ProtectionProbeStubPlan.PageSize);
            _invlpgPage = (byte*)NativeMemory.AlignedAlloc(
                (nuint)X64InvlpgStubPlan.PageSize,
                (nuint)X64InvlpgStubPlan.PageSize);
            if (_probePage == null || _invlpgPage == null)
                throw new OutOfMemoryException();

            Fill(_probePage, X64ProtectionProbeStubPlan.PageSize, 0xcc);
            Fill(_invlpgPage, X64InvlpgStubPlan.PageSize, 0xcc);

            ProbeAllocation = Record(
                UefiMemoryType.LoaderCode,
                ProbeStart,
                1);
            TransitionCodeAllocation = Record(
                UefiMemoryType.LoaderCode,
                TransitionCodeStart,
                1);
            TableStateAllocation = Record(
                UefiMemoryType.LoaderData,
                TableStateStart,
                4);
            BootReservedRangeStatus reservationStatus =
                BootReservedRange.TryCreate(
                    BootReservationKind.PageProtectionProbe,
                    M01014TestData.Physical(ProbeStart),
                    1,
                    out BootReservedRange reservation);
            if (reservationStatus != BootReservedRangeStatus.Success)
            {
                throw new InvalidOperationException(
                    "Probe reservation failed: " + reservationStatus);
            }
            ProbeReservation = reservation;

            X64ProtectionProbeStubPlanResult emitted =
                X64ProtectionProbeStubPlan.Emit(
                    _probePage,
                    ProbeStart,
                    X64ProtectionProbeStubPlan.PageSize,
                    ProbeAllocation,
                    ProbeReservation,
                    entryOffset: 0);
            ProbeStub = X64ProtectionProbeStubPlan.ValidateReadback(
                _probePage,
                ProbeStart,
                X64ProtectionProbeStubPlan.PageSize,
                emitted);
            if (!ProbeStub.ReadBackVerified)
            {
                throw new InvalidOperationException(
                    "Probe readback failed: " + ProbeStub.Status);
            }

            PageTables = new M01014PageTableFixture(
                ProbeStart,
                TableStateStart);
            X64PageMapResult mapped = PageTables.Apply(
                X64PageMapRequest.CreateMap(
                    PageTables.Root,
                    PageTables.Target,
                    M01014TestData.Physical(ProbeStart),
                    M01014TestData.ReadWrite,
                    PageCachePolicy.WriteBack,
                    PageTables.Context));
            if (!mapped.Succeeded)
            {
                throw new InvalidOperationException(
                    "Probe map failed: " + mapped.Status);
            }
            ProtectionMutation = PageTables.Apply(
                X64PageMapRequest.CreateProtect(
                    PageTables.Root,
                    PageTables.Target,
                    M01014TestData.ReadExecute,
                    X64HardwareStatePolicy.Preserve,
                    PageTables.Context));
            if (!ProtectionMutation.Succeeded)
            {
                throw new InvalidOperationException(
                    "Probe protect failed: "
                    + ProtectionMutation.Status);
            }
            LeafReadbackRaw = PageTables.Entries[
                checked((int)ProtectionMutation.TargetEntryIndex)];

            CreateInvlpgEvidence(
                PageTables.Target,
                M01014TestData.ReadExecute,
                out X64InvlpgStubPlanResult invlpgStub,
                out X64InvlpgInvocationPlanResult invlpgInvocation);
            InvlpgStub = invlpgStub;
            InvlpgInvocation = invlpgInvocation;
            if (!InvlpgStub.ReadBackVerified ||
                !InvlpgInvocation.Succeeded)
            {
                throw new InvalidOperationException(
                    "INVLPG evidence failed: "
                    + InvlpgStub.Status
                    + "/"
                    + InvlpgInvocation.Status);
            }

            Observation =
                X64ProtectionProbeObservation.CreateModeled(
                    X64ProtectionProbeTerminalKind.ModeledReturn,
                    ProbeStub.EntryAddress,
                    1,
                    X64ProtectionProbeStubPlan.ExpectedReturnValue,
                    Canary,
                    Canary);
            Events = CreateEvents();
        }

        internal X64ProtectionProbePlanResult Audit(
            BootMemoryRegionRecord? probeAllocation = null,
            BootReservedRange? probeReservation = null,
            BootMemoryRegionRecord? transitionCodeAllocation = null,
            BootMemoryRegionRecord? tableStateAllocation = null,
            PageProtection? tableStateProtection = null,
            X64ProtectionProbeStubPlanResult? probeStub = null,
            X64PageMapResult? protectionMutation = null,
            ulong? leafReadbackRaw = null,
            X64InvlpgStubPlanResult? invlpgStub = null,
            X64InvlpgInvocationPlanResult? invlpgInvocation = null,
            X64ProtectionProbeObservation? observation = null,
            X64ProtectionProbeEvent[]? events = null,
            ulong? eventCount = null,
            ulong? eventCapacity = null,
            bool nullEvents = false)
        {
            X64ProtectionProbeEvent[] selectedEvents =
                events ?? Events;
            if (nullEvents)
            {
                return X64ProtectionProbeStubPlan.Audit(
                    probeAllocation ?? ProbeAllocation,
                    probeReservation ?? ProbeReservation,
                    transitionCodeAllocation
                        ?? TransitionCodeAllocation,
                    tableStateAllocation ?? TableStateAllocation,
                    tableStateProtection
                        ?? M01014TestData.ReadWrite,
                    probeStub ?? ProbeStub,
                    protectionMutation ?? ProtectionMutation,
                    leafReadbackRaw ?? LeafReadbackRaw,
                    invlpgStub ?? InvlpgStub,
                    invlpgInvocation ?? InvlpgInvocation,
                    observation ?? Observation,
                    null,
                    eventCount ?? (ulong)Events.Length,
                    eventCapacity ?? (ulong)Events.Length);
            }

            fixed (X64ProtectionProbeEvent* eventPointer =
                selectedEvents)
            {
                return X64ProtectionProbeStubPlan.Audit(
                    probeAllocation ?? ProbeAllocation,
                    probeReservation ?? ProbeReservation,
                    transitionCodeAllocation
                        ?? TransitionCodeAllocation,
                    tableStateAllocation ?? TableStateAllocation,
                    tableStateProtection
                        ?? M01014TestData.ReadWrite,
                    probeStub ?? ProbeStub,
                    protectionMutation ?? ProtectionMutation,
                    leafReadbackRaw ?? LeafReadbackRaw,
                    invlpgStub ?? InvlpgStub,
                    invlpgInvocation ?? InvlpgInvocation,
                    observation ?? Observation,
                    eventPointer,
                    eventCount ?? (ulong)selectedEvents.Length,
                    eventCapacity ?? (ulong)selectedEvents.Length);
            }
        }

        internal X64ProtectionProbePlanResult AuditWithEventPointer(
            X64ProtectionProbeEvent* events,
            ulong eventCount,
            ulong eventCapacity)
        {
            return X64ProtectionProbeStubPlan.Audit(
                ProbeAllocation,
                ProbeReservation,
                TransitionCodeAllocation,
                TableStateAllocation,
                M01014TestData.ReadWrite,
                ProbeStub,
                ProtectionMutation,
                LeafReadbackRaw,
                InvlpgStub,
                InvlpgInvocation,
                Observation,
                events,
                eventCount,
                eventCapacity);
        }

        internal void CreateInvlpgEvidence(
            VirtualAddress target,
            PageProtection codeProtection,
            out X64InvlpgStubPlanResult verifiedStub,
            out X64InvlpgInvocationPlanResult invocation)
        {
            if (X64TlbInvalidationPlan.TryCreate(
                    target,
                    X64TlbInvalidationPlan.PageSize,
                    out X64TlbInvalidationRange range)
                != X64TlbInvalidationStatus.Success)
            {
                verifiedStub = default;
                invocation = default;
                return;
            }

            X64TlbInvalidationRecord* records =
                stackalloc X64TlbInvalidationRecord[1];
            X64TlbInvalidationPlanResult materialized =
                X64TlbInvalidationPlan.Materialize(
                    range,
                    records,
                    1);
            if (!materialized.Succeeded)
            {
                verifiedStub = default;
                invocation = default;
                return;
            }

            X64InvlpgStubPlanResult emitted =
                X64InvlpgStubPlan.Emit(
                    _invlpgPage,
                    TransitionCodeStart,
                    X64InvlpgStubPlan.PageSize,
                    M01014TestData.Virtual(TransitionCodeStart),
                    InvlpgEntryOffset,
                    records[0]);
            verifiedStub = X64InvlpgStubPlan.ValidateReadback(
                _invlpgPage,
                TransitionCodeStart,
                X64InvlpgStubPlan.PageSize,
                emitted);
            X64InvlpgInvocationRequest request =
                X64InvlpgInvocationRequest.Create(
                    verifiedStub.EntryAddress,
                    records[0],
                    codeProtection,
                    1);
            invocation = X64InvlpgStubPlan.CreateInvocationPlan(
                verifiedStub,
                request);
        }

        public void Dispose()
        {
            PageTables?.Dispose();
            byte* probe = _probePage;
            byte* invlpg = _invlpgPage;
            _probePage = null;
            _invlpgPage = null;
            if (probe != null)
                NativeMemory.AlignedFree(probe);
            if (invlpg != null)
                NativeMemory.AlignedFree(invlpg);
        }

        private static X64ProtectionProbeEvent[] CreateEvents()
        {
            X64ProtectionProbeEvent[] events =
                new X64ProtectionProbeEvent[
                    X64ProtectionProbeStubPlan.RequiredEventCount];
            for (int index = 0; index < events.Length; index++)
            {
                events[index] = new X64ProtectionProbeEvent(
                    (X64ProtectionProbeEventKind)(index + 1),
                    (ulong)index);
            }

            return events;
        }

        private static void Fill(
            byte* destination,
            ulong length,
            byte value)
        {
            for (ulong index = 0; index < length; index++)
                destination[index] = value;
        }
    }
}
