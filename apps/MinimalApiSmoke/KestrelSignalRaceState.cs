using System.Globalization;

/// <summary>Bounded copied coordination state for one app-owned signal race.
/// Mutations are atomic under the instance lock; refusals return false without
/// changing any field. Snapshots own values only, never descriptors, callbacks,
/// events or native pointers. Phases and identities are scheduling intent and
/// do not prove signal delivery, blocked waits, syscall order or guest execution.</summary>

internal sealed partial class KestrelSignalRaceState
{
    internal const int Idle = 0, Starting = 1, InitialArmed = 2, SignalAcknowledged = 3, RecoveryArmed = 4, RecoveryReturned = 5, Cleanup = 6, Complete = 7, Failed = 8;
    private readonly object gate = new();
    private Snapshot state;
    private bool startRequested;
    private bool eventOwned;
    // An accepted request releases scheduling intent once; it is not evidence
    // that the waiter entered epoll or that the actor has run.
    internal bool TryRequestStart() => TryRequestStart(false);
    internal bool TryRequestStart(bool owned)
    {
        lock (gate)
        {
            if (owned != eventOwned || state.Phase != InitialArmed || state.Waiter <= 0 || state.Actor <= 0 || startRequested)
                return false;
            startRequested = true;
            return true;
        }
    }

    internal bool StartRequested
    {
        get
        {
            lock (gate)
                return startRequested;
        }
    }

    internal bool Begin(int positivePid) => Begin(positivePid, false);
    internal bool Begin(int positivePid, bool owned)
    {
        lock (gate)
        {
            if (positivePid <= 0 || state.Phase != Idle)
                return false;
            eventOwned = owned;
            state = new(Starting, positivePid, 0, 0, 0, false);
            return true;
        }
    }

    // Zero leaves an identity unknown/unchanged. Positive identities latch once;
    // copied actor/callback IDs may coincide, but neither is the waiter. This
    // copied-value check grants no native role, thread-generation or wait proof.
    internal bool Advance(int phase, int waiter = 0, int actor = 0, int callback = 0)
    {
        lock (gate)
        {
            if (state.Phase < Starting || phase <= state.Phase || phase > Cleanup || !Identity(state.Waiter, waiter) || !Identity(state.Actor, actor) || !Identity(state.Callback, callback))
                return false;
            int nextWaiter = waiter == 0 ? state.Waiter : waiter, nextActor = actor == 0 ? state.Actor : actor, nextCallback = callback == 0 ? state.Callback : callback;
            if (nextWaiter != 0 && (nextWaiter == nextActor || nextWaiter == nextCallback))
                return false;
            state = state with
            {
                Phase = phase,
                Waiter = nextWaiter,
                Actor = nextActor,
                Callback = nextCallback
            };
            return true;
        }
    }

    internal bool TryRequestRecovery() => TryRequestRecovery(false);
    internal bool TryRequestRecovery(bool owned)
    {
        lock (gate)
        {
            if (owned != eventOwned || state.Phase != RecoveryArmed || state.RecoveryRequested)
                return false;
            state = state with
            {
                RecoveryRequested = true
            };
            return true;
        }
    }

    internal bool Finish(bool success)
    {
        lock (gate)
        {
            if (state.Phase != Cleanup)
                return false;
            state = state with
            {
                Phase = success ? Complete : Failed
            };
            return true;
        }
    }

    internal Snapshot Inspect()
    {
        lock (gate)
            return state;
    }

    internal string Read()
    {
        return Format(Inspect(), false);
    }

    // Idle has no selected contract. Once admitted, a different header cannot
    // relabel the live operation's copied status or release either native wait.
    internal bool TryRead(bool owned, out string value)
    {
        lock (gate)
        {
            value = string.Empty;
            if (state.Phase != Idle && owned != eventOwned)
                return false;
            value = Format(state, owned);
            return true;
        }
    }

    private static string Format(Snapshot value, bool owned)
    {
        string prefix = owned ? "CLROS_SIGNAL_RACE_V3_STATUS" : "CLROS_SIGNAL_RACE_STATUS";
        return string.Create(CultureInfo.InvariantCulture, $"{prefix} phase={value.Phase} pid={value.Process} waiter={value.Waiter} actor={value.Actor} callback={value.Callback} recoveryRequested={(value.RecoveryRequested ? 1 : 0)}");
    }

    private static bool Identity(int current, int supplied) => supplied >= 0 && (supplied == 0 || current == 0 || supplied == current);
}
