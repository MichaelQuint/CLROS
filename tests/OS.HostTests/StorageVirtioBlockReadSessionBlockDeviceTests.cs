using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockReadSessionBlockDeviceTests
{
    private const ushort QueueSize = 8;
    private const ulong HeaderPhysicalAddress = 0x0010_0000UL;
    private const ulong DataPhysicalAddress = 0x0020_0000UL;
    private const ulong StatusPhysicalAddress = 0x0030_0000UL;
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const uint NotifyApertureBarOffset = 0x80;
    private const uint NotifyOffsetMultiplier = 4;
    private const ulong NotifyBarPhysicalBase = 0x80001000UL;
    private const ulong NotifyBarVirtualBase = 0xFFFF800000001000UL;

    [HostTest("storage.virtioblk.readsession block device reads completed destination", HostLane.Fast, 0)]
    private static bool TestReadsCompletedDestination()
    {
        BlockDeviceInfo info = CreateDevice(blockCount: 4);
        byte[] completedDestination = CreateCompletedDestination(info);
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(
            firstSector: 0,
            sectorCount: 4,
            destinationLength: completedDestination.Length,
            completedByteCount: completedDestination.Length,
            validRequestHeader: true);

        VirtioBlockReadSessionBlockDeviceCreateResult create =
            VirtioBlockReadSessionBlockDevice.Create(
                session,
                info,
                completedDestination,
                out VirtioBlockReadSessionBlockDevice? device);
        if (!create.Succeeded || device is null)
        {
            return HostCheck.Result(false);
        }

        byte[] destination = new byte[1024];
        Array.Fill(destination, (byte)0xCC);
        BlockReadResult read = device.ReadBlocks(1, 2, destination);

        return HostCheck.Result(read.Succeeded
            && read.BlocksRead == 2
            && read.BytesRead == 1024
            && BytesEqual(completedDestination.AsSpan(512, 1024), destination)
            && create.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.Success
            && create.BlockDeviceStatus == BlockDeviceStatus.Success
            && create.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.Success
            && create.ReadSessionPipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && create.CompletedByteCount == info.CapacityBytes
            && create.ExpectedByteCount == info.CapacityBytes
            && create.BackingByteCount == info.CapacityBytes
            && VirtioBlockReadSessionBlockDevice.RequiresCompletedReadSession
            && VirtioBlockReadSessionBlockDevice.RequiresFullDiskReadSession
            && VirtioBlockReadSessionBlockDevice.UsesCallerOwnedCompletedDestination
            && VirtioBlockReadSessionBlockDevice.CopiesFromCompletedDestination
            && !VirtioBlockReadSessionBlockDevice.PerformsMmio
            && !VirtioBlockReadSessionBlockDevice.PerformsDmaMapping
            && !VirtioBlockReadSessionBlockDevice.AllocatesDmaMemory
            && !VirtioBlockReadSessionBlockDevice.MapsBars
            && !VirtioBlockReadSessionBlockDevice.AccessesPhysicalAddresses
            && !VirtioBlockReadSessionBlockDevice.ExecutesMemoryBarriers
            && !VirtioBlockReadSessionBlockDevice.ConfiguresLiveQueues
            && !VirtioBlockReadSessionBlockDevice.NotifiesLiveDevice
            && !VirtioBlockReadSessionBlockDevice.PollsDevice
            && !VirtioBlockReadSessionBlockDevice.HandlesInterrupts
            && !VirtioBlockReadSessionBlockDevice.CallsPal
            && !VirtioBlockReadSessionBlockDevice.PerformsDiskRead
            && !VirtioBlockReadSessionBlockDevice.ReadsRealDisk);
    }

    [HostTest("storage.virtioblk.readsession block device rejects default session", HostLane.Fast, 1)]
    private static bool TestRejectsDefaultSession()
    {
        BlockDeviceInfo info = CreateDevice(blockCount: 4);
        VirtioBlockReadSessionBlockDeviceCreateResult create =
            VirtioBlockReadSessionBlockDevice.Create(
                default,
                info,
                new byte[(int)info.CapacityBytes],
                out VirtioBlockReadSessionBlockDevice? device);

        return HostCheck.Result(!create.Succeeded
            && create.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.IncompleteReadSession
            && create.BlockDeviceStatus == BlockDeviceStatus.IoError
            && create.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.Success
            && create.ReadSessionPipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && create.CompletedByteCount == 0
            && create.ExpectedByteCount == info.CapacityBytes
            && device is null);
    }

    [HostTest("storage.virtioblk.readsession block device rejects incomplete session", HostLane.Fast, 2)]
    private static bool TestRejectsIncompleteSession()
    {
        BlockDeviceInfo info = CreateDevice(blockCount: 4);
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(
            firstSector: 0,
            sectorCount: 4,
            destinationLength: (int)info.CapacityBytes,
            completedByteCount: (int)info.CapacityBytes,
            validRequestHeader: false);
        VirtioBlockReadSessionBlockDeviceCreateResult create =
            VirtioBlockReadSessionBlockDevice.Create(
                session,
                info,
                new byte[(int)info.CapacityBytes],
                out VirtioBlockReadSessionBlockDevice? device);

        return HostCheck.Result(!session.Succeeded
            && !create.Succeeded
            && create.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.IncompleteReadSession
            && create.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.AdapterFailed
            && device is null);
    }

    [HostTest("storage.virtioblk.readsession block device rejects invalid geometry", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidGeometry()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(
            firstSector: 0,
            sectorCount: 4,
            destinationLength: 2048,
            completedByteCount: 2048,
            validRequestHeader: true);
        VirtioBlockReadSessionBlockDeviceCreateResult create =
            VirtioBlockReadSessionBlockDevice.Create(
                session,
                default,
                new byte[2048],
                out VirtioBlockReadSessionBlockDevice? device);

        return HostCheck.Result(!create.Succeeded
            && create.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.InvalidBlockGeometry
            && create.BlockDeviceStatus == BlockDeviceStatus.InvalidBlockSize
            && device is null);
    }

    [HostTest("storage.virtioblk.readsession block device rejects mismatched completed byte counts", HostLane.Fast, 4)]
    private static bool TestRejectsMismatchedCompletedByteCounts()
    {
        BlockDeviceInfo info = CreateDevice(blockCount: 4);
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(
            firstSector: 0,
            sectorCount: 3,
            destinationLength: 1536,
            completedByteCount: 1536,
            validRequestHeader: true);
        VirtioBlockReadSessionBlockDeviceCreateResult create =
            VirtioBlockReadSessionBlockDevice.Create(
                session,
                info,
                new byte[(int)info.CapacityBytes],
                out VirtioBlockReadSessionBlockDevice? device);

        return HostCheck.Result(session.Succeeded
            && !create.Succeeded
            && create.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.MismatchedCompletedByteCount
            && create.BlockDeviceStatus == BlockDeviceStatus.InvalidBlockRange
            && create.CompletedByteCount == 1536
            && create.ExpectedByteCount == info.CapacityBytes
            && device is null);
    }

    [HostTest("storage.virtioblk.readsession block device rejects backing bytes too small", HostLane.Fast, 5)]
    private static bool TestRejectsBackingBytesTooSmall()
    {
        BlockDeviceInfo info = CreateDevice(blockCount: 4);
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(
            firstSector: 0,
            sectorCount: 4,
            destinationLength: (int)info.CapacityBytes,
            completedByteCount: (int)info.CapacityBytes,
            validRequestHeader: true);
        VirtioBlockReadSessionBlockDeviceCreateResult create =
            VirtioBlockReadSessionBlockDevice.Create(
                session,
                info,
                new byte[(int)info.CapacityBytes - 1],
                out VirtioBlockReadSessionBlockDevice? device);

        return HostCheck.Result(session.Succeeded
            && !create.Succeeded
            && create.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.BackingBytesTooSmall
            && create.BlockDeviceStatus == BlockDeviceStatus.DestinationTooSmall
            && create.BackingByteCount == info.CapacityBytes - 1
            && device is null);
    }

    [HostTest("storage.virtioblk.readsession block device reports short read destination", HostLane.Fast, 6)]
    private static bool TestReportsShortReadDestination()
    {
        VirtioBlockReadSessionBlockDevice device = CreateAdapter();
        Span<byte> destination = stackalloc byte[511];
        BlockReadResult read = device.ReadBlocks(0, 1, destination);

        return HostCheck.Result(!read.Succeeded
            && read.Status == BlockDeviceStatus.DestinationTooSmall
            && read.BlocksRead == 0
            && read.BytesRead == 0);
    }

    [HostTest("storage.virtioblk.readsession block device reports out-of-range reads", HostLane.Fast, 7)]
    private static bool TestReportsOutOfRangeReads()
    {
        VirtioBlockReadSessionBlockDevice device = CreateAdapter();
        byte[] destination = new byte[512];
        BlockReadResult atEnd = device.ReadBlocks(4, 1, destination);
        BlockReadResult crossesEnd = device.ReadBlocks(3, 2, new byte[1024]);

        return HostCheck.Result(!atEnd.Succeeded
            && atEnd.Status == BlockDeviceStatus.InvalidBlockRange
            && !crossesEnd.Succeeded
            && crossesEnd.Status == BlockDeviceStatus.InvalidBlockRange);
    }

    [HostTest("storage.virtioblk.readsession block device default result is not success", HostLane.Fast, 8)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioBlockReadSessionBlockDeviceCreateResult result = default;
        VirtioBlockReadSessionBlockDeviceCreateResult failed =
            VirtioBlockReadSessionBlockDevice.Create(
                default,
                default,
                ReadOnlyMemory<byte>.Empty,
                out VirtioBlockReadSessionBlockDevice? device);

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.Success
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && device is null
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.InvalidBlockGeometry
            && device is null);
    }

    private static VirtioBlockReadSessionBlockDevice CreateAdapter()
    {
        BlockDeviceInfo info = CreateDevice(blockCount: 4);
        byte[] completedDestination = CreateCompletedDestination(info);
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(
            firstSector: 0,
            sectorCount: 4,
            destinationLength: completedDestination.Length,
            completedByteCount: completedDestination.Length,
            validRequestHeader: true);
        VirtioBlockReadSessionBlockDeviceCreateResult create =
            VirtioBlockReadSessionBlockDevice.Create(
                session,
                info,
                completedDestination,
                out VirtioBlockReadSessionBlockDevice? device);
        if (!create.Succeeded || device is null)
        {
            throw new InvalidOperationException("Could not create test adapter: " + create.Status);
        }

        return device;
    }

    private static VirtioBlockReadOnlyReadSessionResult ExecuteReadSession(
        ulong firstSector,
        uint sectorCount,
        int destinationLength,
        int completedByteCount,
        bool validRequestHeader)
    {
        BlockDeviceInfo device = CreateDevice(blockCount: 16);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 0,
                usedRingIndex: 0);
        CreateReadPipelineBuffers(
            destinationLength,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        if (!validRequestHeader)
        {
            requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength - 1];
        }

        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 0,
            currentUsedIndex: 1,
            descriptorHead: 0,
            usedByteCount: (uint)completedByteCount);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        if (!windows.Succeeded)
        {
            throw new InvalidOperationException("Invalid test register window binding: " + windows.Status);
        }

        VirtioBlockReadOnlyReadSession session =
            new VirtioBlockReadOnlyReadSession(
                device,
                QueueSize,
                states,
                cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                windows.Bindings.NotifyConfiguration,
                CreateNotifyWindowBytes());

        return session.Read(
            firstSector,
            sectorCount,
            VirtioBlockQueueNotificationBarrierReadiness.Ready(81));
    }

    private static BlockDeviceInfo CreateDevice(ulong blockCount)
    {
        BlockDeviceInfoResult result =
            BlockDeviceInfo.Create(VirtioBlockRequestPlan.SectorSizeBytes, blockCount);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Invalid test block device: " + result.Status);
        }

        return result.Info;
    }

    private static byte[] CreateCompletedDestination(BlockDeviceInfo info)
    {
        byte[] bytes = new byte[(int)info.CapacityBytes];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i & 0xFF);
        }

        return bytes;
    }

    private static void CreateReadPipelineBuffers(
        int destinationLength,
        out byte[] requestHeader,
        out byte[] destination,
        out byte[] status,
        out byte[] queueMemory)
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        destination = new byte[destinationLength];
        status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        queueMemory = new byte[(int)queue.Plan.EndOffset];
    }

    private static void PrewriteCompletionSnapshot(
        byte[] queueMemory,
        byte[] status,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        uint descriptorHead,
        uint usedByteCount)
    {
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        WriteUsedCompletion(
            VirtqueueSplitRingPlan.Create(QueueSize).Plan,
            queueMemory,
            previousUsedIndex,
            currentUsedIndex,
            descriptorHead,
            usedByteCount);
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

    private static VirtioPciBarRegisterWindowBindingResult CreateBoundRegisterWindows()
    {
        return VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
            CreateBindingPlan(),
            CreateRegisterWindows());
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.BlockDeviceId;

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
            offsetValue: NotifyApertureBarOffset,
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
            length: VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            offsetValue: 0,
            length: VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength,
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

    private static VirtioPciMappedBarWindow[] CreateRegisterWindows()
    {
        return new VirtioPciMappedBarWindow[]
        {
            CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL),
            CreateRegisterWindow(1, NotifyBarPhysicalBase, NotifyBarVirtualBase),
            CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL),
            CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL),
            default,
            default
        };
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

    private static byte[] CreateNotifyWindowBytes()
    {
        byte[] notifyBytes = new byte[0x1000];
        Array.Fill(notifyBytes, (byte)0xCC);
        return notifyBytes;
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

    private static bool BytesEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }
}
