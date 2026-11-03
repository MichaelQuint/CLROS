using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;
public static partial class ToolingBuildInputs
{
    private sealed record Manifest(IReadOnlyList<string> Patterns, IReadOnlyDictionary<string, string> Files);
}
