using OS.Tooling;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static partial class M02Ud2VectorStateEfiIntegrationTests
{
    private readonly record struct SourceAuthoritySnapshot(string ProjectSha256, string EntrySha256, string SourceClosureSha256, string CompileSha256, int SourceInputCount, int CompileCount);
}
