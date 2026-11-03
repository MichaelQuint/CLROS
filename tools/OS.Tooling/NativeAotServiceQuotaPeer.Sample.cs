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
    /// <summary>Copied original response text at a monotonic elapsed100ns tick.
    /// Status samples are independent observations, not atomic snapshots.</summary>

    public readonly record struct Sample(string Name, long ElapsedTicks, string Body)
    {
        internal int WireExchangeId
        {
            get; init;
        }
        internal int WireConnectionId
        {
            get; init;
        }
    }
}
