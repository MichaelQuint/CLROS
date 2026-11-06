using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

internal static partial class EvidenceManifestWriterTests
{
    private sealed record EvidenceFixture(string Root, string RepositoryPath, string Commit, string CommandFile, EvidenceManifestRequest PassingRequest);
}
