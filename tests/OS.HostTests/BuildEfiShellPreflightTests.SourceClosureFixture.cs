using OS.Tooling;
using OS.Tooling.Native;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class BuildEfiShellPreflightTests
{
    private sealed class SourceClosureFixture
    {
        public required string Root { get; init; }
        public required string ProjectPath { get; init; }
        public required string ScriptPath { get; init; }
        public required string SerializerTargetsPath { get; init; }
    }
}
