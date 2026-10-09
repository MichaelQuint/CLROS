using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Platform;
using Window = OS.Boot.NoAllocationOrUnrelatedFirmwareCallOccursBetween;

internal static unsafe class NoAllocationOrUnrelatedFirmwareCallOccursBetweenTests
{
    private static ulong mapSize, descriptorSize, mapKey, captureStatus, exitStatus;
    private static uint descriptorVersion;
    private static int mapCalls, exitCalls, trace, reentryRefusals;
    private static ulong seenKey, seenHandle, seenCapacity;
    private static FinalMapExitWindowState* active;
    private static bool tryReentry;

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START m03.005");
        Run("reject admission before either callback", Admission);
        Run("fresh full-width key reaches the sole adjacent exit call", Success);
        Run("capture failures consume the window without attempting exit", CaptureFailures);
        Run("invalid final metadata never reaches exit", Metadata);
        Run("both callbacks reject recursive windows", Reentry);
        Run("warmed windows allocate zero managed bytes", Allocation);
        Console.WriteLine("CLROS_M03_005_READY hostOnly=true controlledCallbacks=true realExitBootServices=false");
        Console.WriteLine("TEST_PASS m03.005");
    }

    private static void Run(string name, Action test)
    {

        HostCheck.Run("m03.005 " + name, test);
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static void Reset()
    {
        mapSize = descriptorSize = 40; descriptorVersion = 1;
        mapKey = 0xF123456789ABCDEF; captureStatus = exitStatus = 0;
        mapCalls = exitCalls = trace = reentryRefusals = 0;
        seenKey = seenHandle = seenCapacity = 0; active = null; tryReentry = false;
    }

    private static FinalMapExitWindowResult Invoke(ref FinalMapExitWindowState state, byte* buffer, ulong capacity = 256)
    {
        fixed (FinalMapExitWindowState* p = &state)
        {
            active = p;
            return Window.Execute(ref state, FirmwareServicesMode.BootServicesAvailable,
                &GetMap, &Exit, (void*)0x123456789AB, buffer, capacity);
        }
    }

    [UnmanagedCallersOnly]
    private static ulong GetMap(ulong* size, void* buffer, ulong* key, ulong* descriptor, uint* version)
    {
        mapCalls++; trace = (trace << 4) | 1; seenCapacity = *size;
        *size = mapSize; *key = mapKey; *descriptor = descriptorSize; *version = descriptorVersion;
        if (captureStatus == 0)
            for (int i = 0; i < 40; i++) ((byte*)buffer)[i] = (byte)(0x80 + i);
        CheckReentry();
        return captureStatus;
    }

    [UnmanagedCallersOnly]
    private static ulong Exit(void* handle, ulong key)
    {
        exitCalls++; trace = (trace << 4) | 2; seenHandle = (ulong)handle; seenKey = key;
        CheckReentry();
        return exitStatus;
    }

    private static void CheckReentry()
    {
        if (tryReentry && Window.Execute(ref *active, FirmwareServicesMode.BootServicesAvailable,
            null, null, null, null, 0).Status == FinalMapExitWindowStatus.ReentryRejected)
            reentryRefusals++;
    }

    private static void Admission()
    {
        Reset(); byte* buffer = stackalloc byte[256];
        Need(!default(FinalMapExitWindowResult).Succeeded, "default result");
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<FinalMapExitWindowState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<FinalMapExitWindowResult>(), "POD state/result");
        for (int scenario = 0; scenario < 8; scenario++)
        {
            FinalMapExitWindowState state = default;
            var result = Window.Execute(ref state,
                scenario == 4 ? FirmwareServicesMode.BootServicesExited :
                scenario == 5 ? FirmwareServicesMode.UefiGoneSimulated : FirmwareServicesMode.BootServicesAvailable,
                scenario == 0 ? null : &GetMap, scenario == 1 ? null : &Exit,
                scenario == 2 ? null : (void*)1,
                scenario == 3 ? null : scenario == 6 ? (byte*)(ulong.MaxValue - 15) : scenario == 7 ? (byte*)&state : buffer,
                256);
            Need(result.Status == (scenario == 0 ? FinalMapExitWindowStatus.MissingGetMemoryMap :
                scenario is 3 or 6 or 7 ? FinalMapExitWindowStatus.InvalidOwnedBuffer : FinalMapExitWindowStatus.ExitAdmissionRejected), "admission status");
            Need(!state.IsConsumed && mapCalls == 0 && exitCalls == 0, "rejected admission issued a call");
            Need(Invoke(ref state, buffer).Succeeded, "rejected admission changed state");
            Reset();
        }
    }

    private static void Success()
    {
        byte* storage = stackalloc byte[258]; storage[0] = 0x51; storage[257] = 0x62;
        foreach (ulong key in new ulong[] { 0, 0xF123456789ABCDEF, ulong.MaxValue })
        foreach (ulong returned in new ulong[] { 0, 0x8000000000000002, 0xFEDCBA9876543210 })
        {
            Reset(); mapKey = key; exitStatus = returned; FinalMapExitWindowState state = default;
            var result = Invoke(ref state, storage + 1);
            Need(result.Status == FinalMapExitWindowStatus.Returned && state.IsConsumed, "complete window");
            Need(result.Succeeded == (returned == 0) && result.Exit.FirmwareStatus == returned, "exact firmware status");
            Need(result.Exit.MapKeyRejected == (returned == 0x8000000000000002), "map rejection");
            Need(trace == 0x12 && mapCalls == 1 && exitCalls == 1 && seenKey == key
                && seenHandle == 0x123456789AB && seenCapacity == 256, "adjacent exact callbacks");
            Need(result.Map.MapKey == key && result.Map.BufferAddress == (ulong)(storage + 1)
                && result.Map.MapSize == 40 && result.Map.DescriptorCount == 1, "retained final map");
            Need(storage[0] == 0x51 && storage[257] == 0x62 && storage[1] == 0x80 && storage[40] == 0xA7, "buffer guards/content");
            Need(Invoke(ref state, storage + 1).Status == FinalMapExitWindowStatus.AlreadyConsumed
                && mapCalls == 1 && exitCalls == 1, "reuse invoked firmware");
        }
    }

    private static void CaptureFailures()
    {
        byte* buffer = stackalloc byte[256];
        foreach (ulong status in new ulong[] { 0x8000000000000005, 0x8000000000000007 })
        {
            Reset(); captureStatus = status; mapSize = 320; FinalMapExitWindowState state = default;
            var result = Invoke(ref state, buffer);
            Need(result.Status == FinalMapExitWindowStatus.CaptureFailed && result.CaptureStatus == status
                && result.RequiredBytes == 320 && !result.Map.HasMap() && !result.Exit.CallIssued, "capture failure result");
            Need(mapCalls == 1 && exitCalls == 0 && state.IsConsumed, "capture failure issued exit");
            Need(Invoke(ref state, buffer).Status == FinalMapExitWindowStatus.AlreadyConsumed && mapCalls == 1, "implicit retry");
        }
    }

    private static void Metadata()
    {
        byte* buffer = stackalloc byte[256];
        for (int scenario = 0; scenario < 6; scenario++)
        {
            Reset(); FinalMapExitWindowState state = default;
            switch (scenario)
            {
                case 0: mapSize = 0; break;
                case 1: mapSize = 280; break;
                case 2: descriptorSize = 0; break;
                case 3: descriptorSize = 32; break;
                case 4: descriptorVersion = 0; break;
                case 5: mapSize = 41; break;
            }
            var result = Invoke(ref state, buffer);
            Need(result.Status == FinalMapExitWindowStatus.InvalidMapMetadata && !result.Map.HasMap()
                && !result.Exit.CallIssued && state.IsConsumed && mapCalls == 1 && exitCalls == 0, "invalid metadata admitted");
        }
    }

    private static void Reentry()
    {
        Reset(); tryReentry = true; FinalMapExitWindowState state = default; byte* buffer = stackalloc byte[256];
        Need(Invoke(ref state, buffer).Succeeded && reentryRefusals == 2 && trace == 0x12
            && mapCalls == 1 && exitCalls == 1, "callback reentry changed outer window");
    }

    private static void Allocation()
    {
        Reset(); byte* buffer = stackalloc byte[256];
        for (int i = 0; i < 32; i++) { FinalMapExitWindowState warm = default; Invoke(ref warm, buffer); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool passed = true;
        for (int i = 0; i < 100; i++) { FinalMapExitWindowState state = default; passed &= Invoke(ref state, buffer).Succeeded; }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Need(passed && allocated == 0, "managed allocation in warmed window");
    }
}
