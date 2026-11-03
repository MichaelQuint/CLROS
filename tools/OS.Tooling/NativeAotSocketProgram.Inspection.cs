using System.Text;

namespace OS.Tooling;
public sealed partial class NativeAotSocketProgram
{
    /// <summary>Value-only index identity; reports grant no lookup authority.</summary>

    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
}
