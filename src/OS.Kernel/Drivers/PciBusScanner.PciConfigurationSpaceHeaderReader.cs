namespace OS.Drivers;

public delegate PciConfigurationSpaceReadStatus PciConfigurationSpaceHeaderReader(byte bus, byte device, byte function, Span<byte> destination);
