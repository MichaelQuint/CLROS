using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed record EvidenceStep(string Name, string Executable, IReadOnlyList<string> Arguments, string LogPath)
{
    public string Image { get; init; } = "";
    public string DiskFormat { get; init; } = "";
    public string SerialLog { get; init; } = "";
    public string SummaryLog { get; init; } = "";
    public string DomainXml { get; init; } = "";
    public IReadOnlyList<string> MarkerPresets { get; init; } = [];
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    public bool IsHostGate
    {
        get; init;
    }
}
