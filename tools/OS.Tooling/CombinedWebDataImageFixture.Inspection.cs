using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Std.Hash;
using OS.Storage;
using OS.Tooling.Native;
using DataImage = OS.Tooling.WritableDataImageFixture;
using Gpt = OS.Storage.PrimaryGPTIndependentlyBoundsCRC;

namespace OS.Tooling;

public static partial class CombinedWebDataImageFixture
{
    /// <summary>Copied facts from exact actual GPT/partition bytes. Both GPT
    /// copies and both filesystem bodies have been independently inspected.
    /// The nested Data.CheckedGpt stays false because that helper checks only
    /// its actual FAT body; this enclosing inspection checks its GPT selection.</summary>

    public sealed class Inspection
    {
        internal Inspection(byte[] raw, Guid disk, GptPartitionDescriptor web, GptPartitionDescriptor data, WebRootImageFixture.ImageInventory webFiles, DataImage.Inspection dataFiles)
        {
            RawSha256 = Hash(raw);
            DiskGuid = disk;
            WebPartitionGuid = web.UniquePartitionGuid;
            DataPartitionGuid = data.UniquePartitionGuid;
            WebFirst = web.FirstLba;
            WebLast = web.LastLba;
            DataFirst = data.FirstLba;
            DataLast = data.LastLba;
            WebPartitionSha256 = Hash(Partition(raw, web.FirstLba, web.LastLba));
            DataPartitionSha256 = Hash(Partition(raw, data.FirstLba, data.LastLba));
            WebFiles = Array.AsReadOnly(webFiles.Files.Select(file => new WebFile(file, raw)).ToArray());
            Data = dataFiles;
        }

        public string RawSha256
        {
            get;
        }
        public Guid DiskGuid
        {
            get;
        }
        public Guid WebPartitionGuid
        {
            get;
        }
        public Guid DataPartitionGuid
        {
            get;
        }
        public ulong CapacitySectors => Sectors;
        public ulong WebFirst
        {
            get;
        }
        public ulong WebLast
        {
            get;
        }
        public ulong DataFirst
        {
            get;
        }
        public ulong DataLast
        {
            get;
        }
        public string WebPartitionSha256
        {
            get;
        }
        public string DataPartitionSha256
        {
            get;
        }
        public ReadOnlyCollection<WebFile> WebFiles
        {
            get;
        }
        public DataImage.Inspection Data
        {
            get;
        }
    }
}
