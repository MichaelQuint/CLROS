using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;

internal static unsafe partial class ContinuingFat32MutationTests
{
    private delegate bool BorrowCheck(C.State* state, ulong epoch, ulong address, ulong bytes);
}
