using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Checkpoints retain historical evidence. They never replay TEST_PASS output.
internal sealed partial class HostRunCheckpoint
{
    internal sealed record Entry(string Fingerprint, string Origin, string LogHash, HostTestRunner.FixtureResult Result, Dictionary<string, string> Evidence, string Producer, string ProducerHash)
    {
        public Dictionary<string, string> ArtifactSources { get; init; } = new(StringComparer.Ordinal);
    }
}
