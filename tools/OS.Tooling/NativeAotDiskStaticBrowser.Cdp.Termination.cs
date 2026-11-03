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
    internal sealed partial class Cdp
    {
        internal sealed record Termination(int CloseCommandId, bool CloseAcknowledged, int PendingCommandsAtTermination, long PartialMessageBytesAtTermination, bool PartialMessageFragmentAtTermination, bool StopRequestedAtTermination, string? ErrorType, string? WebSocketError, string? ErrorMessage, bool AcceptedExpectedEof);
    }
}
