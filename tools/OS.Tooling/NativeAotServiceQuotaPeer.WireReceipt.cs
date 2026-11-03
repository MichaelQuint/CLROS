using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    private readonly record struct WireReceipt(string Sha256, int Bytes);
}
