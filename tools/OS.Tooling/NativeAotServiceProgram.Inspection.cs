using OS.Manifest;

namespace OS.Tooling;
public sealed partial class NativeAotServiceProgram
{
    /// <summary>Value-only index report; this alone conveys no lookup authority.</summary>

    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
}
