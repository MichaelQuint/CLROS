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
    private int started;

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

    private sealed class Waiter(int epoll) : IDisposable
    {
        internal readonly ManualResetEventSlim Release = new(false);
        internal int Phase, Tid;
        internal ulong MaskBefore, MaskDuring, MaskAfter;
        internal Abi.AltStack StackBefore, StackAfter;
        internal Abi.WaitResult Result;
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
            }
            catch (Exception error) { Error = error; }
            finally
            {
                Volatile.Write(ref Phase, 2);
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
                Volatile.Write(ref Phase, 3);
            }
        }
        public void Dispose() => Release.Dispose();
    }

    /// <summary>Attempt once per app instance. Null rejects repeated/concurrent
    /// calls before ownership. The caller supplies request cancellation; cleanup
    /// has its own finite bound. Unsupported platforms throw before native entry.
    /// Positive return contains copied observations only; original syscall and
    /// handler/restorer image evidence is still required. No custom handler runs.</summary>
    internal async Task<string?> RunAsync(CancellationToken cancellation)
    {
        if (!Abi.Supported) throw new PlatformNotSupportedException("Linux x64 signal diagnostic required.");
        if (!cancellation.CanBeCanceled) throw new ArgumentException("Request cancellation required.", nameof(cancellation));
        cancellation.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref started, 1, 0) != 0) return null;
        var before = Abi.ReadAction(); Need(Abi.IsDefaultAction(before), "occupied_signal");
        int process = Abi.GetPid(), controller = Abi.GetTid(), read = -1, write = -1, epoll = -1;
        var callback = new CallbackState(); Abi.KernelAction installed = default, restored = default;
        PosixSignalRegistration? registration = null; Waiter? waiter = null; Thread? thread = null;
        bool threadStarted = false, signalSent = false, actionKnown = false, joined = false;
        try
        {
            registration = PosixSignalRegistration.Create((PosixSignal)Abi.Signal, callback.Handle);
            installed = Abi.ReadAction(); actionKnown = true;
            Need(Abi.IsInstalledAction(installed) && installed.Handler != before.Handler, "installed_action");
            Abi.CreatePipe(out read, out write); epoll = Abi.CreateEpoll(); Abi.AddRead(epoll, read, 0x50524f4245);
            waiter = new Waiter(epoll); thread = new Thread(waiter.Run, 256 * 1024) { IsBackground = true };
            thread.Start(); threadStarted = true;
            await Until(() => Volatile.Read(ref waiter.Phase) >= 1, cancellation);
            Need(waiter.Error is null && waiter.Tid > 0 && waiter.Tid != controller && Volatile.Read(ref waiter.Phase) == 1, "waiter_start");
            // A successful original trace must prove the actual wait was entered;
            // this bounded scheduling window is not that proof and is not retried.
            await Task.Delay(25, cancellation);
            Need(Abi.SameAction(installed, Abi.ReadAction()), "action_drift");
            controller = Abi.GetTid(); Need(controller != waiter.Tid, "sender_identity");
            Abi.SendSignal(process, waiter.Tid); signalSent = true;
            await Until(() => Volatile.Read(ref waiter.Phase) >= 2 &&
                Volatile.Read(ref callback.Count) > 0 && Volatile.Read(ref callback.Active) == 0, cancellation);
            Need(waiter.Error is null && waiter.Result.Value == -1 && waiter.Result.Errno == 4 &&
                waiter.Result.Events == uint.MaxValue && waiter.Result.Data == ulong.MaxValue, "native_eintr");
            Need(Volatile.Read(ref callback.Count) == 1 && Volatile.Read(ref callback.Failed) == 0 &&
                Volatile.Read(ref callback.Tid) > 0 && callback.Tid != waiter.Tid, "callback_ack");
        }
        finally
        {
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
            waiter?.Release.Set();
            if (threadStarted)
            {
                if (waiter is not null && Volatile.Read(ref waiter.Phase) < 2 && write >= 0)
                    try { Abi.WriteWake(write); } catch { Fatal("waiter_wake"); }
                joined = thread!.Join(TimeSpan.FromSeconds(5)); if (!joined) Fatal("waiter_join");
            }
            waiter?.Dispose();
            bool closed = true;
            foreach (int descriptor in new[] { epoll, read, write })
                if (descriptor >= 0) try { Abi.Close(descriptor); } catch { closed = false; }
            if (!closed) Fatal("descriptor_cleanup");
        }
        cancellation.ThrowIfCancellationRequested();
        Need(joined && waiter is not null && waiter.Error is null && waiter.Phase == 3 &&
            waiter.MaskAfter == waiter.MaskBefore && Abi.SameStack(waiter.StackBefore, waiter.StackAfter), "joined_restore");
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
