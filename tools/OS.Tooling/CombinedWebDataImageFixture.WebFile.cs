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
    /// <summary>Immutable copied extraction of a preserved EFI/package/web file.
    /// Content excludes slack; physical extents refer to this actual image.</summary>

    public sealed class WebFile
    {
        private readonly byte[] content;
        internal WebFile(WebRootImageFixture.DiskFile file, byte[] raw)
        {
            Path = file.Path;
            Bytes = file.Bytes;
            Sha256 = file.Sha256;
            Attributes = file.Attributes;
            FatDate = file.FatDate;
            FatTime = file.FatTime;
            Extents = Array.AsReadOnly(file.Extents.Select(x => new WebRootImageFixture.Extent(x.FirstCluster, x.Clusters, x.FirstLba)).ToArray());
            content = new byte[checked((int)file.Bytes)];
            int position = 0;
            foreach (var extent in Extents)
            {
                int take = (int)Math.Min((ulong)(content.Length - position), (ulong)extent.Clusters * 512);
                Need(extent.FirstLba < (ulong)raw.Length / 512 && (ulong)take <= (ulong)raw.Length - extent.FirstLba * 512, "web_extent");
                raw.AsSpan(checked((int)extent.FirstLba * 512), take).CopyTo(content.AsSpan(position));
                position += take;
            }

            Need(position == content.Length && Hash(content) == Sha256, "web_extract");
        }

        public string Path
        {
            get;
        }
        public uint Bytes
        {
            get;
        }
        public string Sha256
        {
            get;
        }
        public byte Attributes
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
        public ReadOnlyCollection<WebRootImageFixture.Extent> Extents
        {
            get;
        }

        public byte[] CopyBytes() => (byte[])content.Clone();
    }
}
