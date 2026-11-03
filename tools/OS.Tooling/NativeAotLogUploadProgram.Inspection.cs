namespace OS.Tooling;
public sealed partial class NativeAotLogUploadProgram
{
    /// <summary>Copied index identity. A report alone publishes no instruction lookup.</summary>

    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
}
