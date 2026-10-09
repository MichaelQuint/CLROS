using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class VirtioDeviceInitializationPlanTests
{
    private const ulong RequiredFeature = 1UL << 5;
    private const ulong OptionalFeature = 1UL << 9;
    private const ulong UnsupportedOptionalFeature = 1UL << 11;
    private const ulong MissingRequiredFeature = 1UL << 12;

    [HostTest("drivers.virtio init plans status sequence", HostLane.Fast, 0)]
    private static bool TestPlansStatusSequence()
    {
        VirtioDeviceInitializationPlanResult result = CreateSuccessfulPlan();
        if (!result.Succeeded ||
            result.Status != VirtioDeviceInitializationPlanStatus.Success ||
            !SequenceEquals(
                result.StatusWrites,
                VirtioDeviceInitializationPlan.AcknowledgeStatus,
                VirtioDeviceInitializationPlan.DriverStatus,
                VirtioDeviceInitializationPlan.FeaturesOkStatus,
                VirtioDeviceInitializationPlan.DriverOkStatus))
        {
            return HostCheck.Result(false);
        }

        VirtioDeviceInitializationPlanResult zeroRequiredQueues = VirtioDeviceInitializationPlan.Create(
            deviceFeatures: RequiredFeature,
            requiredFeatures: RequiredFeature,
            optionalFeatures: 0,
            availableQueueCount: 1,
            requiredQueueCount: 0,
            featuresOkAccepted: true);
        if (zeroRequiredQueues.Succeeded ||
            zeroRequiredQueues.Status != VirtioDeviceInitializationPlanStatus.InvalidRequiredQueueCount ||
            !SequenceEquals(
                zeroRequiredQueues.StatusWrites,
                VirtioDeviceInitializationPlan.AcknowledgeStatus,
                VirtioDeviceInitializationPlan.DriverStatus,
                VirtioDeviceInitializationPlan.FeaturesOkStatus,
                VirtioDeviceInitializationPlan.FailedAfterFeaturesOkStatus))
        {
            return HostCheck.Result(false);
        }

        VirtioDeviceInitializationPlanResult insufficientQueues = VirtioDeviceInitializationPlan.Create(
            deviceFeatures: RequiredFeature,
            requiredFeatures: RequiredFeature,
            optionalFeatures: 0,
            availableQueueCount: 1,
            requiredQueueCount: 2,
            featuresOkAccepted: true);

        return HostCheck.Result(!insufficientQueues.Succeeded
            && insufficientQueues.Status == VirtioDeviceInitializationPlanStatus.InsufficientQueueCount
            && insufficientQueues.AvailableQueueCount == 1
            && insufficientQueues.RequiredQueueCount == 2
            && SequenceEquals(
                insufficientQueues.StatusWrites,
                VirtioDeviceInitializationPlan.AcknowledgeStatus,
                VirtioDeviceInitializationPlan.DriverStatus,
                VirtioDeviceInitializationPlan.FeaturesOkStatus,
                VirtioDeviceInitializationPlan.FailedAfterFeaturesOkStatus));
    }

    [HostTest("drivers.virtio init rejects missing features", HostLane.Fast, 1)]
    private static bool TestRejectsMissingFeatures()
    {
        VirtioDeviceInitializationPlanResult result = VirtioDeviceInitializationPlan.Create(
            deviceFeatures: RequiredFeature,
            requiredFeatures: RequiredFeature | MissingRequiredFeature,
            optionalFeatures: OptionalFeature,
            availableQueueCount: 1,
            requiredQueueCount: 1,
            featuresOkAccepted: true);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioDeviceInitializationPlanStatus.MissingRequiredFeatures
            && result.MissingRequiredFeatures == MissingRequiredFeature
            && result.SelectedFeatures == RequiredFeature
            && SequenceEquals(
                result.StatusWrites,
                VirtioDeviceInitializationPlan.AcknowledgeStatus,
                VirtioDeviceInitializationPlan.DriverStatus,
                VirtioDeviceInitializationPlan.FailedBeforeFeaturesOkStatus));
    }

    [HostTest("drivers.virtio init rejects features-ok failure", HostLane.Fast, 2)]
    private static bool TestRejectsFeaturesOkFailure()
    {
        VirtioDeviceInitializationPlanResult result = VirtioDeviceInitializationPlan.Create(
            deviceFeatures: RequiredFeature | OptionalFeature,
            requiredFeatures: RequiredFeature,
            optionalFeatures: OptionalFeature,
            availableQueueCount: 1,
            requiredQueueCount: 1,
            featuresOkAccepted: false);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioDeviceInitializationPlanStatus.FeaturesOkRejected
            && result.MissingRequiredFeatures == 0
            && result.SelectedFeatures == (RequiredFeature | OptionalFeature)
            && SequenceEquals(
                result.StatusWrites,
                VirtioDeviceInitializationPlan.AcknowledgeStatus,
                VirtioDeviceInitializationPlan.DriverStatus,
                VirtioDeviceInitializationPlan.FeaturesOkStatus,
                VirtioDeviceInitializationPlan.FailedBeforeFeaturesOkStatus));
    }

    [HostTest("drivers.virtio init records selected features", HostLane.Fast, 3)]
    private static bool TestRecordsSelectedFeatures()
    {
        VirtioDeviceInitializationPlanResult result = VirtioDeviceInitializationPlan.Create(
            deviceFeatures: RequiredFeature | OptionalFeature,
            requiredFeatures: RequiredFeature,
            optionalFeatures: OptionalFeature | UnsupportedOptionalFeature,
            availableQueueCount: 4,
            requiredQueueCount: 2,
            featuresOkAccepted: true);

        return HostCheck.Result(result.Succeeded
            && result.SelectedFeatures == (RequiredFeature | OptionalFeature)
            && (result.SelectedFeatures & UnsupportedOptionalFeature) == 0
            && result.RequiredFeatures == RequiredFeature
            && result.OptionalFeatures == (OptionalFeature | UnsupportedOptionalFeature)
            && result.AvailableQueueCount == 4
            && result.RequiredQueueCount == 2);
    }

    [HostTest("drivers.virtio init default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioDeviceInitializationPlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioDeviceInitializationPlanStatus.Success
            && result.StatusWrites.Count == 0
            && !result.StatusWrites.TryGetStatus(0, out _)
            && result.SelectedFeatures == 0
            && result.AvailableQueueCount == 0
            && result.RequiredQueueCount == 0);
    }

    private static VirtioDeviceInitializationPlanResult CreateSuccessfulPlan()
    {
        return VirtioDeviceInitializationPlan.Create(
            deviceFeatures: RequiredFeature | OptionalFeature,
            requiredFeatures: RequiredFeature,
            optionalFeatures: OptionalFeature,
            availableQueueCount: 2,
            requiredQueueCount: 1,
            featuresOkAccepted: true);
    }

    private static bool SequenceEquals(
        VirtioDeviceStatusWriteSequence sequence,
        VirtioDeviceStatus status0,
        VirtioDeviceStatus status1,
        VirtioDeviceStatus status2)
    {
        return sequence.Count == 3
            && SequenceStatusEquals(sequence, 0, status0)
            && SequenceStatusEquals(sequence, 1, status1)
            && SequenceStatusEquals(sequence, 2, status2)
            && !sequence.TryGetStatus(3, out _);
    }

    private static bool SequenceEquals(
        VirtioDeviceStatusWriteSequence sequence,
        VirtioDeviceStatus status0,
        VirtioDeviceStatus status1,
        VirtioDeviceStatus status2,
        VirtioDeviceStatus status3)
    {
        return sequence.Count == 4
            && SequenceStatusEquals(sequence, 0, status0)
            && SequenceStatusEquals(sequence, 1, status1)
            && SequenceStatusEquals(sequence, 2, status2)
            && SequenceStatusEquals(sequence, 3, status3)
            && !sequence.TryGetStatus(4, out _);
    }

    private static bool SequenceStatusEquals(
        VirtioDeviceStatusWriteSequence sequence,
        byte index,
        VirtioDeviceStatus expected)
    {
        return sequence.TryGetStatus(index, out VirtioDeviceStatus actual)
            && actual == expected;
    }
}
