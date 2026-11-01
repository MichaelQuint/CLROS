using OS.Boot;
using OS.Manifest;
using OS.Pal;

namespace OS.Kernel.Runtime;
public static class NativeAppLifecyclePlanner
{
    public static NativeAppLifecyclePlanResult Create(NativeAppInvocationPlanResult invocationPlan, OSHostAppLogTable? appLogTable)
    {
        return Create(invocationPlan, NativeAppLogSinkReadiness.FromTable(appLogTable));
    }

    public static NativeAppLifecyclePlanResult Create(NativeAppInvocationPlanResult invocationPlan, NativeAppLogSinkReadiness logSink)
    {
        if (!invocationPlan.Succeeded)
        {
            return NativeAppLifecyclePlanResult.Fail(NativeAppLifecyclePlanStatus.InvocationPlanFailed, invocationPlan.Status, logSink.Status);
        }

        NativeAppInvocationPlan plan = invocationPlan.Plan;
        if (plan.AppKind != NativeManifestAppKind.Package || plan.RuntimeKind != NativeManifestRuntimeKind.NativeAot)
        {
            return NativeAppLifecyclePlanResult.Fail(NativeAppLifecyclePlanStatus.InvalidAppIdentity, invocationPlan.Status, logSink.Status);
        }

        if (!logSink.Succeeded)
        {
            return NativeAppLifecyclePlanResult.Fail(NativeAppLifecyclePlanStatus.LogSinkNotReady, invocationPlan.Status, logSink.Status);
        }

        NativeAppLifecyclePlan lifecyclePlan = new NativeAppLifecyclePlan(plan.ManifestVersion, plan.AppKind, plan.RuntimeKind, plan.NetworkMode, plan.LogMode, plan.ArgumentByteLength, plan.EnvironmentCount, plan.RequiredStackBytes, plan.RequiredWritableStagingBytes, plan.RequiredBootCapabilities, plan.RequiredRuntimeCapabilities, NativeAppExitStatusFormatter.MaximumMarkerBytes, logSink.MaxRecordBytes, logSink.RemainingRecordCapacity);
        return NativeAppLifecyclePlanResult.Success(invocationPlan.Status, logSink.Status, lifecyclePlan);
    }
}
