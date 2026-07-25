namespace OS.Drivers;

public enum VirtioNetQueuePlanStatus
{
    Success = 0,
    InsufficientQueueCount = 1,
    InvalidRxQueueSize = 2,
    InvalidTxQueueSize = 3,
    InvalidRxBufferCount = 4,
    InvalidFrameBufferSize = 5,
    RxQueueTooSmall = 6,
    TxQueueTooSmall = 7,
    BufferByteCountOverflow = 8
}

public enum VirtioNetDescriptorAccessDirection
{
    DeviceReadable = 0,
    DeviceWritable = 1
}

public readonly struct VirtioNetRxBufferPlan
{
    internal VirtioNetRxBufferPlan(
        ushort bufferCount,
        uint frameBufferBytes,
        uint requiredBufferBytes)
    {
        BufferCount = bufferCount;
        FrameBufferBytes = frameBufferBytes;
        RequiredBufferBytes = requiredBufferBytes;
    }

    public ushort BufferCount { get; }

    public uint FrameBufferBytes { get; }

    public uint RequiredBufferBytes { get; }

    public ushort RequiredDescriptorCount => BufferCount;

    public VirtioNetDescriptorAccessDirection DescriptorAccessDirection =>
        VirtioNetDescriptorAccessDirection.DeviceWritable;
}

public readonly struct VirtioNetTxFramePlan
{
    internal VirtioNetTxFramePlan(
        uint frameBufferBytes,
        uint ethernetFrameBytes)
    {
        HeaderBufferBytes = VirtioNetQueuePlan.VirtioNetHeaderLength;
        EthernetFrameBytes = ethernetFrameBytes;
        RequiredBufferBytes = frameBufferBytes;
    }

    public uint HeaderBufferBytes { get; }

    public uint EthernetFrameBytes { get; }

    public uint RequiredBufferBytes { get; }

    public ushort RequiredDescriptorCount => VirtioNetQueuePlan.RequiredTxDescriptorCount;

    public VirtioNetDescriptorAccessDirection HeaderDescriptorAccessDirection =>
        VirtioNetDescriptorAccessDirection.DeviceReadable;

    public VirtioNetDescriptorAccessDirection FrameDescriptorAccessDirection =>
        VirtioNetDescriptorAccessDirection.DeviceReadable;
}

public readonly struct VirtioNetQueuePlan
{
    public const ushort MinimumQueueCount = 2;
    public const ushort ReceiveQueueIndex = 0;
    public const ushort TransmitQueueIndex = 1;
    public const ushort RequiredTxDescriptorCount = 2;

    public const uint VirtioNetHeaderLength = 12;
    public const uint EthernetHeaderLength = 14;
    public const uint EthernetMtuBytes = 1500;
    public const uint MaximumEthernetFrameBytes = EthernetHeaderLength + EthernetMtuBytes;
    public const uint MinimumFrameBufferBytes = VirtioNetHeaderLength + EthernetHeaderLength;
    public const uint MaximumFrameBufferBytes = VirtioNetHeaderLength + MaximumEthernetFrameBytes;

    public const bool AllocatesBuffers = false;
    public const bool PublishesDescriptors = false;
    public const bool NotifiesDevice = false;
    public const bool ParsesVirtioNetHeader = false;

    private VirtioNetQueuePlan(
        ushort availableQueueCount,
        VirtqueueSplitRingPlan rxQueue,
        VirtqueueSplitRingPlan txQueue,
        VirtioNetRxBufferPlan rxBuffers,
        VirtioNetTxFramePlan txFrame,
        uint totalRequiredBufferBytes)
    {
        AvailableQueueCount = availableQueueCount;
        RxQueue = rxQueue;
        TxQueue = txQueue;
        RxBuffers = rxBuffers;
        TxFrame = txFrame;
        TotalRequiredBufferBytes = totalRequiredBufferBytes;
    }

    public ushort AvailableQueueCount { get; }

    public VirtqueueSplitRingPlan RxQueue { get; }

    public VirtqueueSplitRingPlan TxQueue { get; }

    public VirtioNetRxBufferPlan RxBuffers { get; }

    public VirtioNetTxFramePlan TxFrame { get; }

    public uint TotalRequiredBufferBytes { get; }

    public static VirtioNetQueuePlanResult Create(
        ushort availableQueueCount,
        ushort rxQueueSize,
        ushort txQueueSize,
        ushort rxBufferCount,
        uint frameBufferBytes)
    {
        if (availableQueueCount < MinimumQueueCount)
        {
            return VirtioNetQueuePlanResult.Fail(
                VirtioNetQueuePlanStatus.InsufficientQueueCount,
                availableQueueCount,
                rxQueueSize,
                txQueueSize,
                rxBufferCount,
                frameBufferBytes);
        }

        VirtqueueSplitRingPlanResult rxQueue = VirtqueueSplitRingPlan.Create(rxQueueSize);
        if (!rxQueue.Succeeded)
        {
            return VirtioNetQueuePlanResult.Fail(
                VirtioNetQueuePlanStatus.InvalidRxQueueSize,
                availableQueueCount,
                rxQueueSize,
                txQueueSize,
                rxBufferCount,
                frameBufferBytes,
                rxQueue.Status,
                VirtqueueSplitRingPlanStatus.Success);
        }

        VirtqueueSplitRingPlanResult txQueue = VirtqueueSplitRingPlan.Create(txQueueSize);
        if (!txQueue.Succeeded)
        {
            return VirtioNetQueuePlanResult.Fail(
                VirtioNetQueuePlanStatus.InvalidTxQueueSize,
                availableQueueCount,
                rxQueueSize,
                txQueueSize,
                rxBufferCount,
                frameBufferBytes,
                rxQueue.Status,
                txQueue.Status);
        }

        if (rxBufferCount == 0)
        {
            return VirtioNetQueuePlanResult.Fail(
                VirtioNetQueuePlanStatus.InvalidRxBufferCount,
                availableQueueCount,
                rxQueueSize,
                txQueueSize,
                rxBufferCount,
                frameBufferBytes,
                rxQueue.Status,
                txQueue.Status);
        }

        if (frameBufferBytes < MinimumFrameBufferBytes ||
            frameBufferBytes > MaximumFrameBufferBytes)
        {
            return VirtioNetQueuePlanResult.Fail(
                VirtioNetQueuePlanStatus.InvalidFrameBufferSize,
                availableQueueCount,
                rxQueueSize,
                txQueueSize,
                rxBufferCount,
                frameBufferBytes,
                rxQueue.Status,
                txQueue.Status);
        }

        if (rxQueueSize < rxBufferCount)
        {
            return VirtioNetQueuePlanResult.Fail(
                VirtioNetQueuePlanStatus.RxQueueTooSmall,
                availableQueueCount,
                rxQueueSize,
                txQueueSize,
                rxBufferCount,
                frameBufferBytes,
                rxQueue.Status,
                txQueue.Status);
        }

        if (txQueueSize < RequiredTxDescriptorCount)
        {
            return VirtioNetQueuePlanResult.Fail(
                VirtioNetQueuePlanStatus.TxQueueTooSmall,
                availableQueueCount,
                rxQueueSize,
                txQueueSize,
                rxBufferCount,
                frameBufferBytes,
                rxQueue.Status,
                txQueue.Status);
        }

        if (!TryMultiply(
                rxBufferCount,
                frameBufferBytes,
                out uint rxRequiredBufferBytes) ||
            !TryAdd(
                rxRequiredBufferBytes,
                frameBufferBytes,
                out uint totalRequiredBufferBytes))
        {
            return VirtioNetQueuePlanResult.Fail(
                VirtioNetQueuePlanStatus.BufferByteCountOverflow,
                availableQueueCount,
                rxQueueSize,
                txQueueSize,
                rxBufferCount,
                frameBufferBytes,
                rxQueue.Status,
                txQueue.Status);
        }

        uint ethernetFrameBytes = frameBufferBytes - VirtioNetHeaderLength;
        VirtioNetRxBufferPlan rxBuffers = new VirtioNetRxBufferPlan(
            rxBufferCount,
            frameBufferBytes,
            rxRequiredBufferBytes);
        VirtioNetTxFramePlan txFrame = new VirtioNetTxFramePlan(
            frameBufferBytes,
            ethernetFrameBytes);

        VirtioNetQueuePlan plan = new VirtioNetQueuePlan(
            availableQueueCount,
            rxQueue.Plan,
            txQueue.Plan,
            rxBuffers,
            txFrame,
            totalRequiredBufferBytes);

        return VirtioNetQueuePlanResult.Success(
            availableQueueCount,
            rxQueueSize,
            txQueueSize,
            rxBufferCount,
            frameBufferBytes,
            rxQueue.Status,
            txQueue.Status,
            plan);
    }

    private static bool TryMultiply(
        ushort left,
        uint right,
        out uint result)
    {
        ulong value = (ulong)left * right;
        if (value > uint.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }

    private static bool TryAdd(
        uint left,
        uint right,
        out uint result)
    {
        if (left > uint.MaxValue - right)
        {
            result = 0;
            return false;
        }

        result = left + right;
        return true;
    }
}

public readonly struct VirtioNetQueuePlanResult
{
    private VirtioNetQueuePlanResult(
        VirtioNetQueuePlanStatus status,
        ushort availableQueueCount,
        ushort rxQueueSize,
        ushort txQueueSize,
        ushort rxBufferCount,
        uint frameBufferBytes,
        VirtqueueSplitRingPlanStatus rxQueueStatus,
        VirtqueueSplitRingPlanStatus txQueueStatus,
        VirtioNetQueuePlan plan)
    {
        IsInitialized = true;
        Status = status;
        AvailableQueueCount = availableQueueCount;
        RxQueueSize = rxQueueSize;
        TxQueueSize = txQueueSize;
        RxBufferCount = rxBufferCount;
        FrameBufferBytes = frameBufferBytes;
        RxQueueStatus = rxQueueStatus;
        TxQueueStatus = txQueueStatus;
        Plan = plan;
    }

    public bool IsInitialized { get; }

    public VirtioNetQueuePlanStatus Status { get; }

    public ushort AvailableQueueCount { get; }

    public ushort RxQueueSize { get; }

    public ushort TxQueueSize { get; }

    public ushort RxBufferCount { get; }

    public uint FrameBufferBytes { get; }

    public VirtqueueSplitRingPlanStatus RxQueueStatus { get; }

    public VirtqueueSplitRingPlanStatus TxQueueStatus { get; }

    public VirtioNetQueuePlan Plan { get; }

    public bool Succeeded => IsInitialized && Status == VirtioNetQueuePlanStatus.Success;

    public static VirtioNetQueuePlanResult Success(
        ushort availableQueueCount,
        ushort rxQueueSize,
        ushort txQueueSize,
        ushort rxBufferCount,
        uint frameBufferBytes,
        VirtqueueSplitRingPlanStatus rxQueueStatus,
        VirtqueueSplitRingPlanStatus txQueueStatus,
        VirtioNetQueuePlan plan)
    {
        return new VirtioNetQueuePlanResult(
            VirtioNetQueuePlanStatus.Success,
            availableQueueCount,
            rxQueueSize,
            txQueueSize,
            rxBufferCount,
            frameBufferBytes,
            rxQueueStatus,
            txQueueStatus,
            plan);
    }

    public static VirtioNetQueuePlanResult Fail(
        VirtioNetQueuePlanStatus status,
        ushort availableQueueCount,
        ushort rxQueueSize,
        ushort txQueueSize,
        ushort rxBufferCount,
        uint frameBufferBytes,
        VirtqueueSplitRingPlanStatus rxQueueStatus = VirtqueueSplitRingPlanStatus.Success,
        VirtqueueSplitRingPlanStatus txQueueStatus = VirtqueueSplitRingPlanStatus.Success)
    {
        return new VirtioNetQueuePlanResult(
            status,
            availableQueueCount,
            rxQueueSize,
            txQueueSize,
            rxBufferCount,
            frameBufferBytes,
            rxQueueStatus,
            txQueueStatus,
            default);
    }
}
