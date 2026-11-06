using OS.Drivers;

namespace OS.Storage;

public ref struct VirtioBlockReadOnlyReadSession
{
    private readonly BlockDeviceInfo device;
    private readonly ushort queueSize;
    private readonly ulong requestHeaderPhysicalAddress;
    private readonly ulong dataBufferPhysicalAddress;
    private readonly ulong statusBytePhysicalAddress;
    private readonly ushort queueIndex;
    private readonly ushort queueNotifyOffset;
    private readonly uint notifyOffsetMultiplier;
    private readonly VirtioPciBarRegisterWindowBinding notifyConfiguration;
    private Span<VirtioBlockDescriptorOwnershipState> descriptorStates;
    private Span<byte> requestHeader;
    private Span<byte> destination;
    private Span<byte> statusBuffer;
    private Span<byte> queueMemory;
    private Span<byte> notifyWindowBytes;
    private VirtioBlockQueueCursorState cursor;

    public VirtioBlockReadOnlyReadSession(
        BlockDeviceInfo device,
        ushort queueSize,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        VirtioBlockQueueCursorState cursor,
        ulong requestHeaderPhysicalAddress,
        ulong dataBufferPhysicalAddress,
        ulong statusBytePhysicalAddress,
        Span<byte> requestHeader,
        Span<byte> destination,
        Span<byte> statusBuffer,
        Span<byte> queueMemory,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        this.device = device;
        this.queueSize = queueSize;
        this.descriptorStates = descriptorStates;
        this.cursor = cursor;
        this.requestHeaderPhysicalAddress = requestHeaderPhysicalAddress;
        this.dataBufferPhysicalAddress = dataBufferPhysicalAddress;
        this.statusBytePhysicalAddress = statusBytePhysicalAddress;
        this.requestHeader = requestHeader;
        this.destination = destination;
        this.statusBuffer = statusBuffer;
        this.queueMemory = queueMemory;
        this.queueIndex = queueIndex;
        this.queueNotifyOffset = queueNotifyOffset;
        this.notifyOffsetMultiplier = notifyOffsetMultiplier;
        this.notifyConfiguration = notifyConfiguration;
        this.notifyWindowBytes = notifyWindowBytes;
    }

    public const bool OwnsNoAllocations = true;
    public const bool DelegatesReadQueuePipelineAdapter = true;
    public const bool CarriesCallerOwnedCursorState = true;
    public const bool CarriesCallerOwnedDescriptorStateTable = true;
    public const bool CarriesCallerOwnedRequestHeader = true;
    public const bool CarriesCallerOwnedDestinationBuffer = true;
    public const bool CarriesCallerOwnedStatusBuffer = true;
    public const bool CarriesCallerOwnedQueueMemory = true;
    public const bool CarriesCallerOwnedNotifyWindowBuffer = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool PerformsMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool WaitsForUsedRingCompletion = false;

    public VirtioBlockQueueCursorState Cursor => cursor;

    public VirtioBlockReadOnlyReadSessionResult Read(
        ulong firstSector,
        uint sectorCount,
        VirtioBlockQueueNotificationBarrierReadiness barrier)
    {
        VirtioBlockQueueCursorState cursorBefore = cursor;
        VirtioBlockReadQueuePipelineAdapterResult adapter =
            VirtioBlockReadQueuePipelineAdapter.Read(
                device,
                firstSector,
                sectorCount,
                queueSize,
                descriptorStates,
                ref cursor,
                requestHeaderPhysicalAddress,
                dataBufferPhysicalAddress,
                statusBytePhysicalAddress,
                requestHeader,
                destination,
                statusBuffer,
                queueMemory,
                queueIndex,
                queueNotifyOffset,
                notifyOffsetMultiplier,
                barrier,
                notifyConfiguration,
                notifyWindowBytes);

        return VirtioBlockReadOnlyReadSessionResult.FromAdapter(
            adapter,
            cursorBefore,
            cursor);
    }
}
