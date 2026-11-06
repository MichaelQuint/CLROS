using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Tools.QemuRunner;

internal static partial class M02OwnedIdtGatePopulationEfiIntegrationTests
{
    private readonly record struct ScriptResult(int ExitCode, string Output);
}
