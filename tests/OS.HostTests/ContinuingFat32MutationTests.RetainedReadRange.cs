using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Storage;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;

internal static unsafe partial class ContinuingFat32MutationTests
{
    private delegate bool RetainedReadRange(C.State* state, ulong epoch, B.State* block, ulong blockEpoch, ulong first, ulong last);
}
