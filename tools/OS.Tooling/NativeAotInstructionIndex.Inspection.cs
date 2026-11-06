using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
// Shared host-only decoder index. Parsing binds exact executable bytes but does
// not trust the supplied instruction boundaries. Only a public prepared-program
// factory checking a reviewed complete digest can grant lookup authority.
internal sealed partial class NativeAotInstructionIndex
{
    internal readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
}
