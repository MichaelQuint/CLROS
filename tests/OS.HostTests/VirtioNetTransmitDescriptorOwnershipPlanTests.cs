using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class VirtioNetTransmitDescriptorOwnershipPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ushort RxQueueSize = 8;
    private const ushort TxQueueSize = 4;
    private const ushort RxBufferCount = 3;
    private const ushort RxNotifyOffset = 3;
    private const ushort TxNotifyOffset = 5;
    private const ushort RxDeviceMaximumQueueSize = 16;
    private const ushort TxDeviceMaximumQueueSize = 8;
    private const uint NotifyOffsetMultiplier = 4;
    private const uint FrameBufferBytes = VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes;
    private const ulong TxHeaderPhysicalAddress = 0x0050_0000UL;
    private const ulong TxFramePhysicalAddress = 0x0060_0000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtionet.tx.ownership marks published descriptors in flight", TestMarksPublishedDescriptorsInFlight, ref failures);
        Run("drivers.virtionet.tx.ownership reclaims completed descriptors", TestReclaimsCompletedDescriptors, ref failures);
        Run("drivers.virtionet.tx.ownership rejects failed publish", TestRejectsFailedPublish, ref failures);
        Run("drivers.virtionet.tx.ownership rejects failed completion", TestRejectsFailedCompletion, ref failures);
        Run("drivers.virtionet.tx.ownership rejects short state table", TestRejectsShortStateTable, ref failures);
        Run("drivers.virtionet.tx.ownership rejects descriptor already in flight", TestRejectsDescriptorAlreadyInFlight, ref failures);
        Run("drivers.virtionet.tx.ownership rejects descriptor not in flight", TestRejectsDescriptorNotInFlight, ref failures);
        Run("drivers.virtionet.tx.ownership rejects duplicate completion", TestRejectsDuplicateCompletion, ref failures);
        Run("drivers.virtionet.tx.ownership preserves no-mmio boundary", TestPreservesNoMmioBoundary, ref failures);
        Run("drivers.virtionet.tx.freelist selects first free pair", TestFreeListSelectsFirstFreePair, ref failures);
        Run("drivers.virtionet.tx.freelist skips in-flight descriptors", TestFreeListSkipsInFlightDescriptors, ref failures);
        Run("drivers.virtionet.tx.freelist reuses reclaimed descriptors", TestFreeListReusesReclaimedDescriptors, ref failures);
        Run("drivers.virtionet.tx.freelist reports capacity exhaustion", TestFreeListReportsCapacityExhaustion, ref failures);
        Run("drivers.virtionet.tx.freelist rejects invalid queue state", TestFreeListRejectsInvalidQueueState, ref failures);
        Run("drivers.virtionet.tx.freelist preserves no-mmio boundary", TestFreeListPreservesNoMmioBoundary, ref failures);
        Run("drivers.virtionet.tx.publishplan publishes selected free pair", TestPublishPlanPublishesSelectedFreePair, ref failures);
        Run("drivers.virtionet.tx.publishplan skips in-flight descriptors", TestPublishPlanSkipsInFlightDescriptors, ref failures);
        Run("drivers.virtionet.tx.publishplan rejects free-list exhaustion", TestPublishPlanRejectsFreeListExhaustion, ref failures);
        Run("drivers.virtionet.tx.publishplan preserves state on publish failure", TestPublishPlanPreservesStateOnPublishFailure, ref failures);
        Run("drivers.virtionet.tx.publishplan rejects invalid ownership state", TestPublishPlanRejectsInvalidOwnershipState, ref failures);
        Run("drivers.virtionet.tx.publishplan preserves no-mmio boundary", TestPublishPlanPreservesNoMmioBoundary, ref failures);
        Run(
            "drivers.virtionet.tx.descriptor default results are not success",
            TestDescriptorDefaultResultsAreNotSuccess,
            ref failures);
        Run(
            "drivers.virtionet.tx.readiness composes setup publish notify",
            TestReadinessComposesSetupPublishNotify,
            ref failures);
        Run(
            "drivers.virtionet.tx.readiness default result is not success",
            TestReadinessDefaultResultIsNotSuccess,
            ref failures);
        Run(
            "drivers.virtionet.tx.readiness rejects failed setup execution",
            TestReadinessRejectsFailedSetupExecution,
            ref failures);
        Run(
            "drivers.virtionet.tx.readiness rejects failed publish",
            TestReadinessRejectsFailedPublish,
            ref failures);
        Run(
            "drivers.virtionet.tx.readiness rejects missing barrier",
            TestReadinessRejectsMissingBarrier,
            ref failures);
        Run(
            "drivers.virtionet.tx.readiness propagates notification failure",
            TestReadinessPropagatesNotificationFailure,
            ref failures);
        Run(
            "drivers.virtionet.tx.readiness preserves no-live-mmio boundary",
            TestReadinessPreservesNoLiveMmioBoundary,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Virtio-net TX descriptor ownership host tests failed: " + failures);
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

    private static bool TestReadinessDefaultResultIsNotSuccess()
    {
        VirtioNetTransmitReadinessResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioNetTransmitReadinessStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetTransmitReadinessResult>();
    }

    private static bool TestDescriptorDefaultResultsAreNotSuccess()
    {
        VirtioNetTransmitDescriptorFreeListResult freeList = default;
        VirtioNetTransmitDescriptorOwnershipPlanResult ownership = default;
        VirtioNetTransmitDescriptorPublishPlanResult publish = default;

        return !freeList.IsInitialized
            && !freeList.Succeeded
            && freeList.Status == VirtioNetTransmitDescriptorFreeListStatus.Success
            && !ownership.IsInitialized
            && !ownership.Succeeded
            && ownership.Status == VirtioNetTransmitDescriptorOwnershipStatus.Success
            && !publish.IsInitialized
            && !publish.Succeeded
            && publish.Status == VirtioNetTransmitDescriptorPublishPlanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetTransmitDescriptorFreeListResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetTransmitDescriptorOwnershipPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetTransmitDescriptorPublishPlanResult>();
    }

    private static bool TestMarksPublishedDescriptorsInFlight()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out _);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];

        VirtioNetTransmitDescriptorOwnershipPlanResult result =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(publish, states);

        return result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorOwnershipStatus.Success
            && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.Success
            && result.HeaderDescriptorIndex == publish.Plan.HeaderDescriptor.DescriptorIndex
            && result.FrameDescriptorIndex == publish.Plan.FrameDescriptor.DescriptorIndex
            && result.RequiredStateTableLength == 2
            && result.StateTableLength == states.Length
            && result.DescriptorsMarkedInFlight == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && states[publish.Plan.HeaderDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[publish.Plan.FrameDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[2] == VirtioNetTransmitDescriptorOwnershipState.Free
            && states[3] == VirtioNetTransmitDescriptorOwnershipState.Free;
    }

    private static bool TestReclaimsCompletedDescriptors()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        states[3] = VirtioNetTransmitDescriptorOwnershipState.InFlight;
        VirtioNetTransmitDescriptorOwnershipPlanResult mark =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(publish, states);
        VirtioNetTransmitCompletionPlanResult completion =
            CreateSuccessfulCompletion(context, publish, queueMemory, previousUsedIndex: 0);

        VirtioNetTransmitDescriptorOwnershipPlanResult reclaim =
            VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        return mark.Succeeded
            && completion.Succeeded
            && reclaim.Succeeded
            && reclaim.Status == VirtioNetTransmitDescriptorOwnershipStatus.Success
            && reclaim.CompletionPlanStatus == VirtioNetTransmitCompletionPlanStatus.Success
            && reclaim.DescriptorsFreed == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && states[publish.Plan.HeaderDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.Free
            && states[publish.Plan.FrameDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.Free
            && states[3] == VirtioNetTransmitDescriptorOwnershipState.InFlight;
    }

    private static bool TestRejectsFailedPublish()
    {
        TestContext context = CreateContext();
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        VirtioNetTransmitLifecycleResult failedPublish =
            VirtioNetTransmitLifecycle.Publish(
                context.QueuePlan,
                firstDescriptorIndex: 0,
                availableDescriptorCount: VirtioNetTransmitLifecycle.RequiredDescriptorCount,
                availableRingIndex: 0,
                txHeaderPhysicalAddress: TxHeaderPhysicalAddress,
                ethernetFramePhysicalAddress: TxFramePhysicalAddress,
                ethernetFrame: CreateEthernetFrame(),
                txHeader: [],
                queueMemory);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];

        VirtioNetTransmitDescriptorOwnershipPlanResult result =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(failedPublish, states);

        return !failedPublish.Succeeded
            && failedPublish.Status == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall
            && !result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorOwnershipStatus.TransmitPublishFailed
            && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall
            && AllStatesAre(states, VirtioNetTransmitDescriptorOwnershipState.Free);
    }

    private static bool TestRejectsFailedCompletion()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        VirtioNetTransmitDescriptorOwnershipPlanResult mark =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(publish, states);
        VirtioNetQueueNotificationExecutionResult notification =
            CreateExecutedTxNotification(context, publish);
        VirtioNetTransmitCompletionPlanResult failedCompletion =
            VirtioNetTransmitCompletionPlanner.Create(
                publish,
                notification,
                previousUsedIndex: 0,
                queueMemory);

        VirtioNetTransmitDescriptorOwnershipPlanResult result =
            VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion(failedCompletion, states);

        return mark.Succeeded
            && !failedCompletion.Succeeded
            && failedCompletion.Status == VirtioNetTransmitCompletionPlanStatus.NoCompletion
            && !result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorOwnershipStatus.CompletionPlanFailed
            && result.CompletionPlanStatus == VirtioNetTransmitCompletionPlanStatus.NoCompletion
            && states[publish.Plan.HeaderDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[publish.Plan.FrameDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.InFlight;
    }

    private static bool TestRejectsShortStateTable()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetTransmitCompletionPlanResult completion =
            CreateSuccessfulCompletion(context, publish, queueMemory, previousUsedIndex: 0);
        VirtioNetTransmitDescriptorOwnershipState[] shortStates =
            new VirtioNetTransmitDescriptorOwnershipState[1];

        VirtioNetTransmitDescriptorOwnershipPlanResult mark =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(publish, shortStates);
        VirtioNetTransmitDescriptorOwnershipPlanResult reclaim =
            VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion(completion, shortStates);

        return !mark.Succeeded
            && mark.Status == VirtioNetTransmitDescriptorOwnershipStatus.StateTableTooSmall
            && mark.RequiredStateTableLength == 2
            && mark.StateTableLength == 1
            && !reclaim.Succeeded
            && reclaim.Status == VirtioNetTransmitDescriptorOwnershipStatus.StateTableTooSmall
            && shortStates[0] == VirtioNetTransmitDescriptorOwnershipState.Free;
    }

    private static bool TestRejectsDescriptorAlreadyInFlight()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out _);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        states[publish.Plan.HeaderDescriptor.DescriptorIndex] =
            VirtioNetTransmitDescriptorOwnershipState.InFlight;

        VirtioNetTransmitDescriptorOwnershipPlanResult result =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(publish, states);

        return !result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorOwnershipStatus.DescriptorAlreadyInFlight
            && states[publish.Plan.HeaderDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[publish.Plan.FrameDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.Free;
    }

    private static bool TestRejectsDescriptorNotInFlight()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetTransmitCompletionPlanResult completion =
            CreateSuccessfulCompletion(context, publish, queueMemory, previousUsedIndex: 0);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        states[publish.Plan.HeaderDescriptor.DescriptorIndex] =
            VirtioNetTransmitDescriptorOwnershipState.InFlight;

        VirtioNetTransmitDescriptorOwnershipPlanResult result =
            VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        return !result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorOwnershipStatus.DescriptorNotInFlight
            && states[publish.Plan.HeaderDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[publish.Plan.FrameDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.Free;
    }

    private static bool TestRejectsDuplicateCompletion()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        VirtioNetTransmitDescriptorOwnershipPlanResult mark =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(publish, states);
        VirtioNetTransmitCompletionPlanResult completion =
            CreateSuccessfulCompletion(context, publish, queueMemory, previousUsedIndex: 0);
        VirtioNetTransmitDescriptorOwnershipPlanResult first =
            VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion(completion, states);
        VirtioNetTransmitDescriptorOwnershipPlanResult second =
            VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        return mark.Succeeded
            && first.Succeeded
            && !second.Succeeded
            && second.Status == VirtioNetTransmitDescriptorOwnershipStatus.DuplicateCompletion
            && states[publish.Plan.HeaderDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.Free
            && states[publish.Plan.FrameDescriptor.DescriptorIndex] ==
                VirtioNetTransmitDescriptorOwnershipState.Free;
    }

    private static bool TestPreservesNoMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        VirtioNetTransmitDescriptorOwnershipPlanResult mark =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(publish, states);
        VirtioNetTransmitCompletionPlanResult completion =
            CreateSuccessfulCompletion(context, publish, queueMemory, previousUsedIndex: 0);
        VirtioNetTransmitDescriptorOwnershipPlanResult reclaim =
            VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        return mark.Succeeded
            && reclaim.Succeeded
            && !VirtioNetTransmitDescriptorOwnershipPlan.PerformsMmio
            && !VirtioNetTransmitDescriptorOwnershipPlan.NotifiesDevice
            && !VirtioNetTransmitDescriptorOwnershipPlan.PollsDevice
            && !VirtioNetTransmitDescriptorOwnershipPlan.HandlesInterrupts
            && !VirtioNetTransmitDescriptorOwnershipPlan.PerformsDmaMapping
            && !VirtioNetTransmitDescriptorOwnershipPlan.PerformsPacketIo
            && !VirtioNetTransmitDescriptorOwnershipPlan.ReadsQueueMemory
            && VirtioNetTransmitDescriptorOwnershipPlan.MutatesCallerOwnedStateTable;
    }

    private static bool TestFreeListSelectsFirstFreePair()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];

        VirtioNetTransmitDescriptorFreeListResult result =
            VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair(
                context.QueuePlan,
                states);

        return result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorFreeListStatus.Success
            && result.QueuePlanStatus == VirtioNetQueuePlanStatus.Success
            && result.HeaderDescriptorIndex == 0
            && result.FrameDescriptorIndex == 1
            && result.FirstDescriptorIndex == 0
            && result.StateTableLength == states.Length
            && result.RequiredStateTableLength == TxQueueSize
            && result.DescriptorsRequired == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.DescriptorsScanned == 2
            && AllStatesAre(states, VirtioNetTransmitDescriptorOwnershipState.Free);
    }

    private static bool TestFreeListSkipsInFlightDescriptors()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        states[0] = VirtioNetTransmitDescriptorOwnershipState.InFlight;
        states[1] = VirtioNetTransmitDescriptorOwnershipState.InFlight;

        VirtioNetTransmitDescriptorFreeListResult result =
            VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair(
                context.QueuePlan,
                states);

        return result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorFreeListStatus.Success
            && result.HeaderDescriptorIndex == 2
            && result.FrameDescriptorIndex == 3
            && result.DescriptorsScanned == TxQueueSize
            && states[0] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[2] == VirtioNetTransmitDescriptorOwnershipState.Free
            && states[3] == VirtioNetTransmitDescriptorOwnershipState.Free;
    }

    private static bool TestFreeListReusesReclaimedDescriptors()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        VirtioNetTransmitDescriptorOwnershipPlanResult mark =
            VirtioNetTransmitDescriptorOwnershipPlan.MarkPublished(publish, states);
        VirtioNetTransmitCompletionPlanResult completion =
            CreateSuccessfulCompletion(context, publish, queueMemory, previousUsedIndex: 0);
        VirtioNetTransmitDescriptorOwnershipPlanResult reclaim =
            VirtioNetTransmitDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        VirtioNetTransmitDescriptorFreeListResult result =
            VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair(
                context.QueuePlan,
                states);

        return mark.Succeeded
            && reclaim.Succeeded
            && result.Succeeded
            && result.HeaderDescriptorIndex == publish.Plan.HeaderDescriptor.DescriptorIndex
            && result.FrameDescriptorIndex == publish.Plan.FrameDescriptor.DescriptorIndex
            && states[result.HeaderDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.Free
            && states[result.FrameDescriptorIndex] == VirtioNetTransmitDescriptorOwnershipState.Free;
    }

    private static bool TestFreeListReportsCapacityExhaustion()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
        [
            VirtioNetTransmitDescriptorOwnershipState.InFlight,
            VirtioNetTransmitDescriptorOwnershipState.Free,
            VirtioNetTransmitDescriptorOwnershipState.InFlight,
            VirtioNetTransmitDescriptorOwnershipState.Free
        ];

        VirtioNetTransmitDescriptorFreeListResult result =
            VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair(
                context.QueuePlan,
                states);

        return !result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorFreeListStatus.NoFreeDescriptorPair
            && result.RequiredStateTableLength == TxQueueSize
            && result.StateTableLength == states.Length
            && result.DescriptorsScanned == TxQueueSize
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.Free
            && states[3] == VirtioNetTransmitDescriptorOwnershipState.Free;
    }

    private static bool TestFreeListRejectsInvalidQueueState()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] invalidStates =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        invalidStates[1] = (VirtioNetTransmitDescriptorOwnershipState)99;
        VirtioNetTransmitDescriptorOwnershipState[] shortStates =
            new VirtioNetTransmitDescriptorOwnershipState[1];

        VirtioNetTransmitDescriptorFreeListResult invalidState =
            VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair(
                context.QueuePlan,
                invalidStates);
        VirtioNetTransmitDescriptorFreeListResult shortState =
            VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair(
                context.QueuePlan,
                shortStates);

        return !invalidState.Succeeded
            && invalidState.Status == VirtioNetTransmitDescriptorFreeListStatus.InvalidDescriptorState
            && invalidState.InvalidDescriptorIndex == 1
            && invalidStates[1] == (VirtioNetTransmitDescriptorOwnershipState)99
            && !shortState.Succeeded
            && shortState.Status == VirtioNetTransmitDescriptorFreeListStatus.StateTableTooSmall
            && shortState.RequiredStateTableLength == TxQueueSize
            && shortState.StateTableLength == shortStates.Length
            && shortStates[0] == VirtioNetTransmitDescriptorOwnershipState.Free;
    }

    private static bool TestFreeListPreservesNoMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];

        VirtioNetTransmitDescriptorFreeListResult result =
            VirtioNetTransmitDescriptorFreeListPlanner.SelectFreeDescriptorPair(
                context.QueuePlan,
                states);

        return result.Succeeded
            && !VirtioNetTransmitDescriptorFreeListPlanner.PerformsMmio
            && !VirtioNetTransmitDescriptorFreeListPlanner.NotifiesDevice
            && !VirtioNetTransmitDescriptorFreeListPlanner.PollsDevice
            && !VirtioNetTransmitDescriptorFreeListPlanner.HandlesInterrupts
            && !VirtioNetTransmitDescriptorFreeListPlanner.PerformsDmaMapping
            && !VirtioNetTransmitDescriptorFreeListPlanner.PerformsPacketIo
            && !VirtioNetTransmitDescriptorFreeListPlanner.ReadsQueueMemory
            && !VirtioNetTransmitDescriptorFreeListPlanner.MutatesCallerOwnedStateTable
            && AllStatesAre(states, VirtioNetTransmitDescriptorOwnershipState.Free);
    }

    private static bool TestPublishPlanPublishesSelectedFreePair()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        byte[] frame = CreateEthernetFrame();

        VirtioNetTransmitDescriptorPublishPlanResult result =
            VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList(
                context.QueuePlan,
                states,
                availableRingIndex: 11,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                frame,
                txHeader,
                queueMemory);

        return result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorPublishPlanStatus.Success
            && result.QueuePlanStatus == VirtioNetQueuePlanStatus.Success
            && result.FreeListStatus == VirtioNetTransmitDescriptorFreeListStatus.Success
            && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.Success
            && result.HeaderStatus == VirtioNetHeaderStatus.Success
            && result.OwnershipStatus == VirtioNetTransmitDescriptorOwnershipStatus.Success
            && result.TransmitPublishAttempted
            && result.OwnershipMarkAttempted
            && result.HeaderDescriptorIndex == 0
            && result.FrameDescriptorIndex == 1
            && result.TransmitPublish.HeadDescriptorIndex == 0
            && result.NextAvailableRingIndex == 12
            && result.StateTableLength == states.Length
            && result.RequiredStateTableLength == TxQueueSize
            && result.DescriptorsSelected == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.DescriptorsWritten == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.DescriptorsMarkedInFlight == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && states[0] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[2] == VirtioNetTransmitDescriptorOwnershipState.Free
            && states[3] == VirtioNetTransmitDescriptorOwnershipState.Free
            && BytesAreZero(txHeader)
            && DescriptorMatches(
                queueMemory,
                result.TransmitPublish.Plan.HeaderDescriptor.DescriptorTableOffset,
                TxHeaderPhysicalAddress,
                VirtioNetTransmitLifecycle.TxHeaderBytes,
                flags: 1,
                next: 1)
            && DescriptorMatches(
                queueMemory,
                result.TransmitPublish.Plan.FrameDescriptor.DescriptorTableOffset,
                TxFramePhysicalAddress,
                (uint)frame.Length,
                flags: 0,
                next: 0);
    }

    private static bool TestPublishPlanSkipsInFlightDescriptors()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        states[0] = VirtioNetTransmitDescriptorOwnershipState.InFlight;
        states[1] = VirtioNetTransmitDescriptorOwnershipState.InFlight;
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];

        VirtioNetTransmitDescriptorPublishPlanResult result =
            VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList(
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory);

        return result.Succeeded
            && result.HeaderDescriptorIndex == 2
            && result.FrameDescriptorIndex == 3
            && result.TransmitPublish.HeadDescriptorIndex == 2
            && result.TransmitPublish.Plan.HeaderDescriptor.NextDescriptorIndex == 3
            && states[0] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[2] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[3] == VirtioNetTransmitDescriptorOwnershipState.InFlight;
    }

    private static bool TestPublishPlanRejectsFreeListExhaustion()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
        [
            VirtioNetTransmitDescriptorOwnershipState.InFlight,
            VirtioNetTransmitDescriptorOwnershipState.Free,
            VirtioNetTransmitDescriptorOwnershipState.InFlight,
            VirtioNetTransmitDescriptorOwnershipState.Free
        ];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        Array.Fill(queueMemory, (byte)0x37);

        VirtioNetTransmitDescriptorPublishPlanResult result =
            VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList(
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory);

        return !result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorPublishPlanStatus.FreeListFailed
            && result.FreeListStatus == VirtioNetTransmitDescriptorFreeListStatus.NoFreeDescriptorPair
            && !result.TransmitPublishAttempted
            && !result.OwnershipMarkAttempted
            && result.DescriptorsSelected == 0
            && result.DescriptorsWritten == 0
            && result.DescriptorsMarkedInFlight == 0
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.Free
            && states[3] == VirtioNetTransmitDescriptorOwnershipState.Free
            && AllBytesEqual(queueMemory, 0x37);
    }

    private static bool TestPublishPlanPreservesStateOnPublishFailure()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] shortTxHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        Array.Fill(shortTxHeader, (byte)0x21);
        Array.Fill(queueMemory, (byte)0x21);

        VirtioNetTransmitDescriptorPublishPlanResult result =
            VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList(
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                shortTxHeader,
                queueMemory);

        return !result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorPublishPlanStatus.TransmitPublishFailed
            && result.FreeListStatus == VirtioNetTransmitDescriptorFreeListStatus.Success
            && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall
            && result.HeaderStatus == VirtioNetHeaderStatus.BufferTooSmall
            && result.TransmitPublishAttempted
            && !result.OwnershipMarkAttempted
            && result.HeaderDescriptorIndex == 0
            && result.FrameDescriptorIndex == 1
            && result.DescriptorsSelected == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.DescriptorsWritten == 0
            && result.DescriptorsMarkedInFlight == 0
            && AllStatesAre(states, VirtioNetTransmitDescriptorOwnershipState.Free)
            && AllBytesEqual(shortTxHeader, 0x21)
            && AllBytesEqual(queueMemory, 0x21);
    }

    private static bool TestPublishPlanRejectsInvalidOwnershipState()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        states[0] = (VirtioNetTransmitDescriptorOwnershipState)99;
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        Array.Fill(queueMemory, (byte)0x49);

        VirtioNetTransmitDescriptorPublishPlanResult result =
            VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList(
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory);

        return !result.Succeeded
            && result.Status == VirtioNetTransmitDescriptorPublishPlanStatus.FreeListFailed
            && result.FreeListStatus == VirtioNetTransmitDescriptorFreeListStatus.InvalidDescriptorState
            && result.FreeList.InvalidDescriptorIndex == 0
            && !result.TransmitPublishAttempted
            && !result.OwnershipMarkAttempted
            && states[0] == (VirtioNetTransmitDescriptorOwnershipState)99
            && AllBytesEqual(queueMemory, 0x49);
    }

    private static bool TestPublishPlanPreservesNoMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];

        VirtioNetTransmitDescriptorPublishPlanResult result =
            VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList(
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory);

        return result.Succeeded
            && !VirtioNetTransmitDescriptorPublishPlanner.PerformsMmio
            && !VirtioNetTransmitDescriptorPublishPlanner.NotifiesDevice
            && !VirtioNetTransmitDescriptorPublishPlanner.PollsDevice
            && !VirtioNetTransmitDescriptorPublishPlanner.HandlesInterrupts
            && !VirtioNetTransmitDescriptorPublishPlanner.PerformsDmaMapping
            && !VirtioNetTransmitDescriptorPublishPlanner.AllocatesDmaMemory
            && !VirtioNetTransmitDescriptorPublishPlanner.PerformsPacketIo
            && !VirtioNetTransmitDescriptorPublishPlanner.PerformsPacketTransmit
            && !VirtioNetTransmitDescriptorPublishPlanner.WaitsForUsedRingCompletion
            && !VirtioNetTransmitDescriptorPublishPlanner.ReadsQueueMemory
            && VirtioNetTransmitDescriptorPublishPlanner.WritesQueueMemory
            && VirtioNetTransmitDescriptorPublishPlanner.WritesTransmitHeader
            && VirtioNetTransmitDescriptorPublishPlanner.MutatesCallerOwnedStateTable;
    }

    private static bool TestReadinessComposesSetupPublishNotify()
    {
        TestContext context = CreateContext();
        VirtioNetQueueMmioSetupExecutionResult setupExecution = ExecuteQueueSetup(context);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeRegisterAccessor notifyAccessor = new FakeRegisterAccessor(notifyBytes);

        VirtioNetTransmitReadinessResult result =
            VirtioNetTransmitReadinessPlanner.ComposeTransmit(
                context.QueueSetup,
                setupExecution,
                context.QueuePlan,
                states,
                availableRingIndex: 11,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory,
                VirtioNetQueueNotificationBarrierReadiness.Ready(31),
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                notifyAccessor);

        int notifyOffset = checked((int)(TxNotifyOffset * NotifyOffsetMultiplier));
        return setupExecution.Succeeded
            && result.Succeeded
            && result.Status == VirtioNetTransmitReadinessStatus.Success
            && result.QueueSetupPlanStatus == VirtioNetQueueMmioSetupPlanStatus.Success
            && result.QueueSetupExecutionStatus == VirtioNetQueueMmioSetupExecutionStatus.Success
            && result.TransmitPublishPlanStatus == VirtioNetTransmitDescriptorPublishPlanStatus.Success
            && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.Success
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && result.NotificationExecutionStatus == VirtioNetQueueNotificationExecutionStatus.Success
            && result.NotificationSinkStatus == VirtioPciNotifyWriteStatus.Success
            && result.TransmitPublishAttempted
            && result.NotificationPlanAttempted
            && result.NotificationExecutionAttempted
            && result.BarrierSucceeded
            && result.BarrierSequence == 31
            && result.HeaderDescriptorIndex == 0
            && result.FrameDescriptorIndex == 1
            && result.DescriptorsWritten == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.DescriptorsMarkedInFlight == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.PublishedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == 11
            && result.NextAvailableRingIndex == 12
            && result.NotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && states[0] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && notifyAccessor.NotifyWrite16Count == 1
            && notifyAccessor.LastStatus == VirtioPciRegisterWriteStatus.Success
            && ReadUInt16(notifyBytes, notifyOffset) == VirtioNetQueuePlan.TransmitQueueIndex
            && BytesAreZero(txHeader)
            && DescriptorMatches(
                queueMemory,
                result.TransmitPublish.TransmitPublish.Plan.HeaderDescriptor.DescriptorTableOffset,
                TxHeaderPhysicalAddress,
                VirtioNetTransmitLifecycle.TxHeaderBytes,
                flags: 1,
                next: 1);
    }

    private static bool TestReadinessRejectsFailedSetupExecution()
    {
        TestContext context = CreateContext();
        VirtioNetQueueMmioSetupExecutionResult failedSetupExecution =
            VirtioNetQueueMmioSetupExecutor.Execute(
                context.QueueSetup,
                context.QueueSetupCommands,
                context.Apertures.CommonConfiguration,
                accessor: null);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeRegisterAccessor notifyAccessor = new FakeRegisterAccessor(notifyBytes);

        VirtioNetTransmitReadinessResult result =
            VirtioNetTransmitReadinessPlanner.ComposeTransmit(
                context.QueueSetup,
                failedSetupExecution,
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory,
                VirtioNetQueueNotificationBarrierReadiness.Ready(32),
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                notifyAccessor);

        return !failedSetupExecution.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetTransmitReadinessStatus.QueueSetupExecutionFailed
            && result.QueueSetupExecutionStatus == VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady
            && !result.TransmitPublishAttempted
            && !result.NotificationPlanAttempted
            && !result.NotificationExecutionAttempted
            && notifyAccessor.NotifyWrite16Count == 0
            && AllStatesAre(states, VirtioNetTransmitDescriptorOwnershipState.Free)
            && BytesAreZero(txHeader)
            && BytesAreZero(queueMemory);
    }

    private static bool TestReadinessRejectsFailedPublish()
    {
        TestContext context = CreateContext();
        VirtioNetQueueMmioSetupExecutionResult setupExecution = ExecuteQueueSetup(context);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] shortTxHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeRegisterAccessor notifyAccessor = new FakeRegisterAccessor(notifyBytes);

        VirtioNetTransmitReadinessResult result =
            VirtioNetTransmitReadinessPlanner.ComposeTransmit(
                context.QueueSetup,
                setupExecution,
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                shortTxHeader,
                queueMemory,
                VirtioNetQueueNotificationBarrierReadiness.Ready(33),
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                notifyAccessor);

        return setupExecution.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetTransmitReadinessStatus.TransmitPublishFailed
            && result.TransmitPublishPlanStatus ==
                VirtioNetTransmitDescriptorPublishPlanStatus.TransmitPublishFailed
            && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall
            && result.TransmitPublishAttempted
            && !result.NotificationPlanAttempted
            && !result.NotificationExecutionAttempted
            && notifyAccessor.NotifyWrite16Count == 0
            && AllStatesAre(states, VirtioNetTransmitDescriptorOwnershipState.Free)
            && BytesAreZero(queueMemory);
    }

    private static bool TestReadinessRejectsMissingBarrier()
    {
        TestContext context = CreateContext();
        VirtioNetQueueMmioSetupExecutionResult setupExecution = ExecuteQueueSetup(context);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeRegisterAccessor notifyAccessor = new FakeRegisterAccessor(notifyBytes);
        VirtioNetQueueNotificationBarrierReadiness missingBarrier =
            new VirtioNetQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 34);

        VirtioNetTransmitReadinessResult result =
            VirtioNetTransmitReadinessPlanner.ComposeTransmit(
                context.QueueSetup,
                setupExecution,
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory,
                missingBarrier,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                notifyAccessor);

        return !result.Succeeded
            && result.Status == VirtioNetTransmitReadinessStatus.BarrierNotReady
            && result.NotificationExecutionStatus ==
                VirtioNetQueueNotificationExecutionStatus.MemoryBarrierNotReady
            && result.NotificationSinkStatus == VirtioPciNotifyWriteStatus.NotReady
            && result.TransmitPublishAttempted
            && result.NotificationPlanAttempted
            && result.NotificationExecutionAttempted
            && !result.BarrierSucceeded
            && result.BarrierSequence == 34
            && notifyAccessor.NotifyWrite16Count == 0
            && states[0] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && states[1] == VirtioNetTransmitDescriptorOwnershipState.InFlight
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadinessPropagatesNotificationFailure()
    {
        TestContext context = CreateContext();
        VirtioNetQueueMmioSetupExecutionResult setupExecution = ExecuteQueueSetup(context);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeRegisterAccessor notifyAccessor = new FakeRegisterAccessor(notifyBytes)
        {
            NotifyFailureStatus = VirtioPciRegisterWriteStatus.WriteFailed
        };

        VirtioNetTransmitReadinessResult result =
            VirtioNetTransmitReadinessPlanner.ComposeTransmit(
                context.QueueSetup,
                setupExecution,
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory,
                VirtioNetQueueNotificationBarrierReadiness.Ready(35),
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                notifyAccessor);

        return !result.Succeeded
            && result.Status == VirtioNetTransmitReadinessStatus.NotificationExecutionFailed
            && result.NotificationExecutionStatus ==
                VirtioNetQueueNotificationExecutionStatus.SinkWriteFailed
            && result.NotificationSinkStatus == VirtioPciNotifyWriteStatus.WriteFailed
            && result.BarrierSucceeded
            && result.BarrierSequence == 35
            && notifyAccessor.NotifyWrite16Count == 1
            && notifyAccessor.LastStatus == VirtioPciRegisterWriteStatus.WriteFailed
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadinessPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetQueueMmioSetupExecutionResult setupExecution = ExecuteQueueSetup(context);
        VirtioNetTransmitDescriptorOwnershipState[] states =
            new VirtioNetTransmitDescriptorOwnershipState[TxQueueSize];
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];

        VirtioNetTransmitReadinessResult result =
            VirtioNetTransmitReadinessPlanner.ComposeTransmit(
                context.QueueSetup,
                setupExecution,
                context.QueuePlan,
                states,
                availableRingIndex: 0,
                TxHeaderPhysicalAddress,
                TxFramePhysicalAddress,
                CreateEthernetFrame(),
                txHeader,
                queueMemory,
                VirtioNetQueueNotificationBarrierReadiness.Ready(36),
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                new FakeRegisterAccessor(CreateNotifyBytes(context)));

        return result.Succeeded
            && VirtioNetTransmitReadinessPlanner.RequiresQueueSetupExecution
            && VirtioNetTransmitReadinessPlanner.RequiresBarrierReadiness
            && VirtioNetTransmitReadinessPlanner.UsesInjectedRegisterAccessor
            && VirtioNetTransmitReadinessPlanner.UsesFakeRegisterAccessorSink
            && VirtioNetTransmitReadinessPlanner.PublishesCallerOwnedDescriptors
            && VirtioNetTransmitReadinessPlanner.WritesQueueMemory
            && VirtioNetTransmitReadinessPlanner.WritesTransmitHeader
            && VirtioNetTransmitReadinessPlanner.MutatesCallerOwnedStateTable
            && !VirtioNetTransmitReadinessPlanner.PerformsLiveMmio
            && !VirtioNetTransmitReadinessPlanner.MapsBars
            && !VirtioNetTransmitReadinessPlanner.NotifiesLiveDevice
            && !VirtioNetTransmitReadinessPlanner.PollsDevice
            && !VirtioNetTransmitReadinessPlanner.HandlesInterrupts
            && !VirtioNetTransmitReadinessPlanner.PerformsDmaMapping
            && !VirtioNetTransmitReadinessPlanner.AllocatesDmaMemory
            && !VirtioNetTransmitReadinessPlanner.PerformsPacketIo
            && !VirtioNetTransmitReadinessPlanner.PerformsPacketTransmit
            && !VirtioNetTransmitReadinessPlanner.WaitsForUsedRingCompletion
            && !VirtioNetTransmitReadinessPlanner.ReadsQueueMemory
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetTransmitReadinessResult>();
    }

    private static VirtioNetTransmitLifecycleResult CreateTxPublish(
        VirtioNetQueuePlanResult queuePlan,
        out byte[] queueMemory)
    {
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        queueMemory = new byte[(int)queuePlan.Plan.TxQueue.EndOffset];

        return VirtioNetTransmitLifecycle.Publish(
            queuePlan,
            firstDescriptorIndex: 0,
            availableDescriptorCount: VirtioNetTransmitLifecycle.RequiredDescriptorCount,
            availableRingIndex: 7,
            txHeaderPhysicalAddress: TxHeaderPhysicalAddress,
            ethernetFramePhysicalAddress: TxFramePhysicalAddress,
            ethernetFrame: CreateEthernetFrame(),
            txHeader,
            queueMemory);
    }

    private static VirtioNetTransmitCompletionPlanResult CreateSuccessfulCompletion(
        TestContext context,
        VirtioNetTransmitLifecycleResult publish,
        byte[] queueMemory,
        ushort previousUsedIndex)
    {
        VirtioNetQueueNotificationExecutionResult notification =
            CreateExecutedTxNotification(context, publish);
        WriteUsedCompletion(
            publish.Plan.TxQueue,
            queueMemory,
            previousUsedIndex,
            unchecked((ushort)(previousUsedIndex + 1)),
            publish.HeadDescriptorIndex,
            usedByteCount: 0);

        return VirtioNetTransmitCompletionPlanner.Create(
            publish,
            notification,
            previousUsedIndex,
            queueMemory);
    }

    private static VirtioNetQueueNotificationExecutionResult CreateExecutedTxNotification(
        TestContext context,
        VirtioNetTransmitLifecycleResult publish)
    {
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
            context.QueueSetup,
            publish,
            context.Apertures.NotifyConfiguration,
            negotiatedFeatures: 0);

        return VirtioNetQueueNotificationExecutor.Execute(
            plan,
            VirtioNetQueueNotificationBarrierReadiness.Ready(20),
            new FakeNotifyWriteSink());
    }

    private static VirtioNetQueueMmioSetupExecutionResult ExecuteQueueSetup(
        TestContext context)
    {
        return VirtioNetQueueMmioSetupExecutor.Execute(
            context.QueueSetup,
            context.QueueSetupCommands,
            context.Apertures.CommonConfiguration,
            new FakeRegisterAccessor());
    }

    private static void WriteUsedCompletion(
        VirtqueueSplitRingPlan queue,
        byte[] queueMemory,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        uint descriptorHead,
        uint usedByteCount)
    {
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)queue.UsedRing.Offset + 2, sizeof(ushort)),
            currentUsedIndex);

        ushort slot = (ushort)(previousUsedIndex & (queue.QueueSize - 1));
        int elementOffset = (int)queue.UsedRing.Offset +
            (int)VirtqueueSplitRingPlan.UsedRingHeaderLength +
            (slot * (int)VirtqueueSplitRingPlan.UsedRingElementLength);
        EndianPrimitives.WriteUInt32LittleEndian(
            queueMemory.AsSpan(elementOffset, sizeof(uint)),
            descriptorHead);
        EndianPrimitives.WriteUInt32LittleEndian(
            queueMemory.AsSpan(elementOffset + sizeof(uint), sizeof(uint)),
            usedByteCount);
    }

    private static TestContext CreateContext()
    {
        VirtioPciBindingPlanResult bindingPlan = CreateBindingPlan();
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(bindingPlan);
        VirtioNetQueuePlanResult queuePlan = VirtioNetQueuePlan.Create(
            VirtioNetQueuePlan.MinimumQueueCount,
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes);
        if (!queuePlan.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic queue plan.");
        }

        VirtioNetDriverQueueBringUpPlanResult bringUp = VirtioNetDriverQueueBringUpPlan.Create(
            descriptor,
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes,
            queuePlan.Plan.RxQueue.RequiredByteLength,
            queuePlan.Plan.TxQueue.RequiredByteLength,
            queuePlan.Plan.RxBuffers.RequiredBufferBytes,
            queuePlan.Plan.TxFrame.RequiredBufferBytes);
        if (!bringUp.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic bring-up plan.");
        }

        VirtioNetQueueMmioSetupCommand[] commands =
            new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult queueSetup = VirtioNetQueueMmioSetupPlanner.Create(
            bringUp,
            CreateRxAddresses(),
            CreateTxAddresses(),
            CreateRxMetadata(),
            CreateTxMetadata(),
            commands);
        if (!queueSetup.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic queue setup plan.");
        }

        return new TestContext(bindingPlan.Binding.Apertures, queueSetup, commands, queuePlan);
    }

    private static byte[] CreateEthernetFrame()
    {
        byte[] frame = new byte[EthernetHeader.HeaderLength + 4];
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] = (byte)(0x20 + i);
        }

        return frame;
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateRxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(
            descriptorTablePhysicalAddress: 0x100000,
            driverRingPhysicalAddress: 0x101000,
            deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateTxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(
            descriptorTablePhysicalAddress: 0x110000,
            driverRingPhysicalAddress: 0x111000,
            deviceRingPhysicalAddress: 0x112000);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateRxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(
            RxDeviceMaximumQueueSize,
            hasNotifyOffset: true,
            RxNotifyOffset);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateTxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(
            TxDeviceMaximumQueueSize,
            hasNotifyOffset: true,
            TxNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateNetworkDescriptor(
        VirtioPciBindingPlanResult bindingPlan)
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            new VirtioDeviceDescriptorSource(
                sourceIndex: 43,
                bindingPlan,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                VirtioDeviceInitializationProfile.NetworkRequiredQueueCount,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                sourceIndex: 43,
                CreateNetworkConfiguration(
                    new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xCF),
                    VirtioNetDeviceConfig.StatusLinkUp))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic virtio-net descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId);

        return VirtioPciBindingPlan.Create(
            CreateScannedDevice(deviceId),
            CreatePciConfiguration(deviceId),
            CreateBarSizes());
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(
            segmentIndex: 2,
            new PciEcamSegment(
                basePhysicalAddress: 0xE0000000UL,
                segmentGroup: 1,
                startBus: 0,
                endBus: 0x7F),
            new PciScannedDevice(
                bus: 3,
                device: 5,
                function: 0,
                vendorId: VirtioPciDeviceDiscovery.VirtioVendorId,
                deviceId: deviceId,
                revisionId: 0,
                classCode: 0,
                subclass: 0,
                programmingInterface: 0,
                headerType: PciConfigurationHeader.Type0HeaderType,
                isMultifunction: false,
                hasCapabilitiesList: true,
                capabilityPointer: CommonOffset));
    }

    private static byte[] CreatePciConfiguration(ushort deviceId)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x00, sizeof(ushort)),
            VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, 0x80000000);
        WriteUInt32(config, 0x14, 0x80001000);
        WriteUInt32(config, 0x18, 0x80002000);
        WriteUInt32(config, 0x1C, 0x80003000);
        config[0x34] = CommonOffset;

        WriteVirtioCapability(
            config,
            CommonOffset,
            NotifyOffset,
            VirtioPciCapabilityType.CommonConfiguration,
            bar: 0,
            offsetValue: 0x20,
            length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            NotifyOffset,
            IsrOffset,
            VirtioPciCapabilityType.NotifyConfiguration,
            bar: 1,
            offsetValue: 0x80,
            length: 0x100,
            notifyOffsetMultiplier: NotifyOffsetMultiplier,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(
            config,
            IsrOffset,
            DeviceOffset,
            VirtioPciCapabilityType.IsrStatus,
            bar: 2,
            offsetValue: 0,
            length: 1,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            offsetValue: 0,
            length: 0x10,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return [0x1000, 0x1000, 0x1000, 0x1000, 0, 0];
    }

    private static byte[] CreateNetworkConfiguration(MacAddress mac, ushort status)
    {
        byte[] configuration = new byte[8];
        configuration[0] = mac.Octet0;
        configuration[1] = mac.Octet1;
        configuration[2] = mac.Octet2;
        configuration[3] = mac.Octet3;
        configuration[4] = mac.Octet4;
        configuration[5] = mac.Octet5;
        EndianPrimitives.WriteUInt16LittleEndian(configuration.AsSpan(6, sizeof(ushort)), status);
        return configuration;
    }

    private static void WriteVirtioCapability(
        Span<byte> config,
        byte offset,
        byte nextOffset,
        VirtioPciCapabilityType type,
        byte bar,
        uint offsetValue,
        uint length,
        uint notifyOffsetMultiplier,
        byte capabilityLength)
    {
        config[offset] = VirtioPciCapabilityListParser.VendorSpecificCapabilityId;
        config[offset + 1] = nextOffset;
        config[offset + 2] = capabilityLength;
        config[offset + 3] = (byte)type;
        config[offset + 4] = bar;
        WriteUInt32(config, offset + 8, offsetValue);
        WriteUInt32(config, offset + 12, length);
        if (type == VirtioPciCapabilityType.NotifyConfiguration)
        {
            WriteUInt32(config, offset + 16, notifyOffsetMultiplier);
        }
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private static byte[] CreateNotifyBytes(TestContext context)
    {
        byte[] notifyBytes = new byte[(int)context.Apertures.NotifyConfiguration.Length];
        Array.Fill(notifyBytes, (byte)0xCC);
        return notifyBytes;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(source.Slice(offset, sizeof(ushort)));
    }

    private static bool AllStatesAre(
        ReadOnlySpan<VirtioNetTransmitDescriptorOwnershipState> states,
        VirtioNetTransmitDescriptorOwnershipState expected)
    {
        for (int index = 0; index < states.Length; index++)
        {
            if (states[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool DescriptorMatches(
        ReadOnlySpan<byte> queueMemory,
        uint descriptorOffset,
        ulong expectedAddress,
        uint expectedLength,
        ushort flags,
        ushort next)
    {
        ReadOnlySpan<byte> descriptor = queueMemory.Slice((int)descriptorOffset, 16);
        return EndianPrimitives.ReadUInt64LittleEndian(descriptor.Slice(0, 8)) == expectedAddress
            && EndianPrimitives.ReadUInt32LittleEndian(descriptor.Slice(8, 4)) == expectedLength
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(12, 2)) == flags
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(14, 2)) == next;
    }

    private static bool BytesAreZero(ReadOnlySpan<byte> bytes)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllBytesEqual(
        ReadOnlySpan<byte> bytes,
        byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != value)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class FakeNotifyWriteSink : IVirtioPciNotifyWriteSink
    {
        public VirtioPciNotifyWriteStatus Write16(
            VirtioPciBarAperture aperture,
            uint relativeOffset,
            ushort value)
        {
            _ = aperture;
            _ = relativeOffset;
            _ = value;
            return VirtioPciNotifyWriteStatus.Success;
        }
    }

    private sealed class FakeRegisterAccessor : IVirtioPciRegisterAccessor
    {
        private readonly byte[]? _notifyBytes;
        private ushort _selectedQueue;

        public FakeRegisterAccessor(byte[]? notifyBytes = null)
        {
            _notifyBytes = notifyBytes;
        }

        public VirtioPciRegisterWriteStatus NotifyFailureStatus { get; init; } =
            VirtioPciRegisterWriteStatus.Success;

        public VirtioPciRegisterWriteStatus LastStatus { get; private set; } =
            VirtioPciRegisterWriteStatus.NotReady;

        public int NotifyWrite16Count { get; private set; }

        public uint LastNotifyRelativeOffset { get; private set; }

        public ushort LastNotifyValue { get; private set; }

        public VirtioPciRegisterRead16Result Read16(
            VirtioPciBarAperture aperture,
            uint relativeOffset)
        {
            if (aperture.CapabilityType != VirtioPciCapabilityType.CommonConfiguration)
            {
                return new VirtioPciRegisterRead16Result(
                    VirtioPciRegisterReadStatus.ReadFailed,
                    value: 0);
            }

            if (relativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSizeOffset)
            {
                ushort size = _selectedQueue == VirtioNetQueuePlan.ReceiveQueueIndex
                    ? RxDeviceMaximumQueueSize
                    : TxDeviceMaximumQueueSize;
                return new VirtioPciRegisterRead16Result(
                    VirtioPciRegisterReadStatus.Success,
                    size);
            }

            if (relativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueNotifyOffOffset)
            {
                ushort notifyOffset = _selectedQueue == VirtioNetQueuePlan.ReceiveQueueIndex
                    ? RxNotifyOffset
                    : TxNotifyOffset;
                return new VirtioPciRegisterRead16Result(
                    VirtioPciRegisterReadStatus.Success,
                    notifyOffset);
            }

            return new VirtioPciRegisterRead16Result(
                VirtioPciRegisterReadStatus.ReadFailed,
                value: 0);
        }

        public VirtioPciRegisterWriteStatus Write16(
            VirtioPciBarAperture aperture,
            uint relativeOffset,
            ushort value)
        {
            if (aperture.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration)
            {
                NotifyWrite16Count++;
                LastNotifyRelativeOffset = relativeOffset;
                LastNotifyValue = value;

                if (NotifyFailureStatus != VirtioPciRegisterWriteStatus.Success)
                {
                    LastStatus = NotifyFailureStatus;
                    return LastStatus;
                }

                if (_notifyBytes is null ||
                    relativeOffset > int.MaxValue ||
                    (ulong)relativeOffset + sizeof(ushort) > (ulong)_notifyBytes.Length)
                {
                    LastStatus = VirtioPciRegisterWriteStatus.RangeOutsideAperture;
                    return LastStatus;
                }

                EndianPrimitives.WriteUInt16LittleEndian(
                    _notifyBytes.AsSpan((int)relativeOffset, sizeof(ushort)),
                    value);
                LastStatus = VirtioPciRegisterWriteStatus.Success;
                return LastStatus;
            }

            if (aperture.CapabilityType != VirtioPciCapabilityType.CommonConfiguration)
            {
                return VirtioPciRegisterWriteStatus.RangeOutsideAperture;
            }

            if (relativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset)
            {
                _selectedQueue = value;
            }

            return VirtioPciRegisterWriteStatus.Success;
        }

        public VirtioPciRegisterWriteStatus Write64(
            VirtioPciBarAperture aperture,
            uint relativeOffset,
            ulong value)
        {
            _ = relativeOffset;
            _ = value;
            return aperture.CapabilityType == VirtioPciCapabilityType.CommonConfiguration
                ? VirtioPciRegisterWriteStatus.Success
                : VirtioPciRegisterWriteStatus.UnsupportedWidth;
        }
    }

    private readonly struct TestContext
    {
        public TestContext(
            VirtioPciBarApertureSet apertures,
            VirtioNetQueueMmioSetupPlanResult queueSetup,
            VirtioNetQueueMmioSetupCommand[] queueSetupCommands,
            VirtioNetQueuePlanResult queuePlan)
        {
            Apertures = apertures;
            QueueSetup = queueSetup;
            QueueSetupCommands = queueSetupCommands;
            QueuePlan = queuePlan;
        }

        public VirtioPciBarApertureSet Apertures { get; }

        public VirtioNetQueueMmioSetupPlanResult QueueSetup { get; }

        public VirtioNetQueueMmioSetupCommand[] QueueSetupCommands { get; }

        public VirtioNetQueuePlanResult QueuePlan { get; }
    }
}
