namespace OS.Drivers;

public enum PciConfigurationSpaceReadStatus
{
    Success = 0,
    UnsupportedTuple = 1,
    ReadFailed = 2,
    DestinationTooSmall = 3
}

public delegate PciConfigurationSpaceReadStatus PciConfigurationSpaceHeaderReader(
    byte bus,
    byte device,
    byte function,
    Span<byte> destination);

public enum PciBusScanStatus
{
    Success = 0,
    MissingReader = 1,
    InvalidBusRange = 2,
    InvalidDeviceRange = 3,
    InvalidFunctionLimit = 4,
    DestinationTooSmall = 5,
    ReaderFailed = 6,
    HeaderParseFailed = 7
}

public readonly struct PciBusScanRange
{
    public PciBusScanRange(
        byte startBus,
        byte endBus,
        byte startDevice = 0,
        byte endDevice = PciEcamConfigurationSpace.MaximumDevice,
        byte maximumFunction = PciEcamConfigurationSpace.MaximumFunction)
    {
        StartBus = startBus;
        EndBus = endBus;
        StartDevice = startDevice;
        EndDevice = endDevice;
        MaximumFunction = maximumFunction;
    }

    public byte StartBus { get; }

    public byte EndBus { get; }

    public byte StartDevice { get; }

    public byte EndDevice { get; }

    public byte MaximumFunction { get; }
}

public readonly struct PciScannedDevice
{
    public PciScannedDevice(
        byte bus,
        byte device,
        byte function,
        ushort vendorId,
        ushort deviceId,
        byte revisionId,
        byte classCode,
        byte subclass,
        byte programmingInterface,
        byte headerType,
        bool isMultifunction,
        bool hasCapabilitiesList,
        byte capabilityPointer)
    {
        Bus = bus;
        Device = device;
        Function = function;
        VendorId = vendorId;
        DeviceId = deviceId;
        RevisionId = revisionId;
        ClassCode = classCode;
        Subclass = subclass;
        ProgrammingInterface = programmingInterface;
        HeaderType = headerType;
        IsMultifunction = isMultifunction;
        HasCapabilitiesList = hasCapabilitiesList;
        CapabilityPointer = capabilityPointer;
    }

    public byte Bus { get; }

    public byte Device { get; }

    public byte Function { get; }

    public ushort VendorId { get; }

    public ushort DeviceId { get; }

    public byte RevisionId { get; }

    public byte ClassCode { get; }

    public byte Subclass { get; }

    public byte ProgrammingInterface { get; }

    public byte HeaderType { get; }

    public bool IsMultifunction { get; }

    public bool HasCapabilitiesList { get; }

    public byte CapabilityPointer { get; }
}

public readonly struct PciBusScanResult
{
    private PciBusScanResult(
        PciBusScanStatus status,
        PciBusScanRange range,
        int deviceCount,
        int functionReadCount,
        int requiredDeviceCapacity,
        byte failingBus,
        byte failingDevice,
        byte failingFunction,
        PciConfigurationSpaceReadStatus readerStatus,
        PciConfigurationHeaderStatus headerStatus,
        bool isInitialized)
    {
        Status = status;
        Range = range;
        DeviceCount = deviceCount;
        FunctionReadCount = functionReadCount;
        RequiredDeviceCapacity = requiredDeviceCapacity;
        FailingBus = failingBus;
        FailingDevice = failingDevice;
        FailingFunction = failingFunction;
        ReaderStatus = readerStatus;
        HeaderStatus = headerStatus;
        IsInitialized = isInitialized;
    }

    public PciBusScanStatus Status { get; }

    public PciBusScanRange Range { get; }

    public int DeviceCount { get; }

    public int FunctionReadCount { get; }

    public int RequiredDeviceCapacity { get; }

    public byte FailingBus { get; }

    public byte FailingDevice { get; }

    public byte FailingFunction { get; }

    public PciConfigurationSpaceReadStatus ReaderStatus { get; }

    public PciConfigurationHeaderStatus HeaderStatus { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == PciBusScanStatus.Success;

    public static PciBusScanResult Success(
        PciBusScanRange range,
        int deviceCount,
        int functionReadCount)
    {
        return new PciBusScanResult(
            PciBusScanStatus.Success,
            range,
            deviceCount,
            functionReadCount,
            requiredDeviceCapacity: deviceCount,
            failingBus: 0,
            failingDevice: 0,
            failingFunction: 0,
            PciConfigurationSpaceReadStatus.Success,
            PciConfigurationHeaderStatus.Success,
            isInitialized: true);
    }

    public static PciBusScanResult Fail(
        PciBusScanStatus status,
        PciBusScanRange range,
        int deviceCount = 0,
        int functionReadCount = 0,
        int requiredDeviceCapacity = 0,
        byte failingBus = 0,
        byte failingDevice = 0,
        byte failingFunction = 0,
        PciConfigurationSpaceReadStatus readerStatus = PciConfigurationSpaceReadStatus.Success,
        PciConfigurationHeaderStatus headerStatus = PciConfigurationHeaderStatus.Success)
    {
        return new PciBusScanResult(
            status,
            range,
            deviceCount,
            functionReadCount,
            requiredDeviceCapacity,
            failingBus,
            failingDevice,
            failingFunction,
            readerStatus,
            headerStatus,
            isInitialized: true);
    }
}

public static class PciBusScanner
{
    private enum FunctionScanOutcome
    {
        Present = 0,
        Absent = 1,
        Failed = 2
    }

    public static PciBusScanResult Scan(
        PciBusScanRange range,
        PciConfigurationSpaceHeaderReader reader,
        Span<PciScannedDevice> destination)
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
                FunctionScanOutcome functionZero = ScanFunction(
                    range,
                    reader,
                    destination,
                    ref deviceCount,
                    ref functionReadCount,
                    bus,
                    device,
                    function: 0,
                    headerBytes,
                    out PciType0ConfigurationHeader functionZeroHeader,
                    out PciBusScanResult failedResult);

                if (functionZero == FunctionScanOutcome.Failed)
                {
                    return failedResult;
                }

                if (functionZero == FunctionScanOutcome.Absent ||
                    !functionZeroHeader.IsMultifunction ||
                    range.MaximumFunction == 0)
                {
                    continue;
                }

                for (int functionValue = 1; functionValue <= range.MaximumFunction; functionValue++)
                {
                    FunctionScanOutcome functionResult = ScanFunction(
                        range,
                        reader,
                        destination,
                        ref deviceCount,
                        ref functionReadCount,
                        bus,
                        device,
                        (byte)functionValue,
                        headerBytes,
                        out _,
                        out failedResult);

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

        if (range.StartDevice > range.EndDevice ||
            range.EndDevice > PciEcamConfigurationSpace.MaximumDevice)
        {
            return PciBusScanStatus.InvalidDeviceRange;
        }

        if (range.MaximumFunction > PciEcamConfigurationSpace.MaximumFunction)
        {
            return PciBusScanStatus.InvalidFunctionLimit;
        }

        return PciBusScanStatus.Success;
    }

    private static FunctionScanOutcome ScanFunction(
        PciBusScanRange range,
        PciConfigurationSpaceHeaderReader reader,
        Span<PciScannedDevice> destination,
        ref int deviceCount,
        ref int functionReadCount,
        byte bus,
        byte device,
        byte function,
        Span<byte> headerBytes,
        out PciType0ConfigurationHeader header,
        out PciBusScanResult failedResult)
    {
        header = default;
        failedResult = default;

        headerBytes.Clear();
        PciConfigurationSpaceReadStatus readStatus = reader(bus, device, function, headerBytes);
        functionReadCount++;
        if (readStatus != PciConfigurationSpaceReadStatus.Success)
        {
            failedResult = PciBusScanResult.Fail(
                PciBusScanStatus.ReaderFailed,
                range,
                deviceCount,
                functionReadCount,
                failingBus: bus,
                failingDevice: device,
                failingFunction: function,
                readerStatus: readStatus);
            return FunctionScanOutcome.Failed;
        }

        PciConfigurationHeaderResult headerResult = PciConfigurationHeader.ParseType0(headerBytes);
        if (headerResult.Status == PciConfigurationHeaderStatus.InvalidVendor)
        {
            return FunctionScanOutcome.Absent;
        }

        if (!headerResult.Succeeded)
        {
            failedResult = PciBusScanResult.Fail(
                PciBusScanStatus.HeaderParseFailed,
                range,
                deviceCount,
                functionReadCount,
                failingBus: bus,
                failingDevice: device,
                failingFunction: function,
                headerStatus: headerResult.Status);
            return FunctionScanOutcome.Failed;
        }

        if (deviceCount >= destination.Length)
        {
            failedResult = PciBusScanResult.Fail(
                PciBusScanStatus.DestinationTooSmall,
                range,
                deviceCount,
                functionReadCount,
                requiredDeviceCapacity: deviceCount + 1,
                failingBus: bus,
                failingDevice: device,
                failingFunction: function);
            return FunctionScanOutcome.Failed;
        }

        header = headerResult.Header;
        destination[deviceCount] = CreateScannedDevice(bus, device, function, header);
        deviceCount++;
        return FunctionScanOutcome.Present;
    }

    private static PciScannedDevice CreateScannedDevice(
        byte bus,
        byte device,
        byte function,
        PciType0ConfigurationHeader header)
    {
        return new PciScannedDevice(
            bus,
            device,
            function,
            header.VendorId,
            header.DeviceId,
            header.RevisionId,
            header.ClassCode,
            header.Subclass,
            header.ProgrammingInterface,
            header.HeaderType,
            header.IsMultifunction,
            header.HasCapabilitiesList,
            header.CapabilityPointer);
    }
}
