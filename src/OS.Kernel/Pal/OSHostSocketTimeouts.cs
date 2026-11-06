using System.Runtime.InteropServices;

namespace OS.Pal;

public static class OSHostSocketTimeouts
{
    public static OSHostTimeout Infinite => new OSHostTimeout(OSHostSocketConstants.TimeoutKindInfinite, flags: 0, ticks: 0, tickFrequency: 0);
    public static OSHostTimeout Immediate => new OSHostTimeout(OSHostSocketConstants.TimeoutKindImmediate, flags: 0, ticks: 0, tickFrequency: 0);

    public static OSHostStatus TryFromMilliseconds(long milliseconds, ulong tickFrequency, out OSHostTimeout timeout)
    {
        if (milliseconds == -1)
        {
            timeout = Infinite;
            return OSHostStatus.Ok;
        }

        if (milliseconds < -1)
        {
            timeout = default;
            return OSHostStatus.InvalidArgument;
        }

        if (milliseconds == 0)
        {
            timeout = Immediate;
            return OSHostStatus.Ok;
        }

        if (tickFrequency == 0)
        {
            timeout = default;
            return OSHostStatus.InvalidArgument;
        }

        ulong ticks = ConvertMillisecondsToTicks((ulong)milliseconds, tickFrequency);
        timeout = new OSHostTimeout(OSHostSocketConstants.TimeoutKindRelativeTicks, flags: 0, ticks, tickFrequency);
        return OSHostStatus.Ok;
    }

    public static bool IsValid(OSHostTimeout timeout)
    {
        return timeout.Kind switch
        {
            OSHostSocketConstants.TimeoutKindInfinite => timeout.Ticks == 0 && timeout.TickFrequency == 0,
            OSHostSocketConstants.TimeoutKindImmediate => timeout.Ticks == 0 && timeout.TickFrequency == 0,
            OSHostSocketConstants.TimeoutKindRelativeTicks => timeout.TickFrequency != 0,
            OSHostSocketConstants.TimeoutKindAbsoluteTicks => timeout.TickFrequency != 0,
            _ => false
        };
    }

    private static ulong ConvertMillisecondsToTicks(ulong milliseconds, ulong tickFrequency)
    {
        ulong wholeSeconds = milliseconds / 1_000;
        ulong remainderMilliseconds = milliseconds % 1_000;
        if (wholeSeconds > ulong.MaxValue / tickFrequency)
        {
            return ulong.MaxValue;
        }

        ulong ticks = wholeSeconds * tickFrequency;
        ulong remainderTicks = ConvertRemainderMillisecondsToTicks(remainderMilliseconds, tickFrequency);
        return ulong.MaxValue - ticks < remainderTicks ? ulong.MaxValue : ticks + remainderTicks;
    }

    private static ulong ConvertRemainderMillisecondsToTicks(ulong remainderMilliseconds, ulong tickFrequency)
    {
        if (remainderMilliseconds == 0)
        {
            return 0;
        }

        if (tickFrequency > (ulong.MaxValue - 999) / remainderMilliseconds)
        {
            return ulong.MaxValue;
        }

        return ((remainderMilliseconds * tickFrequency) + 999) / 1_000;
    }
}
