using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct VirtioNetPciBinding
{
    public readonly uint Identity;
    public readonly ushort OriginalCommand;
    public readonly byte Bus, Device, Function;
    public readonly VirtioPciCapabilitySet Capabilities;
    internal VirtioNetPciBinding(ushort command, byte bus, byte device, byte function, VirtioPciCapabilitySet capabilities)
    {
        Identity = LiveVirtioNetPCIDevice.ModernNetworkIdentity;
        OriginalCommand = command;
        Bus = bus;
        Device = device;
        Function = function;
        Capabilities = capabilities;
    }
}
