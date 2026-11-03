using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

internal static partial class QemuRunnerHttpProbeTests
{
    private readonly struct FakeQemuHttpEndpoint
    {
        public FakeQemuHttpEndpoint(string qemuPath, string url)
        {
            QemuPath = qemuPath;
            Url = url;
        }

        public string QemuPath { get; }
        public string Url { get; }
    }
}
