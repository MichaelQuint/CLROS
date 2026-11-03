using System.Security.Cryptography;
using OS.Manifest;

namespace OS.Tooling;
public sealed partial class NativeAotStaticFileProgram
{
    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
}
