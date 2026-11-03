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
    /// <summary>Identity of this owned peer, serial path and exact runner result.
    /// Only the process-owning RunAsync factory constructs a completion.</summary>

    public sealed class Completion
    {
        internal Completion(string root, int port, string path, string hash, ProcessResult runner, string serial, bool lifecycle)
        {
            Root = root;
            Port = port;
            Path = path;
            Sha256 = hash;
            Runner = runner;
            SerialPath = serial;
            Lifecycle = lifecycle;
        }

        internal string Root { get; }
        public int Port { get; }
        public string Path { get; }
        public string Sha256 { get; }
        public string SerialPath { get; }
        public ProcessResult Runner { get; }
        /// <summary>Factory-selected report contract, never inferred from supplied text.</summary>

        public bool Lifecycle { get; }

        internal void Verify(AuthorityFiles files)
        {
            var file = files.Open(System.IO.Path.GetDirectoryName(Path)!, Path, "original service peer", "service_peer_log", 65536, singleLink: true);
            byte[] raw = file.ReadBytes(65536, "service_peer_log");
            Need(Convert.ToHexString(SHA256.HashData(raw)) == Sha256, "log_identity");
            Need(InspectOutput(new UTF8Encoding(false, true).GetString(raw), Lifecycle).Port == Port, "port_identity");
        }
    }
}
