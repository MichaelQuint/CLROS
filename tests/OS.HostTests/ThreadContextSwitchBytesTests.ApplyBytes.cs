using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

internal static unsafe partial class ThreadContextSwitchBytesTests
{
    private delegate void ApplyBytes(nint output, uint position, uint limit, bool compare, bool valid, ulong value, int count, out uint finalPosition, out bool finalValid);
}
