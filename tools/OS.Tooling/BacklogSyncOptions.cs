using System.Text.Json.Nodes;

namespace OS.Tooling;

/// <summary>Validated CLI scope for reconciliation against the explicitly pinned LAN repository.</summary>
public sealed record BacklogSyncOptions
{
    public bool Apply { get; init; }
    public bool Offline { get; init; }
    public bool SelfTest { get; init; }
    public string IssueKeys { get; init; } = "";
    public string ManifestPath { get; init; } = "";
    public string BaseUrl { get; init; } = BacklogContracts.BaseUrl;
    public string Owner { get; init; } = BacklogContracts.Owner;
    public string Repository { get; init; } = BacklogContracts.Repository;
    /// <summary>Parse options without contacting the tracker; writes require --apply.</summary>
    public static BacklogSyncOptions Parse(string[] args)
    {
        BacklogSyncOptions value = new();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException("missing_option_value");
            value = CommandOptions.Key(args[i]) switch
            {
                "apply" => value with { Apply = true }, "offline" => value with { Offline = true }, "selftest" => value with { SelfTest = true },
                "manifestpath" => value with { ManifestPath = Next() }, "baseurl" => value with { BaseUrl = Next() },
                "issuekeys" => value with { IssueKeys = Next() },
                "owner" => value with { Owner = Next() }, "repository" => value with { Repository = Next() },
                _ => throw new ArgumentException("unknown_option " + args[i])
            };
        }
        value.Validate(); return value;
    }
    /// <summary>Reject accidental endpoint changes and incompatible execution modes.</summary>
    public void Validate()
    {
        if (BaseUrl.TrimEnd('/') != BacklogContracts.BaseUrl || Owner != BacklogContracts.Owner || Repository != BacklogContracts.Repository)
            throw new ArgumentException("Refusing non-canonical target. Expected " + BacklogContracts.BaseUrl + "/" + BacklogContracts.Owner + "/" + BacklogContracts.Repository + ".");
        if (Apply && (Offline || SelfTest)) throw new ArgumentException("offline/self-test cannot be combined with apply");
    }
}

/// <summary>Injectable transport for deterministic offline policy tests.</summary>
public delegate Task<JsonNode?> BacklogApi(string method, string path, JsonObject? body, CancellationToken cancellation);
