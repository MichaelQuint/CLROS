namespace OS.Tooling;

internal static class EfiSerializerInputs
{
    // Independently reviewed compiler membership. Actual SDK evaluation must
    // match this ordered inventory; discovery cannot approve extra inputs.
    // The extraction preserves declarations, member bodies and layout. Fresh
    // emitted/native qualification remains required for the combined candidate.
    internal static IReadOnlyList<string> CompileItems
    {
        get;
    } = Array.AsReadOnly<string>(
    [
        "Amd64CoffWriter.Amd64CoffObjectDefinition.cs",
        "Amd64CoffWriter.CoffRelocationDefinition.cs",
        "Amd64CoffWriter.CoffSectionDefinition.cs",
        "Amd64CoffWriter.CoffSectionLayout.cs",
        "Amd64CoffWriter.CoffSymbolDefinition.cs",
        "Amd64CoffWriter.CoffWriteError.cs",
        "Amd64CoffWriter.CoffWritePlan.cs",
        "Amd64CoffWriter.cs",
        "CheckedReferenceBarrierThunkGenerator.cs",
        "EhContributionCatalogGenerator.Contribution.cs",
        "EhContributionCatalogGenerator.cs",
        "EhContributionCatalogGenerator.Node.cs",
        "EhContributionCatalogGenerator.Symbol.cs",
        "ExceptionFilterThunkGenerator.cs",
        "ExceptionHandlingThunkGenerator.cs",
        "InstructionPublicationSerializerGenerator.cs",
        "InterfaceDispatchThunkGenerator.cs",
        "NormalFinallyThunkGenerator.cs",
        "Program.CliParseResult.cs",
        "Program.cs",
        "Program.OutputWriteResult.cs",
    ]);

    internal static IReadOnlyList<string> BuildInputs
    {
        get;
    } = Array.AsReadOnly<string>(
        ["OS.X64TextSerializerGenerator.csproj", .. CompileItems, "OS.X64TextSerializerGenerator.targets"]);
}
