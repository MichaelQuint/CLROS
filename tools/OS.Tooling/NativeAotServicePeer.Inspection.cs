using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class NativeAotServicePeer
{
    /// <summary>Value-only observations of supplied peer output, never execution authority.</summary>

    public readonly record struct Inspection(int Port, int Exchanges, int StatusSamples, long DurationMilliseconds, long Calls, long RandomBytes)
    {
        /// <summary>Supplied v2 lifecycle values only; null for v1. This grants no execution authority.</summary>

        public NativeAotServiceLifecyclePeer.Observation? Lifecycle { get; init; }
    }
}
