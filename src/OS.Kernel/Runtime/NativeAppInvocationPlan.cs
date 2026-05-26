using OS.Boot;
using OS.Manifest;

namespace OS.Kernel.Runtime;

public enum NativeAppInvocationPlanStatus
{
    Success = 0,
    ExecutableMemoryPlanFailed = 1,
    LaunchPlanFailed = 2,
    InvalidExecutableMemoryMetadata = 3,
    InvalidEntryOffset = 4,
    InvalidStackSize = 5,
    MetadataOutOfBounds = 6,
    RangeOverflow = 7
}

public readonly struct NativeAppInvocationPlan
{
    public NativeAppInvocationPlan(
        ulong requiredStackBytes,
        ulong requiredWritableStagingBytes,
        ulong executableMapOffsetInPackage,
        ulong executableMapByteLength,
        ulong executableMapEndOffsetInPackage,
        ulong executableContentOffsetInMapping,
        ulong executableBytes,
        ulong entryOffsetInExecutableContent,
        ulong entryOffsetInExecutableMapping,
        ulong entryOffsetInPackage,
        int manifestMetadataByteCount,
        byte manifestVersion,
        NativeManifestAppKind appKind,
        NativeManifestRuntimeKind runtimeKind,
        NativeManifestNetworkMode networkMode,
        NativeManifestLogMode logMode,
        int argumentByteLength,
        int environmentCount,
        BootCapabilities requiredBootCapabilities,
        NativeAppLaunchRuntimeCapabilities requiredRuntimeCapabilities)
    {
        RequiredStackBytes = requiredStackBytes;
        RequiredWritableStagingBytes = requiredWritableStagingBytes;
        ExecutableMapOffsetInPackage = executableMapOffsetInPackage;
        ExecutableMapByteLength = executableMapByteLength;
        ExecutableMapEndOffsetInPackage = executableMapEndOffsetInPackage;
        ExecutableContentOffsetInMapping = executableContentOffsetInMapping;
        ExecutableBytes = executableBytes;
        EntryOffsetInExecutableContent = entryOffsetInExecutableContent;
        EntryOffsetInExecutableMapping = entryOffsetInExecutableMapping;
        EntryOffsetInPackage = entryOffsetInPackage;
        ManifestMetadataByteCount = manifestMetadataByteCount;
        ManifestVersion = manifestVersion;
        AppKind = appKind;
        RuntimeKind = runtimeKind;
        NetworkMode = networkMode;
        LogMode = logMode;
        ArgumentByteLength = argumentByteLength;
        EnvironmentCount = environmentCount;
        RequiredBootCapabilities = requiredBootCapabilities;
        RequiredRuntimeCapabilities = requiredRuntimeCapabilities;
    }

    public ulong RequiredStackBytes { get; }

    public ulong RequiredWritableStagingBytes { get; }

    public ulong ExecutableMapOffsetInPackage { get; }

    public ulong ExecutableMapByteLength { get; }

    public ulong ExecutableMapEndOffsetInPackage { get; }

    public ulong ExecutableContentOffsetInMapping { get; }

    public ulong ExecutableBytes { get; }

    public ulong EntryOffsetInExecutableContent { get; }

    public ulong EntryOffsetInExecutableMapping { get; }

    public ulong EntryOffsetInPackage { get; }

    public int ManifestMetadataByteCount { get; }

    public byte ManifestVersion { get; }

    public NativeManifestAppKind AppKind { get; }

    public NativeManifestRuntimeKind RuntimeKind { get; }

    public NativeManifestNetworkMode NetworkMode { get; }

    public NativeManifestLogMode LogMode { get; }

    public int ArgumentByteLength { get; }

    public int EnvironmentCount { get; }

    public BootCapabilities RequiredBootCapabilities { get; }

    public NativeAppLaunchRuntimeCapabilities RequiredRuntimeCapabilities { get; }
}

public readonly struct NativeAppInvocationPlanResult
{
    private NativeAppInvocationPlanResult(
        NativeAppInvocationPlanStatus status,
        NativeAppExecutableMemoryPlanStatus executableMemoryStatus,
        NativeAppPayloadMappingPlanStatus payloadMappingStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus,
        NativeAppLaunchStatus launchStatus,
        bool isInitialized,
        NativeAppInvocationPlan plan)
    {
        Status = status;
        ExecutableMemoryStatus = executableMemoryStatus;
        PayloadMappingStatus = payloadMappingStatus;
        DescriptorStatus = descriptorStatus;
        PayloadHeaderStatus = payloadHeaderStatus;
        LaunchStatus = launchStatus;
        IsInitialized = isInitialized;
        Plan = plan;
    }

    public NativeAppInvocationPlanStatus Status { get; }

    public NativeAppExecutableMemoryPlanStatus ExecutableMemoryStatus { get; }

    public NativeAppPayloadMappingPlanStatus PayloadMappingStatus { get; }

    public NativeAppLaunchDescriptorStatus DescriptorStatus { get; }

    public NativeAppPayloadHeaderStatus PayloadHeaderStatus { get; }

    public NativeAppLaunchStatus LaunchStatus { get; }

    public bool IsInitialized { get; }

    public NativeAppInvocationPlan Plan { get; }

    public bool Succeeded => IsInitialized && Status == NativeAppInvocationPlanStatus.Success;

    public static NativeAppInvocationPlanResult Success(
        NativeAppExecutableMemoryPlanStatus executableMemoryStatus,
        NativeAppPayloadMappingPlanStatus payloadMappingStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus,
        NativeAppLaunchStatus launchStatus,
        NativeAppInvocationPlan plan)
    {
        return new NativeAppInvocationPlanResult(
            NativeAppInvocationPlanStatus.Success,
            executableMemoryStatus,
            payloadMappingStatus,
            descriptorStatus,
            payloadHeaderStatus,
            launchStatus,
            isInitialized: true,
            plan);
    }

    public static NativeAppInvocationPlanResult Fail(
        NativeAppInvocationPlanStatus status,
        NativeAppExecutableMemoryPlanStatus executableMemoryStatus,
        NativeAppPayloadMappingPlanStatus payloadMappingStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus,
        NativeAppLaunchStatus launchStatus)
    {
        return new NativeAppInvocationPlanResult(
            status,
            executableMemoryStatus,
            payloadMappingStatus,
            descriptorStatus,
            payloadHeaderStatus,
            launchStatus,
            isInitialized: true,
            default);
    }
}

public static class NativeAppInvocationPlanner
{
    public const ulong MinimumStackBytes = 16 * 1024;
    public const ulong DefaultStackBytes = 64 * 1024;
    public const ulong MaximumStackBytes = 16 * 1024 * 1024;

    public const int MaximumInvocationMetadataBytes =
        NativeManifestParser.MaximumAppNameLength +
        NativeManifestParser.MaximumAppPathLength +
        NativeManifestParser.MaximumArgsLength +
        (NativeManifestParser.MaximumEnvironmentCount *
            (NativeManifestParser.MaximumEnvironmentKeyLength +
             NativeManifestParser.MaximumEnvironmentValueLength));

    public static NativeAppInvocationPlanResult Create(
        NativeAppExecutableMemoryPlanResult executableMemoryPlan,
        NativeAppLaunchPlanResult launchPlan,
        ulong stackSizeBytes = DefaultStackBytes)
    {
        if (!executableMemoryPlan.Succeeded)
        {
            return NativeAppInvocationPlanResult.Fail(
                NativeAppInvocationPlanStatus.ExecutableMemoryPlanFailed,
                executableMemoryPlan.Status,
                executableMemoryPlan.PayloadMappingStatus,
                executableMemoryPlan.DescriptorStatus,
                executableMemoryPlan.PayloadHeaderStatus,
                launchPlan.Status);
        }

        if (!launchPlan.Succeeded)
        {
            return NativeAppInvocationPlanResult.Fail(
                NativeAppInvocationPlanStatus.LaunchPlanFailed,
                executableMemoryPlan.Status,
                executableMemoryPlan.PayloadMappingStatus,
                executableMemoryPlan.DescriptorStatus,
                executableMemoryPlan.PayloadHeaderStatus,
                launchPlan.Status);
        }

        NativeAppInvocationPlanStatus executableStatus =
            ValidateExecutableMemoryMetadata(executableMemoryPlan);
        if (executableStatus != NativeAppInvocationPlanStatus.Success)
        {
            return NativeAppInvocationPlanResult.Fail(
                executableStatus,
                executableMemoryPlan.Status,
                executableMemoryPlan.PayloadMappingStatus,
                executableMemoryPlan.DescriptorStatus,
                executableMemoryPlan.PayloadHeaderStatus,
                launchPlan.Status);
        }

        if (!IsValidStackSize(stackSizeBytes, executableMemoryPlan.PageSize))
        {
            return NativeAppInvocationPlanResult.Fail(
                NativeAppInvocationPlanStatus.InvalidStackSize,
                executableMemoryPlan.Status,
                executableMemoryPlan.PayloadMappingStatus,
                executableMemoryPlan.DescriptorStatus,
                executableMemoryPlan.PayloadHeaderStatus,
                launchPlan.Status);
        }

        NativeAppInvocationPlanStatus metadataStatus =
            TryGetManifestMetadataByteCount(
                launchPlan.Plan,
                out int manifestMetadataByteCount);
        if (metadataStatus != NativeAppInvocationPlanStatus.Success)
        {
            return NativeAppInvocationPlanResult.Fail(
                metadataStatus,
                executableMemoryPlan.Status,
                executableMemoryPlan.PayloadMappingStatus,
                executableMemoryPlan.DescriptorStatus,
                executableMemoryPlan.PayloadHeaderStatus,
                launchPlan.Status);
        }

        if (!TryAdd(
                executableMemoryPlan.ExecutableMapOffsetInPackage,
                executableMemoryPlan.EntryOffsetInExecutableMapping,
                out ulong entryOffsetInPackage))
        {
            return NativeAppInvocationPlanResult.Fail(
                NativeAppInvocationPlanStatus.RangeOverflow,
                executableMemoryPlan.Status,
                executableMemoryPlan.PayloadMappingStatus,
                executableMemoryPlan.DescriptorStatus,
                executableMemoryPlan.PayloadHeaderStatus,
                launchPlan.Status);
        }

        NativeAppInvocationPlan plan = new NativeAppInvocationPlan(
            stackSizeBytes,
            executableMemoryPlan.RequiredWritableStagingBytes,
            executableMemoryPlan.ExecutableMapOffsetInPackage,
            executableMemoryPlan.ExecutableMapByteLength,
            executableMemoryPlan.ExecutableMapEndOffsetInPackage,
            executableMemoryPlan.ExecutableContentOffsetInMapping,
            executableMemoryPlan.ExecutableBytes,
            executableMemoryPlan.EntryOffsetInExecutableContent,
            executableMemoryPlan.EntryOffsetInExecutableMapping,
            entryOffsetInPackage,
            manifestMetadataByteCount,
            launchPlan.Plan.ManifestVersion,
            launchPlan.Plan.AppKind,
            launchPlan.Plan.RuntimeKind,
            launchPlan.Plan.RequestedNetworkMode,
            launchPlan.Plan.LogMode,
            launchPlan.Plan.ArgumentByteLength,
            launchPlan.Plan.EnvironmentCount,
            launchPlan.Plan.RequiredBootCapabilities,
            launchPlan.Plan.RequiredRuntimeCapabilities);

        return NativeAppInvocationPlanResult.Success(
            executableMemoryPlan.Status,
            executableMemoryPlan.PayloadMappingStatus,
            executableMemoryPlan.DescriptorStatus,
            executableMemoryPlan.PayloadHeaderStatus,
            launchPlan.Status,
            plan);
    }

    private static NativeAppInvocationPlanStatus ValidateExecutableMemoryMetadata(
        NativeAppExecutableMemoryPlanResult executableMemoryPlan)
    {
        if (executableMemoryPlan.ExecutableBytes == 0 ||
            executableMemoryPlan.ExecutableMapByteLength == 0 ||
            executableMemoryPlan.RequiredWritableStagingBytes == 0 ||
            executableMemoryPlan.PageSize < NativeAppExecutableMemoryPlan.MinimumPageSize ||
            !IsPowerOfTwo(executableMemoryPlan.PageSize) ||
            executableMemoryPlan.ExecutableAlignment == 0 ||
            !IsPowerOfTwo(executableMemoryPlan.ExecutableAlignment) ||
            executableMemoryPlan.ExecutableAlignment > executableMemoryPlan.PageSize)
        {
            return NativeAppInvocationPlanStatus.InvalidExecutableMemoryMetadata;
        }

        if (executableMemoryPlan.EntryOffsetInExecutableContent >= executableMemoryPlan.ExecutableBytes ||
            executableMemoryPlan.EntryOffsetInExecutableMapping >= executableMemoryPlan.ExecutableMapByteLength)
        {
            return NativeAppInvocationPlanStatus.InvalidEntryOffset;
        }

        if (!TryAdd(
                executableMemoryPlan.ExecutableContentOffsetInMapping,
                executableMemoryPlan.ExecutableBytes,
                out ulong executableContentEndInMapping) ||
            !TryAdd(
                executableMemoryPlan.ExecutableContentOffsetInMapping,
                executableMemoryPlan.EntryOffsetInExecutableContent,
                out ulong expectedEntryOffsetInMapping) ||
            !TryAdd(
                executableMemoryPlan.ExecutableMapOffsetInPackage,
                executableMemoryPlan.ExecutableMapByteLength,
                out ulong expectedExecutableMapEndOffset))
        {
            return NativeAppInvocationPlanStatus.RangeOverflow;
        }

        if (executableContentEndInMapping > executableMemoryPlan.ExecutableMapByteLength ||
            expectedEntryOffsetInMapping != executableMemoryPlan.EntryOffsetInExecutableMapping)
        {
            return NativeAppInvocationPlanStatus.InvalidEntryOffset;
        }

        if (expectedExecutableMapEndOffset != executableMemoryPlan.ExecutableMapEndOffsetInPackage ||
            executableMemoryPlan.RequiredWritableStagingBytes != executableMemoryPlan.ExecutableMapByteLength ||
            !IsSafeWriteThenExecutePolicy(
                executableMemoryPlan.WritableStagingProtection,
                executableMemoryPlan.ExecutableProtection,
                executableMemoryPlan.FinalProtection))
        {
            return NativeAppInvocationPlanStatus.InvalidExecutableMemoryMetadata;
        }

        return NativeAppInvocationPlanStatus.Success;
    }

    private static bool IsValidStackSize(ulong stackSizeBytes, uint pageSize)
    {
        ulong pageSizeBytes = pageSize;
        return stackSizeBytes >= MinimumStackBytes
            && stackSizeBytes >= pageSizeBytes
            && stackSizeBytes <= MaximumStackBytes
            && IsAligned(stackSizeBytes, pageSizeBytes);
    }

    private static NativeAppInvocationPlanStatus TryGetManifestMetadataByteCount(
        NativeAppLaunchPlan launchPlan,
        out int manifestMetadataByteCount)
    {
        manifestMetadataByteCount = 0;

        if (!IsValidFieldLength(launchPlan.AppName, NativeManifestParser.MaximumAppNameLength) ||
            !IsValidFieldLength(launchPlan.AppPath, NativeManifestParser.MaximumAppPathLength) ||
            !IsValidFieldLength(launchPlan.Arguments, NativeManifestParser.MaximumArgsLength) ||
            launchPlan.ArgumentByteLength < 0 ||
            launchPlan.ArgumentByteLength > NativeManifestParser.MaximumArgsLength ||
            launchPlan.ArgumentByteLength != launchPlan.Arguments.Length ||
            launchPlan.EnvironmentCount < 0 ||
            launchPlan.EnvironmentCount > NativeManifestParser.MaximumEnvironmentCount)
        {
            return NativeAppInvocationPlanStatus.MetadataOutOfBounds;
        }

        ulong total = 0;
        if (!TryAdd((ulong)launchPlan.AppName.Length, (ulong)launchPlan.AppPath.Length, out total) ||
            !TryAdd(total, (ulong)launchPlan.ArgumentByteLength, out total))
        {
            return NativeAppInvocationPlanStatus.RangeOverflow;
        }

        for (int index = 0; index < launchPlan.EnvironmentCount; index++)
        {
            NativeManifestEnvironmentEntry entry = launchPlan.GetEnvironmentEntry(index);
            if (!entry.IsPresent ||
                !IsValidFieldLength(entry.Key, NativeManifestParser.MaximumEnvironmentKeyLength) ||
                !IsValidFieldLength(entry.Value, NativeManifestParser.MaximumEnvironmentValueLength))
            {
                return NativeAppInvocationPlanStatus.MetadataOutOfBounds;
            }

            if (!TryAdd(total, (ulong)entry.Key.Length, out total) ||
                !TryAdd(total, (ulong)entry.Value.Length, out total))
            {
                return NativeAppInvocationPlanStatus.RangeOverflow;
            }
        }

        if (total > MaximumInvocationMetadataBytes)
        {
            return NativeAppInvocationPlanStatus.MetadataOutOfBounds;
        }

        manifestMetadataByteCount = (int)total;
        return NativeAppInvocationPlanStatus.Success;
    }

    private static bool IsValidFieldLength(NativeManifestField field, int maximumLength)
    {
        return field.IsPresent && field.Length >= 0 && field.Length <= maximumLength;
    }

    private static bool IsSafeWriteThenExecutePolicy(
        NativeAppExecutableMemoryProtection writableStagingProtection,
        NativeAppExecutableMemoryProtection executableProtection,
        NativeAppExecutableMemoryProtection finalProtection)
    {
        return writableStagingProtection != NativeAppExecutableMemoryProtection.None
            && executableProtection != NativeAppExecutableMemoryProtection.None
            && finalProtection != NativeAppExecutableMemoryProtection.None
            && (writableStagingProtection & NativeAppExecutableMemoryProtection.Write) != 0
            && (writableStagingProtection & NativeAppExecutableMemoryProtection.Execute) == 0
            && (executableProtection & NativeAppExecutableMemoryProtection.Execute) != 0
            && (executableProtection & NativeAppExecutableMemoryProtection.Write) == 0
            && (finalProtection & NativeAppExecutableMemoryProtection.Execute) != 0
            && (finalProtection & NativeAppExecutableMemoryProtection.Write) == 0;
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool IsAligned(ulong value, ulong alignment)
    {
        return alignment != 0 && (value & (alignment - 1)) == 0;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > ulong.MaxValue - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }
}
