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
    /// <summary>Canonical copied creation receipt without filename or clock.
    /// Input hashes describe held original files checked during publication;
    /// subsequent Verify rechecks the output, not those former input leases.</summary>

    public sealed class Receipt
    {
        private readonly byte[] json;
        internal Receipt(string webHash, string dataHash, Inspection image)
        {
            OriginalWebSha256 = webHash;
            OriginalDataSha256 = dataHash;
            Image = image;
            json = Serialize(webHash, dataHash, image);
        }

        public string OriginalWebSha256 { get; }
        /// <summary>Whole held data-source file hash: the64MiB seed for Create,
        /// or the128MiB prior combined image for CreateWithRetainedData. It does
        /// not authenticate that source's creator, process join or durability.</summary>

        public string OriginalDataSha256 { get; }
        public Inspection Image { get; }

        public byte[] CopyJson() => (byte[])json.Clone();
    }
}
