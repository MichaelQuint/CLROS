using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

internal static partial class M04ArithmeticConformanceEfiIntegrationTests
{
    private readonly record struct ScriptResult(int ExitCode, string Output);
}
