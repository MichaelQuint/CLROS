namespace OS.Drivers;

public readonly struct PciBusScanResult
{
    private PciBusScanResult(PciBusScanStatus status, PciBusScanRange range, int deviceCount, int functionReadCount, int requiredDeviceCapacity, byte failingBus, byte failingDevice, byte failingFunction, PciConfigurationSpaceReadStatus readerStatus, PciConfigurationHeaderStatus headerStatus, bool isInitialized)
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

    public PciBusScanStatus Status
    {
        get;
    }
    public PciBusScanRange Range
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
    public byte FailingBus
    {
        get;
    }
    public byte FailingDevice
    {
        get;
    }
    public byte FailingFunction
    {
        get;
    }
    public PciConfigurationSpaceReadStatus ReaderStatus
    {
        get;
    }
    public PciConfigurationHeaderStatus HeaderStatus
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PciBusScanStatus.Success;

    public static PciBusScanResult Success(PciBusScanRange range, int deviceCount, int functionReadCount)
    {
        return new PciBusScanResult(PciBusScanStatus.Success, range, deviceCount, functionReadCount, requiredDeviceCapacity: deviceCount, failingBus: 0, failingDevice: 0, failingFunction: 0, PciConfigurationSpaceReadStatus.Success, PciConfigurationHeaderStatus.Success, isInitialized: true);
    }

    public static PciBusScanResult Fail(PciBusScanStatus status, PciBusScanRange range, int deviceCount = 0, int functionReadCount = 0, int requiredDeviceCapacity = 0, byte failingBus = 0, byte failingDevice = 0, byte failingFunction = 0, PciConfigurationSpaceReadStatus readerStatus = PciConfigurationSpaceReadStatus.Success, PciConfigurationHeaderStatus headerStatus = PciConfigurationHeaderStatus.Success)
    {
        return new PciBusScanResult(status, range, deviceCount, functionReadCount, requiredDeviceCapacity, failingBus, failingDevice, failingFunction, readerStatus, headerStatus, isInitialized: true);
    }
}
