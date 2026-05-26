using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalMemoryProtectionPlanTests
{
    private const ulong PageSize = OSHostMemoryProtectionPlan.DefaultPageSize;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.memory protect accepts write then execute", TestAcceptsWriteThenExecute, ref failures);
        Run("pal.memory protect rejects rwx", TestRejectsRwx, ref failures);
        Run("pal.memory protect rejects unaligned range", TestRejectsUnalignedRange, ref failures);
        Run("pal.memory protect rejects overflow", TestRejectsOverflow, ref failures);
        Run("pal.memory protect rejects invalid transition", TestRejectsInvalidTransition, ref failures);
        Run("pal.memory protect default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL memory protection plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAcceptsWriteThenExecute()
    {
        OSHostPageProtection readWrite =
            OSHostPageProtection.Read | OSHostPageProtection.Write;
        OSHostPageProtection readExecute =
            OSHostPageProtection.Read | OSHostPageProtection.Execute;

        OSHostMemoryProtectionPlanResult result = OSHostMemoryProtectionPlan.Create(
            baseAddress: 0x100000,
            byteLength: PageSize * 2,
            currentProtection: readWrite,
            requestedProtection: readExecute);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == OSHostMemoryProtectionPlanStatus.Success
            && result.HostStatus == OSHostStatus.Ok
            && result.BaseAddress == 0x100000
            && result.ByteLength == PageSize * 2
            && result.EndAddress == 0x102000
            && result.PageSizeBytes == PageSize
            && result.PageCount == 2
            && result.CurrentProtection == readWrite
            && result.RequestedProtection == readExecute;
    }

    private static bool TestRejectsRwx()
    {
        OSHostPageProtection readWrite =
            OSHostPageProtection.Read | OSHostPageProtection.Write;
        OSHostPageProtection readWriteExecute =
            OSHostPageProtection.Read |
            OSHostPageProtection.Write |
            OSHostPageProtection.Execute;

        OSHostMemoryProtectionPlanResult requestedRwx = OSHostMemoryProtectionPlan.Create(
            baseAddress: 0x100000,
            byteLength: PageSize,
            currentProtection: readWrite,
            requestedProtection: readWriteExecute);
        OSHostMemoryProtectionPlanResult currentRwx = OSHostMemoryProtectionPlan.Create(
            baseAddress: 0x101000,
            byteLength: PageSize,
            currentProtection: readWriteExecute,
            requestedProtection: OSHostPageProtection.Read | OSHostPageProtection.Execute);

        return !requestedRwx.Succeeded
            && requestedRwx.IsInitialized
            && requestedRwx.Status == OSHostMemoryProtectionPlanStatus.WritableExecutableRejected
            && requestedRwx.HostStatus == OSHostStatus.AccessDenied
            && !currentRwx.Succeeded
            && currentRwx.IsInitialized
            && currentRwx.Status == OSHostMemoryProtectionPlanStatus.WritableExecutableRejected
            && OSHostMemoryProtectionPlan.IsWritableExecutable(readWriteExecute);
    }

    private static bool TestRejectsUnalignedRange()
    {
        OSHostPageProtection readWrite =
            OSHostPageProtection.Read | OSHostPageProtection.Write;
        OSHostPageProtection readExecute =
            OSHostPageProtection.Read | OSHostPageProtection.Execute;

        OSHostMemoryProtectionPlanResult unalignedBase = OSHostMemoryProtectionPlan.Create(
            baseAddress: 0x100001,
            byteLength: PageSize,
            currentProtection: readWrite,
            requestedProtection: readExecute);
        OSHostMemoryProtectionPlanResult unalignedLength = OSHostMemoryProtectionPlan.Create(
            baseAddress: 0x101000,
            byteLength: PageSize + 1,
            currentProtection: readWrite,
            requestedProtection: readExecute);

        return !unalignedBase.Succeeded
            && unalignedBase.IsInitialized
            && unalignedBase.Status == OSHostMemoryProtectionPlanStatus.InvalidRange
            && unalignedBase.HostStatus == OSHostStatus.InvalidArgument
            && !unalignedLength.Succeeded
            && unalignedLength.IsInitialized
            && unalignedLength.Status == OSHostMemoryProtectionPlanStatus.InvalidRange;
    }

    private static bool TestRejectsOverflow()
    {
        OSHostPageProtection readWrite =
            OSHostPageProtection.Read | OSHostPageProtection.Write;
        OSHostPageProtection readExecute =
            OSHostPageProtection.Read | OSHostPageProtection.Execute;

        OSHostMemoryProtectionPlanResult result = OSHostMemoryProtectionPlan.Create(
            baseAddress: ulong.MaxValue - (PageSize - 1),
            byteLength: PageSize * 2,
            currentProtection: readWrite,
            requestedProtection: readExecute);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == OSHostMemoryProtectionPlanStatus.RangeOverflow
            && result.HostStatus == OSHostStatus.InvalidArgument
            && result.EndAddress == 0
            && result.PageCount == 0;
    }

    private static bool TestRejectsInvalidTransition()
    {
        OSHostMemoryProtectionPlanResult result = OSHostMemoryProtectionPlan.Create(
            baseAddress: 0x100000,
            byteLength: PageSize,
            currentProtection: OSHostPageProtection.Read | OSHostPageProtection.Execute,
            requestedProtection: OSHostPageProtection.Read | OSHostPageProtection.Write);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == OSHostMemoryProtectionPlanStatus.InvalidTransition
            && result.HostStatus == OSHostStatus.InvalidArgument;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostMemoryProtectionPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostMemoryProtectionPlanStatus.Success
            && result.HostStatus == OSHostStatus.Ok
            && result.BaseAddress == 0
            && result.ByteLength == 0
            && result.EndAddress == 0
            && result.PageSizeBytes == 0
            && result.PageCount == 0
            && result.CurrentProtection == OSHostPageProtection.None
            && result.RequestedProtection == OSHostPageProtection.None
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostMemoryProtectionPlanResult>();
    }
}
