using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class NativeAotServicePeer
{
    /// <summary>Copied bounded transport response. Caller owns Body. This
    /// observation confers no application, process or guest cleanup authority.</summary>

    public readonly record struct HttpResponseObservation(int Status, DateTimeOffset DateUtc, byte[] Body);
}
