namespace OS.Pal;

public sealed class OSHostMemoryFacade
{
    private readonly OSHostMemoryTable? table;

    public OSHostMemoryFacade(OSHostMemoryTable? table)
    {
        this.table = table;
    }

    public unsafe OSHostStatus AllocPages(
        OSHostMemoryRequest request,
        OSHostHandle* allocation,
        OSHostMemoryRange* range)
    {
        if (allocation is null || range is null)
        {
            return OSHostStatus.InvalidArgument;
        }

        *allocation = default;
        *range = default;

        if (!TryGetTable(out OSHostMemoryTable activeTable))
        {
            return OSHostStatus.InvalidArgument;
        }

        OSHostMemoryAllocationResult alloc = activeTable.AllocPages(request);
        if (!alloc.Succeeded)
        {
            return alloc.Status;
        }

        *allocation = alloc.Allocation;
        *range = alloc.Range;
        return OSHostStatus.Ok;
    }

    public OSHostStatus FreePages(OSHostHandle allocation)
    {
        return TryGetTable(out OSHostMemoryTable activeTable)
            ? activeTable.FreePages(allocation)
            : OSHostStatus.InvalidArgument;
    }

    public OSHostStatus ProtectPages(OSHostMemoryRange range)
    {
        return TryGetTable(out OSHostMemoryTable activeTable)
            ? activeTable.ProtectPages(range)
            : OSHostStatus.InvalidArgument;
    }

    public unsafe OSHostStatus QueryMemory(
        ulong address,
        OSHostMemoryQueryResult* result)
    {
        if (result is null)
        {
            return OSHostStatus.InvalidArgument;
        }

        *result = default;

        if (!TryGetTable(out OSHostMemoryTable activeTable))
        {
            return OSHostStatus.InvalidArgument;
        }

        OSHostStatus status = activeTable.QueryMemory(address, out OSHostMemoryQueryResult query);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        *result = query;
        return OSHostStatus.Ok;
    }

    private bool TryGetTable(out OSHostMemoryTable activeTable)
    {
        if (table is null)
        {
            activeTable = null!;
            return false;
        }

        activeTable = table;
        return true;
    }
}
