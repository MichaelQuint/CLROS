using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Storage;

namespace OS.Tooling;
public static partial class WebRootImageFixture
{
    private sealed class Writer(byte[] disk, Reader original)
    {
        private readonly List<(int Start, int Bytes)> writes = [];
        private uint nextFree = 2;
        private int aliasNumber;
        private readonly HashSet<string> allNames = new(StringComparer.OrdinalIgnoreCase);
        public void Write(Node root)
        {
            Node[] nodes = root.Descendants().ToArray();
            foreach (Node node in nodes)
                allNames.Add(node.Name);
            foreach (Entry entry in original.RootEntries)
            {
                allNames.Add(entry.Name);
                allNames.Add(entry.Alias);
            }

            foreach (Node node in nodes)
            {
                do
                {
                    node.Alias = "W" + (++aliasNumber).ToString("D7", System.Globalization.CultureInfo.InvariantCulture) + (node.Directory ? "   " : "DAT");
                }
                while (allNames.Contains(node.Alias[..8].TrimEnd() + (node.Directory ? "" : ".DAT")));
                Need(node.Alias.Length == 11, "alias_limit");
                allNames.Add(node.Alias[..8] + (node.Directory ? "" : ".DAT"));
            }

            foreach (Node node in nodes.Where(x => x.Directory))
                node.Clusters = Allocate((2 + node.Children.Sum(x => Slots(x.Name)) + 1 + 15) / 16);
            foreach (Node node in nodes.Where(x => !x.Directory))
            {
                uint[] chain = Allocate((node.Bytes.Length + 511) / 512);
                node.Clusters = node.Path == GuestRoot + "/fixture/fragmented.bin" ? chain.Where((_, i) => i % 2 == 0).Concat(chain.Where((_, i) => i % 2 != 0)).ToArray() : chain;
                Link(node.Clusters);
                for (int index = 0; index < node.Clusters.Length; index++)
                {
                    int offset = original.ClusterOffset(node.Clusters[index]);
                    node.Bytes.AsSpan(index * 512, Math.Min(512, node.Bytes.Length - index * 512)).CopyTo(disk.AsSpan(offset));
                }
            }

            foreach (Node directory in nodes.Where(x => x.Directory))
            {
                Link(directory.Clusters);
                List<byte[]> entries = [ShortEntry(".          ", 0x10, directory.Clusters[0], 0), ShortEntry("..         ", 0x10, directory.Parent == root ? original.Volume.RootCluster : directory.Parent!.Clusters[0], 0)];
                foreach (Node child in directory.Children.OrderBy(x => x.Name, StringComparer.Ordinal))
                    entries.AddRange(Entries(child));
                WriteEntries(directory.Clusters, 0, entries);
            }

            uint[] rootChain = original.Chain(original.Volume.RootCluster);
            int end = 0;
            while (end < rootChain.Length * 16 && disk[original.ClusterOffset(rootChain[end / 16]) + end % 16 * 32] != 0)
                end++;
            List<byte[]> appended = [];
            foreach (Node child in root.Children.OrderBy(x => x.Name, StringComparer.Ordinal))
                appended.AddRange(Entries(child));
            int required = (end + appended.Count + 1 + 15) / 16;
            if (required > rootChain.Length)
            {
                uint[] added = Allocate(required - rootChain.Length);
                rootChain = [..rootChain, ..added];
                Link(rootChain);
            }

            WriteEntries(rootChain, end, appended);
            uint free = 0, hint = uint.MaxValue;
            for (uint cluster = 2; cluster < original.Volume.ClusterCount + 2; cluster++)
                if (Next(cluster) == 0)
                {
                    free++;
                    if (hint == uint.MaxValue)
                        hint = cluster;
                }

            int info = checked((int)original.PartitionFirst * 512 + 512);
            foreach (int copy in new[]
            {
                info,
                info + 6 * 512
            }

            )
            {
                W32(disk, copy + 488, free);
                W32(disk, copy + 492, hint);
                writes.Add((copy + 488, 8));
            }
        }

        private uint Next(uint cluster) => U32(disk, checked((int)original.Volume.FirstFatLba * 512 + (int)cluster * 4)) & 0x0fffffff;
        private uint[] Allocate(int count)
        {
            uint[] result = new uint[count];
            for (int i = 0; i < count; i++)
            {
                while (nextFree < original.Volume.ClusterCount + 2 && Next(nextFree) != 0)
                    nextFree++;
                Need(nextFree < original.Volume.ClusterCount + 2, "disk_full");
                uint cluster = nextFree++;
                result[i] = cluster;
                SetNext(cluster, EndOfChain);
                int offset = original.ClusterOffset(cluster);
                disk.AsSpan(offset, 512).Clear();
                writes.Add((offset, 512));
            }

            return result;
        }

        private void SetNext(uint cluster, uint next)
        {
            int offset = checked((int)original.Volume.FirstFatLba * 512 + (int)cluster * 4);
            int bytes = checked((int)original.Volume.FatSectors * 512);
            foreach (int copy in new[]
            {
                offset,
                offset + bytes
            }

            )
            {
                W32(disk, copy, (U32(disk, copy) & 0xf0000000) | next);
                writes.Add((copy, 4));
            }
        }

        private void Link(uint[] chain)
        {
            for (int i = 0; i < chain.Length; i++)
                SetNext(chain[i], i + 1 == chain.Length ? EndOfChain : chain[i + 1]);
        }

        private static int Slots(string name) => (name.Length + 12) / 13 + 1;
        private static byte[] ShortEntry(string alias, byte attributes, uint cluster, uint bytes)
        {
            byte[] entry = new byte[32];
            Encoding.ASCII.GetBytes(alias).CopyTo(entry, 0);
            entry[11] = attributes;
            W16(entry, 16, 0x21);
            W16(entry, 18, 0x21);
            W16(entry, 24, 0x21);
            W16(entry, 20, (ushort)(cluster >> 16));
            W16(entry, 26, (ushort)cluster);
            W32(entry, 28, bytes);
            return entry;
        }

        private static IEnumerable<byte[]> Entries(Node node)
        {
            byte[] shortEntry = ShortEntry(node.Alias, node.Directory ? (byte)0x10 : (byte)0x21, node.Clusters.Length == 0 ? 0 : node.Clusters[0], (uint)node.Bytes.Length);
            byte checksum = 0;
            for (int index = 0; index < 11; index++)
                checksum = unchecked((byte)((checksum >> 1) + ((checksum & 1) << 7) + shortEntry[index]));
            int slots = Slots(node.Name) - 1;
            int[] offsets = [1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30];
            for (int ordinal = slots; ordinal > 0; ordinal--)
            {
                byte[] entry = new byte[32];
                entry[0] = (byte)(ordinal | (ordinal == slots ? 0x40 : 0));
                entry[11] = 15;
                entry[13] = checksum;
                for (int index = 0; index < 13; index++)
                {
                    int position = (ordinal - 1) * 13 + index;
                    W16(entry, offsets[index], position < node.Name.Length ? node.Name[position] : position == node.Name.Length ? (ushort)0 : ushort.MaxValue);
                }

                yield return entry;
            }

            yield return shortEntry;
        }

        private void WriteEntries(uint[] chain, int start, List<byte[]> entries)
        {
            foreach (byte[] entry in entries.Append(new byte[32]))
            {
                Need(start < chain.Length * 16, "directory_capacity");
                int offset = original.ClusterOffset(chain[start / 16]) + start % 16 * 32;
                entry.CopyTo(disk, offset);
                writes.Add((offset, 32));
                start++;
            }
        }

        public void CheckMutationBounds(byte[] before)
        {
            int cursor = 0;
            foreach (var write in writes.OrderBy(x => x.Start))
            {
                if (write.Start > cursor)
                    Need(before.AsSpan(cursor, write.Start - cursor).SequenceEqual(disk.AsSpan(cursor, write.Start - cursor)), "mutation_bounds");
                cursor = Math.Max(cursor, write.Start + write.Bytes);
            }

            Need(before.AsSpan(cursor).SequenceEqual(disk.AsSpan(cursor)), "mutation_bounds");
        }
    }
}
