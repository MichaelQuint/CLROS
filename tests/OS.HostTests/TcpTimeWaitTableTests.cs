using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Net;
using TimeWait = OS.Net.TcpTimeWaitTable;

internal static unsafe partial class TcpTimeWaitTableTests
{
    private const ulong Duration = TimeWait.MinimumDurationNanoseconds;
    [HostTest("net.tcp.time_wait.table bounded capacities preserve tuple sequence and exact deadlines", HostLane.Fast, 0)]
    private static void Capacities()
    {
        foreach (uint capacity in new uint[]
        {
            1,
            2,
            64
        }

        )
        {
            using var f = new Fixture(capacity);
            HostCheck.True(sizeof(TimeWait.Entry) <= 64 && !RuntimeHelpers.IsReferenceOrContainsReferences<TimeWait.Entry>());
            TimeWait.Handle first = default;
            for (uint i = 0; i < capacity; i++)
            {
                HostCheck.Equal(TimeWait.Status.Success, TimeWait.Retain(f.Owner, Key(i), uint.MaxValue, 0, Duration + 100, Duration, 100, out var handle));
                if (i == 0)
                    first = handle;
                HostCheck.Equal(TimeWait.Status.Success, TimeWait.Find(f.Owner, Key(i), out var found, out var record));
                HostCheck.Equal(handle.Index, found.Index);
                HostCheck.True(record.Key.HasSameEndpoint(Key(i)));
                HostCheck.Equal(uint.MaxValue, record.LocalNextSequenceNumber);
                HostCheck.Equal(0U, record.RemoteNextSequenceNumber);
                HostCheck.Equal(Duration + 100, record.OriginalDeadlineNanoseconds);
                HostCheck.Equal(Duration + 100, record.DeadlineNanoseconds);
            }

            byte[] before = f.Snapshot();
            HostCheck.Equal(TimeWait.Status.DuplicateEndpoint, TimeWait.Retain(f.Owner, Key(), 1, 2, Duration + 100, Duration, 100, out _));
            HostCheck.Equal(TimeWait.Status.Full, TimeWait.Retain(f.Owner, Key(capacity), 1, 2, Duration + 100, Duration, 100, out _));
            HostCheck.Equal(TimeWait.Status.RecordsRemain, TimeWait.Retire(f.Owner));
            HostCheck.True(before.AsSpan().SequenceEqual(f.Snapshot()));
            HostCheck.Equal(TimeWait.Status.Success, TimeWait.Discard(f.Owner));
            f.AssertScrubbed();
            HostCheck.Equal(TimeWait.Status.StaleHandle, TimeWait.Read(f.Owner, first, out _));
            HostCheck.Equal(TimeWait.Status.Success, TimeWait.Retire(f.Owner));
        }
    }

    [HostTest("net.tcp.time_wait.table restart retains original deadline and expiry requires explicit call", HostLane.Fast, 1)]
    private static void Deadlines()
    {
        using var f = new Fixture();
        ulong original = Duration + 50;
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Retain(f.Owner, Key(), 0, uint.MaxValue, original, Duration, 200, out var old));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Expire(f.Owner, original - 1));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Restart(f.Owner, old, original - 1));
        ulong restarted = original - 1 + Duration;
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Read(f.Owner, old, out var record));
        HostCheck.Equal(original, record.OriginalDeadlineNanoseconds);
        HostCheck.Equal(restarted, record.DeadlineNanoseconds);
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Expire(f.Owner, original));
        HostCheck.Equal(1, f.Owner->Count);
        byte[] before = f.Snapshot();
        HostCheck.Equal(TimeWait.Status.Expired, TimeWait.Restart(f.Owner, old, restarted));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Find(f.Owner, Key(), out _, out record));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Read(f.Owner, old, out record));
        HostCheck.True(before.AsSpan().SequenceEqual(f.Snapshot()));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Expire(f.Owner, restarted));
        f.AssertScrubbed();
        HostCheck.Equal(TimeWait.Status.NotFound, TimeWait.Find(f.Owner, Key(), out _, out _));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Retain(f.Owner, Key(), 3, 4, restarted + Duration, Duration, restarted, out var current));
        HostCheck.Equal(TimeWait.Status.StaleHandle, TimeWait.Read(f.Owner, old, out _));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Read(f.Owner, current, out record));
        HostCheck.Equal(3U, record.LocalNextSequenceNumber);
    }

    [HostTest("net.tcp.time_wait.table invalid records regression and restart overflow leave metadata intact", HostLane.Fast, 2)]
    private static void ClockGuards()
    {
        using var f = new Fixture();
        byte[] before = f.Snapshot();
        HostCheck.Equal(TimeWait.Status.InvalidClock, TimeWait.Expire(f.Owner, 99));
        HostCheck.Equal(TimeWait.Status.InvalidClock, TimeWait.Retain(f.Owner, Key(), 1, 2, Duration + 100, Duration, 99, out _));
        HostCheck.Equal(TimeWait.Status.InvalidRecord, TimeWait.Retain(f.Owner, default, 1, 2, Duration + 100, Duration, 100, out _));
        HostCheck.Equal(TimeWait.Status.InvalidRecord, TimeWait.Retain(f.Owner, Key(), 1, 2, Duration + 99, Duration - 1, 100, out _));
        HostCheck.Equal(TimeWait.Status.InvalidRecord, TimeWait.Retain(f.Owner, Key(), 1, 2, Duration - 1, Duration, 100, out _));
        HostCheck.Equal(TimeWait.Status.InvalidRecord, TimeWait.Retain(f.Owner, Key(), 1, 2, Duration + 101, Duration, 100, out _));
        HostCheck.Equal(TimeWait.Status.Expired, TimeWait.Retain(f.Owner, Key(), 1, 2, Duration + 100, Duration, Duration + 100, out _));
        HostCheck.True(before.AsSpan().SequenceEqual(f.Snapshot()));
        using var high = new Fixture(now: ulong.MaxValue - Duration - 10);
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Retain(high.Owner, Key(), 1, 2, ulong.MaxValue - 10, Duration, ulong.MaxValue - Duration - 10, out var handle));
        before = high.Snapshot();
        HostCheck.Equal(TimeWait.Status.InvalidClock, TimeWait.Restart(high.Owner, handle, ulong.MaxValue - Duration + 1));
        HostCheck.True(before.AsSpan().SequenceEqual(high.Snapshot()));
    }

    [HostTest("net.tcp.time_wait.table aliases copied owners epochs and generation exhaustion fail closed", HostLane.Fast, 3)]
    private static void Identity()
    {
        using var f = new Fixture(initialize: false);
        byte[] before = f.Snapshot();
        HostCheck.Equal(TimeWait.Status.InvalidStorage, TimeWait.Initialize(f.Owner, f.Entries, 0, 71, 100));
        HostCheck.Equal(TimeWait.Status.InvalidStorage, TimeWait.Initialize(f.Owner, f.Entries, 65, 71, 100));
        HostCheck.Equal(TimeWait.Status.InvalidStorage, TimeWait.Initialize(f.Owner, (TimeWait.Entry*)f.Owner, 1, 71, 100));
        HostCheck.Equal(TimeWait.Status.InvalidStorage, TimeWait.Initialize((TimeWait.Owner*)((byte*)f.Owner + 1), f.Entries, 1, 71, 100));
        HostCheck.Equal(TimeWait.Status.InvalidStorage, TimeWait.Initialize(f.Owner, f.Entries, 1, 0, 100));
        HostCheck.True(before.AsSpan().SequenceEqual(f.Snapshot()));
        ((byte*)f.Entries)[sizeof(TimeWait.Entry) - 1] = 1;
        HostCheck.Equal(TimeWait.Status.InvalidStorage, TimeWait.Initialize(f.Owner, f.Entries, 1, 71, 100));
        ((byte*)f.Entries)[sizeof(TimeWait.Entry) - 1] = 0;
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Initialize(f.Owner, f.Entries, 1, 71, 100));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Retain(f.Owner, Key(), 1, 2, Duration + 100, Duration, 100, out var old));
        TimeWait.Owner copy = *f.Owner;
        HostCheck.Equal(TimeWait.Status.InvalidState, TimeWait.Find(&copy, Key(), out _, out _));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Discard(f.Owner));
        f.AssertScrubbed();
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Retire(f.Owner));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Initialize(f.Owner, f.Entries, 1, 72, 100));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Retain(f.Owner, Key(), 1, 2, Duration + 100, Duration, 100, out _));
        HostCheck.Equal(TimeWait.Status.StaleHandle, TimeWait.Read(f.Owner, old, out _));
        HostCheck.Equal(TimeWait.Status.Success, TimeWait.Discard(f.Owner));
        // Deterministic boundary injection into caller-retained metadata; no wrap loop.
        *(ulong*)((byte*)f.Owner + Marshal.OffsetOf<TimeWait.Owner>("Issued").ToInt32()) = ulong.MaxValue;
        before = f.Snapshot();
        HostCheck.Equal(TimeWait.Status.GenerationExhausted, TimeWait.Retain(f.Owner, Key(), 1, 2, Duration + 100, Duration, 100, out _));
        HostCheck.True(before.AsSpan().SequenceEqual(f.Snapshot()));
    }

    [HostTest("net.tcp.time_wait.table warmed retain lookup restart and expiry allocate no managed bytes", HostLane.Fast, 4)]
    private static void Allocation()
    {
        using var f = new Fixture();
        var key = Key();
        bool success = true;
        ulong now = 100;
        void Cycle()
        {
            success &= TimeWait.Retain(f.Owner, key, 0, uint.MaxValue, now + Duration, Duration, now, out var handle) == TimeWait.Status.Success;
            success &= TimeWait.Find(f.Owner, key, out _, out _) == TimeWait.Status.Success;
            success &= TimeWait.Read(f.Owner, handle, out _) == TimeWait.Status.Success;
            success &= TimeWait.Restart(f.Owner, handle, now) == TimeWait.Status.Success;
            success &= TimeWait.Expire(f.Owner, now + Duration) == TimeWait.Status.Success;
            now += Duration + 1;
        }

        Cycle();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            Cycle();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(success);
        HostCheck.Equal(0L, allocated);
        HostCheck.Equal(0, f.Owner->Count);
    }

    private static TcpConnectionKey Key(uint index = 0) => new(new(10, 0, 2, 15), 8080, new(10, 0, 2, 2), (ushort)(40000 + index));
}
