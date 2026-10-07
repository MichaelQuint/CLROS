using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using OS.Std.Binary;

internal static class
    IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosaTests
{
    private const string ReferenceTestSha256 =
        "DDC01A437FB2F446B18758E4BECC8DA0370177B33B6271897E7295AE14A007BC";

    [ModuleInitializer]
    internal static void Run()
    {
        Console.WriteLine("TEST_START m04.002");

        try
        {
            TestMosaSeriesProjection();
            TestBuiltInCatalogCopiesAndValidates();
            TestReadVectorsAgainstIndependentImplementations();
            TestReverseVectorsAgainstBinaryPrimitives();
            TestUnknownRetrievalFailsClosed();
            TestShortDestinationsRemainUnchanged();
            TestMalformedVectorsFailClosed();
            TestDuplicateKeysFailClosed();
            TestReorderedKeysFailClosed();
            TestFingerprintMismatchFailsClosed();
            TestDefaultResultsAndLayoutsFailClosed();
            TestSourceBoundary();

            Console.WriteLine(
                "CLROS_M04_002_READY readVectors="
                + IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .ReadVectorCount
                + " reverseVectors="
                + IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .ReverseVectorCount
                + " byteSeries="
                + IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .ByteSeriesCount
                + " uint16Series="
                + IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .UInt16SeriesCount
                + " fingerprint="
                + IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .ExpectedCatalogFingerprint.ToString("X16")
                + " referenceTestSha256=" + ReferenceTestSha256
                + " referenceFilesRequired=false hostConformance=true"
                + " guestIntegrated=false gate4=Fail");
            Console.WriteLine("TEST_PASS m04.002");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "TEST_FAIL m04.002 " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }

    private static void TestMosaSeriesProjection()
    {
        byte[] expectedByteSeries = [0, 1, 2, 254, 255];
        ushort[] expectedUInt16Series = [0, 1, 2, 254, 255, 65534, 65535];

        ExpectEqual(
            "byte series count",
            expectedByteSeries.Length,
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ByteSeriesCount);
        for (int index = 0; index < expectedByteSeries.Length; index++)
        {
            if (!IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .TryGetByteSeriesValue(index, out byte actual))
            {
                throw new InvalidOperationException(
                    "Byte series rejected index " + index + ".");
            }

            ExpectEqual("byte series value", expectedByteSeries[index], actual);
        }

        ExpectEqual(
            "UInt16 series count",
            expectedUInt16Series.Length,
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .UInt16SeriesCount);
        for (int index = 0; index < expectedUInt16Series.Length; index++)
        {
            if (!IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .TryGetUInt16SeriesValue(index, out ushort actual))
            {
                throw new InvalidOperationException(
                    "UInt16 series rejected index " + index + ".");
            }

            ExpectEqual(
                "UInt16 series value",
                expectedUInt16Series[index],
                actual);
        }
    }

    private static void TestBuiltInCatalogCopiesAndValidates()
    {
        IndependentEndianReadVector[] reads = CreateReadDestination();
        IndependentEndianReverseVector[] reverses = CreateReverseDestination();

        IndependentEndianConformanceResult result =
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .CopyBuiltInCatalog(reads, reverses);
        ExpectSuccess("built-in catalog", result);
        ExpectEqual(
            "built-in fingerprint",
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ExpectedCatalogFingerprint,
            result.ActualFingerprint);
        ExpectEqual(
            "total vector count",
            32,
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .TotalVectorCount);

        IndependentEndianConformanceResult validate =
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ValidateCatalog(
                    reads,
                    reverses,
                    IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ExpectedCatalogFingerprint);
        ExpectSuccess("copied catalog revalidation", validate);
    }

    private static void TestReadVectorsAgainstIndependentImplementations()
    {
        Span<byte> source = stackalloc byte[2];
        Span<byte> osWritten = stackalloc byte[2];
        Span<byte> bclWritten = stackalloc byte[2];

        for (int index = 0;
            index < IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ReadVectorCount;
            index++)
        {
            if (!IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .TryGetReadVector(index, out var vector))
            {
                throw new InvalidOperationException(
                    "Read catalog rejected index " + index + ".");
            }

            source[0] = vector.FirstByte;
            source[1] = vector.SecondByte;

            ExpectEqual(
                "OS little-endian read key " + vector.Key,
                vector.ExpectedLittleEndian,
                EndianPrimitives.ReadUInt16LittleEndian(source));
            ExpectEqual(
                "BCL little-endian read key " + vector.Key,
                vector.ExpectedLittleEndian,
                BinaryPrimitives.ReadUInt16LittleEndian(source));
            ExpectEqual(
                "OS big-endian read key " + vector.Key,
                vector.ExpectedBigEndian,
                EndianPrimitives.ReadUInt16BigEndian(source));
            ExpectEqual(
                "BCL big-endian read key " + vector.Key,
                vector.ExpectedBigEndian,
                BinaryPrimitives.ReadUInt16BigEndian(source));

            if (!EndianPrimitives.TryReadUInt16LittleEndian(
                    source,
                    out ushort osLittle) ||
                !EndianPrimitives.TryReadUInt16BigEndian(
                    source,
                    out ushort osBig))
            {
                throw new InvalidOperationException(
                    "OS TryRead rejected complete vector " + vector.Key + ".");
            }

            ExpectEqual("OS TryRead little", vector.ExpectedLittleEndian, osLittle);
            ExpectEqual("OS TryRead big", vector.ExpectedBigEndian, osBig);

            osWritten.Clear();
            bclWritten.Clear();
            EndianPrimitives.WriteUInt16LittleEndian(
                osWritten,
                vector.ExpectedLittleEndian);
            BinaryPrimitives.WriteUInt16LittleEndian(
                bclWritten,
                vector.ExpectedLittleEndian);
            ExpectPair("little-endian OS write", vector, osWritten);
            ExpectPair("little-endian BCL write", vector, bclWritten);

            osWritten.Clear();
            bclWritten.Clear();
            EndianPrimitives.WriteUInt16BigEndian(
                osWritten,
                vector.ExpectedBigEndian);
            BinaryPrimitives.WriteUInt16BigEndian(
                bclWritten,
                vector.ExpectedBigEndian);
            ExpectPair("big-endian OS write", vector, osWritten);
            ExpectPair("big-endian BCL write", vector, bclWritten);
        }
    }

    private static void TestReverseVectorsAgainstBinaryPrimitives()
    {
        Span<byte> source = stackalloc byte[2];

        for (int index = 0;
            index < IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ReverseVectorCount;
            index++)
        {
            if (!IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .TryGetReverseVector(index, out var vector))
            {
                throw new InvalidOperationException(
                    "Reverse catalog rejected index " + index + ".");
            }

            ushort reversed = BinaryPrimitives.ReverseEndianness(vector.Input);
            ExpectEqual(
                "BCL reverse key " + vector.Key,
                vector.Expected,
                reversed);
            ExpectEqual(
                "BCL double reverse key " + vector.Key,
                vector.Input,
                BinaryPrimitives.ReverseEndianness(reversed));

            source[0] = (byte)vector.Input;
            source[1] = (byte)(vector.Input >> 8);
            ExpectEqual(
                "OS reverse relation key " + vector.Key,
                vector.Expected,
                EndianPrimitives.ReadUInt16BigEndian(source));
            ExpectEqual(
                "OS native relation key " + vector.Key,
                vector.Input,
                EndianPrimitives.ReadUInt16LittleEndian(source));
        }
    }

    private static void TestUnknownRetrievalFailsClosed()
    {
        if (IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .TryGetByteSeriesValue(-1, out byte byteBelow) ||
            byteBelow != 0 ||
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .TryGetByteSeriesValue(5, out byte byteAbove) ||
            byteAbove != 0)
        {
            throw new InvalidOperationException(
                "Unknown byte-series indexes did not fail closed.");
        }

        if (IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .TryGetUInt16SeriesValue(-1, out ushort uint16Below) ||
            uint16Below != 0 ||
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .TryGetUInt16SeriesValue(7, out ushort uint16Above) ||
            uint16Above != 0)
        {
            throw new InvalidOperationException(
                "Unknown UInt16-series indexes did not fail closed.");
        }

        if (IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .TryGetReadVector(-1, out var readBelow) ||
            readBelow.IsInitialized ||
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .TryGetReadVector(25, out var readAbove) ||
            readAbove.IsInitialized ||
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .TryGetReverseVector(-1, out var reverseBelow) ||
            reverseBelow.IsInitialized ||
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .TryGetReverseVector(7, out var reverseAbove) ||
            reverseAbove.IsInitialized)
        {
            throw new InvalidOperationException(
                "Unknown conformance-vector indexes did not fail closed.");
        }
    }

    private static void TestShortDestinationsRemainUnchanged()
    {
        IndependentEndianReadVector readSentinel =
            new(200, 0xA5, 0x5A, 0x5AA5, 0xA55A);
        IndependentEndianReverseVector reverseSentinel =
            new(200, 0xA55A, 0x5AA5);

        IndependentEndianReadVector[] shortReads =
            new IndependentEndianReadVector[24];
        IndependentEndianReverseVector[] completeReverses =
            new IndependentEndianReverseVector[7];
        Fill(shortReads, readSentinel);
        Fill(completeReverses, reverseSentinel);
        IndependentEndianConformanceResult readFailure =
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .CopyBuiltInCatalog(shortReads, completeReverses);
        ExpectStatus(
            "short read destination",
            IndependentEndianConformanceStatus.ReadDestinationTooSmall,
            readFailure);
        ExpectAll(shortReads, readSentinel, "short read destination");
        ExpectAll(
            completeReverses,
            reverseSentinel,
            "reverse destination after read failure");

        IndependentEndianReadVector[] completeReads =
            new IndependentEndianReadVector[25];
        IndependentEndianReverseVector[] shortReverses =
            new IndependentEndianReverseVector[6];
        Fill(completeReads, readSentinel);
        Fill(shortReverses, reverseSentinel);
        IndependentEndianConformanceResult reverseFailure =
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .CopyBuiltInCatalog(completeReads, shortReverses);
        ExpectStatus(
            "short reverse destination",
            IndependentEndianConformanceStatus.ReverseDestinationTooSmall,
            reverseFailure);
        ExpectAll(
            completeReads,
            readSentinel,
            "read destination after reverse failure");
        ExpectAll(shortReverses, reverseSentinel, "short reverse destination");
    }

    private static void TestMalformedVectorsFailClosed()
    {
        LoadCatalog(out var reads, out var reverses);
        reads[4] = default;
        ExpectStatus(
            "uninitialized read vector",
            IndependentEndianConformanceStatus.ReadVectorUninitialized,
            Validate(reads, reverses));

        LoadCatalog(out reads, out reverses);
        reads[4] = new IndependentEndianReadVector(
            key: 0,
            reads[4].FirstByte,
            reads[4].SecondByte,
            reads[4].ExpectedLittleEndian,
            reads[4].ExpectedBigEndian);
        ExpectStatus(
            "read key out of range",
            IndependentEndianConformanceStatus.ReadVectorKeyOutOfRange,
            Validate(reads, reverses));

        LoadCatalog(out reads, out reverses);
        reads[4] = new IndependentEndianReadVector(
            reads[4].Key,
            reads[4].FirstByte,
            reads[4].SecondByte,
            (ushort)(reads[4].ExpectedLittleEndian ^ 1),
            reads[4].ExpectedBigEndian);
        ExpectStatus(
            "malformed read expectation",
            IndependentEndianConformanceStatus.ReadVectorExpectedValueMismatch,
            Validate(reads, reverses));

        LoadCatalog(out reads, out reverses);
        reverses[3] = default;
        ExpectStatus(
            "uninitialized reverse vector",
            IndependentEndianConformanceStatus.ReverseVectorUninitialized,
            Validate(reads, reverses));

        LoadCatalog(out reads, out reverses);
        reverses[3] = new IndependentEndianReverseVector(
            key: 0,
            reverses[3].Input,
            reverses[3].Expected);
        ExpectStatus(
            "reverse key out of range",
            IndependentEndianConformanceStatus.ReverseVectorKeyOutOfRange,
            Validate(reads, reverses));

        LoadCatalog(out reads, out reverses);
        reverses[3] = new IndependentEndianReverseVector(
            reverses[3].Key,
            reverses[3].Input,
            (ushort)(reverses[3].Expected ^ 1));
        ExpectStatus(
            "malformed reverse expectation",
            IndependentEndianConformanceStatus
                .ReverseVectorExpectedValueMismatch,
            Validate(reads, reverses));

        LoadCatalog(out reads, out reverses);
        ExpectStatus(
            "short read catalog",
            IndependentEndianConformanceStatus.ReadVectorCountMismatch,
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ValidateCatalog(
                    reads.AsSpan(0, 24),
                    reverses,
                    IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ExpectedCatalogFingerprint));
        ExpectStatus(
            "short reverse catalog",
            IndependentEndianConformanceStatus.ReverseVectorCountMismatch,
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ValidateCatalog(
                    reads,
                    reverses.AsSpan(0, 6),
                    IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                        .ExpectedCatalogFingerprint));
    }

    private static void TestDuplicateKeysFailClosed()
    {
        LoadCatalog(out var reads, out var reverses);
        reads[7] = reads[6];
        IndependentEndianConformanceResult readDuplicate = Validate(
            reads,
            reverses);
        ExpectStatus(
            "duplicate read key",
            IndependentEndianConformanceStatus.DuplicateReadVectorKey,
            readDuplicate);
        ExpectEqual("duplicate read problem index", 7, readDuplicate.ProblemIndex);
        ExpectEqual("duplicate read conflict index", 6, readDuplicate.ConflictIndex);

        LoadCatalog(out reads, out reverses);
        reverses[5] = reverses[4];
        IndependentEndianConformanceResult reverseDuplicate = Validate(
            reads,
            reverses);
        ExpectStatus(
            "duplicate reverse key",
            IndependentEndianConformanceStatus.DuplicateReverseVectorKey,
            reverseDuplicate);
        ExpectEqual(
            "duplicate reverse problem index",
            5,
            reverseDuplicate.ProblemIndex);
        ExpectEqual(
            "duplicate reverse conflict index",
            4,
            reverseDuplicate.ConflictIndex);
    }

    private static void TestReorderedKeysFailClosed()
    {
        LoadCatalog(out var reads, out var reverses);
        (reads[0], reads[1]) = (reads[1], reads[0]);
        ExpectStatus(
            "reordered read keys",
            IndependentEndianConformanceStatus.ReadVectorOrderMismatch,
            Validate(reads, reverses));

        LoadCatalog(out reads, out reverses);
        (reverses[0], reverses[1]) = (reverses[1], reverses[0]);
        ExpectStatus(
            "reordered reverse keys",
            IndependentEndianConformanceStatus.ReverseVectorOrderMismatch,
            Validate(reads, reverses));
    }

    private static void TestFingerprintMismatchFailsClosed()
    {
        LoadCatalog(out var reads, out var reverses);
        ulong expected =
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ExpectedCatalogFingerprint;
        IndependentEndianConformanceResult result =
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ValidateCatalog(reads, reverses, expected ^ 1UL);
        ExpectStatus(
            "catalog hash mismatch",
            IndependentEndianConformanceStatus.CatalogFingerprintMismatch,
            result);
        ExpectEqual("catalog actual hash", expected, result.ActualFingerprint);
        ExpectEqual("catalog rejected hash", expected ^ 1UL, result.ExpectedFingerprint);
    }

    private static void TestDefaultResultsAndLayoutsFailClosed()
    {
        IndependentEndianConformanceResult result = default;
        if (result.IsInitialized ||
            result.Succeeded ||
            result.SatisfiesRuntimeCompletionGate4 ||
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .SatisfiesRuntimeCompletionGate4 ||
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .AddsCanonicalSystemNamespace ||
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ReferenceFilesRequired ||
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .RequiresHostConformance)
        {
            throw new InvalidOperationException(
                "Default result or compatibility boundary promoted success.");
        }

        if (RuntimeHelpers.IsReferenceOrContainsReferences<
                IndependentEndianReadVector>() ||
            RuntimeHelpers.IsReferenceOrContainsReferences<
                IndependentEndianReverseVector>() ||
            RuntimeHelpers.IsReferenceOrContainsReferences<
                IndependentEndianConformanceResult>())
        {
            throw new InvalidOperationException(
                "Conformance PODs contain managed references.");
        }
    }

    private static void TestSourceBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string sourcePath = Path.Combine(
            root,
            "std",
            "no-runtime",
            "shared",
            "Binary",
            "IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs");
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

        string[] required =
        [
            "namespace OS.Std.Binary;",
            "public const int ReadVectorCount = ByteSeriesCount * ByteSeriesCount;",
            "public const int ReverseVectorCount = UInt16SeriesCount;",
            "public const ulong ExpectedCatalogFingerprint = 0xE81E4451E8D999F6UL;",
            "public const bool AddsCanonicalSystemNamespace = false;",
            "public const bool SatisfiesRuntimeCompletionGate4 = false;",
            "public const bool ReferenceFilesRequired = false;",
            "public const bool RequiresHostConformance = false;",
            "#if !NOSTDLIB_EFI",
            "public static unsafe IndependentEndianConformanceResult CopyBuiltInCatalog(",
            "IndependentEndianConformanceStatus.ReadVectorPointerNull",
            "IndependentEndianConformanceStatus.ReverseVectorPointerNull",
            "return index switch",
            "ReadOnlySpan<IndependentEndianReadVector>",
            "ReadOnlySpan<IndependentEndianReverseVector>"
        ];
        for (int index = 0; index < required.Length; index++)
        {
            if (!source.Contains(required[index], StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Production source is missing required boundary text: "
                    + required[index] + ".");
            }
        }
    }

    private static IndependentEndianReadVector[] CreateReadDestination()
    {
        return new IndependentEndianReadVector[
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ReadVectorCount];
    }

    private static IndependentEndianReverseVector[] CreateReverseDestination()
    {
        return new IndependentEndianReverseVector[
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .ReverseVectorCount];
    }

    private static void LoadCatalog(
        out IndependentEndianReadVector[] reads,
        out IndependentEndianReverseVector[] reverses)
    {
        reads = CreateReadDestination();
        reverses = CreateReverseDestination();
        ExpectSuccess(
            "load catalog",
            IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                .CopyBuiltInCatalog(reads, reverses));
    }

    private static IndependentEndianConformanceResult Validate(
        IndependentEndianReadVector[] reads,
        IndependentEndianReverseVector[] reverses)
    {
        return IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
            .ValidateCatalog(
                reads,
                reverses,
                IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa
                    .ExpectedCatalogFingerprint);
    }

    private static void ExpectPair(
        string scenario,
        IndependentEndianReadVector vector,
        ReadOnlySpan<byte> actual)
    {
        if (actual.Length != 2 ||
            actual[0] != vector.FirstByte ||
            actual[1] != vector.SecondByte)
        {
            throw new InvalidOperationException(
                scenario + " key " + vector.Key + " returned unexpected bytes.");
        }
    }

    private static void Fill(
        IndependentEndianReadVector[] destination,
        IndependentEndianReadVector value)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = value;
        }
    }

    private static void Fill(
        IndependentEndianReverseVector[] destination,
        IndependentEndianReverseVector value)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = value;
        }
    }

    private static void ExpectAll(
        IndependentEndianReadVector[] actual,
        IndependentEndianReadVector expected,
        string scenario)
    {
        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index].Key != expected.Key ||
                actual[index].FirstByte != expected.FirstByte ||
                actual[index].SecondByte != expected.SecondByte ||
                actual[index].ExpectedLittleEndian !=
                    expected.ExpectedLittleEndian ||
                actual[index].ExpectedBigEndian != expected.ExpectedBigEndian ||
                actual[index].IsInitialized != expected.IsInitialized)
            {
                throw new InvalidOperationException(
                    scenario + " changed element " + index + ".");
            }
        }
    }

    private static void ExpectAll(
        IndependentEndianReverseVector[] actual,
        IndependentEndianReverseVector expected,
        string scenario)
    {
        for (int index = 0; index < actual.Length; index++)
        {
            if (actual[index].Key != expected.Key ||
                actual[index].Input != expected.Input ||
                actual[index].Expected != expected.Expected ||
                actual[index].IsInitialized != expected.IsInitialized)
            {
                throw new InvalidOperationException(
                    scenario + " changed element " + index + ".");
            }
        }
    }

    private static void ExpectSuccess(
        string scenario,
        IndependentEndianConformanceResult result)
    {
        if (!result.Succeeded ||
            result.Status != IndependentEndianConformanceStatus.Success ||
            result.SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException(
                scenario + " expected success but returned " + result.Status + ".");
        }
    }

    private static void ExpectStatus(
        string scenario,
        IndependentEndianConformanceStatus expected,
        IndependentEndianConformanceResult result)
    {
        if (!result.IsInitialized ||
            result.Succeeded ||
            result.Status != expected ||
            result.SatisfiesRuntimeCompletionGate4)
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned "
                + result.Status + ".");
        }
    }

    private static void ExpectEqual(string scenario, int expected, int actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                scenario + " expected " + expected + " but returned "
                + actual + ".");
        }
    }

    private static void ExpectEqual(
        string scenario,
        ulong expected,
        ulong actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                scenario + " expected 0x" + expected.ToString("X")
                + " but returned 0x" + actual.ToString("X") + ".");
        }
    }
}
