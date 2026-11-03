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
    public sealed record Match(string RequestId, int AssignedWireIndex, IReadOnlyList<int> EquivalentWireCandidates);
}
