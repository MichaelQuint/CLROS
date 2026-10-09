using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Runtime;
using OS.Manifest;
using OS.Pal;

internal static class NativeAppLifecyclePlanTests
{

    [HostTest("appmodel.lifecycle accepts invocation and log sink", HostLane.Fast, 0)]
    private static bool TestAcceptsInvocationAndLogSink()
    {
        NativeAppInvocationPlanResult invocationPlan = CreateReadyInvocationPlan();
        OSHostAppLogTable logTable = new(recordCapacity: 4, maxRecordBytes: 128);

        NativeAppLifecyclePlanResult lifecyclePlan =
            NativeAppLifecyclePlanner.Create(invocationPlan, logTable);

        return HostCheck.Result(lifecyclePlan.Succeeded
            && lifecyclePlan.IsInitialized
            && lifecyclePlan.Status == NativeAppLifecyclePlanStatus.Success
            && lifecyclePlan.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && lifecyclePlan.LogStatus == OSHostStatus.Ok
            && lifecyclePlan.Plan.ExitMarkerMaximumBytes == NativeAppExitStatusFormatter.MaximumMarkerBytes
            && lifecyclePlan.Plan.LogRecordMaximumBytes == 128
            && lifecyclePlan.Plan.LogRemainingRecordCapacity == 4);
    }

    [HostTest("appmodel.lifecycle rejects invocation failure", HostLane.Fast, 1)]
    private static bool TestRejectsInvocationFailure()
    {
        NativeAppInvocationPlanResult invocationPlan = NativeAppInvocationPlanResult.Fail(
            NativeAppInvocationPlanStatus.InvalidStackSize,
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            NativeAppLaunchStatus.Success);
        OSHostAppLogTable logTable = new(recordCapacity: 4, maxRecordBytes: 128);

        NativeAppLifecyclePlanResult lifecyclePlan =
            NativeAppLifecyclePlanner.Create(invocationPlan, logTable);

        return HostCheck.Result(!lifecyclePlan.Succeeded
            && lifecyclePlan.IsInitialized
            && lifecyclePlan.Status == NativeAppLifecyclePlanStatus.InvocationPlanFailed
            && lifecyclePlan.InvocationStatus == NativeAppInvocationPlanStatus.InvalidStackSize
            && lifecyclePlan.LogStatus == OSHostStatus.Ok);
    }

    [HostTest("appmodel.lifecycle rejects missing log sink", HostLane.Fast, 2)]
    private static bool TestRejectsMissingLogSink()
    {
        NativeAppInvocationPlanResult invocationPlan = CreateReadyInvocationPlan();

        NativeAppLifecyclePlanResult lifecyclePlan =
            NativeAppLifecyclePlanner.Create(invocationPlan, (OSHostAppLogTable?)null);

        return HostCheck.Result(!lifecyclePlan.Succeeded
            && lifecyclePlan.IsInitialized
            && lifecyclePlan.Status == NativeAppLifecyclePlanStatus.LogSinkNotReady
            && lifecyclePlan.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && lifecyclePlan.LogStatus == OSHostStatus.NotFound);
    }

    [HostTest("appmodel.lifecycle log sink default readiness is not success", HostLane.Fast, 3)]
    private static bool TestLogSinkDefaultReadinessIsNotSuccess()
    {
        NativeAppInvocationPlanResult invocationPlan = CreateReadyInvocationPlan();
        NativeAppLogSinkReadiness logSink = default;

        NativeAppLifecyclePlanResult lifecyclePlan =
            NativeAppLifecyclePlanner.Create(invocationPlan, logSink);

        return HostCheck.Result(!logSink.IsInitialized
            && !logSink.Succeeded
            && logSink.Status == OSHostStatus.Ok
            && logSink.RemainingRecordCapacity == 0
            && logSink.MaxRecordBytes == 0
            && !lifecyclePlan.Succeeded
            && lifecyclePlan.IsInitialized
            && lifecyclePlan.Status == NativeAppLifecyclePlanStatus.LogSinkNotReady
            && lifecyclePlan.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && lifecyclePlan.LogStatus == OSHostStatus.Ok
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppLogSinkReadiness>());
    }

    [HostTest("appmodel.lifecycle preserves app identity", HostLane.Fast, 4)]
    private static bool TestPreservesAppIdentity()
    {
        NativeAppInvocationPlanResult invocationPlan = CreateReadyInvocationPlan(
            argumentByteLength: 9,
            environmentCount: 2,
            networkMode: NativeManifestNetworkMode.Dhcp,
            requiredRuntimeCapabilities:
                NativeAppLaunchRuntimeCapabilities.PackageStorage |
                NativeAppLaunchRuntimeCapabilities.Network);
        OSHostAppLogTable logTable = new(recordCapacity: 2, maxRecordBytes: 128);

        NativeAppLifecyclePlanResult lifecyclePlan =
            NativeAppLifecyclePlanner.Create(invocationPlan, logTable);

        return HostCheck.Result(lifecyclePlan.Succeeded
            && lifecyclePlan.IsInitialized
            && lifecyclePlan.Plan.ManifestVersion == NativeManifest.Version0
            && lifecyclePlan.Plan.AppKind == NativeManifestAppKind.Package
            && lifecyclePlan.Plan.RuntimeKind == NativeManifestRuntimeKind.NativeAot
            && lifecyclePlan.Plan.NetworkMode == NativeManifestNetworkMode.Dhcp
            && lifecyclePlan.Plan.LogMode == NativeManifestLogMode.Serial
            && lifecyclePlan.Plan.ArgumentByteLength == 9
            && lifecyclePlan.Plan.EnvironmentCount == 2
            && lifecyclePlan.Plan.RequiredBootCapabilities == BootCapabilities.TextOutput
            && lifecyclePlan.Plan.RequiredRuntimeCapabilities ==
                (NativeAppLaunchRuntimeCapabilities.PackageStorage |
                 NativeAppLaunchRuntimeCapabilities.Network));
    }

    [HostTest("appmodel.lifecycle default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppLifecyclePlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppLifecyclePlanStatus.Success
            && result.InvocationStatus == NativeAppInvocationPlanStatus.Success
            && result.LogStatus == OSHostStatus.Ok
            && result.Plan.ManifestVersion == 0
            && result.Plan.ArgumentByteLength == 0
            && result.Plan.EnvironmentCount == 0
            && result.Plan.RequiredStackBytes == 0
            && result.Plan.RequiredWritableStagingBytes == 0
            && result.Plan.ExitMarkerMaximumBytes == 0
            && result.Plan.LogRecordMaximumBytes == 0
            && result.Plan.LogRemainingRecordCapacity == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppLifecyclePlanResult>());
    }

    private static NativeAppInvocationPlanResult CreateReadyInvocationPlan(
        int argumentByteLength = 0,
        int environmentCount = 0,
        NativeManifestNetworkMode networkMode = NativeManifestNetworkMode.Dhcp,
        NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities =
            NativeAppLaunchRuntimeCapabilities.PackageStorage)
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
            manifestMetadataByteCount: argumentByteLength,
            manifestVersion: NativeManifest.Version0,
            appKind: NativeManifestAppKind.Package,
            runtimeKind: NativeManifestRuntimeKind.NativeAot,
            networkMode: networkMode,
            logMode: NativeManifestLogMode.Serial,
            argumentByteLength: argumentByteLength,
            environmentCount: environmentCount,
            requiredBootCapabilities: BootCapabilities.TextOutput,
            requiredRuntimeCapabilities: requiredRuntimeCapabilities);

        return NativeAppInvocationPlanResult.Success(
            NativeAppExecutableMemoryPlanStatus.Success,
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            NativeAppLaunchStatus.Success,
            plan);
    }
}
