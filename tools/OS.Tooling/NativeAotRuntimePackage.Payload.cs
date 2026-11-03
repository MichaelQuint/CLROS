using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

public static partial class NativeAotRuntimePackage
{
    internal readonly record struct Payload(string Name, int Bytes, string Hash, string Source, string Guest, int DllBytes, string DllHash, int ObjectBytes, string ObjectHash, int StdoutBytes, string StdoutHash, int Traces);
}
