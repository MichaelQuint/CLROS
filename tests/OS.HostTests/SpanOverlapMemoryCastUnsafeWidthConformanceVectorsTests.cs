using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
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
            TestExplicitCompatibilityPolicies();
            TestBuiltInCopyAndValidation();
            TestSliceVectorsAgainstDotNet10();
            TestOverlapVectorsAgainstDotNet10();
            TestMemoryCastVectorsAgainstDotNet10();
            TestUnalignedWidthVectorsAgainstDotNet10();
            TestExecuteCatalogResultsAndCanaries();
            TestExecuteCatalogFailuresAreAtomic();
            TestExecuteCatalogRejectsAliases();
            TestShortDestinationIsAtomic();
            TestMalformedVectorsFailClosed();
            TestOperationSpecificHostileVectorsFailClosed();
            TestDuplicateAndOrderFailures();
            TestFingerprintFailure();
            TestDefaultValuesFailClosed();
            TestAllocationFreeValidation();
            TestUnsafeWidthSourceMutationRejected();
            TestSourceBoundary();
            TestNoStdLibPointerBranchCompiles();

            string readinessMarker =
                "CLROS_M04_003_READY vectors="
                + SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .TotalVectorCount
                + " spanVectors=4 overlapVectors=5 memoryCastVectors=3"
                + " unsafeWidthVectors=4"
                + " fingerprint="
                + SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint.ToString("X16")
                + " pointerWidth="
                + SpanOverlapMemoryCastUnsafeWidthConformanceVectors.PointerWidth
                + " referenceFilesRequired=false hostConformance=true"
                + " guestIntegrated=false gate4=Fail";
            const string ExpectedReadinessMarker =
                "CLROS_M04_003_READY vectors=16 spanVectors=4"
                + " overlapVectors=5 memoryCastVectors=3 unsafeWidthVectors=4"
                + " fingerprint=F03FF59D36F39ECD pointerWidth=8"
                + " referenceFilesRequired=false hostConformance=true"
                + " guestIntegrated=false gate4=Fail";
            ExpectEqual(
                "exact host readiness marker",
                ExpectedReadinessMarker,
                readinessMarker);
            Console.WriteLine(readinessMarker);
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
        ExpectEqual("scratch bytes", 16,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredScratchBytes);
        ExpectEqual("execution records", 16,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredExecutionRecordCount);
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
                SpanMemoryConformanceResult>() ||
            RuntimeHelpers.IsReferenceOrContainsReferences<
                SpanMemoryVectorExecutionRecord>() ||
            RuntimeHelpers.IsReferenceOrContainsReferences<
                SpanMemoryExecutionResult>())
        {
            throw new InvalidOperationException("Catalog vector/result are not POD.");
        }

        int sliceCount = 0;
        int overlapCount = 0;
        int memoryCastCount = 0;
        int unalignedWidthCount = 0;
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
            switch (vector.Operation)
            {
                case SpanMemoryConformanceOperation.Slice:
                    sliceCount++;
                    break;
                case SpanMemoryConformanceOperation.Overlap:
                    overlapCount++;
                    break;
                case SpanMemoryConformanceOperation.MemoryCast:
                    memoryCastCount++;
                    break;
                case SpanMemoryConformanceOperation.UnalignedWidth:
                    unalignedWidthCount++;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Catalog returned an unknown operation.");
            }
        }

        ExpectEqual("observed slice count", 4, sliceCount);
        ExpectEqual("observed overlap count", 5, overlapCount);
        ExpectEqual("observed cast count", 3, memoryCastCount);
        ExpectEqual("observed width count", 4, unalignedWidthCount);

        if (SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TryGetVector(
                -1,
                out SpanMemoryConformanceVector below) ||
            below.IsInitialized ||
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TryGetVector(
                16,
                out SpanMemoryConformanceVector above) ||
            above.IsInitialized)
        {
            throw new InvalidOperationException("Out-of-range retrieval succeeded.");
        }
    }

    private static void TestExplicitCompatibilityPolicies()
    {
        if (!SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .MemoryCastAllowsUnalignedStart ||
            !SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .MemoryCastFloorTruncatesTrailingBytes ||
            !SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .UnsafeWidthUsesUnsignedReads)
        {
            throw new InvalidOperationException(
                "Memory-cast or unsafe-width compatibility policy changed.");
        }

        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException(
                "The frozen unsafe-width vectors require a little-endian host.");
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
            0xF03FF59D36F39ECDUL,
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
                try
                {
                    _ = bytes.AsSpan(vector.SourceOffset, vector.ByteCount);
                    throw new InvalidOperationException(
                        "Invalid .NET slice unexpectedly succeeded.");
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Expected .NET 10 rejection for the frozen invalid vector.
                }

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
            WritePackedInput(
                storage.AsSpan(vector.SourceOffset),
                vector.ByteCount,
                vector.Input);
            byte[] castBytes = ExecuteDotNetMemoryCast(
                vector,
                storage,
                out int resultElementCount);
            ExpectEqual(
                "memory cast result element count",
                (int)vector.ExpectedElementCount,
                resultElementCount);
            ExpectEqual(
                "memory cast output byte count",
                (int)vector.ExpectedByteCount,
                castBytes.Length);
            ExpectEqual(
                "memory cast exact output bytes",
                vector.Expected,
                Pack(castBytes));
            for (int byteIndex = 0; byteIndex < castBytes.Length; byteIndex++)
            {
                ExpectEqual(
                    "memory cast output byte",
                    (byte)(vector.Expected >> (byteIndex * 8)),
                    castBytes[byteIndex]);
            }

            int ignoredTrailingBytes = vector.ByteCount - castBytes.Length;
            ExpectEqual(
                "memory cast trailing byte count",
                vector.ByteCount % vector.DestinationWidth,
                ignoredTrailingBytes);
        }

        SpanMemoryConformanceVector divisible = Get(9);
        ExpectEqual("divisible cast source width", 1, divisible.SourceWidth);
        ExpectEqual("divisible cast destination width", 2,
            divisible.DestinationWidth);
        ExpectEqual("divisible cast source bytes", 8, divisible.ByteCount);

        SpanMemoryConformanceVector unalignedRemainder = Get(10);
        ExpectEqual("unaligned cast source offset", 1,
            unalignedRemainder.SourceOffset);
        ExpectEqual("remainder cast source bytes", 6,
            unalignedRemainder.ByteCount);
        ExpectEqual("remainder cast destination width", 4,
            unalignedRemainder.DestinationWidth);

        SpanMemoryConformanceVector expansion = Get(11);
        ExpectEqual("expansion cast source width", 4, expansion.SourceWidth);
        ExpectEqual("expansion cast destination width", 1,
            expansion.DestinationWidth);
        ExpectEqual("expansion cast element count", 4,
            expansion.ExpectedElementCount);
    }

    private static void TestUnalignedWidthVectorsAgainstDotNet10()
    {
        Span<byte> storage = stackalloc byte[16];
        for (int index = 12; index < 16; index++)
        {
            SpanMemoryConformanceVector vector = Get(index);
            storage.Clear();
            WriteUnalignedWidth(
                storage.Slice(vector.SourceOffset),
                vector.SourceWidth,
                vector.Input);
            ulong read = ReadWidth(
                storage.Slice(vector.SourceOffset),
                vector.SourceWidth);
            ulong expectedRaw = ConvertWidth(
                vector.Input,
                vector.SourceWidth,
                destinationWidth: 8,
                signExtend: false);
            ExpectEqual(
                "unaligned raw unsigned read",
                expectedRaw,
                read);
            ulong converted = ConvertWidth(
                read,
                vector.SourceWidth,
                vector.DestinationWidth,
                vector.SignExtend);
            ExpectEqual("unaligned converted value", vector.Expected, converted);
        }
    }

    private static void TestExecuteCatalogResultsAndCanaries()
    {
        SpanMemoryConformanceVector[] catalogWithTail =
            new SpanMemoryConformanceVector[17];
        ExpectSuccess(
            "execution catalog fixture",
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .CopyBuiltInCatalog(catalogWithTail));
        SpanMemoryConformanceVector catalogTail = new(
            99,
            SpanMemoryConformanceOperation.Slice,
            0,
            0,
            0,
            1,
            1,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExpectedSuccessFlag,
            0,
            0,
            99,
            0);
        catalogWithTail[16] = catalogTail;

        byte[] warmScratch = new byte[
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredScratchBytes];
        SpanMemoryVectorExecutionRecord[] warmOutput =
            new SpanMemoryVectorExecutionRecord[16];
        SpanMemoryExecutionResult warm =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExecuteCatalog(
                catalogWithTail.AsSpan(0, 16),
                warmScratch,
                warmOutput,
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint);
        ExpectExecutionSuccess("warm execution", warm);
        SpanMemoryVectorExecutionRecord outputTail = warmOutput[0];

        byte[] scratch = new byte[
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredScratchBytes + 3];
        Array.Fill(scratch, (byte)0xA5);
        SpanMemoryVectorExecutionRecord[] output =
            new SpanMemoryVectorExecutionRecord[18];
        Array.Fill(output, outputTail);

        SpanMemoryExecutionResult result =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExecuteCatalog(
                catalogWithTail.AsSpan(0, 16),
                scratch.AsSpan(
                    1,
                    SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                        .RequiredScratchBytes + 1),
                output,
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint);
        ExpectExecutionSuccess("catalog execution", result);
        ExpectEqual("executed vector count", 16, result.ExecutedVectorCount);
        ExpectEqual("execution problem index", -1, result.ProblemIndex);

        for (int index = 0; index < 16; index++)
        {
            SpanMemoryConformanceVector vector = catalogWithTail[index];
            SpanMemoryVectorExecutionRecord record = output[index];
            if (!record.Succeeded)
            {
                throw new InvalidOperationException(
                    "Execution record failed at index " + index + ".");
            }

            ExpectEqual("execution key", vector.Key, record.Key);
            ExpectEqual("execution operation", vector.Operation, record.Operation);
            ExpectEqual(
                "execution output byte count",
                vector.ExpectedByteCount,
                record.OutputByteCount);
            ExpectEqual(
                "execution element count",
                vector.ExpectedElementCount,
                record.ResultElementCount);
            ExpectEqual(
                "execution exact output bytes",
                vector.Expected,
                record.OutputBytes);

            ulong expectedRaw =
                vector.Operation == SpanMemoryConformanceOperation.UnalignedWidth
                ? ConvertWidth(
                    vector.Input,
                    vector.SourceWidth,
                    destinationWidth: 8,
                    signExtend: false)
                : 0;
            ExpectEqual(
                "execution raw unsigned width",
                expectedRaw,
                record.RawUnsignedValue);

            bool expectedRejection =
                vector.Operation == SpanMemoryConformanceOperation.Slice &&
                !vector.ExpectedSuccess;
            ExpectEqual(
                "execution record status",
                expectedRejection
                    ? SpanMemoryVectorExecutionStatus.ExpectedRejection
                    : SpanMemoryVectorExecutionStatus.Success,
                record.Status);
            ExpectEqual(
                "execution rejected input preservation",
                expectedRejection,
                record.RejectedInputPreserved);
            if (expectedRejection)
            {
                ExpectEqual(
                    "rejected slice scratch preservation",
                    vector.Input,
                    record.ScratchAfter);
            }
        }

        ExpectEqual("scratch leading canary", (byte)0xA5, scratch[0]);
        ExpectEqual(
            "scratch in-span tail",
            (byte)0xA5,
            scratch[SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredScratchBytes + 1]);
        ExpectEqual("scratch trailing canary", (byte)0xA5, scratch[^1]);
        ExpectRecordEqual("output first tail", outputTail, output[16]);
        ExpectRecordEqual("output second tail", outputTail, output[17]);
        ExpectEqual("catalog tail key", 99, catalogWithTail[16].Key);
    }

    private static void TestExecuteCatalogFailuresAreAtomic()
    {
        SpanMemoryConformanceVector[] vectors = CreateCopiedCatalog();
        SpanMemoryVectorExecutionRecord sentinel = CreateExecutionSentinel(vectors);

        AssertExecutionFailureAtomic(
            "stale execution fingerprint",
            vectors,
            SpanMemoryExecutionStatus.CatalogValidationFailed,
            SpanMemoryConformanceStatus.CatalogFingerprintMismatch,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExpectedCatalogFingerprint ^ 1UL,
            scratchLength: 16,
            outputLength: 16,
            sentinel);

        SpanMemoryConformanceVector[] malformed = CreateCopiedCatalog();
        malformed[9] = Clone(
            malformed[9],
            expectedByteCount: (byte)(malformed[9].ExpectedByteCount - 1));
        AssertExecutionFailureAtomic(
            "malformed execution catalog",
            malformed,
            SpanMemoryExecutionStatus.CatalogValidationFailed,
            SpanMemoryConformanceStatus.VectorMalformed,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExpectedCatalogFingerprint,
            scratchLength: 16,
            outputLength: 16,
            sentinel);

        AssertExecutionFailureAtomic(
            "short execution scratch",
            vectors,
            SpanMemoryExecutionStatus.ScratchCapacityTooSmall,
            expectedCatalogStatus: null,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExpectedCatalogFingerprint,
            scratchLength: 15,
            outputLength: 16,
            sentinel);
        AssertExecutionFailureAtomic(
            "short execution output",
            vectors,
            SpanMemoryExecutionStatus.OutputCapacityTooSmall,
            expectedCatalogStatus: null,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExpectedCatalogFingerprint,
            scratchLength: 16,
            outputLength: 15,
            sentinel);

        SpanMemoryConformanceVector[] oversized = new SpanMemoryConformanceVector[17];
        Array.Copy(vectors, oversized, vectors.Length);
        AssertExecutionFailureAtomic(
            "execution vector count mismatch",
            oversized,
            SpanMemoryExecutionStatus.CatalogValidationFailed,
            SpanMemoryConformanceStatus.VectorCountMismatch,
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExpectedCatalogFingerprint,
            scratchLength: 16,
            outputLength: 16,
            sentinel);
    }

    private static void TestExecuteCatalogRejectsAliases()
    {
        SpanMemoryConformanceVector[] vectors = CreateCopiedCatalog();
        SpanMemoryVectorExecutionRecord[] output =
            new SpanMemoryVectorExecutionRecord[16];
        Span<byte> vectorBytes = MemoryMarshal.AsBytes(vectors.AsSpan());
        SpanMemoryExecutionResult vectorScratchAlias =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExecuteCatalog(
                vectors,
                vectorBytes[..16],
                output,
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint);
        ExpectExecutionStatus(
            "vector scratch alias",
            SpanMemoryExecutionStatus.BufferAlias,
            vectorScratchAlias);
        ExpectEqual("vector scratch alias preserved", 1, vectors[0].Key);

        int recordBytes = Unsafe.SizeOf<SpanMemoryVectorExecutionRecord>() * 16;
        byte[] outputStorage = new byte[recordBytes];
        Span<SpanMemoryVectorExecutionRecord> aliasedOutput =
            MemoryMarshal.Cast<byte, SpanMemoryVectorExecutionRecord>(
                outputStorage.AsSpan());
        SpanMemoryExecutionResult scratchOutputAlias =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExecuteCatalog(
                vectors,
                outputStorage.AsSpan(0, 16),
                aliasedOutput,
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint);
        ExpectExecutionStatus(
            "scratch output alias",
            SpanMemoryExecutionStatus.BufferAlias,
            scratchOutputAlias);

        byte[] vectorOutputStorage = new byte[recordBytes];
        Span<SpanMemoryConformanceVector> aliasedVectors =
            MemoryMarshal.Cast<byte, SpanMemoryConformanceVector>(
                vectorOutputStorage.AsSpan())[..16];
        ExpectSuccess(
            "vector output alias fixture",
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .CopyBuiltInCatalog(aliasedVectors));
        Span<SpanMemoryVectorExecutionRecord> vectorAliasedOutput =
            MemoryMarshal.Cast<byte, SpanMemoryVectorExecutionRecord>(
                vectorOutputStorage.AsSpan())[..16];
        SpanMemoryExecutionResult vectorOutputAlias =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExecuteCatalog(
                aliasedVectors,
                new byte[16],
                vectorAliasedOutput,
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint);
        ExpectExecutionStatus(
            "vector output alias",
            SpanMemoryExecutionStatus.BufferAlias,
            vectorOutputAlias);
        ExpectEqual("vector output alias preserved", 1, aliasedVectors[0].Key);
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

        vectors = CreateCopiedCatalog();
        original = vectors[3];
        vectors[3] = Clone(original, destinationOffset: -1);
        ExpectStatus(
            "negative destination offset",
            SpanMemoryConformanceStatus.VectorMalformed,
            Validate(vectors));

        vectors = CreateCopiedCatalog();
        original = vectors[3];
        vectors[3] = Clone(original, byteCount: -1);
        ExpectStatus(
            "negative byte count",
            SpanMemoryConformanceStatus.VectorMalformed,
            Validate(vectors));

        vectors = CreateCopiedCatalog();
        original = vectors[3];
        vectors[3] = Clone(original, flags: 0x80);
        ExpectStatus(
            "unknown flags",
            SpanMemoryConformanceStatus.VectorMalformed,
            Validate(vectors));

        vectors = CreateCopiedCatalog();
        original = vectors[3];
        vectors[3] = Clone(original, key: 17);
        ExpectStatus(
            "key above range",
            SpanMemoryConformanceStatus.VectorKeyOutOfRange,
            Validate(vectors));

        vectors = CreateCopiedCatalog();
        original = vectors[3];
        vectors[3] = Clone(
            original,
            operation: SpanMemoryConformanceOperation.Uninitialized);
        ExpectStatus(
            "uninitialized operation",
            SpanMemoryConformanceStatus.OperationOutOfRange,
            Validate(vectors));

        SpanMemoryConformanceVector[] shortCatalog = new SpanMemoryConformanceVector[15];
        ExpectStatus(
            "count mismatch",
            SpanMemoryConformanceStatus.VectorCountMismatch,
            Validate(shortCatalog));

        SpanMemoryConformanceVector[] longCatalog =
            new SpanMemoryConformanceVector[17];
        ExpectStatus(
            "oversized count mismatch",
            SpanMemoryConformanceStatus.VectorCountMismatch,
            Validate(longCatalog));
    }

    private static void TestOperationSpecificHostileVectorsFailClosed()
    {
        SpanMemoryConformanceVector slice = Get(1);
        ExpectMalformed("slice destination offset", 1,
            Clone(slice, destinationOffset: 1));
        ExpectMalformed("slice source width", 1,
            Clone(slice, sourceWidth: 2));
        ExpectMalformed("slice destination width", 1,
            Clone(slice, destinationWidth: 2));
        ExpectMalformed("slice sign extension", 1,
            Clone(slice, flags: 3));
        ExpectMalformed("slice false success", 1,
            Clone(slice, flags: 0));
        ExpectMalformed("slice invalid successful range", 1,
            Clone(slice, sourceOffset: 7, byteCount: 4));
        ExpectMalformed("slice expected value", 1,
            Clone(slice, expected: slice.Expected ^ 1UL));
        ExpectMalformed("slice expected byte count", 1,
            Clone(slice, expectedByteCount: 3));
        ExpectMalformed("slice expected element count", 1,
            Clone(slice, expectedElementCount: 3));

        SpanMemoryConformanceVector rejectedSlice = Get(3);
        ExpectMalformed("slice valid rejected range", 3,
            Clone(rejectedSlice, sourceOffset: 6));

        SpanMemoryConformanceVector overlap = Get(4);
        ExpectMalformed("overlap source width", 4,
            Clone(overlap, sourceWidth: 2));
        ExpectMalformed("overlap destination width", 4,
            Clone(overlap, destinationWidth: 2));
        ExpectMalformed("overlap sign extension", 4,
            Clone(overlap, flags: 3));
        ExpectMalformed("overlap false success", 4,
            Clone(overlap, flags: 0));
        ExpectMalformed("overlap source range", 4,
            Clone(overlap, sourceOffset: 6));
        ExpectMalformed("overlap destination range", 4,
            Clone(overlap, destinationOffset: 6));
        ExpectMalformed("overlap expected value", 4,
            Clone(overlap, expected: overlap.Expected ^ 1UL));
        ExpectMalformed("overlap expected byte count", 4,
            Clone(overlap, expectedByteCount: 7));
        ExpectMalformed("overlap expected element count", 4,
            Clone(overlap, expectedElementCount: 7));

        SpanMemoryConformanceVector memoryCast = Get(9);
        ExpectMalformed("memory cast source width", 9,
            Clone(memoryCast, sourceWidth: 3));
        ExpectMalformed("memory cast destination width", 9,
            Clone(memoryCast, destinationWidth: 3));
        ExpectMalformed("memory cast destination offset", 9,
            Clone(memoryCast, destinationOffset: 1));
        ExpectMalformed("memory cast sign extension", 9,
            Clone(memoryCast, flags: 3));
        ExpectMalformed("memory cast false success", 9,
            Clone(memoryCast, flags: 0));
        ExpectMalformed("memory cast source range", 9,
            Clone(memoryCast, sourceOffset: 15));
        ExpectMalformed("memory cast expected count", 9,
            Clone(memoryCast,
                expectedElementCount: (byte)(memoryCast.ExpectedElementCount + 1)));
        ExpectMalformed("memory cast expected byte count", 9,
            Clone(memoryCast,
                expectedByteCount: (byte)(memoryCast.ExpectedByteCount - 1)));
        ExpectMalformed("memory cast expected bytes", 9,
            Clone(memoryCast, expected: memoryCast.Expected ^ 1UL));

        SpanMemoryConformanceVector expansionCast = Get(11);
        ExpectMalformed("memory cast partial source element", 11,
            Clone(expansionCast, byteCount: 3));

        SpanMemoryConformanceVector width = Get(13);
        ExpectMalformed("unsafe unsupported source width", 13,
            Clone(width, sourceWidth: 3));
        ExpectMalformed("unsafe unsupported destination width", 13,
            Clone(width, destinationWidth: 3));
        ExpectMalformed("unsafe aligned start", 13,
            Clone(width, sourceOffset: 2));
        ExpectMalformed("unsafe destination offset", 13,
            Clone(width, destinationOffset: 1));
        ExpectMalformed("unsafe byte count", 13,
            Clone(width, byteCount: 1));
        ExpectMalformed("unsafe false success", 13,
            Clone(width, flags: SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .SignExtendFlag));
        ExpectMalformed("unsafe expected value", 13,
            Clone(width, expected: width.Expected ^ 1UL));
        ExpectMalformed("unsafe expected byte count", 13,
            Clone(width, expectedByteCount: 7));
        ExpectMalformed("unsafe expected element count", 13,
            Clone(width, expectedElementCount: 2));

        SpanMemoryConformanceVector truncatingWidth = Get(15);
        ExpectMalformed("unsafe invalid sign extension", 15,
            Clone(truncatingWidth, flags: 3));
    }

    private static void TestDuplicateAndOrderFailures()
    {
        SpanMemoryConformanceVector[] vectors = CreateCopiedCatalog();
        vectors[1] = Clone(vectors[1], key: vectors[0].Key);
        SpanMemoryConformanceResult duplicate = Validate(vectors);
        ExpectStatus(
            "duplicate key",
            SpanMemoryConformanceStatus.DuplicateVectorKey,
            duplicate);
        ExpectEqual("duplicate problem index", 1, duplicate.ProblemIndex);
        ExpectEqual("duplicate conflict index", 0, duplicate.ConflictIndex);

        vectors = CreateCopiedCatalog();
        (vectors[0], vectors[1]) = (vectors[1], vectors[0]);
        SpanMemoryConformanceResult reordered = Validate(vectors);
        ExpectStatus(
            "order mismatch",
            SpanMemoryConformanceStatus.VectorOrderMismatch,
            reordered);
        ExpectEqual("order problem index", 0, reordered.ProblemIndex);
        ExpectEqual("order conflict index", -1, reordered.ConflictIndex);
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
        ExpectEqual(
            "stale expected actual fingerprint",
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .ExpectedCatalogFingerprint,
            staleExpected.ActualFingerprint);
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

        SpanMemoryExecutionResult execution = default;
        SpanMemoryVectorExecutionRecord record = default;
        if (execution.IsInitialized || execution.Succeeded ||
            record.IsInitialized || record.Succeeded)
        {
            throw new InvalidOperationException(
                "Default execution result or record succeeded.");
        }
    }

    private static void TestAllocationFreeValidation()
    {
        Span<SpanMemoryConformanceVector> vectors =
            stackalloc SpanMemoryConformanceVector[
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .TotalVectorCount];
        Span<byte> scratch = stackalloc byte[
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .RequiredScratchBytes];
        Span<SpanMemoryVectorExecutionRecord> output =
            stackalloc SpanMemoryVectorExecutionRecord[
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .RequiredExecutionRecordCount];
        ExpectSuccess(
            "allocation warmup copy",
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                .CopyBuiltInCatalog(vectors));
        ExpectSuccess(
            "allocation warmup validation",
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ValidateCatalog(
                vectors,
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint));
        ExpectExecutionSuccess(
            "allocation warmup execution",
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExecuteCatalog(
                vectors,
                scratch,
                output,
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 128; iteration++)
        {
            SpanMemoryConformanceResult copy =
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .CopyBuiltInCatalog(vectors);
            SpanMemoryConformanceResult validation =
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ValidateCatalog(
                    vectors,
                    SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                        .ExpectedCatalogFingerprint);
            SpanMemoryExecutionResult execution =
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExecuteCatalog(
                    vectors,
                    scratch,
                    output,
                    SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                        .ExpectedCatalogFingerprint);
            if (!copy.Succeeded || !validation.Succeeded || !execution.Succeeded)
            {
                throw new InvalidOperationException(
                    "Warmed allocation-free catalog operation failed.");
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        ExpectEqual("copy validation and execution allocation", before, after);
    }

    private static void TestUnsafeWidthSourceMutationRejected()
    {
        string root = FindRepositoryRoot();
        string testPath = Path.Combine(
            root,
            "tests",
            "OS.HostTests",
            "SpanOverlapMemoryCastUnsafeWidthConformanceVectorsTests.cs");
        string source = File.ReadAllText(testPath);
        int readWidthStart = source.LastIndexOf(
            "private static ulong ReadWidth(",
            StringComparison.Ordinal);
        int readWidthEnd = source.IndexOf(
            "private static ulong ConvertWidth(",
            readWidthStart,
            StringComparison.Ordinal);
        if (readWidthStart < 0 || readWidthEnd <= readWidthStart)
        {
            throw new InvalidOperationException(
                "Unsafe-width read source boundary is malformed.");
        }

        string readWidthSource = source[readWidthStart..readWidthEnd];
        if (!HasExactUnsafeWidthReadBranches(readWidthSource))
        {
            throw new InvalidOperationException(
                "Accepted unsafe-width read source failed its own boundary.");
        }

        const string Width8 =
            "8 => Unsafe.ReadUnaligned<ulong>(ref start),";
        const string HostileWidth8 =
            "8 => Unsafe.ReadUnaligned<uint>(ref start),";
        string hostile = readWidthSource.Replace(
            Width8,
            HostileWidth8,
            StringComparison.Ordinal);
        if (hostile == readWidthSource || HasExactUnsafeWidthReadBranches(hostile))
        {
            throw new InvalidOperationException(
                "uint-for-ulong unsafe-width source mutation was accepted.");
        }
    }

    private static bool HasExactUnsafeWidthReadBranches(string source)
    {
        string[] required =
        [
            "1 => Unsafe.ReadUnaligned<byte>(ref start),",
            "2 => Unsafe.ReadUnaligned<ushort>(ref start),",
            "4 => Unsafe.ReadUnaligned<uint>(ref start),",
            "8 => Unsafe.ReadUnaligned<ulong>(ref start),"
        ];
        for (int index = 0; index < required.Length; index++)
        {
            if (CountOccurrences(source, required[index]) != 1)
            {
                return false;
            }
        }

        return !source.Contains(
            "8 => Unsafe.ReadUnaligned<uint>(ref start),",
            StringComparison.Ordinal);
    }

    private static void TestSourceBoundary()
    {
        string root = FindRepositoryRoot();
        string sourcePath = Path.Combine(
            root,
            "std",
            "no-runtime",
            "shared",
            "SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs");
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException(
                "Owned production source is missing: " + sourcePath + ".");
        }

        string source = File.ReadAllText(sourcePath);

        string[] forbidden =
        [
            "namespace System",
            "static readonly",
            "new[]",
            "new []",
            "List<",
            "Dictionary<",
            "Enumerable.",
            "System.Reflection",
            "dynamic ",
            "throw ",
            "references\\",
            "references/",
            "C:\\Projects"
        ];
        for (int index = 0; index < forbidden.Length; index++)
        {
            if (source.Contains(forbidden[index], StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Production source contains forbidden boundary text: "
                    + forbidden[index] + ".");
            }
        }

        RequireContains(source, "namespace OS.Std.Memory;");
        RequireContains(source, "#if !NOSTDLIB_EFI");
        RequireContains(source, "SpanMemoryConformanceVector* destination");
        RequireContains(source, "SpanMemoryConformanceVector* vectors");
        RequireContains(source, "public static unsafe SpanMemoryExecutionResult ExecuteCatalog(");
        RequireContains(source, "SpanMemoryVectorExecutionRecord* output");
        RequireContains(source, "SpanMemoryConformanceStatus.VectorPointerNull");
        RequireContains(source, "SpanMemoryExecutionStatus.PointerRangeWrap");
        RequireContains(source, "SpanMemoryExecutionStatus.BufferAlias");
        RequireContains(source, "stackalloc SpanMemoryConformanceVector[TotalVectorCount]");
        RequireContains(source, "ExpectedCatalogFingerprint = 0xF03FF59D36F39ECDUL");
        RequireContains(source, "14695981039346656037UL");
        RequireContains(source, "1099511628211UL");
        RequireContains(source, "public const int PointerWidth = 8;");
        RequireContains(source, "public const int RequiredScratchBytes = 16;");
        RequireContains(source,
            "public const int RequiredExecutionRecordCount = TotalVectorCount;");
        RequireContains(source, "public const bool MemoryCastAllowsUnalignedStart = true;");
        RequireContains(source,
            "public const bool MemoryCastFloorTruncatesTrailingBytes = true;");
        RequireContains(source, "public const bool UnsafeWidthUsesUnsignedReads = true;");
        RequireContains(source, "public const bool AddsCanonicalSystemNamespace = false;");
        RequireContains(source, "public const bool ReferenceFilesRequired = false;");
        RequireContains(source, "public const bool RequiresHostConformance = false;");
        RequireContains(source, "public const bool SatisfiesRuntimeCompletionGate4 = false;");
        RequireContains(source, "public bool SatisfiesRuntimeCompletionGate4 => false;");
        RequireContains(source, "record.RawUnsignedValue == expectedRawUnsignedValue");
        RequireContains(source, "byteCount / count != size");
        if (source.Contains("nuint.MaxValue", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production source depends on unsupported UIntPtr.MaxValue.");
        }

        int hostBranchStart = source.IndexOf(
            "#if !NOSTDLIB_EFI",
            StringComparison.Ordinal);
        int pointerBranchStart = source.IndexOf(
            "#else",
            hostBranchStart,
            StringComparison.Ordinal);
        int pointerBranchEnd = source.IndexOf(
            "#endif",
            pointerBranchStart,
            StringComparison.Ordinal);
        if (hostBranchStart < 0 ||
            pointerBranchStart <= hostBranchStart ||
            pointerBranchEnd <= pointerBranchStart)
        {
            throw new InvalidOperationException(
                "Host/pointer conditional boundary is malformed.");
        }

        string pointerBranch = source[pointerBranchStart..pointerBranchEnd];
        string[] pointerForbidden =
        [
            "System.",
            "ReadOnlySpan<",
            "Span<",
            "[]",
            "List<",
            "Dictionary<",
            "Enumerable.",
            "Reflection",
            "dynamic ",
            "throw ",
            "Exception",
            "GC.",
            "Array.",
            "new "
        ];
        for (int index = 0; index < pointerForbidden.Length; index++)
        {
            if (pointerBranch.Contains(
                    pointerForbidden[index],
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "NOSTDLIB_EFI pointer branch contains host-only text: "
                    + pointerForbidden[index] + ".");
            }
        }
    }

    private static void TestNoStdLibPointerBranchCompiles()
    {
        string root = FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m04-003-nostdlib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.Copy(
                Path.Combine(root, "global.json"),
                Path.Combine(workspace, "global.json"));
            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibShim.cs"),
                """
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType : Object { }
                    public abstract class Enum : ValueType { }
                    public abstract class Delegate : Object { }
                    public abstract class MulticastDelegate : Delegate { }
                    public class Attribute : Object { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Byte { }
                    public struct Int32 { }
                    public struct UInt64 { }
                    public struct IntPtr { }
                    public struct UIntPtr { }
                    public enum AttributeTargets { }
                    public sealed class AttributeUsageAttribute : Attribute
                    {
                        public AttributeUsageAttribute(AttributeTargets validOn) { }
                        public bool AllowMultiple { get; set; }
                        public bool Inherited { get; set; }
                    }
                }

                namespace System.Runtime.CompilerServices
                {
                    public sealed class IsReadOnlyAttribute : System.Attribute { }
                    public sealed class CompilerGeneratedAttribute : System.Attribute { }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                internal static unsafe class M04003NoStdLibProbe
                {
                    internal static byte Exercise(
                        OS.Std.Memory.SpanMemoryConformanceVector* vectors,
                        byte* scratch,
                        OS.Std.Memory.SpanMemoryVectorExecutionRecord* output)
                    {
                        OS.Std.Memory.SpanMemoryConformanceResult copy =
                            OS.Std.Memory
                                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                                .CopyBuiltInCatalog(vectors, 16);
                        OS.Std.Memory.SpanMemoryExecutionResult execution =
                            OS.Std.Memory
                                .SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                                .ExecuteCatalog(
                                    vectors,
                                    16,
                                    scratch,
                                    16,
                                    output,
                                    16,
                                    0xF03FF59D36F39ECDUL);
                        return copy.Succeeded && execution.Succeeded
                            ? (byte)1
                            : (byte)0;
                    }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string productionPath = Path.Combine(
                root,
                "std",
                "no-runtime",
                "shared",
                "SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs");
            string project =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M04.SpanMemory.NoStdLib</AssemblyName>
                    <TargetFramework>net10.0</TargetFramework>
                    <LangVersion>14.0</LangVersion>
                    <OutputType>Library</OutputType>
                    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                    <ImplicitUsings>disable</ImplicitUsings>
                    <Nullable>disable</Nullable>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                    <NoStdLib>true</NoStdLib>
                    <NoConfig>true</NoConfig>
                    <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                    <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                    <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles>
                    <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
                    <EnableNETAnalyzers>false</EnableNETAnalyzers>
                    <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                    <DefineConstants>NOSTDLIB_EFI</DefineConstants>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <Deterministic>true</Deterministic>
                    <DebugType>none</DebugType>
                    <DebugSymbols>false</DebugSymbols>
                    <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="NoStdLibShim.cs" />
                    <Compile Include="__PRODUCTION__" Link="SpanMemory.cs" />
                    <Compile Include="Probe.cs" />
                  </ItemGroup>
                  <Target Name="CustomizeReferences"
                          BeforeTargets="BeforeCompile"
                          AfterTargets="FindReferenceAssembliesForReferences">
                    <ItemGroup>
                      <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                      <ReferencePath Remove="@(ReferencePath)" />
                    </ItemGroup>
                  </Target>
                </Project>
                """
                .Replace(
                    "__PRODUCTION__",
                    EscapeXml(productionPath),
                    StringComparison.Ordinal);
            string projectPath = Path.Combine(
                workspace,
                "M04.003.NoStdLib.csproj");
            File.WriteAllText(
                projectPath,
                project,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            ProcessStartInfo start = new("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workspace
            };
            start.ArgumentList.Add("build");
            start.ArgumentList.Add(projectPath);
            start.ArgumentList.Add("--nologo");
            start.ArgumentList.Add("-m:1");
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException(
                    "Failed to start M04-003 NOSTDLIB_EFI compile guard.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit(90000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            Task.WaitAll(stdout, stderr);
            if (!exited || process.ExitCode != 0)
            {
                string output = stdout.Result + Environment.NewLine + stderr.Result;
                throw new InvalidOperationException(
                    "M04-003 NOSTDLIB_EFI compile guard failed: "
                    + output.Replace('\r', ' ').Replace('\n', ' ').Trim());
            }
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
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

    private static SpanMemoryVectorExecutionRecord CreateExecutionSentinel(
        SpanMemoryConformanceVector[] vectors)
    {
        byte[] scratch = new byte[16];
        SpanMemoryVectorExecutionRecord[] output =
            new SpanMemoryVectorExecutionRecord[16];
        ExpectExecutionSuccess(
            "execution sentinel fixture",
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExecuteCatalog(
                vectors,
                scratch,
                output,
                SpanOverlapMemoryCastUnsafeWidthConformanceVectors
                    .ExpectedCatalogFingerprint));
        return output[0];
    }

    private static void AssertExecutionFailureAtomic(
        string name,
        SpanMemoryConformanceVector[] vectors,
        SpanMemoryExecutionStatus expectedStatus,
        SpanMemoryConformanceStatus? expectedCatalogStatus,
        ulong fingerprint,
        int scratchLength,
        int outputLength,
        SpanMemoryVectorExecutionRecord sentinel)
    {
        byte[] scratch = new byte[scratchLength];
        Array.Fill(scratch, (byte)0xA5);
        SpanMemoryVectorExecutionRecord[] output =
            new SpanMemoryVectorExecutionRecord[outputLength];
        Array.Fill(output, sentinel);

        SpanMemoryExecutionResult result =
            SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExecuteCatalog(
                vectors,
                scratch,
                output,
                fingerprint);
        ExpectExecutionStatus(name, expectedStatus, result);
        if (expectedCatalogStatus.HasValue)
        {
            if (!result.CatalogValidation.IsInitialized)
            {
                throw new InvalidOperationException(
                    name + " did not preserve catalog failure detail.");
            }

            ExpectEqual(
                name + " catalog status",
                expectedCatalogStatus.Value,
                result.CatalogValidation.Status);
        }

        for (int index = 0; index < scratch.Length; index++)
        {
            ExpectEqual(name + " scratch atomic", (byte)0xA5, scratch[index]);
        }

        for (int index = 0; index < output.Length; index++)
        {
            ExpectRecordEqual(name + " output atomic", sentinel, output[index]);
        }
    }

    private static void ExpectExecutionSuccess(
        string name,
        SpanMemoryExecutionResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                name + " failed with " + result.Status + ".");
        }
    }

    private static void ExpectExecutionStatus(
        string name,
        SpanMemoryExecutionStatus expected,
        SpanMemoryExecutionResult result)
    {
        if (!result.IsInitialized || result.Status != expected || result.Succeeded)
        {
            throw new InvalidOperationException(
                name + " expected " + expected + " but got "
                + result.Status + ".");
        }
    }

    private static void ExpectRecordEqual(
        string name,
        SpanMemoryVectorExecutionRecord expected,
        SpanMemoryVectorExecutionRecord actual)
    {
        if (expected.IsInitialized != actual.IsInitialized ||
            expected.Key != actual.Key ||
            expected.Operation != actual.Operation ||
            expected.Status != actual.Status ||
            expected.OutputByteCount != actual.OutputByteCount ||
            expected.ResultElementCount != actual.ResultElementCount ||
            expected.RawUnsignedValue != actual.RawUnsignedValue ||
            expected.OutputBytes != actual.OutputBytes ||
            expected.ScratchAfter != actual.ScratchAfter ||
            expected.RejectedInputPreserved != actual.RejectedInputPreserved)
        {
            throw new InvalidOperationException(name + " record changed.");
        }
    }

    private static void ExpectMalformed(
        string name,
        int index,
        SpanMemoryConformanceVector hostile)
    {
        SpanMemoryConformanceVector[] vectors = CreateCopiedCatalog();
        vectors[index] = hostile;
        SpanMemoryConformanceResult result = Validate(vectors);
        ExpectStatus(
            name,
            SpanMemoryConformanceStatus.VectorMalformed,
            result);
        ExpectEqual(name + " problem index", index, result.ProblemIndex);
    }

    private static SpanMemoryConformanceVector Clone(
        SpanMemoryConformanceVector vector,
        byte? key = null,
        SpanMemoryConformanceOperation? operation = null,
        int? sourceOffset = null,
        int? destinationOffset = null,
        int? byteCount = null,
        byte? sourceWidth = null,
        byte? destinationWidth = null,
        byte? flags = null,
        byte? expectedByteCount = null,
        byte? expectedElementCount = null,
        ulong? input = null,
        ulong? expected = null)
    {
        return new SpanMemoryConformanceVector(
            key ?? vector.Key,
            operation ?? vector.Operation,
            sourceOffset ?? vector.SourceOffset,
            destinationOffset ?? vector.DestinationOffset,
            byteCount ?? vector.ByteCount,
            sourceWidth ?? vector.SourceWidth,
            destinationWidth ?? vector.DestinationWidth,
            flags ?? vector.Flags,
            expectedByteCount ?? vector.ExpectedByteCount,
            expectedElementCount ?? vector.ExpectedElementCount,
            input ?? vector.Input,
            expected ?? vector.Expected);
    }

    private static byte[] GetInputBytes(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static void WritePackedInput(
        Span<byte> destination,
        int count,
        ulong value)
    {
        for (int index = 0; index < count; index++)
        {
            destination[index] = (byte)(value >> (index * 8));
        }
    }

    private static byte[] ExecuteDotNetMemoryCast(
        SpanMemoryConformanceVector vector,
        byte[] storage,
        out int resultElementCount)
    {
        ReadOnlySpan<byte> bytes = storage.AsSpan(
            vector.SourceOffset,
            vector.ByteCount);
        if (vector.SourceWidth == 1 && vector.DestinationWidth == 2)
        {
            ReadOnlySpan<ushort> cast = MemoryMarshal.Cast<byte, ushort>(bytes);
            resultElementCount = cast.Length;
            return MemoryMarshal.AsBytes(cast).ToArray();
        }

        if (vector.SourceWidth == 1 && vector.DestinationWidth == 4)
        {
            ReadOnlySpan<uint> cast = MemoryMarshal.Cast<byte, uint>(bytes);
            resultElementCount = cast.Length;
            return MemoryMarshal.AsBytes(cast).ToArray();
        }

        if (vector.SourceWidth == 4 && vector.DestinationWidth == 1)
        {
            ReadOnlySpan<uint> source = MemoryMarshal.Cast<byte, uint>(bytes);
            ReadOnlySpan<byte> cast = MemoryMarshal.Cast<uint, byte>(source);
            resultElementCount = cast.Length;
            return cast.ToArray();
        }

        throw new InvalidOperationException(
            "Unexpected frozen memory-cast shape.");
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

    private static void WriteUnalignedWidth(
        Span<byte> destination,
        byte width,
        ulong value)
    {
        ref byte target = ref MemoryMarshal.GetReference(destination);
        switch (width)
        {
            case 1:
                Unsafe.WriteUnaligned(ref target, (byte)value);
                return;
            case 2:
                Unsafe.WriteUnaligned(ref target, (ushort)value);
                return;
            case 4:
                Unsafe.WriteUnaligned(ref target, (uint)value);
                return;
            case 8:
                Unsafe.WriteUnaligned(ref target, value);
                return;
            default:
                throw new InvalidOperationException("Unsupported write width.");
        }
    }

    private static ulong ReadWidth(Span<byte> source, byte width)
    {
        ref byte start = ref MemoryMarshal.GetReference(source);
        return width switch
        {
            1 => Unsafe.ReadUnaligned<byte>(ref start),
            2 => Unsafe.ReadUnaligned<ushort>(ref start),
            4 => Unsafe.ReadUnaligned<uint>(ref start),
            8 => Unsafe.ReadUnaligned<ulong>(ref start),
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

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while (offset <= source.Length - value.Length)
        {
            int index = source.IndexOf(value, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            offset = index + value.Length;
        }

        return count;
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
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            throw new InvalidOperationException(
                name + " expected " + expected + " but got " + actual + ".");
        }
    }
}
