using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

public static partial class NativeAotThreadingPackage
{
    internal readonly record struct Payload(string Name, int Bytes, string Hash, uint Threads, string Source, int DllBytes, string DllHash, int ObjectBytes, string ObjectHash, int StdoutBytes, string StdoutHash, int Traces);
}
