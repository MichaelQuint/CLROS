using OS.Kernel.Platform;

namespace OS.Drivers;
public static class PciEcamPhysicalHeaderReader
{
    public static PciEcamPhysicalHeaderReadResult ReadType0Header(PciEcamSegment segment, IPlatformPhysicalMemory? physicalMemory, byte bus, byte device, byte function, Span<byte> destination)
    {
        if (physicalMemory is null)
        {
            return PciEcamPhysicalHeaderReadResult.MissingPhysicalMemory();
        }

        PciEcamConfigurationAddressResult addressResult = PciEcamConfigurationSpace.CalculateAddress(segment, bus, device, function, registerOffset: 0, accessWidth: 4);
        if (!addressResult.Succeeded)
        {
            return PciEcamPhysicalHeaderReadResult.AddressPlanningFailed(addressResult);
        }

        if (destination.Length < PciConfigurationHeader.Type0HeaderLength)
        {
            return PciEcamPhysicalHeaderReadResult.DestinationTooSmall(addressResult);
        }

        PlatformPhysicalMemoryResult physicalRead = physicalMemory.ReadBytes(addressResult.PhysicalAddress, PciConfigurationHeader.Type0HeaderLength, destination.Slice(0, PciConfigurationHeader.Type0HeaderLength));
        if (!physicalRead.Succeeded)
        {
            return PciEcamPhysicalHeaderReadResult.PhysicalReadFailed(addressResult, physicalRead);
        }

        return PciEcamPhysicalHeaderReadResult.Success(addressResult, physicalRead);
    }
}
