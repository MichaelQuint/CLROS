using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotDiskTimingPeer
{
    public sealed class Completion
    {
        internal Completion(int port, string directory, ProcessResult runner)
        {
            Port = port;
            Directory = directory;
            Runner = runner;
        }

        public int Port
        {
            get;
        }
        public string Directory
        {
            get;
        }
        public ProcessResult Runner
        {
            get;
        }
    }
}
