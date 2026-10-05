using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using Retry = OS.Boot.BoundedRetryStaleKeyStatus;

internal static unsafe class BoundedRetryStaleKeyStatusTests
{
    private const ulong Stale = 0x8000000000000002;
    private static ulong firstStatus, secondStatus, captureStatus, lastKey, trace;
    private static int maps, exits, failCaptureAt, reentries;
    private static bool malformedSecondMap, reenter;
    private static BoundedExitRetryState* active;

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m03.006");
        Run("only stale exit status retries with a fresh map", StatusAndOrder);
        Run("attempt limits and capture failures stop without growth", BoundsAndCapture);
        Run("one retained state rejects reentry reuse and overlap", State);
        Console.WriteLine("CLROS_M03_006_READY hostOnly=true maximumAttempts=2 realExitBootServices=false");
        Console.WriteLine("TEST_PASS m03.006");
    }

    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START m03.006 " + name);
        try { test(); Console.WriteLine("TEST_PASS m03.006 " + name); }
        catch (Exception e) { Console.WriteLine("TEST_FAIL m03.006 " + name + " " + e.Message); throw; }
    }
    private static void Need(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
    private static void Reset(ulong first = 0, ulong second = 0)
    {
        firstStatus = first; secondStatus = second; captureStatus = lastKey = trace = 0;
        maps = exits = failCaptureAt = reentries = 0;
        malformedSecondMap = reenter = false; active = null;
    }
    private static BoundedExitRetryResult Invoke(ref BoundedExitRetryState state, byte* buffer, uint limit = 2)
    {
        fixed (BoundedExitRetryState* pointer = &state)
        {
            active = pointer;
            return Retry.Execute(ref state, FirmwareServicesMode.BootServicesAvailable,
                &GetMap, &Exit, (void*)0x123456789ABCDEF, buffer, 128, limit);
        }
    }
    [UnmanagedCallersOnly]
    private static ulong GetMap(ulong* size, void* buffer, ulong* key, ulong* descriptor, uint* version)
    {
        maps++; trace = (trace << 4) | 1;
        *size = maps == failCaptureAt ? 256UL : 40UL;
        *key = maps == 1 ? 0UL : 0xFFFFFFFFFFFFFFFF;
        *descriptor = malformedSecondMap && maps == 2 ? 0UL : 40UL; *version = 1;
        Reenter();
        return maps == failCaptureAt ? captureStatus : 0;
    }
    [UnmanagedCallersOnly]
    private static ulong Exit(void* handle, ulong key)
    {
        exits++; trace = (trace << 4) | 2; lastKey = key;
        if ((ulong)handle != 0x123456789ABCDEF || key != (maps == 1 ? 0UL : 0xFFFFFFFFFFFFFFFF))
            return 0x8000000000000007;
        Reenter();
        return exits == 1 ? firstStatus : secondStatus;
    }
    private static void Reenter()
    {
        if (reenter && Retry.Execute(ref *active, FirmwareServicesMode.BootServicesAvailable,
            null, null, null, null, 0).Status == BoundedExitRetryStatus.ReentryRejected) reentries++;
    }

    private static void StatusAndOrder()
    {
        byte* buffer = stackalloc byte[128];
        Need(!default(BoundedExitRetryResult).Succeeded &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExitRetryState>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<BoundedExitRetryResult>(), "default and POD contracts");
        Reset(); BoundedExitRetryState state = default;
        var result = Invoke(ref state, buffer);
        Need(result.Succeeded && result.ExitCalls == 1 && result.WindowsStarted == 1 && trace == 0x12, "first success stops");
        Reset(Stale); state = default; result = Invoke(ref state, buffer);
        Need(result.Succeeded && result.ExitCalls == 2 && result.WindowsStarted == 2 && maps == 2 && exits == 2 &&
            trace == 0x1212 && lastKey == ulong.MaxValue && result.Last.Map.MapKey == lastKey, "fresh opaque key and strict call order");
        foreach (ulong status in new ulong[] { 1, 2, 0x8000000000000005, 0x8000000000000007, ulong.MaxValue })
        {
            Reset(status); state = default; result = Invoke(ref state, buffer);
            Need(result.Status == BoundedExitRetryStatus.ExitFailed && result.ExitCalls == 1 &&
                result.Last.Exit.FirmwareStatus == status && trace == 0x12, "only exact error status retries");
        }
    }

    private static void BoundsAndCapture()
    {
        byte* buffer = stackalloc byte[128];
        foreach (uint limit in new uint[] { 0, 3, uint.MaxValue })
        {
            Reset(); BoundedExitRetryState state = default;
            Need(Invoke(ref state, buffer, limit).Status == BoundedExitRetryStatus.InvalidAttemptLimit &&
                maps == 0 && exits == 0 && !state.IsConsumed, "invalid limit leaves state available");
        }
        for (uint limit = 1; limit <= 2; limit++)
        {
            Reset(Stale, Stale); BoundedExitRetryState state = default;
            var result = Invoke(ref state, buffer, limit);
            Need(result.Status == BoundedExitRetryStatus.RetryExhausted && result.ExitCalls == limit &&
                maps == limit && exits == limit && state.IsConsumed, "hard bound consumes state");
        }
        foreach (ulong status in new ulong[] { Stale, 0x8000000000000005, 0x8000000000000007 })
        for (int failure = 1; failure <= 2; failure++)
        {
            Reset(Stale); failCaptureAt = failure; captureStatus = status; BoundedExitRetryState state = default;
            var result = Invoke(ref state, buffer);
            Need(result.Status == BoundedExitRetryStatus.WindowFailed && result.Last.CaptureStatus == status &&
                result.Last.RequiredBytes == 256 && maps == failure && exits == failure - 1 && state.IsConsumed,
                "capture status never retries or grows buffer");
        }
        Reset(Stale); malformedSecondMap = true; BoundedExitRetryState malformed = default;
        var bad = Invoke(ref malformed, buffer);
        Need(bad.Status == BoundedExitRetryStatus.WindowFailed && bad.Last.Status == FinalMapExitWindowStatus.InvalidMapMetadata &&
            maps == 2 && exits == 1 && trace == 0x121, "invalid refreshed metadata suppresses exit");
    }

    private static void State()
    {
        byte* buffer = stackalloc byte[128];
        Reset(Stale); reenter = true; BoundedExitRetryState state = default;
        Need(Invoke(ref state, buffer).Succeeded && reentries == 4 && trace == 0x1212, "both windows reject callback reentry");
        Need(Invoke(ref state, buffer).Status == BoundedExitRetryStatus.AlreadyConsumed &&
            maps == 2 && exits == 2, "reuse cannot capture or exit again");
        Reset(); state = default;
        Need(Retry.Execute(ref state, FirmwareServicesMode.BootServicesAvailable,
            &GetMap, &Exit, (void*)1, (byte*)&state, 128).Status == BoundedExitRetryStatus.InvalidOwnedBuffer &&
            maps == 0 && !state.IsConsumed, "whole state buffer overlap rejected");
        // A warmed loop measures only the coordinator and controlled callbacks.
        Reset(Stale); state = default; Invoke(ref state, buffer);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) { Reset(Stale); state = default; Invoke(ref state, buffer); }
        Need(GC.GetAllocatedBytesForCurrentThread() == before, "zero managed allocation across retries");
    }
}
