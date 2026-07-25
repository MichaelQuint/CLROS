using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;

internal static class BootSequenceAppInvocationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("boot.sequence app invocation ready", TestAppInvocationReady, ref failures);
        Run(
            "boot.sequence app invocation rejects plan failure",
            TestAppInvocationRejectsPlanFailure,
            ref failures);
        Run(
            "boot.sequence app invocation preserves default markers",
            TestAppInvocationPreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Boot sequence app invocation host tests failed: " + failures);
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

    private static bool TestAppInvocationReady()
    {
        NativeAppInvocationPlanResult invocationPlan = CreateReadyInvocationPlan();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppInvocationReadiness(invocationPlan, serial, events);

        return invocationPlan.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppInvocationReady
            && events.Snapshot()[0] == BootSequence.AppInvocationReady;
    }

    private static bool TestAppInvocationRejectsPlanFailure()
    {
        NativeAppInvocationPlanResult invocationPlan = NativeAppInvocationPlanResult.Fail(
            NativeAppInvocationPlanStatus.ExecutableMemoryPlanFailed,
            NativeAppExecutableMemoryPlanStatus.MappingPlanFailed,
            NativeAppPayloadMappingPlanStatus.DescriptorFailed,
            NativeAppLaunchDescriptorStatus.InvalidPayloadHeader,
            NativeAppPayloadHeaderStatus.InvalidMagic,
            NativeAppLaunchStatus.Success);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppInvocationNotReadyMarker(
            NativeAppInvocationPlanStatus.ExecutableMemoryPlanFailed,
            NativeAppExecutableMemoryPlanStatus.MappingPlanFailed,
            NativeAppLaunchStatus.Success);

        BootResult result = BootSequence.RunAppInvocationReadiness(invocationPlan, serial, events);

        return !invocationPlan.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(NativeAppInvocationPlanStatus.ExecutableMemoryPlanFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppExecutableMemoryPlanStatus.MappingPlanFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppLaunchStatus.Success),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker;
    }

    private static bool TestAppInvocationPreservesDefaultMarkers()
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

        return !serial.Contains(BootSequence.AppInvocationReady)
            && !serial.ContainsPrefix(BootSequence.AppInvocationNotReadyPrefix);
    }

    private static NativeAppInvocationPlanResult CreateReadyInvocationPlan()
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

        return NativeAppInvocationPlanResult.Success(
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            NativeAppLaunchStatus.Success,
            plan);
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
