namespace OS.Kernel.Memory;

internal static unsafe class X64Cr3LoadStubPlanComposer
{
    private const ulong MaximumUnsignedLong = ~0UL;
    private const ulong PageSize = OS.Boot.BootMemoryRegionRecord.PageSize;
    private const ulong Cr3PageMask = ~0xFFFUL;
    private const ulong Cr3PwtPcdMask = 0x18UL;
    internal static X64Cr3LoadStubPlanResult CreateCore(X64Cr3ProbePolicy policy, OS.Boot.BootMemoryRegionRecord* allocationRecords, ulong allocationRecordCount, ulong allocationRecordCapacity, OS.Kernel.Boot.X64BootIdentityMappingPlanResult mappingPlan, OS.Kernel.Boot.X64BootIdentityMappingRange* mappingRanges, ulong mappingRangeCount, ulong mappingRangeCapacity, OS.Boot.OwnedBootStackSwitchPlanResult stackSwitchPlan, X64Cr3ProbeStorageLayout storageLayout, X64PagingModePreflightResult preflight, X64PagingModePreflightStubResult preflightMaterialization, X64PagingModePreflightStubResult preflightReadback, bool transitionPlanValid, bool transitionMaterializationValid, bool transitionReadbackValid, bool transitionEvidenceValid, ulong transitionRuntimeStateAddress, ulong preflightEntryOffset, ulong transitionEntryOffset, ulong transitionByteLength)
    {
        OS.Kernel.Boot.X64BootIdentityMappingStatus mappingStatus = mappingPlan.Status;
        OS.Boot.OwnedBootStackSwitchStatus stackStatus = stackSwitchPlan.Status;
        if ((ulong)sizeof(X64Cr3ProbePolicy) != X64Cr3ProbePolicy.StructureSize || (ulong)sizeof(X64Cr3ProbeStorageLayout) != X64Cr3ProbeStorageLayout.StructureSize || (ulong)sizeof(X64Cr3ProbeEvent) != X64Cr3ProbeEvent.StructureSize || (ulong)sizeof(X64Cr3LoadStubPlan) != X64Cr3LoadStubPlan.StructureSize || (ulong)sizeof(X64Cr3LoadStubPlanResult) != X64Cr3LoadStubPlanResult.StructureSize || (ulong)sizeof(X64Cr3ProbeAuditResult) != X64Cr3ProbeAuditResult.StructureSize)
        {
            return Fail(X64Cr3LoadStubPlanStatus.ContractLayoutMismatch);
        }

        if (!policy.IsInitialized)
            return Fail(X64Cr3LoadStubPlanStatus.InvalidPolicy);
        if (policy.EnabledByDefault)
            return Fail(X64Cr3LoadStubPlanStatus.EnabledByDefault);
        if (!policy.ExplicitOneShotOptIn)
            return Fail(X64Cr3LoadStubPlanStatus.ExplicitOptInRequired);
        if (policy.TimeoutTicks == 0)
            return Fail(X64Cr3LoadStubPlanStatus.InvalidTimeout);
        if (allocationRecordCount != X64Cr3LoadStubPlan.RequiredAllocationRecordCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidAllocationRecordCount);
        }

        if (allocationRecordCapacity > X64Cr3LoadStubPlan.RequiredAllocationRecordCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.TooManyAllocationRecords);
        }

        if (allocationRecordCapacity < allocationRecordCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.AllocationRecordCapacityTooSmall);
        }

        if (allocationRecords == null)
            return Fail(X64Cr3LoadStubPlanStatus.MissingAllocationRecords);
        ulong allocationAddress = (ulong)allocationRecords;
        if ((allocationAddress & (X64Cr3LoadStubPlan.RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MisalignedAllocationRecords);
        }

        if (!TryGetSpanEnd(allocationAddress, allocationRecordCapacity, (ulong)sizeof(OS.Boot.BootMemoryRegionRecord), out ulong ignoredAllocationEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.AllocationRecordSpanOverflow);
        }

        OS.Boot.BootMemoryRegionRecord code = allocationRecords[X64Cr3LoadStubPlan.CodeAllocationRecordIndex];
        OS.Boot.BootMemoryRegionRecord data = allocationRecords[X64Cr3LoadStubPlan.DataAllocationRecordIndex];
        if (code.UefiType != (uint)OS.Boot.UefiMemoryType.LoaderCode)
            return Fail(X64Cr3LoadStubPlanStatus.InvalidCodeMemoryType);
        if (!TryValidateAllocation(code, X64Cr3LoadStubPlan.RequiredCodePageCount, out ulong codeStart, out ulong codeEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidCodeAllocation);
        }

        if (data.UefiType != (uint)OS.Boot.UefiMemoryType.LoaderData)
            return Fail(X64Cr3LoadStubPlanStatus.InvalidDataMemoryType);
        if (!mappingPlan.Succeeded)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidMappingPlan, mappingStatus, stackStatus);
        }

        if (mappingPlan.ValidatedSourceCount != mappingPlan.SourceCount || mappingPlan.RequiredPurposeMask != OS.Kernel.Boot.X64BootIdentityMappingPlan.RequiredPurposeMask || (mappingPlan.ObservedPurposeMask & mappingPlan.RequiredPurposeMask) != mappingPlan.RequiredPurposeMask || mappingPlan.MissingPurposeMask != 0 || mappingPlan.InvalidSourceIndex != OS.Kernel.Boot.X64BootIdentityMappingPlanResult.NoIndex || mappingPlan.ConflictingSourceIndex != OS.Kernel.Boot.X64BootIdentityMappingPlanResult.NoIndex || mappingPlan.UnionPageCount == 0 || mappingPlan.UnionPageCount > MaximumUnsignedLong / PageSize || mappingPlan.UnionByteLength != mappingPlan.UnionPageCount * PageSize || mappingPlan.TotalTablePageCount == 0 || mappingPlan.TotalTablePageCount > MaximumUnsignedLong / PageSize || mappingPlan.TotalTableByteLength != mappingPlan.TotalTablePageCount * PageSize || mappingPlan.Pml4PageCount != 1 || !TryAddTablePages(mappingPlan.Pml4PageCount, mappingPlan.PdptPageCount, mappingPlan.PdPageCount, mappingPlan.PtPageCount, out ulong summedTablePages) || summedTablePages != mappingPlan.TotalTablePageCount || mappingPlan.AvailableTablePageCapacity < mappingPlan.TotalTablePageCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidMappingPlan, mappingStatus, stackStatus);
        }

        if (mappingPlan.TotalTablePageCount == 0 || mappingPlan.TotalTablePageCount > MaximumUnsignedLong - X64Cr3LoadStubPlan.RequiredStatePageCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.DataArenaPageCountMismatch, mappingStatus, stackStatus);
        }

        ulong requiredDataPages = mappingPlan.TotalTablePageCount + X64Cr3LoadStubPlan.RequiredStatePageCount;
        if (!TryValidateAllocation(data, requiredDataPages, out ulong dataStart, out ulong dataEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidDataAllocation, mappingStatus, stackStatus);
        }

        if (RangesOverlap(codeStart, codeEnd, dataStart, dataEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.AllocationOverlap, mappingStatus, stackStatus);
        }

        if (mappingRangeCount != mappingPlan.OutputRangeCount || mappingRangeCount == 0)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidMappingRangeCount, mappingStatus, stackStatus);
        }

        if (mappingRangeCapacity < mappingRangeCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MappingRangeCapacityTooSmall, mappingStatus, stackStatus);
        }

        if (mappingRangeCapacity != mappingPlan.DestinationCapacity || mappingRangeCapacity > OS.Kernel.Boot.X64BootIdentityMappingPlan.MaximumDestinationCount)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidMappingRangeCount, mappingStatus, stackStatus);
        }

        if (mappingRanges == null)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MissingMappingRanges, mappingStatus, stackStatus);
        }

        ulong mappingAddress = (ulong)mappingRanges;
        if ((mappingAddress & (X64Cr3LoadStubPlan.RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MisalignedMappingRanges, mappingStatus, stackStatus);
        }

        if (!TryGetSpanEnd(mappingAddress, mappingRangeCapacity, OS.Kernel.Boot.X64BootIdentityMappingRange.StructureSize, out ulong ignoredMappingEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.MappingRangeSpanOverflow, mappingStatus, stackStatus);
        }

        ulong unionPages = 0;
        ulong unionBytes = 0;
        ulong previousEnd = 0;
        bool hasCodeMapping = false;
        bool hasDataMapping = false;
        bool hasStackMapping = false;
        ulong additionalRequiredMask = 1UL << (byte)OS.Kernel.Boot.X64BootIdentityMappingPurpose.AdditionalRequired;
        ulong ownedStackMask = 1UL << (byte)OS.Kernel.Boot.X64BootIdentityMappingPurpose.OwnedStack;
        if (!stackSwitchPlan.Succeeded)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidStackSwitchPlan, mappingStatus, stackStatus);
        }

        OS.Boot.BootMemoryRegionRecord stackCodeAllocation = stackSwitchPlan.Plan.CodeAllocation;
        OS.Boot.BootMemoryRegionRecord stackDataAllocation = stackSwitchPlan.Plan.DataAllocation;
        if (stackCodeAllocation.UefiType != (uint)OS.Boot.UefiMemoryType.LoaderCode || stackDataAllocation.UefiType != (uint)OS.Boot.UefiMemoryType.LoaderData || !TryValidateAllocation(stackCodeAllocation, OS.Boot.OwnedBootStackSwitchPlan.RequiredCodePageCount, out ulong stackCodeStart, out ulong stackCodeEnd) || !TryValidateAllocation(stackDataAllocation, stackDataAllocation.PageCount, out ulong stackDataStart, out ulong stackDataEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidStackSwitchPlan, mappingStatus, stackStatus);
        }

        if (RangesOverlap(codeStart, codeEnd, stackCodeStart, stackCodeEnd) || RangesOverlap(codeStart, codeEnd, stackDataStart, stackDataEnd) || RangesOverlap(dataStart, dataEnd, stackCodeStart, stackCodeEnd) || RangesOverlap(dataStart, dataEnd, stackDataStart, stackDataEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.StackSwitchAllocationOverlap, mappingStatus, stackStatus);
        }

        OS.Boot.VirtualAddress expectedRspAddress = stackSwitchPlan.Plan.ExpectedContinuationStackPointer;
        if (!TryGetVirtualRaw(expectedRspAddress, out ulong expectedRsp) || !TryGetVirtualRaw(stackSwitchPlan.Plan.UsableStackVirtualStart, out ulong usableStackStart) || !TryGetVirtualRaw(stackSwitchPlan.Plan.UsableStackVirtualEndExclusive, out ulong usableStackEnd) || expectedRsp < usableStackStart || expectedRsp >= usableStackEnd)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidExpectedRsp, mappingStatus, stackStatus);
        }

        for (ulong index = 0; index < mappingRangeCount; index++)
        {
            OS.Kernel.Boot.X64BootIdentityMappingRange range = mappingRanges[index];
            if (!TryValidateRange(range, previousEnd, out ulong rangeStart, out ulong rangeEnd))
            {
                return Fail(X64Cr3LoadStubPlanStatus.InvalidMappingRange, mappingStatus, stackStatus, invalidIndex: index);
            }

            previousEnd = rangeEnd;
            if (unionPages > MaximumUnsignedLong - range.PageCount || unionBytes > MaximumUnsignedLong - range.ByteLength)
            {
                return Fail(X64Cr3LoadStubPlanStatus.MappingAccountingMismatch, mappingStatus, stackStatus, invalidIndex: index);
            }

            unionPages += range.PageCount;
            unionBytes += range.ByteLength;
            byte protection = (byte)range.Protection;
            if ((protection & ((byte)OS.Kernel.Memory.PageProtection.Write | (byte)OS.Kernel.Memory.PageProtection.Execute)) == ((byte)OS.Kernel.Memory.PageProtection.Write | (byte)OS.Kernel.Memory.PageProtection.Execute))
            {
                return Fail(X64Cr3LoadStubPlanStatus.WritableExecutableMapping, mappingStatus, stackStatus, invalidIndex: index);
            }

            bool hasAdditional = (range.ContributingPurposeMask & additionalRequiredMask) != 0;
            bool hasOwnedStack = (range.ContributingPurposeMask & ownedStackMask) != 0;
            if (hasAdditional && Covers(rangeStart, rangeEnd, codeStart, codeEnd) && range.Protection == (OS.Kernel.Memory.PageProtection.Read | OS.Kernel.Memory.PageProtection.Execute) && range.CachePolicy == OS.Kernel.Memory.PageCachePolicy.WriteBack)
            {
                hasCodeMapping = true;
            }

            if (hasAdditional && Covers(rangeStart, rangeEnd, dataStart, dataEnd) && range.Protection == (OS.Kernel.Memory.PageProtection.Read | OS.Kernel.Memory.PageProtection.Write) && range.CachePolicy == OS.Kernel.Memory.PageCachePolicy.WriteBack)
            {
                hasDataMapping = true;
            }

            if (hasOwnedStack && Covers(rangeStart, rangeEnd, usableStackStart, usableStackEnd) && expectedRsp >= rangeStart && expectedRsp < rangeEnd && (protection & (byte)OS.Kernel.Memory.PageProtection.Write) != 0 && (protection & (byte)OS.Kernel.Memory.PageProtection.Execute) == 0 && range.CachePolicy == OS.Kernel.Memory.PageCachePolicy.WriteBack)
            {
                hasStackMapping = true;
            }
        }

        if (unionPages != mappingPlan.UnionPageCount || unionBytes != mappingPlan.UnionByteLength)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MappingAccountingMismatch, mappingStatus, stackStatus);
        }

        if (!hasCodeMapping)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MissingStubMapping, mappingStatus, stackStatus);
        }

        if (!hasDataMapping)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MissingDataArenaMapping, mappingStatus, stackStatus);
        }

        if (!hasStackMapping)
        {
            return Fail(X64Cr3LoadStubPlanStatus.MissingStackMapping, mappingStatus, stackStatus);
        }

        if (!preflight.Succeeded || preflight.PhysicalAddressMask == 0 || preflight.PhysicalAddressWidth < 36 || preflight.PhysicalAddressWidth > 52 || preflight.PhysicalAddressMask != ((1UL << preflight.PhysicalAddressWidth) - 1) || (preflight.Cr0 & X64PagingModePreflightStubPlan.Cr0PagingBit) == 0 || (preflight.Cr0 & X64PagingModePreflightStubPlan.Cr0WriteProtectBit) == 0 || (preflight.RawCr4 & X64PagingModePreflightStubPlan.Cr4PaeBit) == 0 || (preflight.RawCr4 & X64PagingModePreflightStubPlan.Cr4La57Bit) != 0 || (preflight.RawCr4 & X64PagingModePreflightStubPlan.Cr4PcideBit) != 0 || (preflight.Ia32Efer & X64PagingModePreflightStubPlan.EferLongModeEnableBit) == 0 || (preflight.Ia32Efer & X64PagingModePreflightStubPlan.EferLongModeActiveBit) == 0 || (preflight.Ia32Efer & X64PagingModePreflightStubPlan.EferNoExecuteEnableBit) == 0)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidPreflightPlan, mappingStatus, stackStatus);
        }

        ulong physicalAddressMask = preflight.PhysicalAddressMask;
        ulong rawFirmwareCr3 = preflight.RawFirmwareCr3;
        ulong permittedFirmwareCr3Bits = (physicalAddressMask & Cr3PageMask) | Cr3PwtPcdMask;
        if (rawFirmwareCr3 == 0 || (rawFirmwareCr3 & ~permittedFirmwareCr3Bits) != 0)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidOriginalRoot, mappingStatus, stackStatus);
        }

        ulong firmwareRootRaw = rawFirmwareCr3 & physicalAddressMask & Cr3PageMask;
        if (firmwareRootRaw == 0 || OS.Boot.PhysicalAddress.TryCreateAlignedFromHardwareRegisterBoundary(firmwareRootRaw, PageSize, out OS.Boot.PhysicalAddress firmwareRootBase) != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidOriginalRoot, mappingStatus, stackStatus);
        }

        if (!TryGetPhysicalRaw(preflight.OwnedRoot, out ulong ownedRoot) || (ownedRoot & (PageSize - 1)) != 0 || (ownedRoot & ~physicalAddressMask) != 0)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidOwnedRoot, mappingStatus, stackStatus);
        }

        if (firmwareRootRaw == ownedRoot)
        {
            return Fail(X64Cr3LoadStubPlanStatus.RootsNotDistinct, mappingStatus, stackStatus);
        }

        if (!TryGetPhysicalRaw(preflight.TableArenaStart, out ulong tableStart) || !TryGetPhysicalRaw(preflight.TableArenaEndExclusive, out ulong tableEnd) || tableStart != dataStart || tableEnd <= tableStart || tableEnd - tableStart != mappingPlan.TotalTableByteLength || ownedRoot != tableStart)
        {
            return Fail(X64Cr3LoadStubPlanStatus.OwnedRootOutsideTableArena, mappingStatus, stackStatus);
        }

        if (firmwareRootRaw >= dataStart && firmwareRootRaw < dataEnd)
        {
            return Fail(X64Cr3LoadStubPlanStatus.OriginalRootInsideOwnedArena, mappingStatus, stackStatus);
        }

        ulong statePageStartRaw = tableEnd;
        if (statePageStartRaw > MaximumUnsignedLong - PageSize || statePageStartRaw + PageSize != dataEnd || OS.Boot.VirtualAddress.TryCreateAlignedFromUefiBoundary(statePageStartRaw, PageSize, out OS.Boot.VirtualAddress statePageStart) != OS.Boot.AddressValueStatus.Success || OS.Boot.VirtualAddress.TryCreateAlignedFromUefiBoundary(dataEnd, PageSize, out OS.Boot.VirtualAddress statePageEnd) != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidStateStorage, mappingStatus, stackStatus);
        }

        if (!storageLayout.IsInitialized || storageLayout.Provenance != X64Cr3ProbeStorageProvenance.CallerAttestedOwnedLoaderData)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidStorageLayout, mappingStatus, stackStatus);
        }

        if (!TryGetVirtualRaw(storageLayout.StateAddress, out ulong stateStart) || storageLayout.StateByteLength != X64Cr3ProbeStorageLayout.RequiredStateRecordByteLength || stateStart != statePageStartRaw || !TryGetRangeEnd(stateStart, storageLayout.StateByteLength, out ulong stateEnd) || stateEnd > dataEnd)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidStateStorage, mappingStatus, stackStatus);
        }

        if (!TryGetVirtualRaw(storageLayout.CanaryAddress, out ulong canaryStart) || storageLayout.CanaryByteLength != X64Cr3ProbeStorageLayout.RequiredCanaryByteLength || (canaryStart & (X64Cr3ProbeStorageLayout.RequiredAlignment - 1)) != 0 || !TryGetRangeEnd(canaryStart, storageLayout.CanaryByteLength, out ulong canaryEnd) || canaryStart < statePageStartRaw || canaryEnd > dataEnd)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidCanaryStorage, mappingStatus, stackStatus);
        }

        if (RangesOverlap(stateStart, stateEnd, canaryStart, canaryEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.StorageOverlap, mappingStatus, stackStatus);
        }

        if (storageLayout.InitialCanaryValue == 0 || storageLayout.UpdatedCanaryValue == 0 || storageLayout.InitialCanaryValue == storageLayout.UpdatedCanaryValue)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidCanaryValues, mappingStatus, stackStatus);
        }

        if (!preflightMaterialization.Succeeded || preflightMaterialization.Operation != X64PagingModePreflightOperation.Materialize || preflightMaterialization.EntryOffset != preflightEntryOffset)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidPreflightMaterialization, mappingStatus, stackStatus);
        }

        if (!preflightReadback.Succeeded || preflightReadback.Operation != X64PagingModePreflightOperation.Validate || preflightReadback.EntryOffset != preflightEntryOffset)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidPreflightReadback, mappingStatus, stackStatus);
        }

        if (!transitionPlanValid || !transitionEvidenceValid)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidTransitionPlan, mappingStatus, stackStatus);
        }

        if (!transitionMaterializationValid)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidTransitionMaterialization, mappingStatus, stackStatus);
        }

        if (!transitionReadbackValid)
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidTransitionReadback, mappingStatus, stackStatus);
        }

        ulong preflightByteLength = X64PagingModePreflightStubPlan.RequiredByteLength;
        if (!TryValidateStubRange(codeStart, codeEnd, preflightEntryOffset, preflightByteLength, out ulong preflightStart, out ulong preflightEnd) || !TryValidateStubRange(codeStart, codeEnd, transitionEntryOffset, transitionByteLength, out ulong transitionStart, out ulong transitionEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.StubByteRangeOutsideCodePage, mappingStatus, stackStatus);
        }

        if ((preflightStart & (X64Cr3LoadStubPlan.RequiredStubEntryAlignment - 1)) != 0 || (transitionStart & (X64Cr3LoadStubPlan.RequiredStubEntryAlignment - 1)) != 0)
        {
            return Fail(X64Cr3LoadStubPlanStatus.StubEntryMisaligned, mappingStatus, stackStatus);
        }

        if (RangesOverlap(preflightStart, preflightEnd, transitionStart, transitionEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.StubByteRangeOverlap, mappingStatus, stackStatus);
        }

        X64Cr3LoadStubPlan plan = X64Cr3LoadStubPlan.CreateValidated(policy, code, data, mappingPlan, stackSwitchPlan, storageLayout, rawFirmwareCr3, firmwareRootBase, preflight.OwnedRoot, preflight.TableArenaStart, preflight.TableArenaEndExclusive, statePageStart, statePageEnd, stackSwitchPlan.Plan.UsableStackVirtualStart, stackSwitchPlan.Plan.UsableStackVirtualEndExclusive, physicalAddressMask, preflight.PhysicalAddressWidth, preflightEntryOffset, preflightByteLength, transitionEntryOffset, transitionByteLength, transitionRuntimeStateAddress);
        return X64Cr3LoadStubPlanResult.Create(X64Cr3LoadStubPlanStatus.Success, OS.Boot.AddressValueStatus.Success, mappingStatus, stackStatus, allocationRecordCount, allocationRecordCapacity, mappingRangeCount, mappingRangeCapacity, X64Cr3LoadStubPlan.NoIndex, X64Cr3LoadStubPlan.NoIndex, plan);
        X64Cr3LoadStubPlanResult Fail(X64Cr3LoadStubPlanStatus status, OS.Kernel.Boot.X64BootIdentityMappingStatus failureMappingStatus = OS.Kernel.Boot.X64BootIdentityMappingStatus.Uninitialized, OS.Boot.OwnedBootStackSwitchStatus failureStackStatus = OS.Boot.OwnedBootStackSwitchStatus.Uninitialized, OS.Boot.AddressValueStatus addressStatus = OS.Boot.AddressValueStatus.Success, ulong invalidIndex = X64Cr3LoadStubPlan.NoIndex, ulong conflictingIndex = X64Cr3LoadStubPlan.NoIndex)
        {
            return X64Cr3LoadStubPlanResult.Create(status, addressStatus, failureMappingStatus, failureStackStatus, allocationRecordCount, allocationRecordCapacity, mappingRangeCount, mappingRangeCapacity, invalidIndex, conflictingIndex, default);
        }
    }

    private static bool TryValidateAllocation(OS.Boot.BootMemoryRegionRecord record, ulong expectedPageCount, out ulong start, out ulong end)
    {
        start = record.PhysicalStart;
        end = record.PhysicalEndExclusive;
        if (start == 0 || (start & (PageSize - 1)) != 0 || record.VirtualStart != start || record.PageCount != expectedPageCount || expectedPageCount == 0 || expectedPageCount > MaximumUnsignedLong / PageSize)
        {
            return false;
        }

        ulong expectedBytes = expectedPageCount * PageSize;
        return record.ByteLength == expectedBytes && start <= MaximumUnsignedLong - expectedBytes && end == start + expectedBytes;
    }

    private static bool TryValidateRange(OS.Kernel.Boot.X64BootIdentityMappingRange range, ulong previousEnd, out ulong start, out ulong end)
    {
        start = 0;
        end = 0;
        if (!range.IsInitialized || range.PageCount == 0 || range.PageCount > MaximumUnsignedLong / PageSize || range.ByteLength != range.PageCount * PageSize || range.ContributingPurposeMask == 0 || (range.ContributingPurposeMask & ~0x17eUL) != 0 || range.Protection == OS.Kernel.Memory.PageProtection.None || ((byte)range.Protection & ~7) != 0 || range.CachePolicy == OS.Kernel.Memory.PageCachePolicy.Uninitialized || (byte)range.CachePolicy > (byte)OS.Kernel.Memory.PageCachePolicy.WriteCombining)
        {
            return false;
        }

        if (!TryGetPhysicalRaw(range.PhysicalStart, out start) || !TryGetPhysicalRaw(range.PhysicalEndExclusive, out end) || !TryGetVirtualRaw(range.VirtualStart, out ulong virtualStart) || !TryGetVirtualRaw(range.VirtualEndExclusive, out ulong virtualEnd) || start != virtualStart || end != virtualEnd || (start & (PageSize - 1)) != 0 || (end & (PageSize - 1)) != 0 || end <= start || end - start != range.ByteLength || start < previousEnd)
        {
            return false;
        }

        return true;
    }

    private static bool TryValidateStubRange(ulong codeStart, ulong codeEnd, ulong entryOffset, ulong byteLength, out ulong start, out ulong end)
    {
        start = 0;
        end = 0;
        if (byteLength == 0 || entryOffset >= PageSize || codeStart > MaximumUnsignedLong - entryOffset)
        {
            return false;
        }

        start = codeStart + entryOffset;
        return TryGetRangeEnd(start, byteLength, out end) && start >= codeStart && end <= codeEnd;
    }

    private static bool TryGetPhysicalRaw(OS.Boot.PhysicalAddress address, out ulong value)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(out value) == OS.Boot.AddressValueStatus.Success;
    }

    private static bool TryGetVirtualRaw(OS.Boot.VirtualAddress address, out ulong value)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(out value) == OS.Boot.AddressValueStatus.Success;
    }

    private static bool TryGetRangeEnd(ulong start, ulong byteLength, out ulong end)
    {
        end = 0;
        if (byteLength == 0 || start > MaximumUnsignedLong - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }

    private static bool TryAddTablePages(ulong first, ulong second, ulong third, ulong fourth, out ulong total)
    {
        total = 0;
        if (first > MaximumUnsignedLong - second)
            return false;
        ulong firstTwo = first + second;
        if (firstTwo > MaximumUnsignedLong - third)
            return false;
        ulong firstThree = firstTwo + third;
        if (firstThree > MaximumUnsignedLong - fourth)
            return false;
        total = firstThree + fourth;
        return true;
    }

    private static bool TryGetSpanEnd(ulong start, ulong capacity, ulong elementSize, out ulong end)
    {
        end = 0;
        if (capacity != 0 && elementSize > MaximumUnsignedLong / capacity)
        {
            return false;
        }

        ulong byteLength = capacity * elementSize;
        if (start > MaximumUnsignedLong - byteLength)
            return false;
        end = start + byteLength;
        return true;
    }

    private static bool Covers(ulong outerStart, ulong outerEnd, ulong innerStart, ulong innerEnd)
    {
        return outerStart <= innerStart && outerEnd >= innerEnd;
    }

    private static bool RangesOverlap(ulong leftStart, ulong leftEnd, ulong rightStart, ulong rightEnd)
    {
        return leftStart < rightEnd && rightStart < leftEnd;
    }
}
