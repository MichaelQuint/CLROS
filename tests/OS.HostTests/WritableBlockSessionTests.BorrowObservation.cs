using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.WritableBlockSession;

internal static unsafe partial class WritableBlockSessionTests
{
    private delegate bool BorrowObservation(OS.Storage.ContinuingFat32Mutation.State* state, ulong epoch, ulong address, ulong bytes);
}
