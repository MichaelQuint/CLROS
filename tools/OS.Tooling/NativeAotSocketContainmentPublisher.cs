namespace OS.Tooling;

/// <summary>Publish only the fixed containment fixture with the original socket
/// compiler recipe. Publication grants no application or guest execution proof.</summary>
public static class NativeAotSocketContainmentPublisher
{
    public const string Application="NativeAotSocketContainment";
    private static readonly NativeAotSocketFixturePublisher Publisher=new(Application,"containment",[
        ("Program.cs","FDC66C995386EFDB0DEC18BF6DB4BB7463FEB5C4BDD3C59E59FBD6F0CFC56B0C"),
        (Application+".csproj","D8AFB29D1F4465AD7D3EF3F4A292255DBC5E6975FFD4AD02948E912B1D88564B")]);

    /// <summary>Validate the exact original recipe and substitute only app/output.
    /// Caller retains input; returned owned arguments grant planning authority only.</summary>
    public static string[] PublishArguments(string originalCommands,string destination)=>Publisher.PublishArguments(originalCommands,destination);

    /// <summary>Linux-only publish from a fresh source cache and original recipe.
    /// Retains source/compiler/native outputs and failures under a fresh artifacts
    /// destination. No app executes; unsupported arguments or inputs return nonzero.</summary>
    public static Task<int> RunAsync(string root,string[] args,TextWriter output)=>Publisher.RunAsync(root,args,output);
}
