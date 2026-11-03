using System.Text;

namespace OS.Tooling;
public sealed partial class NativeAotRuntimeProgram
{
    /// <summary>Value-only report; partial/synthetic reports grant no admission.</summary>

    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
}
