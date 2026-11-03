using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class NativeAotSocketVectorsPeer
{
    /// <summary>Immutable identity of this owned vector peer and its exact
    /// runner result. No original socket peer Completion is substituted.</summary>

    public sealed class Completion
    {
        internal Completion(string root, int port, string path, string hash, ProcessResult runner, string serial)
        {
            Root = root;
            Port = port;
            Path = path;
            Sha256 = hash;
            Runner = runner;
            SerialPath = serial;
        }

        internal string Root { get; }
        public int Port { get; }
        public string Path { get; }
        public string Sha256 { get; }
        public string SerialPath { get; }
        public ProcessResult Runner { get; }

        internal void Verify(AuthorityFiles files)
        {
            var file = files.Open(System.IO.Path.GetDirectoryName(Path)!, Path, "original vector socket peer", "socket_vectors_peer_log", 65536, singleLink: true);
            byte[] raw = file.ReadBytes(65536, "socket_vectors_peer_log");
            if (Convert.ToHexString(SHA256.HashData(raw)) != Sha256)
                throw new ToolFailure("socket_vectors_peer_log_identity");
            InspectOutput(new UTF8Encoding(false, true).GetString(raw), requireSendBackpressure: true);
        }
    }
}
