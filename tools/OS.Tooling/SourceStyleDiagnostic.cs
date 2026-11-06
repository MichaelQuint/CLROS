namespace OS.Tooling;

/// <summary>A deterministic host-only source organization finding. The path is
/// repository-relative and the line is one-based; it grants no build or guest authority.</summary>
public sealed record SourceStyleDiagnostic(string Path, int Line, string Rule, string Message)
{
    public override string ToString() => $"{Path}:{Line}: {Rule} {Message}";
}
