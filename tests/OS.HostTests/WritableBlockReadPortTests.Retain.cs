using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using P = OS.Storage.WritableBlockReadPort;

internal static unsafe partial class WritableBlockReadPortTests
{
    private delegate bool Retain(P.State* state, ulong epoch, ulong address, ulong bytes);
}
