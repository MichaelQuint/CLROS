using System.Globalization;

/// <summary>Bounded copied coordination state for one app-owned signal race.
/// Mutations are atomic under the instance lock; refusals return false without
/// changing any field. Snapshots own values only, never descriptors, callbacks,
/// events or native pointers. Phases and identities are scheduling intent and
/// do not prove signal delivery, blocked waits, syscall order or guest execution.</summary>
internal sealed class KestrelSignalRaceState
{
    internal const int Idle = 0, Starting = 1, InitialArmed = 2, SignalAcknowledged = 3,
        RecoveryArmed = 4, RecoveryReturned = 5, Cleanup = 6, Complete = 7, Failed = 8;
    internal readonly record struct Snapshot(int Phase, int Process, int Waiter, int Actor, int Callback, bool RecoveryRequested);
    private readonly object gate = new();
    private Snapshot state;

    internal bool Begin(int positivePid)
    {
        lock (gate)
        {
            if (positivePid <= 0 || state.Phase != Idle) return false;
            state = new(Starting, positivePid, 0, 0, 0, false); return true;
        }
    }

    // Zero leaves an identity unknown/unchanged. Positive identities latch once;
    // copied actor/callback IDs may coincide, but neither is the waiter. This
    // copied-value check grants no native role, thread-generation or wait proof.
    internal bool Advance(int phase, int waiter = 0, int actor = 0, int callback = 0)
    {
        lock (gate)
        {
            if (state.Phase < Starting || phase <= state.Phase || phase > Cleanup ||
                !Identity(state.Waiter, waiter) || !Identity(state.Actor, actor) || !Identity(state.Callback, callback)) return false;
            int nextWaiter = waiter == 0 ? state.Waiter : waiter,
                nextActor = actor == 0 ? state.Actor : actor, nextCallback = callback == 0 ? state.Callback : callback;
            if (nextWaiter != 0 && (nextWaiter == nextActor || nextWaiter == nextCallback)) return false;
            state = state with { Phase = phase, Waiter = nextWaiter, Actor = nextActor, Callback = nextCallback };
            return true;
        }
    }

    internal bool TryRequestRecovery()
    {
        lock (gate)
        {
            if (state.Phase != RecoveryArmed || state.RecoveryRequested) return false;
            state = state with { RecoveryRequested = true }; return true;
        }
    }

    internal bool Finish(bool success)
    {
        lock (gate)
        {
            if (state.Phase != Cleanup) return false;
            state = state with { Phase = success ? Complete : Failed }; return true;
        }
    }

    internal Snapshot Inspect() { lock (gate) return state; }
    internal string Read()
    {
        Snapshot value = Inspect();
        return string.Create(CultureInfo.InvariantCulture,
            $"CLROS_SIGNAL_RACE_STATUS phase={value.Phase} pid={value.Process} waiter={value.Waiter} actor={value.Actor} callback={value.Callback} recoveryRequested={(value.RecoveryRequested ? 1 : 0)}");
    }
    private static bool Identity(int current, int supplied) => supplied >= 0 && (supplied == 0 || current == 0 || supplied == current);
}
