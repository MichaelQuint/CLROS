using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed record StaticHttpMessage(ServiceWireFlow Flow, int Index, string Method, string Path, Dictionary<string, string> RequestHeaders, Dictionary<string, string> ResponseHeaders, int Status, byte[] Request, byte[] Header, byte[] Body, byte[] RawResponse, int RequestFirst, int RequestLast, int ResponseFirst, int ResponseLast)
    {
        internal StaticFileHttpWireMessage Copy() => new(Flow.Port, Index, RequestFirst, ResponseLast, Convert.ToBase64String(Request), Convert.ToBase64String(Header), Convert.ToBase64String(Body), Convert.ToHexString(SHA256.HashData(RawResponse)));
    }
}
