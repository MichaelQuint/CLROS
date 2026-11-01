using System.Runtime.InteropServices;

namespace OS.Pal;
public sealed class OSHostWaitFacade
{
    private const ulong WaitableHandleByteLength = 8;
    private readonly OSHostWaitTable? table;
    public OSHostWaitFacade(OSHostWaitTable? table)
    {
        this.table = table;
    }

    public unsafe OSHostStatus EventCreate(OSHostEventCreateOptions options, OSHostHandle* waitable)
    {
        if (waitable is null)
        {
            return OSHostStatus.InvalidArgument;
        }

        *waitable = default;
        if (!TryGetTable(out OSHostWaitTable activeTable) || options.Flags != 0 || options.Reserved != 0 || options.InitiallySignaled > 1)
        {
            return OSHostStatus.InvalidArgument;
        }

        OSHostEventCreateResult create = activeTable.CreateEvent((OSHostWaitableKind)options.Kind, options.InitiallySignaled != 0);
        if (!create.Succeeded)
        {
            return create.Status;
        }

        *waitable = create.Waitable;
        return OSHostStatus.Ok;
    }

    public OSHostStatus EventSet(OSHostHandle waitable)
    {
        return TryGetTable(out OSHostWaitTable activeTable) ? activeTable.Set(waitable) : OSHostStatus.InvalidArgument;
    }

    public OSHostStatus EventReset(OSHostHandle waitable)
    {
        return TryGetTable(out OSHostWaitTable activeTable) ? activeTable.Reset(waitable) : OSHostStatus.InvalidArgument;
    }

    public OSHostStatus EventClose(OSHostHandle waitable)
    {
        return TryGetTable(out OSHostWaitTable activeTable) ? activeTable.Close(waitable) : OSHostStatus.InvalidArgument;
    }

    public unsafe OSHostStatus WaitOne(OSHostWaitOneRequest request, OSHostWaitResult* result)
    {
        if (result is null)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (!TryGetTable(out OSHostWaitTable activeTable))
        {
            *result = Result(OSHostStatus.InvalidArgument);
            return OSHostStatus.InvalidArgument;
        }

        OSHostWaitResult wait = activeTable.WaitOne(request);
        *result = wait;
        return StatusFrom(wait);
    }

    public unsafe OSHostStatus WaitMany(OSHostWaitManyRequest request, ReadOnlySpan<OSHostHandle> waitables, OSHostWaitResult* result)
    {
        if (result is null)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (!TryGetTable(out OSHostWaitTable activeTable))
        {
            *result = Result(OSHostStatus.InvalidArgument);
            return OSHostStatus.InvalidArgument;
        }

        OSHostStatus requestStatus = ValidateWaitManyRequest(request, waitables.Length);
        if (requestStatus != OSHostStatus.Ok)
        {
            *result = Result(requestStatus);
            return requestStatus;
        }

        OSHostWaitResult wait = activeTable.WaitMany(waitables, request.Timeout, request.Flags);
        *result = wait;
        return StatusFrom(wait);
    }

    private static OSHostStatus ValidateWaitManyRequest(OSHostWaitManyRequest request, int waitableCount)
    {
        if (request.Reserved != 0 || request.Waitables.Length % WaitableHandleByteLength != 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        ulong expectedByteLength = (ulong)waitableCount * WaitableHandleByteLength;
        if (request.Waitables.Length != expectedByteLength)
        {
            return OSHostStatus.InvalidArgument;
        }

        if (expectedByteLength != 0 && request.Waitables.Address == 0)
        {
            return OSHostStatus.InvalidArgument;
        }

        return OSHostStatus.Ok;
    }

    private static OSHostWaitResult Result(OSHostStatus status)
    {
        return new OSHostWaitResult(OSHostWaitConstants.ResultKindNone, signaledIndex: 0, (int)status, flags: 0, signaledHandle: default);
    }

    private static OSHostStatus StatusFrom(OSHostWaitResult result)
    {
        return result.StatusCode is >= (int)OSHostStatus.Ok and <= (int)OSHostStatus.Closed ? (OSHostStatus)result.StatusCode : OSHostStatus.InternalError;
    }

    private bool TryGetTable(out OSHostWaitTable activeTable)
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
