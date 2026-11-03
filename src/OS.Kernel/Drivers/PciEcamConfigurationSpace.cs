namespace OS.Drivers;

public static class PciEcamConfigurationSpace
{
    public const byte MaximumDevice = 31;
    public const byte MaximumFunction = 7;
    public const ushort FunctionConfigurationSpaceLength = 4096;
    public const ulong BytesPerBus = 1024UL * 1024UL;
    public const ulong BytesPerDevice = 32UL * 1024UL;
    public const ulong BytesPerFunction = 4UL * 1024UL;
    public static PciEcamConfigurationAddressResult CalculateAddress(PciEcamSegment segment, byte bus, byte device, byte function, ushort registerOffset)
    {
        return CalculateAddress(segment, bus, device, function, registerOffset, accessWidth: 1);
    }

    public static PciEcamConfigurationAddressResult CalculateAddress(PciEcamSegment segment, byte bus, byte device, byte function, ushort registerOffset, byte accessWidth)
    {
        PciEcamConfigurationSpaceStatus status = Validate(segment, bus, device, function, registerOffset, accessWidth);
        if (status != PciEcamConfigurationSpaceStatus.Success)
        {
            return PciEcamConfigurationAddressResult.Fail(status, segment, bus, device, function, registerOffset, accessWidth);
        }

        ulong busOffset = (ulong)(bus - segment.StartBus) * BytesPerBus;
        ulong deviceOffset = (ulong)device * BytesPerDevice;
        ulong functionOffset = (ulong)function * BytesPerFunction;
        if (!TryAdd(busOffset, deviceOffset, out ulong busDeviceOffset) || !TryAdd(busDeviceOffset, functionOffset, out ulong functionBaseOffset) || !TryAdd(functionBaseOffset, registerOffset, out ulong ecamOffset) || !TryAdd(segment.BasePhysicalAddress, ecamOffset, out ulong physicalAddress))
        {
            return PciEcamConfigurationAddressResult.Fail(PciEcamConfigurationSpaceStatus.AddressOverflow, segment, bus, device, function, registerOffset, accessWidth);
        }

        return PciEcamConfigurationAddressResult.Success(segment, bus, device, function, registerOffset, accessWidth, ecamOffset, physicalAddress);
    }

    public static PciEcamConfigurationSpaceStatus Validate(PciEcamSegment segment, byte bus, byte device, byte function, ushort registerOffset, byte accessWidth)
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

        if (registerOffset >= FunctionConfigurationSpaceLength || registerOffset > FunctionConfigurationSpaceLength - accessWidth)
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

    private static bool TryAdd(ulong left, ulong right, out ulong result)
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
