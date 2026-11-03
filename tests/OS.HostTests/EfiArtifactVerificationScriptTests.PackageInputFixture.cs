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
    private readonly record struct PackageInputFixture(string Path, long Bytes, string Sha256);
}
