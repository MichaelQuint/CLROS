using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal sealed partial class HostTestFingerprint
{
    internal readonly record struct FileStamp(bool Exists, long Length, long Written, long Created, FileAttributes Attributes)
    {
        internal static FileStamp Read(string path)
        {
            var file = new FileInfo(path);
            return file.Exists ? new(true, file.Length, file.LastWriteTimeUtc.Ticks, file.CreationTimeUtc.Ticks, file.Attributes) : default;
        }
    }
}
