using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;
public static partial class DiskStaticHttpContract
{
    /// <summary>Copied immutable identity and upstream static metadata from one
    /// independently extracted physical file. Target escapes each path segment;
    /// physical Path remains the exact confined inventory key. No content buffer,
    /// mutable inventory alias, device or process authority is retained.</summary>

    public sealed class Asset
    {
        public string Path { get; }
        public string Target { get; }
        public ulong Inode { get; }
        public uint Bytes { get; }
        public string Sha256 { get; }
        public string MediaType { get; }
        public string ETag { get; }
        public string LastModified { get; }
        public bool Published { get; }

        internal Asset(WebRootDiskInventory.Entry file, bool published)
        {
            Path = file.Path;
            Target = "/" + string.Join('/', file.Path[(WebRootDiskInventory.Root.Length + 1)..].Split('/').Select(Uri.EscapeDataString));
            Inode = file.Inode;
            Bytes = file.Bytes;
            Sha256 = file.Sha256;
            Published = published;
            MediaType = Media(file.Path);
            var modified = DateTimeOffset.FromUnixTimeSeconds(file.ModifiedSeconds);
            LastModified = modified.ToString("r", CultureInfo.InvariantCulture);
            ETag = "\"" + (modified.ToFileTime() ^ file.Bytes).ToString("x", CultureInfo.InvariantCulture) + "\"";
        }
    }
}
