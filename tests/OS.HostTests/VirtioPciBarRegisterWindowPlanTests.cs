using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class VirtioPciBarRegisterWindowPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;

    [HostTest("drivers.virtiopci.registerwindow binds common notify isr device apertures", HostLane.Fast, 0)]
    private static bool TestBindsRequiredApertures()
    {
        VirtioPciBindingPlanResult bindingPlan = CreateBindingPlan();
        VirtioPciBarRegisterWindowBindingResult result =
            VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
                bindingPlan,
                CreateWindows());

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.BindingPlanStatus == VirtioPciBindingPlanStatus.Success
            && result.ApertureValidationStatus == VirtioPciBarApertureValidationStatus.Success
            && result.Bindings.CommonConfiguration.IsBound
            && result.Bindings.CommonConfiguration.CapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && result.Bindings.CommonConfiguration.BarIndex == 0
            && result.Bindings.CommonConfiguration.ApertureVirtualBaseAddress == 0xFFFF800000000020UL
            && result.Bindings.NotifyConfiguration.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.Bindings.NotifyConfiguration.BarIndex == 1
            && result.Bindings.NotifyConfiguration.ApertureVirtualBaseAddress == 0xFFFF800000001080UL
            && result.Bindings.IsrStatus.CapabilityType == VirtioPciCapabilityType.IsrStatus
            && result.Bindings.DeviceConfiguration.CapabilityType == VirtioPciCapabilityType.DeviceConfiguration
            && result.Bindings.DeviceConfiguration.AperturePhysicalBaseAddress == 0x80003040UL
            && result.Bindings.DeviceConfiguration.IsDeviceMemory
            && result.Bindings.DeviceConfiguration.IsUncached);
    }

    [HostTest("drivers.virtiopci.registerwindow plans aligned register access", HostLane.Fast, 1)]
    private static bool TestPlansAlignedRegisterAccess()
    {
        VirtioPciBarRegisterWindowBindingResult binding = CreateBoundWindows();
        if (!binding.Succeeded)
        {
            return HostCheck.Result(false);
        }

        VirtioPciBarRegisterWindowAccessPlanResult read16 =
                VirtioPciBarRegisterWindowPlanner.PlanAccess(
                binding.Bindings.CommonConfiguration,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset,
                VirtioPciRegisterAccessWidth.Word);
        VirtioPciBarRegisterWindowAccessPlanResult write64 =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                binding.Bindings.CommonConfiguration,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset,
                VirtioPciRegisterAccessWidth.QWord);

        return HostCheck.Result(read16.Succeeded
            && read16.Plan.WidthBytes == 2
            && read16.Plan.BarRelativeOffset
                == 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset
            && read16.Plan.PhysicalAddress
                == 0x80000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset
            && read16.Plan.VirtualAddress
                == 0xFFFF800000000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset
            && write64.Succeeded
            && write64.Plan.WidthBytes == 8
            && write64.Plan.BarRelativeOffset
                == 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset
            && write64.Plan.PhysicalAddress
                == 0x80000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset
            && write64.Plan.VirtualAddress
                == 0xFFFF800000000000UL + 0x20UL + VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset);
    }

    [HostTest("drivers.virtiopci.registerwindow rejects missing mapping", HostLane.Fast, 2)]
    private static bool TestRejectsMissingMapping()
    {
        VirtioPciMappedBarWindow[] windows = CreateWindows();
        windows[1] = new VirtioPciMappedBarWindow(
            barIndex: 1,
            physicalBaseAddress: 0x80001000UL,
            virtualBaseAddress: 0xFFFF800000001000UL,
            length: 0x1000,
            isMapped: false,
            isMmio: true,
            isDeviceMemory: true,
            isUncached: true);

        VirtioPciBarRegisterWindowBindingResult result =
            VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
                CreateBindingPlan(),
                windows);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowPlanStatus.WindowNotMapped
            && result.FailingCapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.FailingBarIndex == 1);
    }

    [HostTest("drivers.virtiopci.registerwindow rejects base mismatch", HostLane.Fast, 3)]
    private static bool TestRejectsBaseMismatch()
    {
        VirtioPciMappedBarWindow[] windows = CreateWindows();
        windows[0] = new VirtioPciMappedBarWindow(
            barIndex: 0,
            physicalBaseAddress: 0x80004000UL,
            virtualBaseAddress: 0xFFFF800000000000UL,
            length: 0x1000,
            isMapped: true,
            isMmio: true,
            isDeviceMemory: true,
            isUncached: true);

        VirtioPciBarRegisterWindowBindingResult result =
            VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
                CreateBindingPlan(),
                windows);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowPlanStatus.PhysicalBaseMismatch
            && result.FailingCapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && result.FailingBarIndex == 0);
    }

    [HostTest("drivers.virtiopci.registerwindow rejects range overflow", HostLane.Fast, 4)]
    private static bool TestRejectsRangeOverflow()
    {
        VirtioPciBarRegisterWindowBindingResult binding = CreateBoundWindows();
        if (!binding.Succeeded)
        {
            return HostCheck.Result(false);
        }

        VirtioPciBarRegisterWindowAccessPlanResult result =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                binding.Bindings.NotifyConfiguration,
                relativeOffset: 0xFF,
                VirtioPciRegisterAccessWidth.Word);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowPlanStatus.AccessRangeOutsideAperture
            && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.BarIndex == 1
            && result.RelativeOffset == 0xFF);
    }

    [HostTest("drivers.virtiopci.registerwindow rejects unaligned access", HostLane.Fast, 5)]
    private static bool TestRejectsUnalignedAccess()
    {
        VirtioPciBarRegisterWindowBindingResult binding = CreateBoundWindows();
        if (!binding.Succeeded)
        {
            return HostCheck.Result(false);
        }

        VirtioPciBarRegisterWindowAccessPlanResult result =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                binding.Bindings.CommonConfiguration,
                relativeOffset: 1,
                VirtioPciRegisterAccessWidth.Word);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowPlanStatus.AccessUnaligned
            && result.CapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && result.BarIndex == 0
            && result.RelativeOffset == 1);
    }

    [HostTest("drivers.virtiopci.registerwindow preserves no-live-mmio boundary", HostLane.Fast, 6)]
    private static bool TestPreservesNoLiveMmioBoundary()
    {
        VirtioPciBarRegisterWindowBindingResult failedBinding =
            VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
                VirtioPciBindingPlanResult.Fail(
                    VirtioPciBindingPlanStatus.HeaderParseFailed,
                    PciConfigurationHeaderStatus.BufferTooSmall),
                CreateWindows());
        VirtioPciMappedBarWindow[] nonMmioWindows = CreateWindows();
        nonMmioWindows[0] = new VirtioPciMappedBarWindow(
            barIndex: 0,
            physicalBaseAddress: 0x80000000UL,
            virtualBaseAddress: 0xFFFF800000000000UL,
            length: 0x1000,
            isMapped: true,
            isMmio: false,
            isDeviceMemory: true,
            isUncached: true);
        VirtioPciBarRegisterWindowBindingResult nonMmio =
            VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
                CreateBindingPlan(),
                nonMmioWindows);

        return HostCheck.Result(!failedBinding.Succeeded
            && failedBinding.Status == VirtioPciBarRegisterWindowPlanStatus.BindingPlanFailed
            && !nonMmio.Succeeded
            && nonMmio.Status == VirtioPciBarRegisterWindowPlanStatus.WindowNotMmio
            && VirtioPciBarRegisterWindowPlanner.RequiresCallerMappedWindows
            && VirtioPciBarRegisterWindowPlanner.PlansMappedRegisterAddresses
            && !VirtioPciBarRegisterWindowPlanner.MapsBars
            && !VirtioPciBarRegisterWindowPlanner.PerformsLiveMmio
            && !VirtioPciBarRegisterWindowPlanner.ContainsVolatileAccess
            && !VirtioPciBarRegisterWindowPlanner.ReadsDeviceRegisters
            && !VirtioPciBarRegisterWindowPlanner.WritesDeviceRegisters
            && !VirtioPciBarRegisterWindowPlanner.ConfiguresQueues
            && !VirtioPciBarRegisterWindowPlanner.NotifiesLiveDevice
            && !VirtioPciBarRegisterWindowPlanner.PollsDevice
            && !VirtioPciBarRegisterWindowPlanner.HandlesInterrupts
            && !VirtioPciBarRegisterWindowPlanner.AllocatesDmaMemory
            && !VirtioPciBarRegisterWindowPlanner.PerformsDmaMapping
            && !VirtioPciBarRegisterWindowPlanner.PerformsPacketIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciMappedBarWindow>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBarRegisterWindowBinding>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBarRegisterWindowBindingSet>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBarRegisterWindowAccessPlan>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBarRegisterWindowBindingResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBarRegisterWindowAccessPlanResult>());
    }

    [HostTest("drivers.virtiopci.registerwindow default results are not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        VirtioPciBarRegisterWindowBindingResult binding = default;
        VirtioPciBarRegisterWindowAccessPlanResult access = default;
        VirtioPciBarRegisterWindowAccessExecutionResult execution = default;

        return HostCheck.Result(!binding.IsInitialized
            && !binding.Succeeded
            && binding.Status == VirtioPciBarRegisterWindowPlanStatus.Success
            && binding.BindingPlanStatus == VirtioPciBindingPlanStatus.Success
            && binding.ApertureValidationStatus == VirtioPciBarApertureValidationStatus.Success
            && binding.FailingCapabilityType == default
            && binding.FailingBarIndex == 0
            && !binding.Bindings.CommonConfiguration.IsBound
            && !access.IsInitialized
            && !access.Succeeded
            && access.Status == VirtioPciBarRegisterWindowPlanStatus.Success
            && access.CapabilityType == default
            && access.BarIndex == 0
            && access.RelativeOffset == 0
            && access.Width == default
            && !access.Plan.Binding.IsBound
            && !execution.IsInitialized
            && !execution.Succeeded
            && execution.Status == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && execution.CapabilityType == default
            && execution.BarIndex == 0
            && execution.RelativeOffset == 0
            && execution.Width == default
            && execution.WidthBytes == 0
            && execution.BarRelativeOffset == 0
            && execution.Value == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBarRegisterWindowBindingResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBarRegisterWindowAccessPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioPciBarRegisterWindowAccessExecutionResult>());
    }

    [HostTest("drivers.virtiopci.registerwindow.fakeexec writes planned word access", HostLane.Fast, 8)]
    private static bool TestFakeExecutorWritesPlannedWordAccess()
    {
        VirtioPciBarRegisterWindowBindingResult binding = CreateBoundWindows();
        VirtioPciBarRegisterWindowAccessPlanResult access =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                binding.Bindings.CommonConfiguration,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset,
                VirtioPciRegisterAccessWidth.Word);
        byte[] windowBytes = CreateWindowBytes();

        VirtioPciBarRegisterWindowAccessExecutionResult result =
            VirtioPciBarRegisterWindowAccessExecutor.Write(
                access.Plan,
                value: 0x1234,
                windowBytes);

        int offset = checked((int)access.Plan.BarRelativeOffset);
        return HostCheck.Result(binding.Succeeded
            && access.Succeeded
            && result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.CapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && result.BarIndex == 0
            && result.RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.QueueSelectOffset
            && result.Width == VirtioPciRegisterAccessWidth.Word
            && result.WidthBytes == sizeof(ushort)
            && result.BarRelativeOffset == access.Plan.BarRelativeOffset
            && result.Value == 0x1234
            && EndianPrimitives.ReadUInt16LittleEndian(
                windowBytes.AsSpan(offset, sizeof(ushort))) == 0x1234
            && AllBytesEqual(windowBytes, 0, offset, 0xCC)
            && AllBytesEqual(
                windowBytes,
                offset + sizeof(ushort),
                windowBytes.Length - offset - sizeof(ushort),
                0xCC));
    }

    [HostTest("drivers.virtiopci.registerwindow.fakeexec reads planned dword access", HostLane.Fast, 9)]
    private static bool TestFakeExecutorReadsPlannedDwordAccess()
    {
        VirtioPciBarRegisterWindowBindingResult binding = CreateBoundWindows();
        VirtioPciBarRegisterWindowAccessPlanResult access =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                binding.Bindings.CommonConfiguration,
                VirtioPciModernRegisterMap.CommonConfiguration.DeviceFeatureOffset,
                VirtioPciRegisterAccessWidth.DWord);
        byte[] windowBytes = CreateWindowBytes();
        int offset = checked((int)access.Plan.BarRelativeOffset);
        EndianPrimitives.WriteUInt32LittleEndian(
            windowBytes.AsSpan(offset, sizeof(uint)),
            0x89ABCDEF);

        VirtioPciBarRegisterWindowAccessExecutionResult result =
            VirtioPciBarRegisterWindowAccessExecutor.Read(access.Plan, windowBytes);

        return HostCheck.Result(binding.Succeeded
            && access.Succeeded
            && result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.CapabilityType == VirtioPciCapabilityType.CommonConfiguration
            && result.RelativeOffset == VirtioPciModernRegisterMap.CommonConfiguration.DeviceFeatureOffset
            && result.Width == VirtioPciRegisterAccessWidth.DWord
            && result.WidthBytes == sizeof(uint)
            && result.BarRelativeOffset == access.Plan.BarRelativeOffset
            && result.Value == 0x89ABCDEF
            && AllBytesEqual(windowBytes, 0, offset, 0xCC)
            && AllBytesEqual(
                windowBytes,
                offset + sizeof(uint),
                windowBytes.Length - offset - sizeof(uint),
                0xCC));
    }

    [HostTest("drivers.virtiopci.registerwindow.fakeexec rejects missing access plan", HostLane.Fast, 10)]
    private static bool TestFakeExecutorRejectsMissingAccessPlan()
    {
        byte[] windowBytes = CreateWindowBytes();

        VirtioPciBarRegisterWindowAccessExecutionResult read =
            VirtioPciBarRegisterWindowAccessExecutor.Read(default, windowBytes);
        VirtioPciBarRegisterWindowAccessExecutionResult write =
            VirtioPciBarRegisterWindowAccessExecutor.Write(
                default,
                value: 0x55,
                windowBytes);

        return HostCheck.Result(!read.Succeeded
            && read.Status == VirtioPciBarRegisterWindowAccessExecutionStatus.MissingAccessPlan
            && read.WidthBytes == 0
            && read.Value == 0
            && !write.Succeeded
            && write.Status == VirtioPciBarRegisterWindowAccessExecutionStatus.MissingAccessPlan
            && write.WidthBytes == 0
            && write.Value == 0
            && AllBytesEqual(windowBytes, 0xCC));
    }

    [HostTest("drivers.virtiopci.registerwindow.fakeexec rejects short window buffer", HostLane.Fast, 11)]
    private static bool TestFakeExecutorRejectsShortWindowBuffer()
    {
        VirtioPciBarRegisterWindowBindingResult binding = CreateBoundWindows();
        VirtioPciBarRegisterWindowAccessPlanResult access =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                binding.Bindings.NotifyConfiguration,
                relativeOffset: 0,
                VirtioPciRegisterAccessWidth.Word);
        byte[] windowBytes = CreateWindowBytes(length: (int)access.Plan.BarRelativeOffset + 1);

        VirtioPciBarRegisterWindowAccessExecutionResult result =
            VirtioPciBarRegisterWindowAccessExecutor.Write(
                access.Plan,
                value: 0xABCD,
                windowBytes);

        return HostCheck.Result(binding.Succeeded
            && access.Succeeded
            && !result.Succeeded
            && result.Status == VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.Width == VirtioPciRegisterAccessWidth.Word
            && result.WidthBytes == sizeof(ushort)
            && result.Value == 0
            && AllBytesEqual(windowBytes, 0xCC));
    }

    [HostTest("drivers.virtiopci.registerwindow.fakeexec preserves no-live-mmio boundary", HostLane.Fast, 12)]
    private static bool TestFakeExecutorPreservesNoLiveMmioBoundary()
    {
        VirtioPciBarRegisterWindowBindingResult binding = CreateBoundWindows();
        VirtioPciBarRegisterWindowAccessPlanResult access =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                binding.Bindings.CommonConfiguration,
                VirtioPciModernRegisterMap.CommonConfiguration.QueueDescOffset,
                VirtioPciRegisterAccessWidth.QWord);
        byte[] windowBytes = CreateWindowBytes();

        VirtioPciBarRegisterWindowAccessExecutionResult result =
            VirtioPciBarRegisterWindowAccessExecutor.Write(
                access.Plan,
                value: 0x1122334455667788UL,
                windowBytes);

        return HostCheck.Result(binding.Succeeded
            && access.Succeeded
            && result.Succeeded
            && VirtioPciBarRegisterWindowAccessExecutor.RequiresAccessPlan
            && VirtioPciBarRegisterWindowAccessExecutor.UsesCallerOwnedWindowBuffer
            && !VirtioPciBarRegisterWindowAccessExecutor.PerformsLiveMmio
            && !VirtioPciBarRegisterWindowAccessExecutor.ContainsVolatileAccess
            && !VirtioPciBarRegisterWindowAccessExecutor.MapsBars
            && !VirtioPciBarRegisterWindowAccessExecutor.AccessesPhysicalAddresses
            && !VirtioPciBarRegisterWindowAccessExecutor.ExecutesMemoryBarriers
            && !VirtioPciBarRegisterWindowAccessExecutor.ReadsLiveDeviceRegisters
            && !VirtioPciBarRegisterWindowAccessExecutor.WritesLiveDeviceRegisters
            && !VirtioPciBarRegisterWindowAccessExecutor.ConfiguresQueues
            && !VirtioPciBarRegisterWindowAccessExecutor.NotifiesLiveDevice
            && !VirtioPciBarRegisterWindowAccessExecutor.PollsDevice
            && !VirtioPciBarRegisterWindowAccessExecutor.HandlesInterrupts
            && !VirtioPciBarRegisterWindowAccessExecutor.AllocatesDmaMemory
            && !VirtioPciBarRegisterWindowAccessExecutor.PerformsDmaMapping
            && !VirtioPciBarRegisterWindowAccessExecutor.PerformsPacketIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioPciBarRegisterWindowAccessExecutionResult>());
    }

    private static VirtioPciBarRegisterWindowBindingResult CreateBoundWindows()
    {
        return VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
            CreateBindingPlan(),
            CreateWindows());
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        return VirtioPciBindingPlan.Create(
            CreateCandidate(deviceId),
            CreateConfig(deviceId, VirtioPciDeviceDiscovery.NetworkDeviceConfigurationMinimumLength),
            CreateBarSizes());
    }

    private static VirtioPciMappedBarWindow[] CreateWindows()
    {
        return new VirtioPciMappedBarWindow[]
        {
            CreateWindow(0, 0x80000000UL, 0xFFFF800000000000UL),
            CreateWindow(1, 0x80001000UL, 0xFFFF800000001000UL),
            CreateWindow(2, 0x80002000UL, 0xFFFF800000002000UL),
            CreateWindow(3, 0x80003000UL, 0xFFFF800000003000UL),
            default,
            default
        };
    }

    private static VirtioPciMappedBarWindow CreateWindow(
        byte barIndex,
        ulong physicalBaseAddress,
        ulong virtualBaseAddress)
    {
        return new VirtioPciMappedBarWindow(
            barIndex,
            physicalBaseAddress,
            virtualBaseAddress,
            length: 0x1000,
            isMapped: true,
            isMmio: true,
            isDeviceMemory: true,
            isUncached: true);
    }

    private static PciEcamScannedDevice CreateCandidate(ushort deviceId)
    {
        return new PciEcamScannedDevice(
            segmentIndex: 1,
            new PciEcamSegment(
                basePhysicalAddress: 0xE0000000UL,
                segmentGroup: 7,
                startBus: 0,
                endBus: 0x7F),
            new PciScannedDevice(
                bus: 2,
                device: 4,
                function: 1,
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

    private static byte[] CreateConfig(
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
        return new ulong[]
        {
            0x1000,
            0x1000,
            0x1000,
            0x1000,
            0,
            0
        };
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

    private static byte[] CreateWindowBytes(int length = 0x1000)
    {
        byte[] bytes = new byte[length];
        Fill(bytes, 0xCC);
        return bytes;
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> bytes, byte expected)
    {
        return AllBytesEqual(bytes, 0, bytes.Length, expected);
    }

    private static bool AllBytesEqual(
        ReadOnlySpan<byte> bytes,
        int offset,
        int count,
        byte expected)
    {
        for (int index = 0; index < count; index++)
        {
            if (bytes[offset + index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static void Fill(Span<byte> bytes, byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = value;
        }
    }
}
