using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotStagedStaticPeer
{
    public sealed record Response(int Ordinal, string Method, string Path, int Status, string[] RequestHeaders, string[] ResponseHeaders, int Bytes, string Sha256, string BodyFile, long Started, long Finished);
}
