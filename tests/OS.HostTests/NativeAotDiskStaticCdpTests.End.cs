using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using B = OS.Tooling.NativeAotDiskStaticBrowser;

internal static partial class NativeAotDiskStaticCdpTests
{
    private enum End
    {
        Eof,
        CloseFrame,
        BeforeAck,
        Pending,
        OtherAck,
        ErrorAck,
        BadJson,
        Partial,
        EmptyFragment,
        Binary,
        InvalidFrame
    }
}
