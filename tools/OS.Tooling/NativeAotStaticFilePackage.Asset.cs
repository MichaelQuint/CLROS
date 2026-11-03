using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;
public static partial class NativeAotStaticFilePackage
{
    public sealed record Asset(string Path, byte[] Bytes);
}
