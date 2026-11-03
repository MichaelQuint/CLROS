namespace OS.X64TextSerializerGenerator;

/// <summary>The accepted catch transfer instructions with separately admitted normal-finally callbacks.</summary>
public static class NormalFinallyThunkGenerator
{
    public static Amd64CoffObjectDefinition Define()
    {
        var original = ExceptionHandlingThunkGenerator.Define();
        var symbols = original.Symbols!.Select(symbol => symbol with
        {
            Name = symbol.Name switch
            {
                ExceptionHandlingThunkGenerator.Select => "CLROS_NormalFinally_Select",
                ExceptionHandlingThunkGenerator.Validate => "CLROS_NormalFinally_ValidateContinuation",
                ExceptionHandlingThunkGenerator.GetClauseTable => "CLROS_NormalFinally_Clauses",
                ExceptionHandlingThunkGenerator.ClauseTable => "__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__NormalCatcher",
                _ => symbol.Name
            }
        }).ToArray();
        return original with
        {
            Symbols = symbols
        };
    }
}
