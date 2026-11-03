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
    public sealed record Receipt(string Schema, string Sdk, string Runtime, WebRootImageFixture.Snapshot Assets, Observation[] Http, Dictionary<string, string> Sources, Dictionary<string, string> Outputs);
}
