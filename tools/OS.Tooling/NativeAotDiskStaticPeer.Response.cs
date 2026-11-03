using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace OS.Tooling;
public static partial class NativeAotDiskStaticPeer
{
    public sealed record Response(int Ordinal, string Method, string Path, int Status, string[] RequestHeaders, string[] ResponseHeaders, int Bytes, string Sha256, string BodyFile, long Started, long Finished);
}
