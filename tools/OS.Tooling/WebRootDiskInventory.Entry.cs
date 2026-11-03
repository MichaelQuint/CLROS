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
    /// <summary>All collections and content are owned copies. Inode identifies
    /// the actual physical short directory slot: LBA*16+slot+256. Zero mtime is
    /// the project's unspecified all-zero FAT timestamp sentinel. Allocated
    /// sectors come from the checked chain, including directories; Linux's
    /// current directory stat still reports unknown allocation as zero.</summary>

    public sealed class Entry
    {
        private readonly byte[] content;
        public string Path
        {
            get;
        }
        public string ShortAlias
        {
            get;
        }
        public ulong Inode
        {
            get;
        }
        public byte Attributes
        {
            get;
        }
        public bool IsDirectory => (Attributes & 0x10) != 0;
        public uint Bytes
        {
            get;
        }
        public string Sha256
        {
            get;
        }
        public long ModifiedSeconds
        {
            get;
        }
        public ushort FatDate
        {
            get;
        }
        public ushort FatTime
        {
            get;
        }
        public ulong ShortEntryLba
        {
            get;
        }
        public uint ShortEntrySlot
        {
            get;
        }
        public ulong AllocatedSectors
        {
            get;
        }
        public IReadOnlyList<Extent> Extents
        {
            get;
        }
        public IReadOnlyList<Sector> Sectors
        {
            get;
        }

        internal Entry(string path, Found raw, uint[] chain, LiveFat32Volume volume, byte[] data)
        {
            Path = path;
            ShortAlias = raw.Alias;
            Attributes = raw.Attributes;
            Bytes = raw.Bytes;
            Inode = checked(raw.Lba * 16 + raw.Slot + 256);
            ModifiedSeconds = raw.Modified;
            FatDate = raw.Date;
            FatTime = raw.Time;
            ShortEntryLba = raw.Lba;
            ShortEntrySlot = raw.Slot;
            AllocatedSectors = (ulong)chain.Length * volume.SectorsPerCluster;
            content = data;
            Sha256 = Convert.ToHexString(SHA256.HashData(data));
            List<Extent> extents = [];
            List<Sector> sectors = [];
            ulong offset = 0;
            foreach (uint cluster in chain)
            {
                ulong lba = volume.FirstDataLba + (ulong)(cluster - 2) * volume.SectorsPerCluster;
                if (extents.Count != 0 && extents[^1].FirstCluster + extents[^1].Clusters == cluster)
                    extents[^1] = extents[^1] with
                    {
                        Clusters = extents[^1].Clusters + 1,
                        Sectors = extents[^1].Sectors + volume.SectorsPerCluster
                    };
                else
                    extents.Add(new(cluster, 1, lba, volume.SectorsPerCluster));
                for (uint i = 0; i < volume.SectorsPerCluster; i++)
                {
                    uint valid = IsDirectory ? 0U : offset >= Bytes ? 0U : (uint)Math.Min(512UL, Bytes - offset);
                    sectors.Add(new(offset, lba + i, valid));
                    offset += 512;
                }
            }

            Extents = Array.AsReadOnly(extents.ToArray());
            Sectors = Array.AsReadOnly(sectors.ToArray());
        }

        /// <summary>Return a fresh copy; no writable alias to retained evidence
        /// is exposed. Directories have no file content. The caller owns and may
        /// modify the result without changing the inventory.</summary>

        public byte[] CopyBytes() => content.ToArray();
        /// <summary>Copy the exact ordered data-sector coverage of a bounded
        /// file range. Offset at/beyond EOF or length0 returns no sectors.
        /// Directories and ranges above the existing64KiB request bound reject.
        /// This is image coverage, not evidence that a guest issued these reads.</summary>

        public IReadOnlyList<Sector> Cover(ulong offset, uint bytes)
        {
            Need(!IsDirectory && bytes <= 65536 && offset <= long.MaxValue, "range");
            if (offset >= Bytes || bytes == 0)
                return Array.AsReadOnly(Array.Empty<Sector>());
            ulong end = offset + Math.Min((ulong)bytes, Bytes - offset);
            return Array.AsReadOnly(Sectors.Where(s => s.FileOffset < end && offset < s.FileOffset + s.ValidBytes).ToArray());
        }
    }
}
