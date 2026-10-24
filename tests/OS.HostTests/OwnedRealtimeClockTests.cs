using System.Runtime.InteropServices;
using OS.Kernel.Platform;
using Clock = OS.Kernel.Platform.OwnedRealtimeClock;

internal static class OwnedRealtimeClockTests
{
    [HostTest("time.realtime finite full disk anchor survives600 and later started2100 seconds then expires permanently at3600")]
    private static void FullDiskLifetime()
    {
        const ulong frequency=100_000_000,anchor=123_456;
        ulong maximum=OS.Kernel.Compatibility.LinuxX64.DiskStaticExecutionPolicy.RealtimeMaximumElapsedSeconds;
        Clock.State state=default;
        HostCheck.True(Clock.Begin(ref state,1_800_000_000,0,anchor,frequency,maximum));
        HostCheck.True(!state.IsContinuing);
        foreach(ulong elapsed in new ulong[]{600,601,30+2100,maximum-1})
        {
            HostCheck.True(Clock.Read(ref state,anchor+elapsed*frequency,out long seconds,out uint nanos));
            HostCheck.Equal(1_800_000_000L+(long)elapsed,seconds);HostCheck.Equal(0U,nanos);
        }
        RejectedRead(ref state,anchor+maximum*frequency);
        RejectedRead(ref state,anchor+(maximum-1)*frequency);
        HostCheck.True(!Clock.Begin(ref state,1_800_000_000,0,anchor,frequency,maximum));
    }

    [HostTest("time.realtime continuing anchor survives finite ceilings and retains rollback overflow and single-owner guards")]
    private static void ServiceLifetime()
    {
        Clock.State finite=default,service=default;
        const ulong frequency=1_000_000,anchor=100;
        HostCheck.True(Clock.Begin(ref finite,1_000_000,0,anchor,frequency,600) && Clock.Begin(ref service,1_000_000,0,anchor,frequency,0,continuing:true));
        HostCheck.True(!finite.IsContinuing && service.IsContinuing);
        HostCheck.True(!Clock.Read(ref finite,anchor+600*frequency,out long absent,out uint zero) && absent==0 && zero==0 && !finite.IsAvailable);
        HostCheck.True(Clock.Read(ref service,anchor+600*frequency,out long seconds,out uint nanos) && seconds==1_000_600 && nanos==0);
        HostCheck.True(Clock.Read(ref service,anchor+86_401*frequency,out seconds,out nanos) && seconds==1_086_401 && nanos==0);
        var before=service;HostCheck.True(!Clock.Begin(ref service,0,0,anchor,frequency,0,continuing:true) && service.Equals(before));
        HostCheck.True(!Clock.Read(ref service,anchor+86_400*frequency,out absent,out zero) && absent==0 && zero==0 && !service.IsAvailable && !service.IsContinuing);
        HostCheck.True(!Clock.Read(ref service,anchor+86_402*frequency,out absent,out zero) && absent==0 && zero==0);
        Clock.State unbound=default;
        HostCheck.True(!Clock.Begin(ref unbound,0,0,anchor,frequency,600,continuing:true) && unbound.Equals(default(Clock.State)));
        HostCheck.True(!Clock.Begin(ref unbound,0,0,anchor,frequency,0) && unbound.Equals(default(Clock.State)));
        HostCheck.True(Clock.Begin(ref unbound,Clock.MaximumUnixSeconds,999_999_999,anchor,frequency,0,continuing:true));
        HostCheck.True(!Clock.Read(ref unbound,anchor+1,out absent,out zero) && absent==0 && zero==0 && !unbound.IsAvailable);
        Clock.State wrap=default;HostCheck.True(Clock.Begin(ref wrap,0,0,ulong.MaxValue-1,frequency,0,continuing:true));
        HostCheck.True(Clock.Read(ref wrap,ulong.MaxValue,out seconds,out nanos));
        HostCheck.True(!Clock.Read(ref wrap,0,out absent,out zero) && absent==0 && zero==0 && !wrap.IsAvailable);
    }

    [HostTest("time.realtime firmware layout and explicit timezone conversion")]
    private static void FirmwareLayoutAndTimezone()
    {
        HostCheck.Equal(16, Marshal.SizeOf<Clock.FirmwareTime>());
        foreach (var field in new (string Name, int Offset)[]
        {
            ("Year",0), ("Month",2), ("Day",3), ("Hour",4), ("Minute",5), ("Second",6),
            ("Pad1",7), ("Nanosecond",8), ("TimeZone",12), ("Daylight",14), ("Pad2",15)
        }) HostCheck.Equal(field.Offset, Marshal.OffsetOf<Clock.FirmwareTime>(field.Name).ToInt32());
        foreach (short zone in new short[] { -1440, -60, 0, 60, 1440 })
        {
            var time = Firmware(); time.TimeZone = zone; time.Nanosecond = 999999999;
            HostCheck.True(Clock.TryFirmwareUtc(time, false, out long seconds, out uint nanos));
            long local = new DateTimeOffset(2020, 6, 15, 12, 34, 56, TimeSpan.Zero).ToUnixTimeSeconds();
            HostCheck.Equal(local + zone * 60L, seconds);
            HostCheck.Equal(999999999U, nanos);
        }
        var unspecified = Firmware(); unspecified.TimeZone = 2047;
        RejectedFirmware(unspecified);
        HostCheck.True(Clock.TryFirmwareUtc(unspecified, true, out long utc, out _));
        HostCheck.Equal(new DateTimeOffset(2020, 6, 15, 12, 34, 56, TimeSpan.Zero).ToUnixTimeSeconds(), utc);
        var boundary = new Clock.FirmwareTime { Year = 1969, Month = 12, Day = 31, Hour = 23, TimeZone = 60 };
        HostCheck.True(Clock.TryFirmwareUtc(boundary, false, out utc, out _));
        HostCheck.Equal(0L, utc);
        boundary = new() { Year = 9999, Month = 12, Day = 31, Hour = 23, Minute = 59, Second = 59, Nanosecond = 999999999 };
        HostCheck.True(Clock.TryFirmwareUtc(boundary, false, out utc, out uint lastNanos));
        HostCheck.Equal(Clock.MaximumUnixSeconds, utc); HostCheck.Equal(999999999U, lastNanos);
    }

    [HostTest("time.realtime firmware ambiguity padding calendar and utc range reject")]
    private static void InvalidFirmware()
    {
        for (int scenario = 0; scenario < 19; scenario++)
        {
            var time = Firmware();
            switch (scenario)
            {
                case 0: time.Year = 1899; break;
                case 1: time.Year = 10000; break;
                case 2: time.Month = 0; break;
                case 3: time.Day = 31; break;
                case 4: time.Hour = 24; break;
                case 5: time.Minute = 60; break;
                case 6: time.Second = 60; break;
                case 7: time.Nanosecond = 1000000000; break;
                case 8: time.TimeZone = -1441; break;
                case 9: time.TimeZone = 1441; break;
                case 10: time.TimeZone = 2046; break;
                case 11: time.Pad1 = 1; break;
                case 12: time.Pad2 = 1; break;
                case 13: time.Daylight = 1; break;
                case 14: time.Daylight = 2; break;
                case 15: time.Year = 1900; time.Month = 2; time.Day = 29; break;
                case 16: time = new() { Year = 1970, Month = 1, Day = 1, TimeZone = -1 }; break;
                case 17: time = new() { Year = 9999, Month = 12, Day = 31, Hour = 23, Minute = 59, Second = 59, TimeZone = 1 }; break;
                case 18: time = new() { Year = 1900, Month = 1, Day = 1 }; break;
            }
            RejectedFirmware(time);
        }
        var ambiguous = Firmware(); ambiguous.TimeZone = 2047; ambiguous.Daylight = 3;
        RejectedFirmware(ambiguous, true);
    }

    [HostTest("time.realtime begin validates atomically and successful owner is single use")]
    private static void BeginAdmission()
    {
        foreach (var input in new (long Seconds, uint Nanos, ulong Frequency, ulong Lifetime)[]
        {
            (-1,0,1000000,1), (253402300800,0,1000000,1), (0,1000000000,1000000,1),
            (0,uint.MaxValue,1000000,1), (0,0,0,1), (0,0,999999,1),
            (0,0,10000000001,1), (0,0,ulong.MaxValue,1), (0,0,1000000,0),
            (0,0,1000000,86401), (0,0,1000000,ulong.MaxValue)
        })
        {
            Clock.State state = default, before = state;
            HostCheck.True(!Clock.Begin(ref state, input.Seconds, input.Nanos, 123, input.Frequency, input.Lifetime));
            HostCheck.True(state.Equals(before));
            HostCheck.True(!state.IsAvailable); HostCheck.Equal(0UL, state.FrequencyHz);
        }
        Clock.State live = default;
        HostCheck.True(Clock.Begin(ref live, 0, 0, ulong.MaxValue, 1000000, 1));
        var snapshot = live;
        HostCheck.True(!Clock.Begin(ref live, 1, 1, 0, 10000000000, 86400));
        HostCheck.True(live.Equals(snapshot));
        HostCheck.True(Clock.Read(ref live, ulong.MaxValue, out long seconds, out uint nanos));
        HostCheck.Equal(0L, seconds); HostCheck.Equal(0U, nanos);
    }

    [HostTest("time.realtime fractional carry is anchored without cumulative rounding")]
    private static void FractionalCarry()
    {
        Clock.State state = default;
        HostCheck.True(Clock.Begin(ref state, 100, 900000000, 50, 3000000, 2));
        HostCheck.True(Clock.Read(ref state, 51, out long seconds, out uint nanos));
        HostCheck.Equal(100L, seconds); HostCheck.Equal(900000333U, nanos);
        HostCheck.True(Clock.Read(ref state, 52, out seconds, out nanos));
        HostCheck.Equal(100L, seconds); HostCheck.Equal(900000666U, nanos);
        HostCheck.True(Clock.Read(ref state, 450050, out seconds, out nanos));
        HostCheck.Equal(101L, seconds); HostCheck.Equal(50000000U, nanos);
        HostCheck.True(Clock.Read(ref state, 450050, out seconds, out nanos));
        HostCheck.Equal(101L, seconds); HostCheck.Equal(50000000U, nanos);
    }

    [HostTest("time.realtime high frequency large anchor and remainder avoid overflow")]
    private static void HighFrequency()
    {
        const ulong frequency = 10000000000, anchor = ulong.MaxValue - 900000000000000;
        Clock.State state = default;
        HostCheck.True(Clock.Begin(ref state, 0, 0, anchor, frequency, 86400));
        HostCheck.Equal(frequency, state.FrequencyHz);
        HostCheck.True(Clock.Read(ref state, anchor + 1, out long seconds, out uint nanos));
        HostCheck.Equal(0L, seconds); HostCheck.Equal(0U, nanos);
        HostCheck.True(Clock.Read(ref state, anchor + 10, out seconds, out nanos));
        HostCheck.Equal(0L, seconds); HostCheck.Equal(1U, nanos);
        HostCheck.True(Clock.Read(ref state, anchor + 86400 * frequency - 1, out seconds, out nanos));
        HostCheck.Equal(86399L, seconds); HostCheck.Equal(999999999U, nanos);
        RejectedRead(ref state, anchor + 86400 * frequency);
    }

    [HostTest("time.realtime unavailable and preanchor reads invalidate permanently")]
    private static void Unavailable()
    {
        Clock.State state = default;
        RejectedRead(ref state, 0);
        var unavailable = state;
        HostCheck.True(!Clock.Begin(ref state, 1, 0, 0, 1000000, 1));
        HostCheck.True(state.Equals(unavailable));
        state = default;
        HostCheck.True(Clock.Begin(ref state, 1, 0, 100, 1000000, 1));
        RejectedRead(ref state, 99);
        RejectedRead(ref state, 100);
    }

    [HostTest("time.realtime regression invalidates despite later monotonic recovery")]
    private static void Regression()
    {
        Clock.State state = default;
        HostCheck.True(Clock.Begin(ref state, 42, 0, 100, 1000000, 2));
        HostCheck.True(Clock.Read(ref state, 500100, out _, out _));
        RejectedRead(ref state, 500099);
        RejectedRead(ref state, 1000100);
        HostCheck.True(!Clock.Begin(ref state, 42, 0, 1000100, 1000000, 2));
    }

    [HostTest("time.realtime finite expiry is exclusive and cannot recover")]
    private static void Expiry()
    {
        foreach (ulong ticks in new ulong[] { 1000007, 1000008, ulong.MaxValue })
        {
            Clock.State state = default;
            HostCheck.True(Clock.Begin(ref state, 0, 0, 7, 1000000, 1));
            HostCheck.True(Clock.Read(ref state, 1000006, out long seconds, out uint nanos));
            HostCheck.Equal(0L, seconds); HostCheck.Equal(999999000U, nanos);
            RejectedRead(ref state, ticks);
            RejectedRead(ref state, 1000006);
        }
    }

    [HostTest("time.realtime year9999 overflow and fractional carry fail closed")]
    private static void LastSecond()
    {
        Clock.State state = default;
        HostCheck.True(Clock.Begin(ref state, Clock.MaximumUnixSeconds, 999999000, 0, 1000000, 2));
        HostCheck.True(Clock.Read(ref state, 0, out long seconds, out uint nanos));
        HostCheck.Equal(Clock.MaximumUnixSeconds, seconds); HostCheck.Equal(999999000U, nanos);
        RejectedRead(ref state, 1);
        state = default;
        HostCheck.True(Clock.Begin(ref state, Clock.MaximumUnixSeconds, 0, 0, 1000000, 2));
        HostCheck.True(Clock.Read(ref state, 999999, out seconds, out nanos));
        HostCheck.Equal(Clock.MaximumUnixSeconds, seconds); HostCheck.Equal(999999000U, nanos);
        RejectedRead(ref state, 1000000);
    }

    private static Clock.FirmwareTime Firmware() => new()
    { Year = 2020, Month = 6, Day = 15, Hour = 12, Minute = 34, Second = 56 };
    private static void RejectedFirmware(Clock.FirmwareTime time, bool unspecifiedUtc = false)
    {
        HostCheck.True(!Clock.TryFirmwareUtc(time, unspecifiedUtc, out long seconds, out uint nanos));
        HostCheck.Equal(0L, seconds); HostCheck.Equal(0U, nanos);
    }
    private static void RejectedRead(ref Clock.State state, ulong ticks)
    {
        HostCheck.True(!Clock.Read(ref state, ticks, out long seconds, out uint nanos));
        HostCheck.Equal(0L, seconds); HostCheck.Equal(0U, nanos);
        HostCheck.True(!state.IsAvailable); HostCheck.Equal(0UL, state.FrequencyHz);
    }
}
