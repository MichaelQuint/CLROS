namespace OS.Drivers;

public static partial class PciBusScanner
{
    public static PciBusScanResult Scan(PciBusScanRange range, PciConfigurationSpaceHeaderReader reader, Span<PciScannedDevice> destination)
    {
        PciBusScanStatus rangeStatus = ValidateRange(range);
        if (rangeStatus != PciBusScanStatus.Success)
        {
            return PciBusScanResult.Fail(rangeStatus, range);
        }

        if (reader is null)
        {
            return PciBusScanResult.Fail(PciBusScanStatus.MissingReader, range);
        }

        int deviceCount = 0;
        int functionReadCount = 0;
        Span<byte> headerBytes = stackalloc byte[PciConfigurationHeader.Type0HeaderLength];
        for (int busValue = range.StartBus; busValue <= range.EndBus; busValue++)
        {
            byte bus = (byte)busValue;
            for (int deviceValue = range.StartDevice; deviceValue <= range.EndDevice; deviceValue++)
            {
                byte device = (byte)deviceValue;
                FunctionScanOutcome functionZero = ScanFunction(range, reader, destination, ref deviceCount, ref functionReadCount, bus, device, function: 0, headerBytes, out PciType0ConfigurationHeader functionZeroHeader, out PciBusScanResult failedResult);
                if (functionZero == FunctionScanOutcome.Failed)
                {
                    return failedResult;
                }

                if (functionZero == FunctionScanOutcome.Absent || !functionZeroHeader.IsMultifunction || range.MaximumFunction == 0)
                {
                    continue;
                }

                for (int functionValue = 1; functionValue <= range.MaximumFunction; functionValue++)
                {
                    FunctionScanOutcome functionResult = ScanFunction(range, reader, destination, ref deviceCount, ref functionReadCount, bus, device, (byte)functionValue, headerBytes, out _, out failedResult);
                    if (functionResult == FunctionScanOutcome.Failed)
                    {
                        return failedResult;
                    }
                }
            }
        }

        return PciBusScanResult.Success(range, deviceCount, functionReadCount);
    }

    private static PciBusScanStatus ValidateRange(PciBusScanRange range)
    {
        if (range.StartBus > range.EndBus)
        {
            return PciBusScanStatus.InvalidBusRange;
        }

        if (range.StartDevice > range.EndDevice || range.EndDevice > PciEcamConfigurationSpace.MaximumDevice)
        {
            return PciBusScanStatus.InvalidDeviceRange;
        }

        if (range.MaximumFunction > PciEcamConfigurationSpace.MaximumFunction)
        {
            return PciBusScanStatus.InvalidFunctionLimit;
        }

        return PciBusScanStatus.Success;
    }

    private static FunctionScanOutcome ScanFunction(PciBusScanRange range, PciConfigurationSpaceHeaderReader reader, Span<PciScannedDevice> destination, ref int deviceCount, ref int functionReadCount, byte bus, byte device, byte function, Span<byte> headerBytes, out PciType0ConfigurationHeader header, out PciBusScanResult failedResult)
    {
        header = default;
        failedResult = default;
        headerBytes.Clear();
        PciConfigurationSpaceReadStatus readStatus = reader(bus, device, function, headerBytes);
        functionReadCount++;
        if (readStatus != PciConfigurationSpaceReadStatus.Success)
        {
            failedResult = PciBusScanResult.Fail(PciBusScanStatus.ReaderFailed, range, deviceCount, functionReadCount, failingBus: bus, failingDevice: device, failingFunction: function, readerStatus: readStatus);
            return FunctionScanOutcome.Failed;
        }

        PciConfigurationHeaderResult headerResult = PciConfigurationHeader.ParseType0(headerBytes);
        if (headerResult.Status == PciConfigurationHeaderStatus.InvalidVendor)
        {
            return FunctionScanOutcome.Absent;
        }

        if (!headerResult.Succeeded)
        {
            failedResult = PciBusScanResult.Fail(PciBusScanStatus.HeaderParseFailed, range, deviceCount, functionReadCount, failingBus: bus, failingDevice: device, failingFunction: function, headerStatus: headerResult.Status);
            return FunctionScanOutcome.Failed;
        }

        if (deviceCount >= destination.Length)
        {
            failedResult = PciBusScanResult.Fail(PciBusScanStatus.DestinationTooSmall, range, deviceCount, functionReadCount, requiredDeviceCapacity: deviceCount + 1, failingBus: bus, failingDevice: device, failingFunction: function);
            return FunctionScanOutcome.Failed;
        }

        header = headerResult.Header;
        destination[deviceCount] = CreateScannedDevice(bus, device, function, header);
        deviceCount++;
        return FunctionScanOutcome.Present;
    }

    private static PciScannedDevice CreateScannedDevice(byte bus, byte device, byte function, PciType0ConfigurationHeader header)
    {
        return new PciScannedDevice(bus, device, function, header.VendorId, header.DeviceId, header.RevisionId, header.ClassCode, header.Subclass, header.ProgrammingInterface, header.HeaderType, header.IsMultifunction, header.HasCapabilitiesList, header.CapabilityPointer);
    }
}
