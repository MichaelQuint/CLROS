using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;
public static partial class NativeAotServiceQuotaPeer
{
    /// <summary>Immutable bounded raw host I/O values. All ticks are elapsed100ns
    /// from this session's Stopwatch, with no UTC or guest-clock correlation.
    /// Purpose distinguishes the full quota stimulus from a real stream control.
    /// This is not a Completion, PCAP, syscall, termination or guest verdict.</summary>

    public sealed record WireTrace(int Port, string Purpose, long SnapshotTicks, long RawBytes, IReadOnlyList<WireConnection> Connections, IReadOnlyList<WireExchange> Exchanges, IReadOnlyList<WireOperation> Operations, IReadOnlyList<WireSample> Samples);
}
