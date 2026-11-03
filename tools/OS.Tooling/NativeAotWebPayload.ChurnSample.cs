using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class NativeAotWebPayload
{
    private sealed record ChurnSample(string kind, int cycle, string @case, int status, string body);
}
