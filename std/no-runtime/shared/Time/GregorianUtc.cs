namespace OS.Std.Time;

/// <summary>Allocation-free proleptic Gregorian conversion for years 1..9999.
/// This is a finite numeric calendar helper, not a timezone or DateTime API.
/// Calls are independent; invalid civil fields return false and zero seconds.
/// Leap seconds and seasonal timezone rules are unsupported. No exceptions.</summary>
public static class GregorianUtc
{
    /// <summary>Convert validated civil UTC fields to signed seconds since
    /// 1970-01-01. The caller supplies numeric values; nothing is retained.</summary>
    public static bool TryUnixSeconds(uint year, uint month, uint day, uint hour,
        uint minute, uint second, out long seconds)
    {
        seconds = 0;
        if (year is < 1 or > 9999 || month is < 1 or > 12 ||
            hour > 23 || minute > 59 || second > 59) return false;
        bool leap = year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
        if (day == 0 || day > MonthDays(month, leap)) return false;
        long previousYears = year - 1;
        long days = previousYears * 365 + previousYears / 4 - previousYears / 100 +
            previousYears / 400 - 719162 + day - 1;
        for (uint preceding = 1; preceding < month; preceding++) days += MonthDays(preceding, leap);
        seconds = days * 86400 + hour * 3600L + minute * 60L + second;
        return true;
    }

    private static uint MonthDays(uint month, bool leap) => month switch
    {
        2 => leap ? 29U : 28U,
        4 or 6 or 9 or 11 => 30,
        _ => 31
    };
}
