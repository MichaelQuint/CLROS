namespace OS.Kernel.Diagnostics;

public enum NormalizeInterruptFramePageOperation : byte
{
    None = 0,
    Materialize = 1,
    ValidateReadBack = 2
}

public enum NormalizeInterruptFramePageStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    PageCapacityTooSmall = 2,
    MissingPage = 3,
    MisalignedPagePointer = 4,
    NonCanonicalPagePointer = 5,
    PagePointerSpanOverflow = 6,
    NonCanonicalPagePointerSpan = 7,
    NullPageAddress = 8,
    MisalignedPageAddress = 9,
    NonCanonicalPageAddress = 10,
    PageAddressSpanOverflow = 11,
    NonCanonicalPageAddressSpan = 12,
    NullCommonTargetAddress = 13,
    NonCanonicalCommonTargetAddress = 14,
    CommonTargetOverlapsPage = 15,
    CommonTargetOutOfRel32Range = 16,
    PageByteMismatch = 17
}

public readonly struct NormalizeInterruptFramePageResult
{
    private readonly byte _isInitialized;
    private readonly byte _destinationMutated;
    private readonly byte _readBackPerformed;

    private NormalizeInterruptFramePageResult(
        NormalizeInterruptFramePageOperation operation,
        NormalizeInterruptFramePageStatus status,
        ulong modeledPageAddress,
        ulong commonTargetAddress,
        ulong pageCapacity,
        int bytesWritten,
        int bytesCompared,
        int firstMismatchOffset,
        ulong fingerprint,
        bool destinationMutated,
        bool readBackPerformed)
    {
        _isInitialized = 1;
        _destinationMutated = destinationMutated ? (byte)1 : (byte)0;
        _readBackPerformed = readBackPerformed ? (byte)1 : (byte)0;
        Operation = operation;
        Status = status;
        ModeledPageAddress = modeledPageAddress;
        CommonTargetAddress = commonTargetAddress;
        PageCapacity = pageCapacity;
        BytesWritten = bytesWritten;
        BytesCompared = bytesCompared;
        FirstMismatchOffset = firstMismatchOffset;
        HardwareErrorCodeVectorCount =
            NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HardwareErrorCodeVectorCount;
        SyntheticErrorCodeVectorCount =
            NormalizeErrorCodeNonErrorCodeInterruptFrame
                .SyntheticErrorCodeVectorCount;
        Fingerprint = fingerprint;
    }

    public bool IsInitialized => _isInitialized == 1;

    public NormalizeInterruptFramePageOperation Operation { get; }

    public NormalizeInterruptFramePageStatus Status { get; }

    public ulong ModeledPageAddress { get; }

    public ulong CommonTargetAddress { get; }

    public ulong PageCapacity { get; }

    public int RequiredBytes =>
        NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount;

    public int BytesWritten { get; }

    public int BytesCompared { get; }

    public int FirstMismatchOffset { get; }

    public int HardwareErrorCodeVectorCount { get; }

    public int SyntheticErrorCodeVectorCount { get; }

    public ulong Fingerprint { get; }

    public bool DestinationMutated => _destinationMutated == 1;

    public bool ReadBackPerformed => _readBackPerformed == 1;

    public bool Materialized =>
        IsInitialized &&
        Operation == NormalizeInterruptFramePageOperation.Materialize &&
        Status == NormalizeInterruptFramePageStatus.Success &&
        BytesWritten == RequiredBytes &&
        BytesCompared == RequiredBytes &&
        FirstMismatchOffset == -1 &&
        HardwareErrorCodeVectorCount ==
            NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HardwareErrorCodeVectorCount &&
        SyntheticErrorCodeVectorCount ==
            NormalizeErrorCodeNonErrorCodeInterruptFrame
                .SyntheticErrorCodeVectorCount &&
        DestinationMutated &&
        ReadBackPerformed;

    public bool ReadBackValidated =>
        IsInitialized &&
        Operation ==
            NormalizeInterruptFramePageOperation.ValidateReadBack &&
        Status == NormalizeInterruptFramePageStatus.Success &&
        BytesWritten == 0 &&
        BytesCompared == RequiredBytes &&
        FirstMismatchOffset == -1 &&
        HardwareErrorCodeVectorCount ==
            NormalizeErrorCodeNonErrorCodeInterruptFrame
                .HardwareErrorCodeVectorCount &&
        SyntheticErrorCodeVectorCount ==
            NormalizeErrorCodeNonErrorCodeInterruptFrame
                .SyntheticErrorCodeVectorCount &&
        !DestinationMutated &&
        ReadBackPerformed;

    public bool Succeeded => Materialized || ReadBackValidated;

    internal static NormalizeInterruptFramePageResult Pass(
        NormalizeInterruptFramePageOperation operation,
        ulong modeledPageAddress,
        ulong commonTargetAddress,
        ulong pageCapacity,
        int bytesWritten,
        ulong fingerprint,
        bool destinationMutated)
    {
        return new NormalizeInterruptFramePageResult(
            operation,
            NormalizeInterruptFramePageStatus.Success,
            modeledPageAddress,
            commonTargetAddress,
            pageCapacity,
            bytesWritten,
            NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount,
            -1,
            fingerprint,
            destinationMutated,
            true);
    }

    internal static NormalizeInterruptFramePageResult Fail(
        NormalizeInterruptFramePageOperation operation,
        NormalizeInterruptFramePageStatus status,
        ulong modeledPageAddress,
        ulong commonTargetAddress,
        ulong pageCapacity,
        int bytesWritten = 0,
        int bytesCompared = 0,
        int firstMismatchOffset = -1,
        ulong fingerprint = 0,
        bool destinationMutated = false,
        bool readBackPerformed = false)
    {
        return new NormalizeInterruptFramePageResult(
            operation,
            status,
            modeledPageAddress,
            commonTargetAddress,
            pageCapacity,
            bytesWritten,
            bytesCompared,
            firstMismatchOffset,
            fingerprint,
            destinationMutated,
            readBackPerformed);
    }
}

/// <summary>
/// Materializes one caller-owned x64 interrupt-entry page as ordinary bytes.
/// Every 16-byte vector slot creates the same semantic stack prefix:
/// vector, has-hardware-error-code, error-code. The common target consumes
/// that prefix. This contract writes and validates bytes only; it does not
/// install an IDT, publish executable mappings, or execute a stub.
/// </summary>
public static unsafe class NormalizeErrorCodeNonErrorCodeInterruptFrame
{
    public const int VectorCount = 256;
    public const int SlotByteCount = 16;
    public const int PageByteCount = VectorCount * SlotByteCount;
    public const int PageAlignment = PageByteCount;
    public const int NormalizedSemanticSlotCount = 3;

    public const uint HardwareErrorCodeVectorMask = 0x4022_7d00U;
    public const int HardwareErrorCodeVectorCount = 9;
    public const int SyntheticErrorCodeVectorCount =
        VectorCount - HardwareErrorCodeVectorCount;

    public const byte PushImmediate8Opcode = 0x6a;
    public const byte PushImmediate32Opcode = 0x68;
    public const byte JumpRelative32Opcode = 0xe9;
    public const byte NopOpcode = 0x90;

    public const int NoHardwareSyntheticErrorCodePushOffset = 0;
    public const int NoHardwareFlagPushOffset = 2;
    public const int NoHardwareVectorPushOffset = 4;
    public const int NoHardwareJumpOffset = 9;
    public const int NoHardwarePaddingOffset = 14;

    public const int HardwareFlagPushOffset = 0;
    public const int HardwareVectorPushOffset = 2;
    public const int HardwareJumpOffset = 7;
    public const int HardwarePaddingOffset = 12;

    public const int RelativeJumpInstructionByteCount = 5;
    public const ulong Fnv1A64OffsetBasis = 14_695_981_039_346_656_037UL;
    public const ulong Fnv1A64Prime = 1_099_511_628_211UL;

#if !NOSTDLIB_EFI
    public static NormalizeInterruptFramePageResult Materialize(
        Span<byte> destination,
        ulong modeledPageAddress,
        ulong commonTargetAddress)
    {
        fixed (byte* destinationPointer = destination)
        {
            return Materialize(
                destinationPointer,
                (ulong)destination.Length,
                modeledPageAddress,
                commonTargetAddress);
        }
    }

    public static NormalizeInterruptFramePageResult ValidateReadBack(
        ReadOnlySpan<byte> source,
        ulong modeledPageAddress,
        ulong commonTargetAddress)
    {
        fixed (byte* sourcePointer = source)
        {
            return ValidateReadBack(
                sourcePointer,
                (ulong)source.Length,
                modeledPageAddress,
                commonTargetAddress);
        }
    }
#endif

    public static NormalizeInterruptFramePageResult Materialize(
        byte* destination,
        ulong destinationCapacity,
        ulong modeledPageAddress,
        ulong commonTargetAddress)
    {
        NormalizeInterruptFramePageStatus status = ValidateStorageAndTarget(
            destination,
            destinationCapacity,
            modeledPageAddress,
            commonTargetAddress);
        if (status != NormalizeInterruptFramePageStatus.Success)
        {
            return NormalizeInterruptFramePageResult.Fail(
                NormalizeInterruptFramePageOperation.Materialize,
                status,
                modeledPageAddress,
                commonTargetAddress,
                destinationCapacity);
        }

        ulong expectedFingerprint = ComputeExpectedFingerprint(
            modeledPageAddress,
            commonTargetAddress);
        for (int offset = 0; offset < PageByteCount; offset++)
        {
            destination[offset] = ExpectedByteAt(
                offset,
                modeledPageAddress,
                commonTargetAddress);
        }

        ulong observedFingerprint = ComputeFingerprint(
            destination,
            PageByteCount);
        for (int offset = 0; offset < PageByteCount; offset++)
        {
            if (destination[offset] != ExpectedByteAt(
                    offset,
                    modeledPageAddress,
                    commonTargetAddress))
            {
                return NormalizeInterruptFramePageResult.Fail(
                    NormalizeInterruptFramePageOperation.Materialize,
                    NormalizeInterruptFramePageStatus.PageByteMismatch,
                    modeledPageAddress,
                    commonTargetAddress,
                    destinationCapacity,
                    PageByteCount,
                    offset + 1,
                    offset,
                    observedFingerprint,
                    true,
                    true);
            }
        }

        if (observedFingerprint != expectedFingerprint)
        {
            return NormalizeInterruptFramePageResult.Fail(
                NormalizeInterruptFramePageOperation.Materialize,
                NormalizeInterruptFramePageStatus.PageByteMismatch,
                modeledPageAddress,
                commonTargetAddress,
                destinationCapacity,
                PageByteCount,
                PageByteCount,
                -1,
                observedFingerprint,
                true,
                true);
        }

        return NormalizeInterruptFramePageResult.Pass(
            NormalizeInterruptFramePageOperation.Materialize,
            modeledPageAddress,
            commonTargetAddress,
            destinationCapacity,
            PageByteCount,
            observedFingerprint,
            true);
    }

    public static NormalizeInterruptFramePageResult ValidateReadBack(
        byte* source,
        ulong sourceCapacity,
        ulong modeledPageAddress,
        ulong commonTargetAddress)
    {
        NormalizeInterruptFramePageStatus status = ValidateStorageAndTarget(
            source,
            sourceCapacity,
            modeledPageAddress,
            commonTargetAddress);
        if (status != NormalizeInterruptFramePageStatus.Success)
        {
            return NormalizeInterruptFramePageResult.Fail(
                NormalizeInterruptFramePageOperation.ValidateReadBack,
                status,
                modeledPageAddress,
                commonTargetAddress,
                sourceCapacity);
        }

        ulong observedFingerprint = ComputeFingerprint(source, PageByteCount);
        for (int offset = 0; offset < PageByteCount; offset++)
        {
            if (source[offset] != ExpectedByteAt(
                    offset,
                    modeledPageAddress,
                    commonTargetAddress))
            {
                return NormalizeInterruptFramePageResult.Fail(
                    NormalizeInterruptFramePageOperation.ValidateReadBack,
                    NormalizeInterruptFramePageStatus.PageByteMismatch,
                    modeledPageAddress,
                    commonTargetAddress,
                    sourceCapacity,
                    0,
                    offset + 1,
                    offset,
                    observedFingerprint,
                    false,
                    true);
            }
        }

        return NormalizeInterruptFramePageResult.Pass(
            NormalizeInterruptFramePageOperation.ValidateReadBack,
            modeledPageAddress,
            commonTargetAddress,
            sourceCapacity,
            0,
            observedFingerprint,
            false);
    }

    public static bool HasHardwareErrorCode(int vector)
    {
        if (vector < 0 || vector >= 32)
        {
            return false;
        }

        uint bit = 1U << vector;
        return (HardwareErrorCodeVectorMask & bit) != 0;
    }

    public static bool IsCanonicalX64Address(ulong address)
    {
        ulong highBits = address >> 48;
        bool signBitSet = ((address >> 47) & 1UL) != 0;
        return signBitSet
            ? highBits == 0xffffUL
            : highBits == 0;
    }

    private static NormalizeInterruptFramePageStatus ValidateStorageAndTarget(
        byte* storage,
        ulong capacity,
        ulong modeledPageAddress,
        ulong commonTargetAddress)
    {
        if (capacity < PageByteCount)
        {
            return NormalizeInterruptFramePageStatus.PageCapacityTooSmall;
        }

        if (storage == null)
        {
            return NormalizeInterruptFramePageStatus.MissingPage;
        }

        ulong storageAddress = (ulong)storage;
        if ((storageAddress & (PageAlignment - 1UL)) != 0)
        {
            return NormalizeInterruptFramePageStatus.MisalignedPagePointer;
        }

        if (!IsCanonicalX64Address(storageAddress))
        {
            return NormalizeInterruptFramePageStatus.NonCanonicalPagePointer;
        }

        if (storageAddress > ~0UL - capacity)
        {
            return NormalizeInterruptFramePageStatus.PagePointerSpanOverflow;
        }

        if (!IsCanonicalSpan(storageAddress, capacity))
        {
            return NormalizeInterruptFramePageStatus
                .NonCanonicalPagePointerSpan;
        }

        if (modeledPageAddress == 0)
        {
            return NormalizeInterruptFramePageStatus.NullPageAddress;
        }

        if ((modeledPageAddress & (PageAlignment - 1UL)) != 0)
        {
            return NormalizeInterruptFramePageStatus.MisalignedPageAddress;
        }

        if (!IsCanonicalX64Address(modeledPageAddress))
        {
            return NormalizeInterruptFramePageStatus.NonCanonicalPageAddress;
        }

        if (modeledPageAddress > ~0UL - capacity)
        {
            return NormalizeInterruptFramePageStatus.PageAddressSpanOverflow;
        }

        if (!IsCanonicalSpan(modeledPageAddress, capacity))
        {
            return NormalizeInterruptFramePageStatus
                .NonCanonicalPageAddressSpan;
        }

        if (commonTargetAddress == 0)
        {
            return NormalizeInterruptFramePageStatus.NullCommonTargetAddress;
        }

        if (!IsCanonicalX64Address(commonTargetAddress))
        {
            return NormalizeInterruptFramePageStatus
                .NonCanonicalCommonTargetAddress;
        }

        if (commonTargetAddress >= modeledPageAddress &&
            commonTargetAddress < modeledPageAddress + PageByteCount)
        {
            return NormalizeInterruptFramePageStatus.CommonTargetOverlapsPage;
        }

        for (int vector = 0; vector < VectorCount; vector++)
        {
            ulong nextInstructionAddress = GetJumpNextInstructionAddress(
                vector,
                modeledPageAddress);
            if (!TryGetRel32(
                    nextInstructionAddress,
                    commonTargetAddress,
                    out _))
            {
                return NormalizeInterruptFramePageStatus
                    .CommonTargetOutOfRel32Range;
            }
        }

        return NormalizeInterruptFramePageStatus.Success;
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

    private static ulong GetJumpNextInstructionAddress(
        int vector,
        ulong modeledPageAddress)
    {
        int jumpOffset = HasHardwareErrorCode(vector)
            ? HardwareJumpOffset
            : NoHardwareJumpOffset;
        return modeledPageAddress +
            ((ulong)vector * SlotByteCount) +
            (ulong)jumpOffset +
            RelativeJumpInstructionByteCount;
    }

    private static bool TryGetRel32(
        ulong nextInstructionAddress,
        ulong targetAddress,
        out int displacement)
    {
        if (targetAddress >= nextInstructionAddress)
        {
            ulong distance = targetAddress - nextInstructionAddress;
            if (distance > 0x7fff_ffffUL)
            {
                displacement = 0;
                return false;
            }

            displacement = (int)distance;
            return true;
        }

        ulong backwardsDistance = nextInstructionAddress - targetAddress;
        if (backwardsDistance > 0x8000_0000UL)
        {
            displacement = 0;
            return false;
        }

        if (backwardsDistance == 0x8000_0000UL)
        {
            displacement = -2_147_483_647 - 1;
            return true;
        }

        displacement = -(int)backwardsDistance;
        return true;
    }

    private static byte ExpectedByteAt(
        int pageOffset,
        ulong modeledPageAddress,
        ulong commonTargetAddress)
    {
        int vector = pageOffset / SlotByteCount;
        int slotOffset = pageOffset - (vector * SlotByteCount);
        bool hasHardwareErrorCode = HasHardwareErrorCode(vector);

        if (hasHardwareErrorCode)
        {
            switch (slotOffset)
            {
                case HardwareFlagPushOffset:
                    return PushImmediate8Opcode;
                case HardwareFlagPushOffset + 1:
                    return 1;
                case HardwareVectorPushOffset:
                    return PushImmediate32Opcode;
                case HardwareVectorPushOffset + 1:
                    return (byte)vector;
                case HardwareVectorPushOffset + 2:
                case HardwareVectorPushOffset + 3:
                case HardwareVectorPushOffset + 4:
                    return 0;
                case HardwareJumpOffset:
                    return JumpRelative32Opcode;
                case HardwareJumpOffset + 1:
                case HardwareJumpOffset + 2:
                case HardwareJumpOffset + 3:
                case HardwareJumpOffset + 4:
                    return RelativeJumpByteAt(
                        vector,
                        slotOffset - (HardwareJumpOffset + 1),
                        modeledPageAddress,
                        commonTargetAddress);
                default:
                    return NopOpcode;
            }
        }

        switch (slotOffset)
        {
            case NoHardwareSyntheticErrorCodePushOffset:
                return PushImmediate8Opcode;
            case NoHardwareSyntheticErrorCodePushOffset + 1:
                return 0;
            case NoHardwareFlagPushOffset:
                return PushImmediate8Opcode;
            case NoHardwareFlagPushOffset + 1:
                return 0;
            case NoHardwareVectorPushOffset:
                return PushImmediate32Opcode;
            case NoHardwareVectorPushOffset + 1:
                return (byte)vector;
            case NoHardwareVectorPushOffset + 2:
            case NoHardwareVectorPushOffset + 3:
            case NoHardwareVectorPushOffset + 4:
                return 0;
            case NoHardwareJumpOffset:
                return JumpRelative32Opcode;
            case NoHardwareJumpOffset + 1:
            case NoHardwareJumpOffset + 2:
            case NoHardwareJumpOffset + 3:
            case NoHardwareJumpOffset + 4:
                return RelativeJumpByteAt(
                    vector,
                    slotOffset - (NoHardwareJumpOffset + 1),
                    modeledPageAddress,
                    commonTargetAddress);
            default:
                return NopOpcode;
        }
    }

    private static byte RelativeJumpByteAt(
        int vector,
        int displacementByteIndex,
        ulong modeledPageAddress,
        ulong commonTargetAddress)
    {
        TryGetRel32(
            GetJumpNextInstructionAddress(vector, modeledPageAddress),
            commonTargetAddress,
            out int displacement);
        uint encoded = unchecked((uint)displacement);
        return (byte)(encoded >> (displacementByteIndex * 8));
    }

    private static ulong ComputeExpectedFingerprint(
        ulong modeledPageAddress,
        ulong commonTargetAddress)
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < PageByteCount; offset++)
        {
            fingerprint ^= ExpectedByteAt(
                offset,
                modeledPageAddress,
                commonTargetAddress);
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
}
