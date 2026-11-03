namespace OS.Drivers;

public static class PciEcamSegmentScanner
{
    public static PciEcamSegmentScanResult Scan(ReadOnlySpan<PciEcamSegment> segments, PciConfigurationSpaceHeaderReader? reader, Span<PciEcamScannedDevice> destination, Span<PciScannedDevice> scratch)
    {
        if (reader is null)
        {
            return PciEcamSegmentScanResult.Fail(PciEcamSegmentScanStatus.MissingReader);
        }

        if (scratch.Length < destination.Length)
        {
            return PciEcamSegmentScanResult.Fail(PciEcamSegmentScanStatus.ScratchTooSmall, requiredScratchCapacity: destination.Length);
        }

        int deviceCount = 0;
        int functionReadCount = 0;
        for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
        {
            PciEcamSegment segment = segments[segmentIndex];
            if (segment.StartBus > segment.EndBus)
            {
                return PciEcamSegmentScanResult.Fail(PciEcamSegmentScanStatus.InvalidSegmentRange, completedSegmentCount: segmentIndex, deviceCount: deviceCount, functionReadCount: functionReadCount, failingSegmentIndex: segmentIndex, failingSegment: segment);
            }

            int destinationRemaining = destination.Length - deviceCount;
            PciBusScanResult nestedResult = PciBusScanner.Scan(new PciBusScanRange(segment.StartBus, segment.EndBus), reader, scratch.Slice(0, destinationRemaining));
            int copiedDeviceCount = CopySegmentDevices(segmentIndex, segment, scratch, destination.Slice(deviceCount), nestedResult.DeviceCount);
            deviceCount += copiedDeviceCount;
            functionReadCount += nestedResult.FunctionReadCount;
            if (!nestedResult.Succeeded)
            {
                PciEcamSegmentScanStatus status = nestedResult.Status == PciBusScanStatus.DestinationTooSmall ? PciEcamSegmentScanStatus.DestinationTooSmall : PciEcamSegmentScanStatus.NestedScannerFailed;
                return PciEcamSegmentScanResult.Fail(status, completedSegmentCount: segmentIndex, deviceCount: deviceCount, functionReadCount: functionReadCount, requiredDeviceCapacity: CalculateRequiredDeviceCapacity(deviceCount, copiedDeviceCount, nestedResult), failingSegmentIndex: segmentIndex, failingSegment: segment, nestedResult: nestedResult);
            }
        }

        return PciEcamSegmentScanResult.Success(segments.Length, deviceCount, functionReadCount);
    }

    private static int CopySegmentDevices(int segmentIndex, PciEcamSegment segment, ReadOnlySpan<PciScannedDevice> source, Span<PciEcamScannedDevice> destination, int requestedCount)
    {
        int copyCount = requestedCount;
        if (copyCount > destination.Length)
        {
            copyCount = destination.Length;
        }

        for (int index = 0; index < copyCount; index++)
        {
            destination[index] = new PciEcamScannedDevice(segmentIndex, segment, source[index]);
        }

        return copyCount;
    }

    private static int CalculateRequiredDeviceCapacity(int deviceCountAfterCopy, int copiedDeviceCount, PciBusScanResult nestedResult)
    {
        if (nestedResult.Status != PciBusScanStatus.DestinationTooSmall)
        {
            return deviceCountAfterCopy;
        }

        return deviceCountAfterCopy - copiedDeviceCount + nestedResult.RequiredDeviceCapacity;
    }
}
