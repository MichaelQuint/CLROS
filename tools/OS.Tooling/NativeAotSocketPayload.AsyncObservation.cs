using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotSocketPayload
{
    /// <summary>Actual incomplete ValueTasks observed before await. These are
    /// distinct from synchronous EAGAIN counters; native traces must independently
    /// establish the epoll/syscall path. Cancellation and close are required too.</summary>

    public sealed record AsyncObservation(int Received, int Sent, int PartialSends, int PendingSends, int PendingAccepts, int PendingReceives);
}
