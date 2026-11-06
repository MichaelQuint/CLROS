namespace OS.Kernel.Boot;
/// <summary>
/// Builds a deterministic, allocation-free description of the low canonical
/// identity ranges and fresh four-level hierarchy required by early boot.
/// It never allocates table pages, writes page tables, loads CR3, or changes a
/// live mapping.
/// </summary>

public static unsafe partial class X64BootIdentityMappingPlan
{
    public const ulong PageSize = 4096;
    public const ulong MaximumSourceCount = 256;
    public const ulong MaximumDestinationCount = 512;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong LowCanonicalEndExclusive = 0x0000800000000000UL;
    public const ulong RequiredPurposeMask = 0x7e;
    public const ulong RequiredLiveIntervalPurposeMask = 0x7e00;
    public const ulong RequiredLiveIntervalPurposeCount = 7;
    public const bool UsesOnly4KiBLeaves = true;
    public const bool PerformsLiveMapping = false;
    public const bool AllocatesPageTables = false;
    public const bool PopulatesPageTables = false;
    private const ulong MaximumUnsignedLong = ~0UL;
    public static X64BootIdentityMappingPlanResult Create(X64BootIdentityMappingRequirement* source, ulong sourceCount, ulong sourceCapacity, X64BootIdentityMappingRange* destination, ulong destinationCapacity, ulong availableTablePageCapacity, OS.Kernel.Memory.X64PageTableEncodingPreconditions preconditions)
    {
        return CreateCore(source, sourceCount, sourceCapacity, destination, destinationCapacity, availableTablePageCapacity, preconditions, RequiredPurposeMask, requiresGuard: true, exactLiveInterval: false);
    }

    /// <summary>
    /// Builds the exact host-side identity plan for the future composite
    /// owned-root interval. Historical mapped aliases and the
    /// AdditionalRequired wildcard are not accepted; one absent boot-stack
    /// guard sentinel is required.
    /// </summary>

    public static X64BootIdentityMappingPlanResult CreateLiveInterval(X64BootIdentityMappingRequirement* source, ulong sourceCount, ulong sourceCapacity, X64BootIdentityMappingRange* destination, ulong destinationCapacity, ulong availableTablePageCapacity, OS.Kernel.Memory.X64PageTableEncodingPreconditions preconditions)
    {
        return CreateCore(source, sourceCount, sourceCapacity, destination, destinationCapacity, availableTablePageCapacity, preconditions, RequiredLiveIntervalPurposeMask, requiresGuard: true, exactLiveInterval: true);
    }

    private static X64BootIdentityMappingPlanResult CreateCore(X64BootIdentityMappingRequirement* source, ulong sourceCount, ulong sourceCapacity, X64BootIdentityMappingRange* destination, ulong destinationCapacity, ulong availableTablePageCapacity, OS.Kernel.Memory.X64PageTableEncodingPreconditions preconditions, ulong requiredPurposeMask, bool requiresGuard, bool exactLiveInterval)
    {
        PlannerState state = new PlannerState(sourceCount, sourceCapacity, destinationCapacity, availableTablePageCapacity, requiredPurposeMask);
        if (sourceCount == 0)
        {
            return Fail(state, X64BootIdentityMappingStatus.InvalidSourceCount);
        }

        if (exactLiveInterval && sourceCount != RequiredLiveIntervalPurposeCount)
        {
            return Fail(state, X64BootIdentityMappingStatus.InvalidSourceCount);
        }

        if (sourceCount > MaximumSourceCount)
        {
            return Fail(state, X64BootIdentityMappingStatus.TooManySources);
        }

        if (sourceCapacity > MaximumSourceCount || destinationCapacity > MaximumDestinationCount)
        {
            return Fail(state, X64BootIdentityMappingStatus.CapacityExceedsLimit);
        }

        if (sourceCount > sourceCapacity)
        {
            return Fail(state, X64BootIdentityMappingStatus.SourceCapacityTooSmall);
        }

        if ((ulong)sizeof(X64BootIdentityMappingRequirement) != X64BootIdentityMappingRequirement.StructureSize || (ulong)sizeof(X64BootIdentityMappingRange) != X64BootIdentityMappingRange.StructureSize || (ulong)sizeof(X64BootIdentityMappingPlanResult) != X64BootIdentityMappingPlanResult.StructureSize)
        {
            return Fail(state, X64BootIdentityMappingStatus.ContractLayoutMismatch);
        }

        if (source == null)
        {
            return Fail(state, X64BootIdentityMappingStatus.MissingSource);
        }

        if (destination == null)
        {
            return Fail(state, X64BootIdentityMappingStatus.MissingDestination);
        }

        ulong sourceAddress = (ulong)source;
        ulong destinationAddress = (ulong)destination;
        if ((sourceAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(state, X64BootIdentityMappingStatus.MisalignedSource, OS.Boot.AddressValueStatus.MisalignedAddress);
        }

        if ((destinationAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(state, X64BootIdentityMappingStatus.MisalignedDestination, OS.Boot.AddressValueStatus.MisalignedAddress);
        }

        if (!TryGetSpanEnd(sourceAddress, sourceCapacity, X64BootIdentityMappingRequirement.StructureSize, out ulong sourceEnd))
        {
            return Fail(state, X64BootIdentityMappingStatus.SourceSpanOverflow, OS.Boot.AddressValueStatus.AddressOverflow);
        }

        if (!TryGetSpanEnd(destinationAddress, destinationCapacity, X64BootIdentityMappingRange.StructureSize, out ulong destinationEnd))
        {
            return Fail(state, X64BootIdentityMappingStatus.DestinationSpanOverflow, OS.Boot.AddressValueStatus.AddressOverflow);
        }

        if (destinationCapacity != 0 && sourceAddress < destinationEnd && destinationAddress < sourceEnd)
        {
            return Fail(state, X64BootIdentityMappingStatus.SourceDestinationOverlap);
        }

        OS.Kernel.Memory.X64PageTableEntryStatus entryStatus = preconditions.Validate();
        if (entryStatus != OS.Kernel.Memory.X64PageTableEntryStatus.Success)
        {
            return Fail(state, X64BootIdentityMappingStatus.InvalidPreconditions, OS.Boot.AddressValueStatus.Uninitialized, entryStatus);
        }

        entryStatus = preconditions.PatMapping.TryValidateMemoryTypeAtIndex(0, OS.Kernel.Memory.PageCachePolicy.WriteBack);
        if (entryStatus != OS.Kernel.Memory.X64PageTableEntryStatus.Success)
        {
            return Fail(state, X64BootIdentityMappingStatus.PageWalkCacheNotWriteBack, OS.Boot.AddressValueStatus.Uninitialized, entryStatus);
        }

        for (ulong index = 0; index < sourceCount; index++)
        {
            if (!TryNormalizeRequirement(source[index], preconditions, exactLiveInterval, out RequirementView view, out X64BootIdentityMappingStatus status, out OS.Boot.AddressValueStatus addressStatus, out entryStatus))
            {
                return Fail(state, status, addressStatus, entryStatus, index);
            }

            state.ValidatedSourceCount = index + 1;
            state.ObservedPurposeMask |= view.PurposeMask;
            if (view.IsGuard)
            {
                if (state.GuardSourceIndex != X64BootIdentityMappingPlanResult.NoIndex)
                {
                    return Fail(state, X64BootIdentityMappingStatus.MultipleGuards, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success, index, state.GuardSourceIndex);
                }

                state.GuardSourceIndex = index;
            }
        }

        state.MissingPurposeMask = state.RequiredPurposeMask & ~state.ObservedPurposeMask;
        if (requiresGuard && state.GuardSourceIndex == X64BootIdentityMappingPlanResult.NoIndex)
        {
            return Fail(state, X64BootIdentityMappingStatus.MissingGuard, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (state.MissingPurposeMask != 0)
        {
            return Fail(state, X64BootIdentityMappingStatus.MissingRequiredPurpose, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        for (ulong leftIndex = 0; leftIndex < sourceCount; leftIndex++)
        {
            TryReadValidatedRequirement(source[leftIndex], out RequirementView left);
            for (ulong rightIndex = leftIndex + 1; rightIndex < sourceCount; rightIndex++)
            {
                TryReadValidatedRequirement(source[rightIndex], out RequirementView right);
                if (!RangesOverlap(left.Start, left.EndExclusive, right.Start, right.EndExclusive))
                {
                    continue;
                }

                if (left.IsGuard || right.IsGuard)
                {
                    ulong guardIndex = left.IsGuard ? leftIndex : rightIndex;
                    ulong mappedIndex = left.IsGuard ? rightIndex : leftIndex;
                    return Fail(state, X64BootIdentityMappingStatus.GuardOverlap, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success, guardIndex, mappedIndex);
                }

                if (left.Protection != right.Protection)
                {
                    return Fail(state, X64BootIdentityMappingStatus.IncompatibleProtectionOverlap, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success, rightIndex, leftIndex);
                }

                if (left.CachePolicy != right.CachePolicy)
                {
                    return Fail(state, X64BootIdentityMappingStatus.IncompatibleCacheOverlap, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success, rightIndex, leftIndex);
                }
            }
        }

        X64BootIdentityMappingStatus analysisStatus = AnalyzeNormalizedOutput(source, sourceCount, ref state);
        if (analysisStatus != X64BootIdentityMappingStatus.Success)
        {
            return Fail(state, analysisStatus, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (state.RequiredDestinationCapacity > MaximumDestinationCount)
        {
            return Fail(state, X64BootIdentityMappingStatus.TooManyOutputRanges, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (state.RequiredDestinationCapacity > destinationCapacity)
        {
            return Fail(state, X64BootIdentityMappingStatus.DestinationCapacityTooSmall, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (state.TotalTablePageCount > availableTablePageCapacity)
        {
            return Fail(state, X64BootIdentityMappingStatus.TableCapacityTooSmall, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (!CanConstructNormalizedOutput(source, sourceCount, state.RequiredDestinationCapacity))
        {
            return Fail(state, X64BootIdentityMappingStatus.OutputConstructionFailure, OS.Boot.AddressValueStatus.InvalidAddress, OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        // The caller must keep the complete declared source span stable for
        // the duration of this call. All fallible work, including a second
        // reconstruction check, has completed before this write-only pass.
        WriteNormalizedOutput(source, sourceCount, destination);
        state.OutputRangeCount = state.RequiredDestinationCapacity;
        return BuildResult(state, X64BootIdentityMappingStatus.Success, OS.Boot.AddressValueStatus.Success, OS.Kernel.Memory.X64PageTableEntryStatus.Success, X64BootIdentityMappingPlanResult.NoIndex, X64BootIdentityMappingPlanResult.NoIndex);
    }

    private static bool TryNormalizeRequirement(X64BootIdentityMappingRequirement requirement, OS.Kernel.Memory.X64PageTableEncodingPreconditions preconditions, bool exactLiveInterval, out RequirementView view, out X64BootIdentityMappingStatus status, out OS.Boot.AddressValueStatus addressStatus, out OS.Kernel.Memory.X64PageTableEntryStatus entryStatus)
    {
        view = default;
        status = X64BootIdentityMappingStatus.Success;
        addressStatus = OS.Boot.AddressValueStatus.Success;
        entryStatus = OS.Kernel.Memory.X64PageTableEntryStatus.Success;
        if (!requirement.HasCanonicalState)
        {
            status = X64BootIdentityMappingStatus.InvalidRequirementState;
            return false;
        }

        X64BootIdentityMappingPurpose purpose = requirement.Purpose;
        if (!IsValidPurpose(purpose, exactLiveInterval))
        {
            status = X64BootIdentityMappingStatus.InvalidPurpose;
            return false;
        }

        bool isGuard = purpose == X64BootIdentityMappingPurpose.BootStackGuard;
        if (!requirement.PhysicalStart.IsValid)
        {
            status = isGuard ? X64BootIdentityMappingStatus.InvalidPhysicalStart : X64BootIdentityMappingStatus.PageZeroMappingRejected;
            addressStatus = OS.Boot.AddressValueStatus.ZeroAddress;
            return false;
        }

        addressStatus = requirement.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong start);
        if (addressStatus != OS.Boot.AddressValueStatus.Success)
        {
            status = X64BootIdentityMappingStatus.InvalidPhysicalStart;
            return false;
        }

        addressStatus = requirement.PhysicalEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong endExclusive);
        if (addressStatus != OS.Boot.AddressValueStatus.Success)
        {
            status = X64BootIdentityMappingStatus.InvalidPhysicalEnd;
            return false;
        }

        if (endExclusive <= start)
        {
            status = X64BootIdentityMappingStatus.EmptyOrReversedRange;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            return false;
        }

        if ((start & (PageSize - 1)) != 0 || (endExclusive & (PageSize - 1)) != 0)
        {
            status = X64BootIdentityMappingStatus.MisalignedRange;
            addressStatus = OS.Boot.AddressValueStatus.MisalignedAddress;
            return false;
        }

        ulong pageCount = requirement.PageCount;
        ulong byteLength = requirement.ByteLength;
        if (pageCount == 0 || byteLength == 0)
        {
            status = X64BootIdentityMappingStatus.GeometryMismatch;
            addressStatus = OS.Boot.AddressValueStatus.ZeroLength;
            return false;
        }

        if (pageCount > MaximumUnsignedLong / PageSize)
        {
            status = X64BootIdentityMappingStatus.GeometryOverflow;
            addressStatus = OS.Boot.AddressValueStatus.AddressOverflow;
            return false;
        }

        ulong expectedByteLength = pageCount * PageSize;
        if (expectedByteLength != byteLength)
        {
            status = X64BootIdentityMappingStatus.GeometryMismatch;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            return false;
        }

        if (start > MaximumUnsignedLong - byteLength)
        {
            status = X64BootIdentityMappingStatus.GeometryOverflow;
            addressStatus = OS.Boot.AddressValueStatus.AddressOverflow;
            return false;
        }

        if (start + byteLength != endExclusive)
        {
            status = X64BootIdentityMappingStatus.GeometryMismatch;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            return false;
        }

        if (!isGuard && start < PageSize)
        {
            status = X64BootIdentityMappingStatus.PageZeroMappingRejected;
            addressStatus = OS.Boot.AddressValueStatus.ZeroAddress;
            return false;
        }

        if (endExclusive > LowCanonicalEndExclusive)
        {
            status = X64BootIdentityMappingStatus.NonCanonicalIdentityRange;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            return false;
        }

        ulong physicalLimit = 1UL << preconditions.PhysicalAddressWidth;
        if (endExclusive > physicalLimit)
        {
            status = X64BootIdentityMappingStatus.PhysicalAddressOutsideWidth;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            entryStatus = OS.Kernel.Memory.X64PageTableEntryStatus.PhysicalAddressOutsideWidth;
            return false;
        }

        OS.Kernel.Memory.PageProtection protection = requirement.Protection;
        OS.Kernel.Memory.PageCachePolicy cachePolicy = requirement.CachePolicy;
        if (isGuard)
        {
            if (protection != OS.Kernel.Memory.PageProtection.None || cachePolicy != OS.Kernel.Memory.PageCachePolicy.Uninitialized)
            {
                status = X64BootIdentityMappingStatus.InvalidGuardPolicy;
                return false;
            }
        }
        else
        {
            if (!IsValidLeafProtection(protection))
            {
                status = HasProtection(protection, OS.Kernel.Memory.PageProtection.Write) && HasProtection(protection, OS.Kernel.Memory.PageProtection.Execute) ? X64BootIdentityMappingStatus.WritableExecutableRejected : X64BootIdentityMappingStatus.InvalidProtection;
                entryStatus = status == X64BootIdentityMappingStatus.WritableExecutableRejected ? OS.Kernel.Memory.X64PageTableEntryStatus.WritableExecutableRejected : OS.Kernel.Memory.X64PageTableEntryStatus.InvalidProtection;
                return false;
            }

            if (!IsValidCachePolicy(cachePolicy))
            {
                status = X64BootIdentityMappingStatus.InvalidCachePolicy;
                entryStatus = OS.Kernel.Memory.X64PageTableEntryStatus.InvalidCachePolicy;
                return false;
            }

            if (!TryValidateLeafEndpoint(requirement.PhysicalStart, protection, cachePolicy, preconditions, out entryStatus))
            {
                status = X64BootIdentityMappingStatus.LeafEncodingRejected;
                return false;
            }

            ulong lastPageStart = endExclusive - PageSize;
            addressStatus = OS.Boot.PhysicalAddress.TryCreateAlignedFromHardwareRegisterBoundary(lastPageStart, PageSize, out OS.Boot.PhysicalAddress lastPageAddress);
            if (addressStatus != OS.Boot.AddressValueStatus.Success)
            {
                status = X64BootIdentityMappingStatus.InvalidPhysicalEnd;
                return false;
            }

            if (!TryValidateLeafEndpoint(lastPageAddress, protection, cachePolicy, preconditions, out entryStatus))
            {
                status = X64BootIdentityMappingStatus.LeafEncodingRejected;
                return false;
            }
        }

        view = new RequirementView(start, endExclusive, pageCount, byteLength, PurposeBit(purpose), protection, cachePolicy, isGuard);
        addressStatus = OS.Boot.AddressValueStatus.Success;
        entryStatus = OS.Kernel.Memory.X64PageTableEntryStatus.Success;
        return true;
    }

    private static bool TryValidateLeafEndpoint(OS.Boot.PhysicalAddress address, OS.Kernel.Memory.PageProtection protection, OS.Kernel.Memory.PageCachePolicy cachePolicy, OS.Kernel.Memory.X64PageTableEncodingPreconditions preconditions, out OS.Kernel.Memory.X64PageTableEntryStatus entryStatus)
    {
        OS.Kernel.Memory.X64PageTableEntryResult result = OS.Kernel.Memory.X64PageTableEntry.TryEncode(new OS.Kernel.Memory.X64PageTableEntryRequest(OS.Kernel.Memory.X64PageTableLevel.Pte, OS.Kernel.Memory.X64PageTableEntryKind.Leaf, address, protection, cachePolicy), preconditions);
        entryStatus = result.Status;
        return result.Succeeded;
    }

    private static void TryReadValidatedRequirement(X64BootIdentityMappingRequirement requirement, out RequirementView view)
    {
        requirement.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong start);
        requirement.PhysicalEndExclusive.TryGetRawValueForHardwareRegisterBoundary(out ulong endExclusive);
        X64BootIdentityMappingPurpose purpose = requirement.Purpose;
        view = new RequirementView(start, endExclusive, requirement.PageCount, requirement.ByteLength, PurposeBit(purpose), requirement.Protection, requirement.CachePolicy, purpose == X64BootIdentityMappingPurpose.BootStackGuard);
    }

    private static X64BootIdentityMappingStatus AnalyzeNormalizedOutput(X64BootIdentityMappingRequirement* source, ulong sourceCount, ref PlannerState state)
    {
        ulong cursor = PageSize;
        ulong outputCount = 0;
        ulong lastPdptKey = 0;
        ulong lastPdKey = 0;
        ulong lastPtKey = 0;
        while (TryBuildNextMergedRange(source, sourceCount, cursor, out MergedRange range))
        {
            if (outputCount >= MaximumDestinationCount)
            {
                state.RequiredDestinationCapacity = outputCount + 1;
                return X64BootIdentityMappingStatus.TooManyOutputRanges;
            }

            if (!TryValidateOutputConstruction(range))
            {
                return X64BootIdentityMappingStatus.OutputConstructionFailure;
            }

            if (state.UnionPageCount > MaximumUnsignedLong - range.PageCount)
            {
                return X64BootIdentityMappingStatus.UnionPageCountOverflow;
            }

            if (state.UnionByteLength > MaximumUnsignedLong - range.ByteLength)
            {
                return X64BootIdentityMappingStatus.UnionByteCountOverflow;
            }

            state.UnionPageCount += range.PageCount;
            state.UnionByteLength += range.ByteLength;
            ulong lastAddress = range.EndExclusive - 1;
            if (!TryAccumulateDistinctKeys(range.Start >> 39, lastAddress >> 39, outputCount != 0, ref lastPdptKey, ref state.PdptPageCount) || !TryAccumulateDistinctKeys(range.Start >> 30, lastAddress >> 30, outputCount != 0, ref lastPdKey, ref state.PdPageCount) || !TryAccumulateDistinctKeys(range.Start >> 21, lastAddress >> 21, outputCount != 0, ref lastPtKey, ref state.PtPageCount))
            {
                return X64BootIdentityMappingStatus.TablePageCountOverflow;
            }

            outputCount++;
            cursor = range.EndExclusive;
        }

        state.Pml4PageCount = 1;
        state.RequiredDestinationCapacity = outputCount;
        ulong tablePageCount = state.Pml4PageCount;
        if (!TryAdd(tablePageCount, state.PdptPageCount, out tablePageCount) || !TryAdd(tablePageCount, state.PdPageCount, out tablePageCount) || !TryAdd(tablePageCount, state.PtPageCount, out tablePageCount))
        {
            return X64BootIdentityMappingStatus.TablePageCountOverflow;
        }

        state.TotalTablePageCount = tablePageCount;
        if (tablePageCount > MaximumUnsignedLong / PageSize)
        {
            return X64BootIdentityMappingStatus.TableByteCountOverflow;
        }

        state.TotalTableByteLength = tablePageCount * PageSize;
        if (state.UnionPageCount > MaximumUnsignedLong / PageSize || state.UnionPageCount * PageSize != state.UnionByteLength)
        {
            return X64BootIdentityMappingStatus.AccountingMismatch;
        }

        return X64BootIdentityMappingStatus.Success;
    }

    private static bool CanConstructNormalizedOutput(X64BootIdentityMappingRequirement* source, ulong sourceCount, ulong expectedOutputCount)
    {
        ulong cursor = PageSize;
        ulong outputIndex = 0;
        while (TryBuildNextMergedRange(source, sourceCount, cursor, out MergedRange range))
        {
            if (outputIndex >= expectedOutputCount || !TryCreateOutputRange(range, out X64BootIdentityMappingRange output) || !output.HasCanonicalState)
            {
                return false;
            }

            outputIndex++;
            cursor = range.EndExclusive;
        }

        return outputIndex == expectedOutputCount;
    }

    private static void WriteNormalizedOutput(X64BootIdentityMappingRequirement* source, ulong sourceCount, X64BootIdentityMappingRange* destination)
    {
        ulong cursor = PageSize;
        ulong outputIndex = 0;
        while (TryBuildNextMergedRange(source, sourceCount, cursor, out MergedRange range))
        {
            TryCreateOutputRange(range, out X64BootIdentityMappingRange output);
            destination[outputIndex] = output;
            outputIndex++;
            cursor = range.EndExclusive;
        }
    }

    private static bool TryBuildNextMergedRange(X64BootIdentityMappingRequirement* source, ulong sourceCount, ulong cursor, out MergedRange merged)
    {
        merged = default;
        bool found = false;
        RequirementView seed = default;
        for (ulong index = 0; index < sourceCount; index++)
        {
            TryReadValidatedRequirement(source[index], out RequirementView current);
            if (current.IsGuard || current.Start < cursor)
            {
                continue;
            }

            if (!found || current.Start < seed.Start)
            {
                seed = current;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        ulong start = seed.Start;
        ulong endExclusive = seed.EndExclusive;
        ulong purposeMask = seed.PurposeMask;
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (ulong index = 0; index < sourceCount; index++)
            {
                TryReadValidatedRequirement(source[index], out RequirementView current);
                if (current.IsGuard || current.Protection != seed.Protection || current.CachePolicy != seed.CachePolicy || current.Start > endExclusive || current.EndExclusive <= start)
                {
                    continue;
                }

                purposeMask |= current.PurposeMask;
                if (current.EndExclusive > endExclusive)
                {
                    endExclusive = current.EndExclusive;
                    changed = true;
                }
            }
        }

        ulong byteLength = endExclusive - start;
        merged = new MergedRange(start, endExclusive, byteLength / PageSize, byteLength, purposeMask, seed.Protection, seed.CachePolicy);
        return true;
    }

    private static bool TryValidateOutputConstruction(MergedRange range)
    {
        return TryCreateOutputRange(range, out X64BootIdentityMappingRange output) && output.HasCanonicalState;
    }

    private static bool TryCreateOutputRange(MergedRange range, out X64BootIdentityMappingRange output)
    {
        output = default;
        OS.Boot.AddressValueStatus status = OS.Boot.PhysicalAddress.TryCreateAlignedFromHardwareRegisterBoundary(range.Start, PageSize, out OS.Boot.PhysicalAddress physicalStart);
        if (status != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        status = OS.Boot.PhysicalAddress.TryCreateAlignedFromHardwareRegisterBoundary(range.EndExclusive, PageSize, out OS.Boot.PhysicalAddress physicalEnd);
        if (status != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        status = OS.Boot.VirtualAddress.TryCreateAlignedFromHardwareRegisterBoundary(range.Start, PageSize, out OS.Boot.VirtualAddress virtualStart);
        if (status != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        status = OS.Boot.VirtualAddress.TryCreateAlignedFromHardwareRegisterBoundary(range.EndExclusive, PageSize, out OS.Boot.VirtualAddress virtualEnd);
        if (status != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        output = new X64BootIdentityMappingRange(physicalStart, physicalEnd, virtualStart, virtualEnd, range.PageCount, range.ByteLength, range.PurposeMask, range.Protection, range.CachePolicy);
        return true;
    }

    private static bool TryAccumulateDistinctKeys(ulong firstKey, ulong finalKey, bool hasPrevious, ref ulong previousFinalKey, ref ulong count)
    {
        ulong addition = 0;
        if (!hasPrevious)
        {
            addition = finalKey - firstKey + 1;
        }
        else if (finalKey > previousFinalKey)
        {
            ulong nextUncounted = previousFinalKey + 1;
            ulong effectiveFirst = firstKey > nextUncounted ? firstKey : nextUncounted;
            addition = finalKey - effectiveFirst + 1;
        }

        if (count > MaximumUnsignedLong - addition)
        {
            return false;
        }

        count += addition;
        if (!hasPrevious || finalKey > previousFinalKey)
        {
            previousFinalKey = finalKey;
        }

        return true;
    }

    private static bool IsValidPurpose(X64BootIdentityMappingPurpose purpose, bool exactLiveInterval)
    {
        if (exactLiveInterval)
        {
            return purpose == X64BootIdentityMappingPurpose.BootStackGuard || purpose == X64BootIdentityMappingPurpose.CompositeCodeAndCallGate || purpose == X64BootIdentityMappingPurpose.CompositeOwnedStack || purpose == X64BootIdentityMappingPurpose.ProtectionProbePage || purpose == X64BootIdentityMappingPurpose.CompleteTableAndStateArena || purpose == X64BootIdentityMappingPurpose.RuntimeStateAndCanaryPage || purpose == X64BootIdentityMappingPurpose.OwnedRootPage;
        }

        return purpose == X64BootIdentityMappingPurpose.ExecutingImage || purpose == X64BootIdentityMappingPurpose.OwnedStack || purpose == X64BootIdentityMappingPurpose.BootHandoff || purpose == X64BootIdentityMappingPurpose.SerialStub || purpose == X64BootIdentityMappingPurpose.AcpiScratch || purpose == X64BootIdentityMappingPurpose.FramebufferMetadata || purpose == X64BootIdentityMappingPurpose.BootStackGuard || purpose == X64BootIdentityMappingPurpose.AdditionalRequired;
    }

    private static bool IsValidLeafProtection(OS.Kernel.Memory.PageProtection protection)
    {
        return protection == OS.Kernel.Memory.PageProtection.Read || protection == (OS.Kernel.Memory.PageProtection)((byte)OS.Kernel.Memory.PageProtection.Read | (byte)OS.Kernel.Memory.PageProtection.Write) || protection == (OS.Kernel.Memory.PageProtection)((byte)OS.Kernel.Memory.PageProtection.Read | (byte)OS.Kernel.Memory.PageProtection.Execute);
    }

    private static bool IsValidCachePolicy(OS.Kernel.Memory.PageCachePolicy cachePolicy)
    {
        return cachePolicy == OS.Kernel.Memory.PageCachePolicy.WriteBack || cachePolicy == OS.Kernel.Memory.PageCachePolicy.Uncached || cachePolicy == OS.Kernel.Memory.PageCachePolicy.WriteCombining;
    }

    private static bool HasProtection(OS.Kernel.Memory.PageProtection value, OS.Kernel.Memory.PageProtection flag)
    {
        return ((byte)value & (byte)flag) != 0;
    }

    private static ulong PurposeBit(X64BootIdentityMappingPurpose purpose)
    {
        return 1UL << (byte)purpose;
    }

    private static bool RangesOverlap(ulong leftStart, ulong leftEnd, ulong rightStart, ulong rightEnd)
    {
        return leftStart < rightEnd && rightStart < leftEnd;
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
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong result)
    {
        result = 0;
        if (left > MaximumUnsignedLong - right)
        {
            return false;
        }

        result = left + right;
        return true;
    }

    private static X64BootIdentityMappingPlanResult Fail(PlannerState state, X64BootIdentityMappingStatus status, OS.Boot.AddressValueStatus addressStatus = OS.Boot.AddressValueStatus.Uninitialized, OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus = OS.Kernel.Memory.X64PageTableEntryStatus.Uninitialized, ulong invalidSourceIndex = X64BootIdentityMappingPlanResult.NoIndex, ulong conflictingSourceIndex = X64BootIdentityMappingPlanResult.NoIndex)
    {
        return BuildResult(state, status, addressStatus, pageTableEntryStatus, invalidSourceIndex, conflictingSourceIndex);
    }

    private static X64BootIdentityMappingPlanResult BuildResult(PlannerState state, X64BootIdentityMappingStatus status, OS.Boot.AddressValueStatus addressStatus, OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus, ulong invalidSourceIndex, ulong conflictingSourceIndex)
    {
        return new X64BootIdentityMappingPlanResult(status, addressStatus, pageTableEntryStatus, state.SourceCount, state.ValidatedSourceCount, state.SourceCapacity, state.DestinationCapacity, state.RequiredDestinationCapacity, state.OutputRangeCount, state.AvailableTablePageCapacity, state.RequiredPurposeMask, state.ObservedPurposeMask, state.MissingPurposeMask, invalidSourceIndex, conflictingSourceIndex, state.GuardSourceIndex, state.UnionPageCount, state.UnionByteLength, state.Pml4PageCount, state.PdptPageCount, state.PdPageCount, state.PtPageCount, state.TotalTablePageCount, state.TotalTableByteLength);
    }
}
