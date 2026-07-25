using OS.Kernel.Platform;

namespace OS.Drivers;

public enum PciEcamPhysicalHeaderReadStatus
{
    Success = 0,
    MissingPhysicalMemory = 1,
    AddressPlanningFailed = 2,
    DestinationTooSmall = 3,
    PhysicalReadFailed = 4
}

public readonly struct PciEcamPhysicalHeaderReadResult
{
    private PciEcamPhysicalHeaderReadResult(
        PciEcamPhysicalHeaderReadStatus status,
        PciEcamConfigurationAddressResult addressResult,
        PlatformPhysicalMemoryStatus physicalReadStatus,
        int requiredBytes,
        int bytesRead,
        bool isInitialized)
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

    public bool Succeeded =>
        IsInitialized && Status == PciEcamPhysicalHeaderReadStatus.Success;

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

    public static PciEcamPhysicalHeaderReadResult Success(
        PciEcamConfigurationAddressResult addressResult,
        PlatformPhysicalMemoryResult physicalRead)
    {
        return new PciEcamPhysicalHeaderReadResult(
            PciEcamPhysicalHeaderReadStatus.Success,
            addressResult,
            physicalRead.Status,
            PciConfigurationHeader.Type0HeaderLength,
            physicalRead.BytesRead,
            isInitialized: true);
    }

    public static PciEcamPhysicalHeaderReadResult MissingPhysicalMemory()
    {
        return new PciEcamPhysicalHeaderReadResult(
            PciEcamPhysicalHeaderReadStatus.MissingPhysicalMemory,
            default,
            PlatformPhysicalMemoryStatus.SourceUnavailable,
            PciConfigurationHeader.Type0HeaderLength,
            0,
            isInitialized: true);
    }

    public static PciEcamPhysicalHeaderReadResult AddressPlanningFailed(
        PciEcamConfigurationAddressResult addressResult)
    {
        return new PciEcamPhysicalHeaderReadResult(
            PciEcamPhysicalHeaderReadStatus.AddressPlanningFailed,
            addressResult,
            PlatformPhysicalMemoryStatus.Unknown,
            PciConfigurationHeader.Type0HeaderLength,
            0,
            isInitialized: true);
    }

    public static PciEcamPhysicalHeaderReadResult DestinationTooSmall(
        PciEcamConfigurationAddressResult addressResult)
    {
        return new PciEcamPhysicalHeaderReadResult(
            PciEcamPhysicalHeaderReadStatus.DestinationTooSmall,
            addressResult,
            PlatformPhysicalMemoryStatus.DestinationTooSmall,
            PciConfigurationHeader.Type0HeaderLength,
            0,
            isInitialized: true);
    }

    public static PciEcamPhysicalHeaderReadResult PhysicalReadFailed(
        PciEcamConfigurationAddressResult addressResult,
        PlatformPhysicalMemoryResult physicalRead)
    {
        return new PciEcamPhysicalHeaderReadResult(
            PciEcamPhysicalHeaderReadStatus.PhysicalReadFailed,
            addressResult,
            physicalRead.Status,
            physicalRead.RequiredBytes,
            physicalRead.BytesRead,
            isInitialized: true);
    }
}

public static class PciEcamPhysicalHeaderReader
{
    public static PciEcamPhysicalHeaderReadResult ReadType0Header(
        PciEcamSegment segment,
        IPlatformPhysicalMemory? physicalMemory,
        byte bus,
        byte device,
        byte function,
        Span<byte> destination)
    {
        if (physicalMemory is null)
        {
            return PciEcamPhysicalHeaderReadResult.MissingPhysicalMemory();
        }

        PciEcamConfigurationAddressResult addressResult =
            PciEcamConfigurationSpace.CalculateAddress(
                segment,
                bus,
                device,
                function,
                registerOffset: 0,
                accessWidth: 4);

        if (!addressResult.Succeeded)
        {
            return PciEcamPhysicalHeaderReadResult.AddressPlanningFailed(addressResult);
        }

        if (destination.Length < PciConfigurationHeader.Type0HeaderLength)
        {
            return PciEcamPhysicalHeaderReadResult.DestinationTooSmall(addressResult);
        }

        PlatformPhysicalMemoryResult physicalRead = physicalMemory.ReadBytes(
            addressResult.PhysicalAddress,
            PciConfigurationHeader.Type0HeaderLength,
            destination.Slice(0, PciConfigurationHeader.Type0HeaderLength));

        if (!physicalRead.Succeeded)
        {
            return PciEcamPhysicalHeaderReadResult.PhysicalReadFailed(
                addressResult,
                physicalRead);
        }

        return PciEcamPhysicalHeaderReadResult.Success(addressResult, physicalRead);
    }
}

public sealed class PciEcamPhysicalConfigurationSpaceHeaderReader
{
    private readonly PciEcamSegment _segment;
    private readonly IPlatformPhysicalMemory? _physicalMemory;

    public PciEcamPhysicalConfigurationSpaceHeaderReader(
        PciEcamSegment segment,
        IPlatformPhysicalMemory? physicalMemory)
    {
        _segment = segment;
        _physicalMemory = physicalMemory;
    }

    public PciEcamPhysicalHeaderReadResult LastResult { get; private set; }

    public PciConfigurationSpaceReadStatus Read(
        byte bus,
        byte device,
        byte function,
        Span<byte> destination)
    {
        LastResult = PciEcamPhysicalHeaderReader.ReadType0Header(
            _segment,
            _physicalMemory,
            bus,
            device,
            function,
            destination);

        return LastResult.ToConfigurationSpaceReadStatus();
    }
}
