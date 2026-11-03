namespace OS.Std.NoRuntime;
public readonly struct DotNetN10RuntimeExportCompatibilityEntry
{
    internal DotNetN10RuntimeExportCompatibilityEntry(DotNetN10RuntimeExport runtimeExport, DotNetN10RuntimeExportCategory category, DotNetN10RuntimeExportBehavior behavior, DotNetN10HelperSupportState supportState)
    {
        RuntimeExport = runtimeExport;
        Category = category;
        Behavior = behavior;
        SupportState = supportState;
    }

    public DotNetN10RuntimeExport RuntimeExport { get; }
    public DotNetN10RuntimeExportCategory Category { get; }
    public DotNetN10RuntimeExportBehavior Behavior { get; }
    public DotNetN10HelperSupportState SupportState { get; }
    public bool IsInitialized => RuntimeExport != DotNetN10RuntimeExport.Unknown && Category != DotNetN10RuntimeExportCategory.Unknown && Behavior != DotNetN10RuntimeExportBehavior.Unknown && SupportState != DotNetN10HelperSupportState.Unknown;
    public bool SatisfiesRuntimeCompletionGate4 => false;
}
