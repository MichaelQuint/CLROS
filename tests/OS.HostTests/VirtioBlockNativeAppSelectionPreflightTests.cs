using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Diagnostics;
using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Storage;

internal static class VirtioBlockNativeAppSelectionPreflightTests
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

    private const uint SectorSize = 512;
    private const ulong DiskBlockCount = 256;
    private const ulong PartitionStartLba = 40;
    private const ulong PartitionBlockCount = 128;
    private const ushort ReservedSectorCount = 32;
    private const byte FatCount = 2;
    private const uint FatSizeSectors = 8;
    private const uint RootCluster = 2;
    private const uint TotalSectors = 128;
    private const uint ManifestCluster = 15;
    private const uint OsDirectoryCluster = 16;
    private const uint PackageCluster = 17;
    private const ulong RootDirectoryLba = ReservedSectorCount + (FatCount * FatSizeSectors);
    private const ulong PartitionEntryLba = 2;
    private const ulong BackupPartitionEntryLba = DiskBlockCount - 33;
    private const ulong BackupHeaderLba = DiskBlockCount - 1;
    private const uint PartitionEntryCount = 128;
    private const uint PartitionEntrySize = 128;
    private const ulong FirstUsableLba = 34;
    private const ulong LastUsableLba = 220;
    private const string AppPath = "apps/hello.bin";
    private const int AppContentLength = 48;

    private static readonly Guid DiskGuid = new("61616161-3434-5656-7878-909090909090");
    private static readonly Guid EspGuid = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid UniquePartitionGuid = new("eeeeeeee-1111-2222-3333-ffffffffffff");

    [HostTest("appmodel.virtio selection preflight validates scratch", HostLane.Fast, 0)]
    private static bool TestValidatesScratch()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath));
        SelectionScratch scratch = new();

        VirtioBlockNativeAppSelectionPreflightResult success = Load(
            session,
            disk,
            scratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        SelectionScratch shortScratch = new(
            manifestBufferLength: checked((int)NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes - 1));
        VirtioBlockNativeAppSelectionPreflightResult shortManifest = Load(
            session,
            disk,
            shortScratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(success.Succeeded
            && success.ScratchPlanAttempted
            && success.ScratchPlan.Succeeded
            && success.ScratchPlan.ManifestBytesRequired == NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes
            && success.ScratchPlan.PackageBytesRequired == NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes
            && success.SelectionLoadAttempted
            && !shortManifest.Succeeded
            && shortManifest.Status == VirtioBlockNativeAppSelectionPreflightStatus.ScratchPlanFailed
            && shortManifest.ScratchPlanAttempted
            && shortManifest.ScratchPlan.Status == NativeAppSelectionBootScratchPlanStatus.ManifestBufferTooSmall
            && shortManifest.ScratchPlan.ManifestBytesRequired == NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes
            && shortManifest.ScratchPlan.ManifestBytesAvailable == NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes - 1
            && !shortManifest.SelectionLoadAttempted);
    }

    [HostTest("appmodel.virtio selection preflight rejects incomplete session", HostLane.Fast, 1)]
    private static bool TestRejectsIncompleteSession()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: false);
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath));
        SelectionScratch scratch = new();

        VirtioBlockNativeAppSelectionPreflightResult result = Load(
            session,
            disk,
            scratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(!session.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockNativeAppSelectionPreflightStatus.IncompleteReadSession
            && !result.ReadSessionComplete
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.AdapterFailed
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && !result.ScratchPlanAttempted
            && !result.SelectionLoadAttempted
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorBefore.UsedRingIndex == 8
            && result.CursorAfter.AvailableRingIndex == 4
            && result.CursorAfter.UsedRingIndex == 8);
    }

    [HostTest("appmodel.virtio selection preflight preserves selection provenance", HostLane.Fast, 2)]
    private static bool TestPreservesSelectionProvenance()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath));
        SelectionScratch scratch = new();

        VirtioBlockNativeAppSelectionPreflightResult result = Load(
            session,
            disk,
            scratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioBlockNativeAppSelectionPreflightStatus.Success
            && result.ReadSessionComplete
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.Success
            && result.ReadSessionPipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && result.ReadSessionByteCount == 1024
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorBefore.UsedRingIndex == 8
            && result.CursorAfter.AvailableRingIndex == 5
            && result.CursorAfter.UsedRingIndex == 9
            && result.SelectionLoadAttempted
            && result.SelectionLoad.Succeeded
            && result.SelectionLoad.ManifestGptTableSource == GptPartitionTableSource.Primary
            && result.SelectionLoad.PackageGptTableSource == GptPartitionTableSource.Primary
            && result.SelectionLoad.PackageLoad.Status == OsPackageLoadStatus.Success
            && result.SelectionLoad.PackageParse.Succeeded
            && result.SelectionLoad.LaunchPlan.Succeeded
            && result.SelectionLoad.Descriptor.Succeeded
            && result.SelectionLoad.Descriptor.Descriptor.AppBinaryLength == AppContentLength
            && result.SelectionLoad.Descriptor.Descriptor.PayloadHeader.EntryOffset == NativeAppPayloadHeader.HeaderSize
            && result.SelectionLoad.Descriptor.Descriptor.NetworkMode == NativeManifestNetworkMode.Dhcp
            && result.SelectionLoad.Descriptor.Descriptor.LogMode == NativeManifestLogMode.Serial
            && FieldEquals(scratch.ManifestBuffer, result.SelectionLoad.ManifestLoad.Manifest.AppPath, AppPath));
    }

    [HostTest("appmodel.virtio selection preflight reads app from session destination", HostLane.Fast, 3)]
    private static bool TestReadsAppSelectionFromReadSessionDestination()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath));
        VirtioBlockReadOnlyReadSessionResult session =
            ExecuteFullDiskReadSession(disk, out byte[] readDestination);
        SelectionScratch scratch = new();

        VirtioBlockNativeAppSelectionPreflightResult result =
            VirtioBlockNativeAppSelectionPreflight.LoadSelection(
                session,
                MemoryBlockDevice.FromImage(readDestination, SectorSize),
                scratch.PrimaryHeader,
                scratch.PartitionEntries,
                scratch.Partitions,
                scratch.ManifestBuffer,
                scratch.PackageBuffer,
                BootInfo.CreateHost(BootCapabilities.TextOutput),
                NativeAppLaunchRuntimeCapabilities.PackageStorage |
                NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(session.Succeeded
            && session.BlockRead.Succeeded
            && session.BlockRead.BlocksRead == DiskBlockCount
            && session.BlockRead.BytesRead == (ulong)disk.Length
            && readDestination.AsSpan().SequenceEqual(disk)
            && result.Succeeded
            && result.ReadSessionByteCount == (ulong)disk.Length
            && result.SelectionLoadAttempted
            && result.SelectionLoad.Succeeded
            && result.SelectionLoad.ManifestLoad.Succeeded
            && result.SelectionLoad.PackageLoad.Succeeded
            && result.SelectionLoad.PackageParse.Succeeded
            && result.SelectionLoad.LaunchPlan.Succeeded
            && result.SelectionLoad.Descriptor.Succeeded
            && result.SelectionLoad.Descriptor.Descriptor.AppBinaryLength == AppContentLength
            && FieldEquals(scratch.ManifestBuffer, result.SelectionLoad.ManifestLoad.Manifest.AppPath, AppPath));
    }

    [HostTest("appmodel.virtio selection preflight adapts completed read session block device", HostLane.Fast, 4)]
    private static bool TestAdaptsCompletedReadSessionBlockDevice()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath));
        VirtioBlockReadOnlyReadSessionResult session =
            ExecuteFullDiskReadSession(disk, out byte[] readDestination);
        BlockDeviceInfo expectedInfo = CreateDevice(DiskBlockCount);
        SelectionScratch scratch = new();

        VirtioBlockNativeAppSelectionPreflightResult result =
            VirtioBlockNativeAppSelectionPreflight.LoadSelectionFromCompletedReadSession(
                session,
                expectedInfo,
                readDestination,
                scratch.PrimaryHeader,
                scratch.PartitionEntries,
                scratch.Partitions,
                scratch.ManifestBuffer,
                scratch.PackageBuffer,
                BootInfo.CreateHost(BootCapabilities.TextOutput),
                NativeAppLaunchRuntimeCapabilities.PackageStorage |
                NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(session.Succeeded
            && readDestination.AsSpan().SequenceEqual(disk)
            && result.Succeeded
            && result.Status == VirtioBlockNativeAppSelectionPreflightStatus.Success
            && result.AdapterCreateAttempted
            && result.AdapterCreate.Succeeded
            && result.AdapterCreate.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.Success
            && result.AdapterCreate.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.AdapterCreate.CompletedByteCount == (ulong)disk.Length
            && result.AdapterCreate.ExpectedByteCount == expectedInfo.CapacityBytes
            && result.AdapterCreate.BackingByteCount == expectedInfo.CapacityBytes
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.ScratchPlanAttempted
            && result.ScratchPlan.Succeeded
            && result.SelectionLoadAttempted
            && result.SelectionLoad.Succeeded
            && result.SelectionLoad.PackageLoad.Succeeded
            && result.SelectionLoad.Descriptor.Succeeded
            && result.SelectionLoad.Descriptor.Descriptor.AppBinaryLength == AppContentLength
            && FieldEquals(scratch.ManifestBuffer, result.SelectionLoad.ManifestLoad.Manifest.AppPath, AppPath)
            && VirtioBlockNativeAppSelectionPreflight.UsesCompletedReadSessionBlockDeviceAdapter
            && VirtioBlockNativeAppSelectionPreflight.PreservesAdapterCreateProvenance);
    }

    [HostTest("appmodel.virtio selection preflight reports adapter creation failure", HostLane.Fast, 5)]
    private static bool TestReportsAdapterCreationFailure()
    {
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath));
        VirtioBlockReadOnlyReadSessionResult session =
            ExecuteFullDiskReadSession(disk, out byte[] readDestination);
        BlockDeviceInfo expectedInfo = CreateDevice(DiskBlockCount);
        SelectionScratch scratch = new();

        VirtioBlockNativeAppSelectionPreflightResult result =
            VirtioBlockNativeAppSelectionPreflight.LoadSelectionFromCompletedReadSession(
                session,
                expectedInfo,
                readDestination.AsMemory(0, readDestination.Length - 1),
                scratch.PrimaryHeader,
                scratch.PartitionEntries,
                scratch.Partitions,
                scratch.ManifestBuffer,
                scratch.PackageBuffer,
                BootInfo.CreateHost(BootCapabilities.TextOutput),
                NativeAppLaunchRuntimeCapabilities.PackageStorage |
                NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(session.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockNativeAppSelectionPreflightStatus.InvalidBlockDevice
            && result.BlockDeviceStatus == BlockDeviceStatus.DestinationTooSmall
            && result.AdapterCreateAttempted
            && !result.AdapterCreate.Succeeded
            && result.AdapterCreate.Status == VirtioBlockReadSessionBlockDeviceCreateStatus.BackingBytesTooSmall
            && result.AdapterCreate.BlockDeviceStatus == BlockDeviceStatus.DestinationTooSmall
            && result.AdapterCreate.CompletedByteCount == (ulong)disk.Length
            && result.AdapterCreate.ExpectedByteCount == expectedInfo.CapacityBytes
            && result.AdapterCreate.BackingByteCount == expectedInfo.CapacityBytes - 1
            && !result.ScratchPlanAttempted
            && !result.SelectionLoadAttempted
            && result.ScratchPlan.Status == NativeAppSelectionBootScratchPlanStatus.Success
            && result.SelectionLoad.Status == NativeAppSelectionLoadStatus.Success);
    }

    [HostTest("appmodel.virtio selection preflight preserves no-launch boundary", HostLane.Fast, 6)]
    private static bool TestPreservesNoLaunchBoundary()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath));
        SelectionScratch scratch = new();

        VirtioBlockNativeAppSelectionPreflightResult result = Load(
            session,
            disk,
            scratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);

        return HostCheck.Result(result.Succeeded
            && VirtioBlockNativeAppSelectionPreflight.ValidatesCallerOwnedReadSession
            && VirtioBlockNativeAppSelectionPreflight.RejectsIncompleteReadSessionBeforeScratchValidation
            && VirtioBlockNativeAppSelectionPreflight.ValidatesCallerOwnedBlockDeviceGeometry
            && VirtioBlockNativeAppSelectionPreflight.UsesCompletedReadSessionBlockDeviceAdapter
            && VirtioBlockNativeAppSelectionPreflight.ValidatesBootScratchPlan
            && VirtioBlockNativeAppSelectionPreflight.DelegatesNativeAppSelectionLoader
            && VirtioBlockNativeAppSelectionPreflight.PreservesReadSessionProvenance
            && VirtioBlockNativeAppSelectionPreflight.PreservesAdapterCreateProvenance
            && VirtioBlockNativeAppSelectionPreflight.PreservesScratchPlanProvenance
            && VirtioBlockNativeAppSelectionPreflight.PreservesSelectionLoadProvenance
            && !VirtioBlockNativeAppSelectionPreflight.AllocatesBuffers
            && !VirtioBlockNativeAppSelectionPreflight.NotifiesLiveDevice
            && !VirtioBlockNativeAppSelectionPreflight.WaitsForHardware
            && !VirtioBlockNativeAppSelectionPreflight.PerformsDmaMapping
            && !VirtioBlockNativeAppSelectionPreflight.MapsBars
            && !VirtioBlockNativeAppSelectionPreflight.PerformsMmio
            && !VirtioBlockNativeAppSelectionPreflight.ReadsRealDisk
            && !VirtioBlockNativeAppSelectionPreflight.ExecutesAppCode
            && !VirtioBlockNativeAppSelectionPreflight.StartsHostedDotNet
            && !VirtioBlockNativeAppSelectionPreflight.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockNativeAppSelectionPreflightResult>());
    }

    [HostTest("appmodel.virtio selection preflight default result is not success", HostLane.Fast, 7)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioBlockNativeAppSelectionPreflightResult result = default;

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockNativeAppSelectionPreflightStatus.Success
            && !result.IsInitialized
            && !result.ReadSessionComplete
            && !result.AdapterCreateAttempted
            && !result.AdapterCreate.Succeeded
            && !result.ScratchPlanAttempted
            && !result.SelectionLoadAttempted);
    }

    [HostTest("boot.sequence virtio app selection preflight ready", HostLane.Fast, 8)]
    private static bool TestBootSequencePreflightReady()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath));
        SelectionScratch scratch = new();
        VirtioBlockNativeAppSelectionPreflightResult preflight = Load(
            session,
            disk,
            scratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage | NativeAppLaunchRuntimeCapabilities.Network);
        SequenceTestSerialSink serial = new();
        BootEventRingBuffer events = new(8);

        BootResult result = BootSequence.RunAppSelectionPreflightReadiness(preflight, serial, events);

        return HostCheck.Result(preflight.Succeeded
            && preflight.IsInitialized
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.AppSelectionPreflightReady
            && events.Snapshot()[0] == BootSequence.AppSelectionPreflightReady);
    }

    [HostTest("boot.sequence virtio app selection preflight blocks default result", HostLane.Fast, 9)]
    private static bool TestBootSequencePreflightBlocksDefaultResult()
    {
        VirtioBlockNativeAppSelectionPreflightResult preflight = default;
        SequenceTestSerialSink serial = new();
        BootEventRingBuffer events = new(8);

        BootResult result = BootSequence.RunAppSelectionPreflightReadiness(preflight, serial, events);
        string expectedMarker = BootSequence.CreateAppSelectionPreflightNotReadyMarker(
            AppSelectionPreflightReadinessStatus.NotInitialized,
            preflight);

        return HostCheck.Result(!preflight.Succeeded
            && !preflight.IsInitialized
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(AppSelectionPreflightReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker.Contains("initialized=false", StringComparison.Ordinal));
    }

    [HostTest("boot.sequence virtio app selection preflight reports selection failure", HostLane.Fast, 10)]
    private static bool TestBootSequencePreflightReportsSelectionFailure()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        byte[] disk = CreateDiskImage(ValidManifest(AppPath), ValidPackage(AppPath));
        SelectionScratch scratch = new();
        VirtioBlockNativeAppSelectionPreflightResult preflight = Load(
            session,
            disk,
            scratch,
            NativeAppLaunchRuntimeCapabilities.PackageStorage);
        SequenceTestSerialSink serial = new();
        BootEventRingBuffer events = new(8);

        BootResult result = BootSequence.RunAppSelectionPreflightReadiness(preflight, serial, events);
        string expectedMarker = BootSequence.CreateAppSelectionPreflightNotReadyMarker(
            AppSelectionPreflightReadinessStatus.SelectionLoadFailed,
            preflight);

        return HostCheck.Result(!preflight.Succeeded
            && preflight.IsInitialized
            && preflight.Status == VirtioBlockNativeAppSelectionPreflightStatus.SelectionLoadFailed
            && preflight.SelectionLoad.Status == NativeAppSelectionLoadStatus.LaunchPlanFailed
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(AppSelectionPreflightReadinessStatus.SelectionLoadFailed),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker.Contains("selection=LaunchPlanFailed", StringComparison.Ordinal)
            && expectedMarker.Contains("launch=MissingRuntimeCapabilities", StringComparison.Ordinal));
    }

    private static VirtioBlockNativeAppSelectionPreflightResult Load(
        VirtioBlockReadOnlyReadSessionResult session,
        byte[] disk,
        SelectionScratch scratch,
        NativeAppLaunchRuntimeCapabilities runtimeCapabilities)
    {
        return VirtioBlockNativeAppSelectionPreflight.LoadSelection(
            session,
            MemoryBlockDevice.FromImage(disk, SectorSize),
            scratch.PrimaryHeader,
            scratch.PartitionEntries,
            scratch.Partitions,
            scratch.ManifestBuffer,
            scratch.PackageBuffer,
            BootInfo.CreateHost(BootCapabilities.TextOutput),
            runtimeCapabilities);
    }

    private static VirtioBlockReadOnlyReadSessionResult ExecuteReadSession(bool validRequestHeader)
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
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
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: 1024);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        if (!windows.Succeeded)
        {
            throw new InvalidOperationException("Invalid test register window binding: " + windows.Status);
        }

        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockReadOnlyReadSession readSession =
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
                notifyBytes);

        return readSession.Read(
            firstSector: 4,
            sectorCount: 2,
            VirtioBlockQueueNotificationBarrierReadiness.Ready(82));
    }

    private static VirtioBlockReadOnlyReadSessionResult ExecuteFullDiskReadSession(
        byte[] simulatedDiskBytes,
        out byte[] destination)
    {
        BlockDeviceInfo device = CreateDevice(DiskBlockCount);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: simulatedDiskBytes.Length,
            out byte[] requestHeader,
            out destination,
            out byte[] status,
            out byte[] queueMemory);
        simulatedDiskBytes.CopyTo(destination.AsSpan());

        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: checked((uint)simulatedDiskBytes.Length));
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        if (!windows.Succeeded)
        {
            throw new InvalidOperationException("Invalid test register window binding: " + windows.Status);
        }

        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockReadOnlyReadSession readSession =
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
                notifyBytes);

        return readSession.Read(
            firstSector: 0,
            sectorCount: checked((uint)(simulatedDiskBytes.Length / SectorSize)),
            VirtioBlockQueueNotificationBarrierReadiness.Ready(83));
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

    private static byte[] CreateNotifyWindowBytes(int length = 0x1000)
    {
        byte[] notifyBytes = new byte[length];
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

    private static byte[] CreateDiskImage(byte[] manifest, byte[] package)
    {
        byte[] disk = new byte[checked((int)(DiskBlockCount * SectorSize))];
        WriteGpt(disk);
        WriteFat32Volume(disk, checked((int)(PartitionStartLba * SectorSize)), manifest, package);
        return disk;
    }

    private static void WriteGpt(byte[] disk)
    {
        int primaryHeaderOffset = checked((int)SectorSize);
        int backupHeaderOffset = checked((int)(BackupHeaderLba * SectorSize));
        WriteGptHeader(
            disk,
            primaryHeaderOffset,
            currentLba: GptPartitionTableParser.PrimaryHeaderLba,
            backupLba: BackupHeaderLba,
            partitionEntryLba: PartitionEntryLba);
        WriteGptHeader(
            disk,
            backupHeaderOffset,
            currentLba: BackupHeaderLba,
            backupLba: GptPartitionTableParser.PrimaryHeaderLba,
            partitionEntryLba: BackupPartitionEntryLba);
        WritePartitionEntry(
            disk,
            PartitionEntryLba,
            EspGuid,
            UniquePartitionGuid,
            PartitionStartLba,
            PartitionStartLba + PartitionBlockCount - 1,
            "ESP");
        WritePartitionEntry(
            disk,
            BackupPartitionEntryLba,
            EspGuid,
            UniquePartitionGuid,
            PartitionStartLba,
            PartitionStartLba + PartitionBlockCount - 1,
            "ESP");
        RefreshGptCrcs(disk);
    }

    private static void WriteGptHeader(
        byte[] disk,
        int headerOffset,
        ulong currentLba,
        ulong backupLba,
        ulong partitionEntryLba)
    {
        WriteAscii(disk, headerOffset, "EFI PART");
        WriteUInt32(disk, headerOffset + 8, 0x00010000);
        WriteUInt32(disk, headerOffset + 12, GptParser.MinimumHeaderSize);
        WriteUInt64(disk, headerOffset + 24, currentLba);
        WriteUInt64(disk, headerOffset + 32, backupLba);
        WriteUInt64(disk, headerOffset + 40, FirstUsableLba);
        WriteUInt64(disk, headerOffset + 48, LastUsableLba);
        WriteGuid(disk, headerOffset + 56, DiskGuid);
        WriteUInt64(disk, headerOffset + 72, partitionEntryLba);
        WriteUInt32(disk, headerOffset + 80, PartitionEntryCount);
        WriteUInt32(disk, headerOffset + 84, PartitionEntrySize);
    }

    private static void WritePartitionEntry(
        byte[] disk,
        ulong partitionEntryLba,
        Guid partitionTypeGuid,
        Guid uniquePartitionGuid,
        ulong firstLba,
        ulong lastLba,
        string name)
    {
        int entryOffset = checked((int)(partitionEntryLba * SectorSize));
        WriteGuid(disk, entryOffset, partitionTypeGuid);
        WriteGuid(disk, entryOffset + 16, uniquePartitionGuid);
        WriteUInt64(disk, entryOffset + 32, firstLba);
        WriteUInt64(disk, entryOffset + 40, lastLba);
        WriteUtf16Name(disk, entryOffset + 56, name);
    }

    private static void WriteFat32Volume(
        byte[] disk,
        int partitionOffset,
        byte[] manifest,
        byte[] package)
    {
        disk[partitionOffset] = 0xEB;
        disk[partitionOffset + 1] = 0x58;
        disk[partitionOffset + 2] = 0x90;
        WriteAscii(disk, partitionOffset + 3, "OSFAT32 ");
        WriteUInt16(disk, partitionOffset + 11, (ushort)SectorSize);
        disk[partitionOffset + 13] = 1;
        WriteUInt16(disk, partitionOffset + 14, ReservedSectorCount);
        disk[partitionOffset + 16] = FatCount;
        WriteUInt16(disk, partitionOffset + 17, 0);
        WriteUInt16(disk, partitionOffset + 19, 0);
        disk[partitionOffset + 21] = 0xF8;
        WriteUInt16(disk, partitionOffset + 22, 0);
        WriteUInt32(disk, partitionOffset + 32, TotalSectors);
        WriteUInt32(disk, partitionOffset + 36, FatSizeSectors);
        WriteUInt32(disk, partitionOffset + 44, RootCluster);
        WriteAscii(disk, partitionOffset + 82, "FAT32   ");
        disk[partitionOffset + 510] = 0x55;
        disk[partitionOffset + 511] = 0xAA;

        int rootDirectory = partitionOffset + checked((int)(RootDirectoryLba * SectorSize));
        WriteDirectoryEntry(disk, rootDirectory, "OS         ", Fat32Parser.DirectoryAttribute, OsDirectoryCluster, 0);
        disk[rootDirectory + 32] = 0x00;

        int osDirectory = partitionOffset + ClusterOffset(OsDirectoryCluster);
        WriteDirectoryEntry(disk, osDirectory, ".          ", Fat32Parser.DirectoryAttribute, OsDirectoryCluster, 0);
        WriteDirectoryEntry(disk, osDirectory + 32, "..         ", Fat32Parser.DirectoryAttribute, RootCluster, 0);
        WriteLongFileNameEntry(disk, osDirectory + 64, "MANIFEST.JSON", "MANIFE~1JSO");
        WriteDirectoryEntry(disk, osDirectory + 96, "MANIFE~1JSO", 0x20, ManifestCluster, (uint)manifest.Length);
        WriteDirectoryEntry(disk, osDirectory + 128, "APP     PKG", 0x20, PackageCluster, (uint)package.Length);
        disk[osDirectory + 160] = 0x00;

        manifest.CopyTo(disk.AsSpan(partitionOffset + ClusterOffset(ManifestCluster)));
        package.CopyTo(disk.AsSpan(partitionOffset + ClusterOffset(PackageCluster)));
    }

    private static int ClusterOffset(uint cluster)
    {
        ulong lba = RootDirectoryLba + (cluster - RootCluster);
        return checked((int)(lba * SectorSize));
    }

    private static byte[] ValidManifest(string appPath)
    {
        return Encoding.ASCII.GetBytes(
            "format=os-native-manifest\n" +
            "version=0\n" +
            "app.name=hello.app\n" +
            "app.kind=package\n" +
            "app.path=" + appPath + "\n" +
            "runtime.kind=native-aot\n" +
            "args=--port=8080\n" +
            "network=dhcp\n" +
            "log=serial\n");
    }

    private static byte[] ValidPackage(string appPath)
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = OsPackageParser.FileTableEntrySize;
        int pathOffset = tableOffset + tableLength;
        int contentOffset = pathOffset + appPath.Length;
        int packageLength = contentOffset + AppContentLength;

        byte[] package = new byte[packageLength];
        WriteAscii(package, 0, "OSPKGv0");
        package[7] = 0;
        WriteUInt16(package, 8, OsPackageParser.SupportedVersion);
        WriteUInt16(package, 10, OsPackageParser.HeaderSize);
        WriteUInt16(package, 12, 1);
        WriteUInt16(package, 14, OsPackageParser.FileTableEntrySize);
        WriteUInt32(package, 16, tableOffset);
        WriteUInt32(package, 20, tableLength);
        WriteUInt32(package, 24, 0);
        WriteUInt32(package, 28, 0);
        WriteUInt32(package, tableOffset, pathOffset);
        WriteUInt16(package, tableOffset + 4, (ushort)appPath.Length);
        WriteUInt16(package, tableOffset + 6, 0);
        WriteUInt64(package, tableOffset + 8, (ulong)contentOffset);
        WriteUInt64(package, tableOffset + 16, (ulong)AppContentLength);
        WriteUInt64(package, tableOffset + 24, 0);
        WriteAscii(package, pathOffset, appPath);
        WritePayloadHeader(package, contentOffset);

        for (int index = NativeAppPayloadHeader.HeaderSize; index < AppContentLength; index++)
        {
            package[contentOffset + index] = (byte)(0x41 + (index & 0x0F));
        }

        return package;
    }

    private static void WritePayloadHeader(byte[] package, int offset)
    {
        package[offset] = (byte)'O';
        package[offset + 1] = (byte)'S';
        package[offset + 2] = (byte)'A';
        package[offset + 3] = (byte)'P';
        package[offset + 4] = (byte)'P';
        package[offset + 5] = (byte)'v';
        package[offset + 6] = (byte)'0';
        package[offset + 7] = 0;
        WriteUInt16(package, offset + 8, NativeAppPayloadHeader.SupportedVersion);
        WriteUInt16(package, offset + 10, NativeAppPayloadHeader.HeaderSize);
        WriteUInt32(package, offset + 12, 0);
        WriteUInt64(package, offset + 16, NativeAppPayloadHeader.HeaderSize);
        WriteUInt64(package, offset + 24, 0);
    }

    private static bool FieldEquals(byte[] source, NativeManifestField field, string expected)
    {
        if (!field.TryGetSlice(source, out ReadOnlySpan<byte> value) || value.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != (byte)expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteDirectoryEntry(
        byte[] image,
        int offset,
        string rawShortName,
        byte attributes,
        uint firstCluster,
        uint fileSize)
    {
        WriteAscii(image, offset, rawShortName);
        image[offset + 11] = attributes;
        WriteUInt16(image, offset + 20, (ushort)(firstCluster >> 16));
        WriteUInt16(image, offset + 26, (ushort)firstCluster);
        WriteUInt32(image, offset + 28, fileSize);
    }

    private static void WriteLongFileNameEntry(
        byte[] image,
        int offset,
        string fileName,
        string rawShortName)
    {
        image[offset] = 0x41;
        image[offset + 11] = 0x0F;
        image[offset + 12] = 0;
        image[offset + 13] = ComputeLongFileNameChecksum(rawShortName);
        WriteUInt16(image, offset + 26, 0);

        int fileNameIndex = 0;
        bool wroteTerminator = false;
        WriteLongFileNameChars(image, offset + 1, 5, fileName, ref fileNameIndex, ref wroteTerminator);
        WriteLongFileNameChars(image, offset + 14, 6, fileName, ref fileNameIndex, ref wroteTerminator);
        WriteLongFileNameChars(image, offset + 28, 2, fileName, ref fileNameIndex, ref wroteTerminator);
    }

    private static void WriteLongFileNameChars(
        byte[] image,
        int offset,
        int count,
        string fileName,
        ref int fileNameIndex,
        ref bool wroteTerminator)
    {
        for (int i = 0; i < count; i++)
        {
            ushort value;
            if (fileNameIndex < fileName.Length)
            {
                value = (ushort)fileName[fileNameIndex++];
            }
            else if (!wroteTerminator)
            {
                value = 0;
                wroteTerminator = true;
            }
            else
            {
                value = 0xFFFF;
            }

            WriteUInt16(image, offset + i * 2, value);
        }
    }

    private static byte ComputeLongFileNameChecksum(string rawShortName)
    {
        byte sum = 0;
        for (int index = 0; index < rawShortName.Length; index++)
        {
            sum = (byte)(((sum & 1) == 1 ? 0x80 : 0) + (sum >> 1) + (byte)rawShortName[index]);
        }

        return sum;
    }

    private static void RefreshGptCrcs(byte[] image)
    {
        int primaryHeaderOffset = checked((int)SectorSize);
        int backupHeaderOffset = checked((int)(BackupHeaderLba * SectorSize));
        int entryOffset = checked((int)(PartitionEntryLba * SectorSize));
        int entryByteCount = checked((int)(PartitionEntryCount * PartitionEntrySize));
        uint entryCrc32 = Crc32.Compute(image.AsSpan(entryOffset, entryByteCount));
        WriteUInt32(image, primaryHeaderOffset + 88, entryCrc32);
        WriteUInt32(image, backupHeaderOffset + 88, entryCrc32);
        RefreshGptHeaderCrc32(image, primaryHeaderOffset);
        RefreshGptHeaderCrc32(image, backupHeaderOffset);
    }

    private static void RefreshGptHeaderCrc32(byte[] image, int headerOffset)
    {
        WriteUInt32(image, headerOffset + 16, 0);
        WriteUInt32(
            image,
            headerOffset + 16,
            Crc32.Compute(image.AsSpan(headerOffset, checked((int)GptParser.MinimumHeaderSize))));
    }

    private static void WriteAscii(byte[] image, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            image[offset + index] = (byte)value[index];
        }
    }

    private static void WriteUtf16Name(byte[] image, int offset, string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            WriteUInt16(image, offset + index * 2, (ushort)value[index]);
        }
    }

    private static void WriteGuid(byte[] image, int offset, Guid value)
    {
        if (!value.TryWriteBytes(image.AsSpan(offset, 16)))
        {
            throw new InvalidOperationException("Could not write test GUID.");
        }
    }

    private static void WriteUInt16(byte[] image, int offset, int value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), (ushort)value);
    }

    private static void WriteUInt32(byte[] image, int offset, int value)
    {
        WriteUInt32(image, offset, (uint)value);
    }

    private static void WriteUInt32(byte[] image, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset, 4), value);
    }

    private static void WriteUInt64(byte[] image, int offset, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(image.AsSpan(offset, 8), value);
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private sealed class SelectionScratch
    {
        public SelectionScratch(
            int manifestBufferLength = (int)NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes,
            int packageBufferLength = (int)NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes)
        {
            ManifestBuffer = new byte[manifestBufferLength];
            PackageBuffer = new byte[packageBufferLength];
        }

        public byte[] PrimaryHeader { get; } = new byte[SectorSize];

        public byte[] PartitionEntries { get; } =
            new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];

        public GptPartitionDescriptor[] Partitions { get; } = new GptPartitionDescriptor[2];

        public byte[] ManifestBuffer { get; }

        public byte[] PackageBuffer { get; }
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }
    }

    private sealed class MemoryBlockDevice : IReadOnlyBlockDevice
    {
        private MemoryBlockDevice(BlockDeviceInfo info, byte[] image)
        {
            Info = info;
            Image = image;
        }

        public BlockDeviceInfo Info { get; }

        private byte[] Image { get; }

        public static MemoryBlockDevice FromImage(byte[] image, uint blockSize)
        {
            BlockDeviceInfoResult info = BlockDeviceInfo.Create(blockSize, (ulong)image.Length / blockSize);
            if (!info.Succeeded)
            {
                throw new InvalidOperationException("Invalid test image geometry: " + info.Status);
            }

            return new MemoryBlockDevice(info.Info, image);
        }

        public BlockReadResult ReadBlocks(ulong firstBlock, uint blockCount, Span<byte> destination)
        {
            BlockReadResult result = BlockDeviceRead.Validate(Info, firstBlock, blockCount, destination);
            if (!result.Succeeded)
            {
                return result;
            }

            int offset = checked((int)(firstBlock * Info.BlockSize));
            Image.AsSpan(offset, checked((int)result.BytesRead)).CopyTo(destination);
            return result;
        }
    }
}
