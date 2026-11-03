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
    private sealed class Scanner
    {
        private readonly byte[] raw;
        private readonly HashSet<uint> owned = [];
        private readonly HashSet<string> paths = new(StringComparer.Ordinal);
        private readonly int maximumEntries;
        internal readonly LiveFat32Volume Volume;
        internal readonly List<Entry> Entries = [];
        internal readonly List<DirectorySector> NavigationSectors = [];
        internal Scanner(byte[] raw, ulong first, ulong last, int maximumEntries)
        {
            this.raw = raw;
            this.maximumEntries = maximumEntries;
            Span<byte> sector = stackalloc byte[512];
            SectorBytes(first).CopyTo(sector);
            LiveFat32Volume volume = default;
            Need(Chain.ParseBpb(sector, (ulong)raw.Length / 512, first, last, Complete, ref volume) == LiveFat32Status.Success && volume.SectorsPerCluster == 1 && volume.TotalSectors == last - first + 1, "geometry");
            Volume = volume;
            for (uint i = 0; i < volume.FatSectors; i++)
                Need(SectorBytes(volume.FirstFatLba + i).SequenceEqual(SectorBytes(volume.FirstFatLba + volume.FatSectors + i)), "mirror");
        }

        private ReadOnlySpan<byte> SectorBytes(ulong lba)
        {
            Need(lba < (ulong)raw.Length / 512, "sector");
            return raw.AsSpan(checked((int)lba * 512), 512);
        }

        private ulong ClusterLba(uint cluster)
        {
            ulong lba = 0;
            Need(Chain.ClusterFirstLba(Volume, cluster, ref lba) == LiveFat32Status.Success, "cluster");
            return lba;
        }

        private uint[] Clusters(uint first)
        {
            uint[] history = new uint[ReadOnlyWebRootFileSystem.MaximumClusters];
            uint count = 0, cluster = first;
            Span<byte> fat = stackalloc byte[512];
            while (true)
            {
                Need(Chain.Visit(Volume, cluster, history, ref count) == LiveFat32Status.Success && owned.Add(cluster), "chain_ownership");
                ulong lba = Volume.FirstFatLba + cluster / 128;
                SectorBytes(lba).CopyTo(fat);
                uint next = 0;
                var status = Chain.ReadFatEntry(Volume, cluster, lba, fat, Complete, ref next);
                if (status == LiveFat32Status.EndOfChain)
                    return history.AsSpan(0, (int)count).ToArray();
                Need(status == LiveFat32Status.Success, "chain_link");
                cluster = next;
            }
        }

        internal List<Found> Directory(uint first, string path)
        {
            uint[] clusters = Clusters(first);
            foreach (uint cluster in clusters)
                NavigationSectors.Add(new(path, ClusterLba(cluster)));
            return ReadDirectory(clusters);
        }

        private List<Found> ReadDirectory(uint[] chain)
        {
            List<Found> entries = [];
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            LiveVfatState state = default;
            Span<ushort> words = stackalloc ushort[260];
            Span<byte> slot = stackalloc byte[32];
            int physical = 0;
            foreach (uint cluster in chain)
            {
                ulong lba = ClusterLba(cluster);
                ReadOnlySpan<byte> sector = SectorBytes(lba);
                for (uint index = 0; index < 16; index++)
                {
                    Need(++physical <= 4096, "directory_budget");
                    sector.Slice((int)index * 32, 32).CopyTo(slot);
                    var status = Vfat.Consume(slot, ref state, words, out var entry);
                    if (status == LiveDirectoryStatus.End)
                        return entries;
                    Need(status is LiveDirectoryStatus.Entry or LiveDirectoryStatus.Skipped or LiveDirectoryStatus.LongName, "vfat");
                    if (status != LiveDirectoryStatus.Entry)
                        continue;
                    string alias = Encoding.ASCII.GetString(slot[..8]).TrimEnd(' '), ext = Encoding.ASCII.GetString(slot.Slice(8, 3)).TrimEnd(' ');
                    if (ext.Length != 0)
                        alias += "." + ext;
                    string name = entry.NameLength == 0 ? alias : new string (words[..entry.NameLength].ToArray().Select(c => (char)c).ToArray());
                    Need(name.All(c => c is >= ' ' and <= '~') && name is not ("." or "..") && !name.EndsWith('.') && !name.EndsWith(' ') && names.Add(name) && (alias.Equals(name, StringComparison.OrdinalIgnoreCase) || names.Add(alias)), "name_alias");
                    ushort date = BinaryPrimitives.ReadUInt16LittleEndian(slot[24..]), time = BinaryPrimitives.ReadUInt16LittleEndian(slot[22..]);
                    long modified = 0;
                    Need(date == 0 && time == 0 || GregorianUtc.TryUnixSeconds((uint)(1980 + (date >> 9)), (uint)((date >> 5) & 15), (uint)(date & 31), (uint)(time >> 11), (uint)((time >> 5) & 63), (uint)((time & 31) * 2), out modified), "timestamp");
                    entries.Add(new(name, alias, entry.FirstCluster, entry.ByteCount, entry.Attributes, date, time, modified, lba, index));
                }
            }

            Need(Vfat.Finish(state) == LiveDirectoryStatus.End, "directory_end");
            return entries;
        }

        internal void Walk(string path, Found rawEntry, int depth)
        {
            Need(Canonical(path) && depth <= WebRootImageFixture.MaximumDirectoryDepth && paths.Add(path) && Entries.Count < maximumEntries, "path");
            bool directory = (rawEntry.Attributes & 0x10) != 0;
            uint[] clusters = rawEntry.Bytes == 0 && !directory ? [] : Clusters(rawEntry.Cluster);
            Need(directory || (rawEntry.Bytes == 0 ? rawEntry.Cluster == 0 : Chain.CheckLength((uint)clusters.Length, (uint)(((ulong)rawEntry.Bytes + 511) / 512)) == LiveFat32Status.Success), "length");
            byte[] data = directory ? [] : new byte[rawEntry.Bytes];
            int at = 0;
            if (!directory)
                foreach (uint cluster in clusters)
                {
                    int bytes = Math.Min(512, data.Length - at);
                    SectorBytes(ClusterLba(cluster))[..bytes].CopyTo(data.AsSpan(at));
                    at += bytes;
                }

            Entries.Add(new(path, rawEntry, clusters, Volume, data));
            if (directory)
                foreach (var child in ReadDirectory(clusters))
                    Walk(path + "/" + child.Name, child, depth + 1);
        }
    }
}
