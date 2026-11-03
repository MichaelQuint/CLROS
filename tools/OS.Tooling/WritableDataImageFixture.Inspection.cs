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
    /// <summary>Immutable copied values from actual raw bytes, not the writer's
    /// allocation plan. Free candidates describe this exact image hash only.</summary>

    public sealed class Inspection
    {
        internal Inspection(string rawHash, string[] structures, uint free, uint hint, uint[] root, uint[] candidates, Entry[] files, uint first = PartitionFirstLba, bool checkedGpt = true)
        {
            RawSha256 = rawHash;
            StructureHashes = Array.AsReadOnly(structures);
            FreeClusters = free;
            FsInfoHint = hint;
            RootClusters = Array.AsReadOnly(root);
            FreeCandidates = Array.AsReadOnly(candidates);
            Files = Array.AsReadOnly(files);
            FirstLba = first;
            LastLba = checked(first + PartitionLastLba - PartitionFirstLba);
            FirstFatSector = first + ReservedSectors;
            FirstDataSector = FirstFatSector + 2 * FatSectors;
            CheckedGpt = checkedGpt;
        }

        public string RawSha256 { get; }
        public uint FirstLba { get; }
        public uint LastLba { get; }
        public uint FirstFatSector { get; }
        public uint FirstDataSector { get; }
        /// <summary>True only for the original strict64MiB whole-disk inspection.
        /// InspectPartition validates the actual FAT body, not its caller's GPT selection.</summary>

        public bool CheckedGpt { get; }
        /// <summary>Hashes ordered: primary GPT header, backup header, entries,
        /// BPB, FSInfo, FAT0, FAT1. GPT hashes alone are not GPT validation.</summary>

        public ReadOnlyCollection<string> StructureHashes { get; }
        public uint FreeClusters { get; }
        public uint FsInfoHint { get; }
        public ReadOnlyCollection<uint> RootClusters { get; }
        public ReadOnlyCollection<uint> FreeCandidates { get; }
        public ReadOnlyCollection<Entry> Files { get; }

        public Entry? Find(string name) => Files.SingleOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || x.ShortAlias.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
