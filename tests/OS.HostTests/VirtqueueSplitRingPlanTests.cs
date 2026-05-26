using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class VirtqueueSplitRingPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtqueue split ring layout", TestSplitRingLayout, ref failures);
        Run("drivers.virtqueue rejects invalid queue size", TestRejectsInvalidQueueSize, ref failures);
        Run("drivers.virtqueue maps descriptor avail used offsets", TestMapsDescriptorAvailUsedOffsets, ref failures);
        Run("drivers.virtqueue rejects overflow", TestRejectsOverflow, ref failures);
        Run("drivers.virtqueue default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtqueue split-ring host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestSplitRingLayout()
    {
        VirtqueueSplitRingPlanResult result = VirtqueueSplitRingPlan.Create(queueSize: 8);
        if (!result.Succeeded || result.Status != VirtqueueSplitRingPlanStatus.Success)
        {
            return false;
        }

        VirtqueueSplitRingPlan plan = result.Plan;
        return plan.QueueSize == 8
            && plan.QueueStartOffset == 0
            && plan.DescriptorTable.Offset == 0
            && plan.DescriptorTable.Length == 128
            && plan.DescriptorTable.EndOffset == 128
            && plan.DescriptorTable.Alignment == VirtqueueSplitRingPlan.DescriptorTableAlignment
            && plan.DescriptorTable.IsAligned
            && plan.AvailableRing.Offset == 128
            && plan.AvailableRing.Length == 20
            && plan.AvailableRing.EndOffset == 148
            && plan.AvailableRing.Alignment == VirtqueueSplitRingPlan.AvailableRingAlignment
            && plan.AvailableRing.IsAligned
            && plan.UsedRing.Offset == 148
            && plan.UsedRing.Length == 68
            && plan.UsedRing.EndOffset == 216
            && plan.UsedRing.Alignment == VirtqueueSplitRingPlan.UsedRingAlignment
            && plan.UsedRing.IsAligned
            && plan.EndOffset == 216
            && plan.RequiredByteLength == 216;
    }

    private static bool TestRejectsInvalidQueueSize()
    {
        VirtqueueSplitRingPlanResult zero = VirtqueueSplitRingPlan.Create(queueSize: 0);
        VirtqueueSplitRingPlanResult notPowerOfTwo = VirtqueueSplitRingPlan.Create(queueSize: 3);
        VirtqueueSplitRingPlanResult tooLarge = VirtqueueSplitRingPlan.Create(queueSize: 65535);
        VirtqueueSplitRingPlanResult maximum = VirtqueueSplitRingPlan.Create(
            VirtqueueSplitRingPlan.MaximumQueueSize);

        return !zero.Succeeded
            && zero.Status == VirtqueueSplitRingPlanStatus.InvalidQueueSize
            && !notPowerOfTwo.Succeeded
            && notPowerOfTwo.Status == VirtqueueSplitRingPlanStatus.QueueSizeNotPowerOfTwo
            && !tooLarge.Succeeded
            && tooLarge.Status == VirtqueueSplitRingPlanStatus.QueueSizeTooLarge
            && maximum.Succeeded
            && maximum.Plan.QueueSize == VirtqueueSplitRingPlan.MaximumQueueSize;
    }

    private static bool TestMapsDescriptorAvailUsedOffsets()
    {
        VirtqueueSplitRingPlanResult result = VirtqueueSplitRingPlan.Create(
            queueStartOffset: 3,
            queueSize: 8);
        if (!result.Succeeded)
        {
            return false;
        }

        VirtqueueSplitRingPlan plan = result.Plan;
        return plan.QueueStartOffset == 3
            && plan.DescriptorTable.Offset == 16
            && plan.DescriptorTable.Length == 128
            && plan.AvailableRing.Offset == 144
            && plan.AvailableRing.Length == 20
            && plan.UsedRing.Offset == 164
            && plan.UsedRing.Length == 68
            && plan.EndOffset == 232
            && plan.RequiredByteLength == 229
            && plan.DescriptorTable.IsAligned
            && plan.AvailableRing.IsAligned
            && plan.UsedRing.IsAligned;
    }

    private static bool TestRejectsOverflow()
    {
        VirtqueueSplitRingPlanResult descriptorEndOverflow = VirtqueueSplitRingPlan.Create(
            queueStartOffset: uint.MaxValue - 15,
            queueSize: 1);
        VirtqueueSplitRingPlanResult descriptorAlignOverflow = VirtqueueSplitRingPlan.Create(
            queueStartOffset: uint.MaxValue - 1,
            queueSize: 1);

        return !descriptorEndOverflow.Succeeded
            && descriptorEndOverflow.Status == VirtqueueSplitRingPlanStatus.LayoutOverflow
            && !descriptorAlignOverflow.Succeeded
            && descriptorAlignOverflow.Status == VirtqueueSplitRingPlanStatus.LayoutOverflow;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtqueueSplitRingPlanResult result = default;
        VirtqueueSplitRingPlanResult failed =
            VirtqueueSplitRingPlanResult.Fail(
                VirtqueueSplitRingPlanStatus.InvalidQueueSize,
                queueSize: 0);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtqueueSplitRingPlanStatus.Success
            && result.QueueSize == 0
            && result.Plan.QueueSize == 0
            && result.Plan.RequiredByteLength == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtqueueSplitRingPlanStatus.InvalidQueueSize
            && failed.QueueSize == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtqueueSplitRingPlanResult>();
    }
}
