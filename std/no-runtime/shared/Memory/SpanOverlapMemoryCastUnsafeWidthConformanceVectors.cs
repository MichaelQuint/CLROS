namespace OS.Std.Memory;

public static class SpanOverlapMemoryCastUnsafeWidthConformanceVectors
{
    public const int SliceVectorCount = 4;
    public const int OverlapVectorCount = 5;
    public const int MemoryCastVectorCount = 3;
    public const int UnalignedWidthVectorCount = 4;
    public const int TotalVectorCount = SliceVectorCount + OverlapVectorCount + MemoryCastVectorCount + UnalignedWidthVectorCount;
    public const int PointerWidth = 8;
    public const int RequiredScratchBytes = 16;
    public const int RequiredExecutionRecordCount = TotalVectorCount;
    // MemoryMarshal.Cast permits unaligned input and truncates only the
    // incomplete trailing destination element. Keep both policies explicit so
    // a future no-stdlib implementation cannot silently tighten either rule.
    public const bool MemoryCastAllowsUnalignedStart = true;
    public const bool MemoryCastFloorTruncatesTrailingBytes = true;
    // The width vectors model unsigned Unsafe.ReadUnaligned<T> reads for
    // byte/ushort/uint/ulong. Sign extension, when requested by a vector, is a
    // separate conversion after the unsigned read.
    public const bool UnsafeWidthUsesUnsignedReads = true;
    public const byte ExpectedSuccessFlag = 1;
    public const byte SignExtendFlag = 2;
    public const ulong ExpectedCatalogFingerprint = 0xF03FF59D36F39ECDUL;
    public const bool AddsCanonicalSystemNamespace = false;
    public const bool ReferenceFilesRequired = false;
    public const bool RequiresHostConformance = false;
    public const bool SatisfiesRuntimeCompletionGate4 = false;
    public static bool TryGetVector(int index, out SpanMemoryConformanceVector vector)
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
            0 => Vector(1, SpanMemoryConformanceOperation.Slice, 0, 0, 8, 1, 1, ExpectedSuccessFlag, 8, 8, Seed, Seed),
            1 => Vector(2, SpanMemoryConformanceOperation.Slice, 2, 0, 4, 1, 1, ExpectedSuccessFlag, 4, 4, Seed, 0x0000000006050403UL),
            2 => Vector(3, SpanMemoryConformanceOperation.Slice, 8, 0, 0, 1, 1, ExpectedSuccessFlag, 0, 0, Seed, 0),
            3 => Vector(4, SpanMemoryConformanceOperation.Slice, 7, 0, 2, 1, 1, 0, 0, 0, Seed, 0),
            // Overlap: destination before source, destination after source,
            // equal ranges, zero length, and non-overlapping ranges.
            4 => Vector(5, SpanMemoryConformanceOperation.Overlap, 2, 0, 4, 1, 1, ExpectedSuccessFlag, 8, 8, Seed, 0x0807060506050403UL),
            5 => Vector(6, SpanMemoryConformanceOperation.Overlap, 0, 2, 4, 1, 1, ExpectedSuccessFlag, 8, 8, Seed, 0x0807040302010201UL),
            6 => Vector(7, SpanMemoryConformanceOperation.Overlap, 1, 1, 4, 1, 1, ExpectedSuccessFlag, 8, 8, Seed, Seed),
            7 => Vector(8, SpanMemoryConformanceOperation.Overlap, 8, 8, 0, 1, 1, ExpectedSuccessFlag, 8, 8, Seed, Seed),
            8 => Vector(9, SpanMemoryConformanceOperation.Overlap, 0, 4, 4, 1, 1, ExpectedSuccessFlag, 8, 8, Seed, 0x0403020104030201UL),
            // Memory-cast: divisible byte-to-ushort, one byte-to-uint vector
            // that combines an unaligned start with two truncated trailing
            // bytes, and uint-to-byte expansion. Reinterpretation preserves
            // every byte belonging to a complete destination element.
            9 => Vector(10, SpanMemoryConformanceOperation.MemoryCast, 0, 0, 8, 1, 2, ExpectedSuccessFlag, 8, 4, Seed, Seed),
            10 => Vector(11, SpanMemoryConformanceOperation.MemoryCast, 1, 0, 6, 1, 4, ExpectedSuccessFlag, 4, 1, Seed, 0x0000000004030201UL),
            11 => Vector(12, SpanMemoryConformanceOperation.MemoryCast, 0, 0, 4, 4, 1, ExpectedSuccessFlag, 4, 4, 0x00000000A1B2C3D4UL, 0x00000000A1B2C3D4UL),
            // Unaligned access widths 1/2/4/8. These cover zero extension,
            // sign extension, widening, native-width preservation, and
            // truncation of the native-width source to four bytes.
            12 => Vector(13, SpanMemoryConformanceOperation.UnalignedWidth, 1, 0, 1, 1, 8, ExpectedSuccessFlag, 8, 1, 0xFF, 0xFF),
            13 => Vector(14, SpanMemoryConformanceOperation.UnalignedWidth, 1, 0, 2, 2, 8, ExpectedSuccessFlag | SignExtendFlag, 8, 1, 0xFF80, 0xFFFFFFFFFFFFFF80UL),
            14 => Vector(15, SpanMemoryConformanceOperation.UnalignedWidth, 1, 0, 4, 4, 8, ExpectedSuccessFlag, 8, 1, 0x89ABCDEF, 0x0000000089ABCDEFUL),
            15 => Vector(16, SpanMemoryConformanceOperation.UnalignedWidth, 1, 0, 8, 8, 4, ExpectedSuccessFlag, 4, 1, 0x1122334455667788UL, 0x0000000055667788UL),
            _ => default
        };
    }

#if !NOSTDLIB_EFI
    public static SpanMemoryConformanceResult CopyBuiltInCatalog(System.Span<SpanMemoryConformanceVector> destination)
    {
        if (destination.Length < TotalVectorCount)
        {
            return Failure(SpanMemoryConformanceStatus.DestinationTooSmall, destination.Length, -1, 0, ExpectedCatalogFingerprint, 0);
        }

        // Validate the immutable source before mutating caller-owned storage.
        System.Span<SpanMemoryConformanceVector> source = stackalloc SpanMemoryConformanceVector[TotalVectorCount];
        for (int index = 0; index < TotalVectorCount; index++)
        {
            source[index] = GetVector(index);
        }

        SpanMemoryConformanceResult validation = ValidateCatalog(source, ExpectedCatalogFingerprint);
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

    public static SpanMemoryConformanceResult ValidateCatalog(System.ReadOnlySpan<SpanMemoryConformanceVector> vectors, ulong expectedFingerprint)
    {
        if (vectors.Length != TotalVectorCount)
        {
            return Failure(SpanMemoryConformanceStatus.VectorCountMismatch, vectors.Length, -1, vectors.Length, expectedFingerprint, 0);
        }

        for (int index = 0; index < vectors.Length; index++)
        {
            SpanMemoryConformanceStatus status = ValidateVector(vectors[index]);
            if (status != SpanMemoryConformanceStatus.Success)
            {
                return Failure(status, index, -1, vectors.Length, expectedFingerprint, 0);
            }
        }

        for (int index = 0; index < vectors.Length; index++)
        {
            for (int prior = 0; prior < index; prior++)
            {
                if (vectors[index].Key == vectors[prior].Key)
                {
                    return Failure(SpanMemoryConformanceStatus.DuplicateVectorKey, index, prior, vectors.Length, expectedFingerprint, 0);
                }
            }

            if (vectors[index].Key != index + 1)
            {
                return Failure(SpanMemoryConformanceStatus.VectorOrderMismatch, index, -1, vectors.Length, expectedFingerprint, 0);
            }
        }

        ulong actualFingerprint = ComputeCatalogFingerprint(vectors);
        if (actualFingerprint != expectedFingerprint || expectedFingerprint != ExpectedCatalogFingerprint)
        {
            return Failure(SpanMemoryConformanceStatus.CatalogFingerprintMismatch, -1, -1, vectors.Length, expectedFingerprint, actualFingerprint);
        }

        return SpanMemoryConformanceResult.Success(vectors.Length, actualFingerprint);
    }

    public static ulong ComputeCatalogFingerprint(System.ReadOnlySpan<SpanMemoryConformanceVector> vectors)
    {
        ulong hash = 14695981039346656037UL;
        for (int index = 0; index < vectors.Length; index++)
        {
            hash = AppendVector(hash, vectors[index]);
        }

        return hash;
    }

    public static SpanMemoryExecutionResult ExecuteCatalog(System.ReadOnlySpan<SpanMemoryConformanceVector> vectors, System.Span<byte> scratch, System.Span<SpanMemoryVectorExecutionRecord> output, ulong expectedFingerprint)
    {
        if (scratch.Length < RequiredScratchBytes)
        {
            return SpanMemoryExecutionResult.Failure(SpanMemoryExecutionStatus.ScratchCapacityTooSmall, default, scratch.Length, output.Length, 0, -1);
        }

        if (output.Length < RequiredExecutionRecordCount)
        {
            return SpanMemoryExecutionResult.Failure(SpanMemoryExecutionStatus.OutputCapacityTooSmall, default, scratch.Length, output.Length, 0, -1);
        }

        System.ReadOnlySpan<byte> vectorBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(vectors);
        System.Span<byte> outputBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(output);
        if (System.MemoryExtensions.Overlaps(vectorBytes, (System.ReadOnlySpan<byte>)scratch) || System.MemoryExtensions.Overlaps(vectorBytes, (System.ReadOnlySpan<byte>)outputBytes) || System.MemoryExtensions.Overlaps((System.ReadOnlySpan<byte>)scratch, (System.ReadOnlySpan<byte>)outputBytes))
        {
            return SpanMemoryExecutionResult.Failure(SpanMemoryExecutionStatus.BufferAlias, default, scratch.Length, output.Length, 0, -1);
        }

        SpanMemoryConformanceResult validation = ValidateCatalog(vectors, expectedFingerprint);
        if (!validation.Succeeded)
        {
            return SpanMemoryExecutionResult.Failure(SpanMemoryExecutionStatus.CatalogValidationFailed, validation, scratch.Length, output.Length, 0, validation.ProblemIndex);
        }

        for (int index = 0; index < vectors.Length; index++)
        {
            if (!TryExecuteVector(vectors[index], scratch, out SpanMemoryVectorExecutionRecord record))
            {
                return SpanMemoryExecutionResult.Failure(SpanMemoryExecutionStatus.VectorExecutionMismatch, validation, scratch.Length, output.Length, index, index);
            }

            output[index] = record;
        }

        return SpanMemoryExecutionResult.Success(validation, scratch.Length, output.Length);
    }

    private static bool TryExecuteVector(SpanMemoryConformanceVector vector, System.Span<byte> scratch, out SpanMemoryVectorExecutionRecord record)
    {
        scratch[..RequiredScratchBytes].Clear();
        SpanMemoryVectorExecutionStatus status = SpanMemoryVectorExecutionStatus.Success;
        byte outputByteCount = 0;
        byte resultElementCount = 0;
        ulong rawUnsignedValue = 0;
        ulong outputBytes = 0;
        bool rejectedInputPreserved = false;
        switch (vector.Operation)
        {
            case SpanMemoryConformanceOperation.Slice:
                WritePackedBytes(scratch, 0, PointerWidth, vector.Input);
                if (!vector.ExpectedSuccess)
                {
                    status = SpanMemoryVectorExecutionStatus.ExpectedRejection;
                    rejectedInputPreserved = PackBytes(scratch, 0, PointerWidth) == vector.Input;
                    break;
                }

                outputByteCount = vector.ExpectedByteCount;
                resultElementCount = vector.ExpectedElementCount;
                outputBytes = PackBytes(scratch, vector.SourceOffset, outputByteCount);
                break;
            case SpanMemoryConformanceOperation.Overlap:
                WritePackedBytes(scratch, 0, PointerWidth, vector.Input);
                scratch.Slice(vector.SourceOffset, vector.ByteCount).CopyTo(scratch.Slice(vector.DestinationOffset, vector.ByteCount));
                outputByteCount = vector.ExpectedByteCount;
                resultElementCount = vector.ExpectedElementCount;
                outputBytes = PackBytes(scratch, 0, outputByteCount);
                break;
            case SpanMemoryConformanceOperation.MemoryCast:
                WritePackedBytes(scratch, vector.SourceOffset, vector.ByteCount, vector.Input);
                outputByteCount = vector.ExpectedByteCount;
                resultElementCount = vector.ExpectedElementCount;
                outputBytes = PackBytes(scratch, vector.SourceOffset, outputByteCount);
                break;
            case SpanMemoryConformanceOperation.UnalignedWidth:
                WritePackedBytes(scratch, vector.SourceOffset, vector.SourceWidth, vector.Input);
                rawUnsignedValue = PackBytes(scratch, vector.SourceOffset, vector.SourceWidth);
                outputByteCount = vector.ExpectedByteCount;
                resultElementCount = vector.ExpectedElementCount;
                outputBytes = ConvertWidth(rawUnsignedValue, vector.SourceWidth, vector.DestinationWidth, vector.SignExtend);
                break;
            default:
                record = default;
                return false;
        }

        record = SpanMemoryVectorExecutionRecord.Create(vector, status, outputByteCount, resultElementCount, rawUnsignedValue, outputBytes, PackBytes(scratch, 0, PointerWidth), rejectedInputPreserved);
        return RecordMatchesVector(vector, record);
    }

    private static void WritePackedBytes(System.Span<byte> destination, int offset, int count, ulong value)
    {
        for (int index = 0; index < count; index++)
        {
            destination[offset + index] = (byte)(value >> (index * 8));
        }
    }

    private static ulong PackBytes(System.ReadOnlySpan<byte> source, int offset, int count)
    {
        ulong value = 0;
        for (int index = 0; index < count; index++)
        {
            value |= (ulong)source[offset + index] << (index * 8);
        }

        return value;
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

    private static unsafe ulong ComputeCatalogFingerprint(
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

    public static unsafe SpanMemoryExecutionResult ExecuteCatalog(
        SpanMemoryConformanceVector* vectors,
        int vectorCount,
        byte* scratch,
        int scratchCapacity,
        SpanMemoryVectorExecutionRecord* output,
        int outputCapacity,
        ulong expectedFingerprint)
    {
        if (vectors == null)
        {
            SpanMemoryConformanceResult validation = Failure(
                SpanMemoryConformanceStatus.VectorPointerNull,
                -1,
                -1,
                vectorCount,
                expectedFingerprint,
                0);
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.VectorPointerNull,
                validation,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        if (scratch == null)
        {
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.ScratchPointerNull,
                default,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        if (output == null)
        {
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.OutputPointerNull,
                default,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        if (vectorCount < 0)
        {
            SpanMemoryConformanceResult validation = Failure(
                SpanMemoryConformanceStatus.VectorCountMismatch,
                vectorCount,
                -1,
                vectorCount,
                expectedFingerprint,
                0);
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.VectorCountNegative,
                validation,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        if (scratchCapacity < 0)
        {
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.ScratchCapacityNegative,
                default,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        if (outputCapacity < 0)
        {
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.OutputCapacityNegative,
                default,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        if (scratchCapacity < RequiredScratchBytes)
        {
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.ScratchCapacityTooSmall,
                default,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        if (outputCapacity < RequiredExecutionRecordCount)
        {
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.OutputCapacityTooSmall,
                default,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        if (vectorCount != TotalVectorCount)
        {
            SpanMemoryConformanceResult validation = Failure(
                SpanMemoryConformanceStatus.VectorCountMismatch,
                vectorCount,
                -1,
                vectorCount,
                expectedFingerprint,
                0);
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.CatalogValidationFailed,
                validation,
                scratchCapacity,
                outputCapacity,
                0,
                vectorCount);
        }

        if (!TryGetPointerRange(
                vectors,
                TotalVectorCount,
                sizeof(SpanMemoryConformanceVector),
                out nuint vectorStart,
                out nuint vectorEnd) ||
            !TryGetPointerRange(
                scratch,
                RequiredScratchBytes,
                sizeof(byte),
                out nuint scratchStart,
                out nuint scratchEnd) ||
            !TryGetPointerRange(
                output,
                RequiredExecutionRecordCount,
                sizeof(SpanMemoryVectorExecutionRecord),
                out nuint outputStart,
                out nuint outputEnd))
        {
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.PointerRangeWrap,
                default,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        if (RangesOverlap(vectorStart, vectorEnd, scratchStart, scratchEnd) ||
            RangesOverlap(vectorStart, vectorEnd, outputStart, outputEnd) ||
            RangesOverlap(scratchStart, scratchEnd, outputStart, outputEnd))
        {
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.BufferAlias,
                default,
                scratchCapacity,
                outputCapacity,
                0,
                -1);
        }

        SpanMemoryConformanceResult catalogValidation = ValidateCatalog(
            vectors,
            vectorCount,
            expectedFingerprint);
        if (!catalogValidation.Succeeded)
        {
            return SpanMemoryExecutionResult.Failure(
                SpanMemoryExecutionStatus.CatalogValidationFailed,
                catalogValidation,
                scratchCapacity,
                outputCapacity,
                0,
                catalogValidation.ProblemIndex);
        }

        for (int index = 0; index < vectorCount; index++)
        {
            if (!TryExecuteVector(
                    vectors[index],
                    scratch,
                    out SpanMemoryVectorExecutionRecord record))
            {
                return SpanMemoryExecutionResult.Failure(
                    SpanMemoryExecutionStatus.VectorExecutionMismatch,
                    catalogValidation,
                    scratchCapacity,
                    outputCapacity,
                    index,
                    index);
            }

            output[index] = record;
        }

        return SpanMemoryExecutionResult.Success(
            catalogValidation,
            scratchCapacity,
            outputCapacity);
    }

    private static unsafe bool TryExecuteVector(
        SpanMemoryConformanceVector vector,
        byte* scratch,
        out SpanMemoryVectorExecutionRecord record)
    {
        for (int index = 0; index < RequiredScratchBytes; index++)
        {
            scratch[index] = 0;
        }

        SpanMemoryVectorExecutionStatus status =
            SpanMemoryVectorExecutionStatus.Success;
        byte outputByteCount = 0;
        byte resultElementCount = 0;
        ulong rawUnsignedValue = 0;
        ulong outputBytes = 0;
        bool rejectedInputPreserved = false;

        switch (vector.Operation)
        {
            case SpanMemoryConformanceOperation.Slice:
                WritePackedBytes(scratch, 0, PointerWidth, vector.Input);
                if (!vector.ExpectedSuccess)
                {
                    status = SpanMemoryVectorExecutionStatus.ExpectedRejection;
                    rejectedInputPreserved =
                        PackBytes(scratch, 0, PointerWidth) == vector.Input;
                    break;
                }

                outputByteCount = vector.ExpectedByteCount;
                resultElementCount = vector.ExpectedElementCount;
                outputBytes = PackBytes(
                    scratch,
                    vector.SourceOffset,
                    outputByteCount);
                break;
            case SpanMemoryConformanceOperation.Overlap:
                WritePackedBytes(scratch, 0, PointerWidth, vector.Input);
                CopyOverlap(
                    scratch,
                    vector.SourceOffset,
                    vector.DestinationOffset,
                    vector.ByteCount);
                outputByteCount = vector.ExpectedByteCount;
                resultElementCount = vector.ExpectedElementCount;
                outputBytes = PackBytes(scratch, 0, outputByteCount);
                break;
            case SpanMemoryConformanceOperation.MemoryCast:
                WritePackedBytes(
                    scratch,
                    vector.SourceOffset,
                    vector.ByteCount,
                    vector.Input);
                outputByteCount = vector.ExpectedByteCount;
                resultElementCount = vector.ExpectedElementCount;
                outputBytes = PackBytes(
                    scratch,
                    vector.SourceOffset,
                    outputByteCount);
                break;
            case SpanMemoryConformanceOperation.UnalignedWidth:
                WritePackedBytes(
                    scratch,
                    vector.SourceOffset,
                    vector.SourceWidth,
                    vector.Input);
                rawUnsignedValue = PackBytes(
                    scratch,
                    vector.SourceOffset,
                    vector.SourceWidth);
                outputByteCount = vector.ExpectedByteCount;
                resultElementCount = vector.ExpectedElementCount;
                outputBytes = ConvertWidth(
                    rawUnsignedValue,
                    vector.SourceWidth,
                    vector.DestinationWidth,
                    vector.SignExtend);
                break;
            default:
                record = default;
                return false;
        }

        record = SpanMemoryVectorExecutionRecord.Create(
            vector,
            status,
            outputByteCount,
            resultElementCount,
            rawUnsignedValue,
            outputBytes,
            PackBytes(scratch, 0, PointerWidth),
            rejectedInputPreserved);
        return RecordMatchesVector(vector, record);
    }

    private static unsafe void CopyOverlap(
        byte* buffer,
        int sourceOffset,
        int destinationOffset,
        int count)
    {
        if (destinationOffset > sourceOffset &&
            destinationOffset < sourceOffset + count)
        {
            for (int index = count - 1; index >= 0; index--)
            {
                buffer[destinationOffset + index] = buffer[sourceOffset + index];
            }

            return;
        }

        for (int index = 0; index < count; index++)
        {
            buffer[destinationOffset + index] = buffer[sourceOffset + index];
        }
    }

    private static unsafe void WritePackedBytes(
        byte* destination,
        int offset,
        int count,
        ulong value)
    {
        for (int index = 0; index < count; index++)
        {
            destination[offset + index] = (byte)(value >> (index * 8));
        }
    }

    private static unsafe ulong PackBytes(
        byte* source,
        int offset,
        int count)
    {
        ulong value = 0;
        for (int index = 0; index < count; index++)
        {
            value |= (ulong)source[offset + index] << (index * 8);
        }

        return value;
    }

    private static unsafe bool TryGetPointerRange(
        void* pointer,
        int elementCount,
        int elementSize,
        out nuint start,
        out nuint end)
    {
        start = (nuint)pointer;
        end = 0;
        if (elementCount < 0 || elementSize <= 0)
        {
            return false;
        }

        nuint count = (nuint)(uint)elementCount;
        nuint size = (nuint)(uint)elementSize;
        nuint byteCount = count * size;
        if (count != 0 && byteCount / count != size)
        {
            return false;
        }

        end = start + byteCount;
        return end >= start;
    }

    private static bool RangesOverlap(
        nuint firstStart,
        nuint firstEnd,
        nuint secondStart,
        nuint secondEnd)
    {
        return firstStart < secondEnd && secondStart < firstEnd;
    }
#endif
    private static bool RecordMatchesVector(SpanMemoryConformanceVector vector, SpanMemoryVectorExecutionRecord record)
    {
        SpanMemoryVectorExecutionStatus expectedStatus = vector.Operation == SpanMemoryConformanceOperation.Slice && !vector.ExpectedSuccess ? SpanMemoryVectorExecutionStatus.ExpectedRejection : SpanMemoryVectorExecutionStatus.Success;
        ulong expectedRawUnsignedValue = vector.Operation == SpanMemoryConformanceOperation.UnalignedWidth ? ConvertWidth(vector.Input, vector.SourceWidth, PointerWidth, signExtend: false) : 0;
        bool expectedRejectedInputPreserved = expectedStatus == SpanMemoryVectorExecutionStatus.ExpectedRejection;
        return record.Succeeded && record.Key == vector.Key && record.Operation == vector.Operation && record.Status == expectedStatus && record.OutputByteCount == vector.ExpectedByteCount && record.ResultElementCount == vector.ExpectedElementCount && record.RawUnsignedValue == expectedRawUnsignedValue && record.OutputBytes == vector.Expected && record.RejectedInputPreserved == expectedRejectedInputPreserved;
    }

    private static SpanMemoryConformanceStatus ValidateVector(SpanMemoryConformanceVector vector)
    {
        if (!vector.IsInitialized)
        {
            return SpanMemoryConformanceStatus.VectorUninitialized;
        }

        if (vector.Key == 0 || vector.Key > TotalVectorCount)
        {
            return SpanMemoryConformanceStatus.VectorKeyOutOfRange;
        }

        if (vector.Operation < SpanMemoryConformanceOperation.Slice || vector.Operation > SpanMemoryConformanceOperation.UnalignedWidth)
        {
            return SpanMemoryConformanceStatus.OperationOutOfRange;
        }

        if (vector.SourceOffset < 0 || vector.DestinationOffset < 0 || vector.ByteCount < 0 || (vector.Flags & ~(ExpectedSuccessFlag | SignExtendFlag)) != 0)
        {
            return SpanMemoryConformanceStatus.VectorMalformed;
        }

        switch (vector.Operation)
        {
            case SpanMemoryConformanceOperation.Slice:
                if (vector.SourceWidth != 1 || vector.DestinationWidth != 1 || vector.DestinationOffset != 0 || vector.SignExtend || vector.ExpectedSuccess != IsRangeValid(PointerWidth, vector.SourceOffset, vector.ByteCount) || (vector.ExpectedSuccess && vector.Expected != ExtractPackedBytes(vector.Input, vector.SourceOffset, vector.ByteCount)) || vector.ExpectedByteCount != (vector.ExpectedSuccess ? vector.ByteCount : 0) || vector.ExpectedElementCount != vector.ExpectedByteCount)
                {
                    return SpanMemoryConformanceStatus.VectorMalformed;
                }

                break;
            case SpanMemoryConformanceOperation.Overlap:
                if (vector.SourceWidth != 1 || vector.DestinationWidth != 1 || vector.SignExtend || !vector.ExpectedSuccess || vector.ExpectedByteCount != PointerWidth || vector.ExpectedElementCount != PointerWidth || !IsRangeValid(PointerWidth, vector.SourceOffset, vector.ByteCount) || !IsRangeValid(PointerWidth, vector.DestinationOffset, vector.ByteCount) || vector.Expected != ApplyPackedOverlap(vector.Input, vector.SourceOffset, vector.DestinationOffset, vector.ByteCount))
                {
                    return SpanMemoryConformanceStatus.VectorMalformed;
                }

                break;
            case SpanMemoryConformanceOperation.MemoryCast:
                if (!IsSupportedWidth(vector.SourceWidth) || !IsSupportedWidth(vector.DestinationWidth) || vector.DestinationOffset != 0 || vector.SignExtend || !vector.ExpectedSuccess || vector.ByteCount > PointerWidth || vector.ByteCount % vector.SourceWidth != 0 || !IsRangeValid(16, vector.SourceOffset, vector.ByteCount) || vector.ExpectedElementCount != vector.ByteCount / vector.DestinationWidth || vector.ExpectedByteCount != vector.ExpectedElementCount * vector.DestinationWidth || vector.Expected != ExtractPackedBytes(vector.Input, 0, vector.ExpectedByteCount))
                {
                    return SpanMemoryConformanceStatus.VectorMalformed;
                }

                break;
            case SpanMemoryConformanceOperation.UnalignedWidth:
                if (!IsSupportedWidth(vector.SourceWidth) || !IsSupportedWidth(vector.DestinationWidth) || vector.SourceOffset != 1 || vector.DestinationOffset != 0 || vector.ByteCount != vector.SourceWidth || !vector.ExpectedSuccess || vector.ExpectedByteCount != vector.DestinationWidth || vector.ExpectedElementCount != 1 || !IsRangeValid(16, vector.SourceOffset, vector.ByteCount) || (vector.SignExtend && vector.DestinationWidth <= vector.SourceWidth) || vector.Expected != ConvertWidth(vector.Input, vector.SourceWidth, vector.DestinationWidth, vector.SignExtend))
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
        return offset >= 0 && count >= 0 && offset <= length && count <= length - offset;
    }

    private static ulong ExtractPackedBytes(ulong value, int offset, int count)
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

    private static ulong ApplyPackedOverlap(ulong input, int sourceOffset, int destinationOffset, int count)
    {
        ulong result = input;
        for (int index = 0; index < count; index++)
        {
            int sourceShift = (sourceOffset + index) * 8;
            int destinationShift = (destinationOffset + index) * 8;
            ulong sourceByte = (input >> sourceShift) & 0xFFUL;
            ulong destinationMask = 0xFFUL << destinationShift;
            result = (result & ~destinationMask) | (sourceByte << destinationShift);
        }

        return result;
    }

    private static ulong ConvertWidth(ulong value, byte sourceWidth, byte destinationWidth, bool signExtend)
    {
        ulong sourceMask = sourceWidth == PointerWidth ? ~0UL : (1UL << (sourceWidth * 8)) - 1;
        value &= sourceMask;
        if (signExtend && sourceWidth < PointerWidth)
        {
            ulong signBit = 1UL << ((sourceWidth * 8) - 1);
            if ((value & signBit) != 0)
            {
                value |= ~sourceMask;
            }
        }

        ulong destinationMask = destinationWidth == PointerWidth ? ~0UL : (1UL << (destinationWidth * 8)) - 1;
        return value & destinationMask;
    }

    private static SpanMemoryConformanceVector Vector(byte key, SpanMemoryConformanceOperation operation, int sourceOffset, int destinationOffset, int byteCount, byte sourceWidth, byte destinationWidth, byte flags, byte expectedByteCount, byte expectedElementCount, ulong input, ulong expected)
    {
        return new SpanMemoryConformanceVector(key, operation, sourceOffset, destinationOffset, byteCount, sourceWidth, destinationWidth, flags, expectedByteCount, expectedElementCount, input, expected);
    }

    private static ulong AppendVector(ulong hash, SpanMemoryConformanceVector vector)
    {
        hash = AppendByte(hash, vector.Key);
        hash = AppendByte(hash, (byte)vector.Operation);
        hash = AppendInt32(hash, vector.SourceOffset);
        hash = AppendInt32(hash, vector.DestinationOffset);
        hash = AppendInt32(hash, vector.ByteCount);
        hash = AppendByte(hash, vector.SourceWidth);
        hash = AppendByte(hash, vector.DestinationWidth);
        hash = AppendByte(hash, vector.Flags);
        hash = AppendByte(hash, vector.ExpectedByteCount);
        hash = AppendByte(hash, vector.ExpectedElementCount);
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

    private static SpanMemoryConformanceResult Failure(SpanMemoryConformanceStatus status, int problemIndex, int conflictIndex, int vectorCount, ulong expectedFingerprint, ulong actualFingerprint)
    {
        return SpanMemoryConformanceResult.Failure(status, problemIndex, conflictIndex, vectorCount, expectedFingerprint, actualFingerprint);
    }
}
