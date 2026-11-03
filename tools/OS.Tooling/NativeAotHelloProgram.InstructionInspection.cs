using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace OS.Tooling;

public sealed partial class NativeAotHelloProgram
{
    /// <summary>Value-only parser metadata; no instruction lookup or prepared-program authority.</summary>

    public readonly record struct InstructionInspection(int InstructionCount, int SyscallCount, string Sha256);
}
