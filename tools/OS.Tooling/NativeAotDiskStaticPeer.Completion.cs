using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotDiskStaticPeer
{
    public sealed class Completion
    {
        internal Completion(int port, string directory, string hash, ProcessResult runner, bool full = false, bool truncated = false)
        {
            Port = port;
            Directory = directory;
            ReceiptSha256 = hash;
            Runner = runner;
            FullProfile = full;
            Truncated = truncated;
        }

        public int Port
        {
            get;
        }
        public string Directory
        {
            get;
        }
        public string ReceiptSha256
        {
            get;
        }
        public ProcessResult Runner
        {
            get;
        }
        public bool FullProfile
        {
            get;
        }
        public bool Truncated
        {
            get;
        }
    }
}
