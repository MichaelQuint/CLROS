using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotSocketPeer
{
    /// <summary>Immutable identity of this owned peer execution and runner
    /// result. Only RunAsync can construct it; derived output cannot do so.</summary>

    public sealed class Completion
    {
        internal Completion(string root, int port, string path, string hash, ProcessResult runner, string receiveGateSerial)
        {
            Root = root;
            Port = port;
            Path = path;
            Sha256 = hash;
            Runner = runner;
            ReceiveGateSerial = receiveGateSerial;
        }

        internal string Root
        {
            get;
        }
        public int Port
        {
            get;
        }
        public string Path
        {
            get;
        }
        public string Sha256
        {
            get;
        }
        internal string ReceiveGateSerial
        {
            get;
        }
        public ProcessResult Runner
        {
            get;
        }

        internal void Verify(AuthorityFiles files)
        {
            var file = files.Open(System.IO.Path.GetDirectoryName(Path)!, Path, "original socket peer", "socket_peer_log", 65536, singleLink: true);
            byte[] raw = file.ReadBytes(65536, "socket_peer_log");
            if (Convert.ToHexString(SHA256.HashData(raw)) != Sha256)
                throw new ToolFailure("socket_peer_log_identity");
            InspectOutput(new UTF8Encoding(false, true).GetString(raw));
        }
    }
}
