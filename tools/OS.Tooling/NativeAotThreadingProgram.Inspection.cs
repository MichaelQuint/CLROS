using System.Text;

namespace OS.Tooling;

public sealed partial class NativeAotThreadingProgram
{
    /// <summary>Value-only observation metadata; never prepared-program authority.</summary>

    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
}
