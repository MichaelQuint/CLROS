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
    /// <summary>Copied extraction of one regular root file. Lists are readonly;
    /// CopyBytes returns a new logical-byte copy, excluding cluster slack.</summary>

    public sealed class Entry
    {
        private readonly byte[] content;
        internal Entry(string name, string alias, ulong entryOffset, byte attributes, ushort date, ushort time, long modified, byte[] bytes, uint[] clusters, Extent[] extents)
        {
            Name = name;
            ShortAlias = alias;
            ShortEntryOffset = entryOffset;
            Attributes = attributes;
            FatDate = date;
            FatTime = time;
            ModifiedSeconds = modified;
            content = bytes;
            Sha256 = Hash(bytes);
            Clusters = Array.AsReadOnly(clusters);
            Extents = Array.AsReadOnly(extents);
        }

        public string Name { get; }
        public string ShortAlias { get; }
        public ulong ShortEntryOffset { get; }
        public ulong Inode => ShortEntryOffset / 32 + 256;
        public byte Attributes { get; }
        public ushort FatDate { get; }
        public ushort FatTime { get; }
        /// <summary>Declared FAT UTC seconds; zero denotes an unspecified all-zero timestamp.</summary>

        public long ModifiedSeconds { get; }
        public int Bytes => content.Length;
        public string Sha256 { get; }
        public ReadOnlyCollection<uint> Clusters { get; }
        public ReadOnlyCollection<Extent> Extents { get; }

        public byte[] CopyBytes() => (byte[])content.Clone();
    }
}
