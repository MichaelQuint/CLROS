namespace OS.Std.Binary;
public static class IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
{
    public const int ByteSeriesCount = 5;
    public const int UInt16SeriesCount = 7;
    public const int ReadVectorCount = ByteSeriesCount * ByteSeriesCount;
    public const int ReverseVectorCount = UInt16SeriesCount;
    public const int TotalVectorCount = ReadVectorCount + ReverseVectorCount;
    public const ulong ExpectedCatalogFingerprint = 0xE81E4451E8D999F6UL;
    public const bool AddsCanonicalSystemNamespace = false;
    public const bool SatisfiesRuntimeCompletionGate4 = false;
    public const bool ReferenceFilesRequired = false;
    public const bool RequiresHostConformance = false;
    public static bool TryGetByteSeriesValue(int index, out byte value)
    {
        value = index switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            3 => 254,
            4 => 255,
            _ => 0
        };
        return index >= 0 && index < ByteSeriesCount;
    }

    public static bool TryGetUInt16SeriesValue(int index, out ushort value)
    {
        value = index switch
        {
            0 => 0,
            1 => 1,
            2 => 2,
            3 => 254,
            4 => 255,
            5 => 65534,
            6 => 65535,
            _ => 0
        };
        return index >= 0 && index < UInt16SeriesCount;
    }

    public static bool TryGetReadVector(int index, out IndependentEndianReadVector vector)
    {
        vector = GetReadVector(index);
        return vector.IsInitialized;
    }

    public static IndependentEndianReadVector GetReadVector(int index)
    {
        return index switch
        {
            0 => ReadVector(1, 0x00, 0x00, 0x0000, 0x0000),
            1 => ReadVector(2, 0x00, 0x01, 0x0100, 0x0001),
            2 => ReadVector(3, 0x00, 0x02, 0x0200, 0x0002),
            3 => ReadVector(4, 0x00, 0xFE, 0xFE00, 0x00FE),
            4 => ReadVector(5, 0x00, 0xFF, 0xFF00, 0x00FF),
            5 => ReadVector(6, 0x01, 0x00, 0x0001, 0x0100),
            6 => ReadVector(7, 0x01, 0x01, 0x0101, 0x0101),
            7 => ReadVector(8, 0x01, 0x02, 0x0201, 0x0102),
            8 => ReadVector(9, 0x01, 0xFE, 0xFE01, 0x01FE),
            9 => ReadVector(10, 0x01, 0xFF, 0xFF01, 0x01FF),
            10 => ReadVector(11, 0x02, 0x00, 0x0002, 0x0200),
            11 => ReadVector(12, 0x02, 0x01, 0x0102, 0x0201),
            12 => ReadVector(13, 0x02, 0x02, 0x0202, 0x0202),
            13 => ReadVector(14, 0x02, 0xFE, 0xFE02, 0x02FE),
            14 => ReadVector(15, 0x02, 0xFF, 0xFF02, 0x02FF),
            15 => ReadVector(16, 0xFE, 0x00, 0x00FE, 0xFE00),
            16 => ReadVector(17, 0xFE, 0x01, 0x01FE, 0xFE01),
            17 => ReadVector(18, 0xFE, 0x02, 0x02FE, 0xFE02),
            18 => ReadVector(19, 0xFE, 0xFE, 0xFEFE, 0xFEFE),
            19 => ReadVector(20, 0xFE, 0xFF, 0xFFFE, 0xFEFF),
            20 => ReadVector(21, 0xFF, 0x00, 0x00FF, 0xFF00),
            21 => ReadVector(22, 0xFF, 0x01, 0x01FF, 0xFF01),
            22 => ReadVector(23, 0xFF, 0x02, 0x02FF, 0xFF02),
            23 => ReadVector(24, 0xFF, 0xFE, 0xFEFF, 0xFFFE),
            24 => ReadVector(25, 0xFF, 0xFF, 0xFFFF, 0xFFFF),
            _ => default
        };
    }

    public static bool TryGetReverseVector(int index, out IndependentEndianReverseVector vector)
    {
        vector = GetReverseVector(index);
        return vector.IsInitialized;
    }

    public static IndependentEndianReverseVector GetReverseVector(int index)
    {
        return index switch
        {
            0 => ReverseVector(1, 0x0000, 0x0000),
            1 => ReverseVector(2, 0x0001, 0x0100),
            2 => ReverseVector(3, 0x0002, 0x0200),
            3 => ReverseVector(4, 0x00FE, 0xFE00),
            4 => ReverseVector(5, 0x00FF, 0xFF00),
            5 => ReverseVector(6, 0xFFFE, 0xFEFF),
            6 => ReverseVector(7, 0xFFFF, 0xFFFF),
            _ => default
        };
    }

#if !NOSTDLIB_EFI
    public static IndependentEndianConformanceResult CopyBuiltInCatalog(Span<IndependentEndianReadVector> readDestination, Span<IndependentEndianReverseVector> reverseDestination)
    {
        if (readDestination.Length < ReadVectorCount)
        {
            return IndependentEndianConformanceResult.Failure(IndependentEndianConformanceStatus.ReadDestinationTooSmall, problemIndex: readDestination.Length, conflictIndex: -1, readVectorCount: 0, reverseVectorCount: 0, ExpectedCatalogFingerprint, actualFingerprint: 0);
        }

        if (reverseDestination.Length < ReverseVectorCount)
        {
            return IndependentEndianConformanceResult.Failure(IndependentEndianConformanceStatus.ReverseDestinationTooSmall, problemIndex: reverseDestination.Length, conflictIndex: -1, readVectorCount: 0, reverseVectorCount: 0, ExpectedCatalogFingerprint, actualFingerprint: 0);
        }

        for (int index = 0; index < ReadVectorCount; index++)
        {
            readDestination[index] = GetReadVector(index);
        }

        for (int index = 0; index < ReverseVectorCount; index++)
        {
            reverseDestination[index] = GetReverseVector(index);
        }

        return ValidateCatalog(readDestination[..ReadVectorCount], reverseDestination[..ReverseVectorCount], ExpectedCatalogFingerprint);
    }

    public static IndependentEndianConformanceResult ValidateCatalog(ReadOnlySpan<IndependentEndianReadVector> readVectors, ReadOnlySpan<IndependentEndianReverseVector> reverseVectors, ulong expectedFingerprint)
    {
        if (readVectors.Length != ReadVectorCount)
        {
            return Failure(IndependentEndianConformanceStatus.ReadVectorCountMismatch, readVectors.Length, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
        }

        if (reverseVectors.Length != ReverseVectorCount)
        {
            return Failure(IndependentEndianConformanceStatus.ReverseVectorCountMismatch, reverseVectors.Length, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
        }

        for (int index = 0; index < readVectors.Length; index++)
        {
            IndependentEndianReadVector vector = readVectors[index];
            if (!vector.IsInitialized)
            {
                return Failure(IndependentEndianConformanceStatus.ReadVectorUninitialized, index, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
            }

            if (vector.Key == 0 || vector.Key > ReadVectorCount)
            {
                return Failure(IndependentEndianConformanceStatus.ReadVectorKeyOutOfRange, index, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
            }

            ushort expectedLittle = (ushort)(vector.FirstByte | (vector.SecondByte << 8));
            ushort expectedBig = (ushort)((vector.FirstByte << 8) | vector.SecondByte);
            if (vector.ExpectedLittleEndian != expectedLittle || vector.ExpectedBigEndian != expectedBig)
            {
                return Failure(IndependentEndianConformanceStatus.ReadVectorExpectedValueMismatch, index, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
            }
        }

        for (int index = 0; index < reverseVectors.Length; index++)
        {
            IndependentEndianReverseVector vector = reverseVectors[index];
            if (!vector.IsInitialized)
            {
                return Failure(IndependentEndianConformanceStatus.ReverseVectorUninitialized, index, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
            }

            if (vector.Key == 0 || vector.Key > ReverseVectorCount)
            {
                return Failure(IndependentEndianConformanceStatus.ReverseVectorKeyOutOfRange, index, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
            }

            ushort expected = ReverseUInt16(vector.Input);
            if (vector.Expected != expected)
            {
                return Failure(IndependentEndianConformanceStatus.ReverseVectorExpectedValueMismatch, index, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
            }
        }

        for (int index = 0; index < readVectors.Length; index++)
        {
            for (int prior = 0; prior < index; prior++)
            {
                if (readVectors[index].Key == readVectors[prior].Key)
                {
                    return Failure(IndependentEndianConformanceStatus.DuplicateReadVectorKey, index, prior, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
                }
            }
        }

        for (int index = 0; index < reverseVectors.Length; index++)
        {
            for (int prior = 0; prior < index; prior++)
            {
                if (reverseVectors[index].Key == reverseVectors[prior].Key)
                {
                    return Failure(IndependentEndianConformanceStatus.DuplicateReverseVectorKey, index, prior, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
                }
            }
        }

        for (int index = 0; index < readVectors.Length; index++)
        {
            if (readVectors[index].Key != index + 1)
            {
                return Failure(IndependentEndianConformanceStatus.ReadVectorOrderMismatch, index, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
            }
        }

        for (int index = 0; index < reverseVectors.Length; index++)
        {
            if (reverseVectors[index].Key != index + 1)
            {
                return Failure(IndependentEndianConformanceStatus.ReverseVectorOrderMismatch, index, -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint: 0);
            }
        }

        ulong actualFingerprint = ComputeCatalogFingerprint(readVectors, reverseVectors);
        if (actualFingerprint != expectedFingerprint || expectedFingerprint != ExpectedCatalogFingerprint)
        {
            return Failure(IndependentEndianConformanceStatus.CatalogFingerprintMismatch, problemIndex: -1, conflictIndex: -1, readVectors.Length, reverseVectors.Length, expectedFingerprint, actualFingerprint);
        }

        return IndependentEndianConformanceResult.Success(readVectors.Length, reverseVectors.Length, actualFingerprint);
    }

    public static ulong ComputeCatalogFingerprint(ReadOnlySpan<IndependentEndianReadVector> readVectors, ReadOnlySpan<IndependentEndianReverseVector> reverseVectors)
    {
        const ulong FnvOffset = 14695981039346656037UL;
        ulong hash = FnvOffset;
        for (int index = 0; index < readVectors.Length; index++)
        {
            IndependentEndianReadVector vector = readVectors[index];
            hash = AppendFingerprintByte(hash, vector.Key);
            hash = AppendFingerprintByte(hash, vector.FirstByte);
            hash = AppendFingerprintByte(hash, vector.SecondByte);
            hash = AppendFingerprintUInt16(hash, vector.ExpectedLittleEndian);
            hash = AppendFingerprintUInt16(hash, vector.ExpectedBigEndian);
        }

        for (int index = 0; index < reverseVectors.Length; index++)
        {
            IndependentEndianReverseVector vector = reverseVectors[index];
            hash = AppendFingerprintByte(hash, vector.Key);
            hash = AppendFingerprintUInt16(hash, vector.Input);
            hash = AppendFingerprintUInt16(hash, vector.Expected);
        }

        return hash;
    }

#else
    public static unsafe IndependentEndianConformanceResult CopyBuiltInCatalog(
        IndependentEndianReadVector* readDestination,
        int readDestinationCapacity,
        IndependentEndianReverseVector* reverseDestination,
        int reverseDestinationCapacity)
    {
        if (readDestination == null)
        {
            return Failure(
                IndependentEndianConformanceStatus.ReadVectorPointerNull,
                problemIndex: -1,
                conflictIndex: -1,
                readVectorCount: 0,
                reverseVectorCount: 0,
                ExpectedCatalogFingerprint,
                actualFingerprint: 0);
        }

        if (readDestinationCapacity < ReadVectorCount)
        {
            return Failure(
                IndependentEndianConformanceStatus.ReadDestinationTooSmall,
                problemIndex: readDestinationCapacity,
                conflictIndex: -1,
                readVectorCount: 0,
                reverseVectorCount: 0,
                ExpectedCatalogFingerprint,
                actualFingerprint: 0);
        }

        if (reverseDestination == null)
        {
            return Failure(
                IndependentEndianConformanceStatus.ReverseVectorPointerNull,
                problemIndex: -1,
                conflictIndex: -1,
                readVectorCount: 0,
                reverseVectorCount: 0,
                ExpectedCatalogFingerprint,
                actualFingerprint: 0);
        }

        if (reverseDestinationCapacity < ReverseVectorCount)
        {
            return Failure(
                IndependentEndianConformanceStatus
                    .ReverseDestinationTooSmall,
                problemIndex: reverseDestinationCapacity,
                conflictIndex: -1,
                readVectorCount: 0,
                reverseVectorCount: 0,
                ExpectedCatalogFingerprint,
                actualFingerprint: 0);
        }

        for (int index = 0; index < ReadVectorCount; index++)
        {
            readDestination[index] = GetReadVector(index);
        }

        for (int index = 0; index < ReverseVectorCount; index++)
        {
            reverseDestination[index] = GetReverseVector(index);
        }

        return ValidateCatalog(
            readDestination,
            ReadVectorCount,
            reverseDestination,
            ReverseVectorCount,
            ExpectedCatalogFingerprint);
    }

    public static unsafe IndependentEndianConformanceResult ValidateCatalog(
        IndependentEndianReadVector* readVectors,
        int readVectorCount,
        IndependentEndianReverseVector* reverseVectors,
        int reverseVectorCount,
        ulong expectedFingerprint)
    {
        if (readVectors == null)
        {
            return Failure(
                IndependentEndianConformanceStatus.ReadVectorPointerNull,
                problemIndex: -1,
                conflictIndex: -1,
                readVectorCount,
                reverseVectorCount,
                expectedFingerprint,
                actualFingerprint: 0);
        }

        if (reverseVectors == null)
        {
            return Failure(
                IndependentEndianConformanceStatus.ReverseVectorPointerNull,
                problemIndex: -1,
                conflictIndex: -1,
                readVectorCount,
                reverseVectorCount,
                expectedFingerprint,
                actualFingerprint: 0);
        }

        if (readVectorCount != ReadVectorCount)
        {
            return Failure(
                IndependentEndianConformanceStatus.ReadVectorCountMismatch,
                readVectorCount,
                -1,
                readVectorCount,
                reverseVectorCount,
                expectedFingerprint,
                actualFingerprint: 0);
        }

        if (reverseVectorCount != ReverseVectorCount)
        {
            return Failure(
                IndependentEndianConformanceStatus.ReverseVectorCountMismatch,
                reverseVectorCount,
                -1,
                readVectorCount,
                reverseVectorCount,
                expectedFingerprint,
                actualFingerprint: 0);
        }

        for (int index = 0; index < readVectorCount; index++)
        {
            IndependentEndianReadVector vector = readVectors[index];
            if (!vector.IsInitialized)
            {
                return Failure(
                    IndependentEndianConformanceStatus.ReadVectorUninitialized,
                    index,
                    -1,
                    readVectorCount,
                    reverseVectorCount,
                    expectedFingerprint,
                    actualFingerprint: 0);
            }

            if (vector.Key == 0 || vector.Key > ReadVectorCount)
            {
                return Failure(
                    IndependentEndianConformanceStatus.ReadVectorKeyOutOfRange,
                    index,
                    -1,
                    readVectorCount,
                    reverseVectorCount,
                    expectedFingerprint,
                    actualFingerprint: 0);
            }

            ushort expectedLittle = (ushort)(
                vector.FirstByte | (vector.SecondByte << 8));
            ushort expectedBig = (ushort)(
                (vector.FirstByte << 8) | vector.SecondByte);
            if (vector.ExpectedLittleEndian != expectedLittle ||
                vector.ExpectedBigEndian != expectedBig)
            {
                return Failure(
                    IndependentEndianConformanceStatus
                        .ReadVectorExpectedValueMismatch,
                    index,
                    -1,
                    readVectorCount,
                    reverseVectorCount,
                    expectedFingerprint,
                    actualFingerprint: 0);
            }
        }

        for (int index = 0; index < reverseVectorCount; index++)
        {
            IndependentEndianReverseVector vector = reverseVectors[index];
            if (!vector.IsInitialized)
            {
                return Failure(
                    IndependentEndianConformanceStatus
                        .ReverseVectorUninitialized,
                    index,
                    -1,
                    readVectorCount,
                    reverseVectorCount,
                    expectedFingerprint,
                    actualFingerprint: 0);
            }

            if (vector.Key == 0 || vector.Key > ReverseVectorCount)
            {
                return Failure(
                    IndependentEndianConformanceStatus
                        .ReverseVectorKeyOutOfRange,
                    index,
                    -1,
                    readVectorCount,
                    reverseVectorCount,
                    expectedFingerprint,
                    actualFingerprint: 0);
            }

            if (vector.Expected != ReverseUInt16(vector.Input))
            {
                return Failure(
                    IndependentEndianConformanceStatus
                        .ReverseVectorExpectedValueMismatch,
                    index,
                    -1,
                    readVectorCount,
                    reverseVectorCount,
                    expectedFingerprint,
                    actualFingerprint: 0);
            }
        }

        for (int index = 0; index < readVectorCount; index++)
        {
            for (int prior = 0; prior < index; prior++)
            {
                if (readVectors[index].Key == readVectors[prior].Key)
                {
                    return Failure(
                        IndependentEndianConformanceStatus
                            .DuplicateReadVectorKey,
                        index,
                        prior,
                        readVectorCount,
                        reverseVectorCount,
                        expectedFingerprint,
                        actualFingerprint: 0);
                }
            }

            if (readVectors[index].Key != index + 1)
            {
                return Failure(
                    IndependentEndianConformanceStatus.ReadVectorOrderMismatch,
                    index,
                    -1,
                    readVectorCount,
                    reverseVectorCount,
                    expectedFingerprint,
                    actualFingerprint: 0);
            }
        }

        for (int index = 0; index < reverseVectorCount; index++)
        {
            for (int prior = 0; prior < index; prior++)
            {
                if (reverseVectors[index].Key == reverseVectors[prior].Key)
                {
                    return Failure(
                        IndependentEndianConformanceStatus
                            .DuplicateReverseVectorKey,
                        index,
                        prior,
                        readVectorCount,
                        reverseVectorCount,
                        expectedFingerprint,
                        actualFingerprint: 0);
                }
            }

            if (reverseVectors[index].Key != index + 1)
            {
                return Failure(
                    IndependentEndianConformanceStatus
                        .ReverseVectorOrderMismatch,
                    index,
                    -1,
                    readVectorCount,
                    reverseVectorCount,
                    expectedFingerprint,
                    actualFingerprint: 0);
            }
        }

        ulong actualFingerprint = ComputeCatalogFingerprint(
            readVectors,
            readVectorCount,
            reverseVectors,
            reverseVectorCount);
        if (actualFingerprint != expectedFingerprint ||
            expectedFingerprint != ExpectedCatalogFingerprint)
        {
            return Failure(
                IndependentEndianConformanceStatus.CatalogFingerprintMismatch,
                problemIndex: -1,
                conflictIndex: -1,
                readVectorCount,
                reverseVectorCount,
                expectedFingerprint,
                actualFingerprint);
        }

        return IndependentEndianConformanceResult.Success(
            readVectorCount,
            reverseVectorCount,
            actualFingerprint);
    }

    public static unsafe ulong ComputeCatalogFingerprint(
        IndependentEndianReadVector* readVectors,
        int readVectorCount,
        IndependentEndianReverseVector* reverseVectors,
        int reverseVectorCount)
    {
        const ulong FnvOffset = 14695981039346656037UL;
        ulong hash = FnvOffset;

        for (int index = 0; index < readVectorCount; index++)
        {
            IndependentEndianReadVector vector = readVectors[index];
            hash = AppendFingerprintByte(hash, vector.Key);
            hash = AppendFingerprintByte(hash, vector.FirstByte);
            hash = AppendFingerprintByte(hash, vector.SecondByte);
            hash = AppendFingerprintUInt16(hash, vector.ExpectedLittleEndian);
            hash = AppendFingerprintUInt16(hash, vector.ExpectedBigEndian);
        }

        for (int index = 0; index < reverseVectorCount; index++)
        {
            IndependentEndianReverseVector vector = reverseVectors[index];
            hash = AppendFingerprintByte(hash, vector.Key);
            hash = AppendFingerprintUInt16(hash, vector.Input);
            hash = AppendFingerprintUInt16(hash, vector.Expected);
        }

        return hash;
    }
#endif
#if NOSTDLIB_EFI && M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE && !M01_OWNED_PAGING_PROBE
    // The opt-in EFI profile uses bounded stackalloc workspaces without the
    // M01 paging adapter that otherwise owns the no-CRT GS-cookie symbol.
    // RuntimeExport keeps these deterministic code bytes linker-visible as
    // temporary storage; randomized initialization belongs to hardening work.
    [System.Runtime.RuntimeExport("__security_cookie")]
    private static ulong M04SecurityCookieStorageAnchor()
    {
        return 0x434c_524f_5347_5334UL;
    }
#endif
    private static IndependentEndianReadVector ReadVector(byte key, byte firstByte, byte secondByte, ushort expectedLittleEndian, ushort expectedBigEndian)
    {
        return new IndependentEndianReadVector(key, firstByte, secondByte, expectedLittleEndian, expectedBigEndian);
    }

    private static IndependentEndianReverseVector ReverseVector(byte key, ushort input, ushort expected)
    {
        return new IndependentEndianReverseVector(key, input, expected);
    }

    private static ushort ReverseUInt16(ushort value)
    {
        return (ushort)((value >> 8) | (value << 8));
    }

    private static ulong AppendFingerprintUInt16(ulong hash, ushort value)
    {
        hash = AppendFingerprintByte(hash, (byte)value);
        return AppendFingerprintByte(hash, (byte)(value >> 8));
    }

    private static ulong AppendFingerprintByte(ulong hash, byte value)
    {
        const ulong FnvPrime = 1099511628211UL;
        return (hash ^ value) * FnvPrime;
    }

    private static IndependentEndianConformanceResult Failure(IndependentEndianConformanceStatus status, int problemIndex, int conflictIndex, int readVectorCount, int reverseVectorCount, ulong expectedFingerprint, ulong actualFingerprint)
    {
        return IndependentEndianConformanceResult.Failure(status, problemIndex, conflictIndex, readVectorCount, reverseVectorCount, expectedFingerprint, actualFingerprint);
    }
}
