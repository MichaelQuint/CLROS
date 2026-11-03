using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Net;

namespace OS.Tools.QemuRunner;
public sealed partial class ServiceSocketControls
{
    public sealed record HttpExchange(int Port, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc, string RequestHex, string ResponseHex);
}
