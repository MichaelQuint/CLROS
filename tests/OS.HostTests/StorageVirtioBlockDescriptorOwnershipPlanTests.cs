using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockDescriptorOwnershipPlanTests
{
    private const ushort QueueSize = 8;
    private const ulong HeaderPhysicalAddress = 0x0010_0000UL;
    private const ulong DataPhysicalAddress = 0x0020_0000UL;
    private const ulong StatusPhysicalAddress = 0x0030_0000UL;

    [HostTest("storage.virtioblk.ownership marks published descriptors in flight", HostLane.Fast, 0)]
    private static bool TestMarksPublishedDescriptorsInFlight()
    {
        VirtioBlockReadLifecyclePublishResult publish =
            CreateSuccessfulPublish(
                firstDescriptorIndex: 2,
                previousAvailableRingIndex: 4,
                previousUsedIndex: 8,
                out _,
                out _);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        states[7] = VirtioBlockDescriptorOwnershipState.InFlight;

        VirtioBlockDescriptorOwnershipPlanResult result =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(publish, states);

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioBlockDescriptorOwnershipStatus.Success
            && result.PublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && result.RequestHeaderDescriptorIndex == 2
            && result.DataBufferDescriptorIndex == 3
            && result.StatusByteDescriptorIndex == 4
            && result.FirstDescriptorIndex == 2
            && result.RequiredStateTableLength == 5
            && result.StateTableLength == states.Length
            && result.DescriptorsMarkedInFlight == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.DescriptorsFreed == 0
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[3] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[4] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[7] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.ownership reclaims completed descriptors", HostLane.Fast, 1)]
    private static bool TestReclaimsCompletedDescriptors()
    {
        VirtioBlockReadLifecyclePublishResult publish =
            CreateSuccessfulPublish(
                firstDescriptorIndex: 1,
                previousAvailableRingIndex: 5,
                previousUsedIndex: 9,
                out byte[] queueMemory,
                out byte[] status);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        states[6] = VirtioBlockDescriptorOwnershipState.InFlight;
        VirtioBlockDescriptorOwnershipPlanResult mark =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(publish, states);
        VirtioBlockReadLifecycleCompletionResult completion =
            CompleteSuccessfulRead(publish, queueMemory, status);

        VirtioBlockDescriptorOwnershipPlanResult reclaim =
            VirtioBlockDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        return HostCheck.Result(mark.Succeeded
            && completion.Succeeded
            && reclaim.Succeeded
            && reclaim.Status == VirtioBlockDescriptorOwnershipStatus.Success
            && reclaim.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.Success
            && reclaim.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && reclaim.RequestHeaderDescriptorIndex == 1
            && reclaim.DataBufferDescriptorIndex == 2
            && reclaim.StatusByteDescriptorIndex == 3
            && reclaim.DescriptorsFreed == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && states[1] == VirtioBlockDescriptorOwnershipState.Free
            && states[2] == VirtioBlockDescriptorOwnershipState.Free
            && states[3] == VirtioBlockDescriptorOwnershipState.Free
            && states[6] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.ownership rejects failed publish", HostLane.Fast, 2)]
    private static bool TestRejectsFailedPublish()
    {
        VirtioBlockReadLifecyclePublishResult failedPublish = CreateFailedPublish();
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];

        VirtioBlockDescriptorOwnershipPlanResult result =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(failedPublish, states);

        return HostCheck.Result(!failedPublish.Succeeded
            && failedPublish.Status == VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorOwnershipStatus.PublishFailed
            && result.PublishStatus == VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed
            && result.DescriptorsMarkedInFlight == 0
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free));
    }

    [HostTest("storage.virtioblk.ownership rejects failed completion", HostLane.Fast, 3)]
    private static bool TestRejectsFailedCompletion()
    {
        VirtioBlockReadLifecyclePublishResult publish =
            CreateSuccessfulPublish(
                firstDescriptorIndex: 0,
                previousAvailableRingIndex: 2,
                previousUsedIndex: 6,
                out byte[] queueMemory,
                out byte[] status);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorOwnershipPlanResult mark =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(publish, states);
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)publish.QueuePlan.UsedRing.Offset + 2, sizeof(ushort)),
            publish.PreviousUsedIndex);

        VirtioBlockReadLifecycleCompletionResult failedCompletion =
            VirtioBlockReadLifecycle.CompleteReadRequest(
                publish,
                queueMemory,
                status);
        VirtioBlockDescriptorOwnershipPlanResult result =
            VirtioBlockDescriptorOwnershipPlan.ApplyCompletion(failedCompletion, states);

        return HostCheck.Result(mark.Succeeded
            && !failedCompletion.Succeeded
            && failedCompletion.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.NoCompletion
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorOwnershipStatus.CompletionFailed
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.CompletionFailed
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.ownership rejects short state table", HostLane.Fast, 4)]
    private static bool TestRejectsShortStateTable()
    {
        VirtioBlockReadLifecyclePublishResult publish =
            CreateSuccessfulPublish(
                firstDescriptorIndex: 2,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                out byte[] queueMemory,
                out byte[] status);
        VirtioBlockReadLifecycleCompletionResult completion =
            CompleteSuccessfulRead(publish, queueMemory, status);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[4];

        VirtioBlockDescriptorOwnershipPlanResult mark =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(publish, states);
        VirtioBlockDescriptorOwnershipPlanResult reclaim =
            VirtioBlockDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        return HostCheck.Result(!mark.Succeeded
            && mark.Status == VirtioBlockDescriptorOwnershipStatus.StateTableTooSmall
            && mark.RequiredStateTableLength == 5
            && mark.StateTableLength == states.Length
            && !reclaim.Succeeded
            && reclaim.Status == VirtioBlockDescriptorOwnershipStatus.StateTableTooSmall
            && reclaim.RequiredStateTableLength == 5
            && reclaim.StateTableLength == states.Length
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free));
    }

    [HostTest("storage.virtioblk.ownership rejects descriptor already in flight", HostLane.Fast, 5)]
    private static bool TestRejectsDescriptorAlreadyInFlight()
    {
        VirtioBlockReadLifecyclePublishResult publish =
            CreateSuccessfulPublish(
                firstDescriptorIndex: 0,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                out _,
                out _);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        states[1] = VirtioBlockDescriptorOwnershipState.InFlight;

        VirtioBlockDescriptorOwnershipPlanResult result =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(publish, states);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorOwnershipStatus.DescriptorAlreadyInFlight
            && result.RequestHeaderDescriptorIndex == 0
            && result.DataBufferDescriptorIndex == 1
            && result.StatusByteDescriptorIndex == 2
            && states[0] == VirtioBlockDescriptorOwnershipState.Free
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.Free);
    }

    [HostTest("storage.virtioblk.ownership rejects descriptor not in flight", HostLane.Fast, 6)]
    private static bool TestRejectsDescriptorNotInFlight()
    {
        VirtioBlockReadLifecyclePublishResult publish =
            CreateSuccessfulPublish(
                firstDescriptorIndex: 0,
                previousAvailableRingIndex: 1,
                previousUsedIndex: 4,
                out byte[] queueMemory,
                out byte[] status);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorOwnershipPlanResult mark =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(publish, states);
        states[1] = VirtioBlockDescriptorOwnershipState.Free;
        VirtioBlockReadLifecycleCompletionResult completion =
            CompleteSuccessfulRead(publish, queueMemory, status);

        VirtioBlockDescriptorOwnershipPlanResult result =
            VirtioBlockDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        return HostCheck.Result(mark.Succeeded
            && completion.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorOwnershipStatus.DescriptorNotInFlight
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.Free
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.ownership rejects duplicate completion", HostLane.Fast, 7)]
    private static bool TestRejectsDuplicateCompletion()
    {
        VirtioBlockReadLifecyclePublishResult publish =
            CreateSuccessfulPublish(
                firstDescriptorIndex: 0,
                previousAvailableRingIndex: 1,
                previousUsedIndex: 4,
                out byte[] queueMemory,
                out byte[] status);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorOwnershipPlanResult mark =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(publish, states);
        VirtioBlockReadLifecycleCompletionResult completion =
            CompleteSuccessfulRead(publish, queueMemory, status);
        VirtioBlockDescriptorOwnershipPlanResult firstReclaim =
            VirtioBlockDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        VirtioBlockDescriptorOwnershipPlanResult duplicate =
            VirtioBlockDescriptorOwnershipPlan.ApplyCompletion(completion, states);

        return HostCheck.Result(mark.Succeeded
            && firstReclaim.Succeeded
            && !duplicate.Succeeded
            && duplicate.Status == VirtioBlockDescriptorOwnershipStatus.DuplicateCompletion
            && states[0] == VirtioBlockDescriptorOwnershipState.Free
            && states[1] == VirtioBlockDescriptorOwnershipState.Free
            && states[2] == VirtioBlockDescriptorOwnershipState.Free);
    }

    [HostTest("storage.virtioblk.ownership preserves no-live-device boundary", HostLane.Fast, 8)]
    private static bool TestOwnershipPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockReadLifecyclePublishResult publish =
            CreateSuccessfulPublish(
                firstDescriptorIndex: 0,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                out _,
                out _);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorOwnershipPlanResult result =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(publish, states);

        return HostCheck.Result(result.Succeeded
            && !VirtioBlockDescriptorOwnershipPlan.PerformsMmio
            && !VirtioBlockDescriptorOwnershipPlan.NotifiesDevice
            && !VirtioBlockDescriptorOwnershipPlan.PollsDevice
            && !VirtioBlockDescriptorOwnershipPlan.HandlesInterrupts
            && !VirtioBlockDescriptorOwnershipPlan.PerformsDmaMapping
            && !VirtioBlockDescriptorOwnershipPlan.AllocatesDmaMemory
            && !VirtioBlockDescriptorOwnershipPlan.PerformsBlockIo
            && !VirtioBlockDescriptorOwnershipPlan.PerformsDiskRead
            && !VirtioBlockDescriptorOwnershipPlan.ReadsQueueMemory
            && !VirtioBlockDescriptorOwnershipPlan.WritesQueueMemory
            && VirtioBlockDescriptorOwnershipPlan.MutatesCallerOwnedStateTable
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorOwnershipState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorOwnershipPlanResult>());
    }

    [HostTest("storage.virtioblk.freelist selects first free triple", HostLane.Fast, 9)]
    private static bool TestFreeListSelectsFirstFreeTriple()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];

        VirtioBlockDescriptorFreeListResult result =
            VirtioBlockDescriptorFreeListPlanner.SelectFreeDescriptorTriple(
                queue,
                states);

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioBlockDescriptorFreeListStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.RequestHeaderDescriptorIndex == 0
            && result.DataBufferDescriptorIndex == 1
            && result.StatusByteDescriptorIndex == 2
            && result.FirstDescriptorIndex == 0
            && result.DescriptorsRequired == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.DescriptorsScanned == 3
            && result.RequiredStateTableLength == QueueSize
            && result.StateTableLength == states.Length
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free));
    }

    [HostTest("storage.virtioblk.freelist skips in-flight descriptors", HostLane.Fast, 10)]
    private static bool TestFreeListSkipsInFlightDescriptors()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        states[0] = VirtioBlockDescriptorOwnershipState.InFlight;
        states[1] = VirtioBlockDescriptorOwnershipState.InFlight;

        VirtioBlockDescriptorFreeListResult result =
            VirtioBlockDescriptorFreeListPlanner.SelectFreeDescriptorTriple(
                queue,
                states);

        return HostCheck.Result(result.Succeeded
            && result.RequestHeaderDescriptorIndex == 2
            && result.DataBufferDescriptorIndex == 3
            && result.StatusByteDescriptorIndex == 4
            && result.DescriptorsScanned == 5
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.freelist reports capacity exhaustion", HostLane.Fast, 11)]
    private static bool TestFreeListReportsCapacityExhaustion()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorOwnershipState[] states =
        [
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.InFlight,
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.InFlight,
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.Free
        ];

        VirtioBlockDescriptorFreeListResult result =
            VirtioBlockDescriptorFreeListPlanner.SelectFreeDescriptorTriple(
                queue,
                states);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorFreeListStatus.NoFreeDescriptorTriple
            && result.DescriptorsRequired == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.DescriptorsScanned == QueueSize
            && result.RequiredStateTableLength == QueueSize
            && result.StateTableLength == states.Length);
    }

    [HostTest("storage.virtioblk.freelist rejects invalid state", HostLane.Fast, 12)]
    private static bool TestFreeListRejectsInvalidState()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        states[2] = (VirtioBlockDescriptorOwnershipState)99;

        VirtioBlockDescriptorFreeListResult result =
            VirtioBlockDescriptorFreeListPlanner.SelectFreeDescriptorTriple(
                queue,
                states);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorFreeListStatus.InvalidDescriptorState
            && result.InvalidDescriptorIndex == 2
            && result.DescriptorsScanned == 2
            && result.RequiredStateTableLength == QueueSize
            && result.StateTableLength == states.Length
            && states[2] == (VirtioBlockDescriptorOwnershipState)99);
    }

    [HostTest("storage.virtioblk.freelist preserves no-live-device boundary", HostLane.Fast, 13)]
    private static bool TestFreeListPreservesNoLiveDeviceBoundary()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorFreeListResult result =
            VirtioBlockDescriptorFreeListPlanner.SelectFreeDescriptorTriple(
                queue,
                states);

        return HostCheck.Result(result.Succeeded
            && !VirtioBlockDescriptorFreeListPlanner.PerformsMmio
            && !VirtioBlockDescriptorFreeListPlanner.NotifiesDevice
            && !VirtioBlockDescriptorFreeListPlanner.PollsDevice
            && !VirtioBlockDescriptorFreeListPlanner.HandlesInterrupts
            && !VirtioBlockDescriptorFreeListPlanner.PerformsDmaMapping
            && !VirtioBlockDescriptorFreeListPlanner.AllocatesDmaMemory
            && !VirtioBlockDescriptorFreeListPlanner.PerformsBlockIo
            && !VirtioBlockDescriptorFreeListPlanner.PerformsDiskRead
            && !VirtioBlockDescriptorFreeListPlanner.ReadsQueueMemory
            && !VirtioBlockDescriptorFreeListPlanner.MutatesCallerOwnedStateTable
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorFreeListResult>());
    }

    [HostTest("storage.virtioblk.ownership default results are not success", HostLane.Fast, 30)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        VirtioBlockDescriptorFreeListResult freeList = default;
        VirtioBlockDescriptorOwnershipPlanResult ownership = default;
        VirtioBlockDescriptorPublishPlanResult publish = default;
        VirtioBlockDescriptorReclaimPlanResult reclaim = default;
        VirtioBlockQueueCursorPublishResult cursorPublish = default;
        VirtioBlockQueueCursorReclaimResult cursorReclaim = default;

        return HostCheck.Result(!freeList.IsInitialized
            && freeList.Status == VirtioBlockDescriptorFreeListStatus.Success
            && !freeList.Succeeded
            && !ownership.IsInitialized
            && ownership.Status == VirtioBlockDescriptorOwnershipStatus.Success
            && !ownership.Succeeded
            && !publish.IsInitialized
            && publish.Status == VirtioBlockDescriptorPublishPlanStatus.Success
            && !publish.Succeeded
            && !reclaim.IsInitialized
            && reclaim.Status == VirtioBlockDescriptorReclaimPlanStatus.Success
            && !reclaim.Succeeded
            && !cursorPublish.IsInitialized
            && cursorPublish.Status == VirtioBlockQueueCursorPublishStatus.Success
            && !cursorPublish.Succeeded
            && !cursorReclaim.IsInitialized
            && cursorReclaim.Status == VirtioBlockQueueCursorReclaimStatus.Success
            && !cursorReclaim.Succeeded
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorFreeListResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorOwnershipPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorPublishPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorReclaimPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueCursorPublishResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueCursorReclaimResult>());
    }

    [HostTest("storage.virtioblk.publishplan publishes selected free chain", HostLane.Fast, 14)]
    private static bool TestPublishPlanPublishesSelectedFreeChain()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorPublishPlanResult result =
            PublishFromFreeList(
                states,
                destinationLength: 1024,
                previousAvailableRingIndex: 7,
                previousUsedIndex: 11,
                out byte[] requestHeader,
                out byte[] queueMemory);

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioBlockDescriptorPublishPlanStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.FreeListStatus == VirtioBlockDescriptorFreeListStatus.Success
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && result.DescriptorWriterStatus == VirtioBlockDescriptorChainWriterStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && result.LifecyclePublishAttempted
            && result.OwnershipMarkAttempted
            && result.RequestHeaderDescriptorIndex == 0
            && result.DataBufferDescriptorIndex == 1
            && result.StatusByteDescriptorIndex == 2
            && result.FirstDescriptorIndex == 0
            && result.NextAvailableRingIndex == 8
            && result.StateTableLength == states.Length
            && result.RequiredStateTableLength == QueueSize
            && result.DescriptorsSelected == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.DescriptorsWritten == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.DescriptorsMarkedInFlight == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.LifecyclePublish.DescriptorHead == 0
            && result.OwnershipMark.FirstDescriptorIndex == 0
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight
            && EndianPrimitives.ReadUInt64LittleEndian(requestHeader.AsSpan(8, sizeof(ulong))) == 2
            && ReadUInt16(queueMemory, (int)result.LifecyclePublish.AvailableRingEntryOffset) == 0
            && ReadUInt16(queueMemory, (int)result.LifecyclePublish.QueuePlan.AvailableRing.Offset + 2) == 8);
    }

    [HostTest("storage.virtioblk.publishplan skips in-flight descriptors", HostLane.Fast, 15)]
    private static bool TestPublishPlanSkipsInFlightDescriptors()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        states[0] = VirtioBlockDescriptorOwnershipState.InFlight;
        states[1] = VirtioBlockDescriptorOwnershipState.InFlight;
        states[2] = VirtioBlockDescriptorOwnershipState.InFlight;

        VirtioBlockDescriptorPublishPlanResult result =
            PublishFromFreeList(
                states,
                destinationLength: 1024,
                previousAvailableRingIndex: 2,
                previousUsedIndex: 5,
                out _,
                out _);

        return HostCheck.Result(result.Succeeded
            && result.RequestHeaderDescriptorIndex == 3
            && result.DataBufferDescriptorIndex == 4
            && result.StatusByteDescriptorIndex == 5
            && result.DescriptorsSelected == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.DescriptorsMarkedInFlight == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[3] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[4] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[5] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.publishplan rejects free-list exhaustion", HostLane.Fast, 16)]
    private static bool TestPublishPlanRejectsFreeListExhaustion()
    {
        VirtioBlockDescriptorOwnershipState[] states =
        [
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.InFlight,
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.InFlight,
            VirtioBlockDescriptorOwnershipState.Free,
            VirtioBlockDescriptorOwnershipState.Free
        ];

        VirtioBlockDescriptorPublishPlanResult result =
            PublishFromFreeList(
                states,
                destinationLength: 1024,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                out byte[] requestHeader,
                out byte[] queueMemory);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorPublishPlanStatus.FreeListFailed
            && result.FreeListStatus == VirtioBlockDescriptorFreeListStatus.NoFreeDescriptorTriple
            && !result.LifecyclePublishAttempted
            && !result.OwnershipMarkAttempted
            && result.DescriptorsSelected == 0
            && result.DescriptorsWritten == 0
            && result.DescriptorsMarkedInFlight == 0
            && AllBytesEqual(requestHeader, 0)
            && AllBytesEqual(queueMemory, 0)
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[5] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.publishplan preserves state on publish failure", HostLane.Fast, 17)]
    private static bool TestPublishPlanPreservesStateOnPublishFailure()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];

        VirtioBlockDescriptorPublishPlanResult result =
            PublishFromFreeList(
                states,
                destinationLength: 511,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                out _,
                out _);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorPublishPlanStatus.LifecyclePublishFailed
            && result.FreeListStatus == VirtioBlockDescriptorFreeListStatus.Success
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed
            && result.LifecyclePublishAttempted
            && !result.OwnershipMarkAttempted
            && result.DescriptorsSelected == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.DescriptorsWritten == 0
            && result.DescriptorsMarkedInFlight == 0
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free));
    }

    [HostTest("storage.virtioblk.publishplan rejects invalid ownership state", HostLane.Fast, 18)]
    private static bool TestPublishPlanRejectsInvalidOwnershipState()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        states[0] = (VirtioBlockDescriptorOwnershipState)99;

        VirtioBlockDescriptorPublishPlanResult result =
            PublishFromFreeList(
                states,
                destinationLength: 1024,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                out _,
                out _);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorPublishPlanStatus.FreeListFailed
            && result.FreeListStatus == VirtioBlockDescriptorFreeListStatus.InvalidDescriptorState
            && !result.LifecyclePublishAttempted
            && !result.OwnershipMarkAttempted
            && result.DescriptorsSelected == 0
            && states[0] == (VirtioBlockDescriptorOwnershipState)99);
    }

    [HostTest("storage.virtioblk.publishplan preserves no-live-device boundary", HostLane.Fast, 19)]
    private static bool TestPublishPlanPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorPublishPlanResult result =
            PublishFromFreeList(
                states,
                destinationLength: 1024,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                out _,
                out _);

        return HostCheck.Result(result.Succeeded
            && !VirtioBlockDescriptorPublishPlanner.PerformsMmio
            && !VirtioBlockDescriptorPublishPlanner.NotifiesDevice
            && !VirtioBlockDescriptorPublishPlanner.PollsDevice
            && !VirtioBlockDescriptorPublishPlanner.HandlesInterrupts
            && !VirtioBlockDescriptorPublishPlanner.PerformsDmaMapping
            && !VirtioBlockDescriptorPublishPlanner.AllocatesDmaMemory
            && !VirtioBlockDescriptorPublishPlanner.PerformsBlockIo
            && !VirtioBlockDescriptorPublishPlanner.PerformsDiskRead
            && !VirtioBlockDescriptorPublishPlanner.ReadsQueueMemory
            && VirtioBlockDescriptorPublishPlanner.WritesQueueMemory
            && VirtioBlockDescriptorPublishPlanner.WritesRequestHeader
            && VirtioBlockDescriptorPublishPlanner.MutatesCallerOwnedStateTable
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorPublishPlanResult>());
    }

    [HostTest("storage.virtioblk.reclaimplan composes publish completion ownership", HostLane.Fast, 20)]
    private static bool TestReclaimPlanComposesPublishCompletionOwnership()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorPublishPlanResult publish =
            PublishFromFreeList(
                states,
                destinationLength: 1024,
                previousAvailableRingIndex: 7,
                previousUsedIndex: 11,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        WriteSuccessfulCompletion(publish, queueMemory, status);

        VirtioBlockDescriptorReclaimPlanResult result =
            VirtioBlockDescriptorReclaimPlanner.CompletePublishedRead(
                publish,
                queueMemory,
                status,
                states);

        return HostCheck.Result(publish.Succeeded
            && result.Succeeded
            && result.Status == VirtioBlockDescriptorReclaimPlanStatus.Success
            && result.PublishPlanStatus == VirtioBlockDescriptorPublishPlanStatus.Success
            && result.FreeListStatus == VirtioBlockDescriptorFreeListStatus.Success
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && result.CompletionAttempted
            && result.OwnershipReclaimAttempted
            && result.RequestHeaderDescriptorIndex == 0
            && result.DataBufferDescriptorIndex == 1
            && result.StatusByteDescriptorIndex == 2
            && result.FirstDescriptorIndex == 0
            && result.PreviousAvailableRingIndex == 7
            && result.NextAvailableRingIndex == 8
            && result.PreviousUsedIndex == 11
            && result.CurrentUsedIndex == 12
            && result.NextUsedIndex == 12
            && result.ExpectedNextUsedIndex == 12
            && result.UsedRingSlot == 3
            && result.CompletedDescriptorHead == 0
            && result.CompletedByteCount == publish.LifecyclePublish.DataByteCount
            && result.StatusByte == (byte)VirtioBlockDeviceStatus.Ok
            && result.StateTableLength == states.Length
            && result.RequiredStateTableLength == 3
            && result.DescriptorsMarkedInFlight == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.DescriptorsFreed == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.Publish.FirstDescriptorIndex == 0
            && result.Completion.CompletedDescriptorHead == 0
            && result.OwnershipReclaim.DescriptorsFreed ==
                VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free));
    }

    [HostTest("storage.virtioblk.reclaimplan rejects failed publish", HostLane.Fast, 21)]
    private static bool TestReclaimPlanRejectsFailedPublish()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorPublishPlanResult publish =
            PublishFromFreeList(
                states,
                destinationLength: 511,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 5,
                out _,
                out byte[] status,
                out byte[] queueMemory);

        VirtioBlockDescriptorReclaimPlanResult result =
            VirtioBlockDescriptorReclaimPlanner.CompletePublishedRead(
                publish,
                queueMemory,
                status,
                states);

        return HostCheck.Result(!publish.Succeeded
            && publish.Status == VirtioBlockDescriptorPublishPlanStatus.LifecyclePublishFailed
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorReclaimPlanStatus.PublishPlanFailed
            && result.PublishPlanStatus == VirtioBlockDescriptorPublishPlanStatus.LifecyclePublishFailed
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed
            && !result.CompletionAttempted
            && !result.OwnershipReclaimAttempted
            && result.PreviousUsedIndex == 5
            && result.CurrentUsedIndex == 5
            && result.DescriptorsFreed == 0
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free));
    }

    [HostTest("storage.virtioblk.reclaimplan rejects no completion", HostLane.Fast, 22)]
    private static bool TestReclaimPlanRejectsNoCompletion()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorPublishPlanResult publish =
            PublishFromFreeList(
                states,
                destinationLength: 1024,
                previousAvailableRingIndex: 1,
                previousUsedIndex: 9,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)publish.LifecyclePublish.QueuePlan.UsedRing.Offset + 2, sizeof(ushort)),
            publish.PreviousUsedIndex);

        VirtioBlockDescriptorReclaimPlanResult result =
            VirtioBlockDescriptorReclaimPlanner.CompletePublishedRead(
                publish,
                queueMemory,
                status,
                states);

        return HostCheck.Result(publish.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorReclaimPlanStatus.CompletionFailed
            && result.PublishPlanStatus == VirtioBlockDescriptorPublishPlanStatus.Success
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.CompletionFailed
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.NoCompletion
            && result.CompletionAttempted
            && !result.OwnershipReclaimAttempted
            && result.PreviousUsedIndex == 9
            && result.CurrentUsedIndex == 9
            && result.NextUsedIndex == 9
            && result.DescriptorsFreed == 0
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.reclaimplan rejects ownership mismatch", HostLane.Fast, 23)]
    private static bool TestReclaimPlanRejectsOwnershipMismatch()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorPublishPlanResult publish =
            PublishFromFreeList(
                states,
                destinationLength: 1024,
                previousAvailableRingIndex: 2,
                previousUsedIndex: 6,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        states[publish.RequestHeaderDescriptorIndex] = VirtioBlockDescriptorOwnershipState.Free;
        WriteSuccessfulCompletion(publish, queueMemory, status);

        VirtioBlockDescriptorReclaimPlanResult result =
            VirtioBlockDescriptorReclaimPlanner.CompletePublishedRead(
                publish,
                queueMemory,
                status,
                states);

        return HostCheck.Result(publish.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorReclaimPlanStatus.OwnershipReclaimFailed
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.DescriptorNotInFlight
            && result.CompletionAttempted
            && result.OwnershipReclaimAttempted
            && result.DescriptorsFreed == 0
            && states[publish.RequestHeaderDescriptorIndex] == VirtioBlockDescriptorOwnershipState.Free
            && states[publish.DataBufferDescriptorIndex] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[publish.StatusByteDescriptorIndex] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.reclaimplan preserves no-live-device boundary", HostLane.Fast, 24)]
    private static bool TestReclaimPlanPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockDescriptorPublishPlanResult publish =
            PublishFromFreeList(
                states,
                destinationLength: 1024,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 3,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        WriteSuccessfulCompletion(publish, queueMemory, status);

        VirtioBlockDescriptorReclaimPlanResult result =
            VirtioBlockDescriptorReclaimPlanner.CompletePublishedRead(
                publish,
                queueMemory,
                status,
                states);

        return HostCheck.Result(result.Succeeded
            && VirtioBlockDescriptorReclaimPlanner.RequiresSuccessfulPublishPlan
            && VirtioBlockDescriptorReclaimPlanner.RequiresCallerOwnedQueueMemory
            && VirtioBlockDescriptorReclaimPlanner.RequiresCallerOwnedStatusBuffer
            && VirtioBlockDescriptorReclaimPlanner.RequiresCallerOwnedDescriptorStateTable
            && VirtioBlockDescriptorReclaimPlanner.ReadsQueueMemory
            && VirtioBlockDescriptorReclaimPlanner.ReadsStatusBuffer
            && VirtioBlockDescriptorReclaimPlanner.MutatesCallerOwnedStateTable
            && !VirtioBlockDescriptorReclaimPlanner.PerformsMmio
            && !VirtioBlockDescriptorReclaimPlanner.MapsBars
            && !VirtioBlockDescriptorReclaimPlanner.ContainsVolatileAccess
            && !VirtioBlockDescriptorReclaimPlanner.AccessesPhysicalAddresses
            && !VirtioBlockDescriptorReclaimPlanner.ExecutesMemoryBarriers
            && !VirtioBlockDescriptorReclaimPlanner.ConfiguresLiveQueues
            && !VirtioBlockDescriptorReclaimPlanner.NotifiesLiveDevice
            && !VirtioBlockDescriptorReclaimPlanner.PollsDevice
            && !VirtioBlockDescriptorReclaimPlanner.HandlesInterrupts
            && !VirtioBlockDescriptorReclaimPlanner.PerformsDmaMapping
            && !VirtioBlockDescriptorReclaimPlanner.AllocatesDmaMemory
            && !VirtioBlockDescriptorReclaimPlanner.PerformsBlockIo
            && !VirtioBlockDescriptorReclaimPlanner.PerformsDiskRead
            && !VirtioBlockDescriptorReclaimPlanner.WaitsForUsedRingCompletion
            && !VirtioBlockDescriptorReclaimPlanner.WritesQueueMemory
            && !VirtioBlockDescriptorReclaimPlanner.WritesRequestHeader
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorReclaimPlanResult>());
    }

    [HostTest("storage.virtioblk.cursor publishes and advances available index", HostLane.Fast, 25)]
    private static bool TestCursorPublishesAndAdvancesAvailableIndex()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 7,
                usedRingIndex: 11);

        VirtioBlockQueueCursorPublishResult result =
            PublishWithCursor(
                states,
                ref cursor,
                destinationLength: 1024,
                out byte[] requestHeader,
                out _,
                out byte[] queueMemory);

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioBlockQueueCursorPublishStatus.Success
            && result.PublishPlanStatus == VirtioBlockDescriptorPublishPlanStatus.Success
            && result.FreeListStatus == VirtioBlockDescriptorFreeListStatus.Success
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && result.PreviousAvailableRingIndex == 7
            && result.CurrentAvailableRingIndex == 8
            && result.PreviousUsedRingIndex == 11
            && result.CurrentUsedRingIndex == 11
            && cursor.AvailableRingIndex == 8
            && cursor.UsedRingIndex == 11
            && result.Publish.PreviousAvailableRingIndex == 7
            && result.Publish.NextAvailableRingIndex == 8
            && result.Publish.PreviousUsedIndex == 11
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight
            && EndianPrimitives.ReadUInt64LittleEndian(requestHeader.AsSpan(8, sizeof(ulong))) == 2
            && ReadUInt16(queueMemory, (int)result.Publish.LifecyclePublish.AvailableRingEntryOffset) == 0);
    }

    [HostTest("storage.virtioblk.cursor preserves index on publish failure", HostLane.Fast, 26)]
    private static bool TestCursorPreservesIndexOnPublishFailure()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 3,
                usedRingIndex: 4);

        VirtioBlockQueueCursorPublishResult result =
            PublishWithCursor(
                states,
                ref cursor,
                destinationLength: 511,
                out byte[] requestHeader,
                out _,
                out byte[] queueMemory);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockQueueCursorPublishStatus.PublishPlanFailed
            && result.PublishPlanStatus == VirtioBlockDescriptorPublishPlanStatus.LifecyclePublishFailed
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed
            && result.PreviousAvailableRingIndex == 3
            && result.CurrentAvailableRingIndex == 3
            && result.PreviousUsedRingIndex == 4
            && result.CurrentUsedRingIndex == 4
            && cursor.AvailableRingIndex == 3
            && cursor.UsedRingIndex == 4
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && AllBytesEqual(requestHeader, 0)
            && AllBytesEqual(queueMemory, 0));
    }

    [HostTest("storage.virtioblk.cursor reclaims and advances used index", HostLane.Fast, 27)]
    private static bool TestCursorReclaimsAndAdvancesUsedIndex()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 2,
                usedRingIndex: 8);
        VirtioBlockQueueCursorPublishResult publish =
            PublishWithCursor(
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        WriteSuccessfulCompletion(publish.Publish, queueMemory, status);

        VirtioBlockQueueCursorReclaimResult reclaim =
            VirtioBlockQueueCursorPlanner.CompletePublishedRead(
                publish.Publish,
                queueMemory,
                status,
                states,
                ref cursor);

        return HostCheck.Result(publish.Succeeded
            && reclaim.Succeeded
            && reclaim.Status == VirtioBlockQueueCursorReclaimStatus.Success
            && reclaim.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.Success
            && reclaim.PublishPlanStatus == VirtioBlockDescriptorPublishPlanStatus.Success
            && reclaim.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.Success
            && reclaim.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && reclaim.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && reclaim.CompletionAttempted
            && reclaim.OwnershipReclaimAttempted
            && reclaim.PreviousAvailableRingIndex == 3
            && reclaim.CurrentAvailableRingIndex == 3
            && reclaim.PreviousUsedRingIndex == 8
            && reclaim.CurrentUsedRingIndex == 9
            && cursor.AvailableRingIndex == 3
            && cursor.UsedRingIndex == 9
            && reclaim.Reclaim.NextUsedIndex == 9
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free));
    }

    [HostTest("storage.virtioblk.cursor preserves used index on reclaim failure", HostLane.Fast, 28)]
    private static bool TestCursorPreservesUsedIndexOnReclaimFailure()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 1,
                usedRingIndex: 6);
        VirtioBlockQueueCursorPublishResult publish =
            PublishWithCursor(
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)publish.Publish.LifecyclePublish.QueuePlan.UsedRing.Offset + 2, sizeof(ushort)),
            publish.Publish.PreviousUsedIndex);

        VirtioBlockQueueCursorReclaimResult reclaim =
            VirtioBlockQueueCursorPlanner.CompletePublishedRead(
                publish.Publish,
                queueMemory,
                status,
                states,
                ref cursor);

        return HostCheck.Result(publish.Succeeded
            && !reclaim.Succeeded
            && reclaim.Status == VirtioBlockQueueCursorReclaimStatus.ReclaimPlanFailed
            && reclaim.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.CompletionFailed
            && reclaim.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.CompletionFailed
            && reclaim.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.NoCompletion
            && reclaim.CompletionAttempted
            && !reclaim.OwnershipReclaimAttempted
            && reclaim.PreviousAvailableRingIndex == 2
            && reclaim.CurrentAvailableRingIndex == 2
            && reclaim.PreviousUsedRingIndex == 6
            && reclaim.CurrentUsedRingIndex == 6
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 6
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight);
    }

    [HostTest("storage.virtioblk.cursor preserves no-live-device boundary", HostLane.Fast, 29)]
    private static bool TestCursorPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 0,
                usedRingIndex: 3);
        VirtioBlockQueueCursorPublishResult publish =
            PublishWithCursor(
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        WriteSuccessfulCompletion(publish.Publish, queueMemory, status);
        VirtioBlockQueueCursorReclaimResult reclaim =
            VirtioBlockQueueCursorPlanner.CompletePublishedRead(
                publish.Publish,
                queueMemory,
                status,
                states,
                ref cursor);

        return HostCheck.Result(publish.Succeeded
            && reclaim.Succeeded
            && VirtioBlockQueueCursorPlanner.RequiresCallerOwnedCursorState
            && VirtioBlockQueueCursorPlanner.RequiresCallerOwnedQueueMemory
            && VirtioBlockQueueCursorPlanner.RequiresCallerOwnedStatusBuffer
            && VirtioBlockQueueCursorPlanner.RequiresCallerOwnedDescriptorStateTable
            && VirtioBlockQueueCursorPlanner.ReadsQueueMemory
            && VirtioBlockQueueCursorPlanner.ReadsStatusBuffer
            && VirtioBlockQueueCursorPlanner.WritesQueueMemory
            && VirtioBlockQueueCursorPlanner.WritesRequestHeader
            && VirtioBlockQueueCursorPlanner.MutatesCallerOwnedCursorState
            && VirtioBlockQueueCursorPlanner.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockQueueCursorPlanner.PerformsMmio
            && !VirtioBlockQueueCursorPlanner.MapsBars
            && !VirtioBlockQueueCursorPlanner.ContainsVolatileAccess
            && !VirtioBlockQueueCursorPlanner.AccessesPhysicalAddresses
            && !VirtioBlockQueueCursorPlanner.ExecutesMemoryBarriers
            && !VirtioBlockQueueCursorPlanner.ConfiguresLiveQueues
            && !VirtioBlockQueueCursorPlanner.NotifiesLiveDevice
            && !VirtioBlockQueueCursorPlanner.PollsDevice
            && !VirtioBlockQueueCursorPlanner.HandlesInterrupts
            && !VirtioBlockQueueCursorPlanner.PerformsDmaMapping
            && !VirtioBlockQueueCursorPlanner.AllocatesDmaMemory
            && !VirtioBlockQueueCursorPlanner.PerformsBlockIo
            && !VirtioBlockQueueCursorPlanner.PerformsDiskRead
            && !VirtioBlockQueueCursorPlanner.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueCursorState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueCursorPublishResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueCursorReclaimResult>());
    }

    private static VirtioBlockReadLifecyclePublishResult CreateSuccessfulPublish(
        ushort firstDescriptorIndex,
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        out byte[] queueMemory,
        out byte[] status)
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[1024];
        status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        queueMemory = new byte[(int)queue.Plan.EndOffset];

        return VirtioBlockReadLifecycle.PublishReadRequest(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 64,
            firstSector: 2,
            sectorCount: 2,
            queueSize: QueueSize,
            firstDescriptorIndex,
            availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            previousAvailableRingIndex,
            previousUsedIndex,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress,
            requestHeader,
            destination,
            status,
            queueMemory);
    }

    private static VirtioBlockDescriptorPublishPlanResult PublishFromFreeList(
        Span<VirtioBlockDescriptorOwnershipState> states,
        int destinationLength,
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        out byte[] requestHeader,
        out byte[] queueMemory)
    {
        return PublishFromFreeList(
            states,
            destinationLength,
            previousAvailableRingIndex,
            previousUsedIndex,
            out requestHeader,
            out _,
            out queueMemory);
    }

    private static VirtioBlockDescriptorPublishPlanResult PublishFromFreeList(
        Span<VirtioBlockDescriptorOwnershipState> states,
        int destinationLength,
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        out byte[] requestHeader,
        out byte[] status,
        out byte[] queueMemory)
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[destinationLength];
        status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        queueMemory = new byte[(int)queue.Plan.EndOffset];

        return VirtioBlockDescriptorPublishPlanner.PublishReadFromFreeList(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 64,
            firstSector: 2,
            sectorCount: 2,
            queueSize: QueueSize,
            states,
            previousAvailableRingIndex,
            previousUsedIndex,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress,
            requestHeader,
            destination,
            status,
            queueMemory);
    }

    private static VirtioBlockQueueCursorPublishResult PublishWithCursor(
        Span<VirtioBlockDescriptorOwnershipState> states,
        ref VirtioBlockQueueCursorState cursor,
        int destinationLength,
        out byte[] requestHeader,
        out byte[] status,
        out byte[] queueMemory)
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[destinationLength];
        status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        queueMemory = new byte[(int)queue.Plan.EndOffset];

        return VirtioBlockQueueCursorPlanner.PublishReadFromFreeList(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 64,
            firstSector: 2,
            sectorCount: 2,
            queueSize: QueueSize,
            states,
            ref cursor,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress,
            requestHeader,
            destination,
            status,
            queueMemory);
    }

    private static void WriteSuccessfulCompletion(
        VirtioBlockDescriptorPublishPlanResult publish,
        byte[] queueMemory,
        byte[] status)
    {
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        WriteUsedCompletion(
            publish.LifecyclePublish.QueuePlan,
            queueMemory,
            publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            publish.LifecyclePublish.DescriptorHead,
            publish.LifecyclePublish.DataByteCount);
    }

    private static VirtioBlockReadLifecyclePublishResult CreateFailedPublish()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[511];
        byte[] status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];

        return VirtioBlockReadLifecycle.PublishReadRequest(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 64,
            firstSector: 2,
            sectorCount: 1,
            queueSize: QueueSize,
            firstDescriptorIndex: 0,
            availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            previousAvailableRingIndex: 0,
            previousUsedIndex: 0,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress,
            requestHeader,
            destination,
            status,
            queueMemory);
    }

    private static VirtioBlockReadLifecycleCompletionResult CompleteSuccessfulRead(
        VirtioBlockReadLifecyclePublishResult publish,
        byte[] queueMemory,
        byte[] status)
    {
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        WriteUsedCompletion(
            publish.QueuePlan,
            queueMemory,
            publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            publish.DescriptorHead,
            publish.DataByteCount);

        return VirtioBlockReadLifecycle.CompleteReadRequest(
            publish,
            queueMemory,
            status);
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
            queueMemory.AsSpan(elementOffset + 4, sizeof(uint)),
            usedByteCount);
    }

    private static bool AllStatesAre(
        ReadOnlySpan<VirtioBlockDescriptorOwnershipState> states,
        VirtioBlockDescriptorOwnershipState expected)
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

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static bool AllBytesEqual(
        ReadOnlySpan<byte> bytes,
        byte expected)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != expected)
            {
                return false;
            }
        }

        return true;
    }
}
