using System.Diagnostics;
using OS.Tooling;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static partial class EfiArtifactVerificationScriptTests
{
    private readonly record struct PackageDescriptor(string Id, string Version, string RootKind, string ContentHashSha512, int FileCount, long TotalBytes, string AggregateSha256);
}
