using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace OS.Tooling;
// Shared owned-child mechanics only. Public completions and output admission
// remain separate for the unchanged socket peer and the dedicated vector peer.
internal static partial class NativeAotSocketPeerLifecycle
{
    internal sealed record Execution(string Root, int Port, string Path, string Sha256, ProcessResult Runner, string ReceiveGateSerial);
}
