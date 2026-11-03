using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tools.QemuRunner;

public static partial class EvidenceManifestWriter
{
    private sealed record EvidenceArtifactRecord(string Name, string Role, string Path, long Length, string Sha256);
}
