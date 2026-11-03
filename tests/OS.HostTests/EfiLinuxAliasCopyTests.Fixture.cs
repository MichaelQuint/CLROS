using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;
using System.Runtime.Loader;

internal static partial class EfiLinuxAliasCopyTests
{
    private sealed class Fixture(AssemblyLoadContext context, Action<int> run) : IDisposable
    {
        internal void Run(int section) => run(section);
        public void Dispose() => context.Unload();
    }
}
