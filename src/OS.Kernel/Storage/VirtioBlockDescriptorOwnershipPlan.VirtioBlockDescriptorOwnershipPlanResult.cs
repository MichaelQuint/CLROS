using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockDescriptorOwnershipPlanResult
{
    private VirtioBlockDescriptorOwnershipPlanResult(
        VirtioBlockDescriptorOwnershipStatus status,
        VirtioBlockReadLifecyclePublishStatus publishStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsMarkedInFlight,
        ushort descriptorsFreed,
        bool isInitialized)
    {
        Status = status;
        PublishStatus = publishStatus;
        CompletionStatus = completionStatus;
        CompletionReaderStatus = completionReaderStatus;
        RequestHeaderDescriptorIndex = requestHeaderDescriptorIndex;
        DataBufferDescriptorIndex = dataBufferDescriptorIndex;
        StatusByteDescriptorIndex = statusByteDescriptorIndex;
        FirstDescriptorIndex = requestHeaderDescriptorIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        DescriptorsFreed = descriptorsFreed;
        IsInitialized = isInitialized;
    }

    public VirtioBlockDescriptorOwnershipStatus Status { get; }

    public VirtioBlockReadLifecyclePublishStatus PublishStatus { get; }

    public VirtioBlockReadLifecycleCompletionStatus CompletionStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public ushort RequestHeaderDescriptorIndex { get; }

    public ushort DataBufferDescriptorIndex { get; }

    public ushort StatusByteDescriptorIndex { get; }

    public ushort FirstDescriptorIndex { get; }

    public int StateTableLength { get; }

    public int RequiredStateTableLength { get; }

    public ushort DescriptorsMarkedInFlight { get; }

    public ushort DescriptorsFreed { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockDescriptorOwnershipStatus.Success;

    internal static VirtioBlockDescriptorOwnershipPlanResult Create(
        VirtioBlockDescriptorOwnershipStatus status,
        VirtioBlockReadLifecyclePublishStatus publishStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsMarkedInFlight,
        ushort descriptorsFreed)
    {
        return new VirtioBlockDescriptorOwnershipPlanResult(
            status,
            publishStatus,
            completionStatus,
            completionReaderStatus,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            stateTableLength,
            requiredStateTableLength,
            descriptorsMarkedInFlight,
            descriptorsFreed,
            isInitialized: true);
    }
}
