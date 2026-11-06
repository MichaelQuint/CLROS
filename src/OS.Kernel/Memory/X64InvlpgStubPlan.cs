namespace OS.Kernel.Memory;
/// <summary>
/// Emits the exact Windows-x64 two-instruction sequence
/// INVLPG byte ptr [RCX]; RET as ordinary data and binds RCX to one planned
/// target. Host code never invokes the emitted bytes.
/// </summary>

public static unsafe class X64InvlpgStubPlan
{
    public const ulong PageSize = 4096;
    public const ulong EntryAlignment = 16;
    public const ulong RequiredPointerAlignment = 16;
    public const ulong RequiredByteLength = 4;
    public const ulong AuditedInstructionCount = 2;
    public const ulong MaximumInvocationCount = 1;
    public const bool UsesWindowsX64Abi = true;
    public const bool TargetPassedInRcx = true;
    public const bool TouchesNonvolatileRegisters = false;
    public const bool TouchesXmm6ThroughXmm15 = false;
    public const bool HasExternalRelocations = false;
    public const bool PerformsLiveInvalidation = false;
    public const bool ReloadsCr3 = false;
    public const bool InvokesStub = false;
    public const bool HostTestsInvokeBytes = false;
    public static X64InvlpgStubPlanResult Emit(byte* destination, ulong modeledDestinationAddress, ulong destinationCapacity, OS.Boot.VirtualAddress codePageStart, ulong entryOffset, X64TlbInvalidationRecord target)
    {
        X64InvlpgStubPlanStatus geometryStatus = ValidateGeometry(codePageStart, entryOffset, target, out OS.Boot.VirtualAddress codePageEnd, out OS.Boot.VirtualAddress entryAddress, out ulong rawCodePageStart);
        if (geometryStatus != X64InvlpgStubPlanStatus.Success)
        {
            return X64InvlpgStubPlanResult.Fail(geometryStatus, target, destinationCapacity);
        }

        X64InvlpgStubPlanStatus storageStatus = ValidateStorage(destination, modeledDestinationAddress, destinationCapacity, rawCodePageStart, missingStatus: X64InvlpgStubPlanStatus.MissingDestination, misalignedStatus: X64InvlpgStubPlanStatus.MisalignedDestination, capacitySmallStatus: X64InvlpgStubPlanStatus.DestinationCapacityTooSmall, capacityLargeStatus: X64InvlpgStubPlanStatus.DestinationCapacityExceedsPage, spanOverflowStatus: X64InvlpgStubPlanStatus.DestinationSpanOverflow, addressMismatchStatus: X64InvlpgStubPlanStatus.DestinationAddressMismatch);
        if (storageStatus != X64InvlpgStubPlanStatus.Success)
        {
            return X64InvlpgStubPlanResult.Fail(storageStatus, target, destinationCapacity);
        }

        byte* entry = destination + entryOffset;
        entry[0] = 0x0f;
        entry[1] = 0x01;
        entry[2] = 0x39;
        entry[3] = 0xc3;
        return X64InvlpgStubPlanResult.Success(X64InvlpgStubPhase.Materialized, codePageStart, codePageEnd, entryAddress, entryOffset, target, destinationCapacity);
    }

    public static X64InvlpgStubPlanResult ValidateReadback(byte* source, ulong modeledSourceAddress, ulong sourceCapacity, X64InvlpgStubPlanResult expected)
    {
        if (!expected.Succeeded || expected.Phase != X64InvlpgStubPhase.Materialized)
        {
            return X64InvlpgStubPlanResult.Fail(X64InvlpgStubPlanStatus.ExpectedPlanNotMaterialized, expected.Target, sourceCapacity);
        }

        if (expected.CodePageStart.TryGetRawValueForHardwareRegisterBoundary(out ulong rawCodePageStart) != OS.Boot.AddressValueStatus.Success)
        {
            return X64InvlpgStubPlanResult.Fail(X64InvlpgStubPlanStatus.InvalidExpectedPlan, expected.Target, sourceCapacity);
        }

        X64InvlpgStubPlanStatus storageStatus = ValidateStorage(source, modeledSourceAddress, sourceCapacity, rawCodePageStart, X64InvlpgStubPlanStatus.MissingSource, X64InvlpgStubPlanStatus.MisalignedSource, X64InvlpgStubPlanStatus.SourceCapacityTooSmall, X64InvlpgStubPlanStatus.SourceCapacityExceedsPage, X64InvlpgStubPlanStatus.SourceSpanOverflow, X64InvlpgStubPlanStatus.SourceAddressMismatch);
        if (storageStatus != X64InvlpgStubPlanStatus.Success)
        {
            return X64InvlpgStubPlanResult.Fail(storageStatus, expected.Target, sourceCapacity);
        }

        byte* entry = source + expected.EntryOffset;
        if (entry[0] != 0x0f || entry[1] != 0x01 || entry[2] != 0x39 || entry[3] != 0xc3)
        {
            return X64InvlpgStubPlanResult.Fail(X64InvlpgStubPlanStatus.ByteReadbackMismatch, expected.Target, sourceCapacity);
        }

        return X64InvlpgStubPlanResult.Success(X64InvlpgStubPhase.ReadBackVerified, expected.CodePageStart, expected.CodePageEndExclusive, expected.EntryAddress, expected.EntryOffset, expected.Target, sourceCapacity);
    }

    public static X64InvlpgInvocationPlanResult CreateInvocationPlan(X64InvlpgStubPlanResult verifiedStub, X64InvlpgInvocationRequest request)
    {
        if (!verifiedStub.ReadBackVerified)
        {
            return X64InvlpgInvocationPlanResult.Fail(X64InvlpgStubPlanStatus.InvalidExpectedPlan, request);
        }

        X64InvlpgStubPlanStatus requestStatus = request.Validate();
        if (requestStatus != X64InvlpgStubPlanStatus.Success)
        {
            return X64InvlpgInvocationPlanResult.Fail(requestStatus, request);
        }

        if (!request.EntryAddress.Equals(verifiedStub.EntryAddress))
        {
            return X64InvlpgInvocationPlanResult.Fail(X64InvlpgStubPlanStatus.InvocationEntryMismatch, request);
        }

        if (!request.Target.Target.Equals(verifiedStub.Target.Target) || request.Target.Ordinal != verifiedStub.Target.Ordinal)
        {
            return X64InvlpgInvocationPlanResult.Fail(X64InvlpgStubPlanStatus.InvocationTargetMismatch, request);
        }

        if (request.EntryAddress.TryGetRawValueForHardwareRegisterBoundary(out ulong rawEntry) != OS.Boot.AddressValueStatus.Success)
        {
            return X64InvlpgInvocationPlanResult.Fail(X64InvlpgStubPlanStatus.InvalidInvocationRequest, request);
        }

        return X64InvlpgInvocationPlanResult.Success(request, rawEntry);
    }

    private static X64InvlpgStubPlanStatus ValidateGeometry(OS.Boot.VirtualAddress codePageStart, ulong entryOffset, X64TlbInvalidationRecord target, out OS.Boot.VirtualAddress codePageEnd, out OS.Boot.VirtualAddress entryAddress, out ulong rawCodePageStart)
    {
        codePageEnd = default;
        entryAddress = default;
        rawCodePageStart = 0;
        if (target.Validate() != X64TlbInvalidationStatus.Success)
        {
            return X64InvlpgStubPlanStatus.InvalidTargetRecord;
        }

        if (target.Ordinal != 0)
        {
            return X64InvlpgStubPlanStatus.TargetOrdinalMustBeZero;
        }

        if (!codePageStart.IsValid || !codePageStart.IsAligned(PageSize) || codePageStart.TryGetRawValueForHardwareRegisterBoundary(out rawCodePageStart) != OS.Boot.AddressValueStatus.Success)
        {
            return X64InvlpgStubPlanStatus.InvalidCodePage;
        }

        if (rawCodePageStart == 0 || rawCodePageStart >= X64TlbInvalidationPlan.LowCanonicalEndExclusive || codePageStart.TryGetEndExclusive(PageSize, out codePageEnd) != OS.Boot.AddressValueStatus.Success || codePageEnd.TryGetRawValueForHardwareRegisterBoundary(out ulong rawEnd) != OS.Boot.AddressValueStatus.Success || rawEnd > X64TlbInvalidationPlan.LowCanonicalEndExclusive)
        {
            return X64InvlpgStubPlanStatus.NonCanonicalCodePage;
        }

        if (entryOffset >= PageSize || entryOffset > PageSize - RequiredByteLength)
        {
            return X64InvlpgStubPlanStatus.EntryOutsideCodePage;
        }

        if ((entryOffset & (EntryAlignment - 1)) != 0)
        {
            return X64InvlpgStubPlanStatus.MisalignedEntry;
        }

        if (codePageStart.TryAdd(entryOffset, out entryAddress) != OS.Boot.AddressValueStatus.Success || !entryAddress.IsAligned(EntryAlignment))
        {
            return X64InvlpgStubPlanStatus.InvalidEntryOffset;
        }

        return X64InvlpgStubPlanStatus.Success;
    }

    private static X64InvlpgStubPlanStatus ValidateStorage(byte* storage, ulong modeledAddress, ulong capacity, ulong expectedModeledAddress, X64InvlpgStubPlanStatus missingStatus, X64InvlpgStubPlanStatus misalignedStatus, X64InvlpgStubPlanStatus capacitySmallStatus, X64InvlpgStubPlanStatus capacityLargeStatus, X64InvlpgStubPlanStatus spanOverflowStatus, X64InvlpgStubPlanStatus addressMismatchStatus)
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

        return X64InvlpgStubPlanStatus.Success;
    }
}
