using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OS.Boot;
using OS.Kernel.Memory;

// Reference boundary:
// - SharpOS Cr3Accessor.cs, local snapshot SHA-256
//   088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47,
//   was used only to cross-check MOV RAX,CR3 = 0F 20 D8.
// - SharpOS BootAsmGenerator.cs, local snapshot SHA-256
//   B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500,
//   influenced the independent exact-byte/semantic-plan audit pattern.
// - SharpOS PagingValidation.cs, local snapshot SHA-256
//   B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6,
//   supplied negative-test categories only.
// No reference generator, Iced dependency, live privileged path, or copied
// test implementation enters CLROS.
internal static unsafe partial class X64PagingModePreflightStubPlanTests
{
    private readonly record struct NoStdLibBuildResult(int ExitCode, bool TimedOut, string Output, byte[] AssemblyBytes);
}
