using System.Collections.ObjectModel;

namespace OS.Tooling;

/// <summary>Reviewed backlog inventory. Update this single inventory when accepting a new slice.</summary>
public static class BacklogContracts
{
    public const string BaseUrl = "http://192.168.4.35:3000";
    public const int RepositoryId = 9;
    public const string Owner = "admin";
    public const string Repository = "CLROS";
    public const string ManagedStart = "<!-- clros-managed-start -->";
    public const string ManagedEnd = "<!-- clros-managed-end -->";
    public const string ClosureEvidenceMarkerFormat = "<!-- clros-evidence-reviewed:{key} -->";
    public const string OwnershipNote = "The ownedPaths inventory is the exact scope for this slice. Any additional shared integration file requires a separately claimed collision-lane issue.";
    public static IReadOnlyDictionary<string, int> Children { get; } = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["M00"] = 23, ["M01"] = 36, ["M02"] = 20, ["M03"] = 11,
        ["M04"] = 25, ["M05"] = 21, ["M06"] = 21, ["M07"] = 34,
        ["M08"] = 20, ["M09"] = 30, ["M10"] = 27, ["M11"] = 33,
        ["M12"] = 18, ["M13"] = 18, ["M14"] = 18, ["M15"] = 16,
        ["M16"] = 27
    });
    public static int ChildCount => Children.Values.Sum();
    public static int IssueCount => Children.Count + ChildCount;
    public static IReadOnlyList<string> RequiredLabels { get; } = Array.AsReadOnly<string>([
        "agent:ready", "agent:claimed", "agent:blocked", "agent:review", "agent:done",
        "priority:p0", "priority:p1", "priority:p2", "priority:p3", "type:epic", "type:implementation",
        "type:test", "type:tooling", "type:docs", "type:research", "evidence:host", "evidence:qemu", "evidence:libvirt", "evidence:aws", "evidence:azure"]);
    public static IReadOnlyList<string> RequiredIssueFields { get; } = Array.AsReadOnly<string>([
        "key", "title", "milestone", "parentKey", "dependencyKeys", "labels", "priority", "ownedPaths", "forbiddenPaths", "collisionLane",
        "summary", "currentEvidence", "implementationNotes", "referenceInfluences", "acceptanceCriteria", "requiredMarkers", "requiredCommands",
        "documentationPaths", "evidenceBoundary", "desiredState"]);
    public static string EvidenceMarker(string key) => ClosureEvidenceMarkerFormat.Replace("{key}", key.ToUpperInvariant(), StringComparison.Ordinal);
}
