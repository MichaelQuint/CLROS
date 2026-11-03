using OS.Tooling;

internal static class SocketContainmentTransportTests
{
    [HostTest("socket containment binds original lifetime timeout and repost observations", HostLane.Fast, 0)]
    private static void Valid()
    {
        var value = QemuTranscripts.InspectSocketContainmentTransportForTests(Rows());
        HostCheck.Equal(3, value.Lifetimes.Count);
        HostCheck.Equal(63_000_001_000UL, value.TimeoutClock);
        HostCheck.Equal(35_000_000_000UL, value.DropClock);
        HostCheck.Equal(58UL, value.AvailableBefore);
        HostCheck.Equal(59UL, value.AvailableAfter);
        HostCheck.True(value.Lifetimes.Count(held => held.Timeout != 0) == 1 && value.Lifetimes.All(held => held.Retired != 0));
        HostCheck.True(((IList<QemuTranscripts.SocketContainmentLifetime>)value.Lifetimes).IsReadOnly);
        var wrapped = Rows();
        wrapped[3] = wrapped[3].Replace("availableBefore=58 availableAfter=59", "availableBefore=65535 availableAfter=0", StringComparison.Ordinal);
        HostCheck.Equal(0UL, QemuTranscripts.InspectSocketContainmentTransportForTests(wrapped).AvailableAfter);
        var coalesced = Rows();
        coalesced[3] = coalesced[3].Replace("availableAfter=59 delivered=0", "availableAfter=60 delivered=1", StringComparison.Ordinal);
        HostCheck.Equal(1UL, QemuTranscripts.InspectSocketContainmentTransportForTests(coalesced).DeliveredAfterDrop);
        var unaccounted = Rows();
        unaccounted[3] = unaccounted[3].Replace("availableAfter=59", "availableAfter=60", StringComparison.Ordinal);
        Reject(unaccounted);
    }

    [HostTest("socket containment rejects stale ownership duplicate peers and missing retirement", HostLane.Fast, 1)]
    private static void Ownership()
    {
        foreach ((int Row, string Before, string After) change in new (int, string, string)[] {
            (0, "owner=4096", "owner=4097"), (1, "owner=4096", "owner=8192"), (1, "epoch=1", "epoch=2"),
            (1, "index=1", "index=0"), (1, "remotePort=41001", "remotePort=41000"),
            (4, "generation=1", "generation=2"), (4, "index=0", "index=1"), (4, "remotePort=41000", "remotePort=41001"),
            (5, "slot=0", "slot=7"), (6, "generation=1", "generation=2"), (6, "duration=0", "duration=240000000000") })
        {
            var rows = Rows();
            rows[change.Row] = rows[change.Row].Replace(change.Before, change.After, StringComparison.Ordinal);
            Reject(rows);
        }
        foreach (int index in Enumerable.Range(0, 8))
            Reject(Rows().Where((_, i) => i != index).ToArray());
        var duplicated = Rows();
        Reject([.. duplicated, duplicated[^1]]);
    }

    [HostTest("socket containment rejects shared failure early expiry and false healthy ages", HostLane.Fast, 2)]
    private static void Timeouts()
    {
        foreach ((int Row, string Before, string After) change in new (int, string, string)[] {
            (4, "sharedFailed=0", "sharedFailed=1"), (4, "clock=63000001000", "clock=59999999999"),
            (6, "clock=70000200000", "clock=69999999999"), (7, "clock=70000300000", "clock=69999999999") })
        {
            var rows = Rows();
            rows[change.Row] = rows[change.Row].Replace(change.Before, change.After, StringComparison.Ordinal);
            Reject(rows);
        }
        var repeated = Rows();
        Reject([.. repeated[..5], repeated[4], .. repeated[5..]]);
        var reversed = Rows();
        (reversed[3], reversed[4]) = (reversed[4], reversed[3]);
        Reject(reversed);
    }

    [HostTest("socket containment rejects manufactured or unsafe drop progress", HostLane.Fast, 3)]
    private static void Drops()
    {
        foreach ((string Before, string After) change in new (string, string)[] { ("before=0", "before=1"), ("after=1", "after=2"),
            ("running=1", "running=0"), ("availableAfter=59", "availableAfter=58"), ("availableAfter=59", "availableAfter=60"),
            ("availableBefore=58", "availableBefore=65536"), ("clock=35000000000", "clock=0") })
        {
            var rows = Rows();
            rows[3] = rows[3].Replace(change.Before, change.After, StringComparison.Ordinal);
            Reject(rows);
        }
        var beforeFresh = Rows();
        Reject([beforeFresh[3], .. beforeFresh[..3], .. beforeFresh[4..]]);
        var twice = Rows();
        Reject([.. twice[..4], twice[3], .. twice[4..]]);
    }

    private static string[] Rows() => [
        "M16_011_SLOT event=0 " + Fields(0, 1000) + " deadline=0 duration=0",
        "M16_011_SLOT event=0 " + Fields(1, 2000) + " deadline=0 duration=0",
        "M16_011_SLOT event=0 " + Fields(2, 3000) + " deadline=0 duration=0",
        "M07_028_DROP before=0 after=1 availableBefore=58 availableAfter=59 delivered=0 clock=35000000000 running=1",
        "M07_027_TIMEOUT " + Fields(0, 63_000_001_000) + " sharedFailed=0",
        "M16_011_SLOT event=1 " + Fields(0, 70_000_100_000) + " deadline=0 duration=0",
        "M16_011_SLOT event=1 " + Fields(1, 70_000_200_000) + " deadline=0 duration=0",
        "M16_011_SLOT event=1 " + Fields(2, 70_000_300_000) + " deadline=0 duration=0"];

    private static string Fields(int role, ulong clock) => $"slot={role} owner=4096 epoch=1 generation=1 index={role} local=167772687 localPort=8080 remote=167772674 remotePort={41000 + role} clock={clock}";

    private static void Reject(string[] rows)
    {
        bool rejected = false;
        try
        {
            _ = QemuTranscripts.InspectSocketContainmentTransportForTests(rows);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
