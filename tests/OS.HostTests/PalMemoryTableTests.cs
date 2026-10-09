using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Pal;

internal static class PalMemoryTableTests
{
    private const ulong PageSize = OSHostMemoryProtectionPlan.DefaultPageSize;
    private const ulong BaseAddress = 0x400000;

    [HostTest("pal.memory table allocates aligned pages", HostLane.Fast, 0)]
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
            return HostCheck.Result(false);
        }

        if (table.TryGetEntry(alloc.Allocation, out OSHostMemoryTableEntry entry) != OSHostStatus.Ok ||
            entry.Allocation.Value != alloc.Allocation.Value ||
            entry.Range.BaseAddress != BaseAddress ||
            entry.StateFlags != OSHostMemoryStateFlags.Allocated)
        {
            return HostCheck.Result(false);
        }

        OSHostStatus queryStatus = table.QueryMemory(BaseAddress + PageSize, out OSHostMemoryQueryResult query);
        return HostCheck.Result(queryStatus == OSHostStatus.Ok
            && query.Range.BaseAddress == alloc.Range.BaseAddress
            && query.Range.ByteLength == alloc.Range.ByteLength
            && query.PageSizeBytes == PageSize
            && query.StateFlags == OSHostMemoryStateFlags.Allocated);
    }

    [HostTest("pal.memory table rejects invalid request shape", HostLane.Fast, 1)]
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

        return HostCheck.Result(zeroPages.Status == OSHostStatus.InvalidArgument
            && smallAlignment.Status == OSHostStatus.InvalidArgument
            && nonPowerOfTwoAlignment.Status == OSHostStatus.InvalidArgument
            && directExecute.Status == OSHostStatus.InvalidArgument
            && rwx.Status == OSHostStatus.AccessDenied
            && invalidPage.Status == OSHostStatus.InvalidArgument
            && invalidBase.Status == OSHostStatus.InvalidArgument
            && table.ActiveCount == 0);
    }

    [HostTest("pal.memory table reports capacity exhaustion", HostLane.Fast, 2)]
    private static bool TestReportsCapacityExhaustion()
    {
        OSHostMemoryTable handleLimited = NewTable(pageCapacity: 8, handleCapacity: 1);
        OSHostMemoryAllocationResult first = handleLimited.AllocPages(Request(pageCount: 1, alignment: PageSize));
        OSHostMemoryAllocationResult noHandle = handleLimited.AllocPages(Request(pageCount: 1, alignment: PageSize));

        OSHostMemoryTable pageLimited = NewTable(pageCapacity: 1, handleCapacity: 2);
        OSHostMemoryAllocationResult tooManyPages = pageLimited.AllocPages(Request(pageCount: 2, alignment: PageSize));

        return HostCheck.Result(first.Succeeded
            && noHandle.Status == OSHostStatus.OutOfMemory
            && tooManyPages.Status == OSHostStatus.OutOfMemory
            && handleLimited.ActiveCount == 1
            && pageLimited.ActiveCount == 0);
    }

    [HostTest("pal.memory table reuses freed ranges and rejects stale handles", HostLane.Fast, 3)]
    private static bool TestReusesFreedRangesAndRejectsStaleHandles()
    {
        OSHostMemoryTable table = NewTable(pageCapacity: 4, handleCapacity: 1);
        OSHostMemoryAllocationResult first = table.AllocPages(Request(pageCount: 1, alignment: PageSize));

        if (!first.Succeeded ||
            table.FreePages(first.Allocation) != OSHostStatus.Ok)
        {
            return HostCheck.Result(false);
        }

        OSHostStatus staleFree = table.FreePages(first.Allocation);
        OSHostStatus staleEntry = table.TryGetEntry(first.Allocation, out _);
        OSHostStatus staleQuery = table.QueryMemory(first.Range.BaseAddress, out _);
        OSHostMemoryAllocationResult second = table.AllocPages(Request(pageCount: 1, alignment: PageSize));

        return HostCheck.Result(staleFree == OSHostStatus.InvalidArgument
            && staleEntry == OSHostStatus.InvalidArgument
            && staleQuery == OSHostStatus.NotFound
            && second.Succeeded
            && second.Range.BaseAddress == first.Range.BaseAddress
            && second.Allocation.Value != first.Allocation.Value);
    }

    [HostTest("pal.memory table protects write then execute", HostLane.Fast, 4)]
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
            return HostCheck.Result(false);
        }

        OSHostMemoryRange writeAgain = new(
            alloc.Range.BaseAddress,
            alloc.Range.ByteLength,
            (ulong)ReadWrite);
        OSHostMemoryRange rwx = new(
            alloc.Range.BaseAddress,
            alloc.Range.ByteLength,
            (ulong)(ReadWrite | OSHostPageProtection.Execute));

        return HostCheck.Result(query.Range.ProtectionFlags == (ulong)ReadExecute
            && table.ProtectPages(writeAgain) == OSHostStatus.InvalidArgument
            && table.ProtectPages(rwx) == OSHostStatus.AccessDenied);
    }

    [HostTest("pal.memory table rejects stale and partial ranges", HostLane.Fast, 5)]
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
            return HostCheck.Result(false);
        }

        return HostCheck.Result(table.ProtectPages(alloc.Range) == OSHostStatus.InvalidArgument
            && table.QueryMemory(alloc.Range.BaseAddress, out _) == OSHostStatus.NotFound
            && table.QueryMemory(0, out _) == OSHostStatus.InvalidArgument);
    }

    [HostTest("pal.memory table layouts are stable", HostLane.Fast, 6)]
    private static bool TestLayoutsAreStable()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<OSHostMemoryAllocationResult>() ||
            RuntimeHelpers.IsReferenceOrContainsReferences<OSHostMemoryTableEntry>())
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(Marshal.SizeOf<OSHostMemoryAllocationResult>() == 40
            && Marshal.SizeOf<OSHostMemoryTableEntry>() == 40
            && Offset<OSHostMemoryAllocationResult>(nameof(OSHostMemoryAllocationResult.Status)) == 0
            && Offset<OSHostMemoryAllocationResult>(nameof(OSHostMemoryAllocationResult.Allocation)) == 8
            && Offset<OSHostMemoryAllocationResult>(nameof(OSHostMemoryAllocationResult.Range)) == 16
            && Offset<OSHostMemoryTableEntry>(nameof(OSHostMemoryTableEntry.Allocation)) == 0
            && Offset<OSHostMemoryTableEntry>(nameof(OSHostMemoryTableEntry.Range)) == 8
            && Offset<OSHostMemoryTableEntry>(nameof(OSHostMemoryTableEntry.StateFlags)) == 32);
    }

    [HostTest("pal.memory table default result is not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostMemoryAllocationResult allocation = default;

        return HostCheck.Result(!allocation.Succeeded
            && allocation.Status == OSHostStatus.Ok
            && allocation.Reserved == 0
            && allocation.Allocation.Value == 0
            && allocation.Range.BaseAddress == 0
            && allocation.Range.ByteLength == 0
            && allocation.Range.ProtectionFlags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostMemoryAllocationResult>()
            && Marshal.SizeOf<OSHostMemoryAllocationResult>() == 40);
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
