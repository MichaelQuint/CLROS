using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;
public static partial class DiskStaticHttpContract
{
    /// <summary>Owned immutable request inventory, without response buffers or
    /// execution authority. Published identity uses the original fixture's
    /// relative-path/length/hash format, excluding its five synthetic files.</summary>

    public sealed class AssetPlan
    {
        public string Schema => Truncated ? TruncatedAssetSchema : AssetSchema;
        public string Scope => Truncated ? TruncatedAssetScope : AssetScope;
        public bool Truncated { get; }
        public string ImageSha256 { get; }
        public string PublishedInventorySha256 { get; }
        public int PublishedFiles { get; }
        public long PublishedBytes { get; }
        public long ExpectedBodyBytes { get; }
        public int MinimumReadCompletions { get; }
        public IReadOnlyList<Asset> Assets { get; }
        public IReadOnlyList<Request> Requests { get; }
        public bool GrantsOriginalNativeProcessOrBrowserAuthority => false;

        internal AssetPlan(string imageHash, string publishedHash, Asset[] assets, Request[] requests, bool truncated = false)
        {
            ImageSha256 = imageHash;
            PublishedInventorySha256 = publishedHash;
            Truncated = truncated;
            Assets = Array.AsReadOnly(assets);
            Requests = Array.AsReadOnly(requests);
            PublishedFiles = assets.Count(a => a.Published);
            PublishedBytes = assets.Where(a => a.Published).Sum(a => (long)a.Bytes);
            ExpectedBodyBytes = requests.Sum(r => (long)r.BodyBytes);
            MinimumReadCompletions = requests.Sum(r => r.MinimumReadCompletions);
        }
    }
}
