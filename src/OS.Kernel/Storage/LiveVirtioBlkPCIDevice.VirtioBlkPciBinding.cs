using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public readonly struct VirtioBlkPciBinding
{
    public readonly uint Identity;
    public readonly ushort OriginalCommand;
    public readonly byte Bus, Device, Function;
    public readonly VirtioPciCapabilitySet Capabilities;
    internal VirtioBlkPciBinding(uint identity, ushort command, byte bus, byte device,
        byte function, VirtioPciCapabilitySet capabilities)
    {
        Identity = identity; OriginalCommand = command; Bus = bus; Device = device;
        Function = function; Capabilities = capabilities;
    }
}
