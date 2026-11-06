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

internal static partial class RawEspImageBuilder
{
    private readonly struct FatLayout
    {
        private FatLayout(uint fatSectors, uint clusterCount, int fileClusterCount, int manifestFileClusterCount, int packageFileClusterCount)
        {
            FatSectors = fatSectors;
            ClusterCount = clusterCount;
            FileClusterCount = fileClusterCount;
            ManifestFileClusterCount = manifestFileClusterCount;
            PackageFileClusterCount = packageFileClusterCount;
        }

        public uint FatSectors
        {
            get;
        }
        public uint ClusterCount
        {
            get;
        }
        public int FileClusterCount
        {
            get;
        }
        public int ManifestFileClusterCount
        {
            get;
        }
        public int PackageFileClusterCount
        {
            get;
        }
        public bool HasManifest => ManifestFileClusterCount != 0;
        public bool HasPackage => PackageFileClusterCount != 0;
        public bool HasOsDirectory => HasManifest || HasPackage;
        public int OsDirectoryCluster => BootFileFirstCluster + FileClusterCount;
        public int ManifestFileFirstCluster => OsDirectoryCluster + 1;
        public int PackageFileFirstCluster => ManifestFileFirstCluster + ManifestFileClusterCount;
        public int NextFreeCluster => PackageFileFirstCluster + PackageFileClusterCount;
        public int UsedClusterCount => 3 + FileClusterCount + (HasOsDirectory ? 1 + ManifestFileClusterCount + PackageFileClusterCount : 0);

        public static FatLayout Create(uint partitionSectorCount, int efiLength, int manifestLength, int packageLength)
        {
            int fileClusterCount = checked((efiLength + SectorSize * SectorsPerCluster - 1) / (SectorSize * SectorsPerCluster));
            int manifestFileClusterCount = manifestLength == 0 ? 0 : checked((manifestLength + SectorSize * SectorsPerCluster - 1) / (SectorSize * SectorsPerCluster));
            int packageFileClusterCount = packageLength == 0 ? 0 : checked((packageLength + SectorSize * SectorsPerCluster - 1) / (SectorSize * SectorsPerCluster));
            // Ignoring the FATs gives an upper bound on the data clusters.
            // Size for that bound, then derive the actual count from the same FAT size.
            uint maximumClusterCount = checked((partitionSectorCount - ReservedSectors) / SectorsPerCluster);
            uint fatSectors = DivRoundUp(checked(maximumClusterCount + 2), SectorSize / 4);
            uint clusterCount = checked((partitionSectorCount - ReservedSectors - FatCount * fatSectors) / SectorsPerCluster);
            // The last data cluster is count + 1; 0x0FFFFFF7 is the bad-cluster sentinel.
            if (clusterCount >= 0x0FFFFFF6)
            {
                throw new ImageBuilderException(5, "image_too_large", "Image exceeds the FAT32 allocatable cluster-number range.");
            }

            if (clusterCount <= 65525)
            {
                throw new ImageBuilderException(5, "image_too_small", "FAT32 ESP requires more than 65525 clusters with the current layout.");
            }

            int requiredNextCluster = BootFileFirstCluster + fileClusterCount;
            if (manifestFileClusterCount != 0 || packageFileClusterCount != 0)
            {
                requiredNextCluster += 1 + manifestFileClusterCount + packageFileClusterCount;
            }

            if ((uint)requiredNextCluster >= clusterCount)
            {
                throw new ImageBuilderException(6, "efi_too_large", "EFI artifact does not fit in the requested ESP image.");
            }

            return new FatLayout(fatSectors, clusterCount, fileClusterCount, manifestFileClusterCount, packageFileClusterCount);
        }

        private static uint DivRoundUp(uint value, int divisor)
        {
            return checked((uint)(((ulong)value + (uint)divisor - 1) / (uint)divisor));
        }
    }
}
