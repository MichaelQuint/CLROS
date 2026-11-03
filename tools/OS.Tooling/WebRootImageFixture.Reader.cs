using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;
public static partial class WebRootImageFixture
{
    private sealed class Reader
    {
        public readonly byte[] Disk;
        public readonly LiveFat32Volume Volume;
        public readonly ulong PartitionFirst, PartitionLast;
        public readonly List<Entry> RootEntries;
        public readonly Dictionary<string, Entry> FileEntries = new(StringComparer.Ordinal);
        private readonly HashSet<uint> owned = [];
        private readonly List<DiskFile> files = [];
        public Reader(byte[] disk)
        {
            Disk = disk;
            Need(disk.Length is >= 34 * 1024 * 1024 and <= MaximumImageBytes && disk.Length % 512 == 0, "image_size");
            ulong capacity = (ulong)disk.Length / 512;
            var primary = Gpt(false);
            var backup = Gpt(true);
            Need(primary.FirstLba == backup.FirstLba && primary.LastLba == backup.LastLba && disk.AsSpan(2 * 512, 16384).SequenceEqual(disk.AsSpan(disk.Length - 33 * 512, 16384)), "gpt_copies");
            PartitionFirst = primary.FirstLba;
            PartitionLast = primary.LastLba;
            int boot = checked((int)PartitionFirst * 512);
            LiveFat32Volume volume = default;
            Need(FATClusterChainsLoopBadClusterTruncationDetection.ParseBpb(disk.AsSpan(boot, 512), capacity, PartitionFirst, PartitionLast, Complete, ref volume) == LiveFat32Status.Success, "bpb");
            Volume = volume;
            Need(volume.SectorsPerCluster == 1 && volume.TotalSectors == PartitionLast - PartitionFirst + 1 && U16(disk, boot + 48) == 1 && U16(disk, boot + 50) == 6, "geometry");
            int fat = checked((int)volume.FirstFatLba * 512), fatBytes = checked((int)volume.FatSectors * 512);
            Need(disk.AsSpan(fat, fatBytes).SequenceEqual(disk.AsSpan(fat + fatBytes, fatBytes)), "fat_mirror");
            Need(disk.AsSpan(boot, 512).SequenceEqual(disk.AsSpan(boot + 6 * 512, 512)) && disk.AsSpan(boot + 512, 512).SequenceEqual(disk.AsSpan(boot + 7 * 512, 512)), "reserved_copies");
            int info = boot + 512;
            Need(U32(disk, info) == 0x41615252 && U32(disk, info + 484) == 0x61417272 && U32(disk, info + 508) == 0xaa550000, "fsinfo");
            uint free = 0;
            for (uint cluster = 2; cluster < volume.ClusterCount + 2; cluster++)
                if (RawNext(cluster) == 0)
                    free++;
            Need(U32(disk, info + 488)is uint.MaxValue || U32(disk, info + 488) == free, "free_count");
            uint hint = U32(disk, info + 492);
            Need(hint == uint.MaxValue || hint >= 2 && hint < volume.ClusterCount + 2 && RawNext(hint) == 0, "free_hint");
            RootEntries = ReadDirectory(volume.RootCluster);
            VisitEntries("", RootEntries, 0);
            LiveEfiPartitionResult Gpt(bool isBackup)
            {
                int headerOffset = isBackup ? disk.Length - 512 : 512;
                var header = PrimaryGPTIndependentlyBoundsCRC.ValidateHeader(disk.AsSpan(headerOffset, 512), capacity, Complete, isBackup);
                Need(header.Succeeded && header.EntryLba == (isBackup ? capacity - 33 : 2UL), "gpt_header");
                // First/last usable LBAs and disk GUID must agree even when
                // both differing headers have individually correct CRCs.
                Need(disk.AsSpan(512 + 40, 32).SequenceEqual(disk.AsSpan(disk.Length - 512 + 40, 32)), "gpt_identity");
                var partition = FAT32ApplicationEFIPartition.Select(header, disk.AsSpan(checked((int)header.EntryLba * 512), 16384));
                Need(partition.Succeeded, "gpt_partition");
                return partition;
            }
        }

        public ImageInventory Inventory() => new(Hash(Disk), PartitionFirst, PartitionLast, files.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray());
        public int ClusterOffset(uint cluster)
        {
            ulong lba = 0;
            Need(FATClusterChainsLoopBadClusterTruncationDetection.ClusterFirstLba(Volume, cluster, ref lba) == LiveFat32Status.Success, "cluster_bounds");
            return checked((int)lba * 512);
        }

        public uint RawNext(uint cluster) => U32(Disk, checked((int)Volume.FirstFatLba * 512 + (int)cluster * 4)) & 0x0fffffff;
        public uint[] Chain(uint first, bool claim = false)
        {
            List<uint> chain = [];
            HashSet<uint> seen = [];
            uint cluster = first;
            while (true)
            {
                Need(cluster >= 2 && cluster - 2 < Volume.ClusterCount && seen.Add(cluster) && (!claim || owned.Add(cluster)), "chain_loop_or_crosslink");
                chain.Add(cluster);
                ulong sector = Volume.FirstFatLba + cluster / 128;
                uint next = 0;
                var status = FATClusterChainsLoopBadClusterTruncationDetection.ReadFatEntry(Volume, cluster, sector, Disk.AsSpan(checked((int)sector * 512), 512), Complete, ref next);
                if (status == LiveFat32Status.EndOfChain)
                    return chain.ToArray();
                Need(status == LiveFat32Status.Success && chain.Count <= Volume.ClusterCount, "chain_corrupt");
                cluster = next;
            }
        }

        private List<Entry> ReadDirectory(uint first)
        {
            List<Entry> entries = [];
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            List<int> longNameSlots = [];
            LiveVfatState state = default;
            ushort[] name = new ushort[260];
            foreach (uint cluster in Chain(first, claim: true))
                for (int slot = 0; slot < 16; slot++)
                {
                    int position = ClusterOffset(cluster) + slot * 32;
                    var status = MultiClusterDirectoryVFATLongNameSupportSeparate.Consume(Disk.AsSpan(position, 32), ref state, name, out var parsed);
                    Need(status is LiveDirectoryStatus.Entry or LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName or LiveDirectoryStatus.End, "directory_entry");
                    if (status == LiveDirectoryStatus.End)
                        return entries;
                    if (status == LiveDirectoryStatus.LongName)
                    {
                        longNameSlots.Add(position);
                        continue;
                    }

                    if (status != LiveDirectoryStatus.Entry)
                        continue;
                    string alias = Encoding.ASCII.GetString(Disk, position, 8).TrimEnd(' ');
                    string extension = Encoding.ASCII.GetString(Disk, position + 8, 3).TrimEnd(' ');
                    if (extension.Length != 0)
                        alias += "." + extension;
                    string actual = parsed.NameLength == 0 ? alias : new string (name.Take(parsed.NameLength).Select(x => (char)x).ToArray());
                    Need(names.Add(actual), "directory_case_collision");
                    if (!alias.Equals(actual, StringComparison.OrdinalIgnoreCase))
                        Need(names.Add(alias), "directory_alias_collision");
                    entries.Add(new(actual, alias, parsed.FirstCluster, parsed.ByteCount, parsed.Attributes, U16(Disk, position + 24), U16(Disk, position + 22), [..longNameSlots, position]));
                    longNameSlots.Clear();
                }

            Need(state.Slots == 0, "directory_lfn_truncated");
            return entries;
        }

        private void VisitEntries(string parent, List<Entry> entries, int depth)
        {
            Need(depth <= MaximumDirectoryDepth + 2, "directory_depth");
            foreach (Entry entry in entries)
            {
                string path = parent + "/" + entry.Name;
                if ((entry.Attributes & 0x10) != 0)
                {
                    VisitEntries(path, ReadDirectory(entry.Cluster), depth + 1);
                    continue;
                }

                FileEntries.Add(path, entry);
                uint[] chain = entry.Bytes == 0 ? [] : Chain(entry.Cluster, claim: true);
                Need(entry.Bytes == 0 ? entry.Cluster == 0 : (ulong)chain.Length == ((ulong)entry.Bytes + 511) / 512, "file_chain_length");
                Need(entry.Bytes <= Disk.Length, "file_size");
                byte[] bytes = new byte[entry.Bytes];
                int offset = 0;
                foreach (uint cluster in chain)
                {
                    int count = Math.Min(512, bytes.Length - offset);
                    Disk.AsSpan(ClusterOffset(cluster), count).CopyTo(bytes.AsSpan(offset));
                    offset += count;
                }

                List<Extent> extents = [];
                foreach (uint cluster in chain)
                {
                    if (extents.Count != 0 && extents[^1].FirstCluster + extents[^1].Clusters == cluster)
                        extents[^1] = extents[^1] with
                        {
                            Clusters = extents[^1].Clusters + 1
                        };
                    else
                        extents.Add(new(cluster, 1, Volume.FirstDataLba + cluster - 2));
                }

                files.Add(new(path, entry.Bytes, Hash(bytes), entry.Attributes, entry.Date, entry.Time, extents.ToArray()));
            }
        }
    }
}
