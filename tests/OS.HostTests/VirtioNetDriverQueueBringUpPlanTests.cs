using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;
using OS.Storage;

internal static class VirtioNetDriverQueueBringUpPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ushort RxQueueSize = 8;
    private const ushort TxQueueSize = 4;
    private const ushort RxBufferCount = 3;
    private const uint FrameBufferBytes = VirtioNetQueuePlan.MaximumFrameBufferBytes;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtionet.queue.bringup creates publish ready plan", TestCreatesPublishReadyPlan, ref failures);
        Run("drivers.virtionet.queue.bringup rejects non network descriptor", TestRejectsNonNetworkDescriptor, ref failures);
        Run("drivers.virtionet.queue.bringup rejects invalid queue plan", TestRejectsInvalidQueuePlan, ref failures);
        Run("drivers.virtionet.queue.bringup rejects split ring memory", TestRejectsSplitRingMemory, ref failures);
        Run("drivers.virtionet.queue.bringup rejects buffer capacities", TestRejectsBufferCapacities, ref failures);
        Run("drivers.virtionet.queue.bringup rejects byte count overflow", TestRejectsByteCountOverflow, ref failures);
        Run("drivers.virtionet.queue.bringup default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Virtio-net driver queue bring-up host tests failed: " + failures);
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

    private static bool TestCreatesPublishReadyPlan()
    {
        MacAddress macAddress = new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0x77);
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(
            sourceIndex: 31,
            macAddress,
            VirtioNetDeviceConfig.StatusLinkUp | VirtioNetDeviceConfig.StatusAnnounce);
        VirtioNetQueuePlan queuePlan = CreateQueuePlan().Plan;

        VirtioNetDriverQueueBringUpPlanResult result =
            VirtioNetDriverQueueBringUpPlan.Create(
                descriptor,
                RxQueueSize,
                TxQueueSize,
                RxBufferCount,
                FrameBufferBytes,
                queuePlan.RxQueue.RequiredByteLength,
                queuePlan.TxQueue.RequiredByteLength,
                queuePlan.RxBuffers.RequiredBufferBytes,
                queuePlan.TxFrame.RequiredBufferBytes);
        VirtioNetDriverQueueBringUpMetadata plan = result.Plan;

        return result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioNetDriverQueueBringUpPlanStatus.Success
            && result.DeviceKind == VirtioPciDeviceKind.Network
            && result.BindingPlanStatus == VirtioPciBindingPlanStatus.Success
            && result.NetworkConfigurationStatus == VirtioNetDeviceConfigStatus.Success
            && result.InitializationProfileStatus == VirtioDeviceInitializationProfileStatus.Success
            && result.InitializationPlanStatus == VirtioDeviceInitializationPlanStatus.Success
            && result.QueuePlanStatus == VirtioNetQueuePlanStatus.Success
            && result.RxQueueStatus == VirtqueueSplitRingPlanStatus.Success
            && result.TxQueueStatus == VirtqueueSplitRingPlanStatus.Success
            && result.SelectedFeatures == VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask
            && result.UnsupportedSelectedFeatures == 0
            && plan.SourceIndex == 31
            && plan.Binding.DeviceKind == VirtioPciDeviceKind.Network
            && plan.MacAddress == macAddress
            && plan.HasDeviceStatus
            && plan.LinkUp
            && plan.HasAnnounce
            && plan.SelectedFeatures == VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask
            && plan.SupportedFeatureMask == VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask
            && plan.AvailableQueueCount == VirtioDeviceInitializationProfile.NetworkRequiredQueueCount
            && plan.RxQueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex
            && plan.TxQueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && plan.RxQueueSize == RxQueueSize
            && plan.TxQueueSize == TxQueueSize
            && plan.RxDescriptorCount == RxBufferCount
            && plan.TxDescriptorCount == VirtioNetQueuePlan.RequiredTxDescriptorCount
            && plan.RequiredRxQueueMemoryBytes == queuePlan.RxQueue.RequiredByteLength
            && plan.RequiredTxQueueMemoryBytes == queuePlan.TxQueue.RequiredByteLength
            && plan.RequiredRxBufferBytes == queuePlan.RxBuffers.RequiredBufferBytes
            && plan.RequiredTxBufferBytes == queuePlan.TxFrame.RequiredBufferBytes
            && plan.TotalRequiredQueueMemoryBytes ==
                queuePlan.RxQueue.RequiredByteLength + queuePlan.TxQueue.RequiredByteLength
            && plan.TotalRequiredBufferBytes == queuePlan.TotalRequiredBufferBytes
            && plan.ReceivePublishReady
            && plan.TransmitPublishReady
            && plan.PublishReady
            && !VirtioNetDriverQueueBringUpPlan.WritesMmioRegisters
            && !VirtioNetDriverQueueBringUpPlan.SelectsQueuesInDevice
            && !VirtioNetDriverQueueBringUpPlan.PublishesDescriptors
            && !VirtioNetDriverQueueBringUpPlan.NotifiesDevice
            && !VirtioNetDriverQueueBringUpPlan.HandlesInterrupts
            && !VirtioNetDriverQueueBringUpPlan.SendsOrReceivesPackets;
    }

    private static bool TestRejectsNonNetworkDescriptor()
    {
        VirtioDeviceDescriptor descriptor = CreateBlockDescriptor(sourceIndex: 32);
        VirtioNetQueuePlan queuePlan = CreateQueuePlan().Plan;

        VirtioNetDriverQueueBringUpPlanResult result =
            VirtioNetDriverQueueBringUpPlan.Create(
                descriptor,
                RxQueueSize,
                TxQueueSize,
                RxBufferCount,
                FrameBufferBytes,
                queuePlan.RxQueue.RequiredByteLength,
                queuePlan.TxQueue.RequiredByteLength,
                queuePlan.RxBuffers.RequiredBufferBytes,
                queuePlan.TxFrame.RequiredBufferBytes);

        return !result.Succeeded
            && result.Status == VirtioNetDriverQueueBringUpPlanStatus.DescriptorNotNetwork
            && result.DeviceKind == VirtioPciDeviceKind.Block
            && result.BindingPlanStatus == VirtioPciBindingPlanStatus.Success
            && result.NetworkConfigurationStatus == default
            && result.QueuePlanStatus == default;
    }

    private static bool TestRejectsInvalidQueuePlan()
    {
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(
            sourceIndex: 33,
            new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0x88),
            VirtioNetDeviceConfig.StatusLinkUp);

        VirtioNetDriverQueueBringUpPlanResult result =
            VirtioNetDriverQueueBringUpPlan.Create(
                descriptor,
                rxQueueSize: 7,
                txQueueSize: TxQueueSize,
                rxBufferCount: RxBufferCount,
                frameBufferBytes: FrameBufferBytes,
                availableRxQueueMemoryBytes: uint.MaxValue,
                availableTxQueueMemoryBytes: uint.MaxValue,
                availableRxBufferBytes: uint.MaxValue,
                availableTxBufferBytes: uint.MaxValue);

        return !result.Succeeded
            && result.Status == VirtioNetDriverQueueBringUpPlanStatus.QueuePlanFailed
            && result.QueuePlanStatus == VirtioNetQueuePlanStatus.InvalidRxQueueSize
            && result.RxQueueStatus == VirtqueueSplitRingPlanStatus.QueueSizeNotPowerOfTwo
            && result.TxQueueStatus == VirtqueueSplitRingPlanStatus.Success;
    }

    private static bool TestRejectsSplitRingMemory()
    {
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(
            sourceIndex: 34,
            new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0x99),
            VirtioNetDeviceConfig.StatusLinkUp);
        VirtioNetQueuePlan queuePlan = CreateQueuePlan().Plan;

        VirtioNetDriverQueueBringUpPlanResult rxTooSmall =
            VirtioNetDriverQueueBringUpPlan.Create(
                descriptor,
                RxQueueSize,
                TxQueueSize,
                RxBufferCount,
                FrameBufferBytes,
                availableRxQueueMemoryBytes: queuePlan.RxQueue.RequiredByteLength - 1,
                availableTxQueueMemoryBytes: queuePlan.TxQueue.RequiredByteLength,
                availableRxBufferBytes: queuePlan.RxBuffers.RequiredBufferBytes,
                availableTxBufferBytes: queuePlan.TxFrame.RequiredBufferBytes);
        VirtioNetDriverQueueBringUpPlanResult txTooSmall =
            VirtioNetDriverQueueBringUpPlan.Create(
                descriptor,
                RxQueueSize,
                TxQueueSize,
                RxBufferCount,
                FrameBufferBytes,
                queuePlan.RxQueue.RequiredByteLength,
                availableTxQueueMemoryBytes: queuePlan.TxQueue.RequiredByteLength - 1,
                availableRxBufferBytes: queuePlan.RxBuffers.RequiredBufferBytes,
                availableTxBufferBytes: queuePlan.TxFrame.RequiredBufferBytes);

        return !rxTooSmall.Succeeded
            && rxTooSmall.Status == VirtioNetDriverQueueBringUpPlanStatus.RxQueueMemoryTooSmall
            && rxTooSmall.RequiredRxQueueMemoryBytes == queuePlan.RxQueue.RequiredByteLength
            && rxTooSmall.AvailableRxQueueMemoryBytes == queuePlan.RxQueue.RequiredByteLength - 1
            && !txTooSmall.Succeeded
            && txTooSmall.Status == VirtioNetDriverQueueBringUpPlanStatus.TxQueueMemoryTooSmall
            && txTooSmall.RequiredTxQueueMemoryBytes == queuePlan.TxQueue.RequiredByteLength
            && txTooSmall.AvailableTxQueueMemoryBytes == queuePlan.TxQueue.RequiredByteLength - 1;
    }

    private static bool TestRejectsBufferCapacities()
    {
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(
            sourceIndex: 35,
            new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xAA),
            VirtioNetDeviceConfig.StatusLinkUp);
        VirtioNetQueuePlan queuePlan = CreateQueuePlan().Plan;

        VirtioNetDriverQueueBringUpPlanResult rxTooSmall =
            VirtioNetDriverQueueBringUpPlan.Create(
                descriptor,
                RxQueueSize,
                TxQueueSize,
                RxBufferCount,
                FrameBufferBytes,
                queuePlan.RxQueue.RequiredByteLength,
                queuePlan.TxQueue.RequiredByteLength,
                availableRxBufferBytes: queuePlan.RxBuffers.RequiredBufferBytes - 1,
                availableTxBufferBytes: queuePlan.TxFrame.RequiredBufferBytes);
        VirtioNetDriverQueueBringUpPlanResult txTooSmall =
            VirtioNetDriverQueueBringUpPlan.Create(
                descriptor,
                RxQueueSize,
                TxQueueSize,
                RxBufferCount,
                FrameBufferBytes,
                queuePlan.RxQueue.RequiredByteLength,
                queuePlan.TxQueue.RequiredByteLength,
                queuePlan.RxBuffers.RequiredBufferBytes,
                availableTxBufferBytes: queuePlan.TxFrame.RequiredBufferBytes - 1);

        return !rxTooSmall.Succeeded
            && rxTooSmall.Status == VirtioNetDriverQueueBringUpPlanStatus.RxBufferCapacityTooSmall
            && rxTooSmall.RequiredRxBufferBytes == queuePlan.RxBuffers.RequiredBufferBytes
            && rxTooSmall.AvailableRxBufferBytes == queuePlan.RxBuffers.RequiredBufferBytes - 1
            && !txTooSmall.Succeeded
            && txTooSmall.Status == VirtioNetDriverQueueBringUpPlanStatus.TxBufferCapacityTooSmall
            && txTooSmall.RequiredTxBufferBytes == queuePlan.TxFrame.RequiredBufferBytes
            && txTooSmall.AvailableTxBufferBytes == queuePlan.TxFrame.RequiredBufferBytes - 1;
    }

    private static bool TestRejectsByteCountOverflow()
    {
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(
            sourceIndex: 36,
            new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xBB),
            VirtioNetDeviceConfig.StatusLinkUp);
        VirtioNetQueuePlan queuePlan = CreateQueuePlan().Plan;

        VirtioNetDriverQueueBringUpPlanResult result =
            VirtioNetDriverQueueBringUpPlan.Create(
                descriptor,
                RxQueueSize,
                TxQueueSize,
                RxBufferCount,
                FrameBufferBytes,
                queuePlan.RxQueue.RequiredByteLength,
                queuePlan.TxQueue.RequiredByteLength,
                availableRxBufferBytes: uint.MaxValue,
                availableTxBufferBytes: 1);

        return !result.Succeeded
            && result.Status == VirtioNetDriverQueueBringUpPlanStatus.ByteCountOverflow
            && result.RequiredRxQueueMemoryBytes == queuePlan.RxQueue.RequiredByteLength
            && result.RequiredTxQueueMemoryBytes == queuePlan.TxQueue.RequiredByteLength
            && result.AvailableRxBufferBytes == uint.MaxValue
            && result.AvailableTxBufferBytes == 1;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetDriverQueueBringUpPlanResult result = default;

        return !result.IsInitialized
            && result.Status == VirtioNetDriverQueueBringUpPlanStatus.Success
            && !result.Succeeded
            && !result.Plan.PublishReady
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetDriverQueueBringUpPlanResult>();
    }

    private static VirtioNetQueuePlanResult CreateQueuePlan()
    {
        return VirtioNetQueuePlan.Create(
            VirtioNetQueuePlan.MinimumQueueCount,
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes);
    }

    private static VirtioDeviceDescriptor CreateNetworkDescriptor(
        int sourceIndex,
        MacAddress macAddress,
        ushort statusBits)
    {
        return CreateDescriptor(
            sourceIndex,
            VirtioPciDeviceDiscovery.NetworkDeviceId,
            VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
            VirtioDeviceInitializationProfile.NetworkRequiredQueueCount,
            CreateNetworkConfiguration(macAddress, statusBits));
    }

    private static VirtioDeviceDescriptor CreateBlockDescriptor(int sourceIndex)
    {
        return CreateDescriptor(
            sourceIndex,
            VirtioPciDeviceDiscovery.BlockDeviceId,
            VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
            VirtioDeviceInitializationProfile.BlockRequiredQueueCount,
            CreateBlockConfiguration(capacitySectors: 64));
    }

    private static VirtioDeviceDescriptor CreateDescriptor(
        int sourceIndex,
        byte virtioDeviceId,
        ulong features,
        ushort availableQueueCount,
        byte[] configuration)
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            new VirtioDeviceDescriptorSource(
                sourceIndex,
                CreateBindingPlan(virtioDeviceId),
                features,
                availableQueueCount,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(sourceIndex, configuration)
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan(byte virtioDeviceId)
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + virtioDeviceId);
        uint deviceConfigurationLength = virtioDeviceId == VirtioPciDeviceDiscovery.NetworkDeviceId
            ? VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength
            : VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength;

        return VirtioPciBindingPlan.Create(
            CreateScannedDevice(deviceId),
            CreatePciConfiguration(deviceId, deviceConfigurationLength),
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

    private static byte[] CreatePciConfiguration(
        ushort deviceId,
        uint deviceConfigurationLength)
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
            offset: 0x20,
            VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            NotifyOffset,
            IsrOffset,
            VirtioPciCapabilityType.NotifyConfiguration,
            bar: 1,
            offset: 0x80,
            length: 0x100,
            notifyOffsetMultiplier: 4,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(
            config,
            IsrOffset,
            DeviceOffset,
            VirtioPciCapabilityType.IsrStatus,
            bar: 2,
            offset: 0,
            VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            offset: 0x40,
            deviceConfigurationLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return
        [
            0x1000,
            0x1000,
            0x1000,
            0x1000,
            0,
            0
        ];
    }

    private static byte[] CreateNetworkConfiguration(
        MacAddress macAddress,
        ushort statusBits)
    {
        byte[] config = new byte[VirtioNetDeviceConfig.StatusConfigLength];
        macAddress.TryWrite(config, VirtioNetDeviceConfig.MacAddressOffset);
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(VirtioNetDeviceConfig.StatusOffset, VirtioNetDeviceConfig.StatusLength),
            statusBits);
        return config;
    }

    private static byte[] CreateBlockConfiguration(ulong capacitySectors)
    {
        byte[] config = new byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(config, capacitySectors);
        return config;
    }

    private static void WriteVirtioCapability(
        Span<byte> config,
        byte capabilityOffset,
        byte nextOffset,
        VirtioPciCapabilityType type,
        byte bar,
        uint offset,
        uint length,
        uint notifyOffsetMultiplier,
        byte capabilityLength)
    {
        config[capabilityOffset] = VirtioPciCapabilityListParser.VendorSpecificCapabilityId;
        config[capabilityOffset + 1] = nextOffset;
        config[capabilityOffset + 2] = capabilityLength;
        config[capabilityOffset + 3] = (byte)type;
        config[capabilityOffset + 4] = bar;
        WriteUInt32(config, capabilityOffset + 8, offset);
        WriteUInt32(config, capabilityOffset + 12, length);

        if (capabilityLength >= VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength)
        {
            WriteUInt32(config, capabilityOffset + 16, notifyOffsetMultiplier);
        }
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }
}
