using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Std.Binary;

internal static class UefiMemoryDescriptorReaderTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("uefi.descriptor.reader decodes 40-byte stride", TestDecodesFortyByteStride, ref failures);
        Run("uefi.descriptor.reader decodes 48-byte stride", TestDecodesFortyEightByteStride, ref failures);
        Run("uefi.descriptor.reader rejects invalid metadata", TestRejectsInvalidMetadata, ref failures);
        Run("uefi.descriptor.reader reports source and destination capacity", TestReportsCapacityFailures, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("UEFI descriptor reader host tests failed: " + failures);
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

    private static bool TestDecodesFortyByteStride()
    {
        byte[] source = new byte[80];
        WriteDescriptor(
            source.AsSpan(0, 40),
            UefiMemoryType.BootServicesData,
            physicalStart: 0x100000,
            virtualStart: 0,
            numberOfPages: 7,
            attribute: 0x8);
        WriteDescriptor(
            source.AsSpan(40, 40),
            UefiMemoryType.ConventionalMemory,
            physicalStart: 0x200000,
            virtualStart: 0xFFFF800000200000,
            numberOfPages: 512,
            attribute: 0xF);

        Span<UefiMemoryDescriptor> destination = stackalloc UefiMemoryDescriptor[2];
        UefiMemoryDescriptorReaderResult result = UefiMemoryDescriptorReader.Read(
            source,
            descriptorSize: UefiMemoryDescriptorReader.DescriptorStride40,
            descriptorVersion: 1,
            descriptorCount: 2,
            destination);

        return result.Succeeded
            && result.Status == UefiMemoryDescriptorReaderStatus.Success
            && result.DescriptorsWritten == 2
            && result.RequiredSourceBytes == 80
            && result.RequiredDestinationCount == 2
            && destination[0].Type == UefiMemoryType.BootServicesData
            && destination[0].PhysicalStart == 0x100000
            && destination[0].VirtualStart == 0
            && destination[0].NumberOfPages == 7
            && destination[0].Attribute == 0x8
            && destination[1].Type == UefiMemoryType.ConventionalMemory
            && destination[1].PhysicalStart == 0x200000
            && destination[1].VirtualStart == 0xFFFF800000200000
            && destination[1].NumberOfPages == 512
            && destination[1].Attribute == 0xF;
    }

    private static bool TestDecodesFortyEightByteStride()
    {
        byte[] source = new byte[96];
        WriteDescriptor(
            source.AsSpan(0, 40),
            UefiMemoryType.RuntimeServicesCode,
            physicalStart: 0x300000,
            virtualStart: 0xFFFF800000300000,
            numberOfPages: 3,
            attribute: 0x800000000000000F);
        FillPadding(source.AsSpan(40, 8), 0xAA);
        WriteDescriptor(
            source.AsSpan(48, 40),
            UefiMemoryType.AcpiReclaimMemory,
            physicalStart: 0x400000,
            virtualStart: 0,
            numberOfPages: 1,
            attribute: 0x1);
        FillPadding(source.AsSpan(88, 8), 0xCC);

        Span<UefiMemoryDescriptor> destination = stackalloc UefiMemoryDescriptor[3];
        destination[2] = new UefiMemoryDescriptor(
            UefiMemoryType.PersistentMemory,
            0xDEADBEEF,
            0,
            9,
            0x99);

        UefiMemoryDescriptorReaderResult result = UefiMemoryDescriptorReader.Read(
            source,
            descriptorSize: UefiMemoryDescriptorReader.DescriptorStride48,
            descriptorVersion: 2,
            descriptorCount: 2,
            destination);

        return result.Succeeded
            && result.DescriptorsWritten == 2
            && result.RequiredSourceBytes == 96
            && destination[0].Type == UefiMemoryType.RuntimeServicesCode
            && destination[0].PhysicalStart == 0x300000
            && destination[0].VirtualStart == 0xFFFF800000300000
            && destination[0].NumberOfPages == 3
            && destination[0].Attribute == 0x800000000000000F
            && destination[1].Type == UefiMemoryType.AcpiReclaimMemory
            && destination[1].PhysicalStart == 0x400000
            && destination[1].VirtualStart == 0
            && destination[1].NumberOfPages == 1
            && destination[1].Attribute == 0x1
            && destination[2].Type == UefiMemoryType.PersistentMemory
            && destination[2].PhysicalStart == 0xDEADBEEF;
    }

    private static bool TestRejectsInvalidMetadata()
    {
        byte[] source = new byte[48];
        Span<UefiMemoryDescriptor> destination = stackalloc UefiMemoryDescriptor[1];

        UefiMemoryDescriptorReaderResult missingSource = UefiMemoryDescriptorReader.Read(
            ReadOnlySpan<byte>.Empty,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorCount: 1,
            destination);

        UefiMemoryDescriptorReaderResult invalidSize = UefiMemoryDescriptorReader.Read(
            source,
            descriptorSize: 56,
            descriptorVersion: 1,
            descriptorCount: 1,
            destination);

        UefiMemoryDescriptorReaderResult invalidVersion = UefiMemoryDescriptorReader.Read(
            source,
            descriptorSize: 48,
            descriptorVersion: 0,
            descriptorCount: 1,
            destination);

        UefiMemoryDescriptorReaderResult zeroCount = UefiMemoryDescriptorReader.Read(
            source,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorCount: 0,
            destination);

        UefiMemoryDescriptorReaderResult countTooLarge = UefiMemoryDescriptorReader.Read(
            source,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorCount: (ulong)int.MaxValue + 1,
            destination);

        UefiMemoryDescriptorReaderResult sizeOverflow = UefiMemoryDescriptorReader.Read(
            source,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorCount: (ulong.MaxValue / 48) + 1,
            destination);

        return IsStatus(missingSource, UefiMemoryDescriptorReaderStatus.MissingSource)
            && IsStatus(invalidSize, UefiMemoryDescriptorReaderStatus.InvalidDescriptorSize)
            && IsStatus(invalidVersion, UefiMemoryDescriptorReaderStatus.InvalidDescriptorVersion)
            && IsStatus(zeroCount, UefiMemoryDescriptorReaderStatus.InvalidDescriptorCount)
            && IsStatus(countTooLarge, UefiMemoryDescriptorReaderStatus.InvalidDescriptorCount)
            && IsStatus(sizeOverflow, UefiMemoryDescriptorReaderStatus.SizeOverflow);
    }

    private static bool TestReportsCapacityFailures()
    {
        byte[] source = new byte[48];
        WriteDescriptor(
            source.AsSpan(0, 40),
            UefiMemoryType.LoaderData,
            physicalStart: 0x500000,
            virtualStart: 0,
            numberOfPages: 2,
            attribute: 0);

        Span<UefiMemoryDescriptor> destination = stackalloc UefiMemoryDescriptor[1];
        UefiMemoryDescriptorReaderResult sourceTooSmall = UefiMemoryDescriptorReader.Read(
            source.AsSpan(0, 47),
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorCount: 1,
            destination);

        UefiMemoryDescriptorReaderResult destinationTooSmall = UefiMemoryDescriptorReader.Read(
            source,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorCount: 1,
            Span<UefiMemoryDescriptor>.Empty);

        return !sourceTooSmall.Succeeded
            && sourceTooSmall.Status == UefiMemoryDescriptorReaderStatus.SourceTooSmall
            && sourceTooSmall.RequiredSourceBytes == 48
            && sourceTooSmall.RequiredDestinationCount == 1
            && !destinationTooSmall.Succeeded
            && destinationTooSmall.Status == UefiMemoryDescriptorReaderStatus.DestinationTooSmall
            && destinationTooSmall.RequiredSourceBytes == 48
            && destinationTooSmall.RequiredDestinationCount == 1;
    }

    private static bool IsStatus(
        UefiMemoryDescriptorReaderResult result,
        UefiMemoryDescriptorReaderStatus expectedStatus)
    {
        return !result.Succeeded
            && result.Status == expectedStatus
            && result.DescriptorsWritten == 0;
    }

    private static void WriteDescriptor(
        Span<byte> destination,
        UefiMemoryType type,
        ulong physicalStart,
        ulong virtualStart,
        ulong numberOfPages,
        ulong attribute)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), (uint)type);
        destination.Slice(4, 4).Clear();
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(8, 8), physicalStart);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(16, 8), virtualStart);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(24, 8), numberOfPages);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(32, 8), attribute);
    }

    private static void FillPadding(Span<byte> destination, byte value)
    {
        for (int index = 0; index < destination.Length; index++)
        {
            destination[index] = value;
        }
    }
}
