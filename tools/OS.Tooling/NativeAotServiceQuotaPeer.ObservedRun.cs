using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    /// <summary>Existing checked service values plus independently copied raw
    /// host I/O. The existing CLI/owner still emits its original v1 report;
    /// wiring this explicit companion into an owned artifact remains separate.</summary>

    public sealed record ObservedRun(Observation Service, WireTrace Wire);
}
