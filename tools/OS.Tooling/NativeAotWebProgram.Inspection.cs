using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Manifest;

namespace OS.Tooling;
public sealed partial class NativeAotWebProgram
{
    /// <summary>Complete decoder identity; a report alone grants no lookup authority.</summary>

    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
}
