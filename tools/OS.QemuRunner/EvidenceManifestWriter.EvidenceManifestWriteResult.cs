using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tools.QemuRunner;
public readonly record struct EvidenceManifestWriteResult(EvidenceManifestWriteStatus Status, string ManifestPath, string ManifestSha256, string Reason)
{
    public bool Succeeded => Status == EvidenceManifestWriteStatus.Success;
}
