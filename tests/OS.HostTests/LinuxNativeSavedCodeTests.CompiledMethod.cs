using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

internal static unsafe partial class LinuxNativeSavedCodeTests
{
    private sealed class CompiledMethod(AssemblyLoadContext context, Func<nint, nint, bool> compare) : IDisposable
    {
        internal bool Compare(nint code, nint saved) => compare(code, saved);
        public void Dispose() => context.Unload();
    }
}
