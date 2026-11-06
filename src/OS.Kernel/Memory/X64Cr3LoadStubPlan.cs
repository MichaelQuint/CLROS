namespace OS.Kernel.Memory;
/// <summary>
/// Immutable composite evidence for a future reversible CR3 probe. The plan
/// does not invoke either sibling stub or execute a privileged instruction.
/// </summary>

public readonly struct X64Cr3LoadStubPlan
{
    public const ulong StructureSize = 1016;
    public const ulong RequiredAllocationRecordCount = 2;
    public const ulong CodeAllocationRecordIndex = 0;
    public const ulong DataAllocationRecordIndex = 1;
    public const ulong RequiredCodePageCount = 1;
    public const ulong RequiredStatePageCount = 1;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong RequiredStubEntryAlignment = 16;
    public const ulong RequiredProbeStackHeadroomBytes = 64;
    public const ulong RequiredProbeCallFrameBytes = 40;
    public const ulong RequiredEventCount = 9;
    public const ulong MaximumEventCount = 32;
    public const ulong NoIndex = ~0UL;
    public const bool DefaultEnabled = false;
    public const bool RequiresExplicitOneShotOptIn = true;
    public const bool PerformsLiveCr3Load = false;
    public const bool HostTestsExecuteMovCr3 = false;
    public const bool AllowsFirmwareCallbacksInsideInterval = false;
    public const bool AllowsInterruptReenableInsideInterval = false;
    private readonly X64Cr3ProbePolicy _policy;
    private readonly OS.Boot.BootMemoryRegionRecord _codeAllocation;
    private readonly OS.Boot.BootMemoryRegionRecord _dataAllocation;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingPlanResult _identityMappingPlan;
    private readonly OS.Boot.OwnedBootStackSwitchPlanResult _stackSwitchPlan;
    private readonly X64Cr3ProbeStorageLayout _storageLayout;
    private readonly ulong _rawFirmwareCr3;
    private readonly OS.Boot.PhysicalAddress _firmwareRootBase;
    private readonly OS.Boot.PhysicalAddress _ownedRoot;
    private readonly OS.Boot.PhysicalAddress _tableArenaStart;
    private readonly OS.Boot.PhysicalAddress _tableArenaEndExclusive;
    private readonly OS.Boot.VirtualAddress _statePageStart;
    private readonly OS.Boot.VirtualAddress _statePageEndExclusive;
    private readonly OS.Boot.VirtualAddress _usableStackStart;
    private readonly OS.Boot.VirtualAddress _usableStackEndExclusive;
    private readonly ulong _physicalAddressMask;
    private readonly ulong _preflightEntryOffset;
    private readonly ulong _preflightByteLength;
    private readonly ulong _transitionEntryOffset;
    private readonly ulong _transitionByteLength;
    private readonly ulong _transitionRuntimeStateAddress;
    private readonly byte _isInitialized;
    private readonly byte _physicalAddressWidth;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private X64Cr3LoadStubPlan(X64Cr3ProbePolicy policy, OS.Boot.BootMemoryRegionRecord codeAllocation, OS.Boot.BootMemoryRegionRecord dataAllocation, OS.Kernel.Boot.X64BootIdentityMappingPlanResult identityMappingPlan, OS.Boot.OwnedBootStackSwitchPlanResult stackSwitchPlan, X64Cr3ProbeStorageLayout storageLayout, ulong rawFirmwareCr3, OS.Boot.PhysicalAddress firmwareRootBase, OS.Boot.PhysicalAddress ownedRoot, OS.Boot.PhysicalAddress tableArenaStart, OS.Boot.PhysicalAddress tableArenaEndExclusive, OS.Boot.VirtualAddress statePageStart, OS.Boot.VirtualAddress statePageEndExclusive, OS.Boot.VirtualAddress usableStackStart, OS.Boot.VirtualAddress usableStackEndExclusive, ulong physicalAddressMask, byte physicalAddressWidth, ulong preflightEntryOffset, ulong preflightByteLength, ulong transitionEntryOffset, ulong transitionByteLength, ulong transitionRuntimeStateAddress)
    {
        _policy = policy;
        _codeAllocation = codeAllocation;
        _dataAllocation = dataAllocation;
        _identityMappingPlan = identityMappingPlan;
        _stackSwitchPlan = stackSwitchPlan;
        _storageLayout = storageLayout;
        _rawFirmwareCr3 = rawFirmwareCr3;
        _firmwareRootBase = firmwareRootBase;
        _ownedRoot = ownedRoot;
        _tableArenaStart = tableArenaStart;
        _tableArenaEndExclusive = tableArenaEndExclusive;
        _statePageStart = statePageStart;
        _statePageEndExclusive = statePageEndExclusive;
        _usableStackStart = usableStackStart;
        _usableStackEndExclusive = usableStackEndExclusive;
        _physicalAddressMask = physicalAddressMask;
        _preflightEntryOffset = preflightEntryOffset;
        _preflightByteLength = preflightByteLength;
        _transitionEntryOffset = transitionEntryOffset;
        _transitionByteLength = transitionByteLength;
        _transitionRuntimeStateAddress = transitionRuntimeStateAddress;
        _isInitialized = 1;
        _physicalAddressWidth = physicalAddressWidth;
        _reserved1 = 0;
        _reserved2 = 0;
    }

    public bool IsInitialized => _isInitialized == 1 && _physicalAddressWidth >= 36 && _physicalAddressWidth <= 52 && _reserved1 == 0 && _reserved2 == 0;

    public bool IsValid
    {
        get
        {
            if (!IsInitialized || !_policy.IsInitialized || _policy.EnabledByDefault || !_policy.ExplicitOneShotOptIn || _policy.TimeoutTicks == 0 || !HasValidMappingSummary(_identityMappingPlan) || !_stackSwitchPlan.Succeeded || !_storageLayout.IsInitialized || _rawFirmwareCr3 == 0 || _physicalAddressMask == 0 || _physicalAddressMask != ((1UL << _physicalAddressWidth) - 1) || !_firmwareRootBase.IsValid || !_ownedRoot.IsValid || _firmwareRootBase.Equals(_ownedRoot) || !_tableArenaStart.IsValid || !_tableArenaEndExclusive.IsValid || !_statePageStart.IsValid || !_statePageEndExclusive.IsValid || !_usableStackStart.IsValid || !_usableStackEndExclusive.IsValid || !_usableStackStart.Equals(_stackSwitchPlan.Plan.UsableStackVirtualStart) || !_usableStackEndExclusive.Equals(_stackSwitchPlan.Plan.UsableStackVirtualEndExclusive) || _preflightByteLength != X64PagingModePreflightStubPlan.RequiredByteLength || _transitionByteLength != X64ReversibleCr3TransitionStubPlan.RequiredByteLength || !X64ReversibleCr3TransitionStubPlan.IsValidRuntimeStateAddress(_transitionRuntimeStateAddress))
            {
                return false;
            }

            if (_firmwareRootBase.TryGetRawValueForHardwareRegisterBoundary(out ulong firmwareRoot) != OS.Boot.AddressValueStatus.Success || _ownedRoot.TryGetRawValueForHardwareRegisterBoundary(out ulong ownedRoot) != OS.Boot.AddressValueStatus.Success || _usableStackStart.TryGetRawValueForHardwareRegisterBoundary(out ulong usableStackStart) != OS.Boot.AddressValueStatus.Success || _usableStackEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong usableStackEnd) != OS.Boot.AddressValueStatus.Success || usableStackStart > ~0UL - RequiredProbeStackHeadroomBytes || usableStackStart + RequiredProbeStackHeadroomBytes >= usableStackEnd)
            {
                return false;
            }

            if (_identityMappingPlan.TotalTablePageCount == 0 || _identityMappingPlan.TotalTablePageCount > (~0UL - RequiredStatePageCount) || !HasExactAllocation(_codeAllocation, (uint)OS.Boot.UefiMemoryType.LoaderCode, RequiredCodePageCount, out ulong codeStart, out ulong codeEnd) || !HasExactAllocation(_dataAllocation, (uint)OS.Boot.UefiMemoryType.LoaderData, _identityMappingPlan.TotalTablePageCount + RequiredStatePageCount, out ulong dataStart, out ulong dataEnd) || (codeStart < dataEnd && dataStart < codeEnd) || _preflightEntryOffset >= OS.Boot.BootMemoryRegionRecord.PageSize || _transitionEntryOffset >= OS.Boot.BootMemoryRegionRecord.PageSize || codeStart > ~0UL - _preflightEntryOffset || codeStart > ~0UL - _transitionEntryOffset)
            {
                return false;
            }

            OS.Boot.BootMemoryRegionRecord stackCode = _stackSwitchPlan.Plan.CodeAllocation;
            OS.Boot.BootMemoryRegionRecord stackData = _stackSwitchPlan.Plan.DataAllocation;
            if (!HasExactAllocation(stackCode, (uint)OS.Boot.UefiMemoryType.LoaderCode, OS.Boot.OwnedBootStackSwitchPlan.RequiredCodePageCount, out ulong stackCodeStart, out ulong stackCodeEnd) || !HasExactAllocation(stackData, (uint)OS.Boot.UefiMemoryType.LoaderData, stackData.PageCount, out ulong stackDataStart, out ulong stackDataEnd) || RangesOverlap(codeStart, codeEnd, stackCodeStart, stackCodeEnd) || RangesOverlap(codeStart, codeEnd, stackDataStart, stackDataEnd) || RangesOverlap(dataStart, dataEnd, stackCodeStart, stackCodeEnd) || RangesOverlap(dataStart, dataEnd, stackDataStart, stackDataEnd))
            {
                return false;
            }

            ulong preflightStart = codeStart + _preflightEntryOffset;
            ulong transitionStart = codeStart + _transitionEntryOffset;
            if (preflightStart > ~0UL - _preflightByteLength || transitionStart > ~0UL - _transitionByteLength)
            {
                return false;
            }

            ulong preflightEnd = preflightStart + _preflightByteLength;
            ulong transitionEnd = transitionStart + _transitionByteLength;
            if (preflightEnd > codeEnd || transitionEnd > codeEnd || (preflightStart < transitionEnd && transitionStart < preflightEnd) || (preflightStart & (RequiredStubEntryAlignment - 1)) != 0 || (transitionStart & (RequiredStubEntryAlignment - 1)) != 0)
            {
                return false;
            }

            if (_tableArenaStart.TryGetRawValueForHardwareRegisterBoundary(out ulong tableStart) != OS.Boot.AddressValueStatus.Success || _tableArenaEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong tableEnd) != OS.Boot.AddressValueStatus.Success || _statePageStart.TryGetRawValueForHardwareRegisterBoundary(out ulong statePageStart) != OS.Boot.AddressValueStatus.Success || _statePageEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong statePageEnd) != OS.Boot.AddressValueStatus.Success || tableStart != dataStart || ownedRoot != tableStart || tableEnd != statePageStart || statePageEnd != dataEnd || tableEnd <= tableStart || tableEnd - tableStart != _identityMappingPlan.TotalTableByteLength || statePageEnd - statePageStart != OS.Boot.BootMemoryRegionRecord.PageSize)
            {
                return false;
            }

            if (_storageLayout.Provenance != X64Cr3ProbeStorageProvenance.CallerAttestedOwnedLoaderData || _storageLayout.StateAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong stateStart) != OS.Boot.AddressValueStatus.Success || _storageLayout.CanaryAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong canaryStart) != OS.Boot.AddressValueStatus.Success || stateStart != statePageStart || _transitionRuntimeStateAddress != stateStart || (stateStart & (X64Cr3ProbeStorageLayout.RequiredAlignment - 1)) != 0 || _storageLayout.StateByteLength != X64Cr3ProbeStorageLayout.RequiredStateRecordByteLength || _storageLayout.CanaryByteLength != X64Cr3ProbeStorageLayout.RequiredCanaryByteLength || stateStart > ~0UL - _storageLayout.StateByteLength || canaryStart > ~0UL - _storageLayout.CanaryByteLength || canaryStart < stateStart + _storageLayout.StateByteLength || (canaryStart & (X64Cr3ProbeStorageLayout.RequiredAlignment - 1)) != 0 || canaryStart + _storageLayout.CanaryByteLength > statePageEnd || _storageLayout.InitialCanaryValue == 0 || _storageLayout.UpdatedCanaryValue == 0 || _storageLayout.InitialCanaryValue == _storageLayout.UpdatedCanaryValue)
            {
                return false;
            }

            ulong permitted = (_physicalAddressMask & ~0xFFFUL) | 0x18UL;
            return (_rawFirmwareCr3 & ~permitted) == 0 && (_rawFirmwareCr3 & _physicalAddressMask & ~0xFFFUL) == firmwareRoot && (firmwareRoot < dataStart || firmwareRoot >= dataEnd) && (ownedRoot & 0xFFFUL) == 0 && (ownedRoot & ~_physicalAddressMask) == 0;
        }
    }

    private static bool HasValidMappingSummary(OS.Kernel.Boot.X64BootIdentityMappingPlanResult mapping)
    {
        if (!mapping.Succeeded || mapping.SourceCount == 0 || mapping.ValidatedSourceCount != mapping.SourceCount || mapping.SourceCapacity < mapping.SourceCount || mapping.OutputRangeCount == 0 || mapping.DestinationCapacity < mapping.OutputRangeCount || mapping.DestinationCapacity > OS.Kernel.Boot.X64BootIdentityMappingPlan.MaximumDestinationCount || mapping.RequiredDestinationCapacity != mapping.OutputRangeCount || mapping.RequiredPurposeMask != OS.Kernel.Boot.X64BootIdentityMappingPlan.RequiredPurposeMask || (mapping.ObservedPurposeMask & mapping.RequiredPurposeMask) != mapping.RequiredPurposeMask || mapping.MissingPurposeMask != 0 || mapping.InvalidSourceIndex != OS.Kernel.Boot.X64BootIdentityMappingPlanResult.NoIndex || mapping.ConflictingSourceIndex != OS.Kernel.Boot.X64BootIdentityMappingPlanResult.NoIndex || mapping.GuardSourceIndex == OS.Kernel.Boot.X64BootIdentityMappingPlanResult.NoIndex || mapping.GuardSourceIndex >= mapping.SourceCount || mapping.UnionPageCount == 0 || mapping.UnionPageCount > ~0UL / OS.Boot.BootMemoryRegionRecord.PageSize || mapping.UnionByteLength != mapping.UnionPageCount * OS.Boot.BootMemoryRegionRecord.PageSize || mapping.Pml4PageCount != 1 || mapping.TotalTablePageCount == 0 || mapping.TotalTablePageCount > ~0UL / OS.Boot.BootMemoryRegionRecord.PageSize || mapping.TotalTableByteLength != mapping.TotalTablePageCount * OS.Boot.BootMemoryRegionRecord.PageSize || mapping.AvailableTablePageCapacity < mapping.TotalTablePageCount)
        {
            return false;
        }

        if (mapping.Pml4PageCount > ~0UL - mapping.PdptPageCount)
            return false;
        ulong first = mapping.Pml4PageCount + mapping.PdptPageCount;
        if (first > ~0UL - mapping.PdPageCount)
            return false;
        ulong second = first + mapping.PdPageCount;
        return second <= ~0UL - mapping.PtPageCount && second + mapping.PtPageCount == mapping.TotalTablePageCount;
    }

    private static bool HasExactAllocation(OS.Boot.BootMemoryRegionRecord record, uint uefiType, ulong pageCount, out ulong start, out ulong end)
    {
        start = record.PhysicalStart;
        end = record.PhysicalEndExclusive;
        ulong pageSize = OS.Boot.BootMemoryRegionRecord.PageSize;
        if (record.UefiType != uefiType || start == 0 || (start & (pageSize - 1)) != 0 || record.VirtualStart != start || pageCount == 0 || record.PageCount != pageCount || pageCount > ~0UL / pageSize)
        {
            return false;
        }

        ulong bytes = pageCount * pageSize;
        return record.ByteLength == bytes && start <= ~0UL - bytes && end == start + bytes;
    }

    private static bool RangesOverlap(ulong leftStart, ulong leftEnd, ulong rightStart, ulong rightEnd)
    {
        return leftStart < rightEnd && rightStart < leftEnd;
    }

    public X64Cr3ProbePolicy Policy => _policy;
    public OS.Boot.BootMemoryRegionRecord CodeAllocation => _codeAllocation;
    public OS.Boot.BootMemoryRegionRecord DataAllocation => _dataAllocation;
    public OS.Kernel.Boot.X64BootIdentityMappingPlanResult IdentityMappingPlan => _identityMappingPlan;
    public OS.Boot.OwnedBootStackSwitchPlanResult StackSwitchPlan => _stackSwitchPlan;
    public X64Cr3ProbeStorageLayout StorageLayout => _storageLayout;
    /// <summary>
    /// Exact firmware CR3 value, including architecturally meaningful PWT/PCD
    /// low bits. This is deliberately not represented as a physical address.
    /// </summary>

    public ulong RawFirmwareCr3 => _rawFirmwareCr3;
    /// <summary>
    /// Address portion derived from <see cref="RawFirmwareCr3"/> only after
    /// applying the preflight-validated physical-address mask.
    /// </summary>

    public OS.Boot.PhysicalAddress FirmwareRootBase => _firmwareRootBase;
    public OS.Boot.PhysicalAddress OwnedRoot => _ownedRoot;
    public OS.Boot.PhysicalAddress TableArenaStart => _tableArenaStart;
    public OS.Boot.PhysicalAddress TableArenaEndExclusive => _tableArenaEndExclusive;
    public OS.Boot.VirtualAddress StatePageStart => _statePageStart;
    public OS.Boot.VirtualAddress StatePageEndExclusive => _statePageEndExclusive;
    public OS.Boot.VirtualAddress UsableStackStart => _usableStackStart;
    public OS.Boot.VirtualAddress UsableStackEndExclusive => _usableStackEndExclusive;
    public ulong PhysicalAddressMask => _physicalAddressMask;
    public byte PhysicalAddressWidth => _physicalAddressWidth;
    public ulong PreflightEntryOffset => _preflightEntryOffset;
    public ulong PreflightByteLength => _preflightByteLength;
    public ulong TransitionEntryOffset => _transitionEntryOffset;
    public ulong TransitionByteLength => _transitionByteLength;
    /// <summary>
    /// Modeled guest address at which the transition state is stored. This
    /// is retained separately from any host observation pointer so copied
    /// plan evidence cannot be rebound to another state record.
    /// </summary>

    public ulong TransitionRuntimeStateAddress => _transitionRuntimeStateAddress;

    /// <summary>
    /// Composes already-audited sibling byte and runtime evidence into a
    /// default-off reversible probe plan. This method validates only
    /// caller-owned POD; it does not invoke either stub or execute CR3.
    /// </summary>

    public static unsafe X64Cr3LoadStubPlanResult Create(X64Cr3ProbePolicy policy, OS.Boot.BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, OS.Kernel.Boot.X64BootIdentityMappingPlanResult mappingPlan, OS.Kernel.Boot.X64BootIdentityMappingRange* mappingRanges, ulong mappingRangeCount, ulong mappingRangeCapacity, OS.Boot.OwnedBootStackSwitchPlanResult stackSwitchPlan, X64Cr3ProbeStorageLayout storageLayout, X64PagingModePreflightResult preflight, X64PagingModePreflightStubResult preflightMaterialization, X64PagingModePreflightStubResult preflightReadback, X64ReversibleCr3TransitionStubPlanResult transitionMaterialization, X64ReversibleCr3TransitionStubPlanResult transitionReadback, X64ReversibleCr3TransitionEvidenceResult transitionEvidence, ulong preflightEntryOffset, ulong transitionEntryOffset)
    {
        bool transitionContractValid = X64ReversibleCr3TransitionStubPlan.RequiredByteLength != 0 && X64ReversibleCr3TransitionStubPlan.AuditedInstructionCount != 0 && X64ReversibleCr3TransitionStubPlan.RequiredByteLength <= X64ReversibleCr3TransitionStubPlan.MaximumByteCapacity && X64ReversibleCr3TransitionStubPlan.EntryAlignment == RequiredStubEntryAlignment && X64ReversibleCr3TransitionStubPlan.ExternalPatchCount == 0 && !X64ReversibleCr3TransitionStubPlan.HasExternalRelocations && X64ReversibleCr3TransitionStubPlan.UsesWindowsX64Abi && !X64ReversibleCr3TransitionStubPlan.InvokesCallbacks && !X64ReversibleCr3TransitionStubPlan.TouchesNonvolatileGeneralRegisters && !X64ReversibleCr3TransitionStubPlan.TouchesXmm6ThroughXmm15 && X64ReversibleCr3TransitionStubPlan.PreservesStackPointer && X64ReversibleCr3TransitionStubPlan.PreservesShadowSpace && !X64ReversibleCr3TransitionStubPlan.SupportsPcide && !X64ReversibleCr3TransitionStubPlan.SupportsFiveLevelPaging;
        bool transitionMaterializationValid = transitionMaterialization.Succeeded && transitionMaterialization.RequiredBytes == X64ReversibleCr3TransitionStubPlan.RequiredByteLength && transitionMaterialization.BytesWritten == transitionMaterialization.RequiredBytes && transitionMaterialization.InvalidByteIndex == X64ReversibleCr3TransitionStubPlanResult.NoIndex && transitionMaterialization.ExternalPatchCount == 0 && transitionMaterialization.InstructionCount == X64ReversibleCr3TransitionStubPlan.AuditedInstructionCount && !transitionMaterialization.HasExternalRelocations;
        bool transitionReadbackValid = transitionReadback.Succeeded && transitionReadback.RequiredBytes == X64ReversibleCr3TransitionStubPlan.RequiredByteLength && transitionReadback.BytesWritten == transitionReadback.RequiredBytes && transitionReadback.SourceCount == X64ReversibleCr3TransitionStubPlan.RequiredByteLength && transitionReadback.InvalidByteIndex == X64ReversibleCr3TransitionStubPlanResult.NoIndex && transitionReadback.ExternalPatchCount == 0 && transitionReadback.InstructionCount == X64ReversibleCr3TransitionStubPlan.AuditedInstructionCount && !transitionReadback.HasExternalRelocations;
        OS.Boot.AddressValueStatus storageStateAddressStatus = storageLayout.StateAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong storageStateAddress);
        bool transitionEvidenceValid = storageStateAddressStatus == OS.Boot.AddressValueStatus.Success && transitionEvidence.Succeeded && transitionEvidence.State.IsInitialized && transitionEvidence.RuntimeStatus == X64ReversibleCr3TransitionRuntimeStatus.Success && transitionEvidence.Checkpoint == X64ReversibleCr3TransitionCheckpoint.Returned && transitionEvidence.TerminalKind == X64ReversibleCr3TransitionTerminalKind.Returned && transitionEvidence.ExpectedValue == 0 && transitionEvidence.ObservedValue == 0 && transitionEvidence.RuntimeStateAddress == storageStateAddress;
        X64Cr3LoadStubPlanResult composed = X64Cr3LoadStubPlanComposer.CreateCore(policy, allocationRecords, allocationRecordCount, allocationRecordCapacity, mappingPlan, mappingRanges, mappingRangeCount, mappingRangeCapacity, stackSwitchPlan, storageLayout, preflight, preflightMaterialization, preflightReadback, transitionContractValid, transitionMaterializationValid, transitionReadbackValid, transitionEvidenceValid, transitionEvidence.RuntimeStateAddress, preflightEntryOffset, transitionEntryOffset, X64ReversibleCr3TransitionStubPlan.RequiredByteLength);
        if (!composed.Succeeded)
            return composed;
        X64Cr3LoadStubPlan plan = composed.Plan;
        ulong codeStart = plan.CodeAllocation.PhysicalStart;
        ulong preflightEntry = codeStart + preflightEntryOffset;
        ulong transitionEntry = codeStart + transitionEntryOffset;
        if (preflightMaterialization.EntryAddress != preflightEntry || preflightMaterialization.BufferCapacity != OS.Boot.BootMemoryRegionRecord.PageSize)
        {
            return Failure(X64Cr3LoadStubPlanStatus.InvalidPreflightMaterialization);
        }

        if (preflightReadback.EntryAddress != preflightEntry || preflightReadback.BufferCapacity != OS.Boot.BootMemoryRegionRecord.PageSize)
        {
            return Failure(X64Cr3LoadStubPlanStatus.InvalidPreflightReadback);
        }

        if (transitionMaterialization.EntryAddress != transitionEntry)
        {
            return Failure(X64Cr3LoadStubPlanStatus.InvalidTransitionMaterialization);
        }

        ulong transitionSourceCapacity = OS.Boot.BootMemoryRegionRecord.PageSize - transitionEntryOffset;
        if (transitionMaterialization.SourceCapacity != transitionSourceCapacity || transitionMaterialization.SourceCount != X64ReversibleCr3TransitionStubPlan.RequiredByteLength)
        {
            return Failure(X64Cr3LoadStubPlanStatus.InvalidTransitionMaterialization);
        }

        if (transitionReadback.EntryAddress != transitionEntry || transitionReadback.SourceCapacity != transitionSourceCapacity || transitionReadback.SourceCount != X64ReversibleCr3TransitionStubPlan.RequiredByteLength)
        {
            return Failure(X64Cr3LoadStubPlanStatus.InvalidTransitionReadback);
        }

        X64ReversibleCr3TransitionRuntimeState transitionState = transitionEvidence.State;
        if (plan.OwnedRoot.TryGetRawValueForHardwareRegisterBoundary(out ulong ownedRoot) != OS.Boot.AddressValueStatus.Success || plan.StackSwitchPlan.Plan.UsableStackVirtualStart.TryGetRawValueForHardwareRegisterBoundary(out ulong stackStart) != OS.Boot.AddressValueStatus.Success || plan.StackSwitchPlan.Plan.UsableStackVirtualEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong stackEnd) != OS.Boot.AddressValueStatus.Success || plan.StorageLayout.CanaryAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong canaryAddress) != OS.Boot.AddressValueStatus.Success)
        {
            return Failure(X64Cr3LoadStubPlanStatus.InvalidTransitionPlan);
        }

        ulong rawCr4 = preflight.RawCr4;
        ulong originalRflags = preflight.Rflags;
        if (transitionState.OwnedCr3Raw != ownedRoot || transitionState.StackStart != stackStart || transitionState.StackEndExclusive != stackEnd || transitionState.CanaryAddress != canaryAddress || transitionState.ExpectedCanary != plan.StorageLayout.InitialCanaryValue || transitionState.ReplacementCanary != plan.StorageLayout.UpdatedCanaryValue || transitionState.OriginalRflags != originalRflags || transitionState.OriginalCr3Raw != plan.RawFirmwareCr3 || transitionState.OriginalCr4Raw != rawCr4 || transitionState.ClearedCr4Readback != (rawCr4 & ~X64ReversibleCr3TransitionStubPlan.Cr4PageGlobalEnableBit) || transitionState.OwnedCr3Readback != ownedRoot || transitionState.CanaryBefore != plan.StorageLayout.InitialCanaryValue || transitionState.CanaryAfter != plan.StorageLayout.UpdatedCanaryValue || transitionState.FirmwareCr3Readback != plan.RawFirmwareCr3 || transitionState.RestoredCr4Readback != rawCr4 || (originalRflags & X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit) != X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit || (transitionState.CliRflagsReadback & (X64ReversibleCr3TransitionStubPlan.RflagsInterruptEnableBit | X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit)) != X64ReversibleCr3TransitionStubPlan.RflagsArchitecturalReservedOneBit || transitionState.RestoredRflagsReadback != originalRflags || transitionState.EntryRsp != transitionState.ExitRsp || stackStart > ~0UL - RequiredProbeStackHeadroomBytes || transitionState.EntryRsp < stackStart + RequiredProbeStackHeadroomBytes || transitionState.EntryRsp > ~0UL - RequiredProbeCallFrameBytes || transitionState.EntryRsp + RequiredProbeCallFrameBytes > stackEnd || (transitionState.EntryRsp & 15UL) != 8UL || transitionState.PostRestoreCanaryReadback != plan.StorageLayout.UpdatedCanaryValue || transitionState.FirmwareCallbackCount != 0 || transitionState.RuntimeStatus != X64ReversibleCr3TransitionRuntimeStatus.Success || transitionState.Checkpoint != X64ReversibleCr3TransitionCheckpoint.Returned)
        {
            return Failure(X64Cr3LoadStubPlanStatus.InvalidTransitionPlan);
        }

        return composed;
        X64Cr3LoadStubPlanResult Failure(X64Cr3LoadStubPlanStatus status)
        {
            return X64Cr3LoadStubPlanResult.Create(status, OS.Boot.AddressValueStatus.Success, mappingPlan.Status, stackSwitchPlan.Status, allocationRecordCount, allocationRecordCapacity, mappingRangeCount, mappingRangeCapacity, NoIndex, NoIndex, default);
        }
    }

    internal static X64Cr3LoadStubPlan CreateValidated(X64Cr3ProbePolicy policy, OS.Boot.BootMemoryRegionRecord codeAllocation, OS.Boot.BootMemoryRegionRecord dataAllocation, OS.Kernel.Boot.X64BootIdentityMappingPlanResult identityMappingPlan, OS.Boot.OwnedBootStackSwitchPlanResult stackSwitchPlan, X64Cr3ProbeStorageLayout storageLayout, ulong rawFirmwareCr3, OS.Boot.PhysicalAddress firmwareRootBase, OS.Boot.PhysicalAddress ownedRoot, OS.Boot.PhysicalAddress tableArenaStart, OS.Boot.PhysicalAddress tableArenaEndExclusive, OS.Boot.VirtualAddress statePageStart, OS.Boot.VirtualAddress statePageEndExclusive, OS.Boot.VirtualAddress usableStackStart, OS.Boot.VirtualAddress usableStackEndExclusive, ulong physicalAddressMask, byte physicalAddressWidth, ulong preflightEntryOffset, ulong preflightByteLength, ulong transitionEntryOffset, ulong transitionByteLength, ulong transitionRuntimeStateAddress)
    {
        return new X64Cr3LoadStubPlan(policy, codeAllocation, dataAllocation, identityMappingPlan, stackSwitchPlan, storageLayout, rawFirmwareCr3, firmwareRootBase, ownedRoot, tableArenaStart, tableArenaEndExclusive, statePageStart, statePageEndExclusive, usableStackStart, usableStackEndExclusive, physicalAddressMask, physicalAddressWidth, preflightEntryOffset, preflightByteLength, transitionEntryOffset, transitionByteLength, transitionRuntimeStateAddress);
    }

    public unsafe X64Cr3ProbeAuditResult Audit(X64Cr3ProbeEvent* events, ulong eventCount, ulong eventCapacity)
    {
        return X64Cr3ProbeTranscriptAuditor.Audit(this, events, eventCount, eventCapacity);
    }
}
