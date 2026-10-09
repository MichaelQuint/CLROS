using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class VirtioNetDeviceConfigTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const uint DeviceConfigurationBarOffset = 0x40;
    private const ulong DeviceBarPhysicalBase = 0x80003000UL;
    private const ulong DeviceBarVirtualBase = 0xFFFF800000003000UL;

    [HostTest("drivers.virtionet.config parses mac address", HostLane.Fast, 0)]
    private static bool TestParsesMacAddress()
    {
        byte[] config = CreateMinimumConfig(TestMacAddress);

        VirtioNetDeviceConfigParseResult result =
            VirtioNetDeviceConfig.TryParse(config);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioNetDeviceConfigStatus.Success
            && result.Config.MacAddress == TestMacAddress
            && !result.Config.HasStatus
            && result.Config.StatusBits == 0
            && !result.Config.IsLinkUp
            && !result.Config.HasAnnounce
            && result.InputLength == VirtioNetDeviceConfig.MinimumLength
            && result.BytesConsumed == VirtioNetDeviceConfig.MinimumLength);
    }

    [HostTest("drivers.virtionet.config parses optional status", HostLane.Fast, 1)]
    private static bool TestParsesOptionalStatus()
    {
        byte[] config = CreateMinimumConfig(TestMacAddress);
        Array.Resize(ref config, VirtioNetDeviceConfig.StatusConfigLength);
        const ushort statusBits =
            VirtioNetDeviceConfig.StatusLinkUp |
            VirtioNetDeviceConfig.StatusAnnounce;
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(VirtioNetDeviceConfig.StatusOffset, VirtioNetDeviceConfig.StatusLength),
            statusBits);

        VirtioNetDeviceConfigParseResult result =
            VirtioNetDeviceConfig.TryParse(config);

        return HostCheck.Result(result.Succeeded
            && result.Config.MacAddress == TestMacAddress
            && result.Config.HasStatus
            && result.Config.StatusBits == statusBits
            && result.Config.IsLinkUp
            && result.Config.HasAnnounce
            && result.InputLength == VirtioNetDeviceConfig.StatusConfigLength
            && result.BytesConsumed == VirtioNetDeviceConfig.StatusConfigLength);
    }

    [HostTest("drivers.virtionet.config accepts minimum config", HostLane.Fast, 2)]
    private static bool TestAcceptsMinimumConfig()
    {
        byte[] config = CreateMinimumConfig(
            new MacAddress(0x02, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE));

        VirtioNetDeviceConfigParseResult result =
            VirtioNetDeviceConfig.TryParse(config);

        return HostCheck.Result(result.Succeeded
            && result.Config.MacAddress == new MacAddress(0x02, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE)
            && !result.Config.HasStatus
            && result.BytesConsumed == VirtioNetDeviceConfig.MinimumLength);
    }

    [HostTest("drivers.virtionet.config rejects short config", HostLane.Fast, 3)]
    private static bool TestRejectsShortConfig()
    {
        byte[] tooShortForMac = new byte[VirtioNetDeviceConfig.MinimumLength - 1];
        byte[] partialStatus = CreateMinimumConfig(TestMacAddress);
        Array.Resize(ref partialStatus, VirtioNetDeviceConfig.StatusConfigLength - 1);

        VirtioNetDeviceConfigParseResult macResult =
            VirtioNetDeviceConfig.TryParse(tooShortForMac);
        VirtioNetDeviceConfigParseResult statusResult =
            VirtioNetDeviceConfig.TryParse(partialStatus);

        return HostCheck.Result(!macResult.Succeeded
            && macResult.Status == VirtioNetDeviceConfigStatus.BufferTooSmall
            && macResult.InputLength == tooShortForMac.Length
            && macResult.BytesConsumed == 0
            && !statusResult.Succeeded
            && statusResult.Status == VirtioNetDeviceConfigStatus.BufferTooSmall
            && statusResult.InputLength == partialStatus.Length);
    }

    [HostTest("drivers.virtionet.config rejects zero mac", HostLane.Fast, 4)]
    private static bool TestRejectsZeroMac()
    {
        byte[] config = CreateMinimumConfig(default);

        VirtioNetDeviceConfigParseResult result =
            VirtioNetDeviceConfig.TryParse(config);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioNetDeviceConfigStatus.ZeroMacAddress
            && result.InputLength == VirtioNetDeviceConfig.MinimumLength
            && result.BytesConsumed == 0);
    }

    [HostTest("drivers.virtionet.config rejects broadcast mac", HostLane.Fast, 5)]
    private static bool TestRejectsBroadcastMac()
    {
        byte[] config = CreateMinimumConfig(
            new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF));

        VirtioNetDeviceConfigParseResult result =
            VirtioNetDeviceConfig.TryParse(config);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioNetDeviceConfigStatus.BroadcastMacAddress
            && result.InputLength == VirtioNetDeviceConfig.MinimumLength
            && result.BytesConsumed == 0);
    }

    [HostTest("drivers.virtionet.config default results are not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        VirtioNetDeviceConfigParseResult parse = default;
        VirtioNetDeviceConfigRegisterWindowReadResult read = default;

        return HostCheck.Result(!parse.IsInitialized
            && !parse.Succeeded
            && parse.Status == VirtioNetDeviceConfigStatus.Success
            && parse.BytesConsumed == 0
            && !read.IsInitialized
            && !read.Succeeded
            && read.Status == VirtioNetDeviceConfigRegisterWindowReadStatus.Success
            && read.ConfigStatus == VirtioNetDeviceConfigStatus.Success
            && read.BytesRead == 0);
    }

    [HostTest("drivers.net mac validation uses shared helpers", HostLane.Fast, 7)]
    private static bool TestMacValidationUsesSharedHelpers()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string virtioSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Drivers",
            "VirtioNetDeviceConfig.cs"));
        string enaSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Drivers",
            "EnaDeviceAttributes.cs"));

        return HostCheck.Result(DriverSourceUsesSharedMacHelpers(virtioSource)
            && DriverSourceUsesSharedMacHelpers(enaSource));
    }

    private static bool DriverSourceUsesSharedMacHelpers(string source)
    {
        return source.Contains("macAddress.IsZero", StringComparison.Ordinal)
            && source.Contains("macAddress.IsBroadcast", StringComparison.Ordinal)
            && !source.Contains("private static bool IsZero(MacAddress", StringComparison.Ordinal)
            && !source.Contains("private static bool IsBroadcast(MacAddress", StringComparison.Ordinal);
    }

    [HostTest("drivers.virtionet.config.windowexec reads mac address", HostLane.Fast, 8)]
    private static bool TestRegisterWindowReadsMacAddress()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioNetDeviceConfig.MinimumLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(TestMacAddress, hasStatus: false, statusBits: 0);
        byte[] scratch = CreateScratch(VirtioNetDeviceConfig.MinimumLength);

        VirtioNetDeviceConfigRegisterWindowReadResult result =
            VirtioNetDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return HostCheck.Result(windows.Succeeded
            && result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioNetDeviceConfigRegisterWindowReadStatus.Success
            && result.ConfigStatus == VirtioNetDeviceConfigStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.Config.MacAddress == TestMacAddress
            && !result.Config.HasStatus
            && result.BytesRead == VirtioNetDeviceConfig.MinimumLength
            && result.BytesConsumed == VirtioNetDeviceConfig.MinimumLength
            && !result.StatusFieldAttempted
            && !result.StatusFieldRead
            && result.CapabilityType == VirtioPciCapabilityType.DeviceConfiguration
            && result.BarIndex == 3
            && result.LastRelativeOffset == VirtioNetDeviceConfig.MacAddressOffset + sizeof(uint)
            && result.LastAccessWidth == VirtioPciRegisterAccessWidth.Word
            && result.LastAccessWidthBytes == sizeof(ushort)
            && result.LastBarRelativeOffset ==
                DeviceConfigurationBarOffset + VirtioNetDeviceConfig.MacAddressOffset + sizeof(uint)
            && scratch[0] == TestMacAddress.Octet0
            && scratch[1] == TestMacAddress.Octet1
            && scratch[2] == TestMacAddress.Octet2
            && scratch[3] == TestMacAddress.Octet3
            && scratch[4] == TestMacAddress.Octet4
            && scratch[5] == TestMacAddress.Octet5);
    }

    [HostTest("drivers.virtionet.config.windowexec reads optional status", HostLane.Fast, 9)]
    private static bool TestRegisterWindowReadsOptionalStatus()
    {
        const ushort statusBits =
            VirtioNetDeviceConfig.StatusLinkUp |
            VirtioNetDeviceConfig.StatusAnnounce;
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioNetDeviceConfig.StatusConfigLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(
            TestMacAddress,
            hasStatus: true,
            statusBits);
        byte[] scratch = CreateScratch(VirtioNetDeviceConfig.StatusConfigLength);

        VirtioNetDeviceConfigRegisterWindowReadResult result =
            VirtioNetDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return HostCheck.Result(windows.Succeeded
            && result.Succeeded
            && result.Config.MacAddress == TestMacAddress
            && result.Config.HasStatus
            && result.Config.StatusBits == statusBits
            && result.Config.IsLinkUp
            && result.Config.HasAnnounce
            && result.BytesRead == VirtioNetDeviceConfig.StatusConfigLength
            && result.BytesConsumed == VirtioNetDeviceConfig.StatusConfigLength
            && result.StatusFieldAttempted
            && result.StatusFieldRead
            && result.LastRelativeOffset == VirtioNetDeviceConfig.StatusOffset
            && result.LastAccessWidth == VirtioPciRegisterAccessWidth.Word
            && result.LastValue == statusBits
            && EndianPrimitives.ReadUInt16LittleEndian(
                scratch.AsSpan(VirtioNetDeviceConfig.StatusOffset, VirtioNetDeviceConfig.StatusLength)) ==
                    statusBits);
    }

    [HostTest("drivers.virtionet.config.windowexec rejects short scratch buffer", HostLane.Fast, 10)]
    private static bool TestRegisterWindowRejectsShortScratchBuffer()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioNetDeviceConfig.StatusConfigLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(TestMacAddress, hasStatus: true, statusBits: 0);
        byte[] scratch = CreateScratch(VirtioNetDeviceConfig.MinimumLength - 1);

        VirtioNetDeviceConfigRegisterWindowReadResult result =
            VirtioNetDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioNetDeviceConfigRegisterWindowReadStatus.ScratchBufferTooSmall
            && result.ConfigStatus == VirtioNetDeviceConfigStatus.BufferTooSmall
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.ScratchLength == VirtioNetDeviceConfig.MinimumLength - 1
            && result.BytesRead == 0
            && !result.StatusFieldAttempted
            && !result.StatusFieldRead
            && AllBytesEqual(scratch, 0xCC));
    }

    [HostTest("drivers.virtionet.config.windowexec propagates register window failure", HostLane.Fast, 11)]
    private static bool TestRegisterWindowPropagatesRegisterWindowFailure()
    {
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(TestMacAddress, hasStatus: false, statusBits: 0);
        byte[] scratch = CreateScratch(VirtioNetDeviceConfig.MinimumLength);

        VirtioNetDeviceConfigRegisterWindowReadResult result =
            VirtioNetDeviceConfigRegisterWindowReader.Read(
                deviceConfiguration: default,
                deviceWindowBytes,
                scratch);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioNetDeviceConfigRegisterWindowReadStatus.RegisterWindowPlanFailed
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.MissingWindow
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.BytesRead == 0
            && !result.StatusFieldAttempted
            && AllBytesEqual(scratch, 0xCC));
    }

    [HostTest("drivers.virtionet.config.windowexec propagates access failure", HostLane.Fast, 12)]
    private static bool TestRegisterWindowPropagatesAccessFailure()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioNetDeviceConfig.StatusConfigLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(
            TestMacAddress,
            hasStatus: true,
            VirtioNetDeviceConfig.StatusLinkUp,
            checked((int)(DeviceConfigurationBarOffset + VirtioNetDeviceConfig.MinimumLength - 1)));
        byte[] scratch = CreateScratch(VirtioNetDeviceConfig.StatusConfigLength);

        VirtioNetDeviceConfigRegisterWindowReadResult result =
            VirtioNetDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return HostCheck.Result(windows.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetDeviceConfigRegisterWindowReadStatus.AccessExecutionFailed
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.BytesRead == sizeof(uint)
            && !result.StatusFieldAttempted
            && !result.StatusFieldRead);
    }

    [HostTest("drivers.virtionet.config.windowexec propagates config parse failure", HostLane.Fast, 13)]
    private static bool TestRegisterWindowPropagatesConfigParseFailure()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioNetDeviceConfig.MinimumLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(default, hasStatus: false, statusBits: 0);
        byte[] scratch = CreateScratch(VirtioNetDeviceConfig.MinimumLength);

        VirtioNetDeviceConfigRegisterWindowReadResult result =
            VirtioNetDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return HostCheck.Result(windows.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetDeviceConfigRegisterWindowReadStatus.ConfigParseFailed
            && result.ConfigStatus == VirtioNetDeviceConfigStatus.ZeroMacAddress
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.BytesRead == VirtioNetDeviceConfig.MinimumLength
            && result.BytesConsumed == 0);
    }

    [HostTest("drivers.virtionet.config.windowexec preserves no-live-mmio boundary", HostLane.Fast, 14)]
    private static bool TestRegisterWindowPreservesNoLiveMmioBoundary()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioNetDeviceConfig.StatusConfigLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(
            TestMacAddress,
            hasStatus: true,
            VirtioNetDeviceConfig.StatusLinkUp);
        byte[] scratch = CreateScratch(VirtioNetDeviceConfig.StatusConfigLength);

        VirtioNetDeviceConfigRegisterWindowReadResult result =
            VirtioNetDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return HostCheck.Result(result.Succeeded
            && VirtioNetDeviceConfigRegisterWindowReader.RequiresRegisterWindowBinding
            && VirtioNetDeviceConfigRegisterWindowReader.UsesCallerOwnedWindowBuffer
            && VirtioNetDeviceConfigRegisterWindowReader.UsesCallerOwnedScratchBuffer
            && VirtioNetDeviceConfigRegisterWindowReader.InvokesRegisterWindowAccessExecutor
            && VirtioNetDeviceConfigRegisterWindowReader.ReadsCallerOwnedWindowBuffer
            && VirtioNetDeviceConfigRegisterWindowReader.WritesCallerOwnedScratchBuffer
            && !VirtioNetDeviceConfigRegisterWindowReader.MapsBars
            && !VirtioNetDeviceConfigRegisterWindowReader.PerformsLiveMmio
            && !VirtioNetDeviceConfigRegisterWindowReader.ContainsVolatileAccess
            && !VirtioNetDeviceConfigRegisterWindowReader.AccessesPhysicalAddresses
            && !VirtioNetDeviceConfigRegisterWindowReader.ExecutesMemoryBarriers
            && !VirtioNetDeviceConfigRegisterWindowReader.ReadsLiveDeviceRegisters
            && !VirtioNetDeviceConfigRegisterWindowReader.WritesLiveDeviceRegisters
            && !VirtioNetDeviceConfigRegisterWindowReader.ConfiguresQueues
            && !VirtioNetDeviceConfigRegisterWindowReader.NotifiesLiveDevice
            && !VirtioNetDeviceConfigRegisterWindowReader.PollsDevice
            && !VirtioNetDeviceConfigRegisterWindowReader.HandlesInterrupts
            && !VirtioNetDeviceConfigRegisterWindowReader.AllocatesDmaMemory
            && !VirtioNetDeviceConfigRegisterWindowReader.PerformsDmaMapping
            && !VirtioNetDeviceConfigRegisterWindowReader.PerformsPacketIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetDeviceConfigRegisterWindowReadResult>());
    }

    private static byte[] CreateMinimumConfig(MacAddress macAddress)
    {
        byte[] config = new byte[VirtioNetDeviceConfig.MinimumLength];
        PacketParseStatus status = macAddress.TryWrite(config, VirtioNetDeviceConfig.MacAddressOffset);
        if (status != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic MAC config.");
        }

        return config;
    }

    private static VirtioPciBarRegisterWindowBindingResult CreateBoundRegisterWindows(
        uint deviceConfigurationLength)
    {
        return VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
            CreateBindingPlan(deviceConfigurationLength),
            CreateRegisterWindows());
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan(uint deviceConfigurationLength)
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId;
        return VirtioPciBindingPlan.Create(
            CreateCandidate(deviceId),
            CreatePciConfig(deviceId, deviceConfigurationLength),
            CreateBarSizes());
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

    private static byte[] CreatePciConfig(
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
        WriteUInt32(config, 0x1C, (uint)DeviceBarPhysicalBase);
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
            offset: DeviceConfigurationBarOffset,
            length: deviceConfigurationLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return [0x1000, 0x1000, 0x1000, 0x1000, 0, 0];
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindows()
    {
        return
        [
            CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL),
            CreateRegisterWindow(1, 0x80001000UL, 0xFFFF800000001000UL),
            CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL),
            CreateRegisterWindow(3, DeviceBarPhysicalBase, DeviceBarVirtualBase),
            default,
            default
        ];
    }

    private static VirtioPciMappedBarWindow CreateRegisterWindow(
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

    private static byte[] CreateDeviceWindowBytes(
        MacAddress macAddress,
        bool hasStatus,
        ushort statusBits,
        int length = 0x1000)
    {
        byte[] bytes = new byte[length];
        Fill(bytes, 0xCC);
        if (length < (int)DeviceConfigurationBarOffset + VirtioNetDeviceConfig.MinimumLength)
        {
            return bytes;
        }

        Span<byte> config =
            bytes.AsSpan((int)DeviceConfigurationBarOffset, VirtioNetDeviceConfig.MinimumLength);
        PacketParseStatus status = macAddress.TryWrite(config, VirtioNetDeviceConfig.MacAddressOffset);
        if (status != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic MAC config.");
        }

        if (hasStatus &&
            length >= (int)DeviceConfigurationBarOffset + VirtioNetDeviceConfig.StatusConfigLength)
        {
            EndianPrimitives.WriteUInt16LittleEndian(
                bytes.AsSpan(
                    (int)DeviceConfigurationBarOffset + VirtioNetDeviceConfig.StatusOffset,
                    VirtioNetDeviceConfig.StatusLength),
                statusBits);
        }

        return bytes;
    }

    private static byte[] CreateScratch(int length)
    {
        byte[] bytes = new byte[length];
        Fill(bytes, 0xCC);
        return bytes;
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> bytes, byte expected)
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

    private static void Fill(Span<byte> bytes, byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = value;
        }
    }

    private static MacAddress TestMacAddress =>
        new MacAddress(0x02, 0x10, 0x20, 0x30, 0x40, 0x50);
}
