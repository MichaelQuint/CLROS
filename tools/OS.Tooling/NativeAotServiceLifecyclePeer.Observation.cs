using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;

public static partial class NativeAotServiceLifecyclePeer
{
    public sealed record Observation(int Port, int SequentialConnections, int EchoBytes, uint EchoHash, int StreamBytes, string StreamSha256, int ResetRequests, int TimeoutRequests, long HoldStartedTicks, long HealthyCompletedTicks, long HoldReleasedTicks, string TerminalStatus);
}
