using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Pal;

internal static class BootSequenceAppLifecycleReadinessTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("boot.sequence app lifecycle ready", TestAppLifecycleReady, ref failures);
        Run(
            "boot.sequence app lifecycle rejects dependency failure",
            TestAppLifecycleRejectsDependencyFailure,
            ref failures);
        Run(
            "boot.sequence app lifecycle preserves default markers",
            TestAppLifecyclePreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Boot sequence app lifecycle readiness host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAppLifecycleReady()
    {
        NativeAppLifecyclePlanResult lifecyclePlan = CreateReadyLifecyclePlan();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppLifecycleReadiness(lifecyclePlan, serial, events);

        return lifecyclePlan.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppLifecycleReady
            && events.Snapshot()[0] == BootSequence.AppLifecycleReady;
    }

    private static bool TestAppLifecycleRejectsDependencyFailure()
    {
        NativeAppLifecyclePlanResult lifecyclePlan = NativeAppLifecyclePlanResult.Fail(
            NativeAppLifecyclePlanStatus.LogSinkNotReady,
            NativeAppInvocationPlanStatus.Success,
            OSHostStatus.NotFound);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppLifecycleNotReadyMarker(
            NativeAppLifecyclePlanStatus.LogSinkNotReady,
            NativeAppInvocationPlanStatus.Success,
            OSHostStatus.NotFound);

        BootResult result = BootSequence.RunAppLifecycleReadiness(lifecyclePlan, serial, events);

        return !lifecyclePlan.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppLifecyclePlanStatus.LogSinkNotReady),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppInvocationPlanStatus.Success),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(OSHostStatus.NotFound),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestAppLifecyclePreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.AppLifecycleReady)
            && !serial.ContainsPrefix(BootSequence.AppLifecycleNotReadyPrefix);
    }

    private static NativeAppLifecyclePlanResult CreateReadyLifecyclePlan()
    {
        NativeAppInvocationPlan plan = new NativeAppInvocationPlan(
            requiredStackBytes: NativeAppInvocationPlanner.DefaultStackBytes,
            requiredWritableStagingBytes: 4096,
            executableMapOffsetInPackage: 4096,
            executableMapByteLength: 4096,
            executableMapEndOffsetInPackage: 8192,
            executableContentOffsetInMapping: 32,
            executableBytes: 64,
            entryOffsetInExecutableContent: 0,
            entryOffsetInExecutableMapping: 32,
            entryOffsetInPackage: 4128,
            manifestMetadataByteCount: 0,
            manifestVersion: NativeManifest.Version0,
            appKind: NativeManifestAppKind.Package,
            runtimeKind: NativeManifestRuntimeKind.NativeAot,
            networkMode: NativeManifestNetworkMode.Dhcp,
            logMode: NativeManifestLogMode.Serial,
            argumentByteLength: 0,
            environmentCount: 0,
            requiredBootCapabilities: BootCapabilities.TextOutput,
            requiredRuntimeCapabilities: NativeAppLaunchRuntimeCapabilities.PackageStorage);

        NativeAppInvocationPlanResult invocationPlan = NativeAppInvocationPlanResult.Success(
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            NativeAppLaunchStatus.Success,
            plan);

        return NativeAppLifecyclePlanner.Create(
            invocationPlan,
            new OSHostAppLogTable(recordCapacity: 4, maxRecordBytes: 128));
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool Contains(string value)
        {
            return _lines.Contains(value);
        }

        public bool ContainsPrefix(string value)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
