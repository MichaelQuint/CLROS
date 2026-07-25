using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockDeviceConfigTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const uint DeviceConfigurationBarOffset = 0x40;
    private const ulong DeviceBarPhysicalBase = 0x80003000UL;
    private const ulong DeviceBarVirtualBase = 0xFFFF800000003000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage.virtioblk.config parses capacity", TestParsesCapacity, ref failures);
        Run("storage.virtioblk.config creates block info", TestCreatesBlockInfo, ref failures);
        Run("storage.virtioblk.config rejects short config", TestRejectsShortConfig, ref failures);
        Run("storage.virtioblk.config rejects zero capacity", TestRejectsZeroCapacity, ref failures);
        Run("storage.virtioblk.config reports capacity overflow", TestReportsCapacityOverflow, ref failures);
        Run("storage.virtioblk.config.windowexec reads capacity", TestRegisterWindowReadsCapacity, ref failures);
        Run("storage.virtioblk.config.windowexec rejects short scratch buffer", TestRegisterWindowRejectsShortScratchBuffer, ref failures);
        Run("storage.virtioblk.config.windowexec propagates register window failure", TestRegisterWindowPropagatesRegisterWindowFailure, ref failures);
        Run("storage.virtioblk.config.windowexec propagates access failure", TestRegisterWindowPropagatesAccessFailure, ref failures);
        Run("storage.virtioblk.config.windowexec propagates config parse failure", TestRegisterWindowPropagatesConfigParseFailure, ref failures);
        Run("storage.virtioblk.config.windowexec preserves no-live-mmio boundary", TestRegisterWindowPreservesNoLiveMmioBoundary, ref failures);
        Run("storage.virtioblk.config default result is not success", TestDefaultConfigResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.config.windowexec default result is not success", TestDefaultRegisterWindowResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio block device config host tests failed: " + failures);
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

    private static bool TestParsesCapacity()
    {
        Span<byte> config = stackalloc byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(config, 4096);

        VirtioBlockDeviceConfigResult result = VirtioBlockDeviceConfig.Parse(config);

        return result.Succeeded
            && result.Status == VirtioBlockDeviceConfigStatus.Success
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.RawCapacitySectors == 4096
            && result.SectorSizeBytes == VirtioBlockDeviceConfig.SectorSizeBytes
            && !VirtioBlockDeviceConfig.PerformsMmio
            && !VirtioBlockDeviceConfig.ReadsDeviceConfig;
    }

    private static bool TestCreatesBlockInfo()
    {
        Span<byte> config = stackalloc byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(config, 8);

        VirtioBlockDeviceConfigResult result = VirtioBlockDeviceConfig.Parse(config);

        return result.Succeeded
            && result.BlockDeviceInfo.BlockSize == 512
            && result.BlockDeviceInfo.BlockCount == 8
            && result.BlockDeviceInfo.CapacityBytes == 4096;
    }

    private static bool TestRejectsShortConfig()
    {
        Span<byte> config = stackalloc byte[VirtioBlockDeviceConfig.CapacityLength - 1];

        VirtioBlockDeviceConfigResult result = VirtioBlockDeviceConfig.Parse(config);

        return !result.Succeeded
            && result.Status == VirtioBlockDeviceConfigStatus.ConfigTooSmall
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.RawCapacitySectors == 0
            && result.SectorSizeBytes == VirtioBlockDeviceConfig.SectorSizeBytes;
    }

    private static bool TestRejectsZeroCapacity()
    {
        Span<byte> config = stackalloc byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(config, 0);

        VirtioBlockDeviceConfigResult result = VirtioBlockDeviceConfig.Parse(config);

        return !result.Succeeded
            && result.Status == VirtioBlockDeviceConfigStatus.InvalidCapacity
            && result.BlockDeviceStatus == BlockDeviceStatus.InvalidBlockCount
            && result.RawCapacitySectors == 0;
    }

    private static bool TestReportsCapacityOverflow()
    {
        Span<byte> config = stackalloc byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(
            config,
            (ulong.MaxValue / VirtioBlockDeviceConfig.SectorSizeBytes) + 1UL);

        VirtioBlockDeviceConfigResult result = VirtioBlockDeviceConfig.Parse(config);

        return !result.Succeeded
            && result.Status == VirtioBlockDeviceConfigStatus.CapacityOverflow
            && result.BlockDeviceStatus == BlockDeviceStatus.Overflow
            && result.RawCapacitySectors == (ulong.MaxValue / VirtioBlockDeviceConfig.SectorSizeBytes) + 1UL;
    }

    private static bool TestRegisterWindowReadsCapacity()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioBlockDeviceConfig.CapacityLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(rawCapacitySectors: 4096);
        byte[] scratch = CreateScratch(VirtioBlockDeviceConfig.CapacityLength);

        VirtioBlockDeviceConfigRegisterWindowReadResult result =
            VirtioBlockDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return windows.Succeeded
            && result.Succeeded
            && result.Status == VirtioBlockDeviceConfigRegisterWindowReadStatus.Success
            && result.ConfigStatus == VirtioBlockDeviceConfigStatus.Success
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.RawCapacitySectors == 4096
            && result.SectorSizeBytes == VirtioBlockDeviceConfig.SectorSizeBytes
            && result.BlockDeviceInfo.BlockCount == 4096
            && result.BytesRead == VirtioBlockDeviceConfig.CapacityLength
            && result.ScratchLength == VirtioBlockDeviceConfig.CapacityLength
            && result.CapabilityType == VirtioPciCapabilityType.DeviceConfiguration
            && result.BarIndex == 3
            && result.RelativeOffset == VirtioBlockDeviceConfig.CapacityOffset
            && result.AccessWidth == VirtioPciRegisterAccessWidth.QWord
            && result.AccessWidthBytes == VirtioBlockDeviceConfig.CapacityLength
            && result.BarRelativeOffset == DeviceConfigurationBarOffset
            && result.Value == 4096
            && EndianPrimitives.ReadUInt64LittleEndian(scratch) == 4096;
    }

    private static bool TestRegisterWindowRejectsShortScratchBuffer()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioBlockDeviceConfig.CapacityLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(rawCapacitySectors: 4096);
        byte[] scratch = CreateScratch(VirtioBlockDeviceConfig.CapacityLength - 1);

        VirtioBlockDeviceConfigRegisterWindowReadResult result =
            VirtioBlockDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return !result.Succeeded
            && result.Status == VirtioBlockDeviceConfigRegisterWindowReadStatus.ScratchBufferTooSmall
            && result.ConfigStatus == VirtioBlockDeviceConfigStatus.ConfigTooSmall
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.ScratchLength == VirtioBlockDeviceConfig.CapacityLength - 1
            && result.BytesRead == 0
            && AllBytesEqual(scratch, 0xCC);
    }

    private static bool TestRegisterWindowPropagatesRegisterWindowFailure()
    {
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(rawCapacitySectors: 4096);
        byte[] scratch = CreateScratch(VirtioBlockDeviceConfig.CapacityLength);

        VirtioBlockDeviceConfigRegisterWindowReadResult result =
            VirtioBlockDeviceConfigRegisterWindowReader.Read(
                deviceConfiguration: default,
                deviceWindowBytes,
                scratch);

        return !result.Succeeded
            && result.Status == VirtioBlockDeviceConfigRegisterWindowReadStatus.RegisterWindowPlanFailed
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.MissingWindow
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.BytesRead == 0
            && AllBytesEqual(scratch, 0xCC);
    }

    private static bool TestRegisterWindowPropagatesAccessFailure()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioBlockDeviceConfig.CapacityLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(
            rawCapacitySectors: 4096,
            checked((int)(DeviceConfigurationBarOffset + VirtioBlockDeviceConfig.CapacityLength - 1)));
        byte[] scratch = CreateScratch(VirtioBlockDeviceConfig.CapacityLength);

        VirtioBlockDeviceConfigRegisterWindowReadResult result =
            VirtioBlockDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return windows.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockDeviceConfigRegisterWindowReadStatus.AccessExecutionFailed
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.BytesRead == 0
            && AllBytesEqual(scratch, 0xCC);
    }

    private static bool TestRegisterWindowPropagatesConfigParseFailure()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioBlockDeviceConfig.CapacityLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(rawCapacitySectors: 0);
        byte[] scratch = CreateScratch(VirtioBlockDeviceConfig.CapacityLength);

        VirtioBlockDeviceConfigRegisterWindowReadResult result =
            VirtioBlockDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return windows.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockDeviceConfigRegisterWindowReadStatus.ConfigParseFailed
            && result.ConfigStatus == VirtioBlockDeviceConfigStatus.InvalidCapacity
            && result.BlockDeviceStatus == BlockDeviceStatus.InvalidBlockCount
            && result.RawCapacitySectors == 0
            && result.BytesRead == VirtioBlockDeviceConfig.CapacityLength;
    }

    private static bool TestRegisterWindowPreservesNoLiveMmioBoundary()
    {
        VirtioPciBarRegisterWindowBindingResult windows =
            CreateBoundRegisterWindows(VirtioBlockDeviceConfig.CapacityLength);
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(rawCapacitySectors: 4096);
        byte[] scratch = CreateScratch(VirtioBlockDeviceConfig.CapacityLength);

        VirtioBlockDeviceConfigRegisterWindowReadResult result =
            VirtioBlockDeviceConfigRegisterWindowReader.Read(
                windows.Bindings.DeviceConfiguration,
                deviceWindowBytes,
                scratch);

        return result.Succeeded
            && VirtioBlockDeviceConfigRegisterWindowReader.RequiresRegisterWindowBinding
            && VirtioBlockDeviceConfigRegisterWindowReader.UsesCallerOwnedWindowBuffer
            && VirtioBlockDeviceConfigRegisterWindowReader.UsesCallerOwnedScratchBuffer
            && VirtioBlockDeviceConfigRegisterWindowReader.InvokesRegisterWindowAccessExecutor
            && VirtioBlockDeviceConfigRegisterWindowReader.ReadsCallerOwnedWindowBuffer
            && VirtioBlockDeviceConfigRegisterWindowReader.WritesCallerOwnedScratchBuffer
            && !VirtioBlockDeviceConfigRegisterWindowReader.MapsBars
            && !VirtioBlockDeviceConfigRegisterWindowReader.PerformsLiveMmio
            && !VirtioBlockDeviceConfigRegisterWindowReader.ContainsVolatileAccess
            && !VirtioBlockDeviceConfigRegisterWindowReader.AccessesPhysicalAddresses
            && !VirtioBlockDeviceConfigRegisterWindowReader.ExecutesMemoryBarriers
            && !VirtioBlockDeviceConfigRegisterWindowReader.ReadsLiveDeviceRegisters
            && !VirtioBlockDeviceConfigRegisterWindowReader.WritesLiveDeviceRegisters
            && !VirtioBlockDeviceConfigRegisterWindowReader.ConfiguresQueues
            && !VirtioBlockDeviceConfigRegisterWindowReader.NotifiesLiveDevice
            && !VirtioBlockDeviceConfigRegisterWindowReader.PollsDevice
            && !VirtioBlockDeviceConfigRegisterWindowReader.HandlesInterrupts
            && !VirtioBlockDeviceConfigRegisterWindowReader.AllocatesDmaMemory
            && !VirtioBlockDeviceConfigRegisterWindowReader.PerformsDmaMapping
            && !VirtioBlockDeviceConfigRegisterWindowReader.PerformsBlockIo
            && !VirtioBlockDeviceConfigRegisterWindowReader.PerformsDiskRead
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockDeviceConfigRegisterWindowReadResult>();
    }

    private static bool TestDefaultConfigResultIsNotSuccess()
    {
        VirtioBlockDeviceConfigResult result = default;
        VirtioBlockDeviceConfigResult failed =
            VirtioBlockDeviceConfigResult.Fail(
                VirtioBlockDeviceConfigStatus.InvalidCapacity,
                BlockDeviceStatus.InvalidBlockCount,
                rawCapacitySectors: 0,
                VirtioBlockDeviceConfig.SectorSizeBytes);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockDeviceConfigStatus.Success
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.RawCapacitySectors == 0
            && result.SectorSizeBytes == 0
            && result.BlockDeviceInfo.BlockCount == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockDeviceConfigStatus.InvalidCapacity
            && failed.BlockDeviceStatus == BlockDeviceStatus.InvalidBlockCount
            && failed.SectorSizeBytes == VirtioBlockDeviceConfig.SectorSizeBytes
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDeviceConfigResult>();
    }

    private static bool TestDefaultRegisterWindowResultIsNotSuccess()
    {
        VirtioBlockDeviceConfigRegisterWindowReadResult result = default;
        byte[] deviceWindowBytes = CreateDeviceWindowBytes(rawCapacitySectors: 4096);
        byte[] scratch = CreateScratch(VirtioBlockDeviceConfig.CapacityLength - 1);
        VirtioBlockDeviceConfigRegisterWindowReadResult failed =
            VirtioBlockDeviceConfigRegisterWindowReader.Read(
                deviceConfiguration: default,
                deviceWindowBytes,
                scratch);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockDeviceConfigRegisterWindowReadStatus.Success
            && result.ConfigStatus == VirtioBlockDeviceConfigStatus.Success
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.RawCapacitySectors == 0
            && result.SectorSizeBytes == 0
            && result.BlockDeviceInfo.BlockCount == 0
            && result.ScratchLength == 0
            && result.BytesRead == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockDeviceConfigRegisterWindowReadStatus.ScratchBufferTooSmall
            && failed.ConfigStatus == VirtioBlockDeviceConfigStatus.ConfigTooSmall
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockDeviceConfigRegisterWindowReadResult>();
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
            VirtioPciDeviceDiscovery.BlockDeviceId;
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
        ulong rawCapacitySectors,
        int length = 0x1000)
    {
        byte[] bytes = new byte[length];
        Fill(bytes, 0xCC);
        if (length < (int)DeviceConfigurationBarOffset + VirtioBlockDeviceConfig.CapacityLength)
        {
            return bytes;
        }

        EndianPrimitives.WriteUInt64LittleEndian(
            bytes.AsSpan((int)DeviceConfigurationBarOffset, VirtioBlockDeviceConfig.CapacityLength),
            rawCapacitySectors);
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
}
