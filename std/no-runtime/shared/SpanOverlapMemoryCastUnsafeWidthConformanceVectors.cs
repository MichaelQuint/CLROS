namespace OS.Std.Memory;

public enum SpanMemoryConformanceOperation : byte
{
    Uninitialized = 0,
    Slice = 1,
    Overlap = 2,
    MemoryCast = 3,
    UnalignedWidth = 4
}

public enum SpanMemoryConformanceStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    DestinationTooSmall = 2,
    VectorPointerNull = 3,
    VectorCountMismatch = 4,
    VectorUninitialized = 5,
    VectorKeyOutOfRange = 6,
    OperationOutOfRange = 7,
    VectorMalformed = 8,
    DuplicateVectorKey = 9,
    VectorOrderMismatch = 10,
    CatalogFingerprintMismatch = 11
}

public readonly struct SpanMemoryConformanceVector
{
    private readonly bool _isInitialized;

    public SpanMemoryConformanceVector(
        byte key,
        SpanMemoryConformanceOperation operation,
        int sourceOffset,
        int destinationOffset,
        int byteCount,
        byte sourceWidth,
        byte destinationWidth,
        byte flags,
        ulong input,
        ulong expected)
    {
        _isInitialized = true;
        Key = key;
        Operation = operation;
        SourceOffset = sourceOffset;
        DestinationOffset = destinationOffset;
        ByteCount = byteCount;
        SourceWidth = sourceWidth;
        DestinationWidth = destinationWidth;
        Flags = flags;
        Input = input;
        Expected = expected;
    }

    public byte Key { get; }

    public SpanMemoryConformanceOperation Operation { get; }

    public int SourceOffset { get; }

    public int DestinationOffset { get; }

    public int ByteCount { get; }

    public byte SourceWidth { get; }

    public byte DestinationWidth { get; }

    public byte Flags { get; }

    public ulong Input { get; }

    public ulong Expected { get; }

    public bool IsInitialized => _isInitialized;

    public bool ExpectedSuccess =>
        (Flags & SpanOverlapMemoryCastUnsafeWidthConformanceVectors
            .ExpectedSuccessFlag) != 0;

    public bool SignExtend =>
        (Flags & SpanOverlapMemoryCastUnsafeWidthConformanceVectors
            .SignExtendFlag) != 0;
}

public readonly struct SpanMemoryConformanceResult
{
    private readonly bool _isInitialized;

    private SpanMemoryConformanceResult(
        SpanMemoryConformanceStatus status,
        int problemIndex,
        int conflictIndex,
        int vectorCount,
        ulong expectedFingerprint,
        ulong actualFingerprint)
    {
        _isInitialized = true;
        Status = status;
        ProblemIndex = problemIndex;
        ConflictIndex = conflictIndex;
        VectorCount = vectorCount;
        ExpectedFingerprint = expectedFingerprint;
        ActualFingerprint = actualFingerprint;
    }

    public SpanMemoryConformanceStatus Status { get; }

    public int ProblemIndex { get; }

    public int ConflictIndex { get; }

    public int VectorCount { get; }

    public ulong ExpectedFingerprint { get; }

    public ulong ActualFingerprint { get; }

    public bool IsInitialized => _isInitialized;

    public bool Succeeded =>
        _isInitialized &&
        Status == SpanMemoryConformanceStatus.Success &&
        VectorCount ==
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TotalVectorCount &&
        ExpectedFingerprint == ActualFingerprint &&
        ActualFingerprint ==
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExpectedCatalogFingerprint;

    public bool SatisfiesRuntimeCompletionGate4 => false;

    internal static SpanMemoryConformanceResult Success(
        int vectorCount,
        ulong fingerprint)
    {
        return new SpanMemoryConformanceResult(
            SpanMemoryConformanceStatus.Success,
            problemIndex: -1,
            conflictIndex: -1,
            vectorCount,
            fingerprint,
            fingerprint);
    }

    internal static SpanMemoryConformanceResult Failure(
        SpanMemoryConformanceStatus status,
        int problemIndex,
        int conflictIndex,
        int vectorCount,
        ulong expectedFingerprint,
        ulong actualFingerprint)
    {
        return new SpanMemoryConformanceResult(
            status,
            problemIndex,
            conflictIndex,
            vectorCount,
            expectedFingerprint,
            actualFingerprint);
    }
}

public static class SpanOverlapMemoryCastUnsafeWidthConformanceVectors
{
    public const int SliceVectorCount = 4;

    public const int OverlapVectorCount = 5;

    public const int MemoryCastVectorCount = 3;

    public const int UnalignedWidthVectorCount = 4;

    public const int TotalVectorCount =
        SliceVectorCount +
        OverlapVectorCount +
        MemoryCastVectorCount +
        UnalignedWidthVectorCount;

    public const int PointerWidth = 8;

    public const byte ExpectedSuccessFlag = 1;

    public const byte SignExtendFlag = 2;

    public const ulong ExpectedCatalogFingerprint = 0xDE0413BBC2C1453EUL;

    public const bool AddsCanonicalSystemNamespace = false;

    public const bool ReferenceFilesRequired = false;

    public const bool RequiresHostConformance = false;

    public const bool SatisfiesRuntimeCompletionGate4 = false;

    public static bool TryGetVector(
        int index,
        out SpanMemoryConformanceVector vector)
    {
        vector = GetVector(index);
        return vector.IsInitialized;
    }

    public static SpanMemoryConformanceVector GetVector(int index)
    {
        const ulong Seed = 0x0807060504030201UL;

        return index switch
        {
            // Slice: full, middle, empty-at-end, and invalid end overflow.
            0 => Vector(1, SpanMemoryConformanceOperation.Slice, 0, 0, 8,
                1, 1, ExpectedSuccessFlag, Seed, Seed),
            1 => Vector(2, SpanMemoryConformanceOperation.Slice, 2, 0, 4,
                1, 1, ExpectedSuccessFlag, Seed, 0x0000000006050403UL),
            2 => Vector(3, SpanMemoryConformanceOperation.Slice, 8, 0, 0,
                1, 1, ExpectedSuccessFlag, Seed, 0),
            3 => Vector(4, SpanMemoryConformanceOperation.Slice, 7, 0, 2,
                1, 1, 0, Seed, 0),

            // Overlap: destination before source, destination after source,
            // equal ranges, zero length, and non-overlapping ranges.
            4 => Vector(5, SpanMemoryConformanceOperation.Overlap, 2, 0, 4,
                1, 1, ExpectedSuccessFlag, Seed, 0x0807060506050403UL),
            5 => Vector(6, SpanMemoryConformanceOperation.Overlap, 0, 2, 4,
                1, 1, ExpectedSuccessFlag, Seed, 0x0807040302010201UL),
            6 => Vector(7, SpanMemoryConformanceOperation.Overlap, 1, 1, 4,
                1, 1, ExpectedSuccessFlag, Seed, Seed),
            7 => Vector(8, SpanMemoryConformanceOperation.Overlap, 8, 8, 0,
                1, 1, ExpectedSuccessFlag, Seed, Seed),
            8 => Vector(9, SpanMemoryConformanceOperation.Overlap, 0, 4, 4,
                1, 1, ExpectedSuccessFlag, Seed, 0x0403020104030201UL),

            // Memory-cast: divisible/aligned, trailing non-divisible bytes,
            // and an unaligned start. MemoryMarshal.Cast permits both latter
            // cases and truncates only the incomplete trailing element.
            9 => Vector(10, SpanMemoryConformanceOperation.MemoryCast, 0, 0, 8,
                1, 4, ExpectedSuccessFlag, Seed, 2),
            10 => Vector(11, SpanMemoryConformanceOperation.MemoryCast, 0, 0, 6,
                1, 4, ExpectedSuccessFlag, Seed, 1),
            11 => Vector(12, SpanMemoryConformanceOperation.MemoryCast, 1, 0, 4,
                1, 4, ExpectedSuccessFlag, Seed, 1),

            // Unaligned access widths 1/2/4/8. These cover zero extension,
            // sign extension, widening, native-width preservation, and
            // truncation of the native-width source to four bytes.
            12 => Vector(13, SpanMemoryConformanceOperation.UnalignedWidth,
                1, 0, 1, 1, 8, ExpectedSuccessFlag, 0xFF, 0xFF),
            13 => Vector(14, SpanMemoryConformanceOperation.UnalignedWidth,
                1, 0, 2, 2, 8, ExpectedSuccessFlag | SignExtendFlag,
                0xFF80, 0xFFFFFFFFFFFFFF80UL),
            14 => Vector(15, SpanMemoryConformanceOperation.UnalignedWidth,
                1, 0, 4, 4, 8, ExpectedSuccessFlag,
                0x89ABCDEF, 0x0000000089ABCDEFUL),
            15 => Vector(16, SpanMemoryConformanceOperation.UnalignedWidth,
                1, 0, 8, 8, 4, ExpectedSuccessFlag,
                0x1122334455667788UL, 0x0000000055667788UL),
            _ => default
        };
    }

#if !NOSTDLIB_EFI
    public static SpanMemoryConformanceResult CopyBuiltInCatalog(
        System.Span<SpanMemoryConformanceVector> destination)
    {
        if (destination.Length < TotalVectorCount)
        {
            return Failure(
                SpanMemoryConformanceStatus.DestinationTooSmall,
                destination.Length,
                -1,
                0,
                ExpectedCatalogFingerprint,
                0);
        }

        // Validate the immutable source before mutating caller-owned storage.
        System.Span<SpanMemoryConformanceVector> source =
            stackalloc SpanMemoryConformanceVector[TotalVectorCount];
        for (int index = 0; index < TotalVectorCount; index++)
        {
            source[index] = GetVector(index);
        }

        SpanMemoryConformanceResult validation = ValidateCatalog(
            source,
            ExpectedCatalogFingerprint);
        if (!validation.Succeeded)
        {
            return validation;
        }

        for (int index = 0; index < TotalVectorCount; index++)
        {
            destination[index] = source[index];
        }

        return validation;
    }

    public static SpanMemoryConformanceResult ValidateCatalog(
        System.ReadOnlySpan<SpanMemoryConformanceVector> vectors,
        ulong expectedFingerprint)
    {
        if (vectors.Length != TotalVectorCount)
        {
            return Failure(
                SpanMemoryConformanceStatus.VectorCountMismatch,
                vectors.Length,
                -1,
                vectors.Length,
                expectedFingerprint,
                0);
        }

        for (int index = 0; index < vectors.Length; index++)
        {
            SpanMemoryConformanceStatus status = ValidateVector(vectors[index]);
            if (status != SpanMemoryConformanceStatus.Success)
            {
                return Failure(
                    status,
                    index,
                    -1,
                    vectors.Length,
                    expectedFingerprint,
                    0);
            }
        }

        for (int index = 0; index < vectors.Length; index++)
        {
            for (int prior = 0; prior < index; prior++)
            {
                if (vectors[index].Key == vectors[prior].Key)
                {
                    return Failure(
                        SpanMemoryConformanceStatus.DuplicateVectorKey,
                        index,
                        prior,
                        vectors.Length,
                        expectedFingerprint,
                        0);
                }
            }

            if (vectors[index].Key != index + 1)
            {
                return Failure(
                    SpanMemoryConformanceStatus.VectorOrderMismatch,
                    index,
                    -1,
                    vectors.Length,
                    expectedFingerprint,
                    0);
            }
        }

        ulong actualFingerprint = ComputeCatalogFingerprint(vectors);
        if (actualFingerprint != expectedFingerprint ||
            expectedFingerprint != ExpectedCatalogFingerprint)
        {
            return Failure(
                SpanMemoryConformanceStatus.CatalogFingerprintMismatch,
                -1,
                -1,
                vectors.Length,
                expectedFingerprint,
                actualFingerprint);
        }

        return SpanMemoryConformanceResult.Success(
            vectors.Length,
            actualFingerprint);
    }

    public static ulong ComputeCatalogFingerprint(
        System.ReadOnlySpan<SpanMemoryConformanceVector> vectors)
    {
        ulong hash = 14695981039346656037UL;
        for (int index = 0; index < vectors.Length; index++)
        {
            hash = AppendVector(hash, vectors[index]);
        }

        return hash;
    }
#else
    public static unsafe SpanMemoryConformanceResult CopyBuiltInCatalog(
        SpanMemoryConformanceVector* destination,
        int destinationCapacity)
    {
        if (destination == null)
        {
            return Failure(
                SpanMemoryConformanceStatus.VectorPointerNull,
                -1,
                -1,
                0,
                ExpectedCatalogFingerprint,
                0);
        }

        if (destinationCapacity < TotalVectorCount)
        {
            return Failure(
                SpanMemoryConformanceStatus.DestinationTooSmall,
                destinationCapacity,
                -1,
                0,
                ExpectedCatalogFingerprint,
                0);
        }

        SpanMemoryConformanceVector* source =
            stackalloc SpanMemoryConformanceVector[TotalVectorCount];
        for (int index = 0; index < TotalVectorCount; index++)
        {
            source[index] = GetVector(index);
        }

        SpanMemoryConformanceResult validation = ValidateCatalog(
            source,
            TotalVectorCount,
            ExpectedCatalogFingerprint);
        if (!validation.Succeeded)
        {
            return validation;
        }

        for (int index = 0; index < TotalVectorCount; index++)
        {
            destination[index] = source[index];
        }

        return validation;
    }

    public static unsafe SpanMemoryConformanceResult ValidateCatalog(
        SpanMemoryConformanceVector* vectors,
        int vectorCount,
        ulong expectedFingerprint)
    {
        if (vectors == null)
        {
            return Failure(
                SpanMemoryConformanceStatus.VectorPointerNull,
                -1,
                -1,
                vectorCount,
                expectedFingerprint,
                0);
        }

        if (vectorCount != TotalVectorCount)
        {
            return Failure(
                SpanMemoryConformanceStatus.VectorCountMismatch,
                vectorCount,
                -1,
                vectorCount,
                expectedFingerprint,
                0);
        }

        for (int index = 0; index < vectorCount; index++)
        {
            SpanMemoryConformanceStatus status = ValidateVector(vectors[index]);
            if (status != SpanMemoryConformanceStatus.Success)
            {
                return Failure(
                    status,
                    index,
                    -1,
                    vectorCount,
                    expectedFingerprint,
                    0);
            }
        }

        for (int index = 0; index < vectorCount; index++)
        {
            for (int prior = 0; prior < index; prior++)
            {
                if (vectors[index].Key == vectors[prior].Key)
                {
                    return Failure(
                        SpanMemoryConformanceStatus.DuplicateVectorKey,
                        index,
                        prior,
                        vectorCount,
                        expectedFingerprint,
                        0);
                }
            }

            if (vectors[index].Key != index + 1)
            {
                return Failure(
                    SpanMemoryConformanceStatus.VectorOrderMismatch,
                    index,
                    -1,
                    vectorCount,
                    expectedFingerprint,
                    0);
            }
        }

        ulong actualFingerprint = ComputeCatalogFingerprint(vectors, vectorCount);
        if (actualFingerprint != expectedFingerprint ||
            expectedFingerprint != ExpectedCatalogFingerprint)
        {
            return Failure(
                SpanMemoryConformanceStatus.CatalogFingerprintMismatch,
                -1,
                -1,
                vectorCount,
                expectedFingerprint,
                actualFingerprint);
        }

        return SpanMemoryConformanceResult.Success(
            vectorCount,
            actualFingerprint);
    }

    public static unsafe ulong ComputeCatalogFingerprint(
        SpanMemoryConformanceVector* vectors,
        int vectorCount)
    {
        ulong hash = 14695981039346656037UL;
        for (int index = 0; index < vectorCount; index++)
        {
            hash = AppendVector(hash, vectors[index]);
        }

        return hash;
    }
#endif

    private static SpanMemoryConformanceStatus ValidateVector(
        SpanMemoryConformanceVector vector)
    {
        if (!vector.IsInitialized)
        {
            return SpanMemoryConformanceStatus.VectorUninitialized;
        }

        if (vector.Key == 0 || vector.Key > TotalVectorCount)
        {
            return SpanMemoryConformanceStatus.VectorKeyOutOfRange;
        }

        if (vector.Operation < SpanMemoryConformanceOperation.Slice ||
            vector.Operation > SpanMemoryConformanceOperation.UnalignedWidth)
        {
            return SpanMemoryConformanceStatus.OperationOutOfRange;
        }

        if (vector.SourceOffset < 0 ||
            vector.DestinationOffset < 0 ||
            vector.ByteCount < 0 ||
            (vector.Flags & ~(ExpectedSuccessFlag | SignExtendFlag)) != 0)
        {
            return SpanMemoryConformanceStatus.VectorMalformed;
        }

        switch (vector.Operation)
        {
            case SpanMemoryConformanceOperation.Slice:
                if (vector.SourceWidth != 1 ||
                    vector.DestinationWidth != 1 ||
                    vector.SignExtend ||
                    vector.ExpectedSuccess != IsRangeValid(
                        PointerWidth,
                        vector.SourceOffset,
                        vector.ByteCount) ||
                    (vector.ExpectedSuccess &&
                        vector.Expected != ExtractPackedBytes(
                            vector.Input,
                            vector.SourceOffset,
                            vector.ByteCount)))
                {
                    return SpanMemoryConformanceStatus.VectorMalformed;
                }
                break;
            case SpanMemoryConformanceOperation.Overlap:
                if (vector.SourceWidth != 1 ||
                    vector.DestinationWidth != 1 ||
                    vector.SignExtend ||
                    !vector.ExpectedSuccess ||
                    !IsRangeValid(
                        PointerWidth,
                        vector.SourceOffset,
                        vector.ByteCount) ||
                    !IsRangeValid(
                        PointerWidth,
                        vector.DestinationOffset,
                        vector.ByteCount) ||
                    vector.Expected != ApplyPackedOverlap(
                        vector.Input,
                        vector.SourceOffset,
                        vector.DestinationOffset,
                        vector.ByteCount))
                {
                    return SpanMemoryConformanceStatus.VectorMalformed;
                }
                break;
            case SpanMemoryConformanceOperation.MemoryCast:
                if (vector.SourceWidth != 1 ||
                    !IsSupportedWidth(vector.DestinationWidth) ||
                    vector.SignExtend ||
                    !vector.ExpectedSuccess ||
                    !IsRangeValid(16, vector.SourceOffset, vector.ByteCount) ||
                    vector.Expected !=
                        (ulong)(vector.ByteCount / vector.DestinationWidth))
                {
                    return SpanMemoryConformanceStatus.VectorMalformed;
                }
                break;
            case SpanMemoryConformanceOperation.UnalignedWidth:
                if (!IsSupportedWidth(vector.SourceWidth) ||
                    !IsSupportedWidth(vector.DestinationWidth) ||
                    vector.ByteCount != vector.SourceWidth ||
                    !vector.ExpectedSuccess ||
                    vector.SourceOffset == 0 ||
                    vector.Expected != ConvertWidth(
                        vector.Input,
                        vector.SourceWidth,
                        vector.DestinationWidth,
                        vector.SignExtend))
                {
                    return SpanMemoryConformanceStatus.VectorMalformed;
                }
                break;
        }

        return SpanMemoryConformanceStatus.Success;
    }

    private static bool IsSupportedWidth(byte width)
    {
        return width == 1 || width == 2 || width == 4 || width == 8;
    }

    private static bool IsRangeValid(int length, int offset, int count)
    {
        return offset >= 0 &&
            count >= 0 &&
            offset <= length &&
            count <= length - offset;
    }

    private static ulong ExtractPackedBytes(
        ulong value,
        int offset,
        int count)
    {
        if (count == 0)
        {
            return 0;
        }

        ulong shifted = value >> (offset * 8);
        if (count == PointerWidth)
        {
            return shifted;
        }

        ulong mask = (1UL << (count * 8)) - 1;
        return shifted & mask;
    }

    private static ulong ApplyPackedOverlap(
        ulong input,
        int sourceOffset,
        int destinationOffset,
        int count)
    {
        ulong result = input;
        for (int index = 0; index < count; index++)
        {
            int sourceShift = (sourceOffset + index) * 8;
            int destinationShift = (destinationOffset + index) * 8;
            ulong sourceByte = (input >> sourceShift) & 0xFFUL;
            ulong destinationMask = 0xFFUL << destinationShift;
            result = (result & ~destinationMask) |
                (sourceByte << destinationShift);
        }

        return result;
    }

    private static ulong ConvertWidth(
        ulong value,
        byte sourceWidth,
        byte destinationWidth,
        bool signExtend)
    {
        ulong sourceMask = sourceWidth == PointerWidth
            ? ulong.MaxValue
            : (1UL << (sourceWidth * 8)) - 1;
        value &= sourceMask;

        if (signExtend && sourceWidth < PointerWidth)
        {
            ulong signBit = 1UL << ((sourceWidth * 8) - 1);
            if ((value & signBit) != 0)
            {
                value |= ~sourceMask;
            }
        }

        ulong destinationMask = destinationWidth == PointerWidth
            ? ulong.MaxValue
            : (1UL << (destinationWidth * 8)) - 1;
        return value & destinationMask;
    }

    private static SpanMemoryConformanceVector Vector(
        byte key,
        SpanMemoryConformanceOperation operation,
        int sourceOffset,
        int destinationOffset,
        int byteCount,
        byte sourceWidth,
        byte destinationWidth,
        byte flags,
        ulong input,
        ulong expected)
    {
        return new SpanMemoryConformanceVector(
            key,
            operation,
            sourceOffset,
            destinationOffset,
            byteCount,
            sourceWidth,
            destinationWidth,
            flags,
            input,
            expected);
    }

    private static ulong AppendVector(
        ulong hash,
        SpanMemoryConformanceVector vector)
    {
        hash = AppendByte(hash, vector.Key);
        hash = AppendByte(hash, (byte)vector.Operation);
        hash = AppendInt32(hash, vector.SourceOffset);
        hash = AppendInt32(hash, vector.DestinationOffset);
        hash = AppendInt32(hash, vector.ByteCount);
        hash = AppendByte(hash, vector.SourceWidth);
        hash = AppendByte(hash, vector.DestinationWidth);
        hash = AppendByte(hash, vector.Flags);
        hash = AppendUInt64(hash, vector.Input);
        return AppendUInt64(hash, vector.Expected);
    }

    private static ulong AppendInt32(ulong hash, int value)
    {
        return AppendUInt32(hash, unchecked((uint)value));
    }

    private static ulong AppendUInt32(ulong hash, uint value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            hash = AppendByte(hash, (byte)(value >> shift));
        }

        return hash;
    }

    private static ulong AppendUInt64(ulong hash, ulong value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            hash = AppendByte(hash, (byte)(value >> shift));
        }

        return hash;
    }

    private static ulong AppendByte(ulong hash, byte value)
    {
        return unchecked((hash ^ value) * 1099511628211UL);
    }

    private static SpanMemoryConformanceResult Failure(
        SpanMemoryConformanceStatus status,
        int problemIndex,
        int conflictIndex,
        int vectorCount,
        ulong expectedFingerprint,
        ulong actualFingerprint)
    {
        return SpanMemoryConformanceResult.Failure(
            status,
            problemIndex,
            conflictIndex,
            vectorCount,
            expectedFingerprint,
            actualFingerprint);
    }
}
