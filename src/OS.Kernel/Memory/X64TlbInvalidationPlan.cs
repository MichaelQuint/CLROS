namespace OS.Kernel.Memory;
/// <summary>
/// Builds and materializes exact target-only invalidation metadata. It never
/// executes INVLPG, reloads CR3, or observes a processor TLB.
/// </summary>

public static unsafe class X64TlbInvalidationPlan
{
    public const ulong PageSize = 4096;
    public const ulong MaximumPageCount = 4096;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong LowCanonicalEndExclusive = 0x0000_8000_0000_0000UL;
    public const bool PerformsLiveInvalidation = false;
    public const bool ReloadsCr3 = false;
    public static X64TlbInvalidationStatus TryCreate(OS.Boot.VirtualAddress virtualStart, ulong byteLength, out X64TlbInvalidationRange range)
    {
        range = default;
        if (!virtualStart.IsValid)
        {
            return X64TlbInvalidationStatus.InvalidVirtualStart;
        }

        if (!virtualStart.IsAligned(PageSize))
        {
            return X64TlbInvalidationStatus.MisalignedVirtualStart;
        }

        if (byteLength == 0)
        {
            return X64TlbInvalidationStatus.InvalidByteLength;
        }

        if ((byteLength & (PageSize - 1)) != 0)
        {
            return X64TlbInvalidationStatus.ByteLengthNotPageAligned;
        }

        ulong pageCount = byteLength / PageSize;
        if (pageCount == 0 || pageCount > MaximumPageCount)
        {
            return X64TlbInvalidationStatus.TooManyPages;
        }

        if (virtualStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawStart) != OS.Boot.AddressValueStatus.Success)
        {
            return X64TlbInvalidationStatus.InvalidVirtualStart;
        }

        if (rawStart == 0)
        {
            return X64TlbInvalidationStatus.PageZeroRejected;
        }

        if (rawStart >= LowCanonicalEndExclusive)
        {
            return X64TlbInvalidationStatus.NonCanonicalRange;
        }

        if (virtualStart.TryGetEndExclusive(byteLength, out OS.Boot.VirtualAddress endExclusive) != OS.Boot.AddressValueStatus.Success || endExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong rawEnd) != OS.Boot.AddressValueStatus.Success)
        {
            return X64TlbInvalidationStatus.RangeOverflow;
        }

        if (rawEnd > LowCanonicalEndExclusive || rawEnd <= rawStart)
        {
            return X64TlbInvalidationStatus.NonCanonicalRange;
        }

        range = new X64TlbInvalidationRange(virtualStart, endExclusive, byteLength, pageCount);
        return X64TlbInvalidationStatus.Success;
    }

    public static X64TlbInvalidationPlanResult Materialize(X64TlbInvalidationRange range, X64TlbInvalidationRecord* destination, ulong destinationCapacity)
    {
        X64TlbInvalidationStatus rangeStatus = range.Validate();
        if (rangeStatus != X64TlbInvalidationStatus.Success)
        {
            return X64TlbInvalidationPlanResult.Fail(rangeStatus, range, destinationCapacity);
        }

        if (destinationCapacity > MaximumPageCount)
        {
            return X64TlbInvalidationPlanResult.Fail(X64TlbInvalidationStatus.CapacityExceedsLimit, range, destinationCapacity, range.PageCount);
        }

        if (destinationCapacity < range.PageCount)
        {
            return X64TlbInvalidationPlanResult.Fail(X64TlbInvalidationStatus.DestinationCapacityTooSmall, range, destinationCapacity, range.PageCount);
        }

        if (destination == null)
        {
            return X64TlbInvalidationPlanResult.Fail(X64TlbInvalidationStatus.MissingDestination, range, destinationCapacity, range.PageCount);
        }

        ulong destinationAddress = (ulong)destination;
        if ((destinationAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return X64TlbInvalidationPlanResult.Fail(X64TlbInvalidationStatus.MisalignedDestination, range, destinationCapacity, range.PageCount);
        }

        if (!TryGetSpanEnd(destinationAddress, destinationCapacity, X64TlbInvalidationRecord.StructureSize, out _))
        {
            return X64TlbInvalidationPlanResult.Fail(X64TlbInvalidationStatus.DestinationSpanOverflow, range, destinationCapacity, range.PageCount);
        }

        if (range.VirtualStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawStart) != OS.Boot.AddressValueStatus.Success)
        {
            return X64TlbInvalidationPlanResult.Fail(X64TlbInvalidationStatus.InvalidRange, range, destinationCapacity, range.PageCount);
        }

        for (ulong index = 0; index < range.PageCount; index++)
        {
            ulong offset = index * PageSize;
            if (rawStart > ~0UL - offset)
            {
                return X64TlbInvalidationPlanResult.Fail(X64TlbInvalidationStatus.RangeOverflow, range, destinationCapacity, range.PageCount, index);
            }

            OS.Boot.AddressValueStatus addressStatus = OS.Boot.VirtualAddress.TryCreateAlignedFromHardwareRegisterBoundary(rawStart + offset, PageSize, out OS.Boot.VirtualAddress target);
            if (addressStatus != OS.Boot.AddressValueStatus.Success)
            {
                return X64TlbInvalidationPlanResult.Fail(X64TlbInvalidationStatus.InvalidRecord, range, destinationCapacity, range.PageCount, index);
            }
        }

        for (ulong index = 0; index < range.PageCount; index++)
        {
            ulong offset = index * PageSize;
            OS.Boot.VirtualAddress.TryCreateAlignedFromHardwareRegisterBoundary(rawStart + offset, PageSize, out OS.Boot.VirtualAddress target);
            destination[index] = new X64TlbInvalidationRecord(target, index);
        }

        return X64TlbInvalidationPlanResult.Success(range, destinationCapacity);
    }

    internal static X64TlbInvalidationStatus ValidateRangeFields(OS.Boot.VirtualAddress virtualStart, OS.Boot.VirtualAddress virtualEndExclusive, ulong byteLength, ulong pageCount)
    {
        if (TryCreate(virtualStart, byteLength, out X64TlbInvalidationRange recreated) != X64TlbInvalidationStatus.Success)
        {
            return X64TlbInvalidationStatus.InvalidRange;
        }

        if (!virtualEndExclusive.IsValid || !recreated.VirtualEndExclusive.Equals(virtualEndExclusive) || recreated.PageCount != pageCount)
        {
            return X64TlbInvalidationStatus.InvalidRange;
        }

        return X64TlbInvalidationStatus.Success;
    }

    private static bool TryGetSpanEnd(ulong start, ulong capacity, ulong elementSize, out ulong end)
    {
        end = 0;
        if (capacity != 0 && elementSize > ~0UL / capacity)
        {
            return false;
        }

        ulong byteLength = capacity * elementSize;
        if (start > ~0UL - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }
}
