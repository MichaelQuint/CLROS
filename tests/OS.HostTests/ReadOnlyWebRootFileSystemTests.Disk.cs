using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using OS.Drivers;
using OS.Kernel.Platform;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using OS.Storage;

internal static unsafe partial class ReadOnlyWebRootFileSystemTests
{
    internal sealed class Disk
    {
        internal const ulong Partition = 2048;
        internal const string LongName = "a-long-vfat-name-crossing-sector-boundaries.bin";
        internal readonly Dictionary<ulong, byte[]> Sectors = [];
        internal readonly uint[] Reads;
        internal readonly string[] Names;
        internal readonly uint FatSectors;
        internal readonly ulong Capacity, Last, FatFirst, DataFirst;
        internal ulong FailLba = ulong.MaxValue;
        private int alias;
        internal Disk(uint clusters, bool collision)
        {
            FatSectors = (clusters + 2 + 127) / 128;
            FatFirst = Partition + 32;
            DataFirst = FatFirst + 2UL * FatSectors;
            uint total = 32 + FatSectors * 2 + clusters;
            Last = Partition + total - 1;
            Capacity = Last + 35;
            Reads = new uint[(int)Capacity];
            byte[] bpb = Sector(Partition);
            W16(bpb, 11, 512);
            bpb[13] = 1;
            W16(bpb, 14, 32);
            bpb[16] = 2;
            W32(bpb, 32, total);
            W32(bpb, 36, FatSectors);
            W32(bpb, 44, 2);
            bpb[510] = 0x55;
            bpb[511] = 0xaa;
            SetFat(0, 0x0ffffff8);
            SetFat(1, 0x0fffffff);
            foreach (uint c in new uint[] { 2, 3, 5, 8, 11, 12, 13, 400 })
                SetFat(c, 0x0fffffff);
            SetFat(4, 6);
            SetFat(6, 7);
            SetFat(7, 8);
            SetFat(10, 12);
            SetFat(200, 400);
            WriteDirectory([2], Entries("app", 3, 0, true));
            WriteDirectory([3], Entries("wwwroot", 4, 0, true));
            WriteDirectory([5], Entries("info.txt", 11, 1, false));
            Sector(Data(11))[0] = 0x6e;
            var entries = new List<byte[]>();
            var names = new List<string>();
            Add("index.html", 10, 700);
            Add("empty.bin", 0, 0);
            Add("fixture", 5, 0, true);
            while (entries.Count < 15)
            {
                byte[] deleted = new byte[32];
                deleted[0] = 0xe5;
                entries.Add(deleted);
            }
            Add(LongName, 13, 3);
            Add("far.bin", 200, 1024);
            for (uint i = 0; i < 16; i++)
            {
                uint c = 100 + i;
                SetFat(c, 0x0fffffff);
                Add("f" + i.ToString("000") + ".bin", c, 1);
                Sector(Data(c))[0] = (byte)i;
            }
            if (collision)
                Add("INDEX.HTML", 13, 3);
            WriteDirectory([4, 6, 7, 8], entries);
            Names = names.ToArray();
            for (int i = 0; i < 700; i++)
                Sector(Data(i < 512 ? 10U : 12U))[i & 511] = Payload(i);
            "ABC"u8.CopyTo(Sector(Data(13)));
            void Add(string name, uint cluster, uint bytes, bool directory = false)
            {
                entries.AddRange(Entries(name, cluster, bytes, directory));
                names.Add(name);
            }
        }
        internal ulong Data(uint cluster) => DataFirst + cluster - 2;
        internal byte[] Sector(ulong lba)
        {
            if (!Sectors.TryGetValue(lba, out var bytes))
                Sectors.Add(lba, bytes = new byte[512]);
            return bytes;
        }
        internal void SetFat(uint cluster, uint value, bool mirrorOnly = false)
        {
            for (uint copy = mirrorOnly ? 1U : 0; copy < 2; copy++)
                W32(Sector(FatFirst + copy * FatSectors + cluster / 128), (int)(cluster % 128) * 4, value);
        }
        internal uint FatReads()
        {
            uint n = 0;
            for (ulong i = FatFirst; i < DataFirst; i++)
                n += Reads[i];
            return n;
        }
        internal void NoDirectoryEnd()
        {
            for (int i = 0; i < 16; i++)
            {
                byte[] data = Sector(Data(8));
                if (data[i * 32] == 0)
                    data[i * 32] = 0xe5;
            }
        }
        private List<byte[]> Entries(string name, uint cluster, uint bytes, bool directory)
        {
            string shortName = "F" + (++alias).ToString("0000000") + "DAT";
            byte[] shortEntry = new byte[32];
            Encoding.ASCII.GetBytes(shortName).CopyTo(shortEntry, 0);
            shortEntry[11] = directory ? (byte)0x10 : (byte)0x21;
            W16(shortEntry, 24, 0x21);
            W16(shortEntry, 20, (ushort)(cluster >> 16));
            W16(shortEntry, 26, (ushort)cluster);
            W32(shortEntry, 28, bytes);
            return Entries(name, shortEntry);
        }
        internal static List<byte[]> Entries(string name, ReadOnlySpan<byte> shortEntry)
        {
            byte checksum = 0;
            foreach (byte b in shortEntry[..11])
                checksum = unchecked((byte)(((checksum & 1) << 7) + (checksum >> 1) + b));
            int count = (name.Length + 12) / 13;
            List<byte[]> entries = [];
            int[] positions = [1, 3, 5, 7, 9, 14, 16, 18, 20, 22, 24, 28, 30];
            for (int ordinal = count; ordinal > 0; ordinal--)
            {
                byte[] entry = new byte[32];
                entry[0] = (byte)(ordinal | (ordinal == count ? 0x40 : 0));
                entry[11] = 15;
                entry[13] = checksum;
                for (int i = 0; i < 13; i++)
                {
                    int at = (ordinal - 1) * 13 + i;
                    W16(entry, positions[i], at < name.Length ? name[at] : at == name.Length ? (ushort)0 : ushort.MaxValue);
                }
                entries.Add(entry);
            }
            entries.Add(shortEntry.ToArray());
            return entries;
        }
        private void WriteDirectory(uint[] clusters, List<byte[]> entries)
        {
            for (int i = 0; i < entries.Count; i++)
                entries[i].CopyTo(Sector(Data(clusters[i / 16])), i % 16 * 32);
        }
        private static void W16(byte[] b, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(at), value);
        private static void W32(byte[] b, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), value);
    }
}
