using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tools.QemuRunner;
public sealed class EvidenceManifestRequest
{
    public string RepositoryPath { get; init; } = string.Empty;
    public string ExpectedSourceCommit { get; init; } = string.Empty;
    public bool ExpectedSourceDirty { get; init; }
    public DateTimeOffset StartedUtc { get; init; }
    public DateTimeOffset CompletedUtc { get; init; }
    public string Command { get; init; } = string.Empty;
    public int ExitCode { get; init; }
    public EvidenceRunClassification Classification { get; init; }
    public IReadOnlyDictionary<string, string> ToolVersions { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public IReadOnlyList<EvidenceArtifactInput> Artifacts { get; init; } = Array.Empty<EvidenceArtifactInput>();
    public IReadOnlyList<string> RequiredMarkers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ForbiddenMarkers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ObservedMarkers { get; init; } = Array.Empty<string>();
}
