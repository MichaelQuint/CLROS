using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Pal;

internal static partial class BootSequenceAppLifecycleReadinessTests
{
    [HostTest("boot.sequence app lifecycle ready", HostLane.Fast, 0)]
    private static bool TestAppLifecycleReady()
    {
        NativeAppLifecyclePlanResult lifecyclePlan = CreateReadyLifecyclePlan();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppLifecycleReadiness(lifecyclePlan, serial, events);
        return HostCheck.Result(lifecyclePlan.Succeeded && result.Succeeded && serial.Count == 1 && serial.LineAt(0) == BootSequence.AppLifecycleReady && events.Snapshot()[0] == BootSequence.AppLifecycleReady);
    }

    [HostTest("boot.sequence app lifecycle rejects dependency failure", HostLane.Fast, 1)]
    private static bool TestAppLifecycleRejectsDependencyFailure()
    {
        NativeAppLifecyclePlanResult lifecyclePlan = NativeAppLifecyclePlanResult.Fail(NativeAppLifecyclePlanStatus.LogSinkNotReady, NativeAppInvocationPlanStatus.Success, OSHostStatus.NotFound);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppLifecycleNotReadyMarker(NativeAppLifecyclePlanStatus.LogSinkNotReady, NativeAppInvocationPlanStatus.Success, OSHostStatus.NotFound);
        BootResult result = BootSequence.RunAppLifecycleReadiness(lifecyclePlan, serial, events);
        return HostCheck.Result(!lifecyclePlan.Succeeded && !result.Succeeded && result.Reason.Contains(nameof(NativeAppLifecyclePlanStatus.LogSinkNotReady), StringComparison.Ordinal) && result.Reason.Contains(nameof(NativeAppInvocationPlanStatus.Success), StringComparison.Ordinal) && result.Reason.Contains(nameof(OSHostStatus.NotFound), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence app lifecycle preserves default markers", HostLane.Fast, 2)]
    private static bool TestAppLifecyclePreservesDefaultMarkers()
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

        return HostCheck.Result(!serial.Contains(BootSequence.AppLifecycleReady) && !serial.ContainsPrefix(BootSequence.AppLifecycleNotReadyPrefix));
    }

    private static NativeAppLifecyclePlanResult CreateReadyLifecyclePlan()
    {
        NativeAppInvocationPlan plan = new NativeAppInvocationPlan(requiredStackBytes: NativeAppInvocationPlanner.DefaultStackBytes, requiredWritableStagingBytes: 4096, executableMapOffsetInPackage: 4096, executableMapByteLength: 4096, executableMapEndOffsetInPackage: 8192, executableContentOffsetInMapping: 32, executableBytes: 64, entryOffsetInExecutableContent: 0, entryOffsetInExecutableMapping: 32, entryOffsetInPackage: 4128, manifestMetadataByteCount: 0, manifestVersion: NativeManifest.Version0, appKind: NativeManifestAppKind.Package, runtimeKind: NativeManifestRuntimeKind.NativeAot, networkMode: NativeManifestNetworkMode.Dhcp, logMode: NativeManifestLogMode.Serial, argumentByteLength: 0, environmentCount: 0, requiredBootCapabilities: BootCapabilities.TextOutput, requiredRuntimeCapabilities: NativeAppLaunchRuntimeCapabilities.PackageStorage);
        NativeAppInvocationPlanResult invocationPlan = NativeAppInvocationPlanResult.Success(NativeAppExecutableMemoryPlanStatus.Success, NativeAppPayloadMappingPlanStatus.Success, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success, NativeAppLaunchStatus.Success, plan);
        return NativeAppLifecyclePlanner.Create(invocationPlan, new OSHostAppLogTable(recordCapacity: 4, maxRecordBytes: 128));
    }
}
