using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotSocketPayload
{
    /// <summary>Observed counters from four finite connections. Partial/blocked
    /// sends are observations, not required on every host and not guest proof.</summary>

    public sealed record Observation(int Received, int Sent, int PartialSends, int BlockedSends);
}
