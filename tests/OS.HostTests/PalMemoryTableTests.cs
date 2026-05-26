using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Pal;

internal static class PalMemoryTableTests
{
    private const ulong PageSize = OSHostMemoryProtectionPlan.DefaultPageSize;
    private const ulong BaseAddress = 0x400000;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.memory table allocates aligned pages", TestAllocatesAlignedPages, ref failures);
        Run("pal.memory table rejects invalid request shape", TestRejectsInvalidRequestShape, ref failures);
        Run("pal.memory table reports capacity exhaustion", TestReportsCapacityExhaustion, ref failures);
        Run("pal.memory table reuses freed ranges and rejects stale handles", TestReusesFreedRangesAndRejectsStaleHandles, ref failures);
        Run("pal.memory table protects write then execute", TestProtectsWriteThenExecute, ref failures);
        Run("pal.memory table rejects stale and partial ranges", TestRejectsStaleAndPartialRanges, ref failures);
        Run("pal.memory table layouts are stable", TestLayoutsAreStable, ref failures);
        Run("pal.memory table default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL memory table host tests failed: " + failures);
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

    private static bool TestAllocatesAlignedPages()
    {
        OSHostMemoryTable table = NewTable(pageCapacity: 8, handleCapacity: 2);
        OSHostMemoryAllocationResult alloc = table.AllocPages(Request(pageCount: 2, alignment: PageSize * 2));

        if (!alloc.Succeeded ||
            !alloc.Allocation.IsValid ||
            alloc.Range.BaseAddress != BaseAddress ||
            alloc.Range.ByteLength != PageSize * 2 ||
            alloc.Range.ProtectionFlags != (ulong)ReadWrite ||
            table.ActiveCount != 1)
        {
            return false;
        }

        if (table.TryGetEntry(alloc.Allocation, out OSHostMemoryTableEntry entry) != OSHostStatus.Ok ||
            entry.Allocation.Value != alloc.Allocation.Value ||
            entry.Range.BaseAddress != BaseAddress ||
            entry.StateFlags != OSHostMemoryStateFlags.Allocated)
        {
            return false;
        }

        OSHostStatus queryStatus = table.QueryMemory(BaseAddress + PageSize, out OSHostMemoryQueryResult query);
        return queryStatus == OSHostStatus.Ok
            && query.Range.BaseAddress == alloc.Range.BaseAddress
            && query.Range.ByteLength == alloc.Range.ByteLength
            && query.PageSizeBytes == PageSize
            && query.StateFlags == OSHostMemoryStateFlags.Allocated;
    }

    private static bool TestRejectsInvalidRequestShape()
    {
        OSHostMemoryTable table = NewTable(pageCapacity: 8, handleCapacity: 2);
        OSHostMemoryTable invalidPageTable = new(
            pageSizeBytes: 3000,
            baseAddress: BaseAddress,
            pageCapacity: 8,
            handleCapacity: 2);
        OSHostMemoryTable invalidBaseTable = new(
            pageSizeBytes: PageSize,
            baseAddress: BaseAddress + 1,
            pageCapacity: 8,
            handleCapacity: 2);

        OSHostMemoryAllocationResult zeroPages = table.AllocPages(Request(pageCount: 0, alignment: PageSize));
        OSHostMemoryAllocationResult smallAlignment = table.AllocPages(Request(pageCount: 1, alignment: PageSize / 2));
        OSHostMemoryAllocationResult nonPowerOfTwoAlignment = table.AllocPages(Request(pageCount: 1, alignment: PageSize + 512));
        OSHostMemoryAllocationResult directExecute = table.AllocPages(Request(pageCount: 1, alignment: PageSize, ReadExecute));
        OSHostMemoryAllocationResult rwx = table.AllocPages(Request(pageCount: 1, alignment: PageSize, ReadWrite | OSHostPageProtection.Execute));
        OSHostMemoryAllocationResult invalidPage = invalidPageTable.AllocPages(Request(pageCount: 1, alignment: PageSize));
        OSHostMemoryAllocationResult invalidBase = invalidBaseTable.AllocPages(Request(pageCount: 1, alignment: PageSize));

        return zeroPages.Status == OSHostStatus.InvalidArgument
            && smallAlignment.Status == OSHostStatus.InvalidArgument
            && nonPowerOfTwoAlignment.Status == OSHostStatus.InvalidArgument
            && directExecute.Status == OSHostStatus.InvalidArgument
            && rwx.Status == OSHostStatus.AccessDenied
            && invalidPage.Status == OSHostStatus.InvalidArgument
            && invalidBase.Status == OSHostStatus.InvalidArgument
            && table.ActiveCount == 0;
    }

    private static bool TestReportsCapacityExhaustion()
    {
        OSHostMemoryTable handleLimited = NewTable(pageCapacity: 8, handleCapacity: 1);
        OSHostMemoryAllocationResult first = handleLimited.AllocPages(Request(pageCount: 1, alignment: PageSize));
        OSHostMemoryAllocationResult noHandle = handleLimited.AllocPages(Request(pageCount: 1, alignment: PageSize));

        OSHostMemoryTable pageLimited = NewTable(pageCapacity: 1, handleCapacity: 2);
        OSHostMemoryAllocationResult tooManyPages = pageLimited.AllocPages(Request(pageCount: 2, alignment: PageSize));

        return first.Succeeded
            && noHandle.Status == OSHostStatus.OutOfMemory
            && tooManyPages.Status == OSHostStatus.OutOfMemory
            && handleLimited.ActiveCount == 1
            && pageLimited.ActiveCount == 0;
    }

    private static bool TestReusesFreedRangesAndRejectsStaleHandles()
    {
        OSHostMemoryTable table = NewTable(pageCapacity: 4, handleCapacity: 1);
        OSHostMemoryAllocationResult first = table.AllocPages(Request(pageCount: 1, alignment: PageSize));

        if (!first.Succeeded ||
            table.FreePages(first.Allocation) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostStatus staleFree = table.FreePages(first.Allocation);
        OSHostStatus staleEntry = table.TryGetEntry(first.Allocation, out _);
        OSHostStatus staleQuery = table.QueryMemory(first.Range.BaseAddress, out _);
        OSHostMemoryAllocationResult second = table.AllocPages(Request(pageCount: 1, alignment: PageSize));

        return staleFree == OSHostStatus.InvalidArgument
            && staleEntry == OSHostStatus.InvalidArgument
            && staleQuery == OSHostStatus.NotFound
            && second.Succeeded
            && second.Range.BaseAddress == first.Range.BaseAddress
            && second.Allocation.Value != first.Allocation.Value;
    }

    private static bool TestProtectsWriteThenExecute()
    {
        OSHostMemoryTable table = NewTable(pageCapacity: 4, handleCapacity: 1);
        OSHostMemoryAllocationResult alloc = table.AllocPages(Request(pageCount: 1, alignment: PageSize));
        OSHostMemoryRange executeRange = new(
            alloc.Range.BaseAddress,
            alloc.Range.ByteLength,
            (ulong)ReadExecute);

        if (!alloc.Succeeded ||
            table.ProtectPages(executeRange) != OSHostStatus.Ok ||
            table.QueryMemory(alloc.Range.BaseAddress, out OSHostMemoryQueryResult query) != OSHostStatus.Ok)
        {
            return false;
        }

        OSHostMemoryRange writeAgain = new(
            alloc.Range.BaseAddress,
            alloc.Range.ByteLength,
            (ulong)ReadWrite);
        OSHostMemoryRange rwx = new(
            alloc.Range.BaseAddress,
            alloc.Range.ByteLength,
            (ulong)(ReadWrite | OSHostPageProtection.Execute));

        return query.Range.ProtectionFlags == (ulong)ReadExecute
            && table.ProtectPages(writeAgain) == OSHostStatus.InvalidArgument
            && table.ProtectPages(rwx) == OSHostStatus.AccessDenied;
    }

    private static bool TestRejectsStaleAndPartialRanges()
    {
        OSHostMemoryTable table = NewTable(pageCapacity: 4, handleCapacity: 1);
        OSHostMemoryAllocationResult alloc = table.AllocPages(Request(pageCount: 2, alignment: PageSize));
        OSHostMemoryRange partial = new(
            alloc.Range.BaseAddress,
            PageSize,
            (ulong)ReadExecute);
        OSHostMemoryRange outside = new(
            alloc.Range.BaseAddress + alloc.Range.ByteLength,
            PageSize,
            (ulong)ReadWrite);

        if (!alloc.Succeeded ||
            table.ProtectPages(partial) != OSHostStatus.InvalidArgument ||
            table.ProtectPages(outside) != OSHostStatus.InvalidArgument ||
            table.FreePages(alloc.Allocation) != OSHostStatus.Ok)
        {
            return false;
        }

        return table.ProtectPages(alloc.Range) == OSHostStatus.InvalidArgument
            && table.QueryMemory(alloc.Range.BaseAddress, out _) == OSHostStatus.NotFound
            && table.QueryMemory(0, out _) == OSHostStatus.InvalidArgument;
    }

    private static bool TestLayoutsAreStable()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<OSHostMemoryAllocationResult>() ||
            RuntimeHelpers.IsReferenceOrContainsReferences<OSHostMemoryTableEntry>())
        {
            return false;
        }

        return Marshal.SizeOf<OSHostMemoryAllocationResult>() == 40
            && Marshal.SizeOf<OSHostMemoryTableEntry>() == 40
            && Offset<OSHostMemoryAllocationResult>(nameof(OSHostMemoryAllocationResult.Status)) == 0
            && Offset<OSHostMemoryAllocationResult>(nameof(OSHostMemoryAllocationResult.Allocation)) == 8
            && Offset<OSHostMemoryAllocationResult>(nameof(OSHostMemoryAllocationResult.Range)) == 16
            && Offset<OSHostMemoryTableEntry>(nameof(OSHostMemoryTableEntry.Allocation)) == 0
            && Offset<OSHostMemoryTableEntry>(nameof(OSHostMemoryTableEntry.Range)) == 8
            && Offset<OSHostMemoryTableEntry>(nameof(OSHostMemoryTableEntry.StateFlags)) == 32;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostMemoryAllocationResult allocation = default;

        return !allocation.Succeeded
            && allocation.Status == OSHostStatus.Ok
            && allocation.Reserved == 0
            && allocation.Allocation.Value == 0
            && allocation.Range.BaseAddress == 0
            && allocation.Range.ByteLength == 0
            && allocation.Range.ProtectionFlags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostMemoryAllocationResult>()
            && Marshal.SizeOf<OSHostMemoryAllocationResult>() == 40;
    }

    private static OSHostMemoryTable NewTable(ulong pageCapacity, int handleCapacity)
    {
        return new OSHostMemoryTable(
            PageSize,
            BaseAddress,
            pageCapacity,
            handleCapacity);
    }

    private static OSHostMemoryRequest Request(
        ulong pageCount,
        ulong alignment,
        OSHostPageProtection protection = OSHostPageProtection.Read | OSHostPageProtection.Write)
    {
        return new OSHostMemoryRequest(
            pageCount,
            alignment,
            (ulong)protection);
    }

    private static OSHostPageProtection ReadWrite =>
        OSHostPageProtection.Read | OSHostPageProtection.Write;

    private static OSHostPageProtection ReadExecute =>
        OSHostPageProtection.Read | OSHostPageProtection.Execute;

    private static int Offset<T>(string fieldName)
    {
        return (int)Marshal.OffsetOf<T>(fieldName);
    }
}
