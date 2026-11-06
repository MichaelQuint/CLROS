using OS.Tooling;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;

internal static partial class M02UnexpectedVectorPanicEfiIntegrationTests
{
    private readonly record struct Result(int Code, string Output, string Error);
}
