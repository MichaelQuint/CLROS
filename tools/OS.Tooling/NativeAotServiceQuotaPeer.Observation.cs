using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    /// <summary>Supplied loopback values only. The four-connection limit follows
    /// owned client configuration, not a guest transport census. Original
    /// serial/application/syscall/packet/runner/native joins remain mandatory.</summary>

    public sealed record Observation(int Port, int ConnectionLimit, long HeldTicks, long PressureStartedTicks, long RetainedTicks, long HealthyTicks, long WorkerProgressTicks, long OverlapTicks, long PressureAbortedTicks, string PressureError, ulong RetainedBytes, ulong GcCollections, long WorkerCallsBefore, long WorkerCallsAfter, long WorkerIterationsBefore, long WorkerIterationsAfter, IReadOnlyList<Sample> Samples);
}
