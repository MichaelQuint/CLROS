namespace OS.Kernel.Diagnostics;
/// <summary>
/// Pure pre-terminal authority for the M02-013 EFI adapter. Firmware calls and
/// memory mutation stay with the caller; all stage acceptance, hostile-range
/// rejection, and release ordering decisions pass through this shared contract.
/// </summary>

public static class DisabledDefaultUd2VectorStatePreTerminalContract
{
    public const ulong PageByteCount = 4096;
    public const ulong CodePageCount = 2;
    public const ulong DataPageCount = 5;
    public const ulong CodeByteCount = CodePageCount * PageByteCount;
    public const ulong DataByteCount = DataPageCount * PageByteCount;
    public const byte ReleaseDataMask = 1;
    public const byte ReleaseCodeMask = 2;
    public static DisabledDefaultUd2VectorStatePreTerminalResult EvaluateServices(byte currentState, bool bootServicesPresent, bool allocatePagesPresent, bool freePagesPresent)
    {
        DisabledDefaultUd2VectorStatePreTerminalStatus status = currentState != 0 ? DisabledDefaultUd2VectorStatePreTerminalStatus.ExistingState : !bootServicesPresent ? DisabledDefaultUd2VectorStatePreTerminalStatus.MissingBootServices : !allocatePagesPresent ? DisabledDefaultUd2VectorStatePreTerminalStatus.MissingAllocatePages : !freePagesPresent ? DisabledDefaultUd2VectorStatePreTerminalStatus.MissingFreePages : DisabledDefaultUd2VectorStatePreTerminalStatus.Success;
        return NewResult(DisabledDefaultUd2VectorStatePreTerminalStage.Services, status, 0, false, 0, false);
    }

    public static DisabledDefaultUd2VectorStatePreTerminalResult EvaluateCodeAllocation(ulong firmwareStatus, ulong codeAddress)
    {
        DisabledDefaultUd2VectorStatePreTerminalStatus status = firmwareStatus != 0 ? DisabledDefaultUd2VectorStatePreTerminalStatus.CodeAllocationFailed : !IsValidRange(codeAddress, CodeByteCount) ? DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidCodeRange : DisabledDefaultUd2VectorStatePreTerminalStatus.Success;
        return NewResult(DisabledDefaultUd2VectorStatePreTerminalStage.CodeAllocation, status, codeAddress, firmwareStatus == 0, 0, false);
    }

    public static DisabledDefaultUd2VectorStatePreTerminalResult EvaluateDataAllocation(ulong codeAddress, ulong firmwareStatus, ulong dataAddress)
    {
        DisabledDefaultUd2VectorStatePreTerminalStatus status = !IsValidRange(codeAddress, CodeByteCount) ? DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidCodeRange : firmwareStatus != 0 ? DisabledDefaultUd2VectorStatePreTerminalStatus.DataAllocationFailed : !IsValidRange(dataAddress, DataByteCount) ? DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange : RangesOverlap(codeAddress, CodeByteCount, dataAddress, DataByteCount) ? DisabledDefaultUd2VectorStatePreTerminalStatus.OverlappingRanges : DisabledDefaultUd2VectorStatePreTerminalStatus.Success;
        return NewResult(DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation, status, codeAddress, true, dataAddress, firmwareStatus == 0);
    }

    public static DisabledDefaultUd2VectorStatePreTerminalResult EvaluateMaterialization(ulong codeAddress, ulong dataAddress, bool materializationSucceeded)
    {
        return EvaluatePreparedStage(DisabledDefaultUd2VectorStatePreTerminalStage.Materialization, DisabledDefaultUd2VectorStatePreTerminalStatus.MaterializationFailed, codeAddress, dataAddress, materializationSucceeded);
    }

    public static DisabledDefaultUd2VectorStatePreTerminalResult EvaluateReadBack(ulong codeAddress, ulong dataAddress, bool readBackSucceeded)
    {
        return EvaluatePreparedStage(DisabledDefaultUd2VectorStatePreTerminalStage.ReadBack, DisabledDefaultUd2VectorStatePreTerminalStatus.ReadBackFailed, codeAddress, dataAddress, readBackSucceeded);
    }

    public static DisabledDefaultUd2VectorStatePreTerminalResult EvaluateMarkerTransport(ulong codeAddress, ulong dataAddress, bool markerTransportSucceeded)
    {
        return EvaluatePreparedStage(DisabledDefaultUd2VectorStatePreTerminalStage.MarkerTransport, DisabledDefaultUd2VectorStatePreTerminalStatus.MarkerTransportFailed, codeAddress, dataAddress, markerTransportSucceeded);
    }

    public static DisabledDefaultUd2VectorStatePreTerminalResult EvaluatePublication(ulong codeAddress, ulong dataAddress, bool publicationSucceeded)
    {
        return EvaluatePreparedStage(DisabledDefaultUd2VectorStatePreTerminalStage.Publication, DisabledDefaultUd2VectorStatePreTerminalStatus.PublicationFailed, codeAddress, dataAddress, publicationSucceeded);
    }

    public static byte PlanCleanup(bool codeAllocated, ulong codeAddress, bool dataAllocated, ulong dataAddress)
    {
        byte plan = dataAllocated ? ReleaseDataMask : (byte)0;
        if (codeAllocated && (!dataAllocated || AllocationRangesDefinitelyDisjoint(codeAddress, dataAddress)))
        {
            plan |= ReleaseCodeMask;
        }

        return plan;
    }

    public static bool IsValidRange(ulong address, ulong byteCount)
    {
        return address != 0 && byteCount != 0 && (address & (PageByteCount - 1)) == 0 && address <= ~0UL - byteCount && DisabledDefaultUd2VectorStateTransition.IsCanonicalX64Address(address) && DisabledDefaultUd2VectorStateTransition.IsCanonicalX64Address(address + byteCount - 1);
    }

    public static bool RangesOverlap(ulong firstAddress, ulong firstByteCount, ulong secondAddress, ulong secondByteCount)
    {
        if (firstByteCount == 0 || secondByteCount == 0 || firstAddress > ~0UL - firstByteCount || secondAddress > ~0UL - secondByteCount)
        {
            return true;
        }

        ulong firstEnd = firstAddress + firstByteCount;
        ulong secondEnd = secondAddress + secondByteCount;
        return firstAddress < secondEnd && secondAddress < firstEnd;
    }

    public static bool AllocationRangesDefinitelyDisjoint(ulong codeAddress, ulong dataAddress)
    {
        if (codeAddress > ~0UL - CodeByteCount || dataAddress > ~0UL - DataByteCount)
        {
            return false;
        }

        ulong codeEnd = codeAddress + CodeByteCount;
        ulong dataEnd = dataAddress + DataByteCount;
        return codeEnd <= dataAddress || dataEnd <= codeAddress;
    }

    private static DisabledDefaultUd2VectorStatePreTerminalResult EvaluatePreparedStage(DisabledDefaultUd2VectorStatePreTerminalStage stage, DisabledDefaultUd2VectorStatePreTerminalStatus failureStatus, ulong codeAddress, ulong dataAddress, bool operationSucceeded)
    {
        DisabledDefaultUd2VectorStatePreTerminalStatus status = !IsValidRange(codeAddress, CodeByteCount) ? DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidCodeRange : !IsValidRange(dataAddress, DataByteCount) ? DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange : RangesOverlap(codeAddress, CodeByteCount, dataAddress, DataByteCount) ? DisabledDefaultUd2VectorStatePreTerminalStatus.OverlappingRanges : !operationSucceeded ? failureStatus : DisabledDefaultUd2VectorStatePreTerminalStatus.Success;
        return NewResult(stage, status, codeAddress, true, dataAddress, true);
    }

    private static DisabledDefaultUd2VectorStatePreTerminalResult NewResult(DisabledDefaultUd2VectorStatePreTerminalStage stage, DisabledDefaultUd2VectorStatePreTerminalStatus status, ulong codeAddress, bool codeAllocated, ulong dataAddress, bool dataAllocated)
    {
        return new DisabledDefaultUd2VectorStatePreTerminalResult(stage, status, codeAddress, dataAddress, PlanCleanup(codeAllocated, codeAddress, dataAllocated, dataAddress));
    }
}
#if !NOSTDLIB_EFI
#endif
