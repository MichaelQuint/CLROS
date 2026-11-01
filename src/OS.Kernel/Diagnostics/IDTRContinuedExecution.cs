namespace OS.Kernel.Diagnostics;
/// <summary>
/// Emits and audits a frozen, relocation-free Windows-x64 reversible IDTR
/// transition stub as ordinary caller-owned bytes. Host code never invokes
/// the privileged instruction stream.
/// </summary>

public static unsafe class IDTRContinuedExecution
{
    public const int IdtrByteCount = 10;
    public const int IdtTableByteCount = 4096;
    public const ushort IdtrLimit = IdtTableByteCount - 1;
    public const int RequestedIdtrOffset = 0;
    public const int PreviousIdtrOffset = 16;
    public const int LoadedIdtrOffset = 32;
    public const int RestoredIdtrOffset = 48;
    public const int RflagsBeforeOffset = 64;
    public const int RflagsAfterOffset = 72;
    public const int ContextByteCount = 80;
    public const int StubByteCount = 52;
    public const ulong ContextAlignment = 16;
    public const ulong StubAlignment = 16;
    public const ulong IdtAlignment = IdtTableByteCount;
    public const ulong ExecutionCookie = 0x4f53_4944_5452_3132UL;
    public const ulong RflagsInterruptEnableBit = 0x0000_0000_0000_0200UL;
    public const ulong Fnv1A64OffsetBasis = 14_695_981_039_346_656_037UL;
    public const ulong Fnv1A64Prime = 1_099_511_628_211UL;
    public const ulong StubFnv1A64 = 0xaff5_9d64_80de_7f3cUL;
    public const int AuditedInstructionCount = 17;
    public const int LidtInstructionCount = 2;
    public const int SidtInstructionCount = 3;
    public const int CliInstructionCount = 1;
    public const int StiInstructionCount = 1;
    public const int ReturnInstructionCount = 1;
    public const bool UsesWindowsX64Abi = true;
    public const bool ContextPassedInRcx = true;
    public const bool UsesOnlyVolatileRegisters = true;
    public const bool UsesBalancedStackOperations = true;
    public const bool HasCalls = false;
    public const bool HasReturns = true;
    public const bool HasExternalRelocations = false;
    public const bool RestoresPreviousIdtrBeforeReturn = true;
    public const bool RestoresInterruptFlagBeforeReturn = true;
    public const bool HostTestsInvokeBytes = false;
#if !NOSTDLIB_EFI
    public static IDTRContinuedExecutionPlanResult PrepareContext(Span<byte> destination, ulong modeledContextAddress, ulong ownedIdtAddress)
    {
        fixed (byte* destinationPointer = destination)
        {
            return PrepareContext(destinationPointer, modeledContextAddress, (ulong)destination.Length, ownedIdtAddress);
        }
    }

    public static IDTRContinuedExecutionPlanResult EmitStub(Span<byte> destination, ulong modeledStubAddress, ReadOnlySpan<byte> context, ulong modeledContextAddress, ulong ownedIdtAddress)
    {
        fixed (byte* destinationPointer = destination)
        fixed (byte* contextPointer = context)
        {
            return EmitStub(destinationPointer, modeledStubAddress, (ulong)destination.Length, contextPointer, modeledContextAddress, (ulong)context.Length, ownedIdtAddress);
        }
    }

    public static IDTRContinuedExecutionPlanResult ValidateStubReadBack(ReadOnlySpan<byte> source, ulong modeledStubAddress, ReadOnlySpan<byte> context, ulong modeledContextAddress, ulong ownedIdtAddress)
    {
        fixed (byte* sourcePointer = source)
        fixed (byte* contextPointer = context)
        {
            return ValidateStubReadBack(sourcePointer, modeledStubAddress, (ulong)source.Length, contextPointer, modeledContextAddress, (ulong)context.Length, ownedIdtAddress);
        }
    }

    public static IDTRContinuedExecutionAuditResult AuditExecution(ReadOnlySpan<byte> context, ulong modeledContextAddress, ulong ownedIdtAddress, ulong executionCookie)
    {
        fixed (byte* contextPointer = context)
        {
            return AuditExecution(contextPointer, modeledContextAddress, (ulong)context.Length, ownedIdtAddress, executionCookie);
        }
    }

#endif
    public static IDTRContinuedExecutionPlanResult PrepareContext(IDTRContinuedExecutionContext* destination, ulong modeledContextAddress, ulong destinationCapacity, ulong ownedIdtAddress)
    {
        return PrepareContext((byte*)destination, modeledContextAddress, destinationCapacity, ownedIdtAddress);
    }

    public static IDTRContinuedExecutionPlanResult PrepareContext(byte* destination, ulong modeledContextAddress, ulong destinationCapacity, ulong ownedIdtAddress)
    {
        IDTRContinuedExecutionStatus status = ValidateContextAndIdt(destination, modeledContextAddress, destinationCapacity, ownedIdtAddress);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.PrepareContext, status, modeledContextAddress, 0, ownedIdtAddress, destinationCapacity, 0);
        }

        for (int offset = 0; offset < ContextByteCount; offset++)
        {
            destination[offset] = ExpectedPreparedContextByteAt(offset, ownedIdtAddress);
        }

        ulong fingerprint = ComputeFingerprint(destination, ContextByteCount);
        for (int offset = 0; offset < ContextByteCount; offset++)
        {
            if (destination[offset] != ExpectedPreparedContextByteAt(offset, ownedIdtAddress))
            {
                return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.PrepareContext, IDTRContinuedExecutionStatus.ContextByteMismatch, modeledContextAddress, 0, ownedIdtAddress, destinationCapacity, 0, ContextByteCount, offset + 1, 0, 0, offset, fingerprint, 0, true, true);
            }
        }

        return IDTRContinuedExecutionPlanResult.Pass(IDTRContinuedExecutionOperation.PrepareContext, modeledContextAddress, 0, ownedIdtAddress, destinationCapacity, 0, ContextByteCount, 0, fingerprint, 0, true);
    }

    public static IDTRContinuedExecutionPlanResult EmitStub(byte* destination, ulong modeledStubAddress, ulong destinationCapacity, IDTRContinuedExecutionContext* context, ulong modeledContextAddress, ulong contextCapacity, ulong ownedIdtAddress)
    {
        return EmitStub(destination, modeledStubAddress, destinationCapacity, (byte*)context, modeledContextAddress, contextCapacity, ownedIdtAddress);
    }

    public static IDTRContinuedExecutionPlanResult EmitStub(byte* destination, ulong modeledStubAddress, ulong destinationCapacity, byte* context, ulong modeledContextAddress, ulong contextCapacity, ulong ownedIdtAddress)
    {
        IDTRContinuedExecutionStatus status = ValidateAllStorage(destination, modeledStubAddress, destinationCapacity, context, modeledContextAddress, contextCapacity, ownedIdtAddress);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.EmitStub, status, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, destinationCapacity);
        }

        status = ValidatePreparedContext(context, ownedIdtAddress, out int contextBytesCompared, out int contextMismatchOffset, out ulong contextFingerprint);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.EmitStub, status, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, destinationCapacity, 0, contextBytesCompared, 0, 0, contextMismatchOffset, contextFingerprint);
        }

        ulong expectedFingerprint = ComputeExpectedStubFingerprint();
        if (expectedFingerprint != StubFnv1A64)
        {
            return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.EmitStub, IDTRContinuedExecutionStatus.UnexpectedStubFingerprint, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, destinationCapacity, 0, contextBytesCompared, 0, 0, -1, contextFingerprint, expectedFingerprint);
        }

        for (int offset = 0; offset < StubByteCount; offset++)
        {
            destination[offset] = ExpectedStubByteAt(offset);
        }

        ulong observedFingerprint = ComputeFingerprint(destination, StubByteCount);
        for (int offset = 0; offset < StubByteCount; offset++)
        {
            if (destination[offset] != ExpectedStubByteAt(offset))
            {
                return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.EmitStub, IDTRContinuedExecutionStatus.StubByteMismatch, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, destinationCapacity, 0, contextBytesCompared, StubByteCount, offset + 1, offset, contextFingerprint, observedFingerprint, true, true);
            }
        }

        if (observedFingerprint != StubFnv1A64)
        {
            return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.EmitStub, IDTRContinuedExecutionStatus.UnexpectedStubFingerprint, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, destinationCapacity, 0, contextBytesCompared, StubByteCount, StubByteCount, -1, contextFingerprint, observedFingerprint, true, true);
        }

        return IDTRContinuedExecutionPlanResult.Pass(IDTRContinuedExecutionOperation.EmitStub, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, destinationCapacity, 0, StubByteCount, contextFingerprint, observedFingerprint, true);
    }

    public static IDTRContinuedExecutionPlanResult ValidateStubReadBack(byte* source, ulong modeledStubAddress, ulong sourceCapacity, IDTRContinuedExecutionContext* context, ulong modeledContextAddress, ulong contextCapacity, ulong ownedIdtAddress)
    {
        return ValidateStubReadBack(source, modeledStubAddress, sourceCapacity, (byte*)context, modeledContextAddress, contextCapacity, ownedIdtAddress);
    }

    public static IDTRContinuedExecutionPlanResult ValidateStubReadBack(byte* source, ulong modeledStubAddress, ulong sourceCapacity, byte* context, ulong modeledContextAddress, ulong contextCapacity, ulong ownedIdtAddress)
    {
        IDTRContinuedExecutionStatus status = ValidateAllStorage(source, modeledStubAddress, sourceCapacity, context, modeledContextAddress, contextCapacity, ownedIdtAddress);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.ValidateStubReadBack, status, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, sourceCapacity);
        }

        status = ValidatePreparedContext(context, ownedIdtAddress, out int contextBytesCompared, out int contextMismatchOffset, out ulong contextFingerprint);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.ValidateStubReadBack, status, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, sourceCapacity, 0, contextBytesCompared, 0, 0, contextMismatchOffset, contextFingerprint);
        }

        ulong fingerprint = ComputeFingerprint(source, StubByteCount);
        for (int offset = 0; offset < StubByteCount; offset++)
        {
            if (source[offset] != ExpectedStubByteAt(offset))
            {
                return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.ValidateStubReadBack, IDTRContinuedExecutionStatus.StubByteMismatch, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, sourceCapacity, 0, contextBytesCompared, 0, offset + 1, offset, contextFingerprint, fingerprint, false, true);
            }
        }

        if (fingerprint != StubFnv1A64)
        {
            return IDTRContinuedExecutionPlanResult.Fail(IDTRContinuedExecutionOperation.ValidateStubReadBack, IDTRContinuedExecutionStatus.UnexpectedStubFingerprint, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, sourceCapacity, 0, contextBytesCompared, 0, StubByteCount, -1, contextFingerprint, fingerprint, false, true);
        }

        return IDTRContinuedExecutionPlanResult.Pass(IDTRContinuedExecutionOperation.ValidateStubReadBack, modeledContextAddress, modeledStubAddress, ownedIdtAddress, contextCapacity, sourceCapacity, 0, 0, contextFingerprint, fingerprint, false);
    }

    public static IDTRContinuedExecutionAuditResult AuditExecution(IDTRContinuedExecutionContext* context, ulong modeledContextAddress, ulong contextCapacity, ulong ownedIdtAddress, ulong executionCookie)
    {
        return AuditExecution((byte*)context, modeledContextAddress, contextCapacity, ownedIdtAddress, executionCookie);
    }

    public static IDTRContinuedExecutionAuditResult AuditExecution(byte* context, ulong modeledContextAddress, ulong contextCapacity, ulong ownedIdtAddress, ulong executionCookie)
    {
        IDTRContinuedExecutionStatus status = ValidateContextAndIdt(context, modeledContextAddress, contextCapacity, ownedIdtAddress);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return CreateAuditResult(status, null, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, 0, -1);
        }

        int bytesCompared = 0;
        for (int offset = 0; offset < ContextByteCount; offset++)
        {
            if (!IsAuditInvariantContextOffset(offset))
            {
                continue;
            }

            bytesCompared++;
            if (context[offset] != ExpectedPreparedContextByteAt(offset, ownedIdtAddress))
            {
                return CreateAuditResult(IDTRContinuedExecutionStatus.ContextByteMismatch, context, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, bytesCompared, offset);
            }
        }

        if (executionCookie != ExecutionCookie)
        {
            return CreateAuditResult(IDTRContinuedExecutionStatus.ExecutionCookieMismatch, context, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, bytesCompared, -1);
        }

        if (SameBytes(context + PreviousIdtrOffset, context + RequestedIdtrOffset, IdtrByteCount))
        {
            return CreateAuditResult(IDTRContinuedExecutionStatus.NoIdtrTransitionObserved, context, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, bytesCompared, PreviousIdtrOffset);
        }

        for (int offset = 0; offset < IdtrByteCount; offset++)
        {
            bytesCompared++;
            if (context[LoadedIdtrOffset + offset] != context[RequestedIdtrOffset + offset])
            {
                return CreateAuditResult(IDTRContinuedExecutionStatus.LoadedIdtrMismatch, context, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, bytesCompared, LoadedIdtrOffset + offset);
            }
        }

        for (int offset = 0; offset < IdtrByteCount; offset++)
        {
            bytesCompared++;
            if (context[RestoredIdtrOffset + offset] != context[PreviousIdtrOffset + offset])
            {
                return CreateAuditResult(IDTRContinuedExecutionStatus.RestoredIdtrMismatch, context, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, bytesCompared, RestoredIdtrOffset + offset);
            }
        }

        ulong rflagsBefore = ReadU64(context + RflagsBeforeOffset);
        ulong rflagsAfter = ReadU64(context + RflagsAfterOffset);
        if (((rflagsBefore ^ rflagsAfter) & RflagsInterruptEnableBit) != 0)
        {
            return CreateAuditResult(IDTRContinuedExecutionStatus.InterruptFlagMismatch, context, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, bytesCompared, RflagsAfterOffset);
        }

        return CreateAuditResult(IDTRContinuedExecutionStatus.Success, context, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, ContextByteCount, -1);
    }

    public static bool IsCanonicalX64Address(ulong address)
    {
        ulong highBits = address >> 48;
        bool signBitSet = ((address >> 47) & 1UL) != 0;
        return signBitSet ? highBits == 0xffffUL : highBits == 0;
    }

    private static IDTRContinuedExecutionStatus ValidateAllStorage(byte* stub, ulong modeledStubAddress, ulong stubCapacity, byte* context, ulong modeledContextAddress, ulong contextCapacity, ulong ownedIdtAddress)
    {
        IDTRContinuedExecutionStatus status = ValidateContextStorage(context, modeledContextAddress, contextCapacity);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return status;
        }

        status = ValidateStubStorage(stub, modeledStubAddress, stubCapacity);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return status;
        }

        if (RangesOverlap((ulong)stub, stubCapacity, (ulong)context, contextCapacity))
        {
            return IDTRContinuedExecutionStatus.StubOverlapsContextPointer;
        }

        if (RangesOverlap(modeledStubAddress, stubCapacity, modeledContextAddress, contextCapacity))
        {
            return IDTRContinuedExecutionStatus.StubOverlapsContextAddress;
        }

        status = ValidateIdtAddress(ownedIdtAddress);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return status;
        }

        if (RangesOverlap(ownedIdtAddress, IdtTableByteCount, modeledContextAddress, contextCapacity))
        {
            return IDTRContinuedExecutionStatus.IdtOverlapsContextAddress;
        }

        if (RangesOverlap(ownedIdtAddress, IdtTableByteCount, modeledStubAddress, stubCapacity))
        {
            return IDTRContinuedExecutionStatus.IdtOverlapsStubAddress;
        }

        return IDTRContinuedExecutionStatus.Success;
    }

    private static IDTRContinuedExecutionStatus ValidateContextAndIdt(byte* context, ulong modeledContextAddress, ulong contextCapacity, ulong ownedIdtAddress)
    {
        IDTRContinuedExecutionStatus status = ValidateContextStorage(context, modeledContextAddress, contextCapacity);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return status;
        }

        status = ValidateIdtAddress(ownedIdtAddress);
        if (status != IDTRContinuedExecutionStatus.Success)
        {
            return status;
        }

        if (RangesOverlap(ownedIdtAddress, IdtTableByteCount, modeledContextAddress, contextCapacity))
        {
            return IDTRContinuedExecutionStatus.IdtOverlapsContextAddress;
        }

        return IDTRContinuedExecutionStatus.Success;
    }

    private static IDTRContinuedExecutionStatus ValidateContextStorage(byte* storage, ulong modeledAddress, ulong capacity)
    {
        if (capacity < ContextByteCount)
        {
            return IDTRContinuedExecutionStatus.ContextCapacityTooSmall;
        }

        if (storage == null)
        {
            return IDTRContinuedExecutionStatus.MissingContext;
        }

        ulong pointerAddress = (ulong)storage;
        if ((pointerAddress & (ContextAlignment - 1)) != 0)
        {
            return IDTRContinuedExecutionStatus.MisalignedContextPointer;
        }

        if (!IsCanonicalX64Address(pointerAddress))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalContextPointer;
        }

        if (pointerAddress > ~0UL - capacity)
        {
            return IDTRContinuedExecutionStatus.ContextPointerSpanOverflow;
        }

        if (!IsCanonicalSpan(pointerAddress, capacity))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalContextPointerSpan;
        }

        if (modeledAddress == 0)
        {
            return IDTRContinuedExecutionStatus.NullContextAddress;
        }

        if ((modeledAddress & (ContextAlignment - 1)) != 0)
        {
            return IDTRContinuedExecutionStatus.MisalignedContextAddress;
        }

        if (!IsCanonicalX64Address(modeledAddress))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalContextAddress;
        }

        if (modeledAddress > ~0UL - capacity)
        {
            return IDTRContinuedExecutionStatus.ContextAddressSpanOverflow;
        }

        if (!IsCanonicalSpan(modeledAddress, capacity))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalContextAddressSpan;
        }

        return IDTRContinuedExecutionStatus.Success;
    }

    private static IDTRContinuedExecutionStatus ValidateStubStorage(byte* storage, ulong modeledAddress, ulong capacity)
    {
        if (capacity < StubByteCount)
        {
            return IDTRContinuedExecutionStatus.StubCapacityTooSmall;
        }

        if (storage == null)
        {
            return IDTRContinuedExecutionStatus.MissingStub;
        }

        ulong pointerAddress = (ulong)storage;
        if ((pointerAddress & (StubAlignment - 1)) != 0)
        {
            return IDTRContinuedExecutionStatus.MisalignedStubPointer;
        }

        if (!IsCanonicalX64Address(pointerAddress))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalStubPointer;
        }

        if (pointerAddress > ~0UL - capacity)
        {
            return IDTRContinuedExecutionStatus.StubPointerSpanOverflow;
        }

        if (!IsCanonicalSpan(pointerAddress, capacity))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalStubPointerSpan;
        }

        if (modeledAddress == 0)
        {
            return IDTRContinuedExecutionStatus.NullStubAddress;
        }

        if ((modeledAddress & (StubAlignment - 1)) != 0)
        {
            return IDTRContinuedExecutionStatus.MisalignedStubAddress;
        }

        if (!IsCanonicalX64Address(modeledAddress))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalStubAddress;
        }

        if (modeledAddress > ~0UL - capacity)
        {
            return IDTRContinuedExecutionStatus.StubAddressSpanOverflow;
        }

        if (!IsCanonicalSpan(modeledAddress, capacity))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalStubAddressSpan;
        }

        return IDTRContinuedExecutionStatus.Success;
    }

    private static IDTRContinuedExecutionStatus ValidateIdtAddress(ulong address)
    {
        if (address == 0)
        {
            return IDTRContinuedExecutionStatus.NullIdtAddress;
        }

        if ((address & (IdtAlignment - 1)) != 0)
        {
            return IDTRContinuedExecutionStatus.MisalignedIdtAddress;
        }

        if (!IsCanonicalX64Address(address))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalIdtAddress;
        }

        if (address > ~0UL - IdtTableByteCount)
        {
            return IDTRContinuedExecutionStatus.IdtAddressSpanOverflow;
        }

        if (!IsCanonicalSpan(address, IdtTableByteCount))
        {
            return IDTRContinuedExecutionStatus.NonCanonicalIdtAddressSpan;
        }

        return IDTRContinuedExecutionStatus.Success;
    }

    private static IDTRContinuedExecutionStatus ValidatePreparedContext(byte* context, ulong ownedIdtAddress, out int bytesCompared, out int firstMismatchOffset, out ulong fingerprint)
    {
        bytesCompared = 0;
        firstMismatchOffset = -1;
        fingerprint = ComputeFingerprint(context, ContextByteCount);
        for (int offset = 0; offset < ContextByteCount; offset++)
        {
            bytesCompared = offset + 1;
            if (context[offset] != ExpectedPreparedContextByteAt(offset, ownedIdtAddress))
            {
                firstMismatchOffset = offset;
                return IDTRContinuedExecutionStatus.ContextByteMismatch;
            }
        }

        return IDTRContinuedExecutionStatus.Success;
    }

    private static byte ExpectedPreparedContextByteAt(int offset, ulong ownedIdtAddress)
    {
        if (offset == RequestedIdtrOffset)
        {
            return (byte)(IdtrLimit & 0xff);
        }

        if (offset == RequestedIdtrOffset + 1)
        {
            return (byte)(IdtrLimit >> 8);
        }

        if (offset >= RequestedIdtrOffset + 2 && offset < RequestedIdtrOffset + IdtrByteCount)
        {
            int shift = (offset - (RequestedIdtrOffset + 2)) * 8;
            return (byte)(ownedIdtAddress >> shift);
        }

        return 0;
    }

    private static bool IsAuditInvariantContextOffset(int offset)
    {
        if (offset < RequestedIdtrOffset + IdtrByteCount)
        {
            return true;
        }

        return (offset >= 10 && offset < 16) || (offset >= 26 && offset < 32) || (offset >= 42 && offset < 48) || (offset >= 58 && offset < 64);
    }

    private static byte ExpectedStubByteAt(int offset)
    {
        return offset switch
        {
            0 => 0x9c,
            1 => 0x58,
            2 => 0x48,
            3 => 0x89,
            4 => 0x41,
            5 => 0x40,
            6 => 0xfa,
            7 => 0x0f,
            8 => 0x01,
            9 => 0x49,
            10 => 0x10,
            11 => 0x0f,
            12 => 0x01,
            13 => 0x59,
            14 => 0x00,
            15 => 0x0f,
            16 => 0x01,
            17 => 0x49,
            18 => 0x20,
            19 => 0x0f,
            20 => 0x01,
            21 => 0x59,
            22 => 0x10,
            23 => 0x0f,
            24 => 0x01,
            25 => 0x49,
            26 => 0x30,
            27 => 0xa9,
            28 => 0x00,
            29 => 0x02,
            30 => 0x00,
            31 => 0x00,
            32 => 0x74,
            33 => 0x01,
            34 => 0xfb,
            35 => 0x9c,
            36 => 0x5a,
            37 => 0x48,
            38 => 0x89,
            39 => 0x51,
            40 => 0x48,
            41 => 0x48,
            42 => 0xb8,
            43 => 0x32,
            44 => 0x31,
            45 => 0x52,
            46 => 0x54,
            47 => 0x44,
            48 => 0x49,
            49 => 0x53,
            50 => 0x4f,
            51 => 0xc3,
            _ => 0
        };
    }

    private static ulong ComputeExpectedStubFingerprint()
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < StubByteCount; offset++)
        {
            fingerprint ^= ExpectedStubByteAt(offset);
            fingerprint = unchecked(fingerprint * Fnv1A64Prime);
        }

        return fingerprint;
    }

    private static ulong ComputeFingerprint(byte* source, int length)
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < length; offset++)
        {
            fingerprint ^= source[offset];
            fingerprint = unchecked(fingerprint * Fnv1A64Prime);
        }

        return fingerprint;
    }

    private static bool IsCanonicalSpan(ulong start, ulong length)
    {
        ulong endInclusive = start + length - 1;
        if (!IsCanonicalX64Address(endInclusive))
        {
            return false;
        }

        return ((start >> 47) & 1UL) == ((endInclusive >> 47) & 1UL);
    }

    private static bool RangesOverlap(ulong firstStart, ulong firstLength, ulong secondStart, ulong secondLength)
    {
        ulong firstEnd = firstStart + firstLength;
        ulong secondEnd = secondStart + secondLength;
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static bool SameBytes(byte* first, byte* second, int length)
    {
        for (int offset = 0; offset < length; offset++)
        {
            if (first[offset] != second[offset])
            {
                return false;
            }
        }

        return true;
    }

    private static ushort ReadU16(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static ulong ReadU64(byte* source)
    {
        return (ulong)source[0] | ((ulong)source[1] << 8) | ((ulong)source[2] << 16) | ((ulong)source[3] << 24) | ((ulong)source[4] << 32) | ((ulong)source[5] << 40) | ((ulong)source[6] << 48) | ((ulong)source[7] << 56);
    }

    private static IDTRContinuedExecutionAuditResult CreateAuditResult(IDTRContinuedExecutionStatus status, byte* context, ulong modeledContextAddress, ulong ownedIdtAddress, ulong contextCapacity, ulong executionCookie, int contextBytesCompared, int firstMismatchOffset)
    {
        if (context == null || contextCapacity < ContextByteCount)
        {
            return IDTRContinuedExecutionAuditResult.Create(status, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, contextBytesCompared, firstMismatchOffset, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        return IDTRContinuedExecutionAuditResult.Create(status, modeledContextAddress, ownedIdtAddress, contextCapacity, executionCookie, contextBytesCompared, firstMismatchOffset, ReadU16(context + PreviousIdtrOffset), ReadU64(context + PreviousIdtrOffset + 2), ReadU16(context + LoadedIdtrOffset), ReadU64(context + LoadedIdtrOffset + 2), ReadU16(context + RestoredIdtrOffset), ReadU64(context + RestoredIdtrOffset + 2), ReadU64(context + RflagsBeforeOffset), ReadU64(context + RflagsAfterOffset));
    }
}
