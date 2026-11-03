using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotDiskStaticBrowser
{
    /// <summary>Immutable copies of one original HTTP exchange. Request headers
    /// come from requestWillBeSentExtraInfo, response headers/status from
    /// responseReceivedExtraInfo. No CDP request id to guest socket/NAT identity
    /// is inferred. Repeated byte-equivalent exchanges remain distinct rows.</summary>

    public sealed class Exchange
    {
        private readonly byte[] body;
        public string RequestId
        {
            get;
        }
        public string Method
        {
            get;
        }
        public string Target
        {
            get;
        }
        public string Protocol
        {
            get;
        }
        public int Status
        {
            get;
        }
        public IReadOnlyList<Header> RequestHeaders
        {
            get;
        }
        public IReadOnlyList<Header> ResponseHeaders
        {
            get;
        }
        public long EncodedDataLength
        {
            get;
        }
        public bool FromDiskCache
        {
            get;
        }
        public bool FromServiceWorker
        {
            get;
        }
        public bool FromPrefetchCache
        {
            get;
        }
        public string BodyFile
        {
            get;
        }
        public int Bytes => body.Length;
        public string Sha256
        {
            get;
        }

        internal Exchange(string id, string method, string target, string protocol, int status, IReadOnlyList<Header> request, IReadOnlyList<Header> response, long encoded, byte[] bytes, string file)
        {
            RequestId = id;
            Method = method;
            Target = target;
            Protocol = protocol;
            Status = status;
            RequestHeaders = request;
            ResponseHeaders = response;
            EncodedDataLength = encoded;
            body = bytes.ToArray();
            BodyFile = file;
            Sha256 = Hash(body);
        }

        public byte[] CopyBody() => body.ToArray();
        internal bool BodyEquals(ReadOnlySpan<byte> other) => body.AsSpan().SequenceEqual(other);
    }
}
