using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotStaticFileContract
{
    public sealed record Observation(string Method, string Path, int Status, string? ContentType, long? ContentLength, int BodyBytes, string Sha256, string BodyFile, string? ETag, string? LastModified, string? ContentRange, bool Concurrent = false, long Started = 0, long Finished = 0);
}
