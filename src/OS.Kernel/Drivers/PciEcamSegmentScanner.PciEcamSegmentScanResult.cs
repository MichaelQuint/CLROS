namespace OS.Drivers;

public readonly struct PciEcamSegmentScanResult
{
    private PciEcamSegmentScanResult(PciEcamSegmentScanStatus status, int completedSegmentCount, int deviceCount, int functionReadCount, int requiredDeviceCapacity, int requiredScratchCapacity, int failingSegmentIndex, PciEcamSegment failingSegment, PciBusScanResult nestedResult, bool isInitialized)
    {
        Status = status;
        CompletedSegmentCount = completedSegmentCount;
        DeviceCount = deviceCount;
        FunctionReadCount = functionReadCount;
        RequiredDeviceCapacity = requiredDeviceCapacity;
        RequiredScratchCapacity = requiredScratchCapacity;
        FailingSegmentIndex = failingSegmentIndex;
        FailingSegment = failingSegment;
        NestedResult = nestedResult;
        IsInitialized = isInitialized;
    }

    public const int NoFailingSegmentIndex = -1;
    public PciEcamSegmentScanStatus Status
    {
        get;
    }
    public int CompletedSegmentCount
    {
        get;
    }
    public int DeviceCount
    {
        get;
    }
    public int FunctionReadCount
    {
        get;
    }
    public int RequiredDeviceCapacity
    {
        get;
    }
    public int RequiredScratchCapacity
    {
        get;
    }
    public int FailingSegmentIndex
    {
        get;
    }
    public PciEcamSegment FailingSegment
    {
        get;
    }
    public PciBusScanResult NestedResult
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PciEcamSegmentScanStatus.Success;

    public static PciEcamSegmentScanResult Success(int completedSegmentCount, int deviceCount, int functionReadCount)
    {
        return new PciEcamSegmentScanResult(PciEcamSegmentScanStatus.Success, completedSegmentCount, deviceCount, functionReadCount, requiredDeviceCapacity: deviceCount, requiredScratchCapacity: 0, NoFailingSegmentIndex, default, default, isInitialized: true);
    }

    public static PciEcamSegmentScanResult Fail(PciEcamSegmentScanStatus status, int completedSegmentCount = 0, int deviceCount = 0, int functionReadCount = 0, int requiredDeviceCapacity = 0, int requiredScratchCapacity = 0, int failingSegmentIndex = NoFailingSegmentIndex, PciEcamSegment failingSegment = default, PciBusScanResult nestedResult = default)
    {
        return new PciEcamSegmentScanResult(status, completedSegmentCount, deviceCount, functionReadCount, requiredDeviceCapacity, requiredScratchCapacity, failingSegmentIndex, failingSegment, nestedResult, isInitialized: true);
    }
}
