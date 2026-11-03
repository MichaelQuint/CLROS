using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

internal static unsafe partial class ThreadContextSwitchBytesTests
{
    private sealed class CompiledMethod(AssemblyLoadContext context, ApplyBytes apply) : IDisposable
    {
        internal void Apply(nint output, uint position, uint limit, bool compare, bool valid, ulong value, int count, out uint finalPosition, out bool finalValid) => apply(output, position, limit, compare, valid, value, count, out finalPosition, out finalValid);
        public void Dispose() => context.Unload();
    }
}
