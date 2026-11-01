using OS.Manifest;
using OS.Std.Memory;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppPayloadStagingResult
{
    private NativeAppPayloadStagingResult(NativeAppPayloadStagingStatus status, NativeAppPayloadMappingPlanStatus mappingStatus, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, bool isInitialized, ulong payloadOffsetInPackage, ulong executableOffsetInPackage, ulong executableBytes, ulong bytesWritten, ulong requiredBytes)
    {
        Status = status;
        MappingStatus = mappingStatus;
        DescriptorStatus = descriptorStatus;
        PayloadHeaderStatus = payloadHeaderStatus;
        IsInitialized = isInitialized;
        PayloadOffsetInPackage = payloadOffsetInPackage;
        ExecutableOffsetInPackage = executableOffsetInPackage;
        ExecutableBytes = executableBytes;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public NativeAppPayloadStagingStatus Status { get; }
    public NativeAppPayloadMappingPlanStatus MappingStatus { get; }
    public NativeAppLaunchDescriptorStatus DescriptorStatus { get; }
    public NativeAppPayloadHeaderStatus PayloadHeaderStatus { get; }
    public bool IsInitialized { get; }
    public ulong PayloadOffsetInPackage { get; }
    public ulong ExecutableOffsetInPackage { get; }
    public ulong ExecutableBytes { get; }
    public ulong BytesWritten { get; }
    public ulong RequiredBytes { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppPayloadStagingStatus.Success;

    public static NativeAppPayloadStagingResult Success(NativeAppPayloadMappingPlanResult mappingPlan, ulong bytesWritten)
    {
        return new NativeAppPayloadStagingResult(NativeAppPayloadStagingStatus.Success, mappingPlan.Status, mappingPlan.DescriptorStatus, mappingPlan.PayloadHeaderStatus, isInitialized: true, mappingPlan.Plan.PayloadOffsetInPackage, mappingPlan.Plan.ExecutableOffsetInPackage, mappingPlan.Plan.ExecutableLength, bytesWritten, mappingPlan.Plan.ExecutableLength);
    }

    public static NativeAppPayloadStagingResult Fail(NativeAppPayloadStagingStatus status, NativeAppPayloadMappingPlanStatus mappingStatus, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, ulong payloadOffsetInPackage, ulong executableOffsetInPackage, ulong executableBytes, ulong requiredBytes)
    {
        return new NativeAppPayloadStagingResult(status, mappingStatus, descriptorStatus, payloadHeaderStatus, isInitialized: true, payloadOffsetInPackage, executableOffsetInPackage, executableBytes, bytesWritten: 0, requiredBytes);
    }
}
