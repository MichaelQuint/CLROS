using OS.Std.Time;

internal static class GregorianUtcTests
{
    [HostTest("time.gregorian representative leap centuries match DateTimeOffset")]
    private static void CalendarOracle()
    {
        foreach (int year in new[] { 1, 4, 100, 400, 1600, 1700, 1900, 1969, 1970, 1999, 2000, 2004, 2100, 2400, 9999 })
            for (int month = 1; month <= 12; month++)
                foreach (int day in new[] { 1, DateTime.DaysInMonth(year, month) })
                {
                    var expected = new DateTimeOffset(year, month, day, 12, 34, 56, TimeSpan.Zero);
                    HostCheck.True(GregorianUtc.TryUnixSeconds((uint)year, (uint)month, (uint)day, 12, 34, 56, out long actual));
                    HostCheck.Equal(expected.ToUnixTimeSeconds(), actual);
                }
    }

    [HostTest("time.gregorian signed epoch and complete supported boundaries")]
    private static void Boundaries()
    {
        HostCheck.True(GregorianUtc.TryUnixSeconds(1, 1, 1, 0, 0, 0, out long first));
        HostCheck.Equal(-62135596800L, first);
        HostCheck.True(GregorianUtc.TryUnixSeconds(1969, 12, 31, 23, 59, 59, out long before));
        HostCheck.Equal(-1L, before);
        HostCheck.True(GregorianUtc.TryUnixSeconds(1970, 1, 1, 0, 0, 0, out long epoch));
        HostCheck.Equal(0L, epoch);
        HostCheck.True(GregorianUtc.TryUnixSeconds(9999, 12, 31, 23, 59, 59, out long last));
        HostCheck.Equal(253402300799L, last);
    }

    [HostTest("time.gregorian malformed civil fields reject with zero output")]
    private static void Invalid()
    {
        foreach (var value in new (uint Year, uint Month, uint Day, uint Hour, uint Minute, uint Second)[]
        {
            (0,1,1,0,0,0), (10000,1,1,0,0,0), (uint.MaxValue,1,1,0,0,0),
            (2000,0,1,0,0,0), (2000,13,1,0,0,0), (2000,uint.MaxValue,1,0,0,0),
            (2000,1,0,0,0,0), (2000,1,32,0,0,0), (2000,4,31,0,0,0),
            (1900,2,29,0,0,0), (2100,2,29,0,0,0), (2000,2,30,0,0,0),
            (2000,1,1,24,0,0), (2000,1,1,0,60,0), (2000,1,1,0,0,60),
            (2000,1,uint.MaxValue,0,0,0), (2000,1,1,uint.MaxValue,0,0),
            (2000,1,1,0,uint.MaxValue,0), (2000,1,1,0,0,uint.MaxValue)
        })
        {
            HostCheck.True(!GregorianUtc.TryUnixSeconds(value.Year, value.Month, value.Day, value.Hour,
                value.Minute, value.Second, out long seconds));
            HostCheck.Equal(0L, seconds);
        }
    }
}
