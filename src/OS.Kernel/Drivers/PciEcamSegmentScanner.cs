namespace OS.Drivers;

public enum PciEcamSegmentScanStatus
{
    Success = 0,
    MissingReader = 1,
    InvalidSegmentRange = 2,
    ScratchTooSmall = 3,
    DestinationTooSmall = 4,
    NestedScannerFailed = 5
}

public readonly struct PciEcamScannedDevice
{
    public PciEcamScannedDevice(
        int segmentIndex,
        PciEcamSegment segment,
        PciScannedDevice device)
    {
        SegmentIndex = segmentIndex;
        Segment = segment;
        Device = device;
    }

    public int SegmentIndex { get; }

    public PciEcamSegment Segment { get; }

    public PciScannedDevice Device { get; }

    public ulong EcamBasePhysicalAddress => Segment.BasePhysicalAddress;

    public ushort SegmentGroup => Segment.SegmentGroup;

    public byte SegmentStartBus => Segment.StartBus;

    public byte SegmentEndBus => Segment.EndBus;

    public byte Bus => Device.Bus;

    public byte DeviceNumber => Device.Device;

    public byte Function => Device.Function;

    public ushort VendorId => Device.VendorId;

    public ushort DeviceId => Device.DeviceId;

    public byte RevisionId => Device.RevisionId;

    public byte ClassCode => Device.ClassCode;

    public byte Subclass => Device.Subclass;

    public byte ProgrammingInterface => Device.ProgrammingInterface;

    public byte HeaderType => Device.HeaderType;

    public bool IsMultifunction => Device.IsMultifunction;

    public bool HasCapabilitiesList => Device.HasCapabilitiesList;

    public byte CapabilityPointer => Device.CapabilityPointer;
}

public readonly struct PciEcamSegmentScanResult
{
    private PciEcamSegmentScanResult(
        PciEcamSegmentScanStatus status,
        int completedSegmentCount,
        int deviceCount,
        int functionReadCount,
        int requiredDeviceCapacity,
        int requiredScratchCapacity,
        int failingSegmentIndex,
        PciEcamSegment failingSegment,
        PciBusScanResult nestedResult,
        bool isInitialized)
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

    public PciEcamSegmentScanStatus Status { get; }

    public int CompletedSegmentCount { get; }

    public int DeviceCount { get; }

    public int FunctionReadCount { get; }

    public int RequiredDeviceCapacity { get; }

    public int RequiredScratchCapacity { get; }

    public int FailingSegmentIndex { get; }

    public PciEcamSegment FailingSegment { get; }

    public PciBusScanResult NestedResult { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == PciEcamSegmentScanStatus.Success;

    public static PciEcamSegmentScanResult Success(
        int completedSegmentCount,
        int deviceCount,
        int functionReadCount)
    {
        return new PciEcamSegmentScanResult(
            PciEcamSegmentScanStatus.Success,
            completedSegmentCount,
            deviceCount,
            functionReadCount,
            requiredDeviceCapacity: deviceCount,
            requiredScratchCapacity: 0,
            NoFailingSegmentIndex,
            default,
            default,
            isInitialized: true);
    }

    public static PciEcamSegmentScanResult Fail(
        PciEcamSegmentScanStatus status,
        int completedSegmentCount = 0,
        int deviceCount = 0,
        int functionReadCount = 0,
        int requiredDeviceCapacity = 0,
        int requiredScratchCapacity = 0,
        int failingSegmentIndex = NoFailingSegmentIndex,
        PciEcamSegment failingSegment = default,
        PciBusScanResult nestedResult = default)
    {
        return new PciEcamSegmentScanResult(
            status,
            completedSegmentCount,
            deviceCount,
            functionReadCount,
            requiredDeviceCapacity,
            requiredScratchCapacity,
            failingSegmentIndex,
            failingSegment,
            nestedResult,
            isInitialized: true);
    }
}

public static class PciEcamSegmentScanner
{
    public static PciEcamSegmentScanResult Scan(
        ReadOnlySpan<PciEcamSegment> segments,
        PciConfigurationSpaceHeaderReader? reader,
        Span<PciEcamScannedDevice> destination,
        Span<PciScannedDevice> scratch)
    {
        if (reader is null)
        {
            return PciEcamSegmentScanResult.Fail(PciEcamSegmentScanStatus.MissingReader);
        }

        if (scratch.Length < destination.Length)
        {
            return PciEcamSegmentScanResult.Fail(
                PciEcamSegmentScanStatus.ScratchTooSmall,
                requiredScratchCapacity: destination.Length);
        }

        int deviceCount = 0;
        int functionReadCount = 0;

        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            PciEcamSegment segment = segments[segmentIndex];
            if (segment.StartBus > segment.EndBus)
            {
                return PciEcamSegmentScanResult.Fail(
                    PciEcamSegmentScanStatus.InvalidSegmentRange,
                    completedSegmentCount: segmentIndex,
                    deviceCount: deviceCount,
                    functionReadCount: functionReadCount,
                    failingSegmentIndex: segmentIndex,
                    failingSegment: segment);
            }

            int destinationRemaining = destination.Length - deviceCount;
            PciBusScanResult nestedResult = PciBusScanner.Scan(
                new PciBusScanRange(segment.StartBus, segment.EndBus),
                reader,
                scratch.Slice(0, destinationRemaining));

            int copiedDeviceCount = CopySegmentDevices(
                segmentIndex,
                segment,
                scratch,
                destination.Slice(deviceCount),
                nestedResult.DeviceCount);
            deviceCount += copiedDeviceCount;
            functionReadCount += nestedResult.FunctionReadCount;

            if (!nestedResult.Succeeded)
            {
                PciEcamSegmentScanStatus status =
                    nestedResult.Status == PciBusScanStatus.DestinationTooSmall
                        ? PciEcamSegmentScanStatus.DestinationTooSmall
                        : PciEcamSegmentScanStatus.NestedScannerFailed;

                return PciEcamSegmentScanResult.Fail(
                    status,
                    completedSegmentCount: segmentIndex,
                    deviceCount: deviceCount,
                    functionReadCount: functionReadCount,
                    requiredDeviceCapacity: CalculateRequiredDeviceCapacity(
                        deviceCount,
                        copiedDeviceCount,
                        nestedResult),
                    failingSegmentIndex: segmentIndex,
                    failingSegment: segment,
                    nestedResult: nestedResult);
            }
        }

        return PciEcamSegmentScanResult.Success(
            segments.Length,
            deviceCount,
            functionReadCount);
    }

    private static int CopySegmentDevices(
        int segmentIndex,
        PciEcamSegment segment,
        ReadOnlySpan<PciScannedDevice> source,
        Span<PciEcamScannedDevice> destination,
        int requestedCount)
    {
        int copyCount = requestedCount;
        if (copyCount > destination.Length)
        {
            copyCount = destination.Length;
        }

        for (int index = 0; index < copyCount; index++)
        {
            destination[index] = new PciEcamScannedDevice(
                segmentIndex,
                segment,
                source[index]);
        }

        return copyCount;
    }

    private static int CalculateRequiredDeviceCapacity(
        int deviceCountAfterCopy,
        int copiedDeviceCount,
        PciBusScanResult nestedResult)
    {
        if (nestedResult.Status != PciBusScanStatus.DestinationTooSmall)
        {
            return deviceCountAfterCopy;
        }

        return deviceCountAfterCopy - copiedDeviceCount + nestedResult.RequiredDeviceCapacity;
    }
}
