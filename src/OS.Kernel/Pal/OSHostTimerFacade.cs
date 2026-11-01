using System.Runtime.InteropServices;

namespace OS.Pal;
public sealed class OSHostTimerFacade
{
    private readonly OSHostTimerTable? table;
    public OSHostTimerFacade(OSHostTimerTable? table)
    {
        this.table = table;
    }

    public unsafe OSHostStatus TimeGetMonotonicTicks(OSHostTimeSnapshot* snapshot)
    {
        if (!TryGetTable(out OSHostTimerTable activeTable) || snapshot is null)
        {
            return OSHostStatus.InvalidArgument;
        }

        *snapshot = activeTable.GetSnapshot();
        return OSHostStatus.Ok;
    }

    public unsafe OSHostStatus TimerCreate(OSHostHandle* timer)
    {
        if (!TryGetTable(out OSHostTimerTable activeTable) || timer is null)
        {
            return OSHostStatus.InvalidArgument;
        }

        *timer = default;
        OSHostTimerCreateResult create = activeTable.Create();
        if (!create.Succeeded)
        {
            return create.Status;
        }

        *timer = create.Timer;
        return OSHostStatus.Ok;
    }

    public OSHostStatus TimerArm(OSHostHandle timer, OSHostTimerOptions options)
    {
        return TryGetTable(out OSHostTimerTable activeTable) ? activeTable.Arm(timer, options) : OSHostStatus.InvalidArgument;
    }

    public OSHostStatus TimerCancel(OSHostHandle timer)
    {
        return TryGetTable(out OSHostTimerTable activeTable) ? activeTable.Cancel(timer) : OSHostStatus.InvalidArgument;
    }

    public OSHostStatus TimerClose(OSHostHandle timer)
    {
        return TryGetTable(out OSHostTimerTable activeTable) ? activeTable.Close(timer) : OSHostStatus.InvalidArgument;
    }

    public unsafe OSHostStatus TimerPoll(OSHostHandle timer, OSHostTimerPollOutput* output)
    {
        if (!TryGetTable(out OSHostTimerTable activeTable) || output is null)
        {
            return OSHostStatus.InvalidArgument;
        }

        *output = default;
        OSHostTimerPollResult poll = activeTable.Poll(timer);
        if (!poll.Succeeded)
        {
            return poll.Status;
        }

        *output = OSHostTimerPollOutput.FromResult(poll);
        return OSHostStatus.Ok;
    }

    private bool TryGetTable(out OSHostTimerTable activeTable)
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
