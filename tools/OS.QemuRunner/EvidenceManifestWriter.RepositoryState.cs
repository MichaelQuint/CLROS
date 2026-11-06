using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tools.QemuRunner;

public static partial class EvidenceManifestWriter
{
    private readonly record struct RepositoryState(string Commit, bool Dirty);
}
