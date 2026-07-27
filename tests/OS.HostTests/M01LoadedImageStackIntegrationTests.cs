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

internal static unsafe class M01LoadedImageStackIntegrationTests
{
    private const string IssueTestName = "m01.017";
    private const int PageSize = (int)BootReservedRange.PageSize;
    private const ulong GuardPages = 1;
    private const ulong StackPages = 3;
    private const ulong ArenaPages = GuardPages + StackPages;
    private const ulong StubEntryOffset = 16;
    private const ulong PreviousMapKey = 0xf000;
    private const ulong RefreshedMapKey = 0x0010;
    private const ulong PreviousMapGeneration = 7;
    private const ulong RefreshedMapGeneration = 8;
    private const ulong CodeAllocationOrdinal = 1;
    private const ulong DataAllocationOrdinal = 2;
    private const ulong MapCaptureOrdinal = 3;
    private const ulong LedgerBuildOrdinal = 4;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run(
            "m01.017 statuses checkpoints pods and defaults are exact",
            TestStableSurfaceAndDefaults,
            ref failures);
        Run(
            "m01.017 canonical prepare transfer observe succeeds",
            TestCanonicalSuccess,
            ref failures);
        Run(
            "m01.017 boot sequence emits exact stage one success lines",
            TestBootSequenceSuccessEmission,
            ref failures);
        Run(
            "m01.017 boot sequence failure suppresses success lines",
            TestBootSequenceFailureEmission,
            ref failures);
        Run(
            "m01.017 boot sequence rejects nonfinal success and null outputs",
            TestBootSequenceNonfinalAndNullOutputs,
            ref failures);
        Run(
            "m01.017 loaded image virtual mapping is exact and fail closed",
            TestLoadedImageVirtualMapping,
            ref failures);
        Run(
            "m01.017 failed loaded image capture rejects before mutation",
            TestLoadedImageRejection,
            ref failures);
        Run(
            "m01.017 stage one identity mappings and aliases fail closed",
            TestStageOneAddressMappingAndAliases,
            ref failures);
        Run(
            "m01.017 unexpected switch return is terminal",
            TestUnexpectedReturn,
            ref failures);
        Run(
            "m01.017 static-only transfer continuation and return are self contained",
            TestStaticOnlyTransitions,
            ref failures);
        Run(
            "m01.017 allocation pointer count and capacity fail before use",
            TestAllocationPointerMetadata,
            ref failures);
        Run(
            "m01.017 allocation types ranges overlap and accounting fail",
            TestAllocationRecordFailures,
            ref failures);
        Run(
            "m01.017 allocation evidence is exact and ordered",
            TestAllocationEvidenceFailures,
            ref failures);
        Run(
            "m01.017 opaque map key generation ordinal and owner binding fail closed",
            TestMapLedgerEvidenceFailures,
            ref failures);
        Run(
            "m01.017 ledger identity sorting duplicates and stack arena fail closed",
            TestReservationLedgerFailures,
            ref failures);
        Run(
            "m01.017 static state code storage and continuation are contained",
            TestStaticAndCodeStorageFailures,
            ref failures);
        Run(
            "m01.017 stub byte mode length entry and readback are audited",
            TestStubFailuresAndVerificationMode,
            ref failures);
        Run(
            "m01.017 repeated and illegal coordinator transitions fail",
            TestIllegalCoordinatorTransitions,
            ref failures);
        Run(
            "m01.017 continuation rsp must equal the contained plan",
            TestRspFailures,
            ref failures);
        Run(
            "m01.017 planning is deterministic nonmutating and preflight safe",
            TestDeterminismAndPreflightNonMutation,
            ref failures);
        Run(
            "m01.021 static scratch preparation is compact exact and nonmutating on rejection",
            TestStaticScratchPreparationAndRejectionNonMutation,
            ref failures);
        Run(
            "m01.021 rich and static preparation share one low-stack core",
            TestSharedLowStackCoreSourceTopology,
            ref failures);
        Run(
            "m01.017 source api marker and efi linkage firewalls hold",
            TestSourceAndApiFirewalls,
            ref failures);
        Run(
            "m01.017 exact sources compile under nostdlib efi",
            TestNoStdLibEfiCompileClosure,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-017 loaded-image/owned-stack integration tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_017_READY");
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

    private static bool TestStableSurfaceAndDefaults()
    {
        (string Name, byte Value)[] statuses =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("LoadedImageRejected", 2),
            ("AllocationTableRejected", 3),
            ("AllocationEvidenceRejected", 4),
            ("MapLedgerEvidenceRejected", 5),
            ("ReservationLedgerRejected", 6),
            ("ReservationIdentityRejected", 7),
            ("StackLayoutRejected", 8),
            ("StackLifetimeRejected", 9),
            ("StaticStateRejected", 10),
            ("SwitchPlanRejected", 11),
            ("StubEmissionRejected", 12),
            ("StubVerificationRejected", 13),
            ("HandoffMaterializationRejected", 14),
            ("StateInitializationRejected", 15),
            ("ArmRejected", 16),
            ("TransferRejected", 17),
            ("ContinuationRspRejected", 18),
            ("UnexpectedReturn", 19),
            ("IllegalCoordinatorState", 20),
            ("StubByteModeRejected", 21),
            ("LoadedImageVirtualMappingRejected", 22),
            ("StageOneAddressMappingRejected", 23),
            ("EvidenceScratchRejected", 24)
        ];
        (string Name, byte Value)[] checkpoints =
        [
            ("Uninitialized", 0),
            ("LoadedImageValidated", 1),
            ("AllocationEvidenceValidated", 2),
            ("MapLedgerValidated", 3),
            ("StackLayoutReady", 4),
            ("SwitchPlanReady", 5),
            ("StubVerified", 6),
            ("SwitchArmed", 7),
            ("TransferRecorded", 8),
            ("ContinuationRspContained", 9)
        ];
        (string Name, byte Value)[] allocationProvenance =
        [
            ("Uninitialized", 0),
            ("CallerAttestedSuccessfulFirmwarePageAllocation", 1)
        ];
        (string Name, byte Value)[] mapProvenance =
        [
            ("Uninitialized", 0),
            ("CallerAttestedPostAllocationOwnedMap", 1)
        ];
        (string Name, byte Value)[] byteModes =
        [
            ("Uninitialized", 0),
            ("EmitAndVerify", 1),
            ("VerifyExisting", 2)
        ];
        (string Name, byte Value)[] mappingProvenance =
        [
            ("Uninitialized", 0),
            ("CallerAttestedIdentityMapping", 1)
        ];
        (string Name, byte Value)[] addressMappingProvenance =
        [
            ("Uninitialized", 0),
            ("CallerAttestedIdentityMappings", 1)
        ];

        M01LoadedImageStackIntegrationResult defaultResult = default;
        M01LoadedImageStackStaticState defaultState = default;
        M01StageOneAllocationEvidence defaultAllocations = default;
        M01StageOneMapLedgerEvidence defaultMap = default;
        M01LoadedImageVirtualMappingEvidence defaultMapping = default;
        M01StageOneAddressMappingEvidence defaultAddressMapping =
            default;
        M01LoadedImageStackTransferTarget defaultTarget = default;
        M01LoadedImageStackStaticTransitionResult
            defaultStaticTransition = default;
        M01LoadedImageStackStaticPreparationResult
            defaultStaticPreparation = default;

        M01LoadedImageStackIntegrationResult recordDefault =
            M01LoadedImageStackIntegration.RecordTransfer(
                default,
                null,
                0);
        M01LoadedImageStackIntegrationResult observeDefault =
            M01LoadedImageStackIntegration.ObserveContinuationRsp(
                default,
                null,
                0,
                default);
        M01LoadedImageStackIntegrationResult returnDefault =
            M01LoadedImageStackIntegration.ReportUnexpectedReturn(
                default,
                null,
                0);

        return EnumContractMatches<M01LoadedImageStackStatus>(statuses)
            && EnumContractMatches<M01LoadedImageStackCheckpoint>(
                checkpoints)
            && EnumContractMatches<M01StageOneAllocationProvenance>(
                allocationProvenance)
            && EnumContractMatches<M01StageOneMapLedgerProvenance>(
                mapProvenance)
            && EnumContractMatches<M01StageOneStubByteMode>(byteModes)
            && EnumContractMatches<
                M01LoadedImageVirtualMappingProvenance>(
                    mappingProvenance)
            && EnumContractMatches<
                M01StageOneAddressMappingProvenance>(
                    addressMappingProvenance)
            && Unsafe.SizeOf<M01StageOneAllocationEvidence>()
                == M01StageOneAllocationEvidence.StructureSize
            && Unsafe.SizeOf<M01StageOneMapLedgerEvidence>()
                == M01StageOneMapLedgerEvidence.StructureSize
            && Unsafe.SizeOf<M01LoadedImageVirtualMappingEvidence>()
                == M01LoadedImageVirtualMappingEvidence.StructureSize
            && Unsafe.SizeOf<M01StageOneAddressMappingEvidence>()
                == M01StageOneAddressMappingEvidence.StructureSize
            && Unsafe.SizeOf<M01LoadedImageStackStaticState>() == 768
            && Unsafe.SizeOf<M01LoadedImageStackTransferTarget>() == 32
            && M01LoadedImageStackStaticState.RequiredStorageBytes == 768
            && M01LoadedImageStackStaticState.RequiredStorageAlignment == 8
            && M01LoadedImageStackStaticState.HandoffStorageOffset == 16
            && M01LoadedImageStackStaticState.SwitchStateStorageOffset
                == 104
            && M01LoadedImageStackStaticState.StackLayoutStorageOffset
                == 640
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    M01StageOneAllocationEvidence>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    M01StageOneMapLedgerEvidence>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    M01LoadedImageVirtualMappingEvidence>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    M01StageOneAddressMappingEvidence>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    M01LoadedImageStackStaticState>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    M01LoadedImageStackEvidence>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    M01LoadedImageStackIntegrationResult>()
            && defaultResult.Status
                == M01LoadedImageStackStatus.Uninitialized
            && defaultResult.LastCompletedCheckpoint
                == M01LoadedImageStackCheckpoint.Uninitialized
            && !defaultResult.IsPrepared
            && !defaultResult.IsTransferRecorded
            && !defaultResult.Succeeded
            && defaultAllocations.Size == 0
            && defaultAllocations.Version == 0
            && defaultAllocations.Provenance
                == M01StageOneAllocationProvenance.Uninitialized
            && !defaultAllocations.IsValidFor(null, 0, 0)
            && defaultMap.Size == 0
            && defaultMap.Version == 0
            && defaultMap.Provenance
                == M01StageOneMapLedgerProvenance.Uninitialized
            && !defaultMap.IsValidFor(default, null, 0, 0)
            && defaultMapping.Size == 0
            && defaultMapping.Version == 0
            && defaultMapping.Provenance
                == M01LoadedImageVirtualMappingProvenance.Uninitialized
            && !defaultMapping.IsValidFor(default)
            && defaultAddressMapping.Size == 0
            && defaultAddressMapping.Version == 0
            && defaultAddressMapping.Provenance
                == M01StageOneAddressMappingProvenance.Uninitialized
            && !defaultAddressMapping.IsValidFor(
                default,
                default,
                default)
            && !defaultState.IsInitialized
            && defaultState.Status
                == M01LoadedImageStackStatus.Uninitialized
            && defaultState.Checkpoint
                == M01LoadedImageStackCheckpoint.Uninitialized
            && !defaultTarget.Entry.IsValid
            && !defaultTarget.NewRspTop.IsValid
            && !defaultTarget.HandoffAddress.IsValid
            && !defaultTarget.ContinuationAddress.IsValid
            && defaultStaticTransition.Status
                == M01LoadedImageStackStatus.Uninitialized
            && defaultStaticTransition.Checkpoint
                == M01LoadedImageStackCheckpoint.Uninitialized
            && !defaultStaticTransition.TransferRecorded
            && !defaultStaticTransition.ContinuationRspContained
            && Unsafe.SizeOf<
                M01LoadedImageStackStaticPreparationResult>() == 8
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    M01LoadedImageStackStaticPreparationResult>()
            && defaultStaticPreparation.Status
                == M01LoadedImageStackStatus.Uninitialized
            && defaultStaticPreparation.Checkpoint
                == M01LoadedImageStackCheckpoint.Uninitialized
            && !defaultStaticPreparation.IsPrepared
            && IsFailure(
                recordDefault,
                M01LoadedImageStackStatus.IllegalCoordinatorState,
                M01LoadedImageStackCheckpoint.Uninitialized)
            && IsFailure(
                observeDefault,
                M01LoadedImageStackStatus.IllegalCoordinatorState,
                M01LoadedImageStackCheckpoint.Uninitialized)
            && IsFailure(
                returnDefault,
                M01LoadedImageStackStatus.IllegalCoordinatorState,
                M01LoadedImageStackCheckpoint.Uninitialized)
            && M01LoadedImageStackIntegration.RequiredAllocationRecordCount
                == 2
            && M01LoadedImageStackIntegration.CodeAllocationRecordIndex == 0
            && M01LoadedImageStackIntegration.DataAllocationRecordIndex == 1
            && M01LoadedImageStackIntegration
                    .RequiredEvidenceScratchAlignment
                == 8
            && M01LoadedImageStackIntegration
                    .RequiredEvidenceScratchBytes
                == (ulong)Unsafe.SizeOf<
                    M01LoadedImageStackEvidence>()
            && !M01LoadedImageStackIntegration
                .CallsFirmwarePageAllocator
            && !M01LoadedImageStackIntegration.InvokesFirmware
            && !M01LoadedImageStackIntegration.ExecutesStackSwitch
            && !M01LoadedImageStackIntegration.IsGuestEvidence
            && !M01LoadedImageStackIntegration.MakesGuardInaccessible
            && !M01LoadedImageStackIntegration.EnforcesWriteXorExecute
            && !M01LoadedImageStackIntegration
                .HostAuditResultMayCrossStackSwitch
            && !M01LoadedImageStackIntegration
                .StaticTransitionRequiresHostAuditResult;
    }

    private static bool TestLoadedImageVirtualMapping()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LoadedImageVirtualMappingEvidence mapping =
            fixture.CreateVirtualMapping();
        if (mapping.Size
                != M01LoadedImageVirtualMappingEvidence.StructureSize
            || mapping.Version
                != M01LoadedImageVirtualMappingEvidence.CurrentVersion
            || mapping.Provenance
                != M01LoadedImageVirtualMappingProvenance
                    .CallerAttestedIdentityMapping
            || mapping.ImageByteLength
                != fixture.LoadedImage.Range.ImageSize
            || !mapping.PhysicalImageBase.Equals(
                fixture.LoadedImage.Range.ImageBase)
            || !mapping.PhysicalImageEndExclusive.Equals(
                fixture.LoadedImage.Range.ImageEndExclusive)
            || !mapping.VirtualImageBase.Equals(
                Virtual(fixture.ImageBase))
            || !mapping.VirtualImageEndExclusive.Equals(
                Virtual(
                    fixture.ImageBase
                    + CanonicalFixture.ImageBytes))
            || !mapping.IsValidFor(fixture.LoadedImage))
        {
            return false;
        }

        M01LoadedImageVirtualMappingEvidence invalidSize =
            WithUIntAt(mapping, 0, 0);
        M01LoadedImageVirtualMappingEvidence invalidVersion =
            WithUIntAt(mapping, 4, 0);
        M01LoadedImageVirtualMappingEvidence invalidProvenance =
            WithByteAt(mapping, 8, 0);
        M01LoadedImageVirtualMappingEvidence reserved =
            WithByteAt(mapping, 9, 1);

        using CanonicalFixture other = new CanonicalFixture();
        M01LoadedImageVirtualMappingEvidence otherMapping =
            other.CreateVirtualMapping();

        M01LoadedImageVirtualMappingEvidence[] invalid =
        [
            default,
            invalidSize,
            invalidVersion,
            invalidProvenance,
            reserved,
            otherMapping
        ];
        foreach (M01LoadedImageVirtualMappingEvidence evidence in invalid)
        {
            fixture.ResetCanonical();
            M01LoadedImageStackIntegrationResult result =
                fixture.PrepareWithVirtualMapping(evidence);
            if (!IsFailure(
                    result,
                    M01LoadedImageStackStatus
                        .LoadedImageVirtualMappingRejected,
                    M01LoadedImageStackCheckpoint.Uninitialized))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestLoadedImageRejection()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        EfiLoadedImageRangeCapturePlan explicitFailure =
            EfiLoadedImageRangeCapturePlan.Create(
                null,
                1,
                1,
                EfiLoadedImageRange.PageSize);
        EfiLoadedImageRangeCapturePlan[] rejected =
        [
            default,
            explicitFailure
        ];

        if (explicitFailure.Succeeded)
        {
            return CaseFailure(
                "loaded-image-explicit-failure-construction");
        }

        foreach (EfiLoadedImageRangeCapturePlan loadedImage
            in rejected)
        {
            fixture.ResetCanonical();
            byte[] codeBefore = fixture.CopyCodePage();
            byte[] stateBefore = fixture.CopyStaticState();

            M01LoadedImageStackIntegrationResult result =
                fixture.PrepareWithLoadedImage(loadedImage);
            if (!IsFailure(
                    result,
                    M01LoadedImageStackStatus.LoadedImageRejected,
                    M01LoadedImageStackCheckpoint.Uninitialized)
                || !codeBefore.AsSpan().SequenceEqual(
                    fixture.CopyCodePage())
                || !stateBefore.AsSpan().SequenceEqual(
                    fixture.CopyStaticState()))
            {
                return CaseFailure(
                    "loaded-image-rejection status="
                    + result.Status
                    + " checkpoint="
                    + result.LastCompletedCheckpoint);
            }
        }

        return true;
    }

    private static bool TestStageOneAddressMappingAndAliases()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01StageOneAddressMappingEvidence mapping =
            fixture.CreateAddressMapping();
        if (mapping.Size
                != M01StageOneAddressMappingEvidence.StructureSize
            || mapping.Version
                != M01StageOneAddressMappingEvidence.CurrentVersion
            || mapping.Provenance
                != M01StageOneAddressMappingProvenance
                    .CallerAttestedIdentityMappings
            || mapping.OwnedMapByteLength
                != fixture.RefreshedMap.BufferSize
            || !mapping.IsValidFor(
                fixture.Records[0],
                fixture.Records[1],
                fixture.RefreshedMap))
        {
            return CaseFailure("canonical-address-mapping");
        }

        M01StageOneAddressMappingEvidence[] malformed =
        [
            default,
            WithUIntAt(mapping, 0, 0),
            WithUIntAt(mapping, 4, 0),
            WithByteAt(mapping, 8, 0),
            WithByteAt(mapping, 9, 1)
        ];
        foreach (M01StageOneAddressMappingEvidence evidence in malformed)
        {
            fixture.ResetCanonical();
            if (!IsAddressMappingFailure(
                    fixture.PrepareWithAddressMapping(evidence)))
            {
                return CaseFailure("malformed-address-mapping");
            }
        }

        M01StageOneAddressMappingEvidence missing =
            M01StageOneAddressMappingEvidence
                .CaptureCallerAttestedIdentity(
                    null,
                    2,
                    2,
                    fixture.RefreshedMap);
        M01StageOneAddressMappingEvidence wrongCount =
            M01StageOneAddressMappingEvidence
                .CaptureCallerAttestedIdentity(
                    fixture.Records,
                    1,
                    2,
                    fixture.RefreshedMap);
        M01StageOneAddressMappingEvidence tooMany =
            M01StageOneAddressMappingEvidence
                .CaptureCallerAttestedIdentity(
                    fixture.Records,
                    3,
                    3,
                    fixture.RefreshedMap);
        M01StageOneAddressMappingEvidence shortCapacity =
            M01StageOneAddressMappingEvidence
                .CaptureCallerAttestedIdentity(
                    fixture.Records,
                    2,
                    1,
                    fixture.RefreshedMap);
        M01StageOneAddressMappingEvidence misaligned =
            M01StageOneAddressMappingEvidence
                .CaptureCallerAttestedIdentity(
                    (BootMemoryRegionRecord*)(
                        (byte*)fixture.Records + 1),
                    2,
                    2,
                    fixture.RefreshedMap);
        M01StageOneAddressMappingEvidence wrapped =
            M01StageOneAddressMappingEvidence
                .CaptureCallerAttestedIdentity(
                    (BootMemoryRegionRecord*)(~0UL - 7),
                    2,
                    2,
                    fixture.RefreshedMap);
        if (missing.Size != 0
            || wrongCount.Size != 0
            || tooMany.Size != 0
            || shortCapacity.Size != 0
            || misaligned.Size != 0
            || wrapped.Size != 0)
        {
            return CaseFailure("mapping-capture-preflight");
        }

        fixture.ResetCanonical();
        fixture.Records[0] = WithRecord(
            fixture.Records[0],
            virtualStart:
                fixture.Records[0].VirtualStart
                + BootMemoryRegionRecord.PageSize);
        if (!IsAddressMappingFailure(
                fixture.PrepareWithAddressMapping(
                    fixture.CreateAddressMapping())))
        {
            return CaseFailure("nonidentity-code");
        }

        fixture.ResetCanonical();
        fixture.Records[1] = WithRecord(
            fixture.Records[1],
            virtualStart:
                fixture.Records[1].VirtualStart
                + BootMemoryRegionRecord.PageSize);
        if (!IsAddressMappingFailure(
                fixture.PrepareWithAddressMapping(
                    fixture.CreateAddressMapping())))
        {
            return CaseFailure("nonidentity-data");
        }

        if (!RejectsIdentityAlias(
                fixture,
                AliasTarget.LoadedImage,
                aliasCode: true,
                M01LoadedImageStackStatus.ReservationLedgerRejected)
            || !RejectsIdentityAlias(
                fixture,
                AliasTarget.LoadedImage,
                aliasCode: false,
                M01LoadedImageStackStatus.ReservationLedgerRejected)
            || !RejectsIdentityAlias(
                fixture,
                AliasTarget.OwnedMap,
                aliasCode: true,
                M01LoadedImageStackStatus.ReservationLedgerRejected)
            || !RejectsIdentityAlias(
                fixture,
                AliasTarget.OwnedMap,
                aliasCode: false,
                M01LoadedImageStackStatus.ReservationLedgerRejected)
            || !RejectsIdentityAlias(
                fixture,
                AliasTarget.AllocationTable,
                aliasCode: true,
                M01LoadedImageStackStatus.StaticStateRejected)
            || !RejectsIdentityAlias(
                fixture,
                AliasTarget.AllocationTable,
                aliasCode: false,
                M01LoadedImageStackStatus.StaticStateRejected)
            || !RejectsIdentityAlias(
                fixture,
                AliasTarget.ReservationLedger,
                aliasCode: true,
                M01LoadedImageStackStatus.StaticStateRejected)
            || !RejectsIdentityAlias(
                fixture,
                AliasTarget.ReservationLedger,
                aliasCode: false,
                M01LoadedImageStackStatus.StaticStateRejected))
        {
            return CaseFailure("identity-alias-matrix");
        }

        fixture.ResetCanonical();
        fixture.RefreshedMap = WithMap(
            fixture.RefreshedMap,
            bufferAddress: (ulong)fixture.Records);
        fixture.AddOwnedMapReservation((ulong)fixture.Records);
        M01LoadedImageStackIntegrationResult mapTable =
            fixture.PrepareCurrent();
        if (!IsFailure(
                mapTable,
                M01LoadedImageStackStatus.StaticStateRejected,
                M01LoadedImageStackCheckpoint.StackLayoutReady))
        {
            return CaseFailure(
                "map-allocation-table status="
                + mapTable.Status
                + " checkpoint="
                + mapTable.LastCompletedCheckpoint);
        }

        fixture.ResetCanonical();
        fixture.RefreshedMap = WithMap(
            fixture.RefreshedMap,
            bufferAddress: (ulong)fixture.Ledger);
        fixture.AddOwnedMapReservation((ulong)fixture.Ledger);
        M01LoadedImageStackIntegrationResult mapLedger =
            fixture.PrepareCurrent();
        if (!IsFailure(
                mapLedger,
                M01LoadedImageStackStatus.StaticStateRejected,
                M01LoadedImageStackCheckpoint.StackLayoutReady))
        {
            return CaseFailure(
                "map-reservation-ledger status="
                + mapLedger.Status
                + " checkpoint="
                + mapLedger.LastCompletedCheckpoint);
        }

        fixture.ResetCanonical();
        fixture.RefreshedMap = WithMap(
            fixture.RefreshedMap,
            bufferAddress: (ulong)fixture.StaticState);
        if (!IsFailure(
                fixture.PrepareCurrent(),
                M01LoadedImageStackStatus.StaticStateRejected,
                M01LoadedImageStackCheckpoint.StackLayoutReady))
        {
            return CaseFailure("map-static-state");
        }

        fixture.ResetCanonical();
        fixture.RefreshedMap = WithMap(
            fixture.RefreshedMap,
            bufferAddress: Raw(fixture.Continuation));
        if (!IsFailure(
                fixture.PrepareCurrent(),
                M01LoadedImageStackStatus.StaticStateRejected,
                M01LoadedImageStackCheckpoint.StackLayoutReady))
        {
            return CaseFailure("map-continuation");
        }

        fixture.ResetCanonical();
        M01LoadedImageStackIntegrationResult aliasedTables =
            fixture.PrepareWithAllocationTable(
                (BootMemoryRegionRecord*)fixture.Ledger,
                2,
                2);
        bool aliasedTablesRejected = !aliasedTables.IsPrepared
            && !aliasedTables.IsTransferRecorded
            && !aliasedTables.Succeeded
            && aliasedTables.Status
                == M01LoadedImageStackStatus.AllocationTableRejected;
        return aliasedTablesRejected
            || CaseFailure(
                "allocation-table-ledger-alias status="
                + aliasedTables.Status
                + " checkpoint="
                + aliasedTables.LastCompletedCheckpoint);
    }

    private static bool RejectsIdentityAlias(
        CanonicalFixture fixture,
        AliasTarget target,
        bool aliasCode,
        M01LoadedImageStackStatus expectedStatus)
    {
        fixture.ResetCanonical();
        ulong aliasStart = target switch
        {
            AliasTarget.LoadedImage => fixture.ImageBase,
            AliasTarget.OwnedMap =>
                fixture.AlternateOwnedMapAddress,
            AliasTarget.AllocationTable => (ulong)fixture.Records,
            AliasTarget.ReservationLedger => (ulong)fixture.Ledger,
            _ => 0
        };
        if (aliasStart == 0)
        {
            return false;
        }

        int recordIndex = aliasCode ? 0 : 1;
        BootMemoryRegionRecord record = fixture.Records[recordIndex];
        fixture.Records[recordIndex] = WithRecord(
            record,
            physicalStart: aliasStart,
            virtualStart: aliasStart,
            physicalEndExclusive:
                aliasStart + record.ByteLength);
        fixture.RebuildCanonicalLedgerFromRecords();
        if (target == AliasTarget.OwnedMap)
        {
            fixture.RefreshedMap = WithMap(
                fixture.RefreshedMap,
                bufferAddress: aliasStart);
            fixture.AddOwnedMapReservation(aliasStart);
        }

        M01LoadedImageStackIntegrationResult result =
            aliasCode
                ? fixture.PrepareWithStorage(
                    (byte*)aliasStart,
                    PageSize,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    fixture.Continuation,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes)
                : fixture.PrepareCurrent();
        M01LoadedImageStackCheckpoint expectedCheckpoint =
            expectedStatus
                    == M01LoadedImageStackStatus.StaticStateRejected
                ? M01LoadedImageStackCheckpoint.StackLayoutReady
                : M01LoadedImageStackCheckpoint
                    .AllocationEvidenceValidated;
        if (IsFailure(result, expectedStatus, expectedCheckpoint))
        {
            return true;
        }

        return CaseFailure(
            "alias target="
            + target
            + " code="
            + aliasCode
            + " expected="
            + expectedStatus
            + "/"
            + expectedCheckpoint
            + " actual="
            + result.Status
            + "/"
            + result.LastCompletedCheckpoint);
    }

    private enum AliasTarget : byte
    {
        LoadedImage = 1,
        OwnedMap = 2,
        AllocationTable = 3,
        ReservationLedger = 4
    }

    private static bool TestCanonicalSuccess()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        M01LoadedImageStackIntegrationResult prepared =
            fixture.PrepareCurrent();
        if (!prepared.IsPrepared
            || prepared.IsTransferRecorded
            || prepared.Succeeded
            || prepared.Status != M01LoadedImageStackStatus.Success
            || prepared.LastCompletedCheckpoint
                != M01LoadedImageStackCheckpoint.SwitchArmed
            || prepared.Evidence.StubByteMode
                != M01StageOneStubByteMode.EmitAndVerify
            || prepared.Evidence.StubEmission.Operation
                != OwnedBootStackSwitchByteOperation.Emit
            || prepared.Evidence.StubVerification.Operation
                != OwnedBootStackSwitchByteOperation.Verify
            || prepared.Evidence.StackLayoutResult.CurrentLifetime
                != OwnedBootStackLifetime.Ready
            || prepared.Evidence.SwitchStateResult.CurrentState
                != OwnedBootStackSwitchState.Armed
            || prepared.Evidence.StaticStateAddress
                != (ulong)fixture.StaticState
            || prepared.Evidence.StaticStateCapacity
                != M01LoadedImageStackStaticState.RequiredStorageBytes
            || prepared.Evidence.MapLedgerEvidence.PreviousMapKey
                <= prepared.Evidence.MapLedgerEvidence
                    .RefreshedMap.MapKey
            || prepared.Evidence.MapLedgerEvidence
                    .RefreshedMapGeneration
                != prepared.Evidence.MapLedgerEvidence
                        .PreviousMapGeneration
                    + 1
            || !fixture.StaticState->IsInitialized
            || fixture.StaticState->Checkpoint
                != M01LoadedImageStackCheckpoint.SwitchArmed
            || !StubBytesMatch(fixture))
        {
            return false;
        }

        M01LoadedImageStackIntegrationResult transferred =
            M01LoadedImageStackIntegration.RecordTransfer(
                prepared,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        if (!transferred.IsTransferRecorded
            || transferred.Succeeded
            || transferred.LastCompletedCheckpoint
                != M01LoadedImageStackCheckpoint.TransferRecorded
            || transferred.Evidence.SwitchStateResult.CurrentState
                != OwnedBootStackSwitchState.TransferRecorded
            || !transferred.TransferTarget.IsValidFor(
                transferred.Evidence.SwitchPlanResult.Plan)
            || !transferred.TransferTarget.Entry.Equals(
                transferred.Evidence.SwitchPlanResult.Plan.StubVirtualEntry)
            || !transferred.TransferTarget.NewRspTop.Equals(
                transferred.Evidence.SwitchPlanResult.Plan
                    .PreTransferStackPointer)
            || !transferred.TransferTarget.HandoffAddress.Equals(
                transferred.Evidence.SwitchPlanResult.Plan
                    .Handoff.StorageStart)
            || !transferred.TransferTarget.ContinuationAddress.Equals(
                fixture.Continuation))
        {
            return false;
        }

        VirtualAddress expectedRsp =
            transferred.Evidence.SwitchPlanResult.Plan
                .ExpectedContinuationStackPointer;
        M01LoadedImageStackIntegrationResult observed =
            M01LoadedImageStackIntegration.ObserveContinuationRsp(
                transferred,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                expectedRsp);

        return observed.Succeeded
            && !observed.IsPrepared
            && !observed.IsTransferRecorded
            && observed.Status == M01LoadedImageStackStatus.Success
            && observed.LastCompletedCheckpoint
                == M01LoadedImageStackCheckpoint.ContinuationRspContained
            && observed.Evidence.ObservedRsp.Equals(expectedRsp)
            && observed.Evidence.StackLayoutResult.CurrentLifetime
                == OwnedBootStackLifetime.Active
            && observed.Evidence.SwitchStateResult.CurrentState
                == OwnedBootStackSwitchState.ContinuationObserved
            && fixture.StaticState->Status
                == M01LoadedImageStackStatus.Success
            && fixture.StaticState->Checkpoint
                == M01LoadedImageStackCheckpoint.ContinuationRspContained;
    }

    private static bool TestBootSequenceSuccessEmission()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LoadedImageStackIntegrationResult integration =
            CompleteCanonicalIntegration(fixture);
        M01TestSerialSink serial = new M01TestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result =
            BootSequence.RunM01LoadedImageStackIntegration(
                integration,
                serial,
                events);
        string[] expected =
        [
            "TEST_START m01.stage1",
            "M01_LOADED_IMAGE_CAPTURED",
            "M01_STACK_SWITCH_STUB_READY",
            "M01_STACK_ARENA_ALLOCATED",
            "M01_STACK_GUARD_RESERVED",
            "M01_STAGE1_MAP_RECAPTURED",
            "M01_STAGE1_RESERVATIONS_VALIDATED",
            "M01_OWNED_STACK_READY",
            "M01_OWNED_STACK_SWITCHED",
            "M01_RSP_CONTAINED",
            "TEST_PASS m01.stage1"
        ];

        if (!integration.Succeeded
            || !result.Succeeded
            || result.Reason.Length != 0
            || BootSequencePlan.M01StageOneSuccessLineCount
                != expected.Length
            || serial.Count != expected.Length)
        {
            return CaseFailure("boot-sequence-success-shape");
        }

        string[] eventSnapshot = events.Snapshot();
        if (eventSnapshot.Length != expected.Length)
        {
            return CaseFailure("boot-sequence-success-event-count");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    serial.LineAt(index),
                    expected[index],
                    StringComparison.Ordinal)
                || !string.Equals(
                    eventSnapshot[index],
                    expected[index],
                    StringComparison.Ordinal)
                || !string.Equals(
                    BootSequence.CreateM01StageOneSuccessLine(index),
                    expected[index],
                    StringComparison.Ordinal))
            {
                return CaseFailure(
                    "boot-sequence-success-line=" + index);
            }
        }

        return true;
    }

    private static bool TestBootSequenceFailureEmission()
    {
        M01TestSerialSink serial = new M01TestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        M01LoadedImageStackIntegrationResult integration = default;

        BootResult result =
            BootSequence.RunM01LoadedImageStackIntegration(
                integration,
                serial,
                events);
        string detail = "Uninitialized checkpoint=Uninitialized";
        string[] expected =
        [
            "TEST_START m01.stage1",
            BootSequence.M01StageOneTestFailurePrefix + detail,
            BootSequence.M01StageOneFailurePrefix + detail
        ];

        if (result.Succeeded
            || !string.Equals(
                result.Reason,
                "M01 stage one failed: " + detail,
                StringComparison.Ordinal)
            || serial.Count != expected.Length)
        {
            return CaseFailure("boot-sequence-failure-shape");
        }

        string[] eventSnapshot = events.Snapshot();
        if (eventSnapshot.Length != expected.Length)
        {
            return CaseFailure("boot-sequence-failure-event-count");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    serial.LineAt(index),
                    expected[index],
                    StringComparison.Ordinal)
                || !string.Equals(
                    eventSnapshot[index],
                    expected[index],
                    StringComparison.Ordinal))
            {
                return CaseFailure(
                    "boot-sequence-failure-line=" + index);
            }
        }

        for (int index = 1;
            index < BootSequencePlan.M01StageOneSuccessLineCount;
            index++)
        {
            if (serial.Contains(
                BootSequence.CreateM01StageOneSuccessLine(index)))
            {
                return CaseFailure(
                    "boot-sequence-failure-leaked-success-line="
                    + index);
            }
        }

        return true;
    }

    private static bool TestBootSequenceNonfinalAndNullOutputs()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LoadedImageStackIntegrationResult prepared =
            fixture.PrepareCurrent();
        M01TestSerialSink serial = new M01TestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult nonfinal =
            BootSequence.RunM01LoadedImageStackIntegration(
                prepared,
                serial,
                events);
        string detail =
            "IllegalCoordinatorState checkpoint=SwitchArmed";
        string[] expected =
        [
            "TEST_START m01.stage1",
            BootSequence.M01StageOneTestFailurePrefix + detail,
            BootSequence.M01StageOneFailurePrefix + detail
        ];
        string[] eventSnapshot = events.Snapshot();
        if (prepared.Status != M01LoadedImageStackStatus.Success
            || prepared.Succeeded
            || nonfinal.Succeeded
            || !string.Equals(
                nonfinal.Reason,
                "M01 stage one failed: " + detail,
                StringComparison.Ordinal)
            || serial.Count != expected.Length
            || eventSnapshot.Length != expected.Length)
        {
            return CaseFailure("boot-sequence-nonfinal-shape");
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(
                    serial.LineAt(index),
                    expected[index],
                    StringComparison.Ordinal)
                || !string.Equals(
                    eventSnapshot[index],
                    expected[index],
                    StringComparison.Ordinal))
            {
                return CaseFailure(
                    "boot-sequence-nonfinal-line=" + index);
            }
        }

        M01TestSerialSink nullEventSerial =
            new M01TestSerialSink();
        BootEventRingBuffer nullSerialEvents =
            new BootEventRingBuffer(4);
        BootResult nullSerial =
            BootSequence.RunM01LoadedImageStackIntegration(
                default,
                null!,
                nullSerialEvents);
        BootResult nullEvents =
            BootSequence.RunM01LoadedImageStackIntegration(
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

    private static M01LoadedImageStackIntegrationResult
        CompleteCanonicalIntegration(CanonicalFixture fixture)
    {
        M01LoadedImageStackIntegrationResult prepared =
            fixture.PrepareCurrent();
        M01LoadedImageStackIntegrationResult transferred =
            M01LoadedImageStackIntegration.RecordTransfer(
                prepared,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        return M01LoadedImageStackIntegration.ObserveContinuationRsp(
            transferred,
            fixture.StaticState,
            M01LoadedImageStackStaticState.RequiredStorageBytes,
            transferred.Evidence.SwitchPlanResult.Plan
                .ExpectedContinuationStackPointer);
    }

    private static bool TestUnexpectedReturn()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LoadedImageStackIntegrationResult prepared =
            fixture.PrepareCurrent();
        M01LoadedImageStackIntegrationResult transferred =
            M01LoadedImageStackIntegration.RecordTransfer(
                prepared,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        M01LoadedImageStackIntegrationResult returned =
            M01LoadedImageStackIntegration.ReportUnexpectedReturn(
                transferred,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);

        return IsFailure(
                returned,
                M01LoadedImageStackStatus.UnexpectedReturn,
                M01LoadedImageStackCheckpoint.TransferRecorded)
            && returned.Evidence.SwitchStateResult.Status
                == OwnedBootStackSwitchStatus.UnexpectedReturn
            && returned.Evidence.SwitchStateResult.CurrentState
                == OwnedBootStackSwitchState.Failed
            && fixture.StaticState->Status
                == M01LoadedImageStackStatus.UnexpectedReturn
            && fixture.StaticState->SwitchState.State
                == OwnedBootStackSwitchState.Failed
            && fixture.StaticState->SwitchState.TerminalCause
                == OwnedBootStackSwitchTerminalCause
                    .UnexpectedReturnReported;
    }

    private static bool TestStaticOnlyTransitions()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LoadedImageStackIntegrationResult prepared =
            fixture.PrepareCurrent();
        M01LoadedImageStackStaticTransitionResult transferred =
            M01LoadedImageStackIntegration
                .RecordTransferFromStaticState(
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes);
        if (!prepared.IsPrepared
            || !transferred.TransferRecorded
            || transferred.ContinuationRspContained
            || transferred.Status != M01LoadedImageStackStatus.Success
            || transferred.Checkpoint
                != M01LoadedImageStackCheckpoint.TransferRecorded
            || transferred.StateResult.CurrentState
                != OwnedBootStackSwitchState.TransferRecorded
            || transferred.LayoutResult.CurrentLifetime
                != OwnedBootStackLifetime.Ready
            || !transferred.TransferTarget.IsValidFor(
                transferred.StateResult.Plan))
        {
            return false;
        }

        M01LoadedImageStackStaticTransitionResult observed =
            M01LoadedImageStackIntegration
                .ObserveContinuationRspFromStaticState(
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes,
                    transferred.StateResult.Plan
                        .ExpectedContinuationStackPointer);
        if (!observed.ContinuationRspContained
            || observed.TransferRecorded
            || observed.Status != M01LoadedImageStackStatus.Success
            || observed.Checkpoint
                != M01LoadedImageStackCheckpoint
                    .ContinuationRspContained
            || observed.StateResult.CurrentState
                != OwnedBootStackSwitchState.ContinuationObserved
            || observed.LayoutResult.CurrentLifetime
                != OwnedBootStackLifetime.Active
            || fixture.StaticState->StackLayout.Lifetime
                != OwnedBootStackLifetime.Active)
        {
            return false;
        }

        M01LoadedImageStackStaticTransitionResult repeated =
            M01LoadedImageStackIntegration
                .ObserveContinuationRspFromStaticState(
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes,
                    observed.StateResult.Plan
                        .ExpectedContinuationStackPointer);
        if (repeated.Status
                != M01LoadedImageStackStatus.IllegalCoordinatorState
            || repeated.TransferRecorded
            || repeated.ContinuationRspContained)
        {
            return false;
        }

        using CanonicalFixture returnedFixture =
            new CanonicalFixture();
        _ = returnedFixture.PrepareCurrent();
        M01LoadedImageStackStaticTransitionResult returnTransfer =
            M01LoadedImageStackIntegration
                .RecordTransferFromStaticState(
                    returnedFixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes);
        M01LoadedImageStackStaticTransitionResult returned =
            M01LoadedImageStackIntegration
                .ReportUnexpectedReturnFromStaticState(
                    returnedFixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes);
        M01LoadedImageStackStaticTransitionResult missing =
            M01LoadedImageStackIntegration
                .RecordTransferFromStaticState(null, 0);

        if (!returnTransfer.TransferRecorded
            || returned.Status
                != M01LoadedImageStackStatus.UnexpectedReturn
            || returned.Checkpoint
                != M01LoadedImageStackCheckpoint.TransferRecorded
            || returned.StateResult.Status
                != OwnedBootStackSwitchStatus.UnexpectedReturn
            || returned.TransferRecorded
            || returned.ContinuationRspContained
            || missing.Status
                != M01LoadedImageStackStatus.IllegalCoordinatorState)
        {
            return false;
        }

        Type transitionType =
            typeof(M01LoadedImageStackStaticTransitionResult);
        foreach (FieldInfo field in transitionType.GetFields(
            BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly))
        {
            Type fieldType = field.FieldType;
            if (fieldType == typeof(M01LoadedImageStackEvidence)
                || fieldType == typeof(M01StageOneAllocationEvidence)
                || fieldType == typeof(M01StageOneMapLedgerEvidence)
                || fieldType == typeof(BootReservationTableValidationResult)
                || field.Name.Contains(
                    "Address",
                    StringComparison.OrdinalIgnoreCase)
                || field.Name.Contains(
                    "Ledger",
                    StringComparison.OrdinalIgnoreCase)
                || field.Name.Contains(
                    "Table",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return !RuntimeHelpers
            .IsReferenceOrContainsReferences<
                M01LoadedImageStackStaticTransitionResult>();
    }

    private static bool TestAllocationPointerMetadata()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        M01LoadedImageStackIntegrationResult missing =
            fixture.PrepareWithAllocationTable(null, 2, 2);
        M01LoadedImageStackIntegrationResult zero =
            fixture.PrepareWithAllocationTable(fixture.Records, 0, 2);
        M01LoadedImageStackIntegrationResult one =
            fixture.PrepareWithAllocationTable(fixture.Records, 1, 2);
        M01LoadedImageStackIntegrationResult tooMany =
            fixture.PrepareWithAllocationTable(fixture.Records, 3, 3);
        M01LoadedImageStackIntegrationResult capacity =
            fixture.PrepareWithAllocationTable(fixture.Records, 2, 1);
        M01LoadedImageStackIntegrationResult misaligned =
            fixture.PrepareWithAllocationTable(
                (BootMemoryRegionRecord*)((byte*)fixture.Records + 1),
                2,
                2);
        M01LoadedImageStackIntegrationResult overflow =
            fixture.PrepareWithAllocationTable(
                (BootMemoryRegionRecord*)(~0UL - 7),
                2,
                2);

        return IsAllocationTableFailure(missing)
            && IsAllocationTableFailure(zero)
            && IsAllocationTableFailure(one)
            && IsAllocationTableFailure(tooMany)
            && IsAllocationTableFailure(capacity)
            && IsAllocationTableFailure(misaligned)
            && IsAllocationTableFailure(overflow)
            && fixture.CodeAndStateAreZero();
    }

    private static bool TestAllocationRecordFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        BootMemoryRegionRecord canonicalCode = fixture.Records[0];
        BootMemoryRegionRecord canonicalData = fixture.Records[1];

        BootMemoryRegionRecord[] invalidCode =
        [
            WithRecord(
                canonicalCode,
                uefiType: (uint)UefiMemoryType.LoaderData),
            WithRecord(
                canonicalCode,
                uefiType: (uint)UefiMemoryType.ConventionalMemory),
            WithRecord(canonicalCode, physicalStart: 0),
            WithRecord(
                canonicalCode,
                physicalStart: canonicalCode.PhysicalStart + 1),
            WithRecord(canonicalCode, virtualStart: 0),
            WithRecord(
                canonicalCode,
                virtualStart: canonicalCode.VirtualStart + 1),
            WithRecord(canonicalCode, pageCount: 0, byteLength: 0),
            WithRecord(
                canonicalCode,
                pageCount: 2,
                byteLength: 2 * BootMemoryRegionRecord.PageSize,
                physicalEndExclusive:
                    canonicalCode.PhysicalStart
                    + 2 * BootMemoryRegionRecord.PageSize),
            WithRecord(
                canonicalCode,
                byteLength: canonicalCode.ByteLength - 1),
            WithRecord(
                canonicalCode,
                physicalEndExclusive:
                    canonicalCode.PhysicalEndExclusive
                    + BootMemoryRegionRecord.PageSize),
            WithRecord(
                canonicalCode,
                physicalStart: ~0UL - (BootMemoryRegionRecord.PageSize - 1),
                physicalEndExclusive: 1),
            WithRecord(
                canonicalCode,
                virtualStart: ~0UL - (BootMemoryRegionRecord.PageSize - 1))
        ];
        foreach (BootMemoryRegionRecord invalid in invalidCode)
        {
            fixture.ResetCanonical();
            fixture.Records[0] = invalid;
            if (!IsAllocationTableFailure(fixture.PrepareCurrent()))
            {
                return false;
            }
        }

        BootMemoryRegionRecord[] invalidData =
        [
            WithRecord(
                canonicalData,
                uefiType: (uint)UefiMemoryType.LoaderCode),
            WithRecord(
                canonicalData,
                uefiType: (uint)UefiMemoryType.ConventionalMemory),
            WithRecord(canonicalData, physicalStart: 0),
            WithRecord(
                canonicalData,
                physicalStart: canonicalData.PhysicalStart + 1),
            WithRecord(canonicalData, virtualStart: 0),
            WithRecord(
                canonicalData,
                virtualStart: canonicalData.VirtualStart + 1),
            WithRecord(canonicalData, pageCount: 0, byteLength: 0),
            WithRecord(
                canonicalData,
                pageCount: 1,
                byteLength: BootMemoryRegionRecord.PageSize,
                physicalEndExclusive:
                    canonicalData.PhysicalStart
                    + BootMemoryRegionRecord.PageSize),
            WithRecord(
                canonicalData,
                pageCount: ~0UL,
                byteLength: ~0UL),
            WithRecord(
                canonicalData,
                byteLength: canonicalData.ByteLength - 1),
            WithRecord(
                canonicalData,
                physicalEndExclusive:
                    canonicalData.PhysicalEndExclusive
                    + BootMemoryRegionRecord.PageSize),
            WithRecord(
                canonicalData,
                virtualStart: ~0UL - (BootMemoryRegionRecord.PageSize - 1))
        ];
        foreach (BootMemoryRegionRecord invalid in invalidData)
        {
            fixture.ResetCanonical();
            fixture.Records[1] = invalid;
            if (!IsAllocationTableFailure(fixture.PrepareCurrent()))
            {
                return false;
            }
        }

        fixture.ResetCanonical();
        fixture.Records[1] = WithRecord(
            fixture.Records[1],
            physicalStart: fixture.Records[0].PhysicalStart,
            physicalEndExclusive:
                fixture.Records[0].PhysicalStart
                + fixture.Records[1].ByteLength);
        if (!IsAllocationTableFailure(fixture.PrepareCurrent()))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Records[1] = WithRecord(
            fixture.Records[1],
            virtualStart: fixture.Records[0].VirtualStart);
        return IsAllocationTableFailure(fixture.PrepareCurrent());
    }

    private static bool TestAllocationEvidenceFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        M01StageOneAllocationEvidence[] invalid =
        [
            default,
            M01StageOneAllocationEvidence.CaptureCallerAttested(
                fixture.Records + 1,
                2,
                2,
                1,
                2,
                0,
                0),
            M01StageOneAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                1,
                2,
                1,
                2,
                0,
                0),
            M01StageOneAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                2,
                3,
                1,
                2,
                0,
                0),
            M01StageOneAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                2,
                2,
                0,
                1,
                0,
                0),
            M01StageOneAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                2,
                2,
                2,
                2,
                0,
                0),
            M01StageOneAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                2,
                2,
                ~0UL,
                0,
                0,
                0),
            M01StageOneAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                2,
                2,
                1,
                2,
                1,
                0),
            M01StageOneAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                2,
                2,
                1,
                2,
                0,
                1)
        ];

        foreach (M01StageOneAllocationEvidence evidence in invalid)
        {
            fixture.ResetCanonical();
            M01LoadedImageStackIntegrationResult result =
                fixture.PrepareWithAllocationEvidence(evidence);
            if (!IsFailure(
                    result,
                    M01LoadedImageStackStatus.AllocationEvidenceRejected,
                    M01LoadedImageStackCheckpoint.LoadedImageValidated))
            {
                return false;
            }
        }

        M01StageOneAllocationEvidence valid =
            fixture.CreateAllocationEvidence();
        return valid.Size == M01StageOneAllocationEvidence.StructureSize
            && valid.Version == M01StageOneAllocationEvidence.CurrentVersion
            && valid.Provenance
                == M01StageOneAllocationProvenance
                    .CallerAttestedSuccessfulFirmwarePageAllocation
            && valid.AllocationTableAddress == (ulong)fixture.Records
            && valid.AllocationRecordCount == 2
            && valid.AllocationRecordCapacity == 2
            && valid.CodeAllocationOrdinal == CodeAllocationOrdinal
            && valid.DataAllocationOrdinal == DataAllocationOrdinal
            && valid.CodeFirmwareStatus == 0
            && valid.DataFirmwareStatus == 0
            && valid.IsValidFor(fixture.Records, 2, 2);
    }

    private static bool TestMapLedgerEvidenceFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01StageOneAllocationEvidence allocations =
            fixture.CreateAllocationEvidence();

        bool Rejects(M01StageOneMapLedgerEvidence evidence)
        {
            fixture.ZeroMutableStorage();
            M01LoadedImageStackIntegrationResult result =
                fixture.PrepareWithEvidence(allocations, evidence);
            return IsFailure(
                result,
                M01LoadedImageStackStatus.MapLedgerEvidenceRejected,
                M01LoadedImageStackCheckpoint
                    .AllocationEvidenceValidated);
        }

        if (!Rejects(default)
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                previousMapKey: RefreshedMapKey))
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                previousMapGeneration: 0))
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                previousMapGeneration: ~0UL,
                refreshedMapGeneration: 0))
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                refreshedMapGeneration: PreviousMapGeneration))
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                refreshedMapGeneration:
                    RefreshedMapGeneration + 1))
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                mapCaptureOrdinal: DataAllocationOrdinal))
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                ledgerBuildOrdinal: MapCaptureOrdinal))
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                ledger: fixture.Ledger + 1))
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                ledgerCount: fixture.LedgerCount - 1))
            || !Rejects(fixture.CreateMapEvidence(
                allocations,
                ledgerCapacity: fixture.LedgerCount - 1)))
        {
            return false;
        }

        M01StageOneAllocationEvidence otherOrdinals =
            M01StageOneAllocationEvidence.CaptureCallerAttested(
                fixture.Records,
                2,
                2,
                10,
                11,
                0,
                0);
        if (!Rejects(fixture.CreateMapEvidence(otherOrdinals)))
        {
            return false;
        }

        FirmwareMemoryMapRecord map = fixture.RefreshedMap;
        FirmwareMemoryMapRecord[] malformedMaps =
        [
            WithMap(map, flags: 0),
            WithMap(map, bufferAddress: 0),
            WithMap(map, bufferSize: map.MapSize - 1),
            WithMap(map, mapSize: 0),
            WithMap(map, descriptorSize: 0),
            WithMap(map, descriptorCount: 0),
            WithMap(
                map,
                descriptorSize: 2,
                descriptorCount: ~0UL),
            WithMap(
                map,
                mapSize: map.MapSize + map.DescriptorSize)
        ];
        foreach (FirmwareMemoryMapRecord malformed in malformedMaps)
        {
            if (!Rejects(fixture.CreateMapEvidence(
                    allocations,
                    refreshedMap: malformed)))
            {
                return false;
            }
        }

        M01StageOneMapLedgerEvidence valid =
            fixture.CreateMapEvidence(allocations);
        return valid.IsValidFor(
                allocations,
                fixture.Ledger,
                fixture.LedgerCount,
                fixture.LedgerCapacity)
            && valid.PreviousMapKey == PreviousMapKey
            && valid.RefreshedMap.MapKey == RefreshedMapKey
            && valid.PreviousMapKey > valid.RefreshedMap.MapKey
            && valid.RefreshedMapGeneration
                == valid.PreviousMapGeneration + 1
            && valid.MapCaptureOrdinal > valid.DataAllocationOrdinal
            && valid.LedgerBuildOrdinal > valid.MapCaptureOrdinal;
    }

    private static bool TestReservationLedgerFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        fixture.SwapLedgerEntries(1, 2);
        M01LoadedImageStackIntegrationResult unsorted =
            fixture.PrepareCurrent();
        if (!IsReservationFailure(
                unsorted,
                BootReservationTableStatus.UnsortedRange))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.InsertLedgerEntry(2, fixture.Ledger[1]);
        M01LoadedImageStackIntegrationResult duplicate =
            fixture.PrepareCurrent();
        if (!IsReservationFailure(
                duplicate,
                BootReservationTableStatus.DuplicateRange))
        {
            return false;
        }

        BootReservationKind[] imageCoveredKinds =
        [
            BootReservationKind.OwnedMemoryMap,
            BootReservationKind.AcpiSnapshot,
            BootReservationKind.AcpiScratch,
            BootReservationKind.DiagnosticState,
            BootReservationKind.BootHandoff,
            BootReservationKind.FramebufferMetadata
        ];
        foreach (BootReservationKind kind in imageCoveredKinds)
        {
            fixture.ResetCanonical();
            BootReservedRange metadata = CreateRange(
                kind,
                fixture.ImageBase + BootReservedRange.PageSize,
                1);
            fixture.InsertLedgerEntry(1, metadata);
            M01LoadedImageStackIntegrationResult contained =
                fixture.PrepareCurrent();
            if (!IsReservationFailure(
                    contained,
                    BootReservationTableStatus
                        .DuplicateLoadedImageCoverage))
            {
                return false;
            }
        }

        fixture.ResetCanonical();
        fixture.Ledger[0] = CreateRange(
            BootReservationKind.AllocatorArena,
            fixture.ImageBase,
            CanonicalFixture.ImagePages);
        if (!IsFailure(
                fixture.PrepareCurrent(),
                M01LoadedImageStackStatus.ReservationIdentityRejected,
                M01LoadedImageStackCheckpoint
                    .AllocationEvidenceValidated))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Ledger[1] = CreateRange(
            BootReservationKind.AllocatorArena,
            fixture.CodePhysicalStart,
            1);
        if (!IsFailure(
                fixture.PrepareCurrent(),
                M01LoadedImageStackStatus.ReservationIdentityRejected,
                M01LoadedImageStackCheckpoint
                    .AllocationEvidenceValidated))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Ledger[2] = CreateRange(
            BootReservationKind.AllocatorArena,
            fixture.DataPhysicalStart,
            1);
        if (!IsReservationFailure(
                fixture.PrepareCurrent(),
                BootReservationTableStatus.MissingBootStackGuard))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Ledger[3] = CreateRange(
            BootReservationKind.AllocatorArena,
            fixture.DataPhysicalStart + BootReservedRange.PageSize,
            StackPages);
        if (!IsReservationFailure(
                fixture.PrepareCurrent(),
                BootReservationTableStatus.MissingBootStack))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Ledger[3] = CreateRange(
            BootReservationKind.BootStack,
            fixture.DataPhysicalStart
                + 2 * BootReservedRange.PageSize,
            2);
        if (!IsReservationFailure(
                fixture.PrepareCurrent(),
                BootReservationTableStatus.BootStackArenaGap))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Ledger[2] = CreateRange(
            BootReservationKind.BootStackGuard,
            fixture.DataPhysicalStart + BootReservedRange.PageSize,
            1);
        fixture.Ledger[3] = CreateRange(
            BootReservationKind.BootStack,
            fixture.DataPhysicalStart
                + 2 * BootReservedRange.PageSize,
            StackPages);
        if (!IsFailure(
                fixture.PrepareCurrent(),
                M01LoadedImageStackStatus.ReservationIdentityRejected,
                M01LoadedImageStackCheckpoint
                    .AllocationEvidenceValidated))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Records[1] = WithRecord(
            fixture.Records[1],
            pageCount: ArenaPages + 1,
            byteLength:
                (ArenaPages + 1) * BootMemoryRegionRecord.PageSize,
            physicalEndExclusive:
                fixture.DataPhysicalStart
                + (ArenaPages + 1) * BootMemoryRegionRecord.PageSize);
        if (!IsFailure(
                fixture.PrepareCurrent(),
                M01LoadedImageStackStatus.ReservationIdentityRejected,
                M01LoadedImageStackCheckpoint
                    .AllocationEvidenceValidated))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Ledger[2] = WithUlongAt(
            fixture.Ledger[2],
            32,
            fixture.Ledger[2].ByteLength + 1);
        return IsReservationFailure(
            fixture.PrepareCurrent(),
            BootReservationTableStatus.InvalidReservation);
    }

    private static bool TestStaticAndCodeStorageFailures()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    null,
                    PageSize,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    fixture.Continuation,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes),
                "null-code"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    fixture.CodePage,
                    PageSize - 1,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    fixture.Continuation,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes),
                "short-code-capacity"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    fixture.CodePage,
                    PageSize,
                    PageSize - 1,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    fixture.Continuation,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes),
                "short-materialized-page"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    fixture.CodePage + PageSize,
                    PageSize,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    fixture.Continuation,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes),
                "wrong-code-address"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    fixture.CodePage,
                    PageSize,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    fixture.Continuation,
                    null,
                    M01LoadedImageStackStaticState.RequiredStorageBytes),
                "null-static-state"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    fixture.CodePage,
                    PageSize,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    fixture.Continuation,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes - 1),
                "short-static-capacity"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    fixture.CodePage,
                    PageSize,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    fixture.Continuation,
                    (M01LoadedImageStackStaticState*)
                        ((byte*)fixture.StaticState + 1),
                    M01LoadedImageStackStaticState.RequiredStorageBytes),
                "misaligned-static-state"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    fixture.CodePage,
                    PageSize,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    default,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes),
                "default-continuation"))
        {
            return false;
        }

        fixture.ResetCanonical();
        ((byte*)fixture.StaticState)[0] = 1;
        if (!ExpectStaticFailure(
                fixture.PrepareCurrent(),
                "nonzero-static-state"))
        {
            return false;
        }

        fixture.ResetCanonical();
        VirtualAddress outside = Virtual(
            fixture.ImageBase
            + CanonicalFixture.ImageBytes
            + BootReservedRange.PageSize);
        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    fixture.CodePage,
                    PageSize,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    outside,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes),
                "continuation-outside-image"))
        {
            return false;
        }

        fixture.ResetCanonical();
        VirtualAddress handoffStart =
            Virtual((ulong)fixture.StaticState + 16);
        if (!ExpectStaticFailure(
                fixture.PrepareWithStorage(
                    fixture.CodePage,
                    PageSize,
                    PageSize,
                    StubEntryOffset,
                    OwnedBootStackSwitchPlan.RequiredStubByteLength,
                    handoffStart,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes),
                "continuation-overlaps-handoff"))
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.Records[0] = WithRecord(
            fixture.Records[0],
            virtualStart: fixture.ImageBase);
        fixture.RefreshedMap = WithMap(
            fixture.RefreshedMap,
            bufferAddress: fixture.ImageBase + 6000);
        M01LoadedImageStackIntegrationResult nonidentityCode =
            fixture.PrepareWithStorage(
                (byte*)fixture.ImageBase,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                fixture.Continuation,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        return IsAddressMappingFailure(nonidentityCode)
            || CaseFailure(
                "static nonidentity-code status="
                + nonidentityCode.Status
                + " checkpoint="
                + nonidentityCode.LastCompletedCheckpoint);
    }

    private static bool TestStubFailuresAndVerificationMode()
    {
        using CanonicalFixture fixture = new CanonicalFixture();

        M01LoadedImageStackIntegrationResult defaultMode =
            fixture.PrepareWithMode(M01StageOneStubByteMode.Uninitialized);
        M01LoadedImageStackIntegrationResult unknownMode =
            fixture.PrepareWithMode((M01StageOneStubByteMode)0xff);
        if (!IsFailure(
                defaultMode,
                M01LoadedImageStackStatus.StubByteModeRejected,
                M01LoadedImageStackCheckpoint.Uninitialized)
            || !IsFailure(
                unknownMode,
                M01LoadedImageStackStatus.StubByteModeRejected,
                M01LoadedImageStackCheckpoint.Uninitialized))
        {
            return false;
        }

        (ulong Offset, ulong Length, OwnedBootStackSwitchStatus Leaf)[]
            malformedPlans =
        [
            (
                1,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                OwnedBootStackSwitchStatus.MisalignedStubEntry),
            (
                PageSize,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                OwnedBootStackSwitchStatus.InvalidStubEntryOffset),
            (
                PageSize - 8,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                OwnedBootStackSwitchStatus.StubBytePlanOutOfRange),
            (
                ~0UL,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                OwnedBootStackSwitchStatus.StubEntryOverflow),
            (
                StubEntryOffset,
                0,
                OwnedBootStackSwitchStatus.InvalidDeclaredStubByteLength),
            (
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength - 1,
                OwnedBootStackSwitchStatus.InvalidDeclaredStubByteLength),
            (
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength + 1,
                OwnedBootStackSwitchStatus.InvalidDeclaredStubByteLength)
        ];
        foreach ((ulong offset, ulong length, OwnedBootStackSwitchStatus leaf)
            in malformedPlans)
        {
            fixture.ResetCanonical();
            M01LoadedImageStackIntegrationResult result =
                fixture.PrepareWithStub(offset, length);
            if (!IsFailure(
                    result,
                    M01LoadedImageStackStatus.SwitchPlanRejected,
                    M01LoadedImageStackCheckpoint.StackLayoutReady)
                || result.Evidence.SwitchPlanResult.Status != leaf)
            {
                return false;
            }
        }

        fixture.ResetCanonical();
        fixture.WriteAuditedStub();
        M01LoadedImageStackIntegrationResult verified =
            fixture.PrepareWithMode(M01StageOneStubByteMode.VerifyExisting);
        if (!IsFailure(
                verified,
                M01LoadedImageStackStatus.StubByteModeRejected,
                M01LoadedImageStackCheckpoint.SwitchPlanReady)
            || verified.Evidence.StubByteMode
                != M01StageOneStubByteMode.VerifyExisting
            || verified.Evidence.StubEmission.Status
                != OwnedBootStackSwitchStatus.Uninitialized
            || verified.Evidence.StubVerification.Status
                != OwnedBootStackSwitchStatus.Success
            || verified.Evidence.StubVerification.Operation
                != OwnedBootStackSwitchByteOperation.Verify)
        {
            return false;
        }

        fixture.ResetCanonical();
        fixture.WriteAuditedStub();
        fixture.CodePage[StubEntryOffset + 6] ^= 0xff;
        M01LoadedImageStackIntegrationResult mismatch =
            fixture.PrepareWithMode(M01StageOneStubByteMode.VerifyExisting);
        return IsFailure(
                mismatch,
                M01LoadedImageStackStatus.StubVerificationRejected,
                M01LoadedImageStackCheckpoint.SwitchPlanReady)
            && mismatch.Evidence.StubVerification.Status
                == OwnedBootStackSwitchStatus.BytePlanMismatch
            && mismatch.Evidence.StubVerification.MismatchByteOffset == 6;
    }

    private static bool TestIllegalCoordinatorTransitions()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LoadedImageStackIntegrationResult prepared =
            fixture.PrepareCurrent();
        M01LoadedImageStackIntegrationResult secondPrepare =
            fixture.PrepareCurrent();
        if (!prepared.IsPrepared
            || !IsStaticFailure(secondPrepare))
        {
            return false;
        }

        M01LoadedImageStackIntegrationResult observeBeforeTransfer =
            M01LoadedImageStackIntegration.ObserveContinuationRsp(
                prepared,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                prepared.Evidence.SwitchPlanResult.Plan
                    .ExpectedContinuationStackPointer);
        M01LoadedImageStackIntegrationResult returnBeforeTransfer =
            M01LoadedImageStackIntegration.ReportUnexpectedReturn(
                prepared,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        if (!IsIllegal(observeBeforeTransfer)
            || !IsIllegal(returnBeforeTransfer))
        {
            return false;
        }

        M01LoadedImageStackIntegrationResult transferred =
            M01LoadedImageStackIntegration.RecordTransfer(
                prepared,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        if (!transferred.IsTransferRecorded)
        {
            return false;
        }

        M01LoadedImageStackIntegrationResult repeatedTransfer =
            M01LoadedImageStackIntegration.RecordTransfer(
                transferred,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        M01LoadedImageStackIntegrationResult wrongCapacity =
            M01LoadedImageStackIntegration.ObserveContinuationRsp(
                transferred,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes - 1,
                transferred.Evidence.SwitchPlanResult.Plan
                    .ExpectedContinuationStackPointer);
        M01LoadedImageStackIntegrationResult wrongPointer =
            M01LoadedImageStackIntegration.ReportUnexpectedReturn(
                transferred,
                (M01LoadedImageStackStaticState*)
                    ((byte*)fixture.StaticState + 8),
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        if (!IsIllegal(repeatedTransfer)
            || !IsIllegal(wrongCapacity)
            || !IsIllegal(wrongPointer))
        {
            return false;
        }

        M01LoadedImageStackIntegrationResult observed =
            M01LoadedImageStackIntegration.ObserveContinuationRsp(
                transferred,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                transferred.Evidence.SwitchPlanResult.Plan
                    .ExpectedContinuationStackPointer);
        M01LoadedImageStackIntegrationResult repeatedObservation =
            M01LoadedImageStackIntegration.ObserveContinuationRsp(
                transferred,
                fixture.StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                transferred.Evidence.SwitchPlanResult.Plan
                    .ExpectedContinuationStackPointer);
        return observed.Succeeded && IsIllegal(repeatedObservation);
    }

    private static bool TestRspFailures()
    {
        bool Rejects(
            Func<OwnedBootStackSwitchPlan, VirtualAddress> select,
            OwnedBootStackSwitchStatus leaf)
        {
            using CanonicalFixture fixture = new CanonicalFixture();
            M01LoadedImageStackIntegrationResult prepared =
                fixture.PrepareCurrent();
            M01LoadedImageStackIntegrationResult transferred =
                M01LoadedImageStackIntegration.RecordTransfer(
                    prepared,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes);
            M01LoadedImageStackIntegrationResult result =
                M01LoadedImageStackIntegration.ObserveContinuationRsp(
                    transferred,
                    fixture.StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes,
                    select(transferred.Evidence.SwitchPlanResult.Plan));
            return IsFailure(
                    result,
                    M01LoadedImageStackStatus.ContinuationRspRejected,
                    M01LoadedImageStackCheckpoint.TransferRecorded)
                && result.Evidence.SwitchStateResult.Status == leaf
                && fixture.StaticState->Status
                    == M01LoadedImageStackStatus
                        .ContinuationRspRejected;
        }

        return Rejects(
                _ => default,
                OwnedBootStackSwitchStatus.InvalidObservedRsp)
            && Rejects(
                plan => plan.GuardVirtualStart,
                OwnedBootStackSwitchStatus.ObservedRspInGuard)
            && Rejects(
                plan => Add(plan.UsableStackVirtualEndExclusive, 8),
                OwnedBootStackSwitchStatus
                    .ObservedRspOutsideUsableStack)
            && Rejects(
                plan => Add(
                    plan.ExpectedContinuationStackPointer,
                    1),
                OwnedBootStackSwitchStatus.MisalignedObservedRsp)
            && Rejects(
                plan => Subtract(
                    plan.ExpectedContinuationStackPointer,
                    16),
                OwnedBootStackSwitchStatus.ObservedRspMismatch);
    }

    private static bool TestDeterminismAndPreflightNonMutation()
    {
        using CanonicalFixture fixture = new CanonicalFixture();
        M01LoadedImageStackIntegrationResult first =
            fixture.PrepareCurrent();
        byte[] firstCode = fixture.CopyCodePage();
        byte[] firstState = fixture.CopyStaticState();

        fixture.ResetCanonical();
        M01LoadedImageStackIntegrationResult second =
            fixture.PrepareCurrent();
        if (!StructBytesEqual(first, second)
            || !firstCode.AsSpan().SequenceEqual(fixture.CopyCodePage())
            || !firstState.AsSpan().SequenceEqual(
                fixture.CopyStaticState()))
        {
            return false;
        }

        fixture.ResetCanonical();
        BootMemoryRegionRecord[] recordsBefore =
            fixture.CopyAllocationRecords();
        BootReservedRange[] ledgerBefore = fixture.CopyLedger();
        byte[] codeBefore = fixture.CopyCodePage();
        byte[] stateBefore = fixture.CopyStaticState();

        M01LoadedImageStackIntegrationResult invalidFirst =
            fixture.PrepareWithAllocationTable(fixture.Records, 2, 1);
        M01LoadedImageStackIntegrationResult invalidSecond =
            fixture.PrepareWithAllocationTable(fixture.Records, 2, 1);
        if (!StructBytesEqual(invalidFirst, invalidSecond)
            || !recordsBefore.AsSpan().SequenceEqual(
                fixture.CopyAllocationRecords())
            || !LedgerEqual(ledgerBefore, fixture.CopyLedger())
            || !codeBefore.AsSpan().SequenceEqual(
                fixture.CopyCodePage())
            || !stateBefore.AsSpan().SequenceEqual(
                fixture.CopyStaticState()))
        {
            return false;
        }

        _ = fixture.PrepareWithAllocationTable(fixture.Records, 2, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        M01LoadedImageStackIntegrationResult measured =
            fixture.PrepareWithAllocationTable(fixture.Records, 2, 1);
        long after = GC.GetAllocatedBytesForCurrentThread();
        return after == before
            && IsAllocationTableFailure(measured);
    }

    private static bool
        TestStaticScratchPreparationAndRejectionNonMutation()
    {
        using CanonicalFixture richFixture = new CanonicalFixture();
        M01LoadedImageStackIntegrationResult rich =
            richFixture.PrepareCurrent();
        byte[] richCode = richFixture.CopyCodePage();

        using CanonicalFixture fixture = new CanonicalFixture();
        M01LoadedImageStackStaticPreparationResult compact =
            fixture.PrepareFromStaticScratchCurrent();
        M01LoadedImageStackEvidence compactEvidence =
            *fixture.EvidenceScratch;
        if (!rich.IsPrepared
            || !compact.IsPrepared
            || compact.Status != M01LoadedImageStackStatus.Success
            || compact.Checkpoint
                != M01LoadedImageStackCheckpoint.SwitchArmed
            || !fixture.StaticState->IsInitialized
            || compactEvidence.StaticStateAddress
                != (ulong)fixture.StaticState
            || compactEvidence.StaticStateCapacity
                != M01LoadedImageStackStaticState.RequiredStorageBytes
            || !compactEvidence.SwitchPlanResult.Succeeded
            || !compactEvidence.StubEmission.Succeeded
            || !compactEvidence.StubVerification.Succeeded
            || compactEvidence.StubByteMode
                != M01StageOneStubByteMode.EmitAndVerify
            || compactEvidence.SwitchPlanResult.Plan.StackLayout
                    .StackReservation.PageCount
                != rich.Evidence.SwitchPlanResult.Plan.StackLayout
                    .StackReservation.PageCount
            || !richCode.AsSpan().SequenceEqual(
                fixture.CopyCodePage()))
        {
            return CaseFailure("static-scratch-canonical-success");
        }

        M01LoadedImageStackStaticPreparationResult Invoke(
            M01LoadedImageStackEvidence* scratch,
            ulong scratchCapacity,
            BootMemoryRegionRecord* records = null,
            BootReservedRange* ledger = null,
            byte* codePage = null,
            M01LoadedImageStackStaticState* staticState = null,
            VirtualAddress continuation = default,
            M01StageOneMapLedgerEvidence mapEvidence = default)
        {
            BootMemoryRegionRecord* selectedRecords =
                records == null ? fixture.Records : records;
            BootReservedRange* selectedLedger =
                ledger == null ? fixture.Ledger : ledger;
            byte* selectedCodePage =
                codePage == null ? fixture.CodePage : codePage;
            M01LoadedImageStackStaticState* selectedStaticState =
                staticState == null
                    ? fixture.StaticState
                    : staticState;
            VirtualAddress selectedContinuation =
                continuation.IsValid
                    ? continuation
                    : fixture.Continuation;
            M01StageOneAllocationEvidence allocations =
                fixture.CreateAllocationEvidence();
            M01StageOneMapLedgerEvidence selectedMap =
                mapEvidence.Size == 0
                    ? fixture.CreateMapEvidence(allocations)
                    : mapEvidence;

            return fixture.InvokeStatic(
                fixture.LoadedImage,
                fixture.CreateVirtualMapping(),
                fixture.CreateAddressMapping(),
                selectedRecords,
                2,
                2,
                allocations,
                selectedMap,
                selectedLedger,
                fixture.LedgerCount,
                fixture.LedgerCapacity,
                selectedCodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                selectedContinuation,
                selectedStaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                scratch,
                scratchCapacity);
        }

        bool RejectsWithoutMutation(
            Func<M01LoadedImageStackStaticPreparationResult> action,
            string scenario)
        {
            byte[] before = fixture.CopyBackingMemory();
            M01LoadedImageStackStaticPreparationResult result =
                action();
            if (result.Status
                    != M01LoadedImageStackStatus
                        .EvidenceScratchRejected
                || result.Checkpoint
                    != M01LoadedImageStackCheckpoint.Uninitialized
                || result.IsPrepared
                || !before.AsSpan().SequenceEqual(
                    fixture.CopyBackingMemory()))
            {
                return CaseFailure(
                    "static-scratch-rejection-"
                    + scenario
                    + " status="
                    + result.Status
                    + " checkpoint="
                    + result.Checkpoint);
            }

            return true;
        }

        ulong required =
            M01LoadedImageStackIntegration
                .RequiredEvidenceScratchBytes;

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(null, required),
                "null"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(fixture.EvidenceScratch, 0),
                "zero-capacity"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(fixture.EvidenceScratch, required - 1),
                "undersized"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(fixture.EvidenceScratch, required + 1),
                "oversized"))
        {
            return false;
        }

        fixture.ResetCanonical();
        M01LoadedImageStackEvidence* misaligned =
            (M01LoadedImageStackEvidence*)(
                (byte*)fixture.EvidenceScratch + 1);
        if (!RejectsWithoutMutation(
                () => Invoke(misaligned, required),
                "misaligned"))
        {
            return false;
        }

        fixture.ResetCanonical();
        ((byte*)fixture.EvidenceScratch)[required - 1] = 0xa5;
        if (!RejectsWithoutMutation(
                () => Invoke(fixture.EvidenceScratch, required),
                "nonzero"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(
                    (M01LoadedImageStackEvidence*)fixture.CodePage,
                    required),
                "outside-loaded-image"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(
                    fixture.EvidenceScratch,
                    required,
                    codePage: (byte*)fixture.EvidenceScratch),
                "code-alias"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(
                    fixture.EvidenceScratch,
                    required,
                    staticState:
                        (M01LoadedImageStackStaticState*)
                            fixture.EvidenceScratch),
                "static-state-alias"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(
                    fixture.EvidenceScratch,
                    required,
                    records:
                        (BootMemoryRegionRecord*)
                            fixture.EvidenceScratch),
                "allocation-table-alias"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(
                    fixture.EvidenceScratch,
                    required,
                    ledger:
                        (BootReservedRange*)
                            fixture.EvidenceScratch),
                "reservation-ledger-alias"))
        {
            return false;
        }

        fixture.ResetCanonical();
        M01StageOneAllocationEvidence mapAllocations =
            fixture.CreateAllocationEvidence();
        FirmwareMemoryMapRecord destinationAlias =
            fixture.RefreshedMap;
        destinationAlias.BufferAddress =
            (ulong)fixture.EvidenceScratch;
        destinationAlias.BufferSize = required;
        M01StageOneMapLedgerEvidence destinationAliasEvidence =
            fixture.CreateMapEvidence(
                mapAllocations,
                refreshedMap: destinationAlias);
        if (!RejectsWithoutMutation(
                () => Invoke(
                    fixture.EvidenceScratch,
                    required,
                    mapEvidence: destinationAliasEvidence),
                "owned-map-alias"))
        {
            return false;
        }

        fixture.ResetCanonical();
        mapAllocations = fixture.CreateAllocationEvidence();
        FirmwareMemoryMapRecord sourceAlias = fixture.RefreshedMap;
        sourceAlias.SourceBufferAddress =
            (ulong)fixture.EvidenceScratch;
        sourceAlias.SourceBufferSize = required;
        M01StageOneMapLedgerEvidence sourceAliasEvidence =
            fixture.CreateMapEvidence(
                mapAllocations,
                refreshedMap: sourceAlias);
        if (!RejectsWithoutMutation(
                () => Invoke(
                    fixture.EvidenceScratch,
                    required,
                    mapEvidence: sourceAliasEvidence),
                "source-map-alias"))
        {
            return false;
        }

        fixture.ResetCanonical();
        if (!RejectsWithoutMutation(
                () => Invoke(
                    fixture.EvidenceScratch,
                    required,
                    continuation: Virtual(
                        (ulong)fixture.EvidenceScratch)),
                "continuation-alias"))
        {
            return false;
        }

        fixture.ResetCanonical();
        ulong dataAliasStart =
            fixture.ImageBase + BootReservedRange.PageSize;
        ulong relocatedCodeStart =
            fixture.ImageBase
            + 14 * BootReservedRange.PageSize;
        fixture.Records[0] = new BootMemoryRegionRecord(
            (uint)UefiMemoryType.LoaderCode,
            relocatedCodeStart,
            relocatedCodeStart,
            1,
            0,
            BootMemoryRegionRecord.PageSize,
            relocatedCodeStart
                + BootMemoryRegionRecord.PageSize);
        fixture.Records[1] = new BootMemoryRegionRecord(
            (uint)UefiMemoryType.LoaderData,
            dataAliasStart,
            dataAliasStart,
            ArenaPages,
            0,
            ArenaPages * BootMemoryRegionRecord.PageSize,
            dataAliasStart
                + ArenaPages * BootMemoryRegionRecord.PageSize);
        M01LoadedImageStackEvidence* dataScratch =
            (M01LoadedImageStackEvidence*)dataAliasStart;
        if (!RejectsWithoutMutation(
                () => Invoke(
                    dataScratch,
                    required,
                    continuation: Virtual(fixture.ImageBase + 64)),
                "data-allocation-alias"))
        {
            return false;
        }

        fixture.ResetCanonical();
        ulong ledgerRecordSize =
            (ulong)Unsafe.SizeOf<BootReservedRange>();
        ulong countLength =
            fixture.LedgerCount * ledgerRecordSize;
        ulong nearMaximumLedgerAddress =
            (~0UL - countLength)
            & ~(M01LoadedImageStackIntegration
                .RequiredEvidenceScratchAlignment - 1);
        byte[] overflowBefore = fixture.CopyBackingMemory();
        M01LoadedImageStackStaticPreparationResult ledgerOverflow =
            Invoke(
                fixture.EvidenceScratch,
                required,
                ledger:
                    (BootReservedRange*)nearMaximumLedgerAddress);
        if (ledgerOverflow.Status
                != M01LoadedImageStackStatus
                    .MapLedgerEvidenceRejected
            || ledgerOverflow.Checkpoint
                != M01LoadedImageStackCheckpoint.Uninitialized
            || ledgerOverflow.IsPrepared
            || !overflowBefore.AsSpan().SequenceEqual(
                fixture.CopyBackingMemory()))
        {
            return CaseFailure(
                "static-scratch-ledger-capacity-overflow");
        }

        return true;
    }

    private static bool TestSharedLowStackCoreSourceTopology()
    {
        string source = RemoveComments(
            File.ReadAllText(
                IntegrationSourcePath(
                    HostToolProcess.FindRepositoryRoot())));
        if (!ValidateEvidenceScratchSourceContract(source))
        {
            return CaseFailure(
                "evidence-scratch-source-contract");
        }

        Regex overflowGuard = new(
            @"scratchAddress\s*>\s*"
                + @"MaximumUnsignedLong\s*-\s*requiredBytes",
            RegexOptions.CultureInvariant);
        string overflowMutated =
            overflowGuard.Replace(
                source,
                "false",
                1);
        if (string.Equals(
                overflowMutated,
                source,
                StringComparison.Ordinal)
            || ValidateEvidenceScratchSourceContract(
                overflowMutated))
        {
            return CaseFailure(
                "evidence-scratch-overflow-mutation");
        }

        const string coreName =
            "PrepareWithCallerOwnedEvidenceScratch";
        string? richBody = FindStaticMethodBody(
            source,
            "PrepareWithStubByteMode");
        string? staticBody = FindStaticMethodBody(
            source,
            "PrepareFromStaticScratch");
        string? coreBody = FindStaticMethodBody(
            source,
            coreName);
        if (richBody is null
            || staticBody is null
            || coreBody is null)
        {
            return CaseFailure("shared-low-stack-core-method");
        }

        int declarationCount = Regex.Matches(
            source,
            @"\bprivate\s+static\s+"
                + @"M01LoadedImageStackStaticPreparationResult\s+"
                + coreName
                + @"\s*\(",
            RegexOptions.CultureInvariant).Count;
        int totalTokenCount = Regex.Matches(
            source,
            @"\b" + coreName + @"\s*\(",
            RegexOptions.CultureInvariant).Count;
        int richCallCount = Regex.Matches(
            richBody,
            @"\b" + coreName + @"\s*\(",
            RegexOptions.CultureInvariant).Count;
        int staticCallCount = Regex.Matches(
            staticBody,
            @"\b" + coreName + @"\s*\(",
            RegexOptions.CultureInvariant).Count;
        int recursiveCallCount = Regex.Matches(
            coreBody,
            @"\b" + coreName + @"\s*\(",
            RegexOptions.CultureInvariant).Count;

        return declarationCount == 1
            && totalTokenCount == 3
            && richCallCount == 1
            && staticCallCount == 1
            && recursiveCallCount == 0
            || CaseFailure("shared-low-stack-core-topology");
    }

    private static bool
        ValidateEvidenceScratchSourceContract(string source)
    {
        string? body = FindStaticMethodBody(
            source,
            "ValidateEvidenceScratch");
        if (body is null)
        {
            return false;
        }

        const string expected =
            "ulongrequiredBytes=(ulong)sizeof("
            + "M01LoadedImageStackEvidence);"
            + "ulongscratchAddress=(ulong)evidenceScratch;"
            + "if(evidenceScratch==null"
            + "||evidenceScratchCapacity!=requiredBytes"
            + "||(scratchAddress"
            + "&(RequiredEvidenceScratchAlignment-1))!=0"
            + "||scratchAddress"
            + ">MaximumUnsignedLong-requiredBytes)"
            + "{returnfalse;}"
            + "if(requireEvidenceScratchInLoadedImage"
            + "&&(loadedImageMapping.VirtualImageBase"
            + ".TryGetRawValueForUefiBoundary(outulongimageBase)"
            + "!=AddressValueStatus.Success"
            + "||loadedImageMapping.VirtualImageEndExclusive"
            + ".TryGetRawValueForUefiBoundary(outulongimageEnd)"
            + "!=AddressValueStatus.Success"
            + "||scratchAddress<imageBase"
            + "||scratchAddress+requiredBytes>imageEnd))"
            + "{returnfalse;}"
            + "returnIsZeroed((byte*)evidenceScratch,"
            + "requiredBytes);";
        return string.Equals(
            Regex.Replace(
                body,
                @"\s+",
                string.Empty,
                RegexOptions.CultureInvariant),
            expected,
            StringComparison.Ordinal);
    }

    private static bool TestSourceAndApiFirewalls()
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
            @"\bFreePages\b",
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

        if (!HasExactHostConveniencePreprocessorBoundary(stripped))
        {
            return SourceFailure(
                "nostdlib-host-convenience-boundary");
        }

        if (Regex.IsMatch(
                stripped,
                "delegate" + @"\s*\*",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                stripped,
                @"\bcall" + "i" + @"\b",
                RegexOptions.CultureInvariant)
            || !stripped.Contains(
                "public const bool CallsFirmwarePageAllocator = false",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "public const bool InvokesFirmware = false",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "public const bool ExecutesStackSwitch = false",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "public const bool IsGuestEvidence = false",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "public const bool MakesGuardInaccessible = false",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "public const bool EnforcesWriteXorExecute = false",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "public const bool HostAuditResultMayCrossStackSwitch = false",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "public const bool StaticTransitionRequiresHostAuditResult = false",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "PrepareWithStubByteMode(",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "PrepareFromStaticScratch(",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "M01StageOneStubByteMode.VerifyExisting",
                StringComparison.Ordinal)
            || !stripped.Contains(
                "_previousMapKey != _refreshedMap.MapKey",
                StringComparison.Ordinal)
            || stripped.Contains(
                "_previousMapKey < _refreshedMap.MapKey",
                StringComparison.Ordinal)
            || stripped.Contains(
                "_previousMapKey > _refreshedMap.MapKey",
                StringComparison.Ordinal))
        {
            return SourceFailure("required-contract");
        }

        Type[] podTypes =
        [
            typeof(M01StageOneAllocationEvidence),
            typeof(M01StageOneMapLedgerEvidence),
            typeof(M01LoadedImageVirtualMappingEvidence),
            typeof(M01StageOneAddressMappingEvidence),
            typeof(M01LoadedImageStackStaticState),
            typeof(M01LoadedImageStackTransferTarget),
            typeof(M01LoadedImageStackStaticPreparationResult),
            typeof(M01LoadedImageStackStaticTransitionResult),
            typeof(M01LoadedImageStackEvidence),
            typeof(M01LoadedImageStackIntegrationResult)
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

        foreach (MethodInfo method in
            typeof(M01LoadedImageStackIntegration).GetMethods(
                BindingFlags.Public
                    | BindingFlags.Static
                    | BindingFlags.DeclaredOnly))
        {
            if (!IsPodOrPointer(method.ReturnType))
            {
                return SourceFailure(
                    "managed-return=" + method.Name);
            }

            foreach (ParameterInfo parameter in method.GetParameters())
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
                @"..\OS.Kernel\Boot\M01LoadedImageStackIntegration.cs",
                @"Boot\M01LoadedImageStackIntegration.cs")
            || efiProject.Contains(
                "BootSequence.cs",
                StringComparison.Ordinal)
            || Regex.IsMatch(
                efiProject,
                @"<Compile\s+Include\s*=\s*""[^""]*[*?]",
                RegexOptions.CultureInvariant))
        {
            return SourceFailure("efi-project-linkage");
        }

        string efiEntry = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "Boot",
                "EfiEntry.cs"));
        const string probeStart =
            "#if M01_LOADED_IMAGE_STACK_PROBE";
        const string probeElse = "#else";
        const string probeEnd = "#endif";
        int probeStartIndex = efiEntry.IndexOf(
            probeStart,
            StringComparison.Ordinal);
        int probeElseIndex = probeStartIndex < 0
            ? -1
            : efiEntry.IndexOf(
                probeElse,
                probeStartIndex + probeStart.Length,
                StringComparison.Ordinal);
        int probeEndIndex = probeElseIndex < 0
            ? -1
            : efiEntry.IndexOf(
                probeEnd,
                probeElseIndex + probeElse.Length,
                StringComparison.Ordinal);
        int duplicateProbeIndex = probeStartIndex < 0
            ? -1
            : efiEntry.IndexOf(
                probeStart,
                probeStartIndex + probeStart.Length,
                StringComparison.Ordinal);
        if (probeStartIndex < 0
            || probeElseIndex < 0
            || probeEndIndex < 0
            || duplicateProbeIndex >= 0)
        {
            return SourceFailure("efi-entry-probe-boundary");
        }

        string probeImplementation = efiEntry[
            probeStartIndex..probeElseIndex];
        string defaultImplementation = efiEntry[
            probeElseIndex..(probeEndIndex + probeEnd.Length)];
        string outsideProbe = string.Concat(
            efiEntry.AsSpan(0, probeStartIndex),
            efiEntry.AsSpan(probeEndIndex + probeEnd.Length));
        string compactProbe = Regex.Replace(
            probeImplementation,
            @"\s+",
            string.Empty,
            RegexOptions.CultureInvariant);
        string compactDefault = Regex.Replace(
            defaultImplementation,
            @"\s+",
            string.Empty,
            RegexOptions.CultureInvariant);
        string compactOutside = Regex.Replace(
            outsideProbe,
            @"\s+",
            string.Empty,
            RegexOptions.CultureInvariant);
        string[] requiredProbeCalls =
        [
            "M01LoadedImageStackIntegration.PrepareFromStaticScratch(",
            "RecordTransferFromStaticState",
            "ObserveContinuationRspFromStaticState",
            "ReportUnexpectedReturnFromStaticState"
        ];
        foreach (string requiredProbeCall in requiredProbeCalls)
        {
            if (!compactProbe.Contains(
                    requiredProbeCall,
                    StringComparison.Ordinal)
                || compactOutside.Contains(
                    requiredProbeCall,
                    StringComparison.Ordinal)
                || compactDefault.Contains(
                    requiredProbeCall,
                    StringComparison.Ordinal))
            {
                return SourceFailure(
                    "efi-entry-probe-call=" + requiredProbeCall);
            }
        }

        if (compactProbe.Contains(
                "M01LoadedImageStackIntegration.Prepare(",
                StringComparison.Ordinal)
            || compactProbe.Contains(
                "M01LoadedImageStackIntegration.PrepareWithStubByteMode(",
                StringComparison.Ordinal)
            || !defaultImplementation.Contains(
                "return false;",
                StringComparison.Ordinal)
            || efiEntry.Contains(
                "RunM01LoadedImageStackIntegration",
                StringComparison.Ordinal)
            || efiEntry.Contains(
                "RecordTransfer(",
                StringComparison.Ordinal)
            || efiEntry.Contains(
                "ObserveContinuationRsp(",
                StringComparison.Ordinal))
        {
            return SourceFailure("efi-entry-linkage");
        }

        string expectedMarkerSource =
            Path.GetFullPath(TestSourcePath(repositoryRoot));
        List<string> markerSources = [];
        foreach (string candidate in Directory.EnumerateFiles(
            repositoryRoot,
            "*.cs",
            SearchOption.AllDirectories))
        {
            string fullCandidate = Path.GetFullPath(candidate);
            if (IsGeneratedOrReferencePath(fullCandidate)
                || !File.ReadAllText(fullCandidate).Contains(
                    "CLROS_M01_017_READY",
                    StringComparison.Ordinal))
            {
                continue;
            }

            markerSources.Add(fullCandidate);
        }

        string testSource = File.ReadAllText(expectedMarkerSource);
        return markerSources.Count == 1
            && string.Equals(
                markerSources[0],
                expectedMarkerSource,
                StringComparison.OrdinalIgnoreCase)
            && Regex.Matches(
                testSource,
                "Console\\.WriteLine\\(\"CLROS_M01_017_READY\"\\)",
                RegexOptions.CultureInvariant).Count == 1
            && testSource.IndexOf(
                "Console.WriteLine(\"TEST_START \" + IssueTestName)",
                StringComparison.Ordinal)
                < testSource.IndexOf(
                    "Console.WriteLine(\"CLROS_M01_017_READY\")",
                    StringComparison.Ordinal)
            && testSource.IndexOf(
                "Console.WriteLine(\"CLROS_M01_017_READY\")",
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
                "CLROS.M01.LoadedImageStackCompileClosure.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || ContainsInfrastructureFailure(result.Output)
                || !HasNoExternalMetadata(outputAssembly)
                || !HasExactNoStdLibIntegrationSurface(
                    outputAssembly))
            {
                Console.WriteLine(
                    "M01_017_NOSTDLIB_COMPILE_DIAGNOSTIC "
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
                || Convert.ToByte(values[index]) != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFailure(
        M01LoadedImageStackIntegrationResult result,
        M01LoadedImageStackStatus status,
        M01LoadedImageStackCheckpoint checkpoint)
    {
        return result.Status == status
            && result.LastCompletedCheckpoint == checkpoint
            && !result.IsPrepared
            && !result.IsTransferRecorded
            && !result.Succeeded;
    }

    private static bool IsAllocationTableFailure(
        M01LoadedImageStackIntegrationResult result)
    {
        return IsFailure(
            result,
            M01LoadedImageStackStatus.AllocationTableRejected,
            M01LoadedImageStackCheckpoint.LoadedImageValidated);
    }

    private static bool IsReservationFailure(
        M01LoadedImageStackIntegrationResult result,
        BootReservationTableStatus leaf)
    {
        return IsFailure(
                result,
                M01LoadedImageStackStatus.ReservationLedgerRejected,
                M01LoadedImageStackCheckpoint
                    .AllocationEvidenceValidated)
            && result.Evidence.ReservationValidation.Status == leaf;
    }

    private static bool IsAddressMappingFailure(
        M01LoadedImageStackIntegrationResult result)
    {
        return IsFailure(
            result,
            M01LoadedImageStackStatus.StageOneAddressMappingRejected,
            M01LoadedImageStackCheckpoint.AllocationEvidenceValidated);
    }

    private static bool IsStaticFailure(
        M01LoadedImageStackIntegrationResult result)
    {
        return IsFailure(
            result,
            M01LoadedImageStackStatus.StaticStateRejected,
            M01LoadedImageStackCheckpoint.StackLayoutReady);
    }

    private static bool ExpectStaticFailure(
        M01LoadedImageStackIntegrationResult result,
        string subcase)
    {
        if (IsStaticFailure(result))
        {
            return true;
        }

        return CaseFailure(
            "static subcase="
            + subcase
            + " status="
            + result.Status
            + " checkpoint="
            + result.LastCompletedCheckpoint);
    }

    private static bool IsIllegal(
        M01LoadedImageStackIntegrationResult result)
    {
        return result.Status
                == M01LoadedImageStackStatus.IllegalCoordinatorState
            && !result.IsPrepared
            && !result.IsTransferRecorded
            && !result.Succeeded;
    }

    private static bool StubBytesMatch(CanonicalFixture fixture)
    {
        byte[] expected =
        [
            OwnedBootStackSwitchPlan.StubByte00,
            OwnedBootStackSwitchPlan.StubByte01,
            OwnedBootStackSwitchPlan.StubByte02,
            OwnedBootStackSwitchPlan.StubByte03,
            OwnedBootStackSwitchPlan.StubByte04,
            OwnedBootStackSwitchPlan.StubByte05,
            OwnedBootStackSwitchPlan.StubByte06,
            OwnedBootStackSwitchPlan.StubByte07,
            OwnedBootStackSwitchPlan.StubByte08,
            OwnedBootStackSwitchPlan.StubByte09,
            OwnedBootStackSwitchPlan.StubByte10,
            OwnedBootStackSwitchPlan.StubByte11,
            OwnedBootStackSwitchPlan.StubByte12,
            OwnedBootStackSwitchPlan.StubByte13
        ];
        for (int index = 0; index < expected.Length; index++)
        {
            if (fixture.CodePage[StubEntryOffset + (ulong)index]
                != expected[index])
            {
                return false;
            }
        }

        return true;
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
                "Failed to create an M01-017 test reservation.");
        }

        return range;
    }

    private static VirtualAddress Virtual(ulong raw)
    {
        if (VirtualAddress.TryCreateFromUefiBoundary(
                raw,
                out VirtualAddress address)
                != AddressValueStatus.Success)
        {
            return default;
        }

        return address;
    }

    private static ulong Raw(VirtualAddress address)
    {
        return address.TryGetRawValueForUefiBoundary(out ulong raw)
                == AddressValueStatus.Success
            ? raw
            : 0;
    }

    private static VirtualAddress Add(
        VirtualAddress address,
        ulong bytes)
    {
        return address.TryAdd(bytes, out VirtualAddress result)
                == AddressValueStatus.Success
            ? result
            : default;
    }

    private static VirtualAddress Subtract(
        VirtualAddress address,
        ulong bytes)
    {
        if (address.TryGetRawValueForUefiBoundary(out ulong raw)
                != AddressValueStatus.Success
            || raw < bytes)
        {
            return default;
        }

        return Virtual(raw - bytes);
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
            "M01_017_SOURCE_FIREWALL_DIAGNOSTIC " + reason);
        return false;
    }

    private static bool
        HasExactHostConveniencePreprocessorBoundary(
            string source)
    {
        MatchCollection guardedStarts = Regex.Matches(
            source,
            @"(?m)^[ \t]*#if[ \t]+!NOSTDLIB_EFI[ \t]*\r?$",
            RegexOptions.CultureInvariant);
        MatchCollection guardedEnds = Regex.Matches(
            source,
            @"(?m)^[ \t]*#endif[ \t]*\r?$",
            RegexOptions.CultureInvariant);
        Match hostRecord = Regex.Match(
            source,
            @"public\s+static\s+M01LoadedImageStackIntegrationResult\s+RecordTransfer\s*\(",
            RegexOptions.CultureInvariant);
        Match hostObserve = Regex.Match(
            source,
            @"public\s+static\s+M01LoadedImageStackIntegrationResult\s+ObserveContinuationRsp\s*\(",
            RegexOptions.CultureInvariant);
        Match hostUnexpectedReturn = Regex.Match(
            source,
            @"public\s+static\s+M01LoadedImageStackIntegrationResult\s+ReportUnexpectedReturn\s*\(",
            RegexOptions.CultureInvariant);
        Match staticRecord = Regex.Match(
            source,
            @"public\s+static\s+M01LoadedImageStackStaticTransitionResult\s+RecordTransferFromStaticState\s*\(",
            RegexOptions.CultureInvariant);
        Match staticObserve = Regex.Match(
            source,
            @"public\s+static\s+M01LoadedImageStackStaticTransitionResult\s+ObserveContinuationRspFromStaticState\s*\(",
            RegexOptions.CultureInvariant);
        Match staticUnexpectedReturn = Regex.Match(
            source,
            @"public\s+static\s+M01LoadedImageStackStaticTransitionResult\s+ReportUnexpectedReturnFromStaticState\s*\(",
            RegexOptions.CultureInvariant);
        Match stateMatcher = Regex.Match(
            source,
            @"private\s+static\s+bool\s+StaticStateMatches\s*\(",
            RegexOptions.CultureInvariant);

        if (guardedStarts.Count != 2
            || guardedEnds.Count != 2
            || !hostRecord.Success
            || !hostObserve.Success
            || !hostUnexpectedReturn.Success
            || !staticRecord.Success
            || !staticObserve.Success
            || !staticUnexpectedReturn.Success
            || !stateMatcher.Success)
        {
            return false;
        }

        int firstStart = guardedStarts[0].Index;
        int firstEnd = guardedEnds[0].Index;
        int secondStart = guardedStarts[1].Index;
        int secondEnd = guardedEnds[1].Index;
        return staticRecord.Index < firstStart
            && staticObserve.Index < firstStart
            && staticUnexpectedReturn.Index < firstStart
            && firstStart < hostRecord.Index
            && hostRecord.Index < firstEnd
            && firstStart < hostObserve.Index
            && hostObserve.Index < firstEnd
            && firstStart < hostUnexpectedReturn.Index
            && hostUnexpectedReturn.Index < firstEnd
            && firstEnd < secondStart
            && secondStart < stateMatcher.Index
            && stateMatcher.Index < secondEnd;
    }

    private static bool CaseFailure(string reason)
    {
        Console.WriteLine(
            "M01_017_CASE_DIAGNOSTIC " + reason);
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

    private static string? FindStaticMethodBody(
        string source,
        string methodName)
    {
        Match declaration = Regex.Match(
            source,
            @"\b(?:private|public|internal)\s+static\s+"
                + @"(?:unsafe\s+)?"
                + @"[A-Za-z_][A-Za-z_0-9.]*\s*\*?\s+"
                + Regex.Escape(methodName)
                + @"\s*\([^;{}]*\)\s*\{",
            RegexOptions.CultureInvariant);
        if (!declaration.Success)
        {
            return null;
        }

        int open = source.IndexOf(
            '{',
            declaration.Index + declaration.Length - 1);
        if (open < 0)
        {
            return null;
        }

        int depth = 0;
        for (int index = open; index < source.Length; index++)
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
                    return source[(open + 1)..index];
                }
            }
        }

        return null;
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
            "M01LoadedImageStackIntegration.cs");
    }

    private static string TestSourcePath(string root)
    {
        return Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "M01LoadedImageStackIntegrationTests.cs");
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-017-" + Guid.NewGuid().ToString("N"));
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
                    public sealed class IsReadOnlyAttribute : System.Attribute { }
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

                internal static unsafe class M01LoadedImageStackProbe
                {
                    public static M01LoadedImageStackStatus Exercise(
                        BootMemoryRegionRecord* records,
                        BootReservedRange* ledger,
                        byte* codePage,
                        M01LoadedImageStackStaticState* staticState)
                    {
                        M01StageOneAllocationEvidence allocations =
                            M01StageOneAllocationEvidence
                                .CaptureCallerAttested(
                                    records,
                                    2UL,
                                    2UL,
                                    1UL,
                                    2UL,
                                    0UL,
                                    0UL);
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
                        M01StageOneMapLedgerEvidence mapLedger =
                            M01StageOneMapLedgerEvidence
                                .CaptureCallerAttested(
                                    1UL,
                                    1UL,
                                    map,
                                    2UL,
                                    allocations,
                                    3UL,
                                    4UL,
                                    ledger,
                                    4UL,
                                    4UL);
                        _ = M01LoadedImageStackIntegration.Prepare(
                                default,
                                M01LoadedImageVirtualMappingEvidence
                                    .CaptureCallerAttestedIdentity(
                                        default),
                                M01StageOneAddressMappingEvidence
                                    .CaptureCallerAttestedIdentity(
                                        records,
                                        2UL,
                                        2UL,
                                        map),
                                records,
                                2UL,
                                2UL,
                                allocations,
                                mapLedger,
                                ledger,
                                4UL,
                                4UL,
                                codePage,
                                4096UL,
                                4096UL,
                                16UL,
                                OwnedBootStackSwitchPlan
                                    .RequiredStubByteLength,
                                default,
                                staticState,
                                M01LoadedImageStackStaticState
                                    .RequiredStorageBytes);
                        M01LoadedImageStackIntegrationResult audited =
                            M01LoadedImageStackIntegration
                                .PrepareWithStubByteMode(
                                    default,
                                    M01LoadedImageVirtualMappingEvidence
                                        .CaptureCallerAttestedIdentity(
                                            default),
                                    M01StageOneAddressMappingEvidence
                                        .CaptureCallerAttestedIdentity(
                                            records,
                                            2UL,
                                            2UL,
                                            map),
                                    records,
                                    2UL,
                                    2UL,
                                    allocations,
                                    mapLedger,
                                    ledger,
                                    4UL,
                                    4UL,
                                    codePage,
                                    4096UL,
                                    4096UL,
                                    16UL,
                                    OwnedBootStackSwitchPlan
                                        .RequiredStubByteLength,
                                    default,
                                    staticState,
                                    M01LoadedImageStackStaticState
                                        .RequiredStorageBytes,
                                    M01StageOneStubByteMode
                                        .VerifyExisting);
                        _ = M01LoadedImageStackIntegration
                            .RecordTransferFromStaticState(
                                staticState,
                                M01LoadedImageStackStaticState
                                    .RequiredStorageBytes);
                        _ = M01LoadedImageStackIntegration
                            .ObserveContinuationRspFromStaticState(
                                staticState,
                                M01LoadedImageStackStaticState
                                    .RequiredStorageBytes,
                                default);
                        _ = M01LoadedImageStackIntegration
                            .ReportUnexpectedReturnFromStaticState(
                                staticState,
                                M01LoadedImageStackStaticState
                                    .RequiredStorageBytes);
                        return audited.Status;
                    }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.LoadedImageStackCompileClosure</AssemblyName>
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
                <Compile Include="__LOADED_IMAGE_RANGE__" Link="Boot\EfiLoadedImageRange.cs" />
                <Compile Include="__LOADED_IMAGE_PLAN__" Link="Boot\EfiLoadedImageRangeCapturePlan.cs" />
                <Compile Include="__BOOT_HANDOFF__" Link="Boot\BootHandoffRecord.cs" />
                <Compile Include="__STACK_LAYOUT__" Link="Boot\OwnedBootStackLayout.cs" />
                <Compile Include="__UEFI_MEMORY_TYPE__" Link="Boot\UefiMemoryType.cs" />
                <Compile Include="__REGION_RECORD__" Link="Boot\BootMemoryRegionMaterializer.cs" />
                <Compile Include="__SWITCH_PLAN__" Link="Boot\OwnedBootStackSwitchPlan.cs" />
                <Compile Include="__INTEGRATION__" Link="Boot\M01LoadedImageStackIntegration.cs" />
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
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Boot",
                    "TypedPhysicalVirtualDeviceAddressPODWrappers.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__BOOT_RANGE__",
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Memory",
                    "BootReservedRange.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__TABLE_VALIDATOR__",
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Memory",
                    "BootReservationTableValidator.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__LOADED_IMAGE_RANGE__",
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Boot",
                    "EfiLoadedImageRange.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__LOADED_IMAGE_PLAN__",
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Boot",
                    "EfiLoadedImageRangeCapturePlan.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__BOOT_HANDOFF__",
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Boot",
                    "BootHandoffRecord.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__STACK_LAYOUT__",
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Boot",
                    "OwnedBootStackLayout.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__UEFI_MEMORY_TYPE__",
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Boot",
                    "UefiMemoryType.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__REGION_RECORD__",
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Boot",
                    "BootMemoryRegionMaterializer.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__SWITCH_PLAN__",
                EscapeXml(Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS.Kernel",
                    "Boot",
                    "OwnedBootStackSwitchPlan.cs")),
                StringComparison.Ordinal)
            .Replace(
                "__INTEGRATION__",
                EscapeXml(IntegrationSourcePath(repositoryRoot)),
                StringComparison.Ordinal);

            File.WriteAllText(
                Path.Combine(workspace, "M01.017.NoStdLib.csproj"),
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
        start.ArgumentList.Add("M01.017.NoStdLib.csproj");
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
                    nameof(M01LoadedImageStackIntegration),
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

        bool prepare = false;
        bool prepareWithStubByteMode = false;
        bool prepareFromStaticScratch = false;
        bool staticRecord = false;
        bool staticObserve = false;
        bool staticUnexpectedReturn = false;
        bool hostRecord = false;
        bool hostObserve = false;
        bool hostUnexpectedReturn = false;
        bool hostStateMatcher = false;
        foreach (MethodDefinitionHandle handle in metadata
            .GetTypeDefinition(integrationType).GetMethods())
        {
            string name = metadata.GetString(
                metadata.GetMethodDefinition(handle).Name);
            prepare |= string.Equals(
                name,
                "Prepare",
                StringComparison.Ordinal);
            prepareWithStubByteMode |= string.Equals(
                name,
                "PrepareWithStubByteMode",
                StringComparison.Ordinal);
            prepareFromStaticScratch |= string.Equals(
                name,
                "PrepareFromStaticScratch",
                StringComparison.Ordinal);
            staticRecord |= string.Equals(
                name,
                "RecordTransferFromStaticState",
                StringComparison.Ordinal);
            staticObserve |= string.Equals(
                name,
                "ObserveContinuationRspFromStaticState",
                StringComparison.Ordinal);
            staticUnexpectedReturn |= string.Equals(
                name,
                "ReportUnexpectedReturnFromStaticState",
                StringComparison.Ordinal);
            hostRecord |= string.Equals(
                name,
                "RecordTransfer",
                StringComparison.Ordinal);
            hostObserve |= string.Equals(
                name,
                "ObserveContinuationRsp",
                StringComparison.Ordinal);
            hostUnexpectedReturn |= string.Equals(
                name,
                "ReportUnexpectedReturn",
                StringComparison.Ordinal);
            hostStateMatcher |= string.Equals(
                name,
                "StaticStateMatches",
                StringComparison.Ordinal);
        }

        return prepare
            && prepareWithStubByteMode
            && prepareFromStaticScratch
            && staticRecord
            && staticObserve
            && staticUnexpectedReturn
            && !hostRecord
            && !hostObserve
            && !hostUnexpectedReturn
            && !hostStateMatcher;
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
                @"\Aclros-m01-017-[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-017 workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        if ((File.GetAttributes(fullWorkspace)
                & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Refusing to delete a reparse-point M01-017 workspace.");
        }

        Directory.Delete(fullWorkspace, recursive: true);
    }

    private sealed class M01TestSerialSink : ISerialSink
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

    private sealed class CanonicalFixture : IDisposable
    {
        public const ulong ImagePages = 3;
        public const ulong ImageBytes =
            ImagePages * BootReservedRange.PageSize;
        private const ulong BackingPages = 24;
        private const ulong BackingBytes =
            BackingPages * BootReservedRange.PageSize;
        private const ulong LedgerSlotCapacity = 8;
        private const ulong StaticStateOffset = 128;
        private const ulong EvidenceScratchOffset = 1024;
        private const ulong ContinuationOffset =
            BootReservedRange.PageSize + 256;
        private const ulong CodePageOffset =
            4 * BootReservedRange.PageSize;
        private const ulong DataArenaOffset =
            6 * BootReservedRange.PageSize;
        private const ulong AllocationTableOffset =
            12 * BootReservedRange.PageSize;
        private const ulong AlternateOwnedMapOffset =
            13 * BootReservedRange.PageSize;
        private const ulong LedgerOffset =
            17 * BootReservedRange.PageSize;
        private const ulong MapSourceOffset =
            22 * BootReservedRange.PageSize;

        private byte* _backingMemory;
        private byte* _imageMemory;
        private byte* _dataArena;

        public CanonicalFixture()
        {
            try
            {
                _backingMemory = (byte*)NativeMemory.AlignedAlloc(
                    (nuint)BackingBytes,
                    (nuint)PageSize);
                if (_backingMemory == null)
                {
                    throw new OutOfMemoryException(
                        "Unable to allocate the M01-017 test fixture.");
                }

                _imageMemory = _backingMemory;
                CodePage = _backingMemory + CodePageOffset;
                _dataArena = _backingMemory + DataArenaOffset;
                Records = (BootMemoryRegionRecord*)(
                    _backingMemory + AllocationTableOffset);
                Ledger = (BootReservedRange*)(
                    _backingMemory + LedgerOffset);
                StaticState = (M01LoadedImageStackStaticState*)(
                    _imageMemory + StaticStateOffset);
                EvidenceScratch = (M01LoadedImageStackEvidence*)(
                    _imageMemory + EvidenceScratchOffset);
                ResetCanonical();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public byte* CodePage { get; private set; }

        public BootMemoryRegionRecord* Records { get; private set; }

        public BootReservedRange* Ledger { get; private set; }

        public M01LoadedImageStackStaticState* StaticState
        {
            get;
            private set;
        }

        public M01LoadedImageStackEvidence* EvidenceScratch
        {
            get;
            private set;
        }

        public ulong ImageBase => (ulong)_imageMemory;

        public ulong AlternateOwnedMapAddress =>
            (ulong)(_backingMemory + AlternateOwnedMapOffset);

        public ulong CodePhysicalStart { get; private set; }

        public ulong DataPhysicalStart { get; private set; }

        public ulong DataVirtualStart { get; private set; }

        public ulong LedgerCount { get; private set; }

        public ulong LedgerCapacity => LedgerSlotCapacity;

        public EfiLoadedImageRangeCapturePlan LoadedImage
        {
            get;
            private set;
        }

        public VirtualAddress Continuation { get; private set; }

        public FirmwareMemoryMapRecord RefreshedMap
        {
            get;
            set;
        }

        public void ResetCanonical()
        {
            Clear(_backingMemory, BackingBytes);

            EfiLoadedImageProtocolObservation observation =
                EfiLoadedImageProtocolObservation
                    .CaptureFromUefiBoundary(
                        0,
                        1,
                        ImageBase,
                        ImageBase,
                        ImageBytes);
            LoadedImage = EfiLoadedImageRangeCapturePlan.Create(
                &observation,
                1,
                1,
                EfiLoadedImageRange.PageSize);
            if (!LoadedImage.Succeeded)
            {
                throw new InvalidOperationException(
                    "Canonical loaded-image capture failed.");
            }

            CodePhysicalStart = (ulong)CodePage;
            DataPhysicalStart = (ulong)_dataArena;
            DataVirtualStart = DataPhysicalStart;

            Records[0] = new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderCode,
                CodePhysicalStart,
                CodePhysicalStart,
                1,
                0,
                BootMemoryRegionRecord.PageSize,
                CodePhysicalStart + BootMemoryRegionRecord.PageSize);
            Records[1] = new BootMemoryRegionRecord(
                (uint)UefiMemoryType.LoaderData,
                DataPhysicalStart,
                DataVirtualStart,
                ArenaPages,
                0,
                ArenaPages * BootMemoryRegionRecord.PageSize,
                DataPhysicalStart
                    + ArenaPages * BootMemoryRegionRecord.PageSize);

            Ledger[0] = LoadedImage.Range.Reservation;
            Ledger[1] = CreateRange(
                BootReservationKind.BootStackSwitchStub,
                CodePhysicalStart,
                1);
            Ledger[2] = CreateRange(
                BootReservationKind.BootStackGuard,
                DataPhysicalStart,
                GuardPages);
            Ledger[3] = CreateRange(
                BootReservationKind.BootStack,
                DataPhysicalStart
                    + GuardPages * BootReservedRange.PageSize,
                StackPages);
            LedgerCount = 4;

            Continuation = Virtual(ImageBase + ContinuationOffset);
            if (!Continuation.IsValid)
            {
                throw new InvalidOperationException(
                    "Canonical continuation address failed.");
            }

            RefreshedMap = new FirmwareMemoryMapRecord
            {
                BufferAddress =
                    ImageBase + 2 * BootReservedRange.PageSize,
                BufferSize = 80,
                SourceBufferAddress =
                    (ulong)(_backingMemory + MapSourceOffset),
                SourceBufferSize = 80,
                MapSize = 80,
                MapKey = RefreshedMapKey,
                DescriptorSize = 40,
                DescriptorVersion = 1,
                DescriptorCount = 2,
                Flags = FirmwareMemoryMapRecord.FlagOwnedCopy
            };
        }

        public M01StageOneAllocationEvidence
            CreateAllocationEvidence()
        {
            return M01StageOneAllocationEvidence
                .CaptureCallerAttested(
                    Records,
                    2,
                    2,
                    CodeAllocationOrdinal,
                    DataAllocationOrdinal,
                    M01StageOneAllocationEvidence.EfiSuccess,
                    M01StageOneAllocationEvidence.EfiSuccess);
        }

        public M01LoadedImageVirtualMappingEvidence
            CreateVirtualMapping()
        {
            return M01LoadedImageVirtualMappingEvidence
                .CaptureCallerAttestedIdentity(LoadedImage);
        }

        public M01StageOneAddressMappingEvidence
            CreateAddressMapping()
        {
            return M01StageOneAddressMappingEvidence
                .CaptureCallerAttestedIdentity(
                    Records,
                    2,
                    2,
                    RefreshedMap);
        }

        public M01StageOneMapLedgerEvidence CreateMapEvidence(
            M01StageOneAllocationEvidence allocations,
            ulong previousMapKey = PreviousMapKey,
            ulong previousMapGeneration = PreviousMapGeneration,
            FirmwareMemoryMapRecord? refreshedMap = null,
            ulong refreshedMapGeneration = RefreshedMapGeneration,
            ulong mapCaptureOrdinal = MapCaptureOrdinal,
            ulong ledgerBuildOrdinal = LedgerBuildOrdinal,
            BootReservedRange* ledger = null,
            ulong? ledgerCount = null,
            ulong? ledgerCapacity = null)
        {
            BootReservedRange* selectedLedger =
                ledger == null ? Ledger : ledger;
            return M01StageOneMapLedgerEvidence
                .CaptureCallerAttested(
                    previousMapKey,
                    previousMapGeneration,
                    refreshedMap ?? RefreshedMap,
                    refreshedMapGeneration,
                    allocations,
                    mapCaptureOrdinal,
                    ledgerBuildOrdinal,
                    selectedLedger,
                    ledgerCount ?? LedgerCount,
                    ledgerCapacity ?? LedgerCapacity);
        }

        public M01LoadedImageStackIntegrationResult PrepareCurrent()
        {
            M01StageOneAllocationEvidence allocations =
                CreateAllocationEvidence();
            return Invoke(
                LoadedImage,
                CreateVirtualMapping(),
                CreateAddressMapping(),
                Records,
                2,
                2,
                allocations,
                CreateMapEvidence(allocations),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                CodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackStaticPreparationResult
            PrepareFromStaticScratchCurrent()
        {
            M01StageOneAllocationEvidence allocations =
                CreateAllocationEvidence();
            return InvokeStatic(
                LoadedImage,
                CreateVirtualMapping(),
                CreateAddressMapping(),
                Records,
                2,
                2,
                allocations,
                CreateMapEvidence(allocations),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                CodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                EvidenceScratch,
                M01LoadedImageStackIntegration
                    .RequiredEvidenceScratchBytes);
        }

        public M01LoadedImageStackStaticPreparationResult
            InvokeStatic(
                EfiLoadedImageRangeCapturePlan loadedImage,
                M01LoadedImageVirtualMappingEvidence virtualMapping,
                M01StageOneAddressMappingEvidence addressMapping,
                BootMemoryRegionRecord* records,
                ulong recordCount,
                ulong recordCapacity,
                M01StageOneAllocationEvidence allocations,
                M01StageOneMapLedgerEvidence mapLedger,
                BootReservedRange* ledger,
                ulong ledgerCount,
                ulong ledgerCapacity,
                byte* codePage,
                ulong codeCapacity,
                ulong materializedLength,
                ulong stubOffset,
                ulong declaredLength,
                VirtualAddress continuation,
                M01LoadedImageStackStaticState* staticState,
                ulong staticCapacity,
                M01LoadedImageStackEvidence* evidenceScratch,
                ulong evidenceScratchCapacity)
        {
            return M01LoadedImageStackIntegration
                .PrepareFromStaticScratch(
                    loadedImage,
                    virtualMapping,
                    addressMapping,
                    records,
                    recordCount,
                    recordCapacity,
                    allocations,
                    mapLedger,
                    ledger,
                    ledgerCount,
                    ledgerCapacity,
                    codePage,
                    codeCapacity,
                    materializedLength,
                    stubOffset,
                    declaredLength,
                    continuation,
                    staticState,
                    staticCapacity,
                    evidenceScratch,
                    evidenceScratchCapacity);
        }

        public M01LoadedImageStackIntegrationResult
            PrepareWithLoadedImage(
                EfiLoadedImageRangeCapturePlan loadedImage)
        {
            M01StageOneAllocationEvidence allocations =
                CreateAllocationEvidence();
            return Invoke(
                loadedImage,
                CreateVirtualMapping(),
                CreateAddressMapping(),
                Records,
                2,
                2,
                allocations,
                CreateMapEvidence(allocations),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                CodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult
            PrepareWithAllocationTable(
                BootMemoryRegionRecord* records,
                ulong count,
                ulong capacity)
        {
            M01StageOneAllocationEvidence allocations =
                CreateAllocationEvidence();
            return Invoke(
                LoadedImage,
                CreateVirtualMapping(),
                CreateAddressMapping(),
                records,
                count,
                capacity,
                allocations,
                CreateMapEvidence(allocations),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                CodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult
            PrepareWithAllocationEvidence(
                M01StageOneAllocationEvidence allocations)
        {
            M01StageOneAllocationEvidence canonical =
                CreateAllocationEvidence();
            return Invoke(
                LoadedImage,
                CreateVirtualMapping(),
                CreateAddressMapping(),
                Records,
                2,
                2,
                allocations,
                CreateMapEvidence(canonical),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                CodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithEvidence(
            M01StageOneAllocationEvidence allocations,
            M01StageOneMapLedgerEvidence mapLedger)
        {
            return Invoke(
                LoadedImage,
                CreateVirtualMapping(),
                CreateAddressMapping(),
                Records,
                2,
                2,
                allocations,
                mapLedger,
                Ledger,
                LedgerCount,
                LedgerCapacity,
                CodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult
            PrepareWithVirtualMapping(
                M01LoadedImageVirtualMappingEvidence mapping)
        {
            M01StageOneAllocationEvidence allocations =
                CreateAllocationEvidence();
            return Invoke(
                LoadedImage,
                mapping,
                CreateAddressMapping(),
                Records,
                2,
                2,
                allocations,
                CreateMapEvidence(allocations),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                CodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult
            PrepareWithAddressMapping(
                M01StageOneAddressMappingEvidence mapping)
        {
            M01StageOneAllocationEvidence allocations =
                CreateAllocationEvidence();
            return Invoke(
                LoadedImage,
                CreateVirtualMapping(),
                mapping,
                Records,
                2,
                2,
                allocations,
                CreateMapEvidence(allocations),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                CodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithStorage(
            byte* codePage,
            ulong codeCapacity,
            ulong materializedLength,
            ulong stubOffset,
            ulong declaredLength,
            VirtualAddress continuation,
            M01LoadedImageStackStaticState* staticState,
            ulong staticCapacity)
        {
            M01StageOneAllocationEvidence allocations =
                CreateAllocationEvidence();
            return Invoke(
                LoadedImage,
                CreateVirtualMapping(),
                CreateAddressMapping(),
                Records,
                2,
                2,
                allocations,
                CreateMapEvidence(allocations),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                codePage,
                codeCapacity,
                materializedLength,
                stubOffset,
                declaredLength,
                continuation,
                staticState,
                staticCapacity,
                M01StageOneStubByteMode.EmitAndVerify);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithStub(
            ulong stubOffset,
            ulong declaredLength)
        {
            return PrepareWithStorage(
                CodePage,
                PageSize,
                PageSize,
                stubOffset,
                declaredLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public M01LoadedImageStackIntegrationResult PrepareWithMode(
            M01StageOneStubByteMode mode)
        {
            M01StageOneAllocationEvidence allocations =
                CreateAllocationEvidence();
            return Invoke(
                LoadedImage,
                CreateVirtualMapping(),
                CreateAddressMapping(),
                Records,
                2,
                2,
                allocations,
                CreateMapEvidence(allocations),
                Ledger,
                LedgerCount,
                LedgerCapacity,
                CodePage,
                PageSize,
                PageSize,
                StubEntryOffset,
                OwnedBootStackSwitchPlan.RequiredStubByteLength,
                Continuation,
                StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes,
                mode);
        }

        public void InsertLedgerEntry(
            int index,
            BootReservedRange value)
        {
            if (index < 0
                || (ulong)index > LedgerCount
                || LedgerCount == LedgerCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            for (ulong current = LedgerCount;
                current > (ulong)index;
                current--)
            {
                Ledger[current] = Ledger[current - 1];
            }

            Ledger[index] = value;
            LedgerCount++;
        }

        public void RebuildCanonicalLedgerFromRecords()
        {
            BootReservedRange[] ranges =
            [
                LoadedImage.Range.Reservation,
                CreateRange(
                    BootReservationKind.BootStackSwitchStub,
                    Records[0].PhysicalStart,
                    Records[0].PageCount),
                CreateRange(
                    BootReservationKind.BootStackGuard,
                    Records[1].PhysicalStart,
                    1),
                CreateRange(
                    BootReservationKind.BootStack,
                    Records[1].PhysicalStart
                        + BootReservedRange.PageSize,
                    Records[1].PageCount - 1)
            ];
            WriteSortedLedger(ranges);
        }

        public void AddOwnedMapReservation(ulong physicalStart)
        {
            BootReservedRange[] ranges =
                new BootReservedRange[checked((int)LedgerCount + 1)];
            for (int index = 0; index < (int)LedgerCount; index++)
            {
                ranges[index] = Ledger[index];
            }

            ranges[^1] = CreateRange(
                BootReservationKind.OwnedMemoryMap,
                physicalStart,
                1);
            WriteSortedLedger(ranges);
        }

        public void SwapLedgerEntries(int first, int second)
        {
            if (first < 0
                || second < 0
                || (ulong)first >= LedgerCount
                || (ulong)second >= LedgerCount)
            {
                throw new ArgumentOutOfRangeException();
            }

            (Ledger[first], Ledger[second]) =
                (Ledger[second], Ledger[first]);
        }

        public void WriteAuditedStub()
        {
            CodePage[StubEntryOffset + 0] =
                OwnedBootStackSwitchPlan.StubByte00;
            CodePage[StubEntryOffset + 1] =
                OwnedBootStackSwitchPlan.StubByte01;
            CodePage[StubEntryOffset + 2] =
                OwnedBootStackSwitchPlan.StubByte02;
            CodePage[StubEntryOffset + 3] =
                OwnedBootStackSwitchPlan.StubByte03;
            CodePage[StubEntryOffset + 4] =
                OwnedBootStackSwitchPlan.StubByte04;
            CodePage[StubEntryOffset + 5] =
                OwnedBootStackSwitchPlan.StubByte05;
            CodePage[StubEntryOffset + 6] =
                OwnedBootStackSwitchPlan.StubByte06;
            CodePage[StubEntryOffset + 7] =
                OwnedBootStackSwitchPlan.StubByte07;
            CodePage[StubEntryOffset + 8] =
                OwnedBootStackSwitchPlan.StubByte08;
            CodePage[StubEntryOffset + 9] =
                OwnedBootStackSwitchPlan.StubByte09;
            CodePage[StubEntryOffset + 10] =
                OwnedBootStackSwitchPlan.StubByte10;
            CodePage[StubEntryOffset + 11] =
                OwnedBootStackSwitchPlan.StubByte11;
            CodePage[StubEntryOffset + 12] =
                OwnedBootStackSwitchPlan.StubByte12;
            CodePage[StubEntryOffset + 13] =
                OwnedBootStackSwitchPlan.StubByte13;
        }

        public void ZeroMutableStorage()
        {
            Clear(CodePage, PageSize);
            Clear(_imageMemory, ImageBytes);
        }

        public bool CodeAndStateAreZero()
        {
            return IsZero(CodePage, PageSize)
                && IsZero(
                    (byte*)StaticState,
                    M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public byte[] CopyCodePage()
        {
            return CopyBytes(CodePage, PageSize);
        }

        public byte[] CopyStaticState()
        {
            return CopyBytes(
                (byte*)StaticState,
                M01LoadedImageStackStaticState.RequiredStorageBytes);
        }

        public byte[] CopyBackingMemory()
        {
            return CopyBytes(_backingMemory, BackingBytes);
        }

        public BootMemoryRegionRecord[] CopyAllocationRecords()
        {
            return [Records[0], Records[1]];
        }

        public BootReservedRange[] CopyLedger()
        {
            BootReservedRange[] copy =
                new BootReservedRange[(int)LedgerCount];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = Ledger[index];
            }

            return copy;
        }

        public void Dispose()
        {
            if (_backingMemory != null)
            {
                NativeMemory.AlignedFree(_backingMemory);
                _backingMemory = null;
            }

            _imageMemory = null;
            _dataArena = null;
            CodePage = null;
            Records = null;
            Ledger = null;
            StaticState = null;
            EvidenceScratch = null;
        }

        private static M01LoadedImageStackIntegrationResult Invoke(
            EfiLoadedImageRangeCapturePlan loadedImage,
            M01LoadedImageVirtualMappingEvidence virtualMapping,
            M01StageOneAddressMappingEvidence addressMapping,
            BootMemoryRegionRecord* records,
            ulong recordCount,
            ulong recordCapacity,
            M01StageOneAllocationEvidence allocations,
            M01StageOneMapLedgerEvidence mapLedger,
            BootReservedRange* ledger,
            ulong ledgerCount,
            ulong ledgerCapacity,
            byte* codePage,
            ulong codeCapacity,
            ulong materializedLength,
            ulong stubOffset,
            ulong declaredLength,
            VirtualAddress continuation,
            M01LoadedImageStackStaticState* staticState,
            ulong staticCapacity,
            M01StageOneStubByteMode mode)
        {
            return M01LoadedImageStackIntegration
                .PrepareWithStubByteMode(
                    loadedImage,
                    virtualMapping,
                    addressMapping,
                    records,
                    recordCount,
                    recordCapacity,
                    allocations,
                    mapLedger,
                    ledger,
                    ledgerCount,
                    ledgerCapacity,
                    codePage,
                    codeCapacity,
                    materializedLength,
                    stubOffset,
                    declaredLength,
                    continuation,
                    staticState,
                    staticCapacity,
                    mode);
        }

        private void WriteSortedLedger(BootReservedRange[] ranges)
        {
            if ((ulong)ranges.Length > LedgerCapacity)
            {
                throw new InvalidOperationException(
                    "M01-017 ledger fixture capacity exceeded.");
            }

            Array.Sort(
                ranges,
                static (left, right) =>
                {
                    left.PhysicalStart.TryGetRawValueForUefiBoundary(
                        out ulong leftStart);
                    right.PhysicalStart.TryGetRawValueForUefiBoundary(
                        out ulong rightStart);
                    return leftStart.CompareTo(rightStart);
                });
            Clear(
                (byte*)Ledger,
                LedgerSlotCapacity
                    * (ulong)sizeof(BootReservedRange));
            for (int index = 0; index < ranges.Length; index++)
            {
                Ledger[index] = ranges[index];
            }

            LedgerCount = (ulong)ranges.Length;
        }

        private static void Clear(byte* start, ulong length)
        {
            for (ulong index = 0; index < length; index++)
            {
                start[index] = 0;
            }
        }

        private static bool IsZero(byte* start, ulong length)
        {
            for (ulong index = 0; index < length; index++)
            {
                if (start[index] != 0)
                {
                    return false;
                }
            }

            return true;
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
