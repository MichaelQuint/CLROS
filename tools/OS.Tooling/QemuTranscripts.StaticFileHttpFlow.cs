using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied unique stream extents in original SYN order. Queued
    /// application bytes can exceed transmitted bytes only on the reset flow;
    /// the scheduler/transport consumer must independently bound that suffix.</summary>

    public readonly record struct StaticFileHttpFlow(uint RemotePort, int RequestBytes, int ResponseBytes, bool Reset);
}
