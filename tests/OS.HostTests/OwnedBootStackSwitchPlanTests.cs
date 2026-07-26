using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Memory;

internal static class OwnedBootStackSwitchPlanTests
{
    private const string IssueTestName = "m01.005";
    private const ulong CodePhysicalStart = 0x0020_0000;
    private const ulong CodeVirtualStart = 0x0040_0000;
    private const ulong DataPhysicalStart = 0x0030_0000;
    private const ulong DataVirtualStart = 0x0050_0000;
    private const ulong HandoffVirtualStart = 0x0070_0000;
    private const ulong ContinuationVirtualAddress = 0x0080_0010;
    private const ulong StateStorageVirtualStart = 0x0090_0000;
    private const ulong BoundStubEntryOffset = 16;
    private const int PageSize =
        (int)BootMemoryRegionRecord.PageSize;
    private const int PageScratchSize = PageSize * 2;
    private const ulong GuardPages = 1;
    private const ulong StackPages = 3;
    private const ulong ArenaPages = GuardPages + StackPages;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run(
            "m01.005 statuses states and pod layouts are stable",
            TestStableSurface,
            ref failures);
        Run(
            "m01.005 default values fail closed",
            TestDefaultsFailClosed,
            ref failures);
        Run(
            "m01.005 loader code data layout and abi plan succeeds",
            TestSuccessfulPlanAndAbi,
            ref failures);
        Run(
            "m01.005 allocation pointer metadata fails before dereference",
            TestPointerMetadata,
            ref failures);
        Run(
            "m01.005 allocation record failures are classified",
            TestAllocationRecordFailures,
            ref failures);
        Run(
            "m01.005 allocations layout and arena boundaries are exact",
            TestAllocationOverlapLayoutAndArena,
            ref failures);
        Run(
            "m01.005 stub entry and declared bytes remain bounded",
            TestStubEntryAndDeclaredLength,
            ref failures);
        Run(
            "m01.005 static continuation and handoff are range checked",
            TestContinuationAndHandoff,
            ref failures);
        Run(
            "m01.005 exact audited switch bytes are deterministic",
            TestAuditedBytes,
            ref failures);
        Run(
            "m01.005 byte materialization readback fails closed",
            TestByteMaterializationFailures,
            ref failures);
        Run(
            "m01.005 one shot arm transfer and observation succeed",
            TestSuccessfulOneShotSequence,
            ref failures);
        Run(
            "m01.005 repeated illegal and unexpected return states fail",
            TestIllegalOneShotStates,
            ref failures);
        Run(
            "m01.005 rsp audit requires the exact planned value",
            TestRspAuditFailures,
            ref failures);
        Run(
            "m01.005 stub guard and stack remain ledger compatible",
            TestLedgerCompatibility,
            ref failures);
        Run(
            "m01.005 malformed plans and results cannot report success",
            TestMalformedValues,
            ref failures);
        Run(
            "m01.005 planning is deterministic immutable and allocation free",
            TestDeterminismImmutabilityAndAllocation,
            ref failures);
        Run(
            "m01.005 source excludes live switch and hosted dependencies",
            TestSourceBoundary,
            ref failures);
        Run(
            "m01.005 exact sources compile under nostdlib efi",
            TestNoStdLibEfiCompileClosure,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-005 owned boot-stack switch plan host tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_005_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(string name, Func<bool> test, ref int failures)
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

    private static bool TestStableSurface()
    {
        (string Name, byte Value)[] states =
        [
            ("Uninitialized", 0),
            ("Planned", 1),
            ("Armed", 2),
            ("TransferRecorded", 3),
            ("ContinuationObserved", 4),
            ("Failed", 5)
        ];
        (string Name, byte Value)[] continuationContracts =
        [
            ("Uninitialized", 0),
            ("StaticNonReturningRspAndHandoff", 1)
        ];
        (string Name, byte Value)[] provenance =
        [
            ("Uninitialized", 0),
            ("CallerAttestedStaticOwnedData", 1)
        ];
        (string Name, byte Value)[] stateStorageProvenance =
        [
            ("Uninitialized", 0),
            ("CallerAttestedStaticOwnedData", 1)
        ];
        (string Name, byte Value)[] terminalCauses =
        [
            ("Uninitialized", 0),
            ("ContinuationRspAuditFailed", 1),
            ("UnexpectedReturnReported", 2)
        ];
        (string Name, byte Value)[] operations =
        [
            ("Uninitialized", 0),
            ("Emit", 1),
            ("Verify", 2)
        ];
        (string Name, byte Value)[] statuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("InvalidAllocationRecordCount", 2),
            ("TooManyAllocationRecords", 3),
            ("AllocationRecordCapacityTooSmall", 4),
            ("MissingAllocationRecords", 5),
            ("MisalignedAllocationRecords", 6),
            ("AllocationRecordAddressRangeOverflow", 7),
            ("InvalidStackLayout", 8),
            ("StackLayoutNotReady", 9),
            ("InvalidCodeMemoryType", 10),
            ("InvalidCodePhysicalStart", 11),
            ("MisalignedCodePhysicalStart", 12),
            ("InvalidCodeVirtualStart", 13),
            ("MisalignedCodeVirtualStart", 14),
            ("InvalidCodePageCount", 15),
            ("CodeByteCountOverflow", 16),
            ("CodeByteLengthMismatch", 17),
            ("CodePhysicalEndOverflow", 18),
            ("CodePhysicalEndMismatch", 19),
            ("CodeVirtualEndOverflow", 20),
            ("InvalidDataMemoryType", 21),
            ("InvalidDataPhysicalStart", 22),
            ("MisalignedDataPhysicalStart", 23),
            ("InvalidDataVirtualStart", 24),
            ("MisalignedDataVirtualStart", 25),
            ("InvalidDataPageCount", 26),
            ("DataByteCountOverflow", 27),
            ("DataByteLengthMismatch", 28),
            ("DataPhysicalEndOverflow", 29),
            ("DataPhysicalEndMismatch", 30),
            ("DataVirtualEndOverflow", 31),
            ("PhysicalAllocationOverlap", 32),
            ("VirtualAllocationOverlap", 33),
            ("DataArenaMismatch", 34),
            ("GuardVirtualRangeOverflow", 35),
            ("InvalidStubEntryOffset", 36),
            ("MisalignedStubEntry", 37),
            ("StubEntryOverflow", 38),
            ("InvalidDeclaredStubByteLength", 39),
            ("StubBytePlanOutOfRange", 40),
            ("InvalidContinuationAddress", 41),
            ("InvalidContinuationContract", 42),
            ("ContinuationInsideStackArena", 43),
            ("InvalidHandoffProvenance", 44),
            ("InvalidHandoffAddress", 45),
            ("MisalignedHandoffAddress", 46),
            ("InvalidHandoffLength", 47),
            ("HandoffRangeOverflow", 48),
            ("HandoffOverlapsStubPage", 49),
            ("HandoffInsideGuard", 50),
            ("HandoffInsideUsableStack", 51),
            ("AbiStackTooSmall", 52),
            ("AbiStackArithmeticOverflow", 53),
            ("InvalidAbiAlignment", 54),
            ("InvalidPlan", 55),
            ("MissingByteBuffer", 56),
            ("ByteBufferTooSmall", 57),
            ("ByteBufferAddressRangeOverflow", 58),
            ("ByteLengthMismatch", 59),
            ("BytePlanMismatch", 60),
            ("InvalidBytePlanEvidence", 61),
            ("RepeatedArm", 62),
            ("IllegalArmState", 63),
            ("TransferBeforeArm", 64),
            ("RepeatedTransfer", 65),
            ("IllegalTransferState", 66),
            ("ObservationBeforeTransfer", 67),
            ("RepeatedObservation", 68),
            ("InvalidObservedRsp", 69),
            ("ObservedRspInGuard", 70),
            ("ObservedRspOutsideUsableStack", 71),
            ("MisalignedObservedRsp", 72),
            ("ObservedRspMismatch", 73),
            ("ShadowSpaceOverflow", 74),
            ("ShadowSpaceOutOfRange", 75),
            ("UnexpectedReturn", 76),
            ("RepeatedUnexpectedReturn", 77),
            ("InvalidState", 78),
            ("ContinuationInsideStubPage", 79),
            ("StubReservationRejected", 80),
            ("InvalidMaterializedPageAddress", 81),
            ("MaterializedPageMismatch", 82),
            ("MaterializedPageLengthMismatch", 83),
            ("PlanEvidenceMismatch", 84),
            ("MissingStateRecord", 85),
            ("StateRecordCapacityTooSmall", 86),
            ("MisalignedStateRecord", 87),
            ("StateRecordAddressRangeOverflow", 88),
            ("StateRecordStorageMismatch", 89),
            ("StateRecordPlanMismatch", 90),
            ("StateRecordNotEmpty", 91),
            ("InvalidStateRecord", 92),
            ("ContinuationOverlapsHandoff", 93),
            ("InvalidStateStorageProvenance", 94),
            ("InvalidStateStorageAddress", 95),
            ("MisalignedStateStorageAddress", 96),
            ("InvalidStateStorageLength", 97),
            ("StateStorageRangeOverflow", 98),
            ("StateStorageOverlapsStubPage", 99),
            ("StateStorageOverlapsGuard", 100),
            ("StateStorageOverlapsUsableStack", 101),
            ("StateStorageOverlapsHandoff", 102),
            ("ContinuationOverlapsStateStorage", 103)
        ];

        if (!EnumMatches<OwnedBootStackSwitchState>(states)
            || !EnumMatches<OwnedBootStackSwitchContinuationContract>(
                continuationContracts)
            || !EnumMatches<OwnedBootStackSwitchHandoffProvenance>(
                provenance)
            || !EnumMatches<OwnedBootStackSwitchStateStorageProvenance>(
                stateStorageProvenance)
            || !EnumMatches<OwnedBootStackSwitchTerminalCause>(
                terminalCauses)
            || !EnumMatches<OwnedBootStackSwitchByteOperation>(
                operations)
            || !EnumMatches<OwnedBootStackSwitchStatus>(statuses)
            || !IsReadOnlyPod<OwnedBootStackSwitchHandoff>(88)
            || !IsReadOnlyPod<OwnedBootStackSwitchPlan>(496)
            || !IsMutablePod<OwnedBootStackSwitchStateRecord>(536)
            || !IsReadOnlyPod<OwnedBootStackSwitchPlanResult>(528)
            || !IsReadOnlyPod<
                OwnedBootStackSwitchBytePlanResult>(560)
            || !IsReadOnlyPod<
                OwnedBootStackSwitchStateResult>(528)
            || !HasOnlyPrivateConstructors<
                OwnedBootStackSwitchHandoff>()
            || !HasOnlyPrivateConstructors<
                OwnedBootStackSwitchPlan>()
            || !HasOnlyPrivateConstructors<
                OwnedBootStackSwitchStateRecord>()
            || !HasOnlyPrivateConstructors<
                OwnedBootStackSwitchPlanResult>()
            || !HasOnlyPrivateConstructors<
                OwnedBootStackSwitchBytePlanResult>()
            || !HasOnlyPrivateConstructors<
                OwnedBootStackSwitchStateResult>())
        {
            return false;
        }

        if (!HasExactOffsets<OwnedBootStackSwitchHandoff>(
                [
                    ("_size", 0),
                    ("_version", 4),
                    ("_storageStart", 8),
                    ("_storageEndExclusive", 16),
                    ("_storageByteLength", 24),
                    ("_stubEntry", 32),
                    ("_usableStackStart", 40),
                    ("_usableStackEndExclusive", 48),
                    ("_preTransferStackPointer", 56),
                    ("_expectedContinuationStackPointer", 64),
                    ("_continuation", 72),
                    ("_provenance", 80),
                    ("_continuationContract", 81),
                    ("_reserved0", 82),
                    ("_reserved1", 83),
                    ("_reserved2", 84),
                    ("_reserved3", 85),
                    ("_reserved4", 86),
                    ("_reserved5", 87)
                ])
            || !HasExactOffsets<OwnedBootStackSwitchPlan>(
                [
                    ("_codeAllocation", 0),
                    ("_dataAllocation", 56),
                    ("_stubReservation", 112),
                    ("_stackLayout", 152),
                    ("_handoff", 280),
                    ("_stateStorageStart", 368),
                    ("_stateStorageEndExclusive", 376),
                    ("_stateStorageByteLength", 384),
                    ("_stateStorageProvenance", 392),
                    ("_stateStorageReserved0", 393),
                    ("_stateStorageReserved1", 394),
                    ("_stateStorageReserved2", 395),
                    ("_stateStorageReserved3", 396),
                    ("_stateStorageReserved4", 397),
                    ("_stateStorageReserved5", 398),
                    ("_stateStorageReserved6", 399),
                    ("_stubPhysicalEntry", 400),
                    ("_stubVirtualEntry", 408),
                    ("_codeVirtualEndExclusive", 416),
                    ("_dataVirtualEndExclusive", 424),
                    ("_guardVirtualStart", 432),
                    ("_guardVirtualEndExclusive", 440),
                    ("_usableStackVirtualStart", 448),
                    ("_usableStackVirtualEndExclusive", 456),
                    ("_preTransferStackPointer", 464),
                    ("_expectedContinuationStackPointer", 472),
                    ("_stubEntryOffset", 480),
                    ("_declaredStubByteLength", 488)
                ])
            || !HasExactOffsets<OwnedBootStackSwitchStateRecord>(
                [
                    ("_size", 0),
                    ("_version", 4),
                    ("_storageAddress", 8),
                    ("_transitionCount", 16),
                    ("_observedRsp", 24),
                    ("_plan", 32),
                    ("_state", 528),
                    ("_terminalCause", 529),
                    ("_terminalStatus", 530),
                    ("_reserved0", 531),
                    ("_reserved1", 532),
                    ("_reserved2", 533),
                    ("_reserved3", 534),
                    ("_reserved4", 535)
                ])
            || !HasExactOffsets<OwnedBootStackSwitchPlanResult>(
                [
                    ("_status", 0),
                    ("_addressStatus", 1),
                    ("_stackLifetime", 2),
                    ("_reserved0", 3),
                    ("_reserved1", 4),
                    ("_reserved2", 5),
                    ("_reserved3", 6),
                    ("_reserved4", 7),
                    ("_allocationRecordCount", 8),
                    ("_availableAllocationRecordCapacity", 16),
                    ("_invalidAllocationRecordIndex", 24),
                    ("_plan", 32)
                ])
            || !HasExactOffsets<OwnedBootStackSwitchBytePlanResult>(
                [
                    ("_status", 0),
                    ("_operation", 1),
                    ("_reserved0", 2),
                    ("_reserved1", 3),
                    ("_reserved2", 4),
                    ("_reserved3", 5),
                    ("_reserved4", 6),
                    ("_reserved5", 7),
                    ("_codePageBufferAddress", 8),
                    ("_materializedPageByteLength", 16),
                    ("_availablePageCapacity", 24),
                    ("_stubEntryOffset", 32),
                    ("_materializedStubByteLength", 40),
                    ("_bytesProcessed", 48),
                    ("_mismatchByteOffset", 56),
                    ("_plan", 64)
                ])
            || !HasExactOffsets<OwnedBootStackSwitchStateResult>(
                [
                    ("_status", 0),
                    ("_addressStatus", 1),
                    ("_previousState", 2),
                    ("_requestedState", 3),
                    ("_currentState", 4),
                    ("_reserved0", 5),
                    ("_reserved1", 6),
                    ("_reserved2", 7),
                    ("_observedRsp", 8),
                    ("_stateStorageAddress", 16),
                    ("_transitionCount", 24),
                    ("_plan", 32)
                ]))
        {
            return false;
        }

        return OwnedBootStackSwitchPlan.RequiredAllocationRecordCount
                == 2
            && OwnedBootStackSwitchPlan.CodeAllocationRecordIndex == 0
            && OwnedBootStackSwitchPlan.DataAllocationRecordIndex == 1
            && OwnedBootStackSwitchPlan.RequiredCodePageCount == 1
            && OwnedBootStackSwitchPlan.StubEntryAlignmentBytes == 16
            && OwnedBootStackSwitchPlan.X64StackAlignmentBytes == 16
            && OwnedBootStackSwitchPlan.X64ShadowSpaceBytes == 32
            && OwnedBootStackSwitchPlan.FakeReturnAddressBytes == 8
            && OwnedBootStackSwitchPlan.RequiredContinuationFrameBytes
                == 40
            && OwnedBootStackSwitchPlan.RequiredStubByteLength == 14
            && OwnedBootStackSwitchHandoff.StructureSize == 88
            && OwnedBootStackSwitchHandoff.CurrentVersion == 1
            && OwnedBootStackSwitchStateRecord.StructureSize == 536
            && OwnedBootStackSwitchStateRecord.CurrentVersion == 1;
    }

    private static unsafe bool TestDefaultsFailClosed()
    {
        OwnedBootStackSwitchHandoff handoff = default;
        OwnedBootStackSwitchPlan plan = default;
        OwnedBootStackSwitchPlanResult planResult = default;
        OwnedBootStackSwitchBytePlanResult byteResult = default;
        OwnedBootStackSwitchStateRecord stateRecord = default;
        OwnedBootStackSwitchStateResult stateResult = default;

        return !handoff.IsValid
            && handoff.Size == 0
            && handoff.Version == 0
            && !plan.IsValid
            && !stateRecord.IsValid
            && stateRecord.Size == 0
            && stateRecord.Version == 0
            && stateRecord.StorageAddress == 0
            && stateRecord.TransitionCount == 0
            && stateRecord.State
                == OwnedBootStackSwitchState.Uninitialized
            && stateRecord.TerminalCause
                == OwnedBootStackSwitchTerminalCause.Uninitialized
            && stateRecord.TerminalStatus
                == OwnedBootStackSwitchStatus.Uninitialized
            && !planResult.Succeeded
            && planResult.Status
                == OwnedBootStackSwitchStatus.Uninitialized
            && planResult.RequiredAllocationRecordCapacity == 2
            && !byteResult.Succeeded
            && byteResult.Status
                == OwnedBootStackSwitchStatus.Uninitialized
            && byteResult.Operation
                == OwnedBootStackSwitchByteOperation.Uninitialized
            && !stateResult.Succeeded
            && stateResult.Status
                == OwnedBootStackSwitchStatus.Uninitialized
            && stateResult.CurrentState
                == OwnedBootStackSwitchState.Uninitialized
            && !plan.EmitAuditedStubPage(null, 0, 0).Succeeded
            && plan.EmitAuditedStubPage(null, 0, 0).Status
                == OwnedBootStackSwitchStatus.InvalidPlan
            && !plan.VerifyAuditedStubPage(null, 0, 0, 0).Succeeded
            && plan.VerifyAuditedStubPage(null, 0, 0, 0).Status
                == OwnedBootStackSwitchStatus.InvalidPlan
            && !plan.InitializeOneShotState(null, 0).Succeeded
            && plan.InitializeOneShotState(null, 0).Status
                == OwnedBootStackSwitchStatus.InvalidPlan
            && !plan.Arm(null, 0, default).Succeeded
            && plan.Arm(null, 0, default).Status
                == OwnedBootStackSwitchStatus.InvalidPlan
            && !plan.RecordTransfer(null, 0).Succeeded
            && plan.RecordTransfer(null, 0).Status
                == OwnedBootStackSwitchStatus.InvalidPlan
            && !plan.AuditContinuationRsp(null, 0, default).Succeeded
            && plan.AuditContinuationRsp(null, 0, default).Status
                == OwnedBootStackSwitchStatus.InvalidPlan
            && !plan.ReportUnexpectedReturn(null, 0).Succeeded
            && plan.ReportUnexpectedReturn(null, 0).Status
                == OwnedBootStackSwitchStatus.InvalidPlan;
    }

    private static bool TestSuccessfulPlanAndAbi()
    {
        OwnedBootStackSwitchPlanResult result = CreateValidPlan();
        if (!result.Succeeded
            || result.Status != OwnedBootStackSwitchStatus.Success
            || result.AddressStatus != AddressValueStatus.Success
            || result.StackLifetime != OwnedBootStackLifetime.Ready
            || result.AllocationRecordCount != 2
            || result.AvailableAllocationRecordCapacity != 2
            || result.InvalidAllocationRecordIndex
                != OwnedBootStackSwitchPlan.NoIndex)
        {
            return false;
        }

        OwnedBootStackSwitchPlan plan = result.Plan;
        ulong guardStart = Raw(plan.GuardVirtualStart);
        ulong guardEnd = Raw(plan.GuardVirtualEndExclusive);
        ulong stackStart = Raw(plan.UsableStackVirtualStart);
        ulong stackEnd = Raw(plan.UsableStackVirtualEndExclusive);
        ulong preTransfer = Raw(plan.PreTransferStackPointer);
        ulong continuationRsp =
            Raw(plan.ExpectedContinuationStackPointer);

        return plan.IsValid
            && plan.StackLayout.IsValid
            && plan.StackLayout.Lifetime
                == OwnedBootStackLifetime.Ready
            && plan.CodeAllocation.UefiType
                == (uint)UefiMemoryType.LoaderCode
            && plan.CodeAllocation.PageCount == 1
            && plan.CodeAllocation.ByteLength
                == BootMemoryRegionRecord.PageSize
            && plan.StubReservation.IsValid
            && plan.StubReservation.Kind
                == BootReservationKind.BootStackSwitchStub
            && plan.StubReservation.PhysicalStart.Equals(
                plan.StubPhysicalEntry)
            && plan.StubReservation.PageCount == 1
            && plan.DataAllocation.UefiType
                == (uint)UefiMemoryType.LoaderData
            && plan.DataAllocation.PageCount == ArenaPages
            && plan.DataAllocation.PhysicalStart
                == DataPhysicalStart
            && plan.DataAllocation.PhysicalEndExclusive
                == DataPhysicalStart
                    + ArenaPages * BootMemoryRegionRecord.PageSize
            && Raw(plan.StateStorageStart)
                == StateStorageVirtualStart
            && Raw(plan.StateStorageEndExclusive)
                == StateStorageVirtualStart
                    + OwnedBootStackSwitchStateRecord.StructureSize
            && plan.StateStorageByteLength
                == OwnedBootStackSwitchStateRecord.StructureSize
            && plan.StateStorageProvenance
                == OwnedBootStackSwitchStateStorageProvenance
                    .CallerAttestedStaticOwnedData
            && Raw(plan.StubPhysicalEntry) == CodePhysicalStart
            && Raw(plan.StubVirtualEntry) == CodeVirtualStart
            && guardStart == DataVirtualStart
            && guardEnd
                == DataVirtualStart
                    + GuardPages * BootMemoryRegionRecord.PageSize
            && stackStart == guardEnd
            && stackEnd
                == DataVirtualStart
                    + ArenaPages * BootMemoryRegionRecord.PageSize
            && preTransfer == stackEnd - 32
            && continuationRsp == stackEnd - 40
            && (preTransfer & 15) == 0
            && (continuationRsp & 15) == 8
            && continuationRsp >= stackStart
            && continuationRsp + 8 == preTransfer
            && continuationRsp + 40 == stackEnd
            && plan.Handoff.IsValid
            && plan.Handoff.Size
                == OwnedBootStackSwitchHandoff.StructureSize
            && plan.Handoff.Version
                == OwnedBootStackSwitchHandoff.CurrentVersion
            && Raw(plan.Handoff.StorageStart)
                == HandoffVirtualStart
            && plan.Handoff.StorageByteLength
                == OwnedBootStackSwitchHandoff.StructureSize
            && plan.Handoff.StubEntry.Equals(plan.StubVirtualEntry)
            && plan.Handoff.PreTransferStackPointer.Equals(
                plan.PreTransferStackPointer)
            && plan.Handoff.ExpectedContinuationStackPointer.Equals(
                plan.ExpectedContinuationStackPointer)
            && Raw(plan.Handoff.Continuation)
                == ContinuationVirtualAddress
            && plan.Handoff.Provenance
                == OwnedBootStackSwitchHandoffProvenance
                    .CallerAttestedStaticOwnedData
            && plan.Handoff.ContinuationContract
                == OwnedBootStackSwitchContinuationContract
                    .StaticNonReturningRspAndHandoff;
    }

    private static unsafe bool TestPointerMetadata()
    {
        OwnedBootStackLayout ready = CreateReadyLayout();
        VirtualAddress handoff = Virtual(HandoffVirtualStart);
        VirtualAddress stateStorage =
            Virtual(StateStorageVirtualStart);
        VirtualAddress continuation =
            Virtual(ContinuationVirtualAddress);

        OwnedBootStackSwitchPlanResult zero =
            OwnedBootStackSwitchPlan.Create(
                null,
                0,
                0,
                ready,
                0,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                handoff,
                OwnedBootStackSwitchHandoff.StructureSize,
                OwnedBootStackSwitchHandoffProvenance
                    .CallerAttestedStaticOwnedData,
                stateStorage,
                OwnedBootStackSwitchStateRecord.StructureSize,
                OwnedBootStackSwitchStateStorageProvenance
                    .CallerAttestedStaticOwnedData,
                continuation,
                OwnedBootStackSwitchContinuationContract
                    .StaticNonReturningRspAndHandoff);
        OwnedBootStackSwitchPlanResult one =
            OwnedBootStackSwitchPlan.Create(
                null,
                1,
                1,
                ready,
                0,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                handoff,
                OwnedBootStackSwitchHandoff.StructureSize,
                OwnedBootStackSwitchHandoffProvenance
                    .CallerAttestedStaticOwnedData,
                stateStorage,
                OwnedBootStackSwitchStateRecord.StructureSize,
                OwnedBootStackSwitchStateStorageProvenance
                    .CallerAttestedStaticOwnedData,
                continuation,
                OwnedBootStackSwitchContinuationContract
                    .StaticNonReturningRspAndHandoff);
        OwnedBootStackSwitchPlanResult tooMany =
            OwnedBootStackSwitchPlan.Create(
                null,
                3,
                3,
                ready,
                0,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                handoff,
                OwnedBootStackSwitchHandoff.StructureSize,
                OwnedBootStackSwitchHandoffProvenance
                    .CallerAttestedStaticOwnedData,
                stateStorage,
                OwnedBootStackSwitchStateRecord.StructureSize,
                OwnedBootStackSwitchStateStorageProvenance
                    .CallerAttestedStaticOwnedData,
                continuation,
                OwnedBootStackSwitchContinuationContract
                    .StaticNonReturningRspAndHandoff);
        OwnedBootStackSwitchPlanResult capacity =
            OwnedBootStackSwitchPlan.Create(
                null,
                2,
                1,
                ready,
                0,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                handoff,
                OwnedBootStackSwitchHandoff.StructureSize,
                OwnedBootStackSwitchHandoffProvenance
                    .CallerAttestedStaticOwnedData,
                stateStorage,
                OwnedBootStackSwitchStateRecord.StructureSize,
                OwnedBootStackSwitchStateStorageProvenance
                    .CallerAttestedStaticOwnedData,
                continuation,
                OwnedBootStackSwitchContinuationContract
                    .StaticNonReturningRspAndHandoff);
        OwnedBootStackSwitchPlanResult missing =
            OwnedBootStackSwitchPlan.Create(
                null,
                2,
                2,
                ready,
                0,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                handoff,
                OwnedBootStackSwitchHandoff.StructureSize,
                OwnedBootStackSwitchHandoffProvenance
                    .CallerAttestedStaticOwnedData,
                stateStorage,
                OwnedBootStackSwitchStateRecord.StructureSize,
                OwnedBootStackSwitchStateStorageProvenance
                    .CallerAttestedStaticOwnedData,
                continuation,
                OwnedBootStackSwitchContinuationContract
                    .StaticNonReturningRspAndHandoff);
        OwnedBootStackSwitchPlanResult misaligned =
            OwnedBootStackSwitchPlan.Create(
                (BootMemoryRegionRecord*)1,
                2,
                2,
                default,
                0,
                0,
                default,
                0,
                default,
                default,
                0,
                default,
                default,
                default);
        ulong overflowingAddress =
            (~0UL - 31UL) & ~7UL;
        OwnedBootStackSwitchPlanResult overflow =
            OwnedBootStackSwitchPlan.Create(
                (BootMemoryRegionRecord*)overflowingAddress,
                2,
                2,
                default,
                0,
                0,
                default,
                0,
                default,
                default,
                0,
                default,
                default,
                default);

        return Failure(
                zero,
                OwnedBootStackSwitchStatus
                    .InvalidAllocationRecordCount,
                0,
                0,
                OwnedBootStackSwitchPlan.NoIndex)
            && Failure(
                one,
                OwnedBootStackSwitchStatus
                    .InvalidAllocationRecordCount,
                1,
                1,
                OwnedBootStackSwitchPlan.NoIndex)
            && Failure(
                tooMany,
                OwnedBootStackSwitchStatus.TooManyAllocationRecords,
                3,
                3,
                OwnedBootStackSwitchPlan.NoIndex)
            && Failure(
                capacity,
                OwnedBootStackSwitchStatus
                    .AllocationRecordCapacityTooSmall,
                2,
                1,
                OwnedBootStackSwitchPlan.NoIndex)
            && Failure(
                missing,
                OwnedBootStackSwitchStatus.MissingAllocationRecords,
                2,
                2,
                OwnedBootStackSwitchPlan.NoIndex)
            && Failure(
                misaligned,
                OwnedBootStackSwitchStatus
                    .MisalignedAllocationRecords,
                2,
                2,
                OwnedBootStackSwitchPlan.NoIndex)
            && misaligned.AddressStatus
                == AddressValueStatus.MisalignedAddress
            && Failure(
                overflow,
                OwnedBootStackSwitchStatus
                    .AllocationRecordAddressRangeOverflow,
                2,
                2,
                OwnedBootStackSwitchPlan.NoIndex)
            && overflow.AddressStatus
                == AddressValueStatus.AddressOverflow;
    }

    private static bool TestAllocationRecordFailures()
    {
        BootMemoryRegionRecord[] records = CreateValidRecords();

        if (!ExpectRecordFailure(
                records,
                0,
                new BootMemoryRegionRecord(
                    (uint)UefiMemoryType.LoaderData,
                    records[0].PhysicalStart,
                    records[0].VirtualStart,
                    records[0].PageCount,
                    records[0].Attribute,
                    records[0].ByteLength,
                    records[0].PhysicalEndExclusive),
                OwnedBootStackSwitchStatus.InvalidCodeMemoryType)
            || !ExpectRecordFailure(
                records,
                1,
                new BootMemoryRegionRecord(
                    (uint)UefiMemoryType.LoaderCode,
                    records[1].PhysicalStart,
                    records[1].VirtualStart,
                    records[1].PageCount,
                    records[1].Attribute,
                    records[1].ByteLength,
                    records[1].PhysicalEndExclusive),
                OwnedBootStackSwitchStatus.InvalidDataMemoryType)
            || !ExpectRecordFailure(
                records,
                0,
                WithRecord(records[0], physicalStart: 0),
                OwnedBootStackSwitchStatus.InvalidCodePhysicalStart)
            || !ExpectRecordFailure(
                records,
                0,
                WithRecord(
                    records[0],
                    physicalStart:
                        records[0].PhysicalStart + 1),
                OwnedBootStackSwitchStatus.MisalignedCodePhysicalStart)
            || !ExpectRecordFailure(
                records,
                0,
                WithRecord(records[0], virtualStart: 0),
                OwnedBootStackSwitchStatus.InvalidCodeVirtualStart)
            || !ExpectRecordFailure(
                records,
                0,
                WithRecord(
                    records[0],
                    virtualStart:
                        records[0].VirtualStart + 1),
                OwnedBootStackSwitchStatus.MisalignedCodeVirtualStart)
            || !ExpectRecordFailure(
                records,
                0,
                WithRecord(records[0], pageCount: 2),
                OwnedBootStackSwitchStatus.InvalidCodePageCount)
            || !ExpectRecordFailure(
                records,
                1,
                WithRecord(
                    records[1],
                    pageCount: 0,
                    byteLength: 0),
                OwnedBootStackSwitchStatus.InvalidDataPageCount)
            || !ExpectRecordFailure(
                records,
                1,
                WithRecord(
                    records[1],
                    pageCount:
                        ~0UL / BootMemoryRegionRecord.PageSize + 1),
                OwnedBootStackSwitchStatus.DataByteCountOverflow)
            || !ExpectRecordFailure(
                records,
                0,
                WithRecord(
                    records[0],
                    byteLength:
                        records[0].ByteLength + 1),
                OwnedBootStackSwitchStatus.CodeByteLengthMismatch)
            || !ExpectRecordFailure(
                records,
                1,
                WithRecord(
                    records[1],
                    physicalEndExclusive:
                        records[1].PhysicalEndExclusive + 1),
                OwnedBootStackSwitchStatus.DataPhysicalEndMismatch))
        {
            return false;
        }

        BootMemoryRegionRecord physicalWrap =
            new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderData,
                0xFFFF_FFFF_FFFF_F000UL,
                DataVirtualStart,
                1,
                0,
                BootMemoryRegionRecord.PageSize,
                1);
        BootMemoryRegionRecord virtualWrap =
            new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderData,
                DataPhysicalStart,
                0xFFFF_FFFF_FFFF_F000UL,
                1,
                0,
                BootMemoryRegionRecord.PageSize,
                DataPhysicalStart
                    + BootMemoryRegionRecord.PageSize);

        return ExpectRecordFailure(
                records,
                1,
                physicalWrap,
                OwnedBootStackSwitchStatus
                    .DataPhysicalEndOverflow)
            && ExpectRecordFailure(
                records,
                1,
                virtualWrap,
                OwnedBootStackSwitchStatus
                    .DataVirtualEndOverflow);
    }

    private static bool TestAllocationOverlapLayoutAndArena()
    {
        BootMemoryRegionRecord[] physicalOverlap =
            CreateValidRecords();
        physicalOverlap[0] = WithRecord(
            physicalOverlap[0],
            physicalStart: DataPhysicalStart,
            physicalEndExclusive:
                DataPhysicalStart
                    + BootMemoryRegionRecord.PageSize);
        BootMemoryRegionRecord[] virtualOverlap =
            CreateValidRecords();
        virtualOverlap[0] = WithRecord(
            virtualOverlap[0],
            virtualStart: DataVirtualStart);
        BootMemoryRegionRecord[] arenaMismatch =
            CreateValidRecords();
        arenaMismatch[1] = WithRecord(
            arenaMismatch[1],
            physicalStart:
                DataPhysicalStart + BootMemoryRegionRecord.PageSize,
            physicalEndExclusive:
                DataPhysicalStart
                    + (ArenaPages + 1)
                        * BootMemoryRegionRecord.PageSize);

        OwnedBootStackLayout reserved = CreateReservedLayout();
        OwnedBootStackLayout ready = CreateReadyLayout();
        OwnedBootStackLayoutResult activeResult =
            ready.TransitionTo(OwnedBootStackLifetime.Active);

        OwnedBootStackSwitchPlanResult physical =
            CreatePlan(physicalOverlap, ready);
        OwnedBootStackSwitchPlanResult virtualResult =
            CreatePlan(virtualOverlap, ready);
        OwnedBootStackSwitchPlanResult arena =
            CreatePlan(arenaMismatch, ready);
        OwnedBootStackSwitchPlanResult defaultLayout =
            CreatePlan(CreateValidRecords(), default);
        OwnedBootStackSwitchPlanResult reservedLayout =
            CreatePlan(CreateValidRecords(), reserved);
        OwnedBootStackSwitchPlanResult activeLayout =
            CreatePlan(CreateValidRecords(), activeResult.Layout);

        return physical.Status
                == OwnedBootStackSwitchStatus.PhysicalAllocationOverlap
            && physical.InvalidAllocationRecordIndex == 1
            && virtualResult.Status
                == OwnedBootStackSwitchStatus.VirtualAllocationOverlap
            && virtualResult.InvalidAllocationRecordIndex == 1
            && arena.Status
                == OwnedBootStackSwitchStatus.DataArenaMismatch
            && arena.InvalidAllocationRecordIndex == 1
            && defaultLayout.Status
                == OwnedBootStackSwitchStatus.InvalidStackLayout
            && reservedLayout.Status
                == OwnedBootStackSwitchStatus.StackLayoutNotReady
            && reservedLayout.StackLifetime
                == OwnedBootStackLifetime.Reserved
            && activeLayout.Status
                == OwnedBootStackSwitchStatus.StackLayoutNotReady
            && activeLayout.StackLifetime
                == OwnedBootStackLifetime.Active
            && !physical.Succeeded
            && !virtualResult.Succeeded
            && !arena.Succeeded;
    }

    private static bool TestStubEntryAndDeclaredLength()
    {
        BootMemoryRegionRecord[] records = CreateValidRecords();
        OwnedBootStackLayout ready = CreateReadyLayout();

        OwnedBootStackSwitchPlanResult misaligned =
            CreatePlan(records, ready, stubEntryOffset: 1);
        OwnedBootStackSwitchPlanResult atEnd =
            CreatePlan(
                records,
                ready,
                stubEntryOffset: BootMemoryRegionRecord.PageSize);
        OwnedBootStackSwitchPlanResult nearEnd =
            CreatePlan(
                records,
                ready,
                stubEntryOffset:
                    BootMemoryRegionRecord.PageSize - 8);
        OwnedBootStackSwitchPlanResult zeroLength =
            CreatePlan(records, ready, declaredStubByteLength: 0);
        OwnedBootStackSwitchPlanResult longLength =
            CreatePlan(
                records,
                ready,
                declaredStubByteLength:
                    OwnedBootStackSwitchPlan.RequiredStubByteLength + 1);
        OwnedBootStackSwitchPlanResult alignedOffset =
            CreatePlan(records, ready, stubEntryOffset: 16);

        return misaligned.Status
                == OwnedBootStackSwitchStatus.MisalignedStubEntry
            && atEnd.Status
                == OwnedBootStackSwitchStatus.InvalidStubEntryOffset
            && nearEnd.Status
                == OwnedBootStackSwitchStatus.StubBytePlanOutOfRange
            && zeroLength.Status
                == OwnedBootStackSwitchStatus
                    .InvalidDeclaredStubByteLength
            && longLength.Status
                == OwnedBootStackSwitchStatus
                    .InvalidDeclaredStubByteLength
            && alignedOffset.Succeeded
            && Raw(alignedOffset.Plan.StubVirtualEntry)
                == CodeVirtualStart + 16
            && Raw(alignedOffset.Plan.StubPhysicalEntry)
                == CodePhysicalStart + 16;
    }

    private static bool TestContinuationAndHandoff()
    {
        BootMemoryRegionRecord[] records = CreateValidRecords();
        OwnedBootStackLayout ready = CreateReadyLayout();
        ulong guardAddress = DataVirtualStart + 8;
        ulong stackAddress =
            DataVirtualStart
            + GuardPages * BootMemoryRegionRecord.PageSize
            + 8;

        OwnedBootStackSwitchPlanResult noContinuation =
            CreatePlan(
                records,
                ready,
                continuationRaw: 0);
        OwnedBootStackSwitchPlanResult badContract =
            CreatePlan(
                records,
                ready,
                continuationContract: default);
        OwnedBootStackSwitchPlanResult continuationInStack =
            CreatePlan(
                records,
                ready,
                continuationRaw: stackAddress);
        OwnedBootStackSwitchPlanResult continuationInCode =
            CreatePlan(
                records,
                ready,
                continuationRaw: CodeVirtualStart);
        OwnedBootStackSwitchPlanResult noProvenance =
            CreatePlan(
                records,
                ready,
                handoffProvenance: default);
        OwnedBootStackSwitchPlanResult noHandoff =
            CreatePlan(
                records,
                ready,
                handoffAddressRaw: 0);
        OwnedBootStackSwitchPlanResult misalignedHandoff =
            CreatePlan(
                records,
                ready,
                handoffAddressRaw: HandoffVirtualStart + 1);
        OwnedBootStackSwitchPlanResult shortHandoff =
            CreatePlan(
                records,
                ready,
                handoffLength:
                    OwnedBootStackSwitchHandoff.StructureSize - 1);
        OwnedBootStackSwitchPlanResult oversizedHandoff =
            CreatePlan(
                records,
                ready,
                handoffLength:
                    OwnedBootStackSwitchHandoff.StructureSize + 1);
        OwnedBootStackSwitchPlanResult wrappingHandoff =
            CreatePlan(
                records,
                ready,
                handoffAddressRaw: ~0UL - 7,
                handoffLength:
                    OwnedBootStackSwitchHandoff.StructureSize);
        OwnedBootStackSwitchPlanResult codeHandoff =
            CreatePlan(
                records,
                ready,
                handoffAddressRaw: CodeVirtualStart);
        OwnedBootStackSwitchPlanResult guardHandoff =
            CreatePlan(
                records,
                ready,
                handoffAddressRaw: guardAddress);
        OwnedBootStackSwitchPlanResult stackHandoff =
            CreatePlan(
                records,
                ready,
                handoffAddressRaw: stackAddress);
        OwnedBootStackSwitchPlanResult continuationInHandoff =
            CreatePlan(
                records,
                ready,
                continuationRaw: HandoffVirtualStart);
        OwnedBootStackSwitchPlanResult noStateProvenance =
            CreatePlan(
                records,
                ready,
                stateStorageProvenance: default);
        OwnedBootStackSwitchPlanResult noStateStorage =
            CreatePlan(
                records,
                ready,
                stateStorageAddressRaw: 0);
        OwnedBootStackSwitchPlanResult misalignedStateStorage =
            CreatePlan(
                records,
                ready,
                stateStorageAddressRaw: StateStorageVirtualStart + 1);
        OwnedBootStackSwitchPlanResult zeroStateLength =
            CreatePlan(
                records,
                ready,
                stateStorageLength: 0);
        OwnedBootStackSwitchPlanResult shortStateLength =
            CreatePlan(
                records,
                ready,
                stateStorageLength:
                    OwnedBootStackSwitchStateRecord.StructureSize - 1);
        OwnedBootStackSwitchPlanResult longStateLength =
            CreatePlan(
                records,
                ready,
                stateStorageLength:
                    OwnedBootStackSwitchStateRecord.StructureSize + 1);
        OwnedBootStackSwitchPlanResult wrappingStateStorage =
            CreatePlan(
                records,
                ready,
                stateStorageAddressRaw: ~0UL - 7);
        OwnedBootStackSwitchPlanResult codeStateStorage =
            CreatePlan(
                records,
                ready,
                stateStorageAddressRaw: CodeVirtualStart);
        OwnedBootStackSwitchPlanResult guardStateStorage =
            CreatePlan(
                records,
                ready,
                stateStorageAddressRaw: DataVirtualStart);
        OwnedBootStackSwitchPlanResult stackStateStorage =
            CreatePlan(
                records,
                ready,
                stateStorageAddressRaw:
                    DataVirtualStart
                    + BootMemoryRegionRecord.PageSize);
        OwnedBootStackSwitchPlanResult handoffStateStorage =
            CreatePlan(
                records,
                ready,
                stateStorageAddressRaw: HandoffVirtualStart);
        OwnedBootStackSwitchPlanResult continuationStateStorage =
            CreatePlan(
                records,
                ready,
                continuationRaw: StateStorageVirtualStart);

        return noContinuation.Status
                == OwnedBootStackSwitchStatus.InvalidContinuationAddress
            && badContract.Status
                == OwnedBootStackSwitchStatus
                    .InvalidContinuationContract
            && continuationInStack.Status
                == OwnedBootStackSwitchStatus
                    .ContinuationInsideStackArena
            && continuationInCode.Status
                == OwnedBootStackSwitchStatus
                    .ContinuationInsideStubPage
            && noProvenance.Status
                == OwnedBootStackSwitchStatus.InvalidHandoffProvenance
            && noHandoff.Status
                == OwnedBootStackSwitchStatus.InvalidHandoffAddress
            && misalignedHandoff.Status
                == OwnedBootStackSwitchStatus.MisalignedHandoffAddress
            && shortHandoff.Status
                == OwnedBootStackSwitchStatus.InvalidHandoffLength
            && oversizedHandoff.Status
                == OwnedBootStackSwitchStatus.InvalidHandoffLength
            && wrappingHandoff.Status
                == OwnedBootStackSwitchStatus.HandoffRangeOverflow
            && codeHandoff.Status
                == OwnedBootStackSwitchStatus.HandoffOverlapsStubPage
            && guardHandoff.Status
                == OwnedBootStackSwitchStatus.HandoffInsideGuard
            && stackHandoff.Status
                == OwnedBootStackSwitchStatus.HandoffInsideUsableStack
            && continuationInHandoff.Status
                == OwnedBootStackSwitchStatus
                    .ContinuationOverlapsHandoff
            && noStateProvenance.Status
                == OwnedBootStackSwitchStatus
                    .InvalidStateStorageProvenance
            && noStateStorage.Status
                == OwnedBootStackSwitchStatus.InvalidStateStorageAddress
            && misalignedStateStorage.Status
                == OwnedBootStackSwitchStatus
                    .MisalignedStateStorageAddress
            && zeroStateLength.Status
                == OwnedBootStackSwitchStatus.InvalidStateStorageLength
            && shortStateLength.Status
                == OwnedBootStackSwitchStatus.InvalidStateStorageLength
            && longStateLength.Status
                == OwnedBootStackSwitchStatus.InvalidStateStorageLength
            && wrappingStateStorage.Status
                == OwnedBootStackSwitchStatus.StateStorageRangeOverflow
            && codeStateStorage.Status
                == OwnedBootStackSwitchStatus
                    .StateStorageOverlapsStubPage
            && guardStateStorage.Status
                == OwnedBootStackSwitchStatus.StateStorageOverlapsGuard
            && stackStateStorage.Status
                == OwnedBootStackSwitchStatus
                    .StateStorageOverlapsUsableStack
            && handoffStateStorage.Status
                == OwnedBootStackSwitchStatus
                    .StateStorageOverlapsHandoff
            && continuationStateStorage.Status
                == OwnedBootStackSwitchStatus
                    .ContinuationOverlapsStateStorage;
    }

    private static unsafe bool TestAuditedBytes()
    {
        byte[] expected =
        [
            0x48, 0x89, 0xCC,
            0x48, 0x31, 0xED,
            0x6A, 0x00,
            0x48, 0x89, 0xE1,
            0x41, 0xFF, 0xE0
        ];
        byte* scratch = stackalloc byte[PageScratchSize];
        byte* page = AlignPage(scratch);
        OwnedBootStackSwitchStateRecord state = default;
        OwnedBootStackSwitchPlanResult planned =
            CreatePageBoundPlan(
                page,
                stateStorageAddressRaw: (ulong)&state);
        if (!planned.Succeeded)
        {
            return false;
        }

        for (int index = 0; index < PageSize; index++)
        {
            page[index] = 0xA5;
        }

        OwnedBootStackSwitchPlan plan = planned.Plan;
        OwnedBootStackSwitchBytePlanResult emitted =
            plan.EmitAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult verified =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);

        bool prefixUntouched = true;
        bool suffixUntouched = true;
        for (int index = 0;
            index < (int)BoundStubEntryOffset;
            index++)
        {
            prefixUntouched &= page[index] == 0xA5;
        }

        int stubEnd = (int)(
            BoundStubEntryOffset
            + OwnedBootStackSwitchPlan.RequiredStubByteLength);
        for (int index = stubEnd; index < PageSize; index++)
        {
            suffixUntouched &= page[index] == 0xA5;
        }

        return emitted.Succeeded
            && verified.Succeeded
            && emitted.Operation
                == OwnedBootStackSwitchByteOperation.Emit
            && verified.Operation
                == OwnedBootStackSwitchByteOperation.Verify
            && emitted.CodePageBufferAddress == (ulong)page
            && emitted.MaterializedPageByteLength == (ulong)PageSize
            && emitted.AvailablePageCapacity == (ulong)PageSize
            && emitted.StubEntryOffset == BoundStubEntryOffset
            && emitted.MaterializedStubByteLength
                == (ulong)expected.Length
            && emitted.BytesProcessed == (ulong)expected.Length
            && emitted.MismatchByteOffset
                == OwnedBootStackSwitchPlan.NoIndex
            && emitted.Plan.Equals(plan)
            && verified.Plan.Equals(plan)
            && prefixUntouched
            && suffixUntouched
            && PageSliceEquals(
                page + BoundStubEntryOffset,
                expected)
            && !expected.Contains((byte)0xC3)
            && expected[^3] == 0x41
            && expected[^2] == 0xFF
            && expected[^1] == 0xE0;
    }

    private static unsafe bool TestByteMaterializationFailures()
    {
        byte* scratch = stackalloc byte[PageScratchSize * 2];
        byte* page = AlignPage(scratch);
        byte* alternatePage = page + PageSize;
        OwnedBootStackSwitchPlanResult planned =
            CreatePageBoundPlan(page);
        if (!planned.Succeeded)
        {
            return false;
        }

        OwnedBootStackSwitchPlan plan = planned.Plan;
        for (int index = 0; index < PageSize; index++)
        {
            page[index] = 0xCC;
            alternatePage[index] = 0x5A;
        }

        OwnedBootStackSwitchBytePlanResult emitted =
            plan.EmitAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult missingEmit =
            plan.EmitAuditedStubPage(
                null,
                (ulong)PageSize,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult shortCapacity =
            plan.EmitAuditedStubPage(
                page,
                (ulong)PageSize - 1,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult wrongPageLength =
            plan.EmitAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize - 1);
        OwnedBootStackSwitchBytePlanResult misaligned =
            plan.EmitAuditedStubPage(
                page + 1,
                (ulong)PageSize,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult alternate =
            plan.EmitAuditedStubPage(
                alternatePage,
                (ulong)PageSize,
                (ulong)PageSize);
        byte* overflowPointer =
            (byte*)(~0UL - ((ulong)PageSize - 1));
        OwnedBootStackSwitchBytePlanResult pointerOverflow =
            plan.EmitAuditedStubPage(
                overflowPointer,
                (ulong)PageSize,
                (ulong)PageSize);

        OwnedBootStackSwitchBytePlanResult verified =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchBytePlanResult missingVerify =
            plan.VerifyAuditedStubPage(
                null,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchBytePlanResult shortVerify =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize - 1,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchBytePlanResult wrongVerifyPageLength =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize - 1,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchBytePlanResult misalignedVerify =
            plan.VerifyAuditedStubPage(
                page + 1,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchBytePlanResult alternateVerify =
            plan.VerifyAuditedStubPage(
                alternatePage,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchBytePlanResult overflowVerify =
            plan.VerifyAuditedStubPage(
                overflowPointer,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchBytePlanResult lengthMismatch =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength + 1);

        byte* stub = page + BoundStubEntryOffset;
        stub[0] ^= 1;
        OwnedBootStackSwitchBytePlanResult first =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        stub[0] ^= 1;
        stub[7] ^= 1;
        OwnedBootStackSwitchBytePlanResult middle =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        stub[7] ^= 1;
        stub[13] ^= 1;
        OwnedBootStackSwitchBytePlanResult final =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        stub[13] ^= 1;

        return emitted.Succeeded
            && verified.Succeeded
            && verified.Operation
                == OwnedBootStackSwitchByteOperation.Verify
            && missingEmit.Status
                == OwnedBootStackSwitchStatus.MissingByteBuffer
            && shortCapacity.Status
                == OwnedBootStackSwitchStatus.ByteBufferTooSmall
            && wrongPageLength.Status
                == OwnedBootStackSwitchStatus
                    .MaterializedPageLengthMismatch
            && misaligned.Status
                == OwnedBootStackSwitchStatus
                    .InvalidMaterializedPageAddress
            && alternate.Status
                == OwnedBootStackSwitchStatus.MaterializedPageMismatch
            && missingVerify.Status
                == OwnedBootStackSwitchStatus.MissingByteBuffer
            && missingVerify.Operation
                == OwnedBootStackSwitchByteOperation.Verify
            && shortVerify.Status
                == OwnedBootStackSwitchStatus.ByteBufferTooSmall
            && wrongVerifyPageLength.Status
                == OwnedBootStackSwitchStatus
                    .MaterializedPageLengthMismatch
            && misalignedVerify.Status
                == OwnedBootStackSwitchStatus
                    .InvalidMaterializedPageAddress
            && alternateVerify.Status
                == OwnedBootStackSwitchStatus.MaterializedPageMismatch
            && overflowVerify.Status
                == OwnedBootStackSwitchStatus
                    .ByteBufferAddressRangeOverflow
            && first.Status
                == OwnedBootStackSwitchStatus.BytePlanMismatch
            && first.MismatchByteOffset == 0
            && middle.Status
                == OwnedBootStackSwitchStatus.BytePlanMismatch
            && middle.MismatchByteOffset == 7
            && final.Status
                == OwnedBootStackSwitchStatus.BytePlanMismatch
            && final.MismatchByteOffset
                == OwnedBootStackSwitchPlan.RequiredStubByteLength - 1
            && lengthMismatch.Status
                == OwnedBootStackSwitchStatus.ByteLengthMismatch
            && pointerOverflow.Status
                == OwnedBootStackSwitchStatus
                    .ByteBufferAddressRangeOverflow
            && alternatePage[0] == 0x5A
            && alternatePage[PageSize - 1] == 0x5A
            && !first.Succeeded
            && !middle.Succeeded
            && !final.Succeeded;
    }

    private static unsafe bool TestSuccessfulOneShotSequence()
    {
        byte* scratch = stackalloc byte[PageScratchSize];
        byte* page = AlignPage(scratch);
        OwnedBootStackSwitchStateRecord state = default;
        OwnedBootStackSwitchPlanResult planned =
            CreatePageBoundPlan(
                page,
                stateStorageAddressRaw: (ulong)&state);
        if (!planned.Succeeded)
        {
            return false;
        }

        OwnedBootStackSwitchPlan plan = planned.Plan;
        OwnedBootStackSwitchPlan planCopy = plan;
        OwnedBootStackSwitchBytePlanResult emit =
            plan.EmitAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult verify =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchStateResult initialized =
            plan.InitializeOneShotState(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult armed = planCopy.Arm(
            &state,
            OwnedBootStackSwitchStateRecord.StructureSize,
            verify);
        OwnedBootStackSwitchStateResult transferred =
            plan.RecordTransfer(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult observed =
            planCopy.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                plan.ExpectedContinuationStackPointer);

        return emit.Succeeded
            && verify.Succeeded
            && initialized.Succeeded
            && initialized.PreviousState
                == OwnedBootStackSwitchState.Uninitialized
            && initialized.CurrentState
                == OwnedBootStackSwitchState.Planned
            && initialized.TransitionCount == 0
            && armed.Succeeded
            && armed.PreviousState
                == OwnedBootStackSwitchState.Planned
            && armed.RequestedState
                == OwnedBootStackSwitchState.Armed
            && armed.CurrentState
                == OwnedBootStackSwitchState.Armed
            && armed.TransitionCount == 1
            && transferred.Succeeded
            && transferred.PreviousState
                == OwnedBootStackSwitchState.Armed
            && transferred.CurrentState
                == OwnedBootStackSwitchState.TransferRecorded
            && transferred.TransitionCount == 2
            && observed.Succeeded
            && observed.PreviousState
                == OwnedBootStackSwitchState.TransferRecorded
            && observed.CurrentState
                == OwnedBootStackSwitchState.ContinuationObserved
            && observed.TransitionCount == 3
            && observed.ObservedRsp.Equals(
                plan.ExpectedContinuationStackPointer)
            && observed.StateStorageAddress == (ulong)&state
            && observed.Plan.Equals(plan)
            && state.IsValid
            && state.StorageAddress == (ulong)&state
            && state.TransitionCount == 3
            && state.Plan.Equals(plan)
            && state.State
                == OwnedBootStackSwitchState
                    .ContinuationObserved
            && state.TerminalCause
                == OwnedBootStackSwitchTerminalCause.Uninitialized
            && state.TerminalStatus
                == OwnedBootStackSwitchStatus.Uninitialized
            && state.ObservedRsp.Equals(
                plan.ExpectedContinuationStackPointer)
            && plan.Equals(planCopy);
    }

    private static unsafe bool TestIllegalOneShotStates()
    {
        byte* scratch = stackalloc byte[PageScratchSize];
        byte* page = AlignPage(scratch);
        OwnedBootStackSwitchStateRecord state = default;
        OwnedBootStackSwitchPlanResult planned =
            CreatePageBoundPlan(
                page,
                stateStorageAddressRaw: (ulong)&state);
        OwnedBootStackSwitchPlanResult alternatePlanned =
            CreatePageBoundPlan(
                page,
                stateStorageAddressRaw: (ulong)&state,
                continuationRaw: ContinuationVirtualAddress + 0x1000);
        if (!planned.Succeeded || !alternatePlanned.Succeeded)
        {
            return false;
        }

        OwnedBootStackSwitchPlan plan = planned.Plan;
        OwnedBootStackSwitchPlan alternatePlan =
            alternatePlanned.Plan;
        OwnedBootStackSwitchBytePlanResult emitEvidence =
            plan.EmitAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult verify =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);

        OwnedBootStackSwitchStateResult initialized =
            plan.InitializeOneShotState(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult invalidEvidence =
            plan.Arm(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                emitEvidence);
        OwnedBootStackSwitchStateResult firstArm =
            plan.Arm(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                verify);
        OwnedBootStackSwitchPlan planCopy = plan;
        OwnedBootStackSwitchStateResult repeatedArm =
            planCopy.Arm(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                verify);

        OwnedBootStackSwitchStateResult transfer =
            plan.RecordTransfer(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult repeatedTransfer =
            plan.RecordTransfer(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult observation =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                plan.ExpectedContinuationStackPointer);
        OwnedBootStackSwitchStateResult repeatedObservation =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                plan.ExpectedContinuationStackPointer);

        state = default;
        plan.InitializeOneShotState(
            &state,
            OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult transferBeforeArm =
            plan.RecordTransfer(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult observationBeforeTransfer =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                plan.ExpectedContinuationStackPointer);

        state = default;
        bool unexpectedPrepared = PrepareTransferredState(
            plan,
            page,
            &state);
        OwnedBootStackSwitchStateResult unexpected =
            plan.ReportUnexpectedReturn(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult repeatedUnexpected =
            plan.ReportUnexpectedReturn(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        bool unexpectedTerminal =
            state.State == OwnedBootStackSwitchState.Failed
            && state.TerminalCause
                == OwnedBootStackSwitchTerminalCause
                    .UnexpectedReturnReported
            && state.TerminalStatus
                == OwnedBootStackSwitchStatus.UnexpectedReturn;

        state = default;
        alternatePlan.InitializeOneShotState(
            &state,
            OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult crossPlanEvidence =
            alternatePlan.Arm(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                verify);

        state = default;
        plan.InitializeOneShotState(
            &state,
            OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult wrongPlanForState =
            alternatePlan.Arm(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                verify);

        state = default;
        plan.InitializeOneShotState(
            &state,
            OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateRecord copied = state;
        OwnedBootStackSwitchStateResult copiedAddress =
            plan.Arm(
                &copied,
                OwnedBootStackSwitchStateRecord.StructureSize,
                verify);
        OwnedBootStackSwitchStateRecord secondZero = default;
        byte[] secondZeroBefore = StructBytes(secondZero);
        bool secondZeroAligned =
            ((ulong)&secondZero
                & (OwnedBootStackSwitchStateRecord
                    .RequiredStorageAlignment - 1)) == 0;
        OwnedBootStackSwitchStateResult initializeWrongStorage =
            plan.InitializeOneShotState(
                &secondZero,
                OwnedBootStackSwitchStateRecord.StructureSize);
        bool secondZeroUnchanged =
            secondZeroBefore.AsSpan().SequenceEqual(
                StructBytes(secondZero));
        OwnedBootStackSwitchStateResult nonEmpty =
            plan.InitializeOneShotState(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);

        state = default;
        ((byte*)&state)[100] = 1;
        OwnedBootStackSwitchStateResult hiddenNonZero =
            plan.InitializeOneShotState(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);

        state = default;
        plan.InitializeOneShotState(
            &state,
            OwnedBootStackSwitchStateRecord.StructureSize);
        ((byte*)&state)[531] = 1;
        OwnedBootStackSwitchStateResult malformedResult =
            plan.Arm(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                verify);

        OwnedBootStackSwitchStateResult missingState =
            plan.InitializeOneShotState(null, 0);
        OwnedBootStackSwitchStateResult shortState =
            plan.InitializeOneShotState(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize - 1);
        OwnedBootStackSwitchStateResult misalignedState =
            plan.InitializeOneShotState(
                (OwnedBootStackSwitchStateRecord*)1,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult overflowingState =
            plan.InitializeOneShotState(
                (OwnedBootStackSwitchStateRecord*)(~0UL - 7),
                OwnedBootStackSwitchStateRecord.StructureSize);

        return initialized.Succeeded
            && invalidEvidence.Status
                == OwnedBootStackSwitchStatus.InvalidBytePlanEvidence
            && firstArm.Succeeded
            && repeatedArm.Status
                == OwnedBootStackSwitchStatus.RepeatedArm
            && transferBeforeArm.Status
                == OwnedBootStackSwitchStatus.TransferBeforeArm
            && observationBeforeTransfer.Status
                == OwnedBootStackSwitchStatus.ObservationBeforeTransfer
            && transfer.Succeeded
            && repeatedTransfer.Status
                == OwnedBootStackSwitchStatus.RepeatedTransfer
            && observation.Succeeded
            && repeatedObservation.Status
                == OwnedBootStackSwitchStatus.RepeatedObservation
            && unexpectedPrepared
            && unexpected.Status
                == OwnedBootStackSwitchStatus.UnexpectedReturn
            && !unexpected.Succeeded
            && unexpected.CurrentState
                == OwnedBootStackSwitchState.Failed
            && unexpectedTerminal
            && repeatedUnexpected.Status
                == OwnedBootStackSwitchStatus
                    .RepeatedUnexpectedReturn
            && crossPlanEvidence.Status
                == OwnedBootStackSwitchStatus.PlanEvidenceMismatch
            && wrongPlanForState.Status
                == OwnedBootStackSwitchStatus.StateRecordPlanMismatch
            && copiedAddress.Status
                == OwnedBootStackSwitchStatus
                    .StateRecordStorageMismatch
            && secondZeroAligned
            && (ulong)&secondZero != (ulong)&state
            && initializeWrongStorage.Status
                == OwnedBootStackSwitchStatus
                    .StateRecordStorageMismatch
            && secondZeroUnchanged
            && nonEmpty.Status
                == OwnedBootStackSwitchStatus.StateRecordNotEmpty
            && hiddenNonZero.Status
                == OwnedBootStackSwitchStatus.StateRecordNotEmpty
            && malformedResult.Status
                == OwnedBootStackSwitchStatus.InvalidStateRecord
            && missingState.Status
                == OwnedBootStackSwitchStatus.MissingStateRecord
            && shortState.Status
                == OwnedBootStackSwitchStatus
                    .StateRecordCapacityTooSmall
            && misalignedState.Status
                == OwnedBootStackSwitchStatus.MisalignedStateRecord
            && overflowingState.Status
                == OwnedBootStackSwitchStatus
                    .StateRecordAddressRangeOverflow
            && !repeatedUnexpected.Succeeded;
    }

    private static unsafe bool TestRspAuditFailures()
    {
        byte* scratch = stackalloc byte[PageScratchSize];
        byte* page = AlignPage(scratch);
        OwnedBootStackSwitchStateRecord state = default;
        OwnedBootStackSwitchPlanResult planned =
            CreatePageBoundPlan(
                page,
                stateStorageAddressRaw: (ulong)&state);
        if (!planned.Succeeded)
        {
            return false;
        }

        OwnedBootStackSwitchPlan plan = planned.Plan;
        ulong guardStart = Raw(plan.GuardVirtualStart);
        ulong stackStart = Raw(plan.UsableStackVirtualStart);
        ulong stackEnd = Raw(plan.UsableStackVirtualEndExclusive);
        ulong expected =
            Raw(plan.ExpectedContinuationStackPointer);

        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }

        OwnedBootStackSwitchStateResult zero =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                default);
        bool zeroTerminal =
            state.TerminalCause
                == OwnedBootStackSwitchTerminalCause
                    .ContinuationRspAuditFailed
            && state.TerminalStatus
                == OwnedBootStackSwitchStatus.InvalidObservedRsp;
        OwnedBootStackSwitchStateResult auditThenUnexpected =
            plan.ReportUnexpectedReturn(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult guard =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                Virtual(guardStart + 8));

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult below =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                Virtual(guardStart - 8));

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult end =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                Virtual(stackEnd));

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult misaligned =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                Virtual(stackStart + 1));

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult mismatch =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                Virtual(stackStart + 8));

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult shadow =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                Virtual(stackEnd - 8));

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult success =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                Virtual(expected));

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult failedCauseBase =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                default);
        bool failedCauseBaseValid = state.IsValid;
        ((byte*)&state)[529] = (byte)
            OwnedBootStackSwitchTerminalCause
                .UnexpectedReturnReported;
        bool corruptCauseInvalid = !state.IsValid;
        OwnedBootStackSwitchStateResult corruptCauseOperation =
            plan.RecordTransfer(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult failedStatusBase =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                default);
        bool failedStatusBaseValid = state.IsValid;
        ((byte*)&state)[530] =
            (byte)OwnedBootStackSwitchStatus.Success;
        bool corruptStatusInvalid = !state.IsValid;
        OwnedBootStackSwitchStateResult corruptStatusOperation =
            plan.RecordTransfer(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);

        state = default;
        if (!PrepareTransferredState(plan, page, &state))
        {
            return false;
        }
        OwnedBootStackSwitchStateResult failedObservedBase =
            plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                Virtual(stackStart + 8));
        bool failedObservedBaseValid = state.IsValid;
        *(VirtualAddress*)((byte*)&state + 24) =
            plan.ExpectedContinuationStackPointer;
        bool corruptObservedInvalid = !state.IsValid;
        OwnedBootStackSwitchStateResult corruptObservedOperation =
            plan.RecordTransfer(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);

        return zero.Status
                == OwnedBootStackSwitchStatus.InvalidObservedRsp
            && zeroTerminal
            && auditThenUnexpected.Status
                == OwnedBootStackSwitchStatus.InvalidState
            && guard.Status
                == OwnedBootStackSwitchStatus.ObservedRspInGuard
            && below.Status
                == OwnedBootStackSwitchStatus
                    .ObservedRspOutsideUsableStack
            && end.Status
                == OwnedBootStackSwitchStatus
                    .ObservedRspOutsideUsableStack
            && misaligned.Status
                == OwnedBootStackSwitchStatus.MisalignedObservedRsp
            && mismatch.Status
                == OwnedBootStackSwitchStatus.ObservedRspMismatch
            && shadow.Status
                == OwnedBootStackSwitchStatus.ShadowSpaceOutOfRange
            && success.Succeeded
            && failedCauseBase.Status
                == OwnedBootStackSwitchStatus.InvalidObservedRsp
            && failedCauseBaseValid
            && corruptCauseInvalid
            && corruptCauseOperation.Status
                == OwnedBootStackSwitchStatus.InvalidStateRecord
            && failedStatusBase.Status
                == OwnedBootStackSwitchStatus.InvalidObservedRsp
            && failedStatusBaseValid
            && corruptStatusInvalid
            && corruptStatusOperation.Status
                == OwnedBootStackSwitchStatus.InvalidStateRecord
            && failedObservedBase.Status
                == OwnedBootStackSwitchStatus.ObservedRspMismatch
            && failedObservedBaseValid
            && corruptObservedInvalid
            && corruptObservedOperation.Status
                == OwnedBootStackSwitchStatus.InvalidStateRecord;
    }

    private static unsafe bool TestLedgerCompatibility()
    {
        OwnedBootStackSwitchPlan plan = CreateValidPlan().Plan;
        if (PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                plan.CodeAllocation.PhysicalStart,
                BootReservedRange.PageSize,
                out PhysicalAddress codeStart)
                != AddressValueStatus.Success)
        {
            return false;
        }

        BootReservedRange* reservations =
            stackalloc BootReservedRange[3];
        if (BootReservedRange.TryCreate(
                BootReservationKind.BootStackSwitchStub,
                codeStart,
                plan.CodeAllocation.PageCount,
                out reservations[0])
                != BootReservedRangeStatus.Success)
        {
            return false;
        }

        reservations[1] = plan.StackLayout.GuardReservation;
        reservations[2] = plan.StackLayout.StackReservation;
        BootReservationTableValidationResult result =
            BootReservationTableValidator.Validate(
                reservations,
                3,
                3);

        return result.Succeeded
            && result.Status
                == BootReservationTableStatus.Success
            && result.ValidatedReservationCount == 3
            && result.BootStackGuardIndex == 1
            && result.BootStackIndex == 2
            && reservations[0].Kind
                == BootReservationKind.BootStackSwitchStub
            && reservations[1].Kind
                == BootReservationKind.BootStackGuard
            && reservations[2].Kind
                == BootReservationKind.BootStack
            && result.TotalPages == ArenaPages + 1
            && result.BootStackArenaPages == ArenaPages
            && result.BootStackArenaBytes
                == ArenaPages * BootReservedRange.PageSize;
    }

    private static unsafe bool TestMalformedValues()
    {
        OwnedBootStackSwitchStateRecord validState = default;
        OwnedBootStackSwitchPlanResult validResult =
            CreatePlan(
                CreateValidRecords(),
                CreateReadyLayout(),
                stateStorageAddressRaw: (ulong)&validState);
        OwnedBootStackSwitchPlan validPlan = validResult.Plan;
        OwnedBootStackSwitchHandoff validHandoff =
            validPlan.Handoff;

        OwnedBootStackSwitchPlan badStubLength =
            MutateUInt64(validPlan, 488, 13);
        OwnedBootStackSwitchHandoff badHandoffReserved =
            MutateByte(validHandoff, 82, 1);
        OwnedBootStackSwitchPlan badNestedHandoff =
            MutateStruct(
                validPlan,
                280,
                badHandoffReserved);
        OwnedBootStackSwitchPlanResult statusOnly =
            MutateByte(
                default(OwnedBootStackSwitchPlanResult),
                0,
                (byte)OwnedBootStackSwitchStatus.Success);
        OwnedBootStackSwitchBytePlanResult byteStatusOnly =
            MutateByte(
                default(OwnedBootStackSwitchBytePlanResult),
                0,
                (byte)OwnedBootStackSwitchStatus.Success);
        byteStatusOnly = MutateByte(
            byteStatusOnly,
            1,
            (byte)OwnedBootStackSwitchByteOperation.Verify);
        OwnedBootStackSwitchStateResult stateStatusOnly =
            MutateByte(
                default(OwnedBootStackSwitchStateResult),
                0,
                (byte)OwnedBootStackSwitchStatus.Success);
        OwnedBootStackSwitchStateResult initialized =
            validPlan.InitializeOneShotState(
                &validState,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateRecord badStateReserved =
            MutateByte(validState, 531, 1);
        OwnedBootStackSwitchStateRecord badStateTransition =
            MutateUInt64(validState, 16, 1);

        return !badStubLength.IsValid
            && !badHandoffReserved.IsValid
            && !badNestedHandoff.IsValid
            && initialized.Succeeded
            && validState.IsValid
            && !badStateReserved.IsValid
            && !badStateTransition.IsValid
            && !statusOnly.Succeeded
            && !byteStatusOnly.Succeeded
            && !stateStatusOnly.Succeeded
            && validPlan.IsValid
            && validHandoff.IsValid
            && validResult.Succeeded;
    }

    private static unsafe bool TestDeterminismImmutabilityAndAllocation()
    {
        byte* scratch = stackalloc byte[PageScratchSize];
        byte* page = AlignPage(scratch);
        OwnedBootStackSwitchStateRecord state = default;
        BootMemoryRegionRecord[] records = CreateValidRecords();
        records[0] = WithRecord(
            records[0],
            virtualStart: (ulong)page);
        OwnedBootStackLayout ready = CreateReadyLayout();
        byte[] recordsBefore = StructBytes(records);
        byte[] layoutBefore = StructBytes(ready);
        OwnedBootStackSwitchPlanResult first =
            CreatePlan(
                records,
                ready,
                stubEntryOffset: BoundStubEntryOffset,
                stateStorageAddressRaw: (ulong)&state);
        OwnedBootStackSwitchPlanResult second =
            CreatePlan(
                records,
                ready,
                stubEntryOffset: BoundStubEntryOffset,
                stateStorageAddressRaw: (ulong)&state);
        if (!first.Succeeded
            || !second.Succeeded
            || !first.Plan.Equals(second.Plan)
            || !recordsBefore.AsSpan().SequenceEqual(
                StructBytes(records))
            || !layoutBefore.AsSpan().SequenceEqual(
                StructBytes(ready)))
        {
            return false;
        }

        OwnedBootStackSwitchBytePlanResult warmEmit =
            first.Plan.EmitAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult warmVerify =
            first.Plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchStateResult warmInitialize =
            first.Plan.InitializeOneShotState(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult warmArm =
            first.Plan.Arm(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                warmVerify);
        OwnedBootStackSwitchStateResult warmTransfer =
            first.Plan.RecordTransfer(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult warmAudit =
            first.Plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                first.Plan.ExpectedContinuationStackPointer);
        if (!warmEmit.Succeeded
            || !warmVerify.Succeeded
            || !warmInitialize.Succeeded
            || !warmArm.Succeeded
            || !warmTransfer.Succeeded
            || !warmAudit.Succeeded)
        {
            return false;
        }

        state = default;
        long before =
            GC.GetAllocatedBytesForCurrentThread();
        OwnedBootStackSwitchBytePlanResult emit =
            first.Plan.EmitAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult verify =
            first.Plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchStateResult initialize =
            first.Plan.InitializeOneShotState(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult arm =
            first.Plan.Arm(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                verify);
        OwnedBootStackSwitchStateResult transfer =
            first.Plan.RecordTransfer(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult audit =
            first.Plan.AuditContinuationRsp(
                &state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                first.Plan.ExpectedContinuationStackPointer);
        long after =
            GC.GetAllocatedBytesForCurrentThread();

        return after == before
            && emit.Succeeded
            && verify.Succeeded
            && initialize.Succeeded
            && arm.Succeeded
            && transfer.Succeeded
            && audit.Succeeded
            && state.State
                == OwnedBootStackSwitchState.ContinuationObserved
            && first.Plan.Equals(second.Plan);
    }

    private static bool TestSourceBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string sourcePath = GetSwitchPlanSourcePath();
        string source = File.ReadAllText(sourcePath);
        string stripped = RemoveComments(source);
        string[] forbiddenPatterns =
        [
            @"\busing\b",
            @"\bSystem\.",
            @"\bthrow\b",
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
            @"\bulong\.MaxValue\b",
            @"\bstackalloc\b",
            @"\bfixed\b",
            @"\bRuntimeImport\b",
            @"\bRuntimeExport\b",
            @"\bDllImport\b",
            @"\bLibraryImport\b",
            @"\bNativeLibrary\b",
            @"\bUnmanagedCallersOnly\b",
            @"\bMarshal\b",
            @"\bIced\b",
            @"\bBootAsm\b",
            @"\bCompileTimeAsm\b",
            @"\bAllocatePages\b",
            @"\bFreePages\b",
            @"\bOpenProtocol\b",
            @"\bHandleProtocol\b",
            @"\bEfiEntry\b",
            @"\bBootSequence\b",
            @"\bProbes\b",
            @"\bConsole\b",
            @"\bThread\b",
            @"\bTask\b",
            @"\bGC\."
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

        if (Regex.IsMatch(
                stripped,
                "delegate" + @"\s*\*",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                stripped,
                @"\b(Invoke|Execute|Activate)\s*\(",
                RegexOptions.CultureInvariant)
            || !stripped.Contains(
                "RequiredStubByteLength = 14",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "X64ShadowSpaceBytes = 32",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "FakeReturnAddressBytes = 8",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "BootMemoryRegionRecord* allocationRecords",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "allocationRecords[CodeAllocationRecordIndex]",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "allocationRecords[DataAllocationRecordIndex]",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "EmitAuditedStubPage(",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "VerifyAuditedStubPage(",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "InitializeOneShotState(",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "OwnedBootStackSwitchStateRecord* stateStorage",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "OwnedBootStackSwitchStateStorageProvenance",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "IsZeroedStateStorage(stateStorage)",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "index < OwnedBootStackSwitchStateRecord.StructureSize",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "OwnedBootStackSwitchTerminalCause",
                StringComparison.Ordinal))
        {
            return SourceFailure("required-contract");
        }

        if (!HasOrderedFragmentsInMethod(
                stripped,
                "EmitAuditedStubPage(",
                "VerifyAuditedStubPage(",
                [
                    "ValidateMaterializedPage(",
                    "byte* stubEntry = codePageBase + _stubEntryOffset"
                ])
            || !HasOrderedFragmentsInMethod(
                stripped,
                "VerifyAuditedStubPage(",
                "InitializeOneShotState(",
                [
                    "ValidateMaterializedPage(",
                    "materializedStubByteLength != RequiredStubByteLength",
                    "byte* stubEntry = codePageBase + _stubEntryOffset"
                ])
            || !HasOrderedFragmentsInMethod(
                stripped,
                "InitializeOneShotState(",
                "public unsafe OwnedBootStackSwitchStateResult Arm(",
                [
                    "ValidateStateStoragePointer(",
                    "stateStorageAddress != expectedStorageAddress",
                    "IsZeroedStateStorage(stateStorage)",
                    "*stateStorage = OwnedBootStackSwitchStateRecord.Create("
                ]))
        {
            return SourceFailure("method-pointer-order");
        }

        int tableGuard = stripped.IndexOf(
            "tableAddress > MaximumUnsignedLong",
            StringComparison.Ordinal);
        int tableDereference = stripped.IndexOf(
            "allocationRecords[CodeAllocationRecordIndex]",
            StringComparison.Ordinal);
        int pageValidation = stripped.IndexOf(
            "ValidateMaterializedPage(",
            StringComparison.Ordinal);
        int pageDereference = stripped.IndexOf(
            "codePageBase + _stubEntryOffset",
            StringComparison.Ordinal);
        int stateValidation = stripped.IndexOf(
            "ValidateStateStoragePointer(",
            StringComparison.Ordinal);
        int stateDereference = stripped.IndexOf(
            "stateStorage->",
            StringComparison.Ordinal);
        if (tableGuard < 0
            || tableDereference < 0
            || tableGuard > tableDereference
            || pageValidation < 0
            || pageDereference < 0
            || pageValidation > pageDereference
            || stateValidation < 0
            || stateDereference < 0
            || stateValidation > stateDereference)
        {
            return SourceFailure("pointer-validation-precedence");
        }

        string testSource = RemoveComments(
            File.ReadAllText(GetTestSourcePath()));
        string[] testExecutionPatterns =
        [
            "delegate" + @"\s*\*",
            "GetDelegate" + "ForFunctionPointer",
            "GetFunctionPointer" + "ForDelegate",
            "Create" + "Delegate",
            "Dynamic" + "Method",
            "Reflection" + ".Emit",
            @"\b" + "Virtual" + "Protect" + @"\s*\(",
            @"\b" + "Virtual" + "Alloc" + @"\s*\(",
            @"\b" + "call" + "i" + @"\b"
        ];
        foreach (string pattern in testExecutionPatterns)
        {
            if (Regex.IsMatch(
                    testSource,
                    pattern,
                    RegexOptions.CultureInvariant))
            {
                return SourceFailure(
                    "test-live-execution=" + pattern);
            }
        }

        if (stripped.IndexOf(
                "allocationRecords[CodeAllocationRecordIndex]",
                StringComparison.Ordinal)
                < tableGuard)
        {
            return SourceFailure("allocation-dereference-precedence");
        }

        Type[] types =
        [
            typeof(OwnedBootStackSwitchHandoff),
            typeof(OwnedBootStackSwitchPlan),
            typeof(OwnedBootStackSwitchStateRecord),
            typeof(OwnedBootStackSwitchPlanResult),
            typeof(OwnedBootStackSwitchBytePlanResult),
            typeof(OwnedBootStackSwitchStateResult)
        ];
        foreach (Type type in types)
        {
            bool mutableStateRecord =
                type == typeof(OwnedBootStackSwitchStateRecord);
            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly))
            {
                if (!field.FieldType.IsValueType
                    || field.FieldType.IsPointer
                    || field.FieldType.IsArray
                    || field.IsStatic && !field.IsLiteral
                    || !field.IsStatic
                        && field.IsInitOnly == mutableStateRecord)
                {
                    return SourceFailure(
                        "managed-or-mutable-field="
                        + type.Name
                        + "."
                        + field.Name);
                }
            }
        }

        string efiProject = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS", "OS.csproj"));
        if (efiProject.Contains(
                nameof(OwnedBootStackSwitchPlan),
                StringComparison.Ordinal)
            || source.Contains(
                "CLROS_M01_005_READY",
                StringComparison.Ordinal))
        {
            return SourceFailure("canonical-efi-boundary");
        }

        string expectedMarkerSource =
            Path.GetFullPath(GetTestSourcePath());
        List<string> markerSources = [];
        foreach (string candidate in Directory.EnumerateFiles(
            repositoryRoot,
            "*.cs",
            SearchOption.AllDirectories))
        {
            string fullCandidate = Path.GetFullPath(candidate);
            if (IsGeneratedOrReferencePath(fullCandidate)
                || !File.ReadAllText(fullCandidate).Contains(
                    "CLROS_M01_005_READY",
                    StringComparison.Ordinal))
            {
                continue;
            }

            markerSources.Add(fullCandidate);
        }

        return markerSources.Count == 1
            && string.Equals(
                markerSources[0],
                expectedMarkerSource,
                StringComparison.OrdinalIgnoreCase)
            && Regex.Matches(
                File.ReadAllText(expectedMarkerSource),
                "Console\\.WriteLine\\(\"CLROS_M01_005_READY\"\\)",
                RegexOptions.CultureInvariant).Count == 1;
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                using OS.Boot;
                using OS.Kernel.Memory;

                internal static unsafe class PositiveSwitchPlanProbe
                {
                    public static OwnedBootStackSwitchStatus Exercise()
                    {
                        PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                            0x300000UL,
                            BootReservedRange.PageSize,
                            out PhysicalAddress backing);
                        PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                            0x301000UL,
                            BootReservedRange.PageSize,
                            out PhysicalAddress stackStart);
                        PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                            0x304000UL,
                            OwnedBootStackLayout.X64StackAlignmentBytes,
                            out PhysicalAddress stackTop);
                        BootReservedRange* reservations =
                            stackalloc BootReservedRange[2];
                        BootReservedRange.TryCreate(
                            BootReservationKind.BootStackGuard,
                            backing,
                            1UL,
                            out reservations[0]);
                        BootReservedRange.TryCreate(
                            BootReservationKind.BootStack,
                            stackStart,
                            3UL,
                            out reservations[1]);
                        OwnedBootStackLayoutResult reserved =
                            OwnedBootStackLayout.Create(
                                reservations,
                                2UL,
                                2UL,
                                backing,
                                4UL,
                                stackTop);
                        OwnedBootStackLayoutResult ready =
                            reserved.Layout.TransitionTo(
                                OwnedBootStackLifetime.Ready);

                        BootMemoryRegionRecord* allocations =
                            stackalloc BootMemoryRegionRecord[2];
                        byte* pageScratch = stackalloc byte[8192];
                        ulong pageAddress =
                            ((ulong)pageScratch + 4095UL) & ~4095UL;
                        byte* codePage = (byte*)pageAddress;
                        allocations[0] = new BootMemoryRegionRecord(
                            (uint)UefiMemoryType.LoaderCode,
                            0x200000UL,
                            pageAddress,
                            1UL,
                            0UL,
                            4096UL,
                            0x201000UL);
                        allocations[1] = new BootMemoryRegionRecord(
                            (uint)UefiMemoryType.LoaderData,
                            0x300000UL,
                            0x500000UL,
                            4UL,
                            0UL,
                            16384UL,
                            0x304000UL);
                        VirtualAddress.TryCreateFromUefiBoundary(
                            0x700000UL,
                            out VirtualAddress handoff);
                        OwnedBootStackSwitchStateRecord state = default;
                        VirtualAddress.TryCreateFromUefiBoundary(
                            (ulong)&state,
                            out VirtualAddress stateStorage);
                        VirtualAddress.TryCreateFromUefiBoundary(
                            0x800010UL,
                            out VirtualAddress continuation);
                        OwnedBootStackSwitchPlanResult planned =
                            OwnedBootStackSwitchPlan.Create(
                                allocations,
                                2UL,
                                2UL,
                                ready.Layout,
                                0UL,
                                OwnedBootStackSwitchPlan
                                    .RequiredStubByteLength,
                                handoff,
                                OwnedBootStackSwitchHandoff
                                    .StructureSize,
                                OwnedBootStackSwitchHandoffProvenance
                                    .CallerAttestedStaticOwnedData,
                                stateStorage,
                                OwnedBootStackSwitchStateRecord.StructureSize,
                                OwnedBootStackSwitchStateStorageProvenance
                                    .CallerAttestedStaticOwnedData,
                                continuation,
                                OwnedBootStackSwitchContinuationContract
                                    .StaticNonReturningRspAndHandoff);
                        planned.Plan.EmitAuditedStubPage(
                            codePage,
                            4096UL,
                            4096UL);
                        OwnedBootStackSwitchBytePlanResult verified =
                            planned.Plan.VerifyAuditedStubPage(
                                codePage,
                                4096UL,
                                4096UL,
                                OwnedBootStackSwitchPlan
                                    .RequiredStubByteLength);
                        planned.Plan.InitializeOneShotState(
                            &state,
                            OwnedBootStackSwitchStateRecord.StructureSize);
                        OwnedBootStackSwitchStateResult armed =
                            planned.Plan.Arm(
                                &state,
                                OwnedBootStackSwitchStateRecord.StructureSize,
                                verified);
                        OwnedBootStackSwitchStateResult transferred =
                            planned.Plan.RecordTransfer(
                                &state,
                                OwnedBootStackSwitchStateRecord.StructureSize);
                        OwnedBootStackSwitchStateResult observed =
                            planned.Plan.AuditContinuationRsp(
                                &state,
                                OwnedBootStackSwitchStateRecord.StructureSize,
                                planned.Plan
                                    .ExpectedContinuationStackPointer);
                        return observed.Status;
                    }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            NoStdLibBuildResult result =
                RunNoStdLibBuild(workspace);
            string outputAssembly = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.OwnedBootStackSwitchCompileClosure.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || ContainsInfrastructureFailure(result.Output)
                || !HasZeroAssemblyReferences(outputAssembly))
            {
                Console.WriteLine(
                    "M01_005_NOSTDLIB_COMPILE_DIAGNOSTIC "
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

    private static unsafe OwnedBootStackLayout CreateReservedLayout()
    {
        PhysicalAddress backing = Physical(DataPhysicalStart);
        PhysicalAddress stackStart = Physical(
            DataPhysicalStart
            + GuardPages * BootReservedRange.PageSize);
        PhysicalAddress stackTop = Physical(
            DataPhysicalStart
            + ArenaPages * BootReservedRange.PageSize);
        BootReservedRange* reservations =
            stackalloc BootReservedRange[2];
        if (BootReservedRange.TryCreate(
                BootReservationKind.BootStackGuard,
                backing,
                GuardPages,
                out reservations[0])
                != BootReservedRangeStatus.Success
            || BootReservedRange.TryCreate(
                BootReservationKind.BootStack,
                stackStart,
                StackPages,
                out reservations[1])
                != BootReservedRangeStatus.Success)
        {
            return default;
        }

        OwnedBootStackLayoutResult result =
            OwnedBootStackLayout.Create(
                reservations,
                2,
                2,
                backing,
                ArenaPages,
                stackTop);
        return result.Succeeded ? result.Layout : default;
    }

    private static OwnedBootStackLayout CreateReadyLayout()
    {
        OwnedBootStackLayout reserved = CreateReservedLayout();
        OwnedBootStackLayoutResult ready =
            reserved.TransitionTo(OwnedBootStackLifetime.Ready);
        return ready.Succeeded ? ready.Layout : default;
    }

    private static BootMemoryRegionRecord[] CreateValidRecords()
    {
        return
        [
            new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderCode,
                CodePhysicalStart,
                CodeVirtualStart,
                1,
                0,
                BootMemoryRegionRecord.PageSize,
                CodePhysicalStart
                    + BootMemoryRegionRecord.PageSize),
            new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderData,
                DataPhysicalStart,
                DataVirtualStart,
                ArenaPages,
                0,
                ArenaPages * BootMemoryRegionRecord.PageSize,
                DataPhysicalStart
                    + ArenaPages * BootMemoryRegionRecord.PageSize)
        ];
    }

    private static OwnedBootStackSwitchPlanResult CreateValidPlan()
    {
        return CreatePlan(
            CreateValidRecords(),
            CreateReadyLayout());
    }

    private static unsafe byte* AlignPage(byte* scratch)
    {
        return (byte*)(
            ((ulong)scratch + (ulong)PageSize - 1)
            & ~((ulong)PageSize - 1));
    }

    private static unsafe OwnedBootStackSwitchPlanResult
        CreatePageBoundPlan(
            byte* page,
            ulong stateStorageAddressRaw = StateStorageVirtualStart,
            ulong continuationRaw = ContinuationVirtualAddress)
    {
        BootMemoryRegionRecord[] records = CreateValidRecords();
        records[0] = WithRecord(
            records[0],
            virtualStart: (ulong)page);
        return CreatePlan(
            records,
            CreateReadyLayout(),
            stubEntryOffset: BoundStubEntryOffset,
            stateStorageAddressRaw: stateStorageAddressRaw,
            continuationRaw: continuationRaw);
    }

    private static unsafe bool PrepareTransferredState(
        OwnedBootStackSwitchPlan plan,
        byte* page,
        OwnedBootStackSwitchStateRecord* state)
    {
        *state = default;
        OwnedBootStackSwitchBytePlanResult emitted =
            plan.EmitAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize);
        OwnedBootStackSwitchBytePlanResult verified =
            plan.VerifyAuditedStubPage(
                page,
                (ulong)PageSize,
                (ulong)PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength);
        OwnedBootStackSwitchStateResult initialized =
            plan.InitializeOneShotState(
                state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        OwnedBootStackSwitchStateResult armed =
            plan.Arm(
                state,
                OwnedBootStackSwitchStateRecord.StructureSize,
                verified);
        OwnedBootStackSwitchStateResult transferred =
            plan.RecordTransfer(
                state,
                OwnedBootStackSwitchStateRecord.StructureSize);
        return emitted.Succeeded
            && verified.Succeeded
            && initialized.Succeeded
            && armed.Succeeded
            && transferred.Succeeded;
    }

    private static unsafe bool PageSliceEquals(
        byte* actual,
        byte[] expected)
    {
        for (int index = 0; index < expected.Length; index++)
        {
            if (actual[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static unsafe OwnedBootStackSwitchPlanResult CreatePlan(
        BootMemoryRegionRecord[] records,
        OwnedBootStackLayout layout,
        ulong stubEntryOffset = 0,
        ulong declaredStubByteLength =
            OwnedBootStackSwitchPlan.RequiredStubByteLength,
        ulong handoffAddressRaw = HandoffVirtualStart,
        ulong handoffLength =
            OwnedBootStackSwitchHandoff.StructureSize,
        OwnedBootStackSwitchHandoffProvenance handoffProvenance =
            OwnedBootStackSwitchHandoffProvenance
                .CallerAttestedStaticOwnedData,
        ulong stateStorageAddressRaw = StateStorageVirtualStart,
        ulong stateStorageLength =
            OwnedBootStackSwitchStateRecord.StructureSize,
        OwnedBootStackSwitchStateStorageProvenance
            stateStorageProvenance =
                OwnedBootStackSwitchStateStorageProvenance
                    .CallerAttestedStaticOwnedData,
        ulong continuationRaw = ContinuationVirtualAddress,
        OwnedBootStackSwitchContinuationContract continuationContract =
            OwnedBootStackSwitchContinuationContract
                .StaticNonReturningRspAndHandoff)
    {
        VirtualAddress handoffAddress = Virtual(handoffAddressRaw);
        VirtualAddress stateStorageAddress =
            Virtual(stateStorageAddressRaw);
        VirtualAddress continuation = Virtual(continuationRaw);

        fixed (BootMemoryRegionRecord* pointer = records)
        {
            return OwnedBootStackSwitchPlan.Create(
                pointer,
                (ulong)records.Length,
                (ulong)records.Length,
                layout,
                stubEntryOffset,
                declaredStubByteLength,
                handoffAddress,
                handoffLength,
                handoffProvenance,
                stateStorageAddress,
                stateStorageLength,
                stateStorageProvenance,
                continuation,
                continuationContract);
        }
    }

    private static bool ExpectRecordFailure(
        BootMemoryRegionRecord[] baseline,
        int index,
        BootMemoryRegionRecord replacement,
        OwnedBootStackSwitchStatus expectedStatus)
    {
        BootMemoryRegionRecord[] records = baseline.ToArray();
        records[index] = replacement;
        OwnedBootStackSwitchPlanResult result =
            CreatePlan(records, CreateReadyLayout());
        return !result.Succeeded
            && result.Status == expectedStatus
            && result.InvalidAllocationRecordIndex == (ulong)index;
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

    private static bool Failure(
        OwnedBootStackSwitchPlanResult result,
        OwnedBootStackSwitchStatus status,
        ulong count,
        ulong capacity,
        ulong invalidIndex)
    {
        return !result.Succeeded
            && result.Status == status
            && result.AllocationRecordCount == count
            && result.AvailableAllocationRecordCapacity == capacity
            && result.InvalidAllocationRecordIndex == invalidIndex
            && !result.Plan.IsValid;
    }

    private static PhysicalAddress Physical(ulong value)
    {
        return PhysicalAddress.TryCreateFromUefiBoundary(
                value,
                out PhysicalAddress address)
                == AddressValueStatus.Success
            ? address
            : default;
    }

    private static VirtualAddress Virtual(ulong value)
    {
        return VirtualAddress.TryCreateFromUefiBoundary(
                value,
                out VirtualAddress address)
                == AddressValueStatus.Success
            ? address
            : default;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        return address.TryGetRawValueForUefiBoundary(out ulong value)
                == AddressValueStatus.Success
            ? value
            : 0;
    }

    private static ulong Raw(VirtualAddress address)
    {
        return address.TryGetRawValueForUefiBoundary(out ulong value)
                == AddressValueStatus.Success
            ? value
            : 0;
    }

    private static bool EnumMatches<TEnum>(
        (string Name, byte Value)[] expected)
        where TEnum : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(TEnum)) != typeof(byte)
            || Enum.GetValues<TEnum>().Length != expected.Length)
        {
            return false;
        }

        foreach ((string name, byte value) in expected)
        {
            byte raw = value;
            TEnum item = Unsafe.As<byte, TEnum>(ref raw);
            if (Enum.GetName(item) != name)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReadOnlyPod<T>(int expectedSize)
        where T : struct
    {
        Type type = typeof(T);
        return type.IsValueType
            && type.IsDefined(
                typeof(IsReadOnlyAttribute),
                inherit: false)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            && Unsafe.SizeOf<T>() == expectedSize
            && Marshal.SizeOf<T>() == expectedSize;
    }

    private static bool IsMutablePod<T>(int expectedSize)
        where T : struct
    {
        Type type = typeof(T);
        return type.IsValueType
            && !type.IsDefined(
                typeof(IsReadOnlyAttribute),
                inherit: false)
            && !RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            && Unsafe.SizeOf<T>() == expectedSize
            && Marshal.SizeOf<T>() == expectedSize;
    }

    private static bool HasOnlyPrivateConstructors<T>()
        where T : struct
    {
        ConstructorInfo[] constructors = typeof(T).GetConstructors(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);
        return constructors.Length != 0
            && constructors.All(
                constructor => constructor.IsPrivate);
    }

    private static bool HasExactOffsets<T>(
        (string Name, int Offset)[] expected)
        where T : struct
    {
        Type type = typeof(T);
        FieldInfo[] instanceFields = type.GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
        if (instanceFields.Length != expected.Length)
        {
            return false;
        }

        foreach ((string name, int offset) in expected)
        {
            FieldInfo? field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is null
                || Marshal.OffsetOf<T>(name).ToInt32() != offset)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasOrderedFragmentsInMethod(
        string source,
        string methodStartMarker,
        string nextMethodMarker,
        string[] orderedFragments)
    {
        int methodStart = source.IndexOf(
            methodStartMarker,
            StringComparison.Ordinal);
        if (methodStart < 0)
        {
            return false;
        }

        int methodEnd = source.IndexOf(
            nextMethodMarker,
            methodStart + methodStartMarker.Length,
            StringComparison.Ordinal);
        if (methodEnd < 0)
        {
            return false;
        }

        int cursor = methodStart + methodStartMarker.Length;
        foreach (string fragment in orderedFragments)
        {
            int found = source.IndexOf(
                fragment,
                cursor,
                StringComparison.Ordinal);
            if (found < 0 || found >= methodEnd)
            {
                return false;
            }

            cursor = found + fragment.Length;
        }

        return true;
    }

    private static T MutateByte<T>(
        T value,
        int byteOffset,
        byte replacement)
        where T : struct
    {
        T[] values = [value];
        Span<byte> bytes = MemoryMarshal.AsBytes(values.AsSpan());
        bytes[byteOffset] = replacement;
        return values[0];
    }

    private static T MutateUInt64<T>(
        T value,
        int byteOffset,
        ulong replacement)
        where T : struct
    {
        T[] values = [value];
        Span<byte> bytes = MemoryMarshal.AsBytes(values.AsSpan());
        MemoryMarshal.Write(bytes[byteOffset..], in replacement);
        return values[0];
    }

    private static TOuter MutateStruct<TOuter, TNested>(
        TOuter value,
        int byteOffset,
        TNested replacement)
        where TOuter : struct
        where TNested : struct
    {
        TOuter[] values = [value];
        Span<byte> bytes = MemoryMarshal.AsBytes(values.AsSpan());
        MemoryMarshal.Write(bytes[byteOffset..], in replacement);
        return values[0];
    }

    private static byte[] StructBytes<T>(T value)
        where T : struct
    {
        T[] values = [value];
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    private static byte[] StructBytes<T>(T[] values)
        where T : struct
    {
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
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

    private static bool SourceFailure(string reason)
    {
        Console.WriteLine(
            "M01_005_SOURCE_FIREWALL_DIAGNOSTIC " + reason);
        return false;
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-005-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.Copy(
                Path.Combine(repositoryRoot, "global.json"),
                Path.Combine(workspace, "global.json"));
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
                    public sealed class IsReadOnlyAttribute : System.Attribute { }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.OwnedBootStackSwitchCompileClosure</AssemblyName>
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
                <Compile Include="__TYPED_ADDRESSES__" Link="Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs" />
                <Compile Include="__BOOT_RANGE__" Link="Memory\BootReservedRange.cs" />
                <Compile Include="__TABLE_VALIDATOR__" Link="Memory\BootReservationTableValidator.cs" />
                <Compile Include="__STACK_LAYOUT__" Link="Boot\OwnedBootStackLayout.cs" />
                <Compile Include="__UEFI_MEMORY_TYPE__" Link="Boot\UefiMemoryType.cs" />
                <Compile Include="__REGION_RECORD__" Link="Boot\BootMemoryRegionMaterializer.cs" />
                <Compile Include="__SWITCH_PLAN__" Link="Boot\OwnedBootStackSwitchPlan.cs" />
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
                "__TYPED_ADDRESSES__",
                EscapeXml(GetTypedAddressSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__BOOT_RANGE__",
                EscapeXml(GetRangeSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__TABLE_VALIDATOR__",
                EscapeXml(GetValidatorSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__STACK_LAYOUT__",
                EscapeXml(GetLayoutSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__UEFI_MEMORY_TYPE__",
                EscapeXml(GetUefiMemoryTypeSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__REGION_RECORD__",
                EscapeXml(GetRegionRecordSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__SWITCH_PLAN__",
                EscapeXml(GetSwitchPlanSourcePath()),
                StringComparison.Ordinal);

            File.WriteAllText(
                Path.Combine(workspace, "M01.005.NoStdLib.csproj"),
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
        start.ArgumentList.Add("M01.005.NoStdLib.csproj");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-m:1");
        start.ArgumentList.Add("--no-dependencies");
        start.ArgumentList.Add("--no-incremental");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-v:minimal");
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
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

    private static bool HasZeroAssemblyReferences(string assemblyPath)
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
        return metadata.AssemblyReferences.Count == 0;
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

    private static string GetTypedAddressSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "TypedPhysicalVirtualDeviceAddressPODWrappers.cs");
    }

    private static string GetRangeSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootReservedRange.cs");
    }

    private static string GetValidatorSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootReservationTableValidator.cs");
    }

    private static string GetLayoutSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "OwnedBootStackLayout.cs");
    }

    private static string GetUefiMemoryTypeSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "UefiMemoryType.cs");
    }

    private static string GetRegionRecordSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "BootMemoryRegionMaterializer.cs");
    }

    private static string GetSwitchPlanSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "OwnedBootStackSwitchPlan.cs");
    }

    private static string GetTestSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "tests",
            "OS.HostTests",
            "OwnedBootStackSwitchPlanTests.cs");
    }

    private static bool IsGeneratedOrReferencePath(string path)
    {
        string[] excludedSegments =
        [
            "bin",
            "obj",
            "artifacts",
            "references",
            ".git"
        ];
        foreach (string segment in excludedSegments)
        {
            string needle =
                Path.DirectorySeparatorChar
                + segment
                + Path.DirectorySeparatorChar;
            if (path.Contains(
                    needle,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        string fullWorkspace = Path.GetFullPath(workspace)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        string? parent = Directory.GetParent(fullWorkspace)?.FullName
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        string leaf = Path.GetFileName(fullWorkspace);

        if (!string.Equals(
                parent,
                tempRoot,
                StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(
                leaf,
                @"\Aclros-m01-005-[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-005 workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        FileAttributes attributes =
            File.GetAttributes(fullWorkspace);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Refusing to delete a reparse-point M01-005 workspace.");
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
