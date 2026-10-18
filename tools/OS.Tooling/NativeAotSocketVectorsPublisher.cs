namespace OS.Tooling;

/// <summary>Publish the fixed reviewed BufferList fixture using the accepted
/// socket compiler recipe. Publication grants no Linux or guest execution proof.</summary>
public static class NativeAotSocketVectorsPublisher
{
    public const string Application="NativeAotSocketVectors";
    private static readonly NativeAotSocketFixturePublisher Publisher=new(Application,"vectors",[
        ("Program.cs","B6D30356A7A372FE6A3C355022FB33B219767A8B0F271EB57123044B39340752"),
        ("SocketVectorsFixture.cs","63C61901A23E85535CFB120F1DA5DEBA07DEDBFA604F83A7D06A26294EBE5D5A"),
        (Application+".csproj","D8AFB29D1F4465AD7D3EF3F4A292255DBC5E6975FFD4AD02948E912B1D88564B")]);

    /// <summary>Validate the exact original recipe and substitute only app/output.
    /// Caller retains input; returned owned arguments grant planning authority only.</summary>
    public static string[] PublishArguments(string originalCommands,string destination)=>Publisher.PublishArguments(originalCommands,destination);

    /// <summary>Check the three exact current app files and absence of build
    /// overrides/source links. Generated bin/obj are excluded; changed sources throw.
    /// This validates app identity only, without publication or execution authority.</summary>
    public static void RequireCurrentSources(string root)=>Publisher.RequireCurrentSources(root);

    /// <summary>Linux-only publish from a fresh cache and immutable socket recipe.
    /// Owns a fresh artifacts destination and retains source/native outputs/failures.
    /// No app executes; unsupported arguments or inputs return nonzero.</summary>
    public static Task<int> RunAsync(string root,string[] args,TextWriter output)=>Publisher.RunAsync(root,args,output);
}
