using OS.Manifest;

namespace OS.Kernel.Runtime;

public readonly struct NativeAppExecutableMemoryPlanResult
{
    private NativeAppExecutableMemoryPlanResult(NativeAppExecutableMemoryPlanStatus status, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, bool isInitialized, uint pageSize, uint executableAlignment, ulong requiredWritableStagingBytes, ulong executableBytes, ulong executableMapOffsetInPackage, ulong executableMapByteLength, ulong executableMapEndOffsetInPackage, ulong executableContentOffsetInMapping, ulong entryOffsetInExecutableContent, ulong entryOffsetInExecutableMapping, NativeAppExecutableMemoryProtection writableStagingProtection, NativeAppExecutableMemoryProtection executableProtection, NativeAppExecutableMemoryProtection finalProtection)
    {
        Status = status;
        PayloadMappingStatus = payloadMappingStatus;
        DescriptorStatus = descriptorStatus;
        PayloadHeaderStatus = payloadHeaderStatus;
        IsInitialized = isInitialized;
        PageSize = pageSize;
        ExecutableAlignment = executableAlignment;
        RequiredWritableStagingBytes = requiredWritableStagingBytes;
        ExecutableBytes = executableBytes;
        ExecutableMapOffsetInPackage = executableMapOffsetInPackage;
        ExecutableMapByteLength = executableMapByteLength;
        ExecutableMapEndOffsetInPackage = executableMapEndOffsetInPackage;
        ExecutableContentOffsetInMapping = executableContentOffsetInMapping;
        EntryOffsetInExecutableContent = entryOffsetInExecutableContent;
        EntryOffsetInExecutableMapping = entryOffsetInExecutableMapping;
        WritableStagingProtection = writableStagingProtection;
        ExecutableProtection = executableProtection;
        FinalProtection = finalProtection;
    }

    public NativeAppExecutableMemoryPlanStatus Status
    {
        get;
    }
    public NativeAppPayloadMappingPlanStatus PayloadMappingStatus
    {
        get;
    }
    public NativeAppLaunchDescriptorStatus DescriptorStatus
    {
        get;
    }
    public NativeAppPayloadHeaderStatus PayloadHeaderStatus
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeAppExecutableMemoryPlanStatus.Success;
    public uint PageSize
    {
        get;
    }
    public uint ExecutableAlignment
    {
        get;
    }
    public ulong RequiredWritableStagingBytes
    {
        get;
    }
    public ulong ExecutableBytes
    {
        get;
    }
    public ulong ExecutableMapOffsetInPackage
    {
        get;
    }
    public ulong ExecutableMapByteLength
    {
        get;
    }
    public ulong ExecutableMapEndOffsetInPackage
    {
        get;
    }
    public ulong ExecutableContentOffsetInMapping
    {
        get;
    }
    public ulong EntryOffsetInExecutableContent
    {
        get;
    }
    public ulong EntryOffsetInExecutableMapping
    {
        get;
    }
    public NativeAppExecutableMemoryProtection WritableStagingProtection
    {
        get;
    }
    public NativeAppExecutableMemoryProtection ExecutableProtection
    {
        get;
    }
    public NativeAppExecutableMemoryProtection FinalProtection
    {
        get;
    }

    public static NativeAppExecutableMemoryPlanResult Success(NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, uint pageSize, uint executableAlignment, ulong requiredWritableStagingBytes, ulong executableBytes, ulong executableMapOffsetInPackage, ulong executableMapByteLength, ulong executableMapEndOffsetInPackage, ulong executableContentOffsetInMapping, ulong entryOffsetInExecutableContent, ulong entryOffsetInExecutableMapping, NativeAppExecutableMemoryProtection writableStagingProtection, NativeAppExecutableMemoryProtection executableProtection, NativeAppExecutableMemoryProtection finalProtection)
    {
        return new NativeAppExecutableMemoryPlanResult(NativeAppExecutableMemoryPlanStatus.Success, payloadMappingStatus, descriptorStatus, payloadHeaderStatus, isInitialized: true, pageSize, executableAlignment, requiredWritableStagingBytes, executableBytes, executableMapOffsetInPackage, executableMapByteLength, executableMapEndOffsetInPackage, executableContentOffsetInMapping, entryOffsetInExecutableContent, entryOffsetInExecutableMapping, writableStagingProtection, executableProtection, finalProtection);
    }

    public static NativeAppExecutableMemoryPlanResult Fail(NativeAppExecutableMemoryPlanStatus status, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, uint pageSize, uint executableAlignment, NativeAppExecutableMemoryProtection writableStagingProtection, NativeAppExecutableMemoryProtection executableProtection, NativeAppExecutableMemoryProtection finalProtection)
    {
        return new NativeAppExecutableMemoryPlanResult(status, payloadMappingStatus, descriptorStatus, payloadHeaderStatus, isInitialized: true, pageSize, executableAlignment, 0, 0, 0, 0, 0, 0, 0, 0, writableStagingProtection, executableProtection, finalProtection);
    }
}
