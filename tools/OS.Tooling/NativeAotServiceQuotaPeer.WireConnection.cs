using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;
public static partial class NativeAotServiceQuotaPeer
{
    /// <summary>Actual connected host endpoints, observed after connect. IDs are
    /// session-local; ports/IPs imply no guest NAT tuple or packet identity.
    /// DisposedTicks records local stream disposal, not peer/guest termination.</summary>

    public sealed record WireConnection(int Id, string Role, string LocalAddress, int LocalPort, string RemoteAddress, int RemotePort, long ConnectedTicks, long DisposedTicks);
}
