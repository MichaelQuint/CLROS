namespace OS.Drivers;
using OS.Net;

public readonly struct VirtioNetDriverQueueBringUpMetadata
{
    internal VirtioNetDriverQueueBringUpMetadata(int sourceIndex, VirtioPciBindingMetadata binding, MacAddress macAddress, bool hasDeviceStatus, bool linkUp, bool hasAnnounce, ulong selectedFeatures, ulong supportedFeatureMask, ushort availableQueueCount, ushort rxQueueIndex, ushort txQueueIndex, ushort rxQueueSize, ushort txQueueSize, ushort rxDescriptorCount, ushort txDescriptorCount, uint requiredRxQueueMemoryBytes, uint requiredTxQueueMemoryBytes, uint availableRxQueueMemoryBytes, uint availableTxQueueMemoryBytes, uint requiredRxBufferBytes, uint requiredTxBufferBytes, uint availableRxBufferBytes, uint availableTxBufferBytes, uint totalRequiredQueueMemoryBytes, uint totalAvailableQueueMemoryBytes, uint totalRequiredBufferBytes, uint totalAvailableBufferBytes, VirtioNetQueuePlan queuePlan)
    {
        SourceIndex = sourceIndex;
        Binding = binding;
        MacAddress = macAddress;
        HasDeviceStatus = hasDeviceStatus;
        LinkUp = linkUp;
        HasAnnounce = hasAnnounce;
        SelectedFeatures = selectedFeatures;
        SupportedFeatureMask = supportedFeatureMask;
        AvailableQueueCount = availableQueueCount;
        RxQueueIndex = rxQueueIndex;
        TxQueueIndex = txQueueIndex;
        RxQueueSize = rxQueueSize;
        TxQueueSize = txQueueSize;
        RxDescriptorCount = rxDescriptorCount;
        TxDescriptorCount = txDescriptorCount;
        RequiredRxQueueMemoryBytes = requiredRxQueueMemoryBytes;
        RequiredTxQueueMemoryBytes = requiredTxQueueMemoryBytes;
        AvailableRxQueueMemoryBytes = availableRxQueueMemoryBytes;
        AvailableTxQueueMemoryBytes = availableTxQueueMemoryBytes;
        RequiredRxBufferBytes = requiredRxBufferBytes;
        RequiredTxBufferBytes = requiredTxBufferBytes;
        AvailableRxBufferBytes = availableRxBufferBytes;
        AvailableTxBufferBytes = availableTxBufferBytes;
        TotalRequiredQueueMemoryBytes = totalRequiredQueueMemoryBytes;
        TotalAvailableQueueMemoryBytes = totalAvailableQueueMemoryBytes;
        TotalRequiredBufferBytes = totalRequiredBufferBytes;
        TotalAvailableBufferBytes = totalAvailableBufferBytes;
        QueuePlan = queuePlan;
    }

    public int SourceIndex { get; }
    public VirtioPciBindingMetadata Binding { get; }
    public MacAddress MacAddress { get; }
    public bool HasDeviceStatus { get; }
    public bool LinkUp { get; }
    public bool HasAnnounce { get; }
    public ulong SelectedFeatures { get; }
    public ulong SupportedFeatureMask { get; }
    public ushort AvailableQueueCount { get; }
    public ushort RxQueueIndex { get; }
    public ushort TxQueueIndex { get; }
    public ushort RxQueueSize { get; }
    public ushort TxQueueSize { get; }
    public ushort RxDescriptorCount { get; }
    public ushort TxDescriptorCount { get; }
    public uint RequiredRxQueueMemoryBytes { get; }
    public uint RequiredTxQueueMemoryBytes { get; }
    public uint AvailableRxQueueMemoryBytes { get; }
    public uint AvailableTxQueueMemoryBytes { get; }
    public uint RequiredRxBufferBytes { get; }
    public uint RequiredTxBufferBytes { get; }
    public uint AvailableRxBufferBytes { get; }
    public uint AvailableTxBufferBytes { get; }
    public uint TotalRequiredQueueMemoryBytes { get; }
    public uint TotalAvailableQueueMemoryBytes { get; }
    public uint TotalRequiredBufferBytes { get; }
    public uint TotalAvailableBufferBytes { get; }
    public VirtioNetQueuePlan QueuePlan { get; }
    public bool ReceivePublishReady => RxDescriptorCount == QueuePlan.RxBuffers.RequiredDescriptorCount && AvailableRxQueueMemoryBytes >= RequiredRxQueueMemoryBytes && AvailableRxBufferBytes >= RequiredRxBufferBytes;
    public bool TransmitPublishReady => TxDescriptorCount == QueuePlan.TxFrame.RequiredDescriptorCount && AvailableTxQueueMemoryBytes >= RequiredTxQueueMemoryBytes && AvailableTxBufferBytes >= RequiredTxBufferBytes;
    public bool PublishReady => ReceivePublishReady && TransmitPublishReady;
}
