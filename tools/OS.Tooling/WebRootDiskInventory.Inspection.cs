using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Time;
using OS.Storage;
using Chain = OS.Storage.FATClusterChainsLoopBadClusterTruncationDetection;
using Vfat = OS.Storage.MultiClusterDirectoryVFATLongNameSupportSeparate;

namespace OS.Tooling;
public static partial class WebRootDiskInventory
{
    /// <summary>Immutable, ordinal/case-sensitive canonical names only. VFAT
    /// short aliases, traversal, alternate roots and normalization variants are
    /// not lookup routes. This copied result carries no external authority.</summary>

    public sealed class Inspection
    {
        private readonly Dictionary<string, Entry> byPath;
        private readonly Dictionary<ulong, string> directoryByLba;
        private readonly HashSet<ulong> assetSectors;
        private readonly ulong firstFatLba;
        private readonly uint fatSectors;
        public string ImageSha256 { get; }
        public ulong PartitionFirstLba { get; }
        public ulong PartitionLastLba { get; }
        public IReadOnlyList<Entry> Entries { get; }
        /// <summary>Exact checked directory-chain sectors, including navigation
        /// metadata for '/' and '/app' outside the served namespace. These paths
        /// do not become Entries/Find routes. No sector is recognized by its byte
        /// pattern; allocation and global chain ownership establish its role.</summary>

        public IReadOnlyList<DirectorySector> DirectorySectors { get; }
        public bool GrantsNativeDeviceOrProcessAuthority => false;

        internal Inspection(string hash, LiveFat32Volume volume, List<Entry> entries, List<DirectorySector> navigation)
        {
            ImageSha256 = hash;
            PartitionFirstLba = volume.PartitionFirstLba;
            PartitionLastLba = volume.PartitionLastLba;
            firstFatLba = volume.FirstFatLba;
            fatSectors = volume.FatSectors;
            Entries = Array.AsReadOnly(entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray());
            byPath = Entries.ToDictionary(e => e.Path, StringComparer.Ordinal);
            DirectorySectors = Array.AsReadOnly(navigation.Concat(Entries.Where(e => e.IsDirectory).SelectMany(e => e.Sectors.Select(s => new DirectorySector(e.Path, s.Lba)))).OrderBy(s => s.Lba).ToArray());
            assetSectors = Entries.Where(e => !e.IsDirectory).SelectMany(e => e.Sectors.Select(s => s.Lba)).ToHashSet();
            directoryByLba = new();
            foreach (var sector in DirectorySectors)
                Need(sector.Lba >= volume.FirstDataLba && sector.Lba <= volume.PartitionLastLba && !assetSectors.Contains(sector.Lba) && directoryByLba.TryAdd(sector.Lba, sector.Path), "metadata_overlap");
        }

        public Entry? Find(string path) => Canonical(path) && byPath.TryGetValue(path, out var entry) ? entry : null;
        internal MetadataSector Classify(ulong lba)
        {
            // Allocated asset sectors include the complete physical last sector,
            // so bytes after logical EOF never become directory metadata.
            Need(!assetSectors.Contains(lba), "metadata_asset");
            if (lba >= firstFatLba && lba - firstFatLba < fatSectors)
                return new(lba, MetadataKind.Fat0, "");
            ulong second = firstFatLba + fatSectors;
            if (lba >= second && lba - second < fatSectors)
                return new(lba, MetadataKind.Fat1, "");
            if (directoryByLba.TryGetValue(lba, out var path))
                return new(lba, MetadataKind.Directory, path);
            throw new ToolFailure("webroot_disk_inventory_metadata_sector");
        }
    }
}
