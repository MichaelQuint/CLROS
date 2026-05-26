using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class VirtioPciQueueNotificationPlanTests
{
    private const ushort AvailableQueueCount = 2;
    private const ushort TxQueueIndex = 1;
    private const ushort TxQueueNotifyOffset = 7;
    private const uint NotifyOffsetMultiplier = 4;
    private const uint NotifyLength = 0x80;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtiopci.queue.notify creates no data write plan", TestCreatesNoDataWritePlan, ref failures);
        Run("drivers.virtiopci.queue.notify rejects queue preconditions", TestRejectsQueuePreconditions, ref failures);
        Run("drivers.virtiopci.queue.notify rejects notification data features", TestRejectsNotificationDataFeatures, ref failures);
        Run("drivers.virtiopci.queue.notify rejects notify offset overflow", TestRejectsNotifyOffsetOverflow, ref failures);
        Run("drivers.virtiopci.queue.notify rejects notify range outside aperture", TestRejectsNotifyRangeOutsideAperture, ref failures);
        Run("drivers.virtiopci.queue.notify rejects invalid notify aperture", TestRejectsInvalidNotifyAperture, ref failures);
        Run("drivers.virtiopci.queue.notify default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Virtio PCI queue notification plan host tests failed: " + failures);
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

    private static bool TestCreatesNoDataWritePlan()
    {
        VirtioPciBarApertureSet apertures = CreateApertures();

        VirtioPciQueueNotificationPlanResult result =
            VirtioPciQueueNotificationPlanner.Create(
                apertures.NotifyConfiguration,
                TxQueueIndex,
                AvailableQueueCount,
                TxQueueNotifyOffset,
                negotiatedFeatures: 0,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: true);
        VirtioPciQueueNotificationPlan plan = result.Plan;

        return result.Succeeded
            && result.Status == VirtioPciQueueNotificationPlanStatus.Success
            && result.QueueIndex == TxQueueIndex
            && result.AvailableQueueCount == AvailableQueueCount
            && result.QueueNotifyOffset == TxQueueNotifyOffset
            && result.NegotiatedFeatures == 0
            && plan.Aperture.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && plan.QueueIndex == TxQueueIndex
            && plan.QueueNotifyOffset == TxQueueNotifyOffset
            && plan.NotifyOffsetMultiplier == NotifyOffsetMultiplier
            && plan.RelativeOffset == TxQueueNotifyOffset * NotifyOffsetMultiplier
            && plan.BarRelativeOffset == apertures.NotifyConfiguration.Offset + plan.RelativeOffset
            && plan.Address == apertures.NotifyConfiguration.BarBaseAddressBits + plan.BarRelativeOffset
            && plan.WriteValue == TxQueueIndex
            && plan.WriteWidthBits == 16
            && plan.WriteWidthBytes == 2
            && !plan.UsesNotificationData
            && !plan.UsesNotificationConfigData
            && !plan.PerformsMmio
            && !VirtioPciQueueNotificationPlanner.PerformsMmio
            && !VirtioPciQueueNotificationPlanner.PublishesDescriptors
            && !VirtioPciQueueNotificationPlanner.HandlesInterrupts
            && !VirtioPciQueueNotificationPlanner.PerformsPacketIo
            && !VirtioPciQueueNotificationPlanner.SupportsNotificationData
            && !VirtioPciQueueNotificationPlanner.SupportsNotificationConfigData;
    }

    private static bool TestRejectsQueuePreconditions()
    {
        VirtioPciBarApertureSet apertures = CreateApertures();

        VirtioPciQueueNotificationPlanResult invalidQueue =
            VirtioPciQueueNotificationPlanner.Create(
                apertures.NotifyConfiguration,
                queueIndex: AvailableQueueCount,
                AvailableQueueCount,
                TxQueueNotifyOffset,
                negotiatedFeatures: 0,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: true);
        VirtioPciQueueNotificationPlanResult invalidLargeQueue =
            VirtioPciQueueNotificationPlanner.Create(
                apertures.NotifyConfiguration,
                queueIndex: (uint)ushort.MaxValue + 1U,
                availableQueueCount: ushort.MaxValue,
                TxQueueNotifyOffset,
                negotiatedFeatures: 0,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: true);
        VirtioPciQueueNotificationPlanResult disabledQueue =
            VirtioPciQueueNotificationPlanner.Create(
                apertures.NotifyConfiguration,
                TxQueueIndex,
                AvailableQueueCount,
                TxQueueNotifyOffset,
                negotiatedFeatures: 0,
                queueEnabled: false,
                hasNewlyPublishedDescriptors: true);
        VirtioPciQueueNotificationPlanResult noNewDescriptors =
            VirtioPciQueueNotificationPlanner.Create(
                apertures.NotifyConfiguration,
                TxQueueIndex,
                AvailableQueueCount,
                TxQueueNotifyOffset,
                negotiatedFeatures: 0,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: false);

        return !invalidQueue.Succeeded
            && invalidQueue.Status == VirtioPciQueueNotificationPlanStatus.InvalidQueueIndex
            && invalidQueue.QueueIndex == AvailableQueueCount
            && invalidQueue.Plan.Equals(default(VirtioPciQueueNotificationPlan))
            && !invalidLargeQueue.Succeeded
            && invalidLargeQueue.Status == VirtioPciQueueNotificationPlanStatus.InvalidQueueIndex
            && invalidLargeQueue.QueueIndex == (uint)ushort.MaxValue + 1U
            && !disabledQueue.Succeeded
            && disabledQueue.Status == VirtioPciQueueNotificationPlanStatus.QueueNotEnabled
            && !noNewDescriptors.Succeeded
            && noNewDescriptors.Status == VirtioPciQueueNotificationPlanStatus.NoNewlyPublishedDescriptors;
    }

    private static bool TestRejectsNotificationDataFeatures()
    {
        VirtioPciBarApertureSet apertures = CreateApertures();

        VirtioPciQueueNotificationPlanResult notificationData =
            VirtioPciQueueNotificationPlanner.Create(
                apertures.NotifyConfiguration,
                TxQueueIndex,
                AvailableQueueCount,
                TxQueueNotifyOffset,
                VirtioPciQueueNotificationFeatureBits.NotificationData,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: true);
        VirtioPciQueueNotificationPlanResult notificationConfigData =
            VirtioPciQueueNotificationPlanner.Create(
                apertures.NotifyConfiguration,
                TxQueueIndex,
                AvailableQueueCount,
                TxQueueNotifyOffset,
                VirtioPciQueueNotificationFeatureBits.NotificationConfigData,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: true);

        return VirtioPciQueueNotificationFeatureBits.NotificationDataBit == 38
            && VirtioPciQueueNotificationFeatureBits.NotificationConfigDataBit == 39
            && VirtioPciQueueNotificationFeatureBits.NotificationData == 1UL << 38
            && VirtioPciQueueNotificationFeatureBits.NotificationConfigData == 1UL << 39
            && !notificationData.Succeeded
            && notificationData.Status ==
                VirtioPciQueueNotificationPlanStatus.UnsupportedNotificationDataFeature
            && notificationData.NegotiatedFeatures ==
                VirtioPciQueueNotificationFeatureBits.NotificationData
            && !notificationConfigData.Succeeded
            && notificationConfigData.Status ==
                VirtioPciQueueNotificationPlanStatus.UnsupportedNotificationConfigDataFeature
            && notificationConfigData.NegotiatedFeatures ==
                VirtioPciQueueNotificationFeatureBits.NotificationConfigData;
    }

    private static bool TestRejectsNotifyOffsetOverflow()
    {
        VirtioPciBarApertureSet apertures = CreateApertures(
            notifyOffsetMultiplier: 65_538);

        VirtioPciQueueNotificationPlanResult result =
            VirtioPciQueueNotificationPlanner.Create(
                apertures.NotifyConfiguration,
                TxQueueIndex,
                AvailableQueueCount,
                queueNotifyOffset: ushort.MaxValue,
                negotiatedFeatures: 0,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: true);

        return !result.Succeeded
            && result.Status == VirtioPciQueueNotificationPlanStatus.NotifyOffsetOverflow
            && result.QueueNotifyOffset == ushort.MaxValue
            && result.Aperture.NotifyOffsetMultiplier == 65_538;
    }

    private static bool TestRejectsNotifyRangeOutsideAperture()
    {
        VirtioPciBarApertureSet boundaryApertures = CreateApertures(
            notifyOffsetMultiplier: 2,
            notifyLength: NotifyLength);
        VirtioPciBarApertureSet outsideApertures = CreateApertures(
            notifyOffsetMultiplier: 1,
            notifyLength: NotifyLength);

        VirtioPciQueueNotificationPlanResult boundary =
            VirtioPciQueueNotificationPlanner.Create(
                boundaryApertures.NotifyConfiguration,
                TxQueueIndex,
                AvailableQueueCount,
                queueNotifyOffset: 63,
                negotiatedFeatures: 0,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: true);
        VirtioPciQueueNotificationPlanResult outside =
            VirtioPciQueueNotificationPlanner.Create(
                outsideApertures.NotifyConfiguration,
                TxQueueIndex,
                AvailableQueueCount,
                queueNotifyOffset: 127,
                negotiatedFeatures: 0,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: true);

        return boundary.Succeeded
            && boundary.Plan.RelativeOffset == NotifyLength - boundary.Plan.WriteWidthBytes
            && !outside.Succeeded
            && outside.Status == VirtioPciQueueNotificationPlanStatus.NotifyRangeOutsideAperture
            && outside.Aperture.Length == NotifyLength;
    }

    private static bool TestRejectsInvalidNotifyAperture()
    {
        VirtioPciBarApertureSet apertures = CreateApertures();

        VirtioPciQueueNotificationPlanResult result =
            VirtioPciQueueNotificationPlanner.Create(
                apertures.CommonConfiguration,
                TxQueueIndex,
                AvailableQueueCount,
                TxQueueNotifyOffset,
                negotiatedFeatures: 0,
                queueEnabled: true,
                hasNewlyPublishedDescriptors: true);

        return !result.Succeeded
            && result.Status == VirtioPciQueueNotificationPlanStatus.InvalidNotifyAperture
            && result.Aperture.CapabilityType == VirtioPciCapabilityType.CommonConfiguration;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioPciQueueNotificationPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioPciQueueNotificationPlanStatus.Success
            && result.Aperture.CapabilityType == default
            && result.QueueIndex == 0
            && result.AvailableQueueCount == 0
            && result.QueueNotifyOffset == 0
            && result.NegotiatedFeatures == 0
            && result.Plan.Aperture.CapabilityType == default
            && result.Plan.QueueIndex == 0
            && result.Plan.QueueNotifyOffset == 0
            && result.Plan.NotifyOffsetMultiplier == 0
            && result.Plan.RelativeOffset == 0
            && result.Plan.WriteWidthBytes ==
                VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes
            && !result.Plan.PerformsMmio
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciQueueNotificationPlanResult>();
    }

    private static VirtioPciBarApertureSet CreateApertures(
        uint notifyOffsetMultiplier = NotifyOffsetMultiplier,
        uint notifyLength = NotifyLength)
    {
        VirtioPciDeviceDiscoveryResult discovery = VirtioPciDeviceDiscoveryResult.Success(
            new PciDeviceIdentity(
                VirtioPciDeviceDiscovery.VirtioVendorId,
                VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId),
            VirtioPciDeviceKind.Network,
            VirtioPciDeviceDiscovery.NetworkDeviceId,
            VirtioPciCapabilitySet.Create(
                Capability(VirtioPciCapabilityType.CommonConfiguration, bar: 0, offset: 0x20, length: 0x40),
                Capability(
                    VirtioPciCapabilityType.NotifyConfiguration,
                    bar: 1,
                    offset: 0x40,
                    length: notifyLength,
                    notifyOffsetMultiplier),
                Capability(VirtioPciCapabilityType.IsrStatus, bar: 2, offset: 0, length: 1),
                Capability(VirtioPciCapabilityType.DeviceConfiguration, bar: 3, offset: 0, length: 0x10)));

        VirtioPciBarApertureValidationResult validation = VirtioPciBarApertureValidator.Validate(
            discovery,
            DecodeBars(
                bar0: 0x80000000,
                bar1: 0x80001000,
                bar2: 0x80002000,
                bar3: 0x80003000),
            new ulong[] { 0x1000, 0x1000, 0x1000, 0x1000, 0, 0 });

        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic notify aperture.");
        }

        return validation.Apertures;
    }

    private static VirtioPciCapabilityMetadata Capability(
        VirtioPciCapabilityType type,
        byte bar,
        uint offset,
        uint length,
        uint notifyOffsetMultiplier = 0)
    {
        return new VirtioPciCapabilityMetadata(
            type,
            bar,
            offset,
            length,
            notifyOffsetMultiplier);
    }

    private static PciBarDescriptorSet DecodeBars(
        uint bar0,
        uint bar1,
        uint bar2,
        uint bar3)
    {
        byte[] headerBytes = new byte[PciConfigurationHeader.Type0HeaderLength];
        EndianPrimitives.WriteUInt16LittleEndian(
            headerBytes.AsSpan(0x00, sizeof(ushort)),
            VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(
            headerBytes.AsSpan(0x02, sizeof(ushort)),
            (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId));
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(headerBytes, 0x10, bar0);
        WriteUInt32(headerBytes, 0x14, bar1);
        WriteUInt32(headerBytes, 0x18, bar2);
        WriteUInt32(headerBytes, 0x1C, bar3);

        PciConfigurationHeaderResult headerResult = PciConfigurationHeader.ParseType0(headerBytes);
        if (!headerResult.Succeeded)
        {
            throw new InvalidOperationException("Failed to parse deterministic PCI header.");
        }

        return PciBarDecoder.Decode(headerResult.Header).Bars;
    }

    private static void WriteUInt32(byte[] destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, sizeof(uint)), value);
    }
}
