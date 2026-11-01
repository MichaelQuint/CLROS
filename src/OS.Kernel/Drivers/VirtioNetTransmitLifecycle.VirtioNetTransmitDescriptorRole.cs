namespace OS.Drivers;
using OS.Std.Binary;

public enum VirtioNetTransmitDescriptorRole
{
    VirtioHeader = 0,
    EthernetFrame = 1
}
