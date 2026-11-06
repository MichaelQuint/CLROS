using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static unsafe class OwnedX64GDTContainingNullN64BitCodeTests
{
    private const ulong TssBaseAddress = 0xffff_8000_0020_f000UL;
    private const uint TssLimit = 103;
    private const ulong ExactFingerprint = 0x0835_0e20_1241_eb73UL;

    private const string HostReadyMarker =
        "CLROS_M02_007_READY encoding=hostValidated callerOwned=true " +
        "readBack=true guestEncoding=false guestLgdt=false guestLtr=false allocationFree=true";

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m02.007");

        int failures = 0;
        Run("m02.007 encodes the exact owned long mode gdt", TestExactOwnedEncoding, ref failures);
        Run("m02.007 pointer core is bounded and deterministic", TestPointerCoreIsBounded, ref failures);
        Run("m02.007 preserves the full canonical tss base and limit", TestTssRangeEncoding, ref failures);
        Run("m02.007 rejects invalid input without mutation", TestInvalidInputIsAtomic, ref failures);
        Run("m02.007 readback identifies the first corrupted byte", TestReadBackDetectsCorruption, ref failures);
        Run("m02.007 verification rejects invalid metadata", TestVerificationRejectsInvalidMetadata, ref failures);
        Run("m02.007 pointer hot path allocates no managed bytes", TestPointerHotPathDoesNotAllocate, ref failures);
        Run("m02.007 defaults fail closed and source stays no stdlib ready", TestDefaultsAndSourceBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL m02.007 host-tests failures=" + failures);
            throw new InvalidOperationException(
                "Owned x64 GDT encoding host tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
        Console.WriteLine("TEST_PASS m02.007");
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestExactOwnedEncoding()
    {
        Span<byte> destination = stackalloc byte[
            OwnedX64GdtContainingNullN64BitCode.RequiredByteCount + 8];
        destination.Fill(0xcc);

        OwnedX64GdtEncodingResult result =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                destination,
                TssBaseAddress,
                TssLimit);
        ReadOnlySpan<byte> expected =
        [
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xff, 0xff, 0x00, 0x00, 0x00, 0x9a, 0xaf, 0x00,
            0xff, 0xff, 0x00, 0x00, 0x00, 0x92, 0xcf, 0x00,
            0x67, 0x00, 0x00, 0xf0, 0x20, 0x89, 0x00, 0x00,
            0x00, 0x80, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00
        ];

        return result.Succeeded &&
            result.IsInitialized &&
            result.Status == OwnedX64GdtEncodingStatus.Success &&
            result.BytesWritten == expected.Length &&
            result.RequiredBytes == expected.Length &&
            result.TableLimit == 39 &&
            result.Fingerprint == ExactFingerprint &&
            result.DestinationMutated &&
            result.Verification.Succeeded &&
            result.Verification.BytesCompared == expected.Length &&
            result.Verification.FirstMismatchOffset == -1 &&
            result.Verification.ObservedFingerprint == ExactFingerprint &&
            result.Verification.ExpectedFingerprint == ExactFingerprint &&
            destination.Slice(0, expected.Length).SequenceEqual(expected) &&
            IsFilled(destination.Slice(expected.Length), 0xcc) &&
            OwnedX64GdtContainingNullN64BitCode.LogicalDescriptorCount == 4 &&
            OwnedX64GdtContainingNullN64BitCode.StorageSlotCount == 5 &&
            OwnedX64GdtContainingNullN64BitCode.NullSelector == 0x0000 &&
            OwnedX64GdtContainingNullN64BitCode.KernelCodeSelector == 0x0008 &&
            OwnedX64GdtContainingNullN64BitCode.KernelDataSelector == 0x0010 &&
            OwnedX64GdtContainingNullN64BitCode.TssSelector == 0x0018;
    }

    private static bool TestPointerCoreIsBounded()
    {
        byte* first = stackalloc byte[
            OwnedX64GdtContainingNullN64BitCode.RequiredByteCount + 1];
        byte* second = stackalloc byte[
            OwnedX64GdtContainingNullN64BitCode.RequiredByteCount + 1];
        Fill(first, OwnedX64GdtContainingNullN64BitCode.RequiredByteCount + 1, 0xa5);
        Fill(second, OwnedX64GdtContainingNullN64BitCode.RequiredByteCount + 1, 0x5a);

        OwnedX64GdtEncodingResult firstResult =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                first,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount + 1,
                TssBaseAddress,
                TssLimit);
        OwnedX64GdtEncodingResult secondResult =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                second,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount + 1,
                TssBaseAddress,
                TssLimit);

        return firstResult.Succeeded &&
            secondResult.Succeeded &&
            firstResult.Fingerprint == secondResult.Fingerprint &&
            BytesEqual(
                first,
                second,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount) &&
            first[OwnedX64GdtContainingNullN64BitCode.RequiredByteCount] == 0xa5 &&
            second[OwnedX64GdtContainingNullN64BitCode.RequiredByteCount] == 0x5a;
    }

    private static bool TestTssRangeEncoding()
    {
        const ulong LowCanonicalBase = 0x0000_7abc_def1_2345UL;
        const uint MaximumLimit =
            OwnedX64GdtContainingNullN64BitCode.MaximumTssLimit;
        Span<byte> destination = stackalloc byte[
            OwnedX64GdtContainingNullN64BitCode.RequiredByteCount];

        OwnedX64GdtEncodingResult result =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                destination,
                LowCanonicalBase,
                MaximumLimit);
        int offset = OwnedX64GdtContainingNullN64BitCode.TssDescriptorOffset;

        return result.Succeeded &&
            OwnedX64GdtContainingNullN64BitCode.IsCanonicalX64Address(LowCanonicalBase) &&
            OwnedX64GdtContainingNullN64BitCode.IsCanonicalX64Address(TssBaseAddress) &&
            destination[offset + 0] == 0xff &&
            destination[offset + 1] == 0xff &&
            destination[offset + 2] == 0x45 &&
            destination[offset + 3] == 0x23 &&
            destination[offset + 4] == 0xf1 &&
            destination[offset + 5] == 0x89 &&
            destination[offset + 6] == 0x0f &&
            destination[offset + 7] == 0xde &&
            destination[offset + 8] == 0xbc &&
            destination[offset + 9] == 0x7a &&
            destination[offset + 10] == 0x00 &&
            destination[offset + 11] == 0x00 &&
            IsFilled(destination.Slice(offset + 12, 4), 0x00);
    }

    private static bool TestInvalidInputIsAtomic()
    {
        byte* destination = stackalloc byte[
            OwnedX64GdtContainingNullN64BitCode.RequiredByteCount];

        Fill(destination, OwnedX64GdtContainingNullN64BitCode.RequiredByteCount, 0xa5);
        OwnedX64GdtEncodingResult negativeCapacity =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                destination,
                -1,
                TssBaseAddress,
                TssLimit);
        if (!IsAtomicFailure(
                negativeCapacity,
                OwnedX64GdtEncodingStatus.InvalidDestination,
                destination))
        {
            return false;
        }

        OwnedX64GdtEncodingResult shortDestination =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                destination,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount - 1,
                TssBaseAddress,
                TssLimit);
        if (!IsAtomicFailure(
                shortDestination,
                OwnedX64GdtEncodingStatus.DestinationTooSmall,
                destination))
        {
            return false;
        }

        OwnedX64GdtEncodingResult nullDestination =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                null,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                TssBaseAddress,
                TssLimit);
        if (nullDestination.Succeeded ||
            nullDestination.Status != OwnedX64GdtEncodingStatus.InvalidDestination ||
            nullDestination.DestinationMutated ||
            nullDestination.BytesWritten != 0)
        {
            return false;
        }

        OwnedX64GdtEncodingResult nullTss =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                destination,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                0,
                TssLimit);
        if (!IsAtomicFailure(
                nullTss,
                OwnedX64GdtEncodingStatus.NullTssBaseAddress,
                destination))
        {
            return false;
        }

        OwnedX64GdtEncodingResult nonCanonicalTss =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                destination,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                0x0000_8000_0000_0000UL,
                TssLimit);
        if (!IsAtomicFailure(
                nonCanonicalTss,
                OwnedX64GdtEncodingStatus.NonCanonicalTssBaseAddress,
                destination))
        {
            return false;
        }

        OwnedX64GdtEncodingResult smallLimit =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                destination,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                TssBaseAddress,
                OwnedX64GdtContainingNullN64BitCode.MinimumTssLimit - 1);
        if (!IsAtomicFailure(
                smallLimit,
                OwnedX64GdtEncodingStatus.InvalidTssLimit,
                destination))
        {
            return false;
        }

        OwnedX64GdtEncodingResult largeLimit =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                destination,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                TssBaseAddress,
                OwnedX64GdtContainingNullN64BitCode.MaximumTssLimit + 1);
        return IsAtomicFailure(
            largeLimit,
            OwnedX64GdtEncodingStatus.InvalidTssLimit,
            destination) &&
            !OwnedX64GdtContainingNullN64BitCode.IsCanonicalX64Address(
                0x0000_8000_0000_0000UL) &&
            !OwnedX64GdtContainingNullN64BitCode.IsCanonicalX64Address(
                0xffff_7fff_ffff_ffffUL);
    }

    private static bool TestReadBackDetectsCorruption()
    {
        Span<byte> destination = stackalloc byte[
            OwnedX64GdtContainingNullN64BitCode.RequiredByteCount];
        OwnedX64GdtEncodingResult encoded =
            OwnedX64GdtContainingNullN64BitCode.Encode(
                destination,
                TssBaseAddress,
                TssLimit);
        if (!encoded.Succeeded)
        {
            return false;
        }

        const int CorruptOffset =
            OwnedX64GdtContainingNullN64BitCode.KernelCodeDescriptorOffset + 5;
        destination[CorruptOffset] ^= 0x01;
        OwnedX64GdtVerificationResult corrupt =
            OwnedX64GdtContainingNullN64BitCode.Verify(
                destination,
                TssBaseAddress,
                TssLimit);

        destination[CorruptOffset] ^= 0x01;
        OwnedX64GdtVerificationResult restored =
            OwnedX64GdtContainingNullN64BitCode.Verify(
                destination,
                TssBaseAddress,
                TssLimit);

        return !corrupt.Succeeded &&
            corrupt.IsInitialized &&
            corrupt.Status == OwnedX64GdtVerificationStatus.ByteMismatch &&
            corrupt.BytesCompared == CorruptOffset + 1 &&
            corrupt.FirstMismatchOffset == CorruptOffset &&
            corrupt.ObservedFingerprint != corrupt.ExpectedFingerprint &&
            corrupt.ExpectedFingerprint == ExactFingerprint &&
            restored.Succeeded &&
            restored.ObservedFingerprint == ExactFingerprint;
    }

    private static bool TestVerificationRejectsInvalidMetadata()
    {
        byte* source = stackalloc byte[
            OwnedX64GdtContainingNullN64BitCode.RequiredByteCount];
        Fill(source, OwnedX64GdtContainingNullN64BitCode.RequiredByteCount, 0);

        OwnedX64GdtVerificationResult negativeLength =
            OwnedX64GdtContainingNullN64BitCode.Verify(
                source,
                -1,
                TssBaseAddress,
                TssLimit);
        OwnedX64GdtVerificationResult shortSource =
            OwnedX64GdtContainingNullN64BitCode.Verify(
                source,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount - 1,
                TssBaseAddress,
                TssLimit);
        OwnedX64GdtVerificationResult nullSource =
            OwnedX64GdtContainingNullN64BitCode.Verify(
                null,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                TssBaseAddress,
                TssLimit);
        OwnedX64GdtVerificationResult nullTss =
            OwnedX64GdtContainingNullN64BitCode.Verify(
                source,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                0,
                TssLimit);
        OwnedX64GdtVerificationResult nonCanonicalTss =
            OwnedX64GdtContainingNullN64BitCode.Verify(
                source,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                0xffff_7fff_ffff_ffffUL,
                TssLimit);
        OwnedX64GdtVerificationResult invalidLimit =
            OwnedX64GdtContainingNullN64BitCode.Verify(
                source,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                TssBaseAddress,
                TssLimit - 1);

        return !negativeLength.Succeeded &&
            negativeLength.Status == OwnedX64GdtVerificationStatus.InvalidSource &&
            !shortSource.Succeeded &&
            shortSource.Status == OwnedX64GdtVerificationStatus.SourceTooSmall &&
            !nullSource.Succeeded &&
            nullSource.Status == OwnedX64GdtVerificationStatus.InvalidSource &&
            !nullTss.Succeeded &&
            nullTss.Status == OwnedX64GdtVerificationStatus.NullTssBaseAddress &&
            !nonCanonicalTss.Succeeded &&
            nonCanonicalTss.Status == OwnedX64GdtVerificationStatus.NonCanonicalTssBaseAddress &&
            !invalidLimit.Succeeded &&
            invalidLimit.Status == OwnedX64GdtVerificationStatus.InvalidTssLimit;
    }

    private static bool TestPointerHotPathDoesNotAllocate()
    {
        byte* destination = stackalloc byte[
            OwnedX64GdtContainingNullN64BitCode.RequiredByteCount];

        for (int iteration = 0; iteration < 16; iteration++)
        {
            OwnedX64GdtEncodingResult warmup =
                OwnedX64GdtContainingNullN64BitCode.Encode(
                    destination,
                    OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                    TssBaseAddress,
                    TssLimit);
            if (!warmup.Succeeded)
            {
                return false;
            }
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 1024; iteration++)
        {
            OwnedX64GdtEncodingResult result =
                OwnedX64GdtContainingNullN64BitCode.Encode(
                    destination,
                    OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                    TssBaseAddress,
                    TssLimit);
            if (!result.Succeeded)
            {
                return false;
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return after == before;
    }

    private static bool TestDefaultsAndSourceBoundary()
    {
        OwnedX64GdtEncodingResult encoding = default;
        OwnedX64GdtVerificationResult verification = default;
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Diagnostics",
            "OwnedX64GdtContainingNullN64BitCode.cs"));

        return !encoding.IsInitialized &&
            !encoding.Succeeded &&
            encoding.Status == OwnedX64GdtEncodingStatus.Success &&
            encoding.BytesWritten == 0 &&
            encoding.RequiredBytes == 0 &&
            !encoding.DestinationMutated &&
            !encoding.Verification.IsInitialized &&
            !verification.IsInitialized &&
            !verification.Succeeded &&
            verification.Status == OwnedX64GdtVerificationStatus.Success &&
            verification.BytesCompared == 0 &&
            verification.FirstMismatchOffset == 0 &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedX64GdtEncodingResult>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<OwnedX64GdtVerificationResult>() &&
            source.Contains("#if !NOSTDLIB_EFI", StringComparison.Ordinal) &&
            source.Contains("byte* destination", StringComparison.Ordinal) &&
            source.Contains("byte* source", StringComparison.Ordinal) &&
            source.Contains("ReadBackMismatch", StringComparison.Ordinal) &&
            source.Contains("IsCanonicalX64Address", StringComparison.Ordinal) &&
            !source.Contains("GdtDescriptorPlan", StringComparison.Ordinal) &&
            !source.Contains("SharpOS", StringComparison.Ordinal) &&
            !source.Contains("throw", StringComparison.Ordinal) &&
            !source.Contains("new byte[", StringComparison.Ordinal) &&
            !source.Contains("Console", StringComparison.Ordinal);
    }

    private static bool IsAtomicFailure(
        OwnedX64GdtEncodingResult result,
        OwnedX64GdtEncodingStatus expectedStatus,
        byte* destination)
    {
        return result.IsInitialized &&
            !result.Succeeded &&
            result.Status == expectedStatus &&
            result.BytesWritten == 0 &&
            result.RequiredBytes == OwnedX64GdtContainingNullN64BitCode.RequiredByteCount &&
            result.TableLimit == OwnedX64GdtContainingNullN64BitCode.TableLimit &&
            result.Fingerprint == 0 &&
            !result.DestinationMutated &&
            !result.Verification.IsInitialized &&
            IsFilled(
                destination,
                OwnedX64GdtContainingNullN64BitCode.RequiredByteCount,
                0xa5);
    }

    private static void Fill(byte* destination, int length, byte value)
    {
        for (int index = 0; index < length; index++)
        {
            destination[index] = value;
        }
    }

    private static bool IsFilled(byte* source, int length, byte value)
    {
        for (int index = 0; index < length; index++)
        {
            if (source[index] != value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsFilled(ReadOnlySpan<byte> source, byte value)
    {
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] != value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool BytesEqual(byte* first, byte* second, int length)
    {
        for (int index = 0; index < length; index++)
        {
            if (first[index] != second[index])
            {
                return false;
            }
        }

        return true;
    }
}
