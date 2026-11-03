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
    private sealed record Exchange(int Index, string Method, string Path, int Status, long StartedMilliseconds, long CompletedMilliseconds, string DateUtc, string BodyBase64);
}
