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
    /// <summary>Copied packet-consumer values, without packet authority. Headers
    /// are the complete parsed wire fields; response body is the decoded HTTP
    /// entity, request body must be empty. Caller retains the original PCAP and
    /// stream-position authority. Construction copies all mutable inputs.</summary>

    public sealed class WireExchange
    {
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
        public IReadOnlyList<Header> RequestHeaders
        {
            get;
        }
        public IReadOnlyList<Header> ResponseHeaders
        {
            get;
        }

        private readonly byte[] requestBody, responseBody;
        private readonly string sha256;
        internal int Bytes => responseBody.Length;

        public WireExchange(string method, string target, int status, IEnumerable<Header> requestHeaders, IEnumerable<Header> responseHeaders, byte[] requestBody, byte[] responseBody)
        {
            Need(requestBody.Length == 0 && responseBody.Length <= MaximumBodyBytes, "wire_body_bounds");
            Method = method;
            Target = target;
            Status = status;
            RequestHeaders = Array.AsReadOnly(requestHeaders.ToArray());
            ResponseHeaders = Array.AsReadOnly(responseHeaders.ToArray());
            Need(RequestHeaders.Count <= 64 && ResponseHeaders.Count <= 64, "wire_header_bounds");
            this.requestBody = requestBody.ToArray();
            this.responseBody = responseBody.ToArray();
            sha256 = Hash(this.responseBody);
        }

        internal bool Equals(Exchange value) => Method == value.Method && Target == value.Target && Status == value.Status && requestBody.Length == 0 && responseBody.Length == value.Bytes && sha256 == value.Sha256 && value.BodyEquals(responseBody) && SameHeaders(RequestHeaders, value.RequestHeaders) && SameHeaders(ResponseHeaders, value.ResponseHeaders);
    }
}
