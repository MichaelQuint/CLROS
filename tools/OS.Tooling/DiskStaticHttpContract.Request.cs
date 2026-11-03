using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

public static partial class DiskStaticHttpContract
{
    /// <summary>One planned HTTP exchange. BodyOffset/BodyBytes describe exact
    /// entity bytes in Asset, independently hashed during planning. HEAD/304/416
    /// and missing routes have no body; nullable ContentLength allows 304 and the
    /// verified-corrupt target's HEAD404 to omit that header. A supplied length
    /// for the empty HEAD404 must still be zero. MinimumReadCompletions is only
    /// a lower bound from the current
    /// 8 KiB completion prefix, never a prediction of actual read sizes, cache
    /// state or timing. A consumer must join every actual read and body send.</summary>

    public sealed class Request
    {
        public int Ordinal
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
        public int Status
        {
            get;
        }
        public Asset? Asset
        {
            get;
        }
        public IReadOnlyList<Header> Headers
        {
            get;
        }
        public uint BodyOffset
        {
            get;
        }
        public uint BodyBytes
        {
            get;
        }
        public string BodySha256
        {
            get;
        }
        public long? ContentLength
        {
            get;
        }
        public string? ContentRange
        {
            get;
        }
        public int MinimumReadCompletions => checked((int)((BodyBytes + (ulong)MaximumReadCompletionBytes - 1) / MaximumReadCompletionBytes));

        internal Request(int ordinal, string method, string target, int status, Asset? asset, IReadOnlyList<Header> headers, uint offset, uint bytes, string hash, long? contentLength, string? contentRange)
        {
            Ordinal = ordinal;
            Method = method;
            Target = target;
            Status = status;
            Asset = asset;
            Headers = headers;
            BodyOffset = offset;
            BodyBytes = bytes;
            BodySha256 = hash;
            ContentLength = contentLength;
            ContentRange = contentRange;
        }
    }
}
