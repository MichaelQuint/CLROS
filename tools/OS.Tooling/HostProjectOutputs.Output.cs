namespace OS.Tooling;

internal static partial class HostProjectOutputs
{
    internal sealed record Output(string Project, string TargetPath, string Sha256,
        string Configuration, string Platform, string Framework, string RuntimeIdentifier)
    {
        internal string Name => Path.GetFileNameWithoutExtension(Project);
    }
}
