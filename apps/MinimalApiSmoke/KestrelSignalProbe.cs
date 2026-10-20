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
internal sealed class KestrelSignalProbe
{
    private const ulong SignalBit = 1UL << (Abi.Signal - 1), RestorerFlag = 0x04000000;
    private const ulong ProbeTag = 0x50524f4245;
    private int started;
    private readonly KestrelSignalRaceState race = new();

    // Copied phase data gates external stimuli; it never proves a native wait.
    internal string ReadRace() => race.Read();
    internal bool RequestRecovery() => race.TryRequestRecovery();

    // This callback state owns no native pointer, descriptor, event or waiter.
    // A late framework-owned callback can retain this managed object safely;
    // unregistering does not promise to join the global .NET signal thread.
    private sealed class CallbackState
    {
        internal int Count, Active, Failed, Tid;
        internal void Handle(PosixSignalContext context)
        {
            context.Cancel = true;
            Interlocked.Increment(ref Active);
            try
            {
                if (context.Signal != (PosixSignal)Abi.Signal || !Thread.CurrentThread.IsThreadPoolThread)
                    Interlocked.Exchange(ref Failed, 1);
                Volatile.Write(ref Tid, Abi.GetTid());
            }
            catch { Interlocked.Exchange(ref Failed, 1); }
            finally { Interlocked.Increment(ref Count); Interlocked.Decrement(ref Active); }
        }
    }

    private sealed class Waiter(int epoll, int read, bool competing) : IDisposable
    {
        internal readonly ManualResetEventSlim Release = new(false);
        internal readonly ManualResetEventSlim BeginRecovery = new(false);
        internal int Phase, Tid, AbortRecovery;
        internal ulong MaskBefore, MaskDuring, MaskAfter;
        internal Abi.AltStack StackBefore, StackAfter;
        internal Abi.WaitResult Result, Recovery;
        internal byte RecoveredByte;
        internal Exception? Error;
        internal void Run()
        {
            bool maskOwned = false;
            try
            {
                Tid = Abi.GetTid(); MaskBefore = Abi.ReadMask(); StackBefore = Abi.ReadAltStack();
                Abi.SetMask(MaskBefore & ~SignalBit); maskOwned = true; MaskDuring = Abi.ReadMask();
                Need(MaskDuring == (MaskBefore & ~SignalBit), "waiter_mask");
                Volatile.Write(ref Phase, 1);
                Result = Abi.Wait(epoll, 2500);
                Volatile.Write(ref Phase, 2);
                if (competing)
                {
                    if (!BeginRecovery.Wait(TimeSpan.FromSeconds(15))) Fatal("recovery_release");
                    if (Volatile.Read(ref AbortRecovery) == 0)
                    {
                        Volatile.Write(ref Phase, 3);
                        Recovery = Abi.Wait(epoll, 2500);
                        if (Volatile.Read(ref AbortRecovery) == 0 && Recovery.Value == 1 &&
                            Recovery.Events == 1 && Recovery.Data == ProbeTag)
                            RecoveredByte = Abi.ReadByte(read);
                    }
                }
            }
            catch (Exception error) { Error = error; }
            finally
            {
                Volatile.Write(ref Phase, 4);
                if (!Release.Wait(TimeSpan.FromSeconds(15))) Fatal("waiter_release");
                try
                {
                    if (maskOwned)
                    {
                        Abi.SetMask(MaskBefore); MaskAfter = Abi.ReadMask(); StackAfter = Abi.ReadAltStack();
                        Need(MaskAfter == MaskBefore && Abi.SameStack(StackBefore, StackAfter), "waiter_restore");
                    }
                }
                catch (Exception error) { Error ??= error; }
                Volatile.Write(ref Phase, 5);
            }
        }
        public void Dispose() { BeginRecovery.Dispose(); Release.Dispose(); }
    }

    // One dedicated actor borrows both pipe ends only through this delegate.
    // The three nonblocking calls form one finite transaction after Go. Once
    // entered, request cancellation cannot abandon a successfully sent signal.
    private sealed class Actor(int process, int target, int read, int write) : IDisposable
    {
        internal readonly ManualResetEventSlim Go = new(false);
        internal int Tid, Ready, Finished, Sent, Abort;
        internal byte ConsumedByte;
        internal Exception? Error;
        internal void Run()
        {
            try
            {
                Tid = Abi.GetTid(); Volatile.Write(ref Ready, 1);
                if (!Go.Wait(TimeSpan.FromSeconds(15))) Fatal("actor_release");
                if (Volatile.Read(ref Abort) != 0) return;
                Abi.WriteByte(write, Abi.WakeByte);
                Abi.SendSignal(process, target); Volatile.Write(ref Sent, 1);
                ConsumedByte = Abi.ReadByte(read);
                Need(ConsumedByte == Abi.WakeByte, "competing_byte");
            }
            catch (Exception error) { Error = error; }
            finally { Volatile.Write(ref Finished, 1); }
        }
        public void Dispose() => Go.Dispose();
    }

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

    private async Task<string?> RunAsync(bool competing, CancellationToken cancellation)
    {
        if (!Abi.Supported) throw new PlatformNotSupportedException("Linux x64 signal diagnostic required.");
        if (!cancellation.CanBeCanceled) throw new ArgumentException("Request cancellation required.", nameof(cancellation));
        cancellation.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref started, 1, 0) != 0) return null;
        int process = Abi.GetPid(), controller = Abi.GetTid(), read = -1, write = -1, epoll = -1;
        if (competing) Need(race.Begin(process), "race_start");
        var callback = new CallbackState(); Abi.KernelAction before = default, installed = default, restored = default;
        PosixSignalRegistration? registration = null; Waiter? waiter = null; Thread? thread = null;
        Actor? actor = null; Thread? actorThread = null;
        bool threadStarted = false, actorStarted = false, actorJoined = false, signalSent = false,
            actionKnown = false, joined = false, completed = false, restoredWorker = false;
        int recoveryWriter = 0;
        try
        {
            before = Abi.ReadAction(); Need(Abi.IsDefaultAction(before), "occupied_signal");
            registration = PosixSignalRegistration.Create((PosixSignal)Abi.Signal, callback.Handle);
            installed = Abi.ReadAction(); actionKnown = true;
            Need(Abi.IsInstalledAction(installed) && installed.Handler != before.Handler, "installed_action");
            Abi.CreatePipe(out read, out write); epoll = Abi.CreateEpoll(); Abi.AddRead(epoll, read, ProbeTag);
            waiter = new Waiter(epoll, read, competing); thread = new Thread(waiter.Run, 256 * 1024) { IsBackground = true };
            thread.Start(); threadStarted = true;
            await Until(() => Volatile.Read(ref waiter.Phase) >= 1, cancellation);
            Need(waiter.Error is null && waiter.Tid > 0 && waiter.Tid != controller && Volatile.Read(ref waiter.Phase) == 1, "waiter_start");
            if (competing)
            {
                actor = new Actor(process, waiter.Tid, read, write);
                actorThread = new Thread(actor.Run, 256 * 1024) { IsBackground = true };
                actorThread.Start(); actorStarted = true;
                await Until(() => Volatile.Read(ref actor.Ready) != 0 || Volatile.Read(ref actor.Finished) != 0, cancellation);
                Need(actor.Error is null && actor.Tid > 0 && actor.Tid != waiter.Tid && actor.Ready == 1, "actor_start");
                Need(race.Advance(KestrelSignalRaceState.InitialArmed, waiter: waiter.Tid, actor: actor.Tid), "race_initial");
            }
            // A successful original trace must prove the actual wait was entered;
            // this bounded scheduling window is not that proof and is not retried.
            await Task.Delay(25, cancellation);
            Need(Abi.SameAction(installed, Abi.ReadAction()), "action_drift");
            if (competing)
            {
                actor!.Go.Set();
                await Until(() => Volatile.Read(ref actor.Finished) != 0, cancellation);
                actorJoined = actorThread!.Join(TimeSpan.FromSeconds(5)); if (!actorJoined) Fatal("actor_join");
                signalSent = Volatile.Read(ref actor.Sent) != 0;
                Need(actor.Error is null && signalSent && actor.ConsumedByte == Abi.WakeByte, "actor_transaction");
                controller = actor.Tid;
            }
            else
            {
                controller = Abi.GetTid(); Need(controller != waiter.Tid, "sender_identity");
                Abi.SendSignal(process, waiter.Tid); signalSent = true;
            }
            await Until(() => Volatile.Read(ref waiter.Phase) >= 2 &&
                Volatile.Read(ref callback.Count) > 0 && Volatile.Read(ref callback.Active) == 0, cancellation);
            Need(waiter.Error is null && waiter.Result.Value == -1 && waiter.Result.Errno == 4 &&
                waiter.Result.Events == uint.MaxValue && waiter.Result.Data == ulong.MaxValue, "native_eintr");
            Need(Volatile.Read(ref callback.Count) == 1 && Volatile.Read(ref callback.Failed) == 0 &&
                Volatile.Read(ref callback.Tid) > 0 && callback.Tid != waiter.Tid, "callback_ack");
            if (competing)
            {
                Need(race.Advance(KestrelSignalRaceState.SignalAcknowledged, callback: callback.Tid), "race_signal");
                // The joined actor has relinquished read ownership. This phase
                // only schedules the external recovery request; original traces
                // must prove the following epoll wait really blocked empty.
                Need(race.Advance(KestrelSignalRaceState.RecoveryArmed), "race_recovery");
                waiter.BeginRecovery.Set();
                await Until(() => Volatile.Read(ref waiter.Phase) >= 3, cancellation);
                Need(waiter.Error is null && waiter.Phase == 3, "recovery_start");
                await Until(() => race.Inspect().RecoveryRequested || Volatile.Read(ref waiter.Phase) >= 4, cancellation);
                Need(race.Inspect().RecoveryRequested && waiter.Phase == 3, "recovery_request");
                await Task.Delay(25, cancellation);
                Need(waiter.Phase == 3, "recovery_pending");
                recoveryWriter = Abi.GetTid(); Abi.WriteByte(write, Abi.RecoveryByte);
                await Until(() => Volatile.Read(ref waiter.Phase) >= 4, cancellation);
                Need(waiter.Error is null && waiter.Recovery.Value == 1 && waiter.Recovery.Events == 1 &&
                    waiter.Recovery.Data == ProbeTag && waiter.RecoveredByte == Abi.RecoveryByte, "recovery_result");
                Need(race.Advance(KestrelSignalRaceState.RecoveryReturned), "race_return");
            }
            completed = true;
        }
        finally
        {
            bool cleanupPhase = !competing || race.Advance(KestrelSignalRaceState.Cleanup);
            if (actor is not null)
            {
                Volatile.Write(ref actor.Abort, 1); actor.Go.Set();
                if (actorStarted && !actorJoined)
                { actorJoined = actorThread!.Join(TimeSpan.FromSeconds(5)); if (!actorJoined) Fatal("actor_cleanup_join"); }
                // Join before examining Sent: cancellation can arrive between
                // successful tgkill and publication of the copied sent flag.
                signalSent |= Volatile.Read(ref actor.Sent) != 0;
            }
            // Disposing with a signal still in the shared native pipe can invoke
            // its default action later. Never unregister before both witnesses.
            if (signalSent && waiter is not null)
            {
                try { await Until(() => Volatile.Read(ref waiter.Phase) >= 2 &&
                    Volatile.Read(ref callback.Count) > 0 && Volatile.Read(ref callback.Active) == 0, CancellationToken.None); }
                catch { Fatal("pending_signal_cleanup"); }
            }
            if (registration is not null)
            {
                try
                {
                    Need(actionKnown && Abi.SameAction(installed, Abi.ReadAction()), "cleanup_action_drift");
                    registration.Dispose(); restored = Abi.ReadAction();
                    // libc may add its restorer when restoring a default action.
                    // Only this documented normalization is eligible for exact
                    // raw restoration; never overwrite a foreign handler/mask.
                    Need(restored.Handler == before.Handler && restored.Mask == before.Mask &&
                        (restored.Flags & ~RestorerFlag) == (before.Flags & ~RestorerFlag) &&
                        (restored.Restorer == before.Restorer || (restored.Flags & RestorerFlag) != 0), "unregister_restore");
                    if (!Abi.SameAction(before, restored)) Abi.SetAction(before);
                    restored = Abi.ReadAction(); Need(Abi.SameAction(before, restored), "exact_action_restore");
                }
                catch { Fatal("action_cleanup"); }
            }
            if (waiter is not null)
            {
                Volatile.Write(ref waiter.AbortRecovery, 1); waiter.BeginRecovery.Set();
                if (threadStarted && Volatile.Read(ref waiter.Phase) < 4 && write >= 0)
                    try { Abi.WriteWake(write); } catch { Fatal("waiter_wake"); }
                waiter.Release.Set();
            }
            if (threadStarted)
            {
                joined = thread!.Join(TimeSpan.FromSeconds(5)); if (!joined) Fatal("waiter_join");
            }
            waiter?.Dispose(); actor?.Dispose();
            bool closed = true;
            foreach (int descriptor in new[] { epoll, read, write })
                if (descriptor >= 0) try { Abi.Close(descriptor); } catch { closed = false; }
            if (!closed) Fatal("descriptor_cleanup");
            restoredWorker = joined && waiter is not null && waiter.Error is null && waiter.Phase == 5 &&
                waiter.MaskAfter == waiter.MaskBefore && Abi.SameStack(waiter.StackBefore, waiter.StackAfter);
            if (competing) Need(cleanupPhase && race.Finish(completed && restoredWorker && !cancellation.IsCancellationRequested), "race_finish");
        }
        cancellation.ThrowIfCancellationRequested();
        Need(restoredWorker, "joined_restore");
        if (competing)
            return string.Create(CultureInfo.InvariantCulture,
                $"CLROS_SIGNAL_RACE_OK signal={Abi.Signal} pid={process} actor={actor!.Tid} waiter={waiter!.Tid} callback={callback.Tid} callbacks={callback.Count} actorJoined=1 waiterJoined=1 consumed={actor.ConsumedByte} recovery={waiter.RecoveredByte} recoveryWriter={recoveryWriter} initialValue={waiter.Result.Value} initialErrno={waiter.Result.Errno} recoveryValue={waiter.Recovery.Value} recoveryEvents={waiter.Recovery.Events} recoveryTag={waiter.Recovery.Data} handler={installed.Handler} flags={installed.Flags} restorer={installed.Restorer} actionMask={installed.Mask} originalHandler={before.Handler} originalFlags={before.Flags} originalRestorer={before.Restorer} originalActionMask={before.Mask} maskBefore={waiter.MaskBefore} maskDuring={waiter.MaskDuring} maskAfter={waiter.MaskAfter} altAddress={waiter.StackBefore.Address} altFlags={waiter.StackBefore.Flags} altBytes={waiter.StackBefore.Bytes} descriptorsClosed=3 actionRestored=1 altUnchanged=1");
        return string.Create(CultureInfo.InvariantCulture,
            $"CLROS_SIGNAL_PROBE_OK signal={Abi.Signal} pid={process} controller={controller} waiter={waiter!.Tid} callback={callback.Tid} callbacks={callback.Count} handler={installed.Handler} flags={installed.Flags} restorer={installed.Restorer} actionMask={installed.Mask} originalHandler={before.Handler} originalFlags={before.Flags} originalRestorer={before.Restorer} originalActionMask={before.Mask} maskBefore={waiter.MaskBefore} maskDuring={waiter.MaskDuring} maskAfter={waiter.MaskAfter} altAddress={waiter.StackBefore.Address} altFlags={waiter.StackBefore.Flags} altBytes={waiter.StackBefore.Bytes} waitValue={waiter.Result.Value} errno={waiter.Result.Errno} joined=1 descriptorsClosed=3 actionRestored=1 altUnchanged=1");
    }

    private static async Task Until(Func<bool> ready, CancellationToken cancellation)
    {
        for (int attempt = 0; attempt < 1000; attempt++)
        { cancellation.ThrowIfCancellationRequested(); if (ready()) return; await Task.Delay(5, cancellation); }
        throw new TimeoutException("CLROS_SIGNAL_PROBE_FAIL progress");
    }
    private static void Need([DoesNotReturnIf(false)] bool value, string reason)
    { if (!value) throw new InvalidOperationException("CLROS_SIGNAL_PROBE_FAIL " + reason); }
    [DoesNotReturn]
    private static void Fatal(string reason)
    { Console.Error.WriteLine("CLROS_SIGNAL_PROBE_FATAL " + reason); Environment.Exit(70); }
}
