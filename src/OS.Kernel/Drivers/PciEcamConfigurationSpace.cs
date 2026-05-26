namespace OS.Drivers;

public enum PciEcamConfigurationSpaceStatus
{
    Success = 0,
    InvalidBusRange = 1,
    BusOutsideSegment = 2,
    InvalidDevice = 3,
    InvalidFunction = 4,
    InvalidRegister = 5,
    InvalidAccessWidth = 6,
    InvalidRegisterAlignment = 7,
    AddressOverflow = 8
}

public readonly struct PciEcamSegment
{
    public PciEcamSegment(
        ulong basePhysicalAddress,
        ushort segmentGroup,
        byte startBus,
        byte endBus)
    {
        BasePhysicalAddress = basePhysicalAddress;
        SegmentGroup = segmentGroup;
        StartBus = startBus;
        EndBus = endBus;
    }

    public ulong BasePhysicalAddress { get; }

    public ushort SegmentGroup { get; }

    public byte StartBus { get; }

    public byte EndBus { get; }
}

public readonly struct PciEcamConfigurationAddressResult
{
    private PciEcamConfigurationAddressResult(
        bool isInitialized,
        PciEcamConfigurationSpaceStatus status,
        PciEcamSegment segment,
        byte bus,
        byte device,
        byte function,
        ushort registerOffset,
        byte accessWidth,
        ulong ecamOffset,
        ulong physicalAddress)
    {
        IsInitialized = isInitialized;
        Status = status;
        Segment = segment;
        Bus = bus;
        Device = device;
        Function = function;
        RegisterOffset = registerOffset;
        AccessWidth = accessWidth;
        EcamOffset = ecamOffset;
        PhysicalAddress = physicalAddress;
    }

    public bool IsInitialized { get; }

    public PciEcamConfigurationSpaceStatus Status { get; }

    public PciEcamSegment Segment { get; }

    public byte Bus { get; }

    public byte Device { get; }

    public byte Function { get; }

    public ushort RegisterOffset { get; }

    public byte AccessWidth { get; }

    public ulong EcamOffset { get; }

    public ulong PhysicalAddress { get; }

    public bool Succeeded =>
        IsInitialized && Status == PciEcamConfigurationSpaceStatus.Success;

    public static PciEcamConfigurationAddressResult Success(
        PciEcamSegment segment,
        byte bus,
        byte device,
        byte function,
        ushort registerOffset,
        byte accessWidth,
        ulong ecamOffset,
        ulong physicalAddress)
    {
        return new PciEcamConfigurationAddressResult(
            true,
            PciEcamConfigurationSpaceStatus.Success,
            segment,
            bus,
            device,
            function,
            registerOffset,
            accessWidth,
            ecamOffset,
            physicalAddress);
    }

    public static PciEcamConfigurationAddressResult Fail(
        PciEcamConfigurationSpaceStatus status,
        PciEcamSegment segment,
        byte bus,
        byte device,
        byte function,
        ushort registerOffset,
        byte accessWidth)
    {
        return new PciEcamConfigurationAddressResult(
            true,
            status,
            segment,
            bus,
            device,
            function,
            registerOffset,
            accessWidth,
            ecamOffset: 0,
            physicalAddress: 0);
    }
}

public static class PciEcamConfigurationSpace
{
    public const byte MaximumDevice = 31;
    public const byte MaximumFunction = 7;
    public const ushort FunctionConfigurationSpaceLength = 4096;
    public const ulong BytesPerBus = 1024UL * 1024UL;
    public const ulong BytesPerDevice = 32UL * 1024UL;
    public const ulong BytesPerFunction = 4UL * 1024UL;

    public static PciEcamConfigurationAddressResult CalculateAddress(
        PciEcamSegment segment,
        byte bus,
        byte device,
        byte function,
        ushort registerOffset)
    {
        return CalculateAddress(
            segment,
            bus,
            device,
            function,
            registerOffset,
            accessWidth: 1);
    }

    public static PciEcamConfigurationAddressResult CalculateAddress(
        PciEcamSegment segment,
        byte bus,
        byte device,
        byte function,
        ushort registerOffset,
        byte accessWidth)
    {
        PciEcamConfigurationSpaceStatus status = Validate(
            segment,
            bus,
            device,
            function,
            registerOffset,
            accessWidth);
        if (status != PciEcamConfigurationSpaceStatus.Success)
        {
            return PciEcamConfigurationAddressResult.Fail(
                status,
                segment,
                bus,
                device,
                function,
                registerOffset,
                accessWidth);
        }

        ulong busOffset = (ulong)(bus - segment.StartBus) * BytesPerBus;
        ulong deviceOffset = (ulong)device * BytesPerDevice;
        ulong functionOffset = (ulong)function * BytesPerFunction;

        if (!TryAdd(busOffset, deviceOffset, out ulong busDeviceOffset) ||
            !TryAdd(busDeviceOffset, functionOffset, out ulong functionBaseOffset) ||
            !TryAdd(functionBaseOffset, registerOffset, out ulong ecamOffset) ||
            !TryAdd(segment.BasePhysicalAddress, ecamOffset, out ulong physicalAddress))
        {
            return PciEcamConfigurationAddressResult.Fail(
                PciEcamConfigurationSpaceStatus.AddressOverflow,
                segment,
                bus,
                device,
                function,
                registerOffset,
                accessWidth);
        }

        return PciEcamConfigurationAddressResult.Success(
            segment,
            bus,
            device,
            function,
            registerOffset,
            accessWidth,
            ecamOffset,
            physicalAddress);
    }

    public static PciEcamConfigurationSpaceStatus Validate(
        PciEcamSegment segment,
        byte bus,
        byte device,
        byte function,
        ushort registerOffset,
        byte accessWidth)
    {
        if (segment.StartBus > segment.EndBus)
        {
            return PciEcamConfigurationSpaceStatus.InvalidBusRange;
        }

        if (bus < segment.StartBus || bus > segment.EndBus)
        {
            return PciEcamConfigurationSpaceStatus.BusOutsideSegment;
        }

        if (device > MaximumDevice)
        {
            return PciEcamConfigurationSpaceStatus.InvalidDevice;
        }

        if (function > MaximumFunction)
        {
            return PciEcamConfigurationSpaceStatus.InvalidFunction;
        }

        if (!IsAccessWidthSupported(accessWidth))
        {
            return PciEcamConfigurationSpaceStatus.InvalidAccessWidth;
        }

        if (registerOffset >= FunctionConfigurationSpaceLength ||
            registerOffset > FunctionConfigurationSpaceLength - accessWidth)
        {
            return PciEcamConfigurationSpaceStatus.InvalidRegister;
        }

        if ((registerOffset % accessWidth) != 0)
        {
            return PciEcamConfigurationSpaceStatus.InvalidRegisterAlignment;
        }

        return PciEcamConfigurationSpaceStatus.Success;
    }

    private static bool IsAccessWidthSupported(byte accessWidth)
    {
        return accessWidth == 1 || accessWidth == 2 || accessWidth == 4;
    }

    private static bool TryAdd(
        ulong left,
        ulong right,
        out ulong result)
    {
        if (left > ulong.MaxValue - right)
        {
            result = 0;
            return false;
        }

        result = left + right;
        return true;
    }
}
