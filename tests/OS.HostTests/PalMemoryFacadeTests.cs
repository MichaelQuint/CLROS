using System.Runtime.CompilerServices;
using OS.Pal;

internal static class PalMemoryFacadeTests
{
    private const ulong PageSize = OSHostMemoryProtectionPlan.DefaultPageSize;
    private const ulong BaseAddress = 0x600000;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.memory facade translates alloc free protect query", TestTranslatesAllocFreeProtectQuery, ref failures);
        Run("pal.memory facade rejects null outputs and table", TestRejectsNullOutputsAndTable, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL memory facade host tests failed: " + failures);
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

    private static unsafe bool TestTranslatesAllocFreeProtectQuery()
    {
        OSHostMemoryTable table = NewTable(pageCapacity: 4, handleCapacity: 1);
        OSHostMemoryFacade facade = new(table);
        OSHostHandle allocation;
        OSHostMemoryRange range;

        if (facade.AllocPages(Request(pageCount: 1), &allocation, &range) != OSHostStatus.Ok ||
            !allocation.IsValid ||
            range.BaseAddress != BaseAddress ||
            range.ByteLength != PageSize)
        {
            return false;
        }

        OSHostMemoryQueryResult query;
        OSHostMemoryRange executeRange = new(
            range.BaseAddress,
            range.ByteLength,
            (ulong)(OSHostPageProtection.Read | OSHostPageProtection.Execute));

        if (facade.QueryMemory(range.BaseAddress, &query) != OSHostStatus.Ok ||
            query.Range.ProtectionFlags != range.ProtectionFlags ||
            facade.ProtectPages(executeRange) != OSHostStatus.Ok ||
            facade.QueryMemory(range.BaseAddress, &query) != OSHostStatus.Ok ||
            query.Range.ProtectionFlags != executeRange.ProtectionFlags)
        {
            return false;
        }

        return facade.FreePages(allocation) == OSHostStatus.Ok
            && facade.FreePages(allocation) == OSHostStatus.InvalidArgument
            && facade.QueryMemory(range.BaseAddress, &query) == OSHostStatus.NotFound;
    }

    private static unsafe bool TestRejectsNullOutputsAndTable()
    {
        OSHostMemoryTable table = NewTable(pageCapacity: 1, handleCapacity: 1);
        OSHostMemoryFacade facade = new(table);
        OSHostMemoryFacade nullFacade = new(table: null);
        OSHostHandle allocation = new(0xFFFF);
        OSHostMemoryRange range = new(0xFFFF, 0xFFFF, 0xFFFF);
        OSHostMemoryQueryResult query;
        OSHostMemoryRange protectRange = new(
            BaseAddress,
            PageSize,
            (ulong)(OSHostPageProtection.Read | OSHostPageProtection.Execute));

        if (facade.AllocPages(Request(pageCount: 1), null, &range) != OSHostStatus.InvalidArgument ||
            facade.AllocPages(Request(pageCount: 1), &allocation, null) != OSHostStatus.InvalidArgument ||
            facade.QueryMemory(BaseAddress, null) != OSHostStatus.InvalidArgument)
        {
            return false;
        }

        OSHostStatus nullAllocStatus = nullFacade.AllocPages(Request(pageCount: 1), &allocation, &range);
        return nullAllocStatus == OSHostStatus.InvalidArgument
            && !allocation.IsValid
            && range.BaseAddress == 0
            && nullFacade.FreePages(new OSHostHandle(1)) == OSHostStatus.InvalidArgument
            && nullFacade.ProtectPages(protectRange) == OSHostStatus.InvalidArgument
            && nullFacade.QueryMemory(BaseAddress, &query) == OSHostStatus.InvalidArgument;
    }

    private static OSHostMemoryTable NewTable(ulong pageCapacity, int handleCapacity)
    {
        return new OSHostMemoryTable(
            PageSize,
            BaseAddress,
            pageCapacity,
            handleCapacity);
    }

    private static OSHostMemoryRequest Request(ulong pageCount)
    {
        return new OSHostMemoryRequest(
            pageCount,
            PageSize,
            (ulong)(OSHostPageProtection.Read | OSHostPageProtection.Write));
    }
}
