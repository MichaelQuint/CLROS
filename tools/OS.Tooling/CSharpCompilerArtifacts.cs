namespace OS.Tooling;

/// <summary>Identifies the host compiler artifacts used to recover generated
/// syntax. This is discovery provenance, not native qualification evidence.</summary>
public sealed record CSharpCompilerArtifacts(string AssemblyPath, string AssemblySha256,
    string SymbolsPath, string SymbolsSha256)
{
    public string? ReceiptPath
    {
        get; init;
    }
    public string? ReceiptSha256
    {
        get; init;
    }
}
