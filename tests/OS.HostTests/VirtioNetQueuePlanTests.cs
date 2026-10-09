using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class VirtioNetQueuePlanTests
{

    [HostTest("drivers.virtionet queue plan accepts rx tx queues", HostLane.Fast, 0)]
    private static bool TestAcceptsRxTxQueues()
    {
        const ushort rxBufferCount = 4;
        VirtioNetQueuePlanResult result = CreateSuccessfulPlan(rxBufferCount);
        if (!result.Succeeded || result.Status != VirtioNetQueuePlanStatus.Success)
        {
            return HostCheck.Result(false);
        }

        VirtioNetQueuePlan plan = result.Plan;
        uint expectedRxBytes = VirtioNetQueuePlan.MaximumFrameBufferBytes * rxBufferCount;
        uint expectedTotalBytes = expectedRxBytes + VirtioNetQueuePlan.MaximumFrameBufferBytes;

        return HostCheck.Result(plan.AvailableQueueCount == VirtioNetQueuePlan.MinimumQueueCount
            && plan.RxQueue.QueueSize == 8
            && plan.TxQueue.QueueSize == 4
            && plan.RxBuffers.BufferCount == rxBufferCount
            && plan.RxBuffers.FrameBufferBytes == VirtioNetQueuePlan.MaximumFrameBufferBytes
            && plan.RxBuffers.RequiredBufferBytes == expectedRxBytes
            && plan.TxFrame.HeaderBufferBytes == VirtioNetQueuePlan.VirtioNetHeaderLength
            && plan.TxFrame.EthernetFrameBytes == VirtioNetQueuePlan.MaximumEthernetFrameBytes
            && plan.TxFrame.RequiredBufferBytes == VirtioNetQueuePlan.MaximumFrameBufferBytes
            && plan.TotalRequiredBufferBytes == expectedTotalBytes
            && !VirtioNetQueuePlan.AllocatesBuffers
            && !VirtioNetQueuePlan.PublishesDescriptors
            && !VirtioNetQueuePlan.NotifiesDevice
            && !VirtioNetQueuePlan.ParsesVirtioNetHeader);
    }

    [HostTest("drivers.virtionet queue plan marks rx writable and tx readable", HostLane.Fast, 1)]
    private static bool TestMarksDescriptorDirections()
    {
        VirtioNetQueuePlanResult result = CreateSuccessfulPlan(rxBufferCount: 2);
        if (!result.Succeeded)
        {
            return HostCheck.Result(false);
        }

        return HostCheck.Result(result.Plan.RxBuffers.DescriptorAccessDirection ==
                VirtioNetDescriptorAccessDirection.DeviceWritable
            && result.Plan.TxFrame.HeaderDescriptorAccessDirection ==
                VirtioNetDescriptorAccessDirection.DeviceReadable
            && result.Plan.TxFrame.FrameDescriptorAccessDirection ==
                VirtioNetDescriptorAccessDirection.DeviceReadable
            && result.Plan.RxBuffers.RequiredDescriptorCount == 2
            && result.Plan.TxFrame.RequiredDescriptorCount ==
                VirtioNetQueuePlan.RequiredTxDescriptorCount);
    }

    [HostTest("drivers.virtionet queue plan rejects insufficient queues", HostLane.Fast, 2)]
    private static bool TestRejectsInsufficientQueues()
    {
        VirtioNetQueuePlanResult result = VirtioNetQueuePlan.Create(
            availableQueueCount: 1,
            rxQueueSize: 8,
            txQueueSize: 4,
            rxBufferCount: 2,
            frameBufferBytes: VirtioNetQueuePlan.MaximumFrameBufferBytes);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioNetQueuePlanStatus.InsufficientQueueCount
            && result.AvailableQueueCount == 1);
    }

    [HostTest("drivers.virtionet queue plan rejects invalid frame size", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidFrameSize()
    {
        VirtioNetQueuePlanResult tooSmall = VirtioNetQueuePlan.Create(
            availableQueueCount: 2,
            rxQueueSize: 8,
            txQueueSize: 4,
            rxBufferCount: 2,
            frameBufferBytes: VirtioNetQueuePlan.MinimumFrameBufferBytes - 1);
        VirtioNetQueuePlanResult tooLarge = VirtioNetQueuePlan.Create(
            availableQueueCount: 2,
            rxQueueSize: 8,
            txQueueSize: 4,
            rxBufferCount: 2,
            frameBufferBytes: VirtioNetQueuePlan.MaximumFrameBufferBytes + 1);

        return HostCheck.Result(!tooSmall.Succeeded
            && tooSmall.Status == VirtioNetQueuePlanStatus.InvalidFrameBufferSize
            && !tooLarge.Succeeded
            && tooLarge.Status == VirtioNetQueuePlanStatus.InvalidFrameBufferSize);
    }

    private static VirtioNetQueuePlanResult CreateSuccessfulPlan(ushort rxBufferCount)
    {
        return VirtioNetQueuePlan.Create(
            availableQueueCount: VirtioNetQueuePlan.MinimumQueueCount,
            rxQueueSize: 8,
            txQueueSize: 4,
            rxBufferCount: rxBufferCount,
            frameBufferBytes: VirtioNetQueuePlan.MaximumFrameBufferBytes);
    }

    [HostTest("drivers.virtionet queue plan default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetQueuePlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioNetQueuePlanStatus.Success
            && result.AvailableQueueCount == 0
            && result.RxQueueSize == 0
            && result.TxQueueSize == 0
            && result.RxBufferCount == 0
            && result.FrameBufferBytes == 0
            && result.RxQueueStatus == VirtqueueSplitRingPlanStatus.Success
            && result.TxQueueStatus == VirtqueueSplitRingPlanStatus.Success
            && result.Plan.AvailableQueueCount == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetQueuePlanResult>());
    }
}
