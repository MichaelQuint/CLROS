using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Memory;

internal static class MemoryMapInfoTests
{

    [HostTest("memory.mapinfo converts UEFI descriptors", HostLane.Fast, 0)]
    private static bool TestConvertsUefiDescriptors()
    {
        UefiMemoryDescriptor[] descriptors =
        [
            new UefiMemoryDescriptor(
                UefiMemoryType.BootServicesData,
                0x100000,
                0,
                3,
                0x8),
            new UefiMemoryDescriptor(
                UefiMemoryType.RuntimeServicesCode,
                0x200000,
                0xFFFF800000200000,
                5,
                0xF)
        ];

        UefiMemoryMapResult snapshotResult = UefiMemoryMapResult.Captured(
            mapKey: 0x1234,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorEntries: descriptors);

        MemoryMapInfoResult result = MemoryMapInfoResult.FromUefiSnapshot(snapshotResult.Snapshot);

        bool firstFound = result.Info.TryGetRegion(0, out MemoryRegion first);
        bool secondFound = result.Info.TryGetRegion(1, out MemoryRegion second);
        bool outOfRangeFails = !result.Info.TryGetRegion(2, out _);
        bool negativeIndexFails = !result.Info.TryGetRegion(-1, out _);

        return HostCheck.Result(snapshotResult.Succeeded
            && result.Succeeded
            && result.Status == MemoryMapInfoStatus.Success
            && result.InvalidDescriptorIndex == -1
            && result.Message.Length == 0
            && result.Info.MapKey == 0x1234
            && result.Info.DescriptorSize == 48
            && result.Info.DescriptorVersion == 1
            && result.Info.DescriptorCount == 2
            && result.Info.RegionCount == 2
            && result.Info.Regions.Length == 2
            && result.Info.HasRegions
            && firstFound
            && first.Type == MemoryRegionType.BootServicesData
            && first.UefiType == UefiMemoryType.BootServicesData
            && first.UefiTypeValue == (uint)UefiMemoryType.BootServicesData
            && first.PhysicalStart == 0x100000
            && first.VirtualStart == 0
            && !first.HasVirtualAddress
            && first.PageCount == 3
            && first.ByteLength == 3 * MemoryRegion.PageSize
            && first.PhysicalEndExclusive == 0x100000 + (3 * MemoryRegion.PageSize)
            && first.Attribute == 0x8
            && secondFound
            && second.Type == MemoryRegionType.RuntimeServicesCode
            && second.UefiType == UefiMemoryType.RuntimeServicesCode
            && second.VirtualStart == 0xFFFF800000200000
            && second.HasVirtualAddress
            && second.PageCount == 5
            && second.Attribute == 0xF
            && outOfRangeFails
            && negativeIndexFails);
    }

    [HostTest("memory.mapinfo rejects invalid descriptor size", HostLane.Fast, 1)]
    private static bool TestInvalidDescriptorSize()
    {
        MemoryMapInfoResult result = MemoryMapInfoResult.FromUefiDescriptors(
            mapKey: 0x1,
            descriptorSize: MemoryMapInfoResult.MinimumUefiDescriptorSize - 1,
            descriptorVersion: 1,
            descriptorCount: 1,
            descriptorEntries: CreateSingleDescriptor());

        return HostCheck.Result(!result.Succeeded
            && result.Status == MemoryMapInfoStatus.InvalidDescriptorSize
            && result.Info.RegionCount == 0
            && result.InvalidDescriptorIndex == -1
            && result.Message.Contains("descriptor size", StringComparison.Ordinal));
    }

    [HostTest("memory.mapinfo rejects invalid descriptor version", HostLane.Fast, 2)]
    private static bool TestInvalidDescriptorVersion()
    {
        MemoryMapInfoResult result = MemoryMapInfoResult.FromUefiDescriptors(
            mapKey: 0x1,
            descriptorSize: MemoryMapInfoResult.MinimumUefiDescriptorSize,
            descriptorVersion: 0,
            descriptorCount: 1,
            descriptorEntries: CreateSingleDescriptor());

        return HostCheck.Result(!result.Succeeded
            && result.Status == MemoryMapInfoStatus.InvalidDescriptorVersion
            && result.Info.RegionCount == 0
            && result.InvalidDescriptorIndex == -1
            && result.Message.Contains("descriptor version", StringComparison.Ordinal));
    }

    [HostTest("memory.mapinfo rejects invalid descriptor count", HostLane.Fast, 3)]
    private static bool TestInvalidDescriptorCount()
    {
        MemoryMapInfoResult zeroCount = MemoryMapInfoResult.FromUefiDescriptors(
            mapKey: 0x1,
            descriptorSize: MemoryMapInfoResult.MinimumUefiDescriptorSize,
            descriptorVersion: 1,
            descriptorCount: 0,
            descriptorEntries: Array.Empty<UefiMemoryDescriptor>());

        MemoryMapInfoResult mismatchedCount = MemoryMapInfoResult.FromUefiDescriptors(
            mapKey: 0x1,
            descriptorSize: MemoryMapInfoResult.MinimumUefiDescriptorSize,
            descriptorVersion: 1,
            descriptorCount: 2,
            descriptorEntries: CreateSingleDescriptor());

        return HostCheck.Result(!zeroCount.Succeeded
            && zeroCount.Status == MemoryMapInfoStatus.InvalidDescriptorCount
            && zeroCount.Info.RegionCount == 0
            && !mismatchedCount.Succeeded
            && mismatchedCount.Status == MemoryMapInfoStatus.InvalidDescriptorCount
            && mismatchedCount.Info.RegionCount == 0
            && mismatchedCount.Message.Contains("descriptor count", StringComparison.Ordinal));
    }

    [HostTest("memory.mapinfo maps unknown UEFI descriptor type", HostLane.Fast, 4)]
    private static bool TestUnknownUefiDescriptorType()
    {
        const uint unknownTypeValue = 0x80000000;
        UefiMemoryDescriptor[] descriptors =
        [
            new UefiMemoryDescriptor(
                (UefiMemoryType)unknownTypeValue,
                0x300000,
                0,
                1,
                0x22)
        ];

        MemoryMapInfoResult result = MemoryMapInfoResult.FromUefiDescriptors(
            mapKey: 0x2,
            descriptorSize: MemoryMapInfoResult.MinimumUefiDescriptorSize,
            descriptorVersion: 1,
            descriptorCount: descriptors.Length,
            descriptorEntries: descriptors);

        bool regionFound = result.Info.TryGetRegion(0, out MemoryRegion region);

        return HostCheck.Result(result.Succeeded
            && result.Status == MemoryMapInfoStatus.Success
            && regionFound
            && region.Type == MemoryRegionType.Unknown
            && region.UefiTypeValue == unknownTypeValue
            && region.PhysicalStart == 0x300000
            && region.PageCount == 1
            && region.Attribute == 0x22);
    }

    [HostTest("memory.mapinfo rejects zero page descriptors", HostLane.Fast, 5)]
    private static bool TestZeroPageDescriptor()
    {
        UefiMemoryDescriptor[] descriptors =
        [
            new UefiMemoryDescriptor(
                UefiMemoryType.ConventionalMemory,
                0x400000,
                0,
                0,
                0)
        ];

        MemoryMapInfoResult result = MemoryMapInfoResult.FromUefiDescriptors(
            mapKey: 0x3,
            descriptorSize: MemoryMapInfoResult.MinimumUefiDescriptorSize,
            descriptorVersion: 1,
            descriptorCount: descriptors.Length,
            descriptorEntries: descriptors);

        return HostCheck.Result(!result.Succeeded
            && result.Status == MemoryMapInfoStatus.InvalidPageCount
            && result.InvalidDescriptorIndex == 0
            && result.Info.RegionCount == 0
            && result.Message.Contains("page count", StringComparison.Ordinal));
    }

    [HostTest("memory.mapinfo calculates total pages and bytes", HostLane.Fast, 6)]
    private static bool TestTotalPagesAndBytes()
    {
        UefiMemoryDescriptor[] descriptors =
        [
            new UefiMemoryDescriptor(
                UefiMemoryType.LoaderCode,
                0x500000,
                0,
                2,
                0),
            new UefiMemoryDescriptor(
                UefiMemoryType.ConventionalMemory,
                0x600000,
                0,
                6,
                0)
        ];

        MemoryMapInfoResult result = MemoryMapInfoResult.FromUefiDescriptors(
            mapKey: 0x4,
            descriptorSize: 48,
            descriptorVersion: 2,
            descriptorCount: descriptors.Length,
            descriptorEntries: descriptors);

        bool firstFound = result.Info.TryGetRegion(0, out MemoryRegion first);
        bool secondFound = result.Info.TryGetRegion(1, out MemoryRegion second);

        return HostCheck.Result(result.Succeeded
            && result.Info.TotalPages == 8
            && result.Info.TotalBytes == 8 * MemoryRegion.PageSize
            && result.Info.ConventionalMemoryPages == 6
            && result.Info.ConventionalMemoryBytes == 6 * MemoryRegion.PageSize
            && firstFound
            && first.ByteLength == 2 * MemoryRegion.PageSize
            && first.PhysicalEndExclusive == 0x500000 + (2 * MemoryRegion.PageSize)
            && secondFound
            && second.ByteLength == 6 * MemoryRegion.PageSize
            && second.PhysicalEndExclusive == 0x600000 + (6 * MemoryRegion.PageSize));
    }

    [HostTest("memory.mapinfo writes caller-owned regions", HostLane.Fast, 7)]
    private static bool TestWritesCallerOwnedRegions()
    {
        Span<UefiMemoryDescriptor> descriptors = stackalloc UefiMemoryDescriptor[3];
        descriptors[0] = new UefiMemoryDescriptor(
            UefiMemoryType.ConventionalMemory,
            0x800000,
            0,
            6,
            0);
        descriptors[1] = new UefiMemoryDescriptor(
            UefiMemoryType.BootServicesData,
            0x900000,
            0,
            2,
            0x8);
        descriptors[2] = new UefiMemoryDescriptor(
            UefiMemoryType.ConventionalMemory,
            0xA00000,
            0,
            4,
            0);

        Span<MemoryRegion> destination = stackalloc MemoryRegion[3];
        MemoryMapInfoRegionWriteResult result = MemoryMapInfo.TryWriteRegionsFromUefiDescriptors(
            mapKey: 0x55,
            descriptorSize: 48,
            descriptorVersion: 2,
            descriptorCount: descriptors.Length,
            descriptorEntries: descriptors,
            destination);

        return HostCheck.Result(result.Succeeded
            && result.Status == MemoryMapInfoRegionWriteStatus.Success
            && result.MapKey == 0x55
            && result.DescriptorSize == 48
            && result.DescriptorVersion == 2
            && result.DescriptorCount == 3
            && result.RegionCount == 3
            && result.RequiredRegionCapacity == 3
            && result.AvailableRegionCapacity == 3
            && result.InvalidDescriptorIndex == -1
            && result.TotalPages == 12
            && result.TotalBytes == 12 * MemoryRegion.PageSize
            && result.ConventionalMemoryPages == 10
            && result.ConventionalMemoryBytes == 10 * MemoryRegion.PageSize
            && destination[0].Type == MemoryRegionType.ConventionalMemory
            && destination[0].PhysicalStart == 0x800000
            && destination[0].PageCount == 6
            && destination[1].Type == MemoryRegionType.BootServicesData
            && destination[1].Attribute == 0x8
            && destination[2].Type == MemoryRegionType.ConventionalMemory
            && destination[2].PhysicalEndExclusive == 0xA00000 + (4 * MemoryRegion.PageSize));
    }

    [HostTest("memory.mapinfo reports destination capacity", HostLane.Fast, 8)]
    private static bool TestReportsDestinationCapacity()
    {
        Span<UefiMemoryDescriptor> descriptors = stackalloc UefiMemoryDescriptor[2];
        descriptors[0] = new UefiMemoryDescriptor(
            UefiMemoryType.ConventionalMemory,
            0xB00000,
            0,
            2,
            0);
        descriptors[1] = new UefiMemoryDescriptor(
            UefiMemoryType.RuntimeServicesData,
            0xC00000,
            0,
            1,
            0);

        MemoryRegion sentinel = MemoryRegion.FromUefiDescriptor(
            new UefiMemoryDescriptor(
                UefiMemoryType.PersistentMemory,
                0xD00000,
                0,
                1,
                0x44)).Region;
        Span<MemoryRegion> destination = stackalloc MemoryRegion[1];
        destination[0] = sentinel;

        MemoryMapInfoRegionWriteResult result = MemoryMapInfo.TryWriteRegionsFromUefiDescriptors(
            mapKey: 0x66,
            descriptorSize: MemoryMapInfoResult.MinimumUefiDescriptorSize,
            descriptorVersion: 1,
            descriptorCount: descriptors.Length,
            descriptorEntries: descriptors,
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == MemoryMapInfoRegionWriteStatus.DestinationTooSmall
            && result.DescriptorCount == 2
            && result.RegionCount == 0
            && result.RequiredRegionCapacity == 2
            && result.AvailableRegionCapacity == 1
            && result.InvalidDescriptorIndex == -1
            && result.TotalPages == 0
            && result.TotalBytes == 0
            && destination[0].Type == sentinel.Type
            && destination[0].PhysicalStart == sentinel.PhysicalStart
            && destination[0].PageCount == sentinel.PageCount
            && destination[0].Attribute == sentinel.Attribute);
    }

    [HostTest("memory.mapinfo rejects invalid caller-owned descriptors", HostLane.Fast, 9)]
    private static bool TestRejectsInvalidCallerOwnedDescriptors()
    {
        Span<UefiMemoryDescriptor> descriptors = stackalloc UefiMemoryDescriptor[2];
        descriptors[0] = new UefiMemoryDescriptor(
            UefiMemoryType.ConventionalMemory,
            0xE00000,
            0,
            3,
            0);
        descriptors[1] = new UefiMemoryDescriptor(
            UefiMemoryType.ConventionalMemory,
            0xF00000,
            0,
            0,
            0);

        MemoryRegion sentinel = MemoryRegion.FromUefiDescriptor(
            new UefiMemoryDescriptor(
                UefiMemoryType.LoaderData,
                0x1000000,
                0,
                1,
                0x22)).Region;
        Span<MemoryRegion> destination = stackalloc MemoryRegion[2];
        destination[0] = sentinel;

        MemoryMapInfoRegionWriteResult result = MemoryMapInfo.TryWriteRegionsFromUefiDescriptors(
            mapKey: 0x77,
            descriptorSize: MemoryMapInfoResult.MinimumUefiDescriptorSize,
            descriptorVersion: 1,
            descriptorCount: descriptors.Length,
            descriptorEntries: descriptors,
            destination);

        return HostCheck.Result(!result.Succeeded
            && result.Status == MemoryMapInfoRegionWriteStatus.InvalidPageCount
            && result.DescriptorCount == 2
            && result.RegionCount == 0
            && result.RequiredRegionCapacity == 2
            && result.AvailableRegionCapacity == 2
            && result.InvalidDescriptorIndex == 1
            && result.TotalPages == 3
            && result.TotalBytes == 3 * MemoryRegion.PageSize
            && result.ConventionalMemoryPages == 3
            && result.ConventionalMemoryBytes == 3 * MemoryRegion.PageSize
            && result.Message.Contains("page count", StringComparison.Ordinal)
            && destination[0].Type == sentinel.Type
            && destination[0].PhysicalStart == sentinel.PhysicalStart
            && destination[0].PageCount == sentinel.PageCount
            && destination[0].Attribute == sentinel.Attribute);
    }

    private static UefiMemoryDescriptor[] CreateSingleDescriptor()
    {
        return
        [
            new UefiMemoryDescriptor(
                UefiMemoryType.RuntimeServicesData,
                0x700000,
                0,
                1,
                0)
        ];
    }
}
