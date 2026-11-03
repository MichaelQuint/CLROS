internal static class KestrelSignalRaceStateTests
{
    // Pure copied-state controls: phases/identities grant no signal, wait or guest authority.
    [HostTest("m10-012 event race status selects exact v3 prefix and isolates the active profile")]
    private static void ProfileStatus()
    {
        foreach (bool eventOwned in new[] { false, true })
        {
            var state = new KestrelSignalRaceState();
            HostCheck.True(state.TryRead(false, out string legacy));
            HostCheck.True(state.TryRead(true, out string current));
            HostCheck.Equal("CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0", legacy);
            HostCheck.Equal("CLROS_SIGNAL_RACE_V3_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0", current);
            HostCheck.True(state.Begin(50, eventOwned));
            var before = state.Inspect();
            HostCheck.True(!state.TryRead(!eventOwned, out string wrong) && wrong == "");
            HostCheck.True(!state.Begin(99, !eventOwned));
            HostCheck.Equal(before, state.Inspect());
            HostCheck.True(state.TryRead(eventOwned, out string value));
            HostCheck.Equal((eventOwned ? "CLROS_SIGNAL_RACE_V3_STATUS" : "CLROS_SIGNAL_RACE_STATUS") +
                " phase=1 pid=50 waiter=0 actor=0 callback=0 recoveryRequested=0", value);
            if (!eventOwned)
                HostCheck.Equal(value, state.Read());
        }
    }

    [HostTest("m10-012 event race start and recovery refuse cross-profile releases and remain one-shot")]
    private static void ProfileGates()
    {
        foreach (bool eventOwned in new[] { false, true })
        {
            var state = new KestrelSignalRaceState();
            HostCheck.True(state.Begin(60, eventOwned) && state.Advance(2, waiter: 61, actor: 62));
            var armed = state.Inspect();
            HostCheck.True(!state.TryRequestStart(!eventOwned) && !state.StartRequested);
            if (eventOwned)
                HostCheck.True(!state.TryRequestStart());
            HostCheck.Equal(armed, state.Inspect());
            HostCheck.True(state.TryRequestStart(eventOwned) && state.StartRequested && !state.TryRequestStart(eventOwned));
            HostCheck.True(state.Advance(4, callback: 62));
            var recovery = state.Inspect();
            HostCheck.True(!state.TryRequestRecovery(!eventOwned));
            if (eventOwned)
                HostCheck.True(!state.TryRequestRecovery());
            HostCheck.Equal(recovery, state.Inspect());
            HostCheck.True(state.TryRequestRecovery(eventOwned) && !state.TryRequestRecovery(eventOwned));
            HostCheck.True(state.Advance(6) && state.Finish(true));
            var terminal = state.Inspect();
            HostCheck.True(!state.TryRequestStart(eventOwned) && !state.TryRequestRecovery(eventOwned));
            HostCheck.True(!state.TryRead(!eventOwned, out string wrong) && wrong == "");
            HostCheck.Equal(terminal, state.Inspect());
        }
    }

    [HostTest("m10-012 concurrent event race requests accept one matching profile and no legacy release")]
    private static async Task ConcurrentProfileGates()
    {
        var state = new KestrelSignalRaceState();
        HostCheck.True(state.Begin(70, true) && state.Advance(2, waiter: 71, actor: 72));
        var profiles = Enumerable.Range(0, 32).Select(i => i % 2 == 0).ToArray();
        bool[] start = await Task.WhenAll(profiles.Select(profile => Task.Run(() => state.TryRequestStart(profile))))
            .WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.Equal(1, start.Count(value => value));
        HostCheck.True(profiles.Where((profile, i) => start[i]).All(profile => profile));
        HostCheck.True(state.Advance(4, callback: 72));
        bool[] recovery = await Task.WhenAll(profiles.Select(profile => Task.Run(() => state.TryRequestRecovery(profile))))
            .WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.Equal(1, recovery.Count(value => value));
        HostCheck.True(profiles.Where((profile, i) => recovery[i]).All(profile => profile));
    }

    [HostTest("m10-012 signal race start requires both armed identities and accepts only once")]
    private static void StartGates()
    {
        foreach (int phase in new[] { 0, 1, 3, 4, 5, 6, 7, 8 })
        {
            var refused = new KestrelSignalRaceState();
            if (phase > 0)
                HostCheck.True(refused.Begin(10));
            if (phase > 1)
                HostCheck.True(refused.Advance(Math.Min(phase, 6), waiter: 11, actor: 12));
            if (phase > 6)
                HostCheck.True(refused.Finish(phase == 7));
            var before = refused.Inspect();
            HostCheck.True(!refused.TryRequestStart() && !refused.StartRequested);
            HostCheck.Equal(before, refused.Inspect());
        }
        foreach (var pair in new[] { (Waiter: 0, Actor: 0), (Waiter: 11, Actor: 0), (Waiter: 0, Actor: 12) })
        {
            var missing = new KestrelSignalRaceState();
            HostCheck.True(missing.Begin(10) && missing.Advance(2, waiter: pair.Waiter, actor: pair.Actor));
            HostCheck.True(!missing.TryRequestStart() && !missing.StartRequested);
        }
        var state = new KestrelSignalRaceState();
        HostCheck.True(state.Begin(10) && state.Advance(2, waiter: 11, actor: 12));
        HostCheck.True(state.TryRequestStart() && state.StartRequested && !state.TryRequestRecovery());
        var armed = state.Inspect();
        HostCheck.True(!state.TryRequestStart());
        HostCheck.Equal(armed, state.Inspect());
        HostCheck.True(state.Advance(6) && !state.TryRequestStart() && state.StartRequested);
    }

    [HostTest("m10-012 signal race concurrent starts have exactly one acceptance")]
    private static async Task ConcurrentStart()
    {
        var state = new KestrelSignalRaceState();
        HostCheck.True(state.Begin(20) && state.Advance(2, waiter: 21, actor: 22));
        Task<bool>[] requests = Enumerable.Range(0, 32).Select(_ => Task.Run(state.TryRequestStart)).ToArray();
        bool[] results = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.Equal(1, results.Count(value => value));
        HostCheck.True(state.StartRequested && state.Inspect() is { Phase: 2, Waiter: 21, Actor: 22 });
    }

    [HostTest("m10-012 signal race recovery requires its exact phase and accepts only once")]
    private static void RecoveryGates()
    {
        var state = new KestrelSignalRaceState();
        HostCheck.True(!state.TryRequestRecovery() && !state.Advance(2));
        HostCheck.True(state.Begin(10) && !state.TryRequestRecovery());
        HostCheck.True(state.Advance(2, waiter: 11, actor: 12) && !state.TryRequestRecovery());
        HostCheck.True(state.Advance(3, callback: 12) && !state.TryRequestRecovery());
        HostCheck.True(!state.Finish(true));
        HostCheck.True(state.Advance(4) && state.TryRequestRecovery());
        var accepted = state.Inspect();
        HostCheck.True(accepted is { Phase: 4, Process: 10, Waiter: 11, Actor: 12, Callback: 12, RecoveryRequested: true });
        HostCheck.True(!state.TryRequestRecovery());
        HostCheck.Equal(accepted, state.Inspect());
        HostCheck.True(state.Advance(5) && !state.TryRequestRecovery());
        HostCheck.True(state.Inspect().RecoveryRequested);
    }

    [HostTest("m10-012 signal race concurrent recovery requests have exactly one acceptance")]
    private static async Task ConcurrentRecovery()
    {
        var state = new KestrelSignalRaceState();
        HostCheck.True(state.Begin(20) && state.Advance(4, waiter: 21, actor: 22, callback: 22));
        Task<bool>[] requests = Enumerable.Range(0, 32).Select(_ => Task.Run(state.TryRequestRecovery)).ToArray();
        bool[] results = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(10));
        HostCheck.Equal(1, results.Count(value => value));
        HostCheck.True(state.Inspect() is { Phase: 4, Process: 20, Waiter: 21, Actor: 22, Callback: 22, RecoveryRequested: true });
    }

    [HostTest("m10-012 signal race identity and phase refusals leave all copied fields unchanged")]
    private static void AtomicIdentity()
    {
        var state = new KestrelSignalRaceState();
        var idle = state.Inspect();
        HostCheck.True(!state.Begin(0) && !state.Begin(-1));
        HostCheck.Equal(idle, state.Inspect());
        HostCheck.True(state.Begin(30) && state.Advance(2, waiter: 31));
        var armed = state.Inspect();
        foreach (int phase in new[] { -1, 0, 1, 2, 7, 8, 9 })
        {
            HostCheck.True(!state.Advance(phase, actor: 32));
            HostCheck.Equal(armed, state.Inspect());
        }
        HostCheck.True(!state.Begin(30) && !state.Begin(99));
        HostCheck.Equal(armed, state.Inspect());
        HostCheck.True(!state.Advance(3, waiter: -1, actor: 32));
        HostCheck.Equal(armed, state.Inspect());
        HostCheck.True(!state.Advance(3, waiter: 99, actor: 32));
        HostCheck.Equal(armed, state.Inspect());
        HostCheck.True(!state.Advance(3, actor: 32, callback: 31));
        HostCheck.Equal(armed, state.Inspect());
        HostCheck.True(!state.Advance(3, actor: 31, callback: 32));
        HostCheck.Equal(armed, state.Inspect());
        HostCheck.True(!state.Advance(3, actor: 32, callback: -1));
        HostCheck.Equal(armed, state.Inspect());
        HostCheck.True(state.Advance(3, waiter: 31, actor: 32, callback: 32));
        var acknowledged = state.Inspect();
        HostCheck.True(!state.Advance(4, actor: 33) && !state.Advance(4, callback: 33));
        HostCheck.Equal(acknowledged, state.Inspect());
        HostCheck.True(state.Advance(4, waiter: 31, actor: 32, callback: 32));
        HostCheck.True(armed is { Phase: 2, Actor: 0, Callback: 0, RecoveryRequested: false });
    }

    [HostTest("m10-012 signal race cleanup permits either terminal result and then refuses mutations")]
    private static void TerminalAndStatus()
    {
        HostCheck.True(KestrelSignalRaceState.Idle == 0 && KestrelSignalRaceState.Starting == 1 &&
            KestrelSignalRaceState.InitialArmed == 2 && KestrelSignalRaceState.SignalAcknowledged == 3 &&
            KestrelSignalRaceState.RecoveryArmed == 4 && KestrelSignalRaceState.RecoveryReturned == 5 &&
            KestrelSignalRaceState.Cleanup == 6 && KestrelSignalRaceState.Complete == 7 && KestrelSignalRaceState.Failed == 8);
        foreach (bool success in new[] { true, false })
        {
            var state = new KestrelSignalRaceState();
            HostCheck.Equal("CLROS_SIGNAL_RACE_STATUS phase=0 pid=0 waiter=0 actor=0 callback=0 recoveryRequested=0", state.Read());
            HostCheck.True(!state.Finish(success) && state.Begin(40) && !state.Finish(success));
            HostCheck.True(state.Advance(6, waiter: 41, actor: 42, callback: 42) && state.Finish(success));
            var terminal = state.Inspect();
            HostCheck.Equal(success ? 7 : 8, terminal.Phase);
            HostCheck.Equal($"CLROS_SIGNAL_RACE_STATUS phase={(success ? 7 : 8)} pid=40 waiter=41 actor=42 callback=42 recoveryRequested=0", state.Read());
            HostCheck.True(!state.Begin(40) && !state.Advance(6) && !state.TryRequestRecovery() && !state.Finish(!success));
            HostCheck.Equal(terminal, state.Inspect());
        }
    }
}
