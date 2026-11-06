using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;

// Checkpoints retain historical evidence. They never replay TEST_PASS output.
internal sealed partial class HostRunCheckpoint
{
    internal sealed record Selection(string Lane, string? Filter, string? CaseFilter);
}
