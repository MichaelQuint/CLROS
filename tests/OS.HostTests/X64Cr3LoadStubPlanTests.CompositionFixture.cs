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

internal static unsafe partial class X64Cr3LoadStubPlanTests
{
#if !M01_013_LOAD_FOCUSED && !M01_013_PREFLIGHT_FOCUSED && !M01_013_TRANSITION_FOCUSED
#endif
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
        private const ulong StackArenaPages = StackGuardPages + StackPages;
        private const ulong InitialCanary = 0x1122_3344_5566_7788UL;
        private const ulong UpdatedCanary = 0x8877_6655_4433_2211UL;
        private const ulong ValidCr0 = X64PagingModePreflightStubPlan.Cr0PagingBit | X64PagingModePreflightStubPlan.Cr0WriteProtectBit;
        private const ulong ValidCr4 = X64PagingModePreflightStubPlan.Cr4PaeBit | X64ReversibleCr3TransitionStubPlan.Cr4PageGlobalEnableBit;
        private const ulong ValidEfer = X64PagingModePreflightStubPlan.EferLongModeEnableBit | X64PagingModePreflightStubPlan.EferLongModeActiveBit | X64PagingModePreflightStubPlan.EferNoExecuteEnableBit;
        private const ulong ValidRflags = 0x202;
        private byte* _codePage;
        internal CompositionFixture()
        {
            _codePage = (byte*)NativeMemory.AlignedAlloc((nuint)BootMemoryRegionRecord.PageSize, (nuint)BootMemoryRegionRecord.PageSize);
            if (_codePage == null)
                throw new OutOfMemoryException();
            for (ulong index = 0; index < BootMemoryRegionRecord.PageSize; index++)
            {
                _codePage[index] = 0;
            }

            CodeStart = (ulong)_codePage;
            StackSwitch = CreateStackSwitchPlan();
            if (!StackSwitch.Succeeded)
            {
                throw new InvalidOperationException("stack fixture failed: " + StackSwitch.Status);
            }

            ProbeRsp = Virtual(Raw(StackSwitch.Plan.ExpectedContinuationStackPointer) - 64);
            BuildMappingPlan(CodeStart, out X64BootIdentityMappingRequirement[] requirements, out X64BootIdentityMappingRange[] ranges, out X64BootIdentityMappingPlanResult mapping);
            Requirements = requirements;
            MappingRanges = ranges;
            Mapping = mapping;
            if (!Mapping.Succeeded)
            {
                throw new InvalidOperationException("mapping fixture failed: " + Mapping.Status);
            }

            ulong dataPages = Mapping.TotalTablePageCount + 1;
            Allocations = [Record(UefiMemoryType.LoaderCode, CodeStart, 1), Record(UefiMemoryType.LoaderData, DataStart, dataPages)];
            ulong statePage = DataStart + Mapping.TotalTableByteLength;
            Storage = new X64Cr3ProbeStorageLayout(Virtual(statePage), X64Cr3ProbeStorageLayout.RequiredStateRecordByteLength, Virtual(statePage + X64Cr3ProbeStorageLayout.RequiredStateRecordByteLength), X64Cr3ProbeStorageLayout.RequiredCanaryByteLength, InitialCanary, UpdatedCanary, X64Cr3ProbeStorageProvenance.CallerAttestedOwnedLoaderData);
            Policy = new X64Cr3ProbePolicy(enabledByDefault: false, explicitOneShotOptIn: true, timeoutTicks: 1000);
            X64PagingModePreflightObservation observation = X64PagingModePreflightObservation.CreateObserved(ValidCr0, RawFirmwareCr3, ValidCr4, ValidEfer, X64PagingModePreflightStubPlan.CpuidAddressSizesLeaf, 48, ValidRflags);
            X64PagingModePreflightResult preflightState = default;
            PreflightMaterialization = X64PagingModePreflightStubPlan.TryMaterialize(_codePage, BootMemoryRegionRecord.PageSize, PreflightOffset, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &preflightState, X64PagingModePreflightResult.StructureSize);
            PreflightReadback = X64PagingModePreflightStubPlan.TryValidateMaterialized(_codePage, BootMemoryRegionRecord.PageSize, PreflightOffset, X64PagingModePreflightStubPlan.RequiredByteLength, &observation, X64PagingModePreflightObservation.StructureSize, &preflightState, X64PagingModePreflightResult.StructureSize);
            X64PagingModePreflightRequest request = X64PagingModePreflightRequest.CreateEnabled(Physical(DataStart), Physical(DataStart), Mapping.TotalTableByteLength);
            Preflight = X64PagingModePreflightStubPlan.Evaluate(&request, 1, 1, &observation, 1, 1, &preflightState, 1, 1);
            byte* transitionBytes = _codePage + TransitionOffset;
            ulong transitionEntry = CodeStart + TransitionOffset;
            ulong transitionCapacity = BootMemoryRegionRecord.PageSize - TransitionOffset;
            TransitionMaterialization = X64ReversibleCr3TransitionStubPlan.Emit(transitionBytes, transitionCapacity, transitionEntry);
            TransitionReadback = X64ReversibleCr3TransitionStubPlan.Validate(transitionBytes, X64ReversibleCr3TransitionStubPlan.RequiredByteLength, transitionCapacity, transitionEntry);
            ulong stackStart = Raw(StackSwitch.Plan.UsableStackVirtualStart);
            ulong stackEnd = Raw(StackSwitch.Plan.UsableStackVirtualEndExclusive);
            ulong rsp = Raw(ProbeRsp);
            ulong canaryAddress = Raw(Storage.CanaryAddress);
            X64ReversibleCr3TransitionRuntimeState transitionState = CreateSuccessfulTransitionState(stackStart, stackEnd, canaryAddress, rsp);
            TransitionEvidence = X64ReversibleCr3TransitionStubPlan.Evaluate(&transitionState, Raw(Storage.StateAddress), X64ReversibleCr3TransitionTerminalKind.Returned);
        }

        internal ulong CodeStart
        {
            get;
        }
        internal BootMemoryRegionRecord[] Allocations
        {
            get;
        }
        internal X64BootIdentityMappingRequirement[] Requirements
        {
            get;
        }
        internal X64BootIdentityMappingRange[] MappingRanges
        {
            get;
        }
        internal X64BootIdentityMappingPlanResult Mapping
        {
            get;
        }
        internal OwnedBootStackSwitchPlanResult StackSwitch
        {
            get;
        }
        internal VirtualAddress ProbeRsp
        {
            get;
        }
        internal X64Cr3ProbeStorageLayout Storage
        {
            get;
        }
        internal X64Cr3ProbePolicy Policy
        {
            get;
        }
        internal X64PagingModePreflightResult Preflight
        {
            get;
        }
        internal X64PagingModePreflightStubResult PreflightMaterialization
        {
            get;
        }
        internal X64PagingModePreflightStubResult PreflightReadback
        {
            get;
        }
        internal X64ReversibleCr3TransitionStubPlanResult TransitionMaterialization
        {
            get;
        }
        internal X64ReversibleCr3TransitionStubPlanResult TransitionReadback
        {
            get;
        }
        internal X64ReversibleCr3TransitionEvidenceResult TransitionEvidence
        {
            get;
        }

        internal X64Cr3LoadStubPlanResult Compose(X64Cr3ProbePolicy? policy = null, BootMemoryRegionRecord[]? allocations = null, OwnedBootStackSwitchPlanResult? stackSwitch = null, X64Cr3ProbeStorageLayout? storage = null, X64PagingModePreflightResult? preflight = null, X64PagingModePreflightStubResult? preflightMaterialization = null, X64PagingModePreflightStubResult? preflightReadback = null, X64ReversibleCr3TransitionStubPlanResult? transitionMaterialization = null, X64ReversibleCr3TransitionStubPlanResult? transitionReadback = null, X64ReversibleCr3TransitionEvidenceResult? transitionEvidence = null, ulong preflightOffset = PreflightOffset, ulong transitionOffset = TransitionOffset)
        {
            BootMemoryRegionRecord[] selectedAllocations = allocations ?? Allocations;
            fixed (BootMemoryRegionRecord* allocationPointer = selectedAllocations)
            fixed (X64BootIdentityMappingRange* mappingPointer = MappingRanges)
            {
                return X64Cr3LoadStubPlan.Create(policy ?? Policy, allocationPointer, (ulong)selectedAllocations.Length, (ulong)selectedAllocations.Length, Mapping, mappingPointer, Mapping.OutputRangeCount, (ulong)MappingRanges.Length, stackSwitch ?? StackSwitch, storage ?? Storage, preflight ?? Preflight, preflightMaterialization ?? PreflightMaterialization, preflightReadback ?? PreflightReadback, transitionMaterialization ?? TransitionMaterialization, transitionReadback ?? TransitionReadback, transitionEvidence ?? TransitionEvidence, preflightOffset, transitionOffset);
            }
        }

        internal X64Cr3LoadStubPlanResult ComposeRaw(BootMemoryRegionRecord* allocationPointer, ulong allocationCount, ulong allocationCapacity, X64BootIdentityMappingRange* mappingPointer, ulong mappingCount, ulong mappingCapacity)
        {
            return X64Cr3LoadStubPlan.Create(Policy, allocationPointer, allocationCount, allocationCapacity, Mapping, mappingPointer, mappingCount, mappingCapacity, StackSwitch, Storage, Preflight, PreflightMaterialization, PreflightReadback, TransitionMaterialization, TransitionReadback, TransitionEvidence, PreflightOffset, TransitionOffset);
        }

        internal X64Cr3ProbeEvent[] CreateSuccessfulTranscript()
        {
            return [Event(X64Cr3ProbeEventKind.PreMarkerEmitted, ownedRoot: false, updatedCanary: false, elapsed: 1), Event(X64Cr3ProbeEventKind.TransitionEntered, ownedRoot: false, updatedCanary: false, elapsed: 2), Event(X64Cr3ProbeEventKind.OwnedRootLoaded, ownedRoot: true, updatedCanary: false, elapsed: 3), Event(X64Cr3ProbeEventKind.OwnedRootReadBack, ownedRoot: true, updatedCanary: false, elapsed: 4), Event(X64Cr3ProbeEventKind.CanaryUpdated, ownedRoot: true, updatedCanary: true, elapsed: 5), Event(X64Cr3ProbeEventKind.FirmwareRootRestored, ownedRoot: false, updatedCanary: true, elapsed: 6), Event(X64Cr3ProbeEventKind.FirmwareRootReadBack, ownedRoot: false, updatedCanary: true, elapsed: 7), Event(X64Cr3ProbeEventKind.CanaryValidated, ownedRoot: false, updatedCanary: true, elapsed: 8), Event(X64Cr3ProbeEventKind.PostMarkerEmitted, ownedRoot: false, updatedCanary: true, elapsed: 9)];
        }

        internal X64Cr3ProbeEvent Event(X64Cr3ProbeEventKind kind, bool ownedRoot, bool updatedCanary, ulong elapsed, ulong? rawCr3Override = null)
        {
            ulong rawCr3 = rawCr3Override ?? (ownedRoot ? DataStart : RawFirmwareCr3);
            return new X64Cr3ProbeEvent(kind, rawCr3, ProbeRsp, updatedCanary ? UpdatedCanary : InitialCanary, elapsed);
        }

        public void Dispose()
        {
            byte* page = _codePage;
            _codePage = null;
            if (page != null)
                NativeMemory.AlignedFree(page);
        }

        private static X64ReversibleCr3TransitionRuntimeState CreateSuccessfulTransitionState(ulong stackStart, ulong stackEnd, ulong canaryAddress, ulong rsp)
        {
            ulong clearedCr4 = ValidCr4 & ~X64ReversibleCr3TransitionStubPlan.Cr4PageGlobalEnableBit;
            return X64ReversibleCr3TransitionRuntimeState.CreateObserved(DataStart, stackStart, stackEnd, canaryAddress, InitialCanary, UpdatedCanary, ValidRflags, RawFirmwareCr3, ValidCr4, clearedCr4, DataStart, InitialCanary, UpdatedCanary, RawFirmwareCr3, ValidCr4, (ValidRflags & ~X64ReversibleCr3TransitionStubPlan.RflagsInterruptEnableBit) ^ 1UL, ValidRflags, rsp, rsp, UpdatedCanary, 0, X64ReversibleCr3TransitionRuntimeStatus.Success, X64ReversibleCr3TransitionCheckpoint.Returned);
        }

        private static void BuildMappingPlan(ulong codeStart, out X64BootIdentityMappingRequirement[] requirements, out X64BootIdentityMappingRange[] ranges, out X64BootIdentityMappingPlanResult result)
        {
            ulong dataPages = 8;
            requirements = [];
            ranges = [];
            result = default;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                requirements = RequirementsFor(codeStart, dataPages);
                ranges = new X64BootIdentityMappingRange[64];
                fixed (X64BootIdentityMappingRequirement* source = requirements)
                fixed (X64BootIdentityMappingRange* destination = ranges)
                {
                    result = X64BootIdentityMappingPlan.Create(source, (ulong)requirements.Length, (ulong)requirements.Length, destination, (ulong)ranges.Length, 128, PageTableContext());
                }

                if (!result.Succeeded)
                    return;
                ulong exactPages = result.TotalTablePageCount + 1;
                if (exactPages == dataPages)
                    return;
                dataPages = exactPages;
            }

            throw new InvalidOperationException("mapping table/data page accounting did not converge");
        }

        private static X64BootIdentityMappingRequirement[] RequirementsFor(ulong codeStart, ulong dataPages)
        {
            ulong page = BootMemoryRegionRecord.PageSize;
            return [Requirement(X64BootIdentityMappingPurpose.BootStackGuard, 0x1000, 0x2000, PageProtection.None, PageCachePolicy.Uninitialized), Requirement(X64BootIdentityMappingPurpose.ExecutingImage, 0x4000, 0x5000, Protection(PageProtection.Read, PageProtection.Execute)), Requirement(X64BootIdentityMappingPurpose.OwnedStack, StackArenaStart + page, StackArenaStart + StackArenaPages * page, Protection(PageProtection.Read, PageProtection.Write)), Requirement(X64BootIdentityMappingPurpose.BootHandoff, 0x0070_0000, 0x0070_1000, PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.SerialStub, 0x0080_0000, 0x0080_1000, Protection(PageProtection.Read, PageProtection.Execute)), Requirement(X64BootIdentityMappingPurpose.AcpiScratch, 0x0090_0000, 0x0090_1000, PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.FramebufferMetadata, 0x00a0_0000, 0x00a0_1000, PageProtection.Read), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, codeStart, codeStart + page, Protection(PageProtection.Read, PageProtection.Execute)), Requirement(X64BootIdentityMappingPurpose.AdditionalRequired, DataStart, DataStart + dataPages * page, Protection(PageProtection.Read, PageProtection.Write))];
        }

        private static X64BootIdentityMappingRequirement Requirement(X64BootIdentityMappingPurpose purpose, ulong start, ulong end, PageProtection protection)
        {
            return Requirement(purpose, start, end, protection, protection == PageProtection.None ? PageCachePolicy.Uninitialized : PageCachePolicy.WriteBack);
        }

        private static X64BootIdentityMappingRequirement Requirement(X64BootIdentityMappingPurpose purpose, ulong start, ulong end, PageProtection protection, PageCachePolicy cache)
        {
            ulong bytes = end - start;
            return new X64BootIdentityMappingRequirement(purpose, Physical(start), Physical(end), bytes / BootMemoryRegionRecord.PageSize, bytes, protection, cache);
        }

        private static X64PageTableEncodingPreconditions PageTableContext()
        {
            X64PageTableEntryStatus tokenStatus = X64PatMappingToken.TryCreate(0x0006_0501_0007_0406, writeBackIndex: 0, uncachedIndex: 3, writeCombiningIndex: 4, out X64PatMappingToken token);
            if (tokenStatus != X64PageTableEntryStatus.Success)
            {
                throw new InvalidOperationException("PAT fixture failed: " + tokenStatus);
            }

            return X64PageTableEncodingPreconditions.CreateObserved(physicalAddressWidth: 48, nxeEnabled: true, writeProtectEnabled: true, fiveLevelPagingEnabled: false, oneGiBPagesSupported: true, token);
        }

        internal static OwnedBootStackSwitchPlanResult CreateStackSwitchPlan(ulong stackCodeStart = 0x0050_0000, ulong stackBackingStart = StackArenaStart)
        {
            ulong page = BootMemoryRegionRecord.PageSize;
            PhysicalAddress backing = Physical(stackBackingStart);
            BootReservedRange* reservations = stackalloc BootReservedRange[2];
            if (BootReservedRange.TryCreate(BootReservationKind.BootStackGuard, backing, StackGuardPages, out reservations[0]) != BootReservedRangeStatus.Success || BootReservedRange.TryCreate(BootReservationKind.BootStack, Physical(stackBackingStart + page), StackPages, out reservations[1]) != BootReservedRangeStatus.Success)
            {
                return default;
            }

            OwnedBootStackLayoutResult reserved = OwnedBootStackLayout.Create(reservations, 2, 2, backing, StackArenaPages, Physical(stackBackingStart + StackArenaPages * page));
            if (!reserved.Succeeded)
                return default;
            OwnedBootStackLayoutResult ready = reserved.Layout.TransitionTo(OwnedBootStackLifetime.Ready);
            if (!ready.Succeeded)
                return default;
            BootMemoryRegionRecord[] records = [Record(UefiMemoryType.LoaderCode, stackCodeStart, 1), Record(UefiMemoryType.LoaderData, stackBackingStart, StackArenaPages)];
            fixed (BootMemoryRegionRecord* pointer = records)
            {
                return OwnedBootStackSwitchPlan.Create(pointer, 2, 2, ready.Layout, stubEntryOffset: 0, declaredStubByteLength: OwnedBootStackSwitchPlan.RequiredStubByteLength, handoffStorageAddress: Virtual(0x0070_0000), handoffStorageByteLength: OwnedBootStackSwitchHandoff.StructureSize, handoffProvenance: OwnedBootStackSwitchHandoffProvenance.CallerAttestedStaticOwnedData, stateStorageAddress: Virtual(0x0071_0000), stateStorageByteLength: OwnedBootStackSwitchStateRecord.StructureSize, stateStorageProvenance: OwnedBootStackSwitchStateStorageProvenance.CallerAttestedStaticOwnedData, continuationAddress: Virtual(0x0072_0010), continuationContract: OwnedBootStackSwitchContinuationContract.StaticNonReturningRspAndHandoff);
            }
        }
    }
}
