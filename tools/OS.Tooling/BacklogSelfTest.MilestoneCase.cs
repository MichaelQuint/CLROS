using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static OS.Tooling.BacklogManifest;

namespace OS.Tooling;
public static partial class BacklogSelfTest
{
    private sealed record MilestoneCase(string Name, string Intent, string Current, string Action, string Desired, string Failure = "");
}
