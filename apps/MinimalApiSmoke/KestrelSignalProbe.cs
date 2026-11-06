using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Abi = KestrelSignalProbeAbi;

/// <summary>One explicit Linux-x64 signal ownership experiment. The app reserves
/// SIGUSR2 only while its actual disposition is default. One dedicated waiter
/// owns its thread mask and borrows three descriptors; the request owns those
/// descriptors and registration until native return and callback acknowledgment.
/// Callback scheduling is separate from directed native interruption. Unsafe
/// cleanup uncertainty terminates this diagnostic process rather than leaving
/// handlers/threads pointing into released state. No guest authority follows.</summary>

internal sealed partial class KestrelSignalProbe
{
    private const ulong SignalBit = 1UL << (Abi.Signal - 1), RestorerFlag = 0x04000000;
    private const ulong ProbeTag = 0x50524f4245;
    private int started;
    private readonly KestrelSignalRaceState race = new();
    // Copied phase data gates external stimuli; it never proves a native wait.
    internal string ReadRace() => race.Read();
    internal bool TryReadRace(bool eventOwned, out string value) => race.TryRead(eventOwned, out value);
    internal bool RequestStart() => race.TryRequestStart();
    internal bool RequestStart(bool eventOwned) => race.TryRequestStart(eventOwned);
    internal bool RequestRecovery() => race.TryRequestRecovery();
    internal bool RequestRecovery(bool eventOwned) => race.TryRequestRecovery(eventOwned);
    /// <summary>Attempt once per app instance. Null rejects repeated/concurrent
    /// calls before ownership. The caller supplies request cancellation; cleanup
    /// has its own finite bound. Unsupported platforms throw before native entry.
    /// Positive return contains copied observations only; original syscall and
    /// handler/restorer image evidence is still required. No custom handler runs.</summary>

    internal Task<string?> RunAsync(CancellationToken cancellation) => RunAsync(false, cancellation);
    /// <summary>One explicit competing-reader attempt, sharing the signal probe's
    /// single admission. An external owner must request recovery during phase4
    /// and prove actual pending churn and original native ordering separately.
    /// Early readiness, timeout or missing request fails instead of retrying.
    /// Managed joins retire delegate borrows, not native pthread tail lifetimes.</summary>

    internal Task<string?> RunRaceAsync(CancellationToken cancellation) => RunAsync(true, cancellation);
    /// <summary>Explicit v3: one request-owned 120s operation deadline owns
    /// initial/recovery untimed epoll waits and managed preparation gates.
    /// Finally supplies the native wake and separately bounded joined cleanup.
    /// This changes no legacy timed profile and grants no execution evidence.</summary>

    internal Task<string?> RunEventOwnedRaceAsync(CancellationToken cancellation) => RunAsync(true, cancellation, true);
    private async Task<string?> RunAsync(bool competing, CancellationToken cancellation, bool eventOwned = false)
    {
        if (!Abi.Supported)
            throw new PlatformNotSupportedException("Linux x64 signal diagnostic required.");
        if (!cancellation.CanBeCanceled)
            throw new ArgumentException("Request cancellation required.", nameof(cancellation));
        cancellation.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref started, 1, 0) != 0)
            return null;
        using EventLifetime? operation = eventOwned ? new(cancellation) : null;
        if (operation is not null)
            cancellation = operation.Token;
        int process = Abi.GetPid(), controller = Abi.GetTid(), read = -1, write = -1, epoll = -1;
        if (competing)
            Need(race.Begin(process, eventOwned), "race_start");
        var callback = new CallbackState();
        Abi.KernelAction before = default, installed = default, restored = default;
        PosixSignalRegistration? registration = null;
        Waiter? waiter = null;
        Thread? thread = null;
        Actor? actor = null;
        Thread? actorThread = null;
        bool threadStarted = false, actorStarted = false, actorJoined = false, signalSent = false, actionKnown = false, joined = false, completed = false, restoredWorker = false;
        int recoveryWriter = 0;
        Task Progress(Func<bool> ready, CancellationToken token) => operation is null ? Until(ready, token) : UntilOwned(ready, operation);
        try
        {
            before = Abi.ReadAction();
            Need(Abi.IsDefaultAction(before), "occupied_signal");
            registration = PosixSignalRegistration.Create((PosixSignal)Abi.Signal, callback.Handle);
            installed = Abi.ReadAction();
            actionKnown = true;
            Need(Abi.IsInstalledAction(installed) && installed.Handler != before.Handler, "installed_action");
            Abi.CreatePipe(out read, out write);
            epoll = Abi.CreateEpoll();
            Abi.AddRead(epoll, read, ProbeTag);
            waiter = new Waiter(epoll, read, competing, operation);
            thread = new Thread(waiter.Run, 256 * 1024)
            {
                IsBackground = true
            };
            thread.Start();
            threadStarted = true;
            await Progress(() => Volatile.Read(ref waiter.Phase) >= 1, cancellation);
            Need(waiter.Error is null && waiter.Tid > 0 && waiter.Tid != controller && Volatile.Read(ref waiter.Phase) == 1, "waiter_start");
            if (competing)
            {
                actor = new Actor(process, waiter.Tid, read, write, operation);
                actorThread = new Thread(actor.Run, 256 * 1024)
                {
                    IsBackground = true
                };
                actorThread.Start();
                actorStarted = true;
                await Progress(() => Volatile.Read(ref actor.Ready) != 0 || Volatile.Read(ref actor.Finished) != 0, cancellation);
                Need(actor.Error is null && actor.Tid > 0 && actor.Tid != waiter.Tid && actor.Ready == 1, "actor_start");
                Need(race.Advance(KestrelSignalRaceState.InitialArmed, waiter: waiter.Tid, actor: actor.Tid), "race_initial");
            }

            if (competing)
            {
                // The external owner prepares its schedule before releasing the
                // actor. Phase1 still does not prove an actual blocked syscall.
                // A timed-out/failed waiter is never recycled into another try.
                await Progress(() => race.StartRequested || Volatile.Read(ref waiter.Phase) != 1, cancellation);
                Need(race.StartRequested && waiter.Error is null && Volatile.Read(ref waiter.Phase) == 1, "initial_wait_not_pending");
            }
            // The original signal-only experiment keeps its bounded window.
            // Actual native entry remains an original-trace requirement.
            else
                await Task.Delay(25, cancellation);
            Need(Abi.SameAction(installed, Abi.ReadAction()), "action_drift");
            if (competing)
            {
                actor!.Go.Set();
                await Progress(() => Volatile.Read(ref actor.Finished) != 0, cancellation);
                actorJoined = operation?.Join(actorThread!) ?? actorThread!.Join(TimeSpan.FromSeconds(5));
                operation?.Check();
                if (!actorJoined)
                    Fatal("actor_join");
                signalSent = Volatile.Read(ref actor.Sent) != 0;
                Need(actor.Error is null && signalSent && actor.ConsumedByte == Abi.WakeByte, "actor_transaction");
                controller = actor.Tid;
            }
            else
            {
                controller = Abi.GetTid();
                Need(controller != waiter.Tid, "sender_identity");
                Abi.SendSignal(process, waiter.Tid);
                signalSent = true;
            }

            await Progress(() => Volatile.Read(ref waiter.Phase) >= 2 && Volatile.Read(ref callback.Count) > 0 && Volatile.Read(ref callback.Active) == 0, cancellation);
            Need(waiter.Error is null && waiter.Result.Value == -1 && waiter.Result.Errno == 4 && waiter.Result.Events == uint.MaxValue && waiter.Result.Data == ulong.MaxValue, "native_eintr");
            Need(Volatile.Read(ref callback.Count) == 1 && Volatile.Read(ref callback.Failed) == 0 && Volatile.Read(ref callback.Tid) > 0 && callback.Tid != waiter.Tid, "callback_ack");
            if (competing)
            {
                Need(race.Advance(KestrelSignalRaceState.SignalAcknowledged, callback: callback.Tid), "race_signal");
                // The joined actor has relinquished read ownership. This phase
                // only schedules the external recovery request; original traces
                // must prove the following epoll wait really blocked empty.
                Need(race.Advance(KestrelSignalRaceState.RecoveryArmed), "race_recovery");
                waiter.BeginRecovery.Set();
                await Progress(() => Volatile.Read(ref waiter.Phase) >= 3, cancellation);
                Need(waiter.Error is null && waiter.Phase == 3, "recovery_start");
                await Progress(() => race.Inspect().RecoveryRequested || Volatile.Read(ref waiter.Phase) >= 4, cancellation);
                Need(race.Inspect().RecoveryRequested && waiter.Phase == 3, "recovery_request");
                await Task.Delay(25, cancellation);
                Need(waiter.Phase == 3, "recovery_pending");
                recoveryWriter = Abi.GetTid();
                Abi.WriteByte(write, Abi.RecoveryByte);
                await Progress(() => Volatile.Read(ref waiter.Phase) >= 4, cancellation);
                Need(waiter.Error is null && waiter.Recovery.Value == 1 && waiter.Recovery.Events == 1 && waiter.Recovery.Data == ProbeTag && waiter.RecoveredByte == Abi.RecoveryByte, "recovery_result");
                Need(race.Advance(KestrelSignalRaceState.RecoveryReturned), "race_return");
            }

            operation?.Check();
            completed = true;
        }
        finally
        {
            using EventLifetime? cleanup = eventOwned ? new(CancellationToken.None, TimeSpan.FromSeconds(15)) : null;
            bool cleanupPhase = !competing || race.Advance(KestrelSignalRaceState.Cleanup);
            if (actor is not null)
            {
                Volatile.Write(ref actor.Abort, 1);
                actor.Go.Set();
                if (actorStarted && !actorJoined)
                {
                    actorJoined = cleanup?.Join(actorThread!) ?? actorThread!.Join(TimeSpan.FromSeconds(5));
                    if (!actorJoined)
                        Fatal("actor_cleanup_join");
                }

                // Join before examining Sent: cancellation can arrive between
                // successful tgkill and publication of the copied sent flag.
                signalSent |= Volatile.Read(ref actor.Sent) != 0;
            }

            // Disposing with a signal still in the shared native pipe can invoke
            // its default action later. Never unregister before both witnesses.
            if (signalSent && waiter is not null)
            {
                try
                {
                    bool Acknowledged() => Volatile.Read(ref waiter.Phase) >= 2 && Volatile.Read(ref callback.Count) > 0 && Volatile.Read(ref callback.Active) == 0;
                    if (cleanup is null)
                        await Until(Acknowledged, CancellationToken.None);
                    else
                        await UntilOwned(Acknowledged, cleanup);
                }
                catch
                {
                    Fatal("pending_signal_cleanup");
                }
            }

            if (registration is not null)
            {
                try
                {
                    Need(actionKnown && Abi.SameAction(installed, Abi.ReadAction()), "cleanup_action_drift");
                    registration.Dispose();
                    restored = Abi.ReadAction();
                    // libc may add its restorer when restoring a default action.
                    // Only this documented normalization is eligible for exact
                    // raw restoration; never overwrite a foreign handler/mask.
                    Need(restored.Handler == before.Handler && restored.Mask == before.Mask && (restored.Flags & ~RestorerFlag) == (before.Flags & ~RestorerFlag) && (restored.Restorer == before.Restorer || (restored.Flags & RestorerFlag) != 0), "unregister_restore");
                    if (!Abi.SameAction(before, restored))
                        Abi.SetAction(before);
                    restored = Abi.ReadAction();
                    Need(Abi.SameAction(before, restored), "exact_action_restore");
                }
                catch
                {
                    Fatal("action_cleanup");
                }
            }

            if (waiter is not null)
            {
                Volatile.Write(ref waiter.AbortRecovery, 1);
                waiter.BeginRecovery.Set();
                if (threadStarted && Volatile.Read(ref waiter.Phase) < 4 && write >= 0)
                    try
                    {
                        Abi.WriteWake(write);
                    }
                    catch
                    {
                        Fatal("waiter_wake");
                    }

                waiter.Release.Set();
            }

            if (threadStarted)
            {
                joined = cleanup?.Join(thread!) ?? thread!.Join(TimeSpan.FromSeconds(5));
                if (!joined)
                    Fatal("waiter_join");
            }

            waiter?.Dispose();
            actor?.Dispose();
            bool closed = true;
            foreach (int descriptor in new[]
            {
                epoll,
                read,
                write
            }

            )
                if (descriptor >= 0)
                    try
                    {
                        Abi.Close(descriptor);
                    }
                    catch
                    {
                        closed = false;
                    }

            if (!closed)
                Fatal("descriptor_cleanup");
            if (cleanup is not null)
                try
                {
                    cleanup.Check();
                }
                catch
                {
                    Fatal("cleanup_deadline");
                }

            restoredWorker = joined && waiter is not null && waiter.Error is null && waiter.Phase == 5 && waiter.MaskAfter == waiter.MaskBefore && Abi.SameStack(waiter.StackBefore, waiter.StackAfter);
            if (competing)
                Need(cleanupPhase && race.Finish(completed && restoredWorker && !cancellation.IsCancellationRequested), "race_finish");
        }

        operation?.Check();
        cancellation.ThrowIfCancellationRequested();
        Need(restoredWorker, "joined_restore");
        if (competing)
            return string.Create(CultureInfo.InvariantCulture, $"{(eventOwned ? "CLROS_SIGNAL_RACE_V3_OK" : "CLROS_SIGNAL_RACE_OK")} signal={Abi.Signal} pid={process} actor={actor!.Tid} waiter={waiter!.Tid} callback={callback.Tid} callbacks={callback.Count} actorJoined=1 waiterJoined=1 consumed={actor.ConsumedByte} recovery={waiter.RecoveredByte} recoveryWriter={recoveryWriter} initialValue={waiter.Result.Value} initialErrno={waiter.Result.Errno} recoveryValue={waiter.Recovery.Value} recoveryEvents={waiter.Recovery.Events} recoveryTag={waiter.Recovery.Data} handler={installed.Handler} flags={installed.Flags} restorer={installed.Restorer} actionMask={installed.Mask} originalHandler={before.Handler} originalFlags={before.Flags} originalRestorer={before.Restorer} originalActionMask={before.Mask} maskBefore={waiter.MaskBefore} maskDuring={waiter.MaskDuring} maskAfter={waiter.MaskAfter} altAddress={waiter.StackBefore.Address} altFlags={waiter.StackBefore.Flags} altBytes={waiter.StackBefore.Bytes} descriptorsClosed=3 actionRestored=1 altUnchanged=1");
        return string.Create(CultureInfo.InvariantCulture, $"CLROS_SIGNAL_PROBE_OK signal={Abi.Signal} pid={process} controller={controller} waiter={waiter!.Tid} callback={callback.Tid} callbacks={callback.Count} handler={installed.Handler} flags={installed.Flags} restorer={installed.Restorer} actionMask={installed.Mask} originalHandler={before.Handler} originalFlags={before.Flags} originalRestorer={before.Restorer} originalActionMask={before.Mask} maskBefore={waiter.MaskBefore} maskDuring={waiter.MaskDuring} maskAfter={waiter.MaskAfter} altAddress={waiter.StackBefore.Address} altFlags={waiter.StackBefore.Flags} altBytes={waiter.StackBefore.Bytes} waitValue={waiter.Result.Value} errno={waiter.Result.Errno} joined=1 descriptorsClosed=3 actionRestored=1 altUnchanged=1");
    }

    private static async Task Until(Func<bool> ready, CancellationToken cancellation)
    {
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (ready())
                return;
            await Task.Delay(5, cancellation);
        }

        throw new TimeoutException("CLROS_SIGNAL_PROBE_FAIL progress");
    }

    private static async Task UntilOwned(Func<bool> ready, EventLifetime lifetime)
    {
        while (true)
        {
            lifetime.Check();
            if (ready())
                return;
            await Task.Delay(5, lifetime.Token);
        }
    }

    private static void Need([DoesNotReturnIf(false)] bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException("CLROS_SIGNAL_PROBE_FAIL " + reason);
    }

    [DoesNotReturn]
    private static void Fatal(string reason)
    {
        Console.Error.WriteLine("CLROS_SIGNAL_PROBE_FATAL " + reason);
        Environment.Exit(70);
    }
}
