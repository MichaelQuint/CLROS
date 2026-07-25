using System.Runtime.CompilerServices;
using OS.Drivers;

internal static class VirtioDeviceInitializationProfileTests
{
    private const ulong UnsupportedDeviceFeature = 1UL << 40;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtio.profile creates block initialization plan", TestCreatesBlockInitializationPlan, ref failures);
        Run("drivers.virtio.profile creates network initialization plan", TestCreatesNetworkInitializationPlan, ref failures);
        Run("drivers.virtio.profile rejects unknown device kind", TestRejectsUnknownDeviceKind, ref failures);
        Run("drivers.virtio.profile reports insufficient queues", TestReportsInsufficientQueues, ref failures);
        Run("drivers.virtio.profile reports features-ok rejection", TestReportsFeaturesOkRejection, ref failures);
        Run("drivers.virtio.profile default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio device initialization profile host tests failed: " + failures);
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

    private static bool TestCreatesBlockInitializationPlan()
    {
        VirtioDeviceInitializationProfileResult result =
            VirtioDeviceInitializationProfile.Create(
                VirtioPciDeviceKind.Block,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask | UnsupportedDeviceFeature,
                availableQueueCount: 1,
                featuresOkAccepted: true);

        return result.Succeeded
            && result.Status == VirtioDeviceInitializationProfileStatus.Success
            && result.DeviceKind == VirtioPciDeviceKind.Block
            && result.RequiredQueueCount == VirtioDeviceInitializationProfile.BlockRequiredQueueCount
            && result.RequiredFeatures == VirtioDeviceInitializationProfile.BlockRequiredFeatureMask
            && result.OptionalFeatures == VirtioDeviceInitializationProfile.BlockOptionalFeatureMask
            && result.HasInitializationPlan
            && result.InitializationPlan.Succeeded
            && result.InitializationPlan.RequiredQueueCount
                == VirtioDeviceInitializationProfile.BlockRequiredQueueCount
            && result.InitializationPlan.SelectedFeatures
                == VirtioDeviceInitializationProfile.BlockOptionalFeatureMask
            && result.InitializationPlan.RequiredFeatures
                == VirtioDeviceInitializationProfile.NoRequiredFeatureMask
            && SequenceEquals(
                result.InitializationPlan.StatusWrites,
                VirtioDeviceInitializationPlan.AcknowledgeStatus,
                VirtioDeviceInitializationPlan.DriverStatus,
                VirtioDeviceInitializationPlan.FeaturesOkStatus,
                VirtioDeviceInitializationPlan.DriverOkStatus);
    }

    private static bool TestCreatesNetworkInitializationPlan()
    {
        VirtioDeviceInitializationProfileResult result =
            VirtioDeviceInitializationProfile.Create(
                VirtioPciDeviceKind.Network,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask | UnsupportedDeviceFeature,
                availableQueueCount: 2,
                featuresOkAccepted: true);

        return result.Succeeded
            && result.Status == VirtioDeviceInitializationProfileStatus.Success
            && result.DeviceKind == VirtioPciDeviceKind.Network
            && result.RequiredQueueCount == VirtioDeviceInitializationProfile.NetworkRequiredQueueCount
            && result.RequiredFeatures == VirtioDeviceInitializationProfile.NetworkRequiredFeatureMask
            && result.OptionalFeatures == VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask
            && result.HasInitializationPlan
            && result.InitializationPlan.Succeeded
            && result.InitializationPlan.RequiredQueueCount
                == VirtioDeviceInitializationProfile.NetworkRequiredQueueCount
            && result.InitializationPlan.SelectedFeatures
                == VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask
            && (result.InitializationPlan.SelectedFeatures & UnsupportedDeviceFeature) == 0
            && SequenceEquals(
                result.InitializationPlan.StatusWrites,
                VirtioDeviceInitializationPlan.AcknowledgeStatus,
                VirtioDeviceInitializationPlan.DriverStatus,
                VirtioDeviceInitializationPlan.FeaturesOkStatus,
                VirtioDeviceInitializationPlan.DriverOkStatus);
    }

    private static bool TestRejectsUnknownDeviceKind()
    {
        VirtioDeviceInitializationProfileResult result =
            VirtioDeviceInitializationProfile.Create(
                (VirtioPciDeviceKind)99,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                availableQueueCount: 2,
                featuresOkAccepted: true);

        return !result.Succeeded
            && result.Status == VirtioDeviceInitializationProfileStatus.UnknownDeviceKind
            && result.DeviceKind == (VirtioPciDeviceKind)99
            && result.RequiredFeatures == 0
            && result.OptionalFeatures == 0
            && result.RequiredQueueCount == 0
            && !result.HasInitializationPlan;
    }

    private static bool TestReportsInsufficientQueues()
    {
        VirtioDeviceInitializationProfileResult result =
            VirtioDeviceInitializationProfile.Create(
                VirtioPciDeviceKind.Network,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                availableQueueCount: 1,
                featuresOkAccepted: true);

        return !result.Succeeded
            && result.Status == VirtioDeviceInitializationProfileStatus.InitializationPlanFailed
            && result.HasInitializationPlan
            && result.RequiredQueueCount == VirtioDeviceInitializationProfile.NetworkRequiredQueueCount
            && result.InitializationPlan.Status
                == VirtioDeviceInitializationPlanStatus.InsufficientQueueCount
            && result.InitializationPlan.AvailableQueueCount == 1
            && result.InitializationPlan.RequiredQueueCount == 2
            && SequenceEquals(
                result.InitializationPlan.StatusWrites,
                VirtioDeviceInitializationPlan.AcknowledgeStatus,
                VirtioDeviceInitializationPlan.DriverStatus,
                VirtioDeviceInitializationPlan.FeaturesOkStatus,
                VirtioDeviceInitializationPlan.FailedAfterFeaturesOkStatus);
    }

    private static bool TestReportsFeaturesOkRejection()
    {
        VirtioDeviceInitializationProfileResult result =
            VirtioDeviceInitializationProfile.Create(
                VirtioPciDeviceKind.Block,
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                availableQueueCount: 1,
                featuresOkAccepted: false);

        return !result.Succeeded
            && result.Status == VirtioDeviceInitializationProfileStatus.InitializationPlanFailed
            && result.HasInitializationPlan
            && result.InitializationPlan.Status
                == VirtioDeviceInitializationPlanStatus.FeaturesOkRejected
            && result.InitializationPlan.MissingRequiredFeatures == 0
            && result.InitializationPlan.SelectedFeatures
                == VirtioDeviceInitializationProfile.BlockOptionalFeatureMask
            && SequenceEquals(
                result.InitializationPlan.StatusWrites,
                VirtioDeviceInitializationPlan.AcknowledgeStatus,
                VirtioDeviceInitializationPlan.DriverStatus,
                VirtioDeviceInitializationPlan.FeaturesOkStatus,
                VirtioDeviceInitializationPlan.FailedBeforeFeaturesOkStatus);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioDeviceInitializationProfileResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioDeviceInitializationProfileStatus.Success
            && result.DeviceKind == default
            && result.RequiredFeatures == 0
            && result.OptionalFeatures == 0
            && result.AvailableQueueCount == 0
            && result.RequiredQueueCount == 0
            && !result.HasInitializationPlan
            && !result.InitializationPlan.Succeeded;
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
