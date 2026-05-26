using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostTimerPollOutput
{
    public readonly ulong FiredCount;

    public readonly ulong NextDueTicks;

    public readonly uint Signaled;

    public readonly uint Reserved;

    public OSHostTimerPollOutput(
        ulong firedCount,
        ulong nextDueTicks,
        uint signaled,
        uint reserved)
    {
        FiredCount = firedCount;
        NextDueTicks = nextDueTicks;
        Signaled = signaled;
        Reserved = reserved;
    }

    public bool IsSignaled => Signaled != 0;

    public static OSHostTimerPollOutput FromResult(OSHostTimerPollResult result)
    {
        return new OSHostTimerPollOutput(
            result.FiredCount,
            result.NextDueTicks,
            result.Signaled ? 1U : 0U,
            reserved: 0);
    }
}

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
        return TryGetTable(out OSHostTimerTable activeTable)
            ? activeTable.Arm(timer, options)
            : OSHostStatus.InvalidArgument;
    }

    public OSHostStatus TimerCancel(OSHostHandle timer)
    {
        return TryGetTable(out OSHostTimerTable activeTable)
            ? activeTable.Cancel(timer)
            : OSHostStatus.InvalidArgument;
    }

    public OSHostStatus TimerClose(OSHostHandle timer)
    {
        return TryGetTable(out OSHostTimerTable activeTable)
            ? activeTable.Close(timer)
            : OSHostStatus.InvalidArgument;
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
