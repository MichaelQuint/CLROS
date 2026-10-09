using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class EnaAdminQueueLayoutPlanTests
{

    [HostTest("drivers.ena.admin.queue plans aq acq layout", HostLane.Fast, 0)]
    private static bool TestPlansAqAcqLayout()
    {
        EnaAdminQueueLayoutPlanResult result = EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 8,
            adminQueueEntrySize: 64,
            adminCompletionQueueDepth: 8,
            adminCompletionQueueEntrySize: 16,
            baseAlignment: EnaAdminQueueLayoutPlan.DefaultBaseAlignment,
            memoryBudgetBytes: 8192,
            requiresContiguousMemory: true);
        if (!result.Succeeded)
        {
            return HostCheck.Result(false);
        }

        EnaAdminQueueLayoutPlan plan = result.Plan;
        return HostCheck.Result(result.Status == EnaAdminQueueLayoutPlanStatus.Success
            && result.AdminQueueDepth == 8
            && result.AdminCompletionQueueDepth == 8
            && result.AdminQueueEntrySize == 64
            && result.AdminCompletionQueueEntrySize == 16
            && result.TotalRequiredBytes == 4224
            && plan.AdminQueue.Offset == 0
            && plan.AdminQueue.Length == 512
            && plan.AdminQueue.EndOffset == 512
            && plan.AdminQueue.Depth == 8
            && plan.AdminQueue.EntrySize == 64
            && plan.AdminQueue.IsAligned
            && plan.AdminCompletionQueue.Offset == 4096
            && plan.AdminCompletionQueue.Length == 128
            && plan.AdminCompletionQueue.EndOffset == 4224
            && plan.AdminCompletionQueue.Depth == 8
            && plan.AdminCompletionQueue.EntrySize == 16
            && plan.AdminCompletionQueue.IsAligned
            && plan.TotalRequiredBytes == 4224
            && plan.MemoryBudgetBytes == 8192
            && plan.BaseAlignment == EnaAdminQueueLayoutPlan.DefaultBaseAlignment
            && plan.RequiresContiguousMemory
            && plan.FitsBudget
            && EnaAdminQueueLayoutPlan.InitialAdminQueueProducerIndex == 0
            && EnaAdminQueueLayoutPlan.InitialAdminCompletionQueueConsumerIndex == 0
            && EnaAdminQueueLayoutPlan.InitialAdminCompletionPhase == 1);
    }

    [HostTest("drivers.ena.admin.queue rejects default result", HostLane.Fast, 5)]
    private static bool TestRejectsDefaultResult()
    {
        EnaAdminQueueLayoutPlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == EnaAdminQueueLayoutPlanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                EnaAdminQueueLayoutPlanResult>());
    }

    [HostTest("drivers.ena.admin.queue default plan does not fit budget", HostLane.Fast, 6)]
    private static bool TestDefaultPlanDoesNotFitBudget()
    {
        EnaAdminQueueLayoutPlan plan = default;

        return HostCheck.Result(!plan.FitsBudget
            && plan.TotalRequiredBytes == 0
            && plan.MemoryBudgetBytes == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<EnaAdminQueueLayoutPlan>());
    }

    [HostTest("drivers.ena.admin.queue rejects invalid depth", HostLane.Fast, 1)]
    private static bool TestRejectsInvalidDepth()
    {
        EnaAdminQueueLayoutPlanResult zeroDepth = EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 0,
            adminQueueEntrySize: 64,
            adminCompletionQueueDepth: 8,
            adminCompletionQueueEntrySize: 16,
            baseAlignment: EnaAdminQueueLayoutPlan.DefaultBaseAlignment,
            memoryBudgetBytes: 8192,
            requiresContiguousMemory: true);
        EnaAdminQueueLayoutPlanResult notPowerOfTwo = EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 6,
            adminQueueEntrySize: 64,
            adminCompletionQueueDepth: 8,
            adminCompletionQueueEntrySize: 16,
            baseAlignment: EnaAdminQueueLayoutPlan.DefaultBaseAlignment,
            memoryBudgetBytes: 8192,
            requiresContiguousMemory: false);

        return HostCheck.Result(!zeroDepth.Succeeded
            && zeroDepth.Status == EnaAdminQueueLayoutPlanStatus.InvalidDepth
            && zeroDepth.AdminQueueDepth == 0
            && zeroDepth.AdminCompletionQueueDepth == 8
            && zeroDepth.RequiresContiguousMemory
            && !notPowerOfTwo.Succeeded
            && notPowerOfTwo.Status == EnaAdminQueueLayoutPlanStatus.InvalidDepth
            && notPowerOfTwo.AdminQueueDepth == 6
            && !notPowerOfTwo.RequiresContiguousMemory);
    }

    [HostTest("drivers.ena.admin.queue rejects invalid entry size", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidEntrySize()
    {
        EnaAdminQueueLayoutPlanResult zeroEntry = EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 8,
            adminQueueEntrySize: 0,
            adminCompletionQueueDepth: 8,
            adminCompletionQueueEntrySize: 16,
            baseAlignment: EnaAdminQueueLayoutPlan.DefaultBaseAlignment,
            memoryBudgetBytes: 8192,
            requiresContiguousMemory: true);
        EnaAdminQueueLayoutPlanResult unalignedEntry = EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 8,
            adminQueueEntrySize: 63,
            adminCompletionQueueDepth: 8,
            adminCompletionQueueEntrySize: 16,
            baseAlignment: EnaAdminQueueLayoutPlan.DefaultBaseAlignment,
            memoryBudgetBytes: 8192,
            requiresContiguousMemory: true);
        EnaAdminQueueLayoutPlanResult invalidAlignment = EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 8,
            adminQueueEntrySize: 64,
            adminCompletionQueueDepth: 8,
            adminCompletionQueueEntrySize: 16,
            baseAlignment: 24,
            memoryBudgetBytes: 8192,
            requiresContiguousMemory: true);

        return HostCheck.Result(!zeroEntry.Succeeded
            && zeroEntry.Status == EnaAdminQueueLayoutPlanStatus.InvalidEntrySize
            && !unalignedEntry.Succeeded
            && unalignedEntry.Status == EnaAdminQueueLayoutPlanStatus.InvalidEntrySize
            && unalignedEntry.AdminQueueEntrySize == 63
            && !invalidAlignment.Succeeded
            && invalidAlignment.Status == EnaAdminQueueLayoutPlanStatus.InvalidAlignment
            && invalidAlignment.BaseAlignment == 24);
    }

    [HostTest("drivers.ena.admin.queue rejects insufficient memory", HostLane.Fast, 3)]
    private static bool TestRejectsInsufficientMemory()
    {
        EnaAdminQueueLayoutPlanResult result = EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 8,
            adminQueueEntrySize: 64,
            adminCompletionQueueDepth: 8,
            adminCompletionQueueEntrySize: 16,
            baseAlignment: EnaAdminQueueLayoutPlan.DefaultBaseAlignment,
            memoryBudgetBytes: 4223,
            requiresContiguousMemory: true);

        return HostCheck.Result(!result.Succeeded
            && result.Status == EnaAdminQueueLayoutPlanStatus.InsufficientMemory
            && result.TotalRequiredBytes == 4224
            && result.MemoryBudgetBytes == 4223);
    }

    [HostTest("drivers.ena.admin.queue reports arithmetic overflow", HostLane.Fast, 4)]
    private static bool TestReportsArithmeticOverflow()
    {
        EnaAdminQueueLayoutPlanResult multiplyOverflow = EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 65536,
            adminQueueEntrySize: 65536,
            adminCompletionQueueDepth: 8,
            adminCompletionQueueEntrySize: 16,
            baseAlignment: EnaAdminQueueLayoutPlan.DefaultBaseAlignment,
            memoryBudgetBytes: uint.MaxValue,
            requiresContiguousMemory: true);
        EnaAdminQueueLayoutPlanResult alignOverflow = EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 65536,
            adminQueueEntrySize: 65532,
            adminCompletionQueueDepth: 1,
            adminCompletionQueueEntrySize: 4,
            baseAlignment: 1U << 20,
            memoryBudgetBytes: uint.MaxValue,
            requiresContiguousMemory: true);

        return HostCheck.Result(!multiplyOverflow.Succeeded
            && multiplyOverflow.Status == EnaAdminQueueLayoutPlanStatus.LayoutOverflow
            && !alignOverflow.Succeeded
            && alignOverflow.Status == EnaAdminQueueLayoutPlanStatus.LayoutOverflow);
    }
}
