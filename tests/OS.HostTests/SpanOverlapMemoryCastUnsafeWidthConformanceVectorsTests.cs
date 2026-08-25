using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Std.Memory;

internal static class SpanOverlapMemoryCastUnsafeWidthConformanceVectorsTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Console.WriteLine("TEST_START m04.003");

        try
        {
            TestCatalogShapeAndRetrieval();
            TestBuiltInCopyAndValidation();
            TestSliceVectorsAgainstDotNet10();
            TestOverlapVectorsAgainstDotNet10();
            TestMemoryCastVectorsAgainstDotNet10();
            TestUnalignedWidthVectorsAgainstDotNet10();
            TestShortDestinationIsAtomic();
            TestMalformedVectorsFailClosed();
            TestDuplicateAndOrderFailures();
            TestFingerprintFailure();
            TestDefaultValuesFailClosed();
            TestAllocationFreeValidation();
            TestSourceBoundary();

            Console.WriteLine(
                "CLROS_M04_003_READY vectors="
                + SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .TotalVectorCount
                + " slice=4 overlap=5 memoryCast=3 unalignedWidths=4"
                + " pointerWidth="
                + SpanOverlapMemoryCastUnsafeWidthConformanceVectors.PointerWidth
                + " fingerprint="
                + SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint.ToString("X16")
                + " referenceFilesRequired=false hostConformance=true"
                + " guestIntegrated=false gate4=Fail");
            Console.WriteLine("TEST_PASS m04.003");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "TEST_FAIL m04.003 " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void TestCatalogShapeAndRetrieval()
    {
        ExpectEqual("slice count", 4,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.SliceVectorCount);
        ExpectEqual("overlap count", 5,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.OverlapVectorCount);
        ExpectEqual("cast count", 3,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.MemoryCastVectorCount);
        ExpectEqual("width count", 4,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .UnalignedWidthVectorCount);
        ExpectEqual("total count", 16,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TotalVectorCount);
        ExpectEqual("pointer width", 8,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.PointerWidth);
        ExpectEqual("host pointer width", 8, IntPtr.Size);

        if (SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .AddsCanonicalSystemNamespace ||
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ReferenceFilesRequired ||
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiresHostConformance ||
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException(
                "Catalog evidence-boundary constants are not false.");
        }

        if (RuntimeHelpers.IsReferenceOrContainsReferences<
                SpanMemoryConformanceVector>() ||
            RuntimeHelpers.IsReferenceOrContainsReferences<
                SpanMemoryConformanceResult>())
        {
            throw new InvalidOperationException("Catalog vector/result are not POD.");
        }

        for (int index = 0; index < 16; index++)
        {
            if (!SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TryGetVector(
                    index,
                    out SpanMemoryConformanceVector vector))
            {
                throw new InvalidOperationException(
                    "Catalog rejected index " + index + ".");
            }

            ExpectEqual("ordered key", index + 1, vector.Key);
        }

        if (SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TryGetVector(
                -1,
                out _) ||
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TryGetVector(
                16,
                out _))
        {
            throw new InvalidOperationException("Out-of-range retrieval succeeded.");
        }
    }

    private static void TestBuiltInCopyAndValidation()
    {
        SpanMemoryConformanceVector[] vectors = CreateCatalog();
        SpanMemoryConformanceResult result =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .CopyBuiltInCatalog(vectors);
        ExpectSuccess("built-in copy", result);
        ExpectEqual(
            "frozen fingerprint",
            0xDE0413BBC2C1453EUL,
            result.ActualFingerprint);

        SpanMemoryConformanceResult validation =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ValidateCatalog(
                vectors,
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint);
        ExpectSuccess("copied catalog", validation);
    }

    private static void TestSliceVectorsAgainstDotNet10()
    {
        for (int index = 0; index < 4; index++)
        {
            SpanMemoryConformanceVector vector = Get(index);
            byte[] bytes = GetInputBytes(vector.Input);
            bool valid = IsRangeValid(
                bytes.Length,
                vector.SourceOffset,
                vector.ByteCount);
            ExpectEqual("slice validity", vector.ExpectedSuccess, valid);

            if (!valid)
            {
                continue;
            }

            ReadOnlySpan<byte> slice = bytes.AsSpan(
                vector.SourceOffset,
                vector.ByteCount);
            ExpectEqual("slice packed value", vector.Expected, Pack(slice));
        }
    }

    private static void TestOverlapVectorsAgainstDotNet10()
    {
        for (int index = 4; index < 9; index++)
        {
            SpanMemoryConformanceVector vector = Get(index);
            byte[] bytes = GetInputBytes(vector.Input);
            ReadOnlySpan<byte> source = bytes.AsSpan(
                vector.SourceOffset,
                vector.ByteCount);
            Span<byte> destination = bytes.AsSpan(
                vector.DestinationOffset,
                vector.ByteCount);
            source.CopyTo(destination);
            ExpectEqual("overlap packed result", vector.Expected, Pack(bytes));
        }
    }

    private static void TestMemoryCastVectorsAgainstDotNet10()
    {
        for (int index = 9; index < 12; index++)
        {
            SpanMemoryConformanceVector vector = Get(index);
            byte[] storage = new byte[16];
            GetInputBytes(vector.Input).CopyTo(storage, vector.SourceOffset);
            ReadOnlySpan<byte> bytes = storage.AsSpan(
                vector.SourceOffset,
                vector.ByteCount);
            ReadOnlySpan<uint> cast = MemoryMarshal.Cast<byte, uint>(bytes);
            ExpectEqual("memory cast element count", vector.Expected, (ulong)cast.Length);
        }
    }

    private static void TestUnalignedWidthVectorsAgainstDotNet10()
    {
        Span<byte> storage = stackalloc byte[16];
        for (int index = 12; index < 16; index++)
        {
            SpanMemoryConformanceVector vector = Get(index);
            storage.Clear();
            WriteWidth(storage.Slice(vector.SourceOffset), vector.SourceWidth, vector.Input);
            ulong read = ReadWidth(
                storage.Slice(vector.SourceOffset),
                vector.SourceWidth);
            ulong converted = ConvertWidth(
                read,
                vector.SourceWidth,
                vector.DestinationWidth,
                vector.SignExtend);
            ExpectEqual("unaligned converted value", vector.Expected, converted);
        }
    }

    private static void TestShortDestinationIsAtomic()
    {
        SpanMemoryConformanceVector sentinel = new(
            99,
            SpanMemoryConformanceOperation.Slice,
            0,
            0,
            0,
            1,
            1,
            1,
            99,
            99);
        SpanMemoryConformanceVector[] destination = new SpanMemoryConformanceVector[15];
        Array.Fill(destination, sentinel);

        SpanMemoryConformanceResult result =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .CopyBuiltInCatalog(destination);
        ExpectStatus(
            "short destination",
            SpanMemoryConformanceStatus.DestinationTooSmall,
            result);
        for (int index = 0; index < destination.Length; index++)
        {
            ExpectEqual("atomic short destination", 99, destination[index].Key);
        }
    }

    private static void TestMalformedVectorsFailClosed()
    {
        SpanMemoryConformanceVector[] vectors = CreateCopiedCatalog();
        vectors[2] = default;
        ExpectStatus(
            "uninitialized",
            SpanMemoryConformanceStatus.VectorUninitialized,
            Validate(vectors));

        vectors = CreateCopiedCatalog();
        SpanMemoryConformanceVector original = vectors[3];
        vectors[3] = Clone(original, key: 0);
        ExpectStatus(
            "key range",
            SpanMemoryConformanceStatus.VectorKeyOutOfRange,
            Validate(vectors));

        vectors = CreateCopiedCatalog();
        original = vectors[3];
        vectors[3] = Clone(
            original,
            operation: (SpanMemoryConformanceOperation)99);
        ExpectStatus(
            "operation range",
            SpanMemoryConformanceStatus.OperationOutOfRange,
            Validate(vectors));

        vectors = CreateCopiedCatalog();
        original = vectors[3];
        vectors[3] = Clone(original, sourceOffset: -1);
        ExpectStatus(
            "negative offset",
            SpanMemoryConformanceStatus.VectorMalformed,
            Validate(vectors));

        SpanMemoryConformanceVector[] shortCatalog = new SpanMemoryConformanceVector[15];
        ExpectStatus(
            "count mismatch",
            SpanMemoryConformanceStatus.VectorCountMismatch,
            Validate(shortCatalog));
    }

    private static void TestDuplicateAndOrderFailures()
    {
        SpanMemoryConformanceVector[] vectors = CreateCopiedCatalog();
        vectors[1] = Clone(vectors[1], key: vectors[0].Key);
        ExpectStatus(
            "duplicate key",
            SpanMemoryConformanceStatus.DuplicateVectorKey,
            Validate(vectors));

        vectors = CreateCopiedCatalog();
        (vectors[0], vectors[1]) = (vectors[1], vectors[0]);
        ExpectStatus(
            "order mismatch",
            SpanMemoryConformanceStatus.VectorOrderMismatch,
            Validate(vectors));
    }

    private static void TestFingerprintFailure()
    {
        SpanMemoryConformanceVector[] vectors = CreateCopiedCatalog();
        SpanMemoryConformanceVector original = vectors[0];
        vectors[0] = Clone(
            original,
            input: original.Input ^ 1UL,
            expected: original.Expected ^ 1UL);
        ExpectStatus(
            "fingerprint mismatch",
            SpanMemoryConformanceStatus.CatalogFingerprintMismatch,
            Validate(vectors));

        SpanMemoryConformanceResult staleExpected =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ValidateCatalog(
                CreateCopiedCatalog(),
                0);
        ExpectStatus(
            "stale expected fingerprint",
            SpanMemoryConformanceStatus.CatalogFingerprintMismatch,
            staleExpected);
    }

    private static void TestDefaultValuesFailClosed()
    {
        SpanMemoryConformanceResult result = default;
        if (result.IsInitialized || result.Succeeded ||
            result.SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException("Default result succeeded.");
        }

        if (default(SpanMemoryConformanceVector).IsInitialized)
        {
            throw new InvalidOperationException("Default vector initialized.");
        }
    }

    private static void TestAllocationFreeValidation()
    {
        SpanMemoryConformanceVector[] vectors = CreateCopiedCatalog();
        _ = Validate(vectors);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 128; iteration++)
        {
            SpanMemoryConformanceResult result = Validate(vectors);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Warmed validation failed.");
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        ExpectEqual("validation allocation", before, after);
    }

    private static void TestSourceBoundary()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "std",
            "no-runtime",
            "shared",
            "SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs"));

        RequireContains(source, "namespace OS.Std.Memory;");
        RequireContains(source, "#if !NOSTDLIB_EFI");
        RequireContains(source, "SpanMemoryConformanceVector* destination");
        RequireContains(source, "ExpectedCatalogFingerprint = 0xDE0413BBC2C1453EUL");
        RequireContains(source, "public const int PointerWidth = 8;");
        RequireContains(source, "public const bool AddsCanonicalSystemNamespace = false;");
        RequireContains(source, "public const bool ReferenceFilesRequired = false;");
        RequireContains(source, "public const bool RequiresHostConformance = false;");
        RequireContains(source, "public const bool SatisfiesRuntimeCompletionGate4 = false;");
        RequireContains(source, "public bool SatisfiesRuntimeCompletionGate4 => false;");

        if (source.Contains("namespace System", StringComparison.Ordinal) ||
            source.Contains("using System", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production catalog introduced a System namespace/import.");
        }
    }

    private static SpanMemoryConformanceVector[] CreateCatalog()
    {
        return new SpanMemoryConformanceVector[
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TotalVectorCount];
    }

    private static SpanMemoryConformanceVector[] CreateCopiedCatalog()
    {
        SpanMemoryConformanceVector[] vectors = CreateCatalog();
        ExpectSuccess(
            "catalog fixture",
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .CopyBuiltInCatalog(vectors));
        return vectors;
    }

    private static SpanMemoryConformanceVector Get(int index)
    {
        if (!SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TryGetVector(
                index,
                out SpanMemoryConformanceVector vector))
        {
            throw new InvalidOperationException("Missing vector " + index + ".");
        }

        return vector;
    }

    private static SpanMemoryConformanceResult Validate(
        SpanMemoryConformanceVector[] vectors)
    {
        return SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ValidateCatalog(
            vectors,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExpectedCatalogFingerprint);
    }

    private static SpanMemoryConformanceVector Clone(
        SpanMemoryConformanceVector vector,
        byte? key = null,
        SpanMemoryConformanceOperation? operation = null,
        int? sourceOffset = null,
        ulong? input = null,
        ulong? expected = null)
    {
        return new SpanMemoryConformanceVector(
            key ?? vector.Key,
            operation ?? vector.Operation,
            sourceOffset ?? vector.SourceOffset,
            vector.DestinationOffset,
            vector.ByteCount,
            vector.SourceWidth,
            vector.DestinationWidth,
            vector.Flags,
            input ?? vector.Input,
            expected ?? vector.Expected);
    }

    private static byte[] GetInputBytes(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static ulong Pack(ReadOnlySpan<byte> bytes)
    {
        ulong value = 0;
        int count = Math.Min(bytes.Length, 8);
        for (int index = 0; index < count; index++)
        {
            value |= (ulong)bytes[index] << (index * 8);
        }

        return value;
    }

    private static bool IsRangeValid(int length, int offset, int count)
    {
        return offset >= 0 &&
            count >= 0 &&
            offset <= length &&
            count <= length - offset;
    }

    private static void WriteWidth(Span<byte> destination, byte width, ulong value)
    {
        switch (width)
        {
            case 1:
                destination[0] = (byte)value;
                return;
            case 2:
                BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)value);
                return;
            case 4:
                BinaryPrimitives.WriteUInt32LittleEndian(destination, (uint)value);
                return;
            case 8:
                BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
                return;
            default:
                throw new InvalidOperationException("Unsupported write width.");
        }
    }

    private static ulong ReadWidth(ReadOnlySpan<byte> source, byte width)
    {
        return width switch
        {
            1 => source[0],
            2 => BinaryPrimitives.ReadUInt16LittleEndian(source),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(source),
            8 => BinaryPrimitives.ReadUInt64LittleEndian(source),
            _ => throw new InvalidOperationException("Unsupported read width.")
        };
    }

    private static ulong ConvertWidth(
        ulong value,
        byte sourceWidth,
        byte destinationWidth,
        bool signExtend)
    {
        ulong sourceMask = sourceWidth == 8
            ? ulong.MaxValue
            : (1UL << (sourceWidth * 8)) - 1;
        value &= sourceMask;

        if (signExtend && sourceWidth < 8)
        {
            ulong signBit = 1UL << ((sourceWidth * 8) - 1);
            if ((value & signBit) != 0)
            {
                value |= ~sourceMask;
            }
        }

        ulong destinationMask = destinationWidth == 8
            ? ulong.MaxValue
            : (1UL << (destinationWidth * 8)) - 1;
        return value & destinationMask;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("CLROS.slnx root not found.");
    }

    private static void RequireContains(string source, string expected)
    {
        if (!source.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Source boundary missing: " + expected);
        }
    }

    private static void ExpectSuccess(
        string name,
        SpanMemoryConformanceResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                name + " failed with " + result.Status + ".");
        }
    }

    private static void ExpectStatus(
        string name,
        SpanMemoryConformanceStatus expected,
        SpanMemoryConformanceResult result)
    {
        if (!result.IsInitialized || result.Status != expected || result.Succeeded)
        {
            throw new InvalidOperationException(
                name + " expected " + expected + " but got " + result.Status + ".");
        }
    }

    private static void ExpectEqual<T>(string name, T expected, T actual)
        where T : IEquatable<T>
    {
        if (!actual.Equals(expected))
        {
            throw new InvalidOperationException(
                name + " expected " + expected + " but got " + actual + ".");
        }
    }
}
