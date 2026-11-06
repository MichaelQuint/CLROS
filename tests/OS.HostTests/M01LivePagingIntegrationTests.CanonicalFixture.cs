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

internal static unsafe partial class M01LivePagingIntegrationTests
{
    private sealed unsafe class CanonicalFixture : IDisposable
    {
        internal const ulong ValidCr0 = X64PagingModePreflightStubPlan.Cr0PagingBit | X64PagingModePreflightStubPlan.Cr0WriteProtectBit;
        internal const ulong ValidCr4 = X64PagingModePreflightStubPlan.Cr4PaeBit | X64ReversibleCr3TransitionStubPlan.Cr4PageGlobalEnableBit;
        internal const ulong ValidEfer = X64PagingModePreflightStubPlan.EferLongModeEnableBit | X64PagingModePreflightStubPlan.EferLongModeActiveBit | X64PagingModePreflightStubPlan.EferNoExecuteEnableBit;
        internal const ulong ValidRflags = 0x202;
        internal const ulong InitialCanary = 0x1122_3344_5566_7788UL;
        internal const ulong UpdatedCanary = 0x8877_6655_4433_2211UL;
        internal const ulong ProtectionCanary = 0x434c_524f_535f_4f4bUL;
        internal const ulong StageThreeMapGeneration = 10;
        internal const ulong PageTableCoverageBytes = 1UL << 21;
        private const ulong BackingPages = 128;
        private const ulong BackingBytes = BackingPages * BootReservedRange.PageSize;
        private const ulong LoadedImagePages = 3;
        private const ulong LoadedImageBytes = LoadedImagePages * BootReservedRange.PageSize;
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
        private const ulong ContinuationOffset = BootReservedRange.PageSize + 256;
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
        private FixtureMappingProfile _mappingProfile;
        private BootReservedRange[] _legacyStageThreeReservations = [];
        private BootReservedRange[] _liveIntervalStageThreeReservations = [];
        private X64BootIdentityMappingRequirement[] _legacyMappingRequirements = [];
        private X64BootIdentityMappingRequirement[] _liveIntervalMappingRequirements = [];
        private X64BootIdentityMappingRange[] _legacyMappingRanges = [];
        private X64BootIdentityMappingRange[] _liveIntervalMappingRanges = [];
        private X64BootIdentityMappingPlanResult _legacyMapping;
        private X64BootIdentityMappingPlanResult _liveIntervalMapping;
        internal CanonicalFixture()
        {
            try
            {
                _backing = (byte*)NativeMemory.AlignedAlloc((nuint)BackingBytes, (nuint)PageTableCoverageBytes);
                if (_backing == null)
                    throw new OutOfMemoryException();
                Clear(_backing, BackingBytes);
                _stageOneRecords = (BootMemoryRegionRecord*)AtPage(StageOneAllocationTablePage);
                _stageOneLedger = (BootReservedRange*)AtPage(StageOneLedgerPage);
                _stageTwoRecords = (BootMemoryRegionRecord*)AtPage(StageTwoAllocationTablePage);
                _stageTwoLedger = (BootReservedRange*)AtPage(StageTwoLedgerPage);
                StageOneState = (M01LoadedImageStackStaticState*)(_backing + StageOneStaticStateOffset);
                StageTwoState = (M01LiveAllocatorStaticState*)AtPage(StageTwoStaticStatePage);
                Records = (BootMemoryRegionRecord*)AtPage(StageThreeAllocationTablePage);
                Ledger = (BootReservedRange*)AtPage(StageThreeLedgerPage);
                StaticState = (M01LivePagingStaticState*)AtPage(StageThreeStaticStatePage);
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
            get; private set;
        }
        internal ulong BackingAddress => (ulong)_backing;
        internal ulong BackingByteLength => BackingBytes;
        internal ulong LegacyTablePageCount => _legacyMapping.TotalTablePageCount;
        internal ulong LiveIntervalTablePageCount => _liveIntervalMapping.TotalTablePageCount;
        internal M01LiveAllocatorStaticState* StageTwoState
        {
            get; private set;
        }
        internal BootMemoryRegionRecord* Records
        {
            get; private set;
        }
        internal BootReservedRange* Ledger
        {
            get; private set;
        }
        internal M01LivePagingStaticState* StaticState
        {
            get; private set;
        }
        internal M01LivePagingPreparationState* PreparationState => (M01LivePagingPreparationState*)StaticState;
        internal byte* CodePage
        {
            get; private set;
        }
        internal byte* ProbePage
        {
            get; private set;
        }
        internal ulong ProbeStart => (ulong)ProbePage;
        internal ulong TableStateStart => Records[M01LivePagingIntegration.TableStateAllocationRecordIndex].PhysicalStart;
        internal ulong RawFirmwareCr3
        {
            get; private set;
        }
        internal ulong LedgerCount
        {
            get; private set;
        }
        internal ulong LedgerCapacity => StageThreeLedgerCapacity;
        internal FirmwareMemoryMapRecord StageThreeMap
        {
            get; private set;
        }
        internal OwnedBootStackSwitchPlanResult StackSwitchPlan
        {
            get; private set;
        }
        internal M01LivePagingAllocationEvidence AllocationEvidence
        {
            get; private set;
        }
        internal M01LivePagingMapLedgerEvidence MapEvidence
        {
            get; private set;
        }
        internal X64PageTableEncodingPreconditions Preconditions
        {
            get;
        }
        internal X64BootIdentityMappingRequirement[] MappingRequirements { get; private set; } = [];
        internal X64BootIdentityMappingRange[] MappingRanges { get; private set; } = [];
        internal X64BootIdentityMappingPlanResult Mapping
        {
            get; private set;
        }
        internal X64PageTableImagePage[] PageImages { get; private set; } = [];
        internal ulong[] Entries { get; private set; } = [];
        internal ulong MappingRequirementCount => (ulong)MappingRequirements.Length;
        internal ulong MappingRequirementCapacity => (ulong)MappingRequirements.Length;
        internal ulong MappingRangeCapacity => (ulong)MappingRanges.Length;
        internal ulong PageImageCapacity => (ulong)PageImages.Length;
        internal ulong EntryCapacity => (ulong)Entries.Length;
        internal FixtureMappingProfile MappingProfile => _mappingProfile;

        internal void UseLiveIntervalProfile()
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
        }

        internal X64PagingModePreflightObservation PreflightObservation;
        internal X64ReversibleCr3TransitionRuntimeState* TransitionState => (X64ReversibleCr3TransitionRuntimeState*)(Records[M01LivePagingIntegration.TableStateAllocationRecordIndex].VirtualStart + Mapping.TotalTableByteLength);
        internal X64Cr3ProbeEvent[] Cr3Events { get; private set; } = [];

        internal X64ProtectionProbeObservation ProtectionObservation;
        internal X64ProtectionProbeEvent[] ProtectionEvents { get; private set; } = [];
        internal X64Cr3ProbePolicy Cr3Policy
        {
            get; private set;
        }

        internal M01LivePagingIntegrationResult ExecuteCurrent(M01LivePagingByteMode byteMode = M01LivePagingByteMode.EmitAndVerify)
        {
            ActivateMappingProfile(FixtureMappingProfile.LegacyExecute);
            return ExecuteCore(StageTwoState, M01LiveAllocatorStaticState.RequiredStorageBytes, StackSwitchPlan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, MappingRequirements, MappingRequirementCount, MappingRequirementCapacity, MappingRanges, MappingRangeCapacity, PageImages, PageImageCapacity, Entries, EntryCapacity, CodePage, BootReservedRange.PageSize, ProbePage, BootReservedRange.PageSize, byteMode, StaticState, M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal M01LivePagingPreparationResult PrepareCurrent(M01LivePagingByteMode byteMode = M01LivePagingByteMode.EmitAndVerify)
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            fixed (X64PagingModePreflightObservation* preflight = &PreflightObservation)
            {
                return PrepareCore(StageTwoState, 1, 1, StackSwitchPlan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, MappingRequirements, MappingRequirementCount, MappingRequirementCapacity, MappingRanges, 0, MappingRangeCapacity, PageImages, 0, PageImageCapacity, Entries, 0, EntryCapacity, CodePage, BootReservedRange.PageSize, ProbePage, BootReservedRange.PageSize, byteMode, PreparationState, 1, 1, preflight, 1, 1);
            }
        }

        internal M01LivePagingPreparationResult PrepareRequirements(X64BootIdentityMappingRequirement[] requirements)
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            fixed (X64PagingModePreflightObservation* preflight = &PreflightObservation)
            {
                return PrepareCore(StageTwoState, 1, 1, StackSwitchPlan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, requirements, (ulong)requirements.Length, (ulong)requirements.Length, MappingRanges, 0, MappingRangeCapacity, PageImages, 0, PageImageCapacity, Entries, 0, EntryCapacity, CodePage, BootReservedRange.PageSize, ProbePage, BootReservedRange.PageSize, M01LivePagingByteMode.EmitAndVerify, PreparationState, 1, 1, preflight, 1, 1);
            }
        }

        internal M01LivePagingPreparationResult PrepareStageTwo(M01LiveAllocatorStaticState* stageTwo, ulong count, ulong capacity)
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            fixed (X64PagingModePreflightObservation* preflight = &PreflightObservation)
            {
                return PrepareMetadata(stageTwo, count, capacity, StackSwitchPlan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, preflight, 1, 1, PreparationState, 1, 1);
            }
        }

        internal M01LivePagingPreparationResult PrepareAllocation(BootMemoryRegionRecord* records, ulong count, ulong capacity, M01LivePagingAllocationEvidence evidence)
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            fixed (X64PagingModePreflightObservation* preflight = &PreflightObservation)
            {
                return PrepareMetadata(StageTwoState, 1, 1, StackSwitchPlan, records, count, capacity, evidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, preflight, 1, 1, PreparationState, 1, 1);
            }
        }

        internal M01LivePagingPreparationResult PrepareMapLedger(M01LivePagingMapLedgerEvidence evidence, BootReservedRange* ledger, ulong count, ulong capacity)
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            fixed (X64PagingModePreflightObservation* preflight = &PreflightObservation)
            {
                return PrepareMetadata(StageTwoState, 1, 1, StackSwitchPlan, Records, 3, 3, AllocationEvidence, evidence, ledger, count, capacity, preflight, 1, 1, PreparationState, 1, 1);
            }
        }

        internal M01LivePagingPreparationResult PreparePreflight(X64PagingModePreflightObservation* observation, ulong count, ulong capacity)
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            return PrepareMetadata(StageTwoState, 1, 1, StackSwitchPlan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, observation, count, capacity, PreparationState, 1, 1);
        }

        internal M01LivePagingPreparationResult PrepareState(M01LivePagingPreparationState* state, ulong count, ulong capacity)
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            fixed (X64PagingModePreflightObservation* preflight = &PreflightObservation)
            {
                return PrepareMetadata(StageTwoState, 1, 1, StackSwitchPlan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, preflight, 1, 1, state, count, capacity);
            }
        }

        internal M01LivePagingPreparationResult PrepareStateAliasesPreflight()
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            fixed (X64PagingModePreflightObservation* preflight = &PreflightObservation)
            {
                return PrepareMetadata(StageTwoState, 1, 1, StackSwitchPlan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, preflight, 1, 1, (M01LivePagingPreparationState*)preflight, 1, 1);
            }
        }

        internal M01LivePagingIntegrationResult ExecuteRequirements(X64BootIdentityMappingRequirement[] requirements)
        {
            ActivateMappingProfile(FixtureMappingProfile.LegacyExecute);
            return ExecuteWorkspace(requirements, (ulong)requirements.Length, (ulong)requirements.Length, MappingRanges, MappingRangeCapacity, PageImages, PageImageCapacity, Entries, EntryCapacity, CodePage, BootReservedRange.PageSize, ProbePage, BootReservedRange.PageSize);
        }

        internal M01LivePagingIntegrationResult ExecuteWithAllocation(M01LivePagingAllocationEvidence evidence, BootMemoryRegionRecord* records, ulong count, ulong capacity)
        {
            ActivateMappingProfile(FixtureMappingProfile.LegacyExecute);
            return ExecuteCore(StageTwoState, M01LiveAllocatorStaticState.RequiredStorageBytes, StackSwitchPlan, records, count, capacity, evidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, MappingRequirements, MappingRequirementCount, MappingRequirementCapacity, MappingRanges, MappingRangeCapacity, PageImages, PageImageCapacity, Entries, EntryCapacity, CodePage, BootReservedRange.PageSize, ProbePage, BootReservedRange.PageSize, M01LivePagingByteMode.EmitAndVerify, StaticState, M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal M01LivePagingIntegrationResult ExecuteWithMap(M01LivePagingMapLedgerEvidence evidence)
        {
            ActivateMappingProfile(FixtureMappingProfile.LegacyExecute);
            return ExecuteCore(StageTwoState, M01LiveAllocatorStaticState.RequiredStorageBytes, StackSwitchPlan, Records, 3, 3, AllocationEvidence, evidence, Ledger, LedgerCount, LedgerCapacity, MappingRequirements, MappingRequirementCount, MappingRequirementCapacity, MappingRanges, MappingRangeCapacity, PageImages, PageImageCapacity, Entries, EntryCapacity, CodePage, BootReservedRange.PageSize, ProbePage, BootReservedRange.PageSize, M01LivePagingByteMode.EmitAndVerify, StaticState, M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal M01LivePagingIntegrationResult ExecuteWithStack(OwnedBootStackSwitchPlanResult plan)
        {
            ActivateMappingProfile(FixtureMappingProfile.LegacyExecute);
            return ExecuteCore(StageTwoState, M01LiveAllocatorStaticState.RequiredStorageBytes, plan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, MappingRequirements, MappingRequirementCount, MappingRequirementCapacity, MappingRanges, MappingRangeCapacity, PageImages, PageImageCapacity, Entries, EntryCapacity, CodePage, BootReservedRange.PageSize, ProbePage, BootReservedRange.PageSize, M01LivePagingByteMode.EmitAndVerify, StaticState, M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal M01LivePagingIntegrationResult ExecuteMetadata(M01LiveAllocatorStaticState* stageTwo, ulong stageTwoCapacity, OwnedBootStackSwitchPlanResult stackPlan, BootMemoryRegionRecord* records, ulong recordCount, ulong recordCapacity, M01LivePagingAllocationEvidence allocations, M01LivePagingMapLedgerEvidence map, BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, M01LivePagingStaticState* state, ulong stateCapacity)
        {
            ActivateMappingProfile(FixtureMappingProfile.LegacyExecute);
            return ExecuteCore(stageTwo, stageTwoCapacity, stackPlan, records, recordCount, recordCapacity, allocations, map, ledger, ledgerCount, ledgerCapacity, MappingRequirements, MappingRequirementCount, MappingRequirementCapacity, MappingRanges, MappingRangeCapacity, PageImages, PageImageCapacity, Entries, EntryCapacity, CodePage, BootReservedRange.PageSize, ProbePage, BootReservedRange.PageSize, M01LivePagingByteMode.EmitAndVerify, state, stateCapacity);
        }

        internal M01LivePagingPreparationResult PrepareMetadata(M01LiveAllocatorStaticState* stageTwo, ulong stageTwoCount, ulong stageTwoCapacity, OwnedBootStackSwitchPlanResult stackPlan, BootMemoryRegionRecord* records, ulong recordCount, ulong recordCapacity, M01LivePagingAllocationEvidence allocations, M01LivePagingMapLedgerEvidence map, BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, X64PagingModePreflightObservation* preflight, ulong preflightCount, ulong preflightCapacity, M01LivePagingPreparationState* state, ulong stateCount, ulong stateCapacity)
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            return PrepareCore(stageTwo, stageTwoCount, stageTwoCapacity, stackPlan, records, recordCount, recordCapacity, allocations, map, ledger, ledgerCount, ledgerCapacity, MappingRequirements, MappingRequirementCount, MappingRequirementCapacity, MappingRanges, 0, MappingRangeCapacity, PageImages, 0, PageImageCapacity, Entries, 0, EntryCapacity, CodePage, BootReservedRange.PageSize, ProbePage, BootReservedRange.PageSize, M01LivePagingByteMode.EmitAndVerify, state, stateCount, stateCapacity, preflight, preflightCount, preflightCapacity);
        }

        internal M01LivePagingIntegrationResult ExecuteWorkspace(X64BootIdentityMappingRequirement[]? requirements, ulong requirementCount, ulong requirementCapacity, X64BootIdentityMappingRange[]? ranges, ulong rangeCapacity, X64PageTableImagePage[]? pages, ulong pageCapacity, ulong[]? entries, ulong entryCapacity, byte* codePage, ulong codeCapacity, byte* probePage, ulong probeCapacity, bool aliasPageStorage = false)
        {
            ActivateMappingProfile(FixtureMappingProfile.LegacyExecute);
            fixed (X64BootIdentityMappingRequirement* requirementPointer = requirements)
            fixed (X64BootIdentityMappingRange* rangePointer = ranges)
            fixed (X64PageTableImagePage* pagePointer = pages)
            fixed (ulong* entryPointer = entries)
            {
                return ExecuteCoreRawWorkspace(StageTwoState, M01LiveAllocatorStaticState.RequiredStorageBytes, StackSwitchPlan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, requirementPointer, requirementCount, requirementCapacity, rangePointer, rangeCapacity, aliasPageStorage ? (X64PageTableImagePage*)entryPointer : pagePointer, pageCapacity, entryPointer, entryCapacity, codePage, codeCapacity, probePage, probeCapacity, M01LivePagingByteMode.EmitAndVerify, StaticState, M01LivePagingStaticState.RequiredStorageBytes);
            }
        }

        internal M01LivePagingAllocationEvidence CaptureAllocationEvidence(ulong codeOrdinal, ulong probeOrdinal, ulong tableOrdinal, ulong codeStatus = 0, ulong probeStatus = 0, ulong tableStatus = 0, BootMemoryRegionRecord* records = null, ulong count = 3, ulong capacity = 3)
        {
            return M01LivePagingAllocationEvidence.CaptureCallerAttested(records == null ? Records : records, count, capacity, codeOrdinal, probeOrdinal, tableOrdinal, codeStatus, probeStatus, tableStatus);
        }

        internal void ReplaceAllocationRecord(int index, BootMemoryRegionRecord replacement)
        {
            if ((uint)index >= 3)
                throw new ArgumentOutOfRangeException(nameof(index));
            Records[index] = replacement;
            AllocationEvidence = CaptureAllocationEvidence(Cr3CodeAllocationOrdinal, ProbeAllocationOrdinal, TableStateAllocationOrdinal);
            MapEvidence = CaptureMapEvidence();
        }

        internal void CorruptAllocationEvidenceWithoutRefreshing()
        {
            AllocationEvidence = CaptureAllocationEvidence(Cr3CodeAllocationOrdinal, ProbeAllocationOrdinal + 1, TableStateAllocationOrdinal + 1);
        }

        internal M01LivePagingMapLedgerEvidence CaptureMapEvidence(ulong? previousMapKey = null, ulong? previousMapGeneration = null, FirmwareMemoryMapRecord? refreshedMap = null, ulong? refreshedMapGeneration = null, ulong mapCaptureOrdinal = StageThreeMapCaptureOrdinal, ulong ledgerBuildOrdinal = StageThreeLedgerBuildOrdinal, BootReservedRange* ledger = null, ulong? ledgerCount = null, ulong? ledgerCapacity = null)
        {
            return M01LivePagingMapLedgerEvidence.CaptureCallerAttested(previousMapKey ?? StageTwoState->RefreshedMapKey, previousMapGeneration ?? StageTwoState->RefreshedMapGeneration, refreshedMap ?? StageThreeMap, refreshedMapGeneration ?? StageThreeMapGeneration, AllocationEvidence, mapCaptureOrdinal, ledgerBuildOrdinal, ledger == null ? Ledger : ledger, ledgerCount ?? LedgerCount, ledgerCapacity ?? LedgerCapacity);
        }

        internal M01LivePagingMapLedgerEvidence RefreshMapEvidence()
        {
            MapEvidence = CaptureMapEvidence();
            return MapEvidence;
        }

        internal BootReservedRange FindReservation(BootReservationKind kind)
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
            BootReservedRange[] copy = new BootReservedRange[checked((int)LedgerCount)];
            for (int index = 0; index < copy.Length; index++)
                copy[index] = Ledger[index];
            return copy;
        }

        internal void RestoreLedger(BootReservedRange[] ledger)
        {
            Clear((byte*)Ledger, LedgerCapacity * (ulong)sizeof(BootReservedRange));
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

        internal void ReplaceReservation(BootReservationKind kind, BootReservedRange replacement)
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
            Clear((byte*)StaticState, M01LivePagingStaticState.RequiredStorageBytes);
        }

        internal void ClearPreparationState()
        {
            Clear((byte*)PreparationState, M01LivePagingPreparationState.RequiredStorageBytes);
        }

        internal bool PreparationStateIsZero()
        {
            byte* state = (byte*)PreparationState;
            for (ulong index = 0; index < M01LivePagingPreparationState.RequiredStorageBytes; index++)
            {
                if (state[index] != 0)
                    return false;
            }

            return true;
        }

        internal void FillStubPages(byte value)
        {
            for (ulong index = 0; index < BootReservedRange.PageSize; index++)
            {
                CodePage[index] = value;
                ProbePage[index] = value;
            }
        }

        internal bool PaddingIsZero()
        {
            ulong preflightEnd = M01LivePagingIntegration.PreflightEntryOffset + X64PagingModePreflightStubPlan.RequiredByteLength;
            ulong transitionEnd = M01LivePagingIntegration.TransitionEntryOffset + X64ReversibleCr3TransitionStubPlan.RequiredByteLength;
            ulong invlpgEnd = M01LivePagingIntegration.InvlpgEntryOffset + X64InvlpgStubPlan.RequiredByteLength;
            ulong probeEnd = M01LivePagingIntegration.ProtectionProbeEntryOffset + X64ProtectionProbeStubPlan.RequiredByteLength;
            for (ulong index = 0; index < BootReservedRange.PageSize; index++)
            {
                bool codeByte = (index >= M01LivePagingIntegration.PreflightEntryOffset && index < preflightEnd) || (index >= M01LivePagingIntegration.TransitionEntryOffset && index < transitionEnd) || (index >= M01LivePagingIntegration.InvlpgEntryOffset && index < invlpgEnd);
                if (!codeByte && CodePage[index] != 0)
                    return false;
                bool probeByte = index >= M01LivePagingIntegration.ProtectionProbeEntryOffset && index < probeEnd;
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

        internal void RestoreStubPages(byte[] code, byte[] probe)
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

        internal OwnedBootStackSwitchPlanResult CreateMismatchedStackPlan()
        {
            return MismatchedStackPlan;
        }

        internal X64Cr3ProbeEvent CreateCr3Event(X64Cr3ProbeEventKind kind, bool ownedRoot, bool updatedCanary, ulong elapsed)
        {
            return new X64Cr3ProbeEvent(kind, ownedRoot ? TableStateStart : RawFirmwareCr3, ProbeRsp, updatedCanary ? UpdatedCanary : InitialCanary, elapsed);
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
            get; set;
        }
        private VirtualAddress ProbeRsp
        {
            get; set;
        }

        private byte* AtPage(ulong page)
        {
            return _backing + page * BootReservedRange.PageSize;
        }

        private void ActivateMappingProfile(FixtureMappingProfile profile)
        {
            if (_mappingProfile == profile)
                return;
            if (profile == FixtureMappingProfile.LegacyExecute)
            {
                MappingRequirements = _legacyMappingRequirements;
                MappingRanges = _legacyMappingRanges;
                Mapping = _legacyMapping;
                WriteStageThreeLedger((BootReservedRange[])_legacyStageThreeReservations.Clone());
            }
            else if (profile == FixtureMappingProfile.ExactLiveIntervalPreparation)
            {
                MappingRequirements = _liveIntervalMappingRequirements;
                MappingRanges = _liveIntervalMappingRanges;
                Mapping = _liveIntervalMapping;
                WriteStageThreeLedger((BootReservedRange[])_liveIntervalStageThreeReservations.Clone());
            }
            else
            {
                throw new InvalidOperationException("unsupported fixture mapping profile");
            }

            _mappingProfile = profile;
            if (AllocationEvidence.Size != 0)
                MapEvidence = CaptureMapEvidence();
        }

        private void BuildStageOneAndTwo()
        {
            EfiLoadedImageProtocolObservation observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 1, (ulong)_backing, (ulong)_backing, LoadedImageBytes);
            EfiLoadedImageRangeCapturePlan loadedImage = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, EfiLoadedImageRange.PageSize);
            if (!loadedImage.Succeeded)
                throw new InvalidOperationException("stage-one loaded image: " + loadedImage.Status);
            byte* stageOneCode = AtPage(StageOneCodePage);
            byte* stageOneData = AtPage(StageOneDataPage);
            _stageOneRecords[0] = Record(UefiMemoryType.LoaderCode, (ulong)stageOneCode, 1);
            _stageOneRecords[1] = Record(UefiMemoryType.LoaderData, (ulong)stageOneData, StackArenaPages);
            _stageOneLedger[0] = loadedImage.Range.Reservation;
            _stageOneLedger[1] = CreateRange(BootReservationKind.BootStackSwitchStub, (ulong)stageOneCode, 1);
            _stageOneLedger[2] = CreateRange(BootReservationKind.BootStackGuard, (ulong)stageOneData, GuardPages);
            _stageOneLedger[3] = CreateRange(BootReservationKind.BootStack, (ulong)stageOneData + GuardPages * BootReservedRange.PageSize, StackPages);
            FirmwareMemoryMapRecord stageOneMap = CreateOwnedMap(AtPage(StageOneMapDestinationPage), AtPage(StageOneMapSourcePage), StageOneMapKey);
            M01StageOneAllocationEvidence stageOneAllocation = M01StageOneAllocationEvidence.CaptureCallerAttested(_stageOneRecords, 2, 2, StageOneCodeAllocationOrdinal, StageOneDataAllocationOrdinal, M01StageOneAllocationEvidence.EfiSuccess, M01StageOneAllocationEvidence.EfiSuccess);
            M01StageOneMapLedgerEvidence stageOneMapLedger = M01StageOneMapLedgerEvidence.CaptureCallerAttested(StageOnePreviousMapKey, StageOnePreviousMapGeneration, stageOneMap, StageOneMapGeneration, stageOneAllocation, StageOneMapCaptureOrdinal, StageOneLedgerBuildOrdinal, _stageOneLedger, 4, StageOneLedgerCapacity);
            M01LoadedImageStackIntegrationResult prepared = M01LoadedImageStackIntegration.PrepareWithStubByteMode(loadedImage, M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(loadedImage), M01StageOneAddressMappingEvidence.CaptureCallerAttestedIdentity(_stageOneRecords, 2, 2, stageOneMap), _stageOneRecords, 2, 2, stageOneAllocation, stageOneMapLedger, _stageOneLedger, 4, StageOneLedgerCapacity, stageOneCode, BootReservedRange.PageSize, BootReservedRange.PageSize, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Virtual((ulong)_backing + ContinuationOffset), StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes, M01StageOneStubByteMode.EmitAndVerify);
            if (!prepared.IsPrepared)
            {
                throw new InvalidOperationException("stage-one prepare: " + prepared.Status);
            }

            StackSwitchPlan = prepared.Evidence.SwitchPlanResult;
            MismatchedStackPlan = OwnedBootStackSwitchPlan.Create(_stageOneRecords, 2, 2, prepared.Evidence.StackLayoutResult.Layout, StubEntryOffset, OwnedBootStackSwitchPlan.RequiredStubByteLength, Virtual((ulong)StageOneState + M01LoadedImageStackStaticState.HandoffStorageOffset), OwnedBootStackSwitchHandoff.StructureSize, OwnedBootStackSwitchHandoffProvenance.CallerAttestedStaticOwnedData, Virtual((ulong)StageOneState + M01LoadedImageStackStaticState.SwitchStateStorageOffset), OwnedBootStackSwitchStateRecord.StructureSize, OwnedBootStackSwitchStateStorageProvenance.CallerAttestedStaticOwnedData, Virtual((ulong)_backing + ContinuationOffset + 16), OwnedBootStackSwitchContinuationContract.StaticNonReturningRspAndHandoff);
            if (!MismatchedStackPlan.Succeeded)
            {
                throw new InvalidOperationException("mismatch stack fixture: " + MismatchedStackPlan.Status);
            }

            M01LoadedImageStackStaticTransitionResult transfer = M01LoadedImageStackIntegration.RecordTransferFromStaticState(StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes);
            if (!transfer.TransferRecorded)
            {
                throw new InvalidOperationException("stage-one transfer: " + transfer.Status);
            }

            M01LoadedImageStackStaticTransitionResult observed = M01LoadedImageStackIntegration.ObserveContinuationRspFromStaticState(StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes, transfer.StateResult.Plan.ExpectedContinuationStackPointer);
            if (!observed.ContinuationRspContained)
            {
                throw new InvalidOperationException("stage-one continuation: " + observed.Status);
            }

            ulong arenaStart = (ulong)AtPage(LiveArenaPage);
            const ulong arenaPages = 4;
            _stageTwoRecords[0] = Record(UefiMemoryType.LoaderData, arenaStart, arenaPages);
            BootReservedRange[] stageTwoReservations = [loadedImage.Range.Reservation, _stageOneLedger[1], _stageOneLedger[2], _stageOneLedger[3], CreateRange(BootReservationKind.AllocatorArena, arenaStart, arenaPages)];
            WriteLedger(_stageTwoLedger, StageTwoLedgerCapacity, stageTwoReservations);
            FirmwareMemoryMapRecord stageTwoMap = CreateOwnedMap(AtPage(StageTwoMapDestinationPage), AtPage(StageTwoMapSourcePage), StageTwoMapKey);
            M01LiveAllocatorAllocationEvidence stageTwoAllocation = M01LiveAllocatorAllocationEvidence.CaptureCallerAttested(_stageTwoRecords, 1, 1, ArenaAllocationOrdinal, M01LiveAllocatorAllocationEvidence.EfiSuccess);
            M01LiveAllocatorMapLedgerEvidence stageTwoMapLedger = M01LiveAllocatorMapLedgerEvidence.CaptureCallerAttested(StageOneMapKey, StageOneMapGeneration, stageTwoMap, StageTwoMapGeneration, stageTwoAllocation, StageTwoMapCaptureOrdinal, StageTwoLedgerBuildOrdinal, _stageTwoLedger, (ulong)stageTwoReservations.Length, StageTwoLedgerCapacity);
            M01LiveAllocatorIntegrationResult stageTwo = M01LiveAllocatorIntegration.Execute(StageOneState, M01LoadedImageStackStaticState.RequiredStorageBytes, _stageTwoRecords, 1, 1, stageTwoAllocation, stageTwoMapLedger, _stageTwoLedger, (ulong)stageTwoReservations.Length, StageTwoLedgerCapacity, StageTwoState, M01LiveAllocatorStaticState.RequiredStorageBytes);
            if (!stageTwo.Succeeded || !StageTwoState->IsCompleted)
            {
                throw new InvalidOperationException("stage-two fixture: " + stageTwo.Status);
            }
        }

        private void BuildStageThree()
        {
            BuildMappingPlan(out _legacyMappingRequirements, out _legacyMappingRanges, out _legacyMapping);
            BuildLiveIntervalMappingPlan(out _liveIntervalMappingRequirements, out _liveIntervalMappingRanges, out _liveIntervalMapping);
            if (!_legacyMapping.Succeeded || !_liveIntervalMapping.Succeeded)
            {
                throw new InvalidOperationException("stage-three mapping: legacy=" + _legacyMapping.Status + " live=" + _liveIntervalMapping.Status);
            }

            if (_legacyMapping.TotalTablePageCount != _liveIntervalMapping.TotalTablePageCount)
            {
                throw new InvalidOperationException("stage-three mapping table geometry differs: legacy=" + _legacyMapping.TotalTablePageCount + " live=" + _liveIntervalMapping.TotalTablePageCount);
            }

            Records[0] = Record(UefiMemoryType.LoaderCode, (ulong)CodePage, 1);
            Records[1] = Record(UefiMemoryType.LoaderCode, (ulong)ProbePage, 1);
            Records[2] = Record(UefiMemoryType.LoaderData, (ulong)AtPage(StageThreeTablePage), _liveIntervalMapping.TotalTablePageCount + 1);
            PageImages = GC.AllocateArray<X64PageTableImagePage>(checked((int)X64PageMap.MaximumPageImages), pinned: true);
            Entries = GC.AllocateArray<ulong>(checked((int)(_liveIntervalMapping.TotalTablePageCount * X64PageTableImagePage.EntryCountPerPage)), pinned: true);
            BootReservedRange[] legacyReservations = new BootReservedRange[10];
            for (int index = 0; index < 5; index++)
                legacyReservations[index] = _stageTwoLedger[index];
            legacyReservations[5] = CreateRange(BootReservationKind.AcpiScratch, (ulong)AtPage(AcpiRequirementPage), 1);
            legacyReservations[6] = CreateRange(BootReservationKind.FramebufferMetadata, (ulong)AtPage(FramebufferRequirementPage), 1);
            legacyReservations[7] = CreateRange(BootReservationKind.Cr3TransitionStub, Records[0].PhysicalStart, Records[0].PageCount);
            legacyReservations[8] = CreateRange(BootReservationKind.PageProtectionProbe, Records[1].PhysicalStart, Records[1].PageCount);
            legacyReservations[9] = CreateRange(BootReservationKind.PageTableArena, Records[2].PhysicalStart, Records[2].PageCount);
            _legacyStageThreeReservations = legacyReservations;
            BootReservedRange[] liveIntervalReservations = new BootReservedRange[8];
            for (int index = 0; index < 5; index++)
                liveIntervalReservations[index] = _stageTwoLedger[index];
            liveIntervalReservations[5] = legacyReservations[7];
            liveIntervalReservations[6] = legacyReservations[8];
            liveIntervalReservations[7] = legacyReservations[9];
            _liveIntervalStageThreeReservations = liveIntervalReservations;
            ActivateMappingProfile(FixtureMappingProfile.LegacyExecute);
            StageThreeMap = CreateOwnedMap(AtPage(StageThreeMapDestinationPage), AtPage(StageThreeMapSourcePage), 0x0030);
            AllocationEvidence = CaptureAllocationEvidence(Cr3CodeAllocationOrdinal, ProbeAllocationOrdinal, TableStateAllocationOrdinal);
            MapEvidence = CaptureMapEvidence();
            RawFirmwareCr3 = (ulong)AtPage(FirmwareRootPage) | 0x18UL;
            PreflightObservation = X64PagingModePreflightObservation.CreateObserved(ValidCr0, RawFirmwareCr3, ValidCr4, ValidEfer, X64PagingModePreflightStubPlan.CpuidAddressSizesLeaf, 48, ValidRflags);
            ulong stackStart = Raw(StackSwitchPlan.Plan.UsableStackVirtualStart);
            ulong stackEnd = Raw(StackSwitchPlan.Plan.UsableStackVirtualEndExclusive);
            ulong rsp = Raw(StackSwitchPlan.Plan.ExpectedContinuationStackPointer) - 64;
            ProbeRsp = Virtual(rsp);
            ulong clearedCr4 = ValidCr4 & ~X64ReversibleCr3TransitionStubPlan.Cr4PageGlobalEnableBit;
            *TransitionState = X64ReversibleCr3TransitionRuntimeState.CreateObserved(TableStateStart, stackStart, stackEnd, (ulong)TransitionState + X64ReversibleCr3TransitionRuntimeState.StructureSize, InitialCanary, UpdatedCanary, ValidRflags, RawFirmwareCr3, ValidCr4, clearedCr4, TableStateStart, InitialCanary, UpdatedCanary, RawFirmwareCr3, ValidCr4, (ValidRflags & ~X64ReversibleCr3TransitionStubPlan.RflagsInterruptEnableBit) ^ 1UL, ValidRflags, rsp, rsp, UpdatedCanary, 0, X64ReversibleCr3TransitionRuntimeStatus.Success, X64ReversibleCr3TransitionCheckpoint.Returned);
            Cr3Policy = new X64Cr3ProbePolicy(enabledByDefault: false, explicitOneShotOptIn: true, timeoutTicks: 1000);
            Cr3Events = CreateCr3Events();
            ProtectionObservation = X64ProtectionProbeObservation.CreateModeled(X64ProtectionProbeTerminalKind.ModeledReturn, Virtual(ProbeStart), 1, X64ProtectionProbeStubPlan.ExpectedReturnValue, ProtectionCanary, ProtectionCanary);
            ProtectionEvents = new X64ProtectionProbeEvent[X64ProtectionProbeStubPlan.RequiredEventCount];
            for (int index = 0; index < ProtectionEvents.Length; index++)
            {
                ProtectionEvents[index] = new X64ProtectionProbeEvent((X64ProtectionProbeEventKind)(index + 1), (ulong)index);
            }
        }

        private void BuildMappingPlan(out X64BootIdentityMappingRequirement[] requirements, out X64BootIdentityMappingRange[] ranges, out X64BootIdentityMappingPlanResult result)
        {
            ulong dataPages = 5;
            requirements = [];
            ranges = [];
            result = default;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                requirements = RequirementsFor(dataPages);
                ranges = new X64BootIdentityMappingRange[X64BootIdentityMappingPlan.MaximumDestinationCount];
                fixed (X64BootIdentityMappingRequirement* source = requirements)
                fixed (X64BootIdentityMappingRange* destination = ranges)
                {
                    result = X64BootIdentityMappingPlan.Create(source, (ulong)requirements.Length, (ulong)requirements.Length, destination, (ulong)ranges.Length, X64PageMap.MaximumPageImages, Preconditions);
                }

                if (!result.Succeeded)
                    return;
                ulong exactPages = result.TotalTablePageCount + 1;
                if (exactPages == dataPages)
                    return;
                dataPages = exactPages;
            }

            throw new InvalidOperationException("stage-three page accounting did not converge");
        }

        private void BuildLiveIntervalMappingPlan(out X64BootIdentityMappingRequirement[] requirements, out X64BootIdentityMappingRange[] ranges, out X64BootIdentityMappingPlanResult result)
        {
            ulong tableStatePages = 5;
            requirements = [];
            ranges = [];
            result = default;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                requirements = AllocatePinnedCopy(RequirementsForLiveInterval(tableStatePages));
                ranges = GC.AllocateArray<X64BootIdentityMappingRange>(checked((int)X64BootIdentityMappingPlan.MaximumDestinationCount), pinned: true);
                fixed (X64BootIdentityMappingRequirement* source = requirements)
                fixed (X64BootIdentityMappingRange* destination = ranges)
                {
                    result = X64BootIdentityMappingPlan.CreateLiveInterval(source, (ulong)requirements.Length, (ulong)requirements.Length, destination, (ulong)ranges.Length, X64PageMap.MaximumPageImages, Preconditions);
                }

                if (!result.Succeeded)
                    return;
                ulong exactPages = result.TotalTablePageCount + 1;
                if (exactPages == tableStatePages)
                    return;
                tableStatePages = exactPages;
            }

            throw new InvalidOperationException("live-interval page accounting did not converge");
        }

        private static T[] AllocatePinnedCopy<T>(T[] source)
            where T : unmanaged
        {
            T[] destination = GC.AllocateArray<T>(source.Length, pinned: true);
            source.CopyTo(destination, 0);
            return destination;
        }

        private X64BootIdentityMappingRequirement[] RequirementsFor(ulong tableStatePages)
        {
            ulong page = BootReservedRange.PageSize;
            ulong guard = (ulong)AtPage(StageOneDataPage);
            ulong stack = guard + page;
            ulong table = (ulong)AtPage(StageThreeTablePage);
            ulong loadedImage = (ulong)_backing;
            BootReservedRange allocatorArena = _stageTwoLedger[4];
            ulong handoffStorageStart = Raw(StackSwitchPlan.Plan.Handoff.StorageStart);
            ulong handoffStorageEnd = Raw(StackSwitchPlan.Plan.Handoff.StorageEndExclusive);
            ulong handoffPageStart = handoffStorageStart & ~(page - 1);
            ulong handoffPageEnd = checked((handoffStorageEnd + page - 1) & ~(page - 1));
            return [Requirement(X64BootIdentityMappingPurpose.BootStackGuard, guard, guard + page, PageProtection.None, PageCachePolicy.Uninitialized), Requirement(X64BootIdentityMappingPurpose.ExecutingImage, loadedImage, loadedImage + page, PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.ExecutingImage, loadedImage + page, loadedImage + 2 * page, M01014TestData.ReadExecute), Requirement(X64BootIdentityMappingPurpose.ExecutingImage, loadedImage + 2 * page, loadedImage + LoadedImageBytes, PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.OwnedStack, stack, stack + StackPages * page, M01014TestData.ReadWrite), Requirement(X64BootIdentityMappingPurpose.BootHandoff, handoffPageStart, handoffPageEnd, PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.SerialStub, (ulong)AtPage(StageOneCodePage), (ulong)AtPage(StageOneCodePage + 1), M01014TestData.ReadExecute), Requirement(X64BootIdentityMappingPurpose.AcpiScratch, (ulong)AtPage(AcpiRequirementPage), (ulong)AtPage(AcpiRequirementPage + 1), PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.FramebufferMetadata, (ulong)AtPage(FramebufferRequirementPage), (ulong)AtPage(FramebufferRequirementPage + 1), PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, (ulong)CodePage, (ulong)CodePage + page, M01014TestData.ReadExecute), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, (ulong)ProbePage, (ulong)ProbePage + page, M01014TestData.ReadWrite), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, table, table + tableStatePages * page, M01014TestData.ReadWrite), RequirementForReservation(X64BootIdentityMappingPurpose.AdditionalRequired, allocatorArena, M01014TestData.ReadWrite)];
        }

        private X64BootIdentityMappingRequirement[] RequirementsForLiveInterval(ulong tableStatePages)
        {
            ulong page = BootReservedRange.PageSize;
            ulong guard = (ulong)AtPage(StageOneDataPage);
            ulong stack = guard + page;
            ulong table = (ulong)AtPage(StageThreeTablePage);
            ulong tableEnd = checked(table + tableStatePages * page);
            return [Requirement(X64BootIdentityMappingPurpose.BootStackGuard, guard, guard + page, PageProtection.None, PageCachePolicy.Uninitialized), Requirement(X64BootIdentityMappingPurpose.CompositeCodeAndCallGate, (ulong)CodePage, (ulong)CodePage + page, M01014TestData.ReadExecute), Requirement(X64BootIdentityMappingPurpose.CompositeOwnedStack, stack, stack + StackPages * page, M01014TestData.ReadWrite), Requirement(X64BootIdentityMappingPurpose.ProtectionProbePage, (ulong)ProbePage, (ulong)ProbePage + page, M01014TestData.ReadWrite), Requirement(X64BootIdentityMappingPurpose.CompleteTableAndStateArena, table, tableEnd, M01014TestData.ReadWrite), Requirement(X64BootIdentityMappingPurpose.RuntimeStateAndCanaryPage, tableEnd - page, tableEnd, M01014TestData.ReadWrite), Requirement(X64BootIdentityMappingPurpose.OwnedRootPage, table, table + page, M01014TestData.ReadWrite)];
        }

        private M01LivePagingIntegrationResult ExecuteCore(M01LiveAllocatorStaticState* stageTwo, ulong stageTwoCapacity, OwnedBootStackSwitchPlanResult stackPlan, BootMemoryRegionRecord* records, ulong recordCount, ulong recordCapacity, M01LivePagingAllocationEvidence allocations, M01LivePagingMapLedgerEvidence map, BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, X64BootIdentityMappingRequirement[]? requirements, ulong requirementCount, ulong requirementCapacity, X64BootIdentityMappingRange[]? ranges, ulong rangeCapacity, X64PageTableImagePage[]? pages, ulong pageCapacity, ulong[]? entries, ulong entryCapacity, byte* codePage, ulong codeCapacity, byte* probePage, ulong probeCapacity, M01LivePagingByteMode byteMode, M01LivePagingStaticState* state, ulong stateCapacity)
        {
            fixed (X64BootIdentityMappingRequirement* requirementPointer = requirements)
            fixed (X64BootIdentityMappingRange* rangePointer = ranges)
            fixed (X64PageTableImagePage* pagePointer = pages)
            fixed (ulong* entryPointer = entries)
            {
                return ExecuteCoreRawWorkspace(stageTwo, stageTwoCapacity, stackPlan, records, recordCount, recordCapacity, allocations, map, ledger, ledgerCount, ledgerCapacity, requirementPointer, requirementCount, requirementCapacity, rangePointer, rangeCapacity, pagePointer, pageCapacity, entryPointer, entryCapacity, codePage, codeCapacity, probePage, probeCapacity, byteMode, state, stateCapacity);
            }
        }

        private M01LivePagingPreparationResult PrepareCore(M01LiveAllocatorStaticState* stageTwo, ulong stageTwoCount, ulong stageTwoCapacity, OwnedBootStackSwitchPlanResult stackPlan, BootMemoryRegionRecord* records, ulong recordCount, ulong recordCapacity, M01LivePagingAllocationEvidence allocations, M01LivePagingMapLedgerEvidence map, BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, X64BootIdentityMappingRequirement[]? requirements, ulong requirementCount, ulong requirementCapacity, X64BootIdentityMappingRange[]? ranges, ulong rangeCount, ulong rangeCapacity, X64PageTableImagePage[]? pages, ulong pageCount, ulong pageCapacity, ulong[]? entries, ulong entryCount, ulong entryCapacity, byte* codePage, ulong codeCapacity, byte* probePage, ulong probeCapacity, M01LivePagingByteMode byteMode, M01LivePagingPreparationState* state, ulong stateCount, ulong stateCapacity, X64PagingModePreflightObservation* preflight, ulong preflightCount, ulong preflightCapacity)
        {
            fixed (X64BootIdentityMappingRequirement* requirementPointer = requirements)
            fixed (X64BootIdentityMappingRange* rangePointer = ranges)
            fixed (X64PageTableImagePage* pagePointer = pages)
            fixed (ulong* entryPointer = entries)
            {
                return PrepareCoreRawWorkspace(stageTwo, stageTwoCount, stageTwoCapacity, stackPlan, records, recordCount, recordCapacity, allocations, map, ledger, ledgerCount, ledgerCapacity, requirementPointer, requirementCount, requirementCapacity, rangePointer, rangeCount, rangeCapacity, pagePointer, pageCount, pageCapacity, entryPointer, entryCount, entryCapacity, codePage, codeCapacity, probePage, probeCapacity, preflight, preflightCount, preflightCapacity, byteMode, state, stateCount, stateCapacity);
            }
        }

        private M01LivePagingPreparationResult PrepareCoreRawWorkspace(M01LiveAllocatorStaticState* stageTwo, ulong stageTwoCount, ulong stageTwoCapacity, OwnedBootStackSwitchPlanResult stackPlan, BootMemoryRegionRecord* records, ulong recordCount, ulong recordCapacity, M01LivePagingAllocationEvidence allocations, M01LivePagingMapLedgerEvidence map, BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, X64BootIdentityMappingRequirement* requirements, ulong requirementCount, ulong requirementCapacity, X64BootIdentityMappingRange* ranges, ulong rangeCount, ulong rangeCapacity, X64PageTableImagePage* pages, ulong pageCount, ulong pageCapacity, ulong* entries, ulong entryCount, ulong entryCapacity, byte* codePage, ulong codeCapacity, byte* probePage, ulong probeCapacity, X64PagingModePreflightObservation* preflight, ulong preflightCount, ulong preflightCapacity, M01LivePagingByteMode byteMode, M01LivePagingPreparationState* state, ulong stateCount, ulong stateCapacity)
        {
            return M01LivePagingIntegration.Prepare(stageTwo, stageTwoCount, stageTwoCapacity, stackPlan, records, recordCount, recordCapacity, allocations, map, ledger, ledgerCount, ledgerCapacity, requirements, requirementCount, requirementCapacity, ranges, rangeCount, rangeCapacity, pages, pageCount, pageCapacity, entries, entryCount, entryCapacity, Preconditions, codePage, codeCapacity, probePage, probeCapacity, preflight, preflightCount, preflightCapacity, byteMode, state, stateCount, stateCapacity);
        }

        private M01LivePagingIntegrationResult ExecuteCoreRawWorkspace(M01LiveAllocatorStaticState* stageTwo, ulong stageTwoCapacity, OwnedBootStackSwitchPlanResult stackPlan, BootMemoryRegionRecord* records, ulong recordCount, ulong recordCapacity, M01LivePagingAllocationEvidence allocations, M01LivePagingMapLedgerEvidence map, BootReservedRange* ledger, ulong ledgerCount, ulong ledgerCapacity, X64BootIdentityMappingRequirement* requirements, ulong requirementCount, ulong requirementCapacity, X64BootIdentityMappingRange* ranges, ulong rangeCapacity, X64PageTableImagePage* pages, ulong pageCapacity, ulong* entries, ulong entryCapacity, byte* codePage, ulong codeCapacity, byte* probePage, ulong probeCapacity, M01LivePagingByteMode byteMode, M01LivePagingStaticState* state, ulong stateCapacity)
        {
            fixed (X64PagingModePreflightObservation* preflight = &PreflightObservation)
            fixed (X64Cr3ProbeEvent* cr3Events = Cr3Events)
            fixed (X64ProtectionProbeEvent* protectionEvents = ProtectionEvents)
            {
                return M01LivePagingIntegration.Execute(stageTwo, stageTwoCapacity, stackPlan, records, recordCount, recordCapacity, allocations, map, ledger, ledgerCount, ledgerCapacity, requirements, requirementCount, requirementCapacity, ranges, rangeCapacity, pages, pageCapacity, entries, entryCapacity, Preconditions, codePage, codeCapacity, probePage, probeCapacity, preflight, 1, TransitionState, cr3Events, (ulong)Cr3Events.Length, (ulong)Cr3Events.Length, ProtectionObservation, protectionEvents, (ulong)ProtectionEvents.Length, (ulong)ProtectionEvents.Length, Cr3Policy, byteMode, state, stateCapacity);
            }
        }

        private X64Cr3ProbeEvent[] CreateCr3Events()
        {
            return [CreateCr3Event(X64Cr3ProbeEventKind.PreMarkerEmitted, ownedRoot: false, updatedCanary: false, elapsed: 1), CreateCr3Event(X64Cr3ProbeEventKind.TransitionEntered, ownedRoot: false, updatedCanary: false, elapsed: 2), CreateCr3Event(X64Cr3ProbeEventKind.OwnedRootLoaded, ownedRoot: true, updatedCanary: false, elapsed: 3), CreateCr3Event(X64Cr3ProbeEventKind.OwnedRootReadBack, ownedRoot: true, updatedCanary: false, elapsed: 4), CreateCr3Event(X64Cr3ProbeEventKind.CanaryUpdated, ownedRoot: true, updatedCanary: true, elapsed: 5), CreateCr3Event(X64Cr3ProbeEventKind.FirmwareRootRestored, ownedRoot: false, updatedCanary: true, elapsed: 6), CreateCr3Event(X64Cr3ProbeEventKind.FirmwareRootReadBack, ownedRoot: false, updatedCanary: true, elapsed: 7), CreateCr3Event(X64Cr3ProbeEventKind.CanaryValidated, ownedRoot: false, updatedCanary: true, elapsed: 8), CreateCr3Event(X64Cr3ProbeEventKind.PostMarkerEmitted, ownedRoot: false, updatedCanary: true, elapsed: 9)];
        }

        private void WriteStageThreeLedger(BootReservedRange[] reservations)
        {
            WriteLedger(Ledger, LedgerCapacity, reservations);
            LedgerCount = (ulong)reservations.Length;
            if (AllocationEvidence.Size != 0)
                MapEvidence = CaptureMapEvidence();
        }

        private static void WriteLedger(BootReservedRange* destination, ulong capacity, BootReservedRange[] reservations)
        {
            if ((ulong)reservations.Length > capacity)
                throw new InvalidOperationException("ledger capacity exceeded");
            Array.Sort(reservations, static (left, right) =>
            {
                left.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong leftStart);
                right.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong rightStart);
                return leftStart.CompareTo(rightStart);
            });
            Clear((byte*)destination, capacity * (ulong)sizeof(BootReservedRange));
            for (int index = 0; index < reservations.Length; index++)
            {
                destination[index] = reservations[index];
            }
        }

        private static FirmwareMemoryMapRecord CreateOwnedMap(byte* destination, byte* source, ulong mapKey)
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

        internal M01LivePagingPreparationResult PrepareWorkspace(X64BootIdentityMappingRequirement[]? requirements, ulong requirementCount, ulong requirementCapacity, X64BootIdentityMappingRange[]? ranges, ulong rangeCount, ulong rangeCapacity, X64PageTableImagePage[]? pages, ulong pageCount, ulong pageCapacity, ulong[]? entries, ulong entryCount, ulong entryCapacity, byte* codePage, ulong codeCapacity, byte* probePage, ulong probeCapacity, M01LivePagingByteMode byteMode = M01LivePagingByteMode.EmitAndVerify, bool aliasPageStorage = false)
        {
            ActivateMappingProfile(FixtureMappingProfile.ExactLiveIntervalPreparation);
            fixed (X64BootIdentityMappingRequirement* requirementPointer = requirements)
            fixed (X64BootIdentityMappingRange* rangePointer = ranges)
            fixed (X64PageTableImagePage* pagePointer = pages)
            fixed (ulong* entryPointer = entries)
            fixed (X64PagingModePreflightObservation* preflight = &PreflightObservation)
            {
                return PrepareCoreRawWorkspace(StageTwoState, 1, 1, StackSwitchPlan, Records, 3, 3, AllocationEvidence, MapEvidence, Ledger, LedgerCount, LedgerCapacity, requirementPointer, requirementCount, requirementCapacity, rangePointer, rangeCount, rangeCapacity, aliasPageStorage ? (X64PageTableImagePage*)entryPointer : pagePointer, pageCount, pageCapacity, entryPointer, entryCount, entryCapacity, codePage, codeCapacity, probePage, probeCapacity, preflight, 1, 1, byteMode, PreparationState, 1, 1);
            }
        }
    }
}
