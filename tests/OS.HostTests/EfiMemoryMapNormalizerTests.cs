using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Std.Binary;

internal static unsafe class EfiMemoryMapNormalizerTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("efi.memorymap.normalizer summarizes owned descriptors", TestSummarizesOwnedDescriptors, ref failures);
        Run("efi.memorymap.normalizer rejects invalid metadata", TestRejectsInvalidMetadata, ref failures);
        Run("efi.memorymap.normalizer rejects descriptor arithmetic issues", TestRejectsDescriptorArithmeticIssues, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("EFI memory-map normalizer host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestSummarizesOwnedDescriptors()
    {
        byte[] source = new byte[96];
        WriteDescriptor(
            source.AsSpan(0, 40),
            type: 7,
            physicalStart: 0x100000,
            numberOfPages: 4);
        WriteDescriptor(
            source.AsSpan(48, 40),
            type: 4,
            physicalStart: 0x200000,
            numberOfPages: 2);

        fixed (byte* sourcePointer = source)
        {
            EfiMemoryMapNormalizeResult result = EfiMemoryMapNormalizer.Normalize(
                sourcePointer,
                sourceByteCount: 96,
                descriptorSize: 48,
                descriptorVersion: 1,
                descriptorCount: 2);

            return result.Succeeded
                && result.Status == EfiMemoryMapNormalizeStatus.Success
                && result.RequiredSourceBytes == 96
                && result.Summary.DescriptorCount == 2
                && result.Summary.TotalPages == 6
                && result.Summary.TotalBytes == 6 * EfiMemoryMapNormalizer.PageSize
                && result.Summary.ConventionalPages == 4
                && result.Summary.LowestPhysicalAddress == 0x100000
                && result.Summary.HighestPhysicalEndExclusive == 0x202000;
        }
    }

    private static bool TestRejectsInvalidMetadata()
    {
        byte[] source = new byte[48];
        WriteDescriptor(
            source.AsSpan(0, 40),
            type: 7,
            physicalStart: 0x100000,
            numberOfPages: 1);

        fixed (byte* sourcePointer = source)
        {
            EfiMemoryMapNormalizeResult missingSource = EfiMemoryMapNormalizer.Normalize(
                null,
                sourceByteCount: 0,
                descriptorSize: 48,
                descriptorVersion: 1,
                descriptorCount: 1);

            EfiMemoryMapNormalizeResult invalidSize = EfiMemoryMapNormalizer.Normalize(
                sourcePointer,
                sourceByteCount: 48,
                descriptorSize: 56,
                descriptorVersion: 1,
                descriptorCount: 1);

            EfiMemoryMapNormalizeResult invalidVersion = EfiMemoryMapNormalizer.Normalize(
                sourcePointer,
                sourceByteCount: 48,
                descriptorSize: 48,
                descriptorVersion: 0,
                descriptorCount: 1);

            EfiMemoryMapNormalizeResult invalidCount = EfiMemoryMapNormalizer.Normalize(
                sourcePointer,
                sourceByteCount: 48,
                descriptorSize: 48,
                descriptorVersion: 1,
                descriptorCount: 0);

            EfiMemoryMapNormalizeResult sourceTooSmall = EfiMemoryMapNormalizer.Normalize(
                sourcePointer,
                sourceByteCount: 47,
                descriptorSize: 48,
                descriptorVersion: 1,
                descriptorCount: 1);

            return IsStatus(missingSource, EfiMemoryMapNormalizeStatus.MissingSource)
                && IsStatus(invalidSize, EfiMemoryMapNormalizeStatus.InvalidDescriptorSize)
                && IsStatus(invalidVersion, EfiMemoryMapNormalizeStatus.InvalidDescriptorVersion)
                && IsStatus(invalidCount, EfiMemoryMapNormalizeStatus.InvalidDescriptorCount)
                && sourceTooSmall.Status == EfiMemoryMapNormalizeStatus.SourceTooSmall
                && sourceTooSmall.RequiredSourceBytes == 48;
        }
    }

    private static bool TestRejectsDescriptorArithmeticIssues()
    {
        byte[] zeroPagesSource = new byte[48];
        WriteDescriptor(
            zeroPagesSource.AsSpan(0, 40),
            type: 7,
            physicalStart: 0x100000,
            numberOfPages: 0);

        byte[] pageOverflowSource = new byte[48];
        WriteDescriptor(
            pageOverflowSource.AsSpan(0, 40),
            type: 7,
            physicalStart: 0x100000,
            numberOfPages: (EfiMemoryMapNormalizer.MaximumUnsignedLong / EfiMemoryMapNormalizer.PageSize) + 1);

        byte[] addressOverflowSource = new byte[48];
        WriteDescriptor(
            addressOverflowSource.AsSpan(0, 40),
            type: 7,
            physicalStart: EfiMemoryMapNormalizer.MaximumUnsignedLong - 1024,
            numberOfPages: 1);

        fixed (byte* zeroPagesPointer = zeroPagesSource)
        fixed (byte* pageOverflowPointer = pageOverflowSource)
        fixed (byte* addressOverflowPointer = addressOverflowSource)
        {
            EfiMemoryMapNormalizeResult zeroPages = EfiMemoryMapNormalizer.Normalize(
                zeroPagesPointer,
                sourceByteCount: 48,
                descriptorSize: 48,
                descriptorVersion: 1,
                descriptorCount: 1);

            EfiMemoryMapNormalizeResult pageOverflow = EfiMemoryMapNormalizer.Normalize(
                pageOverflowPointer,
                sourceByteCount: 48,
                descriptorSize: 48,
                descriptorVersion: 1,
                descriptorCount: 1);

            EfiMemoryMapNormalizeResult addressOverflow = EfiMemoryMapNormalizer.Normalize(
                addressOverflowPointer,
                sourceByteCount: 48,
                descriptorSize: 48,
                descriptorVersion: 1,
                descriptorCount: 1);

            return zeroPages.Status == EfiMemoryMapNormalizeStatus.InvalidPageCount
                && zeroPages.InvalidDescriptorIndex == 0
                && pageOverflow.Status == EfiMemoryMapNormalizeStatus.PageByteCountOverflow
                && addressOverflow.Status == EfiMemoryMapNormalizeStatus.AddressRangeOverflow;
        }
    }

    private static bool IsStatus(
        EfiMemoryMapNormalizeResult result,
        EfiMemoryMapNormalizeStatus status)
    {
        return !result.Succeeded && result.Status == status;
    }

    private static void WriteDescriptor(
        Span<byte> destination,
        uint type,
        ulong physicalStart,
        ulong numberOfPages)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), type);
        destination.Slice(4, 4).Clear();
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(8, 8), physicalStart);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(16, 8), 0);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(24, 8), numberOfPages);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(32, 8), 0);
    }
}
