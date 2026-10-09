using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;

internal static class BootSequenceAppInvocationTests
{

    [HostTest("boot.sequence app invocation ready", HostLane.Fast, 0)]
    private static bool TestAppInvocationReady()
    {
        NativeAppInvocationPlanResult invocationPlan = CreateReadyInvocationPlan();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppInvocationReadiness(invocationPlan, serial, events);

        return HostCheck.Result(invocationPlan.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppInvocationReady
            && events.Snapshot()[0] == BootSequence.AppInvocationReady);
    }

    [HostTest("boot.sequence app invocation rejects plan failure", HostLane.Fast, 1)]
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

        return HostCheck.Result(!invocationPlan.Succeeded
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
            && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence app invocation preserves default markers", HostLane.Fast, 2)]
    private static bool TestAppInvocationPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.AppInvocationReady)
            && !serial.ContainsPrefix(BootSequence.AppInvocationNotReadyPrefix));
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
