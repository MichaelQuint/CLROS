using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Boot;
using OS.Kernel.Build;
using OS.Manifest;
using OS.Storage;

internal static partial class OsPackageBuilder
{
    private sealed class PackageSourceFile
    {
        public PackageSourceFile(string path, byte[] pathBytes, byte[] content)
        {
            Path = path;
            PathBytes = pathBytes;
            Content = content;
        }

        public string Path
        {
            get;
        }
        public byte[] PathBytes
        {
            get;
        }
        public byte[] Content
        {
            get;
        }
    }
}
