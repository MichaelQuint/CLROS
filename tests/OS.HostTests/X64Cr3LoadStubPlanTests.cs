using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static unsafe class X64Cr3LoadStubPlanTests
{
    private const string LoadTestName = "m01.013.01.load";

#if !M01_013_LOAD_FOCUSED && !M01_013_PREFLIGHT_FOCUSED && !M01_013_TRANSITION_FOCUSED
    [HostFixture(HostLane.Native)]
#endif
    internal static void RunAllM01013()
    {
        Console.WriteLine("TEST_START m01.013");
        int failures = 0;
        failures += RunM01013Load();
        failures += X64PagingModePreflightStubPlanTests
            .RunM01013Preflight();
        failures += X64ReversibleCr3TransitionStubPlanTests
            .RunM01013Transition();
        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL m01.013 failures=" + failures);
            throw new InvalidOperationException(
                "m01.013 aggregate failures=" + failures);
        }

        Console.WriteLine("CLROS_M01_013_READY");
        Console.WriteLine("TEST_PASS m01.013");
    }

    internal static int RunM01013Load()
    {
        Console.WriteLine("TEST_START " + LoadTestName);
        int failures = 0;

        RunCase(
            "enum pod and constants",
            TestEnumPodAndConstants,
            ref failures);
        RunCase(
            "default values fail closed",
            TestDefaultsFailClosed,
            ref failures);
        RunCase(
            "policy storage and event values round trip",
            TestValueRoundTrips,
            ref failures);
        RunCase(
            "composite creation binds exact sibling evidence",
            TestPositiveComposition,
            ref failures);
        RunCase(
            "allocation policy and sibling evidence fail closed",
            TestCompositionFailures,
            ref failures);
        RunCase(
            "transcript preserves raw cr3 and marker order",
            TestTranscriptAudit,
            ref failures);
        RunCase(
            "transcript terminal failures are deterministic",
            TestTranscriptFailures,
            ref failures);
        RunCase(
            "hostile pod numeric and reserved fields fail closed",
            TestHostilePodFields,
            ref failures);
        RunCase(
            "source boundary excludes live integration",
            TestSourceBoundary,
            ref failures);
        RunCase(
            "aggregate coordinator and marker ownership",
            TestAggregateCoordinatorContract,
            ref failures);
        RunCase(
            "nostdlib efi composite closure has no references",
            TestNoStdLibClosure,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + LoadTestName + " failures=" + failures);
            return failures;
        }

        Console.WriteLine("TEST_PASS " + LoadTestName);
        return 0;
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {
        HostCheck.Run(LoadTestName + " " + name, () =>
        {
            try
            {
                if (test())
                    return true;
                Console.WriteLine(
                    "M01_013_LOAD_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "M01_013_LOAD_CASE_FAIL "
                    + Normalize(name)
                    + " "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestEnumPodAndConstants()
    {
        (string Name, byte Value)[] provenanceValues =
        [
            ("Uninitialized", 0),
            ("CallerAttestedOwnedLoaderData", 1)
        ];
        (string Name, byte Value)[] eventValues =
        [
            ("Uninitialized", 0),
            ("PreMarkerEmitted", 1),
            ("TransitionEntered", 2),
            ("OwnedRootLoaded", 3),
            ("OwnedRootReadBack", 4),
            ("CanaryUpdated", 5),
            ("FirmwareRootRestored", 6),
            ("FirmwareRootReadBack", 7),
            ("CanaryValidated", 8),
            ("PostMarkerEmitted", 9),
            ("FirmwareCallbackAttempted", 10),
            ("InterruptReenabledInsideInterval", 11),
            ("Timeout", 12),
            ("Reset", 13),
            ("TripleFault", 14)
        ];
        (string Name, byte Value)[] statusValues =
        [
            ("Uninitialized", 0),
            ("Success", 1),
            ("InvalidPolicy", 2),
            ("EnabledByDefault", 3),
            ("ExplicitOptInRequired", 4),
            ("InvalidTimeout", 5),
            ("InvalidAllocationRecordCount", 6),
            ("TooManyAllocationRecords", 7),
            ("AllocationRecordCapacityTooSmall", 8),
            ("MissingAllocationRecords", 9),
            ("MisalignedAllocationRecords", 10),
            ("AllocationRecordSpanOverflow", 11),
            ("InvalidCodeMemoryType", 12),
            ("InvalidCodeAllocation", 13),
            ("InvalidDataMemoryType", 14),
            ("InvalidDataAllocation", 15),
            ("AllocationOverlap", 16),
            ("DataArenaPageCountMismatch", 17),
            ("InvalidMappingPlan", 18),
            ("InvalidMappingRangeCount", 19),
            ("MappingRangeCapacityTooSmall", 20),
            ("MissingMappingRanges", 21),
            ("MisalignedMappingRanges", 22),
            ("MappingRangeSpanOverflow", 23),
            ("InvalidMappingRange", 24),
            ("MappingAccountingMismatch", 25),
            ("WritableExecutableMapping", 26),
            ("MissingStubMapping", 27),
            ("MissingDataArenaMapping", 28),
            ("InvalidStackSwitchPlan", 29),
            ("InvalidExpectedRsp", 30),
            ("MissingStackMapping", 31),
            ("InvalidOriginalRoot", 32),
            ("InvalidOwnedRoot", 33),
            ("RootsNotDistinct", 34),
            ("OwnedRootOutsideTableArena", 35),
            ("OriginalRootInsideOwnedArena", 36),
            ("InvalidStorageLayout", 37),
            ("InvalidStateStorage", 38),
            ("InvalidCanaryStorage", 39),
            ("StorageOverlap", 40),
            ("InvalidCanaryValues", 41),
            ("InvalidPreflightPlan", 42),
            ("InvalidPreflightMaterialization", 43),
            ("InvalidPreflightReadback", 44),
            ("InvalidTransitionPlan", 45),
            ("InvalidTransitionMaterialization", 46),
            ("InvalidTransitionReadback", 47),
            ("StubByteRangeOverlap", 48),
            ("StubByteRangeOutsideCodePage", 49),
            ("StubEntryMisaligned", 50),
            ("InvalidEventCount", 51),
            ("TooManyEvents", 52),
            ("EventCapacityTooSmall", 53),
            ("MissingEvents", 54),
            ("MisalignedEvents", 55),
            ("EventSpanOverflow", 56),
            ("InvalidEvent", 57),
            ("UnexpectedEvent", 58),
            ("MissingPreMarker", 59),
            ("EarlyPostMarker", 60),
            ("MissingPostMarker", 61),
            ("FirmwareCallbackInsideInterval", 62),
            ("InterruptReenabledInsideInterval", 63),
            ("RootReadbackMismatch", 64),
            ("RspEscapedOwnedStack", 65),
            ("CanaryNotUpdated", 66),
            ("CanaryValidationFailed", 67),
            ("Timeout", 68),
            ("Reset", 69),
            ("TripleFault", 70),
            ("ContractLayoutMismatch", 71),
            ("StackSwitchAllocationOverlap", 72)
        ];

        MethodInfo? create = typeof(X64Cr3LoadStubPlan).GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static);
        ParameterInfo[] createParameters =
            create?.GetParameters() ?? [];

        return HasExactByteEnum<X64Cr3ProbeStorageProvenance>(
                provenanceValues)
            && HasExactByteEnum<X64Cr3ProbeEventKind>(eventValues)
            && HasExactByteEnum<X64Cr3LoadStubPlanStatus>(statusValues)
            && IsReadOnlySequentialUnmanaged<X64Cr3ProbePolicy>()
            && IsReadOnlySequentialUnmanaged<X64Cr3ProbeStorageLayout>()
            && IsReadOnlySequentialUnmanaged<X64Cr3ProbeEvent>()
            && IsReadOnlySequentialUnmanaged<X64Cr3LoadStubPlan>()
            && IsReadOnlySequentialUnmanaged<X64Cr3LoadStubPlanResult>()
            && IsReadOnlySequentialUnmanaged<X64Cr3ProbeAuditResult>()
            && Marshal.SizeOf<X64Cr3ProbePolicy>()
                == (int)X64Cr3ProbePolicy.StructureSize
            && Marshal.SizeOf<X64Cr3ProbeStorageLayout>()
                == (int)X64Cr3ProbeStorageLayout.StructureSize
            && Marshal.SizeOf<X64Cr3ProbeEvent>()
                == (int)X64Cr3ProbeEvent.StructureSize
            && Marshal.SizeOf<X64Cr3LoadStubPlan>()
                == (int)X64Cr3LoadStubPlan.StructureSize
            && Marshal.SizeOf<X64Cr3LoadStubPlanResult>()
                == (int)X64Cr3LoadStubPlanResult.StructureSize
            && Marshal.SizeOf<X64Cr3ProbeAuditResult>()
                == (int)X64Cr3ProbeAuditResult.StructureSize
            && Offset<X64Cr3ProbePolicy>("_timeoutTicks") == 0
            && Offset<X64Cr3ProbePolicy>("_isInitialized") == 8
            && Offset<X64Cr3ProbePolicy>("_enabledByDefault") == 9
            && Offset<X64Cr3ProbePolicy>(
                "_explicitOneShotOptIn") == 10
            && Offset<X64Cr3ProbePolicy>("_reserved0") == 11
            && Offset<X64Cr3ProbePolicy>("_reserved1") == 12
            && Offset<X64Cr3ProbeStorageLayout>(
                "_stateAddress") == 0
            && Offset<X64Cr3ProbeStorageLayout>(
                "_stateByteLength") == 8
            && Offset<X64Cr3ProbeStorageLayout>(
                "_canaryAddress") == 16
            && Offset<X64Cr3ProbeStorageLayout>(
                "_canaryByteLength") == 24
            && Offset<X64Cr3ProbeStorageLayout>(
                "_initialCanaryValue") == 32
            && Offset<X64Cr3ProbeStorageLayout>(
                "_updatedCanaryValue") == 40
            && Offset<X64Cr3ProbeStorageLayout>(
                "_isInitialized") == 48
            && Offset<X64Cr3ProbeStorageLayout>(
                "_provenance") == 49
            && Offset<X64Cr3ProbeStorageLayout>("_reserved0") == 50
            && Offset<X64Cr3ProbeStorageLayout>("_reserved1") == 52
            && Offset<X64Cr3ProbeEvent>("_observedRawCr3") == 0
            && Offset<X64Cr3ProbeEvent>("_observedRsp") == 8
            && Offset<X64Cr3ProbeEvent>("_observedCanary") == 16
            && Offset<X64Cr3ProbeEvent>("_elapsedTicks") == 24
            && Offset<X64Cr3ProbeEvent>("_isInitialized") == 32
            && Offset<X64Cr3ProbeEvent>("_kind") == 33
            && Offset<X64Cr3ProbeEvent>("_reserved0") == 34
            && Offset<X64Cr3ProbeEvent>("_reserved1") == 36
            && Offset<X64Cr3LoadStubPlan>("_policy") == 0
            && Offset<X64Cr3LoadStubPlan>("_codeAllocation") == 16
            && Offset<X64Cr3LoadStubPlan>("_dataAllocation") == 72
            && Offset<X64Cr3LoadStubPlan>("_identityMappingPlan") == 128
            && Offset<X64Cr3LoadStubPlan>("_stackSwitchPlan") == 304
            && Offset<X64Cr3LoadStubPlan>("_storageLayout") == 832
            && Offset<X64Cr3LoadStubPlan>("_rawFirmwareCr3") == 888
            && Offset<X64Cr3LoadStubPlan>("_firmwareRootBase") == 896
            && Offset<X64Cr3LoadStubPlan>("_ownedRoot") == 904
            && Offset<X64Cr3LoadStubPlan>("_tableArenaStart") == 912
            && Offset<X64Cr3LoadStubPlan>(
                "_tableArenaEndExclusive") == 920
            && Offset<X64Cr3LoadStubPlan>("_statePageStart") == 928
            && Offset<X64Cr3LoadStubPlan>(
                "_statePageEndExclusive") == 936
            && Offset<X64Cr3LoadStubPlan>("_usableStackStart") == 944
            && Offset<X64Cr3LoadStubPlan>(
                "_usableStackEndExclusive") == 952
            && Offset<X64Cr3LoadStubPlan>(
                "_physicalAddressMask") == 960
            && Offset<X64Cr3LoadStubPlan>(
                "_preflightEntryOffset") == 968
            && Offset<X64Cr3LoadStubPlan>(
                "_preflightByteLength") == 976
            && Offset<X64Cr3LoadStubPlan>(
                "_transitionEntryOffset") == 984
            && Offset<X64Cr3LoadStubPlan>(
                "_transitionByteLength") == 992
            && Offset<X64Cr3LoadStubPlan>(
                "_transitionRuntimeStateAddress") == 1000
            && Offset<X64Cr3LoadStubPlan>("_isInitialized") == 1008
            && Offset<X64Cr3LoadStubPlan>(
                "_physicalAddressWidth") == 1009
            && Offset<X64Cr3LoadStubPlan>("_reserved1") == 1010
            && Offset<X64Cr3LoadStubPlan>("_reserved2") == 1012
            && Offset<X64Cr3LoadStubPlanResult>("_status") == 0
            && Offset<X64Cr3LoadStubPlanResult>("_addressStatus") == 1
            && Offset<X64Cr3LoadStubPlanResult>(
                "_identityMappingStatus") == 2
            && Offset<X64Cr3LoadStubPlanResult>(
                "_stackSwitchStatus") == 3
            && Offset<X64Cr3LoadStubPlanResult>("_reserved") == 4
            && Offset<X64Cr3LoadStubPlanResult>(
                "_allocationRecordCount") == 8
            && Offset<X64Cr3LoadStubPlanResult>(
                "_allocationRecordCapacity") == 16
            && Offset<X64Cr3LoadStubPlanResult>(
                "_mappingRangeCount") == 24
            && Offset<X64Cr3LoadStubPlanResult>(
                "_mappingRangeCapacity") == 32
            && Offset<X64Cr3LoadStubPlanResult>(
                "_invalidIndex") == 40
            && Offset<X64Cr3LoadStubPlanResult>(
                "_conflictingIndex") == 48
            && Offset<X64Cr3LoadStubPlanResult>("_plan") == 56
            && Offset<X64Cr3ProbeAuditResult>("_status") == 0
            && Offset<X64Cr3ProbeAuditResult>("_reserved0") == 1
            && Offset<X64Cr3ProbeAuditResult>("_reserved1") == 2
            && Offset<X64Cr3ProbeAuditResult>("_reserved2") == 4
            && Offset<X64Cr3ProbeAuditResult>("_eventCount") == 8
            && Offset<X64Cr3ProbeAuditResult>("_eventCapacity") == 16
            && Offset<X64Cr3ProbeAuditResult>(
                "_validatedEventCount") == 24
            && Offset<X64Cr3ProbeAuditResult>(
                "_invalidEventIndex") == 32
            && Offset<X64Cr3ProbeAuditResult>("_finalRawCr3") == 40
            && Offset<X64Cr3ProbeAuditResult>(
                "_finalCanaryValue") == 48
            && Offset<X64Cr3ProbeAuditResult>("_elapsedTicks") == 56
            && X64Cr3LoadStubPlan.RequiredAllocationRecordCount == 2
            && X64Cr3LoadStubPlan.RequiredCodePageCount == 1
            && X64Cr3LoadStubPlan.RequiredStatePageCount == 1
            && X64Cr3LoadStubPlan.RequiredEventCount == 9
            && X64Cr3LoadStubPlan.MaximumEventCount == 32
            && !X64Cr3LoadStubPlan.DefaultEnabled
            && X64Cr3LoadStubPlan.RequiresExplicitOneShotOptIn
            && !X64Cr3LoadStubPlan.PerformsLiveCr3Load
            && !X64Cr3LoadStubPlan.HostTestsExecuteMovCr3
            && !X64Cr3LoadStubPlan
                .AllowsFirmwareCallbacksInsideInterval
            && !X64Cr3LoadStubPlan
                .AllowsInterruptReenableInsideInterval
            && create is not null
            && create.ReturnType == typeof(X64Cr3LoadStubPlanResult)
            && createParameters.Length == 18
            && createParameters[1].ParameterType.IsPointer
            && createParameters[1].ParameterType.GetElementType()
                == typeof(BootMemoryRegionRecord)
            && createParameters[5].ParameterType.IsPointer
            && createParameters[5].ParameterType.GetElementType()
                == typeof(X64BootIdentityMappingRange)
            && createParameters[13].ParameterType
                == typeof(X64ReversibleCr3TransitionStubPlanResult)
            && createParameters[15].ParameterType
                == typeof(X64ReversibleCr3TransitionEvidenceResult);
    }

    private static bool TestDefaultsFailClosed()
    {
        X64Cr3ProbePolicy policy = default;
        X64Cr3ProbeStorageLayout storage = default;
        X64Cr3ProbeEvent probeEvent = default;
        X64Cr3LoadStubPlan plan = default;
        X64Cr3LoadStubPlanResult result = default;
        X64Cr3ProbeAuditResult audit = default;
        X64Cr3ProbeAuditResult invalidAudit = plan.Audit(null, 0, 0);

        return !policy.IsInitialized
            && !storage.IsInitialized
            && !probeEvent.IsInitialized
            && !plan.IsInitialized
            && !plan.IsValid
            && !result.Succeeded
            && result.Status == X64Cr3LoadStubPlanStatus.Uninitialized
            && !audit.Succeeded
            && audit.Status == X64Cr3LoadStubPlanStatus.Uninitialized
            && !invalidAudit.Succeeded
            && invalidAudit.Status
                == X64Cr3LoadStubPlanStatus.Uninitialized;
    }

    private static bool TestValueRoundTrips()
    {
        VirtualAddress state = Virtual(0x0020_4000);
        VirtualAddress canary = Virtual(
            0x0020_4000
            + X64Cr3ProbeStorageLayout
                .RequiredStateRecordByteLength);
        VirtualAddress rsp = Virtual(0x0060_3fd8);
        X64Cr3ProbePolicy policy = new(
            enabledByDefault: false,
            explicitOneShotOptIn: true,
            timeoutTicks: 1000);
        X64Cr3ProbePolicy unsafeDefaultOn = new(
            enabledByDefault: true,
            explicitOneShotOptIn: true,
            timeoutTicks: 1000);
        X64Cr3ProbeStorageLayout storage = new(
            state,
            X64Cr3ProbeStorageLayout.RequiredStateRecordByteLength,
            canary,
            X64Cr3ProbeStorageLayout.RequiredCanaryByteLength,
            0x1122334455667788UL,
            0x8877665544332211UL,
            X64Cr3ProbeStorageProvenance
                .CallerAttestedOwnedLoaderData);
        X64Cr3ProbeEvent probeEvent = new(
            X64Cr3ProbeEventKind.FirmwareRootReadBack,
            0x00300018,
            rsp,
            storage.UpdatedCanaryValue,
            77);

        return policy.IsInitialized
            && !policy.EnabledByDefault
            && policy.ExplicitOneShotOptIn
            && policy.TimeoutTicks == 1000
            && unsafeDefaultOn.IsInitialized
            && unsafeDefaultOn.EnabledByDefault
            && storage.IsInitialized
            && storage.StateAddress.Equals(state)
            && storage.StateByteLength
                == X64Cr3ProbeStorageLayout
                    .RequiredStateRecordByteLength
            && storage.CanaryAddress.Equals(canary)
            && storage.CanaryByteLength
                == X64Cr3ProbeStorageLayout.RequiredCanaryByteLength
            && storage.Provenance
                == X64Cr3ProbeStorageProvenance
                    .CallerAttestedOwnedLoaderData
            && probeEvent.IsInitialized
            && probeEvent.Kind
                == X64Cr3ProbeEventKind.FirmwareRootReadBack
            && probeEvent.ObservedRawCr3 == 0x00300018
            && probeEvent.ObservedRsp.Equals(rsp)
            && probeEvent.ObservedCanary
                == storage.UpdatedCanaryValue
            && probeEvent.ElapsedTicks == 77;
    }

    private static bool TestPositiveComposition()
    {
        using CompositionFixture fixture = new();
        X64Cr3LoadStubPlanResult result = fixture.Compose();
        if (!result.Succeeded)
        {
            Console.WriteLine(
                "M01_013_COMPOSE_DIAGNOSTIC status="
                + result.Status
                + " map="
                + result.IdentityMappingStatus
                + " stack="
                + result.StackSwitchStatus);
            return false;
        }

        X64Cr3LoadStubPlan plan = result.Plan;
        return plan.IsInitialized
            && plan.IsValid
            && plan.CodeAllocation.UefiType
                == (uint)UefiMemoryType.LoaderCode
            && plan.CodeAllocation.PageCount == 1
            && plan.DataAllocation.UefiType
                == (uint)UefiMemoryType.LoaderData
            && plan.DataAllocation.PageCount
                == plan.IdentityMappingPlan.TotalTablePageCount + 1
            && plan.RawFirmwareCr3
                == CompositionFixture.RawFirmwareCr3
            && Raw(plan.FirmwareRootBase)
                == CompositionFixture.FirmwareRoot
            && Raw(plan.OwnedRoot) == CompositionFixture.DataStart
            && plan.PhysicalAddressWidth == 48
            && plan.PhysicalAddressMask == ((1UL << 48) - 1)
            && Raw(plan.TableArenaStart)
                == CompositionFixture.DataStart
            && Raw(plan.TableArenaEndExclusive)
                == CompositionFixture.DataStart
                    + plan.IdentityMappingPlan.TotalTableByteLength
            && Raw(plan.StatePageStart)
                == Raw(plan.TableArenaEndExclusive)
            && Raw(plan.StatePageEndExclusive)
                == plan.DataAllocation.PhysicalEndExclusive
            && plan.StorageLayout.StateByteLength
                == X64ReversibleCr3TransitionRuntimeState.StructureSize
            && plan.PreflightByteLength
                == X64PagingModePreflightStubPlan.RequiredByteLength
            && plan.TransitionByteLength
                == X64ReversibleCr3TransitionStubPlan.RequiredByteLength
            && plan.TransitionRuntimeStateAddress
                == Raw(plan.StorageLayout.StateAddress)
            && plan.PreflightEntryOffset
                == CompositionFixture.PreflightOffset
            && plan.TransitionEntryOffset
                == CompositionFixture.TransitionOffset;
    }

    private static bool TestCompositionFailures()
    {
        using CompositionFixture fixture = new();

        X64Cr3ProbePolicy defaultOn = new(
            enabledByDefault: true,
            explicitOneShotOptIn: true,
            timeoutTicks: fixture.Policy.TimeoutTicks);
        X64Cr3ProbePolicy noOptIn = new(
            enabledByDefault: false,
            explicitOneShotOptIn: false,
            timeoutTicks: fixture.Policy.TimeoutTicks);
        X64Cr3ProbePolicy noTimeout = new(
            enabledByDefault: false,
            explicitOneShotOptIn: true,
            timeoutTicks: 0);
        BootMemoryRegionRecord[] wrongCodeType =
            fixture.Allocations.ToArray();
        wrongCodeType[0] = WithRecord(
            wrongCodeType[0],
            uefiType: (uint)UefiMemoryType.LoaderData);
        BootMemoryRegionRecord[] overlapping =
            fixture.Allocations.ToArray();
        overlapping[1] = WithRecord(
            overlapping[1],
            physicalStart: overlapping[0].PhysicalStart,
            virtualStart: overlapping[0].VirtualStart,
            physicalEndExclusive:
                overlapping[0].PhysicalStart
                + overlapping[1].ByteLength);
        X64Cr3ProbeStorageLayout unchangedCanary = new(
            fixture.Storage.StateAddress,
            fixture.Storage.StateByteLength,
            fixture.Storage.CanaryAddress,
            fixture.Storage.CanaryByteLength,
            fixture.Storage.InitialCanaryValue,
            fixture.Storage.InitialCanaryValue,
            fixture.Storage.Provenance);
        X64ReversibleCr3TransitionStubPlanResult
            reboundMaterializationCapacity = MutateUInt64(
                fixture.TransitionMaterialization,
                48,
                fixture.TransitionMaterialization.SourceCapacity - 1);
        X64ReversibleCr3TransitionStubPlanResult
            reboundReadbackCapacity = MutateUInt64(
                fixture.TransitionReadback,
                48,
                fixture.TransitionReadback.SourceCapacity - 1);
        ulong reboundRuntimeStateAddress =
            Raw(fixture.Storage.StateAddress) - 8;
        X64ReversibleCr3TransitionEvidenceResult
            reboundTransitionEvidence = MutateUInt64(
                fixture.TransitionEvidence,
                24,
                reboundRuntimeStateAddress);
        OwnedBootStackSwitchPlanResult stackCodeOnProbeCode =
            CompositionFixture.CreateStackSwitchPlan(
                stackCodeStart: fixture.CodeStart);
        OwnedBootStackSwitchPlanResult stackCodeOnProbeData =
            CompositionFixture.CreateStackSwitchPlan(
                stackCodeStart: CompositionFixture.DataStart);
        OwnedBootStackSwitchPlanResult stackDataOnProbeCode =
            CompositionFixture.CreateStackSwitchPlan(
                stackBackingStart: fixture.CodeStart);
        OwnedBootStackSwitchPlanResult stackDataOnProbeData =
            CompositionFixture.CreateStackSwitchPlan(
                stackBackingStart: CompositionFixture.DataStart);

        bool pointerMetadataFailures;
        fixed (BootMemoryRegionRecord* allocations =
            fixture.Allocations)
        fixed (X64BootIdentityMappingRange* mappings =
            fixture.MappingRanges)
        {
            pointerMetadataFailures =
                ExpectedFailure(
                    "missing-allocations",
                    fixture.ComposeRaw(
                        null,
                        2,
                        2,
                        mappings,
                        fixture.Mapping.OutputRangeCount,
                        (ulong)fixture.MappingRanges.Length),
                    X64Cr3LoadStubPlanStatus
                        .MissingAllocationRecords)
                && ExpectedFailure(
                    "allocation-count",
                    fixture.ComposeRaw(
                        allocations,
                        1,
                        2,
                        mappings,
                        fixture.Mapping.OutputRangeCount,
                        (ulong)fixture.MappingRanges.Length),
                    X64Cr3LoadStubPlanStatus
                        .InvalidAllocationRecordCount)
                && ExpectedFailure(
                    "allocation-capacity",
                    fixture.ComposeRaw(
                        allocations,
                        2,
                        1,
                        mappings,
                        fixture.Mapping.OutputRangeCount,
                        (ulong)fixture.MappingRanges.Length),
                    X64Cr3LoadStubPlanStatus
                        .AllocationRecordCapacityTooSmall)
                && ExpectedFailure(
                    "allocation-too-many",
                    fixture.ComposeRaw(
                        allocations,
                        2,
                        3,
                        mappings,
                        fixture.Mapping.OutputRangeCount,
                        (ulong)fixture.MappingRanges.Length),
                    X64Cr3LoadStubPlanStatus
                        .TooManyAllocationRecords)
                && ExpectedFailure(
                    "misaligned-allocations",
                    fixture.ComposeRaw(
                        (BootMemoryRegionRecord*)1,
                        2,
                        2,
                        mappings,
                        fixture.Mapping.OutputRangeCount,
                        (ulong)fixture.MappingRanges.Length),
                    X64Cr3LoadStubPlanStatus
                        .MisalignedAllocationRecords)
                && ExpectedFailure(
                    "overflowing-allocations",
                    fixture.ComposeRaw(
                        (BootMemoryRegionRecord*)(~0UL - 7),
                        2,
                        2,
                        mappings,
                        fixture.Mapping.OutputRangeCount,
                        (ulong)fixture.MappingRanges.Length),
                    X64Cr3LoadStubPlanStatus
                        .AllocationRecordSpanOverflow)
                && ExpectedFailure(
                    "missing-mappings",
                    fixture.ComposeRaw(
                        allocations,
                        2,
                        2,
                        null,
                        fixture.Mapping.OutputRangeCount,
                        (ulong)fixture.MappingRanges.Length),
                    X64Cr3LoadStubPlanStatus.MissingMappingRanges)
                && ExpectedFailure(
                    "mapping-capacity",
                    fixture.ComposeRaw(
                        allocations,
                        2,
                        2,
                        mappings,
                        fixture.Mapping.OutputRangeCount,
                        fixture.Mapping.OutputRangeCount - 1),
                    X64Cr3LoadStubPlanStatus
                        .MappingRangeCapacityTooSmall);
        }

        return pointerMetadataFailures
            && ExpectedFailure(
                "default-on",
                fixture.Compose(policy: defaultOn),
                X64Cr3LoadStubPlanStatus.EnabledByDefault)
            && ExpectedFailure(
                "no-opt-in",
                fixture.Compose(policy: noOptIn),
                X64Cr3LoadStubPlanStatus.ExplicitOptInRequired)
            && ExpectedFailure(
                "no-timeout",
                fixture.Compose(policy: noTimeout),
                X64Cr3LoadStubPlanStatus.InvalidTimeout)
            && ExpectedFailure(
                "wrong-code-type",
                fixture.Compose(allocations: wrongCodeType),
                X64Cr3LoadStubPlanStatus.InvalidCodeMemoryType)
            && ExpectedFailure(
                "allocation-overlap",
                fixture.Compose(allocations: overlapping),
                X64Cr3LoadStubPlanStatus.AllocationOverlap)
            && ExpectedFailure(
                "stack-code-on-probe-code",
                fixture.Compose(stackSwitch: stackCodeOnProbeCode),
                X64Cr3LoadStubPlanStatus
                    .StackSwitchAllocationOverlap)
            && ExpectedFailure(
                "stack-code-on-probe-data",
                fixture.Compose(stackSwitch: stackCodeOnProbeData),
                X64Cr3LoadStubPlanStatus
                    .StackSwitchAllocationOverlap)
            && ExpectedFailure(
                "stack-data-on-probe-code",
                fixture.Compose(stackSwitch: stackDataOnProbeCode),
                X64Cr3LoadStubPlanStatus
                    .StackSwitchAllocationOverlap)
            && ExpectedFailure(
                "stack-data-on-probe-data",
                fixture.Compose(stackSwitch: stackDataOnProbeData),
                X64Cr3LoadStubPlanStatus
                    .StackSwitchAllocationOverlap)
            && ExpectedFailure(
                "unchanged-canary",
                fixture.Compose(storage: unchangedCanary),
                X64Cr3LoadStubPlanStatus.InvalidCanaryValues)
            && ExpectedFailure(
                "default-preflight",
                fixture.Compose(
                    preflight:
                        default(X64PagingModePreflightResult)),
                X64Cr3LoadStubPlanStatus.InvalidPreflightPlan)
            && ExpectedFailure(
                "default-transition-evidence",
                fixture.Compose(
                    transitionEvidence:
                        default(
                            X64ReversibleCr3TransitionEvidenceResult)),
                X64Cr3LoadStubPlanStatus.InvalidTransitionPlan)
            && reboundTransitionEvidence.Succeeded
            && ExpectedFailure(
                "transition-evidence-state-address-rebound",
                fixture.Compose(
                    transitionEvidence:
                        reboundTransitionEvidence),
                X64Cr3LoadStubPlanStatus.InvalidTransitionPlan)
            && ExpectedFailure(
                "transition-materialization-capacity-rebound",
                fixture.Compose(
                    transitionMaterialization:
                        reboundMaterializationCapacity),
                X64Cr3LoadStubPlanStatus
                    .InvalidTransitionMaterialization)
            && ExpectedFailure(
                "transition-readback-capacity-rebound",
                fixture.Compose(
                    transitionReadback: reboundReadbackCapacity),
                X64Cr3LoadStubPlanStatus.InvalidTransitionReadback)
            && ExpectedFailure(
                "stub-overlap",
                fixture.Compose(
                    transitionOffset:
                        CompositionFixture.PreflightOffset),
                X64Cr3LoadStubPlanStatus.StubByteRangeOverlap);
    }

    private static bool TestTranscriptAudit()
    {
        using CompositionFixture fixture = new();
        X64Cr3LoadStubPlanResult result = fixture.Compose();
        if (!result.Succeeded)
            return false;

        X64Cr3ProbeEvent[] events =
            fixture.CreateSuccessfulTranscript();
        fixed (X64Cr3ProbeEvent* pointer = events)
        {
            X64Cr3ProbeAuditResult audit =
                result.Plan.Audit(
                    pointer,
                    (ulong)events.Length,
                    (ulong)events.Length);
            return audit.Succeeded
                && audit.EventCount
                    == X64Cr3LoadStubPlan.RequiredEventCount
                && audit.ValidatedEventCount
                    == X64Cr3LoadStubPlan.RequiredEventCount
                && audit.InvalidEventIndex
                    == X64Cr3LoadStubPlan.NoIndex
                && audit.FinalRawCr3
                    == CompositionFixture.RawFirmwareCr3
                && audit.FinalCanaryValue
                    == fixture.Storage.UpdatedCanaryValue
                && (audit.FinalRawCr3 & 0x18UL) == 0x18UL;
        }
    }

    private static bool TestTranscriptFailures()
    {
        using CompositionFixture fixture = new();
        X64Cr3LoadStubPlanResult result = fixture.Compose();
        if (!result.Succeeded)
            return false;

        X64Cr3ProbeEvent[] baseline =
            fixture.CreateSuccessfulTranscript();
        X64Cr3ProbeEvent[] earlyPost = baseline.ToArray();
        earlyPost[3] = fixture.Event(
            X64Cr3ProbeEventKind.PostMarkerEmitted,
            ownedRoot: true,
            updatedCanary: false,
            elapsed: 4);
        X64Cr3ProbeEvent[] rawBitLoss = baseline.ToArray();
        rawBitLoss[6] = fixture.Event(
            X64Cr3ProbeEventKind.FirmwareRootReadBack,
            ownedRoot: false,
            updatedCanary: true,
            elapsed: 7,
            rawCr3Override: CompositionFixture.FirmwareRoot);
        X64Cr3ProbeEvent[] wrongRsp = baseline.ToArray();
        wrongRsp[4] = new X64Cr3ProbeEvent(
            X64Cr3ProbeEventKind.CanaryUpdated,
            CompositionFixture.DataStart,
            Virtual(
                Raw(fixture.StackSwitch.Plan.UsableStackVirtualStart)
                + X64Cr3LoadStubPlan
                    .RequiredProbeStackHeadroomBytes
                - 8),
            fixture.Storage.UpdatedCanaryValue,
            5);
        X64Cr3ProbeEvent[] unchangedCanary = baseline.ToArray();
        unchangedCanary[4] = fixture.Event(
            X64Cr3ProbeEventKind.CanaryUpdated,
            ownedRoot: true,
            updatedCanary: false,
            elapsed: 5);
        X64Cr3ProbeEvent[] callback = baseline.ToArray();
        callback[3] = fixture.Event(
            X64Cr3ProbeEventKind.FirmwareCallbackAttempted,
            ownedRoot: true,
            updatedCanary: false,
            elapsed: 4);
        X64Cr3ProbeEvent[] interrupt = baseline.ToArray();
        interrupt[3] = fixture.Event(
            X64Cr3ProbeEventKind.InterruptReenabledInsideInterval,
            ownedRoot: true,
            updatedCanary: false,
            elapsed: 4);
        X64Cr3ProbeEvent[] timeout = baseline.ToArray();
        timeout[3] = fixture.Event(
            X64Cr3ProbeEventKind.Timeout,
            ownedRoot: true,
            updatedCanary: false,
            elapsed: fixture.Policy.TimeoutTicks);
        X64Cr3ProbeEvent[] reset = baseline.ToArray();
        reset[3] = fixture.Event(
            X64Cr3ProbeEventKind.Reset,
            ownedRoot: true,
            updatedCanary: false,
            elapsed: 4);
        X64Cr3ProbeEvent[] tripleFault = baseline.ToArray();
        tripleFault[3] = fixture.Event(
            X64Cr3ProbeEventKind.TripleFault,
            ownedRoot: true,
            updatedCanary: false,
            elapsed: 4);

        return AuditFailure(
                result.Plan,
                earlyPost,
                X64Cr3LoadStubPlanStatus.EarlyPostMarker,
                3)
            && AuditFailure(
                result.Plan,
                rawBitLoss,
                X64Cr3LoadStubPlanStatus.RootReadbackMismatch,
                6)
            && AuditFailure(
                result.Plan,
                wrongRsp,
                X64Cr3LoadStubPlanStatus.RspEscapedOwnedStack,
                4)
            && AuditFailure(
                result.Plan,
                unchangedCanary,
                X64Cr3LoadStubPlanStatus.CanaryNotUpdated,
                4)
            && AuditFailure(
                result.Plan,
                callback,
                X64Cr3LoadStubPlanStatus
                    .FirmwareCallbackInsideInterval,
                3)
            && AuditFailure(
                result.Plan,
                interrupt,
                X64Cr3LoadStubPlanStatus
                    .InterruptReenabledInsideInterval,
                3)
            && AuditFailure(
                result.Plan,
                timeout,
                X64Cr3LoadStubPlanStatus.Timeout,
                3)
            && AuditFailure(
                result.Plan,
                reset,
                X64Cr3LoadStubPlanStatus.Reset,
                3)
            && AuditFailure(
                result.Plan,
                tripleFault,
                X64Cr3LoadStubPlanStatus.TripleFault,
                3)
            && AuditPrefixFailure(
                result.Plan,
                baseline,
                count: 8,
                X64Cr3LoadStubPlanStatus.MissingPostMarker);
    }

    private static bool TestHostilePodFields()
    {
        using CompositionFixture fixture = new();
        X64Cr3LoadStubPlanResult result = fixture.Compose();
        if (!result.Succeeded)
            return false;

        X64Cr3LoadStubPlan plan = result.Plan;
        X64Cr3ProbeEvent[] events =
            fixture.CreateSuccessfulTranscript();
        X64Cr3ProbeAuditResult audit;
        fixed (X64Cr3ProbeEvent* pointer = events)
        {
            audit = plan.Audit(
                pointer,
                (ulong)events.Length,
                (ulong)events.Length);
        }
        if (!audit.Succeeded)
            return false;

        X64Cr3ProbePolicy policyReserved =
            MutateByte(fixture.Policy, 11, 1);
        X64Cr3ProbeStorageLayout storageReserved =
            MutateByte(fixture.Storage, 50, 1);
        X64Cr3ProbeEvent eventReserved =
            MutateByte(events[0], 34, 1);

        X64Cr3LoadStubPlan planReserved =
            MutateByte(plan, 1010, 1);
        X64Cr3LoadStubPlan badMask =
            MutateUInt64(plan, 960, 0);
        X64Cr3LoadStubPlan badRawCr3 =
            MutateUInt64(
                plan,
                888,
                CompositionFixture.RawFirmwareCr3 | 2UL);
        X64Cr3LoadStubPlan badDataPages =
            MutateUInt64(plan, 96, 99);
        X64Cr3LoadStubPlan badTableEnd =
            MutateUInt64(plan, 920, CompositionFixture.DataStart);
        X64Cr3LoadStubPlan badStorageAddress =
            MutateUInt64(plan, 848, CompositionFixture.DataStart);
        X64Cr3LoadStubPlan badStackStart =
            MutateUInt64(plan, 944, 0);
        X64Cr3LoadStubPlan badTransitionLength =
            MutateUInt64(plan, 992, 0);
        X64Cr3LoadStubPlan reboundTransitionLength =
            MutateUInt64(
                plan,
                992,
                plan.TransitionByteLength - 1);
        X64Cr3LoadStubPlan reboundTransitionRuntimeStateAddress =
            MutateUInt64(
                plan,
                1000,
                plan.TransitionRuntimeStateAddress - 8);
        X64Cr3LoadStubPlan badInitialized =
            MutateByte(plan, 1008, 2);
        OwnedBootStackSwitchPlanResult overlappingStack =
            CompositionFixture.CreateStackSwitchPlan(
                stackCodeStart: fixture.CodeStart);
        if (!overlappingStack.Succeeded)
            return false;
        X64Cr3LoadStubPlan overlappingStackPlan =
            ReplaceValue(plan, 304, overlappingStack);

        X64Cr3LoadStubPlanResult resultReserved =
            MutateByte(result, 4, 1);
        X64Cr3LoadStubPlanResult badAllocationCount =
            MutateUInt64(result, 8, 1);
        X64Cr3LoadStubPlanResult badMappingCount =
            MutateUInt64(result, 24, 0);
        X64Cr3LoadStubPlanResult badNestedPlan =
            MutateUInt64(result, 56 + 960, 0);
        X64Cr3LoadStubPlanResult reboundNestedTransitionLength =
            MutateUInt64(
                result,
                56 + 992,
                plan.TransitionByteLength - 1);
        X64Cr3LoadStubPlanResult
            reboundNestedTransitionRuntimeStateAddress =
                MutateUInt64(
                    result,
                    56 + 1000,
                    plan.TransitionRuntimeStateAddress - 8);
        X64Cr3LoadStubPlanResult overlappingStackResult =
            ReplaceValue(result, 56 + 304, overlappingStack);

        X64Cr3ProbeAuditResult auditReserved =
            MutateByte(audit, 1, 1);
        X64Cr3ProbeAuditResult badEventCount =
            MutateUInt64(audit, 8, 8);
        X64Cr3ProbeAuditResult badEventCapacity =
            MutateUInt64(audit, 16, 33);
        X64Cr3ProbeAuditResult badValidatedCount =
            MutateUInt64(audit, 24, 8);
        X64Cr3ProbeAuditResult badFinalCr3 =
            MutateUInt64(audit, 40, 0);
        X64Cr3ProbeAuditResult badFinalCanary =
            MutateUInt64(audit, 48, 0);

        return !policyReserved.IsInitialized
            && !storageReserved.IsInitialized
            && !eventReserved.IsInitialized
            && !planReserved.IsValid
            && !badMask.IsValid
            && !badRawCr3.IsValid
            && !badDataPages.IsValid
            && !badTableEnd.IsValid
            && !badStorageAddress.IsValid
            && !badStackStart.IsValid
            && !badTransitionLength.IsValid
            && !reboundTransitionLength.IsValid
            && !reboundTransitionRuntimeStateAddress.IsValid
            && !badInitialized.IsValid
            && !overlappingStackPlan.IsValid
            && !resultReserved.Succeeded
            && !badAllocationCount.Succeeded
            && !badMappingCount.Succeeded
            && !badNestedPlan.Succeeded
            && !reboundNestedTransitionLength.Succeeded
            && !reboundNestedTransitionRuntimeStateAddress.Succeeded
            && !overlappingStackResult.Succeeded
            && !auditReserved.Succeeded
            && !badEventCount.Succeeded
            && !badEventCapacity.Succeeded
            && !badValidatedCount.Succeeded
            && !badFinalCr3.Succeeded
            && !badFinalCanary.Succeeded;
    }

    private static bool TestSourceBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string sourcePath = Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Memory",
            "X64Cr3LoadStubPlan.cs");
        string source = File.ReadAllText(sourcePath);
        string stripped = RemoveComments(source);
        string[] forbidden =
        [
            @"\busing\b",
            @"\bSystem\.",
            @"\bthrow\b",
            @"\bcatch\b",
            @"\bfinally\b",
            @"\btypeof\b",
            @"\bReflection\b",
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
            @"\bMarshal\b",
            @"\bIced\b",
            @"\bBootAsm\b",
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
        foreach (string pattern in forbidden)
        {
            if (Regex.IsMatch(
                stripped,
                pattern,
                RegexOptions.CultureInvariant))
            {
                return false;
            }
        }

        return source.Contains(
                "public ulong RawFirmwareCr3",
                StringComparison.Ordinal)
            && source.Contains(
                "public OS.Boot.PhysicalAddress FirmwareRootBase",
                StringComparison.Ordinal)
            && source.Contains(
                "public ulong TransitionRuntimeStateAddress",
                StringComparison.Ordinal)
            && source.Contains(
                "DefaultEnabled = false",
                StringComparison.Ordinal)
            && source.Contains(
                "PerformsLiveCr3Load = false",
                StringComparison.Ordinal)
            && source.Contains(
                "AllowsFirmwareCallbacksInsideInterval = false",
                StringComparison.Ordinal)
            && source.Contains(
                "AllowsInterruptReenableInsideInterval = false",
                StringComparison.Ordinal)
            && !source.Contains(
                "public OS.Boot.PhysicalAddress OriginalRoot",
                StringComparison.Ordinal)
             && !stripped.Contains(
                 "delegate" + "*",
                 StringComparison.Ordinal);
    }

    private static bool TestAggregateCoordinatorContract()
    {
        HostCheck.SingleFixture(typeof(X64Cr3LoadStubPlanTests), nameof(RunAllM01013),
            typeof(X64PagingModePreflightStubPlanTests), typeof(X64ReversibleCr3TransitionStubPlanTests));
        string root = HostToolProcess.FindRepositoryRoot();
        string loadPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "X64Cr3LoadStubPlanTests.cs");
        string preflightPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "X64PagingModePreflightStubPlanTests.cs");
        string transitionPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "X64ReversibleCr3TransitionStubPlanTests.cs");
        if (!File.Exists(loadPath)
            || !File.Exists(preflightPath)
            || !File.Exists(transitionPath))
        {
            return false;
        }

        string loadSource = File.ReadAllText(loadPath);
        string preflightSource = File.ReadAllText(preflightPath);
        string transitionSource = File.ReadAllText(transitionPath);
        string startMarker = "\"TEST_START " + "m01.013\"";
        string readyMarker = "\"CLROS_M01_" + "013_READY\"";
        string passMarker = "\"TEST_PASS " + "m01.013\"";
        int startIndex = loadSource.IndexOf(
            startMarker,
            StringComparison.Ordinal);
        int loadIndex = loadSource.IndexOf(
            "failures += RunM01013Load();",
            StringComparison.Ordinal);
        int preflightIndex = loadSource.IndexOf(
            ".RunM01013Preflight();",
            StringComparison.Ordinal);
        int transitionIndex = loadSource.IndexOf(
            ".RunM01013Transition();",
            StringComparison.Ordinal);
        int readyIndex = loadSource.IndexOf(
            readyMarker,
            StringComparison.Ordinal);
        int passIndex = loadSource.IndexOf(
            passMarker,
            StringComparison.Ordinal);
        if (startIndex < 0
            || loadIndex <= startIndex
            || preflightIndex <= loadIndex
            || transitionIndex <= preflightIndex
            || readyIndex <= transitionIndex
            || passIndex <= readyIndex
            || !loadSource.Contains(
                "\"TEST_FAIL m01.013 failures=\"",
                StringComparison.Ordinal)
            || !loadSource.Contains(
                "throw new InvalidOperationException(",
                StringComparison.Ordinal))
        {
            return false;
        }

        string uniqueReadyMarker = "CLROS_M01_" + "013_READY";
        int markerOwners = 0;
        int markerOccurrences = 0;
        string[] firstPartyRoots =
        [
            "src",
            "std",
            "apps",
            "tools",
            "tests"
        ];
        foreach (string firstPartyRoot in firstPartyRoots)
        {
            string scanRoot = Path.Combine(root, firstPartyRoot);
            if (!Directory.Exists(scanRoot))
                return false;

            foreach (string path in Directory.EnumerateFiles(
                scanRoot,
                "*.cs",
                SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path)
                    .Replace('\\', '/');
                string surrounded = "/" + relative + "/";
                if (surrounded.Contains(
                        "/bin/",
                        StringComparison.OrdinalIgnoreCase)
                    || surrounded.Contains(
                        "/obj/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = File.ReadAllText(path);
                int occurrences = CountOccurrences(
                    candidate,
                    uniqueReadyMarker);
                if (occurrences == 0)
                    continue;

                markerOwners++;
                markerOccurrences += occurrences;
                if (!string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(loadPath),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return markerOwners == 1 && markerOccurrences == 1;
    }

    private static bool TestNoStdLibClosure()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-013-composite-"
            + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            File.Copy(
                Path.Combine(root, "global.json"),
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
                new UTF8Encoding(false));
            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                internal static unsafe class CompositeProbe
                {
                    public static byte Run()
                    {
                        OS.Kernel.Memory.X64Cr3LoadStubPlan plan = default;
                        OS.Kernel.Memory.X64Cr3ProbeAuditResult audit =
                            plan.Audit(null, 0, 0);
                        return (byte)audit.Status;
                    }
                }
                """,
                new UTF8Encoding(false));

            string[] relativeSources =
            [
                @"src\OS.Kernel\Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs",
                @"src\OS.Kernel\Memory\PageProtection.cs",
                @"src\OS.Kernel\Memory\PageCachePolicy.cs",
                @"src\OS.Kernel\Memory\X64PageTableEntry.cs",
                @"src\OS.Kernel\Boot\X64BootIdentityMappingPlan.cs",
                @"src\OS.Kernel\Memory\BootReservedRange.cs",
                @"src\OS.Kernel\Memory\BootReservationTableValidator.cs",
                @"src\OS.Kernel\Boot\OwnedBootStackLayout.cs",
                @"src\OS.Kernel\Boot\UefiMemoryType.cs",
                @"src\OS.Kernel\Boot\BootMemoryRegionMaterializer.cs",
                @"src\OS.Kernel\Boot\OwnedBootStackSwitchPlan.cs",
                @"src\OS.Kernel\Memory\X64PagingModePreflightStubPlan.cs",
                @"src\OS.Kernel\Memory\X64ReversibleCr3TransitionStubPlan.cs",
                @"src\OS.Kernel\Memory\X64Cr3LoadStubPlan.cs"
            ];
            StringBuilder items = new();
            items.AppendLine(
                """    <Compile Include="NoStdLibShim.cs" />""");
            foreach (string relative in relativeSources)
            {
                string full = Path.Combine(root, relative);
                items.Append("    <Compile Include=\"");
                items.Append(EscapeXml(full));
                items.AppendLine("\" />");
            }
            items.AppendLine(
                """    <Compile Include="Probe.cs" />""");

            string project =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M01.Cr3CompositeClosure</AssemblyName>
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
                __ITEMS__
                  </ItemGroup>
                  <Target Name="CustomizeReferences"
                          BeforeTargets="BeforeCompile"
                          AfterTargets="FindReferenceAssembliesForReferences">
                    <ItemGroup>
                      <ReferencePathWithRefAssemblies
                          Remove="@(ReferencePathWithRefAssemblies)" />
                      <ReferencePath Remove="@(ReferencePath)" />
                    </ItemGroup>
                  </Target>
                </Project>
                """
                .Replace(
                    "__ITEMS__",
                    items.ToString().TrimEnd(),
                    StringComparison.Ordinal);
            string projectPath = Path.Combine(
                workspace,
                "M01.013.Composite.NoStdLib.csproj");
            File.WriteAllText(
                projectPath,
                project,
                new UTF8Encoding(false));

            ProcessStartInfo start = new("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workspace
            };
            start.ArgumentList.Add("build");
            start.ArgumentList.Add(projectPath);
            start.ArgumentList.Add("--nologo");
            start.ArgumentList.Add("-m:1");
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException(
                    "Failed to start NOSTDLIB_EFI build.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit(90000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
            Task.WaitAll(stdout, stderr);
            string output = stdout.Result + Environment.NewLine
                + stderr.Result;
            if (!exited || process.ExitCode != 0)
            {
                Console.WriteLine(
                    "M01_013_NOSTDLIB_DIAGNOSTIC "
                    + NormalizeCompilerOutput(output));
                return false;
            }

            string assemblyPath = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.Cr3CompositeClosure.dll");
            if (!File.Exists(assemblyPath))
                return false;
            using FileStream stream = File.OpenRead(assemblyPath);
            using PEReader reader = new(stream);
            return reader.HasMetadata
                && reader.GetMetadataReader()
                    .AssemblyReferences.Count == 0;
        }
        finally
        {
            try
            {
                Directory.Delete(workspace, recursive: true);
            }
            catch
            {
                // Cleanup is not product evidence.
            }
        }
    }

    private static bool Failure(
        X64Cr3LoadStubPlanResult result,
        X64Cr3LoadStubPlanStatus expected)
    {
        return !result.Succeeded
            && result.Status == expected
            && !result.Plan.IsValid;
    }

    private static bool ExpectedFailure(
        string name,
        X64Cr3LoadStubPlanResult result,
        X64Cr3LoadStubPlanStatus expected)
    {
        if (Failure(result, expected))
            return true;

        Console.WriteLine(
            "M01_013_EXPECTED_FAILURE_DIAGNOSTIC "
            + Normalize(name)
            + " expected="
            + expected
            + " actual="
            + result.Status
            + " succeeded="
            + result.Succeeded);
        return false;
    }

    private static bool AuditFailure(
        X64Cr3LoadStubPlan plan,
        X64Cr3ProbeEvent[] events,
        X64Cr3LoadStubPlanStatus expected,
        ulong invalidIndex)
    {
        fixed (X64Cr3ProbeEvent* pointer = events)
        {
            X64Cr3ProbeAuditResult audit = plan.Audit(
                pointer,
                (ulong)events.Length,
                (ulong)events.Length);
            return !audit.Succeeded
                && audit.Status == expected
                && audit.InvalidEventIndex == invalidIndex;
        }
    }

    private static bool AuditPrefixFailure(
        X64Cr3LoadStubPlan plan,
        X64Cr3ProbeEvent[] events,
        ulong count,
        X64Cr3LoadStubPlanStatus expected)
    {
        fixed (X64Cr3ProbeEvent* pointer = events)
        {
            X64Cr3ProbeAuditResult audit = plan.Audit(
                pointer,
                count,
                (ulong)events.Length);
            return !audit.Succeeded
                && audit.Status == expected
                && audit.ValidatedEventCount == count;
        }
    }

    private sealed unsafe class CompositionFixture : IDisposable
    {
        internal const ulong DataStart = 0x0020_0000;
        internal const ulong FirmwareRoot = 0x0030_0000;
        internal const ulong RawFirmwareCr3 = FirmwareRoot | 0x18UL;
        internal const ulong PreflightOffset = 0;
        internal const ulong TransitionOffset = 256;
        private const ulong StackArenaStart = 0x0060_0000;
        private const ulong StackGuardPages = 1;
        private const ulong StackPages = 3;
        private const ulong StackArenaPages =
            StackGuardPages + StackPages;
        private const ulong InitialCanary = 0x1122_3344_5566_7788UL;
        private const ulong UpdatedCanary = 0x8877_6655_4433_2211UL;
        private const ulong ValidCr0 =
            X64PagingModePreflightStubPlan.Cr0PagingBit
            | X64PagingModePreflightStubPlan.Cr0WriteProtectBit;
        private const ulong ValidCr4 =
            X64PagingModePreflightStubPlan.Cr4PaeBit
            | X64ReversibleCr3TransitionStubPlan
                .Cr4PageGlobalEnableBit;
        private const ulong ValidEfer =
            X64PagingModePreflightStubPlan.EferLongModeEnableBit
            | X64PagingModePreflightStubPlan.EferLongModeActiveBit
            | X64PagingModePreflightStubPlan.EferNoExecuteEnableBit;
        private const ulong ValidRflags = 0x202;

        private byte* _codePage;

        internal CompositionFixture()
        {
            _codePage = (byte*)NativeMemory.AlignedAlloc(
                (nuint)BootMemoryRegionRecord.PageSize,
                (nuint)BootMemoryRegionRecord.PageSize);
            if (_codePage == null)
                throw new OutOfMemoryException();
            for (ulong index = 0;
                index < BootMemoryRegionRecord.PageSize;
                index++)
            {
                _codePage[index] = 0;
            }

            CodeStart = (ulong)_codePage;
            StackSwitch = CreateStackSwitchPlan();
            if (!StackSwitch.Succeeded)
            {
                throw new InvalidOperationException(
                    "stack fixture failed: " + StackSwitch.Status);
            }
            ProbeRsp = Virtual(
                Raw(
                    StackSwitch.Plan
                        .ExpectedContinuationStackPointer)
                - 64);

            BuildMappingPlan(
                CodeStart,
                out X64BootIdentityMappingRequirement[] requirements,
                out X64BootIdentityMappingRange[] ranges,
                out X64BootIdentityMappingPlanResult mapping);
            Requirements = requirements;
            MappingRanges = ranges;
            Mapping = mapping;
            if (!Mapping.Succeeded)
            {
                throw new InvalidOperationException(
                    "mapping fixture failed: " + Mapping.Status);
            }

            ulong dataPages = Mapping.TotalTablePageCount + 1;
            Allocations =
            [
                Record(
                    UefiMemoryType.LoaderCode,
                    CodeStart,
                    1),
                Record(
                    UefiMemoryType.LoaderData,
                    DataStart,
                    dataPages)
            ];

            ulong statePage =
                DataStart + Mapping.TotalTableByteLength;
            Storage = new X64Cr3ProbeStorageLayout(
                Virtual(statePage),
                X64Cr3ProbeStorageLayout
                    .RequiredStateRecordByteLength,
                Virtual(
                    statePage
                    + X64Cr3ProbeStorageLayout
                        .RequiredStateRecordByteLength),
                X64Cr3ProbeStorageLayout.RequiredCanaryByteLength,
                InitialCanary,
                UpdatedCanary,
                X64Cr3ProbeStorageProvenance
                    .CallerAttestedOwnedLoaderData);
            Policy = new X64Cr3ProbePolicy(
                enabledByDefault: false,
                explicitOneShotOptIn: true,
                timeoutTicks: 1000);

            X64PagingModePreflightObservation observation =
                X64PagingModePreflightObservation.CreateObserved(
                    ValidCr0,
                    RawFirmwareCr3,
                    ValidCr4,
                    ValidEfer,
                    X64PagingModePreflightStubPlan
                        .CpuidAddressSizesLeaf,
                    48,
                    ValidRflags);
            X64PagingModePreflightResult preflightState = default;
            PreflightMaterialization =
                X64PagingModePreflightStubPlan.TryMaterialize(
                    _codePage,
                    BootMemoryRegionRecord.PageSize,
                    PreflightOffset,
                    X64PagingModePreflightStubPlan
                        .RequiredByteLength,
                    &observation,
                    X64PagingModePreflightObservation.StructureSize,
                    &preflightState,
                    X64PagingModePreflightResult.StructureSize);
            PreflightReadback =
                X64PagingModePreflightStubPlan
                    .TryValidateMaterialized(
                        _codePage,
                        BootMemoryRegionRecord.PageSize,
                        PreflightOffset,
                        X64PagingModePreflightStubPlan
                            .RequiredByteLength,
                        &observation,
                        X64PagingModePreflightObservation
                            .StructureSize,
                        &preflightState,
                        X64PagingModePreflightResult.StructureSize);
            X64PagingModePreflightRequest request =
                X64PagingModePreflightRequest.CreateEnabled(
                    Physical(DataStart),
                    Physical(DataStart),
                    Mapping.TotalTableByteLength);
            Preflight = X64PagingModePreflightStubPlan.Evaluate(
                &request,
                1,
                1,
                &observation,
                1,
                1,
                &preflightState,
                1,
                1);

            byte* transitionBytes = _codePage + TransitionOffset;
            ulong transitionEntry = CodeStart + TransitionOffset;
            ulong transitionCapacity =
                BootMemoryRegionRecord.PageSize - TransitionOffset;
            TransitionMaterialization =
                X64ReversibleCr3TransitionStubPlan.Emit(
                    transitionBytes,
                    transitionCapacity,
                    transitionEntry);
            TransitionReadback =
                X64ReversibleCr3TransitionStubPlan.Validate(
                    transitionBytes,
                    X64ReversibleCr3TransitionStubPlan
                        .RequiredByteLength,
                    transitionCapacity,
                    transitionEntry);

            ulong stackStart =
                Raw(StackSwitch.Plan.UsableStackVirtualStart);
            ulong stackEnd =
                Raw(StackSwitch.Plan.UsableStackVirtualEndExclusive);
            ulong rsp = Raw(ProbeRsp);
            ulong canaryAddress = Raw(Storage.CanaryAddress);
            X64ReversibleCr3TransitionRuntimeState transitionState =
                CreateSuccessfulTransitionState(
                    stackStart,
                    stackEnd,
                    canaryAddress,
                    rsp);
            TransitionEvidence =
                X64ReversibleCr3TransitionStubPlan.Evaluate(
                    &transitionState,
                    Raw(Storage.StateAddress),
                    X64ReversibleCr3TransitionTerminalKind.Returned);
        }

        internal ulong CodeStart { get; }
        internal BootMemoryRegionRecord[] Allocations { get; }
        internal X64BootIdentityMappingRequirement[] Requirements { get; }
        internal X64BootIdentityMappingRange[] MappingRanges { get; }
        internal X64BootIdentityMappingPlanResult Mapping { get; }
        internal OwnedBootStackSwitchPlanResult StackSwitch { get; }
        internal VirtualAddress ProbeRsp { get; }
        internal X64Cr3ProbeStorageLayout Storage { get; }
        internal X64Cr3ProbePolicy Policy { get; }
        internal X64PagingModePreflightResult Preflight { get; }
        internal X64PagingModePreflightStubResult
            PreflightMaterialization { get; }
        internal X64PagingModePreflightStubResult PreflightReadback { get; }
        internal X64ReversibleCr3TransitionStubPlanResult
            TransitionMaterialization { get; }
        internal X64ReversibleCr3TransitionStubPlanResult
            TransitionReadback { get; }
        internal X64ReversibleCr3TransitionEvidenceResult
            TransitionEvidence { get; }

        internal X64Cr3LoadStubPlanResult Compose(
            X64Cr3ProbePolicy? policy = null,
            BootMemoryRegionRecord[]? allocations = null,
            OwnedBootStackSwitchPlanResult? stackSwitch = null,
            X64Cr3ProbeStorageLayout? storage = null,
            X64PagingModePreflightResult? preflight = null,
            X64PagingModePreflightStubResult?
                preflightMaterialization = null,
            X64PagingModePreflightStubResult?
                preflightReadback = null,
            X64ReversibleCr3TransitionStubPlanResult?
                transitionMaterialization = null,
            X64ReversibleCr3TransitionStubPlanResult?
                transitionReadback = null,
            X64ReversibleCr3TransitionEvidenceResult?
                transitionEvidence = null,
            ulong preflightOffset = PreflightOffset,
            ulong transitionOffset = TransitionOffset)
        {
            BootMemoryRegionRecord[] selectedAllocations =
                allocations ?? Allocations;
            fixed (BootMemoryRegionRecord* allocationPointer =
                selectedAllocations)
            fixed (X64BootIdentityMappingRange* mappingPointer =
                MappingRanges)
            {
                return X64Cr3LoadStubPlan.Create(
                    policy ?? Policy,
                    allocationPointer,
                    (ulong)selectedAllocations.Length,
                    (ulong)selectedAllocations.Length,
                    Mapping,
                    mappingPointer,
                    Mapping.OutputRangeCount,
                    (ulong)MappingRanges.Length,
                    stackSwitch ?? StackSwitch,
                    storage ?? Storage,
                    preflight ?? Preflight,
                    preflightMaterialization
                        ?? PreflightMaterialization,
                    preflightReadback ?? PreflightReadback,
                    transitionMaterialization
                        ?? TransitionMaterialization,
                    transitionReadback ?? TransitionReadback,
                    transitionEvidence ?? TransitionEvidence,
                    preflightOffset,
                    transitionOffset);
            }
        }

        internal X64Cr3LoadStubPlanResult ComposeRaw(
            BootMemoryRegionRecord* allocationPointer,
            ulong allocationCount,
            ulong allocationCapacity,
            X64BootIdentityMappingRange* mappingPointer,
            ulong mappingCount,
            ulong mappingCapacity)
        {
            return X64Cr3LoadStubPlan.Create(
                Policy,
                allocationPointer,
                allocationCount,
                allocationCapacity,
                Mapping,
                mappingPointer,
                mappingCount,
                mappingCapacity,
                StackSwitch,
                Storage,
                Preflight,
                PreflightMaterialization,
                PreflightReadback,
                TransitionMaterialization,
                TransitionReadback,
                TransitionEvidence,
                PreflightOffset,
                TransitionOffset);
        }

        internal X64Cr3ProbeEvent[] CreateSuccessfulTranscript()
        {
            return
            [
                Event(
                    X64Cr3ProbeEventKind.PreMarkerEmitted,
                    ownedRoot: false,
                    updatedCanary: false,
                    elapsed: 1),
                Event(
                    X64Cr3ProbeEventKind.TransitionEntered,
                    ownedRoot: false,
                    updatedCanary: false,
                    elapsed: 2),
                Event(
                    X64Cr3ProbeEventKind.OwnedRootLoaded,
                    ownedRoot: true,
                    updatedCanary: false,
                    elapsed: 3),
                Event(
                    X64Cr3ProbeEventKind.OwnedRootReadBack,
                    ownedRoot: true,
                    updatedCanary: false,
                    elapsed: 4),
                Event(
                    X64Cr3ProbeEventKind.CanaryUpdated,
                    ownedRoot: true,
                    updatedCanary: true,
                    elapsed: 5),
                Event(
                    X64Cr3ProbeEventKind.FirmwareRootRestored,
                    ownedRoot: false,
                    updatedCanary: true,
                    elapsed: 6),
                Event(
                    X64Cr3ProbeEventKind.FirmwareRootReadBack,
                    ownedRoot: false,
                    updatedCanary: true,
                    elapsed: 7),
                Event(
                    X64Cr3ProbeEventKind.CanaryValidated,
                    ownedRoot: false,
                    updatedCanary: true,
                    elapsed: 8),
                Event(
                    X64Cr3ProbeEventKind.PostMarkerEmitted,
                    ownedRoot: false,
                    updatedCanary: true,
                    elapsed: 9)
            ];
        }

        internal X64Cr3ProbeEvent Event(
            X64Cr3ProbeEventKind kind,
            bool ownedRoot,
            bool updatedCanary,
            ulong elapsed,
            ulong? rawCr3Override = null)
        {
            ulong rawCr3 = rawCr3Override
                ?? (ownedRoot ? DataStart : RawFirmwareCr3);
            return new X64Cr3ProbeEvent(
                kind,
                rawCr3,
                ProbeRsp,
                updatedCanary ? UpdatedCanary : InitialCanary,
                elapsed);
        }

        public void Dispose()
        {
            byte* page = _codePage;
            _codePage = null;
            if (page != null)
                NativeMemory.AlignedFree(page);
        }

        private static X64ReversibleCr3TransitionRuntimeState
            CreateSuccessfulTransitionState(
                ulong stackStart,
                ulong stackEnd,
                ulong canaryAddress,
                ulong rsp)
        {
            ulong clearedCr4 =
                ValidCr4
                & ~X64ReversibleCr3TransitionStubPlan
                    .Cr4PageGlobalEnableBit;
            return X64ReversibleCr3TransitionRuntimeState.CreateObserved(
                DataStart,
                stackStart,
                stackEnd,
                canaryAddress,
                InitialCanary,
                UpdatedCanary,
                ValidRflags,
                RawFirmwareCr3,
                ValidCr4,
                clearedCr4,
                DataStart,
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
                X64ReversibleCr3TransitionRuntimeStatus.Success,
                X64ReversibleCr3TransitionCheckpoint.Returned);
        }

        private static void BuildMappingPlan(
            ulong codeStart,
            out X64BootIdentityMappingRequirement[] requirements,
            out X64BootIdentityMappingRange[] ranges,
            out X64BootIdentityMappingPlanResult result)
        {
            ulong dataPages = 8;
            requirements = [];
            ranges = [];
            result = default;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                requirements = RequirementsFor(codeStart, dataPages);
                ranges = new X64BootIdentityMappingRange[64];
                fixed (X64BootIdentityMappingRequirement* source =
                    requirements)
                fixed (X64BootIdentityMappingRange* destination = ranges)
                {
                    result = X64BootIdentityMappingPlan.Create(
                        source,
                        (ulong)requirements.Length,
                        (ulong)requirements.Length,
                        destination,
                        (ulong)ranges.Length,
                        128,
                        PageTableContext());
                }
                if (!result.Succeeded)
                    return;

                ulong exactPages = result.TotalTablePageCount + 1;
                if (exactPages == dataPages)
                    return;
                dataPages = exactPages;
            }
            throw new InvalidOperationException(
                "mapping table/data page accounting did not converge");
        }

        private static X64BootIdentityMappingRequirement[]
            RequirementsFor(ulong codeStart, ulong dataPages)
        {
            ulong page = BootMemoryRegionRecord.PageSize;
            return
            [
                Requirement(
                    X64BootIdentityMappingPurpose.BootStackGuard,
                    0x1000,
                    0x2000,
                    PageProtection.None,
                    PageCachePolicy.Uninitialized),
                Requirement(
                    X64BootIdentityMappingPurpose.ExecutingImage,
                    0x4000,
                    0x5000,
                    Protection(
                        PageProtection.Read,
                        PageProtection.Execute)),
                Requirement(
                    X64BootIdentityMappingPurpose.OwnedStack,
                    StackArenaStart + page,
                    StackArenaStart + StackArenaPages * page,
                    Protection(
                        PageProtection.Read,
                        PageProtection.Write)),
                Requirement(
                    X64BootIdentityMappingPurpose.BootHandoff,
                    0x0070_0000,
                    0x0070_1000,
                    PageProtection.Read),
                Requirement(
                    X64BootIdentityMappingPurpose.SerialStub,
                    0x0080_0000,
                    0x0080_1000,
                    Protection(
                        PageProtection.Read,
                        PageProtection.Execute)),
                Requirement(
                    X64BootIdentityMappingPurpose.AcpiScratch,
                    0x0090_0000,
                    0x0090_1000,
                    PageProtection.Read),
                Requirement(
                    X64BootIdentityMappingPurpose.FramebufferMetadata,
                    0x00a0_0000,
                    0x00a0_1000,
                    PageProtection.Read),
                Requirement(
                    X64BootIdentityMappingPurpose.AdditionalRequired,
                    codeStart,
                    codeStart + page,
                    Protection(
                        PageProtection.Read,
                        PageProtection.Execute)),
                Requirement(
                    X64BootIdentityMappingPurpose.AdditionalRequired,
                    DataStart,
                    DataStart + dataPages * page,
                    Protection(
                        PageProtection.Read,
                        PageProtection.Write))
            ];
        }

        private static X64BootIdentityMappingRequirement Requirement(
            X64BootIdentityMappingPurpose purpose,
            ulong start,
            ulong end,
            PageProtection protection)
        {
            return Requirement(
                purpose,
                start,
                end,
                protection,
                protection == PageProtection.None
                    ? PageCachePolicy.Uninitialized
                    : PageCachePolicy.WriteBack);
        }

        private static X64BootIdentityMappingRequirement Requirement(
            X64BootIdentityMappingPurpose purpose,
            ulong start,
            ulong end,
            PageProtection protection,
            PageCachePolicy cache)
        {
            ulong bytes = end - start;
            return new X64BootIdentityMappingRequirement(
                purpose,
                Physical(start),
                Physical(end),
                bytes / BootMemoryRegionRecord.PageSize,
                bytes,
                protection,
                cache);
        }

        private static X64PageTableEncodingPreconditions
            PageTableContext()
        {
            X64PageTableEntryStatus tokenStatus =
                X64PatMappingToken.TryCreate(
                    0x0006_0501_0007_0406,
                    writeBackIndex: 0,
                    uncachedIndex: 3,
                    writeCombiningIndex: 4,
                    out X64PatMappingToken token);
            if (tokenStatus != X64PageTableEntryStatus.Success)
            {
                throw new InvalidOperationException(
                    "PAT fixture failed: " + tokenStatus);
            }
            return X64PageTableEncodingPreconditions.CreateObserved(
                physicalAddressWidth: 48,
                nxeEnabled: true,
                writeProtectEnabled: true,
                fiveLevelPagingEnabled: false,
                oneGiBPagesSupported: true,
                token);
        }

        internal static OwnedBootStackSwitchPlanResult
            CreateStackSwitchPlan(
                ulong stackCodeStart = 0x0050_0000,
                ulong stackBackingStart = StackArenaStart)
        {
            ulong page = BootMemoryRegionRecord.PageSize;
            PhysicalAddress backing = Physical(stackBackingStart);
            BootReservedRange* reservations =
                stackalloc BootReservedRange[2];
            if (BootReservedRange.TryCreate(
                    BootReservationKind.BootStackGuard,
                    backing,
                    StackGuardPages,
                    out reservations[0])
                    != BootReservedRangeStatus.Success
                || BootReservedRange.TryCreate(
                    BootReservationKind.BootStack,
                    Physical(stackBackingStart + page),
                    StackPages,
                    out reservations[1])
                    != BootReservedRangeStatus.Success)
            {
                return default;
            }

            OwnedBootStackLayoutResult reserved =
                OwnedBootStackLayout.Create(
                    reservations,
                    2,
                    2,
                    backing,
                    StackArenaPages,
                    Physical(
                        stackBackingStart
                        + StackArenaPages * page));
            if (!reserved.Succeeded)
                return default;
            OwnedBootStackLayoutResult ready =
                reserved.Layout.TransitionTo(
                    OwnedBootStackLifetime.Ready);
            if (!ready.Succeeded)
                return default;

            BootMemoryRegionRecord[] records =
            [
                Record(UefiMemoryType.LoaderCode, stackCodeStart, 1),
                Record(
                    UefiMemoryType.LoaderData,
                    stackBackingStart,
                    StackArenaPages)
            ];
            fixed (BootMemoryRegionRecord* pointer = records)
            {
                return OwnedBootStackSwitchPlan.Create(
                    pointer,
                    2,
                    2,
                    ready.Layout,
                    stubEntryOffset: 0,
                    declaredStubByteLength:
                        OwnedBootStackSwitchPlan
                            .RequiredStubByteLength,
                    handoffStorageAddress: Virtual(0x0070_0000),
                    handoffStorageByteLength:
                        OwnedBootStackSwitchHandoff.StructureSize,
                    handoffProvenance:
                        OwnedBootStackSwitchHandoffProvenance
                            .CallerAttestedStaticOwnedData,
                    stateStorageAddress: Virtual(0x0071_0000),
                    stateStorageByteLength:
                        OwnedBootStackSwitchStateRecord.StructureSize,
                    stateStorageProvenance:
                        OwnedBootStackSwitchStateStorageProvenance
                            .CallerAttestedStaticOwnedData,
                    continuationAddress: Virtual(0x0072_0010),
                    continuationContract:
                        OwnedBootStackSwitchContinuationContract
                            .StaticNonReturningRspAndHandoff);
            }
        }
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

    private static PageProtection Protection(
        PageProtection left,
        PageProtection right)
    {
        return (PageProtection)((byte)left | (byte)right);
    }

    private static PhysicalAddress Physical(ulong value)
    {
        return PhysicalAddress.TryCreateFromHardwareRegisterBoundary(
                value,
                out PhysicalAddress address)
                == AddressValueStatus.Success
            ? address
            : default;
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

    private static bool HasExactByteEnum<T>(
        (string Name, byte Value)[] expected)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
            return false;

        string[] names = Enum.GetNames<T>();
        T[] values = Enum.GetValues<T>();
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

    private static bool IsReadOnlySequentialUnmanaged<T>()
        where T : unmanaged
    {
        Type type = typeof(T);
        StructLayoutAttribute? layout = type.StructLayoutAttribute;
        return type.IsValueType
            && !RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            && type.IsDefined(
                typeof(IsReadOnlyAttribute),
                inherit: false)
            && layout is not null
            && layout.Value == LayoutKind.Sequential;
    }

    private static long Offset<T>(string fieldName)
        where T : struct
    {
        return Marshal.OffsetOf<T>(fieldName).ToInt64();
    }

    private static T MutateByte<T>(
        T value,
        int offset,
        byte replacement)
        where T : unmanaged
    {
        ((byte*)&value)[offset] = replacement;
        return value;
    }

    private static T MutateUInt64<T>(
        T value,
        int offset,
        ulong replacement)
        where T : unmanaged
    {
        *(ulong*)((byte*)&value + offset) = replacement;
        return value;
    }

    private static TOuter ReplaceValue<TOuter, TInner>(
        TOuter value,
        int offset,
        TInner replacement)
        where TOuter : unmanaged
        where TInner : unmanaged
    {
        *(TInner*)((byte*)&value + offset) = replacement;
        return value;
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

    private static VirtualAddress Virtual(ulong value)
    {
        return VirtualAddress.TryCreateFromHardwareRegisterBoundary(
                value,
                out VirtualAddress address)
                == AddressValueStatus.Success
            ? address
            : default;
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

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string NormalizeCompilerOutput(string value)
    {
        string normalized = Regex.Replace(
            value,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant).Trim();
        return normalized.Length <= 2000
            ? normalized
            : normalized[..2000];
    }

    private static string Normalize(string value)
    {
        return Regex.Replace(
            value.Trim().ToLowerInvariant(),
            @"[^a-z0-9]+",
            "_",
            RegexOptions.CultureInvariant).Trim('_');
    }
}
