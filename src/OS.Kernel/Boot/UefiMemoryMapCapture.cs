namespace OS.Boot;

public enum UefiMemoryMapCaptureStatus
{
    Success = 0,
    MissingGetMemoryMap = 1,
    MissingAllocatePool = 2,
    MissingOwnedDestination = 3,
    InitialGetMemoryMapFailed = 4,
    InvalidInitialMetadata = 5,
    AllocationSizeOverflow = 6,
    AllocatePoolFailed = 7,
    FinalGetMemoryMapFailed = 8,
    InvalidCapturedMetadata = 9,
    CopyPlanRejected = 10
}

public readonly struct UefiMemoryMapCaptureResult
{
    private UefiMemoryMapCaptureResult(
        bool succeeded,
        UefiMemoryMapCaptureStatus status,
        UefiMemoryMapCopyPlanStatus copyPlanStatus,
        FirmwareMemoryMapRecord sourceRecord,
        FirmwareMemoryMapRecord ownedRecord,
        UefiMemoryMapCopyPlan copyPlan,
        ulong firmwareStatus,
        ulong requiredBufferSize,
        ulong allocationSize)
    {
        Succeeded = succeeded;
        Status = status;
        CopyPlanStatus = copyPlanStatus;
        SourceRecord = sourceRecord;
        OwnedRecord = ownedRecord;
        CopyPlan = copyPlan;
        FirmwareStatus = firmwareStatus;
        RequiredBufferSize = requiredBufferSize;
        AllocationSize = allocationSize;
    }

    public bool Succeeded { get; }

    public UefiMemoryMapCaptureStatus Status { get; }

    public UefiMemoryMapCopyPlanStatus CopyPlanStatus { get; }

    public FirmwareMemoryMapRecord SourceRecord { get; }

    public FirmwareMemoryMapRecord OwnedRecord { get; }

    public UefiMemoryMapCopyPlan CopyPlan { get; }

    public ulong FirmwareStatus { get; }

    public ulong RequiredBufferSize { get; }

    public ulong AllocationSize { get; }

    public static UefiMemoryMapCaptureResult Success(
        FirmwareMemoryMapRecord sourceRecord,
        FirmwareMemoryMapRecord ownedRecord,
        UefiMemoryMapCopyPlan copyPlan,
        ulong allocationSize)
    {
        return new UefiMemoryMapCaptureResult(
            true,
            UefiMemoryMapCaptureStatus.Success,
            UefiMemoryMapCopyPlanStatus.Success,
            sourceRecord,
            ownedRecord,
            copyPlan,
            0,
            0,
            allocationSize);
    }

    public static UefiMemoryMapCaptureResult Fail(
        UefiMemoryMapCaptureStatus status,
        UefiMemoryMapCopyPlanStatus copyPlanStatus = UefiMemoryMapCopyPlanStatus.Success,
        FirmwareMemoryMapRecord sourceRecord = default,
        ulong firmwareStatus = 0,
        ulong requiredBufferSize = 0,
        ulong allocationSize = 0)
    {
        return new UefiMemoryMapCaptureResult(
            false,
            status,
            copyPlanStatus,
            sourceRecord,
            default,
            default,
            firmwareStatus,
            requiredBufferSize,
            allocationSize);
    }
}

public static unsafe class UefiMemoryMapCapture
{
    public const uint EfiLoaderData = 2;
    public const ulong EfiSuccess = 0;
    public const ulong EfiBufferTooSmall = 0x8000000000000005;
    public const ulong DefaultDescriptorSlack = 8;
    public const ulong MaximumUnsignedLong = 0xffffffffffffffff;

    public static UefiMemoryMapCaptureResult Capture(
        delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap,
        delegate* unmanaged<uint, ulong, void**, ulong> allocatePool,
        byte* ownedDestination,
        ulong ownedDestinationSize)
    {
        return Capture(
            getMemoryMap,
            allocatePool,
            ownedDestination,
            ownedDestinationSize,
            DefaultDescriptorSlack);
    }

    public static UefiMemoryMapCaptureResult Capture(
        delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap,
        delegate* unmanaged<uint, ulong, void**, ulong> allocatePool,
        byte* ownedDestination,
        ulong ownedDestinationSize,
        ulong descriptorSlack)
    {
        if (getMemoryMap == null)
        {
            return UefiMemoryMapCaptureResult.Fail(UefiMemoryMapCaptureStatus.MissingGetMemoryMap);
        }

        if (allocatePool == null)
        {
            return UefiMemoryMapCaptureResult.Fail(UefiMemoryMapCaptureStatus.MissingAllocatePool);
        }

        if (ownedDestination == null || ownedDestinationSize == 0)
        {
            return UefiMemoryMapCaptureResult.Fail(
                UefiMemoryMapCaptureStatus.MissingOwnedDestination,
                UefiMemoryMapCopyPlanStatus.DestinationTooSmall);
        }

        ulong memoryMapSize = 0;
        ulong mapKey = 0;
        ulong descriptorSize = 0;
        uint descriptorVersion = 0;

        ulong status = getMemoryMap(
            &memoryMapSize,
            null,
            &mapKey,
            &descriptorSize,
            &descriptorVersion);

        if (status != EfiBufferTooSmall)
        {
            return UefiMemoryMapCaptureResult.Fail(
                UefiMemoryMapCaptureStatus.InitialGetMemoryMapFailed,
                firmwareStatus: status,
                requiredBufferSize: memoryMapSize);
        }

        if (memoryMapSize == 0 || descriptorSize == 0 || descriptorSlack == 0)
        {
            return UefiMemoryMapCaptureResult.Fail(
                UefiMemoryMapCaptureStatus.InvalidInitialMetadata,
                requiredBufferSize: memoryMapSize);
        }

        if (descriptorSize > (MaximumUnsignedLong - memoryMapSize) / descriptorSlack)
        {
            return UefiMemoryMapCaptureResult.Fail(
                UefiMemoryMapCaptureStatus.AllocationSizeOverflow,
                UefiMemoryMapCopyPlanStatus.SizeOverflow,
                requiredBufferSize: memoryMapSize);
        }

        ulong allocationSize = memoryMapSize + (descriptorSize * descriptorSlack);
        void* memoryMap = null;
        status = allocatePool(EfiLoaderData, allocationSize, &memoryMap);
        if (status != EfiSuccess || memoryMap == null)
        {
            return UefiMemoryMapCaptureResult.Fail(
                UefiMemoryMapCaptureStatus.AllocatePoolFailed,
                firmwareStatus: status,
                requiredBufferSize: memoryMapSize,
                allocationSize: allocationSize);
        }

        ulong capturedSize = allocationSize;
        status = getMemoryMap(
            &capturedSize,
            memoryMap,
            &mapKey,
            &descriptorSize,
            &descriptorVersion);

        if (status != EfiSuccess)
        {
            return UefiMemoryMapCaptureResult.Fail(
                UefiMemoryMapCaptureStatus.FinalGetMemoryMapFailed,
                firmwareStatus: status,
                requiredBufferSize: capturedSize,
                allocationSize: allocationSize);
        }

        if (capturedSize == 0 || descriptorSize == 0 || descriptorVersion == 0)
        {
            return UefiMemoryMapCaptureResult.Fail(
                UefiMemoryMapCaptureStatus.InvalidCapturedMetadata,
                requiredBufferSize: capturedSize,
                allocationSize: allocationSize);
        }

        FirmwareMemoryMapRecord sourceRecord;
        sourceRecord.BufferAddress = (ulong)memoryMap;
        sourceRecord.BufferSize = allocationSize;
        sourceRecord.SourceBufferAddress = (ulong)memoryMap;
        sourceRecord.SourceBufferSize = allocationSize;
        sourceRecord.MapSize = capturedSize;
        sourceRecord.MapKey = mapKey;
        sourceRecord.DescriptorSize = descriptorSize;
        sourceRecord.DescriptorVersion = descriptorVersion;
        sourceRecord.DescriptorCount = capturedSize / descriptorSize;
        sourceRecord.Flags = 0;

        UefiMemoryMapCopyPlanResult copyPlan = UefiMemoryMapCopyPlanResult.Create(
            sourceRecord,
            ownedDestinationSize);
        if (!copyPlan.Succeeded)
        {
            return UefiMemoryMapCaptureResult.Fail(
                UefiMemoryMapCaptureStatus.CopyPlanRejected,
                copyPlan.Status,
                sourceRecord,
                allocationSize: allocationSize);
        }

        CopyBytes(ownedDestination, memoryMap, copyPlan.Plan.RequiredDestinationBytes);

        FirmwareMemoryMapRecord ownedRecord;
        ownedRecord.BufferAddress = (ulong)ownedDestination;
        ownedRecord.BufferSize = ownedDestinationSize;
        ownedRecord.SourceBufferAddress = sourceRecord.BufferAddress;
        ownedRecord.SourceBufferSize = sourceRecord.BufferSize;
        ownedRecord.MapSize = sourceRecord.MapSize;
        ownedRecord.MapKey = sourceRecord.MapKey;
        ownedRecord.DescriptorSize = sourceRecord.DescriptorSize;
        ownedRecord.DescriptorVersion = sourceRecord.DescriptorVersion;
        ownedRecord.DescriptorCount = sourceRecord.DescriptorCount;
        ownedRecord.Flags = FirmwareMemoryMapRecord.FlagOwnedCopy;

        return UefiMemoryMapCaptureResult.Success(
            sourceRecord,
            ownedRecord,
            copyPlan.Plan,
            allocationSize);
    }

    private static void CopyBytes(byte* destination, void* source, ulong byteCount)
    {
        byte* sourceBytes = (byte*)source;
        for (ulong index = 0; index < byteCount; index++)
        {
            destination[index] = sourceBytes[index];
        }
    }
}
