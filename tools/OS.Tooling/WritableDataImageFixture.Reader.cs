using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Std.Hash;
using OS.Std.Time;
using OS.Storage;

namespace OS.Tooling;

public static partial class WritableDataImageFixture
{
    private sealed class Reader(byte[] disk, uint first = PartitionFirstLba)
    {
        private readonly HashSet<uint> claimed = [];
        private LiveFat32Volume volume;
        private readonly List<Entry> files = [];
        private readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        private readonly uint last = checked(first + PartitionLastLba - PartitionFirstLba);
        private readonly uint firstFat = first + ReservedSectors, firstData = first + ReservedSectors + 2 * FatSectors;
        private int extracted;
        public Inspection Read()
        {
            ReadOnlySpan<byte> mbr = disk.AsSpan(0, 512);
            Need(mbr[510] == 0x55 && mbr[511] == 0xaa && mbr[450] == 0xee && U32(mbr, 454) == 1 && U32(mbr, 458) == Sectors - 1 && mbr.Slice(462, 48).IndexOfAnyExcept((byte)0) < 0, "mbr");
            var primary = Gpt(false);
            var backup = Gpt(true);
            Need(primary.FirstUsableLba == backup.FirstUsableLba && primary.LastUsableLba == backup.LastUsableLba && primary.DiskGuid == backup.DiskGuid && disk.AsSpan(1024, 16384).SequenceEqual(disk.AsSpan((int)(Sectors - 33) * 512, 16384)), "gpt_copies");
            return ReadPartition(checkedGpt: true);
        }

        public Inspection ReadPartition(bool checkedGpt = false)
        {
            Span<byte> boot = disk.AsSpan(checked((int)first * 512), 512);
            Need(FATClusterChainsLoopBadClusterTruncationDetection.ParseBpb(boot, (ulong)disk.Length / 512, first, last, Complete, ref volume) == LiveFat32Status.Success && volume.RootCluster == 2 && volume.ReservedSectors == ReservedSectors && volume.FatSectors == FatSectors && volume.SectorsPerCluster == 1 && volume.ClusterCount == ClusterCount && volume.TotalSectors == last - first + 1 && volume.FirstFatLba == firstFat && volume.FirstDataLba == firstData && U32(boot, 28) == first && U16(boot, 48) == 1 && U16(boot, 50) == 6, "bpb");
            Need(boot.SequenceEqual(disk.AsSpan(checked(((int)first + 6) * 512), 512)), "boot_backup");
            Span<byte> fat = disk.AsSpan(checked((int)firstFat * 512), (int)FatSectors * 512);
            Span<byte> fat1 = disk.AsSpan(checked(((int)firstFat + (int)FatSectors) * 512), fat.Length);
            Need(fat.SequenceEqual(fat1), "fat_mirror");
            Need(U32(fat, 0) == 0x0ffffff8 && U32(fat, 4) == End, "fat_reserved");
            uint[] roots = Chain(2);
            ReadRoot(roots);
            uint free = 0;
            for (uint cluster = 2; cluster < ClusterCount + 2; cluster++)
            {
                uint next = U32(fat, (int)cluster * 4) & 0x0fffffff;
                if (next == 0)
                {
                    Need(!claimed.Contains(cluster), "claimed_free");
                    free++;
                }
                else
                    Need(claimed.Contains(cluster), "unclaimed_cluster");
            }

            Span<byte> info = disk.AsSpan(checked(((int)first + 1) * 512), 512);
            Need(info.SequenceEqual(disk.AsSpan(checked(((int)first + 7) * 512), 512)), "fsinfo_backup");
            Need(U32(info, 0) == 0x41615252 && U32(info, 484) == 0x61417272 && U32(info, 508) == 0xaa550000, "fsinfo");
            Need(U32(info, 488) == uint.MaxValue || U32(info, 488) == free, "free_count");
            uint hint = U32(info, 492);
            Need(hint == uint.MaxValue || hint >= 2 && hint - 2 < ClusterCount && Next(hint) == 0, "free_hint");
            List<uint> candidates = [];
            for (uint cluster = 100; cluster < ClusterCount + 2 && candidates.Count < 16; cluster += 2)
                if (Next(cluster) == 0)
                {
                    Need(!claimed.Contains(cluster), "candidate_not_free");
                    candidates.Add(cluster);
                }

            return new(Hash(disk), [Hash(disk.AsSpan(512, 512)), Hash(disk.AsSpan(disk.Length - 512, 512)), Hash(disk.AsSpan(1024, 16384)), Hash(boot), Hash(info), Hash(fat), Hash(fat1)], free, hint, roots, candidates.ToArray(), files.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(), first, checkedGpt);
        }

        private GptHeader Gpt(bool backup)
        {
            int offset = backup ? ImageBytes - 512 : 512;
            int entryOffset = backup ? (int)(Sectors - 33) * 512 : 1024;
            Span<GptPartitionDescriptor> descriptors = stackalloc GptPartitionDescriptor[128];
            var parsed = backup ? GptPartitionTableParser.ParseBackup(disk.AsSpan(offset, 512), disk.AsSpan(entryOffset, 16384), Sectors, 512, descriptors) : GptPartitionTableParser.ParsePrimary(disk.AsSpan(offset, 512), disk.AsSpan(entryOffset, 16384), Sectors, 512, descriptors);
            Need(parsed.Succeeded && parsed.PartitionsWritten == 1 && parsed.Header.FirstUsableLba == 34 && parsed.Header.LastUsableLba == PartitionLastLba && parsed.Header.DiskGuid == DiskGuid && parsed.Header.PartitionEntryLba == (backup ? Sectors - 33 : 2) && parsed.Header.PartitionEntryCount == 128 && parsed.Header.PartitionEntrySize == 128, "gpt");
            var part = descriptors[0];
            Need(part.PartitionTypeGuid == PartitionType && part.UniquePartitionGuid == PartitionGuid && part.EntryIndex == 0 && part.FirstLba == PartitionFirstLba && part.LastLba == PartitionLastLba && part.Attributes == 0, "partition");
            return parsed.Header;
        }

        private int ClusterOffset(uint cluster) => checked((int)(firstData + cluster - 2) * 512);
        private uint Next(uint cluster) => U32(disk.AsSpan((int)firstFat * 512, (int)FatSectors * 512), checked((int)cluster * 4)) & 0x0fffffff;
        private uint[] Chain(uint first)
        {
            List<uint> chain = [];
            uint current = first;
            while (true)
            {
                Need(current >= 2 && current - 2 < ClusterCount && chain.Count < 4096 && claimed.Add(current), "chain_loop_or_crosslink");
                chain.Add(current);
                ulong sector = firstFat + current / 128;
                uint next = 0;
                var status = FATClusterChainsLoopBadClusterTruncationDetection.ReadFatEntry(volume, current, sector, disk.AsSpan(checked((int)sector * 512), 512), Complete, ref next);
                if (status == LiveFat32Status.EndOfChain)
                    return chain.ToArray();
                Need(status == LiveFat32Status.Success, "chain");
                current = next;
            }
        }

        private void ReadRoot(uint[] roots)
        {
            LiveVfatState state = default;
            Span<ushort> words = stackalloc ushort[260];
            bool end = false;
            var device = new Device(disk);
            foreach (uint cluster in roots)
            {
                for (int slot = 0; slot < 16; slot++)
                {
                    if (end)
                        continue;
                    int offset = ClusterOffset(cluster) + slot * 32;
                    Span<byte> raw = disk.AsSpan(offset, 32);
                    var status = MultiClusterDirectoryVFATLongNameSupportSeparate.Consume(raw, ref state, words, out var entry);
                    if (status == LiveDirectoryStatus.End)
                    {
                        end = true;
                        continue;
                    }

                    if (status == LiveDirectoryStatus.LongName)
                        continue;
                    if (status == LiveDirectoryStatus.Skipped)
                    {
                        Need(raw[0] == 0xe5, "root_nonfile");
                        continue;
                    }

                    Need(status == LiveDirectoryStatus.Entry && (entry.Attributes & 0x18) == 0 && files.Count < MaximumFiles && raw[12] == 0, "directory");
                    string alias = Alias(raw[..11]);
                    string name = entry.HasLongName ? new string(words[..entry.NameLength].ToArray().Select(x => (char)x).ToArray()) : alias;
                    Need(names.Add(name) && (alias.Equals(name, StringComparison.OrdinalIgnoreCase) || names.Add(alias)), "name_collision");
                    uint[] chain = entry.FirstCluster == 0 ? [] : Chain(entry.FirstCluster);
                    Need(entry.ByteCount <= MaximumFileBytes && (ulong)chain.Length == ((ulong)entry.ByteCount + 511) / 512 && extracted <= MaximumExtractedBytes - entry.ByteCount, "file_length");
                    extracted += (int)entry.ByteCount;
                    byte[] content = new byte[(int)entry.ByteCount];
                    for (int i = 0, position = 0; i < chain.Length; i++)
                    {
                        int take = Math.Min(512, content.Length - position);
                        disk.AsSpan(ClusterOffset(chain[i]), take).CopyTo(content.AsSpan(position));
                        position += take;
                    }

                    ushort date = U16(raw, 24), time = U16(raw, 22);
                    long modified = 0;
                    Need(date == 0 && time == 0 || GregorianUtc.TryUnixSeconds((uint)(1980 + (date >> 9)), (uint)((date >> 5) & 15), (uint)(date & 31), (uint)(time >> 11), (uint)((time >> 5) & 63), (uint)((time & 31) * 2), out modified), "timestamp");
                    byte[] independentlyRead = new byte[content.Length];
                    var read = Fat32Parser.ReadRootFile(device, first, name, independentlyRead);
                    Need(read.Succeeded && read.BytesRead == entry.ByteCount && read.Entry.FirstCluster == entry.FirstCluster && content.AsSpan().SequenceEqual(independentlyRead), "independent_extract");
                    List<Extent> extents = [];
                    for (int i = 0; i < chain.Length;)
                    {
                        int start = i++;
                        while (i < chain.Length && chain[i] == chain[i - 1] + 1)
                            i++;
                        extents.Add(new(chain[start], (uint)(i - start), firstData + (ulong)chain[start] - 2));
                    }

                    files.Add(new(name, alias, (ulong)offset, entry.Attributes, date, time, modified, content, chain, extents.ToArray()));
                }
            }

            Need(MultiClusterDirectoryVFATLongNameSupportSeparate.Finish(state) == LiveDirectoryStatus.End, "lfn_tail");
        }
    }
}
