using OS.Std.Binary;

namespace OS.Boot;

public static class UefiMemoryDescriptorReader
{
    public const ulong DescriptorLayoutSize = 40;
    public const ulong DescriptorStride40 = 40;
    public const ulong DescriptorStride48 = 48;
    public static UefiMemoryDescriptorReaderResult Read(ReadOnlySpan<byte> source, ulong descriptorSize, uint descriptorVersion, ulong descriptorCount, Span<UefiMemoryDescriptor> destination)
    {
        if (source.Length == 0)
        {
            return UefiMemoryDescriptorReaderResult.Fail(UefiMemoryDescriptorReaderStatus.MissingSource);
        }

        if (!IsSupportedDescriptorSize(descriptorSize))
        {
            return UefiMemoryDescriptorReaderResult.Fail(UefiMemoryDescriptorReaderStatus.InvalidDescriptorSize);
        }

        if (descriptorVersion == 0)
        {
            return UefiMemoryDescriptorReaderResult.Fail(UefiMemoryDescriptorReaderStatus.InvalidDescriptorVersion);
        }

        if (descriptorCount == 0)
        {
            return UefiMemoryDescriptorReaderResult.Fail(UefiMemoryDescriptorReaderStatus.InvalidDescriptorCount);
        }

        if (descriptorCount > ulong.MaxValue / descriptorSize)
        {
            return UefiMemoryDescriptorReaderResult.Fail(UefiMemoryDescriptorReaderStatus.SizeOverflow);
        }

        if (descriptorCount > int.MaxValue)
        {
            return UefiMemoryDescriptorReaderResult.Fail(UefiMemoryDescriptorReaderStatus.InvalidDescriptorCount);
        }

        ulong requiredSourceBytes = descriptorCount * descriptorSize;
        if ((ulong)source.Length < requiredSourceBytes)
        {
            return UefiMemoryDescriptorReaderResult.Fail(UefiMemoryDescriptorReaderStatus.SourceTooSmall, requiredSourceBytes, descriptorCount);
        }

        if ((ulong)destination.Length < descriptorCount)
        {
            return UefiMemoryDescriptorReaderResult.Fail(UefiMemoryDescriptorReaderStatus.DestinationTooSmall, requiredSourceBytes, descriptorCount);
        }

        int count = (int)descriptorCount;
        int stride = (int)descriptorSize;
        for (int descriptorIndex = 0; descriptorIndex < count; descriptorIndex++)
        {
            int offset = descriptorIndex * stride;
            destination[descriptorIndex] = ReadDescriptor(source.Slice(offset, (int)DescriptorLayoutSize));
        }

        return UefiMemoryDescriptorReaderResult.Success(count, requiredSourceBytes);
    }

    private static bool IsSupportedDescriptorSize(ulong descriptorSize)
    {
        return descriptorSize == DescriptorStride40 || descriptorSize == DescriptorStride48;
    }

    private static UefiMemoryDescriptor ReadDescriptor(ReadOnlySpan<byte> descriptor)
    {
        uint type = EndianPrimitives.ReadUInt32LittleEndian(descriptor.Slice(0, 4));
        ulong physicalStart = EndianPrimitives.ReadUInt64LittleEndian(descriptor.Slice(8, 8));
        ulong virtualStart = EndianPrimitives.ReadUInt64LittleEndian(descriptor.Slice(16, 8));
        ulong numberOfPages = EndianPrimitives.ReadUInt64LittleEndian(descriptor.Slice(24, 8));
        ulong attribute = EndianPrimitives.ReadUInt64LittleEndian(descriptor.Slice(32, 8));
        return new UefiMemoryDescriptor((UefiMemoryType)type, physicalStart, virtualStart, numberOfPages, attribute);
    }
}
