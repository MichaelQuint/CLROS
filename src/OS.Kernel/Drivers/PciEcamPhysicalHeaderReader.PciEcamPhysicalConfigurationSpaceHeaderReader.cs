using OS.Kernel.Platform;

namespace OS.Drivers;
public sealed class PciEcamPhysicalConfigurationSpaceHeaderReader
{
    private readonly PciEcamSegment _segment;
    private readonly IPlatformPhysicalMemory? _physicalMemory;
    public PciEcamPhysicalConfigurationSpaceHeaderReader(PciEcamSegment segment, IPlatformPhysicalMemory? physicalMemory)
    {
        _segment = segment;
        _physicalMemory = physicalMemory;
    }

    public PciEcamPhysicalHeaderReadResult LastResult { get; private set; }

    public PciConfigurationSpaceReadStatus Read(byte bus, byte device, byte function, Span<byte> destination)
    {
        LastResult = PciEcamPhysicalHeaderReader.ReadType0Header(_segment, _physicalMemory, bus, device, function, destination);
        return LastResult.ToConfigurationSpaceReadStatus();
    }
}
