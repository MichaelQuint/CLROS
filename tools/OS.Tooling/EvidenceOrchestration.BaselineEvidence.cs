using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static class BaselineEvidence
{
    public static Task<int> RunAsync(string root, string[] args, TextWriter output, CancellationToken cancellation = default) => EvidenceOrchestration.RunAsync(root, EvidenceOptions.Parse(args), true, output, cancellation);
}
