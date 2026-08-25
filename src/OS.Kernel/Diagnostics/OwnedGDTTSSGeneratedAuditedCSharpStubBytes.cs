namespace OS.Kernel.Diagnostics;

public enum OwnedGdtTssTerminalStubStatus : byte
{
    Success = 0,
    MissingContext = 1,
    ContextCapacityTooSmall = 2,
    MisalignedContextPointer = 3,
    NonCanonicalContextPointer = 4,
    ContextPointerSpanOverflow = 5,
    NonCanonicalContextPointerSpan = 6,
    NullContextAddress = 7,
    MisalignedContextAddress = 8,
    NonCanonicalContextAddress = 9,
    ContextAddressSpanOverflow = 10,
    NonCanonicalContextAddressSpan = 11,
    MissingStub = 12,
    StubCapacityTooSmall = 13,
    MisalignedStubPointer = 14,
    NonCanonicalStubPointer = 15,
    StubPointerSpanOverflow = 16,
    NonCanonicalStubPointerSpan = 17,
    NullStubAddress = 18,
    MisalignedStubAddress = 19,
    NonCanonicalStubAddress = 20,
    StubAddressSpanOverflow = 21,
    NonCanonicalStubAddressSpan = 22,
    StubOverlapsContextPointer = 23,
    StubOverlapsContextAddress = 24,
    ContextByteMismatch = 25,
    StubByteMismatch = 26,
    UnexpectedGeneratedFingerprint = 27
}

/// <summary>
/// Exact caller-owned RCX context consumed by the terminal M02-009 byte plan.
/// All fields are inline bytes. The first and second ten-byte fields are packed
/// x64 GDTR images; the processor writes the second image and the TR selector.
/// </summary>
public unsafe struct OwnedGdtTssTerminalContext
{
    public fixed byte LoadGdtr[10];
    public fixed byte ReadBackGdtr[10];
    public ushort ReadBackTrSelector;
    public ushort Reserved;
    public fixed byte Gdt[40];
    public fixed byte Tss[104];
}

public readonly struct OwnedGdtTssTerminalStubResult
{
    private const byte PrepareContextOperation = 1;
    private const byte EmitStubOperation = 2;
    private const byte ValidateReadBackOperation = 3;

    private readonly byte _isInitialized;
    private readonly byte _operation;

    private OwnedGdtTssTerminalStubResult(
        OwnedGdtTssTerminalStubStatus status,
        byte operation,
        ulong modeledContextAddress,
        ulong modeledStubAddress,
        ulong contextCapacity,
        ulong stubCapacity,
        int contextBytesWritten,
        int contextBytesCompared,
        int stubBytesWritten,
        int stubBytesCompared,
        int firstMismatchOffset,
        ulong contextFingerprint,
        ulong stubFingerprint)
    {
        _isInitialized = 1;
        _operation = operation;
        Status = status;
        ModeledContextAddress = modeledContextAddress;
        ModeledStubAddress = modeledStubAddress;
        ContextCapacity = contextCapacity;
        StubCapacity = stubCapacity;
        ContextBytesWritten = contextBytesWritten;
        ContextBytesCompared = contextBytesCompared;
        StubBytesWritten = stubBytesWritten;
        StubBytesCompared = stubBytesCompared;
        FirstMismatchOffset = firstMismatchOffset;
        ContextFingerprint = contextFingerprint;
        StubFingerprint = stubFingerprint;
    }

    public bool IsInitialized => _isInitialized == 1;

    public OwnedGdtTssTerminalStubStatus Status { get; }

    public ulong ModeledContextAddress { get; }

    public ulong ModeledStubAddress { get; }

    public ulong ContextCapacity { get; }

    public ulong StubCapacity { get; }

    public int ContextBytesWritten { get; }

    public int ContextBytesCompared { get; }

    public int StubBytesWritten { get; }

    public int StubBytesCompared { get; }

    public int FirstMismatchOffset { get; }

    public ulong ContextFingerprint { get; }

    public ulong StubFingerprint { get; }

    public ulong ExpectedStubFingerprint =>
        OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubFnv1A64;

    public bool ContextPrepared =>
        IsInitialized &&
        _operation == PrepareContextOperation &&
        Status == OwnedGdtTssTerminalStubStatus.Success &&
        ContextBytesWritten ==
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount &&
        ContextBytesCompared ==
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount &&
        StubBytesWritten == 0 &&
        StubBytesCompared == 0 &&
        FirstMismatchOffset == -1 &&
        ContextFingerprint != 0;

    public bool StubEmitted =>
        IsInitialized &&
        _operation == EmitStubOperation &&
        Status == OwnedGdtTssTerminalStubStatus.Success &&
        ContextBytesWritten == 0 &&
        ContextBytesCompared ==
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount &&
        StubBytesWritten ==
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubByteCount &&
        StubBytesCompared == 0 &&
        FirstMismatchOffset == -1 &&
        ContextFingerprint != 0 &&
        StubFingerprint == ExpectedStubFingerprint;

    public bool ReadBackValidated =>
        IsInitialized &&
        _operation == ValidateReadBackOperation &&
        Status == OwnedGdtTssTerminalStubStatus.Success &&
        ContextBytesWritten == 0 &&
        ContextBytesCompared ==
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount &&
        StubBytesWritten == 0 &&
        StubBytesCompared ==
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubByteCount &&
        FirstMismatchOffset == -1 &&
        ContextFingerprint != 0 &&
        StubFingerprint == ExpectedStubFingerprint;

    public bool Succeeded =>
        ContextPrepared || StubEmitted || ReadBackValidated;

    internal static OwnedGdtTssTerminalStubResult Prepared(
        ulong modeledContextAddress,
        ulong contextCapacity,
        ulong contextFingerprint)
    {
        return new OwnedGdtTssTerminalStubResult(
            OwnedGdtTssTerminalStubStatus.Success,
            PrepareContextOperation,
            modeledContextAddress,
            0,
            contextCapacity,
            0,
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount,
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount,
            0,
            0,
            -1,
            contextFingerprint,
            0);
    }

    internal static OwnedGdtTssTerminalStubResult Emitted(
        ulong modeledContextAddress,
        ulong modeledStubAddress,
        ulong contextCapacity,
        ulong stubCapacity,
        ulong contextFingerprint,
        ulong stubFingerprint)
    {
        return new OwnedGdtTssTerminalStubResult(
            OwnedGdtTssTerminalStubStatus.Success,
            EmitStubOperation,
            modeledContextAddress,
            modeledStubAddress,
            contextCapacity,
            stubCapacity,
            0,
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount,
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubByteCount,
            0,
            -1,
            contextFingerprint,
            stubFingerprint);
    }

    internal static OwnedGdtTssTerminalStubResult Validated(
        ulong modeledContextAddress,
        ulong modeledStubAddress,
        ulong contextCapacity,
        ulong stubCapacity,
        ulong contextFingerprint,
        ulong stubFingerprint)
    {
        return new OwnedGdtTssTerminalStubResult(
            OwnedGdtTssTerminalStubStatus.Success,
            ValidateReadBackOperation,
            modeledContextAddress,
            modeledStubAddress,
            contextCapacity,
            stubCapacity,
            0,
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount,
            0,
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.StubByteCount,
            -1,
            contextFingerprint,
            stubFingerprint);
    }

    internal static OwnedGdtTssTerminalStubResult Fail(
        OwnedGdtTssTerminalStubStatus status,
        byte operation,
        ulong modeledContextAddress,
        ulong modeledStubAddress,
        ulong contextCapacity,
        ulong stubCapacity,
        int contextBytesCompared = 0,
        int stubBytesCompared = 0,
        int firstMismatchOffset = -1,
        ulong contextFingerprint = 0,
        ulong stubFingerprint = 0)
    {
        return new OwnedGdtTssTerminalStubResult(
            status,
            operation,
            modeledContextAddress,
            modeledStubAddress,
            contextCapacity,
            stubCapacity,
            0,
            contextBytesCompared,
            0,
            stubBytesCompared,
            firstMismatchOffset,
            contextFingerprint,
            stubFingerprint);
    }

    internal static byte PrepareOperation => PrepareContextOperation;

    internal static byte EmitOperation => EmitStubOperation;

    internal static byte ValidateOperation => ValidateReadBackOperation;
}

/// <summary>
/// Materializes a relocation-free Windows-x64 terminal stub as ordinary data.
/// RCX is the sole entry argument and points at an exact prepared context.
/// Host code never invokes these bytes.
/// </summary>
public static unsafe class OwnedGDTTSSGeneratedAuditedCSharpStubBytes
{
    public const int GdtrByteCount = 10;
    public const int GdtByteCount = 40;
    public const int TssByteCount = 104;
    public const ushort GdtrLimit = 39;

    public const ushort NullSelector = 0x0000;
    public const ushort KernelCodeSelector = 0x0008;
    public const ushort KernelDataSelector = 0x0010;
    public const ushort TssSelector = 0x0018;

    public const int LoadGdtrOffset = 0;
    public const int ReadBackGdtrOffset = 10;
    public const int ReadBackTrSelectorOffset = 20;
    public const int ContextReservedOffset = 22;
    public const int GdtOffset = 24;
    public const int TssOffset = 64;
    public const int ContextByteCount = 168;
    public const int RequiredContextByteCount = ContextByteCount;

    public const int NullDescriptorOffset = 0;
    public const int KernelCodeDescriptorOffset = 8;
    public const int KernelDataDescriptorOffset = 16;
    public const int TssDescriptorOffset = 24;
    public const int TssAccessByteOffset = TssDescriptorOffset + 5;
    public const byte AvailableTssAccess = 0x89;
    public const byte BusyTssAccess = 0x8b;

    public const int TssIoMapBaseOffset = 102;
    public const ushort TssIoMapBase = TssByteCount;

    public const ulong RequiredAlignment = 16;
    public const ulong ContextAlignment = RequiredAlignment;
    public const ulong StubAlignment = RequiredAlignment;

    public const int PrologueByteCount = 67;
    public const int SuccessMarkerByteCount = 170;
    public const int FailureMarkerByteCount = 72;
    public const int SerialBlockPrefixByteCount = 4;
    public const int SerialInstructionBytesPerMarkerByte = 12;
    public const int TerminalByteCount = 4;

    public const int SuccessSerialOffset = PrologueByteCount;
    public const int SuccessTerminalOffset =
        SuccessSerialOffset +
        SerialBlockPrefixByteCount +
        (SuccessMarkerByteCount * SerialInstructionBytesPerMarkerByte);
    public const int FailureSerialOffset =
        SuccessTerminalOffset + TerminalByteCount;
    public const int FailureTerminalOffset =
        FailureSerialOffset +
        SerialBlockPrefixByteCount +
        (FailureMarkerByteCount * SerialInstructionBytesPerMarkerByte);
    public const int StubByteCount = FailureTerminalOffset + TerminalByteCount;
    public const int RequiredByteCount = StubByteCount;

    public const ulong Fnv1A64OffsetBasis = 14_695_981_039_346_656_037UL;
    public const ulong Fnv1A64Prime = 1_099_511_628_211UL;
    public const ulong StubFnv1A64 = 0xc9c0_4dee_967d_640aUL;

    public const ushort Com1DataPort = 0x03f8;
    public const ushort Com1LineStatusPort = 0x03fd;
    public const byte Com1TransmitterHoldingRegisterEmptyMask = 0x20;

    public const int AuditedInstructionCount = 1718;
    public const int AuditedConditionalBranchCount = 246;
    public const int AuditedTerminalBranchCount = 2;
    public const int AuditedInInstructionCount =
        SuccessMarkerByteCount + FailureMarkerByteCount;
    public const int AuditedOutInstructionCount = AuditedInInstructionCount;
    public const int AuditedCliInstructionCount = 3;
    public const int AuditedHltInstructionCount = 2;

    public const bool UsesWindowsX64Abi = true;
    public const bool ContextPassedInRcx = true;
    public const bool UsesOnlyVolatileRegisters = true;
    public const bool TouchesRsp = false;
    public const bool UsesStack = false;
    public const bool HasCalls = false;
    public const bool HasReturns = false;
    public const bool HasExternalRelocations = false;
    public const bool UsesAddressDependentMarkerPointers = false;
    public const bool MarkersAreInlineImmediateBytes = true;
    public const bool RequiresWritableGdt = true;
    public const bool TerminalHalt = true;
    public const bool HostTestsInvokeBytes = false;
    public const bool PerformsLiveDescriptorLoad = false;

#if !NOSTDLIB_EFI
    public static OwnedGdtTssTerminalStubResult PrepareContext(
        Span<byte> destination,
        ulong modeledContextAddress)
    {
        fixed (byte* destinationPointer = destination)
        {
            return PrepareContext(
                destinationPointer,
                modeledContextAddress,
                (ulong)destination.Length);
        }
    }

    public static OwnedGdtTssTerminalStubResult Emit(
        Span<byte> destination,
        ulong modeledDestinationAddress,
        ReadOnlySpan<byte> context,
        ulong modeledContextAddress)
    {
        fixed (byte* destinationPointer = destination)
        fixed (byte* contextPointer = context)
        {
            return Emit(
                destinationPointer,
                modeledDestinationAddress,
                (ulong)destination.Length,
                contextPointer,
                modeledContextAddress,
                (ulong)context.Length);
        }
    }

    public static OwnedGdtTssTerminalStubResult ValidateReadBack(
        ReadOnlySpan<byte> source,
        ulong modeledSourceAddress,
        ReadOnlySpan<byte> context,
        ulong modeledContextAddress)
    {
        fixed (byte* sourcePointer = source)
        fixed (byte* contextPointer = context)
        {
            return ValidateReadBack(
                sourcePointer,
                modeledSourceAddress,
                (ulong)source.Length,
                contextPointer,
                modeledContextAddress,
                (ulong)context.Length);
        }
    }
#endif

    public static OwnedGdtTssTerminalStubResult PrepareContext(
        OwnedGdtTssTerminalContext* destination,
        ulong modeledContextAddress,
        ulong destinationCapacity)
    {
        return PrepareContext(
            (byte*)destination,
            modeledContextAddress,
            destinationCapacity);
    }

    public static OwnedGdtTssTerminalStubResult PrepareContext(
        byte* destination,
        ulong modeledContextAddress,
        ulong destinationCapacity)
    {
        OwnedGdtTssTerminalStubStatus storageStatus =
            ValidateContextStorage(
                destination,
                modeledContextAddress,
                destinationCapacity);
        if (storageStatus != OwnedGdtTssTerminalStubStatus.Success)
        {
            return OwnedGdtTssTerminalStubResult.Fail(
                storageStatus,
                OwnedGdtTssTerminalStubResult.PrepareOperation,
                modeledContextAddress,
                0,
                destinationCapacity,
                0);
        }

        for (int offset = 0; offset < ContextByteCount; offset++)
        {
            destination[offset] = ExpectedContextByteAt(
                offset,
                modeledContextAddress);
        }

        ulong contextFingerprint = ComputeFingerprint(
            destination,
            ContextByteCount);
        return OwnedGdtTssTerminalStubResult.Prepared(
            modeledContextAddress,
            destinationCapacity,
            contextFingerprint);
    }

    public static OwnedGdtTssTerminalStubResult Emit(
        byte* destination,
        ulong modeledDestinationAddress,
        ulong destinationCapacity,
        OwnedGdtTssTerminalContext* context,
        ulong modeledContextAddress,
        ulong contextCapacity)
    {
        return Emit(
            destination,
            modeledDestinationAddress,
            destinationCapacity,
            (byte*)context,
            modeledContextAddress,
            contextCapacity);
    }

    public static OwnedGdtTssTerminalStubResult Emit(
        byte* destination,
        ulong modeledDestinationAddress,
        ulong destinationCapacity,
        byte* context,
        ulong modeledContextAddress,
        ulong contextCapacity)
    {
        OwnedGdtTssTerminalStubStatus storageStatus = ValidatePairStorage(
            destination,
            modeledDestinationAddress,
            destinationCapacity,
            context,
            modeledContextAddress,
            contextCapacity);
        if (storageStatus != OwnedGdtTssTerminalStubStatus.Success)
        {
            return OwnedGdtTssTerminalStubResult.Fail(
                storageStatus,
                OwnedGdtTssTerminalStubResult.EmitOperation,
                modeledContextAddress,
                modeledDestinationAddress,
                contextCapacity,
                destinationCapacity);
        }

        OwnedGdtTssTerminalStubStatus contextStatus =
            ValidatePreparedContext(
                context,
                modeledContextAddress,
                out int contextBytesCompared,
                out int contextMismatchOffset,
                out ulong contextFingerprint);
        if (contextStatus != OwnedGdtTssTerminalStubStatus.Success)
        {
            return OwnedGdtTssTerminalStubResult.Fail(
                contextStatus,
                OwnedGdtTssTerminalStubResult.EmitOperation,
                modeledContextAddress,
                modeledDestinationAddress,
                contextCapacity,
                destinationCapacity,
                contextBytesCompared,
                0,
                contextMismatchOffset,
                contextFingerprint,
                0);
        }

        ulong stubFingerprint = ComputeExpectedStubFingerprint();
        if (stubFingerprint != StubFnv1A64)
        {
            return OwnedGdtTssTerminalStubResult.Fail(
                OwnedGdtTssTerminalStubStatus.UnexpectedGeneratedFingerprint,
                OwnedGdtTssTerminalStubResult.EmitOperation,
                modeledContextAddress,
                modeledDestinationAddress,
                contextCapacity,
                destinationCapacity,
                contextBytesCompared,
                0,
                -1,
                contextFingerprint,
                stubFingerprint);
        }

        for (int offset = 0; offset < StubByteCount; offset++)
        {
            destination[offset] = ExpectedStubByteAt(offset);
        }

        return OwnedGdtTssTerminalStubResult.Emitted(
            modeledContextAddress,
            modeledDestinationAddress,
            contextCapacity,
            destinationCapacity,
            contextFingerprint,
            stubFingerprint);
    }

    public static OwnedGdtTssTerminalStubResult ValidateReadBack(
        byte* source,
        ulong modeledSourceAddress,
        ulong sourceCapacity,
        OwnedGdtTssTerminalContext* context,
        ulong modeledContextAddress,
        ulong contextCapacity)
    {
        return ValidateReadBack(
            source,
            modeledSourceAddress,
            sourceCapacity,
            (byte*)context,
            modeledContextAddress,
            contextCapacity);
    }

    public static OwnedGdtTssTerminalStubResult ValidateReadBack(
        byte* source,
        ulong modeledSourceAddress,
        ulong sourceCapacity,
        byte* context,
        ulong modeledContextAddress,
        ulong contextCapacity)
    {
        OwnedGdtTssTerminalStubStatus storageStatus = ValidatePairStorage(
            source,
            modeledSourceAddress,
            sourceCapacity,
            context,
            modeledContextAddress,
            contextCapacity);
        if (storageStatus != OwnedGdtTssTerminalStubStatus.Success)
        {
            return OwnedGdtTssTerminalStubResult.Fail(
                storageStatus,
                OwnedGdtTssTerminalStubResult.ValidateOperation,
                modeledContextAddress,
                modeledSourceAddress,
                contextCapacity,
                sourceCapacity);
        }

        OwnedGdtTssTerminalStubStatus contextStatus =
            ValidatePreparedContext(
                context,
                modeledContextAddress,
                out int contextBytesCompared,
                out int contextMismatchOffset,
                out ulong contextFingerprint);
        if (contextStatus != OwnedGdtTssTerminalStubStatus.Success)
        {
            return OwnedGdtTssTerminalStubResult.Fail(
                contextStatus,
                OwnedGdtTssTerminalStubResult.ValidateOperation,
                modeledContextAddress,
                modeledSourceAddress,
                contextCapacity,
                sourceCapacity,
                contextBytesCompared,
                0,
                contextMismatchOffset,
                contextFingerprint,
                0);
        }

        ulong stubFingerprint = ComputeFingerprint(source, StubByteCount);
        for (int offset = 0; offset < StubByteCount; offset++)
        {
            if (source[offset] != ExpectedStubByteAt(offset))
            {
                return OwnedGdtTssTerminalStubResult.Fail(
                    OwnedGdtTssTerminalStubStatus.StubByteMismatch,
                    OwnedGdtTssTerminalStubResult.ValidateOperation,
                    modeledContextAddress,
                    modeledSourceAddress,
                    contextCapacity,
                    sourceCapacity,
                    contextBytesCompared,
                    offset + 1,
                    offset,
                    contextFingerprint,
                    stubFingerprint);
            }
        }

        return OwnedGdtTssTerminalStubResult.Validated(
            modeledContextAddress,
            modeledSourceAddress,
            contextCapacity,
            sourceCapacity,
            contextFingerprint,
            stubFingerprint);
    }

    public static bool IsCanonicalX64Address(ulong address)
    {
        ulong highBits = address >> 48;
        bool signBitSet = ((address >> 47) & 1UL) != 0;
        return signBitSet
            ? highBits == 0xffffUL
            : highBits == 0;
    }

    private static OwnedGdtTssTerminalStubStatus ValidatePairStorage(
        byte* stub,
        ulong modeledStubAddress,
        ulong stubCapacity,
        byte* context,
        ulong modeledContextAddress,
        ulong contextCapacity)
    {
        OwnedGdtTssTerminalStubStatus contextStatus =
            ValidateContextStorage(
                context,
                modeledContextAddress,
                contextCapacity);
        if (contextStatus != OwnedGdtTssTerminalStubStatus.Success)
        {
            return contextStatus;
        }

        OwnedGdtTssTerminalStubStatus stubStatus = ValidateStubStorage(
            stub,
            modeledStubAddress,
            stubCapacity);
        if (stubStatus != OwnedGdtTssTerminalStubStatus.Success)
        {
            return stubStatus;
        }

        if (RangesOverlap(
                (ulong)stub,
                stubCapacity,
                (ulong)context,
                contextCapacity))
        {
            return OwnedGdtTssTerminalStubStatus
                .StubOverlapsContextPointer;
        }

        if (RangesOverlap(
                modeledStubAddress,
                stubCapacity,
                modeledContextAddress,
                contextCapacity))
        {
            return OwnedGdtTssTerminalStubStatus
                .StubOverlapsContextAddress;
        }

        return OwnedGdtTssTerminalStubStatus.Success;
    }

    private static OwnedGdtTssTerminalStubStatus ValidateContextStorage(
        byte* storage,
        ulong modeledAddress,
        ulong capacity)
    {
        if (capacity < ContextByteCount)
        {
            return OwnedGdtTssTerminalStubStatus.ContextCapacityTooSmall;
        }

        if (storage == null)
        {
            return OwnedGdtTssTerminalStubStatus.MissingContext;
        }

        ulong storageAddress = (ulong)storage;
        if ((storageAddress & (ContextAlignment - 1)) != 0)
        {
            return OwnedGdtTssTerminalStubStatus.MisalignedContextPointer;
        }

        if (!IsCanonicalX64Address(storageAddress))
        {
            return OwnedGdtTssTerminalStubStatus.NonCanonicalContextPointer;
        }

        if (storageAddress > ~0UL - capacity)
        {
            return OwnedGdtTssTerminalStubStatus.ContextPointerSpanOverflow;
        }

        if (!IsCanonicalSpan(storageAddress, capacity))
        {
            return OwnedGdtTssTerminalStubStatus
                .NonCanonicalContextPointerSpan;
        }

        if (modeledAddress == 0)
        {
            return OwnedGdtTssTerminalStubStatus.NullContextAddress;
        }

        if ((modeledAddress & (ContextAlignment - 1)) != 0)
        {
            return OwnedGdtTssTerminalStubStatus.MisalignedContextAddress;
        }

        if (!IsCanonicalX64Address(modeledAddress))
        {
            return OwnedGdtTssTerminalStubStatus.NonCanonicalContextAddress;
        }

        if (modeledAddress > ~0UL - capacity)
        {
            return OwnedGdtTssTerminalStubStatus.ContextAddressSpanOverflow;
        }

        if (!IsCanonicalSpan(modeledAddress, capacity))
        {
            return OwnedGdtTssTerminalStubStatus
                .NonCanonicalContextAddressSpan;
        }

        return OwnedGdtTssTerminalStubStatus.Success;
    }

    private static OwnedGdtTssTerminalStubStatus ValidateStubStorage(
        byte* storage,
        ulong modeledAddress,
        ulong capacity)
    {
        if (capacity < StubByteCount)
        {
            return OwnedGdtTssTerminalStubStatus.StubCapacityTooSmall;
        }

        if (storage == null)
        {
            return OwnedGdtTssTerminalStubStatus.MissingStub;
        }

        ulong storageAddress = (ulong)storage;
        if ((storageAddress & (StubAlignment - 1)) != 0)
        {
            return OwnedGdtTssTerminalStubStatus.MisalignedStubPointer;
        }

        if (!IsCanonicalX64Address(storageAddress))
        {
            return OwnedGdtTssTerminalStubStatus.NonCanonicalStubPointer;
        }

        if (storageAddress > ~0UL - capacity)
        {
            return OwnedGdtTssTerminalStubStatus.StubPointerSpanOverflow;
        }

        if (!IsCanonicalSpan(storageAddress, capacity))
        {
            return OwnedGdtTssTerminalStubStatus.NonCanonicalStubPointerSpan;
        }

        if (modeledAddress == 0)
        {
            return OwnedGdtTssTerminalStubStatus.NullStubAddress;
        }

        if ((modeledAddress & (StubAlignment - 1)) != 0)
        {
            return OwnedGdtTssTerminalStubStatus.MisalignedStubAddress;
        }

        if (!IsCanonicalX64Address(modeledAddress))
        {
            return OwnedGdtTssTerminalStubStatus.NonCanonicalStubAddress;
        }

        if (modeledAddress > ~0UL - capacity)
        {
            return OwnedGdtTssTerminalStubStatus.StubAddressSpanOverflow;
        }

        if (!IsCanonicalSpan(modeledAddress, capacity))
        {
            return OwnedGdtTssTerminalStubStatus.NonCanonicalStubAddressSpan;
        }

        return OwnedGdtTssTerminalStubStatus.Success;
    }

    private static bool IsCanonicalSpan(ulong start, ulong length)
    {
        ulong endInclusive = start + length - 1;
        if (!IsCanonicalX64Address(endInclusive))
        {
            return false;
        }

        return ((start >> 47) & 1UL) ==
            ((endInclusive >> 47) & 1UL);
    }

    private static bool RangesOverlap(
        ulong firstStart,
        ulong firstLength,
        ulong secondStart,
        ulong secondLength)
    {
        ulong firstEnd = firstStart + firstLength;
        ulong secondEnd = secondStart + secondLength;
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static OwnedGdtTssTerminalStubStatus ValidatePreparedContext(
        byte* context,
        ulong modeledContextAddress,
        out int bytesCompared,
        out int firstMismatchOffset,
        out ulong contextFingerprint)
    {
        bytesCompared = 0;
        firstMismatchOffset = -1;
        contextFingerprint = ComputeFingerprint(context, ContextByteCount);

        for (int offset = 0; offset < ContextByteCount; offset++)
        {
            bytesCompared = offset + 1;
            if (context[offset] != ExpectedContextByteAt(
                    offset,
                    modeledContextAddress))
            {
                firstMismatchOffset = offset;
                return OwnedGdtTssTerminalStubStatus.ContextByteMismatch;
            }
        }

        return OwnedGdtTssTerminalStubStatus.Success;
    }

    private static byte ExpectedContextByteAt(
        int offset,
        ulong modeledContextAddress)
    {
        ulong gdtAddress = modeledContextAddress + GdtOffset;
        ulong tssAddress = modeledContextAddress + TssOffset;

        if (offset == LoadGdtrOffset)
        {
            return (byte)GdtrLimit;
        }

        if (offset == LoadGdtrOffset + 1)
        {
            return (byte)(GdtrLimit >> 8);
        }

        if (offset >= LoadGdtrOffset + 2 &&
            offset < LoadGdtrOffset + GdtrByteCount)
        {
            return (byte)(gdtAddress >>
                ((offset - (LoadGdtrOffset + 2)) * 8));
        }

        if (offset < GdtOffset)
        {
            return 0;
        }

        if (offset < TssOffset)
        {
            int gdtByteOffset = offset - GdtOffset;
            if (gdtByteOffset < KernelCodeDescriptorOffset)
            {
                return 0;
            }

            if (gdtByteOffset < KernelDataDescriptorOffset)
            {
                int descriptorOffset =
                    gdtByteOffset - KernelCodeDescriptorOffset;
                switch (descriptorOffset)
                {
                    case 0:
                    case 1:
                        return 0xff;
                    case 5:
                        return 0x9a;
                    case 6:
                        return 0xaf;
                    default:
                        return 0;
                }
            }

            if (gdtByteOffset < TssDescriptorOffset)
            {
                int descriptorOffset =
                    gdtByteOffset - KernelDataDescriptorOffset;
                switch (descriptorOffset)
                {
                    case 0:
                    case 1:
                        return 0xff;
                    case 5:
                        return 0x92;
                    case 6:
                        return 0xcf;
                    default:
                        return 0;
                }
            }

            int tssDescriptorByte =
                gdtByteOffset - TssDescriptorOffset;
            switch (tssDescriptorByte)
            {
                case 0:
                    return 103;
                case 1:
                    return 0;
                case 2:
                    return (byte)tssAddress;
                case 3:
                    return (byte)(tssAddress >> 8);
                case 4:
                    return (byte)(tssAddress >> 16);
                case 5:
                    return AvailableTssAccess;
                case 6:
                    return 0;
                case 7:
                    return (byte)(tssAddress >> 24);
                case 8:
                    return (byte)(tssAddress >> 32);
                case 9:
                    return (byte)(tssAddress >> 40);
                case 10:
                    return (byte)(tssAddress >> 48);
                case 11:
                    return (byte)(tssAddress >> 56);
                default:
                    return 0;
            }
        }

        int tssByteOffset = offset - TssOffset;
        if (tssByteOffset == TssIoMapBaseOffset)
        {
            return (byte)TssIoMapBase;
        }

        if (tssByteOffset == TssIoMapBaseOffset + 1)
        {
            return (byte)(TssIoMapBase >> 8);
        }

        return 0;
    }

    private static byte ExpectedStubByteAt(int offset)
    {
        if (offset < PrologueByteCount)
        {
            return PrologueByteAt(offset);
        }

        if (offset < SuccessTerminalOffset)
        {
            return SerialBlockByteAt(
                offset - SuccessSerialOffset,
                true);
        }

        if (offset < FailureSerialOffset)
        {
            return TerminalByteAt(offset - SuccessTerminalOffset);
        }

        if (offset < FailureTerminalOffset)
        {
            return SerialBlockByteAt(
                offset - FailureSerialOffset,
                false);
        }

        return TerminalByteAt(offset - FailureTerminalOffset);
    }

    private static byte SerialBlockByteAt(
        int blockOffset,
        bool successMarker)
    {
        if (blockOffset < SerialBlockPrefixByteCount)
        {
            switch (blockOffset)
            {
                case 0:
                    return 0x66;
                case 1:
                    return 0xba;
                case 2:
                    return 0xfd;
                default:
                    return 0x03;
            }
        }

        int serialOffset = blockOffset - SerialBlockPrefixByteCount;
        int markerIndex =
            serialOffset / SerialInstructionBytesPerMarkerByte;
        int instructionOffset =
            serialOffset % SerialInstructionBytesPerMarkerByte;
        switch (instructionOffset)
        {
            case 0:
                return 0xec;
            case 1:
                return 0xa8;
            case 2:
                return Com1TransmitterHoldingRegisterEmptyMask;
            case 3:
                return 0x74;
            case 4:
                return 0xfb;
            case 5:
                return 0xb2;
            case 6:
                return 0xf8;
            case 7:
                return 0xb0;
            case 8:
                return successMarker
                    ? SuccessMarkerByteAt(markerIndex)
                    : FailureMarkerByteAt(markerIndex);
            case 9:
                return 0xee;
            case 10:
                return 0xb2;
            default:
                return 0xfd;
        }
    }

    private static byte TerminalByteAt(int offset)
    {
        switch (offset)
        {
            case 0:
                return 0xfa;
            case 1:
                return 0xf4;
            case 2:
                return 0xeb;
            default:
                return 0xfc;
        }
    }

    private static byte PrologueByteAt(int offset)
    {
        int wordIndex = offset >> 3;
        int shift = (offset & 7) * 8;
        ulong word;
        switch (wordIndex)
        {
            case 0:
                word = 0x0018_b866_1101_0ffaUL;
                break;
            case 1:
                word = 0x0f0a_4101_0fd8_000fUL;
                break;
            case 2:
                word = 0x3b66_018b_6614_4900UL;
                break;
            case 3:
                word = 0x0000_0823_850f_0a41UL;
                break;
            case 4:
                word = 0x0c41_3b48_0241_8b48UL;
                break;
            case 5:
                word = 0x8366_0000_0815_850fUL;
                break;
            case 6:
                word = 0x0008_0a85_0f18_1479UL;
                break;
            case 7:
                word = 0x0085_0f8b_3579_8000UL;
                break;
            default:
                word = 0x0000_0000_0000_0008UL;
                break;
        }

        return (byte)(word >> shift);
    }

    private static byte SuccessMarkerByteAt(int index)
    {
        int wordIndex = index >> 3;
        int shift = (index & 7) * 8;
        ulong word;
        switch (wordIndex)
        {
            case 0: word = 0x304d_5f53_4f52_4c43UL; break;
            case 1: word = 0x4552_5f39_3030_5f32UL; break;
            case 2: word = 0x7274_6467_2059_4441UL; break;
            case 3: word = 0x7274_2065_7572_743dUL; break;
            case 4: word = 0x6467_2065_7572_743dUL; break;
            case 5: word = 0x343d_7365_7479_4274UL; break;
            case 6: word = 0x7479_4273_7374_2030UL; break;
            case 7: word = 0x6720_3430_313d_7365UL; break;
            case 8: word = 0x7469_6d69_4c72_7464UL; break;
            case 9: word = 0x6553_7274_2039_333dUL; break;
            case 10: word = 0x323d_726f_7463_656cUL; break;
            case 11: word = 0x4364_656e_776f_2034UL; break;
            case 12: word = 0x6575_7274_3d31_6d6fUL; break;
            case 13: word = 0x616e_696d_7265_7420UL; break;
            case 14: word = 0x7274_3d74_6c61_486cUL; break;
            case 15: word = 0x4574_736f_7020_6575UL; break;
            case 16: word = 0x5374_6f6f_4274_6978UL; break;
            case 17: word = 0x3d73_6563_6976_7265UL; break;
            case 18: word = 0x540a_0d65_736c_6166UL; break;
            case 19: word = 0x5353_4150_5f54_5345UL; break;
            case 20: word = 0x3930_302e_3230_6d20UL; break;
            default: word = 0x0000_0000_0000_0a0dUL; break;
        }

        return (byte)(word >> shift);
    }

    private static byte FailureMarkerByteAt(int index)
    {
        int wordIndex = index >> 3;
        int shift = (index & 7) * 8;
        ulong word;
        switch (wordIndex)
        {
            case 0: word = 0x454e_574f_5f32_304dUL; break;
            case 1: word = 0x5354_5f54_4447_5f44UL; break;
            case 2: word = 0x5946_4952_4556_5f53UL; break;
            case 3: word = 0x0d44_454c_4941_465fUL; break;
            case 4: word = 0x4146_5f54_5345_540aUL; break;
            case 5: word = 0x302e_3230_6d20_4c49UL; break;
            case 6: word = 0x5f72_7464_6720_3930UL; break;
            case 7: word = 0x696d_5f72_745f_726fUL; break;
            default: word = 0x0a0d_6863_7461_6d73UL; break;
        }

        return (byte)(word >> shift);
    }

    private static ulong ComputeFingerprint(byte* source, int length)
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < length; offset++)
        {
            fingerprint ^= source[offset];
            fingerprint *= Fnv1A64Prime;
        }

        return fingerprint;
    }

    private static ulong ComputeExpectedStubFingerprint()
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < StubByteCount; offset++)
        {
            fingerprint ^= ExpectedStubByteAt(offset);
            fingerprint *= Fnv1A64Prime;
        }

        return fingerprint;
    }
}
