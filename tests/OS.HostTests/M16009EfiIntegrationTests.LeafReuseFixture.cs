using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;
using System.Runtime.Loader;

internal static unsafe partial class M16009EfiIntegrationTests
{
    private sealed class LeafReuseFixture(AssemblyLoadContext context, Action<int> run) : IDisposable
    {
        internal void Run(int section) => run(section);
        public void Dispose() => context.Unload();
    }
}
