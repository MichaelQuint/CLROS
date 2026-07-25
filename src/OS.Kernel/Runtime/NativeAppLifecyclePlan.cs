using OS.Boot;
using OS.Manifest;
using OS.Pal;

namespace OS.Kernel.Runtime;

public enum NativeAppLifecyclePlanStatus
{
    Success = 0,
    InvocationPlanFailed = 1,
    InvalidAppIdentity = 2,
    LogSinkNotReady = 3
}

public readonly struct NativeAppLogSinkReadiness
{
    public NativeAppLogSinkReadiness(
        OSHostStatus status,
        int remainingRecordCapacity,
        int maxRecordBytes)
    {
        Status = status;
        RemainingRecordCapacity = remainingRecordCapacity;
        MaxRecordBytes = maxRecordBytes;
        IsInitialized = true;
    }

    public OSHostStatus Status { get; }

    public bool IsInitialized { get; }

    public int RemainingRecordCapacity { get; }

    public int MaxRecordBytes { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == OSHostStatus.Ok &&
        RemainingRecordCapacity > 0 &&
        MaxRecordBytes >= NativeAppExitStatusFormatter.MaximumMarkerBytes;

    public static NativeAppLogSinkReadiness FromTable(OSHostAppLogTable? table)
    {
        if (table is null)
        {
            return new NativeAppLogSinkReadiness(
                OSHostStatus.NotFound,
                remainingRecordCapacity: 0,
                maxRecordBytes: 0);
        }

        int remainingCapacity = table.Capacity - table.Count;
        if (table.Capacity <= 0 || remainingCapacity <= 0)
        {
            return new NativeAppLogSinkReadiness(
                OSHostStatus.OutOfMemory,
                remainingRecordCapacity: remainingCapacity < 0 ? 0 : remainingCapacity,
                table.MaxRecordBytes);
        }

        if (table.MaxRecordBytes < NativeAppExitStatusFormatter.MaximumMarkerBytes)
        {
            return new NativeAppLogSinkReadiness(
                OSHostStatus.BufferTooSmall,
                remainingCapacity,
                table.MaxRecordBytes);
        }

        return new NativeAppLogSinkReadiness(
            OSHostStatus.Ok,
            remainingCapacity,
            table.MaxRecordBytes);
    }
}

public readonly struct NativeAppLifecyclePlan
{
    public NativeAppLifecyclePlan(
        byte manifestVersion,
        NativeManifestAppKind appKind,
        NativeManifestRuntimeKind runtimeKind,
        NativeManifestNetworkMode networkMode,
        NativeManifestLogMode logMode,
        int argumentByteLength,
        int environmentCount,
        ulong requiredStackBytes,
        ulong requiredWritableStagingBytes,
        BootCapabilities requiredBootCapabilities,
        NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities,
        int exitMarkerMaximumBytes,
        int logRecordMaximumBytes,
        int logRemainingRecordCapacity)
    {
        ManifestVersion = manifestVersion;
        AppKind = appKind;
        RuntimeKind = runtimeKind;
        NetworkMode = networkMode;
        LogMode = logMode;
        ArgumentByteLength = argumentByteLength;
        EnvironmentCount = environmentCount;
        RequiredStackBytes = requiredStackBytes;
        RequiredWritableStagingBytes = requiredWritableStagingBytes;
        RequiredBootCapabilities = requiredBootCapabilities;
        RequiredRuntimeCapabilities = requiredRuntimeCapabilities;
        ExitMarkerMaximumBytes = exitMarkerMaximumBytes;
        LogRecordMaximumBytes = logRecordMaximumBytes;
        LogRemainingRecordCapacity = logRemainingRecordCapacity;
    }

    public byte ManifestVersion { get; }

    public NativeManifestAppKind AppKind { get; }

    public NativeManifestRuntimeKind RuntimeKind { get; }

    public NativeManifestNetworkMode NetworkMode { get; }

    public NativeManifestLogMode LogMode { get; }

    public int ArgumentByteLength { get; }

    public int EnvironmentCount { get; }

    public ulong RequiredStackBytes { get; }

    public ulong RequiredWritableStagingBytes { get; }

    public BootCapabilities RequiredBootCapabilities { get; }

    public NativeAppLaunchRuntimeCapabilities RequiredRuntimeCapabilities { get; }

    public int ExitMarkerMaximumBytes { get; }

    public int LogRecordMaximumBytes { get; }

    public int LogRemainingRecordCapacity { get; }
}

public readonly struct NativeAppLifecyclePlanResult
{
    private NativeAppLifecyclePlanResult(
        NativeAppLifecyclePlanStatus status,
        NativeAppInvocationPlanStatus invocationStatus,
        OSHostStatus logStatus,
        bool isInitialized,
        NativeAppLifecyclePlan plan)
    {
        Status = status;
        InvocationStatus = invocationStatus;
        LogStatus = logStatus;
        IsInitialized = isInitialized;
        Plan = plan;
    }

    public NativeAppLifecyclePlanStatus Status { get; }

    public NativeAppInvocationPlanStatus InvocationStatus { get; }

    public OSHostStatus LogStatus { get; }

    public bool IsInitialized { get; }

    public NativeAppLifecyclePlan Plan { get; }

    public bool Succeeded => IsInitialized && Status == NativeAppLifecyclePlanStatus.Success;

    public static NativeAppLifecyclePlanResult Success(
        NativeAppInvocationPlanStatus invocationStatus,
        OSHostStatus logStatus,
        NativeAppLifecyclePlan plan)
    {
        return new NativeAppLifecyclePlanResult(
            NativeAppLifecyclePlanStatus.Success,
            invocationStatus,
            logStatus,
            isInitialized: true,
            plan);
    }

    public static NativeAppLifecyclePlanResult Fail(
        NativeAppLifecyclePlanStatus status,
        NativeAppInvocationPlanStatus invocationStatus,
        OSHostStatus logStatus)
    {
        return new NativeAppLifecyclePlanResult(
            status,
            invocationStatus,
            logStatus,
            isInitialized: true,
            default);
    }
}

public static class NativeAppLifecyclePlanner
{
    public static NativeAppLifecyclePlanResult Create(
        NativeAppInvocationPlanResult invocationPlan,
        OSHostAppLogTable? appLogTable)
    {
        return Create(invocationPlan, NativeAppLogSinkReadiness.FromTable(appLogTable));
    }

    public static NativeAppLifecyclePlanResult Create(
        NativeAppInvocationPlanResult invocationPlan,
        NativeAppLogSinkReadiness logSink)
    {
        if (!invocationPlan.Succeeded)
        {
            return NativeAppLifecyclePlanResult.Fail(
                NativeAppLifecyclePlanStatus.InvocationPlanFailed,
                invocationPlan.Status,
                logSink.Status);
        }

        NativeAppInvocationPlan plan = invocationPlan.Plan;
        if (plan.AppKind != NativeManifestAppKind.Package ||
            plan.RuntimeKind != NativeManifestRuntimeKind.NativeAot)
        {
            return NativeAppLifecyclePlanResult.Fail(
                NativeAppLifecyclePlanStatus.InvalidAppIdentity,
                invocationPlan.Status,
                logSink.Status);
        }

        if (!logSink.Succeeded)
        {
            return NativeAppLifecyclePlanResult.Fail(
                NativeAppLifecyclePlanStatus.LogSinkNotReady,
                invocationPlan.Status,
                logSink.Status);
        }

        NativeAppLifecyclePlan lifecyclePlan = new NativeAppLifecyclePlan(
            plan.ManifestVersion,
            plan.AppKind,
            plan.RuntimeKind,
            plan.NetworkMode,
            plan.LogMode,
            plan.ArgumentByteLength,
            plan.EnvironmentCount,
            plan.RequiredStackBytes,
            plan.RequiredWritableStagingBytes,
            plan.RequiredBootCapabilities,
            plan.RequiredRuntimeCapabilities,
            NativeAppExitStatusFormatter.MaximumMarkerBytes,
            logSink.MaxRecordBytes,
            logSink.RemainingRecordCapacity);

        return NativeAppLifecyclePlanResult.Success(
            invocationPlan.Status,
            logSink.Status,
            lifecyclePlan);
    }
}
