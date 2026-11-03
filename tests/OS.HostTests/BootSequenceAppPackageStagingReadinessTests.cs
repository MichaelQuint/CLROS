using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Storage;

internal static partial class BootSequenceAppPackageStagingReadinessTests
{
    [HostTest("boot.sequence app package staging ready", HostLane.Fast, 0)]
    private static bool TestAppPackageStagingReady()
    {
        NativeAppPackageStagingReadinessResult readiness = CreateReadyReadiness();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult result = BootSequence.RunAppPackageStagingReadiness(readiness, serial, events);
        return HostCheck.Result(readiness.Succeeded && result.Succeeded && serial.Count == 1 && serial.LineAt(0) == BootSequence.AppPackageStagingReady && events.Snapshot()[0] == BootSequence.AppPackageStagingReady);
    }

    [HostTest("boot.sequence app package staging rejects readiness failure", HostLane.Fast, 1)]
    private static bool TestAppPackageStagingRejectsReadinessFailure()
    {
        NativeAppPackageStagingReadinessResult readiness = NativeAppPackageStagingReadinessResult.Fail(NativeAppPackageStagingReadinessStatus.PayloadStagingFailed, NativeManifestLoadStatus.Success, OsPackageStatus.Success, NativeAppLaunchStatus.Success, NativeAppLaunchDescriptorStatus.Success, OsPackageFileStagingStatus.Success, NativeAppPayloadMappingPlanStatus.Success, NativeAppPayloadStagingStatus.DestinationTooSmall, NativeAppExecutableMemoryPlanStatus.Success, NativeAppInvocationPlanStatus.Success, appBinaryFileIndex: 2, packageFileBytesStaged: 512, payloadOffsetInPackage: 4096, executableOffsetInPackage: 4128, payloadExecutableBytesStaged: 0, requiredWritableStagingBytes: 4096, requiredStackBytes: NativeAppInvocationPlanner.DefaultStackBytes, entryOffsetInPackage: 4136);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppPackageStagingNotReadyMarker(AppPackageStagingReadinessStatus.StagingReadinessFailed, readiness);
        BootResult result = BootSequence.RunAppPackageStagingReadiness(readiness, serial, events);
        return HostCheck.Result(!readiness.Succeeded && !result.Succeeded && result.Reason.Contains(nameof(AppPackageStagingReadinessStatus.StagingReadinessFailed), StringComparison.Ordinal) && result.Reason.Contains(nameof(NativeAppPackageStagingReadinessStatus.PayloadStagingFailed), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0) == expectedMarker && events.Snapshot()[0] == expectedMarker);
    }

    [HostTest("boot.sequence app package staging blocks default result", HostLane.Fast, 2)]
    private static bool TestAppPackageStagingBlocksDefaultResult()
    {
        NativeAppPackageStagingReadinessResult readiness = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        string expectedMarker = BootSequence.CreateAppPackageStagingNotReadyMarker(AppPackageStagingReadinessStatus.StagingReadinessFailed, readiness);
        BootResult result = BootSequence.RunAppPackageStagingReadiness(readiness, serial, events);
        return HostCheck.Result(!readiness.IsInitialized && !readiness.Succeeded && !result.Succeeded && serial.Count == 1 && serial.LineAt(0) == expectedMarker && expectedMarker.Contains("initialized=false", StringComparison.Ordinal) && expectedMarker.Contains("readiness=Success", StringComparison.Ordinal));
    }

    [HostTest("boot.sequence app package staging preserves default markers", HostLane.Fast, 3)]
    private static bool TestAppPackageStagingPreservesDefaultMarkers()
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

        return HostCheck.Result(!serial.Contains(BootSequence.AppPackageStagingReady) && !serial.ContainsPrefix(BootSequence.AppPackageStagingNotReadyPrefix) && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppPackageStagingReadinessResult>());
    }

    private static NativeAppPackageStagingReadinessResult CreateReadyReadiness()
    {
        return NativeAppPackageStagingReadinessResult.Success(NativeManifestLoadStatus.Success, OsPackageStatus.Success, NativeAppLaunchStatus.Success, NativeAppLaunchDescriptorStatus.Success, OsPackageFileStagingStatus.Success, NativeAppPayloadMappingPlanStatus.Success, NativeAppPayloadStagingStatus.Success, NativeAppExecutableMemoryPlanStatus.Success, NativeAppInvocationPlanStatus.Success, appBinaryFileIndex: 2, packageFileBytesStaged: 512, payloadOffsetInPackage: 4096, executableOffsetInPackage: 4128, payloadExecutableBytesStaged: 480, requiredWritableStagingBytes: 4096, requiredStackBytes: NativeAppInvocationPlanner.DefaultStackBytes, entryOffsetInPackage: 4136);
    }
}
