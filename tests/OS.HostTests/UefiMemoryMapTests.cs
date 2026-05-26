using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;

internal static unsafe class UefiMemoryMapTests
{
    private static int s_getMemoryMapCallCount;
    private static int s_allocatePoolCallCount;
    private static uint s_requestedPoolType;
    private static ulong s_requestedAllocationSize;
    private static byte* s_firmwareAllocation;
    private static ulong s_initialGetMemoryMapStatus;
    private static ulong s_finalGetMemoryMapStatus;
    private static ulong s_allocatePoolStatus;
    private static ulong s_initialMapSize;
    private static ulong s_capturedMapSize;
    private static ulong s_mapKey;
    private static ulong s_descriptorSize;
    private static uint s_descriptorVersion;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("uefi.memorymap captures descriptor metadata and entries", TestCapturedMemoryMap, ref failures);
        Run("uefi.memorymap reports buffer-too-small status", TestBufferTooSmall, ref failures);
        Run("uefi.memorymap rejects invalid descriptor metadata", TestInvalidDescriptorMetadata, ref failures);
        Run("uefi.memorymap rejects missing descriptor entries", TestMissingDescriptorEntries, ref failures);
        Run("uefi.memorymap preserves firmware error status", TestFirmwareError, ref failures);
        Run("uefi.memorymap.copyplan validates metadata", TestCopyPlanValidMetadata, ref failures);
        Run("uefi.memorymap.copyplan rejects missing source", TestCopyPlanMissingSource, ref failures);
        Run("uefi.memorymap.copyplan rejects invalid sizes", TestCopyPlanInvalidSizes, ref failures);
        Run("uefi.memorymap.copyplan rejects invalid descriptor metadata", TestCopyPlanInvalidDescriptorMetadata, ref failures);
        Run("uefi.memorymap.copyplan rejects descriptor count mismatch", TestCopyPlanDescriptorCountMismatch, ref failures);
        Run("uefi.memorymap.copyplan rejects descriptor count limit", TestCopyPlanDescriptorCountLimit, ref failures);
        Run("uefi.memorymap.copyplan rejects overflow", TestCopyPlanOverflow, ref failures);
        Run("uefi.memorymap.copyplan rejects destination too small", TestCopyPlanDestinationTooSmall, ref failures);
        Run("uefi.memorymap.capture helper copies owned map", TestCaptureHelperCopiesOwnedMap, ref failures);
        Run(
            "uefi.memorymap.capture helper rejects missing firmware pointers",
            TestCaptureHelperRejectsMissingFirmwarePointers,
            ref failures);
        Run(
            "uefi.memorymap.capture helper rejects missing owned destination",
            TestCaptureHelperRejectsMissingOwnedDestination,
            ref failures);
        Run("uefi.memorymap.capture helper reports allocation overflow", TestCaptureHelperReportsAllocationOverflow, ref failures);
        Run("uefi.memorymap.capture helper reports copy-plan status", TestCaptureHelperReportsCopyPlanStatus, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("UEFI memory map host tests failed: " + failures);
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

    private static bool TestCapturedMemoryMap()
    {
        UefiMemoryDescriptor[] descriptors =
        [
            new UefiMemoryDescriptor(
                UefiMemoryType.BootServicesCode,
                0x100000,
                0,
                16,
                0x8),
            new UefiMemoryDescriptor(
                UefiMemoryType.ConventionalMemory,
                0x200000,
                0,
                512,
                0xF)
        ];

        UefiMemoryMapResult result = UefiMemoryMapResult.Captured(
            mapKey: 0x77,
            descriptorSize: 48,
            descriptorVersion: 1,
            descriptorEntries: descriptors);

        bool firstFound = result.Snapshot.TryGetDescriptor(0, out UefiMemoryDescriptor first);
        bool secondFound = result.Snapshot.TryGetDescriptor(1, out UefiMemoryDescriptor second);
        bool outOfRangeFails = !result.Snapshot.TryGetDescriptor(2, out _);
        bool negativeIndexFails = !result.Snapshot.TryGetDescriptor(-1, out _);

        return result.Succeeded
            && result.Status == UefiMemoryMapStatus.Success
            && result.Message.Length == 0
            && result.RequiredBufferSize == 0
            && result.FirmwareStatus == 0
            && result.Snapshot.MapKey == 0x77
            && result.Snapshot.DescriptorSize == 48
            && result.Snapshot.DescriptorVersion == 1
            && result.Snapshot.DescriptorCount == 2
            && result.Snapshot.DescriptorEntries.Length == 2
            && result.Snapshot.HasDescriptors
            && firstFound
            && first.Type == UefiMemoryType.BootServicesCode
            && first.PhysicalStart == 0x100000
            && first.VirtualStart == 0
            && first.NumberOfPages == 16
            && first.Attribute == 0x8
            && secondFound
            && second.Type == UefiMemoryType.ConventionalMemory
            && second.PhysicalStart == 0x200000
            && second.NumberOfPages == 512
            && second.Attribute == 0xF
            && outOfRangeFails
            && negativeIndexFails;
    }

    private static bool TestBufferTooSmall()
    {
        UefiMemoryMapResult result = UefiMemoryMapResult.BufferTooSmall(4096);

        return !result.Succeeded
            && result.Status == UefiMemoryMapStatus.BufferTooSmall
            && result.RequiredBufferSize == 4096
            && result.FirmwareStatus == 0
            && !result.Snapshot.HasDescriptors
            && result.Message.Contains("buffer is too small", StringComparison.Ordinal);
    }

    private static bool TestInvalidDescriptorMetadata()
    {
        UefiMemoryDescriptor[] descriptors =
        [
            new UefiMemoryDescriptor(
                UefiMemoryType.RuntimeServicesData,
                0x300000,
                0,
                4,
                0)
        ];

        UefiMemoryMapResult invalidSize = UefiMemoryMapResult.Captured(1, 0, 1, descriptors);
        UefiMemoryMapResult invalidVersion = UefiMemoryMapResult.Captured(1, 48, 0, descriptors);

        return !invalidSize.Succeeded
            && invalidSize.Status == UefiMemoryMapStatus.InvalidDescriptorSize
            && invalidSize.Snapshot.DescriptorCount == 0
            && invalidSize.Message.Contains("descriptor size", StringComparison.Ordinal)
            && !invalidVersion.Succeeded
            && invalidVersion.Status == UefiMemoryMapStatus.InvalidDescriptorVersion
            && invalidVersion.Snapshot.DescriptorCount == 0
            && invalidVersion.Message.Contains("descriptor version", StringComparison.Ordinal);
    }

    private static bool TestMissingDescriptorEntries()
    {
        UefiMemoryMapResult nullEntries = UefiMemoryMapResult.Captured(1, 48, 1, null);
        UefiMemoryMapResult emptyEntries = UefiMemoryMapResult.Captured(
            1,
            48,
            1,
            Array.Empty<UefiMemoryDescriptor>());

        return !nullEntries.Succeeded
            && nullEntries.Status == UefiMemoryMapStatus.InvalidDescriptorEntries
            && nullEntries.Message.Contains("descriptor entries", StringComparison.Ordinal)
            && !emptyEntries.Succeeded
            && emptyEntries.Status == UefiMemoryMapStatus.InvalidDescriptorEntries
            && emptyEntries.Snapshot.DescriptorCount == 0;
    }

    private static bool TestFirmwareError()
    {
        const ulong efiInvalidParameter = 0x8000000000000002;
        UefiMemoryMapResult result = UefiMemoryMapResult.FirmwareError(efiInvalidParameter);

        return !result.Succeeded
            && result.Status == UefiMemoryMapStatus.FirmwareError
            && result.FirmwareStatus == efiInvalidParameter
            && result.RequiredBufferSize == 0
            && result.Snapshot.DescriptorCount == 0
            && result.Message.Contains("GetMemoryMap failed", StringComparison.Ordinal);
    }

    private static bool TestCopyPlanValidMetadata()
    {
        FirmwareMemoryMapRecord metadata = CreateCopyPlanMetadata();
        UefiMemoryMapCopyPlanResult result = UefiMemoryMapCopyPlanResult.Create(
            metadata,
            destinationCapacity: 4096);

        return result.Succeeded
            && result.Status == UefiMemoryMapCopyPlanStatus.Success
            && result.Plan.SourceAddress == 0x100000
            && result.Plan.SourceBufferSize == 8192
            && result.Plan.SourceMapSize == 48 * 3UL
            && result.Plan.DestinationCapacity == 4096
            && result.Plan.RequiredDestinationBytes == 48 * 3UL
            && result.Plan.MapKey == 0xABC
            && result.Plan.DescriptorSize == 48
            && result.Plan.DescriptorVersion == 1
            && result.Plan.DescriptorCount == 3;
    }

    private static bool TestCopyPlanMissingSource()
    {
        UefiMemoryMapCopyPlanResult missingAddress = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(bufferAddress: 0),
            destinationCapacity: 4096);
        UefiMemoryMapCopyPlanResult missingBuffer = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(bufferSize: 0),
            destinationCapacity: 4096);
        UefiMemoryMapCopyPlanResult missingMap = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(mapSize: 0),
            destinationCapacity: 4096);

        return IsStatus(missingAddress, UefiMemoryMapCopyPlanStatus.MissingSource)
            && IsStatus(missingBuffer, UefiMemoryMapCopyPlanStatus.MissingSource)
            && IsStatus(missingMap, UefiMemoryMapCopyPlanStatus.MissingSource);
    }

    private static bool TestCopyPlanInvalidSizes()
    {
        UefiMemoryMapCopyPlanResult mapExceedsBuffer = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(bufferSize: 128, mapSize: 256),
            destinationCapacity: 4096);

        UefiMemoryMapCopyPlanResult misalignedMap = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(mapSize: 49, descriptorCount: 1),
            destinationCapacity: 4096);

        return IsStatus(mapExceedsBuffer, UefiMemoryMapCopyPlanStatus.InvalidMapSize)
            && IsStatus(misalignedMap, UefiMemoryMapCopyPlanStatus.MapSizeDescriptorMismatch);
    }

    private static bool TestCopyPlanInvalidDescriptorMetadata()
    {
        UefiMemoryMapCopyPlanResult invalidSize = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(mapSize: 39, descriptorSize: 39),
            destinationCapacity: 4096);

        UefiMemoryMapCopyPlanResult invalidVersion = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(descriptorVersion: 0),
            destinationCapacity: 4096);

        return IsStatus(invalidSize, UefiMemoryMapCopyPlanStatus.InvalidDescriptorSize)
            && IsStatus(invalidVersion, UefiMemoryMapCopyPlanStatus.InvalidDescriptorVersion);
    }

    private static bool TestCopyPlanDescriptorCountMismatch()
    {
        UefiMemoryMapCopyPlanResult zeroCount = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(descriptorCount: 0),
            destinationCapacity: 4096);

        UefiMemoryMapCopyPlanResult mismatch = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(mapSize: 96, descriptorCount: 3),
            destinationCapacity: 4096);

        return IsStatus(zeroCount, UefiMemoryMapCopyPlanStatus.InvalidDescriptorCount)
            && IsStatus(mismatch, UefiMemoryMapCopyPlanStatus.InvalidDescriptorCount);
    }

    private static bool TestCopyPlanDescriptorCountLimit()
    {
        UefiMemoryMapCopyPlanResult result = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(mapSize: 48 * 4UL, descriptorCount: 4),
            destinationCapacity: 4096,
            maximumDescriptorCount: 3);

        return IsStatus(result, UefiMemoryMapCopyPlanStatus.DescriptorCountLimitExceeded);
    }

    private static bool TestCopyPlanOverflow()
    {
        UefiMemoryMapCopyPlanResult result = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(
                mapSize: 48,
                descriptorCount: (ulong.MaxValue / 48) + 1),
            destinationCapacity: ulong.MaxValue,
            maximumDescriptorCount: ulong.MaxValue);

        return IsStatus(result, UefiMemoryMapCopyPlanStatus.SizeOverflow);
    }

    private static bool TestCopyPlanDestinationTooSmall()
    {
        UefiMemoryMapCopyPlanResult result = UefiMemoryMapCopyPlanResult.Create(
            CreateCopyPlanMetadata(mapSize: 96, descriptorCount: 2),
            destinationCapacity: 95);

        return IsStatus(result, UefiMemoryMapCopyPlanStatus.DestinationTooSmall);
    }

    private static bool TestCaptureHelperCopiesOwnedMap()
    {
        byte[] firmwareBuffer = new byte[512];
        byte[] ownedBuffer = new byte[512];

        fixed (byte* firmware = firmwareBuffer)
        fixed (byte* owned = ownedBuffer)
        {
            ResetCaptureMock(firmware);

            UefiMemoryMapCaptureResult result = UefiMemoryMapCapture.Capture(
                &MockGetMemoryMap,
                &MockAllocatePool,
                owned,
                (ulong)ownedBuffer.Length);

            return result.Succeeded
                && result.Status == UefiMemoryMapCaptureStatus.Success
                && result.CopyPlanStatus == UefiMemoryMapCopyPlanStatus.Success
                && s_getMemoryMapCallCount == 2
                && s_allocatePoolCallCount == 1
                && s_requestedPoolType == UefiMemoryMapCapture.EfiLoaderData
                && s_requestedAllocationSize == 96 + (48 * UefiMemoryMapCapture.DefaultDescriptorSlack)
                && result.SourceRecord.BufferAddress == (ulong)firmware
                && result.SourceRecord.BufferSize == s_requestedAllocationSize
                && result.SourceRecord.MapSize == 96
                && result.SourceRecord.MapKey == 0xABC
                && result.SourceRecord.DescriptorSize == 48
                && result.SourceRecord.DescriptorVersion == 1
                && result.SourceRecord.DescriptorCount == 2
                && result.OwnedRecord.BufferAddress == (ulong)owned
                && result.OwnedRecord.BufferSize == (ulong)ownedBuffer.Length
                && result.OwnedRecord.SourceBufferAddress == (ulong)firmware
                && result.OwnedRecord.Flags == FirmwareMemoryMapRecord.FlagOwnedCopy
                && result.CopyPlan.RequiredDestinationBytes == 96
                && ownedBuffer[0] == 0xA0
                && ownedBuffer[47] == 0xAF
                && ownedBuffer[95] == 0xAF
                && ownedBuffer[96] == 0;
        }
    }

    private static bool TestCaptureHelperRejectsMissingFirmwarePointers()
    {
        byte[] ownedBuffer = new byte[512];

        fixed (byte* owned = ownedBuffer)
        {
            delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> missingGetMemoryMap = null;
            delegate* unmanaged<uint, ulong, void**, ulong> missingAllocatePool = null;

            UefiMemoryMapCaptureResult missingGetMemoryMapResult = UefiMemoryMapCapture.Capture(
                missingGetMemoryMap,
                &MockAllocatePool,
                owned,
                (ulong)ownedBuffer.Length);

            UefiMemoryMapCaptureResult missingAllocatePoolResult = UefiMemoryMapCapture.Capture(
                &MockGetMemoryMap,
                missingAllocatePool,
                owned,
                (ulong)ownedBuffer.Length);

            return !missingGetMemoryMapResult.Succeeded
                && missingGetMemoryMapResult.Status == UefiMemoryMapCaptureStatus.MissingGetMemoryMap
                && !missingAllocatePoolResult.Succeeded
                && missingAllocatePoolResult.Status == UefiMemoryMapCaptureStatus.MissingAllocatePool;
        }
    }

    private static bool TestCaptureHelperRejectsMissingOwnedDestination()
    {
        UefiMemoryMapCaptureResult missingPointer = UefiMemoryMapCapture.Capture(
            &MockGetMemoryMap,
            &MockAllocatePool,
            null,
            512);

        byte[] ownedBuffer = new byte[1];
        fixed (byte* owned = ownedBuffer)
        {
            UefiMemoryMapCaptureResult missingSize = UefiMemoryMapCapture.Capture(
                &MockGetMemoryMap,
                &MockAllocatePool,
                owned,
                0);

            return !missingPointer.Succeeded
                && missingPointer.Status == UefiMemoryMapCaptureStatus.MissingOwnedDestination
                && missingPointer.CopyPlanStatus == UefiMemoryMapCopyPlanStatus.DestinationTooSmall
                && !missingSize.Succeeded
                && missingSize.Status == UefiMemoryMapCaptureStatus.MissingOwnedDestination
                && missingSize.CopyPlanStatus == UefiMemoryMapCopyPlanStatus.DestinationTooSmall;
        }
    }

    private static bool TestCaptureHelperReportsAllocationOverflow()
    {
        byte[] ownedBuffer = new byte[512];

        fixed (byte* owned = ownedBuffer)
        {
            ResetCaptureMock(
                firmwareAllocation: null,
                initialMapSize: UefiMemoryMapCapture.MaximumUnsignedLong - 4,
                capturedMapSize: 96,
                descriptorSize: 48);

            UefiMemoryMapCaptureResult result = UefiMemoryMapCapture.Capture(
                &MockGetMemoryMap,
                &MockAllocatePool,
                owned,
                (ulong)ownedBuffer.Length);

            return !result.Succeeded
                && result.Status == UefiMemoryMapCaptureStatus.AllocationSizeOverflow
                && result.CopyPlanStatus == UefiMemoryMapCopyPlanStatus.SizeOverflow
                && result.RequiredBufferSize == UefiMemoryMapCapture.MaximumUnsignedLong - 4
                && s_getMemoryMapCallCount == 1
                && s_allocatePoolCallCount == 0;
        }
    }

    private static bool TestCaptureHelperReportsCopyPlanStatus()
    {
        byte[] firmwareBuffer = new byte[512];
        byte[] ownedBuffer = new byte[512];

        fixed (byte* firmware = firmwareBuffer)
        fixed (byte* owned = ownedBuffer)
        {
            ResetCaptureMock(
                firmware,
                initialMapSize: 96,
                capturedMapSize: 95,
                descriptorSize: 48);

            UefiMemoryMapCaptureResult result = UefiMemoryMapCapture.Capture(
                &MockGetMemoryMap,
                &MockAllocatePool,
                owned,
                (ulong)ownedBuffer.Length);

            return !result.Succeeded
                && result.Status == UefiMemoryMapCaptureStatus.CopyPlanRejected
                && result.CopyPlanStatus == UefiMemoryMapCopyPlanStatus.MapSizeDescriptorMismatch
                && result.SourceRecord.BufferAddress == (ulong)firmware
                && result.SourceRecord.MapSize == 95
                && result.SourceRecord.DescriptorSize == 48
                && s_getMemoryMapCallCount == 2
                && s_allocatePoolCallCount == 1
                && ownedBuffer[0] == 0;
        }
    }

    private static bool IsStatus(
        UefiMemoryMapCopyPlanResult result,
        UefiMemoryMapCopyPlanStatus expected)
    {
        return !result.Succeeded
            && result.Status == expected
            && result.Plan.RequiredDestinationBytes == 0;
    }

    private static FirmwareMemoryMapRecord CreateCopyPlanMetadata(
        ulong bufferAddress = 0x100000,
        ulong bufferSize = 8192,
        ulong mapSize = 48 * 3UL,
        ulong mapKey = 0xABC,
        ulong descriptorSize = 48,
        uint descriptorVersion = 1,
        ulong descriptorCount = 3)
    {
        FirmwareMemoryMapRecord record;
        record.BufferAddress = bufferAddress;
        record.BufferSize = bufferSize;
        record.SourceBufferAddress = bufferAddress;
        record.SourceBufferSize = bufferSize;
        record.MapSize = mapSize;
        record.MapKey = mapKey;
        record.DescriptorSize = descriptorSize;
        record.DescriptorVersion = descriptorVersion;
        record.DescriptorCount = descriptorCount;
        record.Flags = 0;
        return record;
    }

    private static void ResetCaptureMock(
        byte* firmwareAllocation,
        ulong initialMapSize = 96,
        ulong capturedMapSize = 96,
        ulong mapKey = 0xABC,
        ulong descriptorSize = 48,
        uint descriptorVersion = 1,
        ulong initialGetMemoryMapStatus = UefiMemoryMapCapture.EfiBufferTooSmall,
        ulong finalGetMemoryMapStatus = UefiMemoryMapCapture.EfiSuccess,
        ulong allocatePoolStatus = UefiMemoryMapCapture.EfiSuccess)
    {
        s_getMemoryMapCallCount = 0;
        s_allocatePoolCallCount = 0;
        s_requestedPoolType = 0;
        s_requestedAllocationSize = 0;
        s_firmwareAllocation = firmwareAllocation;
        s_initialMapSize = initialMapSize;
        s_capturedMapSize = capturedMapSize;
        s_mapKey = mapKey;
        s_descriptorSize = descriptorSize;
        s_descriptorVersion = descriptorVersion;
        s_initialGetMemoryMapStatus = initialGetMemoryMapStatus;
        s_finalGetMemoryMapStatus = finalGetMemoryMapStatus;
        s_allocatePoolStatus = allocatePoolStatus;
    }

    [UnmanagedCallersOnly]
    private static ulong MockGetMemoryMap(
        ulong* memoryMapSize,
        void* memoryMap,
        ulong* mapKey,
        ulong* descriptorSize,
        uint* descriptorVersion)
    {
        s_getMemoryMapCallCount++;
        *mapKey = s_mapKey;
        *descriptorSize = s_descriptorSize;
        *descriptorVersion = s_descriptorVersion;

        if (memoryMap == null)
        {
            *memoryMapSize = s_initialMapSize;
            return s_initialGetMemoryMapStatus;
        }

        byte* destination = (byte*)memoryMap;
        for (ulong index = 0; index < s_capturedMapSize; index++)
        {
            destination[index] = (byte)(0xA0 + (index & 0x0F));
        }

        *memoryMapSize = s_capturedMapSize;
        return s_finalGetMemoryMapStatus;
    }

    [UnmanagedCallersOnly]
    private static ulong MockAllocatePool(uint poolType, ulong size, void** buffer)
    {
        s_allocatePoolCallCount++;
        s_requestedPoolType = poolType;
        s_requestedAllocationSize = size;

        if (s_allocatePoolStatus != UefiMemoryMapCapture.EfiSuccess)
        {
            *buffer = null;
            return s_allocatePoolStatus;
        }

        *buffer = s_firmwareAllocation;
        return UefiMemoryMapCapture.EfiSuccess;
    }
}
