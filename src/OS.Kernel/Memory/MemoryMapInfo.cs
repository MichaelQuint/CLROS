using OS.Boot;

namespace OS.Kernel.Memory;

public readonly struct MemoryMapInfo
{
    private readonly MemoryRegion[]? _regions;

    private MemoryMapInfo(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        ulong totalPages,
        ulong totalBytes,
        ulong conventionalMemoryPages,
        ulong conventionalMemoryBytes,
        MemoryRegion[] regions)
    {
        MapKey = mapKey;
        DescriptorSize = descriptorSize;
        DescriptorVersion = descriptorVersion;
        DescriptorCount = descriptorCount;
        TotalPages = totalPages;
        TotalBytes = totalBytes;
        ConventionalMemoryPages = conventionalMemoryPages;
        ConventionalMemoryBytes = conventionalMemoryBytes;
        _regions = regions;
    }

    public ulong MapKey { get; }

    public ulong DescriptorSize { get; }

    public uint DescriptorVersion { get; }

    public int DescriptorCount { get; }

    public int RegionCount => _regions is null ? 0 : _regions.Length;

    public ulong TotalPages { get; }

    public ulong TotalBytes { get; }

    public ulong ConventionalMemoryPages { get; }

    public ulong ConventionalMemoryBytes { get; }

    public bool HasRegions => RegionCount != 0;

    public ReadOnlySpan<MemoryRegion> Regions
    {
        get
        {
            MemoryRegion[]? regions = _regions;
            return regions is null ? default : regions;
        }
    }

    public bool TryGetRegion(int index, out MemoryRegion region)
    {
        MemoryRegion[]? regions = _regions;
        if (regions is null || (uint)index >= (uint)regions.Length)
        {
            region = default;
            return false;
        }

        region = regions[index];
        return true;
    }

    internal static MemoryMapInfo CreateUnchecked(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        ulong totalPages,
        ulong totalBytes,
        MemoryRegion[] regions)
    {
        return CreateUnchecked(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            totalPages,
            totalBytes,
            conventionalMemoryPages: 0,
            conventionalMemoryBytes: 0,
            regions);
    }

    internal static MemoryMapInfo CreateUnchecked(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        ulong totalPages,
        ulong totalBytes,
        ulong conventionalMemoryPages,
        ulong conventionalMemoryBytes,
        MemoryRegion[] regions)
    {
        return new MemoryMapInfo(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            totalPages,
            totalBytes,
            conventionalMemoryPages,
            conventionalMemoryBytes,
            regions);
    }

    public static MemoryMapInfoRegionWriteResult TryWriteRegionsFromUefiSnapshot(
        UefiMemoryMapSnapshot snapshot,
        Span<MemoryRegion> destination)
    {
        return MemoryMapInfoResult.TryWriteRegionsFromUefiSnapshot(snapshot, destination);
    }

    public static MemoryMapInfoRegionWriteResult TryWriteRegionsFromUefiDescriptors(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        UefiMemoryDescriptor[]? descriptorEntries,
        Span<MemoryRegion> destination)
    {
        return MemoryMapInfoResult.TryWriteRegionsFromUefiDescriptors(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorEntries,
            destination);
    }

    public static MemoryMapInfoRegionWriteResult TryWriteRegionsFromUefiDescriptors(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        int descriptorCount,
        ReadOnlySpan<UefiMemoryDescriptor> descriptorEntries,
        Span<MemoryRegion> destination)
    {
        return MemoryMapInfoResult.TryWriteRegionsFromUefiDescriptors(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorCount,
            descriptorEntries,
            destination);
    }
}
