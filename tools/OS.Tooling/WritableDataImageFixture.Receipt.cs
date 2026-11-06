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
    /// <summary>Exact initial-image extraction receipt, independent of output
    /// filename. CopyJson returns canonical UTF8 bytes with no timestamp/path.</summary>

    public sealed class Receipt
    {
        internal Receipt(Inspection image)
        {
            Image = image;
            json = Serialize(image);
        }

        private readonly byte[] json;
        public Inspection Image
        {
            get;
        }

        public byte[] CopyJson() => (byte[])json.Clone();
    }
}
