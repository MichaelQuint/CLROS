using OS.Storage;

namespace OS.Manifest;
public static class NativeManifestLoader
{
    public const string ManifestPath = "OS/MANIFEST.JSON";
    public static NativeManifestLoadResult LoadFromPrimaryGptEsp(IReadOnlyBlockDevice? disk, Span<byte> manifestBuffer)
    {
        GptEfiSystemPartitionLocationResult esp = GptParser.LocatePrimaryEfiSystemPartition(disk);
        if (!esp.Succeeded)
        {
            return NativeManifestLoadResult.GptFail(esp.Status, esp.BlockStatus);
        }

        PartitionBlockDeviceCreateResult partition = PartitionBlockDevice.Create(disk, esp.PartitionStartLba, esp.PartitionBlockCount);
        if (!partition.Succeeded || partition.Device is null)
        {
            return NativeManifestLoadResult.PartitionFail(partition.Status);
        }

        Fat32FileReadResult file = Fat32Parser.ReadFirstLevelFile(partition.Device, partitionStartLba: 0, ManifestPath, manifestBuffer);
        if (!file.Succeeded)
        {
            return NativeManifestLoadResult.Fat32Fail(file, checked((uint)manifestBuffer.Length));
        }

        NativeManifestParseResult parsed = NativeManifestParser.Parse(manifestBuffer.Slice(0, checked((int)file.BytesRead)));
        if (!parsed.Succeeded)
        {
            return NativeManifestLoadResult.ManifestFail(parsed.Status, file.BytesRead, file.FileSize, checked((uint)manifestBuffer.Length));
        }

        return NativeManifestLoadResult.Success(parsed.Manifest, file.BytesRead, file.FileSize, checked((uint)manifestBuffer.Length));
    }

    public static NativeManifestLoadResult LoadFromPrimaryGptEspWithCallerBuffers(IReadOnlyBlockDevice? disk, Span<byte> primaryHeaderSector, Span<byte> partitionEntrySectors, Span<GptPartitionDescriptor> partitions, Span<byte> manifestBuffer)
    {
        GptEspLocationReadinessResult esp = OsPackageLoader.LocatePrimaryGptEspWithCallerBuffers(disk, primaryHeaderSector, partitionEntrySectors, partitions);
        if (!esp.Succeeded)
        {
            return NativeManifestLoadResult.GptFail(esp);
        }

        PartitionBlockDeviceCreateResult partition = PartitionBlockDevice.Create(disk, esp.PartitionStartLba, esp.PartitionBlockCount);
        if (!partition.Succeeded || partition.Device is null)
        {
            return NativeManifestLoadResult.PartitionFail(partition.Status, esp);
        }

        Fat32FileReadResult file = Fat32Parser.ReadFirstLevelFile(partition.Device, partitionStartLba: 0, ManifestPath, manifestBuffer);
        if (!file.Succeeded)
        {
            return NativeManifestLoadResult.Fat32Fail(file, checked((uint)manifestBuffer.Length), esp);
        }

        NativeManifestParseResult parsed = NativeManifestParser.Parse(manifestBuffer.Slice(0, checked((int)file.BytesRead)));
        if (!parsed.Succeeded)
        {
            return NativeManifestLoadResult.ManifestFail(parsed.Status, file.BytesRead, file.FileSize, checked((uint)manifestBuffer.Length), esp);
        }

        return NativeManifestLoadResult.Success(parsed.Manifest, file.BytesRead, file.FileSize, checked((uint)manifestBuffer.Length), esp);
    }
}
