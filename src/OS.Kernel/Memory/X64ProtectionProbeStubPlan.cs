namespace OS.Kernel.Memory;
/// <summary>
/// Emits and audits the exact MOV EAX,imm32; RET probe as ordinary data and
/// composes the host-only RW+NX to RO+X model. No emitted byte is invoked by
/// this type or by its host tests.
/// </summary>

public static unsafe class X64ProtectionProbeStubPlan
{
    public const ulong PageSize = 4096;
    public const ulong EntryAlignment = 16;
    public const ulong RequiredPointerAlignment = 16;
    public const ulong RequiredByteLength = 6;
    public const ulong AuditedInstructionCount = 2;
    public const uint ExpectedReturnValue = 0x4f53_4f4b;
    public const ulong RequiredEventCount = 9;
    public const bool HasExternalRelocations = false;
    public const bool UsesWindowsX64Abi = true;
    public const bool TouchesNonvolatileRegisters = false;
    public const bool TouchesXmm6ThroughXmm15 = false;
    public const bool PerformsLiveProtectionChange = false;
    public const bool PerformsLiveExecution = false;
    public const bool HostTestsInvokeBytes = false;
    public const bool IsLiveEvidence = false;
    public static X64ProtectionProbeStubPlanResult Emit(byte* destination, ulong modeledDestinationAddress, ulong destinationCapacity, OS.Boot.BootMemoryRegionRecord probeAllocation, BootReservedRange probeReservation, ulong entryOffset)
    {
        X64ProtectionProbeStatus geometryStatus = ValidateProbeGeometry(probeAllocation, probeReservation, entryOffset, out OS.Boot.VirtualAddress pageStart, out OS.Boot.VirtualAddress pageEnd, out OS.Boot.VirtualAddress entryAddress);
        if (geometryStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbeStubPlanResult.Fail(geometryStatus, destinationCapacity);
        }

        X64ProtectionProbeStatus storageStatus = ValidateStorage(destination, modeledDestinationAddress, destinationCapacity, probeAllocation.VirtualStart, X64ProtectionProbeStatus.MissingDestination, X64ProtectionProbeStatus.MisalignedDestination, X64ProtectionProbeStatus.DestinationCapacityTooSmall, X64ProtectionProbeStatus.DestinationCapacityExceedsPage, X64ProtectionProbeStatus.DestinationSpanOverflow, X64ProtectionProbeStatus.DestinationAddressMismatch);
        if (storageStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbeStubPlanResult.Fail(storageStatus, destinationCapacity);
        }

        byte* entry = destination + entryOffset;
        entry[0] = 0xb8;
        entry[1] = (byte)(ExpectedReturnValue & 0xffU);
        entry[2] = (byte)((ExpectedReturnValue >> 8) & 0xffU);
        entry[3] = (byte)((ExpectedReturnValue >> 16) & 0xffU);
        entry[4] = (byte)((ExpectedReturnValue >> 24) & 0xffU);
        entry[5] = 0xc3;
        return X64ProtectionProbeStubPlanResult.Success(X64ProtectionProbeStubPhase.Materialized, pageStart, pageEnd, entryAddress, entryOffset, destinationCapacity);
    }

    public static X64ProtectionProbeStubPlanResult ValidateReadback(byte* source, ulong modeledSourceAddress, ulong sourceCapacity, X64ProtectionProbeStubPlanResult expected)
    {
        if (!expected.Succeeded || expected.Phase != X64ProtectionProbeStubPhase.Materialized)
        {
            return X64ProtectionProbeStubPlanResult.Fail(X64ProtectionProbeStatus.ExpectedStubNotMaterialized, sourceCapacity);
        }

        if (expected.ProbePageStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawPageStart) != OS.Boot.AddressValueStatus.Success)
        {
            return X64ProtectionProbeStubPlanResult.Fail(X64ProtectionProbeStatus.InvalidExpectedStub, sourceCapacity);
        }

        X64ProtectionProbeStatus storageStatus = ValidateStorage(source, modeledSourceAddress, sourceCapacity, rawPageStart, X64ProtectionProbeStatus.MissingSource, X64ProtectionProbeStatus.MisalignedSource, X64ProtectionProbeStatus.SourceCapacityTooSmall, X64ProtectionProbeStatus.SourceCapacityExceedsPage, X64ProtectionProbeStatus.SourceSpanOverflow, X64ProtectionProbeStatus.SourceAddressMismatch);
        if (storageStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbeStubPlanResult.Fail(storageStatus, sourceCapacity);
        }

        byte* entry = source + expected.EntryOffset;
        if (entry[0] != 0xb8 || entry[1] != (byte)(ExpectedReturnValue & 0xffU) || entry[2] != (byte)((ExpectedReturnValue >> 8) & 0xffU) || entry[3] != (byte)((ExpectedReturnValue >> 16) & 0xffU) || entry[4] != (byte)((ExpectedReturnValue >> 24) & 0xffU) || entry[5] != 0xc3)
        {
            return X64ProtectionProbeStubPlanResult.Fail(X64ProtectionProbeStatus.ByteReadbackMismatch, sourceCapacity);
        }

        return X64ProtectionProbeStubPlanResult.Success(X64ProtectionProbeStubPhase.ReadBackVerified, expected.ProbePageStart, expected.ProbePageEndExclusive, expected.EntryAddress, expected.EntryOffset, sourceCapacity);
    }

    public static X64ProtectionProbePlanResult Audit(OS.Boot.BootMemoryRegionRecord probeAllocation, BootReservedRange probeReservation, OS.Boot.BootMemoryRegionRecord transitionCodeAllocation, OS.Boot.BootMemoryRegionRecord tableStateAllocation, PageProtection tableStateProtection, X64ProtectionProbeStubPlanResult probeStub, X64PageMapResult protectionMutation, ulong leafReadbackRaw, X64InvlpgStubPlanResult invlpgStub, X64InvlpgInvocationPlanResult invlpgInvocation, X64ProtectionProbeObservation observation, X64ProtectionProbeEvent* events, ulong eventCount, ulong eventCapacity)
    {
        X64ProtectionProbeStatus allocationStatus = ValidateAllocationAndReservation(probeAllocation, probeReservation);
        if (allocationStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbePlanResult.Fail(allocationStatus, probeAllocation, probeReservation, probeStub, protectionMutation, invlpgStub, invlpgInvocation, observation, eventCount, eventCapacity, leafReadbackRaw: leafReadbackRaw);
        }

        if (!ValidateAllocation(transitionCodeAllocation, (uint)OS.Boot.UefiMemoryType.LoaderCode, requireOnePage: true))
        {
            return Fail(X64ProtectionProbeStatus.InvalidTransitionCodeAllocation);
        }

        if (!ValidateAllocation(tableStateAllocation, (uint)OS.Boot.UefiMemoryType.LoaderData, requireOnePage: false))
        {
            return Fail(X64ProtectionProbeStatus.InvalidTableStateAllocation);
        }

        PageProtection requiredTableStateProtection = (PageProtection)((byte)PageProtection.Read | (byte)PageProtection.Write);
        if (tableStateProtection != requiredTableStateProtection)
        {
            return Fail(X64ProtectionProbeStatus.InvalidTableStateProtection);
        }

        if (AllocationsOverlap(probeAllocation, transitionCodeAllocation) || AllocationsOverlap(probeAllocation, tableStateAllocation) || AllocationsOverlap(transitionCodeAllocation, tableStateAllocation))
        {
            return Fail(X64ProtectionProbeStatus.AllocationOverlap);
        }

        if (!probeStub.ReadBackVerified || !TryGetVirtualAddress(probeAllocation.VirtualStart, out OS.Boot.VirtualAddress probePageStart) || !probeStub.ProbePageStart.Equals(probePageStart))
        {
            return Fail(X64ProtectionProbeStatus.StubAllocationMismatch);
        }

        X64ProtectionProbeStatus protectionStatus = ValidateProtectionMutation(probeAllocation, protectionMutation, leafReadbackRaw);
        if (protectionStatus != X64ProtectionProbeStatus.Success)
        {
            return Fail(protectionStatus);
        }

        if (!TableImageIsContained(tableStateAllocation, protectionMutation))
        {
            return Fail(X64ProtectionProbeStatus.TableImageOutsideStateAllocation);
        }

        if (!invlpgStub.ReadBackVerified)
        {
            return Fail(X64ProtectionProbeStatus.InvalidInvlpgStub);
        }

        if (!TryGetVirtualAddress(transitionCodeAllocation.VirtualStart, out OS.Boot.VirtualAddress transitionCodeStart) || !invlpgStub.CodePageStart.Equals(transitionCodeStart))
        {
            return Fail(X64ProtectionProbeStatus.InvlpgCodeAllocationMismatch);
        }

        if (!invlpgInvocation.Succeeded)
        {
            return Fail(X64ProtectionProbeStatus.InvalidInvlpgInvocation);
        }

        if (!invlpgStub.Target.Target.Equals(protectionMutation.InvalidationRange.VirtualStart) || invlpgStub.Target.Ordinal != 0 || !invlpgInvocation.Request.Target.Target.Equals(invlpgStub.Target.Target) || invlpgInvocation.Request.Target.Ordinal != invlpgStub.Target.Ordinal || !invlpgInvocation.Request.EntryAddress.Equals(invlpgStub.EntryAddress))
        {
            return Fail(X64ProtectionProbeStatus.InvlpgTargetMismatch);
        }

        X64ProtectionProbeStatus observationStatus = observation.Validate();
        if (observationStatus != X64ProtectionProbeStatus.Success)
        {
            return Fail(observationStatus);
        }

        if (!observation.EntryAddress.Equals(probeStub.EntryAddress))
        {
            return Fail(X64ProtectionProbeStatus.InvocationEntryMismatch);
        }

        X64ProtectionProbeStatus eventStatus = ValidateEvents(events, eventCount, eventCapacity, out ulong validatedEventCount, out ulong invalidEventIndex);
        if (eventStatus != X64ProtectionProbeStatus.Success)
        {
            return X64ProtectionProbePlanResult.Fail(eventStatus, probeAllocation, probeReservation, probeStub, protectionMutation, invlpgStub, invlpgInvocation, observation, eventCount, eventCapacity, validatedEventCount, invalidEventIndex, leafReadbackRaw);
        }

        return X64ProtectionProbePlanResult.Success(probeAllocation, probeReservation, probeStub, protectionMutation, invlpgStub, invlpgInvocation, observation, eventCount, eventCapacity, leafReadbackRaw);
        X64ProtectionProbePlanResult Fail(X64ProtectionProbeStatus status)
        {
            return X64ProtectionProbePlanResult.Fail(status, probeAllocation, probeReservation, probeStub, protectionMutation, invlpgStub, invlpgInvocation, observation, eventCount, eventCapacity, leafReadbackRaw: leafReadbackRaw);
        }
    }

    private static X64ProtectionProbeStatus ValidateProtectionMutation(OS.Boot.BootMemoryRegionRecord probeAllocation, X64PageMapResult protectionMutation, ulong leafReadbackRaw)
    {
        if (!protectionMutation.Succeeded || protectionMutation.Request.Operation != X64PageMapOperation.Protect)
        {
            return X64ProtectionProbeStatus.InvalidProtectionMutation;
        }

        if (!TryGetVirtualAddress(probeAllocation.VirtualStart, out OS.Boot.VirtualAddress virtualStart) || !protectionMutation.Request.VirtualAddress.Equals(virtualStart) || !protectionMutation.InvalidationRange.VirtualStart.Equals(virtualStart))
        {
            return X64ProtectionProbeStatus.ProtectionTargetMismatch;
        }

        X64PageTableEntryResult before = X64PageTableEntry.TryDecode(X64PageTableLevel.Pte, protectionMutation.PreviousEntry.RawValue, protectionMutation.Request.Preconditions);
        X64PageTableEntryResult after = X64PageTableEntry.TryDecode(X64PageTableLevel.Pte, protectionMutation.ReplacementEntry.RawValue, protectionMutation.Request.Preconditions);
        if (!before.Succeeded || !after.Succeeded)
        {
            return X64ProtectionProbeStatus.InvalidProtectionMutation;
        }

        PageProtection readWrite = (PageProtection)((byte)PageProtection.Read | (byte)PageProtection.Write);
        PageProtection readExecute = (PageProtection)((byte)PageProtection.Read | (byte)PageProtection.Execute);
        if (before.Protection != readWrite)
        {
            return X64ProtectionProbeStatus.InitialProtectionMismatch;
        }

        if (after.Protection != readExecute)
        {
            return X64ProtectionProbeStatus.FinalProtectionMismatch;
        }

        if (!before.PhysicalAddress.Equals(after.PhysicalAddress) || before.PhysicalAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong rawPhysical) != OS.Boot.AddressValueStatus.Success || rawPhysical != probeAllocation.PhysicalStart)
        {
            return X64ProtectionProbeStatus.ProtectionPhysicalMismatch;
        }

        if (before.CachePolicy != PageCachePolicy.WriteBack || after.CachePolicy != PageCachePolicy.WriteBack)
        {
            return X64ProtectionProbeStatus.ProtectionCacheMismatch;
        }

        if (leafReadbackRaw != protectionMutation.ReplacementEntry.RawValue)
        {
            return X64ProtectionProbeStatus.LeafReadbackMismatch;
        }

        return X64ProtectionProbeStatus.Success;
    }

    private static X64ProtectionProbeStatus ValidateEvents(X64ProtectionProbeEvent* events, ulong eventCount, ulong eventCapacity, out ulong validatedEventCount, out ulong invalidEventIndex)
    {
        validatedEventCount = 0;
        invalidEventIndex = X64ProtectionProbePlanResult.NoIndex;
        if (eventCount != RequiredEventCount)
        {
            return X64ProtectionProbeStatus.InvalidEventCount;
        }

        if (eventCapacity < eventCount)
        {
            return X64ProtectionProbeStatus.EventCapacityTooSmall;
        }

        if (eventCapacity > RequiredEventCount)
        {
            return X64ProtectionProbeStatus.EventCapacityExceedsLimit;
        }

        if (events == null)
        {
            return X64ProtectionProbeStatus.MissingEvents;
        }

        ulong address = (ulong)events;
        if ((address & 7) != 0)
        {
            return X64ProtectionProbeStatus.MisalignedEvents;
        }

        if (eventCapacity > ~0UL / X64ProtectionProbeEvent.StructureSize || address > ~0UL - eventCapacity * X64ProtectionProbeEvent.StructureSize)
        {
            return X64ProtectionProbeStatus.EventSpanOverflow;
        }

        for (ulong index = 0; index < eventCount; index++)
        {
            X64ProtectionProbeStatus eventStatus = events[index].Validate();
            if (eventStatus != X64ProtectionProbeStatus.Success)
            {
                invalidEventIndex = index;
                return eventStatus;
            }

            X64ProtectionProbeEventKind expected = (X64ProtectionProbeEventKind)(index + 1);
            if (events[index].Ordinal != index || events[index].Kind != expected)
            {
                invalidEventIndex = index;
                if (events[index].Kind == X64ProtectionProbeEventKind.ProbeInvocationPlanned && index <= 3)
                {
                    return X64ProtectionProbeStatus.ExecuteBeforeProtectRejected;
                }

                return X64ProtectionProbeStatus.EventOrderMismatch;
            }

            validatedEventCount++;
        }

        return X64ProtectionProbeStatus.Success;
    }

    private static bool TableImageIsContained(OS.Boot.BootMemoryRegionRecord tableStateAllocation, X64PageMapResult protectionMutation)
    {
        if (!protectionMutation.Succeeded || protectionMutation.PageImagePhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong pageImageStart) != OS.Boot.AddressValueStatus.Success || protectionMutation.PageImagePhysicalEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong pageImageEnd) != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        return pageImageStart >= tableStateAllocation.PhysicalStart && pageImageEnd > pageImageStart && pageImageEnd <= tableStateAllocation.PhysicalEndExclusive;
    }

    private static X64ProtectionProbeStatus ValidateProbeGeometry(OS.Boot.BootMemoryRegionRecord probeAllocation, BootReservedRange probeReservation, ulong entryOffset, out OS.Boot.VirtualAddress pageStart, out OS.Boot.VirtualAddress pageEnd, out OS.Boot.VirtualAddress entryAddress)
    {
        pageStart = default;
        pageEnd = default;
        entryAddress = default;
        X64ProtectionProbeStatus status = ValidateAllocationAndReservation(probeAllocation, probeReservation);
        if (status != X64ProtectionProbeStatus.Success)
        {
            return status;
        }

        if (!TryGetVirtualAddress(probeAllocation.VirtualStart, out pageStart) || pageStart.TryGetEndExclusive(PageSize, out pageEnd) != OS.Boot.AddressValueStatus.Success)
        {
            return X64ProtectionProbeStatus.InvalidProbeAllocation;
        }

        if (entryOffset > PageSize - RequiredByteLength)
        {
            return X64ProtectionProbeStatus.EntryOutsideProbePage;
        }

        if ((entryOffset & (EntryAlignment - 1)) != 0)
        {
            return X64ProtectionProbeStatus.MisalignedEntry;
        }

        if (pageStart.TryAdd(entryOffset, out entryAddress) != OS.Boot.AddressValueStatus.Success || !entryAddress.IsAligned(EntryAlignment))
        {
            return X64ProtectionProbeStatus.InvalidEntryOffset;
        }

        return X64ProtectionProbeStatus.Success;
    }

    private static X64ProtectionProbeStatus ValidateAllocationAndReservation(OS.Boot.BootMemoryRegionRecord allocation, BootReservedRange reservation)
    {
        if (!ValidateAllocation(allocation, (uint)OS.Boot.UefiMemoryType.LoaderCode, requireOnePage: false))
        {
            return allocation.UefiType == (uint)OS.Boot.UefiMemoryType.LoaderCode ? X64ProtectionProbeStatus.InvalidProbeAllocation : X64ProtectionProbeStatus.ProbeAllocationMustBeLoaderCode;
        }

        if (allocation.PageCount != 1 || allocation.ByteLength != PageSize)
        {
            return X64ProtectionProbeStatus.ProbeAllocationMustBeOnePage;
        }

        if (allocation.VirtualStart != allocation.PhysicalStart)
        {
            return X64ProtectionProbeStatus.ProbeAllocationNotIdentityMapped;
        }

        if (!reservation.IsValid)
        {
            return X64ProtectionProbeStatus.InvalidProbeReservation;
        }

        if (reservation.Kind != BootReservationKind.PageProtectionProbe)
        {
            return X64ProtectionProbeStatus.ProbeReservationKindMismatch;
        }

        if (reservation.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong reservedStart) != OS.Boot.AddressValueStatus.Success || reservation.PhysicalEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong reservedEnd) != OS.Boot.AddressValueStatus.Success || reservedStart != allocation.PhysicalStart || reservedEnd != allocation.PhysicalEndExclusive || reservation.PageCount != allocation.PageCount || reservation.ByteLength != allocation.ByteLength)
        {
            return X64ProtectionProbeStatus.ProbeReservationRangeMismatch;
        }

        return X64ProtectionProbeStatus.Success;
    }

    private static bool ValidateAllocation(OS.Boot.BootMemoryRegionRecord allocation, uint expectedType, bool requireOnePage)
    {
        if (allocation.UefiType != expectedType || allocation.PhysicalStart == 0 || (allocation.PhysicalStart & (PageSize - 1)) != 0 || allocation.VirtualStart == 0 || (allocation.VirtualStart & (PageSize - 1)) != 0 || allocation.PageCount == 0 || allocation.PageCount > ~0UL / PageSize || allocation.ByteLength != allocation.PageCount * PageSize || allocation.PhysicalStart > ~0UL - allocation.ByteLength || allocation.PhysicalEndExclusive != allocation.PhysicalStart + allocation.ByteLength || allocation.VirtualStart > ~0UL - allocation.ByteLength || allocation.VirtualStart >= X64TlbInvalidationPlan.LowCanonicalEndExclusive || allocation.VirtualStart + allocation.ByteLength > X64TlbInvalidationPlan.LowCanonicalEndExclusive)
        {
            return false;
        }

        return !requireOnePage || allocation.PageCount == 1;
    }

    private static bool AllocationsOverlap(OS.Boot.BootMemoryRegionRecord first, OS.Boot.BootMemoryRegionRecord second)
    {
        bool physicalOverlap = first.PhysicalStart < second.PhysicalEndExclusive && second.PhysicalStart < first.PhysicalEndExclusive;
        ulong firstVirtualEnd = first.VirtualStart + first.ByteLength;
        ulong secondVirtualEnd = second.VirtualStart + second.ByteLength;
        bool virtualOverlap = first.VirtualStart < secondVirtualEnd && second.VirtualStart < firstVirtualEnd;
        return physicalOverlap || virtualOverlap;
    }

    private static bool TryGetVirtualAddress(ulong rawAddress, out OS.Boot.VirtualAddress address)
    {
        return OS.Boot.VirtualAddress.TryCreateAlignedFromHardwareRegisterBoundary(rawAddress, PageSize, out address) == OS.Boot.AddressValueStatus.Success;
    }

    private static X64ProtectionProbeStatus ValidateStorage(byte* storage, ulong modeledAddress, ulong capacity, ulong expectedModeledAddress, X64ProtectionProbeStatus missingStatus, X64ProtectionProbeStatus misalignedStatus, X64ProtectionProbeStatus capacitySmallStatus, X64ProtectionProbeStatus capacityLargeStatus, X64ProtectionProbeStatus spanOverflowStatus, X64ProtectionProbeStatus addressMismatchStatus)
    {
        if (capacity < RequiredByteLength)
        {
            return capacitySmallStatus;
        }

        if (capacity != PageSize)
        {
            return capacityLargeStatus;
        }

        if (storage == null)
        {
            return missingStatus;
        }

        ulong storageAddress = (ulong)storage;
        if ((storageAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return misalignedStatus;
        }

        if (storageAddress > ~0UL - capacity)
        {
            return spanOverflowStatus;
        }

        if (modeledAddress != expectedModeledAddress)
        {
            return addressMismatchStatus;
        }

        return X64ProtectionProbeStatus.Success;
    }
}
