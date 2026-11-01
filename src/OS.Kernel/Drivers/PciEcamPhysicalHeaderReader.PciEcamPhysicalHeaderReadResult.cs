using OS.Kernel.Platform;

namespace OS.Drivers;
public readonly struct PciEcamPhysicalHeaderReadResult
{
    private PciEcamPhysicalHeaderReadResult(PciEcamPhysicalHeaderReadStatus status, PciEcamConfigurationAddressResult addressResult, PlatformPhysicalMemoryStatus physicalReadStatus, int requiredBytes, int bytesRead, bool isInitialized)
    {
        Status = status;
        AddressResult = addressResult;
        PhysicalReadStatus = physicalReadStatus;
        RequiredBytes = requiredBytes;
        BytesRead = bytesRead;
        IsInitialized = isInitialized;
    }

    public PciEcamPhysicalHeaderReadStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == PciEcamPhysicalHeaderReadStatus.Success;
    public PciEcamConfigurationAddressResult AddressResult { get; }
    public PciEcamConfigurationSpaceStatus AddressStatus => AddressResult.Status;
    public PlatformPhysicalMemoryStatus PhysicalReadStatus { get; }
    public int RequiredBytes { get; }
    public int BytesRead { get; }
    public ulong PhysicalAddress => AddressResult.PhysicalAddress;
    public byte Bus => AddressResult.Bus;
    public byte Device => AddressResult.Device;
    public byte Function => AddressResult.Function;

    public PciConfigurationSpaceReadStatus ToConfigurationSpaceReadStatus()
    {
        if (!IsInitialized)
        {
            return PciConfigurationSpaceReadStatus.ReadFailed;
        }

        return Status switch
        {
            PciEcamPhysicalHeaderReadStatus.Success => PciConfigurationSpaceReadStatus.Success,
            PciEcamPhysicalHeaderReadStatus.DestinationTooSmall => PciConfigurationSpaceReadStatus.DestinationTooSmall,
            PciEcamPhysicalHeaderReadStatus.AddressPlanningFailed => PciConfigurationSpaceReadStatus.UnsupportedTuple,
            _ => PciConfigurationSpaceReadStatus.ReadFailed
        };
    }

    public static PciEcamPhysicalHeaderReadResult Success(PciEcamConfigurationAddressResult addressResult, PlatformPhysicalMemoryResult physicalRead)
    {
        return new PciEcamPhysicalHeaderReadResult(PciEcamPhysicalHeaderReadStatus.Success, addressResult, physicalRead.Status, PciConfigurationHeader.Type0HeaderLength, physicalRead.BytesRead, isInitialized: true);
    }

    public static PciEcamPhysicalHeaderReadResult MissingPhysicalMemory()
    {
        return new PciEcamPhysicalHeaderReadResult(PciEcamPhysicalHeaderReadStatus.MissingPhysicalMemory, default, PlatformPhysicalMemoryStatus.SourceUnavailable, PciConfigurationHeader.Type0HeaderLength, 0, isInitialized: true);
    }

    public static PciEcamPhysicalHeaderReadResult AddressPlanningFailed(PciEcamConfigurationAddressResult addressResult)
    {
        return new PciEcamPhysicalHeaderReadResult(PciEcamPhysicalHeaderReadStatus.AddressPlanningFailed, addressResult, PlatformPhysicalMemoryStatus.Unknown, PciConfigurationHeader.Type0HeaderLength, 0, isInitialized: true);
    }

    public static PciEcamPhysicalHeaderReadResult DestinationTooSmall(PciEcamConfigurationAddressResult addressResult)
    {
        return new PciEcamPhysicalHeaderReadResult(PciEcamPhysicalHeaderReadStatus.DestinationTooSmall, addressResult, PlatformPhysicalMemoryStatus.DestinationTooSmall, PciConfigurationHeader.Type0HeaderLength, 0, isInitialized: true);
    }

    public static PciEcamPhysicalHeaderReadResult PhysicalReadFailed(PciEcamConfigurationAddressResult addressResult, PlatformPhysicalMemoryResult physicalRead)
    {
        return new PciEcamPhysicalHeaderReadResult(PciEcamPhysicalHeaderReadStatus.PhysicalReadFailed, addressResult, physicalRead.Status, physicalRead.RequiredBytes, physicalRead.BytesRead, isInitialized: true);
    }
}
