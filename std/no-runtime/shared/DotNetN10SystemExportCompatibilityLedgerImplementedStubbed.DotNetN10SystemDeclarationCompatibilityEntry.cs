namespace OS.Std.NoRuntime;
public readonly struct DotNetN10SystemDeclarationCompatibilityEntry
{
    internal DotNetN10SystemDeclarationCompatibilityEntry(DotNetN10SystemDeclaration declaration, DotNetN10SystemDeclarationCategory category, DotNetN10HelperSupportState supportState)
    {
        Declaration = declaration;
        Category = category;
        SupportState = supportState;
    }

    public DotNetN10SystemDeclaration Declaration { get; }
    public DotNetN10SystemDeclarationCategory Category { get; }
    public DotNetN10HelperSupportState SupportState { get; }
    public bool IsInitialized => Declaration != DotNetN10SystemDeclaration.Unknown && Category != DotNetN10SystemDeclarationCategory.Unknown && SupportState != DotNetN10HelperSupportState.Unknown;
    public bool SatisfiesRuntimeCompletionGate4 => false;
}
