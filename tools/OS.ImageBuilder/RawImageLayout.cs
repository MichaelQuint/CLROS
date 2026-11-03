using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Boot;
using OS.Kernel.Build;
using OS.Manifest;
using OS.Storage;

internal sealed class RawImageLayout
{
    public RawImageLayout(string efiPath, string outputPath, string? manifestPath, string? packagePath, long efiSizeBytes, long manifestSizeBytes, long packageSizeBytes, int imageSizeMiB, long imageBytes, long totalSectors, long partitionStartLba, long partitionLastLba, uint partitionSectorCount, uint fatSectors, uint clusterCount, int fileClusterCount, int manifestFileClusterCount, int packageFileClusterCount, int osDirectoryCluster, int manifestFileFirstCluster, int packageFileFirstCluster)
    {
        EfiPath = efiPath;
        OutputPath = outputPath;
        ManifestPath = manifestPath;
        PackagePath = packagePath;
        EfiSizeBytes = efiSizeBytes;
        ManifestSizeBytes = manifestSizeBytes;
        PackageSizeBytes = packageSizeBytes;
        ImageSizeMiB = imageSizeMiB;
        ImageBytes = imageBytes;
        TotalSectors = totalSectors;
        PartitionStartLba = partitionStartLba;
        PartitionLastLba = partitionLastLba;
        PartitionSectorCount = partitionSectorCount;
        FatSectors = fatSectors;
        ClusterCount = clusterCount;
        FileClusterCount = fileClusterCount;
        ManifestFileClusterCount = manifestFileClusterCount;
        PackageFileClusterCount = packageFileClusterCount;
        OsDirectoryCluster = osDirectoryCluster;
        ManifestFileFirstCluster = manifestFileFirstCluster;
        PackageFileFirstCluster = packageFileFirstCluster;
    }

    public string EfiPath { get; }
    public string OutputPath { get; }
    public string? ManifestPath { get; }
    public string? PackagePath { get; }
    public long EfiSizeBytes { get; }
    public long ManifestSizeBytes { get; }
    public long PackageSizeBytes { get; }
    public bool HasManifest => ManifestSizeBytes != 0;
    public bool HasPackage => PackageSizeBytes != 0;
    public bool HasOsDirectory => HasManifest || HasPackage;
    public int ImageSizeMiB { get; }
    public long ImageBytes { get; }
    public long TotalSectors { get; }
    public long PartitionStartLba { get; }
    public long PartitionLastLba { get; }
    public uint PartitionSectorCount { get; }
    public uint FatSectors { get; }
    public uint ClusterCount { get; }
    public int FileClusterCount { get; }
    public int ManifestFileClusterCount { get; }
    public int PackageFileClusterCount { get; }
    public int OsDirectoryCluster { get; }
    public int ManifestFileFirstCluster { get; }
    public int PackageFileFirstCluster { get; }
    public long PrimaryGptEntriesLba => 2;
    public long BackupGptEntriesLba => TotalSectors - 33;
    public long BackupGptHeaderLba => TotalSectors - 1;
    public long FirstFatLba => PartitionStartLba + RawEspImageBuilder.ReservedSectors;
    public long SecondFatLba => FirstFatLba + FatSectors;
    public long DataStartLba => PartitionStartLba + RawEspImageBuilder.ReservedSectors + RawEspImageBuilder.FatCount * FatSectors;
    public long RootDirectoryLba => ClusterLba(RawEspImageBuilder.RootCluster);
    public long EfiDirectoryLba => ClusterLba(RawEspImageBuilder.EfiDirectoryCluster);
    public long BootDirectoryLba => ClusterLba(RawEspImageBuilder.BootDirectoryCluster);
    public long BootFileLba => ClusterLba(RawEspImageBuilder.BootFileFirstCluster);
    public long OsDirectoryLba => ClusterLba(OsDirectoryCluster);
    public long ManifestFileLba => ClusterLba(ManifestFileFirstCluster);
    public long PackageFileLba => ClusterLba(PackageFileFirstCluster);

    public long ClusterLba(int cluster)
    {
        return DataStartLba + (cluster - 2L) * RawEspImageBuilder.SectorsPerCluster;
    }
}
