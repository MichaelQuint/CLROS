using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;

internal static partial class KestrelSmokePackageContractTests
{
    private readonly struct HostFixturePublish
    {
        public HostFixturePublish(string publishDirectory, bool valid = true)
        {
            this.PublishDirectory = publishDirectory;
            this.Valid = valid;
        }

        public string PublishDirectory { get; }
        public bool Valid { get; }
    }
}
