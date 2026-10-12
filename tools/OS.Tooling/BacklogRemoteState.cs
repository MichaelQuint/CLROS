using System.Text.Json.Nodes;

namespace OS.Tooling;

/// <summary>One tracker snapshot, retaining unselected issues for dependency and ownership decisions.</summary>
public sealed class BacklogRemoteState
{
    public Dictionary<string, JsonObject> Labels { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, JsonObject> Milestones { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, JsonObject> Issues { get; } = new(StringComparer.Ordinal);
    public List<JsonObject> AllIssues { get; } = [];
    public HashSet<string> ReviewedClosures { get; } = new(StringComparer.Ordinal);
}
