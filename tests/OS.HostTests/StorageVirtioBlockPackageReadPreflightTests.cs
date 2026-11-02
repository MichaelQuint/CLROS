using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Diagnostics;
using OS.Manifest;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Storage;

internal static partial class StorageVirtioBlockPackageReadPreflightTests
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
    private const uint PackageCluster = 15;
    private const uint OsDirectoryCluster = 16;
    private const ulong RootDirectoryLba = ReservedSectorCount + (FatCount * FatSizeSectors);
    private const ulong PartitionEntryLba = 2;
    private const ulong BackupPartitionEntryLba = DiskBlockCount - 33;
    private const ulong BackupHeaderLba = DiskBlockCount - 1;
    private const uint PartitionEntryCount = 128;
    private const uint PartitionEntrySize = 128;
    private const ulong FirstUsableLba = 34;
    private const ulong LastUsableLba = 220;

    private static readonly Guid DiskGuid = new Guid("15151515-3434-5656-7878-909090909090");
    private static readonly Guid EspGuid = new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid UniquePartitionGuid = new Guid("bbbbbbbb-1111-2222-3333-cccccccccccc");

    [HostTest("storage.virtio package preflight validates buffers", HostLane.Fast, 0)]
    private static bool TestValidatesBuffers()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        byte[] package = ValidPackage();
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        byte[] headerScratch = new byte[SectorSize];
        byte[] partitionEntryScratch = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] partitions = new GptPartitionDescriptor[2];
        byte[] packageBuffer = new byte[1024];

        VirtioBlockPackageReadPreflightResult result = VirtioBlockPackageReadPreflight.LoadPackage(
            session,
            MemoryBlockDevice.FromImage(disk, SectorSize),
            headerScratch,
            partitionEntryScratch,
            partitions,
            packageBuffer);

        return HostCheck.Result(session.Succeeded
            && result.Succeeded
            && result.Status == VirtioBlockPackageReadPreflightStatus.Success
            && result.ReadSessionComplete
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.Success
            && result.ReadSessionPipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && result.ReadSessionByteCount == 1024
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorBefore.UsedRingIndex == 8
            && result.CursorAfter.AvailableRingIndex == 5
            && result.CursorAfter.UsedRingIndex == 9
            && result.PackageLoadAttempted
            && result.PackageLoad.Succeeded
            && result.PackageLoad.Status == OsPackageLoadStatus.Success
            && result.PackageLoad.GptStatus == GptStatus.Success
            && result.PackageLoad.Fat32Status == Fat32Status.Success
            && result.PackageLoad.PackageStatus == OsPackageStatus.Success
            && result.PackageLoad.BytesRead == package.Length);
    }

    [HostTest("storage.virtio package preflight rejects incomplete session", HostLane.Fast, 1)]
    private static bool TestRejectsIncompleteSession()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: false);
        byte[] disk = CreateDiskImage(ValidPackage(), hasEsp: true, hasPackage: true);
        byte[] headerScratch = new byte[SectorSize];
        byte[] partitionEntryScratch = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] partitions = new GptPartitionDescriptor[2];
        byte[] packageBuffer = new byte[1024];

        VirtioBlockPackageReadPreflightResult result = VirtioBlockPackageReadPreflight.LoadPackage(
            session,
            MemoryBlockDevice.FromImage(disk, SectorSize),
            headerScratch,
            partitionEntryScratch,
            partitions,
            packageBuffer);

        return HostCheck.Result(!session.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockPackageReadPreflightStatus.IncompleteReadSession
            && !result.ReadSessionComplete
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.AdapterFailed
            && !result.PackageLoadAttempted
            && result.PackageLoad.Status == OsPackageLoadStatus.Success
            && result.PackageLoad.BytesRead == 0
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorBefore.UsedRingIndex == 8
            && result.CursorAfter.AvailableRingIndex == 4
            && result.CursorAfter.UsedRingIndex == 8);
    }

    [HostTest("storage.virtio package preflight reports scratch capacity", HostLane.Fast, 2)]
    private static bool TestReportsScratchCapacity()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        byte[] disk = CreateDiskImage(ValidPackage(), hasEsp: true, hasPackage: true);
        byte[] headerScratch = new byte[SectorSize - 1];
        byte[] partitionEntryScratch = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] partitions = new GptPartitionDescriptor[2];
        byte[] packageBuffer = new byte[1024];

        VirtioBlockPackageReadPreflightResult result = VirtioBlockPackageReadPreflight.LoadPackage(
            session,
            MemoryBlockDevice.FromImage(disk, SectorSize),
            headerScratch,
            partitionEntryScratch,
            partitions,
            packageBuffer);

        return HostCheck.Result(session.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockPackageReadPreflightStatus.HeaderScratchTooSmall
            && result.ReadSessionComplete
            && !result.PackageLoadAttempted
            && result.HeaderScratchBytesRequired == SectorSize
            && result.HeaderScratchBytesAvailable == checked((ulong)headerScratch.Length)
            && result.PartitionEntryScratchBytesAvailable == checked((ulong)partitionEntryScratch.Length)
            && result.PartitionDescriptorCapacity == partitions.Length
            && result.PackageBufferBytesRequired == VirtioBlockPackageReadPreflight.MinimumPackageBufferBytes
            && result.PackageBufferBytesAvailable == checked((ulong)packageBuffer.Length)
            && result.CursorAfter.AvailableRingIndex == 5
            && result.CursorAfter.UsedRingIndex == 9);
    }

    [HostTest("storage.virtio package preflight preserves storage boundary", HostLane.Fast, 3)]
    private static bool TestPreservesStorageBoundary()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        byte[] package = ValidPackage();
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        byte[] headerScratch = new byte[SectorSize];
        byte[] partitionEntryScratch = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] partitions = new GptPartitionDescriptor[2];
        byte[] packageBuffer = new byte[1024];

        VirtioBlockPackageReadPreflightResult result = VirtioBlockPackageReadPreflight.LoadPackage(
            session,
            MemoryBlockDevice.FromImage(disk, SectorSize),
            headerScratch,
            partitionEntryScratch,
            partitions,
            packageBuffer);

        return HostCheck.Result(result.Succeeded
            && result.ReadSessionComplete
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorAfter.AvailableRingIndex == 5
            && VirtioBlockPackageReadPreflight.ValidatesCallerOwnedReadSession
            && VirtioBlockPackageReadPreflight.PreservesReadSessionProvenance
            && VirtioBlockPackageReadPreflight.RejectsIncompleteReadSessionBeforePackageParse
            && VirtioBlockPackageReadPreflight.ValidatesGptHeaderScratch
            && VirtioBlockPackageReadPreflight.ValidatesPartitionEntryScratch
            && VirtioBlockPackageReadPreflight.ValidatesPackageBuffer
            && VirtioBlockPackageReadPreflight.ReportsScratchCapacity
            && VirtioBlockPackageReadPreflight.DelegatesPackageLoaderContracts
            && VirtioBlockPackageReadPreflight.RequiresCallerOwnedBlockDevice
            && !VirtioBlockPackageReadPreflight.NotifiesLiveDevice
            && !VirtioBlockPackageReadPreflight.WaitsForHardware
            && !VirtioBlockPackageReadPreflight.PerformsDmaMapping
            && !VirtioBlockPackageReadPreflight.AllocatesDmaMemory
            && !VirtioBlockPackageReadPreflight.MapsBars
            && !VirtioBlockPackageReadPreflight.PerformsMmio
            && !VirtioBlockPackageReadPreflight.AccessesPhysicalAddresses
            && !VirtioBlockPackageReadPreflight.ExecutesMemoryBarriers
            && !VirtioBlockPackageReadPreflight.ConfiguresLiveQueues
            && !VirtioBlockPackageReadPreflight.PollsDevice
            && !VirtioBlockPackageReadPreflight.HandlesInterrupts
            && !VirtioBlockPackageReadPreflight.ReadsRealDisk
            && !VirtioBlockPackageReadPreflight.WaitsForUsedRingCompletion);
    }

    [HostTest("storage.virtio package preflight default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioBlockPackageReadPreflightResult result = default;
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: false);
        byte[] disk = CreateDiskImage(ValidPackage(), hasEsp: true, hasPackage: true);
        byte[] headerScratch = new byte[SectorSize];
        byte[] partitionEntryScratch = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] partitions = new GptPartitionDescriptor[2];
        byte[] packageBuffer = new byte[1024];

        VirtioBlockPackageReadPreflightResult failed = VirtioBlockPackageReadPreflight.LoadPackage(
            session,
            MemoryBlockDevice.FromImage(disk, SectorSize),
            headerScratch,
            partitionEntryScratch,
            partitions,
            packageBuffer);

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockPackageReadPreflightStatus.Success
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.Success
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockPackageReadPreflightStatus.IncompleteReadSession
            && failed.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.AdapterFailed
            && !failed.PackageLoadAttempted
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockPackageReadPreflightResult>());
    }

    [HostTest("storage.virtioblk package preflight stages package file", HostLane.Fast, 5)]
    private static bool TestStagesPackageFileFromReadSessionPreflight()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        byte[] package = ValidPackage();
        byte[] disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        byte[] headerScratch = new byte[SectorSize];
        byte[] partitionEntryScratch = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] partitions = new GptPartitionDescriptor[2];
        byte[] packageBuffer = new byte[1024];
        byte[] staging = new byte[4];

        VirtioBlockPackageFileStagingPreflightResult result =
            VirtioBlockPackageFileStagingPreflight.StagePackageFile(
                session,
                MemoryBlockDevice.FromImage(disk, SectorSize),
                headerScratch,
                partitionEntryScratch,
                partitions,
                packageBuffer,
                "bin/app.dll"u8,
                staging);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockPackageFileStagingPreflightStatus.Success
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.Success
            && result.ReadSessionPipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && result.PackagePreflightStatus == VirtioBlockPackageReadPreflightStatus.Success
            && result.PackageOpenStatus == OsPackageFileSystemStatus.Success
            && result.PackageStagingStatus == OsPackageFileStagingStatus.Success
            && result.ReadSessionByteCount == 1024
            && result.PackageBytesRead == package.Length
            && result.PackageFileBytesStaged == 4
            && result.PackagePreflight.PackageLoadAttempted
            && result.PackagePreflight.PackageLoad.Succeeded
            && result.PackageOpen.FileLength == 4
            && result.PackageStaging.CopyAttempted
            && result.StagingDestinationBytesAvailable == 4
            && staging[0] == 0xCA
            && staging[1] == 0xFE
            && staging[2] == 0xBA
            && staging[3] == 0xBE
            && VirtioBlockPackageFileStagingPreflight.ValidatesCallerOwnedReadSession
            && VirtioBlockPackageFileStagingPreflight.DelegatesPackageReadPreflight
            && VirtioBlockPackageFileStagingPreflight.OpensPackageFileBeforeStaging
            && VirtioBlockPackageFileStagingPreflight.DelegatesPackageFileStagingPlanner
            && VirtioBlockPackageFileStagingPreflight.WritesCallerOwnedDestinationOnly
            && !VirtioBlockPackageFileStagingPreflight.NotifiesLiveDevice
            && !VirtioBlockPackageFileStagingPreflight.WaitsForHardware
            && !VirtioBlockPackageFileStagingPreflight.PerformsDmaMapping
            && !VirtioBlockPackageFileStagingPreflight.AllocatesDmaMemory
            && !VirtioBlockPackageFileStagingPreflight.MapsBars
            && !VirtioBlockPackageFileStagingPreflight.PerformsMmio
            && !VirtioBlockPackageFileStagingPreflight.ReadsRealDisk
            && !VirtioBlockPackageFileStagingPreflight.AllocatesStagingMemory
            && !VirtioBlockPackageFileStagingPreflight.MapsExecutablePages
            && !VirtioBlockPackageFileStagingPreflight.ExecutesAppCode
            && !VirtioBlockPackageFileStagingPreflight.StartsHostedDotNet
            && !VirtioBlockPackageFileStagingPreflight.StartsKestrel
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockPackageFileStagingPreflightResult>());
    }

    [HostTest("storage.virtioblk package file staging formatter formats success marker", HostLane.Fast, 6)]
    private static bool TestPackageFileStagingFormatterFormatsSuccessMarker()
    {
        VirtioBlockPackageFileStagingPreflightResult preflight =
            CreateSuccessfulPackageFileStagingPreflight(
                out byte[] package,
                out _,
                out _,
                out _,
                out _);
        VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult query =
            VirtioBlockPackageFileStagingPreflightDiagnosticFormatter.QueryRequiredBytes(
                preflight);
        byte[] destination = new byte[query.RequiredBytes];

        VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult result =
            VirtioBlockPackageFileStagingPreflightDiagnosticFormatter.FormatResult(
                preflight,
                destination);
        string marker = System.Text.Encoding.ASCII.GetString(destination, 0, result.BytesWritten);
        string expected =
            "APP_PACKAGE_FILE_STAGING_PREFLIGHT status=Success readSession=Success " +
            "pipeline=Success packagePreflight=Success packageOpenInitialized=true " +
            "packageOpen=Success packageStagingInitialized=true packageFileStaging=Success " +
            "readSessionBytes=1024 packageBytes=" + package.Length.ToString() + " stagedBytes=4 stagingCapacity=4 " +
            "sourceFileBytes=4 requiredBytes=4 sourceRangeOverflow=false";

        return HostCheck.Result(preflight.Succeeded
            && query.Succeeded
            && result.Succeeded
            && result.Status == VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.Success
            && result.PreflightStatus == VirtioBlockPackageFileStagingPreflightStatus.Success
            && result.BytesWritten == expected.Length
            && result.RequiredBytes == expected.Length
            && result.RequiredBytes == query.RequiredBytes
            && result.AvailableBytes == destination.Length
            && marker == expected
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult>());
    }

    [HostTest("storage.virtioblk package file staging formatter reports short destination", HostLane.Fast, 7)]
    private static bool TestPackageFileStagingFormatterReportsShortDestination()
    {
        VirtioBlockPackageFileStagingPreflightResult preflight =
            CreateSuccessfulPackageFileStagingPreflight(out _, out _, out _, out _, out _);
        VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult query =
            VirtioBlockPackageFileStagingPreflightDiagnosticFormatter.QueryRequiredBytes(
                preflight);
        byte[] destination = new byte[query.RequiredBytes - 1];
        destination.AsSpan().Fill(0xCC);

        VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult result =
            VirtioBlockPackageFileStagingPreflightDiagnosticFormatter.FormatResult(
                preflight,
                destination);

        return HostCheck.Result(query.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.DestinationTooSmall
            && result.PreflightStatus == VirtioBlockPackageFileStagingPreflightStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == query.RequiredBytes
            && result.AvailableBytes == destination.Length
            && AllBytesEqual(destination, 0xCC));
    }

    [HostTest("storage.virtioblk package file staging formatter default result is not success", HostLane.Fast, 8)]
    private static bool TestPackageFileStagingFormatterDefaultResultIsNotSuccess()
    {
        VirtioBlockPackageFileStagingPreflightResult preflight = default;
        Span<byte> destination = stackalloc byte[256];

        VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult query =
            VirtioBlockPackageFileStagingPreflightDiagnosticFormatter.QueryRequiredBytes(
                preflight);
        VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult result =
            VirtioBlockPackageFileStagingPreflightDiagnosticFormatter.FormatResult(
                preflight,
                destination);
        VirtioBlockPackageFileStagingPreflightDiagnosticFormatResult defaultResult = default;

        return HostCheck.Result(!preflight.Succeeded
            && !query.Succeeded
            && query.Status ==
                VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && !result.Succeeded
            && result.Status ==
                VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus.InvalidResult
            && result.BytesWritten == 0
            && !defaultResult.IsInitialized
            && !defaultResult.Succeeded);
    }

    [HostTest("storage virtio block package file staging formatter uses shared ascii text builder", HostLane.Fast, 9)]
    private static bool TestPackageFileStagingFormatterUsesSharedAsciiTextBuilder()
    {
        return HostCheck.Result(StoragePreflightFormatterSourceGuard.Check(
            HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"),
            typeof(VirtioBlockPackageFileStagingPreflightDiagnosticFormatter).FullName!,
            [nameof(VirtioBlockPackageFileStagingPreflightResult.ReadSessionByteCount),
                nameof(VirtioBlockPackageFileStagingPreflightResult.PackageBytesRead),
                nameof(VirtioBlockPackageFileStagingPreflightResult.PackageFileBytesStaged),
                nameof(VirtioBlockPackageFileStagingPreflightResult.StagingDestinationBytesAvailable),
                nameof(VirtioBlockPackageFileStagingPreflightResult.PackageStaging) + "." + nameof(OsPackageFileStagingResult.FileLength),
                nameof(VirtioBlockPackageFileStagingPreflightResult.PackageStaging) + "." + nameof(OsPackageFileStagingResult.RequiredBytes)]));
    }

    [HostTest("boot.sequence package file staging preflight ready", HostLane.Fast, 10)]
    private static bool TestBootSequencePackageFileStagingPreflightReady()
    {
        VirtioBlockPackageFileStagingPreflightResult preflight =
            CreateSuccessfulPackageFileStagingPreflight(out _, out _, out _, out _, out _);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppPackageFileStagingPreflightReadiness(
            preflight,
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppPackageFileStagingPreflightReadyMarker(
            preflight);

        return HostCheck.Result(preflight.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker.StartsWith(
                BootSequence.AppPackageFileStagingPreflightReadyPrefix,
                StringComparison.Ordinal)
            && expectedMarker.Contains("status=Ready", StringComparison.Ordinal)
            && expectedMarker.Contains("preflight=Success", StringComparison.Ordinal)
            && expectedMarker.Contains("packagePreflight=Success", StringComparison.Ordinal)
            && expectedMarker.Contains("open=Success", StringComparison.Ordinal)
            && expectedMarker.Contains("staging=Success", StringComparison.Ordinal)
            && expectedMarker.Contains("stagedBytes=4", StringComparison.Ordinal)
            && expectedMarker.Contains("destinationCapacity=4", StringComparison.Ordinal)
            && expectedMarker.Contains("packageBufferCapacity=1024", StringComparison.Ordinal));
    }

    [HostTest("boot.sequence package file staging preflight blocks default result", HostLane.Fast, 11)]
    private static bool TestBootSequencePackageFileStagingPreflightBlocksDefaultResult()
    {
        VirtioBlockPackageFileStagingPreflightResult preflight = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunAppPackageFileStagingPreflightReadiness(
            preflight,
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppPackageFileStagingPreflightNotReadyMarker(
            AppPackageFileStagingPreflightReadinessStatus.NotInitialized,
            preflight);

        return HostCheck.Result(!preflight.Succeeded
            && !preflight.IsInitialized
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(AppPackageFileStagingPreflightReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker.StartsWith(
                BootSequence.AppPackageFileStagingPreflightNotReadyPrefix,
                StringComparison.Ordinal)
            && expectedMarker.Contains("initialized=false", StringComparison.Ordinal)
            && expectedMarker.Contains("status=NotInitialized", StringComparison.Ordinal));
    }

    [HostTest("appmodel.selection preflight rejects incomplete read session", HostLane.Fast, 12)]
    private static bool TestAppSelectionPreflightRejectsIncompleteReadSession()
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: false);
        byte[] primaryHeader = new byte[SectorSize];
        byte[] partitionEntries = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        GptPartitionDescriptor[] partitions = new GptPartitionDescriptor[2];
        byte[] manifestBuffer = new byte[(int)NativeAppSelectionBootScratchPlan.DefaultManifestBufferBytes];
        byte[] packageBuffer = new byte[(int)NativeAppSelectionBootScratchPlan.DefaultPackageBufferBytes];

        VirtioBlockNativeAppSelectionPreflightResult result =
            VirtioBlockNativeAppSelectionPreflight.LoadSelection(
                session,
                disk: null,
                primaryHeader,
                partitionEntries,
                partitions,
                manifestBuffer,
                packageBuffer,
                BootInfo.CreateHost(BootCapabilities.TextOutput),
                NativeAppLaunchRuntimeCapabilities.PackageStorage);

        return HostCheck.Result(!session.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockNativeAppSelectionPreflightStatus.IncompleteReadSession
            && !result.ReadSessionComplete
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.AdapterFailed
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && !result.ScratchPlanAttempted
            && !result.SelectionLoadAttempted);
    }

    private static VirtioBlockPackageFileStagingPreflightResult
        CreateSuccessfulPackageFileStagingPreflight(
            out byte[] package,
            out byte[] disk,
            out byte[] packageBuffer,
            out byte[] staging,
            out GptPartitionDescriptor[] partitions)
    {
        VirtioBlockReadOnlyReadSessionResult session = ExecuteReadSession(validRequestHeader: true);
        package = ValidPackage();
        disk = CreateDiskImage(package, hasEsp: true, hasPackage: true);
        byte[] headerScratch = new byte[SectorSize];
        byte[] partitionEntryScratch = new byte[checked((int)(PartitionEntryCount * PartitionEntrySize))];
        partitions = new GptPartitionDescriptor[2];
        packageBuffer = new byte[1024];
        staging = new byte[4];

        return VirtioBlockPackageFileStagingPreflight.StagePackageFile(
            session,
            MemoryBlockDevice.FromImage(disk, SectorSize),
            headerScratch,
            partitionEntryScratch,
            partitions,
            packageBuffer,
            "bin/app.dll"u8,
            staging);
    }

    private static bool AllBytesEqual(byte[] bytes, byte expected)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != expected)
            {
                return false;
            }
        }

        return true;
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
                notifyBytes);

        return session.Read(
            firstSector: 4,
            sectorCount: 2,
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

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private static byte[] CreateDiskImage(byte[] package, bool hasEsp, bool hasPackage)
    {
        byte[] disk = new byte[checked((int)(DiskBlockCount * SectorSize))];
        WriteGpt(disk, hasEsp);
        WriteFat32Volume(disk, checked((int)(PartitionStartLba * SectorSize)), package, hasPackage);
        return disk;
    }

    private static void WriteGpt(byte[] disk, bool hasEsp)
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

        if (hasEsp)
        {
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
        }

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
        byte[] package,
        bool hasPackage)
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
        WriteDirectoryEntry(
            disk,
            rootDirectory,
            "OS         ",
            Fat32Parser.DirectoryAttribute,
            OsDirectoryCluster,
            0);
        disk[rootDirectory + 32] = 0x00;

        int osDirectory = partitionOffset + ClusterOffset(OsDirectoryCluster);
        WriteDirectoryEntry(
            disk,
            osDirectory,
            ".          ",
            Fat32Parser.DirectoryAttribute,
            OsDirectoryCluster,
            0);
        WriteDirectoryEntry(
            disk,
            osDirectory + 32,
            "..         ",
            Fat32Parser.DirectoryAttribute,
            RootCluster,
            0);

        if (hasPackage)
        {
            WriteDirectoryEntry(
                disk,
                osDirectory + 64,
                "APP     PKG",
                attributes: 0x20,
                firstCluster: PackageCluster,
                fileSize: (uint)package.Length);
            disk[osDirectory + 96] = 0x00;

            int packageOffset = partitionOffset + ClusterOffset(PackageCluster);
            package.CopyTo(disk.AsSpan(packageOffset));
        }
        else
        {
            disk[osDirectory + 64] = 0x00;
        }
    }

    private static int ClusterOffset(uint cluster)
    {
        ulong lba = RootDirectoryLba + (cluster - RootCluster);
        return checked((int)(lba * SectorSize));
    }

    private static byte[] ValidPackage()
    {
        const int tableOffset = OsPackageParser.HeaderSize;
        const int tableLength = OsPackageParser.FileTableEntrySize;
        const int pathOffset = tableOffset + tableLength;
        const string path = "bin/app.dll";
        const int contentOffset = pathOffset + 11;
        const int contentLength = 4;

        byte[] package = new byte[contentOffset + contentLength];
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
        WriteUInt16(package, tableOffset + 4, path.Length);
        WriteUInt16(package, tableOffset + 6, 0);
        WriteUInt64(package, tableOffset + 8, contentOffset);
        WriteUInt64(package, tableOffset + 16, contentLength);
        WriteUInt64(package, tableOffset + 24, 0);
        WriteAscii(package, pathOffset, path);
        package[contentOffset] = 0xCA;
        package[contentOffset + 1] = 0xFE;
        package[contentOffset + 2] = 0xBA;
        package[contentOffset + 3] = 0xBE;
        return package;
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

    private static void WriteAscii(byte[] image, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            image[offset + i] = (byte)value[i];
        }
    }

    private static void WriteUtf16Name(byte[] image, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            WriteUInt16(image, offset + i * 2, (ushort)value[i]);
        }
    }

    private static void WriteGuid(byte[] image, int offset, Guid value)
    {
        if (!value.TryWriteBytes(image.AsSpan(offset, 16)))
        {
            throw new InvalidOperationException("Could not write test GUID.");
        }
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

    private static void WriteUInt16(byte[] image, int offset, int value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), (ushort)value);
    }

    private static void WriteUInt32(byte[] image, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset, 4), value);
    }

    private static void WriteUInt64(byte[] image, int offset, ulong value)
    {
        EndianPrimitives.WriteUInt64LittleEndian(image.AsSpan(offset, 8), value);
    }


}
