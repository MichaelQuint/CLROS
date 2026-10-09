[AttributeUsage(AttributeTargets.Method)]
internal sealed class HostFixtureAttribute([System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") : Attribute
{
    public string SourcePath { get; } = sourcePath;
}
