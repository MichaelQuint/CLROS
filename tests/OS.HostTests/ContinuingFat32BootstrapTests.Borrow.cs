using System.Reflection;
using System.Runtime.InteropServices;
using OS.Storage;
using OS.Std.Hash;
using K = OS.Storage.ContinuingFat32Bootstrap;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;

internal static unsafe partial class ContinuingFat32BootstrapTests
{
    private delegate bool Borrow(B.State* state, ulong epoch, ulong address, ulong bytes);
}
