using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class NativeAotStagedStaticPeer
{
    public sealed class Completion
    {
        internal Completion(int port, string directory, string receiptHash, ProcessResult runner)
        {
            Port = port;
            Directory = directory;
            ReceiptSha256 = receiptHash;
            Runner = runner;
        }

        public int Port { get; }
        public string Directory { get; }
        public string ReceiptSha256 { get; }
        public ProcessResult Runner { get; }
    }
}
