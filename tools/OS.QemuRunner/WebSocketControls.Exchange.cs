using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OS.Tools.QemuRunner;
public static partial class WebSocketControls
{
    public readonly record struct Exchange(int SourcePort, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc, long ElapsedMilliseconds, DateTimeOffset DateUtc, string RequestHex, string ResponseHex);
}
