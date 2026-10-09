using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Std.Binary;

internal static class BootMemoryRegionMaterializerTests
{

    [HostTest("boot.memory regions materializes descriptors", HostLane.Fast, 0)]
    private static bool TestMaterializesDescriptors()
    {
        Span<byte> descriptors = stackalloc byte[(int)BootMemoryRegionMaterializer.DescriptorStride48 * 3];
        WriteDescriptor(
            descriptors,
            0,
            UefiMemoryType.ConventionalMemory,
            physicalStart: 0x100000,
            virtualStart: 0,
            pageCount: 6,
            attribute: 0x8);
        WriteDescriptor(
            descriptors,
            1,
            UefiMemoryType.BootServicesData,
            physicalStart: 0x200000,
            virtualStart: 0,
            pageCount: 2,
            attribute: 0x10);
        WriteDescriptor(
            descriptors,
            2,
            UefiMemoryType.RuntimeServicesCode,
            physicalStart: 0x300000,
            virtualStart: 0xFFFF800000300000,
            pageCount: 3,
            attribute: 0x20);

        Span<BootMemoryRegionRecord> regions = stackalloc BootMemoryRegionRecord[3];
        BootMemoryRegionMaterializationResult result = BootMemoryRegionMaterializer.Materialize(
            descriptors,
            BootMemoryRegionMaterializer.DescriptorStride48,
            descriptorVersion: 1,
            descriptorCount: 3,
            regions);

        return HostCheck.Result(result.Succeeded
            && result.Status == BootMemoryRegionMaterializationStatus.Success
            && result.DescriptorCount == 3
            && result.RegionCount == 3
            && result.RequiredRegionCapacity == 3
            && result.AvailableRegionCapacity == 3
            && result.TotalPages == 11
            && result.TotalBytes == 11 * BootMemoryRegionRecord.PageSize
            && result.ConventionalMemoryPages == 6
            && result.ConventionalMemoryBytes == 6 * BootMemoryRegionRecord.PageSize
            && regions[0].UefiType == (uint)UefiMemoryType.ConventionalMemory
            && regions[0].PhysicalStart == 0x100000
            && regions[0].PageCount == 6
            && regions[0].ByteLength == 6 * BootMemoryRegionRecord.PageSize
            && regions[0].PhysicalEndExclusive == 0x100000 + (6 * BootMemoryRegionRecord.PageSize)
            && regions[1].UefiType == (uint)UefiMemoryType.BootServicesData
            && regions[1].Attribute == 0x10
            && regions[2].UefiType == (uint)UefiMemoryType.RuntimeServicesCode
            && regions[2].VirtualStart == 0xFFFF800000300000);
    }

    [HostTest("boot.memory regions reports destination capacity", HostLane.Fast, 1)]
    private static bool TestReportsDestinationCapacity()
    {
        Span<byte> descriptors = stackalloc byte[(int)BootMemoryRegionMaterializer.DescriptorStride40 * 2];
        WriteDescriptor(
            descriptors,
            0,
            UefiMemoryType.ConventionalMemory,
            physicalStart: 0x400000,
            virtualStart: 0,
            pageCount: 1,
            attribute: 0);
        WriteDescriptor(
            descriptors,
            1,
            UefiMemoryType.LoaderData,
            physicalStart: 0x500000,
            virtualStart: 0,
            pageCount: 1,
            attribute: 0);

        BootMemoryRegionRecord sentinel = new BootMemoryRegionRecord(
            (uint)UefiMemoryType.PersistentMemory,
            physicalStart: 0x600000,
            virtualStart: 0,
            pageCount: 4,
            attribute: 0x44,
            byteLength: 4 * BootMemoryRegionRecord.PageSize,
            physicalEndExclusive: 0x600000 + (4 * BootMemoryRegionRecord.PageSize));
        Span<BootMemoryRegionRecord> regions = stackalloc BootMemoryRegionRecord[1];
        regions[0] = sentinel;

        BootMemoryRegionMaterializationResult result = BootMemoryRegionMaterializer.Materialize(
            descriptors,
            BootMemoryRegionMaterializer.DescriptorStride40,
            descriptorVersion: 1,
            descriptorCount: 2,
            regions);

        return HostCheck.Result(!result.Succeeded
            && result.Status == BootMemoryRegionMaterializationStatus.DestinationTooSmall
            && result.DescriptorCount == 2
            && result.RegionCount == 0
            && result.RequiredRegionCapacity == 2
            && result.AvailableRegionCapacity == 1
            && regions[0].UefiType == sentinel.UefiType
            && regions[0].PhysicalStart == sentinel.PhysicalStart
            && regions[0].PageCount == sentinel.PageCount
            && regions[0].Attribute == sentinel.Attribute);
    }

    [HostTest("boot.memory regions rejects invalid descriptors", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidDescriptors()
    {
        Span<byte> zeroPages = stackalloc byte[(int)BootMemoryRegionMaterializer.DescriptorStride40];
        WriteDescriptor(
            zeroPages,
            0,
            UefiMemoryType.ConventionalMemory,
            physicalStart: 0x700000,
            virtualStart: 0,
            pageCount: 0,
            attribute: 0);

        Span<byte> overflowingAddress = stackalloc byte[(int)BootMemoryRegionMaterializer.DescriptorStride40];
        WriteDescriptor(
            overflowingAddress,
            0,
            UefiMemoryType.ConventionalMemory,
            physicalStart: ulong.MaxValue - 10,
            virtualStart: 0,
            pageCount: 1,
            attribute: 0);

        Span<BootMemoryRegionRecord> regions = stackalloc BootMemoryRegionRecord[1];
        BootMemoryRegionMaterializationResult zeroPagesResult = BootMemoryRegionMaterializer.Materialize(
            zeroPages,
            BootMemoryRegionMaterializer.DescriptorStride40,
            descriptorVersion: 1,
            descriptorCount: 1,
            regions);
        BootMemoryRegionMaterializationResult overflowingAddressResult = BootMemoryRegionMaterializer.Materialize(
            overflowingAddress,
            BootMemoryRegionMaterializer.DescriptorStride40,
            descriptorVersion: 1,
            descriptorCount: 1,
            regions);

        return HostCheck.Result(!zeroPagesResult.Succeeded
            && zeroPagesResult.Status == BootMemoryRegionMaterializationStatus.InvalidPageCount
            && zeroPagesResult.InvalidDescriptorIndex == 0
            && !overflowingAddressResult.Succeeded
            && overflowingAddressResult.Status == BootMemoryRegionMaterializationStatus.AddressRangeOverflow
            && overflowingAddressResult.InvalidDescriptorIndex == 0);
    }

    [HostTest("boot.memory regions rejects invalid metadata", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidMetadata()
    {
        Span<byte> descriptors = stackalloc byte[(int)BootMemoryRegionMaterializer.DescriptorStride40];
        WriteDescriptor(
            descriptors,
            0,
            UefiMemoryType.ConventionalMemory,
            physicalStart: 0x800000,
            virtualStart: 0,
            pageCount: 1,
            attribute: 0);
        Span<BootMemoryRegionRecord> regions = stackalloc BootMemoryRegionRecord[1];

        BootMemoryRegionMaterializationResult invalidSize = BootMemoryRegionMaterializer.Materialize(
            descriptors,
            descriptorSize: 32,
            descriptorVersion: 1,
            descriptorCount: 1,
            regions);
        BootMemoryRegionMaterializationResult invalidVersion = BootMemoryRegionMaterializer.Materialize(
            descriptors,
            BootMemoryRegionMaterializer.DescriptorStride40,
            descriptorVersion: 0,
            descriptorCount: 1,
            regions);
        BootMemoryRegionMaterializationResult sourceTooSmall = BootMemoryRegionMaterializer.Materialize(
            descriptors[..32],
            BootMemoryRegionMaterializer.DescriptorStride40,
            descriptorVersion: 1,
            descriptorCount: 1,
            regions);

        return HostCheck.Result(!invalidSize.Succeeded
            && invalidSize.Status == BootMemoryRegionMaterializationStatus.InvalidDescriptorSize
            && !invalidVersion.Succeeded
            && invalidVersion.Status == BootMemoryRegionMaterializationStatus.InvalidDescriptorVersion
            && !sourceTooSmall.Succeeded
            && sourceTooSmall.Status == BootMemoryRegionMaterializationStatus.SourceTooSmall);
    }

    private static void WriteDescriptor(
        Span<byte> descriptors,
        int descriptorIndex,
        UefiMemoryType type,
        ulong physicalStart,
        ulong virtualStart,
        ulong pageCount,
        ulong attribute)
    {
        int stride = descriptors.Length % (int)BootMemoryRegionMaterializer.DescriptorStride48 == 0
            ? (int)BootMemoryRegionMaterializer.DescriptorStride48
            : (int)BootMemoryRegionMaterializer.DescriptorStride40;
        Span<byte> descriptor = descriptors.Slice(descriptorIndex * stride, 40);

        EndianPrimitives.WriteUInt32LittleEndian(descriptor[..4], (uint)type);
        EndianPrimitives.WriteUInt32LittleEndian(descriptor.Slice(4, 4), 0);
        EndianPrimitives.WriteUInt64LittleEndian(descriptor.Slice(8, 8), physicalStart);
        EndianPrimitives.WriteUInt64LittleEndian(descriptor.Slice(16, 8), virtualStart);
        EndianPrimitives.WriteUInt64LittleEndian(descriptor.Slice(24, 8), pageCount);
        EndianPrimitives.WriteUInt64LittleEndian(descriptor.Slice(32, 8), attribute);
    }
}
